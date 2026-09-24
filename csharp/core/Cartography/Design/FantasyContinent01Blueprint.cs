using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 东方幻想大陆标准构图样板蓝图 V7.0 骨架重构版（FantasyContinent01）。
/// 
/// 遵循“中央文明河谷 + 四方生态区 + 环形山脉屏障”地理逻辑架构：
/// 1. 【北境雪岭】：世界最高山系，河流与圣湖源头，打破单线山墙，重构为三大错落雪峰群（西雪岭、天脊极顶、东雪岭），预留雪水峡口与雁门天险；
/// 2. 【中央神圣河谷】：大陆文明中枢核心，雪水发源 $\to$ 中央圣湖（灵渊天池） $\to$ 神都圣城 $\to$ 穿行平原大河 $\to$ 沧津海港，两侧密布水浇田网；
/// 3. 【东方古森】：太古青岚古森，降低内部 50% 重复林簇，分化为密集核心古林、疏林草甸边缘与林间空地（太古神殿巨型遗迹）；
/// 4. 【西南荒漠】：三层生态过渡：流动金沙沙丘 $\to$ 戈壁砾石滩 $\to$ 红砂断崖峡谷（南屏峡谷台地）与大漠金墟；
/// 5. 【南方海岸】：南荒半月深凹海湾、临海沧津大港与外海仙岛；
/// 6. 【城市因果律】：20座战略聚落严格满足至少两项地理因果条件（水源、地形、资源、交通）。
/// </summary>
public static class FantasyContinent01Blueprint
{
    public static MapBlueprint Create()
    {
        return new MapBlueprint
        {
            Name = "FantasyContinent01",
            Description = "神州九域·东方幻想生态骨架重构蓝图 V7.0",

            // ── 0. 宏观生态大区规划（中央文明河谷 + 四方生态区 + 环形山脉屏障） ──
            Regions = new List<RegionBlueprint>
            {
                new() { Id = 1, Name = "北境雪岭", EcologyType = RegionEcologyType.PolarTundra, Center = new PolyVec2(0.50, 0.12), ExtentRadius = 0.32f, Palette = CartographyColor.MistIvory, Description = "极北高寒雪岭与冰川源头，天然屏障" },
                new() { Id = 2, Name = "苍冥天脊", EcologyType = RegionEcologyType.MountainHighland, Center = new PolyVec2(0.49, 0.23), ExtentRadius = 0.26f, Palette = CartographyColor.EmeraldGreen, Description = "北方主山系，三组雪峰峰群错落，天池峡谷源流" },
                new() { Id = 3, Name = "西境云林", EcologyType = RegionEcologyType.WesternForest, Center = new PolyVec2(0.24, 0.46), ExtentRadius = 0.18f, Palette = CartographyColor.EmeraldGreen, Description = "西境云杉林海，核心-林缘-草甸三级梯度" },
                new() { Id = 4, Name = "太古青岚", EcologyType = RegionEcologyType.AncientForest, Center = new PolyVec2(0.76, 0.42), ExtentRadius = 0.18f, Palette = CartographyColor.DeepForest, Description = "东部古老林海，疏离三簇，留白空地与太古神殿巨型遗迹" },
                new() { Id = 5, Name = "中原天府", EcologyType = RegionEcologyType.CentralPlains, Center = new PolyVec2(0.50, 0.48), ExtentRadius = 0.30f, Palette = CartographyColor.EmeraldGreen, Description = "中央神圣河谷文明核心，圣湖圣城、水网梯田草浪" },
                new() { Id = 6, Name = "狂沙金墟", EcologyType = RegionEcologyType.AridDesert, Center = new PolyVec2(0.28, 0.74), ExtentRadius = 0.22f, Palette = CartographyColor.SandyOchre, Description = "西南大荒沙漠生态带，沙丘-戈壁-峡谷三层过渡与大漠金墟" },
                new() { Id = 7, Name = "南部云梦", EcologyType = RegionEcologyType.SouthernWetlands, Center = new PolyVec2(0.56, 0.70), ExtentRadius = 0.18f, Palette = CartographyColor.ColdBlue, Description = "南部湿地泽国，湖泽群与芦苇水网" },
                new() { Id = 8, Name = "东南沧溟", EcologyType = RegionEcologyType.CoastalBay, Center = new PolyVec2(0.78, 0.76), ExtentRadius = 0.22f, Palette = CartographyColor.ColdBlue, Description = "半月深凹海港巨湾，大江入海汇流，外海岛屿" }
            },

            // ── 1. 结构化山脉空间骨架（打破单线山墙，呈现三组雪峰群 + 侧脉 + 荒漠峡谷） ──
            MountainRanges = new List<MountainRangeDefinition>
            {
                // 1.1 北方主山系·苍冥天脊：三大雪峰群（西峰群、天脊极顶天下第一峰、东峰群），留出天池冰川峡谷与雁门险关
                new()
                {
                    Id = 1,
                    Name = "苍冥天脊",
                    Description = "北方主山脊，天下第一峰巍峨擎天，三组雪峰峰群开合，屏护北境并孕育高山圣湖。",
                    DividingRegionA = "北境雪岭",
                    DividingRegionB = "中原天府",
                    StartPoint = new PolyVec2(0.32, 0.25),
                    ControlPoints = new List<PolyVec2>
                    {
                        new(0.40, 0.22), // 西雪峰群
                        new(0.49, 0.20), // 中天天脊极顶（天下第一峰核心中枢）
                        new(0.58, 0.23)  // 东雪峰群
                    },
                    EndPoint = new PolyVec2(0.66, 0.25),
                    RidgeWidth = 42.0f,
                    MajorPeakRatios = new List<float> { 0.18f, 0.48f, 0.78f }, // 0.18f 西雪峰群, 0.48f 天下第一峰极顶, 0.78f 东雪峰群
                    PassRatios = new List<float> { 0.32f, 0.62f }, // 0.32f 天池冰川峡口、0.62f 雁门雄关
                    SpurCount = 4,
                    SpurLength = 42.0f,
                    HasSnowCap = true,
                    Palette = CartographyColor.EmeraldGreen
                },
                // 1.2 支脉A·西雁荡侧脉：向西南舒展，与西境森林交错，护卫雪谷源头
                new()
                {
                    Id = 2,
                    Name = "雁荡侧脉",
                    Description = "主脉西段向西南延伸的纵深侧脉，形成高山雪水峡谷。",
                    DividingRegionA = "苍冥天脊",
                    DividingRegionB = "西境云林",
                    StartPoint = new PolyVec2(0.33, 0.26),
                    ControlPoints = new List<PolyVec2>
                    {
                        new(0.28, 0.32)
                    },
                    EndPoint = new PolyVec2(0.24, 0.38),
                    RidgeWidth = 32.0f,
                    MajorPeakRatios = new List<float> { 0.45f },
                    PassRatios = new List<float>(),
                    SpurCount = 2,
                    SpurLength = 35.0f,
                    HasSnowCap = false,
                    Palette = CartographyColor.EmeraldGreen
                },
                // 1.3 支脉B·东青翠侧脉：向东南舒展，与东部古森林交汇
                new()
                {
                    Id = 3,
                    Name = "青翠云岭",
                    Description = "主脉东段向东南延伸的侧脉，群峰连绵，屏护古老森林北麓。",
                    DividingRegionA = "苍冥天脊",
                    DividingRegionB = "太古青岚",
                    StartPoint = new PolyVec2(0.65, 0.26),
                    ControlPoints = new List<PolyVec2>
                    {
                        new(0.69, 0.31)
                    },
                    EndPoint = new PolyVec2(0.72, 0.36),
                    RidgeWidth = 30.0f,
                    MajorPeakRatios = new List<float> { 0.50f },
                    PassRatios = new List<float>(),
                    SpurCount = 2,
                    SpurLength = 32.0f,
                    HasSnowCap = false,
                    Palette = CartographyColor.EmeraldGreen
                },
                // 1.4 西南荒漠峡谷（红砂绝壁与风蚀断崖台地）
                new()
                {
                    Id = 4,
                    Name = "南屏红砂峡谷",
                    Description = "西南大荒边缘红砂绝壁断崖台地，截断金沙大漠与西部丘陵，形成荒漠山险要地带。",
                    DividingRegionA = "西境云林",
                    DividingRegionB = "狂沙金墟",
                    StartPoint = new PolyVec2(0.18, 0.54),
                    ControlPoints = new List<PolyVec2>
                    {
                        new(0.22, 0.62)
                    },
                    EndPoint = new PolyVec2(0.25, 0.70),
                    RidgeWidth = 30.0f,
                    MajorPeakRatios = new List<float> { 0.50f },
                    PassRatios = new List<float>(),
                    SpurCount = 2,
                    SpurLength = 30.0f,
                    HasSnowCap = false,
                    Palette = CartographyColor.SandyOchre
                }
            },

            // 兼容字段：MountainSpines
            MountainSpines = new List<MountainSpineBlueprint>
            {
                new()
                {
                    Id = 1,
                    Name = "苍冥天脊",
                    StartPoint = new PolyVec2(0.32, 0.25),
                    ControlPoints = new List<PolyVec2> { new(0.40, 0.22), new(0.49, 0.20), new(0.58, 0.23) },
                    EndPoint = new PolyVec2(0.66, 0.25),
                    SpineWidth = 42.0f,
                    MajorPeakRatios = new List<float> { 0.18f, 0.48f, 0.78f },
                    PassRatios = new List<float> { 0.32f, 0.62f },
                    HasSnowCap = true,
                    Palette = CartographyColor.EmeraldGreen,
                    Rank = 3
                },
                new()
                {
                    Id = 2,
                    Name = "南屏红砂峡谷",
                    StartPoint = new PolyVec2(0.18, 0.54),
                    ControlPoints = new List<PolyVec2> { new(0.22, 0.62) },
                    EndPoint = new PolyVec2(0.25, 0.70),
                    SpineWidth = 30.0f,
                    MajorPeakRatios = new List<float> { 0.50f },
                    PassRatios = new List<float>(),
                    HasSnowCap = false,
                    Palette = CartographyColor.SandyOchre,
                    Rank = 2
                }
            },

            // ── 2. 结构化树状水系（北山起源 + 中央圣湖 + 环抱圣都 + 平原九曲 + 沧津海港） ──
            RiverNetworks = new List<RiverNetworkDefinition>
            {
                new()
                {
                    Id = 1,
                    Name = "天水龙江水系",
                    // 主河发源于雪山深谷峡口
                    SourcePoint = new PolyVec2(0.43, 0.20),
                    MainWaypoints = new List<PolyVec2>
                    {
                        new(0.45, 0.26), // 穿出雪水峡谷
                        new(0.47, 0.33), // 中央圣湖（灵渊天池，汇聚冰川融雪之神圣大湖）
                        new(0.48, 0.39), // 出圣湖南下河段
                        new(0.49, 0.44), // 九曲向西环抱神都圣城之北与西侧
                        new(0.53, 0.49), // 穿行京畿梯田水网与沃野平原
                        new(0.58, 0.54), // 中游江陵大都两江汇流处
                        new(0.66, 0.62), // 滋养南部湿地边缘
                        new(0.72, 0.69)  // 下游宽阔河口段
                    },
                    MouthPoint = new PolyVec2(0.78, 0.76), // 注入东南临海沧津半月海湾巨港
                    SourceWidth = 2.4f,
                    MouthWidth = 10.0f,
                    Branches = new List<RiverBranchDefinition>
                    {
                        // 支流1：西林玉溪（发源自西境云杉林海，穿丰泽粮仓，汇入平原主江）
                        new()
                        {
                            Name = "西林玉溪",
                            SourcePoint = new PolyVec2(0.22, 0.44),
                            Waypoints = new List<PolyVec2>
                            {
                                new(0.30, 0.47),
                                new(0.38, 0.48), // 滋润丰泽粮仓
                                new(0.44, 0.47)
                            },
                            ConfluencePoint = new PolyVec2(0.49, 0.46), // 汇入神都南侧主江
                            WidthScale = 0.55f
                        },
                        // 支流2：东境青岚溪（发源自东部古森林碧落幽潭，向西流经古道，在江陵大都汇入主江）
                        new()
                        {
                            Name = "青岚溪",
                            SourcePoint = new PolyVec2(0.71, 0.36),
                            Waypoints = new List<PolyVec2>
                            {
                                new(0.68, 0.45),
                                new(0.62, 0.50)
                            },
                            ConfluencePoint = new PolyVec2(0.58, 0.54),
                            WidthScale = 0.50f
                        },
                        // 支流3：云梦泽支流（南部湿地与大江串联水网）
                        new()
                        {
                            Name = "云梦汊河",
                            SourcePoint = new PolyVec2(0.54, 0.66),
                            Waypoints = new List<PolyVec2>
                            {
                                new(0.60, 0.64)
                            },
                            ConfluencePoint = new PolyVec2(0.66, 0.62),
                            WidthScale = 0.45f
                        }
                    },
                    Lakes = new List<PolyVec2>
                    {
                        new(0.47, 0.33), // 中央圣湖 (灵渊天池 / Sacred Lake)
                        new(0.53, 0.50), // 龙吟洲 (River Islet - 江心绿洲)
                        new(0.45, 0.46), // 碧玉湖 (Central Plains Lake)
                        new(0.56, 0.55), // 清平渚 (Middle Plains Lake)
                        new(0.71, 0.36), // 碧落幽潭 (Eastern Ancient Forest Lake)
                        new(0.58, 0.68), // 云梦大泽 (Southern Wetland Lake)
                        new(0.24, 0.78), // 金沙盐湖 (Desert Salt Lake)
                        new(0.32, 0.74), // 鸣沙泉绿洲 (Desert Oasis 1)
                        new(0.25, 0.68), // 月牙泉绿洲 (Desert Oasis 2)
                        new(0.34, 0.65)  // 瀚海清泉绿洲 (Desert Oasis 3)
                    },
                    Palette = CartographyColor.ColdBlue
                }
            },

            // 兼容字段：RiverCorridors
            RiverCorridors = new List<RiverCorridorBlueprint>
            {
                new()
                {
                    Id = 1,
                    Name = "天水龙江",
                    SourcePoint = new PolyVec2(0.43, 0.20),
                    Waypoints = new List<PolyVec2>
                    {
                        new(0.45, 0.26),
                        new(0.47, 0.33), // 中央圣湖
                        new(0.48, 0.39),
                        new(0.49, 0.44), // 神都圣城
                        new(0.53, 0.49),
                        new(0.58, 0.54), // 江陵大都
                        new(0.66, 0.62),
                        new(0.72, 0.69)
                    },
                    MouthPoint = new PolyVec2(0.78, 0.76),
                    WidthScale = 1.6f
                }
            },

            // ── 3. 结构化体块森林（西林梯度 + 东林收缩30%与三级柔和林缘） ──
            ForestMasses = new List<ForestMassDefinition>
            {
                // 3.1 东部太古青岚古森林：核心收缩30%，林缘过渡柔和展开，太古神殿留白
                new()
                {
                    Id = 1,
                    Name = "太古青岚林海",
                    Description = "东部古老林海，核心收缩30%，外缘呈现疏林草甸三级生态梯度，深藏太古神殿与碧落幽潭。",
                    Center = new PolyVec2(0.76, 0.42),
                    RadiusX = 0.12f, // 核心收缩 30%
                    RadiusY = 0.11f,
                    Rotation = -0.15f,
                    CoreDensity = 0.45f,
                    WoodlandDensity = 0.70f,
                    ShrubDensity = 0.50f,
                    MeadowDensity = 0.25f,
                    DensityCurve = ForestDensityCurveType.SmoothStep,
                    Clearings = new List<ForestClearing>
                    {
                        new() { Position = new PolyVec2(0.76, 0.42), Radius = 36.0f, Name = "太古神殿空坪" },
                        new() { Position = new PolyVec2(0.71, 0.36), Radius = 28.0f, Name = "碧落幽潭" },
                        new() { Position = new PolyVec2(0.80, 0.46), Radius = 24.0f, Name = "幽栖古隙" }
                    },
                    Palette = CartographyColor.DeepForest
                },
                // 3.2 西境云林：核心40% + 林缘40% + 草甸20% 梯度
                new()
                {
                    Id = 2,
                    Name = "西境云林",
                    Description = "西境云杉苍翠林海，三级梯度自然过渡，内辟河谷古道与林业重城。",
                    Center = new PolyVec2(0.24, 0.46),
                    RadiusX = 0.14f,
                    RadiusY = 0.13f,
                    Rotation = 0.10f,
                    CoreDensity = 0.80f,
                    WoodlandDensity = 0.60f,
                    ShrubDensity = 0.30f,
                    MeadowDensity = 0.0f,
                    DensityCurve = ForestDensityCurveType.SmoothStep,
                    Clearings = new List<ForestClearing>
                    {
                        new() { Position = new PolyVec2(0.28, 0.47), Radius = 28.0f, Name = "翠微谷" }
                    },
                    Palette = CartographyColor.EmeraldGreen
                }
            },

            // 兼容字段：ForestZones
            ForestZones = new List<ForestZoneBlueprint>
            {
                new()
                {
                    Id = 1,
                    Name = "太古青岚林海",
                    Center = new PolyVec2(0.76, 0.42),
                    RadiusX = 0.12f,
                    RadiusY = 0.11f,
                    Rotation = -0.15f,
                    CoreDensity = 0.45f,
                    FadeMargin = 0.60f,
                    Palette = CartographyColor.DeepForest
                },
                new()
                {
                    Id = 2,
                    Name = "西境云林",
                    Center = new PolyVec2(0.24, 0.46),
                    RadiusX = 0.14f,
                    RadiusY = 0.13f,
                    Rotation = 0.10f,
                    CoreDensity = 0.80f,
                    FadeMargin = 0.40f,
                    Palette = CartographyColor.EmeraldGreen
                }
            },

            // ── 4. 中央「河谷文明带」细节层（消除空洞，密集梯田水网、湖泊、草浪、孤峰） ──
            PlainsDetail = new PlainsDetailDefinition
            {
                Id = 1,
                Name = "中原河谷文明大平原",
                HeartlandCenter = new PolyVec2(0.50, 0.50),
                AgriculturalZones = new List<AgriculturalZoneDefinition>
                {
                    new()
                    {
                        Name = "神都京畿御苑梯田水网",
                        Center = new PolyVec2(0.49, 0.47),
                        RadiusX = 0.09f,
                        RadiusY = 0.08f,
                        PatchCount = 24
                    },
                    new()
                    {
                        Name = "天水中游粮仓沃野带",
                        Center = new PolyVec2(0.55, 0.52),
                        RadiusX = 0.09f,
                        RadiusY = 0.07f,
                        PatchCount = 22
                    },
                    new()
                    {
                        Name = "江汉平原稻作冲积带",
                        Center = new PolyVec2(0.62, 0.58),
                        RadiusX = 0.08f,
                        RadiusY = 0.06f,
                        PatchCount = 18
                    },
                    new()
                    {
                        Name = "西林丰泽农耕区",
                        Center = new PolyVec2(0.39, 0.48),
                        RadiusX = 0.09f,
                        RadiusY = 0.06f,
                        PatchCount = 20
                    },
                    new()
                    {
                        Name = "汊河水乡滨江农耕带",
                        Center = new PolyVec2(0.45, 0.52),
                        RadiusX = 0.07f,
                        RadiusY = 0.06f,
                        PatchCount = 16
                    }
                },
                SolitaryKnolls = new List<PolyVec2>
                {
                    new(0.40, 0.34), // 北山脚孤峰
                    new(0.58, 0.33), // 武当独秀峰
                    new(0.38, 0.46), // 平原西孤峰
                    new(0.54, 0.42), // 京畿北陵丘
                    new(0.62, 0.46), // 东平原小丘
                    new(0.46, 0.56), // 清平渚畔丘
                    new(0.66, 0.54)  // 江汉界石峰
                },
                MirrorLakes = new List<PolyVec2>
                {
                    new(0.45, 0.46), // 碧玉湖
                    new(0.56, 0.55), // 清平渚
                    new(0.58, 0.68)  // 云梦大泽
                },
                GrassWaveZones = new List<PolyVec2>
                {
                    new(0.36, 0.44),
                    new(0.42, 0.52),
                    new(0.46, 0.42),
                    new(0.52, 0.60),
                    new(0.60, 0.48),
                    new(0.64, 0.56),
                    new(0.50, 0.54),
                    new(0.42, 0.60)
                }
            },

            // ── 5. 西南大荒漠生态区（扩充18%，死寂金沙、顺风沙丘、风蚀雅丹台地、盐碱地） ──
            DesertFields = new List<DesertFieldDefinition>
            {
                new()
                {
                    Id = 1,
                    Name = "狂沙金墟",
                    Center = new PolyVec2(0.27, 0.73),
                    RadiusX = 0.26f, // 扩充 18% 面积
                    RadiusY = 0.20f,
                    WindAngle = 0.35f, // 统一顺风向 (>>>>>)
                    CorridorCount = 4,
                    Oases = new List<PolyVec2>
                    {
                        new(0.32, 0.74), // 鸣沙绿洲 (金沙古堡)
                        new(0.25, 0.68), // 月牙泉
                        new(0.34, 0.65)  // 瀚海清泉 (沙洲新城贸易节点)
                    },
                    Palette = CartographyColor.SandyOchre
                }
            },

            // 兼容字段：DesertZones
            DesertZones = new List<DesertZoneBlueprint>
            {
                new()
                {
                    Id = 1,
                    Name = "狂沙金墟",
                    Center = new PolyVec2(0.27, 0.73),
                    RadiusX = 0.26f,
                    RadiusY = 0.20f,
                    WindAngle = 0.35f,
                    DuneDensity = 1.0f,
                    Palette = CartographyColor.SandyOchre
                }
            },

            // ── 6. 严谨地理因果战略聚落定义（精准20座：1王都 + 3大城 + 3港口 + 8城镇 + 5村落/圣所） ──
            SettlementDefinitions = new List<SettlementDefinition>
            {
                // 1. 王都 (1座)：中央河谷文明中枢
                new()
                {
                    Id = 1,
                    Name = "神京天都",
                    Tier = CartographySettlementTier.Capital,
                    AnchorType = GeographicAnchorType.HeartlandCapital,
                    Position = new PolyVec2(0.50, 0.46),
                    Importance = 4,
                    ShowLabel = true,
                    Description = "天下帝京，万邦来朝，依凭圣湖南下天水九曲，中原之枢。"
                },

                // 2. 州府大城 (3座)：商贸枢纽、平原粮仓、北塞矿郭
                new()
                {
                    Id = 2,
                    Name = "江陵大都",
                    Tier = CartographySettlementTier.City,
                    AnchorType = GeographicAnchorType.RiverConfluence,
                    Position = new PolyVec2(0.58, 0.54),
                    Importance = 3,
                    ShowLabel = true,
                    Description = "两江汇流之枢，楚天重镇，商贾云集之平原商贸大都会。"
                },
                new()
                {
                    Id = 3,
                    Name = "丰泽古郡",
                    Tier = CartographySettlementTier.City,
                    AnchorType = GeographicAnchorType.PlainAgriculturalCity,
                    Position = new PolyVec2(0.38, 0.48),
                    Importance = 3,
                    ShowLabel = true,
                    Description = "平原西境沃土粮仓，阡陌纵横，万顷良田之农业重都。"
                },
                new()
                {
                    Id = 4,
                    Name = "北麓铁府",
                    Tier = CartographySettlementTier.City,
                    AnchorType = GeographicAnchorType.MountainMiningCity,
                    Position = new PolyVec2(0.38, 0.30),
                    Importance = 3,
                    ShowLabel = true,
                    Description = "北山脚下富矿要郭，熔炉昼夜，百炼精金重镇。"
                },

                // 3. 港口城市 (3座)：远洋巨港、江河渡口、外海渔港
                new()
                {
                    Id = 5,
                    Name = "临海沧津",
                    Tier = CartographySettlementTier.Harbor,
                    AnchorType = GeographicAnchorType.NaturalBayHarbor,
                    Position = new PolyVec2(0.78, 0.76),
                    Importance = 3,
                    ShowLabel = true,
                    Description = "东南半月海口巨港，通洋达海，万石海舶之要津。"
                },
                new()
                {
                    Id = 6,
                    Name = "浔阳古埠",
                    Tier = CartographySettlementTier.Harbor,
                    AnchorType = GeographicAnchorType.RiverFerryPort,
                    Position = new PolyVec2(0.51, 0.43),
                    Importance = 3,
                    ShowLabel = true,
                    Description = "大江中游渡口大港，千帆竞发，水陆通衢。"
                },
                new()
                {
                    Id = 7,
                    Name = "渔歌泊",
                    Tier = CartographySettlementTier.Harbor,
                    AnchorType = GeographicAnchorType.CoastalHaven,
                    Position = new PolyVec2(0.84, 0.72),
                    Importance = 3,
                    ShowLabel = true,
                    Description = "外海避风渔商良港，日落千舟，碧波连天。"
                },

                // 4. 要冲城镇 (8座)：咽喉关隘、林缘集镇、绿洲城镇、水乡水寨
                new()
                {
                    Id = 8,
                    Name = "雁门雄关",
                    Tier = CartographySettlementTier.Town,
                    AnchorType = GeographicAnchorType.MountainPass,
                    Position = new PolyVec2(0.55, 0.24),
                    Importance = 2,
                    ShowLabel = true,
                    Description = "一夫当关，万夫莫开，苍冥雪峰隘口第一天险。"
                },
                new()
                {
                    Id = 9,
                    Name = "翠微古郡",
                    Tier = CartographySettlementTier.Town,
                    AnchorType = GeographicAnchorType.ForestMarginCity,
                    Position = new PolyVec2(0.30, 0.47),
                    Importance = 2,
                    ShowLabel = true,
                    Description = "西境森林东缘林业要镇，松涛万顷，木料药材集散之所。"
                },
                new()
                {
                    Id = 10,
                    Name = "金沙古堡",
                    Tier = CartographySettlementTier.Town,
                    AnchorType = GeographicAnchorType.OasisCrossroad,
                    Position = new PolyVec2(0.28, 0.74),
                    Importance = 2,
                    ShowLabel = true,
                    Description = "大荒咽喉，丝路清泉，往来客商歇足驼铃重镇。"
                },
                new()
                {
                    Id = 11,
                    Name = "沙洲新城",
                    Tier = CartographySettlementTier.Town,
                    AnchorType = GeographicAnchorType.OasisTradeCity,
                    Position = new PolyVec2(0.34, 0.65),
                    Importance = 2,
                    ShowLabel = true,
                    Description = "荒漠绿洲新兴商贸节点，瀚海清泉畔商旅交汇之邑。"
                },
                new()
                {
                    Id = 12,
                    Name = "姑苏水郡",
                    Tier = CartographySettlementTier.Town,
                    AnchorType = GeographicAnchorType.WetlandWaterTown,
                    Position = new PolyVec2(0.54, 0.64),
                    Importance = 2,
                    ShowLabel = true,
                    Description = "南部云梦湿地水乡重镇，小桥流水，千汊万泊水城。"
                },
                new()
                {
                    Id = 13,
                    Name = "武当仙镇",
                    Tier = CartographySettlementTier.Town,
                    AnchorType = GeographicAnchorType.SacredTown,
                    Position = new PolyVec2(0.59, 0.30),
                    Importance = 2,
                    ShowLabel = true,
                    Description = "东雪岭独秀峰下道家集市，药香满谷，钟磬相闻。"
                },
                new()
                {
                    Id = 14,
                    Name = "剑阁险关",
                    Tier = CartographySettlementTier.Town,
                    AnchorType = GeographicAnchorType.MountainFortress,
                    Position = new PolyVec2(0.26, 0.36),
                    Importance = 2,
                    ShowLabel = true,
                    Description = "西境支脉险道雄关，连山绝壁，锁钥西陲。"
                },
                new()
                {
                    Id = 15,
                    Name = "襄樊水寨",
                    Tier = CartographySettlementTier.Town,
                    AnchorType = GeographicAnchorType.RiverGarrison,
                    Position = new PolyVec2(0.64, 0.60),
                    Importance = 2,
                    ShowLabel = true,
                    Description = "南部大江咽喉防务水寨，横锁江汉，水陆盘查之所。"
                },

                // 5. 乡村与圣所与巨型遗迹 (5座)：河谷文明村庄、巨型遗迹、道观、渡口
                new()
                {
                    Id = 16,
                    Name = "杏花古村",
                    Tier = CartographySettlementTier.Village,
                    AnchorType = GeographicAnchorType.AgriculturalVillage,
                    Position = new PolyVec2(0.46, 0.47),
                    Importance = 1,
                    ShowLabel = false,
                    Description = "神都京畿西郊沃野农庄，阡陌良田，酒旗斜挑。"
                },
                new()
                {
                    Id = 17,
                    Name = "太古神殿",
                    Tier = CartographySettlementTier.Village,
                    AnchorType = GeographicAnchorType.AncientRelicSite,
                    Position = new PolyVec2(0.76, 0.42),
                    Importance = 1,
                    ShowLabel = true,
                    Description = "古林深处上古苍灵巨型遗迹，苔生石阶，石柱巍然。"
                },
                new()
                {
                    Id = 18,
                    Name = "青岚道观",
                    Tier = CartographySettlementTier.Village,
                    AnchorType = GeographicAnchorType.SacredSanctuary,
                    Position = new PolyVec2(0.71, 0.36),
                    Importance = 1,
                    ShowLabel = false,
                    Description = "林深幽径，古刹清钟，青岚泉畔修士隐修仙居。"
                },
                new()
                {
                    Id = 19,
                    Name = "大漠金墟",
                    Tier = CartographySettlementTier.Village,
                    AnchorType = GeographicAnchorType.AncientRelicSite,
                    Position = new PolyVec2(0.24, 0.78),
                    Importance = 1,
                    ShowLabel = true,
                    Description = "西荒大漠深处黄沙半掩的上古失落都邑巨型遗迹，残垣断壁，石柱苍凉。"
                },
                new()
                {
                    Id = 20,
                    Name = "枫林晚渡",
                    Tier = CartographySettlementTier.Village,
                    AnchorType = GeographicAnchorType.FerryVillage,
                    Position = new PolyVec2(0.55, 0.52),
                    Importance = 1,
                    ShowLabel = false,
                    Description = "官道跨江津头小村，红叶荻花，渔舟唱晚。"
                }
            },

            // 兼容字段：Settlements (精准映射20座战略聚落)
            Settlements = new List<StrategicSettlementBlueprint>
            {
                new() { Id = 1, Name = "神京天都", Tier = CartographySettlementTier.Capital, AnchorType = SettlementAnchorType.InlandCapital, Position = new PolyVec2(0.50, 0.46), Importance = 4, ShowLabel = true },
                new() { Id = 2, Name = "江陵大都", Tier = CartographySettlementTier.City, AnchorType = SettlementAnchorType.RiverConfluence, Position = new PolyVec2(0.58, 0.54), Importance = 3, ShowLabel = true },
                new() { Id = 3, Name = "丰泽古郡", Tier = CartographySettlementTier.City, AnchorType = SettlementAnchorType.PlainCrossroad, Position = new PolyVec2(0.38, 0.48), Importance = 3, ShowLabel = true },
                new() { Id = 4, Name = "北麓铁府", Tier = CartographySettlementTier.City, AnchorType = SettlementAnchorType.InlandCapital, Position = new PolyVec2(0.38, 0.30), Importance = 3, ShowLabel = true },
                new() { Id = 5, Name = "临海沧津", Tier = CartographySettlementTier.Harbor, AnchorType = SettlementAnchorType.NaturalBayHarbor, Position = new PolyVec2(0.78, 0.76), Importance = 3, ShowLabel = true },
                new() { Id = 6, Name = "浔阳古埠", Tier = CartographySettlementTier.Harbor, AnchorType = SettlementAnchorType.RiverConfluence, Position = new PolyVec2(0.51, 0.43), Importance = 3, ShowLabel = true },
                new() { Id = 7, Name = "渔歌泊", Tier = CartographySettlementTier.Harbor, AnchorType = SettlementAnchorType.NaturalBayHarbor, Position = new PolyVec2(0.84, 0.72), Importance = 3, ShowLabel = true },
                new() { Id = 8, Name = "雁门雄关", Tier = CartographySettlementTier.Town, AnchorType = SettlementAnchorType.MountainPass, Position = new PolyVec2(0.55, 0.24), Importance = 2, ShowLabel = true },
                new() { Id = 9, Name = "翠微古郡", Tier = CartographySettlementTier.Town, AnchorType = SettlementAnchorType.PlainCrossroad, Position = new PolyVec2(0.30, 0.47), Importance = 2, ShowLabel = true },
                new() { Id = 10, Name = "金沙古堡", Tier = CartographySettlementTier.Town, AnchorType = SettlementAnchorType.PlainCrossroad, Position = new PolyVec2(0.28, 0.74), Importance = 2, ShowLabel = true },
                new() { Id = 11, Name = "沙洲新城", Tier = CartographySettlementTier.Town, AnchorType = SettlementAnchorType.PlainCrossroad, Position = new PolyVec2(0.34, 0.65), Importance = 2, ShowLabel = true },
                new() { Id = 12, Name = "姑苏水郡", Tier = CartographySettlementTier.Town, AnchorType = SettlementAnchorType.RiverConfluence, Position = new PolyVec2(0.54, 0.64), Importance = 2, ShowLabel = true },
                new() { Id = 13, Name = "武当仙镇", Tier = CartographySettlementTier.Town, AnchorType = SettlementAnchorType.PlainCrossroad, Position = new PolyVec2(0.59, 0.30), Importance = 2, ShowLabel = true },
                new() { Id = 14, Name = "剑阁险关", Tier = CartographySettlementTier.Town, AnchorType = SettlementAnchorType.MountainPass, Position = new PolyVec2(0.26, 0.36), Importance = 2, ShowLabel = true },
                new() { Id = 15, Name = "襄樊水寨", Tier = CartographySettlementTier.Town, AnchorType = SettlementAnchorType.RiverConfluence, Position = new PolyVec2(0.64, 0.60), Importance = 2, ShowLabel = true },
                new() { Id = 16, Name = "杏花古村", Tier = CartographySettlementTier.Village, AnchorType = SettlementAnchorType.PlainCrossroad, Position = new PolyVec2(0.46, 0.47), Importance = 1, ShowLabel = false },
                new() { Id = 17, Name = "太古神殿", Tier = CartographySettlementTier.Village, AnchorType = SettlementAnchorType.PlainCrossroad, Position = new PolyVec2(0.76, 0.42), Importance = 1, ShowLabel = true },
                new() { Id = 18, Name = "青岚道观", Tier = CartographySettlementTier.Village, AnchorType = SettlementAnchorType.SacredSanctuary, Position = new PolyVec2(0.71, 0.36), Importance = 1, ShowLabel = false },
                new() { Id = 19, Name = "大漠金墟", Tier = CartographySettlementTier.Village, AnchorType = SettlementAnchorType.PlainCrossroad, Position = new PolyVec2(0.24, 0.78), Importance = 1, ShowLabel = true },
                new() { Id = 20, Name = "枫林晚渡", Tier = CartographySettlementTier.Village, AnchorType = SettlementAnchorType.RiverConfluence, Position = new PolyVec2(0.55, 0.52), Importance = 1, ShowLabel = false }
            },

            // ── 7. 资源服务型与冒险探索主通道交通网络 ──
            Highways = new List<TradeHighwayBlueprint>
            {
                // 1. 全大陆横贯冒险主线 (西关 -> 翠微 -> 丰泽 -> 神京 -> 浔阳 -> 江陵 -> 襄樊 -> 沧津)
                new() { Id = 1, Name = "剑阁栈道", FromSettlement = "剑阁险关", ToSettlement = "翠微古郡", HighwayTier = 2 },
                new() { Id = 2, Name = "西林运粮官道", FromSettlement = "翠微古郡", ToSettlement = "丰泽古郡", HighwayTier = 2 },
                new() { Id = 3, Name = "丰京平原大官道", FromSettlement = "丰泽古郡", ToSettlement = "神京天都", HighwayTier = 1 },
                new() { Id = 4, Name = "浔阳渡江津道", FromSettlement = "神京天都", ToSettlement = "浔阳古埠", HighwayTier = 1 },
                new() { Id = 5, Name = "天水京陵大官道", FromSettlement = "浔阳古埠", ToSettlement = "江陵大都", HighwayTier = 1 },
                new() { Id = 6, Name = "江汉水运驿道", FromSettlement = "江陵大都", ToSettlement = "襄樊水寨", HighwayTier = 1 },
                new() { Id = 7, Name = "通海金丝官道", FromSettlement = "襄樊水寨", ToSettlement = "临海沧津", HighwayTier = 1 },
                new() { Id = 8, Name = "沧海渔商官道", FromSettlement = "临海沧津", ToSettlement = "渔歌泊", HighwayTier = 2 },

                // 2. 北塞矿运与天子御道 (矿城 -> 雁门关 -> 国都)
                new() { Id = 9, Name = "塞北矿运驿道", FromSettlement = "北麓铁府", ToSettlement = "雁门雄关", HighwayTier = 2 },
                new() { Id = 10, Name = "天子御道", FromSettlement = "雁门雄关", ToSettlement = "神京天都", HighwayTier = 1 },

                // 3. 西荒丝绸之路 (神都 -> 绿洲商都 -> 荒漠古堡 -> 大漠金墟)
                new() { Id = 11, Name = "西荒丝路大道", FromSettlement = "神京天都", ToSettlement = "沙洲新城", HighwayTier = 2 },
                new() { Id = 12, Name = "大漠驼铃古道", FromSettlement = "沙洲新城", ToSettlement = "金沙古堡", HighwayTier = 2 },
                new() { Id = 13, Name = "大漠寻幽古径", FromSettlement = "金沙古堡", ToSettlement = "大漠金墟", HighwayTier = 2 },

                // 4. 水乡内河网道 (江陵 -> 姑苏)
                new() { Id = 14, Name = "云梦泽官道", FromSettlement = "江陵大都", ToSettlement = "姑苏水郡", HighwayTier = 2 },

                // 5. 仙岳朝圣道 (神都 -> 武当)
                new() { Id = 15, Name = "北岳朝圣道", FromSettlement = "神京天都", ToSettlement = "武当仙镇", HighwayTier = 2 },

                // 6. 古林遗迹幽径 (江陵 -> 青岚道观 -> 太古神殿)
                new() { Id = 16, Name = "青岚古林径", FromSettlement = "江陵大都", ToSettlement = "青岚道观", HighwayTier = 2 },
                new() { Id = 17, Name = "太古朝圣幽径", FromSettlement = "青岚道观", ToSettlement = "太古神殿", HighwayTier = 2 }
            }
        };
    }
}
