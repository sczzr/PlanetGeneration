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
	private void ApplyConsolePanelVisibility()
	{
		if (_consolePanel != null) _consolePanel.Visible = _consolePanelVisible;
		if (_consoleCollapseTab != null) _consoleCollapseTab.Visible = _consolePanelVisible;
		if (_consoleSummonTab != null) _consoleSummonTab.Visible = !_consolePanelVisible;
	}

	private void SetConsolePanelVisible(bool visible)
	{
		_consolePanelVisible = visible;
		ApplyConsolePanelVisibility();
		SaveAdvancedSettings();
	}

	private void OnConsoleCollapseTabPressed() => SetConsolePanelVisible(false);

	private void OnConsoleSummonTabPressed() => SetConsolePanelVisible(true);
}
