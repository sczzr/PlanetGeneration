using Godot;

namespace PlanetGeneration.WorldGen;

public sealed class EcologySimulationResult
{
    public required float[,] EcologyHealth { get; init; }
    public required float[,] CivilizationPotential { get; init; }
    public required float AvgEcologyHealth { get; init; }
    public required float AvgCivilizationPotential { get; init; }
    public required float CivilizationEmergencePercent { get; init; }
}

public sealed class EcologySimulator
{
    public EcologySimulationResult Simulate(
        int width,
        int height,
        int seed,
        int epoch,
        int speciesDiversity,
        int civilAggression,
        int magicDensity,
        float seaLevel,
        float[,] elevation,
        float[,] temperature,
        float[,] moisture,
        float[,] river,
        BiomeType[,] biome)
    {
        var ecologyHealth = new float[width, height];
        var civilizationPotential = new float[width, height];

        var diversityNorm = Mathf.Clamp(speciesDiversity / 100f, 0f, 1f);
        var aggressionNorm = Mathf.Clamp(civilAggression / 100f, 0f, 1f);
        var magicNorm = Mathf.Clamp(magicDensity / 100f, 0f, 1f);
        var epochFactor = ComputeEpochFactor(epoch, diversityNorm);
        var safeSeaLevel = Mathf.Clamp(seaLevel, 0.0001f, 0.9999f);

        var totalEco = 0f;
        var totalCivil = 0f;
        var landCells = 0;
        var emergenceCells = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var currentBiome = biome[x, y];
                if (currentBiome == BiomeType.Ocean || currentBiome == BiomeType.ShallowOcean || elevation[x, y] <= safeSeaLevel)
                {
                    ecologyHealth[x, y] = 0f;
                    civilizationPotential[x, y] = 0f;
                    continue;
                }

                var temp = Mathf.Clamp(temperature[x, y], 0f, 1f);
                var moist = Mathf.Clamp(moisture[x, y], 0f, 1f);
                var riverValue = Mathf.Clamp(river[x, y], 0f, 1f);
                var heightFromSea = Mathf.Clamp((elevation[x, y] - safeSeaLevel) / Mathf.Max(1f - safeSeaLevel, 0.0001f), 0f, 1f);

                var temperatureSuitability = 1f - Mathf.Min(Mathf.Abs(temp - 0.58f) * 1.7f, 1f);
                var moistureSuitability = 1f - Mathf.Min(Mathf.Abs(moist - 0.56f) * 1.45f, 1f);
                var waterAccess = Mathf.Clamp(moist * 0.72f + Mathf.Sqrt(riverValue) * 0.28f, 0f, 1f);
                var terrainStability = 1f - Mathf.Pow(heightFromSea, 1.25f);
                var biomeProductivity = GetBiomeProductivity(currentBiome);

                var baseEcology = biomeProductivity * 0.44f
                    + temperatureSuitability * 0.21f
                    + moistureSuitability * 0.19f
                    + waterAccess * 0.16f;

                var patchNoise = HashNoise01(seed, x, y);
                var localVariation = 0.87f + 0.26f * patchNoise;

                var ecology = Mathf.Clamp(
                    baseEcology
                    * (0.55f + 0.45f * epochFactor)
                    * (0.72f + 0.52f * diversityNorm)
                    * localVariation,
                    0f,
                    1f);

                var settlementSuitability = Mathf.Clamp(
                    ecology * 0.42f
                    + waterAccess * 0.23f
                    + terrainStability * 0.22f
                    + temperatureSuitability * 0.13f,
                    0f,
                    1f);

                var conflictDrag = Mathf.Lerp(0.10f, 0.58f, aggressionNorm);
                var magicDrift = 1f - Mathf.Abs(magicNorm - 0.46f) * 1.35f;
                var arcaneModifier = Mathf.Clamp(0.86f + 0.22f * magicDrift, 0.70f, 1.08f);

                var civilization = Mathf.Clamp(
                    settlementSuitability
                    * (0.28f + 0.92f * epochFactor)
                    * (1f - conflictDrag * (1f - ecology * 0.35f))
                    * arcaneModifier,
                    0f,
                    1f);

                ecologyHealth[x, y] = ecology;
                civilizationPotential[x, y] = civilization;

                totalEco += ecology;
                totalCivil += civilization;
                landCells++;
                if (civilization >= 0.67f)
                {
                    emergenceCells++;
                }
            }
        }

        var avgEco = landCells > 0 ? totalEco / landCells : 0f;
        var avgCivil = landCells > 0 ? totalCivil / landCells : 0f;
        var emergence = landCells > 0 ? 100f * emergenceCells / landCells : 0f;

        return new EcologySimulationResult
        {
            EcologyHealth = ecologyHealth,
            CivilizationPotential = civilizationPotential,
            AvgEcologyHealth = avgEco,
            AvgCivilizationPotential = avgCivil,
            CivilizationEmergencePercent = emergence
        };
    }

    private static float ComputeEpochFactor(int epoch, float diversityNorm)
    {
        var safeEpoch = Mathf.Max(epoch, 0);
        var speed = 0.010f + diversityNorm * 0.016f;
        return 1f - Mathf.Exp(-safeEpoch * speed);
    }

    private static float HashNoise01(int seed, int x, int y)
    {
        uint hash = (uint)seed;
        hash ^= (uint)x * 374761393u;
        hash ^= (uint)y * 668265263u;
        hash = (hash ^ (hash >> 13)) * 1274126177u;
        hash ^= hash >> 16;
        return (hash & 0x00FFFFFFu) / 16777215f;
    }

    private static float GetBiomeProductivity(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.River => 0.95f,
            BiomeType.TropicalRainForest => 0.93f,
            BiomeType.TropicalSeasonalForest => 0.84f,
            BiomeType.TemperateRainForest => 0.86f,
            BiomeType.TemperateSeasonalForest => 0.79f,
            BiomeType.Savanna => 0.67f,
            BiomeType.Grassland => 0.70f,
            BiomeType.Shrubland => 0.60f,
            BiomeType.Chaparral => 0.57f,
            BiomeType.BorealForest => 0.56f,
            BiomeType.Taiga => 0.51f,
            BiomeType.Coastland => 0.63f,
            BiomeType.Steppe => 0.35f,
            BiomeType.Tundra => 0.24f,
            BiomeType.TropicalDesert => 0.11f,
            BiomeType.TemperateDesert => 0.15f,
            BiomeType.RockyMountain => 0.18f,
            BiomeType.SnowyMountain => 0.10f,
            BiomeType.Ice => 0.04f,
            _ => 0.22f
        };
    }
}
