using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Cartography.Generator;
using PlanetGeneration.Core.Cartography.Planning;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 幻想制图层统一生成调度器（CartographyGenerator）。
/// 
/// 彻底告别“噪声矩阵 -> 随机生物群系 -> 零散散落”旧时代，
/// 贯彻“世界布局图 (World Layout Graph Architecture)”全新生成范式：
/// 1. WorldPlanner: 规划大陆有机轮廓、连贯山系龙骨、雪山起源水网、生态大区与地理因果战略聚落；
/// 2. MountainGenerator: 铸造步长 20~24px 连绵不可跨越山墙，并赋予四层立体景深（远山、主脊、雪顶、低丘、底雾）；
/// 3. RiverGenerator: 高山起源树状九曲大江折线多段线，自然汇流注海；
/// 4. RegionGenerator: 心密边疏体块林海（腹地空地留白透气，平原0树）与雨影金沙流动大漠；
/// 5. SettlementGenerator: 严格遵循地理因果律的天下神都、江汉大邑、锁喉雄关与海河良港；
/// 6. DecorationGenerator: 平原水浇田肌理、旷野独头孤丘与清平镜湖微地貌。
/// </summary>
public static class CartographyGenerator
{
    public static CartographySnapshot Generate(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        IReadOnlyList<MegaTerrainRegion> megaRegions,
        IReadOnlyList<SettlementInfo> settlements,
        MapBlueprint? blueprint = null)
    {
        // 画师/蓝图规划可以修改艺术工作场，但不能修改已用于水文、模拟和统计的事实字段。
        // 所有入口（包含 Generate(WorldSnapshot)）共用此隔离边界。
        fields = fields.Clone();

        // ── 1. 世界规划式生成流水线 (World Layout Graph Pipeline，仅在指定蓝图或显式启用导演样板时生效) ──
        if (blueprint != null || (options.EnableCartographyDesigner && !string.IsNullOrEmpty(options.BlueprintName)))
        {
            blueprint ??= (options.BlueprintName == "FantasyContinent01" ? FantasyContinent01Blueprint.Create() : null);
            if (blueprint != null)
            {
                var plan = WorldPlanner.Plan(geometry, fields, options, blueprint);

            // 编译区域色彩与艺术规则字典 (RegionStyles)
            var regions = new Dictionary<int, RegionStyle>
            {
                [0] = new RegionStyle
                {
                    RegionId = 0,
                    Name = "中原天府",
                    Palette = CartographyColor.EmeraldGreen,
                    Density = 1.0f,
                    Exaggeration = 1.0f,
                    Fog = true,
                    MistColor = CartographyColor.MistIvory
                }
            };

            foreach (var r in blueprint.Regions)
            {
                regions[r.Id] = new RegionStyle
                {
                    RegionId = r.Id,
                    Name = r.Name,
                    Palette = r.Palette,
                    Density = 1.0f,
                    Exaggeration = 1.0f,
                    Fog = true,
                    MistColor = CartographyColor.MistIvory
                };
            }

            foreach (var reg in plan.Regions)
            {
                if (!regions.ContainsKey(reg.Id))
                {
                    regions[reg.Id] = new RegionStyle
                    {
                        RegionId = reg.Id,
                        Name = reg.Name,
                        Palette = reg.Palette,
                        Density = 1.0f,
                        Exaggeration = 1.0f,
                        Fog = true,
                        MistColor = CartographyColor.MistIvory
                    };
                }
            }

            foreach (var mc in plan.MountainChains)
            {
                if (!regions.ContainsKey(mc.Id))
                {
                    regions[mc.Id] = new RegionStyle
                    {
                        RegionId = mc.Id,
                        Name = mc.Name,
                        Palette = mc.Palette,
                        Density = 1.0f,
                        Exaggeration = 1.0f,
                        Fog = true,
                        MistColor = CartographyColor.MistIvory
                    };
                }
            }

            // 调度全新的规划生成画师族 (Painters)
            // A. 山脉是“墙”不是“点”：连续山墙与 4 层景深 (MountainFar, MountainRidge, SnowCap, Hill, Fog)
            var mountainBrushes = MountainGenerator.Generate(plan.MountainChains, options, regions);

            // B. 河流必须从山开始：九曲回环多段线与汇流注海
            var riverBrushes = RiverGenerator.Generate(plan.RiverSystems, options, regions);
            RiverGenerator.AppendSeaWaves(riverBrushes, geometry, fields, options);

            // C. 森林是“体块”不是“草坪”：中心100%浓墨咬合、腹地留白气孔、平原0树；大漠风向沙垄与绿洲
            var regionBrushes = RegionGenerator.Generate(plan.Regions, options, regions);

            // D. 城市服从地理因果律：神都、江陵、雁门、沧津、金沙、青岚与宏大视觉焦点地标
            var (landmarks, settlementBrushes) = SettlementGenerator.Generate(plan.Settlements, plan.GrandLandmarks, options, regions);

            // E. 平原人文与自然微地貌：水浇田梯田网、旷野小孤丘、清平镜湖、微风草纹
            var decorationBrushes = DecorationGenerator.Generate(plan.Decorations, options, regions);

            // ── F. Map Effects 奇幻制图进阶要素 (蛇曲牛轭湖/分形三角洲/地裂深渊/低洼沼泽/智能海岸线) ──
            var mapEffectsBrushes = new List<BrushInstruction>();

            // 1. 平原河流蛇曲演化与牛轭湖 (Meandering Rivers & Oxbow Lakes)
            foreach (var river in plan.RiverSystems)
            {
                if (river.MainWaypoints != null && river.MainWaypoints.Count >= 4)
                {
                    var meanderResult = MeanderRiverGenerator.ProcessMeander(
                        river.MainWaypoints,
                        river.MouthWidth,
                        (int)(options.Seed ^ (uint)river.Id),
                        river.Palette,
                        river.Id);
                    mapEffectsBrushes.AddRange(meanderResult.OxbowLakes);

                    // 2. 河流入海口树根状分形三角洲 (River Deltas & Silt Islands)
                    var lastIdx = river.MainWaypoints.Count - 1;
                    var mouthPos = river.MainWaypoints[lastIdx];
                    var prevPos = river.MainWaypoints[Math.Max(0, lastIdx - 1)];
                    var flowDir = mouthPos - prevPos;

                    var deltaResult = RiverDeltaGenerator.GenerateDelta(
                        mouthPos,
                        flowDir,
                        river.MouthWidth,
                        (int)(options.Seed ^ 0x64656c),
                        river.Palette,
                        river.Id);
                    mapEffectsBrushes.AddRange(deltaResult.AllInstructions);
                }
            }

            // 3. 大地深渊裂谷断崖与落差排线 (Chasm Abyss & Ragged Cliffs)
            if (plan.MountainChains.Count > 0)
            {
                var baseChain = plan.MountainChains[0];
                if (baseChain.SpineCurve != null && baseChain.SpineCurve.Count >= 4)
                {
                    var chasmStart = baseChain.SpineCurve[baseChain.SpineCurve.Count - 1] + new PolyVec2(28.0, 36.0);
                    var chasmEnd = chasmStart + new PolyVec2(75.0, 48.0);
                    var chasmResult = ChasmGenerator.GenerateChasm(
                        chasmStart,
                        chasmEnd,
                        (int)(options.Seed ^ 0x636861),
                        CartographyColor.SandyOchre,
                        baseChain.Id);
                    mapEffectsBrushes.AddRange(chasmResult.AllInstructions);
                }
            }

            // 4. 幽邃低洼沼泽与倒挂西班牙垂苔 (Swamps & Hanging Moss)
            if (plan.RiverSystems.Count > 0 && plan.RiverSystems[0].Lakes.Count > 0)
            {
                var swampCenter = plan.RiverSystems[0].Lakes[0] + new PolyVec2(-38.0, 42.0);
                var swampResult = SwampGenerator.GenerateSwamp(
                    swampCenter,
                    32.0f,
                    (int)(options.Seed ^ 0x737761),
                    CartographyColor.EmeraldGreen,
                    plan.RiverSystems[0].Id);
                mapEffectsBrushes.AddRange(swampResult.AllInstructions);
            }

            // 5. 智能多阶海岸水波纹与海蚀石拱 (Smart Coastlines & Sea Arches)
            var coastPoints = SmartCoastlineGenerator.ExtractCoastlinePath(geometry, fields, options.SeaLevel);
            if (coastPoints.Count >= 4)
            {
                var coastResult = SmartCoastlineGenerator.GenerateCoastline(
                    coastPoints,
                    (int)(options.Seed ^ 0x636f61),
                    CartographyColor.SeaWave);
                mapEffectsBrushes.AddRange(coastResult.AllInstructions);
            }

            // 聚合全景笔刷指令
            var allBrushes = new List<BrushInstruction>(
                mountainBrushes.Count + riverBrushes.Count + regionBrushes.Count +
                settlementBrushes.Count + decorationBrushes.Count + mapEffectsBrushes.Count);

            allBrushes.AddRange(mountainBrushes);
            allBrushes.AddRange(riverBrushes);
            allBrushes.AddRange(regionBrushes);
            allBrushes.AddRange(settlementBrushes);
            allBrushes.AddRange(decorationBrushes);
            allBrushes.AddRange(mapEffectsBrushes);

            return new CartographySnapshot(regions, allBrushes, landmarks, commands: null, snapshotId: null, createdAt: null, roadGraph: plan.RoadGraph);
            }
        }

        // ── 2. 自然演化模式：无蓝图时遵循底层物理地质、气候、水系与生态聚落数据生成 ──
        var fallbackRegions = RegionStyleGenerator.GenerateStyles(geometry, fields, options, megaRegions);

        var fbMountainBrushes = MountainRangePainter.Paint(
            geometry, fields, options, megaRegions, fallbackRegions, profile: null, narrativeRegions: null);

        var fbForestBrushes = ForestTransitionPainter.Paint(
            geometry, fields, options, megaRegions, fallbackRegions, profile: null, narrativeRegions: null);

        var fbRiverBrushes = RiverArtGenerator.GenerateBrushes(geometry, fields, options, fallbackRegions, null);

        var fbPlainsBrushes = PlainsPainter.Paint(
            geometry, fields, options, null, megaRegions, settlements, fallbackRegions, fbMountainBrushes, null);

        var (fbLandmarks, fbLandmarkBrushes) = LandmarkGenerator.GenerateLandmarks(
            geometry, fields, options, megaRegions, settlements, fallbackRegions);

        var fbAllBrushes = new List<BrushInstruction>(
            fbMountainBrushes.Count + fbForestBrushes.Count + fbRiverBrushes.Count +
            fbPlainsBrushes.Count + fbLandmarkBrushes.Count);

        fbAllBrushes.AddRange(fbMountainBrushes);
        fbAllBrushes.AddRange(fbForestBrushes);
        fbAllBrushes.AddRange(fbRiverBrushes);
        fbAllBrushes.AddRange(fbPlainsBrushes);
        fbAllBrushes.AddRange(fbLandmarkBrushes);

        // ── Map Effects: 智能海岸线与海蚀拱门生成 ──
        var fbCoastPoints = SmartCoastlineGenerator.ExtractCoastlinePath(geometry, fields, options.SeaLevel);
        if (fbCoastPoints.Count >= 4)
        {
            var fbCoastResult = SmartCoastlineGenerator.GenerateCoastline(
                fbCoastPoints,
                (int)(options.Seed ^ 0x636f61),
                CartographyColor.SeaWave);
            fbAllBrushes.AddRange(fbCoastResult.AllInstructions);
        }

        return new CartographySnapshot(fallbackRegions, fbAllBrushes, fbLandmarks);
    }

    /// <summary>
    /// 基于既有世界权威快照生成幻想制图层快照。
    /// </summary>
    public static CartographySnapshot Generate(WorldSnapshot snapshot, MapBlueprint? blueprint = null)
    {
        return Generate(
            snapshot.Geometry,
            snapshot.Fields,
            snapshot.Options,
            snapshot.MegaRegions,
            snapshot.Settlements,
            blueprint);
    }
}
