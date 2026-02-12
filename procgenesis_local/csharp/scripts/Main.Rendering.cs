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
	private void SyncMapAspectToCenter()
	{
		var size = _mapCenter.Size;
		_mapAspect.CustomMinimumSize = new Vector2(Mathf.Max(size.X, 0f), Mathf.Max(size.Y, 0f));
	}


	private void RedrawCurrentLayer()
	{
		if (_primaryWorld == null)
		{
			UpdateLorePanel();
			return;
		}

		var selectedId = _layerOption.GetSelectedId();
		var layer = Enum.IsDefined(typeof(MapLayer), selectedId) ? (MapLayer)selectedId : MapLayer.Satellite;
		SyncMapModeFromLayer(selectedId);

		var primaryRender = GetOrRenderLayer(_primaryWorld, layer);
		_mapTexture.Texture = primaryRender.Texture;
		_lastRenderedImage = primaryRender.Image;
		UpdateLegend(layer);

		if (_compareMode && _compareWorld != null)
		{
			var compareRender = GetOrRenderLayer(_compareWorld, layer);
			_lastCompareImage = compareRender.Image;

			_compareStatsLabel.Visible = true;
			_compareStatsLabel.Text = BuildCompareSummary(_primaryWorld, _compareWorld);
		}
		else
		{
			_lastCompareImage = null;
			_compareStatsLabel.Visible = false;
			_compareStatsLabel.Text = string.Empty;
		}

		if (layer == MapLayer.Cities)
		{
			_cityNamesLabel.Visible = true;
			_cityNamesLabel.Text = BuildCitiesText();
		}
		else
		{
			_cityNamesLabel.Visible = false;
			_cityNamesLabel.Text = string.Empty;
		}

		if (layer != MapLayer.Biomes && layer != MapLayer.Landform)
		{
			ResetBiomeHoverState();
		}

		var stats = _primaryWorld.Stats;
		var morphologyText = GetMorphologyText(_terrainMorphology);
		var continentSuffix = _terrainMorphology == TerrainMorphology.Continents ? $"（{_continentCount}块）" : string.Empty;
		var averageTempCelsius = NormalizedTemperatureToCelsius(stats.AvgTemperature);
		var forestPercent = ComputeForestCoveragePercent(_primaryWorld.Biome, MapWidth, MapHeight);
		var (peakHeightMeters, deepSeaHeightMeters) = ComputeTerrainExtremesMeters(_primaryWorld.Elevation, MapWidth, MapHeight, SeaLevel, _currentReliefExaggeration);
		var baseInfoText =
			$"地形形态:{morphologyText}{continentSuffix} | 海洋占比:{stats.OceanPercent:0.0}% | 森林占比:{forestPercent:0.0}% | 平均温度:{averageTempCelsius:0.0}℃ | 最高山峰高度:{FormatAltitude(peakHeightMeters)} | 最低深海高度:{FormatAltitude(deepSeaHeightMeters)}";

		if (layer == MapLayer.Ecology)
		{
			EnsureEcologySimulation(_primaryWorld);
			var ecology = _primaryWorld.EcologySimulation;
			if (ecology != null)
			{
				baseInfoText += $" | 生态健康:{ecology.AvgEcologyHealth * 100f:0.0}% | 文明潜力:{ecology.AvgCivilizationPotential * 100f:0.0}% | 文明萌发区:{ecology.CivilizationEmergencePercent:0.0}%";
			}
		}

		if (layer == MapLayer.Civilization)
		{
			EnsureCivilizationSimulation(_primaryWorld);
			var civilization = _primaryWorld.CivilizationSimulation;
			if (civilization != null)
			{
				baseInfoText += $" | 政体数量:{civilization.PolityCount} | 领土覆盖:{civilization.ControlledLandPercent:0.0}% | 核心腹地:{civilization.CoreCellPercent:0.0}% | 最大政体占比:{civilization.DominantPolitySharePercent:0.0}% | 聚落分级(村/镇/城邦):{civilization.HamletCount}/{civilization.TownCount}/{civilization.CityStateCount} | 战争热度:{civilization.ConflictHeatPercent:0.0}% | 联盟凝聚:{civilization.AllianceCohesionPercent:0.0}% | 边界波动:{civilization.BorderVolatilityPercent:0.0}%";
				var focusedEvent = GetFocusedTimelineEvent(civilization.RecentEvents);
				if (focusedEvent != null)
				{
					baseInfoText += $" | 回放焦点:第{focusedEvent.Epoch}纪元-{focusedEvent.Category}";
				}
			}
		}

		if (layer == MapLayer.TradeRoutes)
		{
			EnsureCivilizationSimulation(_primaryWorld);
			var civilization = _primaryWorld.CivilizationSimulation;
			if (civilization != null)
			{
				baseInfoText += $" | 贸易走廊覆盖:{civilization.TradeRouteCells} 格 | 枢纽联通率:{civilization.ConnectedHubPercent:0.0}% | 政体数量:{civilization.PolityCount} | 战争热度:{civilization.ConflictHeatPercent:0.0}% | 联盟凝聚:{civilization.AllianceCohesionPercent:0.0}%";
				var focusedEvent = GetFocusedTimelineEvent(civilization.RecentEvents);
				if (focusedEvent != null)
				{
					baseInfoText += $" | 回放焦点:第{focusedEvent.Epoch}纪元-{focusedEvent.Category}";
				}
			}
		}

		_infoLabel.Text = baseInfoText;
		UpdateLorePanel();
	}


	private static string GetMorphologyText(TerrainMorphology morphology)
	{
		return morphology switch
		{
			TerrainMorphology.Balanced => "均衡大陆",
			TerrainMorphology.Supercontinent => "超级大陆",
			TerrainMorphology.Continents => "大陆群",
			TerrainMorphology.Archipelago => "经典群岛",
			TerrainMorphology.FracturedIslands => "破碎岛链",
			TerrainMorphology.ShallowFragments => "浅海碎陆",
			TerrainMorphology.ColdContinent => "寒冷大陆",
			TerrainMorphology.HotWasteland => "炎热荒原",
			_ => morphology.ToString()
		};
	}

	private static float NormalizedTemperatureToCelsius(float normalizedTemperature)
	{
		var t = Mathf.Clamp(normalizedTemperature, 0f, 1f);
		return Mathf.Lerp(TemperatureMinCelsius, TemperatureMaxCelsius, t);
	}

	private static float ComputeForestCoveragePercent(BiomeType[,] biome, int width, int height)
	{
		var landCount = 0;
		var forestCount = 0;

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var current = biome[x, y];
				if (current == BiomeType.Ocean || current == BiomeType.ShallowOcean)
				{
					continue;
				}

				landCount++;
				if (IsForestBiome(current))
				{
					forestCount++;
				}
			}
		}

		if (landCount <= 0)
		{
			return 0f;
		}

		return 100f * forestCount / landCount;
	}

	private static bool IsForestBiome(BiomeType biome)
	{
		return biome switch
		{
			BiomeType.TropicalRainForest => true,
			BiomeType.TropicalSeasonalForest => true,
			BiomeType.TemperateRainForest => true,
			BiomeType.TemperateSeasonalForest => true,
			BiomeType.BorealForest => true,
			BiomeType.Taiga => true,
			_ => false
		};
	}

	private static string FormatAltitude(float meters)
	{
		var kilometers = meters / 1000f;
		return $"{meters:0,0} m（{kilometers:0.0} km）";
	}

	private static (float PeakHeightMeters, float DeepSeaHeightMeters) ComputeTerrainExtremesMeters(float[,] elevation, int width, int height, float seaLevel, float reliefExaggeration)
	{
		var safeSea = Mathf.Clamp(seaLevel, 0.0001f, 0.9999f);
		var maxLandNormalized = 0f;
		var maxSeaDepthNormalized = 0f;

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var value = elevation[x, y];
				if (float.IsNaN(value) || float.IsInfinity(value))
				{
					continue;
				}

				if (value >= safeSea)
				{
					var landHeight = (value - safeSea) / Mathf.Max(1f - safeSea, 0.0001f);
					if (landHeight > maxLandNormalized)
					{
						maxLandNormalized = landHeight;
					}
					continue;
				}

				var seaDepth = (safeSea - value) / Mathf.Max(safeSea, 0.0001f);
				if (seaDepth > maxSeaDepthNormalized)
				{
					maxSeaDepthNormalized = seaDepth;
				}
			}
		}

		var exaggeration = Mathf.Clamp(reliefExaggeration, ReliefExaggerationMin, ReliefExaggerationMax);
		var exaggeratedPeakMeters = maxLandNormalized * EarthHighestPeakMeters * exaggeration;
		var exaggeratedDeepMeters = -maxSeaDepthNormalized * EarthDeepestTrenchMeters * exaggeration;
		return (Mathf.Max(exaggeratedPeakMeters, 0f), Mathf.Min(exaggeratedDeepMeters, 0f));
	}

	private LayerRenderCacheEntry GetOrRenderLayer(GeneratedWorldData world, MapLayer layer)
	{
		var signature = BuildLayerRenderSignature(layer);
		if (world.LayerRenderCache.TryGetValue(layer, out var cached) && cached.Signature == signature)
		{
			cached.LastAccessTick = ++_renderCacheAccessCounter;
			return cached;
		}

		var image = RenderLayer(world, layer);
		var entry = new LayerRenderCacheEntry
		{
			Signature = signature,
			Image = image,
			Texture = ImageTexture.CreateFromImage(image),
			LastAccessTick = ++_renderCacheAccessCounter
		};

		StoreLayerRenderCache(world, layer, entry);
		return entry;
	}

	private int BuildLayerRenderSignature(MapLayer layer)
	{
		return layer switch
		{
			MapLayer.Elevation => (int)_elevationStyle,
			MapLayer.Wind => Mathf.RoundToInt(WindArrowDensity * 1000f),
			MapLayer.Landform => Mathf.RoundToInt(BasinSensitivity * 1000f),
			MapLayer.Ecology => BuildEcologySignature(),
			MapLayer.Civilization => BuildCivilizationSignature(),
			MapLayer.TradeRoutes => BuildCivilizationSignature(),
			_ => 0
		};
	}

	private int BuildEcologySignature()
	{
		return HashCode.Combine(
			Seed,
			_currentEpoch,
			_speciesDiversity,
			_civilAggression,
			_magicDensity,
			MapWidth,
			MapHeight,
			Mathf.RoundToInt(SeaLevel * 10000f));
	}

	private int BuildCivilizationSignature()
	{
		return BuildEcologySignature();
	}

	private void StoreLayerRenderCache(GeneratedWorldData world, MapLayer layer, LayerRenderCacheEntry entry)
	{
		world.LayerRenderCache[layer] = entry;
		if (world.LayerRenderCache.Count <= LayerRenderCacheCapacity)
		{
			return;
		}

		var hasOldest = false;
		var oldestLayer = layer;
		var oldestTick = long.MaxValue;

		foreach (var pair in world.LayerRenderCache)
		{
			if (pair.Key == layer)
			{
				continue;
			}

			if (pair.Value.LastAccessTick >= oldestTick)
			{
				continue;
			}

			hasOldest = true;
			oldestLayer = pair.Key;
			oldestTick = pair.Value.LastAccessTick;
		}

		if (hasOldest)
		{
			world.LayerRenderCache.Remove(oldestLayer);
		}
	}

	private Image RenderLayer(GeneratedWorldData world, MapLayer layer)
	{
		if (layer == MapLayer.Landform)
		{
			var landformImage = BuildLandformImage(world.Elevation, world.Moisture, world.River, SeaLevel, MapWidth, MapHeight);
			return MapWidth == OutputWidth && MapHeight == OutputHeight
				? landformImage
				: UpscaleImageNearest(landformImage, OutputWidth, OutputHeight);
		}

		if (layer == MapLayer.Ecology)
		{
			EnsureEcologySimulation(world);
		}

		if (layer == MapLayer.Civilization)
		{
			EnsureCivilizationSimulation(world);
		}

		if (layer == MapLayer.TradeRoutes)
		{
			EnsureCivilizationSimulation(world);
		}

		var sourceImage = _renderer.Render(
			MapWidth,
			MapHeight,
			layer,
			world.PlateResult,
			world.Elevation,
			world.Temperature,
			world.Moisture,
			world.Wind,
			world.River,
			world.Biome,
			world.Rock,
			world.Ore,
			world.Cities,
			SeaLevel,
			_elevationStyle,
			world.EcologySimulation?.EcologyHealth,
			world.CivilizationSimulation?.Influence,
			world.CivilizationSimulation?.PolityId,
			world.CivilizationSimulation?.BorderMask,
			world.CivilizationSimulation?.TradeRouteMask,
			world.CivilizationSimulation?.TradeFlow);

		var finalImage = MapWidth == OutputWidth && MapHeight == OutputHeight
			? sourceImage
			: UpscaleImageNearest(sourceImage, OutputWidth, OutputHeight);

		if (layer == MapLayer.Wind)
		{
			_renderer.OverlayWindArrows(finalImage, world.Wind, MapWidth, MapHeight, WindArrowDensity);
		}

		if (layer == MapLayer.Civilization || layer == MapLayer.TradeRoutes)
		{
			ApplyTimelineHotspotOverlay(finalImage, world, layer);
		}

		return finalImage;
	}

	private void EnsureEcologySimulation(GeneratedWorldData world)
	{
		var signature = BuildEcologySignature();
		if (world.EcologySimulation != null && world.EcologySignature == signature)
		{
			return;
		}

		world.EcologySimulation = _ecologySimulator.Simulate(
			MapWidth,
			MapHeight,
			Seed,
			_currentEpoch,
			_speciesDiversity,
			_civilAggression,
			_magicDensity,
			SeaLevel,
			world.Elevation,
			world.Temperature,
			world.Moisture,
			world.River,
			world.Biome);
		world.EcologySignature = signature;
	}

	private void EnsureCivilizationSimulation(GeneratedWorldData world)
	{
		EnsureEcologySimulation(world);

		var signature = BuildCivilizationSignature();
		if (world.CivilizationSimulation != null && world.CivilizationSignature == signature)
		{
			return;
		}

		if (world.EcologySimulation == null)
		{
			world.CivilizationSimulation = null;
			world.CivilizationSignature = int.MinValue;
			return;
		}

		world.CivilizationSimulation = _civilizationSimulator.Simulate(
			MapWidth,
			MapHeight,
			Seed,
			_currentEpoch,
			_civilAggression,
			_speciesDiversity,
			SeaLevel,
			world.Elevation,
			world.River,
			world.Biome,
			world.Cities,
			world.EcologySimulation.CivilizationPotential);
		world.CivilizationSignature = signature;
	}

	private Image BuildLandformImage(float[,] elevation, float[,] moisture, float[,] river, float seaLevel, int width, int height)
	{
		var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var landform = ClassifyLandform(x, y, seaLevel, elevation, moisture, river);
				var color = GetLandformColor(landform);
				image.SetPixel(x, y, color);
			}
		}

		return image;
	}

	private static Image UpscaleImageNearest(Image source, int targetWidth, int targetHeight)
	{
		var sourceWidth = source.GetWidth();
		var sourceHeight = source.GetHeight();

		if (sourceWidth == targetWidth && sourceHeight == targetHeight)
		{
			return source;
		}

		var scaled = Image.CreateEmpty(targetWidth, targetHeight, false, Image.Format.Rgba8);

		for (var y = 0; y < targetHeight; y++)
		{
			var sampleY = Mathf.Clamp((int)((long)y * sourceHeight / targetHeight), 0, sourceHeight - 1);
			for (var x = 0; x < targetWidth; x++)
			{
				var sampleX = Mathf.Clamp((int)((long)x * sourceWidth / targetWidth), 0, sourceWidth - 1);
				scaled.SetPixel(x, y, source.GetPixel(sampleX, sampleY));
			}
		}

		return scaled;
	}

	private string BuildCompareSummary(GeneratedWorldData primary, GeneratedWorldData compare)
	{
		var dOcean = primary.Stats.OceanPercent - compare.Stats.OceanPercent;
		var dRiver = primary.Stats.RiverPercent - compare.Stats.RiverPercent;
		var dTemp = primary.Stats.AvgTemperature - compare.Stats.AvgTemperature;
		var dMoist = primary.Stats.AvgMoisture - compare.Stats.AvgMoisture;
		var dCities = primary.Stats.CityCount - compare.Stats.CityCount;

		return $"A:{primary.Tuning.Name}  B:{compare.Tuning.Name} | Δ海洋占比:{dOcean:+0.00;-0.00;0.00}%  Δ河流占比:{dRiver:+0.00;-0.00;0.00}%  Δ平均温度:{dTemp:+0.000;-0.000;0.000}  Δ平均湿度:{dMoist:+0.000;-0.000;0.000}  Δ城市:{dCities:+0;-0;0}";
	}

	private string BuildCitiesText()
	{
		if (_primaryWorld == null)
		{
			return string.Empty;
		}

		var builder = new StringBuilder();
		builder.AppendLine(BuildCityListSection(_primaryWorld.Tuning.Name, _primaryWorld.Cities));

		if (_compareMode && _compareWorld != null)
		{
			builder.AppendLine();
			builder.AppendLine(BuildCityListSection(_compareWorld.Tuning.Name, _compareWorld.Cities));
		}

		return builder.ToString();
	}

	private static string CityPopulationText(CityPopulation population)
	{
		return population switch
		{
			CityPopulation.Small => "small",
			CityPopulation.Medium => "medium",
			_ => "large"
		};
	}

	private string BuildCityListSection(string title, List<CityInfo> cities)
	{
		var builder = new StringBuilder();
		builder.AppendLine($"Cities ({title}):");

		if (cities.Count == 0)
		{
			builder.AppendLine("- none");
			return builder.ToString();
		}

		var count = Mathf.Min(16, cities.Count);
		for (var i = 0; i < count; i++)
		{
			var city = cities[i];
			builder.AppendLine($"{i + 1}. {city.Name} ({city.Position.X}, {city.Position.Y}) [{CityPopulationText(city.Population)}]");
		}

		if (cities.Count > count)
		{
			builder.AppendLine($"...and {cities.Count - count} more");
		}

		return builder.ToString();
	}

}
