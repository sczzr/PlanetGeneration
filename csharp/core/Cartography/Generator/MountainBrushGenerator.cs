using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 山脉山水画笔刷生成器（已由 MountainRangePainter 2.0 升级驱动）。
/// 保留本类以维持向后兼容性。
/// </summary>
public static class MountainBrushGenerator
{
    public static List<BrushInstruction> GenerateBrushes(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        IReadOnlyList<MegaTerrainRegion> megaRegions,
        Dictionary<int, RegionStyle> regionStyles)
    {
        return MountainRangePainter.Paint(geometry, fields, options, megaRegions, regionStyles);
    }
}
