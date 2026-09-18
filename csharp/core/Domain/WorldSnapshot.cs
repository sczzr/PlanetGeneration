using System;
using System.Collections.Generic;

namespace PlanetGeneration.Core.Domain;

/// <summary>
/// 一次完整生成或模拟发布的权威只读快照。
///
/// 地图渲染、悬停交互、小地图、统计、时间轴回放以及 AI 叙事
/// 均消费同一个 WorldSnapshot，消除生产与消费路径上的数据双源分歧。
/// </summary>
public sealed class WorldSnapshot
{
    private static long _nextSnapshotId = 1;

    public long SnapshotId { get; }
    public DateTime CreatedAt { get; }
    public GenerationOptions Options { get; }
    public CellGeometry Geometry { get; }
    public CellFields Fields { get; }
    public IReadOnlyList<SettlementInfo> Settlements { get; }
    public PlateSystemSummary PlateSummary { get; }
    public WorldStatsSummary Stats { get; }
    public EcologyResult? Ecology { get; init; }
    public CivilizationResult? Civilization { get; init; }
    public (float X, float Y)[,]? ContinuousWind { get; init; }

    public int CellCount => Geometry.Count;
    public WorldExtent Extent => Geometry.Extent;

    public WorldSnapshot(
        GenerationOptions options,
        CellGeometry geometry,
        CellFields fields,
        IReadOnlyList<SettlementInfo> settlements,
        PlateSystemSummary plateSummary,
        WorldStatsSummary stats,
        EcologyResult? ecology = null,
        CivilizationResult? civilization = null,
        long? snapshotId = null,
        DateTime? createdAt = null)
    {
        SnapshotId = snapshotId ?? System.Threading.Interlocked.Increment(ref _nextSnapshotId);
        CreatedAt = createdAt ?? DateTime.UtcNow;
        Options = options;
        Geometry = geometry;
        Fields = fields;
        Settlements = settlements;
        PlateSummary = plateSummary;
        Stats = stats;
        Ecology = ecology;
        Civilization = civilization;
    }

    /// <summary>
    /// 基于当前快照生成新的衍生快照（例如模拟纪元推进或微调模拟参数）。
    /// </summary>
    public WorldSnapshot WithSimulation(
        GenerationOptions newOptions,
        CellFields newFields,
        EcologyResult? newEcology,
        CivilizationResult? newCivilization)
    {
        return new WorldSnapshot(
            newOptions,
            Geometry,
            newFields,
            Settlements,
            PlateSummary,
            Stats,
            newEcology,
            newCivilization)
        {
            ContinuousWind = ContinuousWind
        };
    }
}
