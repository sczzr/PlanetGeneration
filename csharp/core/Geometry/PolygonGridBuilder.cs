using System;
using PlanetGeneration.Core.Domain;

namespace PlanetGeneration.Core.Geometry;

/// <summary>
/// 确定性伪随机数发生器（SplitMix64）。
/// </summary>
public struct PolygonRandom
{
    private ulong _state;

    public PolygonRandom(ulong seed)
    {
        _state = seed == 0UL ? 0x9E3779B97F4A7C15UL : seed;
    }

    public ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);
    public double NextSigned() => (NextDouble() * 2d) - 1d;
}

/// <summary>建图统计信息。</summary>
public sealed class PolygonBuildStats
{
    public int CellsDesired { get; init; }
    public int CellCount { get; init; }
    public double SpacingX { get; init; }
    public double SpacingY { get; init; }
    public int VertexCount { get; init; }
    public int NeighborCount { get; init; }
    public double AverageNeighbors { get; init; }
    public int RepairedCells { get; init; }
    public int VerificationFailures { get; init; }
    public int DegenerateCells { get; init; }
}

/// <summary>
/// 多边形几何与拓扑构建器。
/// </summary>
public static class PolygonGridBuilder
{
    public const double DefaultJitterRatio = 0.45d;
    public const int DefaultCellsDesired = GenerationOptions.DefaultTargetCellCount;
    public const int MinCellsDesired = 64;

    /// <summary>
    /// 按点距反推目标地块数。
    /// </summary>
    public static int SuggestCellsDesired(double width, double height, double spacing)
    {
        var safeSpacing = Math.Max(spacing, 0.5d);
        var desired = (int)Math.Round(width * height / (safeSpacing * safeSpacing));
        return Math.Clamp(desired, Math.Min(MinCellsDesired, (int)(width * height)), (int)(width * height));
    }

    /// <summary>
    /// 从逻辑范围、种子和目标地块数构建 CellGeometry。
    /// </summary>
    public static CellGeometry Create(
        WorldExtent extent,
        int seed,
        int cellsDesired,
        int maxRepairRounds,
        out PolygonBuildStats stats)
        => Build(extent, seed, Math.Clamp(cellsDesired, MinCellsDesired, 131072), maxRepairRounds, out stats);

    /// <summary>
    /// 栅格兼容入口：保留旧地图的最小尺寸与“最多每像素一个目标地块”约束，
    /// 但与逻辑世界共用同一套站点、Voronoi、邻接和拾取实现。
    /// </summary>
    public static CellGeometry CreateForRaster(
        int sourceWidth, int sourceHeight, int seed, int cellsDesired,
        int maxRepairRounds, out PolygonBuildStats stats)
    {
        var width = Math.Max(sourceWidth, 2);
        var height = Math.Max(sourceHeight, 2);
        var maxDesired = checked(width * height);
        var desired = Math.Clamp(cellsDesired, Math.Min(MinCellsDesired, maxDesired), maxDesired);
        return Build(new WorldExtent(width, height), seed, desired, maxRepairRounds, out stats);
    }

    private static CellGeometry Build(
        WorldExtent extent, int seed, int desired, int maxRepairRounds, out PolygonBuildStats stats)
    {
        var width = extent.Width;
        var height = extent.Height;

        // 由目标地块数反推点距，并整除经度周期。
        var spacing = Math.Sqrt(width * height / desired);
        var columns = Math.Max(2, (int)Math.Round(width / spacing));
        var rows = Math.Max(2, (int)Math.Round(height / spacing));
        var spacingX = width / columns;
        var spacingY = height / rows;

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
        var index = new SiteIndex(width, height, bucketSize, siteX, siteY);

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

        var geometry = new CellGeometry(
            extent,
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
            index);

        var repairedCells = 0;
        for (var i = 0; i < count; i++)
        {
            if (build.RepairRounds[i] > 0)
            {
                repairedCells++;
            }
        }

        stats = new PolygonBuildStats
        {
            CellsDesired = desired,
            CellCount = count,
            SpacingX = spacingX,
            SpacingY = spacingY,
            VertexCount = vertexTotal,
            NeighborCount = neighborTotal,
            AverageNeighbors = count > 0 ? neighborTotal / (double)count : 0d,
            RepairedCells = repairedCells,
            VerificationFailures = build.VerificationFailures,
            DegenerateCells = build.DegenerateCells,
        };

        return geometry;
    }

    public static CellGeometry Create(
        WorldExtent extent,
        int seed,
        int cellsDesired = DefaultCellsDesired,
        int maxRepairRounds = 8)
        => Create(extent, seed, cellsDesired, maxRepairRounds, out _);
}
