using System;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>
/// 多边形地块的属性集合。
///
/// 采用 SoA（并列数组）而不是结构体数组，理由与 FMG 用 <c>Float32Array / Uint8Array</c> 相同：
///   1. 生成与渲染都是按属性整列遍历（"给所有地块算高度"），SoA 的缓存局部性远好于 AoS；
///   2. 便于用 <c>Parallel.For</c> 分段并行而不产生伪共享；
///   3. 序列化时每列可以独立压缩，缓存体积可控。
///
/// 字段命名对齐现有栅格语义，迁移时 <c>elevation[x, y]</c> → <c>Fields.Height[cell]</c> 一一对应。
/// </summary>
public sealed class PolygonFields
{
    /// <summary>地块数量，等于 <see cref="PolygonGrid.Count"/>。</summary>
    public int Count { get; private init; }

    /// <summary>高程 0..1，对应旧 <c>float[,] elevation</c>。</summary>
    public float[] Height { get; private init; } = Array.Empty<float>();

    /// <summary>温度 0..1，对应旧 <c>float[,] temperature</c>。</summary>
    public float[] Temperature { get; private init; } = Array.Empty<float>();

    /// <summary>湿度 0..1，对应旧 <c>float[,] moisture</c>。</summary>
    public float[] Moisture { get; private init; } = Array.Empty<float>();

    /// <summary>河流流量，对应旧 <c>float[,] river</c>。</summary>
    public float[] River { get; private init; } = Array.Empty<float>();

    /// <summary>生物群系，取值 <c>BiomeType</c>。</summary>
    public byte[] Biome { get; private init; } = Array.Empty<byte>();

    /// <summary>岩石类型，取值 <c>RockType</c>。</summary>
    public byte[] Rock { get; private init; } = Array.Empty<byte>();

    /// <summary>矿产类型，取值 <c>OreType</c>。</summary>
    public byte[] Ore { get; private init; } = Array.Empty<byte>();

    /// <summary>所属板块编号，对应旧 <c>PlateResult.PlateIds</c>。</summary>
    public int[] PlateId { get; private init; } = Array.Empty<int>();

    /// <summary>板块边界类型，取值 <c>PlateBoundaryType</c>；只有板块图层会用到。</summary>
    public byte[] PlateBoundary { get; private init; } = Array.Empty<byte>();

    /// <summary>
    /// 地貌类型，取值 <c>Main.LandformType</c>。
    /// 目前由"质心像素 + 栅格分类器"得到（与交互路径同一口径），
    /// 改用 <c>Cells.C</c> 真实邻接是 P4 的事。
    /// </summary>
    public byte[] Landform { get; private init; } = Array.Empty<byte>();

    /// <summary>生态健康度 0..1，对应旧 <c>EcologySimulationResult.EcologyHealth</c>。</summary>
    public float[] EcologyHealth { get; private init; } = Array.Empty<float>();

    /// <summary>文明潜力 0..1，对应旧 <c>EcologySimulationResult.CivilizationPotential</c>。</summary>
    public float[] CivilizationPotential { get; private init; } = Array.Empty<float>();

    /// <summary>文明影响力 0..1，对应旧 <c>CivilizationSimulationResult.Influence</c>。</summary>
    public float[] Influence { get; private init; } = Array.Empty<float>();

    /// <summary>政体编号，-1 表示无归属，对应旧 <c>CivilizationSimulationResult.PolityId</c>。</summary>
    public short[] PolityId { get; private init; } = Array.Empty<short>();

    /// <summary>是否政体边界地块，对应旧 <c>CivilizationSimulationResult.BorderMask</c>。</summary>
    public bool[] BorderMask { get; private init; } = Array.Empty<bool>();

    /// <summary>是否贸易走廊地块，对应旧 <c>CivilizationSimulationResult.TradeRouteMask</c>。</summary>
    public bool[] TradeRouteMask { get; private init; } = Array.Empty<bool>();

    /// <summary>贸易流量 0..1，对应旧 <c>CivilizationSimulationResult.TradeFlow</c>。</summary>
    public float[] TradeFlow { get; private init; } = Array.Empty<float>();

    /// <summary>所属城市在 <c>Cities</c> 列表中的下标；-1 表示没有城市。</summary>
    public int[] CityId { get; private init; } = Array.Empty<int>();

    /// <summary>
    /// 最陡下降邻居的地块编号；-1 表示该地块是洼地或已经入海。
    /// 这是栅格河流算法（<c>RiverGenerator.SelectNextCell</c> 的 8 邻域择优）在地块层的等价物。
    /// </summary>
    public int[] Downslope { get; private init; } = Array.Empty<int>();

    /// <summary>累积汇流量（上游来水之和），用于按流量决定河道宽度。</summary>
    public float[] Flux { get; private init; } = Array.Empty<float>();

    /// <summary>创建一个全部取默认值的地块属性集合。</summary>
    public static PolygonFields Create(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "地块数量不能为负。");
        }

        var fields = new PolygonFields
        {
            Count = count,
            Height = new float[count],
            Temperature = new float[count],
            Moisture = new float[count],
            River = new float[count],
            Biome = new byte[count],
            Rock = new byte[count],
            Ore = new byte[count],
            PlateId = new int[count],
            PlateBoundary = new byte[count],
            Landform = new byte[count],
            EcologyHealth = new float[count],
            CivilizationPotential = new float[count],
            Influence = new float[count],
            PolityId = new short[count],
            BorderMask = new bool[count],
            TradeRouteMask = new bool[count],
            TradeFlow = new float[count],
            CityId = new int[count],
            Downslope = new int[count],
            Flux = new float[count],
        };

        // -1 是"无归属/无城市/无下游"的哨兵值，必须显式填充：
        // 默认值 0 会被误读成"属于第 0 号政体""流向第 0 号地块"。
        for (var i = 0; i < count; i++)
        {
            fields.PolityId[i] = -1;
            fields.CityId[i] = -1;
            fields.Downslope[i] = -1;
        }

        return fields;
    }
}
