using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Cartography.Planning;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Generator;

/// <summary>
/// 智能多阶海岸水波纹与海蚀石拱生成器（SmartCoastlineGenerator）。
/// 
/// 严格遵循 Map Effects 奇幻制图方法论 (https://www.mapeffects.co/video-tutorials/smart-coastline-brush-video-tutorial & sea-arch)：
/// 1. 【多重等距同心环岸波纹 (Multi-tier Concentric Water Lining)】：
///    - Tier 1（近岸 4~6px）：紧贴陆地轮廓的手绘细碎浪花折线（高不透明度，密密水波）；
///    - Tier 2（浅海 12~16px）：断续节奏的浅海回音线（手绘质感破折线）；
///    - Tier 3（外海 24~30px）：悠长淡雅的外海长涌波纹（低不透明度，渐隐至远海）；
/// 2. 【海角海蚀石拱奇观 (Promontory Sea Arches)】：
///    - 在凸向深海的高曲率岬角与孤立岩礁处，受千百年海浪冲刷形成标志性的天然石拱门（Sea Arch）。
/// </summary>
public static class SmartCoastlineGenerator
{
    public sealed class CoastlineResult
    {
        public List<BrushInstruction> ConcentricWaves { get; }
        public List<BrushInstruction> SeaArches { get; }
        public List<BrushInstruction> AllInstructions { get; }

        public CoastlineResult(List<BrushInstruction> waves, List<BrushInstruction> arches)
        {
            ConcentricWaves = waves;
            SeaArches = arches;
            AllInstructions = new List<BrushInstruction>(waves.Count + arches.Count);
            AllInstructions.AddRange(waves);
            AllInstructions.AddRange(arches);
        }
    }

    /// <summary>
    /// 从地块几何与高度场提取具有代表性的海岸线轨迹点。
    /// </summary>
    public static List<PolyVec2> ExtractCoastlinePath(CellGeometry geometry, CellFields fields, float seaLevel, int maxPoints = 64)
    {
        var coast = new List<PolyVec2>();
        if (geometry == null || fields == null) return coast;

        var visited = new HashSet<int>();
        var coastalCells = new List<int>();

        for (var i = 0; i < geometry.Count; i++)
        {
            if (fields.Height[i] > seaLevel) continue;

            var nStart = geometry.CellNeighborStart[i];
            var nEnd = geometry.CellNeighborStart[i + 1];
            var isCoast = false;

            for (var ni = nStart; ni < nEnd; ni++)
            {
                var nIdx = geometry.CellNeighbors[ni];
                if (nIdx >= 0 && nIdx < geometry.Count && fields.Height[nIdx] > seaLevel)
                {
                    isCoast = true;
                    break;
                }
            }

            if (isCoast) coastalCells.Add(i);
        }

        if (coastalCells.Count == 0) return coast;

        // 从第一个海岸地块出发，沿邻接海岸贪心追踪一条连续海岸线
        var curr = coastalCells[0];
        coast.Add(new PolyVec2(geometry.SiteX[curr], geometry.SiteY[curr]));
        visited.Add(curr);

        while (coast.Count < maxPoints)
        {
            var nStart = geometry.CellNeighborStart[curr];
            var nEnd = geometry.CellNeighborStart[curr + 1];
            var next = -1;
            var bestDist = double.MaxValue;

            for (var ni = nStart; ni < nEnd; ni++)
            {
                var cand = geometry.CellNeighbors[ni];
                if (cand >= 0 && cand < geometry.Count && !visited.Contains(cand) && coastalCells.Contains(cand))
                {
                    var d = Math.Pow(geometry.SiteX[cand] - geometry.SiteX[curr], 2) +
                            Math.Pow(geometry.SiteY[cand] - geometry.SiteY[curr], 2);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        next = cand;
                    }
                }
            }

            if (next == -1) break;

            curr = next;
            visited.Add(curr);
            coast.Add(new PolyVec2(geometry.SiteX[curr], geometry.SiteY[curr]));
        }

        return coast;
    }

    /// <summary>
    /// 对给定的海岸多段线生成多阶智能水波纹与海蚀石拱。
    /// </summary>
    public static CoastlineResult GenerateCoastline(
        IReadOnlyList<PolyVec2> coastPoints,
        int seed,
        CartographyColor oceanColor,
        int regionId = -1,
        bool generateArches = true)
    {
        var waves = new List<BrushInstruction>();
        var arches = new List<BrushInstruction>();

        if (coastPoints == null || coastPoints.Count < 3)
        {
            return new CoastlineResult(waves, arches);
        }

        var rand = new Random(seed ^ 0x434f53); // "COS"
        var tierDistances = new[] { 5.0f, 13.0f, 25.0f };
        var tierOpacities = new[] { 0.75f, 0.48f, 0.28f };
        var tierStrokeWidths = new[] { 1.5f, 1.2f, 0.9f };

        // 1. 生成 3 阶同心水波纹
        for (var tier = 0; tier < tierDistances.Length; tier++)
        {
            var dist = tierDistances[tier];
            var opacity = tierOpacities[tier];
            var strokeWidth = tierStrokeWidths[tier];

            var offsetPoints = OffsetPolyline(coastPoints, dist, rand, tier);
            if (offsetPoints.Count < 2) continue;

            // 切分为若干断续手绘线段
            var segments = BreakIntoHandDrawnSegments(offsetPoints, rand, tier);
            foreach (var seg in segments)
            {
                if (seg.Length < 2) continue;

                waves.Add(new BrushInstruction
                {
                    Type = BrushType.CoastlineWave,
                    Position = seg[0],
                    Points = seg,
                    StrokeWidth = strokeWidth,
                    Opacity = opacity,
                    Tint = oceanColor.Lerp(CartographyColor.MistIvory, 0.25f),
                    RegionId = regionId,
                    VariantKey = $"coastline_tier_{tier + 1}",
                    Tag = $"CoastWave_T{tier + 1}"
                });
            }
        }

        // 2. 检测向海凸出的尖锐岬角并生成海蚀石拱 (Promontory Sea Arch)
        if (generateArches && coastPoints.Count >= 5)
        {
            var bestIdx = -1;
            var minDot = 0.65;
            var bestOutward = PolyVec2.Zero;

            for (var i = 1; i < coastPoints.Count - 1; i++)
            {
                var pPrev = coastPoints[i - 1];
                var pCurr = coastPoints[i];
                var pNext = coastPoints[i + 1];

                var v1 = (pCurr - pPrev).Normalized();
                var v2 = (pNext - pCurr).Normalized();
                var dot = v1.X * v2.X + v1.Y * v2.Y;

                if (dot < minDot)
                {
                    minDot = dot;
                    bestIdx = i;
                    bestOutward = new PolyVec2(-v1.Y - v2.Y, v1.X + v2.X).Normalized();
                }
            }

            if (bestIdx != -1)
            {
                var pCurr = coastPoints[bestIdx];
                var archPos = pCurr + bestOutward * (14.0 + rand.NextDouble() * 6.0);

                arches.Add(new BrushInstruction
                {
                    Type = BrushType.SeaArch,
                    Position = archPos,
                    Scale = 1.0f + (float)rand.NextDouble() * 0.35f,
                    Rotation = MathF.Atan2((float)bestOutward.Y, (float)bestOutward.X) + 1.57f,
                    Opacity = 0.95f,
                    YOrder = (float)archPos.Y,
                    Tint = CartographyColor.SandyOchre.Lerp(new CartographyColor(40, 35, 30, 255), 0.5f),
                    RegionId = regionId,
                    VariantKey = "sea_arch_promontory",
                    Tag = "SeaArch"
                });
            }
        }

        return new CoastlineResult(waves, arches);
    }

    private static List<PolyVec2> OffsetPolyline(IReadOnlyList<PolyVec2> poly, float offsetDist, Random rand, int tier)
    {
        var res = new List<PolyVec2>(poly.Count);
        for (var i = 0; i < poly.Count; i++)
        {
            var pCurr = poly[i];
            var dir = (i == poly.Count - 1)
                ? (pCurr - poly[i - 1]).Normalized()
                : (poly[i + 1] - pCurr).Normalized();

            var normal = new PolyVec2(-dir.Y, dir.X);
            // 细微的手绘手抖波浪噪波
            var jitter = ((float)rand.NextDouble() - 0.5f) * (offsetDist * 0.25f);
            res.Add(pCurr + normal * (offsetDist + jitter));
        }
        return res;
    }

    private static List<PolyVec2[]> BreakIntoHandDrawnSegments(List<PolyVec2> pts, Random rand, int tier)
    {
        var segs = new List<PolyVec2[]>();
        var current = new List<PolyVec2>();

        // Tier 越高，线条断裂留白概率越大
        var breakChance = 0.08f + tier * 0.12f;

        for (var i = 0; i < pts.Count; i++)
        {
            current.Add(pts[i]);

            if (current.Count >= 3 && rand.NextDouble() < breakChance && i < pts.Count - 2)
            {
                segs.Add(current.ToArray());
                current = new List<PolyVec2>();
                // 跳过 1 个点形成留白间隙
                i++;
            }
        }

        if (current.Count >= 2)
        {
            segs.Add(current.ToArray());
        }

        return segs;
    }
}
