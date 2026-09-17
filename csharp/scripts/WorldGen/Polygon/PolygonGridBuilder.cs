using System;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>
/// 多边形核心专用的确定性伪随机数发生器（SplitMix64）。
///
/// 刻意不使用 Godot 的 <c>RandomNumberGenerator</c>：几何部分要能脱离编辑器独立编译与自检。
/// 同一种子 + 同一地图尺寸 + 同一目标地块数，必定得到同一张地块图。
/// </summary>
internal struct PolygonRandom
{
    private ulong _state;

    public PolygonRandom(ulong seed)
    {
        // 0 会让 SplitMix64 的首个输出退化，替换成黄金比例常数。
        _state = seed == 0UL ? 0x9E3779B97F4A7C15UL : seed;
    }

    /// <summary>取下一个 64 位随机数。</summary>
    public ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>返回 [0, 1) 区间的浮点数。</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>返回 [-1, 1) 区间的浮点数，用于对称抖动。</summary>
    public double NextSigned() => (NextDouble() * 2d) - 1d;
}

/// <summary>建图过程的统计信息，供日志与性能预算使用。</summary>
public sealed class PolygonBuildStats
{
    /// <summary>目标地块数（用户设定）。</summary>
    public int CellsDesired { get; init; }

    /// <summary>实际地块数；受点距取整影响，通常与目标接近但不相等。</summary>
    public int CellCount { get; init; }

    /// <summary>抖动前的横向点距（源栅格像素）。</summary>
    public double SpacingX { get; init; }

    /// <summary>抖动前的纵向点距（源栅格像素）。</summary>
    public double SpacingY { get; init; }

    /// <summary>多边形顶点总数。</summary>
    public int VertexCount { get; init; }

    /// <summary>邻接关系总数（无向边数的两倍）。</summary>
    public int NeighborCount { get; init; }

    /// <summary>平均每个地块的邻接数；抖动方格下应稳定在 5~7。</summary>
    public double AverageNeighbors { get; init; }

    /// <summary>为满足顶点校验而补裁过的地块数量；正常为 0。</summary>
    public int RepairedCells { get; init; }

    /// <summary>校验失败次数；正常为 0。</summary>
    public int VerificationFailures { get; init; }

    /// <summary>退化地块数量；正常为 0。</summary>
    public int DegenerateCells { get; init; }
}

/// <summary>
/// 多边形地块网格的构建入口。
///
/// 生成方式（对齐 FMG 的 <c>grid-generator</c> 思路，但针对经度环绕做了调整）：
///   1. 点距由"目标地块数"反推，并把点距取整成能整除地图尺寸的值
///      （<c>SpacingX × Columns == Width</c>），这样点阵在经度方向严格周期，
///      缝两侧的点距与其他位置完全一致，不会出现一道宽缝；
///   2. 在规则方格的每个格心放一个点，随机偏移不超过 0.45 倍点距——
///      既保证点不越出自己的方格（拾取与桶索引因此可靠），也天然避开严格共圆的退化情形；
///   3. 交给 <see cref="VoronoiBuilder"/> 做半平面裁剪并校验；
///   4. 面积与质心由多边形本身算出，这是旧栅格模型里不存在的维度。
///
/// 注意：地块边长与源栅格像素是解耦的——栅格负责连续细节，多边形负责属性归属。
/// 所以高地图尺寸下地块反而更"粗"，这是设计意图而不是缺陷。
/// </summary>
public static class PolygonGridBuilder
{
    /// <summary>抖动幅度上限与点距的比值；0.45 是"点不越出方格"的理论上限 0.5 的保守取值。</summary>
    public const double DefaultJitterRatio = 0.45d;

    /// <summary>默认目标地块数。</summary>
    public const int DefaultCellsDesired = 32768;

    /// <summary>地块数下限；再少就无法铺满球面。</summary>
    public const int MinCellsDesired = 64;

    /// <summary>按"希望地块边长约为多少源像素"估算目标地块数，供 UI 档位使用。</summary>
    public static int SuggestCellsDesired(int sourceWidth, int sourceHeight, double spacingPixels)
    {
        var width = Math.Max(sourceWidth, 2);
        var height = Math.Max(sourceHeight, 2);
        var spacing = Math.Max(spacingPixels, 0.5d);
        var desired = (int)Math.Round(width * height / (spacing * spacing));
        return Math.Clamp(desired, MinCellsDesired, width * height);
    }

    /// <summary>
    /// 构建地块网格，并输出建图统计。
    /// 注意 <paramref name="stats"/> 是 out 参数，C# 不允许它跟在可选参数之后，
    /// 所以这个重载不带默认值，便捷重载见下一个。
    /// </summary>
    /// <param name="sourceWidth">源栅格宽度，同时是经度环绕周期。</param>
    /// <param name="sourceHeight">源栅格高度。</param>
    /// <param name="seed">世界种子，决定抖动；同种子结果可复现。</param>
    /// <param name="cellsDesired">目标地块数。</param>
    /// <param name="maxRepairRounds">单个地块最多补裁几轮。</param>
    /// <param name="stats">输出建图统计。</param>
    public static PolygonGrid Create(
        int sourceWidth,
        int sourceHeight,
        int seed,
        int cellsDesired,
        int maxRepairRounds,
        out PolygonBuildStats stats)
    {
        var width = Math.Max(sourceWidth, 2);
        var height = Math.Max(sourceHeight, 2);

        // 上限是像素总数（一格一像素），下限在极小地图上必须让位，
        // 否则 Math.Clamp 会因为 min > max 抛异常。
        var maxDesired = width * height;
        var desired = Math.Clamp(cellsDesired, Math.Min(MinCellsDesired, maxDesired), maxDesired);

        // 由目标地块数反推点距，再取整成能整除地图尺寸的值。
        var spacing = Math.Sqrt(width * (double)height / desired);
        var columns = Math.Max(2, (int)Math.Round(width / spacing));
        var rows = Math.Max(2, (int)Math.Round(height / spacing));
        var spacingX = width / (double)columns;
        var spacingY = height / (double)rows;

        var count = columns * rows;
        var siteX = new double[count];
        var siteY = new double[count];

        var random = new PolygonRandom(unchecked((ulong)(uint)seed));
        var jitterX = spacingX * DefaultJitterRatio;
        var jitterY = spacingY * DefaultJitterRatio;

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var id = (row * columns) + column;
                siteX[id] = ((column + 0.5d) * spacingX) + (random.NextSigned() * jitterX);
                siteY[id] = ((row + 0.5d) * spacingY) + (random.NextSigned() * jitterY);
            }
        }

        var bucketSize = (spacingX + spacingY) * 0.5d;
        var build = VoronoiBuilder.Build(width, height, siteX, siteY, bucketSize, maxRepairRounds);
        var fields = PolygonFields.Create(count);
        var index = new SiteIndex(width, height, bucketSize, siteX, siteY);

        var grid = PolygonGrid.FromBuild(
            width,
            height,
            spacingX,
            spacingY,
            columns,
            rows,
            siteX,
            siteY,
            build,
            fields,
            index);

        var repairedCells = 0;
        for (var i = 0; i < count; i++)
        {
            if (build.RepairRounds[i] > 0)
            {
                repairedCells++;
            }
        }

        var neighborTotal = 0;
        for (var i = 0; i < count; i++)
        {
            neighborTotal += grid.GetNeighborCount(i);
        }

        stats = new PolygonBuildStats
        {
            CellsDesired = desired,
            CellCount = count,
            SpacingX = spacingX,
            SpacingY = spacingY,
            VertexCount = grid.VertexX.Length,
            NeighborCount = neighborTotal,
            AverageNeighbors = count > 0 ? neighborTotal / (double)count : 0d,
            RepairedCells = repairedCells,
            VerificationFailures = build.VerificationFailures,
            DegenerateCells = build.DegenerateCells,
        };

        return grid;
    }

    /// <summary>构建地块网格（带统计信息）。</summary>
    public static PolygonGrid Create(
        int sourceWidth,
        int sourceHeight,
        int seed,
        out PolygonBuildStats stats,
        int cellsDesired = DefaultCellsDesired,
        int maxRepairRounds = 8)
        => Create(sourceWidth, sourceHeight, seed, cellsDesired, maxRepairRounds, out stats);

    /// <summary>构建地块网格（不关心统计信息时的便捷重载）。</summary>
    public static PolygonGrid Create(
        int sourceWidth,
        int sourceHeight,
        int seed,
        int cellsDesired = DefaultCellsDesired,
        int maxRepairRounds = 8)
        => Create(sourceWidth, sourceHeight, seed, cellsDesired, maxRepairRounds, out _);
}
