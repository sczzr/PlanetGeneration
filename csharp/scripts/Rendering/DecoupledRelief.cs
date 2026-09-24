using Godot;

namespace PlanetGeneration.Rendering;

/// <summary>Compatibility entry point for mountain scenes; uses the same layer contract as every object.</summary>
[Tool]
public partial class DecoupledRelief : UniversalDecoupledObject
{
    public DecoupledRelief()
    {
        SplitMode = DecoupledSplitMode.ColorFeature;
    }

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float SkirtYStart { get => FeatureYStart; set => FeatureYStart = value; }

    [Export]
    public int SkirtZIndex { get => BottomZIndex; set => BottomZIndex = value; }
}
