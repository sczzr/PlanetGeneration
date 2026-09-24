using Godot;
using System;
using PlanetGeneration.UI.State;

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
	public bool IsOpen { get; private set; }

	public override void _Ready()
	{
		IsOpen = Visible;
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
		SetupSeedSpinStyle();
		SetupD20Button();
		SetupTerrainDrawer();
		SetupButtonAnimations();
	}

	private void SetupSeedSpinStyle()
	{
		if (FindChild("SeedSpin", true, false) is SpinBox seedSpin)
		{
			var le = seedSpin.GetLineEdit();
			if (le != null)
			{
				var styleNormal = new StyleBoxFlat
				{
					BgColor = new Color("382618"),
					BorderColor = new Color("7d5836"),
					BorderWidthLeft = 1,
					BorderWidthTop = 1,
					BorderWidthRight = 1,
					BorderWidthBottom = 1,
					CornerRadiusTopLeft = 4,
					CornerRadiusTopRight = 4,
					CornerRadiusBottomRight = 4,
					CornerRadiusBottomLeft = 4,
					ContentMarginLeft = 8,
					ContentMarginRight = 8,
					ContentMarginTop = 3,
					ContentMarginBottom = 3
				};
				var styleFocus = (StyleBoxFlat)styleNormal.Duplicate();
				styleFocus.BorderColor = new Color("e6a845");

				le.AddThemeStyleboxOverride("normal", styleNormal);
				le.AddThemeStyleboxOverride("focus", styleFocus);
				le.AddThemeColorOverride("font_color", new Color("fff0c8"));
				le.AddThemeColorOverride("font_selected_color", Colors.White);
				le.Alignment = HorizontalAlignment.Center;
			}
		}
	}

	public void Open()
	{
		IsOpen = true;
		Visible = true;
		MouseFilter = MouseFilterEnum.Stop;
		Modulate = new Color(1, 1, 1, 0);
		var tween = CreateTween();
		tween.TweenProperty(this, "modulate:a", 1.0f, 0.25)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
	}

	public void Close()
	{
		IsOpen = false;
		MouseFilter = MouseFilterEnum.Ignore;
		var tween = CreateTween();
		tween.TweenProperty(this, "modulate:a", 0.0f, 0.2)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(() => Visible = false));
	}

	private void SetupD20Button()
	{
		if (FindChild("RandomButton", true, false) is Button d20)
		{
			d20.Pressed += () =>
			{
				AnimateD20Roll(d20);
				RandomRequested?.Invoke();
			};
		}
	}

	private void AnimateD20Roll(Button button)
	{
		button.PivotOffset = button.Size / 2f;
		var tween = CreateTween().SetParallel(true);
		tween.TweenProperty(button, "rotation", button.Rotation + Mathf.Pi * 2f, 0.45)
			.SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.Out);

		var scaleTween = CreateTween();
		scaleTween.TweenProperty(button, "scale", new Vector2(1.28f, 1.28f), 0.15)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		scaleTween.TweenProperty(button, "scale", Vector2.One, 0.3)
			.SetTrans(Tween.TransitionType.Bounce)
			.SetEase(Tween.EaseType.Out);
	}

	private void SetupTerrainDrawer()
	{
		if (FindChild("MapThumbIslands", true, false) is BaseButton thumbIslands)
		{
			thumbIslands.Pressed += () => SelectTerrainPreset(3);
		}
		if (FindChild("MapThumbContinents", true, false) is BaseButton thumbContinents)
		{
			thumbContinents.Pressed += () => SelectTerrainPreset(2);
		}
		if (FindChild("TerrainPresetOption", true, false) is OptionButton option)
		{
			option.ItemSelected += id => UpdateDrawerHighlight((int)id);
			UpdateDrawerHighlight(option.Selected);
		}
	}

	private void SelectTerrainPreset(int index)
	{
		if (FindChild("TerrainPresetOption", true, false) is OptionButton option)
		{
			if (index >= 0 && index < option.ItemCount)
			{
				option.Select(index);
				option.EmitSignal(OptionButton.SignalName.ItemSelected, index);
				UpdateDrawerHighlight(index);
			}
		}
	}

	private void UpdateDrawerHighlight(int selectedIndex)
	{
		var thumbIslands = FindChild("MapThumbIslands", true, false) as CanvasItem;
		var thumbContinents = FindChild("MapThumbContinents", true, false) as CanvasItem;
		if (thumbIslands != null)
		{
			var isIsland = (selectedIndex == 3 || selectedIndex == 4);
			var targetModulate = isIsland ? Colors.White : new Color(0.72f, 0.72f, 0.72f, 0.78f);
			var tween = CreateTween();
			tween.TweenProperty(thumbIslands, "modulate", targetModulate, 0.2);
		}
		if (thumbContinents != null)
		{
			var isContinent = (selectedIndex == 0 || selectedIndex == 1 || selectedIndex == 2);
			var targetModulate = isContinent ? Colors.White : new Color(0.72f, 0.72f, 0.72f, 0.78f);
			var tween = CreateTween();
			tween.TweenProperty(thumbContinents, "modulate", targetModulate, 0.2);
		}
	}

	private void SetupButtonAnimations()
	{
		AddButtonHoverAnimation(ConfirmGenerateButton);
		AddButtonHoverAnimation(BackToMenuButton);
		if (RandomAllButton != null) AddButtonHoverAnimation(RandomAllButton);
		if (CloseCornerButton != null) AddButtonHoverAnimation(CloseCornerButton);
	}

	private void AddButtonHoverAnimation(Button button)
	{
		button.PivotOffset = button.Size / 2f;
		button.MouseEntered += () =>
		{
			button.PivotOffset = button.Size / 2f;
			var tween = CreateTween();
			tween.TweenProperty(button, "scale", new Vector2(1.03f, 1.03f), 0.15)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
		};
		button.MouseExited += () =>
		{
			var tween = CreateTween();
			tween.TweenProperty(button, "scale", Vector2.One, 0.15)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
		};
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
		PolygonTileMode tileMode,
		float seaLevel = 0.5f,
		float heatFactor = 0.5f,
		float moistureFactor = 1.0f,
		int erosionIterations = 5,
		float riverDensity = 1.0f,
		bool riversEnabled = true,
		float interiorRelief = 1.0f,
		float orogenyStrength = 1.0f,
		float subductionArcRatio = 0.72f,
		int continentalAge = 58,
		int seed = 0)
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

		SetSliderValue("SeaLevelSlider", seaLevel);
		SetSliderValue("HeatSlider", heatFactor);
		SetSliderValue("MoistureSlider", moistureFactor);
		SetSliderValue("ErosionSlider", erosionIterations);
		SetSliderValue("RiverDensitySlider", riverDensity);
		if (FindChild("RiversSwitch", true, false) is CheckButton riversSwitch)
		{
			riversSwitch.ButtonPressed = riversEnabled;
		}

		SetSliderValue("InteriorReliefSlider", interiorRelief);
		SetSliderValue("OrogenyStrengthSlider", orogenyStrength);
		SetSliderValue("SubductionArcRatioSlider", subductionArcRatio);
		SetSliderValue("ContinentalAgeSlider", continentalAge);

		if (FindChild("SeedSpin", true, false) is SpinBox seedSpin)
		{
			seedSpin.Value = seed;
		}

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
	/// 将主程序刚随机出的种子写回构建界面的种子输入框，
	/// 保证界面显示、存档命名与实际参与生成的种子一致。
	/// </summary>
	public void SetSeed(int seed)
	{
		if (FindChild("SeedSpin", true, false) is SpinBox seedSpin)
		{
			seedSpin.SetValueNoSignal(seed);
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

		var seaLevel = (float)GetSliderValue("SeaLevelSlider", 0.50);
		var heat = (float)GetSliderValue("HeatSlider", 0.50);
		var moisture = (float)GetSliderValue("MoistureSlider", 0.50);
		var erosion = (int)GetSliderValue("ErosionSlider", 5);
		var riverDensity = (float)GetSliderValue("RiverDensitySlider", 0.50);
		var riversEnabled = true;
		if (FindChild("RiversSwitch", true, false) is CheckButton riversSwitch)
		{
			riversEnabled = riversSwitch.ButtonPressed;
		}

		var interiorRelief = (float)GetSliderValue("InteriorReliefSlider", 1.00);
		var orogenyStrength = (float)GetSliderValue("OrogenyStrengthSlider", 1.00);
		var subductionArcRatio = (float)GetSliderValue("SubductionArcRatioSlider", 0.72);
		var continentalAge = (int)GetSliderValue("ContinentalAgeSlider", 58);

		if (FindChild("SeedSpin", true, false) is SpinBox seedSpin)
		{
			main.Seed = (int)seedSpin.Value;
		}

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
			tileMode,
			seaLevel,
			heat,
			moisture,
			erosion,
			riverDensity,
			riversEnabled,
			interiorRelief,
			orogenyStrength,
			subductionArcRatio,
			continentalAge);
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
