using System;
using PlanetGeneration.Core.Domain;

namespace PlanetGeneration.Core.Simulation;

/// <summary>
/// 地块地貌分类器（纯领域无引擎依赖）。
/// 基于多边形真实邻接与局部起伏分析高程形态。
/// </summary>
public static class PolygonLandformClassifier
{
    private const float NeighborHeightDelta = 0.018f;

    public static LandformType Classify(
        CellGeometry geometry,
        CellFields fields,
        int cellId,
        float seaLevel,
        float basinSensitivity)
    {
        return Classify(geometry, fields, cellId, seaLevel, LandformOptions.Default with { BasinSensitivity = basinSensitivity });
    }

    public static LandformType Classify(
        CellGeometry geometry,
        CellFields fields,
        int cellId,
        float seaLevel,
        LandformOptions options)
    {
        var safeSea = Math.Clamp(seaLevel, 0.0001f, 0.9999f);
        var sensitivity = Math.Clamp(options.BasinSensitivity, 0.5f, 2.0f);
        var current = fields.Height[cellId];
        var plateBoundary = (PlateBoundaryType)fields.PlateBoundary[cellId];
        var rock = (RockType)fields.Rock[cellId];
        var t = fields.Temperature[cellId];
        var m = fields.Moisture[cellId];
        var r = fields.River[cellId];

        var start = geometry.CellNeighborStart[cellId];
        var end = geometry.CellNeighborStart[cellId + 1];
        var neighborCount = end - start;

        // ──────────────── 海洋水下地貌分支 ────────────────
        if (current < safeSea)
        {
            var depth = (safeSea - current) / Math.Max(safeSea, 0.0001f);
            var nearLand = false;

            for (var k = start; k < end; k++)
            {
                var nb = geometry.CellNeighbors[k];
                if (fields.Height[nb] >= safeSea)
                {
                    nearLand = true;
                    break;
                }
            }

            // 1. 海沟：板块聚合消亡带（俯冲带）或极端大洋深渊
            if ((plateBoundary == PlateBoundaryType.Convergent && depth > 0.35f) || depth > 0.82f)
            {
                return LandformType.Trench;
            }

            // 2. 洋中脊：板块离散张裂扩张脊的海底隆起山脊
            if (plateBoundary == PlateBoundaryType.Divergent && depth > 0.20f && depth < 0.65f)
            {
                return LandformType.MidOceanRidge;
            }

            // 3. 大陆架浅海：紧邻陆地边缘或深度较浅
            if (nearLand || depth < 0.12f)
            {
                return LandformType.ShallowOcean;
            }

            // 4. 深海盆地：深邃平坦洋盆
            if (depth > 0.45f)
            {
                return LandformType.DeepOcean;
            }

            // 5. 远海大洋
            return LandformType.Ocean;
        }

        // ──────────────── 陆地地貌分支 ────────────────
        if (neighborCount == 0)
        {
            return LandformType.Plain;
        }

        var relativeHeight = (current - safeSea) / Math.Max(1f - safeSea, 0.0001f);

        var minHeight = current;
        var maxHeight = current;
        var sum = 0f;
        var higherCount = 0;
        var lowerCount = 0;
        var seaNeighborCount = 0;

        for (var k = start; k < end; k++)
        {
            var neighbor = geometry.CellNeighbors[k];
            var height = fields.Height[neighbor];

            if (height < minHeight) minHeight = height;
            if (height > maxHeight) maxHeight = height;
            sum += height;

            if (height < safeSea) seaNeighborCount++;

            var diff = height - current;
            if (diff > NeighborHeightDelta) higherCount++;
            else if (diff < -NeighborHeightDelta) lowerCount++;
        }

        var nearSea = seaNeighborCount > 0;
        var islandThreshold = Math.Clamp(0.72f / Math.Max(options.IslandDensity, 0.2f), 0.40f, 0.95f);
        var isIsland = seaNeighborCount == neighborCount || (neighborCount >= 3 && (float)seaNeighborCount / neighborCount >= islandThreshold);
        var meanHeight = sum / neighborCount;
        var localRelief = maxHeight - minHeight;
        var depression = Math.Max(meanHeight - current, 0f);
        var slopeSignal = Math.Max(maxHeight - current, current - minHeight);
        var normalizedSensitivity = (sensitivity - 0.5f) / 1.5f;

        var higherFraction = higherCount / (float)neighborCount;
        var lowerFraction = lowerCount / (float)neighborCount;
        var enclosedByHigher = higherFraction >= Lerp(5f / 8f, 7f / 8f, normalizedSensitivity)
            && lowerFraction <= Lerp(2f / 8f, 0f, normalizedSensitivity);

        var worldH = geometry.Height <= 0d ? 1.0 : geometry.Height;
        var cy = geometry.CentroidY[cellId];
        var latitude = (float)Math.Abs((2d * cy / worldH) - 1d);
        var polarBand = Math.Clamp((latitude - 0.72f) / 0.28f, 0f, 1f);

        // 1. 孤立岛屿：被海水环绕的小块陆地
        if (isIsland && relativeHeight < 0.65f)
        {
            return LandformType.Island;
        }

        // 2. 冰川冰原地貌：极寒气候或高山雪线冰盖
        var glacierT = 0.06f * options.GlacierExtent;
        var glacierPolarBand = Math.Clamp(0.5f / Math.Max(options.GlacierExtent, 0.2f), 0.20f, 0.90f);
        var glacierSnowline = Math.Clamp(0.78f - (options.GlacierExtent - 1.0f) * 0.12f, 0.50f, 0.95f);
        if (t < glacierT || (polarBand > glacierPolarBand && t < 0.12f * options.GlacierExtent && m > 0.15f) || (relativeHeight > glacierSnowline && t < 0.09f * options.GlacierExtent))
        {
            return LandformType.Glacier;
        }

        // 3. 峡湾：高纬沿海深峻冰蚀海湾
        var fjordRelief = 0.035f / Math.Max(options.FjordDepth, 0.2f);
        var fjordSlope = 0.040f / Math.Max(options.FjordDepth, 0.2f);
        if (nearSea && (t < 0.32f || polarBand > 0.30f) && (localRelief > fjordRelief || slopeSignal > fjordSlope) && relativeHeight < 0.45f)
        {
            return LandformType.Fjord;
        }

        // 4. 高山极峰：极高海拔或极其险峻的群山巅峰
        var peakHeight = Math.Clamp(0.85f - (options.PeakFrequency - 1.0f) * 0.06f, 0.70f, 0.96f);
        var peakRelief = Math.Clamp(0.060f / Math.Max(options.PeakFrequency, 0.2f), 0.030f, 0.090f);
        if (relativeHeight > peakHeight || (relativeHeight > peakHeight - 0.11f && localRelief > peakRelief))
        {
            return LandformType.Peak;
        }

        // 5. 火山锥：火成岩孤立喷发体或构造带火山
        var volcanoRelief = 0.038f / Math.Max(options.VolcanoFrequency, 0.2f);
        if (rock == RockType.Igneous && (localRelief > volcanoRelief || (relativeHeight > 0.50f && plateBoundary != PlateBoundaryType.None)))
        {
            return LandformType.Volcano;
        }

        // 6. 地堑断裂谷：板块离散张裂形成的断陷谷地
        var riftRelief = 0.025f / Math.Max(options.RiftFrequency, 0.2f);
        if (plateBoundary == PlateBoundaryType.Divergent && (enclosedByHigher || depression > 0.006f) && localRelief > riftRelief)
        {
            return LandformType.RiftValley;
        }

        // 7. 河流峡谷：高地大河强力下切深谷
        var canyonRelief = Math.Clamp(0.030f / Math.Max(options.CanyonDepth, 0.2f), 0.015f, 0.060f);
        var canyonSlope = Math.Clamp(0.035f / Math.Max(options.CanyonDepth, 0.2f), 0.018f, 0.070f);
        if (relativeHeight > 0.32f && r > 0.05f && (localRelief > canyonRelief || slopeSignal > canyonSlope))
        {
            return LandformType.Canyon;
        }

        // 8. 河口三角洲：江河入海口泥沙受阻分流沉积
        var deltaR = Math.Clamp(0.08f / Math.Max(options.DeltaScale, 0.2f), 0.02f, 0.20f);
        var deltaH = Math.Clamp(0.09f * options.DeltaScale, 0.04f, 0.20f);
        if (nearSea && r > deltaR && relativeHeight < deltaH)
        {
            return LandformType.Delta;
        }

        // 9. 冲积平原：内陆大河中下游泛滥堆积沃野
        var fpR = Math.Clamp(0.12f / Math.Max(options.FloodplainScale, 0.2f), 0.04f, 0.25f);
        var fpH = Math.Clamp(0.28f * options.FloodplainScale, 0.12f, 0.45f);
        if (!nearSea && r > fpR && relativeHeight < fpH && localRelief < 0.022f * options.FloodplainScale)
        {
            return LandformType.Floodplain;
        }

        // 10. 湿地沼泽：低洼地表常年滞水草沼泥炭带
        var wetH = Math.Clamp(0.22f * options.WetlandAbundance, 0.10f, 0.40f);
        var wetM1 = Math.Clamp(0.68f / Math.Max(options.WetlandAbundance, 0.2f), 0.35f, 0.90f);
        var wetM2 = Math.Clamp(0.52f / Math.Max(options.WetlandAbundance, 0.2f), 0.25f, 0.80f);
        if (relativeHeight < wetH && localRelief < 0.018f * Math.Max(options.WetlandAbundance, 0.5f) && (m > wetM1 || (m > wetM2 && r > 0.04f)))
        {
            return LandformType.Wetland;
        }

        // 11. 盆地系统：环高低洼地势
        var basinHeightThreshold = Lerp(0.32f, 0.18f, normalizedSensitivity);
        var basinMoistureThreshold = Lerp(0.42f, 0.55f, normalizedSensitivity);
        var basinRiverThreshold = Lerp(0.02f, 0.05f, normalizedSensitivity);

        if (relativeHeight < basinHeightThreshold && enclosedByHigher)
        {
            if (m < 0.28f && r < 0.03f && depression > 0.005f)
            {
                return LandformType.DryBasin;
            }
            if (m > basinMoistureThreshold || r > basinRiverThreshold || depression > 0.008f)
            {
                return LandformType.Basin;
            }
        }

        var dryBasinHeightThreshold = basinHeightThreshold * 1.15f;
        if (relativeHeight < dryBasinHeightThreshold && enclosedByHigher && m < 0.28f && r < 0.03f && depression > 0.005f)
        {
            return LandformType.DryBasin;
        }

        // 12. 喀斯特峰林：可溶性碳酸盐岩（沉积岩）水热溶蚀石林
        var karstM = Math.Clamp(0.52f / Math.Max(options.KarstFrequency, 0.2f), 0.25f, 0.85f);
        var karstT = Math.Clamp(0.32f / Math.Max(options.KarstFrequency, 0.2f), 0.15f, 0.60f);
        var karstReliefMin = Math.Clamp(0.020f / Math.Max(options.KarstFrequency, 0.2f), 0.008f, 0.035f);
        if (rock == RockType.Sedimentary && m > karstM && t > karstT && localRelief >= karstReliefMin && localRelief <= 0.052f * Math.Max(options.KarstFrequency, 0.5f) && relativeHeight < 0.65f)
        {
            return LandformType.Karst;
        }

        // 13. 沙漠沙丘：干旱酷热风积流动沙丘
        var duneM = Math.Clamp(0.18f * options.DesertDuneScale, 0.05f, 0.45f);
        if (m < duneM && t > 0.30f && localRelief < 0.026f * options.DesertDuneScale && relativeHeight < 0.50f)
        {
            return LandformType.DesertDune;
        }

        // 14. 风蚀雅丹/荒原劣地：干旱半干旱风蚀水冲切割沟壑
        var badlandsM = Math.Clamp(0.30f * options.BadlandsFrequency, 0.10f, 0.55f);
        var badlandsRelief = Math.Clamp(0.026f / Math.Max(options.BadlandsFrequency, 0.2f), 0.012f, 0.050f);
        if (m < badlandsM && localRelief > badlandsRelief && relativeHeight < 0.65f)
        {
            return LandformType.Badlands;
        }

        // 15. 滨海海岸带：近海平缓陆缘
        if (nearSea && relativeHeight < 0.10f)
        {
            return LandformType.Coast;
        }

        // 16. 褶皱山地：隆起山脊
        if (relativeHeight > 0.70f || (relativeHeight > 0.56f && (localRelief > 0.040f || slopeSignal > 0.045f)))
        {
            return LandformType.Mountain;
        }

        // 17. 高原台地：高海拔平坦开阔地
        if (relativeHeight > 0.58f && localRelief < 0.026f * options.PlateauExtent)
        {
            return LandformType.Plateau;
        }

        // 18. 丘陵缓丘：和缓起伏地貌
        if (relativeHeight > 0.40f || localRelief > 0.024f)
        {
            return LandformType.Hill;
        }

        // 19. 内陆平原：常规开阔平原
        return LandformType.Plain;
    }

    public static void ClassifyAll(
        CellGeometry geometry,
        CellFields fields,
        float seaLevel,
        float basinSensitivity,
        byte[] destination)
    {
        ClassifyAll(geometry, fields, seaLevel, LandformOptions.Default with { BasinSensitivity = basinSensitivity }, destination);
    }

    public static void ClassifyAll(
        CellGeometry geometry,
        CellFields fields,
        float seaLevel,
        LandformOptions options,
        byte[] destination)
    {
        var count = geometry.Count;
        for (var cell = 0; cell < count; cell++)
        {
            destination[cell] = (byte)Classify(geometry, fields, cell, seaLevel, options);
        }
    }

    private static float Lerp(float from, float to, float t) => from + ((to - from) * Math.Clamp(t, 0f, 1f));
}
