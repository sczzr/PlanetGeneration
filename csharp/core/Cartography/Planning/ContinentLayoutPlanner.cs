using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Planning;

/// <summary>在可变艺术工作场上规划大陆与海岸轮廓。</summary>
internal static class ContinentLayoutPlanner
{
    internal static List<PolyVec2> CarveContinentLandmass(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        MapBlueprint blueprint,
        float w,
        float h)
    {
        var seaLevel = options.SeaLevel;
        var hullPoints = new List<PolyVec2>
        {
            new(0.14f * w, 0.34f * h),
            new(0.24f * w, 0.14f * h),
            new(0.50f * w, 0.10f * h),
            new(0.76f * w, 0.14f * h),
            new(0.88f * w, 0.34f * h),
            new(0.88f * w, 0.74f * h),
            new(0.70f * w, 0.86f * h),
            new(0.42f * w, 0.88f * h),
            new(0.18f * w, 0.80f * h),
            new(0.10f * w, 0.52f * h)
        };

        var cX = 0.50f * w;
        var cY = 0.50f * h;
        var maxRx = 0.40f * w;
        var maxRy = 0.40f * h;

        for (var c = 0; c < geometry.Count; c++)
        {
            var px = (float)geometry.CentroidX[c];
            var py = (float)geometry.CentroidY[c];

            var dx = (px - cX) / maxRx;
            var dy = (py - cY) / maxRy;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            var angle = MathF.Atan2(dy, dx);

            // 注入多频自然曲折海岸线（海湾、半岛与海角），消除纯椭圆阶梯切痕
            var organicRadius = 1.0f
                + 0.09f * MathF.Sin(3.0f * angle)
                + 0.06f * MathF.Cos(5.0f * angle + 0.8f)
                + 0.03f * MathF.Sin(9.0f * angle);

            // ⑤ 南荒海岸雕刻：在东南海湾方向 (angle 约 0.65 ~ 1.05 rad，dx>0, dy>0) 雕出深凹半月海湾
            if (angle > 0.62f && angle < 1.15f)
            {
                var bayIndent = MathF.Sin((angle - 0.62f) / (1.15f - 0.62f) * MathF.PI) * 0.13f;
                organicRadius -= bayIndent;
            }

            var normD = dist / organicRadius;

            if (normD < 0.90f)
            {
                var landFactor = 1.0f - normD / 0.90f;
                var minLandHeight = seaLevel + 0.08f + landFactor * 0.15f;

                // ② 中央神圣大湖洼地雕刻 (0.47 * w, 0.33 * h)
                var lakeDx = (px - 0.47f * w) / (0.045f * w);
                var lakeDy = (py - 0.33f * h) / (0.038f * h);
                var lakeDistSq = lakeDx * lakeDx + lakeDy * lakeDy;
                if (lakeDistSq < 1.0f)
                {
                    minLandHeight = MathF.Min(minLandHeight, seaLevel + 0.01f);
                }

                fields.Height[c] = MathF.Max(fields.Height[c], minLandHeight);
            }
            else if (normD < 1.12f)
            {
                // 海陆平滑过渡斜坡带（海岸线在 normD ~ 1.0 处自然形成，杜绝锯齿突变）
                var t = (normD - 0.90f) / 0.22f; // 0 -> 1
                var coastElev = seaLevel + 0.08f * (1.0f - t) - 0.12f * t;
                fields.Height[c] = MathF.Min(fields.Height[c], coastElev + 0.03f);
                fields.Height[c] = MathF.Max(fields.Height[c], coastElev - 0.03f);
            }
            else
            {
                // 大陆外围深海
                fields.Height[c] = MathF.Min(fields.Height[c], seaLevel - 0.16f);
            }

            // ⑤ 南荒外海仙岛与渔岛微地形雕刻 (0.86, 0.82) 与 (0.88, 0.72)
            var isle1Dx = (px - 0.86f * w) / (0.032f * w);
            var isle1Dy = (py - 0.82f * h) / (0.026f * h);
            var isle2Dx = (px - 0.88f * w) / (0.028f * w);
            var isle2Dy = (py - 0.72f * h) / (0.024f * h);
            if (isle1Dx * isle1Dx + isle1Dy * isle1Dy < 1.0f || isle2Dx * isle2Dx + isle2Dy * isle2Dy < 1.0f)
            {
                fields.Height[c] = MathF.Max(fields.Height[c], seaLevel + 0.05f);
            }
        }

        return hullPoints;
    }
}
