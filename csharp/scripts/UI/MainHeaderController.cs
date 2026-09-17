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
		AdvancedSettingsButton = GetNode<Button>("HeaderHBox/HeaderButtons/AdvancedSettingsButton");
		GenerateButton = GetNode<Button>("HeaderHBox/HeaderButtons/GenerateButton");
		ExportPngButton = GetNode<Button>("HeaderHBox/HeaderButtons/ExportPngButton");
		ExportJsonButton = GetNode<Button>("HeaderHBox/HeaderButtons/ExportJsonButton");
		ThemeToggleButton = GetNode<Button>("HeaderHBox/HeaderButtons/ThemeToggleButton");

		AdvancedSettingsButton.Pressed += () => AdvancedSettingsRequested?.Invoke();
		GenerateButton.Pressed += () => GenerateRequested?.Invoke();
		ExportPngButton.Pressed += () => ExportPngRequested?.Invoke();
		ExportJsonButton.Pressed += () => ExportJsonRequested?.Invoke();
		ThemeToggleButton.Pressed += () => ThemeToggleRequested?.Invoke();
	}
}
