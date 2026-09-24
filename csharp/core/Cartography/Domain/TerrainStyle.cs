namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 幻想地形艺术风格类型。
/// 将底层的物理 Biome 与地质参数解耦，映射为制图表现层的艺术风格。
/// </summary>
public enum TerrainStyle
{
    Plain,          // 平原旷野
    AncientForest,  // 远古密林（古树/藤蔓/浓郁石绿/深邃晨雾）
    DenseForest,    // 茂密丛林（繁茂林海/松竹交柯）
    SnowMountain,   // 北冥雪山（雪帽玉峰/空灵霁蓝/冰河轻岚）
    RockMountain,   // 青翠岩峦（重岩叠嶂/焦墨勾勒/黛色山体）
    Desert,         // 金沙大漠（新月沙垄/赭石水晕/沙海绿洲）
    Swamp,          // 烟雨湿地（蒹葭芦荡/连绵浅渚/薄暮湿气）
    Volcano,        // 赤峦火山（玄武赤岩/硫磺焦墨/丹砂赤火）
    Coastal,        // 烟波海岸（碧蓝微澜/津渡帆影/金石石岸）
    Hills,          // 翠峦丘陵（连绵起伏/梯田小品/茶丘秀色）
    Plateau         // 巍峨高原（桌状平顶/红砂断崖/空旷苍茫）
}
