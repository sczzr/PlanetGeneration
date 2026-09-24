using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 平原微地貌与通透水墨细节画师 (PlainsPainter)。
/// 
/// 遵循古代山水舆图“平原留白、草纹舒展、孤丘点睛、清泽为镜、农田成网、沙垄顺风”的制图美学：
/// 1. 严格军规：平原区域坚决不生成树木（树木由 ForestMassGenerator/ForestTransitionPainter 专管）；
/// 2. 农田水网水浇地（FieldTerraced / ////）：都城京畿与大江两岸沃土生成整饬手绘田垄肌理；
/// 3. 散落独头小孤丘（SolitaryKnolls / /\）：极小尺寸的清秀孤山，绝不压迫平原开阔感；
/// 4. 清平水泽镜湖（MirrorLakes / ~~~）：内陆汇水清池，四周留白透气；
/// 5. 旷野通风草甸波纹（GrassWaveZones / ~~~~~~）：稀疏典雅，赋予宣纸呼吸感；
/// 6. 风向流动大漠（DesertFields / ~~~~~）：沿风场矢量延展的波浪起伏新月沙垄带与绿洲。
/// </summary>
public static class PlainsPainter
{
    public static List<BrushInstruction> Paint(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        IReadOnlyList<NarrativeRegion>? narrativeRegions,
        IReadOnlyList<MegaTerrainRegion>? megaRegions,
        IReadOnlyList<SettlementInfo> settlements,
        Dictionary<int, RegionStyle> regionStyles,
        IReadOnlyList<BrushInstruction>? mountainBrushes = null,
        MapBlueprint? blueprint = null)
    {
        var instructions = new List<BrushInstruction>();
        var placedKnolls = new List<PolyVec2>();
        var placedTussocks = new List<PolyVec2>();
        var placedWetlands = new List<PolyVec2>();
        var placedLakes = new List<PolyVec2>();
        var placedRocks = new List<PolyVec2>();
        var placedFields = new List<PolyVec2>();
        var placedDunes = new List<PolyVec2>();

        var rand = new Random((int)(options.Seed ^ 0x7733)); // "PLA"
        var seaLevel = options.SeaLevel;
        var count = geometry.Count;
        var w = geometry.Width;
        var h = geometry.Height;

        // 1. 提取实际已发射的山峰与山脊坐标
        var mountainPositions = new List<PolyVec2>();
        if (mountainBrushes != null)
        {
            foreach (var b in mountainBrushes)
            {
                if (b.Type is BrushType.MountainMain or BrushType.MountainSecondary or BrushType.MountainRidge or BrushType.SnowCap)
                {
                    mountainPositions.Add(b.Position);
                }
            }
        }

        // 2. 优先绘制结构化中央平原细节层 (PlainsDetailDefinition)
        if (blueprint?.PlainsDetail != null)
        {
            var pd = blueprint.PlainsDetail;

            // 2.1 农田水网水浇田肌理 (//// Agricultural Zones)
            foreach (var az in pd.AgriculturalZones)
            {
                var azCenter = ToWorld(az.Center, w, h);
                var rx = az.RadiusX * w;
                var ry = az.RadiusY * h;

                for (var k = 0; k < az.PatchCount; k++)
                {
                    var angle = (k / (float)az.PatchCount) * MathF.PI * 2f + (rand.NextSingle() - 0.5f) * 0.5f;
                    var dist = (0.25f + rand.NextSingle() * 0.65f);
                    var fPos = azCenter + new PolyVec2(MathF.Cos(angle) * rx * dist, MathF.Sin(angle) * ry * dist);

                    if (IsFarFromPlaced(fPos, placedFields, 20f))
                    {
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.FieldTerraced,
                            Position = fPos,
                            Scale = 1.05f + rand.NextSingle() * 0.25f,
                            Opacity = 0.95f,
                            YOrder = (float)fPos.Y,
                            Tint = CartographyColor.EmeraldGreen.Lerp(CartographyColor.RicePaper, 0.15f),
                            RegionId = 0,
                            VariantKey = "field_terraced"
                        });
                        placedFields.Add(fPos);
                    }
                }
            }

            // 2.2 散落独头小孤丘 (/\ Solitary Knolls)
            foreach (var knollPt in pd.SolitaryKnolls)
            {
                var kWorld = ToWorld(knollPt, w, h);
                if (IsFarFromPlaced(kWorld, placedKnolls, 30f))
                {
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.Hill,
                        Position = kWorld,
                        Scale = 0.65f + rand.NextSingle() * 0.20f,
                        Opacity = 0.88f,
                        YOrder = (float)kWorld.Y,
                        Tint = CartographyColor.EmeraldGreen.Lerp(CartographyColor.InkCharcoal, 0.20f),
                        RegionId = 0,
                        VariantKey = "solitary_plain_knoll"
                    });
                    placedKnolls.Add(kWorld);
                }
            }

            // 2.3 清平镜湖水泽 (~~~ Mirror Lakes)
            foreach (var lakePt in pd.MirrorLakes)
            {
                var lWorld = ToWorld(lakePt, w, h);
                if (IsFarFromPlaced(lWorld, placedLakes, 40f))
                {
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.LakePond,
                        Position = lWorld,
                        Scale = 1.05f + rand.NextSingle() * 0.20f,
                        Opacity = 0.88f,
                        YOrder = (float)lWorld.Y - 1f,
                        Tint = CartographyColor.ColdBlue,
                        RegionId = 0,
                        VariantKey = "mirror_lake"
                    });
                    placedLakes.Add(lWorld);

                    // 湖畔点缀微量湿地芦苇
                    var reedPos = lWorld + new PolyVec2(18.0, 4.0);
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.WetlandReeds,
                        Position = reedPos,
                        Scale = 0.70f,
                        Opacity = 0.82f,
                        YOrder = (float)reedPos.Y,
                        Tint = CartographyColor.ColdBlue.Lerp(CartographyColor.EmeraldGreen, 0.25f),
                        RegionId = 0,
                        VariantKey = "lake_reeds"
                    });
                }
            }

            // 2.4 通风草甸波纹走廊 (~~~~~~ Grass Wave Zones)
            foreach (var waveCenter in pd.GrassWaveZones)
            {
                var wcWorld = ToWorld(waveCenter, w, h);
                for (var i = 0; i < 4; i++)
                {
                    var offset = new PolyVec2((rand.NextSingle() - 0.5f) * 45f, (rand.NextSingle() - 0.5f) * 30f);
                    var rPos = wcWorld + offset;
                    if (IsFarFromPlaced(rPos, placedTussocks, 24f) && IsFarFromPlaced(rPos, placedKnolls, 16f))
                    {
                        var isWide = rand.NextSingle() < 0.40f;
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.GrassTussock,
                            Position = rPos,
                            Scale = isWide ? 0.72f : 0.58f,
                            Opacity = 0.65f,
                            YOrder = (float)rPos.Y,
                            Tint = CartographyColor.EmeraldGreen.Lerp(CartographyColor.InkCharcoal, 0.20f),
                            RegionId = 0,
                            VariantKey = isWide ? "meadow_ripple_wide" : "meadow_ripple_wave"
                        });
                        placedTussocks.Add(rPos);
                    }
                }
            }
        }

        // 3. 消费结构化大漠定义 (DesertFieldDefinition: 流动波状沙垄带)
        if (blueprint?.DesertFields != null && blueprint.DesertFields.Count > 0)
        {
            foreach (var df in blueprint.DesertFields)
            {
                var dCenter = ToWorld(df.Center, w, h);
                var rx = df.RadiusX * w;
                var ry = df.RadiusY * h;
                var windAngle = df.WindAngle;
                var windDir = new PolyVec2(Math.Cos(windAngle), Math.Sin(windAngle));
                var perpDir = new PolyVec2(-windDir.Y, windDir.X);

                for (var cIdx = -1; cIdx <= 1; cIdx++)
                {
                    var rowOrigin = dCenter + perpDir * (cIdx * ry * 0.42);
                    for (var s = -3; s <= 3; s++)
                    {
                        var dPos = rowOrigin + windDir * (s * (rx * 0.28)) + new PolyVec2((rand.NextDouble() - 0.5) * 8.0, (rand.NextDouble() - 0.5) * 6.0);
                        if (IsFarFromPlaced(dPos, placedDunes, 28f))
                        {
                            var duneScale = 0.88f + rand.NextSingle() * 0.24f;
                            instructions.Add(new BrushInstruction
                            {
                                Type = BrushType.DesertDune,
                                Position = dPos,
                                Scale = duneScale,
                                Rotation = windAngle + (rand.NextSingle() - 0.5f) * 0.12f,
                                Opacity = 0.85f,
                                YOrder = (float)dPos.Y,
                                Tint = CartographyColor.SandyOchre.Lerp(CartographyColor.WarmOchre, 0.30f),
                                RegionId = df.Id,
                                VariantKey = "crescent_dune_flow"
                            });
                            placedDunes.Add(dPos);
                        }
                    }
                }

                // 绿洲清泉
                foreach (var oasisPt in df.Oases)
                {
                    var oWorld = ToWorld(oasisPt, w, h);
                    if (IsFarFromPlaced(oWorld, placedLakes, 40f))
                    {
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.LakePond,
                            Position = oWorld,
                            Scale = 0.75f,
                            Opacity = 0.90f,
                            YOrder = (float)oWorld.Y - 1f,
                            Tint = CartographyColor.ColdBlue.Lerp(CartographyColor.EmeraldGreen, 0.35f),
                            RegionId = df.Id,
                            VariantKey = "desert_oasis"
                        });
                        placedLakes.Add(oWorld);
                    }
                }
            }
        }

        // 4. 通用平原网格微地貌保底（山麓低丘、沿河水汀与开阔草甸，确保无死白）
        for (var c = 0; c < count; c++)
        {
            var hCell = fields.Height[c];
            if (hCell <= seaLevel) continue;

            var landform = (LandformType)fields.Landform[c];
            var cPos = new PolyVec2(geometry.CentroidX[c], geometry.CentroidY[c]);
            var m = fields.Moisture[c];
            var biome = (BiomeType)fields.Biome[c];
            var hasRiver = fields.River[c] > 0 || fields.Flux[c] > 0.8f;

            var isDesert = biome is BiomeType.TropicalDesert or BiomeType.TemperateDesert;
            var isDry = isDesert || biome is BiomeType.Steppe or BiomeType.Savanna;
            var distToMt = GetMinDistance(cPos, mountainPositions);

            var palette = CartographyColor.EmeraldGreen;

            // 山麓缓坡圆丘 (Foothill Knolls)
            if (distToMt >= 22f && distToMt <= 70f)
            {
                var knollProb = landform == LandformType.Hill ? 0.45f : 0.25f;
                if (rand.NextSingle() < knollProb && IsFarFromPlaced(cPos, placedKnolls, 28f))
                {
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.Hill,
                        Position = cPos,
                        Scale = 0.62f + rand.NextSingle() * 0.16f,
                        Opacity = 0.85f,
                        YOrder = (float)cPos.Y,
                        Tint = palette,
                        RegionId = 0,
                        VariantKey = "foothill_knoll"
                    });
                    placedKnolls.Add(cPos);
                }
            }

            // 沿河低洼湿地芦苇 (Wetland Reeds)
            if (hasRiver && hCell < seaLevel + 0.16f && m > 0.40f && distToMt >= 20f && !isDesert)
            {
                if (rand.NextSingle() < 0.35f && IsFarFromPlaced(cPos, placedWetlands, 32f))
                {
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.WetlandReeds,
                        Position = cPos,
                        Scale = 0.75f + rand.NextSingle() * 0.18f,
                        Opacity = 0.85f,
                        YOrder = (float)cPos.Y,
                        Tint = palette.Lerp(CartographyColor.ColdBlue, 0.20f),
                        RegionId = 0,
                        VariantKey = "wetland_reeds"
                    });
                    placedWetlands.Add(cPos);
                }
            }

            // 旷野草原风草细纹 (Grassland Wind Ripples)
            if ((landform is LandformType.Plain or LandformType.Basin or LandformType.Coast) && distToMt >= 30f && !isDesert)
            {
                if (rand.NextSingle() < 0.25f && IsFarFromPlaced(cPos, placedTussocks, 32f) && IsFarFromPlaced(cPos, placedKnolls, 18f))
                {
                    var isWide = rand.NextSingle() < 0.35f;
                    var grassTint = isDry
                        ? CartographyColor.SandyOchre.Lerp(CartographyColor.InkCharcoal, 0.30f)
                        : palette.Lerp(CartographyColor.InkCharcoal, 0.22f);

                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.GrassTussock,
                        Position = cPos,
                        Scale = isWide ? 0.72f : 0.58f,
                        Opacity = 0.65f,
                        YOrder = (float)cPos.Y,
                        Tint = grassTint,
                        RegionId = 0,
                        VariantKey = isWide ? "meadow_ripple_wide" : "meadow_ripple_wave"
                    });
                    placedTussocks.Add(cPos);
                }
            }
        }

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

    private static float GetMinDistance(PolyVec2 pt, List<PolyVec2> points)
    {
        if (points.Count == 0) return float.MaxValue;
        var minDist = float.MaxValue;
        for (var i = 0; i < points.Count; i++)
        {
            var d = (float)pt.DistanceTo(points[i]);
            if (d < minDist) minDist = d;
        }
        return minDist;
    }

    private static bool IsFarFromPlaced(PolyVec2 pos, List<PolyVec2> placed, float minDist)
    {
        var minDistSq = (double)(minDist * minDist);
        for (var i = 0; i < placed.Count; i++)
        {
            if (pos.DistanceSquaredTo(placed[i]) < minDistSq) return false;
        }
        return true;
    }
}
