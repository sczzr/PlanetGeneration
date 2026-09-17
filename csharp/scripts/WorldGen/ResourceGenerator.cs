using Godot;
using System.Threading.Tasks;

namespace PlanetGeneration.WorldGen;

public sealed class ResourceGenerator
{
	public (RockType[,] Rock, OreType[,] Ore) Generate(
		int width,
		int height,
		int seed,
		PlateBoundaryType[,] boundaries)
	{
		var rocks = new RockType[width, height];
		var ores = new OreType[width, height];

		Parallel.For(0, height, y =>
		{
			var rockNoise = new FastNoiseLite
			{
				Seed = (int)(seed ^ 0x6a09e667),
				NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
				FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
				FractalOctaves = 4,
				Frequency = 0.012f
			};

			var oreNoise = new FastNoiseLite
			{
				Seed = (int)(seed ^ 0xbb67ae85),
				NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
				FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
				FractalOctaves = 5,
				Frequency = 0.02f
			};

			for (var x = 0; x < width; x++)
			{
				var nx = Mathf.Cos((x * 2f * Mathf.Pi) / width);
				var nz = Mathf.Sin((x * 2f * Mathf.Pi) / width);
				var ny = 4f * y / height;

				var rockValue = rockNoise.GetNoise3D(nx, ny, nz);
				rockValue = (rockValue + 1f) * 0.5f;
				rockValue *= 0.7f;
				rockValue = Mathf.Pow(rockValue, 2f);

				var isMetamorphic = boundaries[x, y] == PlateBoundaryType.Transform && rockValue > 0.18f;
				var isIgneous = rockValue > 0.4f || boundaries[x, y] == PlateBoundaryType.Convergent;

				rocks[x, y] = isMetamorphic
					? RockType.Metamorphic
					: isIgneous
						? RockType.Igneous
						: RockType.Sedimentary;

				var oreValue =
					0.25f * oreNoise.GetNoise3D(4f * nx, 4f * ny, 4f * nz) +
					0.125f * oreNoise.GetNoise3D(8f * nx, 8f * ny, 8f * nz) +
					0.0625f * oreNoise.GetNoise3D(16f * nx, 16f * ny, 16f * nz);

				oreValue = (oreValue - 0.07f) / 0.3f;
				var oreValue2 = (oreValue + rockValue) / 2f;

				ores[x, y] = DetermineOre(rocks[x, y], oreValue, oreValue2);
			}
		});

		return (rocks, ores);
	}

	private OreType DetermineOre(RockType rock, float oreValue, float oreValue2)
	{
		if (oreValue >= 0.325f && oreValue <= 0.675f)
		{
			return OreType.None;
		}

		return rock switch
		{
			RockType.Sedimentary => SedimentaryOre(oreValue2),
			RockType.Igneous => IgneousOre(oreValue2),
			_ => MetamorphicOre(oreValue2)
		};
	}

	private OreType SedimentaryOre(float value)
	{
		if (value < 0.28f) return OreType.Coal;
		if (value < 0.35f) return OreType.Copper;
		if (value < 0.45f) return OreType.Tin;
		if (value < 0.5f) return OreType.Iron;
		if (value < 0.58f) return OreType.Gold;
		return OreType.Diamond;
	}

	private OreType IgneousOre(float value)
	{
		if (value < 0.22f) return OreType.Copper;
		if (value < 0.28f) return OreType.Platinum;
		if (value < 0.37f) return OreType.Aluminum;
		if (value < 0.46f) return OreType.Iron;
		if (value < 0.55f) return OreType.Silver;
		if (value < 0.62f) return OreType.Tin;
		return OreType.Diamond;
	}

	private OreType MetamorphicOre(float value)
	{
		if (value < 0.28f) return OreType.Copper;
		if (value < 0.36f) return OreType.Lead;
		if (value < 0.51f) return OreType.Silver;
		if (value < 0.62f) return OreType.Gold;
		if (value >= 0.67f) return OreType.Diamond;
		return OreType.None;
	}
}
