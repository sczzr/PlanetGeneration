using Godot;
using PlanetGeneration.UI;

namespace PlanetGeneration;

public partial class Main : Control
{
	// The layout lives entirely in MainLayout.tscn (container-driven);
	// Main only wires composed sub-scenes to the application behavior.

	private WorldSetupController? _worldSetupMenu;


	private void ConnectMainMenu()
	{
		var menu = GetNodeOrNull<MainMenu>("MainMenu")
			?? GetNodeOrNull<MainMenu>("MainLayout/MainMenu")
			?? FindChild("MainMenu", true, false) as MainMenu;

		_worldSetupMenu = GetNodeOrNull<WorldSetupController>("WorldSetupMenu")
			?? GetNodeOrNull<WorldSetupController>("MainLayout/WorldSetupMenu")
			?? FindChild("WorldSetupMenu", true, false) as WorldSetupController;

		if (menu != null)
		{
			menu.ContinueRequested += OnContinueRequested;
			menu.NewWorldRequested += OnNewWorldRequested;
			menu.SettingsRequested += ShowAdvancedSettingsPage;
		}

		if (_worldSetupMenu != null)
		{
			_worldSetupMenu.GenerateRequested += OnWorldSetupGenerate;
			_worldSetupMenu.BackRequested += OnWorldSetupBack;
			_worldSetupMenu.RandomRequested += OnWorldSetupRandom;
		}
	}

	private void OnContinueRequested()
	{
		if (_primaryWorld == null)
		{
			OnNewWorldRequested();
		}
		else
		{
			SetConsolePanelVisible(true);
		}
	}

	private void OnNewWorldRequested()
	{
		var menu = GetNodeOrNull<MainMenu>("MainMenu")
			?? GetNodeOrNull<MainMenu>("MainLayout/MainMenu")
			?? FindChild("MainMenu", true, false) as MainMenu;
		menu?.Hide();

		if (_consolePanel != null)
		{
			_consolePanel.Visible = false;
		}
		if (_consoleSummonTab != null)
		{
			_consoleSummonTab.Visible = false;
		}

		_worldSetupMenu?.SyncFromMain(
			PlateCount,
			_terrainOceanicRatio,
			_terrainContinentBias,
			WindCellCount,
			MoistureIterations,
			BasinSensitivity,
			_magicDensity,
			_civilAggression,
			_speciesDiversity,
			_currentEpoch,
			_polygonTileMode);

		_worldSetupMenu?.Open();
	}

	private void OnWorldSetupBack()
	{
		_worldSetupMenu?.Close();
		var menu = GetNodeOrNull<MainMenu>("MainMenu")
			?? GetNodeOrNull<MainMenu>("MainLayout/MainMenu")
			?? FindChild("MainMenu", true, false) as MainMenu;
		menu?.Show();
	}

	private void OnWorldSetupRandom()
	{
		SetRandomSeed();
		if (_seedSpin != null)
		{
			_seedSpin.Value = Seed;
		}
	}

	private void OnWorldSetupGenerate()
	{
		_worldSetupMenu?.ApplyToMain(this);
		_worldSetupMenu?.Close();
		OnGeneratePressed();
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
		PlanetGeneration.WorldGen.Polygon.PolygonTileMode tileMode)
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
