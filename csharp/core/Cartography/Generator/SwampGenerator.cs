using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Cartography.Planning;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Generator;

/// <summary>
/// 低洼沼泽湿地与垂蔓苔藓生成器（SwampGenerator）。
/// 
/// 严格遵循 Map Effects 奇幻制图方法论 (https://www.mapeffects.co/tutorials/swamps)：
/// 1. 【停滞水泽与低洼泥沼 (Stagnant Swamp Pools)】：
///    - 暗色不饱和的浑浊死水池塘，伴有水平平缓水纹线；
/// 2. 【膨大露根古木 (Flared Root Swamp Trees)】：
///    - 树干基部外扩露根，扎入泥水之中，树干扭曲嶙峋；
/// 3. 【倒挂西班牙垂苔 (Hanging Spanish Moss)】：
///    - 从树冠横生枝桠下倒垂悬挂的长丝状苔藓流苏，营造神秘幽邃的沼泽氛围；
/// 4. 【水生草甸与浮游水草】：
///    - 池畔丛生芦苇蒹葭与微型沼泽浮萍。
/// </summary>
public static class SwampGenerator
{
    public sealed class SwampResult
    {
        public List<BrushInstruction> Pools { get; }
        public List<BrushInstruction> TreesAndMoss { get; }
        public List<BrushInstruction> AllInstructions { get; }

        public SwampResult(List<BrushInstruction> pools, List<BrushInstruction> treesAndMoss)
        {
            Pools = pools;
            TreesAndMoss = treesAndMoss;
            AllInstructions = new List<BrushInstruction>(pools.Count + treesAndMoss.Count);
            AllInstructions.AddRange(pools);
            AllInstructions.AddRange(treesAndMoss);
        }
    }

    /// <summary>
    /// 在指定区域生成一处幽邃古老的沼泽湿地。
    /// </summary>
    public static SwampResult GenerateSwamp(
        PolyVec2 center,
        float radius,
        int seed,
        CartographyColor basePalette,
        int regionId = -1,
        int treeCount = 6)
    {
        var pools = new List<BrushInstruction>();
        var treesAndMoss = new List<BrushInstruction>();

        var rand = new Random(seed ^ 0x535741); // "SWA"
        var murkyWater = new CartographyColor(48, 62, 58, 240); // 浑浊暗墨绿死水

        // 1. 生成 2~4 个重叠的静止泥潭死水池 (Swamp Pools)
        var poolCount = Math.Max(2, (int)(radius / 18.0f));
        for (var p = 0; p < poolCount; p++)
        {
            var offset = new PolyVec2(
                ((float)rand.NextDouble() - 0.5f) * radius * 0.75f,
                ((float)rand.NextDouble() - 0.5f) * radius * 0.55f
            );
            var poolPos = center + offset;

            pools.Add(new BrushInstruction
            {
                Type = BrushType.SwampPool,
                Position = poolPos,
                Scale = (0.85f + (float)rand.NextDouble() * 0.5f) * (radius / 30.0f),
                ScaleY = 0.52f, // 扁平水泽
                Opacity = 0.88f,
                YOrder = (float)poolPos.Y - 2.0f,
                Tint = murkyWater,
                RegionId = regionId,
                VariantKey = "swamp_stagnant_pool",
                Tag = "SwampPool"
            });
        }

        // 2. 散布膨大露根沼泽树与倒垂西班牙苔藓
        for (var i = 0; i < treeCount; i++)
        {
            var angle = (float)rand.NextDouble() * MathF.PI * 2.0f;
            var dist = (float)Math.Sqrt(rand.NextDouble()) * radius * 0.85f;
            var treePos = center + new PolyVec2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);

            var treeScale = 0.9f + (float)rand.NextDouble() * 0.35f;

            // 沼泽古树
            treesAndMoss.Add(new BrushInstruction
            {
                Type = BrushType.TreeGroup,
                Position = treePos,
                Scale = treeScale,
                Opacity = 0.95f,
                YOrder = (float)treePos.Y,
                Tint = basePalette.Lerp(new CartographyColor(35, 45, 40, 255), 0.55f),
                RegionId = regionId,
                VariantKey = "swamp_flared_root_tree",
                Tag = "SwampTree"
            });

            // 树冠下方垂挂西班牙垂苔 (Spanish Moss)
            var mossCount = 2 + rand.Next(3);
            for (var m = 0; m < mossCount; m++)
            {
                var mossOffset = new PolyVec2(
                    ((float)rand.NextDouble() - 0.5f) * 4.0f * treeScale,
                    -1.2f - (float)rand.NextDouble() * 2.0f * treeScale
                );
                var mossPos = treePos + mossOffset;

                treesAndMoss.Add(new BrushInstruction
                {
                    Type = BrushType.SpanishMoss,
                    Position = mossPos,
                    Scale = treeScale * (0.75f + (float)rand.NextDouble() * 0.35f),
                    Opacity = 0.82f,
                    YOrder = (float)treePos.Y + 0.5f,
                    Tint = new CartographyColor(90, 105, 88, 220), // 灰绿色垂蔓
                    RegionId = regionId,
                    VariantKey = "spanish_moss_strands",
                    Tag = "SpanishMoss"
                });
            }

            // 树脚泥水边缘点缀水草芦苇
            if (rand.NextDouble() > 0.35)
            {
                treesAndMoss.Add(new BrushInstruction
                {
                    Type = BrushType.WetlandReeds,
                    Position = treePos + new PolyVec2(((float)rand.NextDouble() - 0.5f) * 8.0f, 4.0f),
                    Scale = 0.70f + (float)rand.NextDouble() * 0.25f,
                    Opacity = 0.85f,
                    Tint = basePalette.Lerp(CartographyColor.EmeraldGreen, 0.4f),
                    RegionId = regionId,
                    VariantKey = "swamp_reeds"
                });
            }
        }

        return new SwampResult(pools, treesAndMoss);
    }
}
