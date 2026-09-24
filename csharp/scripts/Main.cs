using Godot;
using PlanetGeneration.UI;
using PlanetGeneration.WorldGen;
using PlanetGeneration.UI.State;

namespace PlanetGeneration;

public partial class Main : Control
{
	// The layout lives entirely in MainLayout.tscn (container-driven);
	// Main only wires composed sub-scenes to the application behavior.

	private Control? _mainMenu;
	private Control? _pauseMenu;
	private WorldSetupController? _worldSetupMenu;

	private void ConnectMainMenu()
	{
		_mainMenu = GetNodeOrNull<Control>("MainMenu")
			?? GetNodeOrNull<Control>("MainLayout/MainMenu")
			?? FindChild("MainMenu", true, false) as Control;

		_pauseMenu = GetNodeOrNull<Control>("OverlayLayer/PauseMenu")
			?? FindChild("PauseMenu", true, false) as Control;

		_worldSetupMenu = GetNodeOrNull<WorldSetupController>("WorldSetupMenu")
			?? GetNodeOrNull<WorldSetupController>("MainLayout/WorldSetupMenu")
			?? FindChild("WorldSetupMenu", true, false) as WorldSetupController;

		if (_mainMenu != null)
		{
			_mainMenu.Connect("new_game_requested", Callable.From<StringName, Godot.Collections.Dictionary>(OnNewGameRequested));
			_mainMenu.Connect("load_game_requested", Callable.From(OnLoadFromMenuRequested));
			if (_mainMenu.HasSignal("back_to_game_requested"))
			{
				_mainMenu.Connect("back_to_game_requested", Callable.From(OnBackToGameRequested));
			}
		}

		if (_pauseMenu != null)
		{
			_pauseMenu.Connect("main_menu_requested", Callable.From(OnPauseMainMenuRequested));
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("game_pause"))
		{
			if (_mainMenu == null || !_mainMenu.Visible)
			{
				if (_pauseMenu != null && IsInstanceValid(_pauseMenu))
				{
					GetViewport().SetInputAsHandled();
					_pauseMenu.Call("open");
				}
			}
		}
	}

	private void SetInGameHudVisible(bool visible)
	{
		if (!visible)
		{
			if (_minimapPanel != null && IsInstanceValid(_minimapPanel))
			{
				_minimapPanel.Visible = false;
			}
			if (_biomeLegendPanel != null && IsInstanceValid(_biomeLegendPanel))
			{
				_biomeLegendPanel.Visible = false;
			}
			if (_legendPanel != null && IsInstanceValid(_legendPanel))
			{
				_legendPanel.Visible = false;
			}
			if (_biomeHoverPanel != null && IsInstanceValid(_biomeHoverPanel))
			{
				_biomeHoverPanel.Visible = false;
			}
		}
		else
		{
			if (_primaryWorld != null)
			{
				if (_minimapPanel != null && IsInstanceValid(_minimapPanel) && _minimapTexture?.Texture != null)
				{
					_minimapPanel.Visible = true;
				}
				if (_layerOption != null && IsInstanceValid(_layerOption))
				{
					UpdateLegend((MapLayer)_layerOption.GetSelectedId());
				}
			}
		}
	}

	private void OnPauseMainMenuRequested()
	{
		SetConsolePanelVisible(false);
		SetInGameHudVisible(false);
		if (_mainMenu != null && IsInstanceValid(_mainMenu))
		{
			_mainMenu.Visible = true;
			_mainMenu.Call("_restore_menu_column");
		}
	}

	private void OnBackToGameRequested()
	{
		if (_primaryWorld != null)
		{
			SetConsolePanelVisible(true);
			SetInGameHudVisible(true);
		}
	}

	public void OnNewWorldRequested()
	{
		SetConsolePanelVisible(false, animated: false);
		SetInGameHudVisible(false);
		if (_consoleSummonTab != null)
		{
			_consoleSummonTab.Visible = false;
		}

		if (_mainMenu != null && IsInstanceValid(_mainMenu))
		{
			_mainMenu.Visible = true;
			if (_mainMenu.HasMethod("open_world_config"))
			{
				_mainMenu.Call("open_world_config", _primaryWorld != null);
			}
			else
			{
				_mainMenu.Call("_restore_menu_column");
			}
		}
	}

	private void OnNewGameRequested(StringName mode, Godot.Collections.Dictionary config)
	{
		if (mode != "guard" && mode != "Guard")
		{
			return;
		}
		ApplyWorldConfigDictionary(config);
		GenerateWorld();
	}

	public void ApplyWorldConfigDictionary(Godot.Collections.Dictionary config)
	{
		if (config == null) return;

		if (config.TryGetValue("seed", out var seedVal))
		{
			var s = (int)seedVal;
			if (s >= 0)
			{
				Seed = s;
			}
			else
			{
				SetRandomSeed();
			}
			if (_seedSpin != null) _seedSpin.SetValueNoSignal(Seed);
		}

		if (config.TryGetValue("width", out var wVal) && config.TryGetValue("height", out var hVal))
		{
			MapWidth = (int)wVal;
			MapHeight = (int)hVal;
		}

		if (config.TryGetValue("world_type", out var typeVal))
		{
			var typeStr = typeVal.AsString();
			_terrainMorphology = typeStr switch
			{
				"Supercontinent" => TerrainMorphology.Supercontinent,
				"ClassicArchipelago" => TerrainMorphology.Archipelago,
				"BrokenIslandChain" => TerrainMorphology.FracturedIslands,
				"ShallowSea" => TerrainMorphology.ShallowFragments,
				"Continents" => TerrainMorphology.Continents,
				"Archipelago" => TerrainMorphology.Archipelago,
				"FracturedIslands" => TerrainMorphology.FracturedIslands,
				"ShallowFragments" => TerrainMorphology.ShallowFragments,
				"ColdContinent" => TerrainMorphology.ColdContinent,
				"HotWasteland" => TerrainMorphology.HotWasteland,
				"PolarIcelands" => TerrainMorphology.PolarIcelands,
				"AtollChain" => TerrainMorphology.AtollChain,
				"InlandSea" => TerrainMorphology.InlandSea,
				"RiftHighlands" => TerrainMorphology.RiftHighlands,
				_ => TerrainMorphology.Balanced
			};
		}

		if (config.TryGetValue("sea_level", out var seaVal))
		{
			SeaLevel = Mathf.Clamp((float)seaVal, 0.1f, 0.9f);
			if (_seaLevelSlider != null) _seaLevelSlider.SetValueNoSignal(SeaLevel);
			if (_seaLevelValue != null) _seaLevelValue.Text = SeaLevel.ToString("0.00");
		}

		if (config.TryGetValue("heat", out var heatVal))
		{
			HeatFactor = Mathf.Clamp((float)heatVal, 0.01f, 1f);
			if (_heatSlider != null) _heatSlider.SetValueNoSignal(HeatFactor);
			if (_heatValue != null) _heatValue.Text = HeatFactor.ToString("0.00");
		}

		if (config.TryGetValue("moisture", out var moistVal))
		{
			MoistureFactor = Mathf.Clamp((float)moistVal, 0.2f, 2.5f);
			if (_moistureSlider != null) _moistureSlider.SetValueNoSignal(MoistureFactor);
			if (_moistureValue != null) _moistureValue.Text = MoistureFactor.ToString("0.00");
		}

		if (config.TryGetValue("river_density", out var riverVal))
		{
			RiverDensity = Mathf.Clamp((float)riverVal, 0.4f, 2.5f);
			if (_riverDensitySlider != null) _riverDensitySlider.SetValueNoSignal(RiverDensity);
			if (_riverDensityValue != null) _riverDensityValue.Text = RiverDensity.ToString("0.00");
		}

		if (config.TryGetValue("generate_rivers", out var genRiverVal))
		{
			EnableRivers = (bool)genRiverVal;
			if (_riverToggle != null) _riverToggle.ButtonPressed = EnableRivers;
		}
		else if (config.TryGetValue("enable_rivers", out var enRiverVal))
		{
			EnableRivers = (bool)enRiverVal;
			if (_riverToggle != null) _riverToggle.ButtonPressed = EnableRivers;
		}

		if (config.TryGetValue("plate_count", out var plateVal))
		{
			PlateCount = Mathf.Clamp((int)plateVal, 5, 60);
		}

		if (config.TryGetValue("oceanic_ratio", out var oceanRatioVal))
		{
			_terrainOceanicRatio = Mathf.Clamp((float)oceanRatioVal, 0.1f, 0.9f);
		}

		if (config.TryGetValue("continent_bias", out var contBiasVal))
		{
			_terrainContinentBias = Mathf.Clamp((float)contBiasVal, 0f, 0.6f);
		}

		if (config.TryGetValue("landform_relief", out var reliefVal))
		{
			_interiorRelief = Mathf.Clamp((float)reliefVal, 0.3f, 1.5f);
			if (_interiorReliefSlider != null) _interiorReliefSlider.SetValueNoSignal(_interiorRelief);
			if (_interiorReliefValue != null) _interiorReliefValue.Text = _interiorRelief.ToString("0.00");
		}

		if (config.TryGetValue("mountain_range_scale", out var mtScaleVal))
		{
			_orogenyStrength = Mathf.Clamp((float)mtScaleVal, 0.5f, 2.5f);
			if (_orogenyStrengthSlider != null) _orogenyStrengthSlider.SetValueNoSignal(_orogenyStrength);
			if (_orogenyStrengthValue != null) _orogenyStrengthValue.Text = _orogenyStrength.ToString("0.00");
		}

		var lfTuning = LandformTuning;
		if (config.TryGetValue("wetland_abundance", out var wetVal))
			lfTuning = lfTuning with { WetlandAbundance = Mathf.Clamp((float)wetVal, 0.2f, 2.5f) };
		if (config.TryGetValue("canyon_depth", out var canVal))
			lfTuning = lfTuning with { CanyonDepth = Mathf.Clamp((float)canVal, 0.2f, 2.5f) };
		if (config.TryGetValue("delta_scale", out var delVal))
			lfTuning = lfTuning with { DeltaScale = Mathf.Clamp((float)delVal, 0.2f, 2.5f) };
		if (config.TryGetValue("karst_frequency", out var karVal))
			lfTuning = lfTuning with { KarstFrequency = Mathf.Clamp((float)karVal, 0.2f, 2.5f) };
		if (config.TryGetValue("desert_dune_scale", out var desVal))
			lfTuning = lfTuning with { DesertDuneScale = Mathf.Clamp((float)desVal, 0.2f, 2.5f) };
		if (config.TryGetValue("badlands_frequency", out var badVal))
			lfTuning = lfTuning with { BadlandsFrequency = Mathf.Clamp((float)badVal, 0.2f, 2.5f) };
		if (config.TryGetValue("glacier_extent", out var glaVal))
			lfTuning = lfTuning with { GlacierExtent = Mathf.Clamp((float)glaVal, 0.2f, 2.5f) };
		if (config.TryGetValue("fjord_depth", out var fjoVal))
			lfTuning = lfTuning with { FjordDepth = Mathf.Clamp((float)fjoVal, 0.2f, 2.5f) };
		if (config.TryGetValue("peak_frequency", out var peakVal))
			lfTuning = lfTuning with { PeakFrequency = Mathf.Clamp((float)peakVal, 0.2f, 2.5f) };
		if (config.TryGetValue("island_density", out var islVal))
			lfTuning = lfTuning with { IslandDensity = Mathf.Clamp((float)islVal, 0.2f, 2.5f) };
		if (config.TryGetValue("floodplain_scale", out var fpVal))
			lfTuning = lfTuning with { FloodplainScale = Mathf.Clamp((float)fpVal, 0.2f, 2.5f) };
		if (config.TryGetValue("volcano_frequency", out var volVal))
			lfTuning = lfTuning with { VolcanoFrequency = Mathf.Clamp((float)volVal, 0.2f, 2.5f) };
		if (config.TryGetValue("rift_frequency", out var riftVal))
			lfTuning = lfTuning with { RiftFrequency = Mathf.Clamp((float)riftVal, 0.2f, 2.5f) };
		if (config.TryGetValue("plateau_extent", out var platVal))
			lfTuning = lfTuning with { PlateauExtent = Mathf.Clamp((float)platVal, 0.2f, 2.5f) };
		if (config.TryGetValue("basin_sensitivity", out var basinSensVal))
		{
			BasinSensitivity = Mathf.Clamp((float)basinSensVal, 0.5f, 2.0f);
			lfTuning = lfTuning with { BasinSensitivity = BasinSensitivity };
		}
		else if (config.TryGetValue("basin_count", out var basinVal))
		{
			BasinSensitivity = Mathf.Clamp((float)basinVal, 0.5f, 2.0f);
			lfTuning = lfTuning with { BasinSensitivity = BasinSensitivity };
		}
		LandformTuning = lfTuning;

		if (config.TryGetValue("continent_count", out var contCountVal))
		{
			_continentCount = Mathf.Clamp((int)contCountVal, 1, 10);
		}
		else if (config.TryGetValue("mountain_range_count", out var mtRangeCountVal))
		{
			_continentCount = Mathf.Clamp((int)mtRangeCountVal, 1, 10);
		}

		if (config.TryGetValue("mountain_gradient", out var mtGradVal))
		{
			var grad = Mathf.Clamp((float)mtGradVal, 1f, 5f);
			_interiorRelief = 0.5f + (grad - 1f) * 0.25f;
			if (_interiorReliefSlider != null) _interiorReliefSlider.SetValueNoSignal(_interiorRelief);
			if (_interiorReliefValue != null) _interiorReliefValue.Text = _interiorRelief.ToString("0.00");
		}

		if (config.TryGetValue("elevation", out var elevVal))
		{
			_continentalAge = Mathf.Clamp(Mathf.RoundToInt((float)elevVal * 100f), 0, 100);
		}

		if (config.TryGetValue("rock_debris_frequency", out var erosionVal))
		{
			ErosionIterations = Mathf.Clamp((int)erosionVal, 0, 20);
			if (_erosionSlider != null) _erosionSlider.SetValueNoSignal(ErosionIterations);
			if (_erosionValue != null) _erosionValue.Text = ErosionIterations.ToString("0");
		}

		if (config.TryGetValue("magic_density", out var magicVal))
		{
			_magicDensity = Mathf.Clamp((int)magicVal, 0, 100);
			if (_magicSlider != null) _magicSlider.SetValueNoSignal(_magicDensity);
		}

		if (config.TryGetValue("civil_aggression", out var aggrVal))
		{
			_civilAggression = Mathf.Clamp((int)aggrVal, 0, 100);
			if (_aggressionSlider != null) _aggressionSlider.SetValueNoSignal(_civilAggression);
		}

		if (config.TryGetValue("species_diversity", out var divVal))
		{
			_speciesDiversity = Mathf.Clamp((int)divVal, 0, 100);
			if (_diversitySlider != null) _diversitySlider.SetValueNoSignal(_speciesDiversity);
		}

		if (config.TryGetValue("initial_epoch", out var epochVal))
		{
			_currentEpoch = Mathf.Clamp((int)epochVal, 50, 1000);
		}

		if (config.TryGetValue("polygon_tile_mode", out var tileModeVal))
		{
			var idx = (int)tileModeVal;
			_polygonTileMode = idx switch
			{
				0 => PolygonTileMode.Cells,
				1 => PolygonTileMode.Outlined,
				2 => PolygonTileMode.Hybrid,
				3 => PolygonTileMode.Raster,
				_ => PolygonTileMode.Cells
			};
		}
	}

	public void ApplyWorldSetupSettings(
		int plateCount,
		float oceanicRatio,
		float continentBias,
		int windCellCount,
		int moistureIterations,
		float basinSensitivity,
		int magicDensity,
		int civilAggression,
		int speciesDiversity,
		int initialEpoch,
		PolygonTileMode tileMode,
		float? seaLevel = null,
		float? heatFactor = null,
		float? moistureFactor = null,
		int? erosionIterations = null,
		float? riverDensity = null,
		bool? enableRivers = null,
		float? interiorRelief = null,
		float? orogenyStrength = null,
		float? subductionArcRatio = null,
		int? continentalAge = null)
	{
		PlateCount = plateCount;
		_terrainOceanicRatio = oceanicRatio;
		_terrainContinentBias = continentBias;
		WindCellCount = windCellCount;
		MoistureIterations = moistureIterations;
		BasinSensitivity = basinSensitivity;
		_magicDensity = magicDensity;
		_civilAggression = civilAggression;
		_speciesDiversity = speciesDiversity;
		_currentEpoch = initialEpoch;
		_polygonTileMode = tileMode;

		if (seaLevel.HasValue)
		{
			SeaLevel = Mathf.Clamp(seaLevel.Value, 0.1f, 0.9f);
			if (_seaLevelSlider != null) _seaLevelSlider.SetValueNoSignal(SeaLevel);
			if (_seaLevelValue != null) _seaLevelValue.Text = SeaLevel.ToString("0.00");
		}
		if (heatFactor.HasValue)
		{
			HeatFactor = Mathf.Clamp(heatFactor.Value, 0.01f, 1f);
			if (_heatSlider != null) _heatSlider.SetValueNoSignal(HeatFactor);
			if (_heatValue != null) _heatValue.Text = HeatFactor.ToString("0.00");
		}
		if (moistureFactor.HasValue)
		{
			MoistureFactor = Mathf.Clamp(moistureFactor.Value, 0.2f, 2.5f);
			if (_moistureSlider != null) _moistureSlider.SetValueNoSignal(MoistureFactor);
			if (_moistureValue != null) _moistureValue.Text = MoistureFactor.ToString("0.00");
		}
		if (erosionIterations.HasValue)
		{
			ErosionIterations = Mathf.Clamp(erosionIterations.Value, 0, 20);
			if (_erosionSlider != null) _erosionSlider.SetValueNoSignal(ErosionIterations);
			if (_erosionValue != null) _erosionValue.Text = ErosionIterations.ToString();
		}
		if (riverDensity.HasValue)
		{
			RiverDensity = Mathf.Clamp(riverDensity.Value, 0.4f, 2.5f);
			if (_riverDensitySlider != null) _riverDensitySlider.SetValueNoSignal(RiverDensity);
			if (_riverDensityValue != null) _riverDensityValue.Text = RiverDensity.ToString("0.00");
		}
		if (enableRivers.HasValue)
		{
			EnableRivers = enableRivers.Value;
			if (_riverToggle != null) _riverToggle.ButtonPressed = EnableRivers;
		}
		if (interiorRelief.HasValue)
		{
			_interiorRelief = Mathf.Clamp(interiorRelief.Value, 0.5f, 2.0f);
			if (_interiorReliefSlider != null) _interiorReliefSlider.SetValueNoSignal(_interiorRelief);
			if (_interiorReliefValue != null) _interiorReliefValue.Text = _interiorRelief.ToString("0.00");
		}
		if (orogenyStrength.HasValue)
		{
			_orogenyStrength = Mathf.Clamp(orogenyStrength.Value, 0.2f, 2.5f);
			if (_orogenyStrengthSlider != null) _orogenyStrengthSlider.SetValueNoSignal(_orogenyStrength);
			if (_orogenyStrengthValue != null) _orogenyStrengthValue.Text = _orogenyStrength.ToString("0.00");
		}
		if (subductionArcRatio.HasValue)
		{
			_subductionArcRatio = Mathf.Clamp(subductionArcRatio.Value, 0.1f, 1.0f);
			if (_subductionArcRatioSlider != null) _subductionArcRatioSlider.SetValueNoSignal(_subductionArcRatio);
			if (_subductionArcRatioValue != null) _subductionArcRatioValue.Text = _subductionArcRatio.ToString("0.00");
		}
		if (continentalAge.HasValue)
		{
			_continentalAge = Mathf.Clamp(continentalAge.Value, 10, 100);
			if (_continentalAgeSlider != null) _continentalAgeSlider.SetValueNoSignal(_continentalAge);
			if (_continentalAgeValue != null) _continentalAgeValue.Text = _continentalAge.ToString();
		}

		if (_magicSlider != null) _magicSlider.SetValueNoSignal(_magicDensity);
		if (_magicValue != null) _magicValue.Text = _magicDensity.ToString();
		if (_aggressionSlider != null) _aggressionSlider.SetValueNoSignal(_civilAggression);
		if (_aggressionValue != null) _aggressionValue.Text = _civilAggression.ToString();
		if (_diversitySlider != null) _diversitySlider.SetValueNoSignal(_speciesDiversity);
		if (_diversityValue != null) _diversityValue.Text = _speciesDiversity.ToString();
		if (_basinSensitivitySlider != null) _basinSensitivitySlider.SetValueNoSignal(BasinSensitivity);
		if (_basinSensitivityValue != null) _basinSensitivityValue.Text = BasinSensitivity.ToString("0.00");
		if (_timelineSlider != null) _timelineSlider.SetValueNoSignal(_currentEpoch);
		if (_epochLabel != null) _epochLabel.Text = $"第 {_currentEpoch} 纪元";
	}
}
