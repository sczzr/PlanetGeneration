using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Planning;

/// <summary>规划叙事大区内的生态区域和子生态类型。</summary>
internal static class BiomeRegionPlanner
{
    internal static List<PlannedRegion> PlanRegions(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        MapBlueprint blueprint,
        IReadOnlyList<RegionData> narrativeRegions,
        float w,
        float h)
    {
        var result = new List<PlannedRegion>();

        var polarNarrative = narrativeRegions.FirstOrDefault(r => r.Type == NarrativeRegionType.PolarTundra);
        var mountainNarrative = narrativeRegions.FirstOrDefault(r => r.Type == NarrativeRegionType.MountainRange);
        var westForestNarrative = narrativeRegions.FirstOrDefault(r => r.Name.Contains("西境") || (r.Type == NarrativeRegionType.ForestMass && r.Center.X < 0.5 * w));
        var ancientForestNarrative = narrativeRegions.FirstOrDefault(r => r.Name.Contains("太古") || (r.Type == NarrativeRegionType.ForestMass && r.Center.X >= 0.5 * w));
        var plainsNarrative = narrativeRegions.FirstOrDefault(r => r.Type == NarrativeRegionType.PlainsBasin);
        var desertNarrative = narrativeRegions.FirstOrDefault(r => r.Type == NarrativeRegionType.DesertField);
        var wetlandsNarrative = narrativeRegions.FirstOrDefault(r => r.Type == NarrativeRegionType.SouthernWetlands || r.Name.Contains("云梦"));
        var coastalNarrative = narrativeRegions.FirstOrDefault(r => r.Type == NarrativeRegionType.CoastalBay);

        // 1. 北境冰原 (PolarTundra)
        var polarCenter = polarNarrative?.Center ?? new PolyVec2(0.50 * w, 0.13 * h);
        var polarRx = polarNarrative?.RadiusX ?? 0.38f * w;
        var polarRy = polarNarrative?.RadiusY ?? 0.09f * h;

        var polarSubBiomes = new List<PlannedSubBiome>
        {
            new() { Type = PlannedSubBiomeType.TundraPlain, Center = polarCenter, Radius = polarRx * 0.75f, Weight = 0.80f }
        };

        result.Add(new PlannedRegion
        {
            Id = polarNarrative?.Id ?? 1,
            Name = polarNarrative?.Name ?? "北境冰原",
            RegionType = PlannedRegionType.PolarTundra,
            Center = polarCenter,
            RadiusX = polarRx,
            RadiusY = polarRy,
            Palette = polarNarrative?.PrimaryColor ?? CartographyColor.MistIvory,
            SubBiomes = polarSubBiomes
        });

        // 刷实北境极寒苔原群系
        for (var c = 0; c < fields.Count; c++)
        {
            var px = (float)geometry.CentroidX[c];
            var py = (float)geometry.CentroidY[c];
            var dx = (px - (float)polarCenter.X) / polarRx;
            var dy = (py - (float)polarCenter.Y) / polarRy;
            if (dx * dx + dy * dy < 1.0f && py < 0.17f * h)
            {
                fields.Temperature[c] = MathF.Min(fields.Temperature[c], 0.15f);
                fields.Moisture[c] = MathF.Min(fields.Moisture[c], 0.35f);
                if (fields.Height[c] > options.SeaLevel && fields.Landform[c] != (byte)LandformType.Mountain)
                {
                    fields.Biome[c] = (byte)BiomeType.Tundra;
                }
            }
        }

        // 2. 苍冥北岳 (HighlandMountain)
        var mountainCenter = mountainNarrative?.Center ?? new PolyVec2(0.49 * w, 0.23 * h);
        var mountainRx = mountainNarrative?.RadiusX ?? 0.28f * w;
        var mountainRy = mountainNarrative?.RadiusY ?? 0.09f * h;

        var mountainSubBiomes = new List<PlannedSubBiome>
        {
            new() { Type = PlannedSubBiomeType.MountainGorge, Center = new PolyVec2(0.43 * w, 0.21 * h), Radius = 0.08f * w, Weight = 0.50f }
        };

        result.Add(new PlannedRegion
        {
            Id = mountainNarrative?.Id ?? 2,
            Name = mountainNarrative?.Name ?? "苍冥天脊",
            RegionType = PlannedRegionType.HighlandMountain,
            Center = mountainCenter,
            RadiusX = mountainRx,
            RadiusY = mountainRy,
            Palette = mountainNarrative?.PrimaryColor ?? CartographyColor.EmeraldGreen,
            SubBiomes = mountainSubBiomes
        });

        // 3. 西境云林 (WesternForest)
        var westCenter = westForestNarrative?.Center ?? new PolyVec2(0.24 * w, 0.46 * h);
        var westRx = westForestNarrative?.RadiusX ?? 0.16f * w;
        var westRy = westForestNarrative?.RadiusY ?? 0.15f * h;
        var westRot = westForestNarrative?.Rotation ?? 0.10f;

        var westClearings = new List<ForestClearing>
        {
            new() { Position = new PolyVec2(0.28 * w, 0.47 * h), Radius = 28.0f, Name = "翠微谷" }
        };

        var westSubBiomes = new List<PlannedSubBiome>
        {
            new() { Type = PlannedSubBiomeType.DenseCanopy, Center = westCenter, Radius = westRx * 0.55f, Weight = 0.50f },
            new() { Type = PlannedSubBiomeType.ForestHills, Center = new PolyVec2(westCenter.X - westRx * 0.35f, westCenter.Y - westRy * 0.25f), Radius = westRx * 0.35f, Weight = 0.25f },
            new() { Type = PlannedSubBiomeType.BreezePlains, Center = new PolyVec2(westCenter.X + westRx * 0.35f, westCenter.Y + westRy * 0.25f), Radius = westRx * 0.35f, Weight = 0.25f }
        };

        result.Add(new PlannedRegion
        {
            Id = westForestNarrative?.Id ?? 3,
            Name = westForestNarrative?.Name ?? "西境云林",
            RegionType = PlannedRegionType.WesternForest,
            Center = westCenter,
            RadiusX = westRx,
            RadiusY = westRy,
            Rotation = westRot,
            Palette = westForestNarrative?.PrimaryColor ?? CartographyColor.EmeraldGreen,
            Clearings = westClearings,
            SubBiomes = westSubBiomes
        });

        // 刷实西境云林植被群系到 fields
        for (var c = 0; c < fields.Count; c++)
        {
            var px = (float)geometry.CentroidX[c];
            var py = (float)geometry.CentroidY[c];
            var dx = px - (float)westCenter.X;
            var dy = py - (float)westCenter.Y;
            var cos = MathF.Cos(-westRot);
            var sin = MathF.Sin(-westRot);
            var lx = dx * cos - dy * sin;
            var ly = dx * sin + dy * cos;
            var distSq = (lx / westRx) * (lx / westRx) + (ly / westRy) * (ly / westRy);

            if (distSq < 1.0f)
            {
                fields.Moisture[c] = MathF.Max(fields.Moisture[c], 0.78f);
                if (fields.Height[c] > options.SeaLevel && fields.Landform[c] != (byte)LandformType.Mountain)
                {
                    fields.Biome[c] = (byte)BiomeType.TemperateRainForest;
                }
            }
        }

        // 4. 太古青岚林海 (AncientForest)
        var ancientCenter = ancientForestNarrative?.Center ?? new PolyVec2(0.76 * w, 0.42 * h);
        var ancientRx = ancientForestNarrative?.RadiusX ?? 0.16f * w;
        var ancientRy = ancientForestNarrative?.RadiusY ?? 0.15f * h;
        var ancientRot = ancientForestNarrative?.Rotation ?? -0.15f;

        var ancientClearings = new List<ForestClearing>();
        var massBp = blueprint?.ForestMasses?.FirstOrDefault();
        if (massBp != null && massBp.Clearings != null && massBp.Clearings.Count > 0)
        {
            foreach (var c in massBp.Clearings)
            {
                ancientClearings.Add(new ForestClearing
                {
                    Position = PlanningGeometry.ToWorld(c.Position, w, h),
                    Radius = c.Radius,
                    Name = c.Name
                });
            }
        }
        else
        {
            ancientClearings.Add(new ForestClearing { Position = new PolyVec2(0.76 * w, 0.42 * h), Radius = 34.0f, Name = "太古神殿空坪" });
            ancientClearings.Add(new ForestClearing { Position = new PolyVec2(0.72 * w, 0.36 * h), Radius = 26.0f, Name = "灵泉隙地" });
            ancientClearings.Add(new ForestClearing { Position = new PolyVec2(0.80 * w, 0.46 * h), Radius = 24.0f, Name = "幽栖古隙" });
        }

        var ancientSubBiomes = new List<PlannedSubBiome>
        {
            new() { Type = PlannedSubBiomeType.DenseCanopy, Center = ancientCenter, Radius = ancientRx * 0.55f, Weight = 0.50f },
            new() { Type = PlannedSubBiomeType.SacredSanctuary, Center = new PolyVec2(0.76 * w, 0.42 * h), Radius = ancientRx * 0.25f, Weight = 0.30f },
            new() { Type = PlannedSubBiomeType.ForestPond, Center = new PolyVec2(0.72 * w, 0.36 * h), Radius = ancientRx * 0.20f, Weight = 0.20f }
        };

        result.Add(new PlannedRegion
        {
            Id = ancientForestNarrative?.Id ?? 4,
            Name = ancientForestNarrative?.Name ?? "太古青岚",
            RegionType = PlannedRegionType.AncientForest,
            Center = ancientCenter,
            RadiusX = ancientRx,
            RadiusY = ancientRy,
            Rotation = ancientRot,
            Palette = ancientForestNarrative?.PrimaryColor ?? CartographyColor.DeepForest,
            Clearings = ancientClearings,
            SubBiomes = ancientSubBiomes
        });

        // 刷实东部古森林植被群系到 fields
        for (var c = 0; c < fields.Count; c++)
        {
            var px = (float)geometry.CentroidX[c];
            var py = (float)geometry.CentroidY[c];
            var dx = px - (float)ancientCenter.X;
            var dy = py - (float)ancientCenter.Y;
            var cos = MathF.Cos(-ancientRot);
            var sin = MathF.Sin(-ancientRot);
            var lx = dx * cos - dy * sin;
            var ly = dx * sin + dy * cos;
            var distSq = (lx / ancientRx) * (lx / ancientRx) + (ly / ancientRy) * (ly / ancientRy);

            if (distSq < 1.0f)
            {
                fields.Moisture[c] = MathF.Max(fields.Moisture[c], 0.82f);
                if (fields.Height[c] > options.SeaLevel && fields.Landform[c] != (byte)LandformType.Mountain)
                {
                    fields.Biome[c] = (byte)BiomeType.TemperateRainForest;
                }
            }
        }

        // 5. 中原天府平原 (CentralPlains)
        var plainsCenter = plainsNarrative?.Center ?? new PolyVec2(0.50 * w, 0.50 * h);
        var plainsRx = plainsNarrative?.RadiusX ?? 0.30f * w;
        var plainsRy = plainsNarrative?.RadiusY ?? 0.18f * h;

        var plainsSubBiomes = new List<PlannedSubBiome>
        {
            new() { Type = PlannedSubBiomeType.WateredFarmland, Center = new PolyVec2(0.50 * w, 0.48 * h), Radius = 0.14f * w, Weight = 0.40f },
            new() { Type = PlannedSubBiomeType.BreezePlains, Center = new PolyVec2(0.44 * w, 0.52 * h), Radius = 0.18f * w, Weight = 0.25f },
            new() { Type = PlannedSubBiomeType.SolitaryKnolls, Center = new PolyVec2(0.54 * w, 0.42 * h), Radius = 0.12f * w, Weight = 0.15f },
            new() { Type = PlannedSubBiomeType.LakeWetland, Center = new PolyVec2(0.56 * w, 0.55 * h), Radius = 0.10f * w, Weight = 0.10f },
            new() { Type = PlannedSubBiomeType.MarketHamlet, Center = new PolyVec2(0.58 * w, 0.52 * h), Radius = 0.08f * w, Weight = 0.10f }
        };

        result.Add(new PlannedRegion
        {
            Id = plainsNarrative?.Id ?? 5,
            Name = plainsNarrative?.Name ?? "中原天府",
            RegionType = PlannedRegionType.CentralPlains,
            Center = plainsCenter,
            RadiusX = plainsRx,
            RadiusY = plainsRy,
            Palette = plainsNarrative?.PrimaryColor ?? CartographyColor.EmeraldGreen,
            SubBiomes = plainsSubBiomes
        });

        // 润泽平原物理网格
        for (var c = 0; c < fields.Count; c++)
        {
            var px = (float)geometry.CentroidX[c];
            var py = (float)geometry.CentroidY[c];
            var dx = (px - (float)plainsCenter.X) / plainsRx;
            var dy = (py - (float)plainsCenter.Y) / plainsRy;
            if (dx * dx + dy * dy < 1.0f && fields.Height[c] > options.SeaLevel && fields.Landform[c] != (byte)LandformType.Mountain)
            {
                fields.Moisture[c] = MathF.Max(fields.Moisture[c], 0.60f);
                if (fields.Biome[c] != (byte)BiomeType.TemperateRainForest && fields.Biome[c] != (byte)BiomeType.TropicalDesert)
                {
                    fields.Biome[c] = (byte)BiomeType.Grassland;
                }
            }
        }

        // 6. 狂沙金墟 (AridDesert)
        var desertCenter = desertNarrative?.Center ?? new PolyVec2(0.28 * w, 0.74 * h);
        var desertRx = desertNarrative?.RadiusX ?? 0.20f * w;
        var desertRy = desertNarrative?.RadiusY ?? 0.16f * h;
        var desertRot = desertNarrative?.Rotation ?? 0.15f;
        var dfBp = blueprint?.DesertFields?.FirstOrDefault();
        var desertOases = dfBp?.Oases != null && dfBp.Oases.Count > 0
            ? dfBp.Oases.Select(p => PlanningGeometry.ToWorld(p, w, h)).ToList()
            : new List<PolyVec2>
            {
                new(0.32 * w, 0.74 * h),
                new(0.25 * w, 0.68 * h),
                new(0.36 * w, 0.64 * h)
            };

        var desertSubBiomes = new List<PlannedSubBiome>
        {
            new() { Type = PlannedSubBiomeType.DesertDuneLane, Center = desertCenter, Radius = desertRx * 0.65f, Weight = 0.45f },
            new() { Type = PlannedSubBiomeType.DesertMargin, Center = new PolyVec2(desertCenter.X + desertRx * 0.35f, desertCenter.Y - desertRy * 0.30f), Radius = desertRx * 0.35f, Weight = 0.25f },
            new() { Type = PlannedSubBiomeType.DesertOasis, Center = desertOases[0], Radius = desertRx * 0.16f, Weight = 0.15f },
            new() { Type = PlannedSubBiomeType.RockyCliff, Center = new PolyVec2(desertCenter.X - desertRx * 0.35f, desertCenter.Y - desertRy * 0.20f), Radius = desertRx * 0.25f, Weight = 0.15f }
        };

        result.Add(new PlannedRegion
        {
            Id = desertNarrative?.Id ?? 6,
            Name = desertNarrative?.Name ?? "狂沙金墟",
            RegionType = PlannedRegionType.AridDesert,
            Center = desertCenter,
            RadiusX = desertRx,
            RadiusY = desertRy,
            Rotation = desertRot,
            WindAngle = 0.35f,
            Palette = desertNarrative?.PrimaryColor ?? CartographyColor.SandyOchre,
            Oases = desertOases,
            SubBiomes = desertSubBiomes
        });

        // 刷实沙漠干旱群系（彻底驱散南部空白）
        for (var c = 0; c < fields.Count; c++)
        {
            var px = (float)geometry.CentroidX[c];
            var py = (float)geometry.CentroidY[c];
            var dx = (px - (float)desertCenter.X) / desertRx;
            var dy = (py - (float)desertCenter.Y) / desertRy;
            if (dx * dx + dy * dy < 1.0f)
            {
                fields.Moisture[c] = MathF.Min(fields.Moisture[c], 0.10f);
                fields.Temperature[c] = MathF.Max(fields.Temperature[c], 0.80f);
                if (fields.Height[c] > options.SeaLevel && fields.Landform[c] != (byte)LandformType.Mountain)
                {
                    fields.Biome[c] = (byte)BiomeType.TropicalDesert;
                }
            }
        }

        // 7. 南部云梦湿地 (SouthernWetlands)
        var wetlandsCenter = wetlandsNarrative?.Center ?? new PolyVec2(0.56 * w, 0.70 * h);
        var wetlandsRx = wetlandsNarrative?.RadiusX ?? 0.18f * w;
        var wetlandsRy = wetlandsNarrative?.RadiusY ?? 0.13f * h;
        var wetlandsRot = wetlandsNarrative?.Rotation ?? -0.08f;

        var wetlandsSubBiomes = new List<PlannedSubBiome>
        {
            new() { Type = PlannedSubBiomeType.LakeWetland, Center = new PolyVec2(0.58 * w, 0.68 * h), Radius = 0.10f * w, Weight = 0.45f },
            new() { Type = PlannedSubBiomeType.WetlandMarshes, Center = wetlandsCenter, Radius = wetlandsRx * 0.70f, Weight = 0.35f },
            new() { Type = PlannedSubBiomeType.MarketHamlet, Center = new PolyVec2(0.54 * w, 0.64 * h), Radius = 0.08f * w, Weight = 0.20f }
        };

        result.Add(new PlannedRegion
        {
            Id = wetlandsNarrative?.Id ?? 7,
            Name = wetlandsNarrative?.Name ?? "南部云梦",
            RegionType = PlannedRegionType.SouthernWetlands,
            Center = wetlandsCenter,
            RadiusX = wetlandsRx,
            RadiusY = wetlandsRy,
            Rotation = wetlandsRot,
            Palette = wetlandsNarrative?.PrimaryColor ?? CartographyColor.ColdBlue,
            SubBiomes = wetlandsSubBiomes
        });

        // 润泽南部湿地物理网格
        for (var c = 0; c < fields.Count; c++)
        {
            var px = (float)geometry.CentroidX[c];
            var py = (float)geometry.CentroidY[c];
            var dx = (px - (float)wetlandsCenter.X) / wetlandsRx;
            var dy = (py - (float)wetlandsCenter.Y) / wetlandsRy;
            if (dx * dx + dy * dy < 1.0f && fields.Height[c] > options.SeaLevel && fields.Landform[c] != (byte)LandformType.Mountain)
            {
                fields.Moisture[c] = MathF.Max(fields.Moisture[c], 0.88f);
            }
        }

        // 8. 东南沧溟水乡海湾 (CoastalBay)
        var coastalCenter = coastalNarrative?.Center ?? new PolyVec2(0.78 * w, 0.76 * h);
        var coastalRx = coastalNarrative?.RadiusX ?? 0.18f * w;
        var coastalRy = coastalNarrative?.RadiusY ?? 0.15f * h;
        var coastalRot = coastalNarrative?.Rotation ?? -0.10f;

        var coastalSubBiomes = new List<PlannedSubBiome>
        {
            new() { Type = PlannedSubBiomeType.LakeWetland, Center = coastalCenter, Radius = 0.12f * w, Weight = 0.40f },
            new() { Type = PlannedSubBiomeType.WateredFarmland, Center = new PolyVec2(0.72 * w, 0.70 * h), Radius = 0.10f * w, Weight = 0.35f },
            new() { Type = PlannedSubBiomeType.MarketHamlet, Center = new PolyVec2(0.78 * w, 0.76 * h), Radius = 0.08f * w, Weight = 0.25f }
        };

        result.Add(new PlannedRegion
        {
            Id = coastalNarrative?.Id ?? 8,
            Name = coastalNarrative?.Name ?? "东南沧溟",
            RegionType = PlannedRegionType.CoastalBay,
            Center = coastalCenter,
            RadiusX = coastalRx,
            RadiusY = coastalRy,
            Rotation = coastalRot,
            Palette = coastalNarrative?.PrimaryColor ?? CartographyColor.ColdBlue,
            SubBiomes = coastalSubBiomes
        });

        // 润泽东南沧溟物理网格
        for (var c = 0; c < fields.Count; c++)
        {
            var px = (float)geometry.CentroidX[c];
            var py = (float)geometry.CentroidY[c];
            var dx = (px - (float)coastalCenter.X) / coastalRx;
            var dy = (py - (float)coastalCenter.Y) / coastalRy;
            if (dx * dx + dy * dy < 1.0f && fields.Height[c] > options.SeaLevel && fields.Landform[c] != (byte)LandformType.Mountain)
            {
                fields.Moisture[c] = MathF.Max(fields.Moisture[c], 0.75f);
            }
        }

        return result;
    }
}
