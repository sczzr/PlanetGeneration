using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Planning;

/// <summary>
/// 世界艺术布局的阶段协调器。各阶段按依赖顺序执行，并返回统一布局计划。
/// fields 是可变工作场；已发布快照应通过 CartographyGenerator 的隔离入口访问。
/// </summary>
public static class WorldPlanner
{
    public static WorldMapPlan Plan(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        MapBlueprint? blueprint = null)
    {
        blueprint ??= FantasyContinent01Blueprint.Create();

        var w = (float)geometry.Width;
        var h = (float)geometry.Height;

        // ── 0. 规划五大核心叙事大区 (Narrative Region Planning: 北岳、天府、青岚、狂沙、沧溟) ──
        var narrativeRegions = RegionPlanner.PlanNarrativeRegions(w, h, blueprint);

        // ── 1. 雕刻大陆基底轮廓 (Continent Landmass Carving) ──
        var continentHull = ContinentLayoutPlanner.CarveContinentLandmass(geometry, fields, options, blueprint, w, h);

        // ── 2. 规划立体山系骨架与支脉 (MountainSystem Skeleton, Branches & Topology) ──
        var (plannedMountains, mountainTopology) = MountainLayoutPlanner.PlanMountainChains(geometry, fields, options, blueprint, w, h);

        // ── 3. 规划雪山高源多级树状水系 (High Mountain Origin River Networks) ──
        var plannedRivers = RiverLayoutPlanner.PlanRiverNetworks(geometry, fields, options, blueprint, w, h);

        // ── 4. 规划宏观生态大区与复合子生态包 (Region Planning & SubBiomes) ──
        var plannedRegions = BiomeRegionPlanner.PlanRegions(geometry, fields, options, blueprint, narrativeRegions, w, h);

        // ── 5. 依据地理因果律规划 22+ 战略聚落网络 (Hierarchical Settlement Graph) ──
        var plannedSettlements = SettlementLayoutPlanner.PlanSettlements(geometry, fields, options, blueprint, w, h);

        // ── 6. 规划宏大核心视觉地标 (Grand Landmarks: 视觉锚点与叙事核心) ──
        var grandLandmarks = SettlementLayoutPlanner.PlanGrandLandmarks(w, h, plannedMountains, plannedSettlements);

        // ── 7. 提前规划连通文明节点的官道驿道网络 (RoadGraph 提前) ──
        var plannedRoads = InfrastructurePlanner.PlanRoadGraph(geometry, fields, options, blueprint, plannedSettlements, plannedMountains, plannedRivers, w, h);

        // ── 8. 人类文明地理改造与平原修饰 (Human Landscape Modification & Plain Micro-geography) ──
        var plannedDecorations = InfrastructurePlanner.PlanDecorations(geometry, fields, options, blueprint, plannedRoads, plannedRivers, plannedSettlements, w, h);

        return new WorldMapPlan
        {
            ContinentHull = continentHull,
            NarrativeRegions = narrativeRegions,
            MountainTopology = mountainTopology,
            MountainChains = plannedMountains,
            RiverSystems = plannedRivers,
            Regions = plannedRegions,
            Settlements = plannedSettlements,
            GrandLandmarks = grandLandmarks,
            RoadGraph = plannedRoads,
            Decorations = plannedDecorations
        };
    }
}
