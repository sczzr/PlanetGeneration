using System;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 瀚海大漠风貌构图蓝图（DesertZoneBlueprint）。
/// 
/// 定义大漠的宏观位置、风向沙垄轴线与沙丘密度，形成大漠孤烟、气韵流动的瀚海沙海。
/// </summary>
public sealed class DesertZoneBlueprint
{
    public int Id { get; init; } = 1;
    public string Name { get; init; } = "狂沙金墟";

    /// <summary>大漠中心位置（归一化坐标 0.0 ~ 1.0 或世界坐标）。</summary>
    public PolyVec2 Center { get; init; }

    /// <summary>横向半轴半径。</summary>
    public float RadiusX { get; init; } = 0.18f;

    /// <summary>纵向半轴半径。</summary>
    public float RadiusY { get; init; } = 0.12f;

    /// <summary>主风向与沙垄倾角（弧度）。</summary>
    public float WindAngle { get; init; } = 0.35f;

    /// <summary>沙垄数量与密度系数。</summary>
    public float DuneDensity { get; init; } = 1.0f;

    /// <summary>色彩基调。</summary>
    public CartographyColor Palette { get; init; } = CartographyColor.SandyOchre;
}
