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
		_selectedTimelineEventEpoch = _currentEpoch;
		_mountainControlExpanded = true;
		_mapMode = MapMode.Geographic;

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
		SelectMapModeOption(_mapMode);
		ApplyUiFontScale();

		UpdateRiverDensityControlState();
		UpdateRiverLayerAvailability();
		UpdateLorePanel();
	}

	private void LoadAdvancedSettings()
	{
		var config = new ConfigFile();
		if (config.Load(AdvancedSettingsPath) != Error.Ok)
		{
			return;
		}

		EnableRivers = (bool)config.GetValue(AdvancedSettingsSection, "enable_rivers", DefaultEnableRivers);
		RiverDensity = Mathf.Clamp((float)(double)config.GetValue(AdvancedSettingsSection, "river_density", (double)DefaultRiverDensity), 0.4f, 2.5f);
		WindArrowDensity = Mathf.Clamp((float)(double)config.GetValue(AdvancedSettingsSection, "wind_arrow_density", (double)DefaultWindArrowDensity), 0.5f, 2.5f);
		BasinSensitivity = Mathf.Clamp((float)(double)config.GetValue(AdvancedSettingsSection, "basin_sensitivity", (double)DefaultBasinSensitivity), 0.5f, 2.0f);
		_interiorRelief = Mathf.Clamp((float)(double)config.GetValue(AdvancedSettingsSection, "interior_relief", (double)DefaultInteriorRelief), 0.5f, 2.0f);
		_orogenyStrength = Mathf.Clamp((float)(double)config.GetValue(AdvancedSettingsSection, "orogeny_strength", (double)DefaultOrogenyStrength), 0.5f, 2.5f);
		_subductionArcRatio = Mathf.Clamp((float)(double)config.GetValue(AdvancedSettingsSection, "subduction_arc_ratio", (double)DefaultSubductionArcRatio), 0.2f, 1.0f);
		_continentalAge = Mathf.Clamp((int)(long)config.GetValue(AdvancedSettingsSection, "continental_age", (long)DefaultContinentalAge), 0, 100);
		var defaultMountainPresetId = (long)ResolveMountainPresetIdFromCurrentValues();
		var mountainPresetId = (int)(long)config.GetValue(AdvancedSettingsSection, "mountain_preset", defaultMountainPresetId);
		_mountainPresetId = Enum.IsDefined(typeof(MountainPresetId), mountainPresetId)
			? (MountainPresetId)mountainPresetId
			: ResolveMountainPresetIdFromCurrentValues();

		var styleId = (int)(long)config.GetValue(AdvancedSettingsSection, "elevation_style", (long)DefaultElevationStyle);
		_elevationStyle = Enum.IsDefined(typeof(ElevationStyle), styleId)
			? (ElevationStyle)styleId
			: DefaultElevationStyle;

		_preferredLayerId = (int)(long)config.GetValue(AdvancedSettingsSection, "selected_layer", (long)_preferredLayerId);
		_preferredAdvancedPanelVisible = (bool)config.GetValue(AdvancedSettingsSection, "advanced_panel_visible", _preferredAdvancedPanelVisible);
		_mountainControlExpanded = (bool)config.GetValue(AdvancedSettingsSection, "mountain_control_expanded", _mountainControlExpanded);
		_magicDensity = Mathf.Clamp((int)(long)config.GetValue(AdvancedSettingsSection, "magic_density", (long)DefaultMagicDensity), 0, 100);
		_civilAggression = Mathf.Clamp((int)(long)config.GetValue(AdvancedSettingsSection, "civil_aggression", (long)DefaultCivilAggression), 0, 100);
		_speciesDiversity = Mathf.Clamp((int)(long)config.GetValue(AdvancedSettingsSection, "species_diversity", (long)DefaultSpeciesDiversity), 0, 100);
		_uiFontScale = Mathf.Clamp((float)(double)config.GetValue(AdvancedSettingsSection, "ui_font_scale", (double)DefaultUiFontScale), MinUiFontScale, MaxUiFontScale);
		_currentEpoch = Mathf.Clamp((int)(long)config.GetValue(AdvancedSettingsSection, "timeline_epoch", (long)DefaultEpoch), 0, MaxEpoch);
		_selectedTimelineEventEpoch = _currentEpoch;
		var mapModeId = (int)(long)config.GetValue(AdvancedSettingsSection, "map_mode", (long)MapMode.Geographic);
		_mapMode = Enum.IsDefined(typeof(MapMode), mapModeId)
			? (MapMode)mapModeId
			: MapMode.Geographic;
		_lastArchivePath = (string)config.GetValue(ArchiveSection, LastArchivePathKey, string.Empty);

		if (config.HasSectionKey(PerformanceSection, PerformanceCpuScoreKey))
		{
			_cpuPerformanceScore = ClampDouble((double)config.GetValue(PerformanceSection, PerformanceCpuScoreKey, 1.0), MinCpuPerformanceScore, MaxCpuPerformanceScore);
			_performanceSampleReady = true;
		}

		if (config.HasSectionKey(PerformanceSection, PerformanceSecondsPerUnitKey))
		{
			_secondsPerWorkUnit = ClampDouble((double)config.GetValue(PerformanceSection, PerformanceSecondsPerUnitKey, DefaultSecondsPerWorkUnit), MinSecondsPerWorkUnit, MaxSecondsPerWorkUnit);
			_hasHistoricalThroughput = true;
		}
	}

	private void SaveAdvancedSettings()
	{
		var config = new ConfigFile();
		_ = config.Load(AdvancedSettingsPath);

		config.SetValue(AdvancedSettingsSection, "enable_rivers", EnableRivers);
		config.SetValue(AdvancedSettingsSection, "river_density", (double)RiverDensity);
		config.SetValue(AdvancedSettingsSection, "wind_arrow_density", (double)WindArrowDensity);
		config.SetValue(AdvancedSettingsSection, "basin_sensitivity", (double)BasinSensitivity);
		config.SetValue(AdvancedSettingsSection, "interior_relief", (double)_interiorRelief);
		config.SetValue(AdvancedSettingsSection, "orogeny_strength", (double)_orogenyStrength);
		config.SetValue(AdvancedSettingsSection, "subduction_arc_ratio", (double)_subductionArcRatio);
		config.SetValue(AdvancedSettingsSection, "continental_age", (long)_continentalAge);
		config.SetValue(AdvancedSettingsSection, "mountain_preset", (long)_mountainPresetId);
		config.SetValue(AdvancedSettingsSection, "elevation_style", (long)_elevationStyle);
		config.SetValue(AdvancedSettingsSection, "selected_layer", (long)_layerOption.GetSelectedId());
		config.SetValue(AdvancedSettingsSection, "advanced_panel_visible", _advancedSettingsPanel.Visible);
		config.SetValue(AdvancedSettingsSection, "mountain_control_expanded", _mountainControlExpanded);
		config.SetValue(AdvancedSettingsSection, "magic_density", (long)_magicDensity);
		config.SetValue(AdvancedSettingsSection, "civil_aggression", (long)_civilAggression);
		config.SetValue(AdvancedSettingsSection, "species_diversity", (long)_speciesDiversity);
		config.SetValue(AdvancedSettingsSection, "ui_font_scale", (double)_uiFontScale);
		config.SetValue(AdvancedSettingsSection, "timeline_epoch", (long)_currentEpoch);
		config.SetValue(AdvancedSettingsSection, "map_mode", (long)_mapMode);
		config.SetValue(ArchiveSection, LastArchivePathKey, _lastArchivePath);
		config.SetValue(PerformanceSection, PerformanceCpuScoreKey, _cpuPerformanceScore);
		config.SetValue(PerformanceSection, PerformanceSecondsPerUnitKey, _secondsPerWorkUnit);

		_ = config.Save(AdvancedSettingsPath);
	}

}
