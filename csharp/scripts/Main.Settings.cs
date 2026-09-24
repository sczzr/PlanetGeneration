using Godot;
using PlanetGeneration.WorldGen;
using PlanetGeneration.UI.Services;
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
using PlanetGeneration.UI.State;

namespace PlanetGeneration;

public partial class Main : Control
{
	private void OnResetAdvancedSettingsPressed()
	{
		_resetAdvancedConfirmDialog.PopupCentered();
	}

	private void ResetAdvancedSettingsConfirmed()
	{
		ApplyDefaultAdvancedSettings();
		SaveAdvancedSettings();
		UpdateLabels();
		GenerateWorld();
	}

	private void ApplyDefaultAdvancedSettings()
	{
		EnableRivers = DefaultEnableRivers;
		RiverDensity = DefaultRiverDensity;
		WindArrowDensity = DefaultWindArrowDensity;
		BasinSensitivity = DefaultBasinSensitivity;
		_interiorRelief = DefaultInteriorRelief;
		_orogenyStrength = DefaultOrogenyStrength;
		_subductionArcRatio = DefaultSubductionArcRatio;
		_continentalAge = DefaultContinentalAge;
		_mountainPresetId = MountainPresetId.EarthLike;
		_elevationStyle = DefaultElevationStyle;
		_magicDensity = DefaultMagicDensity;
		_civilAggression = DefaultCivilAggression;
		_speciesDiversity = DefaultSpeciesDiversity;
		_uiFontScale = DefaultUiFontScale;
		_currentEpoch = DefaultEpoch;
		_oracleAutoUnloadIdleSeconds = DefaultOracleAutoUnloadIdleSeconds;
		_selectedTimelineEventEpoch = _currentEpoch;

		_riverToggle.ButtonPressed = EnableRivers;
		_riverDensitySlider.SetValueNoSignal(RiverDensity);
		_windArrowDensitySlider.SetValueNoSignal(WindArrowDensity);
		_basinSensitivitySlider.SetValueNoSignal(BasinSensitivity);
		_interiorReliefSlider.SetValueNoSignal(_interiorRelief);
		_orogenyStrengthSlider.SetValueNoSignal(_orogenyStrength);
		_subductionArcRatioSlider.SetValueNoSignal(_subductionArcRatio);
		_continentalAgeSlider.SetValueNoSignal(_continentalAge);
		SelectMountainPresetOption(_mountainPresetId);
		_magicSlider.SetValueNoSignal(_magicDensity);
		_aggressionSlider.SetValueNoSignal(_civilAggression);
		_diversitySlider.SetValueNoSignal(_speciesDiversity);
		_uiFontScaleSlider.SetValueNoSignal(_uiFontScale * 100f);
		_timelineSlider.SetValueNoSignal(_currentEpoch);
		SelectElevationStyleOption(_elevationStyle);
		ApplyUiFontScale();

		UpdateRiverDensityControlState();
		UpdateRiverLayerAvailability();
		UpdateLorePanel();
	}

	private void ApplySnapshotOptions(PlanetGeneration.Application.GeneratedWorldData world)
	{
		var options = world.Snapshot!.Options;
		Seed = options.Seed;
		MapWidth = world.Stats.Width;
		MapHeight = world.Stats.Height;
		_targetCellCount = options.TargetCellCount;
		_cellsDesired = options.TargetCellCount;
		PlateCount = options.PlateCount;
		WindCellCount = options.WindCellCount;
		SeaLevel = options.SeaLevel;
		HeatFactor = options.HeatFactor;
		MoistureFactor = options.MoistureFactor;
		MoistureIterations = options.MoistureIterations;
		ErosionIterations = options.ErosionIterations;
		EnableRivers = options.EnableRivers;
		RiverDensity = options.RiverDensity;
		BasinSensitivity = options.BasinSensitivity;
		LandformTuning = options.LandformTuning;
		_terrainOceanicRatio = options.OceanicRatio;
		_terrainContinentBias = options.ContinentBias;
		_interiorRelief = options.InteriorRelief;
		_orogenyStrength = options.OrogenyStrength;
		_subductionArcRatio = options.SubductionArcRatio;
		_continentalAge = options.ContinentalAge;
		_terrainMorphology = (TerrainMorphology)options.Morphology;
		_continentCount = options.ContinentCount;
		_tuning = world.Tuning;
		_enableCartographyDesigner = options.EnableCartographyDesigner;
		_blueprintName = options.BlueprintName;
		_currentEpoch = options.Epoch;
		_selectedTimelineEventEpoch = options.Epoch;
		_speciesDiversity = options.SpeciesDiversity;
		_civilAggression = options.CivilAggression;
		_magicDensity = options.MagicDensity;
		// 不触发值变更事件，避免载入过程中重算或立即覆盖刚读入的数据。
		_seedSpin?.SetValueNoSignal(Seed);
		_seaLevelSlider?.SetValueNoSignal(SeaLevel);
		_heatSlider?.SetValueNoSignal(HeatFactor);
		_moistureSlider?.SetValueNoSignal(MoistureFactor);
		_erosionSlider?.SetValueNoSignal(ErosionIterations);
		_riverDensitySlider?.SetValueNoSignal(RiverDensity);
		_riverToggle?.SetPressedNoSignal(EnableRivers);
		_basinSensitivitySlider?.SetValueNoSignal(BasinSensitivity);
		_interiorReliefSlider?.SetValueNoSignal(_interiorRelief);
		_orogenyStrengthSlider?.SetValueNoSignal(_orogenyStrength);
		_subductionArcRatioSlider?.SetValueNoSignal(_subductionArcRatio);
		_continentalAgeSlider?.SetValueNoSignal(_continentalAge);
		_magicSlider?.SetValueNoSignal(_magicDensity);
		_aggressionSlider?.SetValueNoSignal(_civilAggression);
		_diversitySlider?.SetValueNoSignal(_speciesDiversity);
		_timelineSlider?.SetValueNoSignal(_currentEpoch);
	}

	private void LoadAdvancedSettings()
	{
		var snapshot = new UiSettingsService().Load(AdvancedSettingsPath);
		var generation = snapshot.Generation;
		var preferences = snapshot.Preferences;

		EnableRivers = generation.EnableRivers;
		RiverDensity = generation.RiverDensity;
		WindArrowDensity = generation.WindArrowDensity;
		BasinSensitivity = generation.BasinSensitivity;
		_interiorRelief = generation.InteriorRelief;
		_orogenyStrength = generation.OrogenyStrength;
		_subductionArcRatio = generation.SubductionArcRatio;
		_continentalAge = generation.ContinentalAge;
		_mountainPresetId = Enum.IsDefined(typeof(MountainPresetId), generation.MountainPresetId)
			? (MountainPresetId)generation.MountainPresetId
			: ResolveMountainPresetIdFromCurrentValues();
		_elevationStyle = Enum.IsDefined(typeof(ElevationStyle), generation.ElevationStyleId)
			? (ElevationStyle)generation.ElevationStyleId
			: DefaultElevationStyle;
		_preferredLayerId = preferences.PreferredLayerId;
		_consolePanelVisible = preferences.ConsolePanelVisible;
		_magicDensity = generation.MagicDensity;
		_civilAggression = generation.CivilAggression;
		_speciesDiversity = generation.SpeciesDiversity;
		_polygonTileMode = Enum.IsDefined(typeof(PolygonTileMode), generation.PolygonTileModeId)
			? (PolygonTileMode)generation.PolygonTileModeId
			: PolygonTileMode.Cells;
		_cellsDesired = generation.CellsDesired > 0 ? generation.CellsDesired : 0;
		_uiFontScale = preferences.UiFontScale;
		_currentEpoch = preferences.CurrentEpoch;
		_oracleAutoUnloadIdleSeconds = preferences.OracleAutoUnloadIdleSeconds;
		_selectedTimelineEventEpoch = _currentEpoch;
		_lastArchivePath = snapshot.LastArchivePath;

		if (snapshot.PerformanceSampleReady)
		{
			_cpuPerformanceScore = ClampDouble(snapshot.CpuPerformanceScore, MinCpuPerformanceScore, MaxCpuPerformanceScore);
			_performanceSampleReady = true;
		}
		if (snapshot.HistoricalThroughput)
		{
			_secondsPerWorkUnit = ClampDouble(snapshot.SecondsPerWorkUnit, MinSecondsPerWorkUnit, MaxSecondsPerWorkUnit);
			_hasHistoricalThroughput = true;
		}
	}

	private void SaveAdvancedSettings()
	{
		var snapshot = new UiSettingsSnapshot();
		var generation = snapshot.Generation;
		var preferences = snapshot.Preferences;
		generation.EnableRivers = EnableRivers;
		generation.RiverDensity = RiverDensity;
		generation.WindArrowDensity = WindArrowDensity;
		generation.BasinSensitivity = BasinSensitivity;
		generation.InteriorRelief = _interiorRelief;
		generation.OrogenyStrength = _orogenyStrength;
		generation.SubductionArcRatio = _subductionArcRatio;
		generation.ContinentalAge = _continentalAge;
		generation.MountainPresetId = (int)_mountainPresetId;
		generation.ElevationStyleId = (int)_elevationStyle;
		generation.MagicDensity = _magicDensity;
		generation.CivilAggression = _civilAggression;
		generation.SpeciesDiversity = _speciesDiversity;
		generation.PolygonTileModeId = (int)_polygonTileMode;
		generation.CellsDesired = _cellsDesired;
		preferences.PreferredLayerId = _layerOption.GetSelectedId();
		preferences.ConsolePanelVisible = _consolePanelVisible;
		preferences.UiFontScale = _uiFontScale;
		preferences.CurrentEpoch = _currentEpoch;
		preferences.OracleAutoUnloadIdleSeconds = _oracleAutoUnloadIdleSeconds;
		snapshot.LastArchivePath = _lastArchivePath;
		snapshot.CpuPerformanceScore = _cpuPerformanceScore;
		snapshot.SecondsPerWorkUnit = _secondsPerWorkUnit;
		_ = new UiSettingsService().Save(snapshot, AdvancedSettingsPath);
	}

}
