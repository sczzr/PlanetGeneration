using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Planning;

/// <summary>规划阶段共享的坐标与样条运算；不持有任何世界状态。</summary>
internal static class PlanningGeometry
{
    internal static PolyVec2 ToWorld(PolyVec2 uv, float w, float h)
    {
        return new PolyVec2(uv.X * w, uv.Y * h);
    }

    internal static List<PolyVec2> BuildDenseSpline(PolyVec2 start, List<PolyVec2> controls, PolyVec2 end, int sampleCount)
    {
        var pts = new List<PolyVec2> { start };
        pts.AddRange(controls);
        pts.Add(end);

        var result = new List<PolyVec2>(sampleCount);
        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (float)(sampleCount - 1);
            result.Add(EvaluateMultiSpline(pts, t));
        }
        return result;
    }

    internal static List<PolyVec2> BuildMeanderingRiverSpline(List<PolyVec2> waypoints, int sampleCount)
    {
        var result = new List<PolyVec2>(sampleCount);
        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (float)(sampleCount - 1);
            var basePt = EvaluateMultiSpline(waypoints, t);

            // 注入细微九曲自然摆动 (Catmull-Rom + 微小正弦横向扰动)
            var sineOffset = MathF.Sin(t * MathF.PI * 9.0f) * 4.5f * (1.0f - MathF.Abs(t - 0.5f) * 0.8f);
            var perpX = -MathF.Sin(t * 6.28f) * sineOffset;
            var perpY = MathF.Cos(t * 6.28f) * sineOffset;

            result.Add(new PolyVec2(basePt.X + perpX, basePt.Y + perpY));
        }
        return result;
    }

    private static PolyVec2 EvaluateMultiSpline(List<PolyVec2> pts, float t)
    {
        if (pts.Count == 0) return PolyVec2.Zero;
        if (pts.Count == 1) return pts[0];
        if (pts.Count == 2) return pts[0] * (1f - t) + pts[1] * t;

        var n = pts.Count - 1;
        var p = t * n;
        var idx = Math.Clamp((int)p, 0, n - 1);
        var localT = p - idx;

        var p0 = pts[Math.Max(0, idx - 1)];
        var p1 = pts[idx];
        var p2 = pts[Math.Min(n, idx + 1)];
        var p3 = pts[Math.Min(n, idx + 2)];

        var t2 = localT * localT;
        var t3 = t2 * localT;

        var x = 0.5f * ((2f * p1.X) +
                        (-p0.X + p2.X) * localT +
                        (2f * p0.X - 5f * p1.X + 4f * p2.X - p3.X) * t2 +
                        (-p0.X + 3f * p1.X - 3f * p2.X + p3.X) * t3);

        var y = 0.5f * ((2f * p1.Y) +
                        (-p0.Y + p2.Y) * localT +
                        (2f * p0.Y - 5f * p1.Y + 4f * p2.Y - p3.Y) * t2 +
                        (-p0.Y + 3f * p1.Y - 3f * p2.Y + p3.Y) * t3);

        return new PolyVec2(x, y);
    }
}
