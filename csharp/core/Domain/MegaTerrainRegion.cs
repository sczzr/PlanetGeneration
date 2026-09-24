using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Domain;

/// <summary>大型宏观地貌分类。</summary>
public enum MegaTerrainType : byte
{
    MegaMountain = 0,   // 超大型山脉 (线状/脊线驱动)
    MegaPlateau = 1,    // 超大型高原 (面状/高程断崖)
    MegaBasin = 2,      // 超大型盆地 (面状/环山低洼)
    MegaDesert = 3,     // 超大型荒漠 (面状/极端干旱)
    MegaForest = 4,     // 超大型森林 (面状/水热充沛)
    MegaHills = 5,      // 区域巨型丘陵 (面状/中度起伏)
    MegaGrassland = 6,  // 超大型草原 (面状/辽阔平坦)
    MegaWetland = 7,    // 超大型沼泽湿地 (面状/高水文滞留)
}

/// <summary>地貌地级评级。</summary>
public enum MegaRegionRank : byte
{
    LocalMinor = 0,     // 局部小地貌 (不予宏观独立标识)
    MajorRegion = 1,    // 大型地貌 (1% ~ 3%, 纹理与细节轻度强化)
    MegaRegion = 2,     // 巨型地貌 (3% ~ 8%, 拥有独立命名与宏观贴图)
    WorldLandmark = 3,  // 世界级自然地标 (> 8%, 拥有独占地貌图腾与题名)
}

/// <summary>山脉骨架主轴节点。</summary>
public sealed record MountainSpineNode(
    PolyVec2 Position,
    float Elevation,
    float RidgeWidth,
    int CellId);

/// <summary>
/// 大型自然地理实体对象 (MegaTerrainRegion)
///
/// 标识少数具有巨大规模和地理意义的世界级自然区域，
/// 将“地表底层存在 (Layer 0)”与“宏观地图标识 (Layer 1)”彻底解耦。
/// </summary>
public sealed class MegaTerrainRegion
{
    public required int Id { get; init; }
    public required MegaTerrainType Type { get; init; }
    public required MegaRegionRank Rank { get; init; }
    public required string Name { get; init; }

    // ── 拓扑与几何归属 ──
    public required int[] Cells { get; init; }
    public required double TotalArea { get; init; }
    public required float AreaShare { get; init; }
    public required PolyVec2 Centroid { get; init; }
    public required PolyVec2 BoundsMin { get; init; }
    public required PolyVec2 BoundsMax { get; init; }

    // ── 形态特征量 ──
    public float Compactness { get; init; }
    public float Continuity { get; init; }
    public float MainDirectionAngle { get; init; }
    public IReadOnlyList<MountainSpineNode>? Spine { get; init; }

    // ── 环境一致性 ──
    public float ElevationMean { get; init; }
    public float ElevationVariance { get; init; }
    public float MoistureMean { get; init; }

    // ── 裁决评分 ──
    public float MegaScore { get; init; }

    // ── 边界去网格平滑轮廓 ──
    public IReadOnlyList<PolyVec2[]> SmoothedPolygons { get; init; } = Array.Empty<PolyVec2[]>();

    // ── 层级嵌套与从属关系 ──
    public int ParentRegionId { get; init; } = -1;
    public List<int> ChildRegionIds { get; init; } = new();

    /// <summary>
    /// 计算指定点在区域内部的归一化相对深度 [0, 1]：
    /// 0.0 表示在边界边缘；1.0 表示在最深核心区。
    /// 用于驱动 Core (>0.6) / Transition (0.3~0.6) / Edge (0.1~0.3) / Outside (<0.1) 4层渐变贴图。
    /// </summary>
    public float ComputeNormalizedDepth(PolyVec2 point)
    {
        if (SmoothedPolygons.Count == 0)
        {
            var distToCenter = point.DistanceTo(Centroid);
            var maxRadius = Math.Max(BoundsMax.X - BoundsMin.X, BoundsMax.Y - BoundsMin.Y) * 0.5;
            if (maxRadius <= 1e-5 || distToCenter > maxRadius) return 0f;
            return (float)Math.Clamp(1.0 - (distToCenter / maxRadius), 0.0, 1.0);
        }

        // 快速 AABB 预筛：超出大区包围盒（含 1px 浮点容差）的点必然在区外
        if (point.X < BoundsMin.X - 1.0 || point.X > BoundsMax.X + 1.0 ||
            point.Y < BoundsMin.Y - 1.0 || point.Y > BoundsMax.Y + 1.0)
        {
            return 0f;
        }

        // 判定点是否落在任一多边形内部，并计算到边界的最短距离
        var isInside = false;
        var minEdgeDist = double.MaxValue;
        for (var p = 0; p < SmoothedPolygons.Count; p++)
        {
            var poly = SmoothedPolygons[p];
            if (poly.Length < 3) continue;

            if (IsPointInsidePolygon(point, poly))
            {
                isInside = true;
            }

            for (var i = 0; i < poly.Length; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Length];
                var d = DistanceToSegment(point, a, b);
                if (d < minEdgeDist) minEdgeDist = d;
            }
        }

        // 既不在多边形内部，且与外边界距离超出容差 (0.5px)，坚决返回 0.0（非森林区 0 树木）
        if (!isInside && minEdgeDist > 0.5)
        {
            return 0f;
        }

        var approxRadius = Math.Sqrt(TotalArea / Math.PI) * 0.75;
        if (approxRadius <= 1e-4) return 1f;

        return (float)Math.Clamp(minEdgeDist / approxRadius, 0.0, 1.0);
    }

    private static bool IsPointInsidePolygon(PolyVec2 pt, PolyVec2[] hull)
    {
        if (hull == null || hull.Length < 3) return false;
        var inside = false;
        for (int i = 0, j = hull.Length - 1; i < hull.Length; j = i++)
        {
            if (((hull[i].Y > pt.Y) != (hull[j].Y > pt.Y)) &&
                (pt.X < (hull[j].X - hull[i].X) * (pt.Y - hull[i].Y) / (hull[j].Y - hull[i].Y) + hull[i].X))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private static double DistanceToSegment(PolyVec2 p, PolyVec2 a, PolyVec2 b)
    {
        var ab = b - a;
        var lenSq = ab.LengthSquared;
        if (lenSq <= 1e-9) return p.DistanceTo(a);

        var t = Math.Clamp((p - a).Dot(ab) / lenSq, 0.0, 1.0);
        var proj = a + ab * t;
        return p.DistanceTo(proj);
    }
}
