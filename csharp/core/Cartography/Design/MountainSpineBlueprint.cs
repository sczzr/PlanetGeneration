using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 宏观山系脊线构图蓝图（MountainSpineBlueprint）。
/// 
/// 定义整条山系的宏观走向、起止中轴、山体宽度包络与主峰/关隘点位，
/// 解决“随机散落小山”的问题，形成有气势、连续大块面形态语言的“大陆山龙骨架”。
/// </summary>
public sealed class MountainSpineBlueprint
{
    public int Id { get; init; } = 1;
    public string Name { get; init; } = "苍冥天脊";

    /// <summary>起点（归一化坐标 0.0 ~ 1.0 或世界坐标）。</summary>
    public PolyVec2 StartPoint { get; init; }

    /// <summary>样条曲线控制点序列（决定山系宏观弯曲折向）。</summary>
    public List<PolyVec2> ControlPoints { get; init; } = new();

    /// <summary>终点。</summary>
    public PolyVec2 EndPoint { get; init; }

    /// <summary>山体横向包络宽度（世界逻辑像素或归一化宽度）。</summary>
    public float SpineWidth { get; init; } = 48.0f;

    /// <summary>主峰沿脊线分布比例（0.0 ~ 1.0），例如 0.45、0.70 处拔起主峰。</summary>
    public List<float> MajorPeakRatios { get; init; } = new() { 0.45f, 0.70f };

    /// <summary>险要隘口关隘分布比例（0.0 ~ 1.0），例如 0.32 处开辟山口并建雄关。</summary>
    public List<float> PassRatios { get; init; } = new() { 0.32f };

    /// <summary>是否终年积雪/雪峰绝顶。</summary>
    public bool HasSnowCap { get; init; } = true;

    /// <summary>主色彩调色调。</summary>
    public CartographyColor Palette { get; init; } = CartographyColor.EmeraldGreen;

    /// <summary>山系重要度评级（世界地标/大区级）。</summary>
    public int Rank { get; init; } = 3;
}
