using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Cartography.Planning;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Generator;

/// <summary>
/// 平原河流蛇曲动力学与牛轭湖生成器（MeanderRiverGenerator）。
/// 
/// 严格遵循 Map Effects 奇幻地图方法论 (https://www.mapeffects.co/tutorials/rivers-curve)：
/// 1. 【平原蛇曲形成机制】：
///    - 河流在低坡度平原遭遇微小阻挡时，会向外弯曲冲刷（Erosion on outer bend），
///      而在内弯泥沙沉积（Deposition on inner bend），强化 S 型大弯；
///    - 自然界蛇曲波长通常为河道宽度的 6 倍左右（Langbein-Leopold 正弦曲度模型）；
/// 2. 【截弯取直与牛轭湖 (Oxbow Lake)】：
///    - 随着蛇曲弧度不断剧烈演化，相邻两弯的“颈部（Neck）”距离逼近临界阈值；
///    - 洪水期水流自然选择阻力最小的直线贯通（Chute Cutoff），废弃原弯曲水道；
///    - 被截断的弧段形成独立的新月形湖泊——牛轭湖，湖滨滋生芦苇与水草。
/// </summary>
public static class MeanderRiverGenerator
{
    public sealed class MeanderResult
    {
        public List<PolyVec2> EvolvedWaypoints { get; }
        public List<BrushInstruction> OxbowLakes { get; }

        public MeanderResult(List<PolyVec2> evolvedWaypoints, List<BrushInstruction> oxbowLakes)
        {
            EvolvedWaypoints = evolvedWaypoints;
            OxbowLakes = oxbowLakes;
        }
    }

    /// <summary>
    /// 对一条河流平原段应用蛇曲演化与牛轭湖剥离。
    /// </summary>
    public static MeanderResult ProcessMeander(
        IReadOnlyList<PolyVec2> originalPoints,
        float riverWidth,
        int seed,
        CartographyColor waterColor,
        int regionId = -1,
        float meanderIntensity = 1.0f)
    {
        var oxbowInstructions = new List<BrushInstruction>();
        if (originalPoints == null || originalPoints.Count < 4)
        {
            return new MeanderResult(new List<PolyVec2>(originalPoints ?? Array.Empty<PolyVec2>()), oxbowInstructions);
        }

        var rand = new Random(seed ^ 0x4f5842); // "OXB"
        var points = ResamplePath(originalPoints, Math.Max(8.0f, riverWidth * 1.5f));
        if (points.Count < 6)
        {
            return new MeanderResult(points, oxbowInstructions);
        }

        // 1. Langbein-Leopold 正弦扰动注入 (S-curve meanders)
        var totalLength = ComputePathLength(points);
        var waveLength = Math.Max(40.0f, riverWidth * 6.0f); // 波长约为河宽 6 倍
        var evolved = new List<PolyVec2>(points.Count);
        evolved.Add(points[0]);

        var currentDist = 0.0f;
        for (var i = 1; i < points.Count - 1; i++)
        {
            var pPrev = points[i - 1];
            var pCurr = points[i];
            var pNext = points[i + 1];

            var segLen = (float)(pCurr - pPrev).Length;
            currentDist += segLen;

            // 仅在中下游平原段强化蛇曲（前 20% 源头山区流速快不蛇曲，80% 之后靠近海口）
            var t = currentDist / Math.Max(1.0f, totalLength);
            var meanderWeight = (t > 0.22f && t < 0.85f)
                ? MathF.Sin((t - 0.22f) / (0.85f - 0.22f) * MathF.PI)
                : 0.0f;

            var dir = (pNext - pPrev).Normalized();
            var normal = new PolyVec2(-dir.Y, dir.X); // 法线向量

            var phase = (float)(currentDist / waveLength * (Math.PI * 2.0));
            // 复合两阶正弦波与微小伪随机扰动
            var amplitude = (riverWidth * 2.2f * meanderIntensity) * meanderWeight;
            var offsetMagnitude = MathF.Sin(phase) * amplitude + MathF.Sin(phase * 2.1f + 0.4f) * (amplitude * 0.35f);

            var displaced = pCurr + normal * offsetMagnitude;
            evolved.Add(displaced);
        }
        evolved.Add(points[points.Count - 1]);

        // 2. 检测紧缩颈部并生成牛轭湖 (Oxbow Lake Cutoff)
        // 当环段 i 与后续段 j 物理距离过近，但沿河距离较大时，触发截弯取直
        var finalPath = new List<PolyVec2>();
        var iPoint = 0;
        var cutoffTriggered = false;

        while (iPoint < evolved.Count)
        {
            finalPath.Add(evolved[iPoint]);

            // 检查是否有机会与前方发生截断（仅触发 1~2 次最典型牛轭湖，避免河道崩解）
            if (!cutoffTriggered && iPoint >= 3 && iPoint < evolved.Count - 8)
            {
                for (var j = iPoint + 5; j < Math.Min(iPoint + 14, evolved.Count - 2); j++)
                {
                    var neckDist = (float)(evolved[iPoint] - evolved[j]).Length;
                    // 颈部距离小于 1.8 倍河宽，判定为成熟蛇曲环
                    if (neckDist < riverWidth * 1.8f)
                    {
                        // 提取被剥离的孤立弧段
                        var loopPoints = new List<PolyVec2>();
                        for (var k = iPoint; k <= j; k++)
                        {
                            loopPoints.Add(evolved[k]);
                        }

                        if (loopPoints.Count >= 4)
                        {
                            var lakeCenter = ComputeCentroid(loopPoints);

                            // 生成新月形牛轭湖图元
                            oxbowInstructions.Add(new BrushInstruction
                            {
                                Type = BrushType.OxbowLake,
                                Position = lakeCenter,
                                Points = loopPoints.ToArray(),
                                Scale = 1.0f + (float)rand.NextDouble() * 0.2f,
                                StrokeWidth = Math.Max(2.5f, riverWidth * 0.85f),
                                Opacity = 0.88f,
                                Tint = waterColor.Lerp(CartographyColor.MistIvory, 0.15f),
                                RegionId = regionId,
                                VariantKey = "oxbow_crescent_lake",
                                Tag = "OxbowLake"
                            });

                            // 并在牛轭湖弯曲外侧点缀蒹葭芦苇水草
                            var reedOffset = (loopPoints[loopPoints.Count / 2] - lakeCenter).Normalized() * (riverWidth * 1.2f);
                            oxbowInstructions.Add(new BrushInstruction
                            {
                                Type = BrushType.WetlandReeds,
                                Position = lakeCenter + reedOffset,
                                Scale = 0.85f + (float)rand.NextDouble() * 0.3f,
                                Opacity = 0.85f,
                                Tint = CartographyColor.EmeraldGreen.Lerp(CartographyColor.MistIvory, 0.2f),
                                RegionId = regionId,
                                VariantKey = "reeds_cluster"
                            });

                            // 河流直接截弯取直跳到 j
                            iPoint = j;
                            cutoffTriggered = true;
                            break;
                        }
                    }
                }
            }

            iPoint++;
        }

        // 3. 如果已有紧缩环截弯取直，则已生成牛轭湖；
        // 若河道本身较平缓未发生自相切紧缩，但流经宽广平原（点数充足且河宽足够），
        // 则在河流平原蛇曲最大弯道外侧冲积平原生成历史截断遗留的经典 C 型牛轭湖（Map Effects 标志性特征）
        if (oxbowInstructions.Count == 0 && evolved.Count >= 6 && riverWidth >= 3.5f)
        {
            var bestBendIdx = -1;
            var maxCurve = 0.0;
            var midStart = Math.Max(1, (int)(evolved.Count * 0.30));
            var midEnd = Math.Min(evolved.Count - 2, (int)(evolved.Count * 0.75));

            for (var i = midStart; i <= midEnd; i++)
            {
                var pPrev = evolved[i - 1];
                var pCurr = evolved[i];
                var pNext = evolved[i + 1];

                var v1 = (pCurr - pPrev).Normalized();
                var v2 = (pNext - pCurr).Normalized();
                var cross = Math.Abs(v1.X * v2.Y - v1.Y * v2.X);
                if (cross > maxCurve)
                {
                    maxCurve = cross;
                    bestBendIdx = i;
                }
            }

            if (bestBendIdx == -1) bestBendIdx = evolved.Count / 2;

            var apex = evolved[bestBendIdx];
            var dir = (evolved[Math.Min(evolved.Count - 1, bestBendIdx + 1)] - evolved[Math.Max(0, bestBendIdx - 1)]).Normalized();
            var normal = new PolyVec2(-dir.Y, dir.X);

            var oxbowCenter = apex + normal * (riverWidth * 2.2f);
            var radius = riverWidth * 1.5f;

            var cShapePoints = new List<PolyVec2>();
            var baseAngle = MathF.Atan2((float)normal.Y, (float)normal.X);
            for (var a = -1.9f; a <= 1.9f; a += 0.45f)
            {
                var angle = baseAngle + a;
                cShapePoints.Add(oxbowCenter + new PolyVec2(MathF.Cos(angle) * radius, MathF.Sin(angle) * (radius * 0.75f)));
            }

            oxbowInstructions.Add(new BrushInstruction
            {
                Type = BrushType.OxbowLake,
                Position = oxbowCenter,
                Points = cShapePoints.ToArray(),
                Scale = 1.0f + (float)rand.NextDouble() * 0.15f,
                StrokeWidth = Math.Max(2.4f, riverWidth * 0.8f),
                Opacity = 0.86f,
                Tint = waterColor.Lerp(CartographyColor.MistIvory, 0.15f),
                RegionId = regionId,
                VariantKey = "oxbow_crescent_lake",
                Tag = "OxbowLake"
            });

            oxbowInstructions.Add(new BrushInstruction
            {
                Type = BrushType.WetlandReeds,
                Position = oxbowCenter + normal * (radius * 0.4f),
                Scale = 0.85f + (float)rand.NextDouble() * 0.25f,
                Opacity = 0.85f,
                Tint = CartographyColor.EmeraldGreen.Lerp(CartographyColor.MistIvory, 0.2f),
                RegionId = regionId,
                VariantKey = "reeds_cluster"
            });
        }

        return new MeanderResult(finalPath, oxbowInstructions);
    }

    private static List<PolyVec2> ResamplePath(IReadOnlyList<PolyVec2> path, float stepSize)
    {
        var result = new List<PolyVec2>();
        if (path == null || path.Count == 0) return result;

        result.Add(path[0]);
        var accum = 0.0f;

        for (var i = 0; i < path.Count - 1; i++)
        {
            var p0 = path[i];
            var p1 = path[i + 1];
            var dist = (float)(p1 - p0).Length;
            if (dist < 1e-4f) continue;

            var dir = (p1 - p0) / dist;
            var pos = 0.0f;

            while (pos + stepSize - accum <= dist)
            {
                pos += (stepSize - accum);
                result.Add(p0 + dir * pos);
                accum = 0.0f;
            }

            accum += (dist - pos);
        }

        if (result.Count < path.Count)
        {
            result.Add(path[path.Count - 1]);
        }

        return result;
    }

    private static float ComputePathLength(IReadOnlyList<PolyVec2> path)
    {
        var len = 0.0f;
        for (var i = 0; i < path.Count - 1; i++)
        {
            len += (float)(path[i + 1] - path[i]).Length;
        }
        return len;
    }

    private static PolyVec2 ComputeCentroid(IReadOnlyList<PolyVec2> pts)
    {
        var sumX = 0.0;
        var sumY = 0.0;
        for (var i = 0; i < pts.Count; i++)
        {
            sumX += pts[i].X;
            sumY += pts[i].Y;
        }
        return new PolyVec2(sumX / pts.Count, sumY / pts.Count);
    }
}
