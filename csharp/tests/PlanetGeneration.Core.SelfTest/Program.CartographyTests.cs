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
    private static void TestFantasyCartographySystem()
    {
        var seaLevel = 0.35f;
        var geom = PolygonGridBuilder.Create(new WorldExtent(2048, 1024), 42, 2048, 8, out _);
        var fields = CellFields.Create(geom.Count);

        for (var i = 0; i < geom.Count; i++)
        {
            var nx = (float)(geom.CentroidX[i] / geom.Width);
            var ny = (float)(geom.CentroidY[i] / geom.Height);

            if (nx is > 0.3f and < 0.7f && ny is > 0.3f and < 0.7f)
            {
                fields.Height[i] = 0.78f;
                fields.Landform[i] = (byte)LandformType.Mountain;
                fields.Biome[i] = (byte)BiomeType.SnowyMountain;
            }
            else if (nx < 0.3f)
            {
                fields.Height[i] = 0.52f;
                fields.Landform[i] = (byte)LandformType.Plain;
                fields.Biome[i] = (byte)BiomeType.TemperateSeasonalForest;
            }
            else
            {
                fields.Height[i] = 0.20f;
                fields.Landform[i] = (byte)LandformType.Ocean;
                fields.Biome[i] = (byte)BiomeType.Ocean;
            }

            fields.Temperature[i] = 0.45f;
            fields.Moisture[i] = 0.65f;
        }

        PolygonTopologyBuilder.BuildDownslope(geom, fields);
        PolygonRiverBuilder.Generate(geom, fields, seaLevel, true, 1.2f);

        var options = new GenerationOptions
        {
            Seed = 42,
            TargetCellCount = 2048,
            SeaLevel = seaLevel,
            EnableCartographyDesigner = false
        };

        var megaRegions = MegaTerrainAnalyzer.Analyze(geom, fields, options);

        var settlements = new List<SettlementInfo>
        {
            new()
            {
                CellId = 10,
                Name = "天都城",
                Score = 95f,
                Rank = SettlementRank.CityState,
                Position = new PolyVec2(geom.CentroidX[10], geom.CentroidY[10])
            },
            new()
            {
                CellId = 20,
                Name = "云溪镇",
                Score = 65f,
                Rank = SettlementRank.Town,
                Position = new PolyVec2(geom.CentroidX[20], geom.CentroidY[20])
            }
        };

        // 1. 生成幻想制图层快照
        var snapshot = CartographyGenerator.Generate(geom, fields, options, megaRegions, settlements);

        Assert(snapshot != null, "制图层快照不应为空");
        Assert(snapshot.Regions.Count > 0, "区域视觉规则集合不应为空");
        Assert(snapshot.Regions.ContainsKey(0), "必须包含默认中原区域风格 (RegionId = 0)");

        // 2. 验证笔刷指令集合
        Assert(snapshot.Brushes.Count > 0, "笔刷绘制指令集不应为空");
        var hasMountains = snapshot.Brushes.Any(b => b.Type is BrushType.MountainMain or BrushType.MountainFar);
        Assert(hasMountains, "应包含山脉笔刷指令 (MountainMain / MountainFar)");

        var hasForest = snapshot.Brushes.Any(b => b.Type is BrushType.ForestCluster or BrushType.TreeGroup);
        Assert(hasForest, "应包含森林笔刷指令 (ForestCluster / TreeGroup)");

        var hasCity = snapshot.Brushes.Any(b => b.Type == BrushType.CityIcon);
        Assert(hasCity, "应包含古建聚落笔刷指令 (CityIcon)");

        // 3. 验证地标与标注
        Assert(snapshot.Landmarks.Count >= 2, "应包含聚落地标数据");
        Assert(snapshot.Landmarks.Any(l => l.Name == "天都城" && l.Type == LandmarkType.Capital), "应正确生成首都级地标");

        // 4. 验证图层绘制命令构建 (PainterCommands)
        Assert(snapshot.Commands.Count >= 7, "应生成至少 7 个层级的 PainterCommand");
        Assert(snapshot.Commands.Any(c => c.LayerId == PainterCommandBuilder.LayerMountain), "应包含山脉图层命令批次");
        Assert(snapshot.Commands.Any(c => c.LayerId == PainterCommandBuilder.LayerLandmark), "应包含地标图层命令批次");

        // 5. 验证 LayerRegistry 中的 PainterLayer 注册
        var painterLayers = LayerRegistry.GetAllPainterLayers();
        Assert(painterLayers.Count == 8, $"LayerRegistry 应包含 8 个 PainterLayer 图层定义，实际为 {painterLayers.Count}");
    }

    private static void TestPhase4ArtisticControl()
    {
        var geom = PolygonGridBuilder.Create(new WorldExtent(2048, 1024), 1024, 2048, 6, out _);
        var fields = CellFields.Create(geom.Count);
        var seaLevel = 0.35f;

        // 构造含有高山与森林的大陆
        for (var i = 0; i < geom.Count; i++)
        {
            var xNorm = (float)(geom.CentroidX[i] / geom.Width);
            var yNorm = (float)(geom.CentroidY[i] / geom.Height);
            var distFromCenter = MathF.Sqrt(MathF.Pow(xNorm - 0.5f, 2f) + MathF.Pow(yNorm - 0.5f, 2f));

            if (distFromCenter < 0.40f)
            {
                fields.Height[i] = seaLevel + 0.35f;
                fields.Landform[i] = (byte)LandformType.Plain;
                fields.Biome[i] = (byte)BiomeType.TemperateSeasonalForest;
                fields.Moisture[i] = 0.55f;
                fields.Temperature[i] = 0.50f;
            }
            else
            {
                fields.Height[i] = seaLevel - 0.15f;
                fields.Landform[i] = (byte)LandformType.Ocean;
            }
        }

        var options = new GenerationOptions
        {
            Seed = 1024,
            TargetCellCount = 2048,
            SeaLevel = seaLevel
        };

        var megaRegions = MegaTerrainAnalyzer.Analyze(geom, fields, options);
        var styles = RegionStyleGenerator.GenerateStyles(geom, fields, options, megaRegions);

        // 1. 验证 MountainRangePainter 2.0 (金字塔层级与空气透视)
        var mountainBrushes = MountainRangePainter.Paint(geom, fields, options, megaRegions, styles);
        if (mountainBrushes.Count > 0)
        {
            var mainPeaks = mountainBrushes.Count(b => b.Type == BrushType.MountainMain);
            var ridges = mountainBrushes.Count(b => b.Type == BrushType.MountainRidge);
            var farPeaks = mountainBrushes.Where(b => b.Type == BrushType.MountainFar).ToList();

            if (farPeaks.Count > 0)
            {
                Assert(farPeaks.All(p => p.Opacity <= 0.45f), "远山剪影 (MountainFar) 透明度必须 <= 0.45 以保证空气透视感");
            }
        }

        // 2. 验证 ForestTransitionPainter (林心、林缘与外缘草甸)
        var forestBrushes = ForestTransitionPainter.Paint(geom, fields, options, megaRegions, styles);
        Assert(forestBrushes.Count > 0, "ForestTransitionPainter 应生成森林过渡笔刷");
        var hasEdgeOrGroup = forestBrushes.Any(b => b.Type == BrushType.TreeGroup);
        Assert(hasEdgeOrGroup, "森林过渡系统应包含疏朗单木微丛 (TreeGroup)");

        // 3. 验证 PlainsBrushGenerator 军规：平原坚决不加树，增加草纹、微丘与沙丘
        // 构造陆地上的干旱沙漠单元测试沙丘生成
        var landCells = new List<int>();
        for (var i = 0; i < geom.Count; i++)
        {
            if (fields.Height[i] > seaLevel)
            {
                landCells.Add(i);
            }
        }
        for (var i = 0; i < Math.Min(80, landCells.Count); i++)
        {
            fields.Biome[landCells[i]] = (byte)BiomeType.TropicalDesert;
        }

        var cCapital = landCells.Count > 85 ? landCells[85] : landCells[0];
        var cTown = landCells.Count > 90 ? landCells[90] : landCells[1];
        var cVillage = landCells.Count > 95 ? landCells[95] : landCells[2];

        var settlements = new List<SettlementInfo>
        {
            new() { CellId = cCapital, Name = "神京", Score = 98f, Rank = SettlementRank.CityState, Position = new PolyVec2(geom.CentroidX[cCapital], geom.CentroidY[cCapital]) },
            new() { CellId = cTown, Name = "渔浦", Score = 60f, Rank = SettlementRank.Town, Position = new PolyVec2(geom.CentroidX[cTown], geom.CentroidY[cTown]) },
            new() { CellId = cVillage, Name = "桃源村", Score = 30f, Rank = SettlementRank.Hamlet, Position = new PolyVec2(geom.CentroidX[cVillage], geom.CentroidY[cVillage]) }
        };

        var plainsBrushes = PlainsBrushGenerator.GenerateBrushes(geom, fields, options, megaRegions, settlements, styles, mountainBrushes);
        var plainTrees = plainsBrushes.Count(b => b.Type is BrushType.TreeGroup or BrushType.ForestCluster);
        Assert(plainTrees == 0, $"平原微地貌画师坚决不允许生成任何树木，实际发现 {plainTrees} 棵树");

        var hasMeadowRipples = plainsBrushes.Any(b => b.Type == BrushType.GrassTussock);
        Assert(hasMeadowRipples, "平原微地貌画师应生成开阔草原风草细纹 (~~~~~)");

        var hasDunes = plainsBrushes.Any(b => b.Type == BrushType.DesertDune);
        Assert(hasDunes, "平原画师应在大漠区域生成流动新月沙丘 (DesertDune)");

        // 4. 验证 LandmarkGenerator 五级符号体系与智能标签过滤
        var (landmarks, landmarkBrushes) = LandmarkGenerator.GenerateLandmarks(geom, fields, options, megaRegions, settlements, styles);
        var capitalLandmark = landmarks.FirstOrDefault(l => l.Name == "神京");
        Assert(capitalLandmark != null, "必须包含首都地标");
        Assert(capitalLandmark.SettlementTier == CartographySettlementTier.Capital, "神京应归入 Capital 级别");
        Assert(capitalLandmark.ShowLabel == true, "天下国都必须显示题注标签");

        var capitalBrush = landmarkBrushes.FirstOrDefault(b => b.Tag == "神京");
        Assert(capitalBrush != null, "必须包含首都符号笔刷");
        Assert(capitalBrush.VariantKey == "symbol_capital", $"首都应采用古地图规范符号 symbol_capital (◎)，实际为 {capitalBrush.VariantKey}");
        Assert(capitalBrush.Scale <= 0.90f, $"首都符号尺寸应缩减至 <= 0.90，实际为 {capitalBrush.Scale}");

        var townBrush = landmarkBrushes.FirstOrDefault(b => b.Tag == "渔浦");
        Assert(townBrush != null, "必须包含城镇/津渡符号笔刷");
        Assert(townBrush.Scale <= 0.75f, $"城镇符号尺寸应缩减至 <= 0.75，实际为 {townBrush.Scale}");

        var villageLandmark = landmarks.FirstOrDefault(l => l.Name == "桃源村");
        Assert(villageLandmark != null, "必须包含村落地标");
        Assert(villageLandmark.SettlementTier == CartographySettlementTier.Village, "桃源村应归入 Village 级别");
        Assert(villageLandmark.ShowLabel == false, "寻常小村落应默认隐藏文字题名，彻底消除地图文字竞争");

        // 5. 验证调色盘低饱和与暖调
        Assert(CartographyColor.EmeraldGreen.G < 0.50f, "青绿调色板应降低纯绿饱和度");
        Assert(CartographyColor.ColdBlue.B < 0.65f, "黛蓝调色板应降低纯蓝饱和度");
        Assert(CartographyColor.WarmOchre.R > 0.70f, "暖赭石调色板色值正确");
    }

    private static void TestCartographyDesignerAndFantasyContinent01()
    {
        var seaLevel = 0.35f;
        var geom = PolygonGridBuilder.Create(new WorldExtent(2048, 1024), 8888, 2048, 8, out _);
        var fields = CellFields.Create(geom.Count);

        // 设置全图基础陆地与海岸，以便蓝图在真实网格中完成几何映射
        for (var i = 0; i < geom.Count; i++)
        {
            var nx = (float)(geom.CentroidX[i] / geom.Width);
            var ny = (float)(geom.CentroidY[i] / geom.Height);

            // 东南边缘与外侧设为海洋，内陆为广阔陆地
            if (nx > 0.90f || ny > 0.90f)
            {
                fields.Height[i] = seaLevel - 0.15f;
                fields.Landform[i] = (byte)LandformType.Ocean;
                fields.Biome[i] = (byte)BiomeType.Ocean;
            }
            else
            {
                fields.Height[i] = seaLevel + 0.15f;
                fields.Landform[i] = (byte)LandformType.Plain;
                fields.Biome[i] = (byte)BiomeType.Grassland;
                fields.Moisture[i] = 0.40f;
            }
        }

        var options = new GenerationOptions
        {
            Seed = 8888,
            TargetCellCount = 2048,
            SeaLevel = seaLevel,
            EnableCartographyDesigner = true,
            BlueprintName = "FantasyContinent01"
        };

        // 1. 测试导演编译器 CartographyDesigner.Design
        var blueprint = FantasyContinent01Blueprint.Create();
        Assert(blueprint != null, "FantasyContinent01Blueprint 蓝图创建不应为空");
        Assert(blueprint.MountainRanges.Count >= 2 && blueprint.MountainSpines.Count >= 2, "样板蓝图应包含至少 2 条主山系");
        Assert(blueprint.ForestMasses.Count >= 1 && blueprint.ForestZones.Count >= 1, "样板蓝图应包含林海区域");
        Assert(blueprint.ForestMasses[0].Clearings.Count >= 3, "太古青岚林海必须包含至少 3 处林间留白隙地 (Clearings)");
        Assert(blueprint.DesertFields.Count >= 1 && blueprint.DesertZones.Count >= 1, "样板蓝图应包含大漠瀚海区域");
        Assert(blueprint.RiverNetworks.Count >= 1 && blueprint.RiverCorridors.Count >= 1, "样板蓝图应包含树状母亲河水系");
        Assert(blueprint.RiverNetworks[0].Branches.Count >= 2, "天水龙江水系应包含至少 2 条主要支流");
        Assert(blueprint.PlainsDetail != null && blueprint.PlainsDetail.AgriculturalZones.Count >= 2, "样板蓝图应包含中央平原丰富度农田水网");
        Assert(blueprint.SettlementDefinitions.Count >= 6 && blueprint.Settlements.Count >= 6, "样板蓝图应包含至少 6 个关键战略聚落");
        Assert(blueprint.Highways.Count >= 4, "样板蓝图应包含至少 4 条干线商道");

        var plan = CartographyDesigner.Design(geom, fields, options, blueprint);
        Assert(plan != null, "导演层编译结果 Plan 不应为空");
        Assert(plan.MegaRegions.Count >= 4, $"导演层编译应生成至少 4 个大型地貌实体，实际为: {plan.MegaRegions.Count}");

        // 验证主山脉 "苍冥天脊"
        var tianji = plan.MegaRegions.FirstOrDefault(r => r.Type == MegaTerrainType.MegaMountain && r.Name == "苍冥天脊");
        Assert(tianji != null, "必须包含'苍冥天脊'大型山脉实体");
        Assert(tianji.Spine != null && tianji.Spine.Count >= 30, $"主山脉脊线节点数应充足 (>= 30)，实际为: {tianji.Spine?.Count}");
        Assert(tianji.Cells.Length > 0, "主山脉覆盖地块不应为空");

        // 验证主峰抬升与关隘鞍部
        var maxSpineElev = tianji!.Spine!.Max(n => n.Elevation);
        var minSpineElev = tianji.Spine!.Min(n => n.Elevation);
        Assert(maxSpineElev > seaLevel + 0.60f, $"主峰海拔必须显著抬升 (> 0.95)，实际为: {maxSpineElev}");
        Assert(minSpineElev < seaLevel + 0.40f, $"关隘鞍部海拔必须显著降低 (< 0.75)，实际为: {minSpineElev}");

        // 验证太古青岚林海
        var linHai = plan.MegaRegions.FirstOrDefault(r => r.Type == MegaTerrainType.MegaForest && r.Name == "太古青岚林海");
        Assert(linHai != null, "必须包含'太古青岚林海'大型森林实体");
        Assert(linHai.Cells.Length > 0, "林海覆盖地块不应为空");

        // 验证母亲河冲积
        var riverCellCount = 0;
        for (var i = 0; i < geom.Count; i++)
        {
            if (fields.River[i] > 0.1f) riverCellCount++;
        }
        Assert(riverCellCount >= 20, $"母亲河走廊应成功冲积生成至少 20 块河流单元，实际为: {riverCellCount}");

        // 验证战略聚落布局
        Assert(plan.Settlements.Count >= 6, $"战略聚落数应 >= 6，实际为: {plan.Settlements.Count}");
        var capital = plan.Settlements.FirstOrDefault(s => s.Name == "神京天都");
        Assert(capital != null, "必须成功锚定'神京天都'");
        Assert(capital.Rank == SettlementRank.CityState, "神京天都必须为天下国都 (CityState)");
        Assert(fields.Height[capital.CellId] > seaLevel, "神京天都必须位于陆地上");

        var pass = plan.Settlements.FirstOrDefault(s => s.Name == "雁门雄关");
        Assert(pass != null, "必须成功锚定'雁门雄关'");

        var harbor = plan.Settlements.FirstOrDefault(s => s.Name == "临海沧津");
        Assert(harbor != null, "必须成功锚定'临海沧津'");

        // 2. 测试 CartographyGenerator 全管线调度消费导演层成果
        var snapshot = CartographyGenerator.Generate(geom, fields, options, Array.Empty<MegaTerrainRegion>(), Array.Empty<SettlementInfo>(), blueprint);
        Assert(snapshot != null, "基于构图蓝图生成的 CartographySnapshot 不应为空");
        Assert(snapshot.Regions.Count >= 5, $"快照区域风格数应 >= 5，实际为: {snapshot.Regions.Count}");

        // 笔刷校验
        var mountainMainBrushes = snapshot.Brushes.Where(b => b.Type == BrushType.MountainMain).ToList();
        Assert(mountainMainBrushes.Count > 0, "必须生成主脉峰峦笔刷 (MountainMain)");

        var forestClusterBrushes = snapshot.Brushes.Where(b => b.Type == BrushType.ForestCluster).ToList();
        Assert(forestClusterBrushes.Count > 0, "必须生成林海大斑块笔刷 (ForestCluster)");

        var duneBrushes = snapshot.Brushes.Where(b => b.Type == BrushType.DesertDune).ToList();
        Assert(duneBrushes.Count > 0, "大漠区域必须生成流动沙垄笔刷 (DesertDune)");

        var tussockBrushes = snapshot.Brushes.Where(b => b.Type == BrushType.GrassTussock).ToList();
        Assert(tussockBrushes.Count > 0, "平原开阔地带必须生成微风草纹笔刷 (GrassTussock)");

        var fieldBrushes = snapshot.Brushes.Where(b => b.Type == BrushType.FieldTerraced).ToList();
        Assert(fieldBrushes.Count > 0, "中央平原都城周边必须生成农田水网水浇地笔刷 (FieldTerraced)");

        var smoothRivers = snapshot.Brushes.Where(b => b.Type == BrushType.RiverStroke && b.Points != null && b.Points.Length > 10).ToList();
        Assert(smoothRivers.Count > 0, "水系必须生成高密度平滑连续大江笔刷多段线 (RiverStroke with Points > 10)");

        var mirrorLakes = snapshot.Brushes.Where(b => b.Type == BrushType.LakePond).ToList();
        Assert(mirrorLakes.Count > 0, "必须生成清平水泽镜湖笔刷 (LakePond)");

        // 验证地标
        var capitalLandmark = snapshot.Landmarks.FirstOrDefault(l => l.Name == "神京天都");
        Assert(capitalLandmark != null, "制图层快照地标中必须包含'神京天都'");
        Assert(capitalLandmark.SettlementTier == CartographySettlementTier.Capital, "神京天都地标等级必须为 Capital");
        Assert(capitalLandmark.ShowLabel == true, "神京天都必须显示题名");
    }

    private static void TestNarrativeRegionsAnd6StagePipeline()
    {
        var geom = PolygonGridBuilder.Create(new WorldExtent(2048, 1024), 1024, 2048, 6, out _);
        var fields = CellFields.Create(geom.Count);
        var options = new GenerationOptions
        {
            Seed = 8888,
            TargetCellCount = 2048,
            SeaLevel = 0.35f,
            EnableCartographyDesigner = true
        };

        var blueprint = FantasyContinent01Blueprint.Create();
        var plan = CartographyDesigner.Design(geom, fields, options, blueprint);

        // 1. 验证 NarrativeRegions 大区数据契约
        Assert(plan.NarrativeRegions != null, "设计结果的 NarrativeRegions 不应为空");
        var nRegions = plan.NarrativeRegions!;
        Assert(nRegions.Count >= 4, $"叙事大区数量应 >= 4，实际为 {nRegions.Count}");

        // 验证山系大区与 MountainChainData
        var mtRegion = nRegions.FirstOrDefault(r => r.Type == NarrativeRegionType.MountainRange);
        Assert(mtRegion != null, "必须包含 MountainRange 叙事大区");
        Assert(mtRegion!.MountainChain != null, "MountainRange 必须包含 MountainChainData");
        var chain = mtRegion.MountainChain!;
        Assert(chain.SpineCurve.Count >= 30, $"主脊样条节点数应充足 (>= 30)，实际为: {chain.SpineCurve.Count}");
        Assert(chain.BranchSpurs.Count >= 4, $"山脉应生成向南舒展的侧向支脉 (>= 4)，实际为: {chain.BranchSpurs.Count}");
        Assert(chain.MajorPeakRatios.Count >= 2, "山系应包含雄峰节点比例");
        Assert(chain.PassRatios.Count >= 1, "山系应包含山门隘口节点比例");

        // 验证体块化林海大区与 ForestMassData
        var forestRegion = nRegions.FirstOrDefault(r => r.Type == NarrativeRegionType.ForestMass);
        Assert(forestRegion != null, "必须包含 ForestMass 叙事大区");
        Assert(forestRegion!.ForestMass != null, "ForestMass 必须包含 ForestMassData");
        var mass = forestRegion.ForestMass!;
        Assert(mass.HullPolygon.Count >= 24, $"有机起伏外轮廓点数应充足 (>= 24)，实际为: {mass.HullPolygon.Count}");
        Assert(mass.Clearings.Count >= 2, $"林海腹地必须开辟林间透气留白空地 (>= 2)，实际为: {mass.Clearings.Count}");
        Assert(mass.Clearings.All(c => c.Radius >= 20.0f), "林间空地半径应充足 (>= 20px)");

        // 验证空地判定算法与空地无树军规
        foreach (var cl in mass.Clearings)
        {
            Assert(ForestMassGenerator.IsInAnyClearing(cl.Position, mass.Clearings), "空地中心必须被识别为在空地内");
        }

        // 验证瀚海大漠大区与 DesertFieldData
        var desertRegion = nRegions.FirstOrDefault(r => r.Type == NarrativeRegionType.DesertField);
        Assert(desertRegion != null, "必须包含 DesertField 叙事大区");
        Assert(desertRegion!.DesertField != null, "DesertField 必须包含 DesertFieldData");
        var desert = desertRegion.DesertField!;
        Assert(desert.DuneRidgeCorridors.Count >= 3, $"大漠必须沿主风向生成平行流动沙垄走廊 (>= 3)，实际为: {desert.DuneRidgeCorridors.Count}");
        Assert(desert.Oases.Count >= 1, "大漠应包含绿洲清泉坐标");

        // 验证天府平原大区与 PlainsBasinData
        var plainsRegion = nRegions.FirstOrDefault(r => r.Type == NarrativeRegionType.PlainsBasin);
        Assert(plainsRegion != null, "必须包含 PlainsBasin 叙事大区");
        Assert(plainsRegion!.PlainsBasin != null, "PlainsBasin 必须包含 PlainsBasinData");
        var pb = plainsRegion.PlainsBasin!;
        Assert(pb.MirrorLakes.Count >= 2, $"平原应包含镜湖水泽 (>= 2)，实际为: {pb.MirrorLakes.Count}");
        Assert(pb.SolitaryKnolls.Count >= 3, $"平原留白区应点缀旷野独头孤丘 (>= 3)，实际为: {pb.SolitaryKnolls.Count}");

        // 2. 验证全管线 CartographyGenerator 调度
        var snapshot = CartographyGenerator.Generate(geom, fields, options, plan.MegaRegions, plan.Settlements, blueprint);
        Assert(snapshot.Brushes.Count > 100, $"制图快照生成的总笔刷数应丰富 (> 100)，实际为: {snapshot.Brushes.Count}");

        // 验证林间空地绝无树木
        var allTreeBrushes = snapshot.Brushes.Where(b => b.Type is BrushType.ForestCluster or BrushType.TreeGroup).ToList();
        Assert(allTreeBrushes.Count > 0, "必须生成树木林冠笔刷");
        foreach (var tree in allTreeBrushes)
        {
            var inClearing = ForestMassGenerator.IsInAnyClearing(tree.Position, mass.Clearings, margin: 2f);
            Assert(!inClearing, $"林间空地内部严禁放置任何树木: 位置 ({tree.Position.X:F1}, {tree.Position.Y:F1})");
        }

        // 验证平原镜湖与沙垄
        var lakeBrushes = snapshot.Brushes.Where(b => b.Type == BrushType.LakePond).ToList();
        Assert(lakeBrushes.Count >= 2, $"平原与大漠应生成镜湖水泽笔刷 (>= 2)，实际为: {lakeBrushes.Count}");

        var duneBrushes = snapshot.Brushes.Where(b => b.Type == BrushType.DesertDune).ToList();
        Assert(duneBrushes.Count >= 3, $"大漠风向沙垄笔刷应生成 (>= 3)，实际为: {duneBrushes.Count}");
    }

    private static void TestWorldPlannerArchitecture()
    {
        var geom = PolygonGridBuilder.Create(new WorldExtent(2048, 1024), 1024, 2048, 6, out _);
        var fields = CellFields.Create(geom.Count);
        var options = new GenerationOptions
        {
            Seed = 9999,
            TargetCellCount = 2048,
            SeaLevel = 0.35f,
            EnableCartographyDesigner = true
        };

        var blueprint = FantasyContinent01Blueprint.Create();

        // 1. 测试 WorldPlanner.Plan 宏观规划
        var plan = WorldPlanner.Plan(geom, fields, options, blueprint);
        Assert(plan != null, "WorldMapPlan 规划结果不应为空");
        Assert(plan.ContinentHull.Count >= 8, "大陆基底多边形点数应充足 (>= 8)");
        Assert(plan.MountainChains.Count >= 2, "山系龙骨数量应 >= 2");
        Assert(plan.MountainChains.Any(m => m.Branches.Count > 0), "主山系必须包含立体纵深侧脉/支脉 (Branches)");
        Assert(plan.RiverSystems.Count >= 1, "树状水网数量应 >= 1");
        Assert(plan.Regions.Count >= 3, "生态大区规划数量应 >= 3");
        Assert(plan.Regions.Any(r => r.SubBiomes.Count > 0), "生态大区必须包含复合子生态包 (SubBiomes)");
        Assert(plan.Settlements.Count >= 20, $"战略聚落网络应丰富 (>= 20)，实际为: {plan.Settlements.Count}");
        Assert(plan.RoadGraph.Segments.Count >= 5, "官道驿路网络路段数应 >= 5");
        Assert(plan.Decorations.FarmlandClusters.Count >= 2, "平原农田聚集区应 >= 2");
        Assert(plan.Decorations.FarmlandCorridors.Count >= 10, "沿官道与江河农田水网走廊点数应充足 (>= 10)");

        // 2. 验证 MountainGenerator 连绵山墙与立体支脉
        var mountainBrushes = MountainGenerator.Generate(plan.MountainChains, options);
        Assert(mountainBrushes.Count > 50, "连绵山墙生成的笔刷数量应充足 (> 50)");
        Assert(mountainBrushes.Any(b => b.Type == BrushType.MountainFar), "山系必须生成远山黛影 (MountainFar)");
        Assert(mountainBrushes.Any(b => b.Type == BrushType.MountainRidge), "山系必须生成连贯山脊主身 (MountainRidge)");
        Assert(mountainBrushes.Any(b => b.Type == BrushType.MountainSecondary), "山系必须生成立体支脉次级山脊 (MountainSecondary)");
        Assert(mountainBrushes.Any(b => b.Type == BrushType.SnowCap), "山系必须生成积雪冰峰 (SnowCap)");
        Assert(mountainBrushes.Any(b => b.Type == BrushType.Hill), "山系必须生成山麓前脚低丘 (Hill)");
        Assert(mountainBrushes.Any(b => b.Type == BrushType.Fog), "山系必须生成山麓流岚 (Fog)");

        // 3. 验证 RiverGenerator 高山起源与九曲折线
        var riverBrushes = RiverGenerator.Generate(plan.RiverSystems, options);
        var mainStems = riverBrushes.Where(b => b.VariantKey == "river_main_stem").ToList();
        Assert(mainStems.Count > 0, "必须生成主干大江笔刷 (river_main_stem)");
        Assert(mainStems[0].Points is { Length: >= 50 }, "主干大江必须包含高密度平滑九曲折线 (>= 50)");

        // 4. 验证 RegionGenerator 体块林海与大漠生态
        var regionBrushes = RegionGenerator.Generate(plan.Regions, options);
        Assert(regionBrushes.Any(b => b.Type == BrushType.ForestCluster), "林海核心必须生成密集成块的水墨大林冠 (ForestCluster)");
        Assert(regionBrushes.Any(b => b.Type == BrushType.DesertDune), "大漠必须生成流动新月沙垄 (DesertDune)");

        // 5. 验证 SettlementGenerator 地理因果律选址与四级梯队
        var (landmarks, settlementBrushes) = SettlementGenerator.Generate(plan.Settlements, options);
        Assert(landmarks.Count >= 20, $"战略聚落地标数量应 >= 20，实际为: {landmarks.Count}");
        Assert(landmarks.Any(l => l.Name == "神京天都" && l.Type == LandmarkType.Capital), "应包含天下国都'神京天都'");
        Assert(landmarks.Any(l => l.Name == "雁门雄关"), "应包含锁山要塞'雁门雄关'");
        Assert(landmarks.Any(l => l.Name == "江陵大都"), "应包含两江汇流'江陵大都'");
        Assert(landmarks.Any(l => l.Name == "临海沧津" && l.Type == LandmarkType.Port), "应包含海河巨港'临海沧津'");
        Assert(landmarks.Any(l => l.Name == "姑苏水郡"), "应包含水乡泽国'姑苏水郡'");
        Assert(landmarks.Any(l => l.Name == "青岚道观"), "应包含林海洞天'青岚道观'");
        Assert(landmarks.Any(l => l.Name == "杏花古村"), "应包含田畴村落'杏花古村'");

        // 6. 验证 DecorationGenerator 人类文明改造
        var decorationBrushes = DecorationGenerator.Generate(plan.Decorations, options);
        Assert(decorationBrushes.Any(b => b.Type == BrushType.FieldTerraced), "微地貌必须生成水浇田梯田笔刷 (FieldTerraced)");

        // 7. 验证全管线 CartographyGenerator
        var snapshot = CartographyGenerator.Generate(geom, fields, options, Array.Empty<MegaTerrainRegion>(), Array.Empty<SettlementInfo>(), blueprint);
        Assert(snapshot.Brushes.Count > 200, $"全管线生成的丰富笔刷数应充足 (> 200)，实际为: {snapshot.Brushes.Count}");
    }

    private static void TestMapEffectsProceduralCartography()
    {
        // ── 1. 验证平原河流蛇曲动力学与牛轭湖 (MeanderRiverGenerator) ──
        var riverPath = new List<PolyVec2>
        {
            new(50.0, 100.0),
            new(80.0, 120.0),
            new(110.0, 150.0),
            new(140.0, 180.0),
            new(170.0, 200.0),
            new(190.0, 175.0),
            new(185.0, 140.0), // 紧缩环段
            new(160.0, 135.0),
            new(145.0, 160.0),
            new(175.0, 210.0),
            new(210.0, 230.0),
            new(250.0, 240.0)
        };

        var meanderRes = MeanderRiverGenerator.ProcessMeander(
            riverPath,
            riverWidth: 8.0f,
            seed: 42,
            waterColor: CartographyColor.ColdBlue,
            regionId: 1,
            meanderIntensity: 1.5f);

        Assert(meanderRes.EvolvedWaypoints.Count >= 4, "蛇曲演化后航路点数应有效保留");
        Assert(meanderRes.OxbowLakes.Count >= 1, "典型闭合蛇曲环必须正确触发截弯取直并剥离出牛轭湖");
        Assert(meanderRes.OxbowLakes.Any(b => b.Type == BrushType.OxbowLake), "必须包含 BrushType.OxbowLake 新月湖图元");
        Assert(meanderRes.OxbowLakes.Any(b => b.Type == BrushType.WetlandReeds), "牛轭湖滨必须点缀伴生芦苇湿地草丛");

        // ── 2. 验证河口树根状分形三角洲与冲积沙洲 (RiverDeltaGenerator) ──
        var mouthPos = new PolyVec2(400.0, 300.0);
        var flowDir = new PolyVec2(1.0, 0.2).Normalized();
        var deltaRes = RiverDeltaGenerator.GenerateDelta(
            mouthPos,
            flowDir,
            mouthWidth: 9.5f,
            seed: 888,
            waterColor: CartographyColor.ColdBlue,
            regionId: 2,
            branchCount: 4,
            deltaReach: 60.0f);

        Assert(deltaRes.DistributaryStreams.Count >= 3, "河口必须生成至少 3 支放射分流水系");
        Assert(deltaRes.DeltaIslands.Count >= 2, "分流水网负空间必须生成至少 2 处冲积沙洲群岛");
        Assert(deltaRes.DeltaIslands.All(b => b.Scale >= 0.5f && b.Scale <= 1.8f), "沙洲尺度必须处于合理视觉比例范围");
        Assert(deltaRes.DeltaIslands.Any(b => b.Type == BrushType.DeltaIsland), "必须包含 BrushType.DeltaIsland 沙洲图元");

        // ── 3. 验证大地深渊裂谷断崖与垂直落差排线 (ChasmGenerator) ──
        var chasmStart = new PolyVec2(100.0, 50.0);
        var chasmEnd = new PolyVec2(220.0, 110.0);
        var chasmRes = ChasmGenerator.GenerateChasm(
            chasmStart,
            chasmEnd,
            seed: 999,
            rockTint: CartographyColor.SandyOchre,
            regionId: 3,
            avgWidth: 24.0f,
            cliffDrop: 28.0f);

        Assert(chasmRes.AbyssBases.Count >= 1, "裂谷底部必须生成深渊黑底基底");
        Assert(chasmRes.AbyssBases[0].Type == BrushType.ChasmAbyss, "深渊基底图元类型必须为 BrushType.ChasmAbyss");
        Assert(chasmRes.AbyssBases[0].Points is { Length: >= 6 }, "深渊基底多边形顶点必须闭合完整");
        Assert(chasmRes.CliffStructures.Count >= 4, "必须生成崖面垂直排线与横向沉积岩层");
        Assert(chasmRes.CliffStructures.Any(b => b.Type == BrushType.ChasmCliff), "必须包含 BrushType.ChasmCliff 断崖图元");

        // ── 4. 验证智能多阶海岸水波纹与海角海蚀拱门 (SmartCoastlineGenerator) ──
        var coastPath = new List<PolyVec2>
        {
            new(50.0, 400.0),
            new(90.0, 380.0),
            new(130.0, 340.0),
            new(150.0, 300.0), // 尖锐岬角
            new(135.0, 260.0),
            new(160.0, 220.0),
            new(200.0, 200.0),
            new(250.0, 190.0)
        };

        var coastRes = SmartCoastlineGenerator.GenerateCoastline(
            coastPath,
            seed: 333,
            oceanColor: CartographyColor.SeaWave,
            regionId: -1,
            generateArches: true);

        Assert(coastRes.ConcentricWaves.Count >= 2, "海岸向外必须生成多阶同心手绘波纹段落");
        Assert(coastRes.ConcentricWaves.All(b => b.Type == BrushType.CoastlineWave), "海岸波纹图元类型必须为 BrushType.CoastlineWave");
        Assert(coastRes.SeaArches.Count >= 1, "突出尖锐岬角处必须生成海蚀石拱奇观 (SeaArch)");
        Assert(coastRes.SeaArches[0].Type == BrushType.SeaArch, "海蚀石拱图元类型必须为 BrushType.SeaArch");

        // ── 5. 验证低洼沼泽死水与倒挂西班牙垂苔 (SwampGenerator) ──
        var swampCenter = new PolyVec2(300.0, 250.0);
        var swampRes = SwampGenerator.GenerateSwamp(
            swampCenter,
            radius: 36.0f,
            seed: 777,
            basePalette: CartographyColor.EmeraldGreen,
            regionId: 4,
            treeCount: 5);

        Assert(swampRes.Pools.Count >= 2, "沼泽必须包含多重重叠低洼死水池塘");
        Assert(swampRes.Pools.All(b => b.Type == BrushType.SwampPool), "死水池塘图元类型必须为 BrushType.SwampPool");
        Assert(swampRes.TreesAndMoss.Any(b => b.Type == BrushType.SpanishMoss), "沼泽树冠下必须挂载倒悬西班牙垂苔 (SpanishMoss)");
        Assert(swampRes.TreesAndMoss.Any(b => b.Type == BrushType.TreeGroup), "必须包含膨大露根沼泽树木");

        // ── 6. 验证全图管线完整集成与图层编译 (CartographyGenerator / PainterCommandBuilder) ──
        var geom = PolygonGridBuilder.Create(WorldExtent.Default, 12345, 2048, 8, out _);
        var fields = CellFields.Create(geom.Count);
        for (var i = 0; i < geom.Count; i++)
        {
            fields.Height[i] = (float)(0.25 + 0.5 * (geom.SiteY[i] / geom.Height));
        }
        var options = new GenerationOptions
        {
            Seed = 12345,
            SeaLevel = 0.35f,
            EnableCartographyDesigner = true,
            BlueprintName = "FantasyContinent01"
        };
        var blueprint = FantasyContinent01Blueprint.Create();
        var snapshot = CartographyGenerator.Generate(geom, fields, options, Array.Empty<MegaTerrainRegion>(), Array.Empty<SettlementInfo>(), blueprint);

        Assert(snapshot != null, "制图快照生成必须成功");
        Assert(snapshot.Brushes.Count > 50, $"制图快照笔刷数 ({snapshot.Brushes.Count}) 应充足");

        // 验证 Map Effects 关键图元全部注入全图流水线
        Assert(snapshot.Brushes.Any(b => b.Type == BrushType.MountainHachure), "主山脉主峰必须渲染托尔金阴影排线 (MountainHachure)");
        Assert(snapshot.Brushes.Any(b => b.Type == BrushType.OxbowLake), "平原河流段必须注入牛轭湖图元 (OxbowLake)");
        Assert(snapshot.Brushes.Any(b => b.Type == BrushType.DeltaIsland), "河口入海段必须注入三角洲沙洲群岛 (DeltaIsland)");
        Assert(snapshot.Brushes.Any(b => b.Type == BrushType.ChasmAbyss), "必须注入大地深渊裂谷黑底图元 (ChasmAbyss)");
        Assert(snapshot.Brushes.Any(b => b.Type == BrushType.SwampPool), "必须注入沼泽死水湿地图元 (SwampPool)");

        // 验证 PainterCommandBuilder 图层路由规范性
        var commands = PainterCommandBuilder.BuildCommands(snapshot.Brushes, snapshot.Landmarks);
        Assert(commands.Any(c => c.LayerId == PainterCommandBuilder.LayerRiver && c.Brushes.Any(b => b.Type is BrushType.OxbowLake or BrushType.CoastlineWave)), "LayerRiver 必须成功路由牛轭湖与海岸波纹");
        Assert(commands.Any(c => c.LayerId == PainterCommandBuilder.LayerForest && c.Brushes.Any(b => b.Type == BrushType.SwampPool)), "LayerForest 必须成功路由沼泽死水底面");
        Assert(commands.Any(c => c.LayerId == PainterCommandBuilder.LayerMountain && c.Brushes.Any(b => b.Type is BrushType.MountainHachure or BrushType.DeltaIsland or BrushType.ChasmAbyss or BrushType.ChasmCliff or BrushType.SpanishMoss)), "LayerMountain 必须成功路由托尔金排线/沙洲/深渊断崖/垂苔");

        // ── 7. 验证种子确定性 (Determinism) ──
        var snapshotRepeat = CartographyGenerator.Generate(geom, fields, options, Array.Empty<MegaTerrainRegion>(), Array.Empty<SettlementInfo>(), blueprint);
        Assert(snapshot.Brushes.Count == snapshotRepeat.Brushes.Count, "相同随机种子下生成的笔刷指令数必须完全严格恒定");
        for (var i = 0; i < Math.Min(30, snapshot.Brushes.Count); i++)
        {
            Assert(snapshot.Brushes[i].Type == snapshotRepeat.Brushes[i].Type, $"第 {i} 个笔刷类型不一致");
            Assert(Math.Abs(snapshot.Brushes[i].Position.X - snapshotRepeat.Brushes[i].Position.X) < 1e-6, $"第 {i} 个笔刷坐标 X 不一致");
            Assert(Math.Abs(snapshot.Brushes[i].Position.Y - snapshotRepeat.Brushes[i].Position.Y) < 1e-6, $"第 {i} 个笔刷坐标 Y 不一致");
        }
    }
}
