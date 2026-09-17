using System;
using System.Collections.Generic;
using System.Text;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>一条校验结果。</summary>
public readonly struct PolygonValidationIssue
{
    /// <summary>校验项名称。</summary>
    public string Check { get; }

    /// <summary>问题描述。</summary>
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
    /// <summary>全部校验项是否通过。</summary>
    public bool Passed => Issues.Count == 0;

    /// <summary>失败项列表；为空即通过。</summary>
    public List<PolygonValidationIssue> Issues { get; } = new();

    /// <summary>已执行的校验项数量。</summary>
    public int CheckedCount { get; internal set; }

    /// <summary>多边形面积之和与地图面积之比；正常应非常接近 1。</summary>
    public double AreaCoverageRatio { get; internal set; }

    /// <summary>平均每个地块的邻接数。</summary>
    public double AverageNeighbors { get; internal set; }

    /// <summary>最小/最大地块面积之比；用于发现极端畸形地块。</summary>
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
/// 地块网格的几何自检。
///
/// 这不是"测试脚手架"，而是产线的一部分：裁剪法的正确性可以逐条断言，
/// 所以建图之后可以直接跑一遍自检（开发期可挂在调试开关后面），
/// 把"看起来对"变成"验证过"。
/// </summary>
public static class PolygonGridValidator
{
    /// <summary>面积覆盖率的允许误差；1% 足以暴露取点或裁剪的系统性错误。</summary>
    private const double AreaCoverageTolerance = 0.01d;

    /// <summary>拾取一致性抽查的随机点数量。</summary>
    private const int PickSampleCount = 20000;

    /// <summary>
    /// 执行全部校验。
    /// </summary>
    /// <param name="grid">待校验的地块网格。</param>
    /// <param name="seed">抽查用的随机种子，保证结果可复现。</param>
    public static PolygonValidationReport Validate(PolygonGrid grid, int seed = 12345)
    {
        var report = new PolygonValidationReport();
        var count = grid.Count;

        // ── 1. 每个地块都有合法的多边形 ────────────────────────────────
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
            report.Fail("多边形顶点数", $"{degenerate} 个地块的角点少于 3 个");
        }

        if (negativeArea > 0)
        {
            report.Fail("面积正定", $"{negativeArea} 个地块面积非正（绕向或裁剪有误）");
        }

        // ── 2. 面积守恒：多边形面积之和必须等于地图面积 ─────────────────
        // 这一条能一次性抓出"漏裁""重复覆盖""点阵有空洞"三类错误。
        report.CheckedCount++;
        var totalArea = 0d;
        var minArea = double.MaxValue;
        var maxArea = 0d;
        for (var i = 0; i < count; i++)
        {
            totalArea += grid.Area[i];
            if (grid.Area[i] < minArea)
            {
                minArea = grid.Area[i];
            }

            if (grid.Area[i] > maxArea)
            {
                maxArea = grid.Area[i];
            }
        }

        var mapArea = grid.Width * grid.Height;
        report.AreaCoverageRatio = mapArea > 0d ? totalArea / mapArea : 0d;
        report.MinMaxAreaRatio = maxArea > 0d ? minArea / maxArea : 0d;

        if (Math.Abs(report.AreaCoverageRatio - 1d) > AreaCoverageTolerance)
        {
            report.Fail("面积守恒", $"面积覆盖率 {report.AreaCoverageRatio:P4}，超出 ±{AreaCoverageTolerance:P0} 容差");
        }

        // ── 3. 多边形必须为凸（Voronoi 单元的充要几何性质）──────────────
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
            report.Fail("凸性", $"{nonConvex} 个地块的多边形非凸（裁剪顺序或候选集有误）");
        }

        // ── 4. 邻接对称：j ∈ C[i] 必须等价于 i ∈ C[j] ───────────────────
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
            report.Fail("邻接非空", $"{emptyNeighbor} 个地块的邻接数少于 2");
        }

        // ── 5. 经度环绕（两条结构性判据，比"首尾列必须相邻"可靠得多）────
        //
        // 判据 A：非极边的归属站点集合必须与邻接表完全一致。
        // 判据 B：顶点数 == 邻接数 + 极边数。多边形的每条边要么对应一个邻居，
        //         要么整条落在南北硬边界上；合并共线顶点之后这条等式应当精确成立。
        // 判据 C：没有顶点落在初始包围盒的经度边界（siteX ± W/2）上——
        //         如果镜像点没生效，包围盒的 x 边就会变成多边形的一条边，这里立刻报警。
        //
        // 注意不能要求"最左列与最右列互为邻居"：同列上下行的站点可能抖动到最左，
        // 把中间地块的左边夹住，于是该地块根本不与最右列相邻——这是合法的环面铺砌。
        //
        // 判据 B 只在"候选半径小于半个环绕周期"时严格成立（列数 ≥ 8）。
        // 更小的地图里，同一站点的两个镜像会各切出一条边，属于环面几何的固有现象。
        report.CheckedCount++;
        var edgeOwnerMismatch = 0;
        var edgeCountMismatch = 0;
        var boxVertexCount = 0;
        var strictEdgeCount = grid.Columns >= 8 && grid.Rows >= 8;

        for (var i = 0; i < count; i++)
        {
            var vertexCount = grid.GetVertexCount(i);
            if (vertexCount < 3)
            {
                continue;
            }

            var start = grid.CellVertexStart[i];
            var end = grid.CellVertexStart[i + 1];

            // 第一遍：数出落在南北硬边界上的边。极边不对应任何邻居。
            var poleEdges = 0;
            for (var k = start; k < end; k++)
            {
                var a = new PolyVec2(grid.VertexX[k], grid.VertexY[k]);
                var next = k + 1 == end ? start : k + 1;
                var b = new PolyVec2(grid.VertexX[next], grid.VertexY[next]);
                if (IsPoleEdge(a, b, grid.Height))
                {
                    poleEdges++;
                }
            }

            // 第二遍：每条非极边都必须归属到邻接表里的某个邻居。
            var ownerFailed = false;
            for (var k = start; k < end && !ownerFailed; k++)
            {
                var a = new PolyVec2(grid.VertexX[k], grid.VertexY[k]);
                var next = k + 1 == end ? start : k + 1;
                var b = new PolyVec2(grid.VertexX[next], grid.VertexY[next]);
                if (IsPoleEdge(a, b, grid.Height))
                {
                    continue;
                }

                var mid = a.Lerp(b, 0.5d);
                var owner = grid.FindNearestCellExcluding(mid.X, mid.Y, i, out var ownerDistance);
                var selfDistance = Math.Sqrt(mid.DistanceSquaredTo(new PolyVec2(grid.SiteX[i], grid.SiteY[i])));
                if (owner < 0 || ownerDistance > selfDistance * (1d + 1e-6d))
                {
                    continue;
                }

                if (!ContainsNeighbor(grid, i, owner))
                {
                    ownerFailed = true;
                }
            }

            if (ownerFailed)
            {
                edgeOwnerMismatch++;
            }

            var expected = grid.GetNeighborCount(i) + poleEdges;
            if (vertexCount < expected || (strictEdgeCount && vertexCount != expected))
            {
                edgeCountMismatch++;
            }

            var leftBound = grid.SiteX[i] - (grid.Width * 0.5d);
            var rightBound = grid.SiteX[i] + (grid.Width * 0.5d);
            for (var k = start; k < end; k++)
            {
                var vx = grid.VertexX[k];
                if (Math.Abs(vx - leftBound) <= 1e-6d || Math.Abs(vx - rightBound) <= 1e-6d)
                {
                    boxVertexCount++;
                    break;
                }
            }
        }

        if (edgeOwnerMismatch > 0)
        {
            report.Fail("边归属", $"{edgeOwnerMismatch} 个地块存在不属于邻接表的边");
        }

        if (edgeCountMismatch > 0)
        {
            report.Fail("边数结构", $"{edgeCountMismatch} 个地块的顶点数与邻接数+极边数不符");
        }

        if (boxVertexCount > 0)
        {
            report.Fail("镜像点生效", $"{boxVertexCount} 个地块的顶点落在包围盒经度边界上");
        }

        // ── 6. 拾取一致性：FindCell 的结果必须真的包含该点 ─────────────
        // Voronoi 单元 = "到本站点最近"的区域，所以最近站点查询与点包含判定应当严格一致。
        report.CheckedCount++;
        var random = new PolygonRandom(unchecked((ulong)(uint)seed));
        var pickMismatch = 0;
        var seamShiftUsed = 0;
        for (var sample = 0; sample < PickSampleCount; sample++)
        {
            var x = random.NextDouble() * grid.Width;
            var y = random.NextDouble() * grid.Height;
            var cell = grid.FindCell(x, y);

            if (ContainsPoint(grid, cell, x, y, out var usedShift))
            {
                if (usedShift)
                {
                    seamShiftUsed++;
                }

                continue;
            }

            pickMismatch++;
        }

        if (pickMismatch > 0)
        {
            report.Fail("拾取一致性", $"{PickSampleCount} 个抽样点中有 {pickMismatch} 个不在 FindCell 返回的多边形内");
        }

        // ── 7. 站点与质心必须落在自己的多边形内部 ──────────────────────
        report.CheckedCount++;
        var siteOutside = 0;
        var centroidOutside = 0;
        for (var i = 0; i < count; i++)
        {
            if (!ContainsPoint(grid, i, grid.SiteX[i], grid.SiteY[i], out _))
            {
                siteOutside++;
            }

            if (!ContainsPoint(grid, i, grid.CentroidX[i], grid.CentroidY[i], out _))
            {
                centroidOutside++;
            }
        }

        if (siteOutside > 0)
        {
            report.Fail("站点自包含", $"{siteOutside} 个地块的中心点不在自己的多边形内");
        }

        if (centroidOutside > 0)
        {
            report.Fail("质心自包含", $"{centroidOutside} 个地块的质心不在自己的多边形内");
        }

        return report;
    }

    /// <summary>判断地块 i 的多边形是否凸（允许极小容差，抗浮点抖动）。</summary>
    private static bool IsConvex(PolygonGrid grid, int cellId)
    {
        var start = grid.CellVertexStart[cellId];
        var end = grid.CellVertexStart[cellId + 1];
        var vertexCount = end - start;
        if (vertexCount < 3)
        {
            return false;
        }

        for (var i = 0; i < vertexCount; i++)
        {
            var a = new PolyVec2(grid.VertexX[start + i], grid.VertexY[start + i]);
            var b = new PolyVec2(grid.VertexX[start + ((i + 1) % vertexCount)], grid.VertexY[start + ((i + 1) % vertexCount)]);
            var c = new PolyVec2(grid.VertexX[start + ((i + 2) % vertexCount)], grid.VertexY[start + ((i + 2) % vertexCount)]);

            var cross = (b - a).Cross(c - b);
            if (cross < -1e-9d)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>判断一条边是否整条落在南北地图边界（极圈）上；极边不对应任何邻居。</summary>
    private static bool IsPoleEdge(PolyVec2 a, PolyVec2 b, double height)
    {
        const double epsilon = 1e-6d;
        var onTop = Math.Abs(a.Y) <= epsilon && Math.Abs(b.Y) <= epsilon;
        var onBottom = Math.Abs(a.Y - height) <= epsilon && Math.Abs(b.Y - height) <= epsilon;
        return onTop || onBottom;
    }

    /// <summary>邻接表里是否含有指定邻居（线性扫描，邻接数只有 5~7）。</summary>
    private static bool ContainsNeighbor(PolygonGrid grid, int cellId, int neighborId)
    {
        var start = grid.CellNeighborStart[cellId];
        var end = grid.CellNeighborStart[cellId + 1];
        for (var k = start; k < end; k++)
        {
            if (grid.CellNeighbors[k] == neighborId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 点是否落在地块的多边形内。
    /// 多边形保存在"未展开帧"里，跨缝地块的角点可能整体偏出一个地图宽度，
    /// 因此需要对 x 尝试 0 / ±Width 三种平移后再判定。
    /// </summary>
    private static bool ContainsPoint(PolygonGrid grid, int cellId, double x, double y, out bool usedShift)
    {
        usedShift = false;

        if (ContainsPointRaw(grid, cellId, x, y))
        {
            return true;
        }

        if (ContainsPointRaw(grid, cellId, x - grid.Width, y))
        {
            usedShift = true;
            return true;
        }

        if (ContainsPointRaw(grid, cellId, x + grid.Width, y))
        {
            usedShift = true;
            return true;
        }

        return false;
    }

    /// <summary>射线法判定点是否在多边形内（边界算在内）。</summary>
    private static bool ContainsPointRaw(PolygonGrid grid, int cellId, double x, double y)
    {
        var start = grid.CellVertexStart[cellId];
        var end = grid.CellVertexStart[cellId + 1];
        var inside = false;

        for (var i = start; i < end; i++)
        {
            var ax = grid.VertexX[i];
            var ay = grid.VertexY[i];
            var next = i + 1 == end ? start : i + 1;
            var bx = grid.VertexX[next];
            var by = grid.VertexY[next];

            // 先判是否落在边上（容差 1e-6，抗浮点误差）。
            if (IsOnSegment(x, y, ax, ay, bx, by))
            {
                return true;
            }

            if ((ay > y) != (by > y))
            {
                var t = (y - ay) / (by - ay);
                if (x < ax + (t * (bx - ax)))
                {
                    inside = !inside;
                }
            }
        }

        return inside;
    }

    /// <summary>点是否落在线段上。</summary>
    private static bool IsOnSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        var cross = ((bx - ax) * (py - ay)) - ((by - ay) * (px - ax));
        if (Math.Abs(cross) > 1e-6d)
        {
            return false;
        }

        var dot = ((px - ax) * (bx - ax)) + ((py - ay) * (by - ay));
        if (dot < -1e-6d)
        {
            return false;
        }

        var lengthSquared = ((bx - ax) * (bx - ax)) + ((by - ay) * (by - ay));
        return dot <= lengthSquared + 1e-6d;
    }
}
