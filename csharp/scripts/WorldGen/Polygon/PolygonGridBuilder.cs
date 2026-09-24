using System;
using PlanetGeneration.Core.Geometry;
using CoreBuilder = PlanetGeneration.Core.Geometry.PolygonGridBuilder;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>旧参数形式的兼容入口；几何构建只在 Core 实现。</summary>
public static class PolygonGridBuilder
{
    public const double DefaultJitterRatio = CoreBuilder.DefaultJitterRatio;
    // 保留旧 API 的默认档位；Core 的逻辑世界默认档位独立为 10000。
    public const int DefaultCellsDesired = 32768;
    public const int MinCellsDesired = CoreBuilder.MinCellsDesired;

    public static int SuggestCellsDesired(int sourceWidth, int sourceHeight, double spacingPixels)
        => CoreBuilder.SuggestCellsDesired(Math.Max(sourceWidth, 2), Math.Max(sourceHeight, 2), spacingPixels);

    public static PolygonGrid Create(int sourceWidth, int sourceHeight, int seed,
        int cellsDesired, int maxRepairRounds, out PolygonBuildStats stats)
        => new(CoreBuilder.CreateForRaster(sourceWidth, sourceHeight, seed, cellsDesired, maxRepairRounds, out stats));

    public static PolygonGrid Create(int sourceWidth, int sourceHeight, int seed,
        out PolygonBuildStats stats, int cellsDesired = DefaultCellsDesired, int maxRepairRounds = 8)
        => Create(sourceWidth, sourceHeight, seed, cellsDesired, maxRepairRounds, out stats);

    public static PolygonGrid Create(int sourceWidth, int sourceHeight, int seed,
        int cellsDesired = DefaultCellsDesired, int maxRepairRounds = 8)
        => Create(sourceWidth, sourceHeight, seed, cellsDesired, maxRepairRounds, out _);
}
