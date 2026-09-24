using System;
using PlanetGeneration.Core.Domain;

namespace PlanetGeneration.Core.Simulation;

/// <summary>
/// 地块生态模拟器。
/// 基于地块多边形属性与生产力计算生态健康度与文明发展潜力。
/// </summary>
public static class PolygonEcologySimulator
{
    public static readonly float[] BiomeProductivity =
    {
        0.22f, // Ocean
        0.22f, // ShallowOcean
        0.63f, // Coastland
        0.04f, // Ice
        0.24f, // Tundra
        0.56f, // BorealForest
        0.51f, // Taiga
        0.35f, // Steppe
        0.70f, // Grassland
        0.57f, // Chaparral
        0.15f, // TemperateDesert
        0.79f, // TemperateSeasonalForest
        0.86f, // TemperateRainForest
        0.67f, // Savanna
        0.60f, // Shrubland
        0.11f, // TropicalDesert
        0.84f, // TropicalSeasonalForest
        0.93f, // TropicalRainForest
        0.18f, // RockyMountain
        0.10f, // SnowyMountain
        0.95f, // River
    };

    public const float DefaultBiomeProductivity = 0.22f;
    private const float EmergenceThreshold = 0.67f;
    private const byte ShallowOceanBiome = 1;
    private const float RuggednessScale = 0.18f;

    public static EcologyResult Simulate(
        CellGeometry geometry,
        CellFields fields,
        int seed,
        int epoch,
        int speciesDiversity,
        int civilAggression,
        int magicDensity,
        float seaLevel)
    {
        var count = geometry.Count;
        var diversityNorm = Clamp01(speciesDiversity / 100f);
        var aggressionNorm = Clamp01(civilAggression / 100f);
        var magicNorm = Clamp01(magicDensity / 100f);
        var epochFactor = ComputeEpochFactor(epoch, diversityNorm);
        var safeSeaLevel = Math.Clamp(seaLevel, 0.0001f, 0.9999f);

        var conflictDrag = Lerp(0.10f, 0.58f, aggressionNorm);
        var magicDrift = 1f - (Math.Abs(magicNorm - 0.46f) * 1.35f);
        var arcaneModifier = Math.Clamp(0.86f + (0.22f * magicDrift), 0.70f, 1.08f);
        var epochEcologyScale = 0.55f + (0.45f * epochFactor);
        var diversityScale = 0.72f + (0.52f * diversityNorm);
        var epochCivilScale = 0.28f + (0.92f * epochFactor);

        var totalEcology = 0f;
        var totalCivilization = 0f;
        var landCells = 0;
        var emergenceCells = 0;

        for (var cell = 0; cell < count; cell++)
        {
            var biome = fields.Biome[cell];
            if (biome <= ShallowOceanBiome || fields.Height[cell] <= safeSeaLevel)
            {
                fields.EcologyHealth[cell] = 0f;
                fields.CivilizationPotential[cell] = 0f;
                continue;
            }

            var temperature = Clamp01(fields.Temperature[cell]);
            var moisture = Clamp01(fields.Moisture[cell]);
            var river = Clamp01(fields.River[cell]);
            var heightFromSea = Clamp01((fields.Height[cell] - safeSeaLevel) / Math.Max(1f - safeSeaLevel, 0.0001f));

            var temperatureSuitability = 1f - Math.Min(Math.Abs(temperature - 0.58f) * 1.7f, 1f);
            var moistureSuitability = 1f - Math.Min(Math.Abs(moisture - 0.56f) * 1.45f, 1f);
            var waterAccess = Clamp01((moisture * 0.72f) + (MathF.Sqrt(river) * 0.28f));

            var ruggedness = ComputeRuggedness(geometry, fields, cell);
            var terrainStability = Clamp01((1f - (ruggedness * 0.78f)) - (heightFromSea * 0.14f));

            var biomeProductivity = GetBiomeProductivity(biome);
            var baseEcology = (biomeProductivity * 0.44f)
                + (temperatureSuitability * 0.21f)
                + (moistureSuitability * 0.19f)
                + (waterAccess * 0.16f);

            var patchNoise = HashNoise01(seed, cell);
            var localVariation = 0.87f + (0.26f * patchNoise);
            var ecology = Clamp01(baseEcology * epochEcologyScale * diversityScale * localVariation);

            var settlementSuitability = Clamp01(
                (ecology * 0.42f)
                + (waterAccess * 0.23f)
                + (terrainStability * 0.22f)
                + (temperatureSuitability * 0.13f));

            var civilization = Clamp01(
                settlementSuitability
                * epochCivilScale
                * (1f - (conflictDrag * (1f - (ecology * 0.35f))))
                * arcaneModifier);

            fields.EcologyHealth[cell] = ecology;
            fields.CivilizationPotential[cell] = civilization;

            totalEcology += ecology;
            totalCivilization += civilization;
            landCells++;

            if (civilization >= EmergenceThreshold)
            {
                emergenceCells++;
            }
        }

        return new EcologyResult
        {
            AvgEcologyHealth = landCells > 0 ? totalEcology / landCells : 0f,
            AvgCivilizationPotential = landCells > 0 ? totalCivilization / landCells : 0f,
            CivilizationEmergencePercent = landCells > 0 ? 100f * emergenceCells / landCells : 0f,
            LandCellCount = landCells,
        };
    }

    private static float ComputeRuggedness(CellGeometry geometry, CellFields fields, int cell)
    {
        var start = geometry.CellNeighborStart[cell];
        var end = geometry.CellNeighborStart[cell + 1];
        var neighborCount = end - start;
        if (neighborCount == 0) return 0f;

        var current = fields.Height[cell];
        var diffSum = 0f;
        for (var k = start; k < end; k++)
        {
            var neighbor = geometry.CellNeighbors[k];
            diffSum += Math.Abs(current - fields.Height[neighbor]);
        }

        return Clamp01((diffSum / neighborCount) / RuggednessScale);
    }

    public static float GetBiomeProductivity(byte biome)
    {
        return biome < BiomeProductivity.Length ? BiomeProductivity[biome] : DefaultBiomeProductivity;
    }

    private static float ComputeEpochFactor(int epoch, float diversityNorm)
    {
        var clampedEpoch = Math.Clamp(epoch, 0, 1000);
        var t = clampedEpoch / 1000f;
        var sCurve = t * t * (3f - (2f * t));
        var diversityBonus = (diversityNorm - 0.5f) * 0.15f;
        return Clamp01(sCurve + diversityBonus);
    }

    private static float HashNoise01(int seed, int cellId)
    {
        unchecked
        {
            var h = (uint)(seed ^ (cellId * 0x9E3779B9));
            h = (h ^ (h >> 16)) * 0x85EBCA6B;
            h = (h ^ (h >> 13)) * 0xC2B2AE35;
            h ^= h >> 16;
            return (h & 0x00FFFFFF) / (float)0x01000000;
        }
    }

    private static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);
    private static float Lerp(float a, float b, float t) => a + ((b - a) * Math.Clamp(t, 0f, 1f));
}
