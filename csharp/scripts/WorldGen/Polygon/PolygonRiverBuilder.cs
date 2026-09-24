namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>旧河道占比参数适配到 Core 密度；保留河流开关与汇流语义。</summary>
public static class PolygonRiverBuilder
{
    public static void Generate(PolygonGrid grid, float seaLevel, float riverFraction, bool enableRivers = true)
        => Core.Simulation.PolygonRiverBuilder.Generate(grid.Geometry, grid.Fields, seaLevel,
            enableRivers, riverFraction / Core.Simulation.PolygonRiverBuilder.DefaultRiverCellFraction);
}
