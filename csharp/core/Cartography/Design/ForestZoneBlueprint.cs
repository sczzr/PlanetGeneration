using System;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 宏观林海大斑块构图蓝图（ForestZoneBlueprint）。
/// 
/// 定义林海的大块面包络范围、林心浓郁度与边缘衰减半径，
/// 解决“全图随机散乱撒树”的问题，形成古画中凝聚深幽的大片水墨林海。
/// </summary>
public sealed class ForestZoneBlueprint
{
    public int Id { get; init; } = 1;
    public string Name { get; init; } = "太古青岚林海";

    /// <summary>林海中心位置（归一化坐标 0.0 ~ 1.0 或世界坐标）。</summary>
    public PolyVec2 Center { get; init; }

    /// <summary>半长轴半径。</summary>
    public float RadiusX { get; init; } = 0.16f;

    /// <summary>半短轴半径。</summary>
    public float RadiusY { get; init; } = 0.12f;

    /// <summary>倾斜旋转角（弧度）。</summary>
    public float Rotation { get; init; } = 0.0f;

    /// <summary>林心核心区树木聚集密度（0.0 ~ 1.0）。</summary>
    public float CoreDensity { get; init; } = 0.95f;

    /// <summary>外缘渐变过渡带宽度比率。</summary>
    public float FadeMargin { get; init; } = 0.35f;

    /// <summary>色彩基调。</summary>
    public CartographyColor Palette { get; init; } = CartographyColor.DeepForest;
}
