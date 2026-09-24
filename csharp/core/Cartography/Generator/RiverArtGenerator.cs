using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 河流水系与水体艺术化生成器（RiverArtGenerator）。
/// 
/// 遵循古代山水图谱“江河有源，主支相生，蜿蜒九曲，注海为归”的地理法则：
/// 1. 结构化树状水系：依据 RiverNetworkDefinition 生成主干大江、各路支流与汇水湖泊；
/// 2. 连续平滑多段线：使用 Catmull-Rom 插值生成高密度平滑点序列，彻底杜绝孤立断裂小线段；
/// 3. 自然渐变江宽：源头细如蚕丝、沿途汇纳百川逐渐开阔、入海口浩荡汪洋；
/// 4. 湖泊与水渚协同：在干支流交汇或盆地腹地生成清平镜湖（LakePond）；
/// 5. 沧海水波细纹（SeaWave）：海域生成微弧笔触水纹。
/// </summary>
public static class RiverArtGenerator
{
    public static List<BrushInstruction> GenerateBrushes(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        Dictionary<int, RegionStyle> regionStyles,
        MapBlueprint? blueprint = null)
    {
        var instructions = new List<BrushInstruction>();
        var seaLevel = options.SeaLevel;
        var w = geometry.Width;
        var h = geometry.Height;

        var hasNetwork = blueprint?.RiverNetworks != null && blueprint.RiverNetworks.Count > 0;

        if (hasNetwork)
        {
            // ── 模式 A: 消费结构化设计水系 (RiverNetworks) ──
            foreach (var rn in blueprint!.RiverNetworks)
            {
                var palette = rn.Palette;

                // 1. 生成主干大江连续平滑样条 (Main Stem)
                var pSource = ToWorld(rn.SourcePoint, w, h);
                var pMouth = ToWorld(rn.MouthPoint, w, h);
                var mainControls = new List<PolyVec2>(rn.MainWaypoints.Count + 2) { pSource };
                foreach (var wp in rn.MainWaypoints)
                {
                    mainControls.Add(ToWorld(wp, w, h));
                }
                mainControls.Add(pMouth);

                var mainSpline = BuildSmoothSpline(mainControls, sampleCount: 95);

                // 发射主干连续水墨多段线
                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.RiverStroke,
                    Position = mainSpline[mainSpline.Count / 2],
                    Points = mainSpline.ToArray(),
                    StrokeWidth = (rn.SourceWidth + rn.MouthWidth) * 0.5f,
                    Opacity = 0.92f,
                    YOrder = (float)mainSpline[0].Y - 5f,
                    Tint = palette,
                    VariantKey = "river_main_stem"
                });

                // 将主干轨迹标记至网格 fields.River，供古道架桥与农田滋养识别
                MarkRiverToFields(geometry, fields, mainSpline, 0.45f);

                // 2. 生成各级支流 (Branches)
                foreach (var br in rn.Branches)
                {
                    var bSource = ToWorld(br.SourcePoint, w, h);
                    var bConfluence = ToWorld(br.ConfluencePoint, w, h);
                    var bControls = new List<PolyVec2> { bSource };
                    foreach (var bwp in br.Waypoints)
                    {
                        bControls.Add(ToWorld(bwp, w, h));
                    }
                    bControls.Add(bConfluence);

                    var branchSpline = BuildSmoothSpline(bControls, sampleCount: 45);

                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.RiverStroke,
                        Position = branchSpline[branchSpline.Count / 2],
                        Points = branchSpline.ToArray(),
                        StrokeWidth = rn.SourceWidth * 1.35f * br.WidthScale,
                        Opacity = 0.88f,
                        YOrder = (float)branchSpline[0].Y - 4f,
                        Tint = palette,
                        VariantKey = "river_branch"
                    });

                    MarkRiverToFields(geometry, fields, branchSpline, 0.30f);
                }

                // 3. 水系串联的清平湖泊 (Lakes)
                foreach (var lPt in rn.Lakes)
                {
                    var lakeWorld = ToWorld(lPt, w, h);
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.LakePond,
                        Position = lakeWorld,
                        Scale = 1.15f,
                        Opacity = 0.90f,
                        YOrder = (float)lakeWorld.Y - 1f,
                        Tint = palette,
                        VariantKey = "system_lake"
                    });
                }
            }
        }
        else
        {
            // ── 模式 B: 回退水文追踪模式，组合为连续平滑水流 ──
            GenerateFallbackContinuousRivers(instructions, geometry, fields, seaLevel);
        }

        // 4. 生成海域细密水波纹线 (SeaWave)
        GenerateSeaWaveInstructions(instructions, geometry, fields, options);

        return instructions;
    }

    private static PolyVec2 ToWorld(PolyVec2 pt, double w, double h)
    {
        if (pt.X <= 1.05 && pt.Y <= 1.05)
        {
            return new PolyVec2(pt.X * w, pt.Y * h);
        }
        return pt;
    }

    private static List<PolyVec2> BuildSmoothSpline(IReadOnlyList<PolyVec2> keyPoints, int sampleCount)
    {
        if (keyPoints.Count < 2) return new List<PolyVec2>(keyPoints);
        if (keyPoints.Count == 2)
        {
            var res = new List<PolyVec2>(sampleCount);
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)(sampleCount - 1);
                res.Add(keyPoints[0] * (1f - t) + keyPoints[1] * t);
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

    private static void MarkRiverToFields(CellGeometry geom, CellFields fields, IReadOnlyList<PolyVec2> spline, float riverVal)
    {
        for (var i = 0; i < spline.Count; i++)
        {
            var pt = spline[i];
            var c = geom.FindCell(pt.X, pt.Y);
            if (c >= 0 && c < fields.Count)
            {
                fields.River[c] = MathF.Max(fields.River[c], riverVal);
                fields.Flux[c] = MathF.Max(fields.Flux[c], 2.5f);
            }
        }
    }

    private static void GenerateFallbackContinuousRivers(
        List<BrushInstruction> instructions,
        CellGeometry geometry,
        CellFields fields,
        float seaLevel)
    {
        var downslope = fields.Downslope;
        var river = fields.River;
        var visited = new bool[fields.Count];

        for (var i = 0; i < fields.Count; i++)
        {
            if (river[i] <= 0.15f || fields.Height[i] <= seaLevel || visited[i]) continue;

            var path = new List<PolyVec2>();
            var curr = i;
            var maxSteps = 40;

            while (curr >= 0 && curr < fields.Count && maxSteps-- > 0)
            {
                visited[curr] = true;
                path.Add(new PolyVec2(geometry.CentroidX[curr], geometry.CentroidY[curr]));

                var next = downslope[curr];
                if (next < 0 || next >= fields.Count || next == curr) break;
                if (fields.Height[next] <= seaLevel)
                {
                    var pCoast = path[^1].Lerp(new PolyVec2(geometry.CentroidX[next], geometry.CentroidY[next]), 0.45d);
                    path.Add(pCoast);
                    break;
                }
                curr = next;
            }

            if (path.Count >= 2)
            {
                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.RiverStroke,
                    Position = path[path.Count / 2],
                    Points = path.ToArray(),
                    StrokeWidth = 3.5f,
                    Opacity = 0.88f,
                    YOrder = (float)path[0].Y - 3f,
                    Tint = CartographyColor.ColdBlue,
                    VariantKey = "river_ink_stroke"
                });
            }
        }
    }

    private static void GenerateSeaWaveInstructions(
        List<BrushInstruction> instructions,
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options)
    {
        var seaLevel = options.SeaLevel;
        var width = geometry.Width;
        var height = geometry.Height;
        var rand = new Random((int)(options.Seed ^ 0x5a5a));

        var stepY = 24.0;
        var stepX = 32.0;

        for (var y = 16.0; y < height - 16.0; y += stepY)
        {
            var offset = (rand.NextDouble() - 0.5) * 10.0;
            for (var x = 16.0; x < width - 16.0; x += stepX)
            {
                var curX = x + offset + (rand.NextDouble() - 0.5) * 6.0;
                var curY = y + (rand.NextDouble() - 0.5) * 4.0;

                var cell = geometry.FindCell(curX, curY);
                if (cell < 0 || cell >= fields.Count || fields.Height[cell] > seaLevel) continue;

                var arcLen = 14.0 + rand.NextDouble() * 8.0;
                var p0 = new PolyVec2(curX - arcLen * 0.5, curY);
                var p1 = new PolyVec2(curX, curY - 2.5);
                var p2 = new PolyVec2(curX + arcLen * 0.5, curY);

                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.SeaWave,
                    Position = new PolyVec2(curX, curY),
                    Points = new[] { p0, p1, p2 },
                    StrokeWidth = 1.0f,
                    Opacity = 0.45f,
                    YOrder = (float)curY - 100f,
                    Tint = CartographyColor.SeaWave,
                    VariantKey = "wave_arc"
                });
            }
        }
    }
}
