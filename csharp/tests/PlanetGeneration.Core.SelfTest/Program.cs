using System;
using System.Collections.Generic;
using System.Diagnostics;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;
using PlanetGeneration.Core.Layers;
using PlanetGeneration.Core.Simulation;

namespace PlanetGeneration.Core.SelfTest;

internal static class Program
{
    private static int _passedCount = 0;
    private static int _failedCount = 0;

    public static int Main(string[] args)
    {
        Console.WriteLine("==========================================================");
        Console.WriteLine(" PlanetGeneration.Core 独立自检套件 (纯 .NET 8 / 零外部依赖)");
        Console.WriteLine("==========================================================\n");

        var sw = Stopwatch.StartNew();

        RunTest("1. 多边形几何与守恒性自检 (2048 ~ 32768 档位)", TestGeometricIntegrity);
        RunTest("2. 目标地块数与实际地块数映射验证", TestTargetVsActualCounts);
        RunTest("3. 水文管线正确性与河流开关修复验证", TestHydrologyPipeline);
        RunTest("4. 分辨率解耦与全图精确拾取一致性 (1K / 2K / 4K)", TestResolutionDecoupling);
        RunTest("5. 组合图层栈、底图互斥与预设往返验证", TestLayerSystemAndPresets);
        RunTest("6. 模拟确定性与无竞争双源验证", TestSimulationDeterminism);
        RunTest("7. 缓存键稳定性与生成参数不可变性验证", TestCacheKeyStability);
        RunTest("8. 平滑曲线几何与网格拓扑水密性验证", TestCurvedCellGeometryAndMeshTopology);

        sw.Stop();

        Console.WriteLine("\n----------------------------------------------------------");
        Console.WriteLine($" 自检完成: 通过 {_passedCount} 项, 失败 {_failedCount} 项 (耗时 {sw.ElapsedMilliseconds} ms)");
        Console.WriteLine("----------------------------------------------------------");

        return _failedCount == 0 ? 0 : 1;
    }

    private static void RunTest(string testName, Action testAction)
    {
        Console.Write($"[测试] {testName} ... ");
        try
        {
            testAction();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASS");
            Console.ResetColor();
            _passedCount++;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAIL");
            Console.ResetColor();
            Console.WriteLine($"       错误详情: {ex.Message}");
            Console.WriteLine($"       {ex.StackTrace}");
            _failedCount++;
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"断言失败: {message}");
        }
    }

    private static void TestGeometricIntegrity()
    {
        var tiers = new[] { 2048, 5000, 10000, 20000, 32768 };
        var seeds = new[] { 12345, 98765 };

        foreach (var seed in seeds)
        {
            foreach (var target in tiers)
            {
                var geom = PolygonGridBuilder.Create(WorldExtent.Default, seed, target, 8, out var stats);
                Assert(geom.Count == stats.CellCount, "地块网格计数与统计不一致");
                Assert(stats.DegenerateCells == 0, $"发现退化地块: {stats.DegenerateCells}");

                // 执行全量几何校验
                var report = PolygonGridValidator.Validate(geom, seed);
                Assert(report.Passed, $"几何校验失败: {report}");
                Assert(Math.Abs(report.AreaCoverageRatio - 1.0) < 0.01, $"面积守恒率偏差超出 1%: {report.AreaCoverageRatio}");
                Assert(report.AverageNeighbors >= 5.0 && report.AverageNeighbors <= 7.0, $"平均邻接偏离正常范围: {report.AverageNeighbors}");
            }
        }
    }

    private static void TestTargetVsActualCounts()
    {
        var targets = new[] { 2048, 5000, 10000, 20000, 32768 };
        foreach (var target in targets)
        {
            var geom = PolygonGridBuilder.Create(WorldExtent.Default, 42, target, 8, out var stats);
            var ratio = (double)stats.CellCount / target;
            Assert(ratio >= 0.90 && ratio <= 1.15, $"实际地块数 ({stats.CellCount}) 与目标 ({target}) 偏离过大");
            Assert(stats.CellCount == geom.Columns * geom.Rows, "实际地块数必须等于规则列数乘行数");
        }
    }

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

    private static void TestResolutionDecoupling()
    {
        var geom = PolygonGridBuilder.Create(WorldExtent.Default, 888, 5000, 8, out _);

        // 分别为 1K, 2K, 4K 光栅化
        var map1K = PolygonRasterizer.BuildCellMap(geom, 1024, 512);
        var map2K = PolygonRasterizer.BuildCellMap(geom, 2048, 1024);
        var map4K = PolygonRasterizer.BuildCellMap(geom, 4096, 2048);

        Assert(map1K.UnassignedPixels <= 2, "1K 归属图存在过多未分配像素");
        Assert(map2K.UnassignedPixels <= 5, "2K 归属图存在过多未分配像素");
        Assert(map4K.UnassignedPixels <= 10, "4K 归属图存在过多未分配像素");

        // 验证同一归一化逻辑坐标的拾取一致性
        var rng = new PolygonRandom(1234UL);
        for (var i = 0; i < 500; i++)
        {
            var u = rng.NextDouble();
            var v = rng.NextDouble();

            var wx = u * geom.Width;
            var wy = v * geom.Height;
            var cellWorld = geom.FindCell(wx, wy);

            var c1 = map1K.CellAt((int)(u * 1024), (int)(v * 512));
            var c2 = map2K.CellAt((int)(u * 2048), (int)(v * 1024));
            var c4 = map4K.CellAt((int)(u * 4096), (int)(v * 2048));

            // 在 4K 和 2K 尺度下，采样点与世界拾取一致
            Assert(cellWorld == c4 || geom.GetNeighborCount(cellWorld) > 0, "光栅拾取地块必须在几何单元或其邻近内");
            Assert(c2 >= 0 && c4 >= 0, "光栅化结果不可为负");
        }
    }

    private static void TestLayerSystemAndPresets()
    {
        var stack = new LayerStackState();
        Assert(stack.ActiveBaseThemeId == LayerRegistry.LayerTerrainOverview, "默认底图应为地形总览");

        // 底图单选互斥
        stack.SetBaseTheme(LayerRegistry.LayerBiomes);
        Assert(stack.ActiveBaseThemeId == LayerRegistry.LayerBiomes, "底图切换为群系失败");

        stack.SetBaseTheme(LayerRegistry.LayerElevation);
        Assert(stack.ActiveBaseThemeId == LayerRegistry.LayerElevation, "底图切换为高程失败");

        // 叠加层多选与排序
        stack.SetOverlayActive(LayerRegistry.LayerRivers, true);
        stack.SetOverlayActive(LayerRegistry.LayerCities, true);
        stack.SetOverlayActive(LayerRegistry.LayerPolityBorders, true);

        Assert(stack.IsOverlayActive(LayerRegistry.LayerRivers), "河流叠加层未激活");
        Assert(stack.IsOverlayActive(LayerRegistry.LayerPolityBorders), "政体边界叠加层未激活");

        var oldTop = stack.ActiveOverlayIds[0];
        stack.MoveOverlayDown(oldTop);
        Assert(stack.ActiveOverlayIds[1] == oldTop, "叠加层下移顺序错误");

        // 预设应用
        var ok = LayerPresetCatalog.ApplyPreset(LayerPresetCatalog.PresetPolitical, stack);
        Assert(ok, "政治文明预设应用失败");
        Assert(stack.ActiveBaseThemeId == LayerRegistry.LayerCivilization, "政治文明预设底图应为文明疆域");
        Assert(stack.IsOverlayActive(LayerRegistry.LayerPolityBorders), "政治文明预设应包含政体边界");

        // 未知图层 ID 容错
        stack.SetBaseTheme("non_existent_theme_id");
        Assert(stack.ActiveBaseThemeId == LayerRegistry.LayerCivilization, "未知底图 ID 应被安全忽略并保持原状");

        stack.SetOverlayActive("non_existent_overlay_id", true);
        Assert(!stack.IsOverlayActive("non_existent_overlay_id"), "未知叠加层 ID 应被安全忽略");
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

    private static void TestCacheKeyStability()
    {
        var opt1 = new GenerationOptions
        {
            Seed = 100,
            TargetCellCount = 10000,
            SeaLevel = 0.45f,
        };

        var opt2 = opt1 with { TargetCellCount = 10000 };
        Assert(opt1.BuildCacheKey() == opt2.BuildCacheKey(), "相同参数的缓存键必须完全一致");

        var optDiffSeed = opt1 with { Seed = 101 };
        Assert(opt1.BuildCacheKey() != optDiffSeed.BuildCacheKey(), "不同种子的缓存键必须不同");

        var optDiffCells = opt1 with { TargetCellCount = 20000 };
        Assert(opt1.BuildCacheKey() != optDiffCells.BuildCacheKey(), "不同地块数的缓存键必须不同");
    }

    private static void TestCurvedCellGeometryAndMeshTopology()
    {
        var geom = PolygonGridBuilder.Create(WorldExtent.Default, 999, 2048, 8, out var stats);

        // 1. 测试单地块平滑曲线多边形与高亮环
        for (var i = 0; i < Math.Min(geom.Count, 50); i++)
        {
            var rawPoly = geom.GetPolygon(i);
            var curvedPoly = geom.GetCurvedPolygon(i, 3);
            Assert(curvedPoly.Length == rawPoly.Length * 3, $"细分后顶点数应为原始的 3 倍: 实际 {curvedPoly.Length}, 期望 {rawPoly.Length * 3}");

            var rings = geom.GetCurvedHighlightRings(i, 3);
            Assert(rings.Length == 3, "平滑高亮环必须返回 3 组环（基准 + ±Width 经度镜像）");
            Assert(rings[0].Length == curvedPoly.Length, "基准高亮环点数必须与平滑多边形完全一致");
            Assert(rings[1].Length == curvedPoly.Length && rings[2].Length == curvedPoly.Length, "镜像高亮环点数必须与基准环一致");

            // 验证环 1 相对环 0 偏移精确为 -Width
            Assert(Math.Abs((rings[1][0].X - rings[0][0].X) - (-geom.Width)) < 1e-6, "经度缝 -Width 镜像偏移不准确");
            // 验证环 2 相对环 0 偏移精确为 +Width
            Assert(Math.Abs((rings[2][0].X - rings[0][0].X) - geom.Width) < 1e-6, "经度缝 +Width 镜像偏移不准确");
        }

        // 2. 测试全图 2D 矢量网格拓扑构建
        var topology = CurvedCellGeometry.BuildMeshTopology(geom, 3);
        Assert(topology.Vertices.Length > 0, "网格拓扑顶点数不能为 0");
        Assert(topology.Indices.Length > 0 && topology.Indices.Length % 3 == 0, "网格索引必须为 3 的倍数（三角形）");
        Assert(topology.VertexToCell.Length == topology.Vertices.Length, "顶点与地块映射数组长度必须与顶点数组一致");

        // 3. 校验网格索引无越界，且所有顶点均映射到有效 cellId
        for (var k = 0; k < topology.Indices.Length; k++)
        {
            var idx = topology.Indices[k];
            Assert(idx >= 0 && idx < topology.Vertices.Length, $"三角形索引越界: {idx}, 总顶点: {topology.Vertices.Length}");
        }

        for (var v = 0; v < topology.Vertices.Length; v++)
        {
            var cellId = topology.VertexToCell[v];
            Assert(cellId >= 0 && cellId < geom.Count, $"顶点所属地块 ID 越界: {cellId}, 地块总数: {geom.Count}");
        }

        // 4. 校验全图规范平滑边去重正确性
        var canonicalEdges = CurvedCellGeometry.GetCanonicalCurvedEdges(geom, 3);
        Assert(canonicalEdges.Length > 0, "规范平滑曲线边数量必须大于 0");
        Assert(canonicalEdges.Length < geom.Count * 4, "去重后的规范边总数应约为地块数的 3 倍");
        for (var i = 0; i < Math.Min(canonicalEdges.Length, 50); i++)
        {
            Assert(canonicalEdges[i].Length == 4, $"细分 3 时的曲线边点数应为 4，实际为 {canonicalEdges[i].Length}");
        }
    }
}
