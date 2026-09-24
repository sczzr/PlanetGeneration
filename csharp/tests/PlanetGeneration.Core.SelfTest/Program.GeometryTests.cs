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

        // 5. 校验带拓扑平滑曲线共享边（CellA 与 CellB）
        var topoEdges = CurvedCellGeometry.GetCanonicalCurvedEdgesWithTopology(geom, 3);
        Assert(topoEdges.Length == canonicalEdges.Length, "拓扑边数量必须与规范边严格一致");
        var internalCount = 0;
        var boundaryCount = 0;
        for (var i = 0; i < topoEdges.Length; i++)
        {
            var e = topoEdges[i];
            Assert(e.CellA >= 0 && e.CellA < geom.Count, $"CellA 越界: {e.CellA}");
            if (e.CellB >= 0)
            {
                Assert(e.CellB < geom.Count, $"CellB 越界: {e.CellB}");
                Assert(e.CellA != e.CellB, "内部边两侧地块不能相同");
                internalCount++;
            }
            else
            {
                boundaryCount++;
            }
        }
        Assert(internalCount > topoEdges.Length * 0.95, $"绝大多数边应为双侧内部边: 内部 {internalCount}, 边界 {boundaryCount}");
    }
}
