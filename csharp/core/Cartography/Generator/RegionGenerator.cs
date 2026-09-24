using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Cartography.Planning;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Generator;

/// <summary>
/// 规划式生态大区画师 V2.0（RegionGenerator）。
/// 
/// 彻底解决“森林贴图死黑堆叠、沙漠突兀无过渡”问题：
/// 1. 【西部森林】：
///    - 严格执行 40% 核心 + 40% 林缘 + 20% 草甸三级梯度；
///    - 步长放宽至 36px，核心树冠重叠率降至自然呼吸状态；
///    - 预留玉溪河谷与林间官道留白通道；
/// 2. 【东部古森林】：
///    - 降低密度，拆解为三组疏离林簇（北、东、西），告别死板大黑块；
///    - 中央开辟开阔留白空坪，容纳太古神殿遗迹与清虚灵潭；
/// 3. 【西南荒漠四层生态过渡】：
///    - 山麓台地 $\to$ 荒漠边缘（枯木沙棘半干草滩） $\to$ 沙漠核心（新月金沙、绿洲、盐湖、雅丹风蚀石） $\to$ 海岸荒地。
/// </summary>
public static class RegionGenerator
{
    public static List<BrushInstruction> Generate(
        IReadOnlyList<PlannedRegion> regions,
        GenerationOptions options,
        Dictionary<int, RegionStyle>? regionStyles = null)
    {
        var instructions = new List<BrushInstruction>();
        if (regions == null || regions.Count == 0) return instructions;

        var rand = new Random((int)(options.Seed ^ 0x524547)); // "REG"

        foreach (var reg in regions)
        {
            RegionStyle? style = null;
            regionStyles?.TryGetValue(reg.Id, out style);
            var palette = style?.Palette ?? reg.Palette;

            switch (reg.RegionType)
            {
                case PlannedRegionType.WesternForest:
                    GenerateWesternForest(reg, palette, rand, instructions);
                    break;

                case PlannedRegionType.AncientForest:
                    GenerateAncientForest(reg, palette, rand, instructions);
                    break;

                case PlannedRegionType.AridDesert:
                    GenerateDesertEcosystem(reg, palette, rand, instructions);
                    break;
            }
        }

        return instructions;
    }

    /// <summary>
    /// 西部森林：40%核心 + 40%林缘 + 20%草甸三级梯度
    /// </summary>
    private static void GenerateWesternForest(
        PlannedRegion reg,
        CartographyColor palette,
        Random rand,
        List<BrushInstruction> instructions)
    {
        var cX = (float)reg.Center.X;
        var cY = (float)reg.Center.Y;
        var rx = reg.RadiusX;
        var ry = reg.RadiusY;
        var rot = reg.Rotation;
        var cos = MathF.Cos(-rot);
        var sin = MathF.Sin(-rot);

        // 步长设为 5.5px，契合微型手绘树木图元并紧密咬合连成片
        const float step = 5.5f;
        var minX = cX - rx * 1.15f;
        var maxX = cX + rx * 1.15f;
        var minY = cY - ry * 1.15f;
        var maxY = cY + ry * 1.15f;
        var treeGrid = new TreeSpatialGrid(6.0);

        var rowIdx = 0;
        var rowStep = step * 0.60f;

        // ── Pass 1: 主林冠簇铺设 ──
        for (var y = minY; y <= maxY; y += rowStep)
        {
            var xOffset = (rowIdx % 2 == 1) ? (step * 0.50f) : 0.0f;
            rowIdx++;
            for (var x = minX - step; x <= maxX + step; x += step)
            {
                var jX = x + xOffset + (rand.NextSingle() - 0.5f) * step * 0.40f;
                var jY = y + (rand.NextSingle() - 0.5f) * rowStep * 0.40f;
                var pt = new PolyVec2(jX, jY);

                var dx = jX - cX;
                var dy = jY - cY;
                var lx = dx * cos - dy * sin;
                var ly = dx * sin + dy * cos;
                var rawDist = MathF.Sqrt((lx / rx) * (lx / rx) + (ly / ry) * (ly / ry));

                // 破边自然扰动
                var angle = MathF.Atan2(ly, lx);
                var boundaryNoise = 0.12f * MathF.Sin(3.0f * angle + 0.4f) + 0.08f * MathF.Cos(5.0f * angle + 1.1f);
                var normalizedDist = rawDist / (1.0f + boundaryNoise);

                // 超出森林边界：坚决不绘制任何树木（平原 0 树）
                if (normalizedDist >= 0.82f) continue;

                // 检查是否落入林间空地或河谷走廊
                if (ForestMassGenerator.IsInAnyClearing(pt, reg.Clearings, margin: 4.0f))
                {
                    continue;
                }

                // 核心密林 (normalizedDist < 0.55)：95% 高密度咬合连片水墨大林冠
                if (normalizedDist < 0.55f)
                {
                    if (rand.NextSingle() <= 0.95f && treeGrid.IsFarFromPlaced(pt, 4.5f))
                    {
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.ForestCluster,
                            Position = pt,
                            Scale = 0.96f + rand.NextSingle() * 0.14f,
                            Opacity = 0.92f,
                            YOrder = (float)pt.Y,
                            Tint = palette,
                            RegionId = reg.Id,
                            VariantKey = "forest_dense_core"
                        });
                        treeGrid.Add(pt);
                    }
                }
                // 林缘过渡 (0.55 <= normalizedDist < 0.82)：85% 高密度树木组合紧密衔接
                else
                {
                    if (rand.NextSingle() <= 0.85f && treeGrid.IsFarFromPlaced(pt, 4.0f))
                    {
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.TreeGroup,
                            Position = pt,
                            Scale = 0.88f + rand.NextSingle() * 0.12f,
                            Opacity = 0.88f,
                            YOrder = (float)pt.Y,
                            Tint = palette.Lerp(CartographyColor.EmeraldGreen, 0.20f),
                            RegionId = reg.Id,
                            VariantKey = "copse_cluster"
                        });
                        treeGrid.Add(pt);
                    }
                }
            }
        }

        // ── Pass 2: 隙缝单木紧密补全 (Gap-Filling Single Trees) ──
        // 使用单颗树木填补林冠间隙与空洞
        var gapStep = 2.4f;
        var gapRowStep = gapStep * 0.70f;
        var gapRowIdx = 0;
        for (var y = minY; y <= maxY; y += gapRowStep)
        {
            var xOffset = (gapRowIdx % 2 == 1) ? (gapStep * 0.50f) : 0.0f;
            gapRowIdx++;
            for (var x = minX - gapStep; x <= maxX + gapStep; x += gapStep)
            {
                var jX = x + xOffset + (rand.NextSingle() - 0.5f) * gapStep * 0.35f;
                var jY = y + (rand.NextSingle() - 0.5f) * gapRowStep * 0.35f;
                var pt = new PolyVec2(jX, jY);

                var dx = jX - cX;
                var dy = jY - cY;
                var lx = dx * cos - dy * sin;
                var ly = dx * sin + dy * cos;
                var rawDist = MathF.Sqrt((lx / rx) * (lx / rx) + (ly / ry) * (ly / ry));

                var angle = MathF.Atan2(ly, lx);
                var boundaryNoise = 0.12f * MathF.Sin(3.0f * angle + 0.4f) + 0.08f * MathF.Cos(5.0f * angle + 1.1f);
                var normalizedDist = rawDist / (1.0f + boundaryNoise);

                if (normalizedDist >= 0.78f) continue;
                if (ForestMassGenerator.IsInAnyClearing(pt, reg.Clearings, margin: 2.5f)) continue;

                if (treeGrid.IsFarFromPlaced(pt, 2.2f))
                {
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.TreeGroup,
                        Position = pt,
                        Scale = 0.82f + rand.NextSingle() * 0.15f,
                        Opacity = 0.90f,
                        YOrder = (float)pt.Y,
                        Tint = palette,
                        RegionId = reg.Id,
                        VariantKey = "tree_single"
                    });
                    treeGrid.Add(pt);
                }
            }
        }
    }

    /// <summary>
    /// 东部古老森林：核心收缩30%，建立“密林 -> 疏林 -> 草原”三级生态柔和渐变过渡，中央留白容纳神殿古迹与灵湖
    /// </summary>
    private static void GenerateAncientForest(
        PlannedRegion reg,
        CartographyColor palette,
        Random rand,
        List<BrushInstruction> instructions)
    {
        var cX = (float)reg.Center.X;
        var cY = (float)reg.Center.Y;
        var rx = reg.RadiusX;
        var ry = reg.RadiusY;

        // 步长设为 5.5px，紧密咬合连成片
        const float step = 5.5f;
        var minX = cX - rx * 1.30f;
        var maxX = cX + rx * 1.30f;
        var minY = cY - ry * 1.30f;
        var maxY = cY + ry * 1.30f;
        var treeGrid = new TreeSpatialGrid(6.0);

        // 三组疏离林簇中心（北簇近碧落幽潭、东簇近海、西南簇向平原过渡）
        var clumpCenters = new[]
        {
            new PolyVec2(cX - rx * 0.10f, cY - ry * 0.38f),      // 北簇 (环抱碧落幽潭)
            new PolyVec2(cX + rx * 0.46f, cY + ry * 0.15f),      // 东簇 (临海古林)
            new PolyVec2(cX - rx * 0.30f, cY + ry * 0.36f)       // 南西簇 (望江疏林)
        };

        var rowIdx = 0;
        var rowStep = step * 0.60f;

        // ── Pass 1: 三大林簇主林冠填充 ──
        for (var y = minY; y <= maxY; y += rowStep)
        {
            var xOffset = (rowIdx % 2 == 1) ? (step * 0.50f) : 0.0f;
            rowIdx++;
            for (var x = minX - step; x <= maxX + step; x += step)
            {
                var jX = x + xOffset + (rand.NextSingle() - 0.5f) * step * 0.40f;
                var jY = y + (rand.NextSingle() - 0.5f) * rowStep * 0.40f;
                var pt = new PolyVec2(jX, jY);

                // 距离大区中心的距离
                var dx = (jX - cX) / rx;
                var dy = (jY - cY) / ry;
                if (dx * dx + dy * dy >= 1.35f) continue;

                // 检查是否落入神殿遗迹与清修幽潭留白空地 (100% 绝对留白透气)
                if (ForestMassGenerator.IsInAnyClearing(pt, reg.Clearings, margin: 8.0f))
                {
                    continue;
                }

                // 计算到最近林簇中心的归一化距离，形成三组分散林岛
                var minDistToClump = float.MaxValue;
                foreach (var cc in clumpCenters)
                {
                    var cdx = (float)(jX - cc.X) / (rx * 0.42f);
                    var cdy = (float)(jY - cc.Y) / (ry * 0.42f);
                    var d = MathF.Sqrt(cdx * cdx + cdy * cdy);
                    if (d < minDistToClump) minDistToClump = d;
                }

                // 超出林簇范围：坚决不绘制树木，保持开阔平原净空
                if (minDistToClump >= 0.75f) continue;

                if (minDistToClump < 0.40f)
                {
                    // 1. 核心古老密林 (DenseCore) - 92% 高密度水墨大林冠连成片
                    if (rand.NextSingle() <= 0.92f && treeGrid.IsFarFromPlaced(pt, 4.5f))
                    {
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.ForestCluster,
                            Position = pt,
                            Scale = 0.92f + rand.NextSingle() * 0.14f,
                            Opacity = 0.90f,
                            YOrder = (float)pt.Y,
                            Tint = palette,
                            RegionId = reg.Id,
                            VariantKey = "forest_dense_core"
                        });
                        treeGrid.Add(pt);
                    }
                }
                else
                {
                    // 2. 中层林缘区 (Woodland) - 80% 树木紧密交柯
                    if (rand.NextSingle() <= 0.80f && treeGrid.IsFarFromPlaced(pt, 4.0f))
                    {
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.TreeGroup,
                            Position = pt,
                            Scale = 0.84f + rand.NextSingle() * 0.12f,
                            Opacity = 0.86f,
                            YOrder = (float)pt.Y,
                            Tint = palette.Lerp(CartographyColor.EmeraldGreen, 0.22f),
                            RegionId = reg.Id,
                            VariantKey = "copse_cluster"
                        });
                        treeGrid.Add(pt);
                    }
                }
            }
        }

        // ── Pass 2: 隙缝单木紧密补全 ──
        var gapStep = 2.4f;
        var gapRowStep = gapStep * 0.70f;
        var gapRowIdx = 0;
        for (var y = minY; y <= maxY; y += gapRowStep)
        {
            var xOffset = (gapRowIdx % 2 == 1) ? (gapStep * 0.50f) : 0.0f;
            gapRowIdx++;
            for (var x = minX - gapStep; x <= maxX + gapStep; x += gapStep)
            {
                var jX = x + xOffset + (rand.NextSingle() - 0.5f) * gapStep * 0.35f;
                var jY = y + (rand.NextSingle() - 0.5f) * gapRowStep * 0.35f;
                var pt = new PolyVec2(jX, jY);

                var dx = (jX - cX) / rx;
                var dy = (jY - cY) / ry;
                if (dx * dx + dy * dy >= 1.35f) continue;

                if (ForestMassGenerator.IsInAnyClearing(pt, reg.Clearings, margin: 4.0f)) continue;

                var minDistToClump = float.MaxValue;
                foreach (var cc in clumpCenters)
                {
                    var cdx = (float)(jX - cc.X) / (rx * 0.42f);
                    var cdy = (float)(jY - cc.Y) / (ry * 0.42f);
                    var d = MathF.Sqrt(cdx * cdx + cdy * cdy);
                    if (d < minDistToClump) minDistToClump = d;
                }

                if (minDistToClump >= 0.72f) continue;

                if (treeGrid.IsFarFromPlaced(pt, 2.2f))
                {
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.TreeGroup,
                        Position = pt,
                        Scale = 0.82f + rand.NextSingle() * 0.15f,
                        Opacity = 0.88f,
                        YOrder = (float)pt.Y,
                        Tint = palette,
                        RegionId = reg.Id,
                        VariantKey = "tree_single"
                    });
                    treeGrid.Add(pt);
                }
            }
        }
    }

    /// <summary>
    /// 西南大荒漠：扩大18%面积，死寂金沙、顺风沙丘、风蚀雅丹、盐碱地与三层自然过渡
    /// </summary>
    private static void GenerateDesertEcosystem(
        PlannedRegion reg,
        CartographyColor palette,
        Random rand,
        List<BrushInstruction> instructions)
    {
        var cX = (float)reg.Center.X;
        var cY = (float)reg.Center.Y;
        var rx = reg.RadiusX;
        var ry = reg.RadiusY;

        // ── 1. 半干旱外围过渡带 (0.70 <= d < 1.05)：枯木残枝、风蚀沙棘、干草滩，杜绝绿黄突变 ──
        for (var i = 0; i < 22; i++)
        {
            var angle = (float)(i * (MathF.PI * 2.0f / 22.0f) + (rand.NextSingle() - 0.5f) * 0.20f);
            var distFactor = 0.74f + rand.NextSingle() * 0.28f;
            var edgeX = cX + MathF.Cos(angle) * rx * distFactor;
            var edgeY = cY + MathF.Sin(angle) * ry * distFactor;
            var edgePos = new PolyVec2(edgeX, edgeY);

            if (i % 2 == 0)
            {
                // 枯木残枝 (Death/Arid Vibe)
                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.TreeGroup,
                    Position = edgePos,
                    Scale = 0.82f + rand.NextSingle() * 0.15f,
                    Opacity = 0.80f,
                    YOrder = (float)edgePos.Y,
                    Tint = palette.Lerp(CartographyColor.SandyOchre, 0.45f),
                    RegionId = reg.Id,
                    VariantKey = "withered_tree_arid"
                });
            }
            else
            {
                // 半干旱沙棘与旱地草丛
                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.GrassTussock,
                    Position = edgePos,
                    Scale = 0.82f + rand.NextSingle() * 0.15f,
                    Opacity = 0.78f,
                    YOrder = (float)edgePos.Y,
                    Tint = CartographyColor.SandyOchre.Lerp(CartographyColor.EmeraldGreen, 0.20f),
                    RegionId = reg.Id,
                    VariantKey = "grass_steppe_dry"
                });
            }
        }

        // ── 2. 沙漠核心区：顺风向新月金沙垄走廊（统一倾角 0.35 rad，波状金色沙海） ──
        var lanes = new[] { -1.35f, -0.45f, 0.45f, 1.35f };
        foreach (var lane in lanes)
        {
            var laneOffsetY = lane * ry * 0.24f;
            var duneCount = 7;
            for (var i = 0; i < duneCount; i++)
            {
                var t = (i + 0.5f) / duneCount;
                var x = cX - rx * 0.60f + t * rx * 1.20f + (rand.NextSingle() - 0.5f) * 14.0f;
                var y = cY + laneOffsetY + (rand.NextSingle() - 0.5f) * 10.0f;
                var pos = new PolyVec2(x, y);

                // 避开各处绿洲核心
                var nearOasis = false;
                foreach (var oasis in reg.Oases)
                {
                    var ddx = pos.X - oasis.X;
                    var ddy = pos.Y - oasis.Y;
                    if (ddx * ddx + ddy * ddy < 800.0)
                    {
                        nearOasis = true;
                        break;
                    }
                }
                if (nearOasis) continue;

                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.DesertDune,
                    Position = pos,
                    Rotation = reg.WindAngle + (rand.NextSingle() - 0.5f) * 0.05f, // 严格顺风统一角度 0.35 rad
                    Scale = 1.05f + rand.NextSingle() * 0.15f,
                    Opacity = 0.90f,
                    YOrder = (float)pos.Y,
                    Tint = palette,
                    RegionId = reg.Id,
                    VariantKey = "dune_crescent"
                });
            }
        }

        // ── 3. 风蚀雅丹残岩台地 (6处断崖台地，强化大荒苍茫与死亡感) ──
        for (var i = 0; i < 6; i++)
        {
            var angle = MathF.PI * 0.70f + (i * 0.18f);
            var cliffX = cX + MathF.Cos(angle) * rx * 0.70f;
            var cliffY = cY + MathF.Sin(angle) * ry * 0.70f;
            var cPos = new PolyVec2(cliffX, cliffY);

            instructions.Add(new BrushInstruction
            {
                Type = BrushType.PlateauCliff,
                Position = cPos,
                Scale = 0.92f + rand.NextSingle() * 0.14f,
                Opacity = 0.90f,
                YOrder = (float)cPos.Y,
                Tint = palette,
                RegionId = reg.Id,
                VariantKey = "plateau_cliff_edge"
            });
        }

        // ── 4. 鸣沙绿洲生态 (绿洲水塘 + 环抱沙柳胡杨) ──
        foreach (var oasisPos in reg.Oases)
        {
            instructions.Add(new BrushInstruction
            {
                Type = BrushType.LakePond,
                Position = oasisPos,
                Scale = 1.20f,
                Opacity = 0.92f,
                YOrder = (float)oasisPos.Y - 1.0f,
                Tint = CartographyColor.ColdBlue.Lerp(CartographyColor.EmeraldGreen, 0.40f),
                RegionId = reg.Id,
                VariantKey = "oasis_spring"
            });

            for (var t = 0; t < 3; t++)
            {
                var angle = (float)(t * Math.PI * 0.7 + rand.NextDouble() * 0.3);
                var dist = 6.0f + rand.NextSingle() * 4.0f;
                var willowPos = oasisPos + new PolyVec2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);

                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.TreeGroup,
                    Position = willowPos,
                    Scale = 0.85f + rand.NextSingle() * 0.12f,
                    Opacity = 0.90f,
                    YOrder = (float)willowPos.Y,
                    Tint = CartographyColor.EmeraldGreen,
                    RegionId = reg.Id,
                    VariantKey = "willow_stream"
                });
            }
        }

        // ── 5. 金沙盐湖与盐碱盆地微地貌 (白色盐壳与死寂干涸河床) ──
        var saltLakePos = new PolyVec2(cX - rx * 0.42f, cY + ry * 0.35f);
        instructions.Add(new BrushInstruction
        {
            Type = BrushType.LakePond,
            Position = saltLakePos,
            Scale = 1.20f,
            Opacity = 0.86f,
            YOrder = (float)saltLakePos.Y - 1.0f,
            Tint = CartographyColor.ColdBlue.Lerp(CartographyColor.White, 0.58f),
            RegionId = reg.Id,
            VariantKey = "salt_lake_basin"
        });

        // 盐湖周边盐碱干涸裂地
        for (var s = 0; s < 3; s++)
        {
            var sAngle = (float)(s * Math.PI * 0.65 + 0.4);
            var sPos = saltLakePos + new PolyVec2(MathF.Cos(sAngle) * 18.0, MathF.Sin(sAngle) * 14.0);
            instructions.Add(new BrushInstruction
            {
                Type = BrushType.GrassTussock,
                Position = sPos,
                Scale = 0.76f,
                Opacity = 0.70f,
                YOrder = (float)sPos.Y,
                Tint = CartographyColor.SandyOchre.Lerp(CartographyColor.White, 0.45f),
                RegionId = reg.Id,
                VariantKey = "salt_crust_alkali"
            });
        }
    }
}
