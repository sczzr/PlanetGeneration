namespace PlanetGeneration.Core.Domain;

/// <summary>板块边界类型。</summary>
public enum PlateBoundaryType : byte
{
    None = 0,
    Convergent = 1,
    Divergent = 2,
    Transform = 3,
}

/// <summary>生物群系类型。</summary>
public enum BiomeType : byte
{
    Ocean = 0,
    ShallowOcean = 1,
    Coastland = 2,
    Ice = 3,
    Tundra = 4,
    BorealForest = 5,
    Taiga = 6,
    Steppe = 7,
    Grassland = 8,
    Chaparral = 9,
    TemperateDesert = 10,
    TemperateSeasonalForest = 11,
    TemperateRainForest = 12,
    Savanna = 13,
    Shrubland = 14,
    TropicalDesert = 15,
    TropicalSeasonalForest = 16,
    TropicalRainForest = 17,
    RockyMountain = 18,
    SnowyMountain = 19,
    River = 20,
}

/// <summary>岩石类型。</summary>
public enum RockType : byte
{
    Sedimentary = 0,
    Igneous = 1,
    Metamorphic = 2,
}

/// <summary>资源大类分类。</summary>
public enum ResourceCategory : byte
{
    None = 0,
    /// <summary>基础工业矿产：解决文明发展的“物质”问题，地质驱动。</summary>
    Industrial = 1,
    /// <summary>超自然矿产：解决魔法/修仙/奇幻的“能量”问题，灵脉驱动。</summary>
    Supernatural = 2,
    /// <summary>卡牌资源：解决知识/能力/规则的“信息”问题，文明驱动。</summary>
    Card = 3,
}

/// <summary>
/// 资源品阶，遵循全局设定规范「天、地、灵、凡」单字体系：
/// 凡（C/D级）、灵（B级）、地（A级）、天（S级）。
/// </summary>
public enum ResourceTier : byte
{
    None = 0,
    Fan = 1,  // 凡
    Ling = 2, // 灵
    Di = 3,   // 地
    Tian = 4, // 天
}

/// <summary>
/// 矿产与资源类型，严格按照《游戏矿产资源与地形分布设计.md》重构：
/// 包含 11 种基础工业矿产、10 种超自然矿产、7 种卡牌资源（共 28 种资源）。
/// 成员顺序与 PlanetGeneration.WorldGen.OreType 逐项对齐，两处按 byte 互转。
/// </summary>
public enum OreType : byte
{
    None = 0,

    // ── 基础工业矿产 (1..11) ──
    Stone = 1,          // 石材 [凡]
    Clay = 2,           // 黏土 [凡]
    Limestone = 3,      // 石灰石 [凡]
    Coal = 4,           // 煤矿 [凡]
    Iron = 5,           // 铁矿 [凡]
    Copper = 6,         // 铜矿 [凡]
    Aluminum = 7,       // 铝矿 [灵]
    Silicon = 8,        // 硅矿 [灵]
    Oil = 9,            // 石油 [地]
    NaturalGas = 10,    // 天然气 [地]
    RareMetal = 11,     // 稀有金属 [地]

    // ── 超自然矿产 (12..21) ──
    SpiritCrystal = 12,     // 灵晶 [灵]
    SunfireCrystal = 13,    // 炎曜晶 [灵]
    FrostSoulCrystal = 14,  // 寒魄晶 [灵]
    ThunderMarrow = 15,     // 雷髓矿 [灵]
    LifePith = 16,          // 生灵髓 [灵]
    NetherCrystal = 17,     // 幽冥晶 [地]
    VoidCrystal = 18,       // 空冥晶 [地]
    AstralPith = 19,        // 星髓 [地]
    LawStone = 20,          // 律纹石 [天]
    GenesisOre = 21,        // 源质矿 [天]

    // ── 卡牌资源 (22..28) ──
    MemorySand = 22,        // 忆晶砂 [灵]
    RuneOre = 23,           // 灵纹矿 [灵]
    ResonanceCrystal = 24,  // 共鸣晶 [地]
    EchoStone = 25,         // 回响石 [地]
    OrderedGold = 26,       // 定序金 [地]
    RealmCasketCrystal = 27,// 界匣晶 [天]
    KarmaStone = 28,        // 因律石 [天]
}

/// <summary>
/// 资源元数据与扩展辅助类。
/// </summary>
public static class OreTypeExtensions
{
    public static ResourceCategory GetCategory(this OreType ore) => ore switch
    {
        >= OreType.Stone and <= OreType.RareMetal => ResourceCategory.Industrial,
        >= OreType.SpiritCrystal and <= OreType.GenesisOre => ResourceCategory.Supernatural,
        >= OreType.MemorySand and <= OreType.KarmaStone => ResourceCategory.Card,
        _ => ResourceCategory.None
    };

    public static ResourceTier GetTier(this OreType ore) => ore switch
    {
        OreType.Stone or OreType.Clay or OreType.Limestone or OreType.Coal or OreType.Iron or OreType.Copper
            => ResourceTier.Fan,

        OreType.Aluminum or OreType.Silicon or OreType.SpiritCrystal or OreType.SunfireCrystal or OreType.FrostSoulCrystal
            or OreType.ThunderMarrow or OreType.LifePith or OreType.MemorySand or OreType.RuneOre
            => ResourceTier.Ling,

        OreType.Oil or OreType.NaturalGas or OreType.RareMetal or OreType.NetherCrystal or OreType.VoidCrystal
            or OreType.AstralPith or OreType.ResonanceCrystal or OreType.EchoStone or OreType.OrderedGold
            => ResourceTier.Di,

        OreType.LawStone or OreType.GenesisOre or OreType.RealmCasketCrystal or OreType.KarmaStone
            => ResourceTier.Tian,

        _ => ResourceTier.None
    };

    public static string GetTierName(this OreType ore) => ore.GetTier() switch
    {
        ResourceTier.Fan => "凡",
        ResourceTier.Ling => "灵",
        ResourceTier.Di => "地",
        ResourceTier.Tian => "天",
        _ => "无"
    };

    public static string GetDisplayName(this OreType ore) => ore switch
    {
        OreType.Stone => "石材",
        OreType.Clay => "黏土",
        OreType.Limestone => "石灰石",
        OreType.Coal => "煤矿",
        OreType.Iron => "铁矿",
        OreType.Copper => "铜矿",
        OreType.Aluminum => "铝矿",
        OreType.Silicon => "硅矿",
        OreType.Oil => "石油",
        OreType.NaturalGas => "天然气",
        OreType.RareMetal => "稀有金属",

        OreType.SpiritCrystal => "灵晶",
        OreType.SunfireCrystal => "炎曜晶",
        OreType.FrostSoulCrystal => "寒魄晶",
        OreType.ThunderMarrow => "雷髓矿",
        OreType.LifePith => "生灵髓",
        OreType.NetherCrystal => "幽冥晶",
        OreType.VoidCrystal => "空冥晶",
        OreType.AstralPith => "星髓",
        OreType.LawStone => "律纹石",
        OreType.GenesisOre => "源质矿",

        OreType.MemorySand => "忆晶砂",
        OreType.RuneOre => "灵纹矿",
        OreType.ResonanceCrystal => "共鸣晶",
        OreType.EchoStone => "回响石",
        OreType.OrderedGold => "定序金",
        OreType.RealmCasketCrystal => "界匣晶",
        OreType.KarmaStone => "因律石",

        _ => "无矿"
    };

    public static string GetCategoryName(this OreType ore) => ore.GetCategory() switch
    {
        ResourceCategory.Industrial => "工业",
        ResourceCategory.Supernatural => "超凡",
        ResourceCategory.Card => "卡牌",
        _ => "无"
    };
}

/// <summary>地貌大类分类。</summary>
public enum LandformCategory : byte
{
    Marine = 0,    // 海洋构造
    Tectonic = 1,  // 大地构造
    Fluvial = 2,   // 流水沉积与侵蚀
    Climatic = 3,  // 气候与特殊外力
}

/// <summary>
/// 地貌类型（共 25 种典型地球地貌）。
/// 涵盖海洋构造、大地构造、流水沉积/侵蚀与气候岩性特殊地貌。
/// 0~10 号成员与历史版本严格向后兼容。
/// </summary>
public enum LandformType : byte
{
    // ── 基础与海洋构造 (0..3, 10..12) ──
    Ocean = 0,             // 远海/大洋
    DeepOcean = 1,         // 深海盆地 (洋盆)
    Trench = 2,            // 海沟 (俯冲消亡带)
    Coast = 3,             // 海岸带 (滨海潮间带)
    Plain = 4,             // 内陆平原 (开阔低平原)
    Basin = 5,             // 湿润盆地 (向心汇水盆地)
    Plateau = 6,           // 高原台地 (高海拔平坦台地)
    Hill = 7,              // 丘陵缓丘 (和缓起伏地貌)
    Mountain = 8,          // 褶皱山地 (挤压造山脊岭)
    Volcano = 9,           // 火山锥 (岩浆溢流孤峰)
    Island = 10,           // 岛屿 (大洋孤立陆块)
    ShallowOcean = 11,     // 大陆架浅海 (大陆边缘平缓浅海)
    MidOceanRidge = 12,    // 洋中脊 (海底扩张脊带)

    // ── 流水与沉积侵蚀 (13..15, 24) ──
    Floodplain = 13,       // 冲积平原 (大河泛滥堆积沃野)
    Delta = 14,            // 河口三角洲 (河口受顶托泥沙沉积扇)
    Canyon = 15,           // 河流峡谷 (高地强切蚀深峻峡谷)

    // ── 气候与大地构造细分 (16..23) ──
    DryBasin = 16,         // 干旱盆地 (极端干旱内流盐沼盆地)
    Peak = 17,             // 高山极峰 (雪线险峻高寒峰巅)
    RiftValley = 18,       // 地堑断裂谷 (地壳拉张断陷狭谷)
    Karst = 19,            // 喀斯特峰林 (碳酸盐岩水热溶蚀石林)
    DesertDune = 20,       // 沙漠沙丘 (极干旱风积流动沙海)
    Badlands = 21,         // 风蚀雅丹 (干旱劣地与风蚀垄槽)
    Glacier = 22,          // 冰川冰原 (极地或高山常年大陆冰盖)
    Fjord = 23,            // 峡湾 (高纬寒带冰川侵蚀侵水海湾)
    Wetland = 24,          // 湿地沼泽 (低平滞水草沼与泥炭湿地)
}

/// <summary>
/// 地貌元数据与辅助扩展方法。
/// </summary>
public static class LandformTypeExtensions
{
    public static LandformCategory GetCategory(this LandformType landform) => landform switch
    {
        LandformType.Ocean or LandformType.DeepOcean or LandformType.Trench or LandformType.Coast
            or LandformType.Island or LandformType.ShallowOcean or LandformType.MidOceanRidge
            => LandformCategory.Marine,

        LandformType.Plain or LandformType.Basin or LandformType.Plateau or LandformType.Hill
            or LandformType.Mountain or LandformType.Volcano or LandformType.Peak or LandformType.RiftValley
            => LandformCategory.Tectonic,

        LandformType.Floodplain or LandformType.Delta or LandformType.Canyon or LandformType.Wetland
            => LandformCategory.Fluvial,

        LandformType.DryBasin or LandformType.Karst or LandformType.DesertDune or LandformType.Badlands
            or LandformType.Glacier or LandformType.Fjord
            => LandformCategory.Climatic,

        _ => LandformCategory.Tectonic
    };

    public static string GetCategoryName(this LandformType landform) => landform.GetCategory() switch
    {
        LandformCategory.Marine => "海洋构造",
        LandformCategory.Tectonic => "大地构造",
        LandformCategory.Fluvial => "流水水文",
        LandformCategory.Climatic => "气候岩性",
        _ => "地貌"
    };

    public static string GetDisplayName(this LandformType landform) => landform switch
    {
        LandformType.Ocean => "远海大洋",
        LandformType.DeepOcean => "深海盆地",
        LandformType.Trench => "深海海沟",
        LandformType.Coast => "滨海海岸",
        LandformType.Plain => "内陆平原",
        LandformType.Basin => "湿润盆地",
        LandformType.Plateau => "高原台地",
        LandformType.Hill => "丘陵缓丘",
        LandformType.Mountain => "褶皱山地",
        LandformType.Volcano => "火山锥",
        LandformType.Island => "孤立岛屿",
        LandformType.ShallowOcean => "大陆架浅海",
        LandformType.MidOceanRidge => "大洋中脊",
        LandformType.Floodplain => "冲积平原",
        LandformType.Delta => "河口三角洲",
        LandformType.Canyon => "河流峡谷",
        LandformType.DryBasin => "干旱盐盆",
        LandformType.Peak => "高山极峰",
        LandformType.RiftValley => "地堑断裂谷",
        LandformType.Karst => "喀斯特峰林",
        LandformType.DesertDune => "沙漠沙丘",
        LandformType.Badlands => "风蚀雅丹",
        LandformType.Glacier => "冰川冰原",
        LandformType.Fjord => "冰蚀峡湾",
        LandformType.Wetland => "湿地沼泽",
        _ => "未定地貌"
    };

    public static string GetDescription(this LandformType landform) => landform switch
    {
        LandformType.Ocean => "广阔深远的大洋水域，洋流循环畅通，远离大陆架。",
        LandformType.DeepOcean => "大洋深部平坦盆地，水深幽暗，水压极大，沉积缓慢。",
        LandformType.Trench => "大洋板块向大陆板块俯冲消亡形成的狭长深渊，地壳运动剧烈。",
        LandformType.Coast => "海陆直接交接的平缓沉积地带，潮汐作用明显，港口条件优越。",
        LandformType.Plain => "广袤坦荡、高差极微的开阔陆地，适宜大面积耕作与聚落营建。",
        LandformType.Basin => "四周被山地环抱、中心低平汇水的封闭盆地，水汽聚积，农业丰茂。",
        LandformType.Plateau => "大面积地壳隆升的高海拔平坦台地，边缘陡峻，日照强而昼夜温差大。",
        LandformType.Hill => "连绵起伏的和缓坡地，相对高差适中，林果与梯田错落分布。",
        LandformType.Mountain => "构造挤压抬升的崎岖造山带，山脊如龙，垂直气候分异显著。",
        LandformType.Volcano => "岩浆沿地壳薄弱带溢流喷涌堆成的锥状山体，伴生富矿与地热。",
        LandformType.Island => "四面环海的小块独立陆地，受海洋气候温养，生态独特而隔离。",
        LandformType.ShallowOcean => "贴近大陆边缘的水下平缓大陆架，光照充足，海洋生物与渔业繁茂。",
        LandformType.MidOceanRidge => "板块离散张裂扩张的海底山脊，炽热岩浆自裂谷喷溢凝固为新洋壳。",
        LandformType.Floodplain => "大河下游泛滥沉积形成的平坦沃壤，地力肥沃，灌溉水系四通八达。",
        LandformType.Delta => "江河入海口泥沙受顶托分流堆积的扇状水网低湿地，航运与水稻农业发达。",
        LandformType.Canyon => "深居高亢地块的大河剧烈下切形成的壁立千仞峡谷，险峻壮美难于逾越。",
        LandformType.DryBasin => "内陆封闭蒸发强于补给的低洼荒芜区，河流归于断流，多盐碱析出。",
        LandformType.Peak => "超越常年雪线之上的极高寒山岳巅峰，终年积雪覆冰，人迹罕至。",
        LandformType.RiftValley => "地壳拉张断裂下陷形成的深邃长条形地堑谷地，伴生阶梯断崖与湖泊。",
        LandformType.Karst => "可溶性石灰岩在温暖多雨冲刷下溶蚀出的峰林、天坑与地下暗河奇观。",
        LandformType.DesertDune => "极度干旱旷野上受盛行风搬运堆积的流动沙丘与瀚海，生机难存。",
        LandformType.Badlands => "干旱脆弱地层在强风与偶发山洪冲切下形成的千沟万壑绝壁与风蚀土垄。",
        LandformType.Glacier => "极地大陆或高山严寒处常年冰雪压实冻结的固态水体，雕琢大地轨迹。",
        LandformType.Fjord => "高纬度古冰川巨力开凿的深邃U形谷被海水涌入淹没而成的狭长险丽水湾。",
        LandformType.Wetland => "地势低洼平缓、常年排水不畅导致的积水草甸泥炭沼泽，生物多样性极其丰富。",
        _ => "自然形成的特殊地理构造形态。"
    };

    public static bool IsWater(this LandformType landform) => landform switch
    {
        LandformType.Ocean or LandformType.DeepOcean or LandformType.Trench
            or LandformType.ShallowOcean or LandformType.MidOceanRidge => true,
        _ => false
    };

    public static bool IsMountainOrHighland(this LandformType landform) => landform switch
    {
        LandformType.Mountain or LandformType.Peak or LandformType.Plateau
            or LandformType.Volcano or LandformType.Hill or LandformType.Canyon or LandformType.Karst => true,
        _ => false
    };

    public static float GetMovementCostMultiplier(this LandformType landform) => landform switch
    {
        LandformType.Plain or LandformType.Floodplain or LandformType.Delta
            or LandformType.Coast => 1.0f,
        LandformType.Basin => 1.1f,
        LandformType.Hill or LandformType.Karst => 1.8f,
        LandformType.Wetland => 2.4f,
        LandformType.Plateau or LandformType.DryBasin => 3.0f,
        LandformType.DesertDune or LandformType.Badlands => 3.6f,
        LandformType.Canyon or LandformType.RiftValley or LandformType.Fjord => 4.5f,
        LandformType.Mountain => 8.0f,
        LandformType.Peak or LandformType.Volcano or LandformType.Glacier => 999.0f,
        _ => 1.5f
    };
}

/// <summary>聚落分级。</summary>
public enum SettlementRank : byte
{
    Hamlet = 0,    // 村落
    Town = 1,      // 城镇
    CityState = 2, // 城邦/主城
}

/// <summary>地形宏观形态。</summary>
public enum TerrainMorphology
{
    Balanced = 0,
    Supercontinent = 1,
    Continents = 2,
    Archipelago = 3,
    FracturedIslands = 4,
    ShallowFragments = 5,
    ColdContinent = 6,
    HotWasteland = 7,
    PolarIcelands = 8,
    AtollChain = 9,
    InlandSea = 10,
    RiftHighlands = 11,
}

/// <summary>高程调色样式。</summary>
public enum ElevationStyleId
{
    Standard = 0,
    Hypsometric = 1,
    Topographic = 2,
    Geological = 3,
}
