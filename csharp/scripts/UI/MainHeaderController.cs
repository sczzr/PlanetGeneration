using Godot;
using System;

namespace PlanetGeneration.UI;

/// <summary>
/// Owns the header controls and exposes semantic commands to the screen coordinator.
/// The coordinator remains responsible for the actual application behavior.
/// </summary>
public partial class MainHeaderController : PanelContainer
{
	public event Action? AdvancedSettingsRequested;
	public event Action? GenerateRequested;
	public event Action? ExportPngRequested;
	public event Action? ExportJsonRequested;
	public event Action? ThemeToggleRequested;

	public Button AdvancedSettingsButton { get; private set; } = null!;
	public Button GenerateButton { get; private set; } = null!;
	public Button ExportPngButton { get; private set; } = null!;
	public Button ExportJsonButton { get; private set; } = null!;
	public Button ThemeToggleButton { get; private set; } = null!;

	public override void _Ready()
	{
		AdvancedSettingsButton = GetNodeOrNull<Button>("HeaderHBox/HeaderButtons/AdvancedSettingsButton")
			?? FindChild("AdvancedSettingsButton", true, false) as Button
			?? throw new InvalidOperationException("AdvancedSettingsButton not found.");

		GenerateButton = GetNodeOrNull<Button>("HeaderHBox/HeaderButtons/GenerateButton")
			?? FindChild("GenerateButton", true, false) as Button
			?? throw new InvalidOperationException("GenerateButton not found.");

		ExportPngButton = GetNodeOrNull<Button>("HeaderHBox/HeaderButtons/ExportPngButton")
			?? FindChild("ExportPngButton", true, false) as Button
			?? throw new InvalidOperationException("ExportPngButton not found.");

		ExportJsonButton = GetNodeOrNull<Button>("HeaderHBox/HeaderButtons/ExportJsonButton")
			?? FindChild("ExportJsonButton", true, false) as Button
			?? throw new InvalidOperationException("ExportJsonButton not found.");

		ThemeToggleButton = GetNodeOrNull<Button>("HeaderHBox/HeaderButtons/ThemeToggleButton")
			?? FindChild("ThemeToggleButton", true, false) as Button
			?? throw new InvalidOperationException("ThemeToggleButton not found.");

		AdvancedSettingsButton.Pressed += () => AdvancedSettingsRequested?.Invoke();
		GenerateButton.Pressed += () => GenerateRequested?.Invoke();
		ExportPngButton.Pressed += () => ExportPngRequested?.Invoke();
		ExportJsonButton.Pressed += () => ExportJsonRequested?.Invoke();
		ThemeToggleButton.Pressed += () => ThemeToggleRequested?.Invoke();
	}
}
