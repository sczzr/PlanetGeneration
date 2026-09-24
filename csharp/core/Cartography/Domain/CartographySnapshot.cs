using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Cartography.Planning;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 幻想制图层只读快照（CartographySnapshot）。
/// 
/// 承载地图表现层的全部艺术规则与笔刷数据，彻底解耦物理模拟核心与最终渲染器：
/// 1. Regions: 宏观地理区域的视觉风格与色彩调色板；
/// 2. Brushes: 全量手绘笔刷指令（远山/主峰/雪顶/林海/树群/江河折线/沙垄/流岚/建筑符号）；
/// 3. Landmarks: 地图地标与书法题名元数据；
/// 4. Commands: 按图层与景深优先级规整后的绘制批次；
/// 5. RoadGraph: 规划级官道驿道网络。
/// </summary>
public sealed class CartographySnapshot
{
    public long SnapshotId { get; }

    public DateTime CreatedAt { get; }

    /// <summary>区域视觉规则表（Key 为 RegionId）。</summary>
    public Dictionary<int, RegionStyle> Regions { get; init; } = new();

    /// <summary>笔刷绘制指令序列。</summary>
    public List<BrushInstruction> Brushes { get; init; } = new();

    /// <summary>地图地标与名胜列表。</summary>
    public List<LandmarkStyle> Landmarks { get; init; } = new();

    /// <summary>按图层组织的绘制批次指令。</summary>
    public List<PainterCommand> Commands { get; init; } = new();

    /// <summary>规划级官道驿路图。</summary>
    public PlannedRoadGraph? RoadGraph { get; init; }

    public CartographySnapshot(
        Dictionary<int, RegionStyle> regions,
        List<BrushInstruction> brushes,
        List<LandmarkStyle> landmarks,
        List<PainterCommand>? commands = null,
        long? snapshotId = null,
        DateTime? createdAt = null,
        PlannedRoadGraph? roadGraph = null)
    {
        SnapshotId = snapshotId ?? DateTime.UtcNow.Ticks;
        CreatedAt = createdAt ?? DateTime.UtcNow;
        Regions = regions;
        Brushes = brushes;
        Landmarks = landmarks;
        Commands = commands ?? PainterCommandBuilder.BuildCommands(brushes, landmarks);
        RoadGraph = roadGraph;
    }

    public override string ToString() => $"CartographySnapshot[{SnapshotId}]: Regions={Regions.Count}, Brushes={Brushes.Count}, Landmarks={Landmarks.Count}, Commands={Commands.Count}";
}
