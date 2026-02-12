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
	private void SetupMapSizeOptions()
	{
		_mapSizeOption.Clear();

		for (var index = 0; index < MapSizePresets.Length; index++)
		{
			var size = MapSizePresets[index];
			_mapSizeOption.AddItem(BuildInfoPointOptionText(index, size), index);
		}

		const int selectedIndex = 0;

		ApplyMapSizePreset(selectedIndex);
		_mapSizeOption.Select(selectedIndex);
		_lastConfirmedMapSizeIndex = selectedIndex;
		ShowInfoPointHint();

		_mapSizeOption.ItemSelected += id =>
		{
			if (_suppressMapSizeSelectionHandler)
			{
				return;
			}

			var selectedIndex = (int)id;
			if (RequiresMapInfoConfirmation(selectedIndex))
			{
				_pendingMapSizeIndex = selectedIndex;
				_mapInfoWarningDialog.DialogText = BuildMapInfoWarningText(selectedIndex);
				_mapInfoWarningDialog.PopupCentered();

				_suppressMapSizeSelectionHandler = true;
				_mapSizeOption.Select(_lastConfirmedMapSizeIndex);
				_suppressMapSizeSelectionHandler = false;
				return;
			}

			CommitMapSizeSelection(selectedIndex);
		};
	}

	private bool RequiresMapInfoConfirmation(int presetIndex)
	{
		if (_skipMapInfoWarningForSession)
		{
			return false;
		}

		if (presetIndex < 0 || presetIndex >= MapSizePresets.Length)
		{
			return false;
		}

		if (presetIndex == _lastConfirmedMapSizeIndex)
		{
			return false;
		}

		var profile = BuildInfoPointOptionText(presetIndex, MapSizePresets[presetIndex]);
		return profile is "高清" or "极致";
	}

	private static string BuildMapInfoWarningText(int presetIndex)
	{
		var level = presetIndex >= 0 && presetIndex < MapSizePresets.Length
			? BuildInfoPointOptionText(presetIndex, MapSizePresets[presetIndex])
			: "高等级";
		return $"你选择了“{level}”地图信息级别，生成耗时会明显增加。是否继续？";
	}

	private void ConfirmMapInfoSelection()
	{
		if (_pendingMapSizeIndex < 0)
		{
			return;
		}

		if (_mapInfoWarningSkipCheck.ButtonPressed)
		{
			_skipMapInfoWarningForSession = true;
		}
		_mapInfoWarningSkipCheck.ButtonPressed = false;

		CommitMapSizeSelection(_pendingMapSizeIndex);
		_pendingMapSizeIndex = -1;
	}

	private void CommitMapSizeSelection(int selectedIndex)
	{
		ApplyMapSizePreset(selectedIndex);
		_suppressMapSizeSelectionHandler = true;
		_mapSizeOption.Select(selectedIndex);
		_suppressMapSizeSelectionHandler = false;
		_lastConfirmedMapSizeIndex = selectedIndex;
		ShowInfoPointHint();
		GenerateWorld();
	}

	private void SetupTerrainPresetOptions()
	{
		_terrainPresetOption.Clear();

		for (var index = 0; index < TerrainPresets.Length; index++)
		{
			_terrainPresetOption.AddItem(TerrainPresets[index].Name, index);
		}

		const int defaultIndex = 0;
		_terrainPresetOption.Select(defaultIndex);
		ApplyTerrainPreset(defaultIndex, regenerate: false);

		_terrainPresetOption.ItemSelected += id =>
		{
			ApplyTerrainPreset((int)id, regenerate: true);
		};
	}

	private void SetupMountainPresetOptions()
	{
		_mountainPresetOption.Clear();
		for (var index = 0; index < MountainPresets.Length; index++)
		{
			var preset = MountainPresets[index];
			_mountainPresetOption.AddItem(preset.Name, (int)preset.Id);
		}

		_mountainPresetOption.AddItem("自定义", (int)MountainPresetId.Custom);

		if (_mountainPresetId != MountainPresetId.Custom && !TryGetMountainPreset(_mountainPresetId, out _))
		{
			_mountainPresetId = ResolveMountainPresetIdFromCurrentValues();
		}

		if (_mountainPresetId == MountainPresetId.Custom)
		{
			SyncMountainPresetFromCurrentValues();
		}
		else
		{
			ApplyMountainPreset(_mountainPresetId, regenerate: false, persist: false);
		}

		_mountainPresetOption.ItemSelected += id =>
		{
			if (_suppressMountainPresetSelectionHandler)
			{
				return;
			}

			var selectedId = _mountainPresetOption.GetItemId((int)id);
			if (!Enum.IsDefined(typeof(MountainPresetId), selectedId))
			{
				return;
			}

			var presetId = (MountainPresetId)selectedId;
			if (presetId == MountainPresetId.Custom)
			{
				_mountainPresetId = MountainPresetId.Custom;
				SaveAdvancedSettings();
				return;
			}

			ApplyMountainPreset(presetId, regenerate: true, persist: true);
		};
	}

	private void ApplyMountainPreset(MountainPresetId presetId, bool regenerate, bool persist)
	{
		if (!TryGetMountainPreset(presetId, out var preset))
		{
			_mountainPresetId = MountainPresetId.Custom;
			SelectMountainPresetOption(_mountainPresetId);
			return;
		}

		_mountainPresetId = presetId;
		_interiorRelief = Mathf.Clamp(preset.InteriorRelief, 0.5f, 2.0f);
		_orogenyStrength = Mathf.Clamp(preset.OrogenyStrength, 0.5f, 2.5f);
		_subductionArcRatio = Mathf.Clamp(preset.SubductionArcRatio, 0.2f, 1.0f);
		_continentalAge = Mathf.Clamp(preset.ContinentalAge, 0, 100);

		_interiorReliefSlider.SetValueNoSignal(_interiorRelief);
		_orogenyStrengthSlider.SetValueNoSignal(_orogenyStrength);
		_subductionArcRatioSlider.SetValueNoSignal(_subductionArcRatio);
		_continentalAgeSlider.SetValueNoSignal(_continentalAge);
		SelectMountainPresetOption(_mountainPresetId);

		UpdateLabels();
		if (persist)
		{
			SaveAdvancedSettings();
		}

		if (regenerate)
		{
			GenerateWorld();
		}
	}

	private bool TryGetMountainPreset(MountainPresetId presetId, out MountainPreset preset)
	{
		for (var index = 0; index < MountainPresets.Length; index++)
		{
			if (MountainPresets[index].Id == presetId)
			{
				preset = MountainPresets[index];
				return true;
			}
		}

		preset = null!;
		return false;
	}

	private void SelectMountainPresetOption(MountainPresetId presetId)
	{
		for (var index = 0; index < _mountainPresetOption.ItemCount; index++)
		{
			if (_mountainPresetOption.GetItemId(index) != (int)presetId)
			{
				continue;
			}

			_suppressMountainPresetSelectionHandler = true;
			_mountainPresetOption.Select(index);
			_suppressMountainPresetSelectionHandler = false;
			return;
		}

		for (var index = 0; index < _mountainPresetOption.ItemCount; index++)
		{
			if (_mountainPresetOption.GetItemId(index) != (int)MountainPresetId.Custom)
			{
				continue;
			}

			_suppressMountainPresetSelectionHandler = true;
			_mountainPresetOption.Select(index);
			_suppressMountainPresetSelectionHandler = false;
			return;
		}
	}

	private MountainPresetId ResolveMountainPresetIdFromCurrentValues()
	{
		for (var index = 0; index < MountainPresets.Length; index++)
		{
			var preset = MountainPresets[index];
			if (!AreNearlyEqual(_interiorRelief, preset.InteriorRelief))
			{
				continue;
			}

			if (!AreNearlyEqual(_orogenyStrength, preset.OrogenyStrength))
			{
				continue;
			}

			if (!AreNearlyEqual(_subductionArcRatio, preset.SubductionArcRatio))
			{
				continue;
			}

			if (_continentalAge != preset.ContinentalAge)
			{
				continue;
			}

			return preset.Id;
		}

		return MountainPresetId.Custom;
	}

	private void SyncMountainPresetFromCurrentValues()
	{
		var resolvedPreset = ResolveMountainPresetIdFromCurrentValues();
		if (_mountainPresetId == resolvedPreset)
		{
			return;
		}

		_mountainPresetId = resolvedPreset;
		SelectMountainPresetOption(_mountainPresetId);
	}

	private static bool AreNearlyEqual(float left, float right)
	{
		return Mathf.Abs(left - right) <= 0.005f;
	}

	private void SetupElevationStyleOptions()
	{
		_elevationStyleOption.Clear();
		_elevationStyleOption.AddItem("写实", (int)ElevationStyle.Realistic);
		_elevationStyleOption.AddItem("地形图", (int)ElevationStyle.Topographic);
		SelectElevationStyleOption(_elevationStyle);

		_elevationStyleOption.ItemSelected += index =>
		{
			var styleId = _elevationStyleOption.GetItemId((int)index);
			_elevationStyle = Enum.IsDefined(typeof(ElevationStyle), styleId)
				? (ElevationStyle)styleId
				: ElevationStyle.Realistic;
			SaveAdvancedSettings();
			RedrawCurrentLayer();
		};
	}

	private void SetupArchiveOptions()
	{
		_archivePathByOptionId.Clear();
		_archiveOption.Clear();
		_archiveOption.AddItem("(无) 仅自动缓存", 0);

		PopulateArchiveOptionItems();

		if (!_archiveOptionSignalBound)
		{
			_archiveOptionSignalBound = true;
			_archiveOption.ItemSelected += id =>
			{
				if (_suppressArchiveSelectionHandler)
				{
					return;
				}

				if (!_archivePathByOptionId.TryGetValue((int)id, out var archivePath) || string.IsNullOrEmpty(archivePath))
				{
					_lastArchivePath = string.Empty;
					SaveAdvancedSettings();
					return;
				}

				LoadArchiveByPath(archivePath);
			};
		}
	}

	private void PopulateArchiveOptionItems()
	{
		var archiveDir = BuildArchiveDirectoryPath();
		if (!IODirectory.Exists(archiveDir))
		{
			SelectArchiveOptionId(0);
			return;
		}

		var files = IODirectory.GetFiles(archiveDir, "*" + ArchiveFileExtension, System.IO.SearchOption.TopDirectoryOnly);
		Array.Sort(files, (left, right) => IOFile.GetLastWriteTimeUtc(right).CompareTo(IOFile.GetLastWriteTimeUtc(left)));

		var nextOptionId = 1;
		var selectedId = 0;
		for (var i = 0; i < files.Length; i++)
		{
			var filePath = files[i];
			var label = BuildArchiveOptionLabel(filePath);
			_archiveOption.AddItem(label, nextOptionId);
			_archivePathByOptionId[nextOptionId] = filePath;

			if (!string.IsNullOrEmpty(_lastArchivePath) && string.Equals(filePath, _lastArchivePath, StringComparison.OrdinalIgnoreCase))
			{
				selectedId = nextOptionId;
			}

			nextOptionId++;
		}

		if (selectedId == 0 && !string.IsNullOrEmpty(_lastArchivePath))
		{
			_lastArchivePath = string.Empty;
			SaveAdvancedSettings();
		}

		SelectArchiveOptionId(selectedId);
	}

	private void SelectArchiveOptionId(int optionId)
	{
		for (var index = 0; index < _archiveOption.ItemCount; index++)
		{
			if (_archiveOption.GetItemId(index) != optionId)
			{
				continue;
			}

			_suppressArchiveSelectionHandler = true;
			_archiveOption.Select(index);
			_suppressArchiveSelectionHandler = false;
			return;
		}

		_suppressArchiveSelectionHandler = true;
		_archiveOption.Select(0);
		_suppressArchiveSelectionHandler = false;
	}

	private string BuildArchiveOptionLabel(string archivePath)
	{
		var fileName = IOPath.GetFileName(archivePath);
		var timestampText = IOFile.GetLastWriteTime(archivePath).ToString("MM-dd HH:mm");
		if (!TryReadPersistedCacheEntryFromFile(archivePath, out var payload))
		{
			return $"{timestampText} | {fileName}";
		}

		var modeText = payload.CompareMode && payload.Compare != null ? "对比" : "单图";
		return $"{timestampText} | seed:{payload.Seed} | {payload.MapWidth}x{payload.MapHeight} | {modeText}";
	}

	private void SelectElevationStyleOption(ElevationStyle style)
	{
		for (var index = 0; index < _elevationStyleOption.ItemCount; index++)
		{
			if (_elevationStyleOption.GetItemId(index) != (int)style)
			{
				continue;
			}

			_elevationStyleOption.Select(index);
			return;
		}

		_elevationStyleOption.Select(0);
	}

	private void SetupContinentCountOptions()
	{
		_continentCountOption.Clear();
		_continentCountOption.AddItem("2 块", 2);
		_continentCountOption.AddItem("3 块", 3);
		_continentCountOption.AddItem("4 块", 4);
		SelectContinentCountOption(_continentCount);

		_continentCountOption.ItemSelected += index =>
		{
			_continentCount = Mathf.Clamp(_continentCountOption.GetItemId((int)index), 2, 4);
			UpdateContinentCountVisibility();

			if (_terrainMorphology == TerrainMorphology.Continents)
			{
				GenerateWorld();
			}
		};

		UpdateContinentCountVisibility();
	}

	private void SelectContinentCountOption(int count)
	{
		for (var index = 0; index < _continentCountOption.ItemCount; index++)
		{
			if (_continentCountOption.GetItemId(index) != count)
			{
				continue;
			}

			_continentCountOption.Select(index);
			return;
		}

		_continentCountOption.Select(1);
	}

	private void UpdateContinentCountVisibility()
	{
		_continentCountWrap.Visible = _terrainMorphology == TerrainMorphology.Continents;
	}

	private void ApplyTerrainPreset(int presetIndex, bool regenerate)
	{
		var index = Mathf.Clamp(presetIndex, 0, TerrainPresets.Length - 1);
		var preset = TerrainPresets[index];

		SeaLevel = Mathf.Clamp(preset.SeaLevel, 0f, 0.9f);
		PlateCount = Mathf.Clamp(preset.PlateCount, 1, 200);
		WindCellCount = Mathf.Clamp(preset.WindCellCount, 1, 100);
		HeatFactor = Mathf.Clamp(preset.HeatFactor, 0.01f, 1f);
		ErosionIterations = Mathf.Clamp(preset.ErosionIterations, 0, 20);
		_terrainOceanicRatio = Mathf.Clamp(preset.OceanicRatio, 0.05f, 0.95f);
		_terrainContinentBias = Mathf.Clamp(preset.ContinentBias, 0f, 1f);
		_terrainMorphology = preset.Morphology;
		_continentCount = Mathf.Clamp(preset.ContinentCount, 2, 4);

		_seaLevelSlider.SetValueNoSignal(SeaLevel);
		_heatSlider.SetValueNoSignal(HeatFactor);
		_erosionSlider.SetValueNoSignal(ErosionIterations);
		SelectContinentCountOption(_continentCount);
		UpdateContinentCountVisibility();

		UpdateLabels();

		if (regenerate)
		{
			GenerateWorld();
		}
	}

	private static string BuildInfoPointOptionText(int index, Vector2I size)
	{
		var profile = index switch
		{
			0 => "预览",
			1 => "标准",
			2 => "精细",
			3 => "高清",
			_ => "极致"
		};

		return profile;
	}

	private void ApplyMapSizePreset(int presetIndex)
	{
		var index = Mathf.Clamp(presetIndex, 0, MapSizePresets.Length - 1);
		MapWidth = MapSizePresets[index].X;
		MapHeight = MapSizePresets[index].Y;
	}

	private int FindMapSizePresetIndex(int width, int height)
	{
		for (var index = 0; index < MapSizePresets.Length; index++)
		{
			var size = MapSizePresets[index];
			if (size.X == width && size.Y == height)
			{
				return index;
			}
		}

		return -1;
	}

	private bool IsHighInfoPointSelected()
	{
		return MapWidth >= 2048 || MapHeight >= 1024;
	}

	private void ShowInfoPointHint()
	{
		_infoLabel.Text = IsHighInfoPointSelected()
			? HighInfoPointWarningText
			: FixedUltraOutputText;
	}

}
