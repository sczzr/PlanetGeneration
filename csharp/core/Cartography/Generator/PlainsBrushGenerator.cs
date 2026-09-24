using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 平原微地貌特征分类。
/// </summary>
public enum PlainsFeatureType
{
    FoothillKnoll, // 山麓缓坡圆丘
    IsolatedKnoll, // 平原独头小孤丘 (/\)
    MirrorLake,    // 汇水镜湖水泽 (~~~)
    WetlandReeds,  // 湿地浅渚芦苇
    GrassWave,     // 草原起伏风草波纹 (~~~~~~)
    RockOutcrop,   // 荒野石阜与残岩 (o o)
    WildShrub,     // 旷野灌木小品
    DesertDune     // 瀚海流线新月沙丘
}

/// <summary>
/// 平原微地貌与景观细节画师（PlainsBrushGenerator）。
/// 
/// 遵循古代山水舆图“平原留白、草纹舒展、孤丘点睛、清泽为镜、瀚海连垄”的制图美学：
/// 1. 严格军规：平原区域坚决不再生成树木（树木已由 ForestTransitionPainter 专管）；
/// 2. 平原草纹（~~~~~）：稀疏优雅的风草波纹（GrassTussock），打破单调空白，赋予宣纸呼吸感；
/// 3. 散落孤丘（/\）：极小尺寸的平原独头微丘（Hill，Scale 0.45~0.60），遥望山系；
/// 4. 镜湖清渚（LakePond）：内陆汇水洼地与河套清池，四周舒朗留白；
/// 5. 湿地浅渚与芦苇（WetlandReeds）：沿低洼水滨微量点缀；
/// 6. 荒野石阜（RockOutcrop）：干旱或低丘台地边缘的小巧岩石露头；
/// 7. 荒野灌木（WildShrub）：干旱半干旱草原上的丛生点缀；
/// 8. 荒漠沙垄（DesertDune）：大漠腹地 15~25 条优雅起伏的新月风纹沙丘，告别单一死黄平涂。
/// </summary>
public static class PlainsBrushGenerator
{
    public static List<BrushInstruction> GenerateBrushes(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        IReadOnlyList<MegaTerrainRegion> megaRegions,
        IReadOnlyList<SettlementInfo> settlements,
        Dictionary<int, RegionStyle> regionStyles,
        IReadOnlyList<BrushInstruction>? mountainBrushes = null)
    {
        var instructions = new List<BrushInstruction>();
        var placedKnolls = new List<PolyVec2>();
        var placedTussocks = new List<PolyVec2>();
        var placedWetlands = new List<PolyVec2>();
        var placedLakes = new List<PolyVec2>();
        var placedRocks = new List<PolyVec2>();
        var placedShrubs = new List<PolyVec2>();
        var placedDunes = new List<PolyVec2>();

        var rand = new Random((int)(options.Seed ^ 0x7733)); // "PLA"
        var seaLevel = options.SeaLevel;
        var count = geometry.Count;

        // 1. 提取实际已发射的山峰与山脊坐标，计算与山体骨架的距离
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

        // 2. 遍历全图大陆多边形单元，进行分层平原景观微地貌填充
        for (var c = 0; c < count; c++)
        {
            var h = fields.Height[c];
            if (h <= seaLevel) continue; // 跳过海洋

            var landform = (LandformType)fields.Landform[c];
            var cPos = new PolyVec2(geometry.CentroidX[c], geometry.CentroidY[c]);
            var m = fields.Moisture[c];
            var biome = (BiomeType)fields.Biome[c];
            var hasRiver = fields.River[c] > 0 || fields.Flux[c] > 0.8f;

            // 区域配色基调解析
            var palette = CartographyColor.EmeraldGreen;
            var regionId = 0;
            if (regionStyles != null && regionStyles.TryGetValue(0, out var defaultStyle))
            {
                palette = defaultStyle.Palette;
            }

            var isDesert = biome is BiomeType.TropicalDesert or BiomeType.TemperateDesert;
            var isDry = isDesert || biome is BiomeType.Steppe or BiomeType.Savanna;
            var distToMt = GetMinDistance(cPos, mountainPositions);

            // ── 生态分区 A: 山麓缓坡连绵微丘 (Foothill Knolls - 紧贴山脉边缘递减) ──
            if (distToMt >= 22f && distToMt <= 75f)
            {
                var knollSpawnProb = landform == LandformType.Hill ? 0.50f : 0.30f;
                if (rand.NextSingle() < knollSpawnProb && IsFarFromPlaced(cPos, placedKnolls, 26f))
                {
                    var isTerrace = m > 0.32f && h < seaLevel + 0.32f && rand.NextSingle() < 0.40f;
                    var hillScale = 0.65f + (float)rand.NextDouble() * 0.18f;

                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.Hill,
                        Position = cPos,
                        Scale = hillScale,
                        Opacity = 0.88f,
                        YOrder = (float)cPos.Y,
                        Tint = isTerrace ? palette.Lerp(CartographyColor.SandyOchre, 0.25f) : palette,
                        RegionId = regionId,
                        VariantKey = isTerrace ? "foothill_terrace" : "foothill_knoll"
                    });
                    placedKnolls.Add(cPos);
                }
            }

            // ── 生态分区 B: 广袤中央平原散落孤丘 (/\ Isolated Knolls) ──
            // 距离山体很远的大平原，稀疏点缀极其小巧清秀的独头小山包，打破单调
            if (distToMt > 80f && (landform is LandformType.Plain or LandformType.Basin) && !isDesert)
            {
                if (rand.NextSingle() < 0.10f && IsFarFromPlaced(cPos, placedKnolls, 65f))
                {
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.Hill,
                        Position = cPos,
                        Scale = 0.48f + (float)rand.NextDouble() * 0.14f, // 极小尺寸，不压平原
                        Opacity = 0.78f,
                        YOrder = (float)cPos.Y,
                        Tint = palette.Lerp(CartographyColor.InkCharcoal, 0.20f),
                        RegionId = regionId,
                        VariantKey = "isolated_plain_knoll"
                    });
                    placedKnolls.Add(cPos);
                }
            }

            // ── 生态分区 C: 内陆低洼汇水镜湖 (Lake / Pond) ──
            var isSink = fields.Downslope[c] == c || (hasRiver && m > 0.48f && h < seaLevel + 0.18f);
            if (isSink && distToMt >= 25f && !isDesert)
            {
                if (rand.NextSingle() < 0.22f && IsFarFromPlaced(cPos, placedLakes, 60f))
                {
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.LakePond,
                        Position = cPos,
                        Scale = 0.85f + (float)rand.NextDouble() * 0.20f,
                        Opacity = 0.82f,
                        YOrder = (float)cPos.Y - 1f,
                        Tint = CartographyColor.ColdBlue,
                        RegionId = regionId,
                        VariantKey = "inland_pond"
                    });
                    placedLakes.Add(cPos);
                }
            }

            // ── 生态分区 D: 沿河低洼湿地芦荡 (Wetland Reeds) ──
            if (hasRiver && h < seaLevel + 0.16f && m > 0.40f && distToMt >= 20f && !isDesert)
            {
                if (rand.NextSingle() < 0.35f && IsFarFromPlaced(cPos, placedWetlands, 32f))
                {
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.WetlandReeds,
                        Position = cPos,
                        Scale = 0.75f + (float)rand.NextDouble() * 0.18f,
                        Opacity = 0.85f,
                        YOrder = (float)cPos.Y,
                        Tint = palette.Lerp(CartographyColor.ColdBlue, 0.20f),
                        RegionId = regionId,
                        VariantKey = "wetland_reeds"
                    });
                    placedWetlands.Add(cPos);
                }
            }

            // ── 生态分区 E: 旷野草原风草细纹 (~~~~~ Grassland Wind Ripples) ──
            // 稀疏优雅，半透明微墨色，点缀中央平原留白，杜绝密集杂乱
            if ((landform is LandformType.Plain or LandformType.Basin or LandformType.Coast) && distToMt >= 25f && !isDesert)
            {
                // 控制生成率：平原每隔较大间距才点缀一组风草波纹
                if (rand.NextSingle() < 0.30f && IsFarFromPlaced(cPos, placedTussocks, 30f) && IsFarFromPlaced(cPos, placedKnolls, 16f))
                {
                    var isWide = rand.NextSingle() < 0.35f;
                    var grassTint = isDry
                        ? CartographyColor.SandyOchre.Lerp(CartographyColor.InkCharcoal, 0.30f)
                        : palette.Lerp(CartographyColor.InkCharcoal, 0.22f);

                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.GrassTussock,
                        Position = cPos,
                        Scale = isWide ? 0.75f : 0.60f,
                        Opacity = 0.65f, // 通透浅淡，不与主体争抢
                        YOrder = (float)cPos.Y,
                        Tint = grassTint,
                        RegionId = regionId,
                        VariantKey = isWide ? "meadow_ripple_wide" : "meadow_ripple_wave"
                    });
                    placedTussocks.Add(cPos);
                }
            }

            // ── 生态分区 F: 荒野石阜与残岩露头 (Rock Outcrops) ──
            // 在较干燥或低丘边缘，点缀极少数荒原巨石/台地石阜
            if ((isDry || landform is LandformType.Plateau or LandformType.Hill) && distToMt >= 20f && !isDesert)
            {
                if (rand.NextSingle() < 0.08f && IsFarFromPlaced(cPos, placedRocks, 48f) && IsFarFromPlaced(cPos, placedKnolls, 20f))
                {
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.PlateauCliff,
                        Position = cPos,
                        Scale = 0.45f + (float)rand.NextDouble() * 0.12f,
                        Opacity = 0.82f,
                        YOrder = (float)cPos.Y,
                        Tint = CartographyColor.SandyOchre.Lerp(CartographyColor.InkCharcoal, 0.35f),
                        RegionId = regionId,
                        VariantKey = "rock_outcrop"
                    });
                    placedRocks.Add(cPos);
                }
            }

            // ── 生态分区 G: 旷野丛生灌木微品 (Wild Shrubs) ──
            // 在干旱半干旱平原/稀树草甸，生成小尺寸灌木丛生点缀
            if (isDry && !isDesert && distToMt >= 18f)
            {
                if (rand.NextSingle() < 0.15f && IsFarFromPlaced(cPos, placedShrubs, 34f))
                {
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.GrassTussock,
                        Position = cPos,
                        Scale = 0.52f + (float)rand.NextDouble() * 0.14f,
                        Opacity = 0.75f,
                        YOrder = (float)cPos.Y,
                        Tint = CartographyColor.SandyOchre.Lerp(CartographyColor.EmeraldGreen, 0.30f),
                        RegionId = regionId,
                        VariantKey = "shrub"
                    });
                    placedShrubs.Add(cPos);
                }
            }

            // ── 生态分区 H: 大漠新月流线沙丘 (Desert Dunes) ──
            // 在荒漠区域生成 15~25 条优雅起伏的流动风纹新月沙丘
            if (isDesert && distToMt >= 15f)
            {
                if (rand.NextSingle() < 0.32f && IsFarFromPlaced(cPos, placedDunes, 32f))
                {
                    var duneScale = 0.82f + (float)rand.NextDouble() * 0.28f;
                    var duneRot = 0.25f + (rand.NextSingle() - 0.5f) * 0.20f; // 贴合微风偏角
                    var duneTint = CartographyColor.SandyOchre.Lerp(CartographyColor.WarmOchre, 0.30f);

                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.DesertDune,
                        Position = cPos,
                        Scale = duneScale,
                        Rotation = duneRot,
                        Opacity = 0.82f,
                        YOrder = (float)cPos.Y,
                        Tint = duneTint,
                        RegionId = regionId,
                        VariantKey = "crescent_dune"
                    });
                    placedDunes.Add(cPos);
                }
            }
        }

        // 3. 针对大型荒漠（MegaDesert）腹地进行沙丘垄补充，确保大漠沙丘形成气势连绵流线
        if (megaRegions != null)
        {
            foreach (var r in megaRegions)
            {
                if (r.Type != MegaTerrainType.MegaDesert || r.Cells.Length < 3) continue;

                var dAngle = r.MainDirectionAngle + 0.35f;
                for (var idx = 0; idx < r.Cells.Length; idx += Math.Max(1, r.Cells.Length / 18))
                {
                    var cell = r.Cells[idx];
                    var cPos = new PolyVec2(geometry.CentroidX[cell], geometry.CentroidY[cell]);
                    var depth = r.ComputeNormalizedDepth(cPos);

                    // 仅在大漠腹地补充沙垄
                    if (depth >= 0.20f && IsFarFromPlaced(cPos, placedDunes, 28f))
                    {
                        var duneScale = 0.88f + (float)rand.NextDouble() * 0.25f;
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.DesertDune,
                            Position = cPos,
                            Scale = duneScale,
                            Rotation = dAngle + (rand.NextSingle() - 0.5f) * 0.15f,
                            Opacity = 0.85f,
                            YOrder = (float)cPos.Y,
                            Tint = CartographyColor.SandyOchre.Lerp(CartographyColor.WarmOchre, 0.35f),
                            RegionId = r.Id,
                            VariantKey = "barchan_dune"
                        });
                        placedDunes.Add(cPos);
                    }
                }
            }
        }

        return instructions;
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
