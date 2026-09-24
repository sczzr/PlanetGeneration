using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Cartography.Planning;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Generator;

/// <summary>
/// 树根状河口分形三角洲生成器（RiverDeltaGenerator）。
/// 
/// 严格遵循 Map Effects 奇幻制图方法论 (https://www.mapeffects.co/tutorials/river-deltas)：
/// 1. 【冲积扇与分流体系】：
///    - 河流入海时流速骤减，携带泥沙大量沉积形成半封闭冲积扇；
///    - 三角洲是地图上河流唯一自然分流（Distributaries）的区域，呈树根状放射涌入大海；
/// 2. 【负空间冲积沙洲群岛 (Delta Islands)】：
///    - 分流河道切削的负空间形成若干大小错落的冲积小岛（Delta Islands）；
///    - 小岛下边缘具有断裂阴影线（Broken depth lines），岛上点缀耐盐碱芦苇与湿地草丛。
/// </summary>
public static class RiverDeltaGenerator
{
    public sealed class DeltaResult
    {
        public List<BrushInstruction> DistributaryStreams { get; }
        public List<BrushInstruction> DeltaIslands { get; }
        public List<BrushInstruction> AllInstructions { get; }

        public DeltaResult(List<BrushInstruction> streams, List<BrushInstruction> islands)
        {
            DistributaryStreams = streams;
            DeltaIslands = islands;
            AllInstructions = new List<BrushInstruction>(streams.Count + islands.Count);
            AllInstructions.AddRange(streams);
            AllInstructions.AddRange(islands);
        }
    }

    /// <summary>
    /// 在河口处生成树根状分形三角洲与冲积岛屿群。
    /// </summary>
    public static DeltaResult GenerateDelta(
        PolyVec2 mouthPos,
        PolyVec2 incomingDir,
        float mouthWidth,
        int seed,
        CartographyColor waterColor,
        int regionId = -1,
        int branchCount = 4,
        float deltaReach = 55.0f)
    {
        var streams = new List<BrushInstruction>();
        var islands = new List<BrushInstruction>();

        if (mouthWidth < 3.0f)
        {
            return new DeltaResult(streams, islands);
        }

        var rand = new Random(seed ^ 0x44454c); // "DEL"
        var forward = incomingDir.Length > 1e-4 ? incomingDir.Normalized() : new PolyVec2(1.0, 0.0);
        var lateral = new PolyVec2(-forward.Y, forward.X);

        var spreadAngle = MathF.PI * 0.38f; // ~68 度扇形展开角
        var baseAngle = MathF.Atan2((float)forward.Y, (float)forward.X);

        var distributaryEnds = new List<PolyVec2>();
        var distributaryPaths = new List<List<PolyVec2>>();

        // 1. 生成树根状发散分流水道 (Distributaries)
        for (var b = 0; b < branchCount; b++)
        {
            var t = (branchCount <= 1) ? 0.5f : (float)b / (branchCount - 1);
            var angleOffset = (t - 0.5f) * spreadAngle + ((float)rand.NextDouble() - 0.5f) * 0.12f;
            var currentAngle = baseAngle + angleOffset;

            var branchDir = new PolyVec2(MathF.Cos(currentAngle), MathF.Sin(currentAngle));
            var branchReach = deltaReach * (0.85f + (float)rand.NextDouble() * 0.35f);

            // 中间骨干分流稍粗，两侧侧支稍细
            var branchWidth = Math.Max(1.8f, mouthWidth * (0.42f + 0.25f * (1.0f - MathF.Abs(t - 0.5f) * 2f)));

            // 构造 3~4 段具有微小有机弯折的水道点序列
            var p0 = mouthPos;
            var p1 = mouthPos + branchDir * (branchReach * 0.35f) + lateral * (((float)rand.NextDouble() - 0.5f) * mouthWidth * 0.5f);
            var p2 = mouthPos + branchDir * (branchReach * 0.70f) + lateral * (((float)rand.NextDouble() - 0.5f) * mouthWidth * 0.6f);
            var p3 = mouthPos + branchDir * branchReach;

            var path = new List<PolyVec2> { p0, p1, p2, p3 };
            distributaryPaths.Add(path);
            distributaryEnds.Add(p3);

            streams.Add(new BrushInstruction
            {
                Type = BrushType.RiverStroke,
                Position = p0,
                Points = path.ToArray(),
                StrokeWidth = branchWidth,
                Opacity = 0.88f,
                Tint = waterColor,
                RegionId = regionId,
                VariantKey = "delta_distributary",
                Tag = $"DeltaBranch_{b}"
            });

            // 次级微支流分裂（在末端随机分出微小溪流注入浅海）
            if (rand.NextDouble() > 0.45)
            {
                var subAngle = currentAngle + (((float)rand.NextDouble() > 0.5f ? 1.0f : -1.0f) * 0.28f);
                var subDir = new PolyVec2(MathF.Cos(subAngle), MathF.Sin(subAngle));
                var subReach = deltaReach * 0.45f;
                var subPath = new[] { p2, p2 + subDir * subReach };

                streams.Add(new BrushInstruction
                {
                    Type = BrushType.RiverStroke,
                    Position = p2,
                    Points = subPath,
                    StrokeWidth = Math.Max(1.2f, branchWidth * 0.5f),
                    Opacity = 0.78f,
                    Tint = waterColor,
                    RegionId = regionId,
                    VariantKey = "delta_sub_distributary"
                });
            }
        }

        // 2. 提取分流水道之间的负空间生成冲积沙洲群岛 (Delta Islands)
        for (var i = 0; i < distributaryPaths.Count - 1; i++)
        {
            var leftPath = distributaryPaths[i];
            var rightPath = distributaryPaths[i + 1];

            // 取中段位置计算沙洲岛屿中心
            var midLeft = leftPath[leftPath.Count / 2];
            var midRight = rightPath[rightPath.Count / 2];
            var islandPos = (midLeft + midRight) * 0.5;

            var islandScale = (float)(midRight - midLeft).Length * 0.045f;
            islandScale = Math.Clamp(islandScale, 0.6f, 1.4f);

            // 沙洲岛屿图元
            islands.Add(new BrushInstruction
            {
                Type = BrushType.DeltaIsland,
                Position = islandPos,
                Scale = islandScale,
                ScaleY = 0.65f, // 扁椭圆沙洲轮廓
                Rotation = baseAngle + ((float)rand.NextDouble() - 0.5f) * 0.2f,
                Opacity = 0.95f,
                YOrder = (float)islandPos.Y,
                Tint = CartographyColor.SandyOchre.Lerp(CartographyColor.EmeraldGreen, 0.35f),
                RegionId = regionId,
                VariantKey = "delta_silt_island",
                Tag = $"DeltaIsland_{i}"
            });

            // 岛上点缀耐盐芦苇水草
            islands.Add(new BrushInstruction
            {
                Type = BrushType.WetlandReeds,
                Position = islandPos + lateral * (((float)rand.NextDouble() - 0.5f) * 4.0f),
                Scale = 0.75f + (float)rand.NextDouble() * 0.25f,
                Opacity = 0.85f,
                Tint = CartographyColor.EmeraldGreen.Lerp(CartographyColor.SandyOchre, 0.25f),
                RegionId = regionId,
                VariantKey = "reeds_tuft"
            });
        }

        return new DeltaResult(streams, islands);
    }
}
