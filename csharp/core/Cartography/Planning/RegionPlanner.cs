using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Planning;

/// <summary>
/// 宏观叙事区域规划器（RegionPlanner）。
/// 
/// 彻底废弃“孤立局部噪声模拟”旧路线，确立“区域叙事（Narrative Regions）”作为地图的第一公民。
/// 将全大陆有机划分为五大叙事大区：
/// 1. 【青冥北岳】（极北高寒雪山高地）：横亘千里龙脉，高耸雪峰，雄关锁道；
/// 2. 【中原天府】（天水之枢大江沃野）：九曲大江穿城，帝都京畿，阡陌水田，商贾繁华；
/// 3. 【太古青岚】（水墨浩荡太古林海）：四级生态衰减水墨体块，腹地清修留白；
/// 4. 【狂沙大荒】（金沙瀚海裂谷残脉）：向东向南大幅延展，流动新月沙垄，断崖红砂，根除南部空白；
/// 5. 【东南沧溟】（万舶归流海湾水乡）：半月巨港，大江注海，渔村桑麻，无缝接续大荒与平原。
/// </summary>
public static class RegionPlanner
{
    public static List<RegionData> PlanNarrativeRegions(float w, float h, MapBlueprint? blueprint = null)
    {
        var regions = new List<RegionData>
        {
            // ── 1. 北境冰原 (PolarTundra) ──
            new()
            {
                Id = 1,
                Name = "北境冰原",
                Title = "极北高寒苔原冻土",
                Type = NarrativeRegionType.PolarTundra,
                Center = new PolyVec2(0.50 * w, 0.13 * h),
                RadiusX = 0.38f * w,
                RadiusY = 0.09f * h,
                Rotation = 0.0f,
                PrimaryColor = CartographyColor.MistIvory,
                FeatureDescriptions = new List<string>
                {
                    "极北终年积雪冻土荒原",
                    "冷灰色水墨晕染冰原",
                    "高寒地衣与冰封幽湖"
                },
                ContainedSettlementNames = new List<string>(),
                Bounds = GenerateOvalPolygon(0.50f * w, 0.13f * h, 0.38f * w, 0.09f * h, 0.0f)
            },

            // ── 2. 苍冥北岳 (NorthMountain) ──
            new()
            {
                Id = 2,
                Name = "苍冥北岳",
                Title = "北方主山脉雪山群岳",
                Type = NarrativeRegionType.MountainRange,
                Center = new PolyVec2(0.49 * w, 0.25 * h),
                RadiusX = 0.28f * w,
                RadiusY = 0.09f * h,
                Rotation = 0.02f,
                PrimaryColor = CartographyColor.EmeraldGreen,
                FeatureDescriptions = new List<string>
                {
                    "主脉50%+支脉35%+孤峰15%立体山系",
                    "高耸雪峰与幽深雪水峡谷",
                    "北山天池高原湖水源地",
                    "一夫当关雁门天堑隘口",
                    "北麓矿业重城与仙道福地"
                },
                ContainedSettlementNames = new List<string>
                {
                    "北麓铁府", "雁门雄关", "武当仙镇", "桃源幽坞", "剑阁险关"
                },
                Bounds = GenerateOvalPolygon(0.49f * w, 0.25f * h, 0.28f * w, 0.09f * h, 0.02f)
            },

            // ── 3. 西境云林 (WestForest) ──
            new()
            {
                Id = 3,
                Name = "西境云林",
                Title = "西境云杉苍翠林海",
                Type = NarrativeRegionType.ForestMass,
                Center = new PolyVec2(0.24 * w, 0.46 * h),
                RadiusX = 0.16f * w,
                RadiusY = 0.15f * h,
                Rotation = 0.10f,
                PrimaryColor = CartographyColor.EmeraldGreen,
                FeatureDescriptions = new List<string>
                {
                    "核心40%+林缘40%+草甸20%三级梯度",
                    "玉溪穿越林谷滋润平原",
                    "林间商道与林业重郡"
                },
                ContainedSettlementNames = new List<string>
                {
                    "翠微古郡"
                },
                Bounds = GenerateOvalPolygon(0.24f * w, 0.46f * h, 0.16f * w, 0.15f * h, 0.10f)
            },

            // ── 4. 太古青岚 (EastAncientForest) ──
            new()
            {
                Id = 4,
                Name = "太古青岚",
                Title = "东部古老林海与遗迹",
                Type = NarrativeRegionType.ForestMass,
                Center = new PolyVec2(0.76 * w, 0.42 * h),
                RadiusX = 0.16f * w,
                RadiusY = 0.15f * h,
                Rotation = -0.15f,
                PrimaryColor = CartographyColor.DeepForest,
                FeatureDescriptions = new List<string>
                {
                    "疏离三组水墨古老林簇",
                    "腹地留白空坪开阔通透",
                    "上古苍灵神殿遗迹",
                    "灵泉清修幽潭"
                },
                ContainedSettlementNames = new List<string>
                {
                    "太古神殿", "青岚道观"
                },
                Bounds = GenerateOvalPolygon(0.76f * w, 0.42f * h, 0.16f * w, 0.15f * h, -0.15f)
            },

            // ── 5. 中原天府 (CentralPlains) ──
            new()
            {
                Id = 5,
                Name = "中原天府",
                Title = "大陆文明中枢大平原",
                Type = NarrativeRegionType.PlainsBasin,
                Center = new PolyVec2(0.50 * w, 0.50 * h),
                RadiusX = 0.30f * w,
                RadiusY = 0.18f * h,
                Rotation = 0.0f,
                PrimaryColor = CartographyColor.EmeraldGreen,
                FeatureDescriptions = new List<string>
                {
                    "占大陆25%~30%的核心平原",
                    "天水九曲干流穿行环抱",
                    "碧玉湖与清平渚水泽点缀",
                    "官道沿江纵横密布水浇田网",
                    "神都王都坐镇天下之枢"
                },
                ContainedSettlementNames = new List<string>
                {
                    "神京天都", "江陵大都", "丰泽古郡", "浔阳古埠", "杏花古村", "枫林晚渡"
                },
                Bounds = GenerateOvalPolygon(0.50f * w, 0.50f * h, 0.30f * w, 0.18f * h, 0.0f)
            },

            // ── 6. 狂沙金墟 (SouthwestDesert) ──
            new()
            {
                Id = 6,
                Name = "狂沙金墟",
                Title = "西南瀚海流沙与绿洲",
                Type = NarrativeRegionType.DesertField,
                Center = new PolyVec2(0.28 * w, 0.74 * h),
                RadiusX = 0.20f * w,
                RadiusY = 0.16f * h,
                Rotation = 0.15f,
                PrimaryColor = CartographyColor.SandyOchre,
                FeatureDescriptions = new List<string>
                {
                    "山麓-荒边-核心-荒滩四层过渡",
                    "顺风向新月沙垄走廊",
                    "鸣沙清泉绿洲与金沙古堡",
                    "盐湖微地貌与风蚀雅丹石丘",
                    "丝路驼铃孤驿"
                },
                ContainedSettlementNames = new List<string>
                {
                    "金沙古堡", "沙洲新城"
                },
                Bounds = GenerateOvalPolygon(0.28f * w, 0.74f * h, 0.20f * w, 0.16f * h, 0.15f)
            },

            // ── 7. 南部云梦 (SouthernWetlands) ──
            new()
            {
                Id = 7,
                Name = "南部云梦",
                Title = "南部水乡泽国与湿地",
                Type = NarrativeRegionType.SouthernWetlands,
                Center = new PolyVec2(0.56 * w, 0.70 * h),
                RadiusX = 0.18f * w,
                RadiusY = 0.13f * h,
                Rotation = -0.08f,
                PrimaryColor = CartographyColor.ColdBlue,
                FeatureDescriptions = new List<string>
                {
                    "连绵云梦大泽与千汊河网",
                    "茂密芦苇荡与水乡泽国",
                    "姑苏水郡小桥流水",
                    "蚕桑渔猎之乡"
                },
                ContainedSettlementNames = new List<string>
                {
                    "姑苏水郡", "襄樊水寨"
                },
                Bounds = GenerateOvalPolygon(0.56f * w, 0.70f * h, 0.18f * w, 0.13f * h, -0.08f)
            },

            // ── 8. 东南沧溟 (SoutheastHarborBay) ──
            new()
            {
                Id = 8,
                Name = "东南沧溟",
                Title = "海河交汇避风良港",
                Type = NarrativeRegionType.CoastalBay,
                Center = new PolyVec2(0.78 * w, 0.76 * h),
                RadiusX = 0.18f * w,
                RadiusY = 0.15f * h,
                Rotation = -0.10f,
                PrimaryColor = CartographyColor.ColdBlue,
                FeatureDescriptions = new List<string>
                {
                    "大江终点宽阔三角洲入海口",
                    "半月良湾天然避风海港",
                    "万石海舶与千帆竞渡沧海龙津"
                },
                ContainedSettlementNames = new List<string>
                {
                    "临海沧津", "渔歌泊"
                },
                Bounds = GenerateOvalPolygon(0.78f * w, 0.76f * h, 0.18f * w, 0.15f * h, -0.10f)
            }
        };

        return regions;
    }

    /// <summary>
    /// 计算指定世界坐标点在特定叙事区域中的影响权重 (0.0 ~ 1.0)。
    /// 考虑中心距离、轴半径、旋转角及非线性衰减。
    /// </summary>
    public static float GetRegionInfluence(PolyVec2 pos, RegionData region)
    {
        var dx = (float)(pos.X - region.Center.X);
        var dy = (float)(pos.Y - region.Center.Y);
        var cos = MathF.Cos(-region.Rotation);
        var sin = MathF.Sin(-region.Rotation);
        var lx = dx * cos - dy * sin;
        var ly = dx * sin + dy * cos;

        var normSq = (lx / region.RadiusX) * (lx / region.RadiusX) +
                     (ly / region.RadiusY) * (ly / region.RadiusY);

        if (normSq >= 1.0f) return 0.0f;

        var dist = MathF.Sqrt(normSq);
        // 核心区 (d < 0.6) 保持 1.0，边缘 (0.6 ~ 1.0) 平滑衰减
        if (dist <= 0.60f) return 1.0f;
        var t = (dist - 0.60f) / 0.40f;
        return 1.0f - (t * t * (3.0f - 2.0f * t)); // smoothstep
    }

    /// <summary>
    /// 查找指定世界坐标点所属的主要叙事区域。
    /// </summary>
    public static RegionData? FindDominantRegion(PolyVec2 pos, IReadOnlyList<RegionData> regions)
    {
        RegionData? best = null;
        var maxWeight = 0.0f;

        foreach (var r in regions)
        {
            var w = GetRegionInfluence(pos, r);
            if (w > maxWeight)
            {
                maxWeight = w;
                best = r;
            }
        }

        return best;
    }

    private static List<PolyVec2> GenerateOvalPolygon(float cx, float cy, float rx, float ry, float rot, int samples = 32)
    {
        var pts = new List<PolyVec2>(samples);
        var cos = MathF.Cos(rot);
        var sin = MathF.Sin(rot);

        for (var i = 0; i < samples; i++)
        {
            var theta = (i / (float)samples) * MathF.PI * 2.0f;
            var ox = MathF.Cos(theta) * rx;
            var oy = MathF.Sin(theta) * ry;

            var px = cx + ox * cos - oy * sin;
            var py = cy + ox * sin + oy * cos;
            pts.Add(new PolyVec2(px, py));
        }

        return pts;
    }
}
