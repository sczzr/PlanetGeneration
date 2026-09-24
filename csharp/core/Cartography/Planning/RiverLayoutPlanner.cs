using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Planning;

/// <summary>规划蓝图河网及其艺术工作场标记。</summary>
internal static class RiverLayoutPlanner
{
    internal static List<PlannedRiverSystem> PlanRiverNetworks(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        MapBlueprint blueprint,
        float w,
        float h)
    {
        var result = new List<PlannedRiverSystem>();
        var seaLevel = options.SeaLevel;

        var sourceRivers = blueprint.RiverNetworks.Count > 0
            ? blueprint.RiverNetworks
            : ConvertFromCorridors(blueprint.RiverCorridors);

        foreach (var def in sourceRivers)
        {
            var pSource = PlanningGeometry.ToWorld(def.SourcePoint, w, h);
            var pMouth = PlanningGeometry.ToWorld(def.MouthPoint, w, h);
            var pMains = def.MainWaypoints.Select(p => PlanningGeometry.ToWorld(p, w, h)).ToList();

            // 生成具备东方山水“九曲回环”韵味的大江主干
            var allWaypoints = new List<PolyVec2> { pSource };
            allWaypoints.AddRange(pMains);
            allWaypoints.Add(pMouth);

            var denseRiver = PlanningGeometry.BuildMeanderingRiverSpline(allWaypoints, sampleCount: 80);

            var plannedBranches = new List<PlannedRiverBranch>();
            foreach (var b in def.Branches)
            {
                var bSource = PlanningGeometry.ToWorld(b.SourcePoint, w, h);
                var bMouth = PlanningGeometry.ToWorld(b.ConfluencePoint, w, h);
                var bWaypoints = b.Waypoints.Select(p => PlanningGeometry.ToWorld(p, w, h)).ToList();

                var bPoints = new List<PolyVec2> { bSource };
                bPoints.AddRange(bWaypoints);
                bPoints.Add(bMouth);

                var denseBranch = PlanningGeometry.BuildMeanderingRiverSpline(bPoints, sampleCount: 38);
                plannedBranches.Add(new PlannedRiverBranch
                {
                    Name = b.Name,
                    SourcePoint = bSource,
                    Waypoints = denseBranch,
                    ConfluencePoint = bMouth,
                    WidthScale = b.WidthScale
                });

                // 刷入支流水文数据
                foreach (var bpt in denseBranch)
                {
                    var bc = geometry.FindCell(bpt.X, bpt.Y);
                    if (bc >= 0 && bc < fields.Count)
                    {
                        fields.River[bc] = MathF.Max(fields.River[bc], 5.5f);
                        fields.Moisture[bc] = MathF.Max(fields.Moisture[bc], 0.75f);
                    }
                }
            }

            var lakes = def.Lakes.Select(p => PlanningGeometry.ToWorld(p, w, h)).ToList();

            var riverSys = new PlannedRiverSystem
            {
                Id = def.Id,
                Name = def.Name,
                SourcePoint = pSource,
                MainWaypoints = denseRiver,
                MouthPoint = pMouth,
                SourceWidth = def.SourceWidth,
                MouthWidth = def.MouthWidth,
                Branches = plannedBranches,
                Lakes = lakes,
                Palette = def.Palette
            };

            // 将主江水文数据烙印入 fields.River 与 fields.Moisture
            foreach (var pt in denseRiver)
            {
                var c = geometry.FindCell(pt.X, pt.Y);
                if (c >= 0 && c < fields.Count)
                {
                    fields.River[c] = MathF.Max(fields.River[c], 9.5f);
                    fields.Moisture[c] = MathF.Max(fields.Moisture[c], 0.88f);
                }
            }

            // 湖泽区域润泽
            foreach (var lpt in lakes)
            {
                var lc = geometry.FindCell(lpt.X, lpt.Y);
                if (lc >= 0 && lc < fields.Count)
                {
                    fields.Moisture[lc] = 0.95f;
                }
            }

            result.Add(riverSys);
        }

        return result;
    }

    private static List<RiverNetworkDefinition> ConvertFromCorridors(IReadOnlyList<RiverCorridorBlueprint> corridors)
    {
        var list = new List<RiverNetworkDefinition>();
        foreach (var c in corridors)
        {
            list.Add(new RiverNetworkDefinition
            {
                Id = c.Id,
                Name = c.Name,
                SourcePoint = c.SourcePoint,
                MainWaypoints = c.Waypoints,
                MouthPoint = c.MouthPoint,
                SourceWidth = 2.4f * c.WidthScale,
                MouthWidth = 9.0f * c.WidthScale,
                Palette = CartographyColor.ColdBlue
            });
        }
        return list;
    }
}
