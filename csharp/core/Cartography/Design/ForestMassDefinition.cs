using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 森林多层密度衰减曲线类型。
/// </summary>
public enum ForestDensityCurveType
{
    Linear,
    Sigmoid,
    SmoothStep
}

/// <summary>
/// 体块化林海结构化设计定义（ForestMassDefinition）。
/// 
/// 实现水墨手绘地图中由内而外的 4 级自然层次衰减：
/// 1. 密林核心（Core: 100% 大水墨林冠簇，深幽浩荡）；
/// 2. 茂密树林（Woodland: 70% 双木/单木组，疏朗有致）；
/// 3. 疏林灌木（Shrub: 30% 低矮灌木点缀，渐次稀疏）；
/// 4. 边缘草甸（Meadow: 0% 树木彻底停放，自然消融于平原）；
/// 5. 林间隙地（Clearings[]: 穿插在林海腹地的透气留白孔，杜绝死绿实块）。
/// </summary>
public sealed class ForestMassDefinition
{
    public int Id { get; init; } = 2;
    public string Name { get; init; } = "太古青岚林海";
    public string Description { get; init; } = string.Empty;

    /// <summary>林海几何中心（归一化 [0, 1]）。</summary>
    public PolyVec2 Center { get; init; } = new(0.72, 0.40);

    /// <summary>横向半轴跨度（归一化 [0, 1]）。</summary>
    public float RadiusX { get; init; } = 0.18f;

    /// <summary>纵向半轴跨度（归一化 [0, 1]）。</summary>
    public float RadiusY { get; init; } = 0.14f;

    /// <summary>区域旋转倾角（弧度）。</summary>
    public float Rotation { get; init; } = -0.22f;

    /// <summary>核心林冠密度（默认 1.0f）。</summary>
    public float CoreDensity { get; init; } = 1.0f;

    /// <summary>茂密树林层密度（默认 0.70f）。</summary>
    public float WoodlandDensity { get; init; } = 0.70f;

    /// <summary>外缘灌木层密度（默认 0.30f）。</summary>
    public float ShrubDensity { get; init; } = 0.30f;

    /// <summary>外缘平原草甸密度（0.0f，绝不放树）。</summary>
    public float MeadowDensity { get; init; } = 0.0f;

    /// <summary>衰减曲线类型。</summary>
    public ForestDensityCurveType DensityCurve { get; init; } = ForestDensityCurveType.SmoothStep;

    /// <summary>林间透气留白空地列表。</summary>
    public List<ForestClearing> Clearings { get; init; } = new();

    /// <summary>林海深翠调色盘。</summary>
    public CartographyColor Palette { get; init; } = CartographyColor.DeepForest;
}
