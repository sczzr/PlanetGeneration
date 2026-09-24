using Godot;
using PlanetGeneration.WorldGen;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using IOPath = System.IO.Path;
using IOFile = System.IO.File;
using IODirectory = System.IO.Directory;
using IOFileInfo = System.IO.FileInfo;
using CryptoSha256 = System.Security.Cryptography.SHA256;

namespace PlanetGeneration;

public partial class Main : Control
{
	private float[,] NormalizeElevationForPipeline(float[,] source, int width, int height, float seaLevel, float targetOceanRatio)
	{
		var samples = ArrayPool<float>.Shared.Rent(width * height);
		var count = 0;

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var value = source[x, y];
				if (float.IsNaN(value) || float.IsInfinity(value))
				{
					continue;
				}

				samples[count++] = value;
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
					continue;
				}

				var landT = Mathf.Clamp((value - oceanPivot) / upperRange, 0f, 1f);
				source[x, y] = seaLevel + (1f - seaLevel) * Mathf.Pow(landT, 1.05f);
			}
		}

		ArrayPool<float>.Shared.Return(samples);
		return source;
	}

	private float[,] ApplyTerrainMorphologyMask(float[,] source, PlateResult plateResult, int width, int height, float seaLevel, float continentBias, float interiorRelief, float orogenyStrength, float subductionArcRatio, int continentalAge, TerrainMorphology morphology, int seed, int continentCount)
	{
		var morphologyTimer = Stopwatch.StartNew();
		if (continentBias <= 0.001f && morphology == TerrainMorphology.Balanced)
		{
			GD.Print($"[WorldGen][地图 {width}x{height}][地形与地貌] 已跳过形态掩膜: {morphologyTimer.Elapsed.TotalMilliseconds:0} ms");
			return source;
		}

		var bias = Mathf.Clamp(continentBias, 0f, 1f);
		var relief = Mathf.Clamp(interiorRelief, 0.5f, 2.0f);
		var orogenyScale = Mathf.Clamp(orogenyStrength, 0.5f, 2.5f);
		var ageNorm = Mathf.Clamp(continentalAge / 100f, 0f, 1f);
		var ageRoughnessFactor = Mathf.Lerp(1.24f, 0.72f, ageNorm);
		var maskTimer = Stopwatch.StartNew();
		var orogenyMask = BuildOrogenyMask(plateResult, source, width, height, seaLevel, morphology, seed, subductionArcRatio);
		GD.Print($"[WorldGen][地图 {width}x{height}][地形与地貌] 山脉掩膜: {maskTimer.Elapsed.TotalMilliseconds:0} ms");
		var noiseTimer = Stopwatch.StartNew();

		// 归一化阶段按分位数重排高程，所以掩膜幅度需盖过板块高程本身的动态范围，模板拓扑才会真正成型。
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
		GD.Print($"[WorldGen][地图 {width}x{height}][地形与地貌] 噪声与参数初始化: {noiseTimer.Elapsed.TotalMilliseconds:0} ms");
		var result = new float[width, height];
		var gridTimer = Stopwatch.StartNew();

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
				var lobeB = ComputeWrappedRadial(px, py, 0.68f, 0.45f);
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
					falloff += contour * contourAmp * (0.55f + 0.45f * bias);
					falloff += fragments * fragmentAmp;
				}
				falloff = Mathf.Clamp(falloff, 0f, 1f);
				falloff = Mathf.Pow(falloff, shapePower);

				var uplift = falloff * Mathf.Lerp(0.05f, upliftMax, bias);
				var edgeDrop = (1f - falloff) * Mathf.Lerp(0.02f, edgeDropMax, bias);
				var shifted = source[x, y] + uplift - edgeDrop;
				var interiorRidgeLine = 0f;

				if (falloff > 0.48f)
				{
					var interiorMask = (falloff - 0.48f) / 0.52f;
					var interiorNoise = 0.5f + 0.5f * fragmentNoise.GetNoise3D(
						8.4f * nx + 13.7f,
						8.4f * ny - 9.2f,
						8.4f * nz + 4.6f);
					var ridgeNoise = fragmentNoise.GetNoise3D(
						12.8f * nx - 5.1f,
						12.8f * ny + 7.9f,
						12.8f * nz + 3.4f);
					var ridgeSpineNoise = fragmentNoise.GetNoise3D(
						5.0f * nx + 11.2f,
						5.0f * ny - 4.8f,
						5.0f * nz + 6.3f);
					var ridgeShape = 1f - Mathf.Abs(ridgeNoise);
					var ridgeSpineShape = 1f - Mathf.Abs(ridgeSpineNoise);
					var ridgeCombined = ridgeShape * 0.58f + ridgeSpineShape * 0.42f;
					interiorRidgeLine = Mathf.Pow(Mathf.Clamp((ridgeCombined - 0.26f) / 0.74f, 0f, 1f), 1.70f);
					var ridgeCorridor = 0.5f + 0.5f * contourNoise.GetNoise3D(
						1.9f * nx - 2.7f,
						1.9f * ny + 5.4f,
						1.9f * nz + 8.1f);
					interiorRidgeLine = Mathf.Clamp(interiorRidgeLine * Mathf.Lerp(0.72f, 1.12f, ridgeCorridor), 0f, 1f);

					var ridgeBias = morphology switch
					{
						TerrainMorphology.Supercontinent => 0.58f,
						TerrainMorphology.Continents => 0.50f,
						TerrainMorphology.ColdContinent => 0.54f,
						TerrainMorphology.PolarIcelands => 0.52f,
						TerrainMorphology.InlandSea => 0.56f,
						TerrainMorphology.RiftHighlands => 0.34f,
						_ => 0.46f
					};

					var ridgeStrength = Mathf.Clamp((interiorNoise - ridgeBias) / Mathf.Max(1f - ridgeBias, 0.0001f), 0f, 1f);
					var basinStrength = Mathf.Clamp((ridgeBias - interiorNoise) / Mathf.Max(ridgeBias, 0.0001f), 0f, 1f);

					shifted += interiorMask * ridgeStrength * interiorRidgeLine * Mathf.Lerp(0.006f, 0.038f, bias) * relief * ageRoughnessFactor;
					shifted -= interiorMask * basinStrength * Mathf.Lerp(0.01f, 0.06f, bias) * relief * ageRoughnessFactor;

					var broadInteriorFlatten = Mathf.Clamp((falloff - 0.54f) / 0.46f, 0f, 1f);
					shifted -= broadInteriorFlatten * (1f - interiorRidgeLine * 0.60f) * Mathf.Lerp(0.010f, 0.040f, bias) * (2.10f - relief) * Mathf.Lerp(0.94f, 1.34f, ageNorm);

					var deepInteriorPlateau = Mathf.Clamp((falloff - 0.64f) / 0.36f, 0f, 1f);
					shifted -= deepInteriorPlateau * (1f - interiorRidgeLine) * Mathf.Lerp(0.016f, 0.074f, bias) * (2.05f - relief) * Mathf.Lerp(0.96f, 1.42f, ageNorm);

					var interiorCore = Mathf.Clamp((falloff - 0.56f) / 0.44f, 0f, 1f);
					var plainField = 0.5f + 0.5f * contourNoise.GetNoise3D(
						2.4f * nx + 3.8f,
						2.4f * ny - 6.2f,
						2.4f * nz + 1.9f);
					var plainMask = Mathf.Pow(Mathf.Clamp((plainField - 0.26f) / 0.74f, 0f, 1f), 1.25f);
					shifted -= interiorCore * plainMask * (1f - interiorRidgeLine) * Mathf.Lerp(0.008f, 0.034f, bias) * (2.02f - relief) * Mathf.Lerp(0.90f, 1.22f, ageNorm);

					var basinNoiseA = fragmentNoise.GetNoise3D(
						4.8f * nx - 10.4f,
						4.8f * ny + 12.1f,
						4.8f * nz - 7.6f);
					var basinNoiseB = contourNoise.GetNoise3D(
						3.2f * nx + 8.7f,
						3.2f * ny - 2.5f,
						3.2f * nz + 11.3f);
					var basinField = Mathf.Abs(basinNoiseA * 0.68f + basinNoiseB * 0.32f);
					var basinMask = Mathf.Pow(Mathf.Clamp((0.22f - basinField) / 0.22f, 0f, 1f), 1.55f);
					shifted -= interiorCore * basinMask * (1f - interiorRidgeLine * 0.35f) * Mathf.Lerp(0.010f, 0.045f, bias) * Mathf.Lerp(0.92f, 1.28f, ageNorm);
				}

				var edgeBand = Mathf.Clamp((0.62f - falloff) / 0.34f, 0f, 1f);
				var orogeny = orogenyMask[x, y];
				if (orogeny > 0.001f)
				{
					var edgeMountainBoost = Mathf.Lerp(0.02f, 0.14f, bias) * relief;
					var youngEdgeBoost = Mathf.Lerp(1.18f, 0.86f, ageNorm);
					shifted += orogeny * (0.55f + 0.45f * edgeBand) * edgeMountainBoost * orogenyScale * youngEdgeBoost;

					var inlandSuppression = Mathf.Clamp((falloff - 0.68f) / 0.32f, 0f, 1f);
					var oldContinentSmoothing = Mathf.Lerp(0.90f, 1.35f, ageNorm);
					shifted -= orogeny * inlandSuppression * Mathf.Lerp(0.005f, 0.032f, bias) * (2.2f - relief) * Mathf.Lerp(0.8f, 1.3f, Mathf.Clamp(orogenyScale - 0.5f, 0f, 2f) / 2f) * oldContinentSmoothing;
				}
				else if (falloff > 0.70f)
				{
					var deepInterior = Mathf.Clamp((falloff - 0.70f) / 0.30f, 0f, 1f);
					shifted -= deepInterior * Mathf.Lerp(0.008f, 0.045f, bias) * (2.1f - relief) * Mathf.Lerp(0.96f, 1.42f, ageNorm);
				}

				if (falloff < 0.22f)
				{
					shifted -= (0.22f - falloff) * Mathf.Lerp(0.05f, 0.20f, bias);
				}

				result[x, y] = Mathf.Clamp(shifted, 0f, 1f);
			}
		});

		GD.Print($"[WorldGen][地图 {width}x{height}][地形与地貌] 主网格循环: {gridTimer.Elapsed.TotalMilliseconds:0} ms | 单元格 {width * (long)height:N0}");
		GD.Print($"[WorldGen][地图 {width}x{height}][地形与地貌] 形态掩膜总计: {morphologyTimer.Elapsed.TotalMilliseconds:0} ms");
		return result;
	}

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

	private float MapSeaLevelToTargetOceanRatio(float seaLevel)
	{
		var sea = Mathf.Clamp(seaLevel, 0.1f, 0.9f);
		var t = (sea - 0.1f) / 0.8f;
		var oneMinusT = 1f - t;

		var y0 = 0.30f;
		var y1 = 0.815f;
		var y2 = 0.95f;

		var ratio =
			oneMinusT * oneMinusT * y0 +
			2f * oneMinusT * t * y1 +
			t * t * y2;

		return Mathf.Clamp(ratio, 0.30f, 0.95f);
	}

	private static float[,] BuildOrogenyMask(PlateResult plateResult, float[,] elevation, int width, int height, float seaLevel, TerrainMorphology morphology, int seed, float subductionArcRatio)
	{
		var mask = new float[width, height];
		var arcRatio = Mathf.Clamp(subductionArcRatio, 0.2f, 1.0f);

		Parallel.For(0, height, y =>
		{
			for (var x = 0; x < width; x++)
			{
				if (elevation[x, y] <= seaLevel)
				{
					continue;
				}

				var boundaryType = plateResult.BoundaryTypes[x, y];
				float baseWeight;
				switch (boundaryType)
				{
					case PlateBoundaryType.Convergent:
						baseWeight = 1.0f;
						break;
					case PlateBoundaryType.Transform:
						baseWeight = 0.55f;
						break;
					case PlateBoundaryType.Divergent:
						baseWeight = 0.20f;
						break;
					default:
						baseWeight = 0f;
						break;
				}

				if (baseWeight <= 0f)
				{
					continue;
				}

				if (boundaryType == PlateBoundaryType.Convergent)
				{
					var arcNoise = HashNoise01(seed ^ unchecked((int)0x3c6ef35f), x, y);
					if (arcNoise > arcRatio)
					{
						baseWeight *= 0.36f;
					}
				}

				if (IsNearSeaEdge(x, y, elevation, width, height, seaLevel, 4))
				{
					baseWeight *= 1.26f;
				}

				baseWeight *= morphology switch
				{
					TerrainMorphology.Archipelago => 0.82f,
					TerrainMorphology.FracturedIslands => 0.78f,
					TerrainMorphology.ShallowFragments => 0.84f,
					TerrainMorphology.AtollChain => 0.62f,
					TerrainMorphology.PolarIcelands => 1.05f,
					TerrainMorphology.InlandSea => 1.08f,
					TerrainMorphology.RiftHighlands => 1.15f,
					_ => 1f
				};

				if (baseWeight > mask[x, y])
				{
					mask[x, y] = Mathf.Clamp(baseWeight, 0f, 1.2f);
				}
			}
		});

		return BlurMask(mask, width, height, 3);
	}

	private static bool IsNearSeaEdge(int x, int y, float[,] elevation, int width, int height, float seaLevel, int radius)
	{
		for (var oy = -radius; oy <= radius; oy++)
		{
			for (var ox = -radius; ox <= radius; ox++)
			{
				if (ox == 0 && oy == 0)
				{
					continue;
				}

				var nx = WrapX(x + ox, width);
				var ny = ClampY(y + oy, height);
				if (elevation[nx, ny] <= seaLevel)
				{
					return true;
				}
			}
		}

		return false;
	}

	private static float[,] BlurMask(float[,] source, int width, int height, int radius)
	{
		if (radius <= 0)
		{
			return source;
		}

		var blurred = new float[width, height];
		var sigma = Mathf.Max(radius * 0.65f, 0.5f);

		Parallel.For(0, height, y =>
		{
			for (var x = 0; x < width; x++)
			{
				var accum = 0f;
				var weightSum = 0f;

				for (var oy = -radius; oy <= radius; oy++)
				{
					for (var ox = -radius; ox <= radius; ox++)
					{
						var nx = WrapX(x + ox, width);
						var ny = ClampY(y + oy, height);
						var distSq = ox * ox + oy * oy;
						var weight = Mathf.Exp(-distSq / (2f * sigma * sigma));

						accum += source[nx, ny] * weight;
						weightSum += weight;
					}
				}

				blurred[x, y] = weightSum > 0f ? accum / weightSum : source[x, y];
			}
		});

		return blurred;
	}

}
