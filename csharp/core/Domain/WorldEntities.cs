using System.Collections.Generic;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Domain;

/// <summary>聚落实体信息。</summary>
public sealed record SettlementInfo
{
    public required int CellId { get; init; }
    public required string Name { get; init; }
    public required float Score { get; init; }
    public required SettlementRank Rank { get; init; }
    public required PolyVec2 Position { get; init; }
}

/// <summary>板块站点信息（纯领域类型，无 Godot 依赖）。</summary>
public sealed record PlateSiteInfo
{
    public required int Id { get; init; }
    public required PolyVec2 Position { get; init; }
    public required PolyVec2 Motion { get; init; }
    public required bool IsOceanic { get; init; }
    public required float BaseElevation { get; init; }
    public required float ColorR { get; init; }
    public required float ColorG { get; init; }
    public required float ColorB { get; init; }
}

/// <summary>板块系统聚合摘要。</summary>
public sealed record PlateSystemSummary
{
    public required IReadOnlyList<PlateSiteInfo> Sites { get; init; }
}

/// <summary>基于多边形面积统计的世界概览。</summary>
public sealed record WorldStatsSummary
{
    public required int CellCount { get; init; }
    public required int LandCellCount { get; init; }
    public required int CityCount { get; init; }
    public required float OceanPercent { get; init; }
    public required float RiverPercent { get; init; }
    public required float AvgTemperature { get; init; }
    public required float AvgMoisture { get; init; }
    public required float ForestPercent { get; init; }
    public required float PeakElevationMeters { get; init; }
    public required float DeepSeaDepthMeters { get; init; }
}
