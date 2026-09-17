using System;
using System.Collections.Generic;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>
/// 多边形地块网格：一次生成、之后只读，是地块属性的真源。
///
/// 几何与拓扑都用 CSR（压缩稀疏行）扁平存储，而不是 <c>int[][] / List[]</c>：
///   1. 32k~64k 个地块时，锯齿数组的数组头与分散分配会造成明显的 GC 压力；
///   2. 扁平数组可以直接序列化、可以整体交给并行循环；
///   3. 内存占用可预测（约 6 个顶点/地块 × 16 字节 ≈ 100 字节/地块）。
///
/// 坐标系约定：
///   · 站点坐标已归一化到 [0, Width)，横向环绕；
///   · 多边形顶点保留"未展开帧"的原始坐标，允许 x 落在 [0, Width) 之外。
///     骑在经度缝上的地块因此仍是一个完整连续的多边形，
///     由渲染层负责在 x ± Width 处补画一份，而不是在这里把多边形切断。
/// </summary>
public sealed class PolygonGrid
{
    /// <summary>桶索引的桶边长；建图后仍需保留，用于精确拾取查询。</summary>
    private readonly SiteIndex _index;

    private PolygonGrid(
        double width,
        double height,
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
        PolygonFields fields,
        SiteIndex index)
    {
        Width = width;
        Height = height;
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
        Fields = fields;
        _index = index;
    }

    /// <summary>地图宽度，也是经度方向的环绕周期（源栅格像素）。</summary>
    public double Width { get; }

    /// <summary>地图高度（纬度方向，不环绕）。</summary>
    public double Height { get; }

    /// <summary>抖动前的横向点距；<c>SpacingX × Columns == Width</c>，因此点阵严格周期。</summary>
    public double SpacingX { get; }

    /// <summary>抖动前的纵向点距。</summary>
    public double SpacingY { get; }

    /// <summary>规则方格列数，等于横向地块数。</summary>
    public int Columns { get; }

    /// <summary>规则方格行数，等于纵向地块数。</summary>
    public int Rows { get; }

    /// <summary>地块总数。</summary>
    public int Count => SiteX.Length;

    /// <summary>地块中心横坐标（已归一化到 [0, Width)）。</summary>
    public double[] SiteX { get; }

    /// <summary>地块中心纵坐标。</summary>
    public double[] SiteY { get; }

    /// <summary>扁平顶点横坐标（未展开帧，可能超出 [0, Width)）。</summary>
    public double[] VertexX { get; }

    /// <summary>扁平顶点纵坐标。</summary>
    public double[] VertexY { get; }

    /// <summary>CSR：地块 <c>i</c> 的顶点区间为 <c>[CellVertexStart[i], CellVertexStart[i + 1])</c>。</summary>
    public int[] CellVertexStart { get; }

    /// <summary>CSR：地块 <c>i</c> 的邻接区间为 <c>[CellNeighborStart[i], CellNeighborStart[i + 1])</c>。</summary>
    public int[] CellNeighborStart { get; }

    /// <summary>扁平邻接表，按地块编号升序。</summary>
    public int[] CellNeighbors { get; }

    /// <summary>是否贴着南北地图边界（极圈是硬边，这些地块的多边形被地图上下边截断）。</summary>
    public bool[] CellPole { get; }

    /// <summary>多边形是否骑在经度缝上（渲染时需要补画一份）。</summary>
    public bool[] CellSeam { get; }

    /// <summary>多边形面积（源栅格像素²）。旧系统没有这个维度，人口/权重类计算可以开始用它。</summary>
    public double[] Area { get; }

    /// <summary>多边形质心横坐标（未展开帧，可能超出 [0, Width)）。</summary>
    public double[] CentroidX { get; }

    /// <summary>多边形质心纵坐标。</summary>
    public double[] CentroidY { get; }

    /// <summary>地块属性集合。</summary>
    public PolygonFields Fields { get; }

    /// <summary>地块 <c>i</c> 的顶点数量。</summary>
    public int GetVertexCount(int cellId) => CellVertexStart[cellId + 1] - CellVertexStart[cellId];

    /// <summary>地块 <c>i</c> 的邻接数量。</summary>
    public int GetNeighborCount(int cellId) => CellNeighborStart[cellId + 1] - CellNeighborStart[cellId];

    /// <summary>取地块 <c>i</c> 的第 <paramref name="slot"/> 个邻居（0 起）。</summary>
    public int GetNeighbor(int cellId, int slot) => CellNeighbors[CellNeighborStart[cellId] + slot];

    /// <summary>取地块 <c>i</c> 的多边形角点（未展开帧，首尾不重复，逆时针）。</summary>
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

    /// <summary>取地块 <c>i</c> 的多边形质心。</summary>
    public PolyVec2 GetCentroid(int cellId) => new(CentroidX[cellId], CentroidY[cellId]);

    /// <summary>
    /// 取用于**高亮绘制**的多边形环，共 3 份。
    ///
    /// 多边形保存在"未展开帧"里，骑经度缝的地块顶点会散落在 x ≈ 0 与 x ≈ Width 两侧，
    /// 直接画会被拉成横贯整张图的细条。这里的做法是：
    ///   1. 把每个顶点平移到"离质心最近"的镜像副本上，得到一个连续的本地多边形；
    ///   2. 再整体按 0 / −Width / +Width 各输出一份。
    /// 缝上的地块必然有一份落在画布内，超出的部分交给调用方裁剪——
    /// 这样就不需要在多边形层面做缝切分。
    ///
    /// 放在核心层而不是 Godot 层，是为了让这段几何能被自检覆盖。
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

            // 平移到离质心最近的镜像副本。
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
    /// 精确拾取：返回包含该点的地块编号。
    /// Voronoi 单元的定义就是"到本站点比到任何其他站点都近"的区域，
    /// 所以"最近站点"与"包含点"严格等价——拾取结果不可能落在多边形之外。
    /// 复杂度是桶查询的均摊 O(1)。
    /// </summary>
    public int FindCell(double x, double y)
    {
        var id = _index.FindNearest(x, y, -1, out _);
        return id < 0 ? 0 : id;
    }

    /// <summary>
    /// 排除指定地块后的最近地块查询。
    /// 用于几何自检（判断某条边的归属站点）与拓扑工具，不对外作为拾取接口。
    /// </summary>
    internal int FindNearestCellExcluding(double x, double y, int excludeCellId, out double distance)
        => _index.FindNearest(x, y, excludeCellId, out distance);

    /// <summary>
    /// 收集以 (x, y) 为中心、半径 <paramref name="radius"/> 内的全部地块（环绕距离）。
    /// 从精确拾取的种子出发沿邻接做环形 BFS——这就是"刷选/框选地块"的基础。
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

        // 种子必须用精确拾取：快速拾取在方格边界附近可能选错地块，
        // 而种子一旦落在半径之外，整次 BFS 会直接空手而归。
        var seed = FindCell(x, y);
        var frontier = new List<int> { seed };
        visited.Add(seed);

        // 多扩两环再收手：地块是凸的，但图上距离与欧氏距离并不严格一致，
        // 留一点余量可以避免漏掉"绕一圈才够近"的地块。
        const int ringSlack = 2;
        var ringsSinceLastInside = 0;
        var maxRings = Math.Max(Columns, Rows) + 2;

        for (var ring = 0; ring < maxRings && frontier.Count > 0; ring++)
        {
            var next = new List<int>();
            var anyInside = false;

            foreach (var cell in frontier)
            {
                if (WrappedDistanceSquared(x, y, SiteX[cell], SiteY[cell]) <= radiusSquared)
                {
                    anyInside = true;
                    result.Add(cell);
                }

                // 无论是否落在半径内都继续外扩，否则"半径外的地块"会挡住它更远的邻居。
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

    /// <summary>环绕距离的平方：横向取"绕过去更近"的一侧，纵向不环绕。</summary>
    public double WrappedDistanceSquared(double x0, double y0, double x1, double y1)
    {
        var dx = Math.Abs(x0 - x1);
        if (dx > Width * 0.5d)
        {
            dx = Width - dx;
        }

        var dy = y0 - y1;
        return (dx * dx) + (dy * dy);
    }

    /// <summary>环绕距离。</summary>
    public double WrappedDistance(double x0, double y0, double x1, double y1)
        => Math.Sqrt(WrappedDistanceSquared(x0, y0, x1, y1));

    /// <summary>把横坐标归一化到 [0, Width)。</summary>
    public double NormalizeX(double x)
    {
        var wrapped = x % Width;
        return wrapped < 0d ? wrapped + Width : wrapped;
    }

    /// <summary>由建图结果构造网格；只应由 <c>PolygonGridBuilder</c> 调用。</summary>
    internal static PolygonGrid FromBuild(
        double width,
        double height,
        double spacingX,
        double spacingY,
        int columns,
        int rows,
        double[] siteX,
        double[] siteY,
        VoronoiBuildResult build,
        PolygonFields fields,
        SiteIndex index)
    {
        var count = siteX.Length;

        var vertexTotal = 0;
        var neighborTotal = 0;
        for (var i = 0; i < count; i++)
        {
            vertexTotal += build.Polygons[i].Count;
            neighborTotal += build.Neighbors[i].Count;
        }

        var vertexX = new double[vertexTotal];
        var vertexY = new double[vertexTotal];
        var cellVertexStart = new int[count + 1];
        var cellNeighborStart = new int[count + 1];
        var cellNeighbors = new int[neighborTotal];
        var area = new double[count];
        var centroidX = new double[count];
        var centroidY = new double[count];

        var vertexCursor = 0;
        var neighborCursor = 0;

        for (var i = 0; i < count; i++)
        {
            var polygon = build.Polygons[i];
            cellVertexStart[i] = vertexCursor;
            for (var k = 0; k < polygon.Count; k++)
            {
                vertexX[vertexCursor] = polygon[k].X;
                vertexY[vertexCursor] = polygon[k].Y;
                vertexCursor++;
            }

            area[i] = VoronoiBuilder.SignedArea(polygon);
            var centroid = VoronoiBuilder.Centroid(polygon);
            centroidX[i] = centroid.X;
            centroidY[i] = centroid.Y;

            var neighborList = build.Neighbors[i];
            cellNeighborStart[i] = neighborCursor;
            for (var k = 0; k < neighborList.Count; k++)
            {
                cellNeighbors[neighborCursor] = neighborList[k];
                neighborCursor++;
            }
        }

        cellVertexStart[count] = vertexCursor;
        cellNeighborStart[count] = neighborCursor;

        return new PolygonGrid(
            width,
            height,
            spacingX,
            spacingY,
            columns,
            rows,
            siteX,
            siteY,
            vertexX,
            vertexY,
            cellVertexStart,
            cellNeighborStart,
            cellNeighbors,
            build.TouchesPole,
            build.CrossesSeam,
            area,
            centroidX,
            centroidY,
            fields,
            index);
    }
}
