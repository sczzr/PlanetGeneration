using Godot;
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

public enum OreType
{
    None,
    Coal,
    Copper,
    Tin,
    Iron,
    Gold,
    Diamond,
    Platinum,
    Aluminum,
    Silver,
    Lead
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
