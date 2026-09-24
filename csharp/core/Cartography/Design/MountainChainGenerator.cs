using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 连绵山系生成器 (MountainChainGenerator)。
/// 
/// 负责将大区山脉规划编译为具有连贯地理骨架（Mountain Chain）的有机山系结构：
/// 1. 主脊样条（Spine Curve）平滑插值；
/// 2. 侧向支脉（Lateral Spurs）向平原侧以自然夹角舒展伸出；
/// 3. 支脉之间自然围合的山间谷地（Valley Corridors）；
/// 4. 雄峰（Dominant Peaks）与山门隘口（Passes）锚定。
/// </summary>
public static class MountainChainGenerator
{
    public static MountainChainData Generate(
        PolyVec2 startPoint,
        IReadOnlyList<PolyVec2> controlPoints,
        PolyVec2 endPoint,
        float ridgeWidth,
        IReadOnlyList<float>? majorPeakRatios = null,
        IReadOnlyList<float>? passRatios = null,
        int spurCount = 5,
        float spurLength = 55.0f,
        int seed = 42)
    {
        var rand = new Random(seed ^ 0x4d544e); // "MTN"

        // 1. 构建主脊平滑样条曲线
        var spineCurve = BuildSpline(startPoint, controlPoints, endPoint, sampleCount: 45);

        // 2. 确定主峰与隘口比例
        var peaks = majorPeakRatios != null ? new List<float>(majorPeakRatios) : new List<float> { 0.45f, 0.70f };
        var passes = passRatios != null ? new List<float>(passRatios) : new List<float> { 0.30f };

        // 3. 生发生长侧向支脉（Spurs）与山谷廊道（Valley Corridors）
        var branchSpurs = new List<List<PolyVec2>>(spurCount);
        var valleys = new List<PolyVec2>();

        if (spineCurve.Count >= 10 && spurCount > 0)
        {
            var spineLen = spineCurve.Count;
            // 支脉分布在主脊 18% ~ 82% 之间
            var startIdx = (int)(spineLen * 0.18f);
            var endIdx = (int)(spineLen * 0.82f);
            var span = endIdx - startIdx;
            var step = span / (float)Math.Max(1, spurCount);

            PolyVec2? prevSpurOrigin = null;

            for (var k = 0; k < spurCount; k++)
            {
                var idx = Math.Clamp((int)(startIdx + k * step + (rand.NextSingle() - 0.5f) * 2f), 1, spineLen - 2);
                var origin = spineCurve[idx];

                // 计算主脊切线方向
                var pPrev = spineCurve[idx - 1];
                var pNext = spineCurve[idx + 1];
                var tangent = (pNext - pPrev).Normalized();
                var normal = new PolyVec2(-tangent.Y, tangent.X); // 默认垂直向南（正Y）

                // 如果正常朝向偏向北方，则翻转使之指向南侧平原
                if (normal.Y < 0)
                {
                    normal = -normal;
                }

                // 支脉以 50° ~ 75° 斜伸向平原
                var skewAngle = (rand.NextSingle() - 0.5f) * 0.35f;
                var cosSkew = MathF.Cos(skewAngle);
                var sinSkew = MathF.Sin(skewAngle);
                var spurDir = new PolyVec2(
                    normal.X * cosSkew - normal.Y * sinSkew,
                    normal.X * sinSkew + normal.Y * cosSkew).Normalized();

                // 生成支脉折线（3~5 个节点）
                var spurNodes = new List<PolyVec2>();
                spurNodes.Add(origin);

                var curPos = origin;
                var actualSpurLen = spurLength * (0.80f + rand.NextSingle() * 0.40f);
                var subSteps = 4;
                var subStepDist = actualSpurLen / subSteps;

                for (var s = 1; s <= subSteps; s++)
                {
                    // 支脉略带自然弯曲游移
                    var wander = (rand.NextSingle() - 0.5f) * 0.18f;
                    var cosW = MathF.Cos(wander);
                    var sinW = MathF.Sin(wander);
                    spurDir = new PolyVec2(
                        spurDir.X * cosW - spurDir.Y * sinW,
                        spurDir.X * sinW + spurDir.Y * cosW).Normalized();

                    curPos += spurDir * subStepDist;
                    spurNodes.Add(curPos);
                }

                branchSpurs.Add(spurNodes);

                // 在相邻支脉之间，记录山谷廊道入口（内凹的低缓平地）
                if (prevSpurOrigin.HasValue)
                {
                    var midSpine = (prevSpurOrigin.Value + origin) * 0.5;
                    var valleyPos = midSpine + normal * (actualSpurLen * 0.45);
                    valleys.Add(valleyPos);
                }
                prevSpurOrigin = origin;
            }
        }

        return new MountainChainData
        {
            SpineCurve = spineCurve,
            RidgeWidth = ridgeWidth,
            BranchSpurs = branchSpurs,
            MajorPeakRatios = peaks,
            PassRatios = passes,
            ValleyCorridors = valleys
        };
    }

    /// <summary>
    /// 使用 Catmull-Rom 样条构建连续平滑脊线点序列。
    /// </summary>
    public static List<PolyVec2> BuildSpline(
        PolyVec2 start,
        IReadOnlyList<PolyVec2> controls,
        PolyVec2 end,
        int sampleCount = 45)
    {
        var keyPoints = new List<PolyVec2>(controls.Count + 2) { start };
        keyPoints.AddRange(controls);
        keyPoints.Add(end);

        if (keyPoints.Count < 2) return keyPoints;
        if (keyPoints.Count == 2)
        {
            var res = new List<PolyVec2>(sampleCount);
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)(sampleCount - 1);
                res.Add(start * (1f - t) + end * t);
            }
            return res;
        }

        var result = new List<PolyVec2>(sampleCount);
        var totalSegments = keyPoints.Count - 1;

        for (var i = 0; i < sampleCount; i++)
        {
            var globalT = (i / (float)(sampleCount - 1)) * totalSegments;
            var segIdx = Math.Clamp((int)Math.Floor(globalT), 0, totalSegments - 1);
            var segT = (float)(globalT - segIdx);

            var p0 = segIdx > 0 ? keyPoints[segIdx - 1] : keyPoints[segIdx] * 2f - keyPoints[segIdx + 1];
            var p1 = keyPoints[segIdx];
            var p2 = keyPoints[segIdx + 1];
            var p3 = segIdx + 2 < keyPoints.Count ? keyPoints[segIdx + 2] : keyPoints[segIdx + 1] * 2f - keyPoints[segIdx];

            result.Add(CatmullRom(p0, p1, p2, p3, segT));
        }

        return result;
    }

    private static PolyVec2 CatmullRom(PolyVec2 p0, PolyVec2 p1, PolyVec2 p2, PolyVec2 p3, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        var f0 = -0.5 * t3 + t2 - 0.5 * t;
        var f1 = 1.5 * t3 - 2.5 * t2 + 1.0;
        var f2 = -1.5 * t3 + 2.0 * t2 + 0.5 * t;
        var f3 = 0.5 * t3 - 0.5 * t2;
        return p0 * f0 + p1 * f1 + p2 * f2 + p3 * f3;
    }
}
