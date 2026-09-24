using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>旧网格校验入口；使用 Core 的完整几何验证。</summary>
public static class PolygonGridValidator
{
    public static PolygonValidationReport Validate(PolygonGrid grid, int seed = 12345)
        => Core.Geometry.PolygonGridValidator.Validate(grid.Geometry, seed);
}
