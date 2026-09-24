using Godot;
using PlanetGeneration.Core.Domain;
using System.Collections.Generic;

namespace PlanetGeneration.WorldGen;

public enum PlateBoundaryType
{
    None,
    Convergent,
    Divergent,
    Transform
}

public enum BiomeType
{
    Ocean,
    ShallowOcean,
    Coastland,
    Ice,
    Tundra,
    BorealForest,
    Taiga,
    Steppe,
    Grassland,
    Chaparral,
    TemperateDesert,
    TemperateSeasonalForest,
    TemperateRainForest,
    Savanna,
    Shrubland,
    TropicalDesert,
    TropicalSeasonalForest,
    TropicalRainForest,
    RockyMountain,
    SnowyMountain,
    River
}

public enum RockType
{
    Sedimentary,
    Igneous,
    Metamorphic
}

/// <summary>
/// 成员顺序与 PlanetGeneration.Core.Domain.OreType 逐项对齐，
/// 两者经 BaseFieldGeneratorAdapter 以 byte 互转，任何一侧插队都会串色。
/// </summary>
public enum OreType : byte
{
    None = 0,

    // 基础工业矿产 (1..11)
    Stone = 1,          // 石材
    Clay = 2,           // 黏土
    Limestone = 3,      // 石灰石
    Coal = 4,           // 煤矿
    Iron = 5,           // 铁矿
    Copper = 6,         // 铜矿
    Aluminum = 7,       // 铝矿
    Silicon = 8,        // 硅矿
    Oil = 9,            // 石油
    NaturalGas = 10,    // 天然气
    RareMetal = 11,     // 稀有金属

    // 超自然矿产 (12..21)
    SpiritCrystal = 12,     // 灵晶
    SunfireCrystal = 13,    // 炎曜晶
    FrostSoulCrystal = 14,  // 寒魄晶
    ThunderMarrow = 15,     // 雷髓矿
    LifePith = 16,          // 生灵髓
    NetherCrystal = 17,     // 幽冥晶
    VoidCrystal = 18,       // 空冥晶
    AstralPith = 19,        // 星髓
    LawStone = 20,          // 律纹石
    GenesisOre = 21,        // 源质矿

    // 卡牌资源 (22..28)
    MemorySand = 22,        // 忆晶砂
    RuneOre = 23,           // 灵纹矿
    ResonanceCrystal = 24,  // 共鸣晶
    EchoStone = 25,         // 回响石
    OrderedGold = 26,       // 定序金
    RealmCasketCrystal = 27,// 界匣晶
    KarmaStone = 28         // 因律石
}

public static class WorldGenOreTypeExtensions
{
    public static PlanetGeneration.Core.Domain.ResourceCategory GetCategory(this OreType ore)
        => ((PlanetGeneration.Core.Domain.OreType)ore).GetCategory();

    public static PlanetGeneration.Core.Domain.ResourceTier GetTier(this OreType ore)
        => ((PlanetGeneration.Core.Domain.OreType)ore).GetTier();

    public static string GetTierName(this OreType ore)
        => ((PlanetGeneration.Core.Domain.OreType)ore).GetTierName();

    public static string GetDisplayName(this OreType ore)
        => ((PlanetGeneration.Core.Domain.OreType)ore).GetDisplayName();

    public static string GetCategoryName(this OreType ore)
        => ((PlanetGeneration.Core.Domain.OreType)ore).GetCategoryName();
}


public enum CityPopulation
{
    Small,
    Medium,
    Large
}

public sealed class CityInfo
{
    public required Vector2I Position { get; init; }
    public required float Score { get; init; }
    public required string Name { get; init; }
    public required CityPopulation Population { get; init; }
}

public sealed class WorldTuning
{
    public required string Name { get; init; }
    public required float DeepOceanFactor { get; init; }
    public required float CoastBand { get; init; }
    public required float MountainThreshold { get; init; }
    public required float RiverSourceElevationThreshold { get; init; }
    public required float RiverSourceMoistureThreshold { get; init; }
    public required float RiverSourceChance { get; init; }

    public static WorldTuning Legacy() => new()
    {
        Name = "Legacy",
        DeepOceanFactor = 0.5714f,
        CoastBand = 0.027f,
        MountainThreshold = 0.68f,
        RiverSourceElevationThreshold = 0.55f,
        RiverSourceMoistureThreshold = 0.15f,
        RiverSourceChance = 0.0035f
    };

    public static WorldTuning Balanced() => new()
    {
        Name = "Balanced",
        DeepOceanFactor = 0.60f,
        CoastBand = 0.022f,
        MountainThreshold = 0.70f,
        RiverSourceElevationThreshold = 0.57f,
        RiverSourceMoistureThreshold = 0.17f,
        RiverSourceChance = 0.0028f
    };
}

public sealed class WorldStats
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int CityCount { get; init; }
    public required float OceanPercent { get; init; }
    public required float RiverPercent { get; init; }
    public required float AvgTemperature { get; init; }
    public required float AvgMoisture { get; init; }
}

public sealed class PlateSite
{
    public required int Id { get; init; }
    public required Vector2I Position { get; init; }
    public required Vector2 Motion { get; init; }
    public required bool IsOceanic { get; init; }
    public required float BaseElevation { get; init; }
    public required Color DebugColor { get; init; }
}

public struct PlateStressCell
{
    public bool IsBorder { get; init; }
    public float DirectForce { get; init; }
    public float ShearForce { get; init; }
    public PlateBoundaryType Type { get; init; }
    public int Id0 { get; init; }
    public int Id1 { get; init; }
    public Vector2I Neighbor { get; init; }
}

public struct PlateNeighborInfo
{
    public int Id { get; init; }
    public int NeighborId { get; init; }
    public float DirectForce { get; init; }
    public float ShearForce { get; init; }
    public PlateBoundaryType Type { get; init; }
}

public struct PlateEdgePoint
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Id { get; init; }
    public int NeighborId { get; init; }
    public PlateBoundaryType Type { get; init; }
    public bool IsOceanic { get; init; }
}

public sealed class PlateResult
{
    public required int[,] PlateIds { get; init; }
    public required float[,] PlateBaseElevation { get; init; }
    public required PlateBoundaryType[,] BoundaryTypes { get; init; }
    public required PlateStressCell[,] StressMap { get; init; }
    public required List<PlateNeighborInfo> Neighbors { get; init; }
    public required List<PlateEdgePoint> BorderPoints { get; init; }
    public required List<PlateSite> Sites { get; init; }
}
