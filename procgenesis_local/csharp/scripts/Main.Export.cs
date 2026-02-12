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
	private void OnExportPngPressed()
	{
		if (_lastRenderedImage == null)
		{
			_infoLabel.Text = "Nothing to export yet. Generate first.";
			return;
		}

		var defaultName = BuildDefaultMapExportName();
		ConfigureAndShowSaveDialog(ExportKind.Png, "保存 PNG", defaultName, "*.png");
	}

	private void OnExportJsonPressed()
	{
		if (_primaryWorld == null)
		{
			_infoLabel.Text = "Nothing to export yet. Generate first.";
			return;
		}

		var defaultName = BuildDefaultExportName("data", ".json");
		ConfigureAndShowSaveDialog(ExportKind.Json, "保存 JSON", defaultName, "*.json");
	}

	private string BuildDefaultExportName(string prefix, string extension)
	{
		var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
		return $"{prefix}_{Seed}_{timestamp}{extension}";
	}

	private string BuildDefaultMapExportName()
	{
		var layerTag = GetLayerFileTag(GetCurrentLayer());
		return BuildDefaultExportName($"map_{layerTag}", ".png");
	}

	private void ConfigureAndShowSaveDialog(ExportKind exportKind, string title, string defaultName, string filter)
	{
		_pendingExportKind = exportKind;
		_saveFileDialog.Title = title;
		_saveFileDialog.ClearFilters();
		_saveFileDialog.AddFilter(filter);
		_saveFileDialog.CurrentFile = defaultName;
		_saveFileDialog.PopupCentered(new Vector2I(920, 560));
	}

	private void OnSaveFileSelected(string selectedPath)
	{
		switch (_pendingExportKind)
		{
			case ExportKind.Png:
				SavePngToPath(selectedPath);
				break;
			case ExportKind.Json:
				SaveJsonToPath(selectedPath);
				break;
		}

		_pendingExportKind = ExportKind.None;
	}

	private void SavePngToPath(string selectedPath)
	{
		if (_lastRenderedImage == null)
		{
			_infoLabel.Text = "PNG export failed: no image.";
			return;
		}

		var layerTag = GetLayerFileTag(GetCurrentLayer());
		var primaryPath = EnsureLayerTagInPath(EnsureFileExtension(selectedPath, ".png"), layerTag);
		var primaryError = _lastRenderedImage.SavePng(primaryPath);

		if (_compareMode && _lastCompareImage != null)
		{
			var comparePath = InsertSuffixBeforeExtension(primaryPath, "_B");
			var compareError = _lastCompareImage.SavePng(comparePath);

			_infoLabel.Text = primaryError == Error.Ok && compareError == Error.Ok
				? $"PNG exported: {primaryPath} + {comparePath}"
				: $"PNG export failed: A={primaryError}, B={compareError}";
			return;
		}

		_infoLabel.Text = primaryError == Error.Ok
			? $"PNG exported: {primaryPath}"
			: $"PNG export failed: {primaryError}";
	}

	private void SaveJsonToPath(string selectedPath)
	{
		if (_primaryWorld == null)
		{
			_infoLabel.Text = "JSON export failed: no world.";
			return;
		}

		var path = EnsureFileExtension(selectedPath, ".json");

		var payload = new Godot.Collections.Dictionary
		{
			["seed"] = Seed,
			["width"] = OutputWidth,
			["height"] = OutputHeight,
			["info_width"] = MapWidth,
			["info_height"] = MapHeight,
			["plates"] = PlateCount,
			["wind_cells"] = WindCellCount,
			["sea_level"] = SeaLevel,
			["heat"] = HeatFactor,
			["river_density"] = RiverDensity,
			["erosion"] = ErosionIterations,
			["continent_count"] = _continentCount,
			["compare_mode"] = _compareMode,
			["primary"] = BuildWorldDictionary(_primaryWorld)
		};

		if (_compareMode && _compareWorld != null)
		{
			payload["compare"] = BuildWorldDictionary(_compareWorld);
		}

		using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
		if (file == null)
		{
			_infoLabel.Text = "JSON export failed: cannot open file.";
			return;
		}

		file.StoreString(Json.Stringify(payload, "  "));
		_infoLabel.Text = $"JSON exported: {path}";
	}

	private static string EnsureFileExtension(string path, string extension)
	{
		if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
		{
			return path;
		}

		return path + extension;
	}

	private static string InsertSuffixBeforeExtension(string path, string suffix)
	{
		var slashIndex = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
		var dotIndex = path.LastIndexOf('.');

		if (dotIndex <= slashIndex)
		{
			return path + suffix;
		}

		return string.Concat(path.AsSpan(0, dotIndex), suffix, path.AsSpan(dotIndex));
	}

	private MapLayer GetCurrentLayer()
	{
		var selectedId = _layerOption.GetSelectedId();
		return Enum.IsDefined(typeof(MapLayer), selectedId) ? (MapLayer)selectedId : MapLayer.Satellite;
	}

	private static string GetLayerFileTag(MapLayer layer)
	{
		return layer switch
		{
			MapLayer.Satellite => "satellite",
			MapLayer.Plates => "plates",
			MapLayer.Temperature => "temperature",
			MapLayer.Rivers => "rivers",
			MapLayer.Moisture => "moisture",
			MapLayer.Wind => "wind",
			MapLayer.Elevation => "elevation",
			MapLayer.RockTypes => "rocktypes",
			MapLayer.Ores => "ores",
			MapLayer.Biomes => "biomes",
			MapLayer.Cities => "cities",
			MapLayer.Landform => "landform",
			MapLayer.Ecology => "ecology",
			MapLayer.Civilization => "civilization",
			MapLayer.TradeRoutes => "trade_routes",
			_ => "satellite"
		};
	}

	private static string EnsureLayerTagInPath(string path, string layerTag)
	{
		var slashIndex = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
		var dotIndex = path.LastIndexOf('.');
		if (dotIndex <= slashIndex)
		{
			dotIndex = path.Length;
		}

		var fileNameStart = slashIndex + 1;
		var fileNameLength = dotIndex - fileNameStart;
		if (fileNameLength <= 0)
		{
			return path;
		}

		var fileNameWithoutExt = path.Substring(fileNameStart, fileNameLength);
		var token = $"_{layerTag}";
		if (fileNameWithoutExt.Contains(token, StringComparison.OrdinalIgnoreCase))
		{
			return path;
		}

		return path.Insert(dotIndex, token);
	}

	private Godot.Collections.Dictionary BuildWorldDictionary(GeneratedWorldData world)
	{
		var cities = new Godot.Collections.Array<Godot.Collections.Dictionary>();
		foreach (var city in world.Cities)
		{
			cities.Add(new Godot.Collections.Dictionary
			{
				["name"] = city.Name,
				["x"] = city.Position.X,
				["y"] = city.Position.Y,
				["score"] = city.Score,
				["population"] = CityPopulationText(city.Population)
			});
		}

		return new Godot.Collections.Dictionary
		{
			["preset"] = world.Tuning.Name,
			["city_count"] = world.Cities.Count,
			["cities"] = cities,
			["stats"] = new Godot.Collections.Dictionary
			{
				["ocean_percent"] = world.Stats.OceanPercent,
				["river_percent"] = world.Stats.RiverPercent,
				["avg_temperature"] = world.Stats.AvgTemperature,
				["avg_moisture"] = world.Stats.AvgMoisture
			}
		};
	}

}
