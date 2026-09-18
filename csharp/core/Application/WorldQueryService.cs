using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Application;

/// <summary>地块悬停与选中详情数据对象。</summary>
public sealed record CellSampleDetails
{
    public required int CellId { get; init; }
    public required PolyVec2 Site { get; init; }
    public required PolyVec2 Centroid { get; init; }
    public required double Area { get; init; }

    public required float Height { get; init; }
    public required float Temperature { get; init; }
    public required float Moisture { get; init; }
    public required float River { get; init; }

    public required BiomeType Biome { get; init; }
    public required LandformType Landform { get; init; }
    public required RockType Rock { get; init; }
    public required OreType Ore { get; init; }
    public required int PlateId { get; init; }
    public required PlateBoundaryType PlateBoundary { get; init; }

    public required float EcologyHealth { get; init; }
    public required float CivilizationPotential { get; init; }
    public required float Influence { get; init; }
    public required short PolityId { get; init; }
    public required bool BorderMask { get; init; }
    public required bool TradeRouteMask { get; init; }
    public required float TradeFlow { get; init; }

    public required int CityId { get; init; }
    public SettlementInfo? Settlement { get; init; }
}

/// <summary>
/// 世界数据查询服务（拾取、悬停采样与空间查询）。
/// </summary>
public static class WorldQueryService
{
    public static CellSampleDetails GetCellSample(WorldSnapshot snapshot, int cellId)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var safeId = Math.Clamp(cellId, 0, geom.Count - 1);

        SettlementInfo? settlement = null;
        var cityId = fields.CityId[safeId];
        if (cityId >= 0 && cityId < snapshot.Settlements.Count)
        {
            settlement = snapshot.Settlements[cityId];
        }

        return new CellSampleDetails
        {
            CellId = safeId,
            Site = geom.GetSite(safeId),
            Centroid = geom.GetCentroid(safeId),
            Area = geom.Area[safeId],
            Height = fields.Height[safeId],
            Temperature = fields.Temperature[safeId],
            Moisture = fields.Moisture[safeId],
            River = fields.River[safeId],
            Biome = (BiomeType)fields.Biome[safeId],
            Landform = (LandformType)fields.Landform[safeId],
            Rock = (RockType)fields.Rock[safeId],
            Ore = (OreType)fields.Ore[safeId],
            PlateId = fields.PlateId[safeId],
            PlateBoundary = (PlateBoundaryType)fields.PlateBoundary[safeId],
            EcologyHealth = fields.EcologyHealth[safeId],
            CivilizationPotential = fields.CivilizationPotential[safeId],
            Influence = fields.Influence[safeId],
            PolityId = fields.PolityId[safeId],
            BorderMask = fields.BorderMask[safeId],
            TradeRouteMask = fields.TradeRouteMask[safeId],
            TradeFlow = fields.TradeFlow[safeId],
            CityId = cityId,
            Settlement = settlement,
        };
    }

    public static CellSampleDetails QueryAtPoint(WorldSnapshot snapshot, double worldX, double worldY)
    {
        var cellId = snapshot.Geometry.FindCell(worldX, worldY);
        return GetCellSample(snapshot, cellId);
    }

    public static List<int> QueryRadius(WorldSnapshot snapshot, double worldX, double worldY, double radius)
    {
        return snapshot.Geometry.FindAll(worldX, worldY, radius);
    }
}
