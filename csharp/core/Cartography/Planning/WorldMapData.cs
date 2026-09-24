using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Planning;

/// <summary>
/// 规划级山系支脉数据结构。
/// 用于构建具有进深与网状结构的立体山系（MountainSystem）。
/// </summary>
public sealed class PlannedMountainBranch
{
    public string Name { get; init; } = string.Empty;
    public List<PolyVec2> SpineCurve { get; init; } = new();
    public float RidgeWidth { get; init; } = 36.0f;
    public CartographyColor Palette { get; init; } = CartographyColor.EmeraldGreen;
}

/// <summary>
/// 规划级山系数据结构。
/// </summary>
public sealed class PlannedMountainChain
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public List<PolyVec2> SpineCurve { get; init; } = new();
    public float RidgeWidth { get; init; } = 48.0f;
    public List<float> MajorPeakRatios { get; init; } = new();
    public List<float> PassRatios { get; init; } = new();
    public bool HasSnowCap { get; init; } = true;
    public CartographyColor Palette { get; init; } = CartographyColor.EmeraldGreen;
    public int SpurCount { get; init; } = 4;
    public float SpurLength { get; init; } = 50.0f;

    /// <summary>向南或向两侧舒展的立体纵深侧脉/支脉列表。</summary>
    public List<PlannedMountainBranch> Branches { get; init; } = new();
}

/// <summary>
/// 规划级河流支流数据结构。
/// </summary>
public sealed class PlannedRiverBranch
{
    public string Name { get; init; } = string.Empty;
    public PolyVec2 SourcePoint { get; init; }
    public List<PolyVec2> Waypoints { get; init; } = new();
    public PolyVec2 ConfluencePoint { get; init; }
    public float WidthScale { get; init; } = 0.6f;
}

/// <summary>
/// 规划级树状水系数据结构。
/// 严格遵循“高山积雪发源 -> 沿坡蜿蜒下泄 -> 平原九曲回环 -> 汇聚注海”的水文物理与视觉逻辑。
/// </summary>
public sealed class PlannedRiverSystem
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public PolyVec2 SourcePoint { get; init; }
    public List<PolyVec2> MainWaypoints { get; init; } = new();
    public PolyVec2 MouthPoint { get; init; }
    public float SourceWidth { get; init; } = 2.4f;
    public float MouthWidth { get; init; } = 9.5f;
    public List<PlannedRiverBranch> Branches { get; init; } = new();
    public List<PolyVec2> Lakes { get; init; } = new();
    public CartographyColor Palette { get; init; } = CartographyColor.ColdBlue;
}

/// <summary>
/// 规划级宏观生态大区复合子生态类型。
/// </summary>
public enum PlannedSubBiomeType
{
    DenseCanopy,        // 密林核心冠层
    ForestHills,        // 林海丘陵叠翠
    ForestPond,         // 幽林清潭水泊
    SacredSanctuary,    // 仙家福地古观
    WateredFarmland,    // 沿道/沿江水浇田网带
    BreezePlains,       // 微风拂草宣纸留白
    SolitaryKnolls,     // 旷野独头孤丘
    LakeWetland,        // 镜湖泽国蒹葭
    MarketHamlet,       // 官道十字集市村落
    DesertDuneLane,     // 顺风向新月沙垄走廊
    RockyCliff,         // 裂谷断崖残岩
    DesertOasis,        // 瀚海清泉绿洲
    DesertMargin,       // 荒漠边缘半干旱过渡带
    CoastalBarren,      // 海岸荒地
    WetlandMarshes,     // 南部水泽芦荡
    TundraPlain,        // 北境苔原冰原
    MountainGorge       // 高山峡谷源流
}

/// <summary>
/// 规划级子生态包（SubBiome）。
/// </summary>
public sealed class PlannedSubBiome
{
    public PlannedSubBiomeType Type { get; init; }
    public PolyVec2 Center { get; init; }
    public float Radius { get; init; }
    public float Weight { get; init; } = 1.0f;
}

/// <summary>
/// 宏观生态大区类型。
/// </summary>
public enum PlannedRegionType
{
    HighlandMountain,   // 北寒雪山高地
    AncientForest,      // 太古水墨林海
    CentralPlains,      // 中原天府盆地
    AridDesert,         // 西南金沙大漠
    SouthernPlateau,    // 南屏断崖台地
    CoastalBay,         // 东南海湾良港
    PolarTundra,        // 北境冰原苔原
    WesternForest,      // 西境云杉林海
    SouthernWetlands    // 南部云梦湿地
}

/// <summary>
/// 规划级宏观生态大区。
/// </summary>
public sealed class PlannedRegion
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public PlannedRegionType RegionType { get; init; }
    public PolyVec2 Center { get; init; }
    public float RadiusX { get; init; }
    public float RadiusY { get; init; }
    public float Rotation { get; init; }
    public CartographyColor Palette { get; init; } = CartographyColor.EmeraldGreen;
    public List<PlanetGeneration.Core.Cartography.Design.ForestClearing> Clearings { get; init; } = new();
    public float WindAngle { get; init; } = 0.38f;
    public List<PolyVec2> Oases { get; init; } = new();

    /// <summary>内部丰富度复合子生态列表。</summary>
    public List<PlannedSubBiome> SubBiomes { get; init; } = new();
}

/// <summary>
/// 规划级战略聚落因果律锚点类型。
/// </summary>
public enum PlannedSettlementAnchor
{
    MountainPass,       // 锁山雄关（一夫当关）
    RiverConfluence,    // 干支交汇大都会（商贾云集）
    RiverLoopCapital,   // 平原江湾国都（天府之枢）
    SeaHarbor,          // 半月巨湾天然良港（万舶朝宗）
    DesertOasis,        // 丝路绿洲驿站（驼铃要冲）
    SacredSanctuary,    // 幽林隐仙古观（清虚福地）
    CanalTown,          // 运河泽国水乡（水网重郭）
    NorthwestBorderCity,// 西北陇右名郡（丝路重镇）
    MountainFort,       // 山道险关要塞（剑阁锁钥）
    RiverFerryPort,     // 大江咽喉渡口（浔阳古埠）
    RiverGarrison,      // 江防水寨军要（襄樊重镇）
    SacredTown,         // 名山脚下道镇（武当仙镇）
    MountainMiningCity, // 北山脚下矿业重城（北麓铁府）
    ForestMarginCity,   // 森林边缘林业名郡（翠微古郡）
    AncientRelic,       // 古林深处上古遗迹（太古神殿）
    WetlandWaterTown,   // 南部湿地水乡名城（云梦泽城）
    FarmVillage,        // 阡陌农耕村落（杏花古村/桑麻村）
    ValleyHaven,        // 幽谷隐逸桃源（桃源坞）
    FerryVillage,       // 渡口小村集市（枫林晚渡）
    FishingHaven,       // 滨海渔港海村（渔歌泊）
    PostStation,        // 官道丝路驿站（柳林驿/驼铃驿）
    PlainAgriculturalCity,// 平原西侧粮仓农业名都（丰泽古郡）
    OasisTradeCity      // 瀚海清泉丝路贸易节点（沙洲新城）
}

/// <summary>
/// 规划级战略聚落节点。
/// </summary>
public sealed class PlannedSettlement
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public PolyVec2 Position { get; init; }
    public PlannedSettlementAnchor AnchorType { get; init; }
    public int Tier { get; init; } // 4=Capital, 3=City/Port, 2=Town/Pass/Garrison, 1=Village/Sanctuary/Ferry
    public float CityScore { get; init; }
    public string Description { get; init; } = string.Empty;
    public bool ShowLabel { get; init; } = true;
    public float Scale { get; init; } = 1.0f;
}

/// <summary>
/// 规划级道路路段。
/// </summary>
public sealed class PlannedRoadSegment
{
    public string FromSettlement { get; init; } = string.Empty;
    public string ToSettlement { get; init; } = string.Empty;
    public List<PolyVec2> Waypoints { get; init; } = new();
    public int RoadTier { get; init; } = 1; // 1=帝京官道, 2=郡县驿道, 3=乡野山径
}

/// <summary>
/// 规划级道路网络图。
/// </summary>
public sealed class PlannedRoadGraph
{
    public List<PlannedRoadSegment> Segments { get; init; } = new();
}

/// <summary>
/// 规划级平原人文与自然微地貌修饰。
/// </summary>
public sealed class PlannedDecorations
{
    /// <summary>沿官道与大江两岸密集铺设的农田网带坐标列表。</summary>
    public List<PolyVec2> FarmlandCorridors { get; init; } = new();

    /// <summary>农田水浇地集中聚集区中心点列表。</summary>
    public List<PolyVec2> FarmlandClusters { get; init; } = new();

    /// <summary>旷野点睛孤丘坐标列表。</summary>
    public List<PolyVec2> SolitaryKnolls { get; init; } = new();

    /// <summary>清平镜湖坐标列表。</summary>
    public List<PolyVec2> MirrorLakes { get; init; } = new();

    /// <summary>蒹葭芦荡湿地坐标列表。</summary>
    public List<PolyVec2> ReedWetlands { get; init; } = new();

    /// <summary>微风草浪纹理中心列表。</summary>
    public List<PolyVec2> GrassWaves { get; init; } = new();
}

/// <summary>
/// 宏观世界规划产物图（WorldMapPlan）。
/// 作为“世界规划式生成”（World Layout Graph Architecture）的顶层骨架协议。
/// </summary>
public sealed class WorldMapPlan
{
    /// <summary>大陆有机陆基多边形外包凸包/点阵。</summary>
    public List<PolyVec2> ContinentHull { get; init; } = new();

    /// <summary>五大核心叙事大区（Narrative Regions）。</summary>
    public List<RegionData> NarrativeRegions { get; init; } = new();

    /// <summary>立体山系网络骨架拓扑（Mountain Topology）。</summary>
    public MountainTopology MountainTopology { get; init; } = new();

    /// <summary>连贯不可跨越的宏观山系龙骨（含主脉与斜出支脉）。</summary>
    public List<PlannedMountainChain> MountainChains { get; init; } = new();

    /// <summary>高山发源直抵大海的树状水文水系。</summary>
    public List<PlannedRiverSystem> RiverSystems { get; init; } = new();

    /// <summary>宏观生态功能大区（林海、平原、沙漠、台地）。</summary>
    public List<PlannedRegion> Regions { get; init; } = new();

    /// <summary>严谨服从地理因果律的 20+ 个层级战略聚落网络。</summary>
    public List<PlannedSettlement> Settlements { get; init; } = new();

    /// <summary>宏大核心视觉地标（Grand Landmarks: 视觉锚点与叙事核心）。</summary>
    public List<PlannedLandmark> GrandLandmarks { get; init; } = new();

    /// <summary>连通国都、州府、要塞、村落的立体官道驿道网络。</summary>
    public PlannedRoadGraph RoadGraph { get; init; } = new();

    /// <summary>平原农耕文明与自然微地貌修饰。</summary>
    public PlannedDecorations Decorations { get; init; } = new();
}

/// <summary>
/// 叙事区域核心数据结构（RegionData）。
/// </summary>
public sealed class RegionData
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public NarrativeRegionType Type { get; init; }
    public PolyVec2 Center { get; init; }
    public float RadiusX { get; init; }
    public float RadiusY { get; init; }
    public float Rotation { get; init; }
    public CartographyColor PrimaryColor { get; init; } = CartographyColor.EmeraldGreen;
    public List<string> FeatureDescriptions { get; init; } = new();
    public List<string> ContainedSettlementNames { get; init; } = new();
    public List<PolyVec2> Bounds { get; init; } = new();
}

/// <summary>
/// 山系立体拓扑结构（Mountain Topology）。
/// </summary>
public sealed class MountainTopology
{
    public string Name { get; init; } = string.Empty;
    public List<PolyVec2> MainSpine { get; set; } = new();
    public List<List<PolyVec2>> Branches { get; init; } = new();
    public List<PolyVec2> Peaks { get; init; } = new();
    public List<PolyVec2> Valleys { get; init; } = new();
    public List<PolyVec2> Passes { get; init; } = new();
    public float SnowLineAltitude { get; init; } = 0.75f;
}

/// <summary>
/// 宏大视觉地标类型（Grand Landmark Type）。
/// </summary>
public enum PlannedLandmarkType
{
    SnowPeakSummit,    // 极北雪峰绝顶 (高耸宏大雪山)
    ImperialPalace,    // 天府神都大殿 (万国来朝大郭)
    CangjinPort,       // 沧津海港半月城船队 (万舶巨港)
    IronFortressPass,  // 铁血雄关锁钥 (雄伟要塞城楼)
    SacredTempleTower, // 青岚灵刹宝塔 (七级浮屠道观)
    DesertRelicPyramid // 瀚海古城遗迹 (狂沙神殿废墟)
}

/// <summary>
/// 宏大视觉地标数据结构（PlannedLandmark）。
/// 拥有高尺寸加权（Scale 1.25~1.6）与视觉置顶层级，作为全图核心视觉焦点。
/// </summary>
public sealed class PlannedLandmark
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public PlannedLandmarkType Type { get; init; }
    public PolyVec2 Position { get; init; }
    public float VisualScale { get; init; } = 1.4f;
    public string Description { get; init; } = string.Empty;
    public CartographyColor Palette { get; init; } = CartographyColor.CinnabarRed;
}
