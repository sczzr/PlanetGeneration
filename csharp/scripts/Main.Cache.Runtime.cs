using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Persistence;
using Godot;
using PlanetGeneration.WorldGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using PlanetGeneration.Application;
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
		return WorldGenerationCacheKey.BuildSession(BuildGenerationOptions(_tuning, Seed),
			_compareMode ? BuildGenerationOptions(GetAlternateTuning(_tuning), Seed + 1) : null);
	}

	private bool TryGetWorldGenerationCache(string cacheKey, out GeneratedWorldData primaryWorld, out GeneratedWorldData? compareWorld)
	{
		WorldSnapshot? primary = null;
		WorldSnapshot? comparison = null;
		if (_worldGenerationCache.TryGetValue(cacheKey, out var entry))
		{
			entry.LastAccessTick = ++_worldCacheAccessCounter;
			primary = entry.PrimarySnapshot;
			comparison = entry.CompareSnapshot;
		}
		else if (TryLoadWorldGenerationCacheFromDisk(cacheKey, out var archive))
		{
			primary = archive.Primary;
			comparison = archive.Comparison;
		}
		if (primary == null)
		{
			primaryWorld = null!;
			compareWorld = null;
			return false;
		}
		// 活动会话拿到独立投影。时间轴更新不会改写缓存中的原始快照。
		primaryWorld = SnapshotWorldProjection.Create(primary);
		compareWorld = comparison == null ? null : SnapshotWorldProjection.Create(comparison);
		StoreWorldGenerationCache(cacheKey, primaryWorld, compareWorld, persistToDisk: false);
		return true;
	}

	private void StoreWorldGenerationCache(string cacheKey, GeneratedWorldData primaryWorld,
		GeneratedWorldData? compareWorld, bool persistToDisk = true)
	{
		var primary = primaryWorld.Snapshot;
		var comparison = compareWorld?.Snapshot;
		// 旧手动档案可以查看/重存，但不能冒充同参数的 Core 生成缓存。
		if (primary == null || (compareWorld != null && comparison == null)) return;
		if (cacheKey != WorldGenerationCacheKey.BuildSession(primary.Options, comparison?.Options))
			throw new InvalidOperationException("缓存键与实际快照输入不一致。");
		_worldGenerationCache[cacheKey] = new WorldGenerationCacheEntry
		{
			Key = cacheKey, PrimarySnapshot = primary, CompareSnapshot = comparison,
			// 保留原有像素预算，限制大尺寸世界的缓存容量；活动投影不存入缓存。
			EstimatedCells = (long)primaryWorld.Stats.Width * primaryWorld.Stats.Height
				+ (compareWorld == null ? 0 : (long)compareWorld.Stats.Width * compareWorld.Stats.Height),
			KnownBufferBytes = SnapshotBufferSize.Measure(primary) + (comparison == null ? 0 : SnapshotBufferSize.Measure(comparison)),
			LastAccessTick = ++_worldCacheAccessCounter
		};
		while (_worldGenerationCache.Count > WorldGenerationCacheCapacity || GetWorldCacheTotalCells() > WorldGenerationCacheMaxCells)
		{
			var oldestKey = string.Empty;
			var oldestTick = long.MaxValue;
			foreach (var pair in _worldGenerationCache)
			{
				if (pair.Value.LastAccessTick >= oldestTick) continue;
				oldestTick = pair.Value.LastAccessTick;
				oldestKey = pair.Key;
			}
			if (string.IsNullOrEmpty(oldestKey)) break;
			_worldGenerationCache.Remove(oldestKey);
		}
		if (persistToDisk)
			_ = Task.Run(() => SaveWorldGenerationCacheToDisk(cacheKey, primary, comparison));
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

	private void SaveWorldGenerationCacheToDisk(string cacheKey, WorldSnapshot primary, WorldSnapshot? comparison)
	{
		try
		{
			WorldSnapshotArchive.Save(BuildCacheFilePath(cacheKey), primary, comparison);
			PruneWorldCacheDiskFiles(BuildAutoCacheDirectoryPath());
		}
		catch (Exception ex)
		{
			GD.PushWarning($"[WorldCache] 快照缓存保存失败: {ex.Message}");
		}
	}

	private void PruneWorldCacheDiskFiles(string cacheDir)
	{
		try
		{
			var files = IODirectory.GetFiles(cacheDir, "*" + CacheFileExtension, System.IO.SearchOption.TopDirectoryOnly);
			if (files.Length <= WorldCacheDiskKeepFiles)
			{
				return;
			}

			Array.Sort(files, (left, right) => IOFile.GetLastWriteTimeUtc(left).CompareTo(IOFile.GetLastWriteTimeUtc(right)));
			for (var i = 0; i < files.Length - WorldCacheDiskKeepFiles; i++)
			{
				try
				{
					IOFile.Delete(files[i]);
				}
				catch
				{
					// 文件被占用等瞬时问题跳过即可，下一次写入会继续修剪。
				}
			}
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

	/// <summary>
	/// 只读文件头部来构建档案下拉标签。档案正文可达上百 MB，
	/// 为一个标签做整档 JSON 反序列化会在主线程上冻结数秒。
	/// 注意 Json.Stringify 按键名字母序输出，"seed" 排在体积巨大的 "primary"
	/// 之后（深达数 MB），头部里读不到；但 "cache_key" 排最前且自带
	/// 旧版 ver:N|宽x高|seed:S 或 Core 的 ext:/seed: 摘要，"compare_mode" 也在头部，足够拼出标签。
	/// </summary>
	private bool TryReadPersistedCacheHeaderFromFile(
		string filePath,
		out int seed,
		out int mapWidth,
		out int mapHeight,
		out bool compareMode)
	{
		seed = 0;
		mapWidth = 0;
		mapHeight = 0;
		compareMode = false;

		if (!IOFile.Exists(filePath))
		{
			return false;
		}

		try
		{
			using var stream = IOFile.OpenRead(filePath);
			var headerBytes = new byte[ArchiveHeaderProbeBytes];
			var read = stream.Read(headerBytes, 0, headerBytes.Length);
			var header = Encoding.UTF8.GetString(headerBytes, 0, read);

			if (!WorldArchiveHeader.TryParse(header, out var summary))
			{
				return false;
			}
			mapWidth = summary.Width;
			mapHeight = summary.Height;
			seed = summary.Seed;
			compareMode = summary.CompareMode;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private bool TryLoadWorldGenerationCacheFromDisk(string cacheKey, out SnapshotArchiveContent content)
	{
		content = null!;
		var filePath = BuildCacheFilePath(cacheKey);
		if (!IOFile.Exists(filePath)) return false;
		try
		{
			if (!WorldSnapshotArchive.HasSnapshotHeader(filePath)) return false;
			var restored = WorldSnapshotArchive.Load(filePath);
			if (restored.CacheKey != cacheKey) return false;
			content = restored;
			return true;
		}
		catch (Exception ex)
		{
			GD.PushWarning($"[WorldCache] 忽略损坏或不兼容缓存 '{filePath}': {ex.Message}");
			return false;
		}
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
		if (_isSavingArchive)
		{
			_infoLabel.Text = "正在保存存档，请稍候。";
			return;
		}

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

		_isSavingArchive = true;
		_infoLabel.Text = "正在保存存档…";
		_ = SaveArchiveAsync();
	}

	/// <summary>
	/// 存档体积可达上百 MB（JSON 序列化 + 写盘），放在工作线程执行，
	/// 完成后借助 Godot 的主线程同步上下文回到 UI 更新界面。
	/// </summary>
	private async Task SaveArchiveAsync()
	{
		try
		{
			var archiveDir = BuildArchiveDirectoryPath();
			IODirectory.CreateDirectory(archiveDir);

			var snapshot = _primarySnapshot;
			var comparisonSnapshot = _compareSnapshot;
			var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
			var fileName = $"{ArchiveFilePrefix}{timestamp}_{snapshot?.Options.Seed ?? Seed}_{snapshot?.Extent.Width ?? MapWidth}x{snapshot?.Extent.Height ?? MapHeight}{ArchiveFileExtension}";
			var archivePath = IOPath.Combine(archiveDir, fileName);

			var cacheKey = BuildWorldGenerationCacheKey();
			var primaryWorld = _primaryWorld!;
			var compareWorld = _compareWorld;

			await Task.Run(() =>
			{
				if (snapshot != null)
				{
					WorldSnapshotArchive.Save(archivePath, snapshot, comparisonSnapshot);
				}
				else
				{
					// 旧档案的兼容重存，不伪造缺失的权威快照。
					var payload = BuildPersistedWorldCacheEntry(cacheKey, primaryWorld, compareWorld);
					var jsonText = Json.Stringify(ConvertPersistedCacheToGodotDictionary(payload));
					IOFile.WriteAllText(archivePath, jsonText, Encoding.UTF8);
				}
			});

			_lastArchivePath = archivePath;
			SaveAdvancedSettings();
			SetupArchiveOptions();
			RefreshCacheStatsLabel();
			_infoLabel.Text = $"存档已保存：{fileName}";
		}
		catch (Exception ex)
		{
			GD.PushError($"[WorldArchive] 保存失败: {ex}");
			_infoLabel.Text = "存档保存失败（权限或磁盘异常）。";
		}
		finally
		{
			_isSavingArchive = false;
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

		try
		{
			GeneratedWorldData primary;
			GeneratedWorldData? compare;
			string cacheKey;
			int seed, width, height;
			if (WorldSnapshotArchive.HasSnapshotHeader(archivePath))
			{
				var archive = WorldSnapshotArchive.Load(archivePath);
				primary = SnapshotWorldProjection.Create(archive.Primary);
				compare = archive.Comparison == null ? null : SnapshotWorldProjection.Create(archive.Comparison);
				cacheKey = archive.CacheKey;
				seed = archive.Primary.Options.Seed;
				width = primary.Stats.Width;
				height = primary.Stats.Height;
				ApplySnapshotOptions(primary);
			}
			else
			{
				if (!TryReadPersistedCacheEntryFromFile(archivePath, out var payload))
					throw new InvalidOperationException("旧档案内容损坏或格式不兼容。");
				primary = RestoreGeneratedWorldData(payload.Primary);
				compare = payload.CompareMode && payload.Compare != null ? RestoreGeneratedWorldData(payload.Compare) : null;
				cacheKey = payload.CacheKey;
				seed = payload.Seed;
				width = payload.MapWidth;
				height = payload.MapHeight;
			}

			_primaryWorld = primary;
			_compareWorld = compare;
			_compareMode = compare != null;
			_compareToggle.SetPressedNoSignal(_compareMode);
			_compareToggle.Visible = _compareMode;
			_compareToggle.Disabled = !_compareMode;

			// 快照引用直接来自活动世界；切换到旧档案时自然为 null，不留下上一世界。
			_mapCanvas?.DetachSnapshot();

			Seed = seed;
			_seedSpin.SetValueNoSignal(Seed);
			MapWidth = width;
			MapHeight = height;
			var mapSizeIndex = FindMapSizePresetIndex(MapWidth, MapHeight);
			if (mapSizeIndex >= 0)
			{
				_suppressMapSizeSelectionHandler = true;
				_mapSizeOption.Select(mapSizeIndex);
				_suppressMapSizeSelectionHandler = false;
				_lastConfirmedMapSizeIndex = mapSizeIndex;
			}

			StoreWorldGenerationCache(cacheKey, primary, compare, persistToDisk: false);
			_lastArchivePath = archivePath;
			SaveAdvancedSettings();
			ShowInfoPointHint();
			UpdateLabels();
			UpdateCellCountDisplay();
			RedrawCurrentLayer();
			RefreshCacheStatsLabel();
			_infoLabel.Text = $"已读取存档：{IOPath.GetFileName(archivePath)}";
		}
		catch (Exception ex)
		{
			GD.PushError($"[WorldArchive] 恢复失败: {ex}");
			_infoLabel.Text = "读取存档失败（数据恢复异常）。";
		}
	}

	/// <summary>
	/// 主菜单"读取档案"：优先读上次使用的存档，否则取目录里最新的存档；
	/// 一个可用存档都没有（或读取失败）时回退到新建世界流程，
	/// 避免玩家停留在既没有世界也没有控制台的空场景里。
	/// </summary>
	private void OnLoadFromMenuRequested()
	{
		var archivePath = ResolveLatestArchivePath();
		if (archivePath != null)
		{
			LoadArchiveByPath(archivePath);
		}

		if (_primaryWorld == null)
		{
			OnNewWorldRequested();
			return;
		}

		SetConsolePanelVisible(true);
		SetInGameHudVisible(true);
	}

	private string? ResolveLatestArchivePath()
	{
		if (!string.IsNullOrEmpty(_lastArchivePath) && IOFile.Exists(_lastArchivePath))
		{
			return _lastArchivePath;
		}

		var archiveDir = BuildArchiveDirectoryPath();
		if (!IODirectory.Exists(archiveDir))
		{
			return null;
		}

		var files = IODirectory.GetFiles(archiveDir, "*" + ArchiveFileExtension, System.IO.SearchOption.TopDirectoryOnly);
		if (files.Length == 0)
		{
			return null;
		}

		Array.Sort(files, (left, right) => IOFile.GetLastWriteTimeUtc(right).CompareTo(IOFile.GetLastWriteTimeUtc(left)));
		return files[0];
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
			memoryBytes += pair.Value.KnownBufferBytes;
		}

		GetDirectoryStats(BuildAutoCacheDirectoryPath(), "*" + CacheFileExtension, out var autoCacheFiles, out var autoCacheBytes);
		GetDirectoryStats(BuildArchiveDirectoryPath(), "*" + ArchiveFileExtension, out var archiveFiles, out var archiveBytes);

		_cacheStatsLabel.Text = $"自动缓存 | 内存:{memoryEntries} 条（基础数据缓冲 {FormatByteSize(memoryBytes)}，不含制图/对象开销）| 磁盘:{autoCacheFiles} 文件（{FormatByteSize(autoCacheBytes)}）\n存档:{archiveFiles} 份（{FormatByteSize(archiveBytes)}）";
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
