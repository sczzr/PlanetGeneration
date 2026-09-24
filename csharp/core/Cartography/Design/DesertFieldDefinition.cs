using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 瀚海流沙与风蚀地貌结构化设计定义（DesertFieldDefinition）。
/// 
/// 彻底解决“沙丘素材随意散布”的问题，构建沿风向延展的连绵沙垄与绿洲生命线：
/// 1. 主风向角（WindAngle）；
/// 2. 平行新月沙垄走廊轴线（DuneCorridors: ~~~~~~）；
/// 3. 大漠深处清泉与胡杨绿洲（Oases: 丝路歇足点）；
/// 4. 干涸台地裂谷与残岩露头（RockOutcrops）。
/// </summary>
public sealed class DesertFieldDefinition
{
    public int Id { get; init; } = 4;
    public string Name { get; init; } = "狂沙金墟";

    /// <summary>大漠几何中心（归一化 [0, 1]）。</summary>
    public PolyVec2 Center { get; init; } = new(0.32, 0.72);

    /// <summary>横向半轴跨度（归一化 [0, 1]）。</summary>
    public float RadiusX { get; init; } = 0.18f;

    /// <summary>纵向半轴跨度（归一化 [0, 1]）。</summary>
    public float RadiusY { get; init; } = 0.13f;

    /// <summary>主风向角（弧度）。沙垄垂直或平行于风向延展。</summary>
    public float WindAngle { get; init; } = 0.38f;

    /// <summary>沙垄走廊数量（默认 3 条平行带）。</summary>
    public int CorridorCount { get; init; } = 3;

    /// <summary>大漠绿洲坐标列表（归一化 [0, 1]）。</summary>
    public List<PolyVec2> Oases { get; init; } = new()
    {
        new(0.35, 0.74)
    };

    /// <summary>大漠暖黄调色盘。</summary>
    public CartographyColor Palette { get; init; } = CartographyColor.SandyOchre;
}
