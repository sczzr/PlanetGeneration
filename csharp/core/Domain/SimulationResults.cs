namespace PlanetGeneration.Core.Domain;

/// <summary>生态模拟结果。</summary>
public sealed record EcologyResult
{
    public required float AvgEcologyHealth { get; init; }
    public required float AvgCivilizationPotential { get; init; }
    public required float CivilizationEmergencePercent { get; init; }
    public required int LandCellCount { get; init; }
}

/// <summary>文明纪元事件。</summary>
public sealed record CivilizationEvent
{
    public required int Epoch { get; init; }
    public required string Category { get; init; }
    public required string Summary { get; init; }
    public required int ImpactLevel { get; init; }
}

/// <summary>文明演化模拟结果。</summary>
public sealed record CivilizationResult
{
    public required int PolityCount { get; init; }
    public required int HamletCount { get; init; }
    public required int TownCount { get; init; }
    public required int CityStateCount { get; init; }
    public required int TradeRouteCells { get; init; }
    public required float ControlledLandPercent { get; init; }
    public required float CoreCellPercent { get; init; }
    public required float DominantPolitySharePercent { get; init; }
    public required float ConnectedHubPercent { get; init; }
    public required float ConflictHeatPercent { get; init; }
    public required float AllianceCohesionPercent { get; init; }
    public required float BorderVolatilityPercent { get; init; }
    public required int LandCellCount { get; init; }
    public required CivilizationEvent[] RecentEvents { get; init; }
}
