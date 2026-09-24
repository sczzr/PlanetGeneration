using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Planning;

/// <summary>规划道路与依赖道路、河网的平原装饰。</summary>
internal static class InfrastructurePlanner
{
    internal static PlannedRoadGraph PlanRoadGraph(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        MapBlueprint blueprint,
        List<PlannedSettlement> settlements,
        List<PlannedMountainChain> mountains,
        List<PlannedRiverSystem> rivers,
        float w,
        float h)
    {
        var graph = new PlannedRoadGraph();
        var settlementMap = settlements.ToDictionary(s => s.Name, s => s);

        // 1. 基于 Blueprint.Highways 编译核心干线官道
        var highways = blueprint.Highways.Count > 0
            ? blueprint.Highways
            : new List<TradeHighwayBlueprint>
            {
                new() { Name = "平原东西大官道", FromSettlement = "翠微古郡", ToSettlement = "神京天都", HighwayTier = 1 },
                new() { Name = "通海金丝官道", FromSettlement = "神京天都", ToSettlement = "沧海龙津", HighwayTier = 1 },
                new() { Name = "北塞铁关驿道", FromSettlement = "北麓铁府", ToSettlement = "雁门天堑", HighwayTier = 2 },
                new() { Name = "天子御道", FromSettlement = "雁门天堑", ToSettlement = "神京天都", HighwayTier = 1 },
                new() { Name = "云梦泽官道", FromSettlement = "神京天都", ToSettlement = "姑苏泽城", HighwayTier = 1 },
                new() { Name = "西荒丝路驼道", FromSettlement = "神京天都", ToSettlement = "金沙古堡", HighwayTier = 2 }
            };

        foreach (var hw in highways)
        {
            if (!settlementMap.TryGetValue(hw.FromSettlement, out var sFrom) ||
                !settlementMap.TryGetValue(hw.ToSettlement, out var sTo))
            {
                continue;
            }

            var p0 = sFrom.Position;
            var p1 = sTo.Position;

            // 生成贴合平原地势的自然微弯折线
            var mid = new PolyVec2((p0.X + p1.X) * 0.5, (p0.Y + p1.Y) * 0.5);
            var dx = p1.X - p0.X;
            var dy = p1.Y - p0.Y;
            var len = MathF.Sqrt((float)(dx * dx + dy * dy));

            // 微小侧向弧度，模拟避开陡坡和深沼的古道
            var perpX = -dy / (len + 0.001f) * 12.0;
            var perpY = dx / (len + 0.001f) * 12.0;
            var ctrl = new PolyVec2(mid.X + perpX, mid.Y + perpY);

            var waypoints = PlanningGeometry.BuildDenseSpline(p0, new List<PolyVec2> { ctrl }, p1, sampleCount: 24);

            graph.Segments.Add(new PlannedRoadSegment
            {
                FromSettlement = hw.FromSettlement,
                ToSettlement = hw.ToSettlement,
                Waypoints = waypoints,
                RoadTier = hw.HighwayTier
            });
        }

        // 2. 将离干线较近的村落/要塞自动通过支线驿道接入道路网
        var majorSettlements = settlements.Where(s => s.Tier >= 3).ToList();
        foreach (var s in settlements.Where(s => s.Tier < 3))
        {
            // 若已作为 Highway 端点则跳过
            if (graph.Segments.Any(seg => seg.FromSettlement == s.Name || seg.ToSettlement == s.Name)) continue;

            // 找最近的州府或国都
            var nearest = majorSettlements
                .OrderBy(m => (m.Position.X - s.Position.X) * (m.Position.X - s.Position.X) +
                              (m.Position.Y - s.Position.Y) * (m.Position.Y - s.Position.Y))
                .FirstOrDefault();

            if (nearest != null)
            {
                var waypoints = PlanningGeometry.BuildDenseSpline(s.Position, new List<PolyVec2>(), nearest.Position, sampleCount: 16);
                graph.Segments.Add(new PlannedRoadSegment
                {
                    FromSettlement = s.Name,
                    ToSettlement = nearest.Name,
                    Waypoints = waypoints,
                    RoadTier = s.Tier == 2 ? 2 : 3
                });
            }
        }

        return graph;
    }

    internal static PlannedDecorations PlanDecorations(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        MapBlueprint blueprint,
        PlannedRoadGraph roads,
        List<PlannedRiverSystem> rivers,
        List<PlannedSettlement> settlements,
        float w,
        float h)
    {
        var pd = blueprint.PlainsDetail;
        var farmlandCorridors = new List<PolyVec2>();
        var farmlandClusters = new List<PolyVec2>();
        var knolls = new List<PolyVec2>();
        var lakes = new List<PolyVec2>();
        var reedWetlands = new List<PolyVec2>();
        var grassWaves = new List<PolyVec2>();

        var rand = new Random((int)(options.Seed ^ 0x444543));

        // ── 1. 文明活动改造：沿官道与平原江河两岸生成水浇田走廊 (Farmland Corridors) ──
        // 彻底消灭平原空旷，构建「河谷文明带」：使官道两翼水田密布、阡陌纵横
        foreach (var seg in roads.Segments.Where(s => s.RoadTier <= 2))
        {
            // 核心平原走廊加密：丰泽、神京、浔阳、江陵之间大官道步长缩小且双层铺展
            var isCentralPlainsRoad = seg.FromSettlement is "丰泽古郡" or "神京天都" or "浔阳古埠" or "江陵大都" ||
                                      seg.ToSettlement is "丰泽古郡" or "神京天都" or "浔阳古埠" or "江陵大都";
            var step = isCentralPlainsRoad ? 2 : 3;

            for (var i = 2; i < seg.Waypoints.Count - 2; i += step)
            {
                var pt = seg.Waypoints[i];
                var prev = seg.Waypoints[i - 1];
                var next = seg.Waypoints[i + 1];

                var dx = next.X - prev.X;
                var dy = next.Y - prev.Y;
                var len = MathF.Sqrt((float)(dx * dx + dy * dy));
                if (len < 0.001f) continue;
                var nx = -dy / len;
                var ny = dx / len;

                // 沿道两侧铺设水浇田
                var layerCount = isCentralPlainsRoad ? 2 : 1;
                for (var layer = 0; layer < layerCount; layer++)
                {
                    for (var side = -1; side <= 1; side += 2)
                    {
                        var baseDist = layer == 0 ? 22.0 : 38.0;
                        var offsetDist = baseDist + (rand.NextSingle() - 0.5f) * 10.0;
                        var farmPos = new PolyVec2(pt.X + nx * side * offsetDist, pt.Y + ny * side * offsetDist);

                        var c = geometry.FindCell(farmPos.X, farmPos.Y);
                        if (c >= 0 && c < fields.Count && fields.Height[c] > options.SeaLevel &&
                            fields.Landform[c] != (byte)LandformType.Mountain &&
                            fields.Biome[c] != (byte)BiomeType.TropicalDesert &&
                            fields.Moisture[c] >= 0.35f)
                        {
                            // 避开已有聚落核心（保持城郭前后有适度透气留白）
                            if (!settlements.Any(s => MathF.Sqrt((float)((s.Position.X - farmPos.X) * (s.Position.X - farmPos.X) + (s.Position.Y - farmPos.Y) * (s.Position.Y - farmPos.Y))) < 28.0f))
                            {
                                farmlandCorridors.Add(farmPos);
                            }
                        }
                    }
                }
            }
        }

        // ── 2. 传统农耕聚集区 (Farmland Clusters) ──
        if (pd != null)
        {
            foreach (var az in pd.AgriculturalZones)
            {
                var baseCenter = PlanningGeometry.ToWorld(az.Center, w, h);
                farmlandClusters.Add(baseCenter);

                // 每个主要农业区扩展 1-2 处卫星梯田群，形成连片农耕走廊
                for (var si = 0; si < 2; si++)
                {
                    var sAngle = (float)(si * Math.PI + rand.NextDouble() * 0.6);
                    var sDist = 28.0f + rand.NextSingle() * 14.0f;
                    var satPos = baseCenter + new PolyVec2(MathF.Cos(sAngle) * sDist, MathF.Sin(sAngle) * sDist);
                    var sc = geometry.FindCell(satPos.X, satPos.Y);
                    if (sc >= 0 && sc < fields.Count && fields.Height[sc] > options.SeaLevel &&
                        fields.Landform[sc] != (byte)LandformType.Mountain &&
                        fields.Biome[sc] != (byte)BiomeType.TropicalDesert)
                    {
                        farmlandClusters.Add(satPos);
                    }
                }
            }
            foreach (var k in pd.SolitaryKnolls)
            {
                knolls.Add(PlanningGeometry.ToWorld(k, w, h));
            }
            foreach (var l in pd.MirrorLakes)
            {
                lakes.Add(PlanningGeometry.ToWorld(l, w, h));
            }
            foreach (var gw in pd.GrassWaveZones)
            {
                grassWaves.Add(PlanningGeometry.ToWorld(gw, w, h));
            }
        }
        else
        {
            farmlandClusters.Add(new PolyVec2(0.48 * w, 0.48 * h));
            farmlandClusters.Add(new PolyVec2(0.56 * w, 0.53 * h));
            farmlandClusters.Add(new PolyVec2(0.62 * w, 0.58 * h));
            farmlandClusters.Add(new PolyVec2(0.38 * w, 0.49 * h));

            knolls.Add(new PolyVec2(0.40 * w, 0.34 * h));
            knolls.Add(new PolyVec2(0.58 * w, 0.33 * h));
            knolls.Add(new PolyVec2(0.38 * w, 0.46 * h));
            knolls.Add(new PolyVec2(0.54 * w, 0.42 * h));
            knolls.Add(new PolyVec2(0.62 * w, 0.46 * h));
            knolls.Add(new PolyVec2(0.46 * w, 0.56 * h));
            knolls.Add(new PolyVec2(0.66 * w, 0.54 * h));

            lakes.Add(new PolyVec2(0.45 * w, 0.46 * h));
            lakes.Add(new PolyVec2(0.56 * w, 0.55 * h));
            lakes.Add(new PolyVec2(0.58 * w, 0.68 * h));
        }

        // 湖泽周边点缀芦苇湿地
        foreach (var lk in lakes)
        {
            reedWetlands.Add(new PolyVec2(lk.X + 14.0, lk.Y + 8.0));
            reedWetlands.Add(new PolyVec2(lk.X - 12.0, lk.Y - 10.0));
        }

        return new PlannedDecorations
        {
            FarmlandCorridors = farmlandCorridors,
            FarmlandClusters = farmlandClusters,
            SolitaryKnolls = knolls,
            MirrorLakes = lakes,
            ReedWetlands = reedWetlands,
            GrassWaves = grassWaves
        };
    }
}
