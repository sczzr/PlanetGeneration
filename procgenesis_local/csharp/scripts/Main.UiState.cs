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
	private void UpdateLabels()
	{
		_seaLevelValue.Text = SeaLevel.ToString("0.00");
		_heatValue.Text = HeatFactor.ToString("0.00");
		_erosionValue.Text = ErosionIterations.ToString();
		_riverDensityValue.Text = RiverDensity.ToString("0.00");
		_windArrowDensityValue.Text = WindArrowDensity.ToString("0.00");
		_basinSensitivityValue.Text = BasinSensitivity.ToString("0.00");
		_interiorReliefValue.Text = _interiorRelief.ToString("0.00");
		_orogenyStrengthValue.Text = _orogenyStrength.ToString("0.00");
		_subductionArcRatioValue.Text = _subductionArcRatio.ToString("0.00");
		_continentalAgeValue.Text = _continentalAge.ToString();
		UpdateMountainControlSummary();
		_magicValue.Text = _magicDensity.ToString();
		_aggressionValue.Text = _civilAggression.ToString();
		_diversityValue.Text = _speciesDiversity.ToString();
		_uiFontScaleValue.Text = $"{Mathf.RoundToInt(_uiFontScale * 100f)}%";
		if (Mathf.Abs((float)_uiFontScaleSlider.Value - _uiFontScale * 100f) > 0.01f)
		{
			_uiFontScaleSlider.SetValueNoSignal(_uiFontScale * 100f);
		}
		_epochLabel.Text = $"第 {_currentEpoch} 纪元";
		if (Mathf.Abs((float)_timelineSlider.Value - _currentEpoch) > 0.01f)
		{
			_timelineSlider.SetValueNoSignal(_currentEpoch);
		}
	}

	private void OnUiFontScaleChanged(double value)
	{
		_uiFontScale = Mathf.Clamp((float)value / 100f, MinUiFontScale, MaxUiFontScale);
		ApplyUiFontScale();
		UpdateLabels();
		SaveAdvancedSettings();
	}

	private void CaptureUiFontSizeBaselines()
	{
		_baseFontSizeByControl.Clear();
		_baseRichTextFontSizeByControl.Clear();
		CaptureUiFontSizeBaselinesRecursive(this);
	}

	private void CaptureUiFontSizeBaselinesRecursive(Control control)
	{
		var fontSize = control.GetThemeFontSize("font_size");
		if (fontSize > 0)
		{
			_baseFontSizeByControl[control] = fontSize;
		}

		if (control is RichTextLabel richText)
		{
			var normalFontSize = richText.GetThemeFontSize("normal_font_size");
			if (normalFontSize > 0)
			{
				_baseRichTextFontSizeByControl[richText] = normalFontSize;
			}
		}

		foreach (Node child in control.GetChildren())
		{
			if (child is Control childControl)
			{
				CaptureUiFontSizeBaselinesRecursive(childControl);
			}
		}
	}

	private void ApplyUiFontScale()
	{
		foreach (var pair in _baseFontSizeByControl)
		{
			if (!IsInstanceValid(pair.Key))
			{
				continue;
			}

			pair.Key.AddThemeFontSizeOverride("font_size", ScaleUiFontSize(pair.Value));
		}

		foreach (var pair in _baseRichTextFontSizeByControl)
		{
			if (!IsInstanceValid(pair.Key))
			{
				continue;
			}

			pair.Key.AddThemeFontSizeOverride("normal_font_size", ScaleUiFontSize(pair.Value));
		}
	}

	private T GetNodeByName<T>(string name) where T : Node
	{
		var node = FindChild(name, true, false);
		if (node is not T typed)
		{
			throw new InvalidOperationException($"Node '{name}' not found or not of type {typeof(T).Name}.");
		}

		return typed;
	}

}
