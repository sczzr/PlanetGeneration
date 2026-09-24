using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 东方山水画山脉画师 2.0 (MountainRangePainter 2.0)。
/// 
/// 遵循古代山水画与东方幻想舆图美学：
/// 1. 骨架重塑：山脉为“地理骨架”而非全屏填充物，整体密度削减 30%，留出开阔河谷与内陆留白；
/// 2. 金字塔三级体系：主峰 10%、中山连脊 30%、低山丘陵 60%，消除中心化压迫感；
/// 3. 空气透视与景深流岚：远山浅黛（透明度 0.35）、近山苍润（透明度 0.90）、山腰出岫、山麓云雾遮掩切痕；
/// 4. 彻底杜绝山体腹地大面积地毯式补峰，保持山系内部通风透气。
/// </summary>
public static class MountainRangePainter
{
    private sealed record SpineSamplePoint(
        PolyVec2 Position,
        float Elevation,
        float TangentAngle,
        float RidgeWidth,
        float DistanceAlongSpine,
        bool IsPass);

    private sealed class BranchSpur
    {
        public required List<PolyVec2> Positions { get; init; }
        public required List<float> Elevations { get; init; }
        public float DirectionAngle { get; init; }
    }

    public static List<BrushInstruction> Paint(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        IReadOnlyList<MegaTerrainRegion>? megaRegions,
        Dictionary<int, RegionStyle> regionStyles,
        MountainDensityProfile? profile = null,
        IReadOnlyList<Design.NarrativeRegion>? narrativeRegions = null)
    {
        profile ??= new MountainDensityProfile();
        var instructions = new List<BrushInstruction>();
        var placedMainSummits = new List<PolyVec2>();
        var placedSecondaryPeaks = new List<PolyVec2>();
        var placedRidges = new List<PolyVec2>();
        var placedFarPeaks = new List<PolyVec2>();
        var placedHills = new List<PolyVec2>();
        var placedMistCenters = new List<PolyVec2>();

        var rand = new Random((int)(options.Seed ^ 0x4d4f55)); // "MOU"
        var seaLevel = options.SeaLevel;

        // 1. 优先消费显式叙事大区（NarrativeRegion: MountainChain）
        var hasRenderedNarrativeMountain = false;
        if (narrativeRegions != null)
        {
            foreach (var nr in narrativeRegions)
            {
                if (nr.Type != Design.NarrativeRegionType.MountainRange || nr.MountainChain == null) continue;
                hasRenderedNarrativeMountain = true;
                PaintNarrativeMountainChain(
                    nr,
                    nr.MountainChain,
                    geometry,
                    fields,
                    options,
                    regionStyles,
                    profile,
                    rand,
                    seaLevel,
                    instructions,
                    placedMainSummits,
                    placedSecondaryPeaks,
                    placedRidges,
                    placedFarPeaks,
                    placedHills);
            }
        }

        if (hasRenderedNarrativeMountain) return instructions;
        if (megaRegions == null || megaRegions.Count == 0) return instructions;

        // 1. 大型连绵山系（MegaMountain）
        foreach (var m in megaRegions)
        {
            if (m.Type != MegaTerrainType.MegaMountain || m.Spine == null || m.Spine.Count == 0) continue;

            regionStyles.TryGetValue(m.Id, out var style);
            var density = Math.Clamp((style?.Density ?? 1.0f) * profile.GlobalDensityMultiplier, 0.4f, 1.8f);
            var exaggeration = style?.Exaggeration ?? 1.0f;
            var isFogEnabled = style?.Fog ?? true;
            var mistColor = style?.MistColor ?? CartographyColor.MistIvory;
            var palette = style?.Palette ?? CartographyColor.EmeraldGreen;

            var spine = m.Spine;
            var rankScale = m.Rank switch
            {
                MegaRegionRank.WorldLandmark => 1.25f,
                MegaRegionRank.MegaRegion => 1.10f,
                _ => 0.95f
            };
            rankScale *= exaggeration;

            // 对主脉脊线进行平滑样条采样（采样步长提升至 26f / density，消除过密拥堵）
            var stepDist = profile.SpineSampleDistance / density;
            var spineSamples = SampleSpline(spine, stepDist, seaLevel);
            if (spineSamples.Count == 0) continue;

            // 识别主脊上的少数主峰（严格限制在全脉 10% 数量级）
            var summits = DetectSummits(spineSamples, seaLevel, rankScale, profile);

            // 生发侧向支脉（Spurs）：数量适度，舒展山势
            var branches = GenerateBranchRidges(spineSamples, rand, density, seaLevel);

            // ── Layer 1: 远山浅黛剪影 (MountainFar) ──
            // 背向偏移，透明度 0.35~0.45，极淡墨色，营造东方山水空气感
            var farStep = (int)Math.Max(3, Math.Round(3.2 / density));
            for (var i = 0; i < spineSamples.Count; i += farStep)
            {
                var pt = spineSamples[i];
                if (pt.IsPass) continue;

                var farOffset = new PolyVec2(
                    (rand.NextSingle() - 0.5f) * 16f,
                    -20f - rand.NextSingle() * 16f);
                var farPos = pt.Position + farOffset;

                if (IsFarFromPlaced(farPos, placedFarPeaks, 34f / density))
                {
                    var farScale = (0.65f + (pt.Elevation - seaLevel) * 0.30f) * rankScale * (0.85f + rand.NextSingle() * 0.20f);
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.MountainFar,
                        Position = farPos,
                        Rotation = Math.Clamp(pt.TangentAngle * 0.12f, -0.10f, 0.10f),
                        Scale = farScale,
                        Opacity = 0.38f, // 远山极淡，空气感
                        YOrder = (float)farPos.Y - 24f,
                        Tint = palette.Lerp(CartographyColor.MistIvory, 0.45f),
                        RegionId = m.Id,
                        VariantKey = "mountain_far_silhouette"
                    });
                    placedFarPeaks.Add(farPos);
                }
            }

            // ── Layer 2: 巍峨主峰与伴峰锚定 (MountainMain & MountainSecondary - 先行锚定核心峰位) ──
            foreach (var summit in summits)
            {
                var sPos = summit.Position;
                var sElev = summit.Elevation;
                var isSnow = summit.IsSnow;
                var sScale = summit.Scale;

                if (IsFarFromPlaced(sPos, placedMainSummits, profile.DominantPeakMinDistance * sScale))
                {
                    // 1. 发射主峰
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.MountainMain,
                        Position = sPos,
                        Rotation = Math.Clamp(summit.TangentAngle * 0.18f, -0.12f, 0.12f),
                        Scale = sScale,
                        Opacity = 0.98f,
                        YOrder = (float)sPos.Y + 0.5f,
                        Tint = palette,
                        RegionId = m.Id,
                        VariantKey = isSnow ? "peak_snow" : "peak_main"
                    });
                    placedMainSummits.Add(sPos);
                    placedRidges.Add(sPos); // 关键：在连脊列表中登记占位，彻底杜绝同位重复覆盖！

                    if (isSnow)
                    {
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.SnowCap,
                            Position = sPos,
                            Rotation = Math.Clamp(summit.TangentAngle * 0.15f, -0.10f, 0.10f),
                            Scale = sScale * 1.02f,
                            Opacity = 0.95f,
                            YOrder = (float)sPos.Y + 0.8f,
                            Tint = CartographyColor.White,
                            RegionId = m.Id,
                            VariantKey = "snow_cap"
                        });
                    }

                    // 2. 单侧伴峰拱卫（自然错落，互不遮盖）
                    var tangentDir = new PolyVec2(Math.Cos(summit.TangentAngle), Math.Sin(summit.TangentAngle));
                    var compSide = (rand.NextSingle() > 0.5f ? 1.0 : -1.0);
                    var compDist = 20f * sScale;
                    var compPos = sPos + tangentDir * (compDist * compSide) + new PolyVec2(0d, 4d);

                    if (IsFarFromPlaced(compPos, placedSecondaryPeaks, profile.SecondaryPeakMinDistance))
                    {
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.MountainSecondary,
                            Position = compPos,
                            Rotation = Math.Clamp(summit.TangentAngle * 0.20f, -0.14f, 0.14f),
                            Scale = sScale * 0.85f,
                            Opacity = 0.92f,
                            YOrder = (float)compPos.Y + 0.3f,
                            Tint = palette,
                            RegionId = m.Id,
                            VariantKey = isSnow ? "companion_snow" : "companion_peak"
                        });
                        placedSecondaryPeaks.Add(compPos);
                        placedRidges.Add(compPos);
                    }
                }
            }

            // ── Layer 3: 中山连脊与单峰骨肉 (MountainRidge - 单峰程序化搭接，末梢过渡为缓丘) ──
            for (var i = 0; i < spineSamples.Count; i++)
            {
                var pt = spineSamples[i];
                if (pt.IsPass) continue; // 峡谷隘口留白不放山脊，形成自然山门

                var pos = pt.Position;
                var distRatio = (float)i / Math.Max(1, spineSamples.Count - 1);
                var isEnd = distRatio < 0.10f || distRatio > 0.90f;
                var isSnow = pt.Elevation > seaLevel + 0.48f || style?.Style == TerrainStyle.SnowMountain;
                var ridgeScale = (0.95f + Math.Clamp((pt.Elevation - seaLevel) * 0.45f, 0f, 0.30f)) * rankScale;

                // 互斥步长确保单峰以 35%~50% 宽度自然搭接
                if (IsFarFromPlaced(pos, placedRidges, profile.RidgeMinDistance * ridgeScale / density))
                {
                    // 脊线末梢逐渐过渡为圆缓丘 (Hill)，自然融入平原
                    if (isEnd && pt.Elevation < seaLevel + 0.28f)
                    {
                        var hillScale = ridgeScale * 0.80f;
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.Hill,
                            Position = pos,
                            Scale = hillScale,
                            Opacity = 0.85f,
                            YOrder = (float)pos.Y,
                            Tint = palette,
                            RegionId = m.Id,
                            VariantKey = "foothill_knoll"
                        });
                        placedRidges.Add(pos);
                        placedHills.Add(pos);
                    }
                    else
                    {
                        // 适度法向微景深抖动，前后山咬合错落
                        var normalDir = new PolyVec2(-Math.Sin(pt.TangentAngle), Math.Cos(pt.TangentAngle));
                        var jitter = (rand.NextSingle() - 0.5f) * 5.0f;
                        var ridgePos = pos + normalDir * jitter;

                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.MountainRidge,
                            Position = ridgePos,
                            Rotation = Math.Clamp(pt.TangentAngle * 0.22f, -0.16f, 0.16f),
                            Scale = ridgeScale,
                            Opacity = 0.95f,
                            YOrder = (float)ridgePos.Y,
                            Tint = palette,
                            RegionId = m.Id,
                            VariantKey = isSnow ? "snow_ridge" : "mountain_ridge"
                        });
                        placedRidges.Add(pos);
                    }
                }
            }

            // ── Layer 4: 支脉与低山缓丘 (Low Foothills & Knolls - 占 60%) ──
            foreach (var branch in branches)
            {
                for (var bIdx = 0; bIdx < branch.Positions.Count; bIdx++)
                {
                    var bPos = branch.Positions[bIdx];
                    var bElev = branch.Elevations[bIdx];
                    var isBranchTip = bIdx >= branch.Positions.Count - 2;

                    if (isBranchTip)
                    {
                        // 支脉末端平缓融入平原：生成山麓余脉缓丘 (Hill)
                        if (IsFarFromPlaced(bPos, placedHills, profile.HillMinDistance / density))
                        {
                            instructions.Add(new BrushInstruction
                            {
                                Type = BrushType.Hill,
                                Position = bPos,
                                Scale = 0.75f * rankScale * (0.85f + rand.NextSingle() * 0.25f),
                                Opacity = 0.82f,
                                YOrder = (float)bPos.Y,
                                Tint = palette,
                                RegionId = m.Id,
                                VariantKey = "foothill_knoll"
                            });
                            placedHills.Add(bPos);
                        }
                    }
                    else
                    {
                        // 支脉躯干：次级山脊
                        var bScale = (0.75f + Math.Clamp((bElev - seaLevel) * 0.35f, 0f, 0.25f)) * rankScale;
                        if (IsFarFromPlaced(bPos, placedRidges, (profile.RidgeMinDistance + 4f) * bScale))
                        {
                            instructions.Add(new BrushInstruction
                            {
                                Type = BrushType.MountainRidge,
                                Position = bPos,
                                Rotation = Math.Clamp(branch.DirectionAngle * 0.18f, -0.15f, 0.15f),
                                Scale = bScale,
                                Opacity = 0.90f,
                                YOrder = (float)bPos.Y,
                                Tint = palette,
                                RegionId = m.Id,
                                VariantKey = "branch_spur_ridge"
                            });
                            placedRidges.Add(bPos);
                        }
                    }
                }
            }

            // ── Layer 4b: 山体腹地微量点缀（消除老版全图地毯式补峰产生的窒息感） ──
            if (m.Cells != null && m.Cells.Length > 0)
            {
                foreach (var cell in m.Cells)
                {
                    var h = fields.Height[cell];
                    if (h <= seaLevel + 0.52f) continue; // 仅极高险峻腹地

                    var cellPos = new PolyVec2(geometry.CentroidX[cell], geometry.CentroidY[cell]);

                    // 严格大间距互斥，确保内部宽阔山谷留白
                    if (!IsFarFromPlaced(cellPos, placedMainSummits, 50f) ||
                        !IsFarFromPlaced(cellPos, placedSecondaryPeaks, 40f) ||
                        !IsFarFromPlaced(cellPos, placedRidges, 32f))
                    {
                        continue;
                    }

                    if (rand.NextSingle() > profile.InteriorInfillProbability) continue;

                    var cellSnow = fields.Biome[cell] == (byte)BiomeType.SnowyMountain || h > seaLevel + 0.60f;
                    var cellScale = (0.70f + (h - seaLevel) * 0.28f) * rankScale * (0.85f + rand.NextSingle() * 0.15f);

                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.MountainSecondary,
                        Position = cellPos,
                        Rotation = (rand.NextSingle() - 0.5f) * 0.12f,
                        Scale = cellScale,
                        Opacity = 0.90f,
                        YOrder = (float)cellPos.Y + 0.2f,
                        Tint = palette,
                        RegionId = m.Id,
                        VariantKey = cellSnow ? "massif_snow_peak" : "massif_peak"
                    });
                    placedSecondaryPeaks.Add(cellPos);
                }
            }

            // ── Layer 5: 烟岚流雾与山麓留白 (Fog) ──
            if (isFogEnabled)
            {
                // A. 主峰根部流岚（柔和遮挡山脚平底切痕）
                foreach (var sPos in placedMainSummits)
                {
                    var mistPos = sPos + new PolyVec2(0d, 11d);
                    if (IsFarFromPlaced(mistPos, placedMistCenters, 35f))
                    {
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.Fog,
                            Position = mistPos,
                            Rotation = 0f,
                            Scale = 1.30f * rankScale,
                            ScaleY = 0.42f,
                            Opacity = 0.40f,
                            YOrder = (float)mistPos.Y + 3f,
                            Tint = mistColor,
                            RegionId = m.Id,
                            VariantKey = "mist_mountain_foot"
                        });
                        placedMistCenters.Add(mistPos);
                    }
                }

                // B. 鞍部与峡谷隘口流云（穿山轻霭）
                foreach (var pt in spineSamples)
                {
                    if (!pt.IsPass) continue;
                    var pos = pt.Position;
                    if (IsFarFromPlaced(pos, placedMistCenters, 36f))
                    {
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.Fog,
                            Position = pos + new PolyVec2(0d, 4d),
                            Rotation = Math.Clamp(pt.TangentAngle * 0.20f, -0.12f, 0.12f),
                            Scale = 1.35f * rankScale,
                            ScaleY = 0.45f,
                            Opacity = 0.46f,
                            YOrder = (float)pos.Y + 2f,
                            Tint = mistColor,
                            RegionId = m.Id,
                            VariantKey = "mist_valley_pass"
                        });
                        placedMistCenters.Add(pos);
                    }
                }
            }
        }

        // 2. 巨型高原断崖（MegaPlateau）- 保持疏朗
        foreach (var p in megaRegions)
        {
            if (p.Type != MegaTerrainType.MegaPlateau) continue;
            regionStyles.TryGetValue(p.Id, out var style);
            var palette = style?.Palette ?? CartographyColor.SandyOchre;

            foreach (var cell in p.Cells)
            {
                if (rand.NextSingle() > 0.28f) continue; // 降低出现率
                var pos = new PolyVec2(geometry.CentroidX[cell], geometry.CentroidY[cell]);
                if (IsFarFromPlaced(pos, placedRidges, 38f))
                {
                    var depth = p.ComputeNormalizedDepth(pos);
                    var isEdge = depth < 0.35f;

                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.PlateauCliff,
                        Position = pos,
                        Scale = 0.90f + (float)rand.NextDouble() * 0.20f,
                        Opacity = 0.92f,
                        YOrder = (float)pos.Y,
                        Tint = palette,
                        RegionId = p.Id,
                        VariantKey = isEdge ? "plateau_cliff_edge" : "plateau_table_top"
                    });
                    placedRidges.Add(pos);
                }
            }
        }

        // 3. 巨型丘陵（MegaHills）- 翠峦漫坡
        foreach (var h in megaRegions)
        {
            if (h.Type != MegaTerrainType.MegaHills) continue;
            regionStyles.TryGetValue(h.Id, out var style);
            var palette = style?.Palette ?? CartographyColor.EmeraldGreen;

            foreach (var cell in h.Cells)
            {
                if (rand.NextSingle() > 0.30f) continue; // 降低密度
                var pos = new PolyVec2(geometry.CentroidX[cell], geometry.CentroidY[cell]);
                if (IsFarFromPlaced(pos, placedHills, 32f))
                {
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.Hill,
                        Position = pos,
                        Scale = 0.75f + (float)rand.NextDouble() * 0.20f,
                        Opacity = 0.85f,
                        YOrder = (float)pos.Y,
                        Tint = palette,
                        RegionId = h.Id,
                        VariantKey = "hills_soft"
                    });
                    placedHills.Add(pos);
                }
            }
        }

        // 4. 超高孤峰与罕见独立火山（极低概率点缀）
        for (var cell = 0; cell < fields.Count; cell++)
        {
            if (fields.Height[cell] <= seaLevel) continue;
            var lf = (LandformType)fields.Landform[cell];
            var isVolcano = lf == LandformType.Volcano;
            var isExtremePeak = lf == LandformType.Mountain && fields.Height[cell] > seaLevel + 0.65f;

            if (!isVolcano && !isExtremePeak) continue;

            var pos = new PolyVec2(geometry.CentroidX[cell], geometry.CentroidY[cell]);
            if (!IsFarFromPlaced(pos, placedMainSummits, 85f)) continue;
            if (!isVolcano && rand.NextSingle() > 0.10f) continue;

            var isSnow = fields.Height[cell] > seaLevel + 0.54f || fields.Biome[cell] == (byte)BiomeType.SnowyMountain;
            instructions.Add(new BrushInstruction
            {
                Type = BrushType.MountainMain,
                Position = pos,
                Scale = 1.05f + (float)rand.NextDouble() * 0.20f,
                Opacity = 0.95f,
                YOrder = (float)pos.Y,
                Tint = isVolcano ? CartographyColor.CinnabarRed : CartographyColor.EmeraldGreen,
                VariantKey = isVolcano ? "volcano_cone" : (isSnow ? "isolated_snow_peak" : "isolated_peak")
            });
            placedMainSummits.Add(pos);
        }

        return instructions;
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

    private static List<SpineSamplePoint> SampleSpline(
        IReadOnlyList<MountainSpineNode> spine,
        float stepDist,
        float seaLevel)
    {
        var samples = new List<SpineSamplePoint>();
        if (spine.Count < 2) return samples;

        var totalDist = 0d;
        var segDists = new double[spine.Count - 1];
        for (var i = 0; i < spine.Count - 1; i++)
        {
            segDists[i] = spine[i].Position.DistanceTo(spine[i + 1].Position);
            totalDist += segDists[i];
        }

        var sampleCount = Math.Max(2, (int)(totalDist / stepDist));
        for (var s = 0; s <= sampleCount; s++)
        {
            var targetDist = (s / (double)sampleCount) * totalDist;
            var accDist = 0d;
            var segIdx = 0;

            for (var i = 0; i < segDists.Length; i++)
            {
                if (accDist + segDists[i] >= targetDist || i == segDists.Length - 1)
                {
                    segIdx = i;
                    break;
                }
                accDist += segDists[i];
            }

            var segLen = Math.Max(segDists[segIdx], 1e-4);
            var localT = (float)((targetDist - accDist) / segLen);

            var p0 = spine[Math.Max(0, segIdx - 1)].Position;
            var p1 = spine[segIdx].Position;
            var p2 = spine[Math.Min(spine.Count - 1, segIdx + 1)].Position;
            var p3 = spine[Math.Min(spine.Count - 1, segIdx + 2)].Position;

            var pos = CatmullRom(p0, p1, p2, p3, localT);
            var elev = spine[segIdx].Elevation + (spine[Math.Min(spine.Count - 1, segIdx + 1)].Elevation - spine[segIdx].Elevation) * localT;
            var ridgeW = spine[segIdx].RidgeWidth + (spine[Math.Min(spine.Count - 1, segIdx + 1)].RidgeWidth - spine[segIdx].RidgeWidth) * localT;

            var tangent = CatmullRomTangent(p0, p1, p2, p3, localT);
            var tangentAngle = (float)Math.Atan2(tangent.Y, tangent.X);

            // 识别峡谷隘口/鞍部低谷
            var isPass = elev < seaLevel + 0.22f;
            if (!isPass && s > 2 && s < sampleCount - 2)
            {
                var cycleDist = (float)(targetDist % 200.0);
                if (cycleDist < stepDist * 1.5f)
                {
                    isPass = true;
                }
            }

            samples.Add(new SpineSamplePoint(pos, elev, tangentAngle, ridgeW, (float)targetDist, isPass));
        }

        return samples;
    }

    private static List<BranchSpur> GenerateBranchRidges(
        List<SpineSamplePoint> spineSamples,
        Random rand,
        float density,
        float seaLevel)
    {
        var branches = new List<BranchSpur>();
        if (spineSamples.Count < 5) return branches;

        var spurSpacing = (int)Math.Max(4, Math.Round(4.5 / density));
        var sideToggle = -1f;

        for (var i = spurSpacing; i < spineSamples.Count - spurSpacing; i += spurSpacing)
        {
            var pt = spineSamples[i];
            if (pt.IsPass) continue;

            sideToggle = -sideToggle;
            var normalAngle = pt.TangentAngle + (sideToggle > 0 ? MathF.PI * 0.5f : -MathF.PI * 0.5f);
            var branchAngle = normalAngle + (rand.NextSingle() - 0.5f) * 0.30f;

            var branchLen = Math.Clamp(pt.RidgeWidth * (0.80f + rand.NextSingle() * 0.35f), 20f, 55f);
            var steps = (int)Math.Clamp(branchLen / 18f, 2, 3);
            var stepLen = branchLen / steps;

            var bPositions = new List<PolyVec2>(steps);
            var bElevations = new List<float>(steps);

            var currPos = pt.Position;
            var currElev = pt.Elevation;

            for (var s = 1; s <= steps; s++)
            {
                var wanderAngle = branchAngle + (rand.NextSingle() - 0.5f) * 0.20f;
                var segDir = new PolyVec2(Math.Cos(wanderAngle), Math.Sin(wanderAngle));
                currPos += segDir * stepLen;

                var t = s / (float)steps;
                var elevDrop = (pt.Elevation - (seaLevel + 0.12f)) * MathF.Pow(t, 1.2f);
                var elev = MathF.Max(seaLevel + 0.10f, pt.Elevation - elevDrop);

                bPositions.Add(currPos);
                bElevations.Add(elev);
            }

            branches.Add(new BranchSpur
            {
                Positions = bPositions,
                Elevations = bElevations,
                DirectionAngle = branchAngle
            });
        }

        return branches;
    }

    private static List<MountainPeakNode> DetectSummits(
        List<SpineSamplePoint> spineSamples,
        float seaLevel,
        float rankScale,
        MountainDensityProfile profile)
    {
        var summits = new List<MountainPeakNode>();
        if (spineSamples.Count == 0) return summits;

        // 1. 寻找全山系第一主峰
        var highestElev = float.MinValue;
        var highestIdx = 0;
        for (var i = 0; i < spineSamples.Count; i++)
        {
            if (spineSamples[i].Elevation > highestElev)
            {
                highestElev = spineSamples[i].Elevation;
                highestIdx = i;
            }
        }

        var mainScale = (1.25f + Math.Clamp((highestElev - seaLevel) * 0.5f, 0f, 0.40f)) * rankScale;
        summits.Add(new MountainPeakNode(
            spineSamples[highestIdx].Position,
            highestElev,
            mainScale,
            IsDominant: true,
            IsSnow: highestElev > seaLevel + 0.46f,
            spineSamples[highestIdx].TangentAngle,
            "peak_main"));

        // 2. 沿脊线寻找极少数次级主峰（严格要求高间距，全脉不超过 2~3 座主峰）
        for (var i = 2; i < spineSamples.Count - 2; i++)
        {
            if (Math.Abs(i - highestIdx) < 5) continue;
            var pt = spineSamples[i];
            if (pt.IsPass) continue;

            var prev = spineSamples[i - 1];
            var next = spineSamples[i + 1];

            if (pt.Elevation > prev.Elevation && pt.Elevation > next.Elevation && pt.Elevation > seaLevel + 0.38f)
            {
                var tooClose = false;
                foreach (var existing in summits)
                {
                    if (pt.Position.DistanceTo(existing.Position) < profile.DominantPeakMinDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose && summits.Count < 3) // 严格限制整条山脉主峰数量
                {
                    var subScale = (1.08f + Math.Clamp((pt.Elevation - seaLevel) * 0.35f, 0f, 0.25f)) * rankScale;
                    summits.Add(new MountainPeakNode(
                        pt.Position,
                        pt.Elevation,
                        subScale,
                        IsDominant: false,
                        IsSnow: pt.Elevation > seaLevel + 0.46f,
                        pt.TangentAngle,
                        "peak_sub_main"));
                }
            }
        }

        return summits;
    }

    private static void PaintNarrativeMountainChain(
        Design.NarrativeRegion nr,
        Design.MountainChainData chain,
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        Dictionary<int, RegionStyle> regionStyles,
        MountainDensityProfile profile,
        Random rand,
        float seaLevel,
        List<BrushInstruction> instructions,
        List<PolyVec2> placedMainSummits,
        List<PolyVec2> placedSecondaryPeaks,
        List<PolyVec2> placedRidges,
        List<PolyVec2> placedFarPeaks,
        List<PolyVec2> placedHills)
    {
        regionStyles.TryGetValue(nr.Id, out var style);
        var density = Math.Clamp((style?.Density ?? 1.0f) * profile.GlobalDensityMultiplier, 0.4f, 1.8f);
        var exaggeration = style?.Exaggeration ?? 1.0f;
        var isFogEnabled = style?.Fog ?? true;
        var mistColor = style?.MistColor ?? CartographyColor.MistIvory;
        var palette = style?.Palette ?? nr.Palette;
        var rankScale = 1.15f * exaggeration;

        var spine = chain.SpineCurve;
        if (spine.Count < 2) return;

        // 1. 远山黛影 (MountainFar)
        var farStep = (int)Math.Max(3, Math.Round(3.2 / density));
        for (var i = 0; i < spine.Count; i += farStep)
        {
            var pt = spine[i];
            var farOffset = new PolyVec2((rand.NextSingle() - 0.5f) * 16f, -22f - rand.NextSingle() * 16f);
            var farPos = pt + farOffset;

            if (IsFarFromPlaced(farPos, placedFarPeaks, 34f / density))
            {
                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.MountainFar,
                    Position = farPos,
                    Scale = 0.85f * rankScale * (0.85f + rand.NextSingle() * 0.20f),
                    Opacity = 0.38f,
                    YOrder = (float)farPos.Y - 24f,
                    Tint = palette.Lerp(CartographyColor.MistIvory, 0.45f),
                    RegionId = nr.Id,
                    VariantKey = "mountain_far_silhouette"
                });
                placedFarPeaks.Add(farPos);
            }
        }

        // 2. 巍峨主峰与雪峰 (MountainMain & SnowCap & MountainSecondary - 先行锚定核心峰位)
        foreach (var peakRatio in chain.MajorPeakRatios)
        {
            var idx = Math.Clamp((int)(peakRatio * (spine.Count - 1)), 0, spine.Count - 1);
            var sPos = spine[idx];
            var sScale = 1.30f * rankScale;

            if (IsFarFromPlaced(sPos, placedMainSummits, profile.DominantPeakMinDistance * 0.7f))
            {
                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.MountainMain,
                    Position = sPos,
                    Scale = sScale,
                    Opacity = 0.98f,
                    YOrder = (float)sPos.Y + 0.5f,
                    Tint = palette,
                    RegionId = nr.Id,
                    VariantKey = "peak_main"
                });
                placedMainSummits.Add(sPos);
                placedRidges.Add(sPos); // 登记占位，避免主峰与连脊同位覆盖！

                // 雪峰
                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.SnowCap,
                    Position = sPos,
                    Scale = sScale * 1.02f,
                    Opacity = 0.95f,
                    YOrder = (float)sPos.Y + 0.8f,
                    Tint = CartographyColor.White,
                    RegionId = nr.Id,
                    VariantKey = "snow_cap"
                });

                // 单侧伴峰拱卫
                var compPos = sPos + new PolyVec2(20.0 * rankScale, 5.0);
                if (IsFarFromPlaced(compPos, placedSecondaryPeaks, profile.SecondaryPeakMinDistance))
                {
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.MountainSecondary,
                        Position = compPos,
                        Scale = sScale * 0.85f,
                        Opacity = 0.92f,
                        YOrder = (float)compPos.Y + 0.3f,
                        Tint = palette,
                        RegionId = nr.Id,
                        VariantKey = "companion_peak"
                    });
                    placedSecondaryPeaks.Add(compPos);
                    placedRidges.Add(compPos);
                }
            }
        }

        // 3. 主脊连续山体骨肉 (MountainRidge - 单峰程序化搭接)
        for (var i = 0; i < spine.Count; i++)
        {
            var t = i / (float)(spine.Count - 1);

            // 检查是否位于关隘隘口
            var isPass = false;
            foreach (var passRatio in chain.PassRatios)
            {
                if (MathF.Abs(t - passRatio) < 0.045f)
                {
                    isPass = true;
                    break;
                }
            }
            if (isPass) continue; // 关隘处留空不放高山脊，供古道穿越

            var pos = spine[i];
            var ridgeScale = 1.05f * rankScale;
            var isEnd = t < 0.10f || t > 0.90f;

            if (IsFarFromPlaced(pos, placedRidges, profile.RidgeMinDistance * ridgeScale / density))
            {
                var pPrev = i > 0 ? spine[i - 1] : spine[0];
                var pNext = i < spine.Count - 1 ? spine[i + 1] : spine[^1];
                var tangentAngle = MathF.Atan2((float)(pNext.Y - pPrev.Y), (float)(pNext.X - pPrev.X));

                if (isEnd)
                {
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.Hill,
                        Position = pos,
                        Scale = ridgeScale * 0.80f,
                        Opacity = 0.85f,
                        YOrder = (float)pos.Y,
                        Tint = palette,
                        RegionId = nr.Id,
                        VariantKey = "foothill_knoll"
                    });
                    placedRidges.Add(pos);
                    placedHills.Add(pos);
                }
                else
                {
                    var normalDir = new PolyVec2(-Math.Sin(tangentAngle), Math.Cos(tangentAngle));
                    var jitter = (rand.NextSingle() - 0.5f) * 5.0f;
                    var ridgePos = pos + normalDir * jitter;

                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.MountainRidge,
                        Position = ridgePos,
                        Rotation = Math.Clamp(tangentAngle * 0.22f, -0.16f, 0.16f),
                        Scale = ridgeScale,
                        Opacity = 0.95f,
                        YOrder = (float)ridgePos.Y,
                        Tint = palette,
                        RegionId = nr.Id,
                        VariantKey = "mountain_chain_ridge"
                    });
                    placedRidges.Add(pos);
                }
            }
        }

        // 4. 侧向支脉与山麓低丘 (Spurs: 中段次脊，末梢丘陵)
        foreach (var branch in chain.BranchSpurs)
        {
            for (var bIdx = 0; bIdx < branch.Count; bIdx++)
            {
                var bPos = branch[bIdx];
                var isBranchTip = bIdx >= branch.Count - 2;

                if (isBranchTip)
                {
                    if (IsFarFromPlaced(bPos, placedHills, profile.HillMinDistance / density))
                    {
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.Hill,
                            Position = bPos,
                            Scale = 0.70f * rankScale * (0.85f + rand.NextSingle() * 0.25f),
                            Opacity = 0.82f,
                            YOrder = (float)bPos.Y,
                            Tint = palette,
                            RegionId = nr.Id,
                            VariantKey = "foothill_knoll"
                        });
                        placedHills.Add(bPos);
                    }
                }
                else
                {
                    var bScale = 0.85f * rankScale;
                    if (IsFarFromPlaced(bPos, placedRidges, (profile.RidgeMinDistance + 2f) * bScale))
                    {
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.MountainRidge,
                            Position = bPos,
                            Scale = bScale,
                            Opacity = 0.90f,
                            YOrder = (float)bPos.Y,
                            Tint = palette,
                            RegionId = nr.Id,
                            VariantKey = "branch_spur_ridge"
                        });
                        placedRidges.Add(bPos);
                    }
                }
            }
        }

        // 5. 空气流岚 (Fog)
        if (isFogEnabled)
        {
            var mistCount = Math.Max(3, (int)(spine.Count * 0.12f * density));
            for (var i = 0; i < mistCount; i++)
            {
                var sIdx = rand.Next(spine.Count);
                var mPos = spine[sIdx] + new PolyVec2((rand.NextSingle() - 0.5f) * 20f, 15f + rand.NextSingle() * 15f);
                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.Fog,
                    Position = mPos,
                    Rotation = (rand.NextSingle() - 0.5f) * 0.12f,
                    Scale = 1.35f + rand.NextSingle() * 0.35f,
                    ScaleY = 0.38f,
                    Opacity = 0.28f,
                    YOrder = (float)mPos.Y + 3f,
                    Tint = mistColor,
                    RegionId = nr.Id,
                    VariantKey = "mist_mountain_foothill"
                });
            }
        }
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

    private static PolyVec2 CatmullRomTangent(PolyVec2 p0, PolyVec2 p1, PolyVec2 p2, PolyVec2 p3, float t)
    {
        var t2 = t * t;
        var f0 = -1.5 * t2 + 2.0 * t - 0.5;
        var f1 = 4.5 * t2 - 5.0 * t;
        var f2 = -4.5 * t2 + 4.0 * t + 0.5;
        var f3 = 1.5 * t2 - t;
        return p0 * f0 + p1 * f1 + p2 * f2 + p3 * f3;
    }
}

