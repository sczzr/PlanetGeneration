using Godot;

namespace PlanetGeneration.Rendering;

/// <summary>
/// A separate grass/mud/contact-shadow texture attached to the map plane.
/// Use under the object's foot anchor. All upright objects in this canvas must use
/// GroundDecalLayers.Body; terrain must use GroundDecalLayers.Terrain.
/// </summary>
[Tool]
public partial class GroundDecal : Sprite2D
{
    public override void _EnterTree()
    {
        ZAsRelative = false;
        ZIndex = GroundDecalLayers.Decal;
    }
}
