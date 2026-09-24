using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 江河单条支流定义（RiverBranchDefinition）。
/// </summary>
public sealed class RiverBranchDefinition
{
    public string Name { get; init; } = "天水北支";
    public PolyVec2 SourcePoint { get; init; }
    public List<PolyVec2> Waypoints { get; init; } = new();
    public PolyVec2 ConfluencePoint { get; init; }
    public float WidthScale { get; init; } = 1.0f;
}

/// <summary>
/// 树状水系与湖泊网络结构化设计定义（RiverNetworkDefinition）。
/// 
/// 彻底解决“断裂蓝色短线段”问题，建立具有清晰地理脉络的大江水系：
/// 结构：湖泊/雪峰源头 ──> 主干龙江 ──> 沿途纳各路支流 ──> 汇聚清平大湖 ──> 宽阔三角洲入海。
/// </summary>
public sealed class RiverNetworkDefinition
{
    public int Id { get; init; } = 1;
    public string Name { get; init; } = "天水龙江水系";

    /// <summary>主江源头（归一化 [0, 1]）。</summary>
    public PolyVec2 SourcePoint { get; init; } = new(0.35, 0.28);

    /// <summary>主干九曲蜿蜒中继路径控制点序列（归一化 [0, 1]）。</summary>
    public List<PolyVec2> MainWaypoints { get; init; } = new()
    {
        new(0.42, 0.38),
        new(0.50, 0.48),
        new(0.62, 0.55),
        new(0.74, 0.62)
    };

    /// <summary>主江入海口（归一化 [0, 1]）。</summary>
    public PolyVec2 MouthPoint { get; init; } = new(0.85, 0.68);

    /// <summary>主江源头宽度（像素）。</summary>
    public float SourceWidth { get; init; } = 2.4f;

    /// <summary>主江入海河口宽度（像素）。</summary>
    public float MouthWidth { get; init; } = 8.5f;

    /// <summary>各级支流列表。</summary>
    public List<RiverBranchDefinition> Branches { get; init; } = new();

    /// <summary>水系串联或滋养的重要湖泊坐标列表（归一化 [0, 1]）。</summary>
    public List<PolyVec2> Lakes { get; init; } = new()
    {
        new(0.44, 0.43), // 碧玉湖
        new(0.55, 0.58)  // 清平渚
    };

    /// <summary>水系水墨青碧配色。</summary>
    public CartographyColor Palette { get; init; } = CartographyColor.ColdBlue;
}
