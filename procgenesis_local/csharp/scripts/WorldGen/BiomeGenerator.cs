using Godot;
using System.Threading.Tasks;

namespace PlanetGeneration.WorldGen;

public sealed class BiomeGenerator
{
    public BiomeType[,] Generate(int width, int height, float seaLevel, float[,] elevation, float[,] moisture, float[,] temperature, float[,] riverLayer, WorldTuning tuning)
    {
        var biome = new BiomeType[width, height];

        Parallel.For(0, height, y =>
        {
            var latitude = height <= 1 ? 0f : Mathf.Abs((2f * y / (height - 1f)) - 1f);
            var polarBand = Mathf.Clamp((latitude - 0.76f) / 0.24f, 0f, 1f);

            for (var x = 0; x < width; x++)
            {
                var e = elevation[x, y];
                var m = moisture[x, y];
                var t = temperature[x, y];

                if (t < 0.03f && e < 0.65f && e >= seaLevel)
                {
                    biome[x, y] = BiomeType.Ice;
                    continue;
                }

                if (e < (tuning.DeepOceanFactor * seaLevel))
                {
                    biome[x, y] = BiomeType.Ocean;
                    continue;
                }

                if (e < seaLevel)
                {
                    biome[x, y] = BiomeType.ShallowOcean;
                    continue;
                }

                if (e < seaLevel + tuning.CoastBand)
                {
                    biome[x, y] = BiomeType.Coastland;
                    continue;
                }

                if (e >= tuning.MountainThreshold)
                {
                    biome[x, y] = t > 0.2f ? BiomeType.RockyMountain : BiomeType.SnowyMountain;
                    continue;
                }

                if (polarBand > 0f)
                {
                    var polarIceCutoff = Mathf.Lerp(0.11f, 0.22f, polarBand);
                    if (t <= polarIceCutoff)
                    {
                        biome[x, y] = m < 0.10f ? BiomeType.Tundra : BiomeType.Ice;
                        continue;
                    }
                }

                if (t > 0.6f)
                {
                    if (m < 0.15f)
                    {
                        biome[x, y] = BiomeType.TropicalDesert;
                    }
                    else if (m < seaLevel)
                    {
                        biome[x, y] = BiomeType.Savanna;
                    }
                    else if (m < 0.5f)
                    {
                        biome[x, y] = BiomeType.Shrubland;
                    }
                    else if (m < 0.75f)
                    {
                        biome[x, y] = BiomeType.TropicalSeasonalForest;
                    }
                    else
                    {
                        biome[x, y] = BiomeType.TropicalRainForest;
                    }

                    continue;
                }

                if (t > 0.25f)
                {
                    if (m < 0.15f)
                    {
                        biome[x, y] = BiomeType.TemperateDesert;
                    }
                    else if (m < 0.2f)
                    {
                        biome[x, y] = BiomeType.Steppe;
                    }
                    else if (m < 0.4f)
                    {
                        biome[x, y] = BiomeType.Grassland;
                    }
                    else if (m < 0.5f)
                    {
                        biome[x, y] = BiomeType.Chaparral;
                    }
                    else if (m < 0.85f)
                    {
                        biome[x, y] = BiomeType.TemperateSeasonalForest;
                    }
                    else
                    {
                        biome[x, y] = BiomeType.TemperateRainForest;
                    }

                    continue;
                }

                if (t > 0.05f)
                {
                    if (m < 0.2f)
                    {
                        biome[x, y] = BiomeType.Tundra;
                    }
                    else if (m < 0.55f)
                    {
                        biome[x, y] = BiomeType.Taiga;
                    }
                    else
                    {
                        biome[x, y] = BiomeType.BorealForest;
                    }

                    continue;
                }

                biome[x, y] = m < 0.1f ? BiomeType.Tundra : BiomeType.Ice;
            }
        });

        return biome;
    }
}
