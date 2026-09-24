using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 地标与古地图规范符号生成器（LandmarkGenerator）。
/// 
/// 遵循古代舆图与东方幻想制图规范：
/// 1. 废弃复杂的城堡建筑模型，改用经典古地图符号体系：
///    - 王都 / 巨城：◎（双重同心圆，中嵌朱砂微核）
///    - 城镇 / 府县：□（端正细方框）
///    - 寻常村落：●（清秀实心小圆点）
///    - 扼要雄关：☲（关隘横杠堞文）
///    - 临水津渡：⚓（古典舟楫铁锚）
///    - 仙山洞府：⛩ / 宝塔微符
/// 2. 视觉降噪：尺寸整体缩减 30%，墨色柔和（#3a322a，Alpha 0.85），退居三级层级，绝不与山川争艳；
/// 3. 文字题名置于最顶层，配暖象牙色光晕（Halo），防遮挡。
/// </summary>
public static class LandmarkGenerator
{
    public static (List<LandmarkStyle> Landmarks, List<BrushInstruction> Brushes) GenerateLandmarks(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        IReadOnlyList<MegaTerrainRegion> megaRegions,
        IReadOnlyList<SettlementInfo> settlements,
        Dictionary<int, RegionStyle> regionStyles)
    {
        var landmarks = new List<LandmarkStyle>();
        var brushes = new List<BrushInstruction>();
        var nextId = 1;
        var seaLevel = options.SeaLevel;

        // 1. 文明古地图规范符号聚落（五级分化：Capital, City, Town, Harbor, Village）
        if (settlements != null && settlements.Count > 0)
        {
            // 选定天下国都（优先高等级首要聚落）
            var capitalSettlement = settlements.FirstOrDefault(s => s.Rank == SettlementRank.CityState)
                                    ?? settlements.FirstOrDefault(s => s.Rank == SettlementRank.Town)
                                    ?? settlements[0];

            foreach (var s in settlements)
            {
                var pt = s.Position;
                var isCapital = s == capitalSettlement;

                // 判断是否临海/临大水（津渡海港）
                var isCoastAdjacent = false;
                var start = geometry.CellNeighborStart[s.CellId];
                var end = geometry.CellNeighborStart[s.CellId + 1];
                for (var k = start; k < end; k++)
                {
                    var nb = geometry.CellNeighbors[k];
                    if (fields.Height[nb] <= seaLevel)
                    {
                        isCoastAdjacent = true;
                        break;
                    }
                }

                // 判断是否扼守险隘关口（临山地且四周多山）
                var isMountainAdjacent = fields.Landform[s.CellId] == (byte)LandformType.Mountain;
                var isPass = false;
                if (!isCapital && s.Rank != SettlementRank.CityState)
                {
                    var mtCount = 0;
                    for (var k = start; k < end; k++)
                    {
                        var nb = geometry.CellNeighbors[k];
                        var nblf = (LandformType)fields.Landform[nb];
                        if (nblf is LandformType.Mountain or LandformType.Hill) mtCount++;
                    }
                    if (mtCount >= 3 && isMountainAdjacent) isPass = true;
                }

                // 确定五级符号、图标类型、字号、尺寸与标签显示策略
                CartographySettlementTier tier;
                LandmarkType lType;
                string iconKey;
                int fontSz;
                float scale;
                int importance;
                bool showLabel;

                if (isCapital)
                {
                    // 1级：天下国都（◎ 双重同心圆 + 华盖飞旗）
                    tier = CartographySettlementTier.Capital;
                    lType = LandmarkType.Capital;
                    iconKey = "symbol_capital";
                    fontSz = 14;
                    scale = 0.88f;
                    importance = 4;
                    showLabel = true;
                }
                else if (s.Rank == SettlementRank.CityState)
                {
                    // 2级：州府大都会（□ 重郭城垣方框）
                    tier = CartographySettlementTier.City;
                    lType = LandmarkType.Capital;
                    iconKey = "symbol_city";
                    fontSz = 13;
                    scale = 0.78f;
                    importance = 3;
                    showLabel = true;
                }
                else if (isPass)
                {
                    // 3级关防：隘口关卡（☲ 双轨堞文）
                    tier = CartographySettlementTier.Town;
                    lType = LandmarkType.Town;
                    iconKey = "symbol_pass";
                    fontSz = 11;
                    scale = 0.65f;
                    importance = 3;
                    showLabel = true;
                }
                else if (isCoastAdjacent && (s.Name.Contains("港") || s.Name.Contains("津") || s.Name.Contains("渡") || s.Name.Contains("海") || s.Rank == SettlementRank.Town))
                {
                    // 4级水运：津渡泊港（⚓ 铁锚）
                    tier = CartographySettlementTier.Harbor;
                    lType = LandmarkType.Port;
                    iconKey = "symbol_port";
                    fontSz = 11;
                    scale = 0.65f;
                    importance = 3;
                    showLabel = true;
                }
                else if (s.Rank == SettlementRank.Town)
                {
                    // 3级城镇：县治驿市（□ 端正单线方框）
                    tier = CartographySettlementTier.Town;
                    lType = LandmarkType.Town;
                    iconKey = "symbol_town";
                    fontSz = 11;
                    scale = 0.68f;
                    importance = 3;
                    showLabel = true;
                }
                else if (s.Rank == SettlementRank.Hamlet && isMountainAdjacent && (s.CellId % 4 == 0))
                {
                    // 仙山寺观（⛩ / 宝塔）
                    tier = CartographySettlementTier.Village;
                    lType = LandmarkType.Temple;
                    iconKey = "symbol_temple";
                    fontSz = 10;
                    scale = 0.60f;
                    importance = 2;
                    showLabel = true;
                }
                else
                {
                    // 5级村落：乡野小村（● 清秀实心小墨点）
                    // 核心制图军规：小村落默认不生成文字题名，仅存符号点，彻底消除文字竞争
                    tier = CartographySettlementTier.Village;
                    lType = LandmarkType.Village;
                    iconKey = "symbol_village";
                    fontSz = 9;
                    scale = 0.46f;
                    importance = 1;
                    showLabel = false;
                }

                var landmark = new LandmarkStyle
                {
                    Id = nextId++,
                    Name = s.Name,
                    Position = pt,
                    Type = lType,
                    IconType = BrushType.CityIcon,
                    Importance = importance,
                    FontSize = fontSz,
                    HasHalo = true,
                    TextColor = CartographyColor.InkCharcoal,
                    Scale = scale,
                    ShowLabel = showLabel,
                    SettlementTier = tier
                };
                landmarks.Add(landmark);

                // 生成对应的古地图规范点线符号笔刷指令
                // 柔和深墨色（#3a322a，Alpha 0.85），避免生硬刺眼的死黑
                var softInkTint = new CartographyColor(0.23f, 0.20f, 0.17f, 0.85f);
                brushes.Add(new BrushInstruction
                {
                    Type = BrushType.CityIcon,
                    Position = pt,
                    Scale = scale,
                    Opacity = 0.88f,
                    YOrder = (float)pt.Y + 2f,
                    Tint = softInkTint,
                    VariantKey = iconKey,
                    Tag = s.Name
                });
            }
        }

        // 2. 宏观自然实体地标题名（名山主峰、神海、奇原、巨漠）
        if (megaRegions != null)
        {
            foreach (var r in megaRegions)
            {
                if (r.Rank < MegaRegionRank.MajorRegion) continue;

                var pt = r.Centroid;
                var fontSz = r.Rank switch
                {
                    MegaRegionRank.WorldLandmark => 15,
                    MegaRegionRank.MegaRegion => 13,
                    _ => 11
                };

                if (r.Type == MegaTerrainType.MegaMountain && r.Spine != null && r.Spine.Count > 0)
                {
                    // 主峰题名置于最高山脊上方
                    var highest = r.Spine.OrderByDescending(n => n.Elevation).First();
                    pt = highest.Position + new PolyVec2(16d, -22d);

                    landmarks.Add(new LandmarkStyle
                    {
                        Id = nextId++,
                        Name = r.Name,
                        Position = pt,
                        Type = LandmarkType.SacredPeak,
                        Importance = 3,
                        FontSize = fontSz,
                        HasHalo = true,
                        TextColor = CartographyColor.InkCharcoal,
                        Scale = 1.15f,
                        RegionId = r.Id
                    });
                }
                else
                {
                    var isWet = r.Type == MegaTerrainType.MegaWetland;
                    landmarks.Add(new LandmarkStyle
                    {
                        Id = nextId++,
                        Name = r.Name,
                        Position = pt,
                        Type = LandmarkType.NaturalWonder,
                        Importance = 3,
                        FontSize = fontSz,
                        HasHalo = true,
                        TextColor = isWet ? CartographyColor.ColdBlue : CartographyColor.InkCharcoal,
                        Scale = 1.05f,
                        RegionId = r.Id
                    });
                }
            }
        }

        return (landmarks, brushes);
    }
}
