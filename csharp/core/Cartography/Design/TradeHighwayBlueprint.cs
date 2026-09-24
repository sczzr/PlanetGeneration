using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 战略商道与古道驿径蓝图（TradeHighwayBlueprint）。
/// 
/// 规划大陆干线商旅通道、丝绸之路与江防驿径。
/// </summary>
public sealed class TradeHighwayBlueprint
{
    public int Id { get; init; } = 1;
    public string Name { get; init; } = "天都南巡驿道";

    /// <summary>起点聚落名称。</summary>
    public string FromSettlement { get; init; } = string.Empty;

    /// <summary>终点聚落名称。</summary>
    public string ToSettlement { get; init; } = string.Empty;

    /// <summary>指定中继经由点（归一化坐标或世界坐标）。</summary>
    public List<PolyVec2> Waypoints { get; init; } = new();

    /// <summary>干线道路等级（1: 丝路官道, 2: 寻常驿径）。</summary>
    public int HighwayTier { get; init; } = 1;
}
