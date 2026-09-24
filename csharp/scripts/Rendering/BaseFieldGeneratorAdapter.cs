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

        // 2. 岩性与矿化异常度
        var (rockRaster, oreAnomaly) = _resourceGenerator.Generate(width, height, seed, plateResult.BoundaryTypes);

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
        var windVectors = _moistureGenerator.GenerateBaseWind(width, height, seed, options.WindCellCount > 0 ? options.WindCellCount : 64);
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

        // 6. 矿产与多层资源生成（结合地质构造、温湿度环境与灵脉）
        var resCtx = new ResourceContext
        {
            Width = width,
            Height = height,
            Seed = seed,
            Rocks = rockRaster,
            Anomaly = oreAnomaly,
            Elevation = elevation,
            SeaLevel = seaLevel,
            MagicDensity = options.MagicDensity,
            Temperature = temperature,
            Moisture = moisture,
            Boundaries = plateResult.BoundaryTypes
        };
        var deposits = _resourceGenerator.GenerateAllDeposits(resCtx);

        // 7. 转换格式至 Core 域模型
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
        var indBytes = new byte[width, height];
        var supBytes = new byte[width, height];
        var crdBytes = new byte[width, height];
        var leyBytes = new byte[width, height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                rockBytes[x, y] = (byte)rockRaster[x, y];
                oreBytes[x, y] = (byte)deposits.PrimaryOre[x, y];
                indBytes[x, y] = (byte)deposits.IndustrialOre[x, y];
                supBytes[x, y] = (byte)deposits.SupernaturalOre[x, y];
                crdBytes[x, y] = (byte)deposits.CardOre[x, y];
                leyBytes[x, y] = deposits.Leyline[x, y];
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
            Ore = oreBytes,
            IndustrialOre = indBytes,
            SupernaturalOre = supBytes,
            CardOre = crdBytes,
            Leyline = leyBytes
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
            TerrainMorphology.Continents => (1.10f, 0.38f, 0.28f, 0.14f, 0.08f),
            TerrainMorphology.Archipelago => (1.48f, 0.14f, 0.24f, 0.24f, 0.22f),
            TerrainMorphology.FracturedIslands => (1.65f, 0.11f, 0.27f, 0.28f, 0.30f),
            TerrainMorphology.ShallowFragments => (1.32f, 0.16f, 0.20f, 0.20f, 0.16f),
            TerrainMorphology.ColdContinent => (1.00f, 0.29f, 0.19f, 0.17f, 0.10f),
            TerrainMorphology.HotWasteland => (1.08f, 0.27f, 0.17f, 0.15f, 0.09f),
            TerrainMorphology.PolarIcelands => (1.05f, 0.62f, 0.50f, 0.20f, 0.16f),
            TerrainMorphology.AtollChain => (1.90f, 0.80f, 0.60f, 0.30f, 0.34f),
            TerrainMorphology.InlandSea => (0.90f, 0.85f, 0.70f, 0.06f, 0.03f),
            TerrainMorphology.RiftHighlands => (1.02f, 0.58f, 0.34f, 0.30f, 0.24f),
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
                var radial = ComputeWrappedRadial(px, py, 0.5f, 0.5f);
                var lobeA = ComputeWrappedRadial(px, py, 0.34f, 0.56f);
                var lobeC = ComputeWrappedRadial(px, py, 0.18f, 0.42f);

                var nx = Mathf.Cos((x * 2f * Mathf.Pi) / Mathf.Max(width, 1));
                var nz = Mathf.Sin((x * 2f * Mathf.Pi) / Mathf.Max(width, 1));

                var morphologyBase = morphology switch
                {
                    TerrainMorphology.Supercontinent => Mathf.Clamp(1f - 1.24f * radial, 0f, 1f),
                    TerrainMorphology.Continents => BuildContinentsBase(radial, fragmentNoise, nx, ny, nz, px, py, continentCount, seed),
                    TerrainMorphology.Archipelago => Mathf.Clamp(0.56f - 0.42f * radial, 0f, 1f),
                    TerrainMorphology.FracturedIslands => Mathf.Clamp(0.52f - 0.34f * radial, 0f, 1f),
                    TerrainMorphology.ShallowFragments => Mathf.Clamp(0.62f - 0.48f * radial, 0f, 1f),
                    TerrainMorphology.ColdContinent => Mathf.Max(
                        Mathf.Clamp(1f - 1.55f * radial, 0f, 1f),
                        Mathf.Clamp(1f - 1.95f * lobeA, 0f, 1f) * 0.65f),
                    TerrainMorphology.HotWasteland => Mathf.Max(
                        Mathf.Clamp(1f - 1.62f * radial, 0f, 1f),
                        Mathf.Clamp(1f - 2.10f * lobeC, 0f, 1f) * 0.45f),
                    TerrainMorphology.PolarIcelands => Mathf.Max(
                        Mathf.Clamp(1f - 2.55f * py, 0f, 1f),
                        Mathf.Clamp(1f - 2.55f * (1f - py), 0f, 1f)),
                    TerrainMorphology.AtollChain => Mathf.Clamp(0.46f - 1.90f * Mathf.Abs(py - 0.5f), 0f, 1f),
                    TerrainMorphology.InlandSea => Mathf.Clamp(
                        Mathf.Clamp(1f - 1.05f * radial, 0f, 1f)
                        - 1.15f * Mathf.Clamp(1f - 3.40f * radial, 0f, 1f),
                        0f,
                        1f),
                    TerrainMorphology.RiftHighlands => Mathf.Clamp(1f - 1.18f * radial, 0f, 1f),
                    _ => Mathf.Clamp(1f - 1.45f * radial, 0f, 1f)
                };

                var contour = contourNoise.GetNoise3D(2.6f * nx, 2.6f * ny, 2.6f * nz);
                var fragments = fragmentNoise.GetNoise3D(6.2f * nx, 6.2f * ny, 6.2f * nz);

                var falloff = morphologyBase;
                if (morphology == TerrainMorphology.Continents)
                {
                    if (morphologyBase > 0.001f)
                    {
                        falloff += (contour * contourAmp * (0.55f + 0.45f * bias) + fragments * fragmentAmp) * Mathf.Sqrt(morphologyBase);
                    }
                    else
                    {
                        falloff = 0f;
                    }
                }
                else
                {
                    falloff += contour * contourAmp * (0.55f + 0.45f * bias) + fragments * fragmentAmp;
                }
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

    private static readonly Vector2[] ContinentCenters2 =
    {
        new Vector2(0.30f, 0.44f),
        new Vector2(0.74f, 0.56f)
    };

    private static readonly Vector2[] ContinentCenters3 =
    {
        new Vector2(0.34f, 0.56f),
        new Vector2(0.68f, 0.45f),
        new Vector2(0.18f, 0.42f)
    };

    private static readonly Vector2[] ContinentCenters4 =
    {
        new Vector2(0.12f, 0.34f),
        new Vector2(0.37f, 0.70f),
        new Vector2(0.63f, 0.30f),
        new Vector2(0.88f, 0.66f)
    };

    private static readonly (float Cx, float Cy, float Rx, float Ry, float Height)[] ContinentLobes1 =
    {
        (0.50f, 0.50f, 0.26f, 0.22f, 1.00f)
    };

    private static readonly (float Cx, float Cy, float Rx, float Ry, float Height)[] ContinentLobes2 =
    {
        (0.26f, 0.50f, 0.15f, 0.20f, 1.00f),
        (0.74f, 0.50f, 0.15f, 0.20f, 1.00f)
    };

    private static readonly (float Cx, float Cy, float Rx, float Ry, float Height)[] ContinentLobes3 =
    {
        (0.28f, 0.35f, 0.14f, 0.14f, 1.00f),
        (0.72f, 0.35f, 0.14f, 0.14f, 1.00f),
        (0.50f, 0.70f, 0.15f, 0.14f, 1.00f)
    };

    private static readonly (float Cx, float Cy, float Rx, float Ry, float Height)[] ContinentLobes4 =
    {
        (0.27f, 0.32f, 0.13f, 0.12f, 1.00f),
        (0.27f, 0.68f, 0.13f, 0.12f, 1.00f),
        (0.73f, 0.32f, 0.13f, 0.12f, 1.00f),
        (0.73f, 0.68f, 0.13f, 0.12f, 1.00f)
    };

    private static readonly (float Cx, float Cy, float Rx, float Ry, float Height)[] ContinentLobes5 =
    {
        // 1. 西北大洲 (North-West Continent)
        (0.22f, 0.30f, 0.13f, 0.12f, 1.00f),
        (0.25f, 0.39f, 0.07f, 0.07f, 0.94f),

        // 2. 西南大洲 (South-West Continent)
        (0.22f, 0.70f, 0.12f, 0.13f, 0.98f),
        (0.25f, 0.61f, 0.06f, 0.07f, 0.92f),

        // 3. 中北大洲 (North-Central Continent)
        (0.52f, 0.28f, 0.14f, 0.12f, 1.00f),
        (0.54f, 0.38f, 0.07f, 0.07f, 0.94f),

        // 4. 中南大洲 (South-Central Continent)
        (0.52f, 0.72f, 0.13f, 0.13f, 0.98f),
        (0.50f, 0.62f, 0.07f, 0.07f, 0.92f),

        // 5. 东部大洲 (Eastern Continent)
        (0.84f, 0.50f, 0.13f, 0.15f, 0.98f),
        (0.88f, 0.40f, 0.06f, 0.08f, 0.92f)
    };

    private static readonly (float Cx, float Cy, float Rx, float Ry, float Height)[] ContinentLobes6 =
    {
        (0.20f, 0.32f, 0.10f, 0.11f, 1.00f),
        (0.52f, 0.32f, 0.10f, 0.11f, 1.00f),
        (0.84f, 0.32f, 0.10f, 0.11f, 1.00f),
        (0.20f, 0.68f, 0.10f, 0.11f, 0.98f),
        (0.52f, 0.68f, 0.10f, 0.11f, 0.98f),
        (0.84f, 0.68f, 0.10f, 0.11f, 0.98f)
    };

    private static readonly (float Cx, float Cy, float Rx, float Ry, float Height)[] ContinentLobes7 =
    {
        (0.18f, 0.30f, 0.09f, 0.10f, 1.00f),
        (0.50f, 0.30f, 0.09f, 0.10f, 1.00f),
        (0.82f, 0.30f, 0.09f, 0.10f, 1.00f),
        (0.34f, 0.50f, 0.09f, 0.09f, 0.96f),
        (0.18f, 0.70f, 0.09f, 0.10f, 0.98f),
        (0.56f, 0.70f, 0.09f, 0.10f, 0.98f),
        (0.88f, 0.70f, 0.09f, 0.10f, 0.98f)
    };

    private static float BuildContinentsBase(float radial, FastNoiseLite fragmentNoise, float nx, float ny, float nz, float px, float py, int continentCount, int seed)
    {
        var normalizedCount = Mathf.Clamp(continentCount, 1, 7);
        var lobes = normalizedCount switch
        {
            1 => ContinentLobes1,
            2 => ContinentLobes2,
            3 => ContinentLobes3,
            4 => ContinentLobes4,
            6 => ContinentLobes6,
            7 => ContinentLobes7,
            _ => ContinentLobes5
        };

        // 球面域扭曲：使各大洲形态摆脱机械椭圆感，形成自然弧形山系与海湾走势
        var warpX = fragmentNoise.GetNoise3D(2.4f * nx + 13.7f, 2.4f * ny - 9.2f, 2.4f * nz + seed * 0.0001f) * 0.032f;
        var warpY = fragmentNoise.GetNoise3D(2.4f * nx - 8.1f, 2.4f * ny + 11.4f, 2.4f * nz - seed * 0.0001f) * 0.032f;
        var warpedPx = px + warpX;
        var warpedPy = Mathf.Clamp(py + warpY, 0.02f, 0.98f);

        // 2 级球面分形噪声，丰富大陆边缘的岬角、半岛与海湾，消除机械几何感
        var coastNoise = fragmentNoise.GetNoise3D(4.2f * nx + seed * 0.0003f, 4.2f * ny, 4.2f * nz - seed * 0.0002f);
        var fineNoise = fragmentNoise.GetNoise3D(9.6f * nx, 9.6f * ny, 9.6f * nz) * 0.5f;
        var fractalDistort = coastNoise * 0.16f + fineNoise * 0.08f;

        var baseShape = 0f;
        for (var index = 0; index < lobes.Length; index++)
        {
            var (cx, cy, rx, ry, height) = lobes[index];
            var dx = Mathf.Abs(warpedPx - cx);
            if (dx > 0.5f)
            {
                dx = 1f - dx;
            }
            var dy = Mathf.Abs(warpedPy - cy);

            // 各向异性椭圆尺度归一化
            var nxDist = dx / rx;
            var nyDist = dy / ry;
            var dist = Mathf.Sqrt(nxDist * nxDist + nyDist * nyDist);

            // 分形海岸扰动
            var effectiveDist = dist + fractalDistort;

            if (effectiveDist < 1.02f)
            {
                float continentVal;
                if (effectiveDist <= 0.60f)
                {
                    continentVal = height;
                }
                else
                {
                    // 从 0.60 到 1.02 平滑三次 Hermite 阶跃，形成自然平缓的大陆架与海岸坡降
                    var t = (effectiveDist - 0.60f) / 0.42f;
                    var smooth = 1f - (t * t * (3f - 2f * t));
                    continentVal = smooth * height;
                }
                baseShape = Mathf.Max(baseShape, continentVal);
            }
        }

        return Mathf.Clamp(baseShape, 0f, 1f);
    }

    private static float ComputeWrappedRadial(float x, float y, float cx, float cy)
    {
        var dx = Mathf.Abs(x - cx);
        if (dx > 0.5f)
        {
            dx = 1f - dx;
        }

        var dy = Mathf.Abs(y - cy);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }
}
