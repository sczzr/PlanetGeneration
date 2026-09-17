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
	private PersistedWorldData SerializeGeneratedWorld(GeneratedWorldData world)
	{
		var width = world.Stats.Width;
		var height = world.Stats.Height;

		var boundaryTypes = new int[width, height];
		var biome = new int[width, height];
		var rock = new int[width, height];
		var ore = new int[width, height];

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				boundaryTypes[x, y] = (int)world.PlateResult.BoundaryTypes[x, y];
				biome[x, y] = (int)world.Biome[x, y];
				rock[x, y] = (int)world.Rock[x, y];
				ore[x, y] = (int)world.Ore[x, y];
			}
		}

		var cities = new PersistedCityInfo[world.Cities.Count];
		for (var i = 0; i < world.Cities.Count; i++)
		{
			var city = world.Cities[i];
			cities[i] = new PersistedCityInfo
			{
				X = city.Position.X,
				Y = city.Position.Y,
				Score = city.Score,
				Name = city.Name,
				Population = (int)city.Population
			};
		}

		var sites = new PersistedPlateSite[world.PlateResult.Sites.Count];
		for (var i = 0; i < world.PlateResult.Sites.Count; i++)
		{
			var site = world.PlateResult.Sites[i];
			sites[i] = new PersistedPlateSite
			{
				Id = site.Id,
				X = site.Position.X,
				Y = site.Position.Y,
				MotionX = site.Motion.X,
				MotionY = site.Motion.Y,
				IsOceanic = site.IsOceanic,
				BaseElevation = site.BaseElevation,
				ColorR = site.DebugColor.R,
				ColorG = site.DebugColor.G,
				ColorB = site.DebugColor.B,
				ColorA = site.DebugColor.A
			};
		}

		return new PersistedWorldData
		{
			TuningName = world.Tuning.Name,
			Stats = new PersistedWorldStats
			{
				Width = width,
				Height = height,
				CityCount = world.Stats.CityCount,
				OceanPercent = world.Stats.OceanPercent,
				RiverPercent = world.Stats.RiverPercent,
				AvgTemperature = world.Stats.AvgTemperature,
				AvgMoisture = world.Stats.AvgMoisture
			},
			PlateIds = world.PlateResult.PlateIds,
			BoundaryTypes = boundaryTypes,
			Elevation = world.Elevation,
			Temperature = world.Temperature,
			Moisture = world.Moisture,
			River = world.River,
			Wind = world.Wind,
			Biome = biome,
			Rock = rock,
			Ore = ore,
			Cities = cities,
			PlateSites = sites
		};
	}

	private GeneratedWorldData RestoreGeneratedWorldData(PersistedWorldData world)
	{
		var width = Math.Max(world.Stats.Width, 1);
		var height = Math.Max(world.Stats.Height, 1);

		var boundaryTypes = new PlateBoundaryType[width, height];
		var biome = new BiomeType[width, height];
		var rock = new RockType[width, height];
		var ore = new OreType[width, height];

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				boundaryTypes[x, y] = (PlateBoundaryType)world.BoundaryTypes[x, y];
				biome[x, y] = (BiomeType)world.Biome[x, y];
				rock[x, y] = (RockType)world.Rock[x, y];
				ore[x, y] = (OreType)world.Ore[x, y];
			}
		}

		var sites = new List<PlateSite>(Math.Max(world.PlateSites.Length, 1));
		for (var i = 0; i < world.PlateSites.Length; i++)
		{
			var site = world.PlateSites[i];
			sites.Add(new PlateSite
			{
				Id = site.Id,
				Position = new Vector2I(site.X, site.Y),
				Motion = new Vector2(site.MotionX, site.MotionY),
				IsOceanic = site.IsOceanic,
				BaseElevation = site.BaseElevation,
				DebugColor = new Color(site.ColorR, site.ColorG, site.ColorB, site.ColorA)
			});
		}

		if (sites.Count == 0)
		{
			sites.Add(new PlateSite
			{
				Id = 0,
				Position = new Vector2I(0, 0),
				Motion = new Vector2(1f, 0f),
				IsOceanic = true,
				BaseElevation = 0.5f,
				DebugColor = Colors.White
			});
		}

		var plateIds = world.PlateIds;
		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				if (plateIds[x, y] < 0 || plateIds[x, y] >= sites.Count)
				{
					plateIds[x, y] = 0;
				}
			}
		}

		var cities = new List<CityInfo>(world.Cities.Length);
		for (var i = 0; i < world.Cities.Length; i++)
		{
			var city = world.Cities[i];
			cities.Add(new CityInfo
			{
				Position = new Vector2I(city.X, city.Y),
				Score = city.Score,
				Name = city.Name,
				Population = Enum.IsDefined(typeof(CityPopulation), city.Population)
					? (CityPopulation)city.Population
					: CityPopulation.Medium
			});
		}

		var plateResult = new PlateResult
		{
			PlateIds = plateIds,
			PlateBaseElevation = Array2D.Create(width, height, 0f),
			BoundaryTypes = boundaryTypes,
			StressMap = new PlateStressCell[width, height],
			Neighbors = new List<PlateNeighborInfo>(),
			BorderPoints = new List<PlateEdgePoint>(),
			Sites = sites
		};

		var tuning = ResolveTuningByName(world.TuningName);
		var stats = new WorldStats
		{
			Width = width,
			Height = height,
			CityCount = world.Stats.CityCount,
			OceanPercent = world.Stats.OceanPercent,
			RiverPercent = world.Stats.RiverPercent,
			AvgTemperature = world.Stats.AvgTemperature,
			AvgMoisture = world.Stats.AvgMoisture
		};

		return new GeneratedWorldData
		{
			PlateResult = plateResult,
			Elevation = world.Elevation,
			Temperature = world.Temperature,
			Moisture = world.Moisture,
			Wind = world.Wind,
			River = world.River,
			Biome = biome,
			Rock = rock,
			Ore = ore,
			Cities = cities,
			Stats = stats,
			Tuning = tuning
		};
	}

	private static WorldTuning ResolveTuningByName(string name)
	{
		return name == "Legacy" ? WorldTuning.Legacy() : WorldTuning.Balanced();
	}

	private Godot.Collections.Dictionary ConvertPersistedCacheToGodotDictionary(PersistedWorldCacheEntry entry)
	{
		var result = new Godot.Collections.Dictionary
		{
			["version"] = entry.Version,
			["cache_key"] = entry.CacheKey,
			["seed"] = entry.Seed,
			["map_width"] = entry.MapWidth,
			["map_height"] = entry.MapHeight,
			["compare_mode"] = entry.CompareMode,
			["primary"] = ConvertPersistedWorldToGodotDictionary(entry.Primary)
		};

		if (entry.Compare != null)
		{
			result["compare"] = ConvertPersistedWorldToGodotDictionary(entry.Compare);
		}

		return result;
	}


	private Godot.Collections.Dictionary ConvertPersistedWorldToGodotDictionary(PersistedWorldData world)
	{
		var width = Math.Max(world.Stats.Width, 1);
		var height = Math.Max(world.Stats.Height, 1);

		var cityArray = new Godot.Collections.Array();
		for (var i = 0; i < world.Cities.Length; i++)
		{
			var city = world.Cities[i];
			cityArray.Add(new Godot.Collections.Dictionary
			{
				["x"] = city.X,
				["y"] = city.Y,
				["score"] = city.Score,
				["name"] = city.Name,
				["population"] = city.Population
			});
		}

		var siteArray = new Godot.Collections.Array();
		for (var i = 0; i < world.PlateSites.Length; i++)
		{
			var site = world.PlateSites[i];
			siteArray.Add(new Godot.Collections.Dictionary
			{
				["id"] = site.Id,
				["x"] = site.X,
				["y"] = site.Y,
				["motion_x"] = site.MotionX,
				["motion_y"] = site.MotionY,
				["is_oceanic"] = site.IsOceanic,
				["base_elevation"] = site.BaseElevation,
				["color_r"] = site.ColorR,
				["color_g"] = site.ColorG,
				["color_b"] = site.ColorB,
				["color_a"] = site.ColorA
			});
		}

		return new Godot.Collections.Dictionary
		{
			["tuning_name"] = world.TuningName,
			["stats"] = new Godot.Collections.Dictionary
			{
				["width"] = world.Stats.Width,
				["height"] = world.Stats.Height,
				["city_count"] = world.Stats.CityCount,
				["ocean_percent"] = world.Stats.OceanPercent,
				["river_percent"] = world.Stats.RiverPercent,
				["avg_temperature"] = world.Stats.AvgTemperature,
				["avg_moisture"] = world.Stats.AvgMoisture
			},
			["plate_ids"] = FlattenInt2D(world.PlateIds, width, height),
			["boundary_types"] = FlattenInt2D(world.BoundaryTypes, width, height),
			["elevation"] = FlattenFloat2D(world.Elevation, width, height),
			["temperature"] = FlattenFloat2D(world.Temperature, width, height),
			["moisture"] = FlattenFloat2D(world.Moisture, width, height),
			["river"] = FlattenFloat2D(world.River, width, height),
			["wind"] = FlattenVector2_2D(world.Wind, width, height),
			["biome"] = FlattenInt2D(world.Biome, width, height),
			["rock"] = FlattenInt2D(world.Rock, width, height),
			["ore"] = FlattenInt2D(world.Ore, width, height),
			["cities"] = cityArray,
			["plate_sites"] = siteArray
		};
	}

	private PersistedWorldCacheEntry? ConvertGodotDictionaryToPersistedCache(Godot.Collections.Dictionary root)
	{
		if (!root.ContainsKey("cache_key") || !root.ContainsKey("primary"))
		{
			return null;
		}

		var entry = new PersistedWorldCacheEntry
		{
			Version = ReadIntFromDictionary(root, "version", 1),
			CacheKey = ReadStringFromDictionary(root, "cache_key", string.Empty),
			Seed = ReadIntFromDictionary(root, "seed", 0),
			MapWidth = ReadIntFromDictionary(root, "map_width", 0),
			MapHeight = ReadIntFromDictionary(root, "map_height", 0),
			CompareMode = ReadBoolFromDictionary(root, "compare_mode", false),
			Primary = ConvertGodotDictionaryToPersistedWorld((Godot.Collections.Dictionary)root["primary"])
		};

		if (root.ContainsKey("compare"))
		{
			entry.Compare = ConvertGodotDictionaryToPersistedWorld((Godot.Collections.Dictionary)root["compare"]);
		}

		return entry;
	}

	private PersistedWorldData ConvertGodotDictionaryToPersistedWorld(Godot.Collections.Dictionary dict)
	{
		var statsDict = (Godot.Collections.Dictionary)dict["stats"];
		var width = ReadIntFromDictionary(statsDict, "width", 1);
		var height = ReadIntFromDictionary(statsDict, "height", 1);

		var citiesRaw = ReadArrayFromDictionary(dict, "cities");
		var cities = new PersistedCityInfo[citiesRaw.Count];
		for (var i = 0; i < citiesRaw.Count; i++)
		{
			var cityDict = (Godot.Collections.Dictionary)citiesRaw[i];
			cities[i] = new PersistedCityInfo
			{
				X = ReadIntFromDictionary(cityDict, "x", 0),
				Y = ReadIntFromDictionary(cityDict, "y", 0),
				Score = ReadFloatFromDictionary(cityDict, "score", 0f),
				Name = ReadStringFromDictionary(cityDict, "name", string.Empty),
				Population = ReadIntFromDictionary(cityDict, "population", 1)
			};
		}

		var sitesRaw = ReadArrayFromDictionary(dict, "plate_sites");
		var sites = new PersistedPlateSite[sitesRaw.Count];
		for (var i = 0; i < sitesRaw.Count; i++)
		{
			var siteDict = (Godot.Collections.Dictionary)sitesRaw[i];
			sites[i] = new PersistedPlateSite
			{
				Id = ReadIntFromDictionary(siteDict, "id", i),
				X = ReadIntFromDictionary(siteDict, "x", 0),
				Y = ReadIntFromDictionary(siteDict, "y", 0),
				MotionX = ReadFloatFromDictionary(siteDict, "motion_x", 1f),
				MotionY = ReadFloatFromDictionary(siteDict, "motion_y", 0f),
				IsOceanic = ReadBoolFromDictionary(siteDict, "is_oceanic", false),
				BaseElevation = ReadFloatFromDictionary(siteDict, "base_elevation", 0.5f),
				ColorR = ReadFloatFromDictionary(siteDict, "color_r", 1f),
				ColorG = ReadFloatFromDictionary(siteDict, "color_g", 1f),
				ColorB = ReadFloatFromDictionary(siteDict, "color_b", 1f),
				ColorA = ReadFloatFromDictionary(siteDict, "color_a", 1f)
			};
		}

		return new PersistedWorldData
		{
			TuningName = ReadStringFromDictionary(dict, "tuning_name", "Balanced"),
			Stats = new PersistedWorldStats
			{
				Width = width,
				Height = height,
				CityCount = ReadIntFromDictionary(statsDict, "city_count", 0),
				OceanPercent = ReadFloatFromDictionary(statsDict, "ocean_percent", 0f),
				RiverPercent = ReadFloatFromDictionary(statsDict, "river_percent", 0f),
				AvgTemperature = ReadFloatFromDictionary(statsDict, "avg_temperature", 0f),
				AvgMoisture = ReadFloatFromDictionary(statsDict, "avg_moisture", 0f)
			},
			PlateIds = UnflattenInt2D((Godot.Collections.Array)dict["plate_ids"], width, height),
			BoundaryTypes = UnflattenInt2D((Godot.Collections.Array)dict["boundary_types"], width, height),
			Elevation = UnflattenFloat2D((Godot.Collections.Array)dict["elevation"], width, height),
			Temperature = UnflattenFloat2D((Godot.Collections.Array)dict["temperature"], width, height),
			Moisture = UnflattenFloat2D((Godot.Collections.Array)dict["moisture"], width, height),
			River = UnflattenFloat2D((Godot.Collections.Array)dict["river"], width, height),
			Wind = UnflattenVector2_2D((Godot.Collections.Array)dict["wind"], width, height),
			Biome = UnflattenInt2D((Godot.Collections.Array)dict["biome"], width, height),
			Rock = UnflattenInt2D((Godot.Collections.Array)dict["rock"], width, height),
			Ore = UnflattenInt2D((Godot.Collections.Array)dict["ore"], width, height),
			Cities = cities,
			PlateSites = sites
		};
	}

	private static Godot.Collections.Array FlattenInt2D(int[,] source, int width, int height)
	{
		var output = new Godot.Collections.Array();
		output.Resize(width * height);
		var index = 0;
		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				output[index++] = source[x, y];
			}
		}

		return output;
	}

	private static Godot.Collections.Array FlattenFloat2D(float[,] source, int width, int height)
	{
		var output = new Godot.Collections.Array();
		output.Resize(width * height);
		var index = 0;
		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				output[index++] = source[x, y];
			}
		}

		return output;
	}

	private static Godot.Collections.Array FlattenVector2_2D(Vector2[,] source, int width, int height)
	{
		var output = new Godot.Collections.Array();
		output.Resize(width * height * 2);
		var index = 0;
		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var value = source[x, y];
				output[index++] = value.X;
				output[index++] = value.Y;
			}
		}

		return output;
	}

	private static int[,] UnflattenInt2D(Godot.Collections.Array source, int width, int height)
	{
		var result = new int[width, height];
		var max = Math.Min(source.Count, width * height);
		for (var i = 0; i < max; i++)
		{
			var x = i % width;
			var y = i / width;
			result[x, y] = ConvertVariantToInt(source[i]);
		}

		return result;
	}

	private static float[,] UnflattenFloat2D(Godot.Collections.Array source, int width, int height)
	{
		var result = new float[width, height];
		var max = Math.Min(source.Count, width * height);
		for (var i = 0; i < max; i++)
		{
			var x = i % width;
			var y = i / width;
			result[x, y] = ConvertVariantToFloat(source[i]);
		}

		return result;
	}

	private static Vector2[,] UnflattenVector2_2D(Godot.Collections.Array source, int width, int height)
	{
		var result = new Vector2[width, height];
		var cellCount = Math.Min(source.Count / 2, width * height);
		for (var i = 0; i < cellCount; i++)
		{
			var x = i % width;
			var y = i / width;
			var index = i * 2;
			result[x, y] = new Vector2(ConvertVariantToFloat(source[index]), ConvertVariantToFloat(source[index + 1]));
		}

		return result;
	}

	private static int ReadIntFromDictionary(Godot.Collections.Dictionary dict, string key, int fallback)
	{
		if (!dict.ContainsKey(key))
		{
			return fallback;
		}

		return ConvertVariantToInt(dict[key]);
	}

	private static float ReadFloatFromDictionary(Godot.Collections.Dictionary dict, string key, float fallback)
	{
		if (!dict.ContainsKey(key))
		{
			return fallback;
		}

		return ConvertVariantToFloat(dict[key]);
	}

	private static bool ReadBoolFromDictionary(Godot.Collections.Dictionary dict, string key, bool fallback)
	{
		if (!dict.ContainsKey(key))
		{
			return fallback;
		}

		var value = dict[key];
		if (value.VariantType == Variant.Type.Bool)
		{
			return (bool)value;
		}

		return fallback;
	}

	private static string ReadStringFromDictionary(Godot.Collections.Dictionary dict, string key, string fallback)
	{
		if (!dict.ContainsKey(key))
		{
			return fallback;
		}

		var value = dict[key];
		if (value.VariantType == Variant.Type.String)
		{
			return (string)value;
		}

		return fallback;
	}

	private static Godot.Collections.Array ReadArrayFromDictionary(Godot.Collections.Dictionary dict, string key)
	{
		if (!dict.ContainsKey(key))
		{
			return new Godot.Collections.Array();
		}

		var value = dict[key];
		if (value.VariantType == Variant.Type.Array)
		{
			return (Godot.Collections.Array)value;
		}

		return new Godot.Collections.Array();
	}

	private static int ConvertVariantToInt(Variant value)
	{
		return value.VariantType switch
		{
			Variant.Type.Int => (int)(long)value,
			Variant.Type.Float => Mathf.RoundToInt((float)(double)value),
			Variant.Type.Bool => (bool)value ? 1 : 0,
			_ => 0
		};
	}

	private static float ConvertVariantToFloat(Variant value)
	{
		return value.VariantType switch
		{
			Variant.Type.Float => (float)(double)value,
			Variant.Type.Int => (long)value,
			_ => 0f
		};
	}

}
