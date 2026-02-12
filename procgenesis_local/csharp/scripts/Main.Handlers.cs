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
	private void OnGeneratePressed()
	{
		Seed = (int)_seedSpin.Value;
		ApplyMapSizePreset(_mapSizeOption.GetSelectedId());
		ShowInfoPointHint();
		GenerateWorld();
	}

	private void OnRandomPressed()
	{
		SetRandomSeed();
		_seedSpin.Value = Seed;
		GenerateWorld();
	}

	private void SetRandomSeed()
	{
		var rng = new Godot.RandomNumberGenerator();
		rng.Randomize();
		Seed = rng.RandiRange(int.MinValue, int.MaxValue);
	}

	private void RandomizeReliefExaggeration()
	{
		var rng = new Godot.RandomNumberGenerator();
		rng.Seed = (ulong)(uint)(Seed ^ unchecked((int)0x5bd1e995));
		_currentReliefExaggeration = rng.RandfRange(ReliefExaggerationMin, ReliefExaggerationMax);
	}

	private void OnSeaLevelChanged(double value)
	{
		SeaLevel = Mathf.Clamp((float)value, 0.1f, 0.9f);
		UpdateLabels();
		GenerateWorld();
	}

	private void OnHeatChanged(double value)
	{
		HeatFactor = Mathf.Clamp((float)value, 0.01f, 1f);
		UpdateLabels();
		GenerateWorld();
	}

	private void OnErosionChanged(double value)
	{
		ErosionIterations = Mathf.Clamp((int)Mathf.Round((float)value), 0, 20);
		UpdateLabels();
		GenerateWorld();
	}

	private void OnRiverDensityChanged(double value)
	{
		RiverDensity = Mathf.Clamp((float)value, 0.4f, 2.5f);
		SaveAdvancedSettings();
		UpdateLabels();
		GenerateWorld();
	}

	private void UpdateRiverDensityControlState()
	{
		var isEnabled = EnableRivers;
		_riverDensitySlider.Editable = isEnabled;
		_riverDensitySlider.Modulate = isEnabled
			? Colors.White
			: new Color(0.65f, 0.70f, 0.78f, 0.72f);
		_riverDensityValue.Modulate = isEnabled
			? Colors.White
			: new Color(0.68f, 0.73f, 0.80f, 0.9f);
	}

	private void UpdateRiverLayerAvailability()
	{
		const int riverLayerId = (int)MapLayer.Rivers;
		var riverEnabled = EnableRivers;

		var riverItemIndex = _layerOption.GetItemIndex(riverLayerId);
		if (riverItemIndex >= 0)
		{
			_layerOption.SetItemDisabled(riverItemIndex, !riverEnabled);
		}

		if (_layerButtonsById.TryGetValue(riverLayerId, out var riverButton))
		{
			riverButton.Disabled = !riverEnabled;
		}

		if (!riverEnabled && _layerOption.GetSelectedId() == riverLayerId)
		{
			SelectLayerById((int)MapLayer.Satellite);
			return;
		}

		UpdateLayerQuickButtons();
	}

	private void OnWindArrowDensityChanged(double value)
	{
		WindArrowDensity = Mathf.Clamp((float)value, 0.5f, 2.5f);
		SaveAdvancedSettings();
		UpdateLabels();
		if (GetCurrentLayer() == MapLayer.Wind)
		{
			RedrawCurrentLayer();
		}
	}

	private void OnBasinSensitivityChanged(double value)
	{
		BasinSensitivity = Mathf.Clamp((float)value, 0.5f, 2.0f);
		SaveAdvancedSettings();
		UpdateLabels();
		var layer = GetCurrentLayer();
		if (layer == MapLayer.Biomes || layer == MapLayer.Landform)
		{
			RedrawCurrentLayer();
		}
	}

	private void OnInteriorReliefChanged(double value)
	{
		_interiorRelief = Mathf.Clamp((float)value, 0.5f, 2.0f);
		SyncMountainPresetFromCurrentValues();
		SaveAdvancedSettings();
		UpdateLabels();
		GenerateWorld();
	}

	private void OnOrogenyStrengthChanged(double value)
	{
		_orogenyStrength = Mathf.Clamp((float)value, 0.5f, 2.5f);
		SyncMountainPresetFromCurrentValues();
		SaveAdvancedSettings();
		UpdateLabels();
		GenerateWorld();
	}

	private void OnSubductionArcRatioChanged(double value)
	{
		_subductionArcRatio = Mathf.Clamp((float)value, 0.2f, 1.0f);
		SyncMountainPresetFromCurrentValues();
		SaveAdvancedSettings();
		UpdateLabels();
		GenerateWorld();
	}

	private void OnContinentalAgeChanged(double value)
	{
		_continentalAge = Mathf.Clamp((int)Mathf.Round((float)value), 0, 100);
		SyncMountainPresetFromCurrentValues();
		SaveAdvancedSettings();
		UpdateLabels();
		GenerateWorld();
	}

	private void OnMagicDensityChanged(double value)
	{
		_magicDensity = Mathf.Clamp((int)Mathf.Round((float)value), 0, 100);
		InvalidateEcologyCaches();
		UpdateLabels();
		UpdateLorePanel();
		SaveAdvancedSettings();
	}

	private void OnCivilAggressionChanged(double value)
	{
		_civilAggression = Mathf.Clamp((int)Mathf.Round((float)value), 0, 100);
		InvalidateEcologyCaches();
		UpdateLabels();
		UpdateLorePanel();
		SaveAdvancedSettings();
	}

	private void OnSpeciesDiversityChanged(double value)
	{
		_speciesDiversity = Mathf.Clamp((int)Mathf.Round((float)value), 0, 100);
		InvalidateEcologyCaches();
		UpdateLabels();
		UpdateLorePanel();
		SaveAdvancedSettings();
	}

	private void OnTimelineChanged(double value)
	{
		_currentEpoch = Mathf.Clamp((int)Mathf.Round((float)value), 0, MaxEpoch);
		_selectedTimelineEventEpoch = _currentEpoch;
		InvalidateEcologyCaches();
		UpdateLabels();
		UpdateLorePanel();
		SaveAdvancedSettings();
	}

	private void OnPrevEpochPressed()
	{
		MoveTimelineEventCursor(-1);
	}

	private void OnNextEpochPressed()
	{
		MoveTimelineEventCursor(1);
	}

	private void MoveTimelineEventCursor(int direction)
	{
		if (_primaryWorld == null)
		{
			ApplyEpochFromReplayCursor(Mathf.Clamp(_currentEpoch + direction, 0, MaxEpoch));
			return;
		}

		EnsureCivilizationSimulation(_primaryWorld);
		var events = _primaryWorld.CivilizationSimulation?.RecentEvents ?? Array.Empty<CivilizationEpochEvent>();
		if (events.Length == 0)
		{
			ApplyEpochFromReplayCursor(Mathf.Clamp(_currentEpoch + direction, 0, MaxEpoch));
			return;
		}

		var currentIndex = ResolveTimelineEventIndex(events, _selectedTimelineEventEpoch >= 0 ? _selectedTimelineEventEpoch : _currentEpoch);
		var nextIndex = Mathf.Clamp(currentIndex + direction, 0, events.Length - 1);
		_selectedTimelineEventEpoch = events[nextIndex].Epoch;
		ApplyEpochFromReplayCursor(_selectedTimelineEventEpoch);
	}

	private void ApplyEpochFromReplayCursor(int targetEpoch)
	{
		var clampedEpoch = Mathf.Clamp(targetEpoch, 0, MaxEpoch);
		if (clampedEpoch == _currentEpoch)
		{
			UpdateLabels();
			RedrawCurrentLayer();
			SaveAdvancedSettings();
			return;
		}

		_currentEpoch = clampedEpoch;
		InvalidateEcologyCaches();
		UpdateLabels();
		UpdateLorePanel();
		SaveAdvancedSettings();
	}

	private void InvalidateEcologyCaches()
	{
		if (_primaryWorld != null)
		{
			_primaryWorld.EcologySimulation = null;
			_primaryWorld.EcologySignature = int.MinValue;
			_primaryWorld.CivilizationSimulation = null;
			_primaryWorld.CivilizationSignature = int.MinValue;
			_primaryWorld.LayerRenderCache.Remove(MapLayer.Ecology);
			_primaryWorld.LayerRenderCache.Remove(MapLayer.Civilization);
			_primaryWorld.LayerRenderCache.Remove(MapLayer.TradeRoutes);
		}

		if (_compareWorld != null)
		{
			_compareWorld.EcologySimulation = null;
			_compareWorld.EcologySignature = int.MinValue;
			_compareWorld.CivilizationSimulation = null;
			_compareWorld.CivilizationSignature = int.MinValue;
			_compareWorld.LayerRenderCache.Remove(MapLayer.Ecology);
			_compareWorld.LayerRenderCache.Remove(MapLayer.Civilization);
			_compareWorld.LayerRenderCache.Remove(MapLayer.TradeRoutes);
		}

		var layer = GetCurrentLayer();
		if (layer == MapLayer.Ecology || layer == MapLayer.Civilization || layer == MapLayer.TradeRoutes)
		{
			RedrawCurrentLayer();
		}
	}

}
