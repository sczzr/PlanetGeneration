using System;
using PlanetGeneration.WorldGen.Polygon;

namespace PolygonSelfTest;

/// <summary>
/// 逐边诊断：把某个地块的每条边摊开，打印它的归属站点、归属距离与自身距离。
/// 用来定位"多边形正确但邻接表为空"这类只有几何数值才能解释的问题。
/// </summary>
internal static class EdgeDiagnostics
{
    public static void Run()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var grid = PolygonGridBuilder.Create(1024, 512, 20260917, out var stats);
        Console.WriteLine($"地块 {stats.CellCount}  点距 {stats.SpacingX:0.###}×{stats.SpacingY:0.###}"
            + $"  顶点 {stats.VertexCount}  平均邻接 {stats.AverageNeighbors:0.00}");

        var samples = new[] { 0, 1, grid.Count / 2, grid.Count - 1 };

        // 邻接数分布：先看清"哪些地块有邻居"，再去解释为什么。
        var histogram = new int[16];
        var firstWithNeighbors = -1;
        for (var i = 0; i < grid.Count; i++)
        {
            var n = grid.GetNeighborCount(i);
            histogram[Math.Min(n, 15)]++;
            if (n > 0 && firstWithNeighbors < 0)
            {
                firstWithNeighbors = i;
            }
        }

        Console.WriteLine("邻接数分布：" + string.Join(", ", DescribeHistogram(histogram)));
        Console.WriteLine($"首个有邻居的地块：{firstWithNeighbors}");
        if (firstWithNeighbors >= 0)
        {
            samples = new[] { 0, firstWithNeighbors, grid.Count / 2, grid.Count - 1 };
        }

        foreach (var cell in samples)
        {
            var polygon = grid.GetPolygon(cell);
            Console.WriteLine();
            Console.WriteLine($"地块 {cell}（列 {cell % grid.Columns} 行 {cell / grid.Columns}）"
                + $" 站点 ({grid.SiteX[cell]:0.####}, {grid.SiteY[cell]:0.####}) 顶点 {polygon.Length}"
                + $" 邻接 [{string.Join(", ", GetNeighbors(grid, cell))}]");

            for (var k = 0; k < polygon.Length; k++)
            {
                var a = polygon[k];
                var b = polygon[(k + 1) % polygon.Length];
                var mid = a.Lerp(b, 0.5d);
                var selfDistance = Math.Sqrt(mid.DistanceSquaredTo(new PolyVec2(grid.SiteX[cell], grid.SiteY[cell])));
                var bruteOwner = FindNearest(grid, mid.X, mid.Y, cell, out var bruteDistance);
                var indexOwner = grid.FindNearestCellExcluding(mid.X, mid.Y, cell, out var indexDistance);

                Console.WriteLine(
                    $"  边 {k}  中点 ({mid.X:0.####}, {mid.Y:0.####})"
                    + $"  自身距 {selfDistance:0.######}"
                    + $"  暴力 {bruteOwner}/{bruteDistance:0.######}"
                    + $"  索引 {indexOwner}/{indexDistance:0.######}"
                    + (bruteOwner == indexOwner ? string.Empty : "  ← 索引与暴力不一致"));
            }
        }
    }

    private static string[] DescribeHistogram(int[] histogram)
    {
        var parts = new System.Collections.Generic.List<string>();
        for (var i = 0; i < histogram.Length; i++)
        {
            if (histogram[i] == 0)
            {
                continue;
            }

            parts.Add(i == histogram.Length - 1 ? $"{i}+:{histogram[i]}" : $"{i}:{histogram[i]}");
        }

        return parts.ToArray();
    }

    private static string[] GetNeighbors(PolygonGrid grid, int cell)
    {
        var count = grid.GetNeighborCount(cell);
        var result = new string[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = grid.GetNeighbor(cell, i).ToString();
        }

        return result;
    }

    /// <summary>暴力最近点（排除自身，环绕距离），作为桶索引的参照实现。</summary>
    private static int FindNearest(PolygonGrid grid, double x, double y, int exclude, out double distance)
    {
        var best = -1;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < grid.Count; i++)
        {
            if (i == exclude)
            {
                continue;
            }

            var d = grid.WrappedDistanceSquared(x, y, grid.SiteX[i], grid.SiteY[i]);
            if (d >= bestDistance)
            {
                continue;
            }

            bestDistance = d;
            best = i;
        }

        distance = best >= 0 ? Math.Sqrt(bestDistance) : double.MaxValue;
        return best;
    }
}
