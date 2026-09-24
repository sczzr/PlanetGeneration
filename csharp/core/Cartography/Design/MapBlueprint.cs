using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 区域宏观生态分类。
/// </summary>
public enum RegionEcologyType
{
    MountainHighland, // 崇山峻岭 / 天柱绝顶
    AncientForest,    // 太古林海 / 青岚幽谷
    CentralPlains,    // 中原沃土 / 天府原野
    AridDesert,       // 瀚海流沙 / 狂风大漠
    RiverBasin,       // 盆地水乡 / 湖泽平原
    CoastalBay,       // 沧海重关 / 滨海津渡
    PolarTundra,      // 北境冰原 / 极地高寒
    WesternForest,    // 西境森林 / 云杉松林
    SouthernWetlands  // 南部湿地 / 云梦水泽
}

/// <summary>
/// 宏观地理大区规划蓝图（RegionBlueprint）。
/// </summary>
public sealed class RegionBlueprint
{
    public int Id { get; init; } = 1;
    public string Name { get; init; } = "中原天府";
    public RegionEcologyType EcologyType { get; init; } = RegionEcologyType.CentralPlains;
    public PolyVec2 Center { get; init; }
    public float ExtentRadius { get; init; } = 0.25f;
    public CartographyColor Palette { get; init; } = CartographyColor.EmeraldGreen;
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// 地图构图总蓝图（MapBlueprint）。
/// 
/// 作为“地图导演层（CartographyDesigner）”的核心数据输入，
/// 决定整张地图宏观 80% 的构图、视觉焦点、山脉骨骼走向与战略枢纽布局。
/// </summary>
public sealed class MapBlueprint
{
    public string Name { get; init; } = "FantasyContinent";
    public string Description { get; init; } = string.Empty;

    /// <summary>宏观生态区域规划。</summary>
    public List<RegionBlueprint> Regions { get; init; } = new();

    /// <summary>显式叙事大区规划（Narrative Regions: 苍冥群岳、大荒林海、狂沙绝境、中原天府等）。</summary>
    public List<NarrativeRegion> NarrativeRegions { get; init; } = new();

    /// <summary>结构化山系空间骨架定义列表。</summary>
    public List<MountainRangeDefinition> MountainRanges { get; init; } = new();

    /// <summary>结构化体块林海定义列表。</summary>
    public List<ForestMassDefinition> ForestMasses { get; init; } = new();

    /// <summary>结构化树状平滑水系定义列表。</summary>
    public List<RiverNetworkDefinition> RiverNetworks { get; init; } = new();

    /// <summary>中央平原丰富度细节层定义。</summary>
    public PlainsDetailDefinition? PlainsDetail { get; set; }

    /// <summary>结构化风向大漠定义列表。</summary>
    public List<DesertFieldDefinition> DesertFields { get; init; } = new();

    /// <summary>地貌节点战略聚落定义列表。</summary>
    public List<SettlementDefinition> SettlementDefinitions { get; init; } = new();

    /// <summary>宏观山系骨架规划（整条大山脉中轴、包络宽度、主峰与关隘，兼容向后）。</summary>
    public List<MountainSpineBlueprint> MountainSpines { get; init; } = new();

    /// <summary>宏观林海大斑块规划（林心浓郁包络与衰减外缘，兼容向后）。</summary>
    public List<ForestZoneBlueprint> ForestZones { get; init; } = new();

    /// <summary>瀚海大漠风貌规划（大漠中心、风向沙垄轴线与密度，兼容向后）。</summary>
    public List<DesertZoneBlueprint> DesertZones { get; init; } = new();

    /// <summary>母亲河走廊规划（发源于山脉，流经平原，注入沧海，兼容向后）。</summary>
    public List<RiverCorridorBlueprint> RiverCorridors { get; init; } = new();

    /// <summary>战略节点与聚落规划（神都、江畔大都、隘口要塞、半月海港，兼容向后）。</summary>
    public List<StrategicSettlementBlueprint> Settlements { get; init; } = new();

    /// <summary>干线商道与驿径规划（依山傍水、连通要冲，兼容向后）。</summary>
    public List<TradeHighwayBlueprint> Highways { get; init; } = new();
}
