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
	private Tween? _consoleTween;

	private void ApplyConsolePanelVisibility(bool animated = false)
	{
		if (_consolePanel == null) return;

		_consoleTween?.Kill();

		if ((_mainMenu != null && _mainMenu.Visible) || (_worldSetupMenu != null && _worldSetupMenu.IsOpen) || _primaryWorld == null)
		{
			_consolePanel.Visible = false;
			if (_consoleCollapseTab != null) _consoleCollapseTab.Visible = false;
			if (_consoleSummonTab != null) _consoleSummonTab.Visible = false;
			return;
		}

		var panelWidth = _consolePanel.Size.X > 0 ? _consolePanel.Size.X : 372.0f;
		var targetVisibleX = 12.0f;
		var targetHiddenX = -(panelWidth + 24.0f);

		if (!animated)
		{
			_consolePanel.Visible = _consolePanelVisible;
			_consolePanel.Position = new Vector2(_consolePanelVisible ? targetVisibleX : targetHiddenX, _consolePanel.Position.Y);
			_consolePanel.Modulate = new Color(1, 1, 1, _consolePanelVisible ? 1.0f : 0.0f);
			if (_consoleCollapseTab != null) _consoleCollapseTab.Visible = _consolePanelVisible;
			if (_consoleSummonTab != null) _consoleSummonTab.Visible = !_consolePanelVisible;
			return;
		}

		_consoleTween = CreateTween().SetParallel(true);

		if (_consolePanelVisible)
		{
			_consolePanel.Visible = true;
			if (_consoleCollapseTab != null)
			{
				_consoleCollapseTab.Visible = true;
				_consoleCollapseTab.Modulate = Colors.White;
			}
			if (_consoleSummonTab != null) _consoleSummonTab.Visible = false;

			_consoleTween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			_consoleTween.TweenProperty(_consolePanel, "position:x", targetVisibleX, 0.24f);
			_consoleTween.TweenProperty(_consolePanel, "modulate:a", 1.0f, 0.20f);
		}
		else
		{
			if (_consoleSummonTab != null)
			{
				_consoleSummonTab.Visible = true;
				_consoleSummonTab.Modulate = new Color(1, 1, 1, 0);
				_consoleTween.TweenProperty(_consoleSummonTab, "modulate:a", 1.0f, 0.20f);
			}

			_consoleTween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
			_consoleTween.TweenProperty(_consolePanel, "position:x", targetHiddenX, 0.20f);
			_consoleTween.TweenProperty(_consolePanel, "modulate:a", 0.0f, 0.18f);
			_consoleTween.Chain().TweenCallback(Callable.From(() =>
			{
				if (!_consolePanelVisible)
				{
					_consolePanel.Visible = false;
				}
			}));
		}
	}

	private void SetConsolePanelVisible(bool visible, bool animated = true)
	{
		_consolePanelVisible = visible;
		ApplyConsolePanelVisibility(animated);
		SaveAdvancedSettings();
	}

	private void OnConsoleCollapseTabPressed() => SetConsolePanelVisible(false, true);

	private void OnConsoleSummonTabPressed() => SetConsolePanelVisible(true, true);
}
