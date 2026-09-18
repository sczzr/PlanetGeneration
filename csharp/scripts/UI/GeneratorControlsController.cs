using Godot;
using System;

namespace PlanetGeneration.UI;

/// <summary>
/// Owns the generator controls across the console tab pages and forwards user intent.
/// The tab strip itself is the page navigation; each page is a composed scene instance.
/// The terrain page concentrates every world-shaping parameter; display-only and
/// simulation-only knobs stay on their own pages.
/// </summary>
public partial class GeneratorControlsController : TabContainer
{
	public event Action? RandomRequested;
	public event Action? ApplyRequested;
	public event Action<double>? SeaLevelChanged;
	public event Action<double>? HeatChanged;
	public event Action<double>? MoistureChanged;
	public event Action<double>? ErosionChanged;
	public event Action<double>? InteriorReliefChanged;
	public event Action<double>? OrogenyStrengthChanged;
	public event Action<double>? SubductionArcRatioChanged;
	public event Action<double>? ContinentalAgeChanged;
	public event Action<bool>? RiversToggled;
	public event Action<double>? RiverDensityChanged;

	public Button RandomButton { get; private set; } = null!;
	public Button ApplyButton { get; private set; } = null!;
	public SpinBox SeedSpin { get; private set; } = null!;
	public HSlider SeaLevelSlider { get; private set; } = null!;
	public HSlider HeatSlider { get; private set; } = null!;
	public HSlider MoistureSlider { get; private set; } = null!;
	public HSlider ErosionSlider { get; private set; } = null!;
	public HSlider InteriorReliefSlider { get; private set; } = null!;
	public HSlider OrogenyStrengthSlider { get; private set; } = null!;
	public HSlider SubductionArcRatioSlider { get; private set; } = null!;
	public HSlider ContinentalAgeSlider { get; private set; } = null!;
	public BaseButton RiversSwitch { get; private set; } = null!;
	public HSlider RiverDensitySlider { get; private set; } = null!;
	public Label SeaLevelValue { get; private set; } = null!;
	public Label HeatValue { get; private set; } = null!;
	public Label MoistureValue { get; private set; } = null!;
	public Label ErosionValue { get; private set; } = null!;
	public Label InteriorReliefValue { get; private set; } = null!;
	public Label OrogenyStrengthValue { get; private set; } = null!;
	public Label SubductionArcRatioValue { get; private set; } = null!;
	public Label ContinentalAgeValue { get; private set; } = null!;
	public Label RiverDensityValue { get; private set; } = null!;

	private const string ParamsRoot = "ParamsPage/ParamsMargin/Content/ParamsBox";
	private const string MountainRoot = $"{ParamsRoot}/MountainBody/MountainBodyContent";

	private static readonly string[] TabTitles = { "🗺️ 地貌参数", "📑 观测图层", "⚙️ 深度法则", "📜 叙事推演", "⏳ 演化编年" };

	public bool HasParamControls { get; private set; }

	public override void _Ready()
	{
		UpdateTabTitles();

		var seedSpinNode = GetNodeOrNull<SpinBox>($"{ParamsRoot}/SeedRow/SeedSpin")
			?? FindChild("SeedSpin", true, false) as SpinBox;

		if (seedSpinNode != null)
		{
			HasParamControls = true;
			SeedSpin = seedSpinNode;
			RandomButton = FindParamNode<Button>($"{ParamsRoot}/SeedRow/RandomButton", "RandomButton");
			ApplyButton = FindParamNode<Button>($"{ParamsRoot}/SeedRow/ApplyButton", "ApplyButton");
			SeaLevelSlider = FindParamNode<HSlider>($"{ParamsRoot}/SeaWrap/SeaLevelSlider", "SeaLevelSlider");
			HeatSlider = FindParamNode<HSlider>($"{ParamsRoot}/HeatWrap/HeatSlider", "HeatSlider");
			MoistureSlider = FindParamNode<HSlider>($"{ParamsRoot}/MoistureWrap/MoistureSlider", "MoistureSlider");
			ErosionSlider = FindParamNode<HSlider>($"{ParamsRoot}/ErosionWrap/ErosionSlider", "ErosionSlider");
			InteriorReliefSlider = FindParamNode<HSlider>($"{MountainRoot}/InteriorReliefRow/InteriorReliefSlider", "InteriorReliefSlider");
			OrogenyStrengthSlider = FindParamNode<HSlider>($"{MountainRoot}/OrogenyStrengthRow/OrogenyStrengthSlider", "OrogenyStrengthSlider");
			SubductionArcRatioSlider = FindParamNode<HSlider>($"{MountainRoot}/SubductionArcRatioRow/SubductionArcRatioSlider", "SubductionArcRatioSlider");
			ContinentalAgeSlider = FindParamNode<HSlider>($"{MountainRoot}/ContinentalAgeRow/ContinentalAgeSlider", "ContinentalAgeSlider");
			RiversSwitch = FindParamNode<BaseButton>($"{ParamsRoot}/RiversGroup/RiversSwitch", "RiversSwitch");
			RiverDensitySlider = FindParamNode<HSlider>($"{ParamsRoot}/RiverDensityRow/RiverDensitySlider", "RiverDensitySlider");
			SeaLevelValue = FindParamNode<Label>($"{ParamsRoot}/SeaWrap/SeaLevelValue", "SeaLevelValue");
			HeatValue = FindParamNode<Label>($"{ParamsRoot}/HeatWrap/HeatValue", "HeatValue");
			MoistureValue = FindParamNode<Label>($"{ParamsRoot}/MoistureWrap/MoistureValue", "MoistureValue");
			ErosionValue = FindParamNode<Label>($"{ParamsRoot}/ErosionWrap/ErosionValue", "ErosionValue");
			InteriorReliefValue = FindParamNode<Label>($"{MountainRoot}/InteriorReliefRow/InteriorReliefValue", "InteriorReliefValue");
			OrogenyStrengthValue = FindParamNode<Label>($"{MountainRoot}/OrogenyStrengthRow/OrogenyStrengthValue", "OrogenyStrengthValue");
			SubductionArcRatioValue = FindParamNode<Label>($"{MountainRoot}/SubductionArcRatioRow/SubductionArcRatioValue", "SubductionArcRatioValue");
			ContinentalAgeValue = FindParamNode<Label>($"{MountainRoot}/ContinentalAgeRow/ContinentalAgeValue", "ContinentalAgeValue");
			RiverDensityValue = FindParamNode<Label>($"{ParamsRoot}/RiverDensityRow/RiverDensityValue", "RiverDensityValue");

			if (RandomButton != null) RandomButton.Pressed += () => RandomRequested?.Invoke();
			if (ApplyButton != null) ApplyButton.Pressed += () => ApplyRequested?.Invoke();
			if (SeaLevelSlider != null) SeaLevelSlider.ValueChanged += value => SeaLevelChanged?.Invoke(value);
			if (HeatSlider != null) HeatSlider.ValueChanged += value => HeatChanged?.Invoke(value);
			if (MoistureSlider != null) MoistureSlider.ValueChanged += value => MoistureChanged?.Invoke(value);
			if (ErosionSlider != null) ErosionSlider.ValueChanged += value => ErosionChanged?.Invoke(value);
			if (InteriorReliefSlider != null) InteriorReliefSlider.ValueChanged += value => InteriorReliefChanged?.Invoke(value);
			if (OrogenyStrengthSlider != null) OrogenyStrengthSlider.ValueChanged += value => OrogenyStrengthChanged?.Invoke(value);
			if (SubductionArcRatioSlider != null) SubductionArcRatioSlider.ValueChanged += value => SubductionArcRatioChanged?.Invoke(value);
			if (ContinentalAgeSlider != null) ContinentalAgeSlider.ValueChanged += value => ContinentalAgeChanged?.Invoke(value);
			if (RiversSwitch != null) RiversSwitch.Toggled += value => RiversToggled?.Invoke(value);
			if (RiverDensitySlider != null) RiverDensitySlider.ValueChanged += value => RiverDensityChanged?.Invoke(value);
		}

		TabChanged += OnTabChanged;
	}

	private void UpdateTabTitles()
	{
		for (var index = 0; index < GetTabCount(); index++)
		{
			var child = GetTabControl(index);
			if (child == null) continue;
			var iconPrefix = child.Name.ToString() switch
			{
				"LayersPage" => "📑 观测图层",
				"AdvancedPage" => "⚙️ 观察法则",
				"LorePanel" => "📜 叙事推演",
				"HistoryPage" => "⏳ 演化编年",
				"ParamsPage" => "🗺️ 地貌参数",
				_ => child.HasMeta("_tab_title") ? child.GetMeta("_tab_title").AsString() : child.Name.ToString()
			};
			SetTabTitle(index, iconPrefix);
		}
	}

	private T? FindParamNode<T>(string relativePath, string fallbackName) where T : class
	{
		return (GetNodeOrNull<Node>(relativePath) ?? FindChild(fallbackName, true, false)) as T;
	}

	private void OnTabChanged(long tabIndex)
	{
		var currentChild = GetCurrentTabControl();
		if (currentChild != null)
		{
			currentChild.Modulate = new Color(1, 1, 1, 0.4f);
			var tween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tween.TweenProperty(currentChild, "modulate:a", 1.0f, 0.16f);
		}
	}

	/// <summary>Selects a console tab by index; the tab strip owns the visual transition.</summary>
	public void ShowPage(int index)
	{
		CurrentTab = Mathf.Clamp(index, 0, GetTabCount() - 1);
	}
}
