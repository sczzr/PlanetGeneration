using Godot;
using System.Threading.Tasks;

namespace PlanetGeneration.WorldGen;

public sealed class MoistureGenerator
{
    public float[,] GenerateBaseMoisture(int width, int height, float seaLevel, float[,] elevation, float[,] temperature)
    {
        var moisture = new float[width, height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                moisture[x, y] = elevation[x, y] < seaLevel ? temperature[x, y] : 0f;
            }
        }

        return moisture;
    }

    public Vector2[,] GenerateBaseWind(int width, int height, int seed, int windCellCount)
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)(uint)(seed ^ 0x9e3779b9);

        var wind = new Vector2[width, height];
        var windCount = new float[width, height];

        var diag = Mathf.Sqrt(width * width + height * height);
        // Very large maps otherwise make each source touch an O(diag^2)
        // perimeter. A 512-cell radius still gives broad planetary wind bands
        // while keeping generation time bounded as resolution increases.
        var maxReach = Mathf.Min(Mathf.Max(2, Mathf.FloorToInt(diag / 4f)), 512);

        for (var i = 0; i < windCellCount; i++)
        {
            var originX = rng.RandiRange(0, width - 1);
            var originY = rng.RandiRange(0, height - 1);
            var intensity = rng.RandfRange(1f, 50f);
            var reach = rng.RandiRange(1, maxReach);
            var clockwise = rng.Randf() > 0.5f;

            void VisitRingCell(int p, int q, int r)
            {
                var x = originX + p;
                var y = originY + q;

                if (x < 0)
                {
                    x += width;
                }
                else if (x >= width)
                {
                    x -= width;
                }

                if (y < 0)
                {
                    y = 0;
                }
                else if (y >= height)
                {
                    y = height - 1;
                }

                var vx = clockwise ? intensity * (-q) / r : intensity * q / r;
                var vy = clockwise ? intensity * p / r : intensity * (-p) / r;

                wind[x, y] += new Vector2(vx, vy);
                windCount[x, y] += 1f;
            }

            // Walk only the ring perimeter: (2r+1)^2 per radius degenerates to
            // O(reach^3) per wind cell because interior points fail the edge
            // test; the four edges are O(r) each, O(reach^2) per cell total.
            for (var r = 1; r <= reach; r++)
            {
                for (var p = -r; p <= r; p++)
                {
                    VisitRingCell(p, -r, r);
                    VisitRingCell(p, r, r);
                }

                for (var q = -r + 1; q < r; q++)
                {
                    VisitRingCell(-r, q, r);
                    VisitRingCell(r, q, r);
                }
            }
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (windCount[x, y] > 0)
                {
                    wind[x, y] /= windCount[x, y];
                }
                else
                {
                    wind[x, y] = new Vector2(rng.RandfRange(-25f, 25f), rng.RandfRange(-25f, 25f));
                }
            }
        }

        return wind;
    }

    public float[,] DistributeMoisture(
        int width,
        int height,
        float seaLevel,
        float[,] elevation,
        float[,] baseMoisture,
        float[,] temperature,
        Vector2[,] wind,
        int iterations,
        int seed = 0,
        float moistureFactor = 1.0f)
    {
        var distributed = Array2D.Create(width, height, 0f);

        // The 5-octave legacy noise dominates this stage; sample it per row in
        // parallel (stateless position-based noise, so per-row copies with the
        // same seed produce identical values).
        var noiseValues = new float[width, height];
        Parallel.For(0, height, y =>
        {
            var noise = new FastNoiseLite
            {
                Seed = seed ^ unchecked((int)0x6c8e9cf5),
                NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
                Frequency = 1f
            };

            for (var x = 0; x < width; x++)
            {
                noiseValues[x, y] = SampleLegacyNoise(noise, x, y, width, height);
            }
        });

        var factor = Mathf.Clamp(moistureFactor, 0.1f, 3.0f);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var noiseValue = noiseValues[x, y];

                var isLand = elevation[x, y] >= seaLevel;
                if (isLand)
                {
                    distributed[x, y] += 0.15f * noiseValue * factor;
                    continue;
                }

                var windX = wind[x, y].X;
                var windY = wind[x, y].Y;
                var windSpeed = Mathf.Sqrt(windX * windX + windY * windY);
                if (windSpeed <= 0.0001f)
                {
                    continue;
                }

                var moistureRemaining = baseMoisture[x, y] * 50f * factor;
                var lastElevation = elevation[x, y];

                var unitX = windX / windSpeed;
                var unitY = windY / windSpeed;

                var xvec = x + Mathf.RoundToInt(unitX);
                var yvec = y + Mathf.RoundToInt(unitY);
                var stepCount = 0;

                while (moistureRemaining > 0.1f && stepCount < 1000)
                {
                    WrapX(ref xvec, width);
                    if (yvec < 0 || yvec >= height)
                    {
                        break;
                    }

                    var currentElevation = elevation[xvec, yvec];
                    var currentTemperature = temperature[xvec, yvec];

                    float slope;
                    var slopeBasis = Mathf.Sqrt(currentElevation) - (0.5f * currentTemperature) - (0.005f * windSpeed) + 0.7f;

                    if (lastElevation >= seaLevel)
                    {
                        slope = (currentElevation - lastElevation) * slopeBasis;
                    }
                    else
                    {
                        slope = 0.01f * (currentElevation - lastElevation) * slopeBasis;
                    }

                    if (slope <= 0.002f)
                    {
                        slope = 0.002f;
                    }

                    var transfer = moistureRemaining * slope;
                    if (float.IsNaN(transfer) || float.IsInfinity(transfer))
                    {
                        break;
                    }

                    distributed[xvec, yvec] += transfer;
                    windSpeed = wind[xvec, yvec].Length();

                    if (currentElevation < seaLevel)
                    {
                        distributed[xvec, yvec] = 0.0001f;
                    }

                    if (currentElevation > 0.6f)
                    {
                        moistureRemaining -= 4f * distributed[xvec, yvec];
                    }
                    else
                    {
                        moistureRemaining -= distributed[xvec, yvec];
                    }

                    lastElevation = currentElevation;

                    xvec += Mathf.RoundToInt(unitX);
                    yvec += Mathf.RoundToInt(unitY);

                    WrapX(ref xvec, width);
                    if (yvec < 0 || yvec >= height)
                    {
                        break;
                    }

                    var resultantX = wind[xvec, yvec].X + windX;
                    var resultantY = wind[xvec, yvec].Y + windY;
                    var resultantMagnitude = Mathf.Sqrt(resultantX * resultantX + resultantY * resultantY);
                    if (resultantMagnitude <= 0.0001f)
                    {
                        break;
                    }

                    unitX = resultantX / resultantMagnitude;
                    unitY = resultantY / resultantMagnitude;

                    stepCount++;
                }
            }
        }

        _ = iterations;
        distributed = AverageLandValues(distributed, elevation, width, height, seaLevel, 10);

        FinalizeMoisture(distributed, elevation, width, height, seaLevel);
        return distributed;
    }


    private void FinalizeMoisture(float[,] values, float[,] elevation, int width, int height, float seaLevel)
    {
        Parallel.For(0, height, y =>
        {
            for (var x = 0; x < width; x++)
            {
                if (elevation[x, y] < seaLevel)
                {
                    values[x, y] = 0f;
                    continue;
                }

                var value = values[x, y];
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                {
                    values[x, y] = 0f;
                }
                else
                {
                    values[x, y] = Mathf.Clamp(value, 0f, 1.2f);
                }
            }
        });
    }

    private float[,] AverageLandValues(float[,] input, float[,] elevation, int width, int height, float seaLevel, int radius)
    {
        // The original implementation visited (2r+1)^2 cells for every output
        // cell. Two box-filter passes produce the same rectangular window in
        // O(width*height*r) time and avoid tens of millions of repeated bounds
        // calculations on normal maps.
        var horizontalSum = new float[width, height];
        var horizontalCount = new int[width, height];
        Parallel.For(0, height, y =>
        {
            for (var x = 0; x < width; x++)
            {
                var sum = 0f;
                var count = 0;
                for (var ox = -radius; ox <= radius; ox++)
                {
                    var nx = x + ox;
                    if (nx < 0) nx += width;
                    else if (nx >= width) nx -= width;
                    if (elevation[nx, y] > seaLevel)
                    {
                        sum += input[nx, y];
                        count++;
                    }
                }
                horizontalSum[x, y] = sum;
                horizontalCount[x, y] = count;
            }
        });

        var averaged = new float[width, height];
        Parallel.For(0, height, y =>
        {
            for (var x = 0; x < width; x++)
            {
                var sum = 0f;
                var count = 1; // Preserve the original denominator baseline.
                for (var oy = -radius; oy <= radius; oy++)
                {
                    var ny = y + oy;
                    if (ny < 0) ny = 0;
                    else if (ny >= height) ny = height - 1;
                    sum += horizontalSum[x, ny];
                    count += horizontalCount[x, ny];
                }
                averaged[x, y] = sum / count;
            }
        });
        return averaged;
    }

    private float SampleLegacyNoise(FastNoiseLite noise, int x, int y, int width, int height)
    {
        var ny = 4f * y / height;
        var nx = Mathf.Cos((x * 2f * Mathf.Pi) / width);
        var nz = Mathf.Sin((x * 2f * Mathf.Pi) / width);

        var value =
            noise.GetNoise3D(nx, ny, nz) +
            0.5f * noise.GetNoise3D(2f * nx, 2f * ny, 2f * nz) +
            0.25f * noise.GetNoise3D(4f * nx, 4f * ny, 4f * nz) +
            0.125f * noise.GetNoise3D(8f * nx, 8f * ny, 8f * nz) +
            0.0625f * noise.GetNoise3D(16f * nx, 16f * ny, 16f * nz);

        value /= 1.28f;
        value = Mathf.Pow(value, 2f);
        return Mathf.Max(value, 0f);
    }

    private void WrapX(ref int x, int width)
    {
        if (x >= width)
        {
            x %= width;
        }
        else if (x < 0)
        {
            x = width + x;
        }
    }
}
