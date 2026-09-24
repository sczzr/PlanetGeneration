using System;
using System.Collections.Generic;

namespace PlanetGeneration.Core.Geometry;

/// <summary>
/// 候选站点：一个地块中心在"某个镜像副本"下的位置，以及到查询点的环绕距离。
/// 经度方向环绕时，同一个站点会以 x ± k·地图宽度 的多个副本参与裁剪，
/// 距离与位置必须成对保存，否则会裁出错误的多边形。
/// </summary>
internal readonly struct Candidate
{
    /// <summary>地块（站点）编号。</summary>
    public int Id { get; }

    /// <summary>镜像到查询点附近的实际坐标。</summary>
    public PolyVec2 Position { get; }

    /// <summary>到查询点的环绕距离。</summary>
    public double Distance { get; }

    public Candidate(int id, PolyVec2 position, double distance)
    {
        Id = id;
        Position = position;
        Distance = distance;
    }
}

/// <summary>按距离升序比较候选站点；裁剪依赖"由近到远"的顺序做提前退出。</summary>
internal sealed class CandidateDistanceComparer : IComparer<Candidate>
{
    public static readonly CandidateDistanceComparer Instance = new();

    public int Compare(Candidate x, Candidate y) => x.Distance.CompareTo(y.Distance);
}

/// <summary>
/// 均匀桶索引：为站点提供"最近点"与"半径内点集"查询，经度方向环绕。
///
/// 抖动方格保证每桶平均只有一个点，因此桶搜索的常数极小；
/// 建图总复杂度接近 O(地块数)，而不是朴素实现的 O(地块数²)。
/// </summary>
public sealed class SiteIndex
{
    private readonly double _width;
    private readonly double _height;
    private readonly double _bucketWidth;
    private readonly double _bucketHeight;
    private readonly double _minBucket;
    private readonly int _columns;
    private readonly int _rows;

    /// <summary>CSR 结构：第 b 个桶的站点在 <see cref="_items"/> 中的区间为 [_bucketStart[b], _bucketStart[b + 1])。</summary>
    private readonly int[] _bucketStart;

    private readonly int[] _items;
    private readonly double[] _siteX;
    private readonly double[] _siteY;

    public SiteIndex(double width, double height, double bucketSize, double[] siteX, double[] siteY)
    {
        _width = width;
        _height = height;
        var validSize = Math.Max(bucketSize, 1e-6d);
        _columns = Math.Max(1, (int)Math.Round(width / validSize));
        _rows = Math.Max(1, (int)Math.Round(height / validSize));
        _bucketWidth = width / _columns;
        _bucketHeight = height / _rows;
        _minBucket = Math.Min(_bucketWidth, _bucketHeight);
        _siteX = siteX;
        _siteY = siteY;

        var bucketCount = _columns * _rows;
        var start = new int[bucketCount + 1];
        var bucketOf = new int[siteX.Length];

        // 第一遍：统计每桶数量（存到 start[b + 1]，随后前缀和变成起始下标）。
        for (var i = 0; i < siteX.Length; i++)
        {
            var bucket = BucketIndexOf(siteX[i], siteY[i]);
            bucketOf[i] = bucket;
            start[bucket + 1]++;
        }

        for (var b = 0; b < bucketCount; b++)
        {
            start[b + 1] += start[b];
        }

        _bucketStart = start;
        _items = new int[siteX.Length];

        // 第二遍：按桶把站点 id 填进扁平数组。
        var cursor = new int[bucketCount];
        for (var i = 0; i < siteX.Length; i++)
        {
            var bucket = bucketOf[i];
            _items[_bucketStart[bucket] + cursor[bucket]] = i;
            cursor[bucket]++;
        }
    }

    /// <summary>把横坐标折算成桶列号（环绕）。</summary>
    private int WrapColumn(int column)
    {
        var wrapped = column % _columns;
        return wrapped < 0 ? wrapped + _columns : wrapped;
    }

    /// <summary>把纵坐标折算成桶行号（钳制，纬度方向不环绕）。</summary>
    private int ClampRow(int row)
    {
        if (row < 0)
        {
            return 0;
        }

        return row >= _rows ? _rows - 1 : row;
    }

    private int BucketIndexOf(double x, double y)
    {
        var column = WrapColumn((int)Math.Floor(x / _bucketWidth));
        var row = ClampRow((int)Math.Floor(y / _bucketHeight));
        return (row * _columns) + column;
    }

    /// <summary>环绕距离的平方：横向取"绕过去更近"的那一侧，纵向不环绕。</summary>
    private double WrappedDistanceSquared(double x0, double y0, double x1, double y1)
    {
        var dx = Math.Abs(x0 - x1);
        if (_width > 0d && dx > _width * 0.5d)
        {
            dx = _width - dx;
        }

        var dy = y0 - y1;
        return (dx * dx) + (dy * dy);
    }

    /// <summary>把站点镜像到查询点附近（±k·地图宽度），返回距离最近的那个副本。</summary>
    public PolyVec2 MirroredPosition(int id, double referenceX)
    {
        var x = _siteX[id];
        if (_width > 0d)
        {
            x -= Math.Round((x - referenceX) / _width) * _width;
        }

        return new PolyVec2(x, _siteY[id]);
    }

    /// <summary>
    /// 枚举某个站点的**全部**镜像副本中落在半径内的那些。
    ///
    /// 周期域上的正确做法是枚举所有副本，而不是只取最近的一个：
    /// 当环绕周期不够大时（例如只有 2 列），同一个站点的两个镜像到查询点的距离可能相等，
    /// 只取其中一个就会漏掉一条约束，裁出错误的多边形。
    /// </summary>
    internal void CollectImages(int id, double referenceX, double referenceY, double radius, List<Candidate> output)
    {
        var radiusSquared = radius * radius;
        var baseIndex = _width > 0d ? (int)Math.Round((_siteX[id] - referenceX) / _width) : 0;

        for (var k = baseIndex - 1; k <= baseIndex + 1; k++)
        {
            var px = _width > 0d ? _siteX[id] - (k * _width) : _siteX[id];
            var dx = px - referenceX;
            var dy = _siteY[id] - referenceY;
            var distSq = (dx * dx) + (dy * dy);
            if (distSq > radiusSquared)
            {
                continue;
            }

            output.Add(new Candidate(id, new PolyVec2(px, _siteY[id]), Math.Sqrt(distSq)));
        }
    }

    /// <summary>
    /// 收集半径内的全部站点（含全部镜像副本），结果未排序。
    /// </summary>
    internal void CollectWithin(double x, double y, double radius, List<Candidate> output)
    {
        var ringCountX = (int)Math.Ceiling(radius / _bucketWidth);
        var ringCountY = (int)Math.Ceiling(radius / _bucketHeight);
        var centerColumn = WrapColumn((int)Math.Floor(x / _bucketWidth));
        var centerRow = ClampRow((int)Math.Floor(y / _bucketHeight));
        var radiusSquared = radius * radius;

        for (var row = centerRow - ringCountY; row <= centerRow + ringCountY; row++)
        {
            if (row < 0 || row >= _rows)
            {
                continue;
            }

            for (var column = centerColumn - ringCountX; column <= centerColumn + ringCountX; column++)
            {
                var bucket = (row * _columns) + WrapColumn(column);
                for (var k = _bucketStart[bucket]; k < _bucketStart[bucket + 1]; k++)
                {
                    var id = _items[k];
                    var baseIndex = _width > 0d ? (int)Math.Round((_siteX[id] - x) / _width) : 0;

                    // 与 CollectImages 同理：所有镜像副本都要参与裁剪。
                    for (var image = baseIndex - 1; image <= baseIndex + 1; image++)
                    {
                        var px = _width > 0d ? _siteX[id] - (image * _width) : _siteX[id];
                        var dx = px - x;
                        var dy = _siteY[id] - y;
                        var distSq = (dx * dx) + (dy * dy);
                        if (distSq > radiusSquared)
                        {
                            continue;
                        }

                        output.Add(new Candidate(id, new PolyVec2(px, _siteY[id]), Math.Sqrt(distSq)));
                    }
                }
            }
        }
    }

    /// <summary>
    /// 查询最近站点（可排除自身）。返回 -1 表示没找到。
    /// 采用逐环扩大搜索：当"当前环的最小可能距离"已经超过已知最优距离时即可停止。
    /// </summary>
    /// <param name="distance">返回的是**真实距离**，不是平方距离。</param>
    public int FindNearest(double x, double y, int excludeId, out double distance)
    {
        // 距离计算使用单周期坐标；拖动地图数个周期后也应命中同一地块。
        x %= _width;
        if (x < 0d) x += _width;
        var centerColumn = WrapColumn((int)Math.Floor(x / _bucketWidth));
        var centerRow = ClampRow((int)Math.Floor(y / _bucketHeight));
        var bestId = -1;
        var bestDistanceSquared = double.MaxValue;

        var maxRing = Math.Max(_columns, _rows) + 2;
        for (var ring = 0; ring <= maxRing; ring++)
        {
            ScanRing(centerColumn, centerRow, ring, x, y, excludeId, ref bestId, ref bestDistanceSquared);

            // 停止判据：环 r 之外的桶里，任何点到查询点的距离都至少是 r × 最小桶边长。
            var ringReach = ring * _minBucket;
            if (bestId >= 0 && ringReach * ringReach > bestDistanceSquared)
            {
                break;
            }
        }

        distance = bestId >= 0 ? Math.Sqrt(bestDistanceSquared) : double.MaxValue;
        return bestId;
    }

    private void ScanRing(
        int centerColumn,
        int centerRow,
        int ring,
        double x,
        double y,
        int excludeId,
        ref int bestId,
        ref double bestDistanceSquared)
    {
        var minRow = centerRow - ring;
        var maxRow = centerRow + ring;

        for (var row = minRow; row <= maxRow; row++)
        {
            if (row < 0 || row >= _rows)
            {
                continue;
            }

            // 只有首尾两行需要整行扫描，中间行只需扫描左右两条边。
            var edgeRow = row == minRow || row == maxRow;
            for (var column = centerColumn - ring; column <= centerColumn + ring; column++)
            {
                if (!edgeRow && column != centerColumn - ring && column != centerColumn + ring)
                {
                    continue;
                }

                var bucket = (row * _columns) + WrapColumn(column);
                for (var k = _bucketStart[bucket]; k < _bucketStart[bucket + 1]; k++)
                {
                    var id = _items[k];
                    if (id == excludeId)
                    {
                        continue;
                    }

                    var distSq = WrappedDistanceSquared(x, y, _siteX[id], _siteY[id]);
                    if (distSq >= bestDistanceSquared)
                    {
                        continue;
                    }

                    bestDistanceSquared = distSq;
                    bestId = id;
                }
            }
        }
    }
}

/// <summary>建图结果：每个地块的多边形角点、邻接表，以及若干诊断计数。</summary>
public sealed class VoronoiBuildResult
{
    /// <summary>每个地块的角点序列（逆时针，首尾不重复）。</summary>
    public required List<PolyVec2>[] Polygons { get; init; }

    /// <summary>每个地块的相邻地块（按 id 升序，只含共享一条边的邻居）。</summary>
    public required List<int>[] Neighbors { get; init; }

    /// <summary>是否贴着南北地图边界（纬度方向不环绕，边界是硬边）。</summary>
    public required bool[] TouchesPole { get; init; }

    /// <summary>多边形是否跨越经度缝（存在角点 x 落在 [0, 地图宽度) 之外）。</summary>
    public required bool[] CrossesSeam { get; init; }

    /// <summary>每个地块为满足校验而额外补裁的轮数；正常应为 0。</summary>
    public required int[] RepairRounds { get; init; }

    /// <summary>校验失败的次数（补裁超限或反复不收敛）；正常应为 0。</summary>
    public required int VerificationFailures { get; init; }

    /// <summary>退化成角点少于 3 个的地块数量；正常应为 0。</summary>
    public required int DegenerateCells { get; init; }
}

/// <summary>
/// Voronoi 地块建图：抖动方格站点 → 半平面裁剪 → 顶点校验 → 邻接提取。
///
/// 与 FMG 的差异（有意为之）：
/// FMG 走 Delaunator 三角剖分再取对偶；这里改成"用二分面逐个裁剪初始包围盒"。
/// 理由有两条：
///   1. C# 侧没有现成的 Delaunator，自实现 Bowyer–Watson 的邻接维护与退化处理容易出隐蔽 bug；
///   2. 裁剪法的正确性可以自证——每个多边形角点都能验证"不存在更近的站点切掉它"，
///      于是几何自检不需要靠肉眼比对，而是可以写成断言。
///
/// 复杂度：候选点按距离升序处理，一旦"候选距离的一半 ≥ 当前多边形最大顶点半径"即可停止，
/// 因为更远的二分面一定切不到多边形。抖动方格下每个地块实际只用到 8~14 个候选。
/// </summary>
public static class VoronoiBuilder
{
    /// <summary>判定"点落在初始包围盒边界上"的容差（像素）。</summary>
    private const double BoundaryEpsilon = 1e-6d;

    /// <summary>校验时允许的相对误差；Voronoi 角点到三个站点严格等距，故只需防浮点抖动。</summary>
    private const double VerificationTolerance = 1e-9d;

    /// <summary>
    /// 构建 Voronoi 地块图。
    /// </summary>
    /// <param name="width">地图宽度（经度方向，环绕周期）。</param>
    /// <param name="height">地图高度（纬度方向，不环绕）。</param>
    /// <param name="siteX">站点横坐标，已归一化到 [0, width)。</param>
    /// <param name="siteY">站点纵坐标，落在 [0, height] 内。</param>
    /// <param name="bucketSize">桶索引的桶边长，取抖动前的点距即可。</param>
    /// <param name="maxRepairRounds">单个地块最多补裁几轮。</param>
    public static VoronoiBuildResult Build(
        double width,
        double height,
        double[] siteX,
        double[] siteY,
        double bucketSize,
        int maxRepairRounds = 8)
    {
        var count = siteX.Length;
        var index = new SiteIndex(width, height, bucketSize, siteX, siteY);

        var polygons = new List<PolyVec2>[count];
        var neighbors = new List<int>[count];
        var touchesPole = new bool[count];
        var crossesSeam = new bool[count];
        var repairRounds = new int[count];

        var verificationFailures = 0;
        var degenerateCells = 0;

        // 候选半径取 3 倍点距：抖动方格下最大顶点半径不会超过约 1.5 倍点距，
        // 3 倍是安全余量，同时把桶搜索限制在 7×7 个桶内。
        var candidateRadius = bucketSize * 3d;

        var candidates = new List<Candidate>(128);
        var extra = new List<Candidate>(8);

        for (var i = 0; i < count; i++)
        {
            var origin = new PolyVec2(siteX[i], siteY[i]);

            candidates.Clear();
            index.CollectWithin(origin.X, origin.Y, candidateRadius, candidates);
            candidates.Sort(CandidateDistanceComparer.Instance);

            extra.Clear();
            var rounds = 0;
            List<PolyVec2> polygon;

            while (true)
            {
                polygon = ClipAll(origin, width, height, candidates, i, extra);

                // 顶点校验：任一角点若存在更近的站点，说明候选集漏了它，补上后重裁。
                var violator = FindViolator(polygon, origin, i, index);
                if (violator < 0)
                {
                    break;
                }

                if (rounds >= maxRepairRounds || ContainsId(extra, violator))
                {
                    verificationFailures++;
                    break;
                }

                // 补上该站点的**全部**镜像副本：周期域上每个副本都是一条独立约束。
                index.CollectImages(violator, origin.X, origin.Y, candidateRadius, extra);
                rounds++;
            }

            repairRounds[i] = rounds;

            if (polygon.Count < 3)
            {
                degenerateCells++;
                polygons[i] = polygon;
                neighbors[i] = new List<int>();
                continue;
            }

            NormalizeOrientation(polygon);

            // 跨经度缝检测：角点跑到 [0, width) 之外，说明这个地块骑在缝上。
            var seam = false;
            for (var k = 0; k < polygon.Count; k++)
            {
                var v = polygon[k];
                if (v.X < -BoundaryEpsilon || v.X > width + BoundaryEpsilon)
                {
                    seam = true;
                    break;
                }
            }

            var pole = false;
            for (var k = 0; k < polygon.Count; k++)
            {
                var y = polygon[k].Y;
                if (Math.Abs(y) <= BoundaryEpsilon || Math.Abs(y - height) <= BoundaryEpsilon)
                {
                    pole = true;
                    break;
                }
            }

            polygons[i] = polygon;
            crossesSeam[i] = seam;
            touchesPole[i] = pole;
            neighbors[i] = ExtractNeighbors(polygon, origin, i, height, index);
        }

        // 保证邻接关系的严格对称性（拓扑无向图性质：若 A 是 B 的邻居，则 B 也是 A 的邻居）
        for (var i = 0; i < count; i++)
        {
            var list = neighbors[i];
            for (var k = 0; k < list.Count; k++)
            {
                var j = list[k];
                if (j >= 0 && j < count && j != i)
                {
                    var otherList = neighbors[j];
                    if (!otherList.Contains(i))
                    {
                        otherList.Add(i);
                    }
                }
            }
        }

        for (var i = 0; i < count; i++)
        {
            neighbors[i].Sort();
        }

        return new VoronoiBuildResult
        {
            Polygons = polygons,
            Neighbors = neighbors,
            TouchesPole = touchesPole,
            CrossesSeam = crossesSeam,
            RepairRounds = repairRounds,
            VerificationFailures = verificationFailures,
            DegenerateCells = degenerateCells,
        };
    }

    /// <summary>从初始包围盒开始，按距离升序裁剪，再叠加补裁候选。</summary>
    private static List<PolyVec2> ClipAll(
        PolyVec2 origin,
        double width,
        double height,
        List<Candidate> candidates,
        int selfId,
        List<Candidate> extra)
    {
        var polygon = BuildInitialBox(origin, width, height);

        for (var k = 0; k < candidates.Count; k++)
        {
            var candidate = candidates[k];
            if (candidate.Id == selfId)
            {
                continue;
            }

            // 提前退出：二分面到站点的距离是候选距离的一半，
            // 若它已经不小于多边形最大顶点半径，则该面切不到多边形，更远的候选更不可能。
            if (candidate.Distance * 0.5d >= MaxVertexRadius(polygon, origin))
            {
                break;
            }

            var clipped = ClipHalfPlane(polygon, origin, candidate.Position);
            if (clipped.Count < 3)
            {
                break;
            }

            polygon = clipped;
        }

        for (var k = 0; k < extra.Count; k++)
        {
            var clipped = ClipHalfPlane(polygon, origin, extra[k].Position);
            if (clipped.Count < 3)
            {
                break;
            }

            polygon = clipped;
        }

        RemoveRedundantVertices(polygon);
        return polygon;
    }

    /// <summary>
    /// 初始包围盒：横向以站点为中心、宽一个环绕周期；纵向就是地图上下边界（极圈）。
    /// 纵向用真实地图边界是有意的——纬度方向不环绕，极点就是硬边。
    /// </summary>
    private static List<PolyVec2> BuildInitialBox(PolyVec2 origin, double width, double height)
    {
        var left = origin.X - (width * 0.5d);
        var right = origin.X + (width * 0.5d);
        return new List<PolyVec2>(8)
        {
            new(left, 0d),
            new(right, 0d),
            new(right, height),
            new(left, height),
        };
    }

    /// <summary>
    /// 用二分面裁剪多边形：保留"到 <paramref name="self"/> 不比到 <paramref name="other"/> 远"的一侧。
    /// 即 p·(other - self) ≤ (|other|² - |self|²) / 2。
    /// </summary>
    private static List<PolyVec2> ClipHalfPlane(List<PolyVec2> polygon, PolyVec2 self, PolyVec2 other)
    {
        var nx = other.X - self.X;
        var ny = other.Y - self.Y;
        var normSquared = (nx * nx) + (ny * ny);
        if (normSquared <= 1e-12d)
        {
            // 两个站点重合（理论上不会发生），该二分面无意义。
            return polygon;
        }

        var offset = (((other.X * other.X) + (other.Y * other.Y)) - ((self.X * self.X) + (self.Y * self.Y))) * 0.5d;
        var output = new List<PolyVec2>(polygon.Count + 2);

        for (var i = 0; i < polygon.Count; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % polygon.Count];
            var f1 = (p1.X * nx) + (p1.Y * ny) - offset;
            var f2 = (p2.X * nx) + (p2.Y * ny) - offset;

            if (f1 <= 0d)
            {
                output.Add(p1);
            }

            // 只在与裁剪线真正相交时插入交点；f 恰为 0 的顶点已经原样保留，避免重复点。
            if ((f1 < 0d && f2 > 0d) || (f1 > 0d && f2 < 0d))
            {
                var t = f1 / (f1 - f2);
                output.Add(p1.Lerp(p2, t));
            }
        }

        return output;
    }

    /// <summary>
    /// 去掉重复顶点与共线顶点。
    ///
    /// 裁剪（Sutherland–Hodgman）会在两条约束线几乎相切处留下多余的共线顶点：
    /// 它不影响形状，却会让顶点数虚高、拖慢光栅化，也让"边数 == 邻接数 + 极边数"
    /// 这条结构性判据失真。容差 1e-7px 远低于任何有意义的几何尺度，
    /// 而近乎平行的两条约束线产生的交点距离远超该容差，因此不会被误删。
    /// </summary>
    private static void RemoveRedundantVertices(List<PolyVec2> polygon)
    {
        const double duplicateToleranceSquared = 1e-18d;
        const double collinearTolerance = 1e-7d;

        var changed = true;
        while (changed && polygon.Count > 3)
        {
            changed = false;
            for (var i = polygon.Count - 1; i >= 0 && polygon.Count > 3; i--)
            {
                var previous = polygon[(i - 1 + polygon.Count) % polygon.Count];
                var current = polygon[i];
                var next = polygon[(i + 1) % polygon.Count];

                if (current.DistanceSquaredTo(next) < duplicateToleranceSquared)
                {
                    polygon.RemoveAt(i);
                    changed = true;
                    continue;
                }

                var edgeX = next.X - previous.X;
                var edgeY = next.Y - previous.Y;
                var edgeLength = Math.Sqrt((edgeX * edgeX) + (edgeY * edgeY));
                if (edgeLength > 1e-12d)
                {
                    var distance = Math.Abs((edgeX * (current.Y - previous.Y)) - (edgeY * (current.X - previous.X))) / edgeLength;
                    if (distance < collinearTolerance)
                    {
                        polygon.RemoveAt(i);
                        changed = true;
                    }
                }
            }
        }
    }

    /// <summary>多边形顶点到站点的最大距离；用于裁剪的提前退出判定。</summary>
    private static double MaxVertexRadius(List<PolyVec2> polygon, PolyVec2 origin)
    {
        var maxSquared = 0d;
        for (var i = 0; i < polygon.Count; i++)
        {
            var distSquared = polygon[i].DistanceSquaredTo(origin);
            if (distSquared > maxSquared)
            {
                maxSquared = distSquared;
            }
        }

        return Math.Sqrt(maxSquared);
    }

    /// <summary>
    /// 校验：找出"比当前站点更接近某个角点"的站点。
    /// 这是裁剪法正确性的证书——返回 -1 说明多边形已经就是真正的 Voronoi 单元。
    /// </summary>
    private static int FindViolator(
        List<PolyVec2> polygon,
        PolyVec2 origin,
        int selfId,
        SiteIndex index)
    {
        var worstId = -1;
        var worstDistance = double.MaxValue;

        for (var i = 0; i < polygon.Count; i++)
        {
            var vertex = polygon[i];
            var selfDistance = Math.Sqrt(vertex.DistanceSquaredTo(origin));
            var otherId = index.FindNearest(vertex.X, vertex.Y, selfId, out var otherDistance);

            if (otherId < 0 || otherDistance >= selfDistance * (1d - VerificationTolerance))
            {
                continue;
            }

            if (otherDistance >= worstDistance)
            {
                continue;
            }

            worstDistance = otherDistance;
            worstId = otherId;
        }

        return worstId;
    }

    /// <summary>把多边形统一成逆时针（有向面积为正）。</summary>
    private static void NormalizeOrientation(List<PolyVec2> polygon)
    {
        if (SignedArea(polygon) < 0d)
        {
            polygon.Reverse();
        }
    }

    /// <summary>有向面积（鞋带公式）；逆时针为正。</summary>
    public static double SignedArea(IReadOnlyList<PolyVec2> polygon)
    {
        var sum = 0d;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            sum += (a.X * b.Y) - (b.X * a.Y);
        }

        return sum * 0.5d;
    }

    /// <summary>多边形质心（面积加权）；退化为零面积时回退到顶点平均值。</summary>
    public static PolyVec2 Centroid(IReadOnlyList<PolyVec2> polygon)
    {
        var area = 0d;
        var x = 0d;
        var y = 0d;

        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            var cross = (a.X * b.Y) - (b.X * a.Y);
            area += cross;
            x += (a.X + b.X) * cross;
            y += (a.Y + b.Y) * cross;
        }

        area *= 0.5d;
        if (Math.Abs(area) <= 1e-12d)
        {
            var sumX = 0d;
            var sumY = 0d;
            for (var i = 0; i < polygon.Count; i++)
            {
                sumX += polygon[i].X;
                sumY += polygon[i].Y;
            }

            var count = Math.Max(polygon.Count, 1);
            return new PolyVec2(sumX / count, sumY / count);
        }

        var factor = 1d / (6d * area);
        return new PolyVec2(x * factor, y * factor);
    }

    /// <summary>
    /// 从多边形边提取相邻地块：每条边取中点，找最近的"非自身"站点。
    /// Voronoi 边上任意一点到两侧站点等距，所以真正共享这条边的邻居必然被选中。
    /// 落在南北地图边界上的边属于硬边界，不产生邻居。
    /// </summary>
    private static List<int> ExtractNeighbors(
        List<PolyVec2> polygon,
        PolyVec2 origin,
        int selfId,
        double height,
        SiteIndex index)
    {
        var result = new List<int>(8);

        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];

            if (IsPoleEdge(a, b, height))
            {
                continue;
            }

            var mid = a.Lerp(b, 0.5d);
            var owner = index.FindNearest(mid.X, mid.Y, selfId, out var ownerDistance);
            if (owner < 0)
            {
                continue;
            }

            var selfDistance = Math.Sqrt(mid.DistanceSquaredTo(origin));
            if (ownerDistance > selfDistance * (1d + 1e-6d))
            {
                continue;
            }

            if (!result.Contains(owner))
            {
                result.Add(owner);
            }
        }

        result.Sort();
        return result;
    }

    /// <summary>判断一条边是否整条落在南北地图边界上。</summary>
    private static bool IsPoleEdge(PolyVec2 a, PolyVec2 b, double height)
    {
        var onTop = Math.Abs(a.Y) <= BoundaryEpsilon && Math.Abs(b.Y) <= BoundaryEpsilon;
        var onBottom = Math.Abs(a.Y - height) <= BoundaryEpsilon && Math.Abs(b.Y - height) <= BoundaryEpsilon;
        return onTop || onBottom;
    }

    private static bool ContainsId(List<Candidate> candidates, int id)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Id == id)
            {
                return true;
            }
        }

        return false;
    }
}
