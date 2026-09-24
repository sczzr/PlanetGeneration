using System;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 地理战略锚定类型。
/// </summary>
public enum SettlementAnchorType
{
    InlandCapital,       // 天下神都 / 沃野中枢
    RiverConfluence,     // 两江汇流 / 江汉重镇
    MountainPass,        // 雄关要隘 / 锁钥锁喉
    NaturalBayHarbor,    // 半月海港 / 泊舟津渡
    SacredSanctuary,     // 仙山洞府 / 宝刹佛寺
    PlainCrossroad       // 通衢驿市 / 农桑小邑
}

/// <summary>
/// 战略聚落构图蓝图（StrategicSettlementBlueprint）。
/// 
/// 确保城市不再由细胞自动机随机漫射生成，而是严格服务于地理要冲与交通网络。
/// </summary>
public sealed class StrategicSettlementBlueprint
{
    public int Id { get; init; } = 1;
    public string Name { get; init; } = "神京天都";

    /// <summary>五级规范等级。</summary>
    public CartographySettlementTier Tier { get; init; } = CartographySettlementTier.Capital;

    /// <summary>地理锚定类型。</summary>
    public SettlementAnchorType AnchorType { get; init; } = SettlementAnchorType.InlandCapital;

    /// <summary>规划位置（归一化坐标 0.0 ~ 1.0 或世界坐标）。</summary>
    public PolyVec2 Position { get; init; }

    /// <summary>政治/经济重要度（1~4）。</summary>
    public int Importance { get; init; } = 4;

    /// <summary>是否必须在顶层渲染书法题注。</summary>
    public bool ShowLabel { get; init; } = true;

    /// <summary>描述或背景设定。</summary>
    public string Description { get; init; } = string.Empty;
}
