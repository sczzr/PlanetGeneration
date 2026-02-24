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
	public override void _Ready()
	{
		_mapTexture = GetNodeByName<TextureRect>("MapTexture");
		_seedSpin = GetNodeByName<SpinBox>("SeedSpin");
		_seaLevelSlider = GetNodeByName<HSlider>("SeaLevelSlider");
		_heatSlider = GetNodeByName<HSlider>("HeatSlider");
		_erosionSlider = GetNodeByName<HSlider>("ErosionSlider");
		_seaLevelValue = GetNodeByName<Label>("SeaLevelValue");
		_heatValue = GetNodeByName<Label>("HeatValue");
		_erosionValue = GetNodeByName<Label>("ErosionValue");
		_infoLabel = GetNodeByName<Label>("InfoLabel");
		_compareStatsLabel = GetNodeByName<Label>("CompareStatsLabel");
		_cityNamesLabel = GetNodeByName<RichTextLabel>("CityNamesLabel");
		_legendPanel = GetNodeByName<Control>("LegendPanel");
		_legendTitle = GetNodeByName<Label>("LegendTitle");
		_legendTexture = GetNodeByName<TextureRect>("LegendTexture");
		_legendMinLabel = GetNodeByName<Label>("LegendMin");
		_legendMaxLabel = GetNodeByName<Label>("LegendMax");
		_biomeLegendPanel = GetNodeByName<Control>("BiomeLegendPanel");
		_biomeLegendText = GetNodeByName<RichTextLabel>("BiomeLegendText");
		_layerOption = GetNodeByName<OptionButton>("LayerOption");
		_mapSizeOption = GetNodeByName<OptionButton>("MapSizeOption");
		_terrainPresetOption = GetNodeByName<OptionButton>("TerrainPresetOption");
		_mountainPresetOption = GetNodeByName<OptionButton>("MountainPresetOption");
		_elevationStyleOption = GetNodeByName<OptionButton>("ElevationStyleOption");
		_continentCountOption = GetNodeByName<OptionButton>("ContinentCountOption");
		_archiveOption = GetNodeByName<OptionButton>("ArchiveOption");
		_mapModeOption = GetNodeByName<OptionButton>("MapModeOption");
		_advancedSettingsButton = GetNodeByName<Button>("AdvancedSettingsButton");
		_resetAdvancedSettingsButton = GetNodeByName<Button>("ResetAdvancedSettingsButton");
		_persistCacheGroupButton = GetNodeByName<Button>("PersistCacheGroupButton");
		_clearCacheButton = GetNodeByName<Button>("ClearCacheButton");
		_riverToggle = GetNodeByName<CheckBox>("RiverToggle");
		_compareToggle = GetNodeByName<CheckBox>("CompareToggle");
		_exportPngButton = GetNodeByName<Button>("ExportPngButton");
		_exportJsonButton = GetNodeByName<Button>("ExportJsonButton");
		_themeToggleButton = GetNodeByName<Button>("ThemeToggleButton");
		_generateProgress = GetNodeByName<ProgressBar>("GenerateProgress");
		_progressStatus = GetNodeByName<Label>("ProgressStatus");
		_cacheStatsLabel = GetNodeByName<Label>("CacheStatsLabel");
		_progressOverlay = GetNodeByName<Control>("ProgressOverlay");
		_layerButtons = GetNodeByName<Container>("LayerButtons");
		_layerRow = GetNodeByName<Control>("LayerRow");
		_mapCenter = GetNodeByName<Control>("MapCenter");
		_mapRoot = GetNodeByName<Control>("MapRoot");
		_advancedSettingsPanel = GetNodeByName<Control>("AdvancedSettingsPanel");
		_biomeHoverPanel = GetNodeByName<Control>("BiomeHoverPanel");
		_biomeHoverText = GetNodeByName<Label>("BiomeHoverText");
		_continentCountWrap = GetNodeByName<Control>("ContinentCountWrap");
		_mapAspect = GetNodeByName<AspectRatioContainer>("MapAspect");
		_saveFileDialog = GetNodeByName<FileDialog>("SaveFileDialog");
		_resetAdvancedConfirmDialog = GetNodeByName<ConfirmationDialog>("ResetAdvancedConfirmDialog");
		_mapInfoWarningDialog = GetNodeByName<ConfirmationDialog>("MapInfoWarningDialog");
		_mapInfoWarningSkipCheck = GetNodeByName<CheckBox>("MapInfoWarningSkipCheck");
		_riverDensitySlider = GetNodeByName<HSlider>("RiverDensitySlider");
		_riverDensityValue = GetNodeByName<Label>("RiverDensityValue");
		_windArrowDensitySlider = GetNodeByName<HSlider>("WindArrowDensitySlider");
		_windArrowDensityValue = GetNodeByName<Label>("WindArrowDensityValue");
		_basinSensitivitySlider = GetNodeByName<HSlider>("BasinSensitivitySlider");
		_basinSensitivityValue = GetNodeByName<Label>("BasinSensitivityValue");
		_interiorReliefSlider = GetNodeByName<HSlider>("InteriorReliefSlider");
		_interiorReliefValue = GetNodeByName<Label>("InteriorReliefValue");
		_orogenyStrengthSlider = GetNodeByName<HSlider>("OrogenyStrengthSlider");
		_orogenyStrengthValue = GetNodeByName<Label>("OrogenyStrengthValue");
		_subductionArcRatioSlider = GetNodeByName<HSlider>("SubductionArcRatioSlider");
		_subductionArcRatioValue = GetNodeByName<Label>("SubductionArcRatioValue");
		_continentalAgeSlider = GetNodeByName<HSlider>("ContinentalAgeSlider");
		_continentalAgeValue = GetNodeByName<Label>("ContinentalAgeValue");
		_mountainControlToggleButton = GetNodeByName<Button>("MountainControlToggleButton");
		_mountainControlBody = GetNodeByName<Control>("MountainControlBody");
		_mountainControlSummaryLabel = GetNodeByName<Label>("MountainControlSummary");
		_magicSlider = GetNodeByName<HSlider>("MagicSlider");
		_magicValue = GetNodeByName<Label>("MagicValue");
		_aggressionSlider = GetNodeByName<HSlider>("AggressionSlider");
		_aggressionValue = GetNodeByName<Label>("AggressionValue");
		_diversitySlider = GetNodeByName<HSlider>("DiversitySlider");
		_diversityValue = GetNodeByName<Label>("DiversityValue");
		_timelineSlider = GetNodeByName<HSlider>("TimelineSlider");
		_uiFontScaleSlider = GetNodeByName<HSlider>("UiFontScaleSlider");
		_prevEpochButton = GetNodeByName<Button>("PrevEpochButton");
		_nextEpochButton = GetNodeByName<Button>("NextEpochButton");
		_uiFontScaleValue = GetNodeByName<Label>("UiFontScaleValue");
		_epochLabel = GetNodeByName<Label>("EpochLabel");
		_epochEventIndexLabel = GetNodeByName<Label>("EpochEventIndexLabel");
		_loreStateLabel = GetNodeByName<Label>("LoreStateLabel");
		_threatLabel = GetNodeByName<Label>("ThreatLabel");
		_loreText = GetNodeByName<RichTextLabel>("LoreText");

		_generateProgress.Value = 0;
		_progressStatus.Text = "待命";
		_progressOverlay.Visible = false;
		_advancedSettingsPanel.Visible = false;
		_biomeHoverPanel.Visible = false;
		_pendingExportKind = ExportKind.None;

		_saveFileDialog.FileMode = FileDialog.FileModeEnum.SaveFile;
		_saveFileDialog.Access = FileDialog.AccessEnum.Filesystem;
		_saveFileDialog.FileSelected += OnSaveFileSelected;
		_saveFileDialog.Canceled += () => _pendingExportKind = ExportKind.None;
		_resetAdvancedConfirmDialog.Confirmed += ResetAdvancedSettingsConfirmed;
		_mapInfoWarningDialog.Confirmed += ConfirmMapInfoSelection;
		_mapInfoWarningDialog.Canceled += () =>
		{
			_pendingMapSizeIndex = -1;
			_mapInfoWarningSkipCheck.ButtonPressed = false;
		};

		_mapCenter.Resized += SyncMapAspectToCenter;
		CallDeferred(nameof(SyncMapAspectToCenter));
		LoadAdvancedSettings();

		SetupLayerOptions();
		SetupMapSizeOptions();
		SetupContinentCountOptions();
		SetupTerrainPresetOptions();
		SetupMountainPresetOptions();
		SetupElevationStyleOptions();
		SetupArchiveOptions();
		SetupMapModeOptions();
		DisableMouseWheelForAllSliders();
		CaptureUiFontSizeBaselines();
		ApplyUiFontScale();

		GetNodeByName<Button>("GenerateButton").Pressed += OnGeneratePressed;
		GetNodeByName<Button>("RandomButton").Pressed += OnRandomPressed;
		_advancedSettingsButton.Pressed += ToggleAdvancedSettings;
		_resetAdvancedSettingsButton.Pressed += OnResetAdvancedSettingsPressed;
		_exportPngButton.Pressed += OnExportPngPressed;
		_exportJsonButton.Pressed += OnExportJsonPressed;
		_themeToggleButton.Pressed += OnThemeTogglePressed;
		_persistCacheGroupButton.Pressed += OnSaveArchivePressed;
		_clearCacheButton.Pressed += OnClearCachePressed;

		_seaLevelSlider.ValueChanged += OnSeaLevelChanged;
		_heatSlider.ValueChanged += OnHeatChanged;
		_erosionSlider.ValueChanged += OnErosionChanged;
		_riverDensitySlider.ValueChanged += OnRiverDensityChanged;
		_windArrowDensitySlider.ValueChanged += OnWindArrowDensityChanged;
		_basinSensitivitySlider.ValueChanged += OnBasinSensitivityChanged;
		_interiorReliefSlider.ValueChanged += OnInteriorReliefChanged;
		_orogenyStrengthSlider.ValueChanged += OnOrogenyStrengthChanged;
		_subductionArcRatioSlider.ValueChanged += OnSubductionArcRatioChanged;
		_continentalAgeSlider.ValueChanged += OnContinentalAgeChanged;
		_mountainControlToggleButton.Pressed += ToggleMountainControlGroup;
		_magicSlider.ValueChanged += OnMagicDensityChanged;
		_aggressionSlider.ValueChanged += OnCivilAggressionChanged;
		_diversitySlider.ValueChanged += OnSpeciesDiversityChanged;
		_uiFontScaleSlider.ValueChanged += OnUiFontScaleChanged;
		_timelineSlider.ValueChanged += OnTimelineChanged;
		_prevEpochButton.Pressed += OnPrevEpochPressed;
		_nextEpochButton.Pressed += OnNextEpochPressed;
		_layerOption.ItemSelected += _ =>
		{
			SyncMapModeFromLayer(_layerOption.GetSelectedId());
			RedrawCurrentLayer();
			UpdateLayerQuickButtons();
			SaveAdvancedSettings();
		};

		_mapModeOption.ItemSelected += id =>
		{
			ApplyMapModeSelection((MapMode)_mapModeOption.GetItemId((int)id), persist: true, applyRecommendedLayer: true);
		};

		_riverToggle.Toggled += value =>
		{
			EnableRivers = value;
			UpdateRiverDensityControlState();
			UpdateRiverLayerAvailability();
			SaveAdvancedSettings();
			GenerateWorld();
		};

		_compareToggle.Toggled += value =>
		{
			_compareMode = value;
			GenerateWorld();
		};

		SetRandomSeed();
		_seedSpin.Value = Seed;
		_seaLevelSlider.Value = SeaLevel;
		_heatSlider.Value = HeatFactor;
		_erosionSlider.Value = ErosionIterations;
		_riverDensitySlider.Value = RiverDensity;
		_windArrowDensitySlider.Value = WindArrowDensity;
		_basinSensitivitySlider.Value = BasinSensitivity;
		_interiorReliefSlider.Value = _interiorRelief;
		_orogenyStrengthSlider.Value = _orogenyStrength;
		_subductionArcRatioSlider.Value = _subductionArcRatio;
		_continentalAgeSlider.Value = _continentalAge;
		_magicSlider.Value = _magicDensity;
		_aggressionSlider.Value = _civilAggression;
		_diversitySlider.Value = _speciesDiversity;
		_uiFontScaleSlider.SetValueNoSignal(_uiFontScale * 100f);
		_timelineSlider.Value = _currentEpoch;
		UpdateTimelineReplayCursor(Array.Empty<CivilizationEpochEvent>());
		_riverToggle.ButtonPressed = EnableRivers;
		UpdateRiverDensityControlState();
		UpdateRiverLayerAvailability();
		_compareToggle.ButtonPressed = false;
		_compareToggle.Visible = false;
		_compareToggle.Disabled = true;
		_legendPanel.Visible = false;
		_biomeLegendPanel.Visible = false;
		_compareStatsLabel.Visible = false;
		_cityNamesLabel.Visible = false;
		_layerOption.Visible = false;
		_layerRow.Visible = true;
		_layerRow.ZIndex = 10;
		ApplySavedUiState();
		ApplyMapModeSelection(_mapMode, persist: false, applyRecommendedLayer: false);
		UpdateLayerQuickButtons();

		_mapTexture.MouseFilter = Control.MouseFilterEnum.Stop;
		_mapTexture.GuiInput += OnMapTextureGuiInput;
		_mapTexture.MouseExited += OnMapTextureMouseExited;

		UpdateLabels();
		UpdateLorePanel();
		RefreshCacheStatsLabel();
		InitializeOracleUI();
		GenerateWorld();
	}

	private void ToggleAdvancedSettings()
	{
		SetAdvancedSettingsPanelVisible(!_advancedSettingsPanel.Visible);
	}

	public override void _Input(InputEvent @event)
	{
		if (!_advancedSettingsPanel.Visible)
		{
			return;
		}

		if (@event is InputEventKey keyEvent &&
			keyEvent.Pressed &&
			!keyEvent.Echo &&
			keyEvent.Keycode == Key.Escape)
		{
			SetAdvancedSettingsPanelVisible(false);
			return;
		}

		if (@event is not InputEventMouseButton mouseButton ||
			mouseButton.ButtonIndex != MouseButton.Left ||
			!mouseButton.Pressed)
		{
			return;
		}

		var clickPosition = mouseButton.Position;
		if (IsPointInsideControl(_advancedSettingsPanel, clickPosition) ||
			IsPointInsideControl(_advancedSettingsButton, clickPosition))
		{
			return;
		}

		SetAdvancedSettingsPanelVisible(false);
	}

	private void SetAdvancedSettingsPanelVisible(bool visible, bool persist = true)
	{
		if (_advancedSettingsPanel.Visible == visible)
		{
			return;
		}

		_advancedSettingsPanel.Visible = visible;
		if (persist)
		{
			SaveAdvancedSettings();
		}
	}

	private void ToggleMountainControlGroup()
	{
		SetMountainControlExpanded(!_mountainControlExpanded);
	}

	private void SetMountainControlExpanded(bool expanded, bool persist = true)
	{
		_mountainControlExpanded = expanded;
		_mountainControlToggleButton.Text = expanded ? "收起山脉参数 ▲" : "展开山脉参数 ▼";
		UpdateMountainControlSummary();
		_mountainControlBody.Visible = expanded;
		_mountainControlSummaryLabel.Visible = !expanded;

		if (persist)
		{
			SaveAdvancedSettings();
		}
	}

	private void UpdateMountainControlSummary()
	{
		_mountainControlSummaryLabel.Text =
			$"起伏:{_interiorRelief:0.00} | 造山:{_orogenyStrength:0.00} | 俯冲:{_subductionArcRatio:0.00} | 年龄:{_continentalAge}";
	}

	private void ApplySavedUiState()
	{
		var savedMapSizeIndex = FindMapSizePresetIndex(MapWidth, MapHeight);
		if (savedMapSizeIndex >= 0)
		{
			_suppressMapSizeSelectionHandler = true;
			_mapSizeOption.Select(savedMapSizeIndex);
			_suppressMapSizeSelectionHandler = false;
			_lastConfirmedMapSizeIndex = savedMapSizeIndex;
		}

		var layerIndex = _layerOption.GetItemIndex(_preferredLayerId);
		var canUsePreferredLayer = layerIndex >= 0 && !_layerOption.IsItemDisabled(layerIndex);
		_magicSlider.SetValueNoSignal(_magicDensity);
		_aggressionSlider.SetValueNoSignal(_civilAggression);
		_diversitySlider.SetValueNoSignal(_speciesDiversity);
		_timelineSlider.SetValueNoSignal(_currentEpoch);
		SelectMapModeOption(_mapMode);
		SelectLayerById(canUsePreferredLayer ? _preferredLayerId : (int)MapLayer.Satellite, persist: false);
		SetAdvancedSettingsPanelVisible(_preferredAdvancedPanelVisible, persist: false);
		SetMountainControlExpanded(_mountainControlExpanded, persist: false);
	}

	private static bool IsPointInsideControl(Control control, Vector2 point)
	{
		return control.Visible && control.GetGlobalRect().HasPoint(point);
	}


}
