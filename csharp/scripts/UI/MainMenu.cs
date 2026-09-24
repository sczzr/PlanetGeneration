using Godot;
using System;

namespace PlanetGeneration.UI;

/// <summary>
/// Full-screen title overlay. Owns its menu buttons and reports user intent as events;
/// the main scene decides what "new world" or "settings" actually do.
/// </summary>
public partial class MainMenu : Control
{
	public event Action? ContinueRequested;
	public event Action? NewWorldRequested;
	public event Action? LoadRequested;
	public event Action? SettingsRequested;

	public override void _Ready()
	{
		GetNode<Button>("%ContinueButton").Pressed += () =>
		{
			Close();
			ContinueRequested?.Invoke();
		};
		GetNode<Button>("%NewWorldButton").Pressed += () =>
		{
			Close();
			NewWorldRequested?.Invoke();
		};
		GetNode<Button>("%LoadButton").Pressed += () =>
		{
			Close();
			LoadRequested?.Invoke();
		};
		GetNode<Button>("%SettingsButton").Pressed += () =>
		{
			Close();
			SettingsRequested?.Invoke();
		};
		GetNode<Button>("%ExitButton").Pressed += () => GetTree().Quit();
	}

	private void Close() => Hide();
}
