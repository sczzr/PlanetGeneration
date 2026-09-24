using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 宏观山系结构化设计定义（MountainRangeDefinition）。
/// 
/// 承担地图骨架分割（Spatial Divider）的核心职责：
/// 1. 空间分割角色：划分南北或东西不同生态区域；
/// 2. 主脊骨架样条（Spine）：连续平滑中轴，厚重山体搭接；
/// 3. 弧形开展支脉（Curved Spurs）：向平原侧弧度围合，形成山间盆地与谷地；
/// 4. 巍峨主峰与雪峰（Major Peaks）与战略关隘隘口（Passes）。
/// </summary>
public sealed class MountainRangeDefinition
{
    public int Id { get; init; } = 1;
    public string Name { get; init; } = "苍冥天脊";
    public string Description { get; init; } = string.Empty;

    /// <summary>空间分割角色定义：所阻隔/分隔的两个宏观地理大区名称。</summary>
    public string DividingRegionA { get; init; } = "极北高寒";
    public string DividingRegionB { get; init; } = "中原天府";

    /// <summary>主脊起点坐标（归一化 [0, 1]）。</summary>
    public PolyVec2 StartPoint { get; init; } = new(0.16, 0.33);

    /// <summary>主脊中继曲线控制点集合（归一化 [0, 1]）。</summary>
    public List<PolyVec2> ControlPoints { get; init; } = new();

    /// <summary>主脊终点坐标（归一化 [0, 1]）。</summary>
    public PolyVec2 EndPoint { get; init; } = new(0.84, 0.35);

    /// <summary>主脊宽度包络（像素）。</summary>
    public float RidgeWidth { get; init; } = 52.0f;

    /// <summary>主要主峰在主脊上的归一化位置比例 [0, 1]。</summary>
    public List<float> MajorPeakRatios { get; init; } = new() { 0.46f, 0.72f };

    /// <summary>山门隘口（关隘）在主脊上的归一化位置比例 [0, 1]。</summary>
    public List<float> PassRatios { get; init; } = new() { 0.30f };

    /// <summary>向内陆舒展的侧向支脉数量。</summary>
    public int SpurCount { get; init; } = 5;

    /// <summary>支脉平均伸展长度（像素）。</summary>
    public float SpurLength { get; init; } = 58.0f;

    /// <summary>是否拥有皑皑雪峰绝顶。</summary>
    public bool HasSnowCap { get; init; } = true;

    /// <summary>山脉水墨调色盘。</summary>
    public CartographyColor Palette { get; init; } = CartographyColor.EmeraldGreen;
}
