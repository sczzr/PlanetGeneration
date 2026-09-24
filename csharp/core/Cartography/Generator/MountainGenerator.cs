using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Cartography.Planning;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Generator;

/// <summary>
/// 规划式山系画师 V2.0（MountainGenerator）。
/// 
/// 彻底解决“横贯式连续山墙”阻断视觉的问题，严格贯彻：
/// 1. 【山脉生态结构拆解】：
///    - 主山脉（50%）：呈起伏主脊段落，步长放宽至 36~42px，自然咬合呼吸，不挤压成死板铁壁；
///    - 支脉（35%）：沿斜出走向舒展，由高向低过渡融入平原；
///    - 孤峰（15%）：山脚、山口两侧及平原边缘点缀独立单峰与奇峰（如武当独秀峰、北麓奇石）；
/// 2. 【雪峰绝顶与峡谷源流】：
///    - 仅在主峰至高处叠加 SnowCap 极顶；
///    - 峡谷源流处（谷口）与关隘（山口）彻底留白，让大江源流与官道自然穿出；
/// 3. 【立体景深（远山、主峰、支脉、低丘、山麓流岚）】。
/// </summary>
public static class MountainGenerator
{
    public static List<BrushInstruction> Generate(
        IReadOnlyList<PlannedMountainChain> chains,
        GenerationOptions options,
        Dictionary<int, RegionStyle>? regionStyles = null)
    {
        var instructions = new List<BrushInstruction>();
        if (chains == null || chains.Count == 0) return instructions;

        var rand = new Random((int)(options.Seed ^ 0x4d4f55)); // "MOU"

        foreach (var chain in chains)
        {
            var spline = chain.SpineCurve;
            if (spline == null || spline.Count < 2) continue;

            RegionStyle? style = null;
            regionStyles?.TryGetValue(chain.Id, out style);
            var palette = style?.Palette ?? chain.Palette;
            var isFogEnabled = style?.Fog ?? true;
            var mistColor = style?.MistColor ?? CartographyColor.MistIvory;

            // ── 1. Layer 1: 远山浅黛剪影 (MountainFar - 仅在主峰后方与天峰两侧自然隐现) ──
            foreach (var peakRatio in chain.MajorPeakRatios)
            {
                var peakIdx = Math.Clamp((int)(peakRatio * (spline.Count - 1)), 0, spline.Count - 1);
                var peakPt = spline[peakIdx];
                var isSupreme = chain.Id == 1 && MathF.Abs(peakRatio - 0.48f) < 0.05f;

                // 天下第一峰后方布置宏大气势的远山重峦
                var farCount = isSupreme ? 2 : 1;
                for (var fi = 0; fi < farCount; fi++)
                {
                    var farOffset = isSupreme
                        ? new PolyVec2((fi == 0 ? -28.0 : 28.0) + (rand.NextSingle() - 0.5f) * 10.0, -32.0 - rand.NextSingle() * 12.0)
                        : new PolyVec2((rand.NextSingle() - 0.5f) * 16.0, -26.0 - rand.NextSingle() * 10.0);

                    var farPos = peakPt + farOffset;

                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.MountainFar,
                        Position = farPos,
                        Scale = (isSupreme ? 1.35f : 0.95f) + rand.NextSingle() * 0.15f,
                        Opacity = isSupreme ? 0.42f : 0.32f,
                        YOrder = (float)farPos.Y - 30f,
                        Tint = palette.Lerp(CartographyColor.MistIvory, 0.42f),
                        RegionId = chain.Id,
                        VariantKey = "mountain_far_wash"
                    });
                }
            }

            // ── 2. Layer 2: 主山脉主脊 (三组雪峰群：西雪峰群、中极天脊群、东雪峰群，打破单线山墙) ──
            // 彻底告别连续单线铁壁，簇间为宽阔雪谷与关隘；荒漠余脉特化为红砂断崖峡谷
            for (var i = 1; i < spline.Count - 1; i++)
            {
                var t = i / (float)(spline.Count - 1);

                // 检查是否位于三大透气缺口/山口（天池冰川深谷、雁门险关等）
                if (IsOpenPassOrValley(t, chain.PassRatios)) continue;

                var isDesertCanyon = chain.Id == 4 || chain.Palette == CartographyColor.SandyOchre;

                if (!isDesertCanyon)
                {
                    // 自然疏密采样：三组错落雪峰群
                    // 簇1: 0.08 ~ 0.26 (西雪峰群); 簇2: 0.38 ~ 0.58 (中天天脊极顶群); 簇3: 0.68 ~ 0.88 (东雪峰群)
                    var inCluster1 = t >= 0.08f && t <= 0.26f;
                    var inCluster2 = t >= 0.38f && t <= 0.58f;
                    var inCluster3 = t >= 0.68f && t <= 0.88f;

                    if (!inCluster1 && !inCluster2 && !inCluster3)
                    {
                        // 峰群之间为开阔峡谷通道，不铺设连续大山墙，彻底打破一条线！
                        continue;
                    }

                    // 簇内删除30%普通冗余山体，但在主要雪峰周围保持巍峨
                    var isNearMajorPeak = MathF.Abs(t - 0.18f) < 0.05f || MathF.Abs(t - 0.48f) < 0.06f || MathF.Abs(t - 0.78f) < 0.05f;
                    if (!isNearMajorPeak && i % 2 == 1) continue;
                }

                var pt = spline[i];
                var pPrev = spline[Math.Max(0, i - 1)];
                var pNext = spline[Math.Min(spline.Count - 1, i + 1)];
                var tanX = (float)(pNext.X - pPrev.X);
                var tanY = (float)(pNext.Y - pPrev.Y);
                var tanLen = MathF.Sqrt(tanX * tanX + tanY * tanY);
                var normX = tanLen > 0.001f ? -tanY / tanLen : 0f;
                var normY = tanLen > 0.001f ? tanX / tanLen : 1f;
                if (normY < 0) { normX = -normX; normY = -normY; } // 指向南方

                var tangentAngle = MathF.Atan2(tanY, tanX);
                var isMajorPeak = IsMajorPeakNode(t, chain.MajorPeakRatios);
                var isSupremePeak = chain.Id == 1 && MathF.Abs(t - 0.48f) < 0.045f;

                // 引入自然前后错落位移（前山阳面 +14~+22px，后山阴面 -10~-16px），彻底打破单一笔直横线！
                var depthPattern = (i % 3);
                var depthOffset = depthPattern switch
                {
                    0 => 16.0f + (rand.NextSingle() - 0.5f) * 6.0f,  // 前山（靠南）
                    1 => -12.0f + (rand.NextSingle() - 0.5f) * 6.0f, // 后山（靠北）
                    _ => 2.0f + (rand.NextSingle() - 0.5f) * 6.0f    // 中脊
                };

                // 第一峰居中且略靠前，气势压顶
                if (isSupremePeak) depthOffset = 6.0f;

                var peakPos = new PolyVec2(pt.X + normX * depthOffset, pt.Y + normY * depthOffset);

                if (isDesertCanyon)
                {
                    // 荒漠峡谷红砂断崖与雅丹台地地貌
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.PlateauCliff,
                        Position = peakPos,
                        Rotation = Math.Clamp(tangentAngle * 0.18f, -0.15f, 0.15f),
                        Scale = 1.05f + rand.NextSingle() * 0.14f,
                        Opacity = 0.92f,
                        YOrder = (float)peakPos.Y,
                        Tint = palette,
                        RegionId = chain.Id,
                        VariantKey = "canyon_sandstone_cliff"
                    });
                    continue;
                }

                if (isSupremePeak)
                {
                    // ── 【天下第一峰】（一级核心景观第①位：全图最高、最大、视觉灵魂） ──
                    const float supremeScale = 1.15f;
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.MountainMain,
                        Position = peakPos,
                        Rotation = Math.Clamp(tangentAngle * 0.08f, -0.06f, 0.06f),
                        Scale = supremeScale,
                        Opacity = 1.0f,
                        YOrder = (float)peakPos.Y + 2.0f,
                        Tint = palette,
                        RegionId = chain.Id,
                        VariantKey = "peak_snow"
                    });

                    // 巍峨雪冠 (SnowCap)
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.SnowCap,
                        Position = peakPos + new PolyVec2(0d, -13.0),
                        Rotation = Math.Clamp(tangentAngle * 0.05f, -0.04f, 0.04f),
                        Scale = supremeScale * 0.76f,
                        Opacity = 0.98f,
                        YOrder = (float)peakPos.Y + 2.5f,
                        Tint = CartographyColor.White,
                        RegionId = chain.Id,
                        VariantKey = "snow_cap_crest"
                    });

                    // Map Effects: 托尔金山脉背光侧斜向阴影排线 (Tolkien Hachures)
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.MountainHachure,
                        Position = peakPos + new PolyVec2(12.0 * supremeScale, -4.0),
                        Rotation = -0.785f,
                        Scale = supremeScale,
                        StrokeWidth = 1.4f,
                        Opacity = 0.88f,
                        YOrder = (float)peakPos.Y + 2.2f,
                        Tint = palette.Lerp(new CartographyColor(25, 20, 18, 255), 0.75f),
                        RegionId = chain.Id,
                        VariantKey = "tolkien_shadow_hachures",
                        Tag = "TolkienHachure"
                    });

                    // 第一峰山腰缭绕高山流岚
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.Fog,
                        Position = peakPos + new PolyVec2(0d, 12.0),
                        Scale = 1.45f,
                        ScaleY = 0.35f,
                        Opacity = 0.55f,
                        YOrder = (float)peakPos.Y + 1.2f,
                        Tint = mistColor,
                        RegionId = chain.Id,
                        VariantKey = "mist_supreme_cloud"
                    });
                }
                else if (isMajorPeak)
                {
                    // 三大雪峰群的主峰（西雪峰群主峰、东雪峰群主峰等）
                    var peakScale = 1.02f + rand.NextSingle() * 0.08f;
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.MountainMain,
                        Position = peakPos,
                        Rotation = Math.Clamp(tangentAngle * 0.12f, -0.10f, 0.10f),
                        Scale = peakScale,
                        Opacity = 0.98f,
                        YOrder = (float)peakPos.Y + 0.5f,
                        Tint = palette,
                        RegionId = chain.Id,
                        VariantKey = chain.HasSnowCap ? "peak_snow" : "peak_main"
                    });

                    // Map Effects: 托尔金山脉背光侧斜向阴影排线
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.MountainHachure,
                        Position = peakPos + new PolyVec2(9.0 * peakScale, -2.0),
                        Rotation = -0.785f,
                        Scale = peakScale,
                        StrokeWidth = 1.2f,
                        Opacity = 0.82f,
                        YOrder = (float)peakPos.Y + 0.4f,
                        Tint = palette.Lerp(new CartographyColor(25, 20, 18, 255), 0.70f),
                        RegionId = chain.Id,
                        VariantKey = "tolkien_shadow_hachures",
                        Tag = "TolkienHachure"
                    });

                    if (chain.HasSnowCap)
                    {
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.SnowCap,
                            Position = peakPos + new PolyVec2(0d, -11.0),
                            Rotation = Math.Clamp(tangentAngle * 0.08f, -0.06f, 0.06f),
                            Scale = peakScale * 0.72f,
                            Opacity = 0.95f,
                            YOrder = (float)peakPos.Y + 0.6f,
                            Tint = CartographyColor.White,
                            RegionId = chain.Id,
                            VariantKey = "snow_cap_crest"
                        });
                    }
                }
                else
                {
                    // 簇层次级山脊，前后错落有致
                    var ridgeScale = 0.98f + (rand.NextSingle() - 0.5f) * 0.08f;
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.MountainRidge,
                        Position = peakPos,
                        Rotation = Math.Clamp(tangentAngle * 0.18f, -0.15f, 0.15f),
                        Scale = ridgeScale,
                        Opacity = 0.92f,
                        YOrder = (float)peakPos.Y,
                        Tint = palette,
                        RegionId = chain.Id,
                        VariantKey = chain.HasSnowCap ? "ridge_snow" : "ridge_wall_body"
                    });
                }
            }

            // ── 3. Layer 3: 山麓外围丰富丘陵带（三级阶梯：雪峰极顶 -> 主山脊梁 -> 山脚丘陵过渡带） ──
            for (var i = 2; i < spline.Count - 2; i += 3)
            {
                var t = i / (float)(spline.Count - 1);
                if (IsOpenPassOrValley(t, chain.PassRatios)) continue;
                if (chain.Id == 4 || chain.Palette == CartographyColor.SandyOchre) continue;
                var basePt = spline[i];
                var hillPos = basePt + new PolyVec2((rand.NextSingle() - 0.5f) * 16.0, 32.0 + rand.NextSingle() * 12.0);

                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.Hill,
                    Position = hillPos,
                    Scale = 0.95f + rand.NextSingle() * 0.10f,
                    Opacity = 0.88f,
                    YOrder = (float)hillPos.Y,
                    Tint = palette.Lerp(CartographyColor.EmeraldGreen, 0.38f),
                    RegionId = chain.Id,
                    VariantKey = "knoll_foothill"
                });
            }

            // ── 4. Layer 4: 孤峰 (15% 比例，山脚与山口两侧散落独秀奇峰) ──
            var solitaryTList = new[] { 0.16f, 0.38f, 0.62f, 0.82f };
            foreach (var st in solitaryTList)
            {
                if (IsOpenPassOrValley(st, chain.PassRatios)) continue;
                var sIdx = Math.Clamp((int)(st * (spline.Count - 1)), 0, spline.Count - 1);
                var basePt = spline[sIdx];
                // 孤峰位于山脉南侧阳坡 26~34px 处
                var solitaryPos = basePt + new PolyVec2((rand.NextSingle() - 0.5f) * 16.0, 26.0 + rand.NextSingle() * 10.0);

                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.MountainSecondary,
                    Position = solitaryPos,
                    Scale = 0.96f + rand.NextSingle() * 0.10f,
                    Opacity = 0.90f,
                    YOrder = (float)solitaryPos.Y,
                    Tint = palette.Lerp(CartographyColor.EmeraldGreen, 0.25f),
                    RegionId = chain.Id,
                    VariantKey = "solitary_peak"
                });
            }

            // ── 5. Layer 5: 支脉 (35% 比例，纵深斜出山脊) ──
            if (chain.Branches != null && chain.Branches.Count > 0)
            {
                foreach (var branch in chain.Branches)
                {
                    var bSpline = branch.SpineCurve;
                    if (bSpline == null || bSpline.Count < 2) continue;

                    for (var bi = 1; bi < bSpline.Count; bi += 2)
                    {
                        var bPt = bSpline[bi];
                        var bPrev = bSpline[bi - 1];
                        var bTangent = MathF.Atan2((float)(bPt.Y - bPrev.Y), (float)(bPt.X - bPrev.X));
                        var bt = bi / (float)(bSpline.Count - 1);

                        if (bt > 0.65f)
                        {
                            // 支脉末梢化为低丘余脉融入平原
                            instructions.Add(new BrushInstruction
                            {
                                Type = BrushType.Hill,
                                Position = bPt,
                                Scale = 0.95f + rand.NextSingle() * 0.10f,
                                Opacity = 0.86f,
                                YOrder = (float)bPt.Y,
                                Tint = palette.Lerp(CartographyColor.EmeraldGreen, 0.40f),
                                RegionId = chain.Id,
                                VariantKey = "spur_hill_tail"
                            });
                        }
                        else
                        {
                            // 支脉中段次级山峰
                            instructions.Add(new BrushInstruction
                            {
                                Type = BrushType.MountainSecondary,
                                Position = bPt,
                                Rotation = Math.Clamp(bTangent * 0.18f, -0.14f, 0.14f),
                                Scale = 0.96f + rand.NextSingle() * 0.10f,
                                Opacity = 0.90f,
                                YOrder = (float)bPt.Y,
                                Tint = palette,
                                RegionId = chain.Id,
                                VariantKey = "spur_secondary_ridge"
                            });
                        }
                    }
                }
            }

            // ── 5. Layer 5: 山麓流岚与关隘云雾 ──
            if (isFogEnabled)
            {
                // 山口山门云岫
                foreach (var pr in chain.PassRatios)
                {
                    var pIdx = Math.Clamp((int)(pr * (spline.Count - 1)), 0, spline.Count - 1);
                    var passPt = spline[pIdx];
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.Fog,
                        Position = passPt + new PolyVec2(0d, 4.0),
                        Scale = 1.35f,
                        ScaleY = 0.38f,
                        Opacity = 0.48f,
                        YOrder = (float)passPt.Y + 2.0f,
                        Tint = mistColor,
                        RegionId = chain.Id,
                        VariantKey = "mist_pass_gate"
                    });
                }

                // 2-3 处山脚低缓薄雾，增添山水意境
                for (var i = 4; i < spline.Count - 4; i += 8)
                {
                    var pt = spline[i];
                    var fogPos = pt + new PolyVec2((rand.NextSingle() - 0.5f) * 16.0, 16.0 + rand.NextSingle() * 8.0);
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.Fog,
                        Position = fogPos,
                        Scale = 1.25f,
                        ScaleY = 0.32f,
                        Opacity = 0.38f,
                        YOrder = (float)fogPos.Y + 1.0f,
                        Tint = mistColor,
                        RegionId = chain.Id,
                        VariantKey = "mist_mountain_foot"
                    });
                }
            }
        }

        return instructions;
    }

    private static bool IsOpenPassOrValley(float t, IReadOnlyList<float> passRatios)
    {
        // 关隘山口开辟宽阔山门 (门宽约 0.06)
        if (passRatios != null)
        {
            foreach (var pr in passRatios)
            {
                if (MathF.Abs(t - pr) < 0.065f) return true;
            }
        }
        // 雪谷源头（约 t = 0.32 ~ 0.38）留出大江源流穿山之谷
        if (MathF.Abs(t - 0.35f) < 0.055f) return true;

        return false;
    }

    private static bool IsMajorPeakNode(float t, IReadOnlyList<float> peakRatios)
    {
        if (peakRatios == null || peakRatios.Count == 0) return false;
        foreach (var pr in peakRatios)
        {
            if (MathF.Abs(t - pr) < 0.045f) return true;
        }
        return false;
    }
}
