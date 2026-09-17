using Godot;
using System.Collections.Generic;

namespace PlanetGeneration.WorldGen;

public sealed class CityGenerator
{
	private struct CityCandidate
	{
		public int X;
		public int Y;
		public float Score;
	}

	private struct SelectedCity
	{
		public int X;
		public int Y;
		public float Score;
		public CityPopulation Population;
	}

	public List<CityInfo> Generate(
		int width,
		int height,
		int seed,
		float seaLevel,
		float[,] elevation,
		float[,] moisture,
		float[,] river,
		BiomeType[,] biome)
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(uint)(seed ^ 0x3c6ef372);

		var candidates = BuildCandidates(width, height, seaLevel, elevation, moisture, river, biome, rng);
		candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

		var numCities = ComputeCityTarget(width, height, rng);

		var selected = SelectCities(candidates, numCities, width, height, seaLevel, elevation, rng);

		var nameRng = new RandomNumberGenerator();
		nameRng.Seed = (ulong)(uint)(seed ^ unchecked((int)0x9e3779b1));

		var result = new List<CityInfo>(selected.Count);
		for (var i = 0; i < selected.Count; i++)
		{
			var city = selected[i];
			result.Add(new CityInfo
			{
				Position = new Vector2I(city.X, city.Y),
				Score = city.Score,
				Population = city.Population,
				Name = BuildCityName(nameRng)
			});
		}

		return result;
	}

	private List<CityCandidate> BuildCandidates(
		int width,
		int height,
		float seaLevel,
		float[,] elevation,
		float[,] moisture,
		float[,] river,
		BiomeType[,] biome,
		RandomNumberGenerator rng)
	{
		var candidates = new List<CityCandidate>(width * height);
		var seaRange = Mathf.Max(1f - seaLevel, 0.0001f);

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				if (elevation[x, y] <= seaLevel || biome[x, y] is BiomeType.Ocean or BiomeType.ShallowOcean)
				{
					continue;
				}

				var nearRiver = false;
				var nearOcean = false;
				var riverNeighbors = 0;

				for (var oy = -1; oy <= 1; oy++)
				{
					var ny = y + oy;
					if (ny < 0 || ny >= height)
					{
						continue;
					}

					for (var ox = -1; ox <= 1; ox++)
					{
						if (ox == 0 && oy == 0)
						{
							continue;
						}

						var nx = WrapX(x + ox, width);
						if (river[nx, ny] > 0.04f)
						{
							nearRiver = true;
							riverNeighbors++;
						}

						if (biome[nx, ny] is BiomeType.Ocean or BiomeType.ShallowOcean)
						{
							nearOcean = true;
						}
					}
				}

				var moistureFactor = Mathf.Clamp(moisture[x, y], 0f, 1f);
				var localRiver = Mathf.Clamp(river[x, y], 0f, 1f);
				var riverAccess = Mathf.Clamp(Mathf.Sqrt(localRiver) * 0.66f + (nearRiver ? 0.34f : 0f), 0f, 1f);
				var coastAccess = nearOcean ? 1f : 0f;
				var elevationNorm = Mathf.Clamp((elevation[x, y] - seaLevel) / seaRange, 0f, 1f);
				var mountainPenalty = Mathf.Clamp((elevationNorm - 0.55f) / 0.45f, 0f, 1f);
				var biomeSuitability = GetBiomeSuitability(biome[x, y]);
				var confluenceBonus = Mathf.Clamp((riverNeighbors - 1) * 0.05f, 0f, 0.14f);

				var score = moistureFactor * 0.34f
					+ riverAccess * 0.36f
					+ coastAccess * 0.12f
					+ biomeSuitability * 0.22f
					+ (1f - mountainPenalty) * 0.08f
					+ confluenceBonus;

				if (nearOcean && localRiver > 0.08f)
				{
					score += 0.06f;
				}

				var jitter = 0.88f + 0.24f * rng.Randf();
				score = Mathf.Clamp(score * jitter, 0f, 1f);

				candidates.Add(new CityCandidate
				{
					X = x,
					Y = y,
					Score = score
				});
			}
		}

		return candidates;
	}

	private List<SelectedCity> SelectCities(
		List<CityCandidate> sortedCandidates,
		int numCities,
		int width,
		int height,
		float seaLevel,
		float[,] elevation,
		RandomNumberGenerator rng)
	{
		var target = Mathf.Clamp(numCities, 1, sortedCandidates.Count);
		var cities = new List<SelectedCity>(target);

		if (sortedCandidates.Count == 0)
		{
			return cities;
		}

		var mapScale = (width + height) * 0.5f;
		var spacing = Mathf.Max(6f, mapScale / Mathf.Max(Mathf.Sqrt(target) * 2.25f, 1f));
		var minSpacing = Mathf.Max(3f, spacing * 0.45f);

		while (cities.Count < target && spacing >= minSpacing)
		{
			var addedThisRound = false;

			for (var i = 0; i < sortedCandidates.Count && cities.Count < target; i++)
			{
				var candidate = sortedCandidates[i];
				if (elevation[candidate.X, candidate.Y] < seaLevel)
				{
					continue;
				}

				if (!HasSpacing(candidate, cities, width, spacing))
				{
					continue;
				}

				var rank = cities.Count;
				cities.Add(new SelectedCity
				{
					X = candidate.X,
					Y = candidate.Y,
					Score = candidate.Score,
					Population = SamplePopulation(rng, candidate.Score, rank, target)
				});
				addedThisRound = true;
			}

			spacing *= addedThisRound ? 0.90f : 0.78f;
		}

		if (cities.Count == 0)
		{
			var top = sortedCandidates[0];
			cities.Add(new SelectedCity
			{
				X = top.X,
				Y = top.Y,
				Score = top.Score,
				Population = CityPopulation.Medium
			});
		}

		return cities;
	}

	private static int ComputeCityTarget(int width, int height, RandomNumberGenerator rng)
	{
		var maxCities = Mathf.Max(3, Mathf.RoundToInt(0.000055f * width * height));
		var roll = Mathf.Pow(rng.Randf(), 0.72f);
		var target = 2 + Mathf.FloorToInt(roll * Mathf.Max(maxCities - 1, 1));
		return Mathf.Clamp(target, 2, maxCities);
	}

	private static bool HasSpacing(CityCandidate candidate, List<SelectedCity> cities, int width, float spacing)
	{
		for (var i = 0; i < cities.Count; i++)
		{
			var selected = cities[i];
			var dx = Mathf.Abs(candidate.X - selected.X);
			if (dx > width * 0.5f)
			{
				dx = width - dx;
			}

			var dy = Mathf.Abs(candidate.Y - selected.Y);
			var distance = Mathf.Sqrt(dx * dx + dy * dy);
			if (distance < spacing)
			{
				return false;
			}
		}

		return true;
	}

	private CityPopulation SamplePopulation(RandomNumberGenerator rng, float score, int rank, int total)
	{
		var rankFactor = total <= 1 ? 1f : 1f - rank / (float)(total - 1);
		var largeChance = Mathf.Clamp(0.05f + score * 0.24f + rankFactor * 0.18f, 0.05f, 0.72f);
		var mediumChance = Mathf.Clamp(0.44f + score * 0.22f + rankFactor * 0.06f, 0.24f, 0.88f - largeChance);

		var value = rng.Randf();
		if (value < largeChance)
		{
			return CityPopulation.Large;
		}

		if (value < largeChance + mediumChance)
		{
			return CityPopulation.Medium;
		}

		return CityPopulation.Small;
	}

	private static float GetBiomeSuitability(BiomeType biome)
	{
		return biome switch
		{
			BiomeType.River => 0.96f,
			BiomeType.Coastland => 0.86f,
			BiomeType.TropicalRainForest => 0.82f,
			BiomeType.TropicalSeasonalForest => 0.80f,
			BiomeType.TemperateRainForest => 0.79f,
			BiomeType.TemperateSeasonalForest => 0.84f,
			BiomeType.Grassland => 0.88f,
			BiomeType.Savanna => 0.71f,
			BiomeType.Shrubland => 0.58f,
			BiomeType.Chaparral => 0.56f,
			BiomeType.BorealForest => 0.54f,
			BiomeType.Taiga => 0.50f,
			BiomeType.Steppe => 0.40f,
			BiomeType.Tundra => 0.24f,
			BiomeType.TemperateDesert => 0.17f,
			BiomeType.TropicalDesert => 0.12f,
			BiomeType.RockyMountain => 0.15f,
			BiomeType.SnowyMountain => 0.08f,
			BiomeType.Ice => 0.04f,
			_ => 0.42f
		};
	}

	private static int WrapX(int x, int width)
	{
		if (x >= 0)
		{
			return x % width;
		}

		var wrapped = x % width;
		return wrapped == 0 ? 0 : wrapped + width;
	}

	private string BuildCityName(RandomNumberGenerator rng)
	{
		string[] prefixes = ["Ar", "Bel", "Cor", "Dor", "Eld", "Fal", "Gal", "Hel", "Ir", "Kor", "Lan", "Mor", "Nor", "Or", "Pel", "Quel", "Riv", "Sol", "Tor", "Val"];
		string[] mids = ["a", "e", "i", "o", "u", "ae", "ia", "or", "an", "en", "ur", "el"];
		string[] suffixes = ["dor", "haven", "ford", "gate", "keep", "port", "heim", "grad", "stead", "wick", "crest", "fall", "mere", "hold"];

		var prefix = prefixes[rng.RandiRange(0, prefixes.Length - 1)];
		var middle = mids[rng.RandiRange(0, mids.Length - 1)];
		var suffix = suffixes[rng.RandiRange(0, suffixes.Length - 1)];

		return prefix + middle + suffix;
	}
}
