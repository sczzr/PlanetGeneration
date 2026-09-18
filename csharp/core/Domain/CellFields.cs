using System;

namespace PlanetGeneration.Core.Domain;

/// <summary>
/// 按地块编号 (CellId) 索引的属性列（SoA 架构）。
///
/// 并列数组具备绝佳的缓存局部性与分段并行能力，
/// 支持根据不同阶段的属性变动仅局部克隆或刷新。
/// </summary>
public sealed class CellFields
{
    public int Count { get; private init; }

    public float[] Height { get; private init; } = Array.Empty<float>();
    public float[] Temperature { get; private init; } = Array.Empty<float>();
    public float[] Moisture { get; private init; } = Array.Empty<float>();
    public float[] WindX { get; private init; } = Array.Empty<float>();
    public float[] WindY { get; private init; } = Array.Empty<float>();
    public float[] River { get; private init; } = Array.Empty<float>();
    public byte[] Biome { get; private init; } = Array.Empty<byte>();
    public byte[] Rock { get; private init; } = Array.Empty<byte>();
    public byte[] Ore { get; private init; } = Array.Empty<byte>();
    public int[] PlateId { get; private init; } = Array.Empty<int>();
    public byte[] PlateBoundary { get; private init; } = Array.Empty<byte>();
    public byte[] Landform { get; private init; } = Array.Empty<byte>();

    public float[] EcologyHealth { get; private init; } = Array.Empty<float>();
    public float[] CivilizationPotential { get; private init; } = Array.Empty<float>();
    public float[] Influence { get; private init; } = Array.Empty<float>();
    public short[] PolityId { get; private init; } = Array.Empty<short>();
    public bool[] BorderMask { get; private init; } = Array.Empty<bool>();
    public bool[] TradeRouteMask { get; private init; } = Array.Empty<bool>();
    public float[] TradeFlow { get; private init; } = Array.Empty<float>();

    public int[] CityId { get; private init; } = Array.Empty<int>();
    public int[] Downslope { get; private init; } = Array.Empty<int>();
    public float[] Flux { get; private init; } = Array.Empty<float>();

    public static CellFields Create(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "地块数量不能为负数。");
        }

        var cityId = new int[count];
        var downslope = new int[count];
        var polityId = new short[count];
        for (var i = 0; i < count; i++)
        {
            cityId[i] = -1;
            downslope[i] = -1;
            polityId[i] = -1;
        }

        return new CellFields
        {
            Count = count,
            Height = new float[count],
            Temperature = new float[count],
            Moisture = new float[count],
            WindX = new float[count],
            WindY = new float[count],
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
            PolityId = polityId,
            BorderMask = new bool[count],
            TradeRouteMask = new bool[count],
            TradeFlow = new float[count],
            CityId = cityId,
            Downslope = downslope,
            Flux = new float[count],
        };
    }

    /// <summary>创建属性列的深拷贝副本。</summary>
    public CellFields Clone()
    {
        var copy = Create(Count);
        Array.Copy(Height, copy.Height, Count);
        Array.Copy(Temperature, copy.Temperature, Count);
        Array.Copy(Moisture, copy.Moisture, Count);
        Array.Copy(WindX, copy.WindX, Count);
        Array.Copy(WindY, copy.WindY, Count);
        Array.Copy(River, copy.River, Count);
        Array.Copy(Biome, copy.Biome, Count);
        Array.Copy(Rock, copy.Rock, Count);
        Array.Copy(Ore, copy.Ore, Count);
        Array.Copy(PlateId, copy.PlateId, Count);
        Array.Copy(PlateBoundary, copy.PlateBoundary, Count);
        Array.Copy(Landform, copy.Landform, Count);
        Array.Copy(EcologyHealth, copy.EcologyHealth, Count);
        Array.Copy(CivilizationPotential, copy.CivilizationPotential, Count);
        Array.Copy(Influence, copy.Influence, Count);
        Array.Copy(PolityId, copy.PolityId, Count);
        Array.Copy(BorderMask, copy.BorderMask, Count);
        Array.Copy(TradeRouteMask, copy.TradeRouteMask, Count);
        Array.Copy(TradeFlow, copy.TradeFlow, Count);
        Array.Copy(CityId, copy.CityId, Count);
        Array.Copy(Downslope, copy.Downslope, Count);
        Array.Copy(Flux, copy.Flux, Count);
        return copy;
    }
}
