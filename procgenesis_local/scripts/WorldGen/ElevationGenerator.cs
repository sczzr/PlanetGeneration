using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlanetGeneration.WorldGen;

public sealed class ElevationGenerator
{
    public float[,] Generate(int width, int height, int seed, float seaLevel, PlateResult plateResult)
    {
        var baseNoise = CreateBaseNoise(width, height, seed);
        return ApplyPlateStress(baseNoise, width, height, seaLevel, seed, plateResult);
    }

    private float[,] CreateBaseNoise(int width, int height, int seed)
    {
        var elevation = new float[width, height];

        Parallel.For(0, height, y =>
        {
            var noise = new FastNoiseLite
            {
                Seed = seed ^ unchecked((int)0x5f3759df),
                NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
                Frequency = 1f
            };

            var ny = 4f * y / Mathf.Max(height, 1);

            for (var x = 0; x < width; x++)
            {
                var nx = Mathf.Cos((x * 2f * Mathf.Pi) / Mathf.Max(width, 1));
                var nz = Mathf.Sin((x * 2f * Mathf.Pi) / Mathf.Max(width, 1));

                var value =
                    noise.GetNoise3D(nx, ny, nz) +
                    0.5f * noise.GetNoise3D(2f * nx, 2f * ny, 2f * nz) +
                    0.25f * noise.GetNoise3D(4f * nx, 4f * ny, 4f * nz) +
                    0.125f * noise.GetNoise3D(8f * nx, 8f * ny, 8f * nz) +
                    0.0625f * noise.GetNoise3D(16f * nx, 16f * ny, 16f * nz);

                value /= 1.28f;
                value = Mathf.Pow(value, 2f);
                elevation[x, y] = value;
            }
        });

        return elevation;
    }

    private float[,] ApplyPlateStress(float[,] baseElevation, int width, int height, float seaLevel, int seed, PlateResult plateResult)
    {
        var modified = new float[width, height];

        var plateCount = plateResult.Sites.Count;
        var neighborsByPlate = new List<PlateNeighborInfo>[plateCount];
        var borderByPlate = new List<PlateEdgePoint>[plateCount];

        foreach (var relation in plateResult.Neighbors)
        {
            if (relation.Id < 0 || relation.Id >= plateCount)
            {
                continue;
            }

            neighborsByPlate[relation.Id] ??= new List<PlateNeighborInfo>();
            neighborsByPlate[relation.Id].Add(relation);
        }

        foreach (var edge in plateResult.BorderPoints)
        {
            if (edge.Id < 0 || edge.Id >= plateCount)
            {
                continue;
            }

            borderByPlate[edge.Id] ??= new List<PlateEdgePoint>();
            borderByPlate[edge.Id].Add(edge);
        }

        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)(uint)(seed ^ unchecked((int)0x7f4a7c15));
        var gradientInitValue = rng.Randf();
        var gradientCoefficient = rng.Randf() * 0.1f + 0.1f;

        Parallel.For(0, height, y =>
        {
            var detailNoise = new FastNoiseLite
            {
                Seed = seed ^ unchecked((int)0x1b873593),
                NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
                Frequency = 1f
            };

            var ny = 4f * y / Mathf.Max(height, 1);
            var gradientFactor = ComputeGradientFactor(y, height, gradientInitValue, gradientCoefficient);

            for (var x = 0; x < width; x++)
            {
                var plateId = plateResult.PlateIds[x, y];
                var baseEl = plateResult.PlateBaseElevation[x, y];

                var bestDistance = float.PositiveInfinity;
                var totalPressure = 0f;

                if (plateId >= 0 && plateId < plateCount)
                {
                    var plateNeighbors = neighborsByPlate[plateId];

                    if (plateNeighbors != null)
                    {
                        for (var i = 0; i < plateNeighbors.Count; i++)
                        {
                            var relation = plateNeighbors[i];
                            if (relation.NeighborId < 0 || relation.NeighborId >= plateCount)
                            {
                                continue;
                            }

                            var neighborEdges = borderByPlate[relation.NeighborId];
                            if (neighborEdges == null || neighborEdges.Count == 0)
                            {
                                continue;
                            }

                            var closestDistance = float.PositiveInfinity;
                            var closestType = PlateBoundaryType.None;

                            for (var n = 0; n < neighborEdges.Count; n++)
                            {
                                var edge = neighborEdges[n];

                                var dx = Mathf.Abs(edge.X - x);
                                if (dx > width / 2f)
                                {
                                    dx = width - dx;
                                }

                                var dy = edge.Y - y;
                                var distance = Mathf.Sqrt(dx * dx + dy * dy);

                                if (distance >= closestDistance)
                                {
                                    continue;
                                }

                                closestDistance = distance;
                                closestType = edge.Type;
                            }

                            if (float.IsNaN(closestDistance) || float.IsInfinity(closestDistance))
                            {
                                continue;
                            }

                            var isTransform = closestType == PlateBoundaryType.Transform;
                            var distanceFactor = isTransform
                                ? 0.2f / (0.002f * closestDistance * closestDistance + 1f)
                                : 0.4f / (0.02f * closestDistance * closestDistance + 1f);

                            totalPressure += relation.DirectForce * distanceFactor;

                            if (closestDistance < bestDistance)
                            {
                                bestDistance = closestDistance;
                            }
                        }
                    }
                }

                if (float.IsNaN(bestDistance) || float.IsInfinity(bestDistance))
                {
                    bestDistance = Mathf.Max(width, height);
                }

                var modifiedBaseElevation = baseEl + (1f / (0.01f * (bestDistance * bestDistance) + 1f)) * (1f - baseEl);

                var nx = Mathf.Cos((x * 2f * Mathf.Pi) / Mathf.Max(width, 1));
                var nz = Mathf.Sin((x * 2f * Mathf.Pi) / Mathf.Max(width, 1));

                var value =
                    0.125f * detailNoise.GetNoise3D(8f * nx, 8f * ny, 8f * nz) +
                    0.0625f * detailNoise.GetNoise3D(16f * nx, 16f * ny, 16f * nz) +
                    0.03125f * detailNoise.GetNoise3D(32f * nx, 32f * ny, 32f * nz);

                value *= 7f;
                value *= 1f / (1f + Mathf.Pow(100f, -5f * (value - 0.8f)));

                var input = baseElevation[x, y];

                if (((input + (totalPressure * 0.7f * value)) * baseEl) >= seaLevel)
                {
                    var elevated = (input + totalPressure * value) * modifiedBaseElevation * gradientFactor;
                    elevated = elevated + (0.15f * (1f - elevated)) - 0.12f;
                    modified[x, y] = elevated;
                }
                else
                {
                    modified[x, y] = (input + (totalPressure * 0.7f * value)) * (baseEl * modifiedBaseElevation);
                }

            }
        });

        return modified;
    }

    private float ComputeGradientFactor(int y, int height, float gradientInitValue, float gradientCoefficient)
    {
        var coefficientHeight = Mathf.Max(gradientCoefficient * Mathf.Max(height, 1), 0.0001f);
        var gradientFactor = (y / coefficientHeight) + gradientInitValue;

        var threshold = height * (gradientCoefficient * gradientInitValue + (1f - gradientCoefficient));
        if (y >= threshold)
        {
            gradientFactor =
                -1f * (1f / coefficientHeight) * (y - ((1f - gradientCoefficient) * height)) +
                1f +
                gradientInitValue;
        }

        if (gradientFactor > 1f)
        {
            gradientFactor = 1f;
        }

        return gradientFactor;
    }
}
