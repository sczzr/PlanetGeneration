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

        // Pair-level relation lookup: relation (cell's plate -> plate owning the border points).
        // The old implementation rescanned every border point of each neighbor plate for every
        // cell, which is O(cells * relations * borderPoints) and dominated generation time.
        var relationMask = new bool[plateCount, plateCount];
        var pairForce = new float[plateCount, plateCount];
        var pairIsTransform = new bool[plateCount, plateCount];

        foreach (var relation in plateResult.Neighbors)
        {
            if (relation.Id < 0 || relation.Id >= plateCount || relation.NeighborId < 0 || relation.NeighborId >= plateCount)
            {
                continue;
            }

            relationMask[relation.Id, relation.NeighborId] = true;
            pairForce[relation.Id, relation.NeighborId] = relation.DirectForce;
            pairIsTransform[relation.Id, relation.NeighborId] = relation.Type == PlateBoundaryType.Transform;
        }

        var borderPointsByPlate = new List<PlateEdgePoint>[plateCount];
        foreach (var edge in plateResult.BorderPoints)
        {
            if (edge.Id < 0 || edge.Id >= plateCount)
            {
                continue;
            }

            (borderPointsByPlate[edge.Id] ??= new List<PlateEdgePoint>()).Add(edge);
        }

        var totalPressure = new float[width, height];
        var bestDistance = new float[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bestDistance[x, y] = float.PositiveInfinity;
            }
        }

        var distanceField = new float[width, height];
        var typeField = new PlateBoundaryType[width, height];

        // One distance field per plate: distance from every cell to the nearest border point
        // owned by that plate. Accumulating per plate gives the same pressure each cell used
        // to get from scanning its neighbors' border point lists, at O(plates * cells) cost.
        for (var plate = 0; plate < plateCount; plate++)
        {
            var borderPoints = borderPointsByPlate[plate];
            if (borderPoints == null || borderPoints.Count == 0)
            {
                continue;
            }

            BuildBorderDistanceField(borderPoints, width, height, distanceField, typeField);

            Parallel.For(0, height, y =>
            {
                for (var x = 0; x < width; x++)
                {
                    var plateId = plateResult.PlateIds[x, y];
                    if (plateId < 0 || plateId >= plateCount || !relationMask[plateId, plate])
                    {
                        continue;
                    }

                    var distance = distanceField[x, y];
                    var distanceFactor = pairIsTransform[plateId, plate]
                        ? 0.2f / (0.002f * distance * distance + 1f)
                        : 0.4f / (0.02f * distance * distance + 1f);

                    totalPressure[x, y] += pairForce[plateId, plate] * distanceFactor;

                    if (distance < bestDistance[x, y])
                    {
                        bestDistance[x, y] = distance;
                    }
                }
            });
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
            var maxExtent = Mathf.Max(width, height);

            for (var x = 0; x < width; x++)
            {
                var baseEl = plateResult.PlateBaseElevation[x, y];

                var cellBestDistance = bestDistance[x, y];
                if (float.IsInfinity(cellBestDistance) || float.IsNaN(cellBestDistance))
                {
                    cellBestDistance = maxExtent;
                }

                var cellTotalPressure = totalPressure[x, y];

                var modifiedBaseElevation = baseEl + (1f / (0.01f * (cellBestDistance * cellBestDistance) + 1f)) * (1f - baseEl);

                var nx = Mathf.Cos((x * 2f * Mathf.Pi) / Mathf.Max(width, 1));
                var nz = Mathf.Sin((x * 2f * Mathf.Pi) / Mathf.Max(width, 1));

                var value =
                    0.125f * detailNoise.GetNoise3D(8f * nx, 8f * ny, 8f * nz) +
                    0.0625f * detailNoise.GetNoise3D(16f * nx, 16f * ny, 16f * nz) +
                    0.03125f * detailNoise.GetNoise3D(32f * nx, 32f * ny, 32f * nz);

                value *= 7f;
                value *= 1f / (1f + Mathf.Pow(100f, -5f * (value - 0.8f)));

                var input = baseElevation[x, y];

                if (((input + (cellTotalPressure * 0.7f * value)) * baseEl) >= seaLevel)
                {
                    var elevated = (input + cellTotalPressure * value) * modifiedBaseElevation * gradientFactor;
                    elevated = elevated + (0.15f * (1f - elevated)) - 0.12f;
                    modified[x, y] = elevated;
                }
                else
                {
                    modified[x, y] = (input + (cellTotalPressure * 0.7f * value)) * (baseEl * modifiedBaseElevation);
                }

            }
        });

        return modified;
    }

    // Two-pass chamfer distance transform (3-4 weights, normalized back to pixels).
    // X wraps around the map edge; Y clamps, matching the original point search.
    private static void BuildBorderDistanceField(
        List<PlateEdgePoint> borderPoints,
        int width,
        int height,
        float[,] distanceField,
        PlateBoundaryType[,] typeField)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                distanceField[x, y] = float.PositiveInfinity;
            }
        }

        foreach (var point in borderPoints)
        {
            distanceField[point.X, point.Y] = 0f;
            typeField[point.X, point.Y] = point.Type;
        }

        const float straight = 3f;
        const float diagonal = 4f;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var distance = distanceField[x, y];
                var type = typeField[x, y];

                var nx = x > 0 ? x - 1 : width - 1;
                if (distanceField[nx, y] + straight < distance)
                {
                    distance = distanceField[nx, y] + straight;
                    type = typeField[nx, y];
                }

                var py = y > 0 ? y - 1 : 0;
                if (py != y)
                {
                    var left = x > 0 ? x - 1 : width - 1;
                    var right = x + 1 < width ? x + 1 : 0;

                    if (distanceField[left, py] + diagonal < distance)
                    {
                        distance = distanceField[left, py] + diagonal;
                        type = typeField[left, py];
                    }

                    if (distanceField[x, py] + straight < distance)
                    {
                        distance = distanceField[x, py] + straight;
                        type = typeField[x, py];
                    }

                    if (distanceField[right, py] + diagonal < distance)
                    {
                        distance = distanceField[right, py] + diagonal;
                        type = typeField[right, py];
                    }
                }

                distanceField[x, y] = distance;
                typeField[x, y] = type;
            }
        }

        for (var y = height - 1; y >= 0; y--)
        {
            for (var x = width - 1; x >= 0; x--)
            {
                var distance = distanceField[x, y];
                var type = typeField[x, y];

                var nx = x + 1 < width ? x + 1 : 0;
                if (distanceField[nx, y] + straight < distance)
                {
                    distance = distanceField[nx, y] + straight;
                    type = typeField[nx, y];
                }

                var py = y + 1 < height ? y + 1 : height - 1;
                if (py != y)
                {
                    var left = x > 0 ? x - 1 : width - 1;
                    var right = x + 1 < width ? x + 1 : 0;

                    if (distanceField[left, py] + diagonal < distance)
                    {
                        distance = distanceField[left, py] + diagonal;
                        type = typeField[left, py];
                    }

                    if (distanceField[x, py] + straight < distance)
                    {
                        distance = distanceField[x, py] + straight;
                        type = typeField[x, py];
                    }

                    if (distanceField[right, py] + diagonal < distance)
                    {
                        distance = distanceField[right, py] + diagonal;
                        type = typeField[right, py];
                    }
                }

                distanceField[x, y] = distance;
                typeField[x, y] = type;
            }
        }

        const float normalize = 1f / 3f;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var distance = distanceField[x, y];
                distanceField[x, y] = float.IsInfinity(distance) ? distance : distance * normalize;
            }
        }
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
