using Godot;
using PlanetGeneration.UI.State;
using System;

namespace PlanetGeneration.UI.Services;

/// <summary>
/// Persists UI preferences and generation inputs without depending on Control nodes.
/// Main can map these DTOs to its existing fields during the incremental migration.
/// </summary>
public sealed class UiSettingsService
{
	public const string DefaultPath = "user://advanced_settings.cfg";

	private const string SettingsSection = "advanced";
	private const string ArchiveSection = "archive";
	private const string PerformanceSection = "performance";

	public UiSettingsSnapshot Load(string path = DefaultPath)
	{
		var snapshot = new UiSettingsSnapshot();
		var config = new ConfigFile();
		if (config.Load(path) != Error.Ok)
		{
			return snapshot;
		}

		var generation = snapshot.Generation;
		var preferences = snapshot.Preferences;

		generation.EnableRivers = (bool)config.GetValue(SettingsSection, "enable_rivers", generation.EnableRivers);
		generation.RiverDensity = ClampDouble(config, SettingsSection, "river_density", generation.RiverDensity, 0.4, 2.5);
		generation.WindArrowDensity = ClampDouble(config, SettingsSection, "wind_arrow_density", generation.WindArrowDensity, 0.5, 2.5);
		generation.BasinSensitivity = ClampDouble(config, SettingsSection, "basin_sensitivity", generation.BasinSensitivity, 0.5, 2.0);
		generation.InteriorRelief = ClampDouble(config, SettingsSection, "interior_relief", generation.InteriorRelief, 0.5, 2.0);
		generation.OrogenyStrength = ClampDouble(config, SettingsSection, "orogeny_strength", generation.OrogenyStrength, 0.5, 2.5);
		generation.SubductionArcRatio = ClampDouble(config, SettingsSection, "subduction_arc_ratio", generation.SubductionArcRatio, 0.2, 1.0);
		generation.ContinentalAge = ClampInt(config, SettingsSection, "continental_age", generation.ContinentalAge, 0, 100);
		generation.MountainPresetId = ReadInt(config, SettingsSection, "mountain_preset", generation.MountainPresetId);
		generation.ElevationStyleId = ReadInt(config, SettingsSection, "elevation_style", generation.ElevationStyleId);
		generation.MagicDensity = ClampInt(config, SettingsSection, "magic_density", generation.MagicDensity, 0, 100);
		generation.CivilAggression = ClampInt(config, SettingsSection, "civil_aggression", generation.CivilAggression, 0, 100);
		generation.SpeciesDiversity = ClampInt(config, SettingsSection, "species_diversity", generation.SpeciesDiversity, 0, 100);

		preferences.PreferredLayerId = ReadInt(config, SettingsSection, "selected_layer", preferences.PreferredLayerId);
		preferences.AdvancedPanelVisible = (bool)config.GetValue(SettingsSection, "advanced_panel_visible", preferences.AdvancedPanelVisible);
		preferences.ConsolePanelVisible = (bool)config.GetValue(SettingsSection, "console_panel_visible", preferences.ConsolePanelVisible);
		preferences.UiFontScale = ClampDouble(config, SettingsSection, "ui_font_scale", preferences.UiFontScale, 0.60, 1.40);
		preferences.CurrentEpoch = ClampInt(config, SettingsSection, "timeline_epoch", preferences.CurrentEpoch, 0, 1000);
		preferences.OracleAutoUnloadIdleSeconds = ClampInt(config, SettingsSection, "oracle_auto_unload_idle_seconds", preferences.OracleAutoUnloadIdleSeconds, 30, 1800);
		preferences.SelectedTimelineEventEpoch = preferences.CurrentEpoch;

		snapshot.LastArchivePath = (string)config.GetValue(ArchiveSection, "last_path", string.Empty);
		if (config.HasSectionKey(PerformanceSection, "cpu_score"))
		{
			snapshot.CpuPerformanceScore = ClampDouble(config, PerformanceSection, "cpu_score", 1.0, 0.25, 4.0);
			snapshot.PerformanceSampleReady = true;
		}
		if (config.HasSectionKey(PerformanceSection, "seconds_per_unit"))
		{
			snapshot.SecondsPerWorkUnit = ClampDouble(config, PerformanceSection, "seconds_per_unit", 0.55, 0.05, 20.0);
			snapshot.HistoricalThroughput = true;
		}

		return snapshot;
	}

	public Error Save(UiSettingsSnapshot snapshot, string path = DefaultPath)
	{
		var config = new ConfigFile();
		_ = config.Load(path);

		var generation = snapshot.Generation;
		var preferences = snapshot.Preferences;
		config.SetValue(SettingsSection, "enable_rivers", generation.EnableRivers);
		config.SetValue(SettingsSection, "river_density", (double)generation.RiverDensity);
		config.SetValue(SettingsSection, "wind_arrow_density", (double)generation.WindArrowDensity);
		config.SetValue(SettingsSection, "basin_sensitivity", (double)generation.BasinSensitivity);
		config.SetValue(SettingsSection, "interior_relief", (double)generation.InteriorRelief);
		config.SetValue(SettingsSection, "orogeny_strength", (double)generation.OrogenyStrength);
		config.SetValue(SettingsSection, "subduction_arc_ratio", (double)generation.SubductionArcRatio);
		config.SetValue(SettingsSection, "continental_age", (long)generation.ContinentalAge);
		config.SetValue(SettingsSection, "mountain_preset", (long)generation.MountainPresetId);
		config.SetValue(SettingsSection, "elevation_style", (long)generation.ElevationStyleId);
		config.SetValue(SettingsSection, "magic_density", (long)generation.MagicDensity);
		config.SetValue(SettingsSection, "civil_aggression", (long)generation.CivilAggression);
		config.SetValue(SettingsSection, "species_diversity", (long)generation.SpeciesDiversity);
		config.SetValue(SettingsSection, "selected_layer", (long)preferences.PreferredLayerId);
		config.SetValue(SettingsSection, "advanced_panel_visible", preferences.AdvancedPanelVisible);
		config.SetValue(SettingsSection, "console_panel_visible", preferences.ConsolePanelVisible);
		config.SetValue(SettingsSection, "ui_font_scale", (double)preferences.UiFontScale);
		config.SetValue(SettingsSection, "timeline_epoch", (long)preferences.CurrentEpoch);
		config.SetValue(SettingsSection, "oracle_auto_unload_idle_seconds", (long)preferences.OracleAutoUnloadIdleSeconds);
		config.SetValue(ArchiveSection, "last_path", snapshot.LastArchivePath);
		config.SetValue(PerformanceSection, "cpu_score", snapshot.CpuPerformanceScore);
		config.SetValue(PerformanceSection, "seconds_per_unit", snapshot.SecondsPerWorkUnit);

		return config.Save(path);
	}

	private static int ReadInt(ConfigFile config, string section, string key, int fallback)
	{
		return (int)(long)config.GetValue(section, key, (long)fallback);
	}

	private static int ClampInt(ConfigFile config, string section, string key, int fallback, int min, int max)
	{
		return Math.Clamp(ReadInt(config, section, key, fallback), min, max);
	}

	private static float ClampDouble(ConfigFile config, string section, string key, float fallback, double min, double max)
	{
		return (float)Math.Clamp((double)config.GetValue(section, key, (double)fallback), min, max);
	}

	private static double ClampDouble(ConfigFile config, string section, string key, double fallback, double min, double max)
	{
		return Math.Clamp((double)config.GetValue(section, key, fallback), min, max);
	}
}

public sealed class UiSettingsSnapshot
{
	public GeneratorSettings Generation { get; } = new();
	public UiPreferences Preferences { get; } = new();
	public string LastArchivePath { get; set; } = string.Empty;
	public double CpuPerformanceScore { get; set; } = 1.0;
	public double SecondsPerWorkUnit { get; set; } = 0.55;
	public bool PerformanceSampleReady { get; set; }
	public bool HistoricalThroughput { get; set; }
}
