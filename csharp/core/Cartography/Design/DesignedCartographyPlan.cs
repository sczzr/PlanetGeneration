using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Domain;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 经过导演层编译后的结构化制图计划（DesignedCartographyPlan）。
/// 
/// 携带已投射至多边形网格空间的宏观山系龙骨、林海斑块、大漠瀚海、战略聚落与区域风格规则，
/// 直接交付给各大表现层画师（MapPainters）进行大块面插画级渲染。
/// </summary>
public sealed class DesignedCartographyPlan
{
    public required MapBlueprint Blueprint { get; init; }

    /// <summary>导演规划的宏观自然地理实体（山系龙骨、林海大斑块、大漠瀚海）。</summary>
    public required IReadOnlyList<MegaTerrainRegion> MegaRegions { get; init; }

    /// <summary>显式叙事地理大区实体（Narrative Regions: 包含体块化林海、主脉支脉山系等）。</summary>
    public IReadOnlyList<NarrativeRegion>? NarrativeRegions { get; init; }

    /// <summary>导演规划的战略要冲聚落（国都、江畔大都、隘口要塞、津渡港口）。</summary>
    public required IReadOnlyList<SettlementInfo> Settlements { get; init; }

    /// <summary>各区域色彩与艺术风格规则。</summary>
    public required Dictionary<int, RegionStyle> RegionStyles { get; init; }
}
