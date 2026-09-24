using PlanetGeneration.Core.Domain;
using CoreSimulator = PlanetGeneration.Core.Simulation.PolygonEcologySimulator;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>兼容旧网格参数；生态计算与生产力表统一由 Core 提供。</summary>
public static class PolygonEcologySimulator
{
    public static float[] BiomeProductivity => CoreSimulator.BiomeProductivity;
    public const float DefaultBiomeProductivity = CoreSimulator.DefaultBiomeProductivity;
    public static float GetBiomeProductivity(byte biome) => CoreSimulator.GetBiomeProductivity(biome);

    public static EcologyResult Simulate(PolygonGrid grid, int seed, int epoch,
        int speciesDiversity, int civilAggression, int magicDensity, float seaLevel)
        => CoreSimulator.Simulate(grid.Geometry, grid.Fields, seed, epoch,
            speciesDiversity, civilAggression, magicDensity, seaLevel);
}
