using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 地标分类。
/// </summary>
public enum LandmarkType
{
    Capital,        // 帝国国都 / 主府
    Town,           // 繁华名城 / 郡城
    Village,        // 乡野茅舍 / 村落
    Temple,         // 宝刹古寺 / 道观
    Pagoda,         // 临江宝塔 / 佛塔
    SacredPeak,     // 天下名山 / 洞天福地主峰
    NaturalWonder,  // 自然奇观（峡谷、仙湖、天坑）
    AncientRuins,   // 上古遗迹 / 仙人洞府
    Port            // 沿海港口 / 渡口津渡
}

/// <summary>
/// 古地图聚落五级规范层级。
/// </summary>
public enum CartographySettlementTier
{
    Capital, // 1级：天下国都 / 宏伟首府 (◎ + 旗)
    City,    // 2级：万户巨邑 / 州府大都 (□ + 重郭)
    Town,    // 3级：县治津关 / 驿市重镇 (□)
    Harbor,  // 4级：滨海水运 / 泊舟津渡 (⚓)
    Village  // 5级：山野散落 / 农庄小村 (●)
}

/// <summary>
/// 地标与题注视觉样式数据（LandmarkStyle）。
/// </summary>
public sealed class LandmarkStyle
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public PolyVec2 Position { get; set; }

    public LandmarkType Type { get; set; } = LandmarkType.Village;

    public BrushType IconType { get; set; } = BrushType.CityIcon;

    /// <summary>重要度评级（1: 普通, 2: 重要, 3: 核心, 4: 世界级）。</summary>
    public int Importance { get; set; } = 1;

    /// <summary>书法题名字号。</summary>
    public int FontSize { get; set; } = 12;

    /// <summary>是否带有宣纸墨色防干扰光晕。</summary>
    public bool HasHalo { get; set; } = true;

    /// <summary>字体颜色。</summary>
    public CartographyColor TextColor { get; set; } = CartographyColor.InkCharcoal;

    /// <summary>图标整体缩放系数。</summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>与地标关联的特定区域 ID。</summary>
    public int RegionId { get; set; } = -1;

    /// <summary>是否在地图顶层渲染文字题名（小村落默认只存符号点，不显文字）。</summary>
    public bool ShowLabel { get; set; } = true;

    /// <summary>聚落五级符号规范层级。</summary>
    public CartographySettlementTier SettlementTier { get; set; } = CartographySettlementTier.Village;

    public override string ToString() => $"Landmark[{Id}]: {Name} ({Type}, Tier={SettlementTier}, Pos={Position}, Imp={Importance})";
}
