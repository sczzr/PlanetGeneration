using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Planning;

/// <summary>规划聚落布局和对应视觉地标。</summary>
internal static class SettlementLayoutPlanner
{
    internal static List<PlannedSettlement> PlanSettlements(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        MapBlueprint blueprint,
        float w,
        float h)
    {
        var result = new List<PlannedSettlement>();

        var defs = blueprint.SettlementDefinitions.Count > 0
            ? blueprint.SettlementDefinitions
            : ConvertFromStrategic(blueprint.Settlements);

        foreach (var def in defs)
        {
            var pos = PlanningGeometry.ToWorld(def.Position, w, h);
            var anchor = def.AnchorType switch
            {
                GeographicAnchorType.MountainPass => PlannedSettlementAnchor.MountainPass,
                GeographicAnchorType.RiverConfluence => PlannedSettlementAnchor.RiverConfluence,
                GeographicAnchorType.HeartlandCapital => PlannedSettlementAnchor.RiverLoopCapital,
                GeographicAnchorType.NaturalBayHarbor => PlannedSettlementAnchor.SeaHarbor,
                GeographicAnchorType.OasisCrossroad => PlannedSettlementAnchor.DesertOasis,
                GeographicAnchorType.OasisTradeCity => PlannedSettlementAnchor.OasisTradeCity,
                GeographicAnchorType.PlainAgriculturalCity => PlannedSettlementAnchor.PlainAgriculturalCity,
                GeographicAnchorType.SacredSanctuary => PlannedSettlementAnchor.SacredSanctuary,
                GeographicAnchorType.CanalWaterTown => PlannedSettlementAnchor.CanalTown,
                GeographicAnchorType.NorthwestBorderCity => PlannedSettlementAnchor.NorthwestBorderCity,
                GeographicAnchorType.MountainFortress => PlannedSettlementAnchor.MountainFort,
                GeographicAnchorType.RiverFerryPort => PlannedSettlementAnchor.RiverFerryPort,
                GeographicAnchorType.RiverGarrison => PlannedSettlementAnchor.RiverGarrison,
                GeographicAnchorType.SacredTown => PlannedSettlementAnchor.SacredTown,
                GeographicAnchorType.MountainMiningCity => PlannedSettlementAnchor.MountainMiningCity,
                GeographicAnchorType.ForestMarginCity => PlannedSettlementAnchor.ForestMarginCity,
                GeographicAnchorType.AncientRelicSite => PlannedSettlementAnchor.AncientRelic,
                GeographicAnchorType.WetlandWaterTown => PlannedSettlementAnchor.WetlandWaterTown,
                GeographicAnchorType.AgriculturalVillage => PlannedSettlementAnchor.FarmVillage,
                GeographicAnchorType.ValleyHaven => PlannedSettlementAnchor.ValleyHaven,
                GeographicAnchorType.FerryVillage => PlannedSettlementAnchor.FerryVillage,
                GeographicAnchorType.CoastalHaven => PlannedSettlementAnchor.FishingHaven,
                _ => PlannedSettlementAnchor.FarmVillage
            };

            var tier = def.Tier switch
            {
                CartographySettlementTier.Capital => 4,
                CartographySettlementTier.City => 3,
                CartographySettlementTier.Harbor => 3,
                CartographySettlementTier.Town => 2,
                _ => 1
            };

            // 计算地理因果律评分：CityScore = river*0.4 + road*0.3 + fertility*0.2 + coast*0.2
            var cell = geometry.FindCell(pos.X, pos.Y);
            var riverScore = cell >= 0 && cell < fields.Count && fields.River[cell] > 0.5f ? 1.0f : 0.4f;
            var fertility = cell >= 0 && cell < fields.Count && fields.Moisture[cell] > 0.4f ? 0.9f : 0.3f;
            var score = riverScore * 0.4f + fertility * 0.3f + (tier >= 3 ? 0.3f : 0.1f);

            var scale = tier switch
            {
                4 => 1.45f,
                3 => 1.25f,
                2 => 1.10f,
                _ => 0.88f
            };

            result.Add(new PlannedSettlement
            {
                Id = def.Id,
                Name = def.Name,
                Position = pos,
                AnchorType = anchor,
                Tier = tier,
                CityScore = score,
                Description = def.Description,
                ShowLabel = def.ShowLabel,
                Scale = scale
            });
        }

        return result;
    }

    internal static List<PlannedLandmark> PlanGrandLandmarks(
        float w,
        float h,
        List<PlannedMountainChain> mountains,
        List<PlannedSettlement> settlements)
    {
        return new List<PlannedLandmark>
        {
            new()
            {
                Id = 1,
                Name = "苍冥雪峰绝顶",
                Type = PlannedLandmarkType.SnowPeakSummit,
                Position = new PolyVec2(0.49 * w, 0.20 * h),
                VisualScale = 1.65f,
                Description = "极北第一天柱神峰，万古积雪，俯瞰九州，天水与圣湖发源地。",
                Palette = CartographyColor.White
            },
            new()
            {
                Id = 2,
                Name = "神京天都·万象神宫",
                Type = PlannedLandmarkType.ImperialPalace,
                Position = new PolyVec2(0.50 * w, 0.46 * h),
                VisualScale = 1.55f,
                Description = "中原天下大都，紫宸金阙，四海承平之极枢。",
                Palette = CartographyColor.CinnabarRed
            },
            new()
            {
                Id = 3,
                Name = "临海沧津·万舶巨港",
                Type = PlannedLandmarkType.CangjinPort,
                Position = new PolyVec2(0.78 * w, 0.76 * h),
                VisualScale = 1.45f,
                Description = "东南半月巨湾通洋要塞，万石海舶，千帆浩荡。",
                Palette = CartographyColor.ColdBlue
            },
            new()
            {
                Id = 4,
                Name = "雁门雄关·铁壁要塞",
                Type = PlannedLandmarkType.IronFortressPass,
                Position = new PolyVec2(0.55 * w, 0.24 * h),
                VisualScale = 1.42f,
                Description = "锁钥北门，两山合抱，一夫当关万夫莫开。",
                Palette = CartographyColor.InkCharcoal
            },
            new()
            {
                Id = 5,
                Name = "太古青岚·苍灵神庙",
                Type = PlannedLandmarkType.SacredTempleTower,
                Position = new PolyVec2(0.76 * w, 0.42 * h),
                VisualScale = 1.38f,
                Description = "太古深林腹地，上古神殿遗迹，紫气东来。",
                Palette = CartographyColor.EmeraldGreen
            },
            new()
            {
                Id = 6,
                Name = "狂沙金墟·失落金墟",
                Type = PlannedLandmarkType.DesertRelicPyramid,
                Position = new PolyVec2(0.24 * w, 0.78 * h),
                VisualScale = 1.40f,
                Description = "大荒深处沉睡的失落古城阙巨型遗迹，金戈铁马，残垣断壁。",
                Palette = CartographyColor.SandyOchre
            }
        };
    }

    private static List<SettlementDefinition> ConvertFromStrategic(IReadOnlyList<StrategicSettlementBlueprint> settlements)
    {
        var list = new List<SettlementDefinition>();
        foreach (var s in settlements)
        {
            list.Add(new SettlementDefinition
            {
                Id = s.Id,
                Name = s.Name,
                Tier = s.Tier,
                Position = s.Position,
                AnchorType = s.AnchorType switch
                {
                    SettlementAnchorType.InlandCapital => GeographicAnchorType.HeartlandCapital,
                    SettlementAnchorType.RiverConfluence => GeographicAnchorType.RiverConfluence,
                    SettlementAnchorType.MountainPass => GeographicAnchorType.MountainPass,
                    SettlementAnchorType.NaturalBayHarbor => GeographicAnchorType.NaturalBayHarbor,
                    SettlementAnchorType.SacredSanctuary => GeographicAnchorType.SacredSanctuary,
                    _ => GeographicAnchorType.HeartlandCapital
                },
                Importance = s.Importance,
                ShowLabel = s.ShowLabel,
                Description = s.Description
            });
        }
        return list;
    }
}
