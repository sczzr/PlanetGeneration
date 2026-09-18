using Godot;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Layers;
using System;
using System.Collections.Generic;

namespace PlanetGeneration.UI;

/// <summary>
/// 图层控制面板控制器：
/// 支持 13 种基础底图主题单选互斥、8 种矢量叠加层多选/透明度/线宽/图层上下排序，
/// 提供 6 种内置预设与自定义切换，并实时展示目标地块数、实际地块数与当前分辨率。
/// </summary>
public partial class LayerPanelController : ScrollContainer
{
	public event Action<LayerStackState>? LayerStackChanged;
	public event Action<string>? PresetSelected;

	private LayerStackState? _layerStack;
	private bool _updatingUi;

	private Label _statusLabel = null!;
	private OptionButton _presetOption = null!;
	private OptionButton _baseThemeOption = null!;
	private VBoxContainer _overlaysContainer = null!;

	// 8 种叠加图层定义（按默认展示顺序）
	private static readonly (string Id, string Name)[] OverlayCatalog = new[]
	{
		(LayerRegistry.LayerRivers, "河流水系 (Rivers)"),
		("coastlines", "海岸轮廓 (Coastlines)"),
		(LayerRegistry.LayerPolityBorders, "政体国界 (Borders)"),
		(LayerRegistry.LayerCities, "聚落城镇 (Cities)"),
		(LayerRegistry.LayerTradeRoutes, "贸易走廊 (Trade Routes)"),
		(LayerRegistry.LayerCellBorders, "地块线框 (Cell Outlines)"),
		(LayerRegistry.LayerWindArrows, "风向洋流 (Wind Arrows)"),
		(LayerRegistry.LayerPlateBorders, "板块边界 (Plate Borders)")
	};

	// 13 种基础底图主题定义
	private static readonly (string Id, string Name)[] BaseThemeCatalog = new[]
	{
		(LayerRegistry.LayerTerrainOverview, "地形总览 (自然卫星)"),
		(LayerRegistry.LayerBiomes, "生物群系 (Biomes)"),
		(LayerRegistry.LayerElevation, "高程分层 (Elevation)"),
		(LayerRegistry.LayerTemperature, "地表气温 (Temperature)"),
		(LayerRegistry.LayerMoisture, "降水湿度 (Moisture)"),
		(LayerRegistry.LayerLandform, "地貌构造 (Landforms)"),
		(LayerRegistry.LayerPlates, "地壳板块 (Plates)"),
		(LayerRegistry.LayerRockTypes, "岩石地质 (Rock Types)"),
		(LayerRegistry.LayerOres, "矿产分布 (Ores)"),
		(LayerRegistry.LayerEcology, "生态状态 (Ecology)"),
		(LayerRegistry.LayerCivilization, "文明疆域 (Civilization)"),
		(LayerRegistry.LayerTradeFlow, "贸易走廊强度 (Trade Flow)"),
		(LayerRegistry.LayerCellGrid, "地块网格调试 (Cell Grid)")
	};

	// 6 种内置预设
	private static readonly (string Id, string Name)[] PresetCatalog = new[]
	{
		(LayerPresetCatalog.PresetPhysical, "自然地理 (默认)"),
		(LayerPresetCatalog.PresetPolitical, "政治文明"),
		(LayerPresetCatalog.PresetTrade, "贸易网络"),
		(LayerPresetCatalog.PresetClimate, "气候分析"),
		(LayerPresetCatalog.PresetGeology, "地质资源"),
		(LayerPresetCatalog.PresetCellDebug, "地块调试"),
		("custom", "── 自定义 ──")
	};

	private readonly Dictionary<string, (CheckBox check, HSlider opacity, Label opacityLabel, SpinBox width, Button btnUp, Button btnDown)> _overlayControls = new(StringComparer.OrdinalIgnoreCase);

	public override void _Ready()
	{
		_statusLabel = GetNodeOrNull<Label>("LayersMargin/Content/StatusCard/StatusLabel")
			?? CreateFallbackStatusLabel();

		_presetOption = GetNodeOrNull<OptionButton>("LayersMargin/Content/PresetSection/PresetOption")
			?? CreateFallbackPresetOption();

		_baseThemeOption = GetNodeOrNull<OptionButton>("LayersMargin/Content/BaseThemeSection/BaseThemeOption")
			?? CreateFallbackBaseThemeOption();

		_overlaysContainer = GetNodeOrNull<VBoxContainer>("LayersMargin/Content/OverlaysSection/OverlaysContainer")
			?? CreateFallbackOverlaysContainer();

		SetupPresetOptions();
		SetupBaseThemeOptions();
		BuildOverlayList();
	}

	public void AttachLayerStack(LayerStackState layerStack)
	{
		_layerStack = layerStack;
		RefreshUi();
	}

	public void UpdateStatus(int targetCells, int actualCells, int width, int height)
	{
		if (IsInstanceValid(_statusLabel))
		{
			_statusLabel.Text = $"地块: 目标 {targetCells:N0} · 实际 {actualCells:N0} | 分辨率: {width}×{height}";
		}
	}

	private void SetupPresetOptions()
	{
		if (!IsInstanceValid(_presetOption)) return;
		_presetOption.Clear();
		for (var i = 0; i < PresetCatalog.Length; i++)
		{
			_presetOption.AddItem(PresetCatalog[i].Name, i);
		}

		_presetOption.ItemSelected += index =>
		{
			if (_updatingUi || _layerStack == null) return;
			var presetId = PresetCatalog[(int)index].Id;
			if (presetId != "custom")
			{
				LayerPresetCatalog.ApplyPreset(presetId, _layerStack);
				PresetSelected?.Invoke(presetId);
				RefreshUi();
				NotifyLayerStackChanged();
			}
		};
	}

	private void SetupBaseThemeOptions()
	{
		if (!IsInstanceValid(_baseThemeOption)) return;
		_baseThemeOption.Clear();
		for (var i = 0; i < BaseThemeCatalog.Length; i++)
		{
			_baseThemeOption.AddItem(BaseThemeCatalog[i].Name, i);
		}

		_baseThemeOption.ItemSelected += index =>
		{
			if (_updatingUi || _layerStack == null) return;
			var themeId = BaseThemeCatalog[(int)index].Id;
			_layerStack.SetBaseTheme(themeId);

			// 河流默认只在地形总览中开启显示；切换至其他底图主题时默认关闭
			var isTerrainOverview = string.Equals(themeId, LayerRegistry.LayerTerrainOverview, StringComparison.OrdinalIgnoreCase);
			_layerStack.SetOverlayActive(LayerRegistry.LayerRivers, isTerrainOverview);

			SetPresetToCustom();
			RefreshUi();
			NotifyLayerStackChanged();
		};
	}

	private void BuildOverlayList()
	{
		if (!IsInstanceValid(_overlaysContainer)) return;

		foreach (var child in _overlaysContainer.GetChildren())
		{
			child.QueueFree();
		}
		_overlayControls.Clear();

		for (var i = 0; i < OverlayCatalog.Length; i++)
		{
			var (id, name) = OverlayCatalog[i];
			var row = new PanelContainer
			{
				CustomMinimumSize = new Vector2(0, 38),
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};

			var rowMargin = new MarginContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			rowMargin.AddThemeConstantOverride("margin_left", 6);
			rowMargin.AddThemeConstantOverride("margin_right", 6);
			rowMargin.AddThemeConstantOverride("margin_top", 4);
			rowMargin.AddThemeConstantOverride("margin_bottom", 4);
			row.AddChild(rowMargin);

			var hbox = new HBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			hbox.AddThemeConstantOverride("separation", 6);
			rowMargin.AddChild(hbox);

			var check = new CheckBox
			{
				Text = name,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				FocusMode = FocusModeEnum.None
			};
			check.Toggled += active =>
			{
				if (_updatingUi || _layerStack == null) return;
				_layerStack.SetOverlayActive(id, active);
				if (string.Equals(id, LayerRegistry.LayerCities, StringComparison.OrdinalIgnoreCase))
				{
					_layerStack.SetOverlayActive(LayerRegistry.LayerCityLabels, active);
				}
				SetPresetToCustom();
				NotifyLayerStackChanged();
			};
			hbox.AddChild(check);

			// 透明度滑块
			var opacitySlider = new HSlider
			{
				CustomMinimumSize = new Vector2(60, 0),
				MinValue = 0,
				MaxValue = 100,
				Step = 5,
				Value = 100,
				SizeFlagsVertical = SizeFlags.ShrinkCenter
			};
			var opacityLabel = new Label
			{
				CustomMinimumSize = new Vector2(36, 0),
				Text = "100%",
				HorizontalAlignment = HorizontalAlignment.Right
			};
			opacityLabel.AddThemeFontSizeOverride("font_size", 11);

			opacitySlider.ValueChanged += val =>
			{
				opacityLabel.Text = $"{(int)val}%";
				if (_updatingUi || _layerStack == null) return;
				_layerStack.SetOpacity(id, (float)(val / 100.0));
				SetPresetToCustom();
				NotifyLayerStackChanged();
			};
			hbox.AddChild(opacitySlider);
			hbox.AddChild(opacityLabel);

			// 粗细调节
			var widthSpin = new SpinBox
			{
				CustomMinimumSize = new Vector2(54, 0),
				MinValue = 0.5,
				MaxValue = 5.0,
				Step = 0.5,
				Value = 1.5,
				SizeFlagsVertical = SizeFlags.ShrinkCenter
			};
			widthSpin.ValueChanged += val =>
			{
				if (_updatingUi || _layerStack == null) return;
				_layerStack.SetWidthOrSize(id, (float)val);
				SetPresetToCustom();
				NotifyLayerStackChanged();
			};
			hbox.AddChild(widthSpin);

			// 上移/下移按钮
			var btnUp = new Button
			{
				Text = "▲",
				CustomMinimumSize = new Vector2(24, 24),
				TooltipText = "图层上移"
			};
			btnUp.Pressed += () =>
			{
				if (_layerStack == null) return;
				if (_layerStack.MoveOverlayUp(id))
				{
					SetPresetToCustom();
					RefreshUi();
					NotifyLayerStackChanged();
				}
			};
			hbox.AddChild(btnUp);

			var btnDown = new Button
			{
				Text = "▼",
				CustomMinimumSize = new Vector2(24, 24),
				TooltipText = "图层下移"
			};
			btnDown.Pressed += () =>
			{
				if (_layerStack == null) return;
				if (_layerStack.MoveOverlayDown(id))
				{
					SetPresetToCustom();
					RefreshUi();
					NotifyLayerStackChanged();
				}
			};
			hbox.AddChild(btnDown);

			_overlaysContainer.AddChild(row);
			_overlayControls[id] = (check, opacitySlider, opacityLabel, widthSpin, btnUp, btnDown);
		}
	}

	public void RefreshUi()
	{
		if (_layerStack == null) return;
		_updatingUi = true;

		// 选中底图
		for (var i = 0; i < BaseThemeCatalog.Length; i++)
		{
			if (string.Equals(BaseThemeCatalog[i].Id, _layerStack.ActiveBaseThemeId, StringComparison.OrdinalIgnoreCase))
			{
				_baseThemeOption.Select(i);
				break;
			}
		}

		// 刷新叠加层开关与滑块
		foreach (var (id, (check, opacitySlider, opacityLabel, widthSpin, _, _)) in _overlayControls)
		{
			var active = _layerStack.IsOverlayActive(id);
			check.ButtonPressed = active;

			var opacity = _layerStack.GetOpacity(id);
			opacitySlider.Value = Math.Round(opacity * 100);
			opacityLabel.Text = $"{(int)opacitySlider.Value}%";

			var width = _layerStack.GetWidthOrSize(id);
			widthSpin.Value = width;
		}

		_updatingUi = false;
	}

	private void SetPresetToCustom()
	{
		if (IsInstanceValid(_presetOption))
		{
			_presetOption.Select(PresetCatalog.Length - 1);
		}
	}

	private void NotifyLayerStackChanged()
	{
		if (_layerStack != null)
		{
			LayerStackChanged?.Invoke(_layerStack);
		}
	}

	private Label CreateFallbackStatusLabel()
	{
		var label = new Label
		{
			Text = "地块: 目标 10,000 · 实际 10,000 | 分辨率: 2048×1024",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		label.AddThemeFontSizeOverride("font_size", 12);
		return label;
	}

	private OptionButton CreateFallbackPresetOption() => new();
	private OptionButton CreateFallbackBaseThemeOption() => new();
	private VBoxContainer CreateFallbackOverlaysContainer() => new();
}
