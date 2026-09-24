using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 体块化森林生成器 (ForestMassGenerator)。
/// 
/// 负责将大区规划编译为具有实体剪影感（Forest Mass）的有机大斑块，
/// 包含和谐起伏的外凸凹边界与林间自然留白隙地（Clearings）。
/// </summary>
public static class ForestMassGenerator
{
    public static ForestMassData Generate(
        PolyVec2 center,
        float radiusX,
        float radiusY,
        float rotationAngle,
        int seed,
        int clearingCount = 3)
    {
        var rand = new Random(seed ^ 0x464f52); // "FOR"

        // 1. 生成自然起伏的有机凹凸轮廓多边形 (Harmonic Perturbed Hull)
        const int samplePoints = 36;
        var hull = new List<PolyVec2>(samplePoints);
        var cosRot = MathF.Cos(rotationAngle);
        var sinRot = MathF.Sin(rotationAngle);

        var phase1 = rand.NextSingle() * MathF.PI * 2f;
        var phase2 = rand.NextSingle() * MathF.PI * 2f;
        var phase3 = rand.NextSingle() * MathF.PI * 2f;

        for (var i = 0; i < samplePoints; i++)
        {
            var theta = (i / (float)samplePoints) * MathF.PI * 2f;

            // 三阶谐波扰动：形成自然林岬、林湾与伸展叶瓣
            var harmonic = 1.0f
                + 0.16f * MathF.Sin(3f * theta + phase1)
                + 0.10f * MathF.Cos(5f * theta + phase2)
                - 0.08f * MathF.Sin(2f * theta + phase3);

            var rx = radiusX * harmonic;
            var ry = radiusY * harmonic;

            var lx = MathF.Cos(theta) * rx;
            var ly = MathF.Sin(theta) * ry;

            // 应用区域旋转
            var gx = center.X + (lx * cosRot - ly * sinRot);
            var gy = center.Y + (lx * sinRot + ly * cosRot);

            hull.Add(new PolyVec2(gx, gy));
        }

        // 2. 在林腹生成 2~4 处自然林间隙地（Clearings）
        var clearings = new List<ForestClearing>(clearingCount);
        var clearingNames = new[] { "青岚清修谷", "灵泉隙地", "仙隐空坪", "栖霞林间" };

        for (var k = 0; k < clearingCount; k++)
        {
            var cAngle = (k / (float)clearingCount) * MathF.PI * 2f + (rand.NextSingle() - 0.5f) * 0.6f;
            var cDistRatio = 0.30f + rand.NextSingle() * 0.35f; // 位于林中距核心 30%~65% 处

            var clx = MathF.Cos(cAngle) * (radiusX * cDistRatio);
            var cly = MathF.Sin(cAngle) * (radiusY * cDistRatio);

            var cgx = center.X + (clx * cosRot - cly * sinRot);
            var cgy = center.Y + (clx * sinRot + cly * cosRot);

            clearings.Add(new ForestClearing
            {
                Position = new PolyVec2(cgx, cgy),
                Radius = 28.0f + rand.NextSingle() * 18.0f,
                Name = clearingNames[k % clearingNames.Length]
            });
        }

        return new ForestMassData
        {
            HullPolygon = hull,
            CoreRatio = 0.72f,
            EdgeMarginWidth = 42.0f,
            Clearings = clearings,
            CanopyDensity = 1.0f
        };
    }

    /// <summary>
    /// 判断测试点是否落在森林实体多边形内部（基于射线交叉判定）。
    /// </summary>
    public static bool IsInside(PolyVec2 pt, IReadOnlyList<PolyVec2> hull)
    {
        if (hull == null || hull.Count < 3) return false;
        var inside = false;
        for (int i = 0, j = hull.Count - 1; i < hull.Count; j = i++)
        {
            if (((hull[i].Y > pt.Y) != (hull[j].Y > pt.Y)) &&
                (pt.X < (hull[j].X - hull[i].X) * (pt.Y - hull[i].Y) / (hull[j].Y - hull[i].Y) + hull[i].X))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>
    /// 判断测试点是否落在任何林间空地（Clearing）范围内。
    /// </summary>
    public static bool IsInAnyClearing(PolyVec2 pt, IReadOnlyList<ForestClearing> clearings, float margin = 0f)
    {
        if (clearings == null || clearings.Count == 0) return false;
        for (var i = 0; i < clearings.Count; i++)
        {
            var cl = clearings[i];
            var r = cl.Radius + margin;
            if (pt.DistanceSquaredTo(cl.Position) <= r * r)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 计算测试点到多边形轮廓边界线的最短距离。
    /// </summary>
    public static float ComputeDistanceToBoundary(PolyVec2 pt, IReadOnlyList<PolyVec2> hull)
    {
        if (hull == null || hull.Count < 2) return 0f;
        var minSq = double.MaxValue;

        for (int i = 0, j = hull.Count - 1; i < hull.Count; j = i++)
        {
            var p1 = hull[j];
            var p2 = hull[i];

            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            var lenSq = dx * dx + dy * dy;

            double distSq;
            if (lenSq < 1e-8)
            {
                distSq = pt.DistanceSquaredTo(p1);
            }
            else
            {
                var t = Math.Clamp(((pt.X - p1.X) * dx + (pt.Y - p1.Y) * dy) / lenSq, 0.0, 1.0);
                var projX = p1.X + t * dx;
                var projY = p1.Y + t * dy;
                var proj = new PolyVec2(projX, projY);
                distSq = pt.DistanceSquaredTo(proj);
            }

            if (distSq < minSq) minSq = distSq;
        }

        return (float)Math.Sqrt(minSq);
    }
}

