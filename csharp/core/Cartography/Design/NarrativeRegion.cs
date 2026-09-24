using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 叙事大区类型（Narrative Region Type）。
/// 地图策划的第一公民，统领地貌、植被、水文与聚落布局。
/// </summary>
public enum NarrativeRegionType
{
    MountainRange,  // 崇山峻岭 / 连绵群岳
    ForestMass,     // 万顷林海 / 太古深林
    DesertField,    // 瀚海流沙 / 狂沙大漠
    PlainsBasin,    // 天府平原 / 沃野盆地
    CoastalBay,     // 滨海津湾 / 沧海咽喉
    PolarTundra,    // 极北冰原 / 高寒苔原
    SouthernWetlands// 南部湿地 / 云梦水泽
}

/// <summary>
/// 森林内部隙地 / 空地（Clearing）。
/// 穿插于密林腹地，形成“万顷苍翠中的自然留白”，打破死板贴图感。
/// </summary>
public sealed class ForestClearing
{
    public PolyVec2 Position { get; init; }
    public float Radius { get; init; } = 35.0f;
    public string Name { get; init; } = "幽谷隙地";
}

/// <summary>
/// 体块化森林大区数据（ForestMassData）。
/// </summary>
public sealed class ForestMassData
{
    /// <summary>森林有机实体轮廓多边形。</summary>
    public List<PolyVec2> HullPolygon { get; init; } = new();

    /// <summary>密林核心区面积比例（0.65 ~ 0.80）。</summary>
    public float CoreRatio { get; init; } = 0.72f;

    /// <summary>边缘羽化带宽度（像素）。</summary>
    public float EdgeMarginWidth { get; init; } = 48.0f;

    /// <summary>林间空地 / 留白隙地列表。</summary>
    public List<ForestClearing> Clearings { get; init; } = new();

    /// <summary>核心林冠密度。</summary>
    public float CanopyDensity { get; init; } = 1.0f;
}

/// <summary>
/// 连绵山系大区数据（MountainChainData）。
/// </summary>
public sealed class MountainChainData
{
    /// <summary>主脉脊线中轴样条。</summary>
    public List<PolyVec2> SpineCurve { get; init; } = new();

    /// <summary>主脊宽度包络（像素）。</summary>
    public float RidgeWidth { get; init; } = 45.0f;

    /// <summary>侧向支脉（Spurs）列表：自脊线向平原舒展的分支山脊。</summary>
    public List<List<PolyVec2>> BranchSpurs { get; init; } = new();

    /// <summary>主要雄峰（主峰/雪峰）在主脊上的归一化位置比例 [0, 1]。</summary>
    public List<float> MajorPeakRatios { get; init; } = new();

    /// <summary>山门隘口（关卡）在主脊上的归一化位置比例 [0, 1]。</summary>
    public List<float> PassRatios { get; init; } = new();

    /// <summary>山脉两侧纵深谷地（Valleys）锚点，供道路与溪流穿行。</summary>
    public List<PolyVec2> ValleyCorridors { get; init; } = new();
}

/// <summary>
/// 风向大漠大区数据（DesertFieldData）。
/// </summary>
public sealed class DesertFieldData
{
    /// <summary>主风向角（弧度）。沙垄垂直或平行于风向延展。</summary>
    public float WindAngle { get; init; } = 0.35f;

    /// <summary>流动新月沙垄走廊轴线。</summary>
    public List<List<PolyVec2>> DuneRidgeCorridors { get; init; } = new();

    /// <summary>大漠绿洲与清泉坐标。</summary>
    public List<PolyVec2> Oases { get; init; } = new();
}

/// <summary>
/// 平原盆地大区数据（PlainsBasinData）。
/// </summary>
public sealed class PlainsBasinData
{
    /// <summary>中央平原开阔腹心。</summary>
    public PolyVec2 HeartlandCenter { get; init; }

    /// <summary>平原清平小湖 / 镜湖坐标。</summary>
    public List<PolyVec2> MirrorLakes { get; init; } = new();

    /// <summary>旷野风草细纹微地貌聚集区。</summary>
    public List<PolyVec2> MeadowRippleZones { get; init; } = new();

    /// <summary>散落独头孤丘坐标。</summary>
    public List<PolyVec2> SolitaryKnolls { get; init; } = new();
}

/// <summary>
/// 叙事地理大区实体（NarrativeRegion）。
/// 作为构图导演层与各画师之间最核心的数据媒介。
/// </summary>
public sealed class NarrativeRegion
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public NarrativeRegionType Type { get; init; }
    public PolyVec2 Center { get; init; }
    public float ExtentRadius { get; init; } = 0.25f;
    public CartographyColor Palette { get; init; } = CartographyColor.EmeraldGreen;
    public string Description { get; init; } = string.Empty;

    /// <summary>区域平滑包络轮廓多边形。</summary>
    public List<PolyVec2> BoundaryPolygon { get; set; } = new();

    /// <summary>林海专属数据（当 Type == ForestMass 时非空）。</summary>
    public ForestMassData? ForestMass { get; set; }

    /// <summary>山系专属数据（当 Type == MountainRange 时非空）。</summary>
    public MountainChainData? MountainChain { get; set; }

    /// <summary>大漠专属数据（当 Type == DesertField 时非空）。</summary>
    public DesertFieldData? DesertField { get; set; }

    /// <summary>平原专属数据（当 Type == PlainsBasin 时非空）。</summary>
    public PlainsBasinData? PlainsBasin { get; set; }
}
