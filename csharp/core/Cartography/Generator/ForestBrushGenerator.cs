using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 森林艺术化笔刷生成器（已由 ForestTransitionPainter 升级驱动）。
/// 保留本类以维持向后兼容性。
/// </summary>
public static class ForestBrushGenerator
{
    public static List<BrushInstruction> GenerateBrushes(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        IReadOnlyList<MegaTerrainRegion> megaRegions,
        Dictionary<int, RegionStyle> regionStyles)
    {
        return ForestTransitionPainter.Paint(geometry, fields, options, megaRegions, regionStyles);
    }
}
