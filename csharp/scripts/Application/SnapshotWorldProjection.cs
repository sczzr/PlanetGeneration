using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Generation;
using PlanetGeneration.Core.Geometry;
using Legacy = PlanetGeneration.WorldGen;
using PolygonGrid = PlanetGeneration.WorldGen.Polygon.PolygonGrid;

namespace PlanetGeneration.Application;

/// <summary>
/// WorldSnapshot → 旧界面数据的单向投影，不生成地形、不进行生态/文明模拟。
/// 所有数组均为衍生数据；修改旧界面的缓存不会反向改写权威快照。
/// </summary>
internal static class SnapshotWorldProjection
{
    public static GeneratedWorldData Create(WorldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var width = checked((int)snapshot.Extent.Width);
        var height = checked((int)snapshot.Extent.Height);
        if (width <= 0 || height <= 0 || (long)width * height > 16_777_216 || width != snapshot.Extent.Width || height != snapshot.Extent.Height)
            throw new ArgumentException("旧界面投影要求整数世界尺寸。", nameof(snapshot));
        var map = PolygonRasterizer.BuildCellMap(snapshot.Geometry, width, height);
        var fields = snapshot.Fields;
        T[,] Project<T>(T[] values) => ProjectColumn(map, values);
        var tuning = snapshot.Options.Tuning;
        var sites = BuildPlateSites(snapshot, width, height);
        var baseElevations = fields.PlateId.Select(id => id >= 0 && id < sites.Count ? sites[id].BaseElevation : 0.5f).ToArray();
        var cities = snapshot.Settlements.Select(s => new Legacy.CityInfo
        {
            Position = new Vector2I(
                Math.Clamp((int)Math.Floor(snapshot.Extent.WrapX(s.Position.X)), 0, width - 1),
                Math.Clamp((int)Math.Floor(s.Position.Y), 0, height - 1)),
            Name = s.Name, Score = s.Score,
            Population = s.Rank switch
            {
                SettlementRank.CityState => Legacy.CityPopulation.Large,
                SettlementRank.Town => Legacy.CityPopulation.Medium,
                _ => Legacy.CityPopulation.Small
            }
        }).ToList();
        var world = new GeneratedWorldData
        {
            Elevation = Project(fields.Height), Temperature = Project(fields.Temperature), Moisture = Project(fields.Moisture),
            Wind = Project(fields.WindX.Select((x, i) => new Vector2(x, fields.WindY[i])).ToArray()),
            River = Project(fields.River),
            Biome = Project(Array.ConvertAll(fields.Biome, value => (Legacy.BiomeType)value)),
            Rock = Project(Array.ConvertAll(fields.Rock, value => (Legacy.RockType)value)),
            Ore = Project(Array.ConvertAll(fields.Ore, value => (Legacy.OreType)value)),
            IndustrialOre = Project(Array.ConvertAll(fields.IndustrialOre, value => (Legacy.OreType)value)),
            SupernaturalOre = Project(Array.ConvertAll(fields.SupernaturalOre, value => (Legacy.OreType)value)),
            CardOre = Project(Array.ConvertAll(fields.CardOre, value => (Legacy.OreType)value)), Leyline = Project(fields.Leyline),
            Cities = cities, CityCell = snapshot.Settlements.Select(s => s.CellId).ToArray(),
            PlateResult = new Legacy.PlateResult
            {
                PlateIds = Project(fields.PlateId), PlateBaseElevation = Project(baseElevations),
                BoundaryTypes = Project(Array.ConvertAll(fields.PlateBoundary, value => (Legacy.PlateBoundaryType)value)),
                StressMap = new Legacy.PlateStressCell[width, height], Sites = sites,
                Neighbors = new List<Legacy.PlateNeighborInfo>(), BorderPoints = new List<Legacy.PlateEdgePoint>()
            },
            Tuning = new Legacy.WorldTuning
            {
                Name = tuning.Name, DeepOceanFactor = tuning.DeepOceanFactor, CoastBand = tuning.CoastBand,
                MountainThreshold = tuning.MountainThreshold, RiverSourceElevationThreshold = tuning.RiverSourceElevationThreshold,
                RiverSourceMoistureThreshold = tuning.RiverSourceMoistureThreshold, RiverSourceChance = tuning.RiverSourceChance
            },
            Stats = new Legacy.WorldStats
            {
                Width = width, Height = height, CityCount = snapshot.Stats.CityCount, OceanPercent = snapshot.Stats.OceanPercent,
                RiverPercent = snapshot.Stats.RiverPercent, AvgTemperature = snapshot.Stats.AvgTemperature, AvgMoisture = snapshot.Stats.AvgMoisture
            },
            PolygonCellMap = map, PolygonCellsDesired = snapshot.Options.TargetCellCount
        };
        UpdateSimulation(world, snapshot);
        return world;
    }

    public static void EnsureSimulation(GeneratedWorldData world, int epoch, int diversity, int aggression, int magic)
    {
        var snapshot = world.Snapshot ?? throw new ArgumentException("该世界不是快照投影。", nameof(world));
        var options = snapshot.Options;
        if (snapshot.Ecology == null || snapshot.Civilization == null || options.Epoch != epoch || options.SpeciesDiversity != diversity || options.CivilAggression != aggression || options.MagicDensity != magic)
        {
            snapshot = new PlanetGeneration.Core.Application.WorldSimulationService()
                .ReSimulateEpoch(snapshot, epoch, diversity, aggression, magic);
            UpdateSimulation(world, snapshot);
        }
        else if (world.EcologySimulation == null || world.CivilizationSimulation == null || world.PolygonEcology == null || world.PolygonCivilization == null)
        {
            UpdateSimulation(world, snapshot);
        }
    }

    public static void UpdateSimulation(GeneratedWorldData world, WorldSnapshot snapshot)
    {
        if (world.Snapshot != null && !ReferenceEquals(world.Snapshot.Geometry, snapshot.Geometry))
            throw new ArgumentException("模拟更新不能替换几何；请重新创建投影。", nameof(snapshot));
        var map = world.PolygonCellMap ?? throw new InvalidOperationException("缺少像素归属图。");
        var fields = snapshot.Fields;
        T[,] Project<T>(T[] values) => ProjectColumn(map, values);
        var grid = new PolygonGrid(snapshot.Geometry, fields.Clone());
        var ecology = snapshot.Ecology;
        var projectedEcology = ecology == null ? null : new Legacy.EcologySimulationResult
        {
            EcologyHealth = Project(fields.EcologyHealth), CivilizationPotential = Project(fields.CivilizationPotential),
            AvgEcologyHealth = ecology.AvgEcologyHealth, AvgCivilizationPotential = ecology.AvgCivilizationPotential,
            CivilizationEmergencePercent = ecology.CivilizationEmergencePercent
        };
        var civilization = snapshot.Civilization;
        var projectedCivilization = civilization == null ? null : new Legacy.CivilizationSimulationResult
        {
            Influence = Project(fields.Influence), PolityId = Project(Array.ConvertAll(fields.PolityId, value => (int)value)),
            BorderMask = Project(fields.BorderMask), TradeRouteMask = Project(fields.TradeRouteMask), TradeFlow = Project(fields.TradeFlow),
            PolityCount = civilization.PolityCount, HamletCount = civilization.HamletCount, TownCount = civilization.TownCount,
            CityStateCount = civilization.CityStateCount, TradeRouteCells = civilization.TradeRouteCells,
            ControlledLandPercent = civilization.ControlledLandPercent, CoreCellPercent = civilization.CoreCellPercent,
            DominantPolitySharePercent = civilization.DominantPolitySharePercent, ConnectedHubPercent = civilization.ConnectedHubPercent,
            ConflictHeatPercent = civilization.ConflictHeatPercent, AllianceCohesionPercent = civilization.AllianceCohesionPercent,
            BorderVolatilityPercent = civilization.BorderVolatilityPercent,
            RecentEvents = civilization.RecentEvents.Select(e => new Legacy.CivilizationEpochEvent
            {
                Epoch = e.Epoch, Category = e.Category, Summary = e.Summary, ImpactLevel = e.ImpactLevel
            }).ToArray()
        };
        // 分配和转换全部成功后再发布，避免异常留下半更新的会话。
        world.InvalidateSimulationCaches();
        world.PolygonGrid = grid;
        world.PolygonEcology = snapshot.Ecology;
        world.PolygonCivilization = snapshot.Civilization;
        world.EcologySimulation = projectedEcology;
        world.CivilizationSimulation = projectedCivilization;
        world.Snapshot = snapshot;
    }

    private static T[,] ProjectColumn<T>(PolygonCellMap map, T[] values)
    {
        if (values.Length != map.CellCount) throw new ArgumentException("地块字段数量不匹配。", nameof(values));
        var raster = new T[map.Width, map.Height];
        for (var y = 0; y < map.Height; y++)
            for (var x = 0; x < map.Width; x++) raster[x, y] = values[map.CellAt(x, y)];
        return raster;
    }

    private static List<Legacy.PlateSite> BuildPlateSites(WorldSnapshot snapshot, int width, int height)
    {
        // BaseFieldGeneratorAdapter 的站点坐标来自内部连续场，旧界面使用源栅格坐标。
        var (fieldWidth, fieldHeight) = FieldSamplingPolicy.ResolveInternalResolution(snapshot.Options);
        var sites = snapshot.PlateSummary.Sites.Select(s => new Legacy.PlateSite
        {
            Id = s.Id, IsOceanic = s.IsOceanic, BaseElevation = s.BaseElevation,
            Position = new Vector2I((int)(s.Position.X * width / fieldWidth), (int)(s.Position.Y * height / fieldHeight)),
            Motion = new Vector2((float)s.Motion.X, (float)s.Motion.Y), DebugColor = new Color(s.ColorR, s.ColorG, s.ColorB)
        }).ToList();
        if (sites.Count == 0)
            sites.Add(new Legacy.PlateSite { Id = 0, Position = Vector2I.Zero, Motion = Vector2.Zero,
                IsOceanic = false, BaseElevation = 0.5f, DebugColor = Colors.White });
        return sites;
    }
}
