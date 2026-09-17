namespace PlanetGeneration.WorldGen;

public sealed class StatsCalculator
{
    public WorldStats Calculate(
        int width,
        int height,
        BiomeType[,] biome,
        float[,] moisture,
        float[,] temperature,
        float[,] river,
        int cityCount)
    {
        var total = width * height;
        var ocean = 0;
        var riverCount = 0;
        var tempSum = 0f;
        var moistSum = 0f;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (biome[x, y] is BiomeType.Ocean or BiomeType.ShallowOcean)
                {
                    ocean++;
                }

                if (river[x, y] > 0.01f)
                {
                    riverCount++;
                }

                tempSum += temperature[x, y];
                moistSum += moisture[x, y];
            }
        }

        return new WorldStats
        {
            Width = width,
            Height = height,
            CityCount = cityCount,
            OceanPercent = 100f * ocean / total,
            RiverPercent = 100f * riverCount / total,
            AvgTemperature = tempSum / total,
            AvgMoisture = moistSum / total
        };
    }
}
