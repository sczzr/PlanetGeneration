using Godot;
using PlanetGeneration.WorldGen;
using System;
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
		var samples = new float[width * height];
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
		var normalized = new float[width, height];

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var value = source[x, y];
				if (float.IsNaN(value) || float.IsInfinity(value))
				{
					normalized[x, y] = 0f;
					continue;
				}

				if (value <= oceanPivot)
				{
					var waterT = Mathf.Clamp((value - min) / lowerRange, 0f, 1f);
					normalized[x, y] = waterT * seaLevel;
					continue;
				}

				var landT = Mathf.Clamp((value - oceanPivot) / upperRange, 0f, 1f);
				normalized[x, y] = seaLevel + (1f - seaLevel) * Mathf.Pow(landT, 1.05f);
			}
		}

		return normalized;
	}

	private float[,] ApplyTerrainMorphologyMask(float[,] source, PlateResult plateResult, int width, int height, float seaLevel, float continentBias, float interiorRelief, float orogenyStrength, float subductionArcRatio, int continentalAge, TerrainMorphology morphology, int seed, int continentCount)
	{
		if (continentBias <= 0.001f && morphology == TerrainMorphology.Balanced)
		{
			return source;
		}

		var bias = Mathf.Clamp(continentBias, 0f, 1f);
		var relief = Mathf.Clamp(interiorRelief, 0.5f, 2.0f);
		var orogenyScale = Mathf.Clamp(orogenyStrength, 0.5f, 2.5f);
		var ageNorm = Mathf.Clamp(continentalAge / 100f, 0f, 1f);
		var ageRoughnessFactor = Mathf.Lerp(1.24f, 0.72f, ageNorm);
		var result = new float[width, height];
		var orogenyMask = BuildOrogenyMask(plateResult, source, width, height, seaLevel, morphology, seed, subductionArcRatio);

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

		for (var y = 0; y < height; y++)
		{
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
					_ => Mathf.Clamp(1f - 1.45f * radial, 0f, 1f)
				};

				var contour = contourNoise.GetNoise3D(2.6f * nx, 2.6f * ny, 2.6f * nz);
				var fragments = fragmentNoise.GetNoise3D(6.2f * nx, 6.2f * ny, 6.2f * nz);

				var falloff = morphologyBase;
				falloff += contour * contourAmp * (0.55f + 0.45f * bias);
				falloff += fragments * fragmentAmp;
				falloff = Mathf.Clamp(falloff, 0f, 1f);
				falloff = Mathf.Pow(falloff, shapePower);

				var uplift = falloff * Mathf.Lerp(0.05f, upliftMax, bias);
				var edgeDrop = (1f - falloff) * Mathf.Lerp(0.02f, edgeDropMax, bias);
				var shifted = source[x, y] + uplift - edgeDrop;

				if (falloff > 0.48f)
				{
					var interiorMask = (falloff - 0.48f) / 0.52f;
					var interiorNoise = 0.5f + 0.5f * fragmentNoise.GetNoise3D(
						8.4f * nx + 13.7f,
						8.4f * ny - 9.2f,
						8.4f * nz + 4.6f);

					var ridgeBias = morphology switch
					{
						TerrainMorphology.Supercontinent => 0.58f,
						TerrainMorphology.Continents => 0.50f,
						TerrainMorphology.ColdContinent => 0.54f,
						_ => 0.46f
					};

					var ridgeStrength = Mathf.Clamp((interiorNoise - ridgeBias) / Mathf.Max(1f - ridgeBias, 0.0001f), 0f, 1f);
					var basinStrength = Mathf.Clamp((ridgeBias - interiorNoise) / Mathf.Max(ridgeBias, 0.0001f), 0f, 1f);

					shifted += interiorMask * ridgeStrength * Mathf.Lerp(0.01f, 0.08f, bias) * relief * ageRoughnessFactor;
					shifted -= interiorMask * basinStrength * Mathf.Lerp(0.01f, 0.06f, bias) * relief * ageRoughnessFactor;
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
					shifted -= deepInterior * Mathf.Lerp(0.004f, 0.030f, bias) * (2.1f - relief) * Mathf.Lerp(0.92f, 1.38f, ageNorm);
				}

				if (falloff < 0.22f)
				{
					shifted -= (0.22f - falloff) * Mathf.Lerp(0.05f, 0.20f, bias);
				}

				result[x, y] = Mathf.Clamp(shifted, 0f, 1f);
			}
		}

		return result;
	}

	private static float BuildContinentsBase(float radial, FastNoiseLite fragmentNoise, float nx, float ny, float nz, float px, float py, int continentCount, int seed)
	{
		var normalizedCount = Mathf.Clamp(continentCount, 2, 4);
		var centers = normalizedCount switch
		{
			2 => ContinentCenters2,
			4 => ContinentCenters4,
			_ => ContinentCenters3
		};

		var lobeScale = normalizedCount switch
		{
			2 => 1.70f,
			4 => 1.78f,
			_ => 1.84f
		};

		var baseShape = 0f;
		for (var index = 0; index < centers.Length; index++)
		{
			var center = centers[index];
			var lobe = ComputeWrappedRadial(px, py, center.X, center.Y);
			var continent = Mathf.Clamp(1f - lobeScale * lobe, 0f, 1f);

			if (normalizedCount == 4)
			{
				var core = Mathf.Clamp(1f - 2.30f * lobe, 0f, 1f);
				continent = Mathf.Max(continent, 0.92f * core);
			}

			baseShape = Mathf.Max(baseShape, continent);
		}

		var centerBridge = Mathf.Clamp(1f - (radial / 0.20f), 0f, 1f);
		var centerSuppression = normalizedCount switch
		{
			2 => 0.24f,
			4 => 0.26f,
			_ => 0.36f
		};
		var centerNoise = 0.5f + 0.5f * fragmentNoise.GetNoise3D(3.1f * nx + seed * 0.0003f, 3.1f * ny, 3.1f * nz - seed * 0.0002f);
		var centerSuppressionScale = Mathf.Lerp(0.70f, 1.20f, centerNoise);
		baseShape -= centerBridge * centerSuppression * centerSuppressionScale;

		var split = fragmentNoise.GetNoise3D(5.8f * nx, 5.8f * ny, 5.8f * nz);
		baseShape += split * (normalizedCount == 4 ? 0.08f : 0.10f);

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

		for (var y = 0; y < height; y++)
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
					_ => 1f
				};

				if (baseWeight > mask[x, y])
				{
					mask[x, y] = Mathf.Clamp(baseWeight, 0f, 1.2f);
				}
			}
		}

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

		for (var y = 0; y < height; y++)
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
		}

		return blurred;
	}

}
