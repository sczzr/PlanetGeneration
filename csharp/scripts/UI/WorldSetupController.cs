using Godot;
using System;
using PlanetGeneration.WorldGen.Polygon;

namespace PlanetGeneration.UI;

/// <summary>
/// 控制创世工坊·世界构建页面（WorldSetupMenu）。
/// 包含“一级基础配置”与“二级高级折叠配置”两级面板结构，
/// 负责数值徽标同步、展开/收起切换以及与主控制器 Main 的双向参数同步。
/// </summary>
public partial class WorldSetupController : Control
{
	public event Action? GenerateRequested;
	public event Action? BackRequested;
	public event Action? RandomRequested;

	public Button ConfirmGenerateButton { get; private set; } = null!;
	public Button BackToMenuButton { get; private set; } = null!;
	public Button? CloseCornerButton { get; private set; }
	public Button? RandomAllButton { get; private set; }
	public Button? ToggleAdvancedButton { get; private set; }
	public Control? AdvancedContainer { get; private set; }
	public ScrollContainer? BodyScroll { get; private set; }

	public override void _Ready()
	{
		ConfirmGenerateButton = FindChild("ConfirmGenerateButton", true, false) as Button
			?? throw new InvalidOperationException("ConfirmGenerateButton not found in WorldSetupMenu.");
		BackToMenuButton = FindChild("BackToMenuButton", true, false) as Button
			?? throw new InvalidOperationException("BackToMenuButton not found in WorldSetupMenu.");
		CloseCornerButton = FindChild("CloseCornerButton", true, false) as Button;
		RandomAllButton = FindChild("RandomAllButton", true, false) as Button;
		ToggleAdvancedButton = FindChild("ToggleAdvancedButton", true, false) as Button;
		AdvancedContainer = FindChild("AdvancedContainer", true, false) as Control;
		BodyScroll = FindChild("BodyScroll", true, false) as ScrollContainer;

		ConfirmGenerateButton.Pressed += () => GenerateRequested?.Invoke();
		BackToMenuButton.Pressed += () => BackRequested?.Invoke();
		if (CloseCornerButton != null)
		{
			CloseCornerButton.Pressed += () => BackRequested?.Invoke();
		}
		if (RandomAllButton != null)
		{
			RandomAllButton.Pressed += () => RandomRequested?.Invoke();
		}
		if (ToggleAdvancedButton != null && AdvancedContainer != null)
		{
			ToggleAdvancedButton.Pressed += OnToggleAdvancedPressed;
		}

		SetupPolygonTileModeOptions();
		HookSliderValueUpdates();
	}

	public void Open()
	{
		Visible = true;
	}

	public void Close()
	{
		Visible = false;
	}

	private void OnToggleAdvancedPressed()
	{
		if (AdvancedContainer == null || ToggleAdvancedButton == null) return;

		AdvancedContainer.Visible = !AdvancedContainer.Visible;
		ToggleAdvancedButton.Text = AdvancedContainer.Visible
			? "▲ 收起高级物理与构造配置 (COLLAPSE ADVANCED)"
			: "⚙️ 展开高级物理与构造配置 (EXPAND ADVANCED) ▼";

		if (BodyScroll != null)
		{
			if (AdvancedContainer.Visible)
			{
				GetTree().CreateTimer(0.05).Timeout += () =>
				{
					if (BodyScroll != null && IsInstanceValid(BodyScroll))
					{
						var tween = CreateTween();
						tween.TweenProperty(BodyScroll, "scroll_vertical", 480, 0.35)
							.SetTrans(Tween.TransitionType.Cubic)
							.SetEase(Tween.EaseType.Out);
					}
				};
			}
			else
			{
				var tween = CreateTween();
				tween.TweenProperty(BodyScroll, "scroll_vertical", 0, 0.25)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			}
		}
	}

	private void SetupPolygonTileModeOptions()
	{
		if (FindChild("PolygonTileModeOption", true, false) is OptionButton modeOption)
		{
			modeOption.Clear();
			modeOption.AddItem("多边形单元格 (标准桌游风格)", 0);
			modeOption.AddItem("单元格 + 描边 (清晰边界)", 1);
			modeOption.AddItem("混合渲染 (分类多边形/连续栅格)", 2);
			modeOption.AddItem("经典连续栅格 (无缝卫星图)", 3);
			modeOption.Select(0);
		}
	}

	private void HookSliderValueUpdates()
	{
		// 基础滑条
		BindSliderLabel("SeaLevelSlider", "SeaLevelValue", val => val.ToString("0.00"));
		BindSliderLabel("HeatSlider", "HeatValue", val => val.ToString("0.00"));
		BindSliderLabel("MoistureSlider", "MoistureValue", val => val.ToString("0.00"));
		BindSliderLabel("ErosionSlider", "ErosionValue", val => val.ToString("0"));
		BindSliderLabel("RiverDensitySlider", "RiverDensityValue", val => val.ToString("0.00"));

		// 文明与法则基础初值
		BindSliderLabel("SetupMagicSlider", "SetupMagicValue", val => val.ToString("0"));
		BindSliderLabel("SetupAggressionSlider", "SetupAggressionValue", val => val.ToString("0"));
		BindSliderLabel("SetupEpochSlider", "SetupEpochValue", val =>
		{
			var ep = (int)val;
			if (ep <= 100) return $"{ep} (蛮荒原初)";
			if (ep <= 500) return $"{ep} (城邦繁盛)";
			return $"{ep} (帝国争霸)";
		});

		// 高级构造地质滑条
		BindSliderLabel("PlateCountSlider", "PlateCountValue", val => val.ToString("0"));
		BindSliderLabel("OceanicRatioSlider", "OceanicRatioValue", val => $"{(int)Math.Round(val * 100f)}%");
		BindSliderLabel("ContinentBiasSlider", "ContinentBiasValue", val => $"{(int)Math.Round(val * 100f)}%");
		BindSliderLabel("InteriorReliefSlider", "InteriorReliefValue", val => val.ToString("0.00"));
		BindSliderLabel("OrogenyStrengthSlider", "OrogenyStrengthValue", val => val.ToString("0.00"));
		BindSliderLabel("SubductionArcRatioSlider", "SubductionArcRatioValue", val => val.ToString("0.00"));
		BindSliderLabel("ContinentalAgeSlider", "ContinentalAgeValue", val => val.ToString("0"));

		// 高级大气与水汽滑条
		BindSliderLabel("WindCellCountSlider", "WindCellCountValue", val => val.ToString("0"));
		BindSliderLabel("MoistureIterationsSlider", "MoistureIterationsValue", val => val.ToString("0"));
		BindSliderLabel("SetupBasinSensitivitySlider", "SetupBasinSensitivityValue", val => val.ToString("0.00"));

		// 高级生态滑条
		BindSliderLabel("SetupDiversitySlider", "SetupDiversityValue", val => val.ToString("0"));
	}

	private void BindSliderLabel(string sliderName, string labelName, Func<double, string> formatter)
	{
		if (FindChild(sliderName, true, false) is HSlider slider &&
		    FindChild(labelName, true, false) is Label label)
		{
			label.Text = formatter(slider.Value);
			slider.ValueChanged += val =>
			{
				label.Text = formatter(val);
			};
		}
	}

	/// <summary>
	/// 从主程序 Main 同步当前世界与法则设定到构建界面。
	/// </summary>
	public void SyncFromMain(
		int plateCount,
		float oceanicRatio,
		float continentBias,
		int windCellCount,
		int moistureIterations,
		float basinSensitivity,
		int magicDensity,
		int civilAggression,
		int speciesDiversity,
		int currentEpoch,
		PolygonTileMode tileMode)
	{
		SetSliderValue("PlateCountSlider", plateCount);
		SetSliderValue("OceanicRatioSlider", oceanicRatio);
		SetSliderValue("ContinentBiasSlider", continentBias);
		SetSliderValue("WindCellCountSlider", windCellCount);
		SetSliderValue("MoistureIterationsSlider", moistureIterations);
		SetSliderValue("SetupBasinSensitivitySlider", basinSensitivity);
		SetSliderValue("SetupMagicSlider", magicDensity);
		SetSliderValue("SetupAggressionSlider", civilAggression);
		SetSliderValue("SetupDiversitySlider", speciesDiversity);
		SetSliderValue("SetupEpochSlider", currentEpoch);

		if (FindChild("PolygonTileModeOption", true, false) is OptionButton modeOption)
		{
			var idx = tileMode switch
			{
				PolygonTileMode.Cells => 0,
				PolygonTileMode.Outlined => 1,
				PolygonTileMode.Hybrid => 2,
				PolygonTileMode.Raster => 3,
				_ => 0
			};
			modeOption.Select(idx);
		}
	}

	/// <summary>
	/// 将玩家在构建页面设置的高级常数写回 Main。
	/// </summary>
	public void ApplyToMain(Main main)
	{
		var plateCount = (int)GetSliderValue("PlateCountSlider", 20);
		var oceanicRatio = (float)GetSliderValue("OceanicRatioSlider", 0.48);
		var continentBias = (float)GetSliderValue("ContinentBiasSlider", 0.18);
		var windCellCount = (int)GetSliderValue("WindCellCountSlider", 10);
		var moistureIterations = (int)GetSliderValue("MoistureIterationsSlider", 8);
		var basinSensitivity = (float)GetSliderValue("SetupBasinSensitivitySlider", 1.0);
		var magicDensity = (int)GetSliderValue("SetupMagicSlider", 75);
		var civilAggression = (int)GetSliderValue("SetupAggressionSlider", 42);
		var speciesDiversity = (int)GetSliderValue("SetupDiversitySlider", 68);
		var initialEpoch = (int)GetSliderValue("SetupEpochSlider", 450);

		var tileMode = PolygonTileMode.Cells;
		if (FindChild("PolygonTileModeOption", true, false) is OptionButton modeOption)
		{
			tileMode = modeOption.Selected switch
			{
				0 => PolygonTileMode.Cells,
				1 => PolygonTileMode.Outlined,
				2 => PolygonTileMode.Hybrid,
				3 => PolygonTileMode.Raster,
				_ => PolygonTileMode.Cells
			};
		}

		main.ApplyWorldSetupSettings(
			plateCount,
			oceanicRatio,
			continentBias,
			windCellCount,
			moistureIterations,
			basinSensitivity,
			magicDensity,
			civilAggression,
			speciesDiversity,
			initialEpoch,
			tileMode);
	}

	private void SetSliderValue(string sliderName, double value)
	{
		if (FindChild(sliderName, true, false) is HSlider slider)
		{
			slider.SetValueNoSignal(value);
		}
	}

	private double GetSliderValue(string sliderName, double fallback)
	{
		if (FindChild(sliderName, true, false) is HSlider slider)
		{
			return slider.Value;
		}
		return fallback;
	}
}
