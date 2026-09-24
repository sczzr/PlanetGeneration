using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Planning;

/// <summary>规划主山脉、支脉和山系拓扑。</summary>
internal static class MountainLayoutPlanner
{
    internal static (List<PlannedMountainChain> chains, MountainTopology topology) PlanMountainChains(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        MapBlueprint blueprint,
        float w,
        float h)
    {
        var result = new List<PlannedMountainChain>();
        var topology = new MountainTopology
        {
            Name = "苍冥天脊立体山系拓扑",
            SnowLineAltitude = 0.72f
        };
        var seaLevel = options.SeaLevel;

        var sourceRanges = blueprint.MountainRanges.Count > 0
            ? blueprint.MountainRanges
            : ConvertFromSpines(blueprint.MountainSpines);

        foreach (var def in sourceRanges)
        {
            var pStart = PlanningGeometry.ToWorld(def.StartPoint, w, h);
            var pEnd = PlanningGeometry.ToWorld(def.EndPoint, w, h);
            var pControls = def.ControlPoints.Select(p => PlanningGeometry.ToWorld(p, w, h)).ToList();

            // 生成高频连贯样条曲线（步长约 22px，山脊紧密咬合）
            var spline = PlanningGeometry.BuildDenseSpline(pStart, pControls, pEnd, sampleCount: 48);

            // 生成向平原侧舒展的侧脉支脉（Spurs）
            var plannedBranches = new List<PlannedMountainBranch>();
            if (def.SpurCount > 0 && def.SpurLength > 0f)
            {
                for (var s = 0; s < def.SpurCount; s++)
                {
                    var spurT = (s + 0.5f) / def.SpurCount;
                    var spurBaseIdx = Math.Clamp((int)(spurT * (spline.Count - 1)), 1, spline.Count - 2);
                    var basePt = spline[spurBaseIdx];
                    var prevPt = spline[spurBaseIdx - 1];
                    var nextPt = spline[spurBaseIdx + 1];

                    // 计算法线方向（南向舒展）
                    var tanX = nextPt.X - prevPt.X;
                    var tanY = nextPt.Y - prevPt.Y;
                    var normX = -tanY;
                    var normY = tanX;
                    var len = MathF.Sqrt((float)(normX * normX + normY * normY));
                    if (len > 0.001f)
                    {
                        normX /= len;
                        normY /= len;
                    }
                    if (normY < 0) { normX = -normX; normY = -normY; } // 强制指向南方平原

                    var spurEnd = new PolyVec2(
                        basePt.X + normX * def.SpurLength,
                        basePt.Y + normY * def.SpurLength);

                    var spurSpline = PlanningGeometry.BuildDenseSpline(basePt, new List<PolyVec2>
                    {
                        new(basePt.X + normX * def.SpurLength * 0.5f + normY * 12.0f,
                            basePt.Y + normY * def.SpurLength * 0.5f - normX * 12.0f)
                    }, spurEnd, sampleCount: 16);

                    plannedBranches.Add(new PlannedMountainBranch
                    {
                        Name = $"{def.Name}·支脉{s + 1}",
                        SpineCurve = spurSpline,
                        RidgeWidth = def.RidgeWidth * 0.70f,
                        Palette = def.Palette
                    });

                    // 拓扑结构收集支脉骨架
                    topology.Branches.Add(spurSpline);
                }
            }

            // 区分主脊与次级支脉拓扑
            if (def.Id == 1)
            {
                topology.MainSpine = spline;
                foreach (var mpr in def.MajorPeakRatios)
                {
                    var peakIdx = Math.Clamp((int)(mpr * (spline.Count - 1)), 0, spline.Count - 1);
                    topology.Peaks.Add(spline[peakIdx]);
                }
                foreach (var pr in def.PassRatios)
                {
                    var passIdx = Math.Clamp((int)(pr * (spline.Count - 1)), 0, spline.Count - 1);
                    topology.Passes.Add(spline[passIdx]);
                }
                // 标注山谷幽谷源头
                topology.Valleys.Add(spline[(int)(0.18f * (spline.Count - 1))]);
                topology.Valleys.Add(spline[(int)(0.58f * (spline.Count - 1))]);
            }
            else
            {
                topology.Branches.Add(spline);
            }

            var chain = new PlannedMountainChain
            {
                Id = def.Id,
                Name = def.Name,
                SpineCurve = spline,
                RidgeWidth = def.RidgeWidth,
                MajorPeakRatios = def.MajorPeakRatios,
                PassRatios = def.PassRatios,
                HasSnowCap = def.HasSnowCap,
                Palette = def.Palette,
                SpurCount = def.SpurCount,
                SpurLength = def.SpurLength,
                Branches = plannedBranches
            };

            // 将主脊骨架刷入物理网格 CellFields
            for (var i = 0; i < spline.Count; i++)
            {
                var pt = spline[i];
                var t = i / (float)(spline.Count - 1);

                var isPass = false;
                foreach (var pr in def.PassRatios)
                {
                    if (MathF.Abs(t - pr) < 0.045f)
                    {
                        isPass = true;
                        break;
                    }
                }

                var isMajorPeak = false;
                foreach (var mpr in def.MajorPeakRatios)
                {
                    if (MathF.Abs(t - mpr) < 0.06f)
                    {
                        isMajorPeak = true;
                        break;
                    }
                }

                var cell = geometry.FindCell(pt.X, pt.Y);
                if (cell >= 0 && cell < fields.Count)
                {
                    if (isPass)
                    {
                        // 隘口：低缓山谷，供古道穿行
                        fields.Height[cell] = seaLevel + 0.16f;
                        fields.Landform[cell] = (byte)LandformType.Hill;
                    }
                    else
                    {
                        // 巍峨主脊/雪峰
                        var elev = isMajorPeak ? seaLevel + 0.72f : seaLevel + 0.48f;
                        fields.Height[cell] = MathF.Max(fields.Height[cell], elev);
                        fields.Landform[cell] = (byte)LandformType.Mountain;
                        fields.Biome[cell] = (def.HasSnowCap && elev > seaLevel + 0.60f)
                            ? (byte)BiomeType.SnowyMountain
                            : (byte)BiomeType.RockyMountain;
                    }
                }
            }

            // 将支脉刷入物理网格
            foreach (var br in plannedBranches)
            {
                for (var bi = 0; bi < br.SpineCurve.Count; bi++)
                {
                    var bPt = br.SpineCurve[bi];
                    var bCell = geometry.FindCell(bPt.X, bPt.Y);
                    if (bCell >= 0 && bCell < fields.Count && fields.Height[bCell] > seaLevel)
                    {
                        var bElev = seaLevel + 0.28f + (1f - (float)bi / br.SpineCurve.Count) * 0.16f;
                        fields.Height[bCell] = MathF.Max(fields.Height[bCell], bElev);
                        if (fields.Landform[bCell] != (byte)LandformType.Mountain)
                        {
                            fields.Landform[bCell] = (byte)LandformType.Hill;
                        }
                    }
                }
            }

            result.Add(chain);
        }

        return (result, topology);
    }

    private static List<MountainRangeDefinition> ConvertFromSpines(IReadOnlyList<MountainSpineBlueprint> spines)
    {
        var list = new List<MountainRangeDefinition>();
        foreach (var s in spines)
        {
            list.Add(new MountainRangeDefinition
            {
                Id = s.Id,
                Name = s.Name,
                StartPoint = s.StartPoint,
                ControlPoints = s.ControlPoints,
                EndPoint = s.EndPoint,
                RidgeWidth = s.SpineWidth,
                MajorPeakRatios = s.MajorPeakRatios,
                PassRatios = s.PassRatios,
                HasSnowCap = s.HasSnowCap,
                Palette = s.Palette
            });
        }
        return list;
    }
}
