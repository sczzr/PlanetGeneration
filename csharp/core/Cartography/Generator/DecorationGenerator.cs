using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Cartography.Planning;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Generator;

/// <summary>
/// 规划式平原人文与自然微地貌修饰画师（DecorationGenerator）。
/// 
/// 负责在开阔天府平原营造富有生活气息与留白美学的微地貌与文明活动图景：
/// 1. 【沿道沿江农田走廊 (FarmlandCorridors)】：在主干官道与天水大江两侧密集辐射排布水浇田梯田，彻底解决“平原太空”问题；
/// 2. 【水浇田梯田聚集区 (FarmlandClusters)】：神都、江陵、姑苏、浔阳等沃野核心集中呈现青碧梯田图元；
/// 3. 【旷野点睛孤丘 (SolitaryKnolls)】：平原孤立小丘，与北方远山呼应；
/// 4. 【清平镜湖与蒹葭水泽 (MirrorLakes & ReedWetlands)】：点缀澄澈小湖与蒹葭芦荡；
/// 5. 【风草微波 (GrassWaves)】：给宣纸平原以微风拂草的呼吸感。
/// </summary>
public static class DecorationGenerator
{
    public static List<BrushInstruction> Generate(
        PlannedDecorations decorations,
        GenerationOptions options,
        Dictionary<int, RegionStyle>? regionStyles = null)
    {
        var instructions = new List<BrushInstruction>();
        if (decorations == null) return instructions;

        var rand = new Random((int)(options.Seed ^ 0x444543)); // "DEC"

        // ── 1. 文明活动改造：沿道沿江密集水浇田走廊 (FarmlandCorridors) ──
        if (decorations.FarmlandCorridors != null)
        {
            foreach (var farmPos in decorations.FarmlandCorridors)
            {
                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.FieldTerraced,
                    Position = farmPos,
                    Rotation = (rand.NextSingle() - 0.5f) * 0.16f,
                    Scale = 1.05f + rand.NextSingle() * 0.18f,
                    Opacity = 0.90f,
                    YOrder = (float)farmPos.Y,
                    Tint = CartographyColor.EmeraldGreen.Lerp(CartographyColor.RicePaper, 0.22f),
                    VariantKey = "field_terraced_patch"
                });
            }
        }

        // ── 2. 传统核心农耕聚集区 (FarmlandClusters: 自然离散组团 [][] + [] + [][][]) ──
        if (decorations.FarmlandClusters != null)
        {
            // 组团相对偏移模板：双联水田 [][] + 独亩水田 [] + 三阶梯水田 [][][]
            var organicOffsets = new (float dx, float dy)[]
            {
                // 组团一：双联水田 [][]
                (-24f, -10f),
                (-4f, -12f),
                // 组团二：独亩水田 []
                (22f, -16f),
                // 组团三：三联水田梯田带 [][][]
                (-16f, 14f),
                (6f, 16f),
                (28f, 12f)
            };

            foreach (var farmCenter in decorations.FarmlandClusters)
            {
                foreach (var offset in organicOffsets)
                {
                    var fPos = farmCenter + new PolyVec2(
                        offset.dx + (rand.NextSingle() - 0.5f) * 6.0,
                        offset.dy + (rand.NextSingle() - 0.5f) * 4.0);

                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.FieldTerraced,
                        Position = fPos,
                        Rotation = (rand.NextSingle() - 0.5f) * 0.14f,
                        Scale = 1.06f + rand.NextSingle() * 0.16f,
                        Opacity = 0.92f,
                        YOrder = (float)fPos.Y,
                        Tint = CartographyColor.EmeraldGreen.Lerp(CartographyColor.RicePaper, 0.25f),
                        VariantKey = "field_terraced_patch"
                    });
                }

                // 田园周边点缀 1~2 株疏离桑柳与微型村落树木
                for (var s = 0; s < 2; s++)
                {
                    var sAngle = (float)(s * Math.PI + rand.NextDouble() * 0.5);
                    var sDist = 16.0f + rand.NextSingle() * 6.0f;
                    var shrubPos = farmCenter + new PolyVec2(MathF.Cos(sAngle) * sDist, MathF.Sin(sAngle) * sDist);

                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.TreeGroup,
                        Position = shrubPos,
                        Scale = 0.70f + rand.NextSingle() * 0.10f,
                        Opacity = 0.85f,
                        YOrder = (float)shrubPos.Y,
                        Tint = CartographyColor.EmeraldGreen.Lerp(CartographyColor.SandyOchre, 0.15f),
                        VariantKey = "copse_cluster"
                    });
                }
            }
        }

        // ── 3. 旷野点睛孤丘 (SolitaryKnolls - 平原奇峰) ──
        if (decorations.SolitaryKnolls != null)
        {
            foreach (var knollPos in decorations.SolitaryKnolls)
            {
                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.Hill,
                    Position = knollPos,
                    Scale = 0.74f + rand.NextSingle() * 0.18f,
                    Opacity = 0.90f,
                    YOrder = (float)knollPos.Y,
                    Tint = CartographyColor.EmeraldGreen.Lerp(CartographyColor.SandyOchre, 0.25f),
                    VariantKey = "knoll_solitary"
                });
            }
        }

        // ── 4. 清平镜湖与浅渚 (MirrorLakes) ──
        if (decorations.MirrorLakes != null)
        {
            foreach (var lakePos in decorations.MirrorLakes)
            {
                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.LakePond,
                    Position = lakePos,
                    Scale = 1.25f + rand.NextSingle() * 0.20f,
                    Opacity = 0.88f,
                    YOrder = (float)lakePos.Y - 1.0f,
                    Tint = CartographyColor.ColdBlue.Lerp(CartographyColor.MistIvory, 0.20f),
                    VariantKey = "lake_mirror_plains"
                });

                // 湖畔稀疏几丛水草
                for (var r = 0; r < 2; r++)
                {
                    var rAngle = (float)(r * Math.PI + rand.NextDouble() * 0.5);
                    var rDist = 12.0f;
                    var rPos = lakePos + new PolyVec2(MathF.Cos(rAngle) * rDist, MathF.Sin(rAngle) * rDist);

                    instructions.Add(new BrushInstruction
                    {
                        Type = BrushType.WetlandReeds,
                        Position = rPos,
                        Scale = 0.78f,
                        Opacity = 0.82f,
                        YOrder = (float)rPos.Y,
                        Tint = CartographyColor.EmeraldGreen,
                        VariantKey = "reed_patch"
                    });
                }
            }
        }

        // ── 5. 蒹葭芦荡湿地小品 (ReedWetlands) ──
        if (decorations.ReedWetlands != null)
        {
            foreach (var reedPos in decorations.ReedWetlands)
            {
                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.WetlandReeds,
                    Position = reedPos,
                    Scale = 0.82f + rand.NextSingle() * 0.15f,
                    Opacity = 0.84f,
                    YOrder = (float)reedPos.Y,
                    Tint = CartographyColor.EmeraldGreen.Lerp(CartographyColor.ColdBlue, 0.20f),
                    VariantKey = "reed_patch"
                });
            }
        }

        // ── 6. 微风草浪细纹 (GrassWaves - 宣纸呼吸感) ──
        if (decorations.GrassWaves != null)
        {
            foreach (var grassPos in decorations.GrassWaves)
            {
                instructions.Add(new BrushInstruction
                {
                    Type = BrushType.GrassTussock,
                    Position = grassPos,
                    Scale = 0.82f + rand.NextSingle() * 0.15f,
                    Opacity = 0.75f,
                    YOrder = (float)grassPos.Y,
                    Tint = CartographyColor.EmeraldGreen.Lerp(CartographyColor.RicePaper, 0.45f),
                    VariantKey = "grass_plain_breeze"
                });
            }
        }

        return instructions;
    }
}
