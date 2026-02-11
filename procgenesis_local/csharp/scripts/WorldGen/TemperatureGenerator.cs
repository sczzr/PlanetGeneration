using Godot;
using System.Threading.Tasks;

namespace PlanetGeneration.WorldGen;

public sealed class TemperatureGenerator
{
    public float[,] Generate(int width, int height, int seed, float[,] elevation, float heatFactor, bool randomizeHeat)
    {
        var normalizedHeat = Mathf.Clamp(heatFactor, 0.01f, 1f);

        if (randomizeHeat)
        {
            var rng = new RandomNumberGenerator();
            rng.Seed = (ulong)(uint)(seed ^ 0x45d9f3b);
            normalizedHeat = Mathf.Pow(rng.Randf(), 1f / 3f);
        }
        else
        {
            normalizedHeat = Mathf.Pow(normalizedHeat, 1f / 3f);
        }

        var result = new float[width, height];

        Parallel.For(0, height, y =>
        {
            var noise = new FastNoiseLite
            {
                Seed = seed + 7919,
                NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
                Frequency = 1f
            };

            var latitudinalFactor = SampleLatitudinalGradient(y, height, normalizedHeat);

            for (var x = 0; x < width; x++)
            {
                var nx = Mathf.Cos((x * 2f * Mathf.Pi) / width);
                var nz = Mathf.Sin((x * 2f * Mathf.Pi) / width);
                var ny = 4f * y / height;

                var value =
                    noise.GetNoise3D(nx, ny, nz) +
                    0.5f * noise.GetNoise3D(2f * nx, 2f * ny, 2f * nz) +
                    0.25f * noise.GetNoise3D(4f * nx, 4f * ny, 4f * nz) +
                    0.125f * noise.GetNoise3D(8f * nx, 8f * ny, 8f * nz) +
                    0.0625f * noise.GetNoise3D(16f * nx, 16f * ny, 16f * nz);

                value /= 1.28f;
                value = Mathf.Pow(value, 2f);

                var temperature = 1.15f * value * latitudinalFactor;
                var elevationValue = elevation[x, y];

                if (elevationValue > 0.9f)
                {
                    temperature -= 0.4f * elevationValue;
                }
                else if (elevationValue > 0.8f)
                {
                    temperature -= 0.2f * elevationValue;
                }
                else if (elevationValue > 0.7f)
                {
                    temperature -= 0.12f * elevationValue;
                }
                else if (elevationValue > 0.6f)
                {
                    temperature -= 0.08f * elevationValue;
                }
                else if (elevationValue > 0.5f)
                {
                    temperature -= 0.05f * elevationValue;
                }
                else if (elevationValue > 0.4f)
                {
                    temperature -= 0.02f * elevationValue;
                }

                if (float.IsNaN(temperature) || float.IsInfinity(temperature))
                {
                    temperature = 0f;
                }

                result[x, y] = temperature;
            }
        });

        return NormalizeTemperature(result, width, height);
    }

    private float[,] NormalizeTemperature(float[,] source, int width, int height)
    {
        var min = float.MaxValue;
        var max = float.MinValue;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = source[x, y];
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    continue;
                }

                if (value < min)
                {
                    min = value;
                }

                if (value > max)
                {
                    max = value;
                }
            }
        }

        if (min == float.MaxValue || max == float.MinValue)
        {
            return source;
        }

        var range = Mathf.Max(max - min, 0.00001f);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = source[x, y];
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    source[x, y] = 0f;
                    continue;
                }

                source[x, y] = Mathf.Clamp((value - min) / range, 0f, 1f);
            }
        }

        return source;
    }

    private float SampleLatitudinalGradient(int y, int height, float heatFactor)
    {
        var half = height * 0.5f;
        var yf = y;

        if (yf <= half)
        {
            var denominator = Mathf.Max(heatFactor * half, 0.0001f);
            return Mathf.Clamp(yf / denominator, 0f, 1f);
        }

        var whiteEnd = half + (1f - heatFactor) * half;
        if (yf <= whiteEnd)
        {
            return 1f;
        }

        var denominatorBottom = Mathf.Max(height - whiteEnd, 0.0001f);
        var t = (yf - whiteEnd) / denominatorBottom;
        return Mathf.Clamp(1f - t, 0f, 1f);
    }
}
