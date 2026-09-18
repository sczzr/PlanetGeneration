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

/// <summary>矿产类型。</summary>
public enum OreType : byte
{
    None = 0,
    Coal = 1,
    Copper = 2,
    Tin = 3,
    Iron = 4,
    Gold = 5,
    Diamond = 6,
    Platinum = 7,
    Aluminum = 8,
    Silver = 9,
    Lead = 10,
}

/// <summary>地貌类型。</summary>
public enum LandformType : byte
{
    Ocean = 0,
    DeepOcean = 1,
    Trench = 2,
    Coast = 3,
    Plain = 4,
    Basin = 5,
    Plateau = 6,
    Hill = 7,
    Mountain = 8,
    Volcano = 9,
    Island = 10,
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
}

/// <summary>高程调色样式。</summary>
public enum ElevationStyleId
{
    Standard = 0,
    Hypsometric = 1,
    Topographic = 2,
    Geological = 3,
}
