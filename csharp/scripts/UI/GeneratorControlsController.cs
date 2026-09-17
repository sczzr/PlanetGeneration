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
	public HSlider ErosionSlider { get; private set; } = null!;
	public HSlider InteriorReliefSlider { get; private set; } = null!;
	public HSlider OrogenyStrengthSlider { get; private set; } = null!;
	public HSlider SubductionArcRatioSlider { get; private set; } = null!;
	public HSlider ContinentalAgeSlider { get; private set; } = null!;
	public BaseButton RiversSwitch { get; private set; } = null!;
	public HSlider RiverDensitySlider { get; private set; } = null!;
	public Label SeaLevelValue { get; private set; } = null!;
	public Label HeatValue { get; private set; } = null!;
	public Label ErosionValue { get; private set; } = null!;
	public Label InteriorReliefValue { get; private set; } = null!;
	public Label OrogenyStrengthValue { get; private set; } = null!;
	public Label SubductionArcRatioValue { get; private set; } = null!;
	public Label ContinentalAgeValue { get; private set; } = null!;
	public Label RiverDensityValue { get; private set; } = null!;

	private const string ParamsRoot = "ParamsPage/ParamsMargin/Content/ParamsBox";
	private const string MountainRoot = $"{ParamsRoot}/MountainBody/MountainBodyContent";

	private static readonly string[] TabTitles = { "地形配置", "图层", "高级设置", "世界与AI", "历史统计" };

	public override void _Ready()
	{
		for (var index = 0; index < TabTitles.Length && index < GetTabCount(); index++)
		{
			SetTabTitle(index, TabTitles[index]);
		}

		RandomButton = GetNode<Button>($"{ParamsRoot}/SeedRow/RandomButton");
		ApplyButton = GetNode<Button>($"{ParamsRoot}/SeedRow/ApplyButton");
		SeedSpin = GetNode<SpinBox>($"{ParamsRoot}/SeedRow/SeedSpin");
		SeaLevelSlider = GetNode<HSlider>($"{ParamsRoot}/SeaWrap/SeaLevelSlider");
		HeatSlider = GetNode<HSlider>($"{ParamsRoot}/HeatWrap/HeatSlider");
		ErosionSlider = GetNode<HSlider>($"{ParamsRoot}/ErosionWrap/ErosionSlider");
		InteriorReliefSlider = GetNode<HSlider>($"{MountainRoot}/InteriorReliefRow/InteriorReliefSlider");
		OrogenyStrengthSlider = GetNode<HSlider>($"{MountainRoot}/OrogenyStrengthRow/OrogenyStrengthSlider");
		SubductionArcRatioSlider = GetNode<HSlider>($"{MountainRoot}/SubductionArcRatioRow/SubductionArcRatioSlider");
		ContinentalAgeSlider = GetNode<HSlider>($"{MountainRoot}/ContinentalAgeRow/ContinentalAgeSlider");
		RiversSwitch = GetNode<BaseButton>($"{ParamsRoot}/RiversGroup/RiversSwitch");
		RiverDensitySlider = GetNode<HSlider>($"{ParamsRoot}/RiverDensityRow/RiverDensitySlider");
		SeaLevelValue = GetNode<Label>($"{ParamsRoot}/SeaWrap/SeaLevelValue");
		HeatValue = GetNode<Label>($"{ParamsRoot}/HeatWrap/HeatValue");
		ErosionValue = GetNode<Label>($"{ParamsRoot}/ErosionWrap/ErosionValue");
		InteriorReliefValue = GetNode<Label>($"{MountainRoot}/InteriorReliefRow/InteriorReliefValue");
		OrogenyStrengthValue = GetNode<Label>($"{MountainRoot}/OrogenyStrengthRow/OrogenyStrengthValue");
		SubductionArcRatioValue = GetNode<Label>($"{MountainRoot}/SubductionArcRatioRow/SubductionArcRatioValue");
		ContinentalAgeValue = GetNode<Label>($"{MountainRoot}/ContinentalAgeRow/ContinentalAgeValue");
		RiverDensityValue = GetNode<Label>($"{ParamsRoot}/RiverDensityRow/RiverDensityValue");

		RandomButton.Pressed += () => RandomRequested?.Invoke();
		ApplyButton.Pressed += () => ApplyRequested?.Invoke();
		SeaLevelSlider.ValueChanged += value => SeaLevelChanged?.Invoke(value);
		HeatSlider.ValueChanged += value => HeatChanged?.Invoke(value);
		ErosionSlider.ValueChanged += value => ErosionChanged?.Invoke(value);
		InteriorReliefSlider.ValueChanged += value => InteriorReliefChanged?.Invoke(value);
		OrogenyStrengthSlider.ValueChanged += value => OrogenyStrengthChanged?.Invoke(value);
		SubductionArcRatioSlider.ValueChanged += value => SubductionArcRatioChanged?.Invoke(value);
		ContinentalAgeSlider.ValueChanged += value => ContinentalAgeChanged?.Invoke(value);
		RiversSwitch.Toggled += value => RiversToggled?.Invoke(value);
		RiverDensitySlider.ValueChanged += value => RiverDensityChanged?.Invoke(value);
	}

	/// <summary>Selects a console tab by index; the tab strip owns the visual transition.</summary>
	public void ShowPage(int index)
	{
		CurrentTab = Mathf.Clamp(index, 0, GetTabCount() - 1);
	}
}
