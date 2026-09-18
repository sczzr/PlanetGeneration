using System;
using System.Collections.Generic;
using System.Text;
using PlanetGeneration.Core.Domain;

namespace PlanetGeneration.Core.Geometry;

/// <summary>一条校验结果。</summary>
public readonly struct PolygonValidationIssue
{
    public string Check { get; }
    public string Detail { get; }

    public PolygonValidationIssue(string check, string detail)
    {
        Check = check;
        Detail = detail;
    }

    public override string ToString() => $"[{Check}] {Detail}";
}

/// <summary>几何自检报告。</summary>
public sealed class PolygonValidationReport
{
    public bool Passed => Issues.Count == 0;
    public List<PolygonValidationIssue> Issues { get; } = new();
    public int CheckedCount { get; internal set; }
    public double AreaCoverageRatio { get; internal set; }
    public double AverageNeighbors { get; internal set; }
    public double MinMaxAreaRatio { get; internal set; }

    internal void Fail(string check, string detail) => Issues.Add(new PolygonValidationIssue(check, detail));

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"校验项 {CheckedCount} 项，{(Passed ? "全部通过" : $"失败 {Issues.Count} 项")}");
        builder.AppendLine($"面积覆盖率 {AreaCoverageRatio:P4}，平均邻接 {AverageNeighbors:0.00}，最小/最大面积比 {MinMaxAreaRatio:0.####}");
        foreach (var issue in Issues)
        {
            builder.AppendLine("  " + issue);
        }

        return builder.ToString();
    }
}

/// <summary>
/// 多边形几何自检器。
/// </summary>
public static class PolygonGridValidator
{
    private const double AreaCoverageTolerance = 0.01d;
    private const int PickSampleCount = 20000;

    public static PolygonValidationReport Validate(CellGeometry grid, int seed = 12345)
    {
        var report = new PolygonValidationReport();
        var count = grid.Count;

        // 1. 顶点数与面积正定性
        report.CheckedCount++;
        var degenerate = 0;
        var negativeArea = 0;
        for (var i = 0; i < count; i++)
        {
            if (grid.GetVertexCount(i) < 3)
            {
                degenerate++;
            }

            if (grid.Area[i] <= 0d)
            {
                negativeArea++;
            }
        }

        if (degenerate > 0)
        {
            report.Fail("多边形顶点数", $"{degenerate} 个地块角点少于 3 个");
        }

        if (negativeArea > 0)
        {
            report.Fail("面积正定", $"{negativeArea} 个地块面积非正");
        }

        // 2. 面积守恒：多边形面积和 == 地图总面积
        report.CheckedCount++;
        var totalArea = 0d;
        var minArea = double.MaxValue;
        var maxArea = 0d;
        for (var i = 0; i < count; i++)
        {
            totalArea += grid.Area[i];
            if (grid.Area[i] < minArea) minArea = grid.Area[i];
            if (grid.Area[i] > maxArea) maxArea = grid.Area[i];
        }

        var mapArea = grid.Width * grid.Height;
        report.AreaCoverageRatio = mapArea > 0d ? totalArea / mapArea : 0d;
        report.MinMaxAreaRatio = maxArea > 0d ? minArea / maxArea : 0d;

        if (Math.Abs(report.AreaCoverageRatio - 1d) > AreaCoverageTolerance)
        {
            report.Fail("面积守恒", $"面积覆盖率 {report.AreaCoverageRatio:P4}，超出 ±{AreaCoverageTolerance:P0} 容差");
        }

        // 3. 多边形凸性
        report.CheckedCount++;
        var nonConvex = 0;
        for (var i = 0; i < count; i++)
        {
            if (!IsConvex(grid, i))
            {
                nonConvex++;
            }
        }

        if (nonConvex > 0)
        {
            report.Fail("凸性", $"{nonConvex} 个多边形非凸");
        }

        // 4. 邻接对称性
        report.CheckedCount++;
        var asymmetric = 0;
        var selfNeighbor = 0;
        var emptyNeighbor = 0;
        var neighborTotal = 0;
        for (var i = 0; i < count; i++)
        {
            var neighborCount = grid.GetNeighborCount(i);
            neighborTotal += neighborCount;

            if (neighborCount < 2)
            {
                emptyNeighbor++;
            }

            for (var k = 0; k < neighborCount; k++)
            {
                var j = grid.GetNeighbor(i, k);
                if (j == i)
                {
                    selfNeighbor++;
                    continue;
                }

                if (!ContainsNeighbor(grid, j, i))
                {
                    asymmetric++;
                }
            }
        }

        report.AverageNeighbors = count > 0 ? neighborTotal / (double)count : 0d;

        if (asymmetric > 0)
        {
            report.Fail("邻接对称", $"{asymmetric} 条邻接关系不对称");
        }

        if (selfNeighbor > 0)
        {
            report.Fail("自邻接", $"{selfNeighbor} 个地块把自己列为邻居");
        }

        if (emptyNeighbor > 0)
        {
            report.Fail("邻接非空", $"{emptyNeighbor} 个地块邻接数少于 2");
        }

        // 5. 经度环绕有效性
        report.CheckedCount++;
        var edgeOwnerMismatch = 0;
        for (var i = 0; i < count; i++)
        {
            var vertexCount = grid.GetVertexCount(i);
            if (vertexCount < 3) continue;

            var start = grid.CellVertexStart[i];
            var end = grid.CellVertexStart[i + 1];
            for (var k = start; k < end; k++)
            {
                var a = new PolyVec2(grid.VertexX[k], grid.VertexY[k]);
                var next = k + 1 == end ? start : k + 1;
                var b = new PolyVec2(grid.VertexX[next], grid.VertexY[next]);
                if (IsPoleEdge(a, b, grid.Height)) continue;

                var mid = a.Lerp(b, 0.5d);
                var owner = grid.FindNearestCellExcluding(mid.X, mid.Y, i, out _);
                if (owner >= 0 && !ContainsNeighbor(grid, i, owner))
                {
                    edgeOwnerMismatch++;
                }
            }
        }

        if (edgeOwnerMismatch > 0)
        {
            report.Fail("环绕邻接拓扑", $"{edgeOwnerMismatch} 条多边形边未在邻接表中对应");
        }

        // 6. 随机抽样拾取一致性
        report.CheckedCount++;
        var pickMismatch = 0;
        var rng = new PolygonRandom((ulong)seed);
        for (var s = 0; s < PickSampleCount; s++)
        {
            var px = rng.NextDouble() * grid.Width;
            var py = rng.NextDouble() * grid.Height;

            var picked = grid.FindCell(px, py);
            var bestCell = 0;
            var bestDistSq = double.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var dsq = grid.Extent.DistanceSquaredWrapped(new PolyVec2(px, py), new PolyVec2(grid.SiteX[i], grid.SiteY[i]));
                if (dsq < bestDistSq)
                {
                    bestDistSq = dsq;
                    bestCell = i;
                }
            }

            if (picked != bestCell)
            {
                var dPicked = Math.Sqrt(grid.Extent.DistanceSquaredWrapped(new PolyVec2(px, py), new PolyVec2(grid.SiteX[picked], grid.SiteY[picked])));
                var dBest = Math.Sqrt(bestDistSq);
                if (Math.Abs(dPicked - dBest) > 1e-6)
                {
                    pickMismatch++;
                }
            }
        }

        if (pickMismatch > 0)
        {
            report.Fail("拾取一致性", $"{pickMismatch}/{PickSampleCount} 个抽样点与暴力最近点不一致");
        }

        return report;
    }

    private static bool IsConvex(CellGeometry grid, int cellId)
    {
        var count = grid.GetVertexCount(cellId);
        if (count < 3) return false;

        var start = grid.CellVertexStart[cellId];
        var sign = 0;
        for (var i = 0; i < count; i++)
        {
            var a = new PolyVec2(grid.VertexX[start + i], grid.VertexY[start + i]);
            var b = new PolyVec2(grid.VertexX[start + ((i + 1) % count)], grid.VertexY[start + ((i + 1) % count)]);
            var c = new PolyVec2(grid.VertexX[start + ((i + 2) % count)], grid.VertexY[start + ((i + 2) % count)]);

            var cross = (b - a).Cross(c - b);
            if (Math.Abs(cross) <= 1e-9d) continue;

            var currentSign = cross > 0d ? 1 : -1;
            if (sign == 0) sign = currentSign;
            else if (sign != currentSign) return false;
        }

        return true;
    }

    private static bool ContainsNeighbor(CellGeometry grid, int cell, int target)
    {
        var start = grid.CellNeighborStart[cell];
        var end = grid.CellNeighborStart[cell + 1];
        for (var k = start; k < end; k++)
        {
            if (grid.CellNeighbors[k] == target) return true;
        }
        return false;
    }

    private static bool IsPoleEdge(PolyVec2 a, PolyVec2 b, double height)
    {
        const double eps = 1e-4d;
        var onTop = Math.Abs(a.Y) <= eps && Math.Abs(b.Y) <= eps;
        var onBottom = Math.Abs(a.Y - height) <= eps && Math.Abs(b.Y - height) <= eps;
        return onTop || onBottom;
    }
}
