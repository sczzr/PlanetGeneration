using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Cartography.Planning;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Generator;

/// <summary>
/// 大地裂谷断崖与深渊生成器（ChasmGenerator）。
/// 
/// 严格遵循 Map Effects 奇幻制图方法论 (https://www.mapeffects.co/tutorials/chasm)：
/// 1. 【平行锯齿双崖壁 (Dual Ragged Cliff Edges)】：
///    - 双侧平行起伏折线呈现等角俯视视角（Isometric Perspective）；
/// 2. 【垂直崖面落差排线与水平沉积岩层 (Vertical Hatching & Strata Lines)】：
///    - 从崖壁各突出角点向下投射垂直裂理线，勾勒崖面立体转折；
///    - 顺应崖壁走向添加横向等高沉积岩层线（Sediment Contour Layers）；
/// 3. 【深渊黑度渐变 (Gradient Abyss)】：
///    - 随着深度增加光照迅速被吞噬，峡谷底部渐变为深邃黑暗（Pitch Black / #120F0E）；
///    - 崖壁顶部边缘施加高亮边缘线（Top Rim Highlight）。
/// </summary>
public static class ChasmGenerator
{
    public sealed class ChasmResult
    {
        public List<BrushInstruction> AbyssBases { get; }
        public List<BrushInstruction> CliffStructures { get; }
        public List<BrushInstruction> AllInstructions { get; }

        public ChasmResult(List<BrushInstruction> abyss, List<BrushInstruction> cliffs)
        {
            AbyssBases = abyss;
            CliffStructures = cliffs;
            AllInstructions = new List<BrushInstruction>(abyss.Count + cliffs.Count);
            AllInstructions.AddRange(abyss);
            AllInstructions.AddRange(cliffs);
        }
    }

    /// <summary>
    /// 在指定两点间生成一道震撼的大地深渊裂谷与断崖排线。
    /// </summary>
    public static ChasmResult GenerateChasm(
        PolyVec2 startPt,
        PolyVec2 endPt,
        int seed,
        CartographyColor rockTint,
        int regionId = -1,
        float avgWidth = 22.0f,
        float cliffDrop = 26.0f)
    {
        var abyss = new List<BrushInstruction>();
        var cliffs = new List<BrushInstruction>();

        var delta = endPt - startPt;
        var length = (float)delta.Length;
        if (length < 20.0f) return new ChasmResult(abyss, cliffs);

        var rand = new Random(seed ^ 0x434841); // "CHA"
        var dir = delta.Normalized();
        var normal = new PolyVec2(-dir.Y, dir.X); // 法向量

        var segCount = Math.Max(6, (int)(length / 18.0f));
        var step = length / segCount;

        var topCliff = new List<PolyVec2>(segCount + 1);
        var bottomCliff = new List<PolyVec2>(segCount + 1);

        // 1. 生成双侧平行锯齿断崖轮廓 (Top & Bottom Rims)
        for (var i = 0; i <= segCount; i++)
        {
            var t = (float)i / segCount;
            var basePos = startPt + dir * (i * step);

            // 越靠近两端裂谷逐渐闭合收窄
            var taper = MathF.Sin(t * MathF.PI);
            var localWidth = avgWidth * taper;

            // 局部噪声横向扰动
            var noise1 = ((float)rand.NextDouble() - 0.5f) * (localWidth * 0.45f);
            var noise2 = ((float)rand.NextDouble() - 0.5f) * (localWidth * 0.45f);

            var pTop = basePos + normal * (localWidth * 0.5f + noise1);
            var pBottom = basePos - normal * (localWidth * 0.5f + noise2);

            topCliff.Add(pTop);
            bottomCliff.Add(pBottom);
        }

        // 2. 峡谷底部深渊渐变基底 (Chasm Abyss Base - 幽暗深渊)
        var abyssPoints = new List<PolyVec2>();
        abyssPoints.AddRange(topCliff);
        for (var i = bottomCliff.Count - 1; i >= 0; i--)
        {
            abyssPoints.Add(bottomCliff[i]);
        }

        var chasmCenter = (startPt + endPt) * 0.5;

        abyss.Add(new BrushInstruction
        {
            Type = BrushType.ChasmAbyss,
            Position = chasmCenter,
            Points = abyssPoints.ToArray(),
            Opacity = 0.98f,
            YOrder = (float)chasmCenter.Y,
            Tint = new CartographyColor(18, 14, 12, 255), // 接近纯黑的古褐色深渊
            RegionId = regionId,
            VariantKey = "chasm_abyss_polygon",
            Tag = "ChasmAbyss"
        });

        // 3. 沿上沿突出角点向下绘制垂直落差排线与横向沉积等高线 (Cliff Strata & Hatching)
        for (var i = 1; i < topCliff.Count - 1; i++)
        {
            var pPrev = topCliff[i - 1];
            var pCurr = topCliff[i];
            var pNext = topCliff[i + 1];

            // 检查角点是否向外凸出
            var v1 = (pCurr - pPrev).Normalized();
            var v2 = (pNext - pCurr).Normalized();
            var cross = v1.X * v2.Y - v1.Y * v2.X;

            var dropHeight = cliffDrop * (0.75f + (float)rand.NextDouble() * 0.5f);
            var pDropEnd = pCurr + new PolyVec2(0.0, dropHeight); // 向下投影呈现等角立体壁面

            // 垂直落差排线 (Vertical Cliff Drop Line)
            cliffs.Add(new BrushInstruction
            {
                Type = BrushType.ChasmCliff,
                Position = pCurr,
                Points = new[] { pCurr, pDropEnd },
                StrokeWidth = 1.6f,
                Opacity = 0.85f,
                YOrder = (float)pCurr.Y + 2.0f,
                Tint = rockTint.Lerp(new CartographyColor(25, 20, 18, 255), 0.7f),
                RegionId = regionId,
                VariantKey = "cliff_vertical_hatch"
            });

            // 水平沉积岩层裂纹 (Horizontal Contour Strata Lines)
            if (i % 2 == 0)
            {
                var midDrop = pCurr + new PolyVec2(0.0, dropHeight * 0.45f);
                var strataEnd = midDrop + dir * (step * 0.6f);
                cliffs.Add(new BrushInstruction
                {
                    Type = BrushType.ChasmCliff,
                    Position = midDrop,
                    Points = new[] { midDrop, strataEnd },
                    StrokeWidth = 1.0f,
                    Opacity = 0.70f,
                    YOrder = (float)midDrop.Y,
                    Tint = rockTint.Lerp(CartographyColor.MistIvory, 0.15f),
                    RegionId = regionId,
                    VariantKey = "cliff_strata_line"
                });
            }
        }

        // 4. 崖壁上边缘受光高亮线 (Top Rim Highlight - Add Blend)
        cliffs.Add(new BrushInstruction
        {
            Type = BrushType.ChasmCliff,
            Position = startPt,
            Points = topCliff.ToArray(),
            StrokeWidth = 1.8f,
            Opacity = 0.90f,
            YOrder = (float)startPt.Y - 1.0f,
            Tint = CartographyColor.MistIvory,
            RegionId = regionId,
            VariantKey = "cliff_rim_highlight",
            Tag = "ChasmRim"
        });

        return new ChasmResult(abyss, cliffs);
    }
}
