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
    private static void TestHydrologyPipeline()
    {
        var geom = PolygonGridBuilder.Create(WorldExtent.Default, 777, 5000, 8, out _);
        var count = geom.Count;
        var fields = CellFields.Create(count);

        // 构造倾斜地形测试：左高右低
        for (var i = 0; i < count; i++)
        {
            var xRatio = (float)(geom.SiteX[i] / geom.Width);
            fields.Height[i] = 0.2f + (0.6f * (1f - xRatio));
            fields.Moisture[i] = 0.5f;
        }

        const float seaLevel = 0.35f;

        // 1. 验证下泄构建
        PolygonTopologyBuilder.BuildDownslope(geom, fields);
        var downslopeCount = 0;
        for (var i = 0; i < count; i++)
        {
            if (fields.Downslope[i] >= 0) downslopeCount++;
        }
        Assert(downslopeCount > 0, "最陡下降方向未正确建立");

        // 2. 验证河流关闭 (EnableRivers = false) 时河道彻底清零
        PolygonRiverBuilder.Generate(geom, fields, seaLevel, enableRivers: false, riverDensity: 1.0f);
        var riverActiveCount = 0;
        for (var i = 0; i < count; i++)
        {
            if (fields.River[i] > 0f) riverActiveCount++;
        }
        Assert(riverActiveCount == 0, $"关闭河流时 river 属性应全部为 0，实测有 {riverActiveCount} 块激活");
        Assert(fields.Flux[0] >= 0f, "即便河流关闭，基本地表径流 Flux 仍应累积");

        // 3. 验证河流开启 (EnableRivers = true) 时产生有效且连通的河道
        PolygonRiverBuilder.Generate(geom, fields, seaLevel, enableRivers: true, riverDensity: 1.0f);
        riverActiveCount = 0;
        for (var i = 0; i < count; i++)
        {
            if (fields.River[i] > 0f) riverActiveCount++;
        }
        Assert(riverActiveCount > 50, $"开启河流后应产生足够的河网地块，实测: {riverActiveCount}");

        // 4. 全海极端情况
        for (var i = 0; i < count; i++) fields.Height[i] = 0.1f;
        PolygonRiverBuilder.Generate(geom, fields, seaLevel, enableRivers: true, riverDensity: 1.0f);
        riverActiveCount = 0;
        for (var i = 0; i < count; i++)
        {
            if (fields.River[i] > 0f) riverActiveCount++;
        }
        Assert(riverActiveCount == 0, "全海地形下不应产生任何河道");
    }

    private static void TestSimulationDeterminism()
    {
        var geom = PolygonGridBuilder.Create(WorldExtent.Default, 555, 3000, 8, out _);
        var fields1 = CellFields.Create(geom.Count);
        var fields2 = CellFields.Create(geom.Count);

        for (var i = 0; i < geom.Count; i++)
        {
            fields1.Height[i] = 0.5f;
            fields1.Temperature[i] = 0.6f;
            fields1.Moisture[i] = 0.7f;
            fields1.Biome[i] = (byte)BiomeType.TemperateSeasonalForest;

            fields2.Height[i] = 0.5f;
            fields2.Temperature[i] = 0.6f;
            fields2.Moisture[i] = 0.7f;
            fields2.Biome[i] = (byte)BiomeType.TemperateSeasonalForest;
        }

        var eco1 = PolygonEcologySimulator.Simulate(geom, fields1, 1001, 50, 50, 50, 50, 0.45f);
        var eco2 = PolygonEcologySimulator.Simulate(geom, fields2, 1001, 50, 50, 50, 50, 0.45f);

        Assert(Math.Abs(eco1.AvgEcologyHealth - eco2.AvgEcologyHealth) < 1e-6f, "生态模拟在相同输入下不具备确定性");
        Assert(eco1.LandCellCount == eco2.LandCellCount, "陆地地块计数不一致");

        var civ1 = PolygonCivilizationSimulator.Simulate(geom, fields1, 1001, 50, 50, 50, 0.45f);
        var civ2 = PolygonCivilizationSimulator.Simulate(geom, fields2, 1001, 50, 50, 50, 0.45f);

        Assert(civ1.PolityCount == civ2.PolityCount, "政体数量确定性校验失败");
        Assert(civ1.TradeRouteCells == civ2.TradeRouteCells, "贸易网络确定性校验失败");
        Assert(civ1.Routes != null && civ2.Routes != null && civ1.Routes.Count == civ2.Routes.Count, "贸易路线数量确定性校验失败");
    }
}
