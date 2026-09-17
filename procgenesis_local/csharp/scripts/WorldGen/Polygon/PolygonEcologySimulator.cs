using System;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>生态模拟的聚合结果（地块版）。</summary>
public sealed class PolygonEcologyResult
{
    /// <summary>陆地地块的平均生态健康度 0~1。</summary>
    public required float AvgEcologyHealth { get; init; }

    /// <summary>陆地地块的平均文明潜力 0~1。</summary>
    public required float AvgCivilizationPotential { get; init; }

    /// <summary>文明潜力达到萌发阈值的地块占陆地地块的百分比。</summary>
    public required float CivilizationEmergencePercent { get; init; }

    /// <summary>参与统计的陆地地块数。</summary>
    public required int LandCellCount { get; init; }
}

/// <summary>
/// 地块生态模拟：把逐像素的 <c>EcologySimulator</c> 搬到地块图上。
///
/// 这个模拟器是**闭式**的——每个像素（地块）只依赖自己的高度/温度/湿度/河流/群系，
/// 没有任何邻域运算。所以地块化几乎是逐行翻译，唯一需要换的是"逐像素噪声"：
/// 栅格版用 <c>HashNoise01(seed, x, y)</c> 给每个像素一点斑块差异，
/// 地块版用 <c>HashNoise01(seed, cellId)</c>，语义一致（都是"每单元一点的随机扰动"）。
///
/// 群系生产力表放在这里，栅格版 <c>EcologySimulator</c> 改为引用同一份表，
/// 两条路径因此不可能出现"同一群系两种生产力"。
/// </summary>
public static class PolygonEcologySimulator
{
    /// <summary>
    /// 群系生产力，下标为群系序号（与 <c>BiomeType</c> 逐项对应）。
    /// 栅格版与地块版共用这一份，避免两条路径的取值分叉。
    /// </summary>
    public static readonly float[] BiomeProductivity =
    {
        0.22f, // Ocean
        0.22f, // ShallowOcean
        0.63f, // Coastland
        0.04f, // Ice
        0.24f, // Tundra
        0.56f, // BorealForest
        0.51f, // Taiga
        0.35f, // Steppe
        0.70f, // Grassland
        0.57f, // Chaparral
        0.15f, // TemperateDesert
        0.79f, // TemperateSeasonalForest
        0.86f, // TemperateRainForest
        0.67f, // Savanna
        0.60f, // Shrubland
        0.11f, // TropicalDesert
        0.84f, // TropicalSeasonalForest
        0.93f, // TropicalRainForest
        0.18f, // RockyMountain
        0.10f, // SnowyMountain
        0.95f, // River
    };

    /// <summary>群系序号越界时的兜底生产力。</summary>
    public const float DefaultBiomeProductivity = 0.22f;

    /// <summary>文明潜力达到这个值算作"文明萌发区"。</summary>
    private const float EmergenceThreshold = 0.67f;

    /// <summary>海洋群系序号（Ocean / ShallowOcean）。</summary>
    private const byte ShallowOceanBiome = 1;

    /// <summary>
    /// 起伏度归一化标尺：相邻地块平均高差达到这个值即视为"极度险峻"（起伏度 = 1）。
    /// 在 0~1 的高程值域里，0.18 大约是"台地边缘"到"山脊带"的量级。
    /// </summary>
    private const float RuggednessScale = 0.18f;

    /// <summary>
    /// 运行地块生态模拟，结果写入 <c>Fields.EcologyHealth</c> 与 <c>Fields.CivilizationPotential</c>。
    /// </summary>
    /// <param name="grid">地块网格；需已采样好 Height / Temperature / Moisture / River / Biome。</param>
    /// <param name="seed">世界种子。</param>
    /// <param name="epoch">纪元。</param>
    /// <param name="speciesDiversity">物种多样性 0~100。</param>
    /// <param name="civilAggression">文明侵略性 0~100。</param>
    /// <param name="magicDensity">魔法密度 0~100。</param>
    /// <param name="seaLevel">海平面。</param>
    public static PolygonEcologyResult Simulate(
        PolygonGrid grid,
        int seed,
        int epoch,
        int speciesDiversity,
        int civilAggression,
        int magicDensity,
        float seaLevel)
    {
        var fields = grid.Fields;
        var count = grid.Count;

        var diversityNorm = Clamp01(speciesDiversity / 100f);
        var aggressionNorm = Clamp01(civilAggression / 100f);
        var magicNorm = Clamp01(magicDensity / 100f);
        var epochFactor = ComputeEpochFactor(epoch, diversityNorm);
        var safeSeaLevel = Math.Clamp(seaLevel, 0.0001f, 0.9999f);

        var conflictDrag = Lerp(0.10f, 0.58f, aggressionNorm);
        var magicDrift = 1f - (Math.Abs(magicNorm - 0.46f) * 1.35f);
        var arcaneModifier = Math.Clamp(0.86f + (0.22f * magicDrift), 0.70f, 1.08f);
        var epochEcologyScale = 0.55f + (0.45f * epochFactor);
        var diversityScale = 0.72f + (0.52f * diversityNorm);
        var epochCivilScale = 0.28f + (0.92f * epochFactor);

        var totalEcology = 0f;
        var totalCivilization = 0f;
        var landCells = 0;
        var emergenceCells = 0;

        for (var cell = 0; cell < count; cell++)
        {
            var biome = fields.Biome[cell];
            if (biome <= ShallowOceanBiome || fields.Height[cell] <= safeSeaLevel)
            {
                fields.EcologyHealth[cell] = 0f;
                fields.CivilizationPotential[cell] = 0f;
                continue;
            }

            var temperature = Clamp01(fields.Temperature[cell]);
            var moisture = Clamp01(fields.Moisture[cell]);
            var river = Clamp01(fields.River[cell]);
            var heightFromSea = Clamp01((fields.Height[cell] - safeSeaLevel) / Math.Max(1f - safeSeaLevel, 0.0001f));

            var temperatureSuitability = 1f - Math.Min(Math.Abs(temperature - 0.58f) * 1.7f, 1f);
            var moistureSuitability = 1f - Math.Min(Math.Abs(moisture - 0.56f) * 1.45f, 1f);
            var waterAccess = Clamp01((moisture * 0.72f) + (MathF.Sqrt(river) * 0.28f));

            // ── 稳定性：改看"局部起伏"而非"绝对海拔" ──
            //
            // 旧式 terrainStability = 1 - heightFromSea^1.25 把"高"直接等同于"不宜居"。
            // 这在栅格版里不明显（高程是渐变的），但地块版把它暴露成一个整块问题：
            // 高原内陆的稳定性掉到 0.47 左右，潜力被压到 0.21~0.30 之间，
            // 而文明阈值是 0.2588——**高原整块被地理条件本身排除在文明之外**，
            // 地图上表现为"沿海环状、内陆空心"。
            //
            // 真正阻碍定居的从来不是绝对高度，而是**地形起伏**：平坦的高原
            // （安第斯高原、青藏谷地）完全能承载稠密文明，陡峭的低山谷地却不能。
            // 地块模型恰好有条件算这个量——用真实邻接取平均高差，
            // 这是栅格 8 邻域版本做不到、也没想到要做的事。
            var ruggedness = ComputeRuggedness(grid, cell, count);
            // 起伏项主导，绝对海拔只留一点轻度修正（高寒、缺氧的边际效应）。
            var terrainStability = Clamp01(
                (1f - (ruggedness * 0.78f))
                - (heightFromSea * 0.14f));

            var biomeProductivity = GetBiomeProductivity(biome);

            var baseEcology = (biomeProductivity * 0.44f)
                + (temperatureSuitability * 0.21f)
                + (moistureSuitability * 0.19f)
                + (waterAccess * 0.16f);

            // 逐单元斑块扰动：栅格版按像素坐标取哈希，地块版按地块编号取。
            var patchNoise = HashNoise01(seed, cell);
            var localVariation = 0.87f + (0.26f * patchNoise);

            var ecology = Clamp01(baseEcology * epochEcologyScale * diversityScale * localVariation);

            var settlementSuitability = Clamp01(
                (ecology * 0.42f)
                + (waterAccess * 0.23f)
                + (terrainStability * 0.22f)
                + (temperatureSuitability * 0.13f));

            var civilization = Clamp01(
                settlementSuitability
                * epochCivilScale
                * (1f - (conflictDrag * (1f - (ecology * 0.35f))))
                * arcaneModifier);

            fields.EcologyHealth[cell] = ecology;
            fields.CivilizationPotential[cell] = civilization;

            totalEcology += ecology;
            totalCivilization += civilization;
            landCells++;

            if (civilization >= EmergenceThreshold)
            {
                emergenceCells++;
            }
        }

        return new PolygonEcologyResult
        {
            AvgEcologyHealth = landCells > 0 ? totalEcology / landCells : 0f,
            AvgCivilizationPotential = landCells > 0 ? totalCivilization / landCells : 0f,
            CivilizationEmergencePercent = landCells > 0 ? 100f * emergenceCells / landCells : 0f,
            LandCellCount = landCells,
        };
    }

    /// <summary>取群系生产力；序号越界时给兜底值而不是抛异常（缓存恢复过的世界可能有脏数据）。</summary>
    public static float GetBiomeProductivity(byte biome)
        => biome < BiomeProductivity.Length ? BiomeProductivity[biome] : DefaultBiomeProductivity;

    /// <summary>
    /// 局部地形起伏度 0~1：取该地块与真实邻居的平均高差，再按一个"险峻"标尺归一化。
    ///
    /// 这是地块模型特有的能力——栅格版的 8 邻域里，斜对角的像素距离是 √2 倍，
    /// 高差不能直接平均；而地块图的邻居是**真实共边的多边形**，平均高差有明确含义。
    ///
    /// 归一化标尺取 0.18：在 0~1 的高程值域里，相邻地块平均相差 0.18 已经是很陡的地形。
    /// 返回 0 表示完全平坦（高原台地、平原），返回 1 表示极度破碎（山脊、峡谷带）。
    /// </summary>
    private static float ComputeRuggedness(PolygonGrid grid, int cell, int count)
    {
        if (cell < 0 || cell >= count)
        {
            return 0f;
        }

        var start = grid.CellNeighborStart[cell];
        var end = grid.CellNeighborStart[cell + 1];
        if (end <= start)
        {
            // 孤立地块（理论上不该出现）：无法判断起伏，按平坦处理。
            return 0f;
        }

        var height = grid.Fields.Height[cell];
        var totalDelta = 0f;
        var neighbors = 0;
        for (var k = start; k < end; k++)
        {
            var neighbor = grid.CellNeighbors[k];
            if (neighbor < 0 || neighbor >= count)
            {
                continue;
            }

            totalDelta += MathF.Abs(grid.Fields.Height[neighbor] - height);
            neighbors++;
        }

        if (neighbors == 0)
        {
            return 0f;
        }

        var meanDelta = totalDelta / neighbors;
        return Clamp01(meanDelta / RuggednessScale);
    }

    /// <summary>纪元推进因子：越晚的纪元越接近 1（生态与文明逐步成熟）。</summary>
    private static float ComputeEpochFactor(int epoch, float diversityNorm)
    {
        var safeEpoch = Math.Max(epoch, 0);
        var speed = 0.010f + (diversityNorm * 0.016f);
        return 1f - MathF.Exp(-safeEpoch * speed);
    }

    /// <summary>按地块编号取 0~1 的哈希噪声。</summary>
    private static float HashNoise01(int seed, int cell)
    {
        var hash = (uint)seed;
        hash ^= (uint)cell * 374761393u;
        hash = (hash ^ (hash >> 13)) * 1274126177u;
        hash ^= hash >> 16;
        return (hash & 0x00FFFFFFu) / 16777215f;
    }

    private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

    private static float Lerp(float from, float to, float t) => from + ((to - from) * Math.Clamp(t, 0f, 1f));
}
