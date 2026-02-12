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
	private string BuildWorldGenerationCacheKey()
	{
		var seaLevelQuantized = Mathf.RoundToInt(SeaLevel * 10000f);
		var heatFactorQuantized = Mathf.RoundToInt(HeatFactor * 10000f);
		var riverDensityQuantized = Mathf.RoundToInt(RiverDensity * 10000f);
		var oceanicRatioQuantized = Mathf.RoundToInt(_terrainOceanicRatio * 10000f);
		var continentBiasQuantized = Mathf.RoundToInt(_terrainContinentBias * 10000f);
		var interiorReliefQuantized = Mathf.RoundToInt(_interiorRelief * 10000f);
		var orogenyStrengthQuantized = Mathf.RoundToInt(_orogenyStrength * 10000f);
		var subductionArcRatioQuantized = Mathf.RoundToInt(_subductionArcRatio * 10000f);
		var continentalAgeQuantized = Mathf.Clamp(_continentalAge, 0, 100);
		var deepOceanFactorQuantized = Mathf.RoundToInt(_tuning.DeepOceanFactor * 10000f);
		var coastBandQuantized = Mathf.RoundToInt(_tuning.CoastBand * 10000f);
		var mountainThresholdQuantized = Mathf.RoundToInt(_tuning.MountainThreshold * 10000f);
		var riverSourceElevationQuantized = Mathf.RoundToInt(_tuning.RiverSourceElevationThreshold * 10000f);
		var riverSourceMoistureQuantized = Mathf.RoundToInt(_tuning.RiverSourceMoistureThreshold * 10000f);
		var riverSourceChanceQuantized = Mathf.RoundToInt(_tuning.RiverSourceChance * 1_000_000f);

		return $"ver:{WorldGenerationAlgorithmVersion}|{MapWidth}x{MapHeight}|seed:{Seed}|plates:{PlateCount}|wind:{WindCellCount}|sea:{seaLevelQuantized}|heat:{heatFactorQuantized}|moIter:{MoistureIterations}|erosion:{ErosionIterations}|rivers:{(EnableRivers ? 1 : 0)}|riverDensity:{riverDensityQuantized}|oceanic:{oceanicRatioQuantized}|continentBias:{continentBiasQuantized}|interiorRelief:{interiorReliefQuantized}|orogeny:{orogenyStrengthQuantized}|subductionArc:{subductionArcRatioQuantized}|age:{continentalAgeQuantized}|morph:{(int)_terrainMorphology}|continentCount:{_continentCount}|tuning:{_tuning.Name}|deepOcean:{deepOceanFactorQuantized}|coast:{coastBandQuantized}|mountain:{mountainThresholdQuantized}|riverSrcEl:{riverSourceElevationQuantized}|riverSrcMo:{riverSourceMoistureQuantized}|riverSrcChance:{riverSourceChanceQuantized}|compare:{(_compareMode ? 1 : 0)}";
	}

	private bool TryGetWorldGenerationCache(string cacheKey, out GeneratedWorldData primaryWorld, out GeneratedWorldData? compareWorld)
	{
		if (_worldGenerationCache.TryGetValue(cacheKey, out var entry))
		{
			entry.LastAccessTick = ++_worldCacheAccessCounter;
			primaryWorld = entry.PrimaryWorld;
			compareWorld = entry.CompareWorld;
			return true;
		}

		if (TryLoadWorldGenerationCacheFromDisk(cacheKey, out primaryWorld, out compareWorld))
		{
			StoreWorldGenerationCache(cacheKey, primaryWorld, compareWorld, persistToDisk: false);
			return true;
		}

		primaryWorld = null!;
		compareWorld = null;
		return false;
	}

	private void StoreWorldGenerationCache(string cacheKey, GeneratedWorldData primaryWorld, GeneratedWorldData? compareWorld, bool persistToDisk = true)
	{
		var estimatedCells = (long)primaryWorld.Stats.Width * primaryWorld.Stats.Height;
		if (compareWorld != null)
		{
			estimatedCells += (long)compareWorld.Stats.Width * compareWorld.Stats.Height;
		}

		_worldGenerationCache[cacheKey] = new WorldGenerationCacheEntry
		{
			Key = cacheKey,
			PrimaryWorld = primaryWorld,
			CompareWorld = compareWorld,
			EstimatedCells = estimatedCells,
			LastAccessTick = ++_worldCacheAccessCounter
		};

		while (_worldGenerationCache.Count > WorldGenerationCacheCapacity || GetWorldCacheTotalCells() > WorldGenerationCacheMaxCells)
		{
			var oldestKey = string.Empty;
			var oldestTick = long.MaxValue;

			foreach (var pair in _worldGenerationCache)
			{
				if (pair.Value.LastAccessTick >= oldestTick)
				{
					continue;
				}

				oldestTick = pair.Value.LastAccessTick;
				oldestKey = pair.Key;
			}

			if (string.IsNullOrEmpty(oldestKey))
			{
				break;
			}

			_worldGenerationCache.Remove(oldestKey);
		}

		if (persistToDisk)
		{
			SaveWorldGenerationCacheToDisk(cacheKey, primaryWorld, compareWorld);
		}

		RefreshCacheStatsLabel();
	}

	private string BuildAutoCacheDirectoryPath()
	{
		return ProjectSettings.GlobalizePath($"user://{CacheDirectoryName}/{CacheDataDirectoryName}");
	}

	private string BuildArchiveDirectoryPath()
	{
		return ProjectSettings.GlobalizePath($"user://{CacheDirectoryName}/{ArchiveDataDirectoryName}");
	}

	private string BuildCacheFilePath(string cacheKey)
	{
		var bytes = CryptoSha256.HashData(Encoding.UTF8.GetBytes(cacheKey));
		var fileHash = Convert.ToHexString(bytes).ToLowerInvariant();
		return IOPath.Combine(BuildAutoCacheDirectoryPath(), fileHash + CacheFileExtension);
	}

	private void SaveWorldGenerationCacheToDisk(string cacheKey, GeneratedWorldData primaryWorld, GeneratedWorldData? compareWorld)
	{
		try
		{
			var cacheDir = BuildAutoCacheDirectoryPath();
			IODirectory.CreateDirectory(cacheDir);

			var payload = BuildPersistedWorldCacheEntry(cacheKey, primaryWorld, compareWorld);
			var jsonText = Json.Stringify(ConvertPersistedCacheToGodotDictionary(payload));
			IOFile.WriteAllText(BuildCacheFilePath(cacheKey), jsonText, Encoding.UTF8);
		}
		catch
		{
		}
	}

	private bool TryReadPersistedCacheEntryFromFile(string filePath, out PersistedWorldCacheEntry payload)
	{
		if (!IOFile.Exists(filePath))
		{
			payload = null!;
			return false;
		}

		try
		{
			var text = IOFile.ReadAllText(filePath, Encoding.UTF8);
			var parsed = Json.ParseString(text);
			if (parsed.VariantType != Variant.Type.Dictionary)
			{
				payload = null!;
				return false;
			}

			var converted = ConvertGodotDictionaryToPersistedCache((Godot.Collections.Dictionary)parsed);
			if (converted == null || string.IsNullOrEmpty(converted.CacheKey))
			{
				payload = null!;
				return false;
			}

			payload = converted;
			return true;
		}
		catch
		{
			payload = null!;
			return false;
		}
	}

	private bool TryLoadWorldGenerationCacheFromDisk(string cacheKey, out GeneratedWorldData primaryWorld, out GeneratedWorldData? compareWorld)
	{
		var filePath = BuildCacheFilePath(cacheKey);

		if (!TryReadPersistedCacheEntryFromFile(filePath, out var payload) || payload.CacheKey != cacheKey)
		{
			primaryWorld = null!;
			compareWorld = null;
			return false;
		}

		primaryWorld = RestoreGeneratedWorldData(payload.Primary);
		compareWorld = payload.CompareMode && payload.Compare != null
			? RestoreGeneratedWorldData(payload.Compare)
			: null;

		return true;
	}

	private PersistedWorldCacheEntry BuildPersistedWorldCacheEntry(string cacheKey, GeneratedWorldData primaryWorld, GeneratedWorldData? compareWorld)
	{
		return new PersistedWorldCacheEntry
		{
			Version = 1,
			CacheKey = cacheKey,
			Seed = Seed,
			MapWidth = MapWidth,
			MapHeight = MapHeight,
			CompareMode = compareWorld != null,
			Primary = SerializeGeneratedWorld(primaryWorld),
			Compare = compareWorld != null ? SerializeGeneratedWorld(compareWorld) : null
		};
	}


	private void OnSaveArchivePressed()
	{
		if (_isGenerating)
		{
			_infoLabel.Text = "正在生成中，稍后再保存存档。";
			return;
		}

		if (_primaryWorld == null)
		{
			_infoLabel.Text = "当前没有可保存的地图。";
			return;
		}

		try
		{
			var archiveDir = BuildArchiveDirectoryPath();
			IODirectory.CreateDirectory(archiveDir);

			var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
			var fileName = $"{ArchiveFilePrefix}{timestamp}_{Seed}_{MapWidth}x{MapHeight}{ArchiveFileExtension}";
			var archivePath = IOPath.Combine(archiveDir, fileName);

			var cacheKey = BuildWorldGenerationCacheKey();
			var payload = BuildPersistedWorldCacheEntry(cacheKey, _primaryWorld, _compareWorld);
			var jsonText = Json.Stringify(ConvertPersistedCacheToGodotDictionary(payload));
			IOFile.WriteAllText(archivePath, jsonText, Encoding.UTF8);

			_lastArchivePath = archivePath;
			SaveAdvancedSettings();
			SetupArchiveOptions();
			RefreshCacheStatsLabel();
			_infoLabel.Text = $"存档已保存：{fileName}";
		}
		catch
		{
			_infoLabel.Text = "存档保存失败（权限或磁盘异常）。";
		}
	}

	private void OnClearCachePressed()
	{
		if (_isGenerating)
		{
			_infoLabel.Text = "正在生成中，稍后再清理缓存。";
			return;
		}

		var memoryCount = _worldGenerationCache.Count;
		_worldGenerationCache.Clear();

		try
		{
			var cacheDir = BuildAutoCacheDirectoryPath();
			if (IODirectory.Exists(cacheDir))
			{
				IODirectory.Delete(cacheDir, true);
			}
		}
		catch
		{
			_infoLabel.Text = "自动缓存内存已清理，磁盘清理失败（权限或文件占用）。";
			RefreshCacheStatsLabel();
			return;
		}

		RefreshCacheStatsLabel();
		_infoLabel.Text = $"自动缓存已清理（内存 {memoryCount} 条，磁盘目录已删除）。";
	}

	private void LoadArchiveByPath(string archivePath)
	{
		if (_isGenerating)
		{
			_infoLabel.Text = "正在生成中，稍后再读取存档。";
			return;
		}

		if (!TryReadPersistedCacheEntryFromFile(archivePath, out var payload))
		{
			_infoLabel.Text = "读取存档失败（文件损坏或格式不兼容）。";
			return;
		}

		try
		{
			var primary = RestoreGeneratedWorldData(payload.Primary);
			GeneratedWorldData? compare = null;
			if (payload.CompareMode && payload.Compare != null)
			{
				compare = RestoreGeneratedWorldData(payload.Compare);
			}

			_primaryWorld = primary;
			_compareWorld = compare;
			_compareMode = compare != null;
			_compareToggle.ButtonPressed = _compareMode;
			_compareToggle.Visible = _compareMode;
			_compareToggle.Disabled = !_compareMode;

			Seed = payload.Seed;
			_seedSpin.Value = Seed;
			MapWidth = payload.MapWidth;
			MapHeight = payload.MapHeight;
			var mapSizeIndex = FindMapSizePresetIndex(MapWidth, MapHeight);
			if (mapSizeIndex >= 0)
			{
				_suppressMapSizeSelectionHandler = true;
				_mapSizeOption.Select(mapSizeIndex);
				_suppressMapSizeSelectionHandler = false;
				_lastConfirmedMapSizeIndex = mapSizeIndex;
			}

			StoreWorldGenerationCache(payload.CacheKey, primary, compare, persistToDisk: false);
			_lastArchivePath = archivePath;
			SaveAdvancedSettings();
			ShowInfoPointHint();
			RedrawCurrentLayer();
			RefreshCacheStatsLabel();
			_infoLabel.Text = $"已读取存档：{IOPath.GetFileName(archivePath)}";
		}
		catch
		{
			_infoLabel.Text = "读取存档失败（数据恢复异常）。";
		}
	}

	private void RefreshCacheStatsLabel()
	{
		if (_cacheStatsLabel == null)
		{
			return;
		}

		var memoryEntries = _worldGenerationCache.Count;
		long memoryBytes = 0;
		foreach (var pair in _worldGenerationCache)
		{
			memoryBytes += pair.Value.EstimatedCells * ApproxBytesPerCachedCell;
		}

		GetDirectoryStats(BuildAutoCacheDirectoryPath(), "*" + CacheFileExtension, out var autoCacheFiles, out var autoCacheBytes);
		GetDirectoryStats(BuildArchiveDirectoryPath(), "*" + ArchiveFileExtension, out var archiveFiles, out var archiveBytes);

		_cacheStatsLabel.Text = $"自动缓存 | 内存:{memoryEntries} 条（约 {FormatByteSize(memoryBytes)}）| 磁盘:{autoCacheFiles} 文件（{FormatByteSize(autoCacheBytes)}）\n存档:{archiveFiles} 份（{FormatByteSize(archiveBytes)}）";
	}

	private static void GetDirectoryStats(string directoryPath, string searchPattern, out int fileCount, out long totalBytes)
	{
		fileCount = 0;
		totalBytes = 0;

		if (!IODirectory.Exists(directoryPath))
		{
			return;
		}

		var files = IODirectory.GetFiles(directoryPath, searchPattern, System.IO.SearchOption.TopDirectoryOnly);
		fileCount = files.Length;
		for (var i = 0; i < files.Length; i++)
		{
			var info = new IOFileInfo(files[i]);
			totalBytes += info.Length;
		}
	}

	private static string FormatByteSize(long bytes)
	{
		if (bytes < 1024)
		{
			return $"{bytes} B";
		}

		var kb = bytes / 1024.0;
		if (kb < 1024.0)
		{
			return $"{kb:0.0} KB";
		}

		var mb = kb / 1024.0;
		if (mb < 1024.0)
		{
			return $"{mb:0.0} MB";
		}

		var gb = mb / 1024.0;
		return $"{gb:0.00} GB";
	}

	private long GetWorldCacheTotalCells()
	{
		long total = 0;
		foreach (var entry in _worldGenerationCache.Values)
		{
			total += entry.EstimatedCells;
		}

		return total;
	}

}
