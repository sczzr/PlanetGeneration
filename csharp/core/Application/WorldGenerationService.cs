using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlanetGeneration.Core.Cartography;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Generation;
using PlanetGeneration.Core.Geometry;
using PlanetGeneration.Core.Simulation;

namespace PlanetGeneration.Core.Application;

/// <summary>
/// 显式分阶段世界生成服务。
///
/// 严格按依赖顺序执行：
/// 几何 -> 连续场 -> 采样 -> 下游/汇流/河网 -> 地貌/群系 -> 聚落 -> 生态/文明 -> 面积统计 -> 发布 Snapshot。
/// 支持取消令牌与带阶段名的进度报告。
/// </summary>
public sealed class WorldGenerationService
{
    private readonly IBaseFieldGenerator _fieldGenerator;

    public WorldGenerationService(IBaseFieldGenerator fieldGenerator)
    {
        _fieldGenerator = fieldGenerator;
    }

    public async Task<WorldSnapshot> GenerateAsync(
        GenerationOptions options,
        IProgress<(float Progress, string Stage)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // 1. 构建几何与空间索引
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report((5f, "构建地块几何"));

        var geometry = await Task.Run(() =>
        {
            return PolygonGridBuilder.Create(
                options.Extent,
                options.Seed,
                options.TargetCellCount,
                8,
                out _);
        }, cancellationToken);

        // 2. 生成内部连续场
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report((20f, "计算基础连续物理场"));

        var (internalW, internalH) = FieldSamplingPolicy.ResolveInternalResolution(options);
        var baseFields = await Task.Run(() =>
        {
            return _fieldGenerator.GenerateFields(options, internalW, internalH);
        }, cancellationToken);

        // 3. 构建归属图并采样到地块属性
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report((40f, "采样基础场至地块"));

        var cellMap = await Task.Run(() =>
        {
            return PolygonRasterizer.BuildCellMap(geometry, internalW, internalH);
        }, cancellationToken);

        var fields = CellFields.Create(geometry.Count);
        await Task.Run(() =>
        {
            CellFieldSampler.SampleAll(geometry, cellMap, baseFields, fields);
        }, cancellationToken);

        // 4. 水文拓扑：下游关系先于汇流与河流
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report((55f, "模拟最陡下降与水文河网"));

        await Task.Run(() =>
        {
            PolygonTopologyBuilder.BuildDownslope(geometry, fields);
            PolygonRiverBuilder.Generate(geometry, fields, options.SeaLevel, options.EnableRivers, options.RiverDensity);
        }, cancellationToken);

        // 5. 地貌与群系分类
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report((70f, "分类地貌与生物群系"));

        await Task.Run(() =>
        {
            PolygonLandformClassifier.ClassifyAll(geometry, fields, options.SeaLevel, options.GetEffectiveLandformTuning(), fields.Landform);
            CellBiomeClassifier.ClassifyAll(geometry, fields, options.SeaLevel, options.Tuning);
        }, cancellationToken);

        // 6. 聚落选址
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report((80f, "聚落核心选址"));

        var settlements = await Task.Run(() =>
        {
            return CellSettlementGenerator.Generate(geometry, fields, options.Seed, options.SeaLevel);
        }, cancellationToken);

        // 7. 生态与文明演化模拟
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report((90f, "模拟生态与文明演化"));

        var ecologyResult = await Task.Run(() =>
        {
            return PolygonEcologySimulator.Simulate(
                geometry,
                fields,
                options.Seed,
                options.Epoch,
                options.SpeciesDiversity,
                options.CivilAggression,
                options.MagicDensity,
                options.SeaLevel);
        }, cancellationToken);

        var civilizationResult = await Task.Run(() =>
        {
            return PolygonCivilizationSimulator.Simulate(
                geometry,
                fields,
                options.Seed,
                options.Epoch,
                options.CivilAggression,
                options.SpeciesDiversity,
                options.SeaLevel);
        }, cancellationToken);

        // 8. 面积占比统计
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report((96f, "计算地理统计与装配快照"));

        var stats = await Task.Run(() =>
        {
            return ComputeWorldStats(geometry, fields, options.SeaLevel, settlements.Count);
        }, cancellationToken);

        // 9. 提取与裁决世界级大型宏观生态地貌
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report((98f, "裁决世界级大型宏观地貌"));

        var megaRegions = await Task.Run(() =>
        {
            return MegaTerrainAnalyzer.Analyze(geometry, fields, options);
        }, cancellationToken);

        // 10. 生成幻想制图表现层 (Fantasy Cartography)
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report((99f, "生成幻想制图层"));

        var cartography = await Task.Run(() =>
        {
            return CartographyGenerator.Generate(geometry, fields, options, megaRegions, settlements);
        }, cancellationToken);

        var plateSummary = new PlateSystemSummary
        {
            Sites = baseFields.Plates.Sites
        };

        progress?.Report((100f, "完成"));

        return new WorldSnapshot(
            options,
            geometry,
            fields,
            settlements,
            plateSummary,
            stats,
            ecologyResult,
            civilizationResult,
            megaRegions: megaRegions,
            cartography: cartography)
        {
            ContinuousWind = baseFields.Wind
        };
    }

    private static WorldStatsSummary ComputeWorldStats(
        CellGeometry geometry,
        CellFields fields,
        float seaLevel,
        int cityCount)
    {
        var count = geometry.Count;
        var totalArea = 0d;
        var oceanArea = 0d;
        var riverArea = 0d;
        var forestArea = 0d;
        var landArea = 0d;

        var tempSum = 0d;
        var moistSum = 0d;
        var landCells = 0;

        for (var i = 0; i < count; i++)
        {
            var area = geometry.Area[i];
            totalArea += area;

            var h = fields.Height[i];
            var b = (BiomeType)fields.Biome[i];

            if (h <= seaLevel || b is BiomeType.Ocean or BiomeType.ShallowOcean)
            {
                oceanArea += area;
            }
            else
            {
                landArea += area;
                landCells++;
                tempSum += fields.Temperature[i] * area;
                moistSum += fields.Moisture[i] * area;

                if (fields.River[i] > 0.05f) riverArea += area;
                if (b is BiomeType.BorealForest or BiomeType.Taiga or BiomeType.TemperateSeasonalForest
                    or BiomeType.TemperateRainForest or BiomeType.TropicalSeasonalForest or BiomeType.TropicalRainForest)
                {
                    forestArea += area;
                }
            }
        }

        var safeTotal = Math.Max(totalArea, 1e-6d);
        var safeLand = Math.Max(landArea, 1e-6d);

        return new WorldStatsSummary
        {
            CellCount = count,
            LandCellCount = landCells,
            CityCount = cityCount,
            OceanPercent = (float)(oceanArea / safeTotal * 100d),
            RiverPercent = (float)(riverArea / safeLand * 100d),
            ForestPercent = (float)(forestArea / safeLand * 100d),
            AvgTemperature = (float)(tempSum / safeLand),
            AvgMoisture = (float)(moistSum / safeLand),
            PeakElevationMeters = 8848f,
            DeepSeaDepthMeters = -11034f,
        };
    }
}
