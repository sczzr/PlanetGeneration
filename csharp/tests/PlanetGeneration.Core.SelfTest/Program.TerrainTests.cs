using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PlanetGeneration.Core.Cartography;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Cartography.Generator;
using PlanetGeneration.Core.Cartography.Planning;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;
using PlanetGeneration.Core.Layers;
using PlanetGeneration.Core.Simulation;

namespace PlanetGeneration.Core.SelfTest;

internal static partial class Program
{
    private static void TestMegaTerrainRegions()
    {
        var geom = PolygonGridBuilder.Create(WorldExtent.Default, 42, 2048, 8, out _);
        var fields = CellFields.Create(geom.Count);
        var seaLevel = 0.35f;

        // 设置模拟的基础地貌与生物群系场
        for (var i = 0; i < geom.Count; i++)
        {
            var cx = (float)geom.CentroidX[i];
            var cy = (float)geom.CentroidY[i];

            // 构造一条贯穿世界的巨大山脉（Andes/Himalaya style line）
            var distToMountainSpine = MathF.Abs(cy - (cx * 0.4f + 300f));
            if (distToMountainSpine < 55f)
            {
                fields.Height[i] = seaLevel + 0.42f;
                fields.Landform[i] = (byte)LandformType.Mountain;
                fields.Biome[i] = (byte)BiomeType.RockyMountain;
            }
            // 构造一片连续的大森林区域 (Amazon style forest)
            else if (cx > 800f && cx < 1600f && cy > 500f && cy < 900f)
            {
                fields.Height[i] = seaLevel + 0.15f;
                fields.Landform[i] = (byte)LandformType.Plain;
                fields.Biome[i] = (byte)BiomeType.TropicalRainForest;
                fields.Moisture[i] = 0.85f;
            }
            else
            {
                fields.Height[i] = seaLevel + 0.10f;
                fields.Landform[i] = (byte)LandformType.Plain;
                fields.Biome[i] = (byte)BiomeType.Grassland;
                fields.Moisture[i] = 0.4f;
            }
        }

        var options = new GenerationOptions
        {
            Seed = 42,
            TargetCellCount = 2048,
            SeaLevel = seaLevel
        };

        var megaRegions = MegaTerrainAnalyzer.Analyze(geom, fields, options);

        // 1. 验证大型地貌输出
        Assert(megaRegions.Count > 0, "大型地貌识别结果不应为空");

        // 2. 验证大型山脉被成功识别且提取了脊线 (Spine)
        var mountains = megaRegions.Where(r => r.Type == MegaTerrainType.MegaMountain).ToList();
        Assert(mountains.Count > 0, "应成功识别出大型山脉 (MegaMountain)");
        var primaryMountain = mountains[0];
        Assert(primaryMountain.Spine != null && primaryMountain.Spine.Count >= 3, "大型山脉必须提取包含至少 3 个节点的脊线 (Spine)");
        Assert(primaryMountain.MegaScore > 0f, "大型山脉评分必须大于 0");
        Assert(!string.IsNullOrWhiteSpace(primaryMountain.Name), "大型山脉必须被赋予题名");

        // 3. 验证大型森林被成功识别
        var forests = megaRegions.Where(r => r.Type == MegaTerrainType.MegaForest).ToList();
        Assert(forests.Count > 0, "应成功识别出大型森林 (MegaForest)");
        var primaryForest = forests[0];
        Assert(primaryForest.Cells.Length >= 5, "大型森林地块数量必须达到规模阈值");
        Assert(primaryForest.TotalArea > 0, "大型森林总面积必须大于 0");

        // 4. 验证平滑轮廓多边形
        Assert(primaryForest.SmoothedPolygons.Count > 0, "大型森林必须提取平滑边界多边形");
        Assert(primaryForest.SmoothedPolygons[0].Length >= 6, "平滑边界多边形必须包含平滑细分点");

        // 5. 验证 4 层渐变深度计算 (Core > Transition > Edge)
        var centerPoint = primaryForest.Centroid;
        var centerDepth = primaryForest.ComputeNormalizedDepth(centerPoint);
        Assert(centerDepth >= 0.35f, $"腹地中心点归一化深度应明显大于边缘，实际为: {centerDepth}");

        var edgePoint = primaryForest.SmoothedPolygons[0][0];
        var edgeDepth = primaryForest.ComputeNormalizedDepth(edgePoint);
        Assert(edgeDepth < 0.25f, $"边界顶点归一化深度应接近 0，实际为: {edgeDepth}");
    }

    private static void TestEarthGeomorphologyClassification()
    {
        // 1. 验证 25 种地球地貌枚举与元数据完备性
        var allLandforms = Enum.GetValues<LandformType>();
        Assert(allLandforms.Length == 25, $"地球地貌类型总数应为 25 种，实际为: {allLandforms.Length}");

        foreach (var lf in allLandforms)
        {
            var name = lf.GetDisplayName();
            var desc = lf.GetDescription();
            var catName = lf.GetCategoryName();
            var cost = lf.GetMovementCostMultiplier();

            Assert(!string.IsNullOrWhiteSpace(name) && name != "未定地貌", $"地貌 {lf} 显示名称未定义或非法");
            Assert(!string.IsNullOrWhiteSpace(desc) && desc != "—", $"地貌 {lf} 地质成因描述未定义或非法");
            Assert(!string.IsNullOrWhiteSpace(catName), $"地貌 {lf} 分类名称为空");
            Assert(cost >= 1.0f, $"地貌 {lf} 通达阻力系数异常: {cost}");
        }

        // 2. 构造测试网格验证地貌分类器
        const float seaLevel = 0.40f;
        var geom = PolygonGridBuilder.Create(new WorldExtent(2048, 1024), 999, 1024, 8, out _);
        var fields = CellFields.Create(geom.Count);

        // 构造倾斜与多样化环境场
        for (var i = 0; i < geom.Count; i++)
        {
            var nx = (float)(geom.CentroidX[i] / geom.Width);
            var ny = (float)(geom.CentroidY[i] / geom.Height);

            fields.Height[i] = 0.10f + 0.85f * nx;
            fields.Temperature[i] = 1f - MathF.Abs(ny - 0.5f) * 1.8f;
            fields.Moisture[i] = 0.1f + 0.8f * ny;
            fields.River[i] = (nx > 0.4f && nx < 0.6f && ny > 0.3f && ny < 0.7f) ? 0.25f : 0f;
            fields.Rock[i] = (byte)((i % 3) == 0 ? RockType.Sedimentary : (i % 3 == 1 ? RockType.Igneous : RockType.Metamorphic));
            fields.PlateBoundary[i] = (byte)((i % 10) == 1 ? PlateBoundaryType.Convergent : ((i % 10 == 2) ? PlateBoundaryType.Divergent : PlateBoundaryType.None));
        }

        var classified = new byte[geom.Count];
        PolygonLandformClassifier.ClassifyAll(geom, fields, seaLevel, 1.0f, classified);

        // 3. 验证海陆一致性守恒 (h < seaLevel <=> IsWater)
        for (var i = 0; i < geom.Count; i++)
        {
            var lf = (LandformType)classified[i];
            var isWaterHeight = fields.Height[i] < seaLevel;
            var isWaterLandform = lf.IsWater();
            Assert(isWaterHeight == isWaterLandform, $"地块 #{i} 海陆属性脱节: Height={fields.Height[i]}, SeaLevel={seaLevel}, Landform={lf}");
        }

        // 4. 验证确定性与可复现性
        var classified2 = new byte[geom.Count];
        PolygonLandformClassifier.ClassifyAll(geom, fields, seaLevel, 1.0f, classified2);
        Assert(classified.SequenceEqual(classified2), "相同输入两次地貌分类结果不一致");

        // 5. 验证地貌非退化性 (全图至少涌现 8 种以上地貌)
        var distinctTypes = classified.Distinct().Count();
        Assert(distinctTypes >= 8, $"全图涌现地貌种类过少 ({distinctTypes} 种)，分类器退化");

        // 6. 验证典型地球地貌物理判据定向测试
        // 6.1 俯冲消亡带水深处 -> Trench
        var cTrench = 10;
        fields.Height[cTrench] = 0.10f; // 深海 (seaLevel=0.40)
        fields.PlateBoundary[cTrench] = (byte)PlateBoundaryType.Convergent;
        var lfTrench = PolygonLandformClassifier.Classify(geom, fields, cTrench, seaLevel, 1.0f);
        Assert(lfTrench == LandformType.Trench, $"俯冲带深水地块应被判定为海沟 (Trench)，实际为: {lfTrench}");

        // 6.2 离散扩张脊水深处 -> MidOceanRidge
        var cRidge = 11;
        fields.Height[cRidge] = 0.25f; // 中度水深
        fields.PlateBoundary[cRidge] = (byte)PlateBoundaryType.Divergent;
        var lfRidge = PolygonLandformClassifier.Classify(geom, fields, cRidge, seaLevel, 1.0f);
        Assert(lfRidge == LandformType.MidOceanRidge, $"离散张裂洋底应被判定为洋中脊 (MidOceanRidge)，实际为: {lfRidge}");

        // 6.3 极高海拔险峰 -> Peak
        var cPeak = 20;
        fields.Height[cPeak] = 0.96f;
        var lfPeak = PolygonLandformClassifier.Classify(geom, fields, cPeak, seaLevel, 1.0f);
        Assert(lfPeak == LandformType.Peak, $"极高海拔地块应被判定为高山极峰 (Peak)，实际为: {lfPeak}");

        // 6.4 极寒低洼/高纬大陆 -> Glacier
        var cGlacier = 30;
        fields.Height[cGlacier] = 0.50f;
        fields.Temperature[cGlacier] = 0.03f; // 极寒
        var lfGlacier = PolygonLandformClassifier.Classify(geom, fields, cGlacier, seaLevel, 1.0f);
        Assert(lfGlacier == LandformType.Glacier, $"极寒陆地地块应被判定为冰川冰原 (Glacier)，实际为: {lfGlacier}");

        // 6.5 极干热平原 -> DesertDune
        var cDesert = 40;
        fields.Height[cDesert] = 0.52f;
        // 让邻居高度相近以消除 localRelief
        var nbStart = geom.CellNeighborStart[cDesert];
        var nbEnd = geom.CellNeighborStart[cDesert + 1];
        for (var k = nbStart; k < nbEnd; k++) fields.Height[geom.CellNeighbors[k]] = 0.52f;
        fields.Moisture[cDesert] = 0.08f;
        fields.Temperature[cDesert] = 0.85f;
        var lfDesert = PolygonLandformClassifier.Classify(geom, fields, cDesert, seaLevel, 1.0f);
        Assert(lfDesert == LandformType.DesertDune, $"极干热平坦陆地应被判定为沙漠沙丘 (DesertDune)，实际为: {lfDesert}");

        // 6.6 沉积岩湿热带中度起伏 -> Karst
        var cKarst = 50;
        fields.Height[cKarst] = 0.55f;
        fields.Rock[cKarst] = (byte)RockType.Sedimentary;
        fields.Moisture[cKarst] = 0.75f;
        fields.Temperature[cKarst] = 0.65f;
        var knollNbStart = geom.CellNeighborStart[cKarst];
        var knollNbEnd = geom.CellNeighborStart[cKarst + 1];
        for (var k = knollNbStart; k < knollNbEnd; k++)
        {
            fields.Height[geom.CellNeighbors[k]] = 0.55f; // 确认为内陆，消除孤岛效应
        }
        if (knollNbEnd > knollNbStart)
        {
            fields.Height[geom.CellNeighbors[knollNbStart]] = 0.58f; // 营造局部中度起伏 ~0.03
        }
        var lfKarst = PolygonLandformClassifier.Classify(geom, fields, cKarst, seaLevel, 1.0f);
        Assert(lfKarst == LandformType.Karst, $"沉积岩湿热起伏带应被判定为喀斯特峰林 (Karst)，实际为: {lfKarst}");

        // 6.7 孤立岛屿 (四面环海) -> Island
        var cIsland = 60;
        fields.Height[cIsland] = 0.46f;
        var islStart = geom.CellNeighborStart[cIsland];
        var islEnd = geom.CellNeighborStart[cIsland + 1];
        for (var k = islStart; k < islEnd; k++)
        {
            fields.Height[geom.CellNeighbors[k]] = 0.20f; // 全部邻居为海洋
        }
        var lfIsland = PolygonLandformClassifier.Classify(geom, fields, cIsland, seaLevel, 1.0f);
        Assert(lfIsland == LandformType.Island, $"四面环海陆块应被判定为孤立岛屿 (Island)，实际为: {lfIsland}");

        // 7. 验证 LandformOptions 动态调节灵敏度
        var lowOpts = new LandformOptions
        {
            WetlandAbundance = 0.2f,
            DesertDuneScale = 0.2f,
            PeakFrequency = 0.2f
        };
        var highOpts = new LandformOptions
        {
            WetlandAbundance = 2.5f,
            DesertDuneScale = 2.5f,
            PeakFrequency = 2.5f
        };
        var classifiedLow = new byte[geom.Count];
        var classifiedHigh = new byte[geom.Count];
        PolygonLandformClassifier.ClassifyAll(geom, fields, seaLevel, lowOpts, classifiedLow);
        PolygonLandformClassifier.ClassifyAll(geom, fields, seaLevel, highOpts, classifiedHigh);

        var wetLow = classifiedLow.Count(b => (LandformType)b == LandformType.Wetland);
        var wetHigh = classifiedHigh.Count(b => (LandformType)b == LandformType.Wetland);
        Assert(wetHigh >= wetLow, $"高湿地丰度设置下湿地数量 ({wetHigh}) 应大于等于低设置 ({wetLow})");

        var duneLow = classifiedLow.Count(b => (LandformType)b == LandformType.DesertDune);
        var duneHigh = classifiedHigh.Count(b => (LandformType)b == LandformType.DesertDune);
        Assert(duneHigh >= duneLow, $"高沙丘广度设置下沙丘数量 ({duneHigh}) 应大于等于低设置 ({duneLow})");

        // 8. 验证 GenerationOptions 缓存键对 LandformTuning 敏感
        var genOptA = new GenerationOptions { LandformTuning = new LandformOptions { WetlandAbundance = 1.0f } };
        var genOptB = new GenerationOptions { LandformTuning = new LandformOptions { WetlandAbundance = 1.5f } };
        Assert(genOptA.BuildCacheKey() != genOptB.BuildCacheKey(), "修改地貌微调参数后生成缓存键应发生改变");
    }
}
