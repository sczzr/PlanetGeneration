using Godot;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Generation;
using PlanetGeneration.WorldGen;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlanetGeneration.Rendering;

/// <summary>
/// 基础连续场生成器适配器：
/// 将既有 Godot 噪声与板块/地表算法对接为 Core 库的 <see cref="IBaseFieldGenerator"/> 契约。
/// </summary>
public sealed class BaseFieldGeneratorAdapter : IBaseFieldGenerator
{
    private readonly PlateGenerator _plateGenerator = new();
    private readonly ElevationGenerator _elevationGenerator = new();
    private readonly ErosionSimulator _erosionSimulator = new();
    private readonly TemperatureGenerator _temperatureGenerator = new();
    private readonly MoistureGenerator _moistureGenerator = new();
    private readonly ResourceGenerator _resourceGenerator = new();

    public BaseContinuousFields GenerateFields(GenerationOptions options, int fieldWidth, int fieldHeight)
    {
        var width = fieldWidth;
        var height = fieldHeight;
        var seed = options.Seed;
        var seaLevel = options.SeaLevel;

        // 1. 板块生成
        var plateCount = options.PlateCount;
        var oceanicRatio = options.OceanicRatio;
        var plateResult = _plateGenerator.Generate(width, height, plateCount, seed, oceanicRatio);

        // 2. 资源分布
        var (rockRaster, oreRaster) = _resourceGenerator.Generate(width, height, seed, plateResult.BoundaryTypes);

        // 3. 高度场基础与地貌掩膜
        var elevation = _elevationGenerator.Generate(width, height, seed, seaLevel, plateResult);
        elevation = ApplyMorphologyMask(
            elevation,
            plateResult,
            width,
            height,
            seaLevel,
            options.ContinentBias,
            options.InteriorRelief,
            options.OrogenyStrength,
            options.SubductionArcRatio,
            options.ContinentalAge,
            options.Morphology,
            seed,
            options.ContinentCount);

        // 4. 水力侵蚀与地形归一化
        var water = Array2D.Create(width, height, 1f);
        var emptyRiver = Array2D.Create(width, height, 0f);
        _erosionSimulator.Run(width, height, options.ErosionIterations, elevation, water, emptyRiver);

        var targetOceanRatio = Mathf.Clamp(seaLevel * 1.15f, 0.20f, 0.85f);
        elevation = NormalizeElevation(elevation, width, height, seaLevel, targetOceanRatio);

        // 5. 温度、风场与湿度扩散
        var temperature = _temperatureGenerator.Generate(width, height, seed, elevation, options.HeatFactor);
        var windVectors = _moistureGenerator.GenerateBaseWind(width, height, seed, 8);
        var baseMoisture = _moistureGenerator.GenerateBaseMoisture(width, height, seaLevel, elevation, temperature);
        var moisture = _moistureGenerator.DistributeMoisture(
            width,
            height,
            seaLevel,
            elevation,
            baseMoisture,
            temperature,
            windVectors,
            options.MoistureIterations,
            seed,
            options.MoistureFactor);

        // 6. 转换格式至 Core 域模型
        var corePlateSites = new List<PlateSiteInfo>(plateResult.Sites.Count);
        for (var i = 0; i < plateResult.Sites.Count; i++)
        {
            var s = plateResult.Sites[i];
            corePlateSites.Add(new PlateSiteInfo
            {
                Id = s.Id,
                Position = new PlanetGeneration.Core.Geometry.PolyVec2(s.Position.X, s.Position.Y),
                Motion = new PlanetGeneration.Core.Geometry.PolyVec2(s.Motion.X, s.Motion.Y),
                IsOceanic = s.IsOceanic,
                BaseElevation = s.BaseElevation,
                ColorR = s.DebugColor.R,
                ColorG = s.DebugColor.G,
                ColorB = s.DebugColor.B
            });
        }

        var coreBoundaries = new PlanetGeneration.Core.Domain.PlateBoundaryType[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                coreBoundaries[x, y] = (PlanetGeneration.Core.Domain.PlateBoundaryType)(byte)plateResult.BoundaryTypes[x, y];
            }
        }

        var corePlates = new BasePlateField
        {
            Width = width,
            Height = height,
            PlateIds = plateResult.PlateIds,
            BoundaryTypes = coreBoundaries,
            Sites = corePlateSites
        };

        var wind = new (float X, float Y)[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var w = windVectors[x, y];
                wind[x, y] = (w.X, w.Y);
            }
        }

        var rockBytes = new byte[width, height];
        var oreBytes = new byte[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                rockBytes[x, y] = (byte)rockRaster[x, y];
                oreBytes[x, y] = (byte)oreRaster[x, y];
            }
        }

        return new BaseContinuousFields
        {
            Width = width,
            Height = height,
            Plates = corePlates,
            Elevation = elevation,
            Temperature = temperature,
            Moisture = moisture,
            Wind = wind,
            Rock = rockBytes,
            Ore = oreBytes
        };
    }

    private static float[,] NormalizeElevation(float[,] source, int width, int height, float seaLevel, float targetOceanRatio)
    {
        var samples = ArrayPool<float>.Shared.Rent(width * height);
        var count = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = source[x, y];
                if (!float.IsNaN(value) && !float.IsInfinity(value))
                {
                    samples[count++] = value;
                }
            }
        }

        if (count <= 1)
        {
            ArrayPool<float>.Shared.Return(samples);
            return source;
        }

        Array.Sort(samples, 0, count);
        var lowIndex = Mathf.Clamp(Mathf.FloorToInt(count * 0.02f), 0, count - 1);
        var highIndex = Mathf.Clamp(Mathf.FloorToInt(count * 0.98f), lowIndex + 1, count - 1);
        var oceanIndex = Mathf.Clamp(
            Mathf.FloorToInt((count - 1) * Mathf.Clamp(targetOceanRatio, 0.02f, 0.98f)),
            lowIndex,
            highIndex - 1);

        var min = samples[lowIndex];
        var max = samples[highIndex];
        var oceanPivot = samples[oceanIndex];

        var lowerRange = Mathf.Max(oceanPivot - min, 0.00001f);
        var upperRange = Mathf.Max(max - oceanPivot, 0.00001f);

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

                if (value <= oceanPivot)
                {
                    var waterT = Mathf.Clamp((value - min) / lowerRange, 0f, 1f);
                    source[x, y] = waterT * seaLevel;
                }
                else
                {
                    var landT = Mathf.Clamp((value - oceanPivot) / upperRange, 0f, 1f);
                    source[x, y] = seaLevel + ((1f - seaLevel) * Mathf.Pow(landT, 1.05f));
                }
            }
        }

        ArrayPool<float>.Shared.Return(samples);
        return source;
    }

    private static float[,] ApplyMorphologyMask(
        float[,] source,
        PlateResult plateResult,
        int width,
        int height,
        float seaLevel,
        float continentBias,
        float interiorRelief,
        float orogenyStrength,
        float subductionArcRatio,
        int continentalAge,
        TerrainMorphology morphology,
        int seed,
        int continentCount)
    {
        if (continentBias <= 0.001f && morphology == TerrainMorphology.Balanced)
        {
            return source;
        }

        var bias = Mathf.Clamp(continentBias, 0f, 1f);
        var (shapePower, upliftMax, edgeDropMax, contourAmp, fragmentAmp) = morphology switch
        {
            TerrainMorphology.Supercontinent => (0.82f, 0.40f, 0.22f, 0.14f, 0.04f),
            TerrainMorphology.Continents => (1.12f, 0.30f, 0.20f, 0.18f, 0.12f),
            TerrainMorphology.Archipelago => (1.48f, 0.14f, 0.24f, 0.24f, 0.22f),
            TerrainMorphology.FracturedIslands => (1.65f, 0.11f, 0.27f, 0.28f, 0.30f),
            TerrainMorphology.ShallowFragments => (1.32f, 0.16f, 0.20f, 0.20f, 0.16f),
            TerrainMorphology.ColdContinent => (1.00f, 0.29f, 0.19f, 0.17f, 0.10f),
            TerrainMorphology.HotWasteland => (1.08f, 0.27f, 0.17f, 0.15f, 0.09f),
            _ => (1.20f, 0.24f, 0.16f, 0.16f, 0.08f)
        };

        var result = new float[width, height];

        Parallel.For(0, height, y =>
        {
            var contourNoise = new FastNoiseLite
            {
                Seed = seed ^ unchecked((int)0x6f1d3a89),
                NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
                Frequency = 1f
            };
            var fragmentNoise = new FastNoiseLite
            {
                Seed = seed ^ unchecked((int)0x3f84d5b5),
                NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
                Frequency = 1f
            };

            var ny = 4f * y / Mathf.Max(height, 1);
            var py = height <= 1 ? 0f : (float)y / (height - 1);

            for (var x = 0; x < width; x++)
            {
                var px = width <= 1 ? 0f : (float)x / (width - 1);
                var dx = Math.Abs(px - 0.5f);
                if (dx > 0.5f) dx = 1f - dx;
                var dy = py - 0.5f;
                var radial = Mathf.Sqrt((dx * dx * 4f) + (dy * dy * 4f));

                var nx = Mathf.Cos((x * 2f * Mathf.Pi) / Mathf.Max(width, 1));
                var nz = Mathf.Sin((x * 2f * Mathf.Pi) / Mathf.Max(width, 1));

                var morphologyBase = morphology switch
                {
                    TerrainMorphology.Supercontinent => Mathf.Clamp(1f - 1.24f * radial, 0f, 1f),
                    TerrainMorphology.Archipelago => Mathf.Clamp(0.56f - 0.42f * radial, 0f, 1f),
                    TerrainMorphology.FracturedIslands => Mathf.Clamp(0.52f - 0.34f * radial, 0f, 1f),
                    TerrainMorphology.ShallowFragments => Mathf.Clamp(0.62f - 0.48f * radial, 0f, 1f),
                    _ => Mathf.Clamp(1f - 1.45f * radial, 0f, 1f)
                };

                var contour = contourNoise.GetNoise3D(2.6f * nx, 2.6f * ny, 2.6f * nz);
                var fragments = fragmentNoise.GetNoise3D(6.2f * nx, 6.2f * ny, 6.2f * nz);

                var falloff = morphologyBase + (contour * contourAmp * (0.55f + 0.45f * bias)) + (fragments * fragmentAmp);
                falloff = Mathf.Clamp(falloff, 0f, 1f);
                falloff = Mathf.Pow(falloff, shapePower);

                var uplift = falloff * Mathf.Lerp(0.05f, upliftMax, bias);
                var edgeDrop = (1f - falloff) * Mathf.Lerp(0.02f, edgeDropMax, bias);
                var shifted = source[x, y] + uplift - edgeDrop;

                result[x, y] = Mathf.Clamp(shifted, 0f, 1f);
            }
        });

        return result;
    }
}
