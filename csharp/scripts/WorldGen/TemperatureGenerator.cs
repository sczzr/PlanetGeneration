using Godot;
using System.Threading.Tasks;

namespace PlanetGeneration.WorldGen;

public sealed class TemperatureGenerator
{
    public float[,] Generate(int width, int height, int seed, float[,] elevation, float heatFactor)
    {
        var normalizedHeat = Mathf.Clamp(heatFactor, 0.01f, 1f);
        normalizedHeat = Mathf.Pow(normalizedHeat, 1f / 3f);

        var result = new float[width, height];

        Parallel.For(0, height, y =>
        {
            var noise = new FastNoiseLite
            {
                Seed = seed + 7919,
                NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
                Frequency = 1f
            };

            var latitude = height <= 1 ? 0f : Mathf.Abs((2f * y / (height - 1f)) - 1f);
            var latitudinalFactor = SampleLatitudinalGradient(y, height, normalizedHeat);
            var polarCooling = SamplePolarCooling(y, height, normalizedHeat);
            var deepPolarCooling = SampleDeepPolarCooling(latitude, normalizedHeat);

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

                temperature -= polarCooling;
                temperature -= deepPolarCooling;

                if (latitude > 0.84f)
                {
                    var compression = Mathf.Clamp((latitude - 0.84f) / 0.16f, 0f, 1f);
                    temperature *= Mathf.Lerp(1f, 0.62f, compression);
                }

                if (float.IsNaN(temperature) || float.IsInfinity(temperature))
                {
                    temperature = 0f;
                }

                result[x, y] = temperature;
            }
        });

        var normalized = NormalizeTemperature(result, width, height);
        return ApplyEarthLikePolarProfile(normalized, width, height, normalizedHeat);
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

    private float[,] ApplyEarthLikePolarProfile(float[,] source, int width, int height, float heatFactor)
    {
        if (height <= 1)
        {
            return source;
        }

        var polarCapLimit = Mathf.Lerp(0.30f, 0.18f, 1f - heatFactor);
        var polarCoolingStrength = Mathf.Lerp(0.16f, 0.28f, 1f - heatFactor);
        var deepPolarCoolingStrength = Mathf.Lerp(0.12f, 0.24f, 1f - heatFactor);

        for (var y = 0; y < height; y++)
        {
            var latitude = Mathf.Abs((2f * y / (height - 1f)) - 1f);
            var polarBand = Mathf.Clamp((latitude - 0.62f) / 0.38f, 0f, 1f);
            polarBand = polarBand * polarBand * (3f - 2f * polarBand);

            var deepPolarBand = Mathf.Clamp((latitude - 0.84f) / 0.16f, 0f, 1f);
            deepPolarBand = deepPolarBand * deepPolarBand * (3f - 2f * deepPolarBand);

            var cooling = polarBand * polarCoolingStrength + deepPolarBand * deepPolarCoolingStrength;
            var cap = Mathf.Lerp(1f, polarCapLimit, deepPolarBand);

            for (var x = 0; x < width; x++)
            {
                var value = Mathf.Clamp(source[x, y] - cooling, 0f, 1f);
                if (deepPolarBand > 0f)
                {
                    value = Mathf.Min(value, cap);
                }

                source[x, y] = value;
            }
        }

        return source;
    }

    private float SampleLatitudinalGradient(int y, int height, float heatFactor)
    {
        if (height <= 1)
        {
            return 1f;
        }

        var latitude = Mathf.Abs((2f * y / (height - 1f)) - 1f);
        var curvature = Mathf.Lerp(1.62f, 1.24f, heatFactor);
        var equatorWeight = 1f - Mathf.Pow(latitude, curvature);
        var polarBaseline = Mathf.Lerp(0.03f, 0.08f, heatFactor);
        return Mathf.Clamp(Mathf.Lerp(polarBaseline, 1f, equatorWeight), 0f, 1f);
    }

    private float SamplePolarCooling(int y, int height, float heatFactor)
    {
        if (height <= 1)
        {
            return 0f;
        }

        var latitude = Mathf.Abs((2f * y / (height - 1f)) - 1f);
        var t = Mathf.Clamp((latitude - 0.62f) / 0.38f, 0f, 1f);
        var smooth = t * t * (3f - 2f * t);
        var strength = Mathf.Lerp(0.46f, 0.30f, heatFactor);
        return smooth * strength;
    }

    private float SampleDeepPolarCooling(float latitude, float heatFactor)
    {
        var t = Mathf.Clamp((latitude - 0.84f) / 0.16f, 0f, 1f);
        var smooth = t * t * (3f - 2f * t);
        var strength = Mathf.Lerp(0.22f, 0.12f, heatFactor);
        return smooth * strength;
    }
}
