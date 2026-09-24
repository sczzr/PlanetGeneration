using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Cartography.Planning;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Generator;

/// <summary>
/// 规划式九曲水文画师（RiverGenerator）。
/// 
/// 彻底废弃“平原随机直线割裂”缺陷，严格贯彻：
/// 1. 【高山雪水发源】：水源必起于雪山深谷鞍部；
/// 2. 【平原九曲回环】：主干大江经由 Catmull-Rom 与正弦多阶扰动，呈东方古地图大江蜿蜒环抱中原之势；
/// 3. 【支流自然纳汇】：林海青岚玉溪与大漠边缘支流自然汇入干流（树状水网）；
/// 4. 【水势由狭入阔】：源头清澈细窄（2.4px），入海汪洋宽阔（9.5px+），海口呈喇叭状三角洲注入大海；
/// 5. 【串联湖泽浅渚】：碧玉湖与清平渚沿途点缀，芦荡蒹葭环生。
/// </summary>
public static class RiverGenerator
{
    public static List<BrushInstruction> Generate(
        IReadOnlyList<PlannedRiverSystem> riverSystems,
        GenerationOptions options,
        Dictionary<int, RegionStyle>? regionStyles = null)
    {
        var instructions = new List<BrushInstruction>();
        if (riverSystems == null || riverSystems.Count == 0) return instructions;

        var rand = new Random((int)(options.Seed ^ 0x524956)); // "RIV"

        foreach (var river in riverSystems)
        {
            var palette = river.Palette;

            // ── 1. 主干大江 (River Main Stem - 东方九曲大江) ──
            if (river.MainWaypoints != null && river.MainWaypoints.Count >= 2)
            {
                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.RiverStroke,
                    Position = river.MainWaypoints[0],
                    Points = river.MainWaypoints.ToArray(),
                    StrokeWidth = river.MouthWidth,
                    Opacity = 0.95f,
                    Tint = palette,
                    RegionId = river.Id,
                    VariantKey = "river_main_stem",
                    Tag = river.Name
                });
            }

            // ── 2. 支流水系 (Tributaries - 汇流成川) ──
            foreach (var branch in river.Branches)
            {
                if (branch.Waypoints == null || branch.Waypoints.Count < 2) continue;

                var branchWidth = MathF.Max(1.8f, river.SourceWidth * branch.WidthScale * 1.35f);
                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.RiverStroke,
                    Position = branch.Waypoints[0],
                    Points = branch.Waypoints.ToArray(),
                    StrokeWidth = branchWidth,
                    Opacity = 0.90f,
                    Tint = palette.Lerp(CartographyColor.EmeraldGreen, 0.15f),
                    RegionId = river.Id,
                    VariantKey = "river_tributary",
                    Tag = branch.Name
                });
            }

            // ── 3. 串联名湖与清平浅渚 (LakePond & WetlandReeds) ──
            for (var lIdx = 0; lIdx < river.Lakes.Count; lIdx++)
            {
                var lakePos = river.Lakes[lIdx];
                var isCentralValleyLake = (lIdx == 0 && river.Lakes.Count >= 1);

                if (isCentralValleyLake)
                {
                    // 【中央圣湖 / 灵渊天池】：群山脚下核心神圣大湖，碧玉澄澈，3段沿河道蜿蜒重叠的长条水体与双侧芦苇荡
                    var flowAngle = 0.48f; // 沿河流自西北流向东南夹角
                    var cos = MathF.Cos(flowAngle);
                    var sin = MathF.Sin(flowAngle);
                    var normX = -sin;
                    var normY = cos;

                    var lobeOffsets = new[] { -22.0f, 0.0f, 22.0f };
                    var lobeScales = new[] { 1.35f, 1.62f, 1.40f };

                    for (var li = 0; li < lobeOffsets.Length; li++)
                    {
                        var lobePos = lakePos + new PolyVec2(cos * lobeOffsets[li], sin * lobeOffsets[li]);
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.LakePond,
                            Position = lobePos,
                            Rotation = flowAngle + (rand.NextSingle() - 0.5f) * 0.12f,
                            Scale = lobeScales[li] + rand.NextSingle() * 0.10f,
                            Opacity = 0.92f,
                            YOrder = (float)lobePos.Y - 1.0f,
                            Tint = palette.Lerp(CartographyColor.EmeraldGreen, 0.32f), // 碧玉澄澈湖水
                            RegionId = river.Id,
                            VariantKey = "lake_mirror_scenic"
                        });
                    }

                    // 湖身两侧密集天然芦苇水草
                    for (var side = -1; side <= 1; side += 2)
                    {
                        for (var ri = 0; ri < 4; ri++)
                        {
                            var along = (ri - 1.5f) * 12.0f;
                            var across = side * (16.0f + rand.NextSingle() * 8.0f);
                            var rPos = lakePos + new PolyVec2(cos * along + normX * across, sin * along + normY * across);

                            instructions.Add(new BrushInstruction
                            {
                                Type = BrushType.WetlandReeds,
                                Position = rPos,
                                Scale = 0.88f + rand.NextSingle() * 0.18f,
                                Opacity = 0.88f,
                                YOrder = (float)rPos.Y,
                                Tint = CartographyColor.EmeraldGreen,
                                RegionId = river.Id,
                                VariantKey = "reed_water_edge"
                            });
                        }
                    }
                }
                else if (lIdx == 2 && river.Lakes.Count >= 3)
                {
                    // 【江心洲 / 龙吟渚】：大江开阔处江心芳草绿洲与沙洲群
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.WetlandReeds,
                        Position = lakePos,
                        Scale = 1.25f + rand.NextSingle() * 0.15f,
                        Opacity = 0.95f,
                        YOrder = (float)lakePos.Y + 0.5f,
                        Tint = CartographyColor.EmeraldGreen.Lerp(CartographyColor.RicePaper, 0.15f),
                        RegionId = river.Id,
                        VariantKey = "wetland_islet"
                    });

                    // 江心洲头洲尾芳草
                    for (var s = -1; s <= 1; s += 2)
                    {
                        var sPos = lakePos + new PolyVec2(s * 14.0 + (rand.NextSingle() - 0.5f) * 4.0, (rand.NextSingle() - 0.5f) * 6.0);
                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.WetlandReeds,
                            Position = sPos,
                            Scale = 0.82f + rand.NextSingle() * 0.12f,
                            Opacity = 0.88f,
                            YOrder = (float)sPos.Y,
                            Tint = CartographyColor.EmeraldGreen,
                            RegionId = river.Id,
                            VariantKey = "reed_water_edge"
                        });
                    }
                }
                else
                {
                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.LakePond,
                        Position = lakePos,
                        Scale = 1.35f + rand.NextSingle() * 0.25f,
                        Opacity = 0.88f,
                        YOrder = (float)lakePos.Y - 1.0f,
                        Tint = palette.Lerp(CartographyColor.ColdBlue, 0.40f),
                        RegionId = river.Id,
                        VariantKey = "lake_mirror_scenic"
                    });

                    // 湖畔蒹葭水草
                    for (var r = 0; r < 3; r++)
                    {
                        var angle = (float)(r * Math.PI * 0.65 + rand.NextDouble() * 0.35);
                        var dist = 14.0f + rand.NextSingle() * 10.0f;
                        var reedPos = lakePos + new PolyVec2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);

                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.WetlandReeds,
                            Position = reedPos,
                            Scale = 0.85f + rand.NextSingle() * 0.20f,
                            Opacity = 0.85f,
                            YOrder = (float)reedPos.Y,
                            Tint = CartographyColor.EmeraldGreen,
                            RegionId = river.Id,
                            VariantKey = "reed_water_edge"
                        });
                    }
                }
            }
        }

        return instructions;
    }

    /// <summary>
    /// 为海域网格生成雅致稀疏的古典水波纹指令 (SeaWave)。
    /// </summary>
    public static void AppendSeaWaves(
        List<BrushInstruction> instructions,
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options)
    {
        var seaLevel = options.SeaLevel;
        var width = geometry.Width;
        var height = geometry.Height;
        var rand = new Random((int)(options.Seed ^ 0x5a5a));

        var stepY = 24.0;
        var stepX = 32.0;

        for (var y = 16.0; y < height - 16.0; y += stepY)
        {
            var offset = (rand.NextDouble() - 0.5) * 10.0;
            for (var x = 16.0; x < width - 16.0; x += stepX)
            {
                var curX = x + offset + (rand.NextDouble() - 0.5) * 6.0;
                var curY = y + (rand.NextDouble() - 0.5) * 4.0;

                var cell = geometry.FindCell(curX, curY);
                if (cell < 0 || cell >= fields.Count || fields.Height[cell] > seaLevel) continue;

                var arcLen = 14.0 + rand.NextDouble() * 8.0;
                var p0 = new PolyVec2(curX - arcLen * 0.5, curY);
                var p1 = new PolyVec2(curX, curY - 2.5);
                var p2 = new PolyVec2(curX + arcLen * 0.5, curY);

                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.SeaWave,
                    Position = new PolyVec2(curX, curY),
                    Points = new[] { p0, p1, p2 },
                    StrokeWidth = 1.0f,
                    Opacity = 0.45f,
                    YOrder = (float)curY - 100f,
                    Tint = CartographyColor.SeaWave,
                    VariantKey = "wave_arc"
                });
            }
        }
    }
}
