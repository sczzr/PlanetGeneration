using PlanetGeneration.Core.Domain;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>兼容旧网格参数；文明状态与事件直接使用 Core 的结果类型。</summary>
public static class PolygonCivilizationSimulator
{
    public static CivilizationResult Simulate(PolygonGrid grid, int seed, int epoch,
        int civilAggression, int speciesDiversity, float seaLevel)
        => Core.Simulation.PolygonCivilizationSimulator.Simulate(
            grid.Geometry, grid.Fields, seed, epoch, civilAggression, speciesDiversity, seaLevel);
}
