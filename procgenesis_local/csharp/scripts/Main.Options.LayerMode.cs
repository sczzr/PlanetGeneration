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
	private void SetupLayerOptions()
	{
		_layerOption.Clear();
		_layerOption.AddItem("Satellite", (int)MapLayer.Satellite);
		_layerOption.AddItem("Plates", (int)MapLayer.Plates);

		_layerOption.AddItem("Temperature", (int)MapLayer.Temperature);
		_layerOption.AddItem("Rivers", (int)MapLayer.Rivers);
		_layerOption.AddItem("Moisture", (int)MapLayer.Moisture);
		_layerOption.AddItem("Wind", (int)MapLayer.Wind);
		_layerOption.AddItem("Elevation", (int)MapLayer.Elevation);
		_layerOption.AddItem("RockTypes", (int)MapLayer.RockTypes);

		_layerOption.AddItem("Ores", (int)MapLayer.Ores);
		_layerOption.AddItem("Biomes", (int)MapLayer.Biomes);
		_layerOption.AddItem("Cities", (int)MapLayer.Cities);
		_layerOption.AddItem("地貌", (int)MapLayer.Landform);
		_layerOption.AddItem("生态演化", (int)MapLayer.Ecology);
		_layerOption.AddItem("文明疆域", (int)MapLayer.Civilization);
		_layerOption.AddItem("贸易走廊", (int)MapLayer.TradeRoutes);
		_layerOption.Select(0);
		BuildLayerButtons();
	}

	private void SelectLayerById(int layerId, bool persist = true)
	{
		var index = _layerOption.GetItemIndex(layerId);
		if (index < 0)
		{
			return;
		}

		_layerOption.Select(index);
		SyncMapModeFromLayer(layerId);
		RedrawCurrentLayer();
		UpdateLayerQuickButtons();
		if (persist)
		{
			SaveAdvancedSettings();
		}
	}

	private void BuildLayerButtons()
	{
		var children = _layerButtons.GetChildren();
		foreach (Node child in children)
		{
			_layerButtons.RemoveChild(child);
			child.Free();
		}

		_layerButtonsById.Clear();
		BuildLayerCategorySection("自然 / NATURAL", NaturalLayerIds,
			new Color(0.090196f, 0.145098f, 0.231373f, 0.96f),
			new Color(0.129412f, 0.196078f, 0.301961f, 0.98f),
			new Color(0.180392f, 0.415686f, 0.862745f, 1f),
			new Color(0.764706f, 0.858824f, 1f, 0.45f));

		BuildLayerCategorySection("人文 / CIVIL", HumanLayerIds,
			new Color(0.254f, 0.196f, 0.118f, 0.95f),
			new Color(0.320f, 0.240f, 0.145f, 0.98f),
			new Color(0.780f, 0.522f, 0.145f, 1f),
			new Color(1f, 0.804f, 0.439f, 0.45f));

		BuildLayerCategorySection("神秘 / ARCANE", ArcaneLayerIds,
			new Color(0.176f, 0.106f, 0.286f, 0.95f),
			new Color(0.243f, 0.141f, 0.388f, 0.98f),
			new Color(0.525f, 0.286f, 0.902f, 1f),
			new Color(0.819f, 0.666f, 1f, 0.45f));
	}

	private void BuildLayerCategorySection(string title, int[] layerIds, Color normalBg, Color hoverBg, Color activeBg, Color activeBorder)
	{
		var section = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		section.AddThemeConstantOverride("separation", 4);

		var header = new Label
		{
			Text = title
		};
		header.AddThemeColorOverride("font_color", new Color(0.639f, 0.725f, 0.835f, 0.9f));
		header.AddThemeFontSizeOverride("font_size", 10);
		_baseFontSizeByControl[header] = 10;
		section.AddChild(header);

		var row = new HFlowContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("h_separation", 6);
		row.AddThemeConstantOverride("v_separation", 6);
		section.AddChild(row);

		var normalStyle = CreateLayerButtonStyle(normalBg, new Color(activeBorder.R, activeBorder.G, activeBorder.B, 0.18f));
		var hoverStyle = CreateLayerButtonStyle(hoverBg, new Color(activeBorder.R, activeBorder.G, activeBorder.B, 0.28f));
		var activeStyle = CreateLayerButtonStyle(activeBg, activeBorder);

		foreach (var layerId in layerIds)
		{
			var itemIndex = _layerOption.GetItemIndex(layerId);
			if (itemIndex < 0)
			{
				continue;
			}

			var button = new Button
			{
				Text = GetLayerButtonText(layerId, _layerOption.GetItemText(itemIndex)),
				ToggleMode = true,
				FocusMode = Control.FocusModeEnum.None,
				CustomMinimumSize = new Vector2(70f, 28f),
				ClipText = true
			};

			button.AddThemeStyleboxOverride("normal", normalStyle);
			button.AddThemeStyleboxOverride("hover", hoverStyle);
			button.AddThemeStyleboxOverride("pressed", activeStyle);
			button.AddThemeStyleboxOverride("focus", activeStyle);
			button.AddThemeStyleboxOverride("disabled", normalStyle);
			button.AddThemeColorOverride("font_color", new Color(0.84f, 0.9f, 0.98f, 0.95f));
			button.AddThemeColorOverride("font_hover_color", Colors.White);
			button.AddThemeColorOverride("font_pressed_color", Colors.White);
			button.AddThemeColorOverride("font_focus_color", Colors.White);
			button.AddThemeFontSizeOverride("font_size", 12);
			_baseFontSizeByControl[button] = 12;

			var capturedLayerId = layerId;
			button.Pressed += () => SelectLayerById(capturedLayerId);

			row.AddChild(button);
			_layerButtonsById[layerId] = button;
		}

		if (row.GetChildCount() > 0)
		{
			_layerButtons.AddChild(section);
		}
		else
		{
			section.QueueFree();
		}
	}

	private void SetupMapModeOptions()
	{
		_mapModeOption.Clear();
		_mapModeOption.AddItem("地理", (int)MapMode.Geographic);
		_mapModeOption.AddItem("政区", (int)MapMode.Geopolitical);
		_mapModeOption.AddItem("奥术", (int)MapMode.Arcane);
		SelectMapModeOption(_mapMode);
	}

	private void ApplyMapModeSelection(MapMode mode, bool persist, bool applyRecommendedLayer)
	{
		_mapMode = mode;
		SelectMapModeOption(mode);

		if (applyRecommendedLayer)
		{
			var recommendedLayer = GetRecommendedLayerForMode(mode);
			if (recommendedLayer >= 0)
			{
				SelectLayerById(recommendedLayer, persist: false);
			}
		}

		UpdateLorePanel();
		if (persist)
		{
			SaveAdvancedSettings();
		}
	}

	private void SelectMapModeOption(MapMode mode)
	{
		for (var index = 0; index < _mapModeOption.ItemCount; index++)
		{
			if (_mapModeOption.GetItemId(index) == (int)mode)
			{
				_mapModeOption.Select(index);
				return;
			}
		}

		if (_mapModeOption.ItemCount > 0)
		{
			_mapModeOption.Select(0);
		}
	}

	private void SyncMapModeFromLayer(int layerId)
	{
		var inferredMode = DetermineMapModeByLayer(layerId);
		if (_mapMode == inferredMode)
		{
			return;
		}

		_mapMode = inferredMode;
		SelectMapModeOption(_mapMode);
		UpdateLorePanel();
	}

	private static MapMode DetermineMapModeByLayer(int layerId)
	{
		if (Array.IndexOf(ArcaneLayerIds, layerId) >= 0)
		{
			return MapMode.Arcane;
		}

		if (Array.IndexOf(HumanLayerIds, layerId) >= 0)
		{
			return MapMode.Geopolitical;
		}

		return MapMode.Geographic;
	}

	private int GetRecommendedLayerForMode(MapMode mode)
	{
		return mode switch
		{
			MapMode.Geographic => (int)MapLayer.Satellite,
			MapMode.Geopolitical => (int)MapLayer.TradeRoutes,
			MapMode.Arcane => (int)MapLayer.Biomes,
			_ => -1
		};
	}

	private string GetLayerButtonText(int layerId, string fallback)
	{
		return layerId switch
		{
			(int)MapLayer.Satellite => "卫星",
			(int)MapLayer.Plates => "板块",
			(int)MapLayer.Temperature => "温度",
			(int)MapLayer.Rivers => "河流",
			(int)MapLayer.Moisture => "降水",
			(int)MapLayer.Wind => "风场",
			(int)MapLayer.Elevation => "高程",
			(int)MapLayer.RockTypes => "岩石",
			(int)MapLayer.Ores => "矿产",
			(int)MapLayer.Biomes => "群系",
			(int)MapLayer.Cities => "城市",
			(int)MapLayer.Landform => "地貌",
			(int)MapLayer.Ecology => "生态",
			(int)MapLayer.Civilization => "文明",
			(int)MapLayer.TradeRoutes => "贸易",
			_ => fallback
		};
	}

	private void UpdateLayerQuickButtons()
	{
		var selectedId = _layerOption.GetSelectedId();
		foreach (var pair in _layerButtonsById)
		{
			var isSelected = pair.Key == selectedId;
			pair.Value.ButtonPressed = isSelected;
			pair.Value.Modulate = pair.Value.Disabled
				? new Color(0.66f, 0.72f, 0.80f, 0.74f)
				: Colors.White;
		}
	}

	private static StyleBoxFlat CreateLayerButtonStyle(Color background, Color border)
	{
		return new StyleBoxFlat
		{
			BgColor = background,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			BorderColor = border,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6
		};
	}

	private int ScaleUiFontSize(int baseSize)
	{
		return Mathf.Clamp(Mathf.RoundToInt(baseSize * _uiFontScale), 8, 72);
	}

	private void DisableMouseWheelForAllSliders()
	{
		DisableMouseWheelForAllSlidersRecursive(this);
	}

	private static void DisableMouseWheelForAllSlidersRecursive(Node node)
	{
		if (node is HSlider slider)
		{
			slider.Scrollable = false;
		}

		foreach (Node child in node.GetChildren())
		{
			DisableMouseWheelForAllSlidersRecursive(child);
		}
	}

}
