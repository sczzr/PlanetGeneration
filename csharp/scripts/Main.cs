using Godot;
using PlanetGeneration.UI;

namespace PlanetGeneration;

public partial class Main : Control
{
	// The layout lives entirely in MainLayout.tscn (container-driven);
	// Main only wires composed sub-scenes to the application behavior.

	private void ConnectMainMenu()
	{
		var menu = GetNode<MainMenu>("MainLayout/MainMenu");
		menu.NewWorldRequested += OnGeneratePressed;
		menu.SettingsRequested += ShowAdvancedSettingsPage;
	}
}
