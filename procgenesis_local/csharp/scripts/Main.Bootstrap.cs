using Godot;
using PlanetGeneration.WorldGen;
using PlanetGeneration.UI;
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
	private PlanetGeneration.UI.GeneratorControlsController? _controlsController;

	public override void _Ready()
	{
		var headerController = GetNodeOrNull<MainHeaderController>("MainLayout/ConsolePanel/ConsoleVBox/HeaderPanel");
		var controlsController = GetNodeOrNull<GeneratorControlsController>("MainLayout/ConsolePanel/ConsoleVBox/ConsoleTabs");
		_controlsController = controlsController;
		_mapTexture = GetNodeByName<TextureRect>("MapTexture");
		_generateButton = headerController?.GenerateButton
			?? GetNodeByName<Button>("GenerateButton");
		_randomButton = controlsController?.RandomButton ?? GetNodeByName<Button>("RandomButton");
		_seedSpin = controlsController?.SeedSpin ?? GetNodeByName<SpinBox>("SeedSpin");
		_seaLevelSlider = controlsController?.SeaLevelSlider ?? GetNodeByName<HSlider>("SeaLevelSlider");
		_heatSlider = controlsController?.HeatSlider ?? GetNodeByName<HSlider>("HeatSlider");
		_erosionSlider = controlsController?.ErosionSlider ?? GetNodeByName<HSlider>("ErosionSlider");
		_seaLevelValue = controlsController?.SeaLevelValue ?? GetNodeByName<Label>("SeaLevelValue");
		_heatValue = controlsController?.HeatValue ?? GetNodeByName<Label>("HeatValue");
		_erosionValue = controlsController?.ErosionValue ?? GetNodeByName<Label>("ErosionValue");
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
		_advancedSettingsButton = headerController?.AdvancedSettingsButton ?? GetNodeByName<Button>("AdvancedSettingsButton");
		_resetAdvancedSettingsButton = GetNodeByName<Button>("ResetAdvancedSettingsButton");
		_persistCacheGroupButton = GetNodeByName<Button>("PersistCacheGroupButton");
		_clearCacheButton = GetNodeByName<Button>("ClearCacheButton");
		_riverToggle = GetNodeByName<BaseButton>("RiversSwitch");
		_compareToggle = GetNodeByName<BaseButton>("CompareToggle");
		_exportPngButton = headerController?.ExportPngButton ?? GetNodeByName<Button>("ExportPngButton");
		_exportJsonButton = headerController?.ExportJsonButton ?? GetNodeByName<Button>("ExportJsonButton");
		_themeToggleButton = headerController?.ThemeToggleButton ?? GetNodeByName<Button>("ThemeToggleButton");
		_generateProgress = GetNodeByName<ProgressBar>("GenerateProgress");
		_progressStatus = GetNodeByName<Label>("ProgressStatus");
		_cacheStatsLabel = GetNodeByName<Label>("CacheStatsLabel");
		_progressOverlay = GetNodeByName<Control>("ProgressOverlay");
		_layerTree = GetNodeByName<Tree>("LayerTree");
		_mapCenter = GetNodeByName<Control>("MapCenter");
		_mapRoot = GetNodeByName<Control>("MapRoot");
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
		_consolePanel = GetNodeByName<Control>("ConsolePanel");
		_consoleCollapseTab = GetNodeByName<Button>("ConsoleCollapseTab");
		_consoleSummonTab = GetNodeByName<Button>("ConsoleSummonTab");
		_minimapPanel = GetNodeByName<Control>("MinimapPanel");
		_minimapTexture = GetNodeByName<TextureRect>("MinimapTexture");
		_minimapViewRect = GetNodeByName<ReferenceRect>("MinimapViewRect");

		_generateProgress.Value = 0;
		_progressStatus.Text = "待命";
		_progressOverlay.Visible = false;
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
		ApplyConsolePanelVisibility();

		_consoleCollapseTab.Pressed += OnConsoleCollapseTabPressed;
		_consoleSummonTab.Pressed += OnConsoleSummonTabPressed;
		_minimapTexture.GuiInput += OnMinimapGuiInput;
		_minimapTexture.MouseExited += () => _minimapDragging = false;

		SetupLayerOptions();
		SetupMapSizeOptions();
		SetupContinentCountOptions();
		SetupTerrainPresetOptions();
		SetupMountainPresetOptions();
		SetupElevationStyleOptions();
		SetupArchiveOptions();
		DisableMouseWheelForAllSliders();
		CaptureUiFontSizeBaselines();
		ApplyUiFontScale();

		if (headerController != null)
		{
			headerController.GenerateRequested += OnGeneratePressed;
			headerController.AdvancedSettingsRequested += ShowAdvancedSettingsPage;
			headerController.ExportPngRequested += OnExportPngPressed;
			headerController.ExportJsonRequested += OnExportJsonPressed;
			headerController.ThemeToggleRequested += OnThemeTogglePressed;
		}
		else
		{
			_generateButton.Pressed += OnGeneratePressed;
			_advancedSettingsButton.Pressed += ShowAdvancedSettingsPage;
		}
		_resetAdvancedSettingsButton.Pressed += OnResetAdvancedSettingsPressed;
		if (headerController == null)
		{
			_exportPngButton.Pressed += OnExportPngPressed;
			_exportJsonButton.Pressed += OnExportJsonPressed;
			_themeToggleButton.Pressed += OnThemeTogglePressed;
		}
		_persistCacheGroupButton.Pressed += OnSaveArchivePressed;
		_clearCacheButton.Pressed += OnClearCachePressed;

		if (controlsController != null)
		{
			controlsController.ApplyRequested += OnGeneratePressed;
			controlsController.RandomRequested += OnRandomPressed;
			controlsController.SeaLevelChanged += OnSeaLevelChanged;
			controlsController.HeatChanged += OnHeatChanged;
			controlsController.ErosionChanged += OnErosionChanged;
			controlsController.InteriorReliefChanged += OnInteriorReliefChanged;
			controlsController.OrogenyStrengthChanged += OnOrogenyStrengthChanged;
			controlsController.SubductionArcRatioChanged += OnSubductionArcRatioChanged;
			controlsController.ContinentalAgeChanged += OnContinentalAgeChanged;
			controlsController.RiversToggled += OnRiversToggled;
			controlsController.RiverDensityChanged += OnRiverDensityChanged;
			SetupLeftPanelSwitches();
		}
		else
		{
			_randomButton.Pressed += OnRandomPressed;
			_seaLevelSlider.ValueChanged += OnSeaLevelChanged;
			_heatSlider.ValueChanged += OnHeatChanged;
			_erosionSlider.ValueChanged += OnErosionChanged;
			_riverToggle.Toggled += OnRiversToggled;
			_riverDensitySlider.ValueChanged += OnRiverDensityChanged;
			_interiorReliefSlider.ValueChanged += OnInteriorReliefChanged;
			_orogenyStrengthSlider.ValueChanged += OnOrogenyStrengthChanged;
			_subductionArcRatioSlider.ValueChanged += OnSubductionArcRatioChanged;
			_continentalAgeSlider.ValueChanged += OnContinentalAgeChanged;
		}
		_magicSlider.ValueChanged += OnMagicDensityChanged;
		_aggressionSlider.ValueChanged += OnCivilAggressionChanged;
		_diversitySlider.ValueChanged += OnSpeciesDiversityChanged;
		_windArrowDensitySlider.ValueChanged += OnWindArrowDensityChanged;
		_basinSensitivitySlider.ValueChanged += OnBasinSensitivityChanged;
		_uiFontScaleSlider.ValueChanged += OnUiFontScaleChanged;
		_timelineSlider.ValueChanged += OnTimelineChanged;
		_prevEpochButton.Pressed += OnPrevEpochPressed;
		_nextEpochButton.Pressed += OnNextEpochPressed;
		_layerOption.ItemSelected += _ =>
		{
			RedrawCurrentLayer();
			SyncLayerTreeSelection();
			SaveAdvancedSettings();
		};

		_layerTree.ItemSelected += OnLayerTreeItemSelected;

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
		_riverToggle.SetPressedNoSignal(EnableRivers);
		UpdateRiverDensityControlState();
		UpdateRiverLayerAvailability();
		_compareToggle.ButtonPressed = false;
		_legendPanel.Visible = false;
		_biomeLegendPanel.Visible = false;
		_compareStatsLabel.Visible = false;
		_cityNamesLabel.Visible = false;
		_layerOption.Visible = false;
		ApplySavedUiState();
		SyncLayerTreeSelection();

		_mapTexture.MouseFilter = Control.MouseFilterEnum.Stop;
		_mapTexture.GuiInput += OnMapTextureGuiInput;
		_mapTexture.MouseExited += OnMapTextureMouseExited;

		ConnectMainMenu();
		ThemeManager.Instance?.RefreshTheme();
		UpdateLabels();
		UpdateLorePanel();
		RefreshCacheStatsLabel();
		InitializeOracleUI();
		GenerateWorld();
	}

	private void SetupLeftPanelSwitches()
	{
		var epochReplaySwitch = GetNodeByName<BaseButton>("EpochReplaySwitch");
		epochReplaySwitch.SetPressedNoSignal(true);
		epochReplaySwitch.Toggled += value =>
		{
			_timelineSlider.Editable = value;
			_prevEpochButton.Disabled = !value;
			_nextEpochButton.Disabled = !value;
			SaveAdvancedSettings();
		};
	}

	private void ShowAdvancedSettingsPage()
	{
		SetConsolePanelVisible(true);
		_controlsController?.ShowPage(2);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey mapZoomKey && mapZoomKey.Pressed && !mapZoomKey.Echo)
		{
			if (mapZoomKey.Keycode == Key.Equal || mapZoomKey.Keycode == Key.KpAdd)
			{
				SetMapZoom(_mapZoom + MapZoomStep);
				GetViewport().SetInputAsHandled();
				return;
			}
			if (mapZoomKey.Keycode == Key.Minus || mapZoomKey.Keycode == Key.KpSubtract)
			{
				SetMapZoom(_mapZoom - MapZoomStep);
				GetViewport().SetInputAsHandled();
				return;
			}
			if (mapZoomKey.Keycode == Key.Key0 || mapZoomKey.Keycode == Key.Kp0)
			{
				SetMapZoom(1.0f);
				GetViewport().SetInputAsHandled();
				return;
			}
		}

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
		SelectLayerById(canUsePreferredLayer ? _preferredLayerId : (int)MapLayer.Satellite, persist: false);
	}

	private static bool IsPointInsideControl(Control control, Vector2 point)
	{
		return control.Visible && control.GetGlobalRect().HasPoint(point);
	}


}
