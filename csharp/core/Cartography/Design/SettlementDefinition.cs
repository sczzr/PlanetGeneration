using System;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 战略聚落地理节点类型。
/// 遵循“城市 = 资源 + 交通 + 地形节点”军规，绝非无序散布。
/// </summary>
public enum GeographicAnchorType
{
    MountainPass,       // 锁山隘口（如雁门雄关）
    RiverConfluence,    // 江汉交汇/曲流凹岸要邑（如江陵大都）
    NaturalBayHarbor,   // 海河交汇深水良湾巨港（如临海沧津）
    HeartlandCapital,   // 平原腹心/龙脉汇聚国都（如神京天都）
    OasisCrossroad,     // 大漠清泉/驼铃丝路古堡（如金沙古堡）
    SacredSanctuary,    // 名山洞天/林深隐修古刹（如青岚道观）
    CanalWaterTown,     // 运河泽国水乡重郭（如姑苏水郡）
    NorthwestBorderCity,// 西北丝路名郡（如陇右名都）
    MountainFortress,   // 山道险关要塞（如剑阁险关）
    RiverFerryPort,     // 大江咽喉渡口（如浔阳古埠）
    RiverGarrison,      // 江防要塞水寨（如襄樊水寨）
    SacredTown,         // 仙山脚下道镇（如武当仙镇）
    MountainMiningCity, // 北山脚下矿业重城（如北麓铁府）
    ForestMarginCity,   // 森林边缘林业名郡（如翠微古郡）
    AncientRelicSite,   // 古林深处上古遗迹（如太古神殿）
    WetlandWaterTown,   // 南部湿地泽国水城（如云梦泽城）
    AgriculturalVillage,// 阡陌农耕村落（如杏花古村/桑麻村）
    ValleyHaven,        // 幽谷隐逸避世村落（如桃源坞）
    FerryVillage,       // 渡口古村集市（如枫林晚渡）
    CoastalHaven,       // 滨海渔港渔村（如渔歌泊）
    PlainAgriculturalCity,// 平原西侧粮仓农业名都（如丰泽古郡）
    OasisTradeCity      // 瀚海清泉丝路贸易节点（如沙洲新城）
}

/// <summary>
/// 战略聚落结构化设计定义（SettlementDefinition）。
/// </summary>
public sealed class SettlementDefinition
{
    public int Id { get; init; } = 1;
    public string Name { get; init; } = "神京天都";
    public CartographySettlementTier Tier { get; init; } = CartographySettlementTier.Capital;
    public GeographicAnchorType AnchorType { get; init; } = GeographicAnchorType.HeartlandCapital;
    public PolyVec2 Position { get; init; } = new(0.48, 0.46);
    public int Importance { get; init; } = 4;
    public bool ShowLabel { get; init; } = true;
    public string Description { get; init; } = string.Empty;
}
