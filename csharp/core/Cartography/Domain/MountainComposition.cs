using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 山脉采样点元数据。
/// </summary>
public sealed record MountainSpineSample(
    PolyVec2 Position,
    float Elevation,
    float TangentAngle,
    float RidgeWidth,
    float DistanceAlongSpine,
    bool IsPass);

/// <summary>
/// 山脉侧向支脉（Spur / Branch Ridge）。
/// </summary>
public sealed class MountainBranchRidge
{
    public required IReadOnlyList<MountainSpineSample> Points { get; init; }
    public float SideSign { get; init; } // -1 = 左侧, +1 = 右侧
    public float BranchAngle { get; init; }
    public float Length { get; init; }
}

/// <summary>
/// 结构化山峰节点（主峰或拱卫伴峰）。
/// </summary>
public sealed record MountainPeakNode(
    PolyVec2 Position,
    float Elevation,
    float Scale,
    bool IsDominant,
    bool IsSnow,
    float TangentAngle,
    string VariantKey);

/// <summary>
/// 单个山系的完整山水画构图对象 (MountainRangeComposition)。
/// 
/// 表达“主脊走向 + 侧向支脉 + 巍峨主峰 + 拱卫伴峰 + 横卧连脊 + 隘口峡谷 + 山脚流岚”的
/// 东方传统青绿山水空间层次组合。
/// </summary>
public sealed class MountainRangeComposition
{
    public required int RegionId { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<MountainSpineSample> SpineSamples { get; init; }
    public required IReadOnlyList<MountainBranchRidge> Branches { get; init; }
    public required IReadOnlyList<MountainPeakNode> DominantPeaks { get; init; }
    public required IReadOnlyList<MountainPeakNode> CompanionPeaks { get; init; }
    public required IReadOnlyList<PolyVec2> Passes { get; init; }
    public float SnowElevationThreshold { get; init; }
}
