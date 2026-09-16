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
