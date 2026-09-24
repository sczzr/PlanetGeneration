using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 母亲河走廊构图蓝图（RiverCorridorBlueprint）。
/// 
/// 规划一条有灵气、串联群山与平原的宏观母亲河：
/// 发源于雪峰山谷，蜿蜒穿越沃土平原，滋养神都与大邑，最终汇入沧海港湾。
/// </summary>
public sealed class RiverCorridorBlueprint
{
    public int Id { get; init; } = 1;
    public string Name { get; init; } = "天水龙江";

    /// <summary>发源地（山脉深谷坐标）。</summary>
    public PolyVec2 SourcePoint { get; init; }

    /// <summary>中继拐弯与河套走廊坐标。</summary>
    public List<PolyVec2> Waypoints { get; init; } = new();

    /// <summary>入海口坐标。</summary>
    public PolyVec2 MouthPoint { get; init; }

    /// <summary>江河宽度系数（1.0 ~ 2.5）。</summary>
    public float WidthScale { get; init; } = 1.4f;
}
