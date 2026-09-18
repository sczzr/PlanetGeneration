using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Domain;

/// <summary>
/// 只读地块几何与空间拓扑。
///
/// 几何属性在网格构建完成后保持只读，不随模拟纪元或图层切换改变。
/// A/B 对比世界如果拥有相同的种子和地块规模，可以安全共享同一份 CellGeometry，
/// 但各自拥有独立的 CellFields。
/// </summary>
public sealed class CellGeometry
{
    private readonly SiteIndex _index;

    public CellGeometry(
        WorldExtent extent,
        double spacingX,
        double spacingY,
        int columns,
        int rows,
        double[] siteX,
        double[] siteY,
        double[] vertexX,
        double[] vertexY,
        int[] cellVertexStart,
        int[] cellNeighborStart,
        int[] cellNeighbors,
        bool[] cellPole,
        bool[] cellSeam,
        double[] area,
        double[] centroidX,
        double[] centroidY,
        SiteIndex index)
    {
        Extent = extent;
        SpacingX = spacingX;
        SpacingY = spacingY;
        Columns = columns;
        Rows = rows;
        SiteX = siteX;
        SiteY = siteY;
        VertexX = vertexX;
        VertexY = vertexY;
        CellVertexStart = cellVertexStart;
        CellNeighborStart = cellNeighborStart;
        CellNeighbors = cellNeighbors;
        CellPole = cellPole;
        CellSeam = cellSeam;
        Area = area;
        CentroidX = centroidX;
        CentroidY = centroidY;
        _index = index;
    }

    /// <summary>世界逻辑范围与环绕规则。</summary>
    public WorldExtent Extent { get; }

    public double Width => Extent.Width;
    public double Height => Extent.Height;

    /// <summary>抖动前的横向点距；SpacingX × Columns == Width。</summary>
    public double SpacingX { get; }

    /// <summary>抖动前的纵向点距。</summary>
    public double SpacingY { get; }

    /// <summary>规则方格列数。</summary>
    public int Columns { get; }

    /// <summary>规则方格行数。</summary>
    public int Rows { get; }

    /// <summary>地块总数。</summary>
    public int Count => SiteX.Length;

    /// <summary>地块中心横坐标（[0, Width)）。</summary>
    public double[] SiteX { get; }

    /// <summary>地块中心纵坐标。</summary>
    public double[] SiteY { get; }

    /// <summary>扁平顶点横坐标（未展开帧）。</summary>
    public double[] VertexX { get; }

    /// <summary>扁平顶点纵坐标。</summary>
    public double[] VertexY { get; }

    /// <summary>CSR：地块 i 的顶点区间为 [CellVertexStart[i], CellVertexStart[i + 1])。</summary>
    public int[] CellVertexStart { get; }

    /// <summary>CSR：地块 i 的邻接区间为 [CellNeighborStart[i], CellNeighborStart[i + 1])。</summary>
    public int[] CellNeighborStart { get; }

    /// <summary>扁平邻接表，按地块编号升序。</summary>
    public int[] CellNeighbors { get; }

    /// <summary>是否贴着南北地图边界（极圈硬边界）。</summary>
    public bool[] CellPole { get; }

    /// <summary>多边形是否骑在经度缝上。</summary>
    public bool[] CellSeam { get; }

    /// <summary>多边形面积。</summary>
    public double[] Area { get; }

    /// <summary>多边形质心横坐标。</summary>
    public double[] CentroidX { get; }

    /// <summary>多边形质心纵坐标。</summary>
    public double[] CentroidY { get; }

    /// <summary>地块角点数量。</summary>
    public int GetVertexCount(int cellId) => CellVertexStart[cellId + 1] - CellVertexStart[cellId];

    /// <summary>地块邻接数量。</summary>
    public int GetNeighborCount(int cellId) => CellNeighborStart[cellId + 1] - CellNeighborStart[cellId];

    /// <summary>取地块的第 slot 个邻居。</summary>
    public int GetNeighbor(int cellId, int slot) => CellNeighbors[CellNeighborStart[cellId] + slot];

    /// <summary>取地块角点序列（未展开帧，逆时针）。</summary>
    public PolyVec2[] GetPolygon(int cellId)
    {
        var start = CellVertexStart[cellId];
        var end = CellVertexStart[cellId + 1];
        var result = new PolyVec2[end - start];
        for (var i = start; i < end; i++)
        {
            result[i - start] = new PolyVec2(VertexX[i], VertexY[i]);
        }

        return result;
    }

    /// <summary>取地块质心。</summary>
    public PolyVec2 GetCentroid(int cellId) => new(CentroidX[cellId], CentroidY[cellId]);

    /// <summary>取地块中心站点。</summary>
    public PolyVec2 GetSite(int cellId) => new(SiteX[cellId], SiteY[cellId]);

    /// <summary>
    /// 取用于高亮绘制的多边形环（含未展开与经度镜像副本）。
    /// </summary>
    public PolyVec2[][] GetHighlightRings(int cellId)
    {
        var vertexCount = GetVertexCount(cellId);
        var rings = new PolyVec2[3][];
        if (vertexCount < 3)
        {
            rings[0] = Array.Empty<PolyVec2>();
            rings[1] = Array.Empty<PolyVec2>();
            rings[2] = Array.Empty<PolyVec2>();
            return rings;
        }

        var start = CellVertexStart[cellId];
        var referenceX = CentroidX[cellId];
        var basePoints = new PolyVec2[vertexCount];
        for (var i = 0; i < vertexCount; i++)
        {
            var vx = VertexX[start + i];
            vx -= Math.Round((vx - referenceX) / Width) * Width;
            basePoints[i] = new PolyVec2(vx, VertexY[start + i]);
        }

        for (var ring = 0; ring < 3; ring++)
        {
            var shift = ring switch
            {
                1 => -Width,
                2 => Width,
                _ => 0d,
            };

            var points = new PolyVec2[vertexCount];
            for (var i = 0; i < vertexCount; i++)
            {
                points[i] = new PolyVec2(basePoints[i].X + shift, basePoints[i].Y);
            }

            rings[ring] = points;
        }

        return rings;
    }

    /// <summary>
    /// 精确拾取：均摊 O(1) 查询点 (x, y) 落在哪个 Voronoi 地块。
    /// </summary>
    public int FindCell(double x, double y)
    {
        var id = _index.FindNearest(x, y, -1, out _);
        return id < 0 ? 0 : id;
    }

    /// <summary>
    /// 排除指定地块的最近邻查询（自检用）。
    /// </summary>
    public int FindNearestCellExcluding(double x, double y, int excludeCellId, out double distance)
        => _index.FindNearest(x, y, excludeCellId, out distance);

    /// <summary>
    /// 收集以 (x, y) 为中心、半径 radius 内的所有地块（环形 BFS）。
    /// </summary>
    public List<int> FindAll(double x, double y, double radius)
    {
        var result = new List<int>(32);
        if (radius <= 0d)
        {
            result.Add(FindCell(x, y));
            return result;
        }

        var radiusSquared = radius * radius;
        var visited = new HashSet<int>();
        var seed = FindCell(x, y);
        var frontier = new List<int> { seed };
        visited.Add(seed);

        const int ringSlack = 2;
        var ringsSinceLastInside = 0;
        var maxRings = Math.Max(Columns, Rows) + 2;

        for (var ring = 0; ring < maxRings && frontier.Count > 0; ring++)
        {
            var next = new List<int>();
            var anyInside = false;

            foreach (var cell in frontier)
            {
                var site = new PolyVec2(SiteX[cell], SiteY[cell]);
                if (Extent.DistanceSquaredWrapped(new PolyVec2(x, y), site) <= radiusSquared)
                {
                    anyInside = true;
                    result.Add(cell);
                }

                var neighborStart = CellNeighborStart[cell];
                var neighborEnd = CellNeighborStart[cell + 1];
                for (var k = neighborStart; k < neighborEnd; k++)
                {
                    var neighbor = CellNeighbors[k];
                    if (visited.Add(neighbor))
                    {
                        next.Add(neighbor);
                    }
                }
            }

            ringsSinceLastInside = anyInside ? 0 : ringsSinceLastInside + 1;
            if (ringsSinceLastInside > ringSlack)
            {
                break;
            }

            frontier = next;
        }

        return result;
    }
}
