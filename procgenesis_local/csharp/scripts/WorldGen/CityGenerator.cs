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

		var candidates = BuildCandidates(width, height, moisture, river, biome, rng);
		candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

		var percentCities = Mathf.RoundToInt(0.00005f * width * height);
		if (percentCities < 1)
		{
			percentCities = 1;
		}

		var numCities = Mathf.FloorToInt(Mathf.Pow(rng.Randf(), 0.714f) * (percentCities - 1)) + 1;
		numCities = Mathf.Clamp(numCities, 1, percentCities);

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
		float[,] moisture,
		float[,] river,
		BiomeType[,] biome,
		RandomNumberGenerator rng)
	{
		var candidates = new List<CityCandidate>(width * height);

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var nearRiver = false;
				var nearOcean = false;

				for (var ox = -1; ox <= 1; ox++)
				{
					for (var oy = -1; oy <= 1; oy++)
					{
						if (ox == 0 && oy == 0)
						{
							continue;
						}

						var ny = y + oy;
						if (ny < 0 || ny >= height)
						{
							continue;
						}

						var nx = x + ox;
						if (nx >= width)
						{
							nx %= width;
						}
						else if (nx < 0)
						{
							nx = width + ox;
						}

						if (river[nx, ny] > 0f)
						{
							nearRiver = true;
							break;
						}

						if (biome[nx, ny] == BiomeType.Ocean || biome[nx, ny] == BiomeType.ShallowOcean)
						{
							nearOcean = true;
							break;
						}
					}
				}

				var score = nearRiver
					? moisture[x, y] + (rng.Randf() * 40f + 10f)
					: moisture[x, y] * rng.Randf() * 0.4f;

				_ = nearOcean;

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
		var cities = new List<SelectedCity>(numCities);

		var firstPopulation = SamplePopulation(rng);

		while (cities.Count < 1)
		{
			if (sortedCandidates.Count == 0)
			{
				return cities;
			}

			var top = sortedCandidates[0];
			if (elevation[top.X, top.Y] >= seaLevel)
			{
				cities.Add(new SelectedCity
				{
					X = top.X,
					Y = top.Y,
					Score = top.Score,
					Population = firstPopulation
				});
				break;
			}

			sortedCandidates.RemoveAt(0);
		}

		if (sortedCandidates.Count == 0)
		{
			return cities;
		}

		var xWrapCheck = 0f;
		var distance = 0f;
		var i = -1;
		var j = -1;
		var count = 0;

		for (var s = 0; ; s++)
		{
			j++;

			while (distance < 35f)
			{
				count++;
				if (count > 1)
				{
					j = 0;
				}

				i++;
				if (i >= sortedCandidates.Count)
				{
					break;
				}

				var candidate = sortedCandidates[i];
				var targetCity = cities[j];

				var dx = Mathf.Abs(candidate.X - targetCity.X);
				if (dx > width / 2f)
				{
					xWrapCheck = width - dx;
				}
				else
				{
					xWrapCheck = dx;
				}

				var dy = Mathf.Abs(candidate.Y - targetCity.Y);
				distance = Mathf.Sqrt(xWrapCheck * xWrapCheck + dy * dy);
			}

			if (i >= sortedCandidates.Count)
			{
				break;
			}

			var current = sortedCandidates[i];

			if (j == cities.Count - 1)
			{
				if (elevation[current.X, current.Y] >= seaLevel)
				{
					cities.Add(new SelectedCity
					{
						X = current.X,
						Y = current.Y,
						Score = current.Score,
						Population = SamplePopulation(rng)
					});
				}

				j = -1;
			}
			else
			{
				i--;
			}

			distance = 0f;
			count = 0;

			if (cities.Count == numCities || i == sortedCandidates.Count - 1)
			{
				break;
			}
		}

		return cities;
	}

	private CityPopulation SamplePopulation(RandomNumberGenerator rng)
	{
		var value = rng.Randf();
		if (value < 0.35f)
		{
			return CityPopulation.Small;
		}

		if (value < 0.85f)
		{
			return CityPopulation.Medium;
		}

		return CityPopulation.Large;
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
