using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Cartography.Planning;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Generator;

/// <summary>
/// 规划式战略聚落地标画师（SettlementGenerator）。
/// 
/// 呈现星罗棋布的 22+ 座四级文明网络：
/// 1. 【文明梯队与图元分级】：
///    - Tier 4: 天下国都（神京天都，双重城垣，大字书法题名）；
///    - Tier 3: 州府名都与海河巨港（江陵大都、临海沧津、姑苏水郡、陇右名都）；
///    - Tier 2: 锁喉关隘、军防水寨与要冲市肆（雁门雄关、剑阁险关、金沙古堡、浔阳古埠、襄樊水寨、武当仙镇）；
///    - Tier 1: 乡野古村、仙家洞天与野渡渔村（杏花村、桃源坞、枫林渡、渔歌泊、青岚观等，微型图元，抑制文字噪点）。
/// 2. 【分级书法题名】：
///    - 仅为高阶与重点地标生成醒目标签（ShowLabel），普通村落保留雅致符号点缀。
/// </summary>
public static class SettlementGenerator
{
    public static (List<LandmarkStyle> Landmarks, List<BrushInstruction> Brushes) Generate(
        IReadOnlyList<PlannedSettlement> settlements,
        GenerationOptions options,
        Dictionary<int, RegionStyle>? regionStyles = null)
    {
        return Generate(settlements, null, options, regionStyles);
    }

    public static (List<LandmarkStyle> Landmarks, List<BrushInstruction> Brushes) Generate(
        IReadOnlyList<PlannedSettlement> settlements,
        IReadOnlyList<PlannedLandmark>? grandLandmarks,
        GenerationOptions options,
        Dictionary<int, RegionStyle>? regionStyles = null)
    {
        var landmarks = new List<LandmarkStyle>();
        var brushes = new List<BrushInstruction>();
        if (settlements == null || settlements.Count == 0) return (landmarks, brushes);

        var nextId = 1;

        // ── 1. 战略聚落网络生成 ──
        foreach (var s in settlements)
        {
            var (landmarkType, iconVariant, settlementTier, scale, fontSize) = s.AnchorType switch
            {
                PlannedSettlementAnchor.RiverLoopCapital => (
                    LandmarkType.Capital,
                    "city_capital",
                    CartographySettlementTier.Capital,
                    1.48f,
                    18),

                PlannedSettlementAnchor.RiverConfluence => (
                    LandmarkType.Town,
                    "city_large",
                    CartographySettlementTier.City,
                    1.24f,
                    15),

                PlannedSettlementAnchor.PlainAgriculturalCity => (
                    LandmarkType.Town,
                    "city_large",
                    CartographySettlementTier.City,
                    1.22f,
                    15),

                PlannedSettlementAnchor.MountainMiningCity => (
                    LandmarkType.Town,
                    "city_large",
                    CartographySettlementTier.City,
                    1.22f,
                    14),

                PlannedSettlementAnchor.CanalTown or PlannedSettlementAnchor.NorthwestBorderCity => (
                    LandmarkType.Town,
                    "city_large",
                    CartographySettlementTier.City,
                    1.20f,
                    14),

                PlannedSettlementAnchor.SeaHarbor => (
                    LandmarkType.Port,
                    "symbol_port",
                    CartographySettlementTier.Harbor,
                    1.24f,
                    15),

                PlannedSettlementAnchor.RiverFerryPort => (
                    LandmarkType.Port,
                    "symbol_port",
                    CartographySettlementTier.Harbor,
                    1.18f,
                    14),

                PlannedSettlementAnchor.FishingHaven => (
                    LandmarkType.Port,
                    "symbol_port",
                    CartographySettlementTier.Harbor,
                    1.12f,
                    13),

                PlannedSettlementAnchor.MountainPass => (
                    LandmarkType.Town,
                    "pass_garrison",
                    CartographySettlementTier.Town,
                    1.18f,
                    14),

                PlannedSettlementAnchor.MountainFort => (
                    LandmarkType.Town,
                    "pass_garrison",
                    CartographySettlementTier.Town,
                    1.14f,
                    13),

                PlannedSettlementAnchor.RiverGarrison => (
                    LandmarkType.Town,
                    "pass_garrison",
                    CartographySettlementTier.Town,
                    1.14f,
                    13),

                PlannedSettlementAnchor.ForestMarginCity => (
                    LandmarkType.Town,
                    "city_town",
                    CartographySettlementTier.Town,
                    1.14f,
                    13),

                PlannedSettlementAnchor.DesertOasis => (
                    LandmarkType.Town,
                    "city_town",
                    CartographySettlementTier.Town,
                    1.12f,
                    13),

                PlannedSettlementAnchor.OasisTradeCity => (
                    LandmarkType.Town,
                    "city_town",
                    CartographySettlementTier.Town,
                    1.14f,
                    13),

                PlannedSettlementAnchor.WetlandWaterTown => (
                    LandmarkType.Town,
                    "city_town",
                    CartographySettlementTier.Town,
                    1.14f,
                    13),

                PlannedSettlementAnchor.SacredTown => (
                    LandmarkType.Town,
                    "city_town",
                    CartographySettlementTier.Town,
                    1.12f,
                    13),

                PlannedSettlementAnchor.AncientRelic => (
                    LandmarkType.Temple,
                    "relic_pyramid",
                    CartographySettlementTier.Village,
                    1.18f,
                    13),

                PlannedSettlementAnchor.SacredSanctuary => (
                    LandmarkType.Temple,
                    "temple_pagoda",
                    CartographySettlementTier.Village,
                    1.05f,
                    12),

                PlannedSettlementAnchor.ValleyHaven => (
                    LandmarkType.Village,
                    "village_cluster",
                    CartographySettlementTier.Village,
                    0.88f,
                    11),

                PlannedSettlementAnchor.FerryVillage => (
                    LandmarkType.Village,
                    "village_cluster",
                    CartographySettlementTier.Village,
                    0.88f,
                    11),

                _ => (
                    LandmarkType.Village,
                    "village_cluster",
                    CartographySettlementTier.Village,
                    0.86f,
                    11)
            };

            // 1. 生成建筑图元笔刷 (CityIcon) - 抬高 YOrder 确保不被树木与低丘遮挡
            brushes.Add(new BrushInstruction
            {
                Type = BrushType.CityIcon,
                Position = s.Position,
                Scale = scale,
                Opacity = 0.98f,
                YOrder = (float)s.Position.Y + 6.0f,
                Tint = CartographyColor.InkCharcoal,
                VariantKey = iconVariant,
                Tag = s.Name
            });

            // 2. 生成地标题注 (LandmarkStyle)
            landmarks.Add(new LandmarkStyle
            {
                Id = nextId++,
                Name = s.Name,
                Position = s.Position,
                Type = landmarkType,
                IconType = BrushType.CityIcon,
                Importance = s.Tier,
                FontSize = fontSize,
                HasHalo = true,
                TextColor = (s.AnchorType == PlannedSettlementAnchor.SeaHarbor || s.AnchorType == PlannedSettlementAnchor.FishingHaven)
                    ? CartographyColor.ColdBlue.Lerp(CartographyColor.InkCharcoal, 0.4f)
                    : CartographyColor.InkCharcoal,
                Scale = scale,
                ShowLabel = s.ShowLabel,
                SettlementTier = settlementTier
            });
        }

        // ── 2. 宏大视觉地标（Grand Landmarks: 视觉锚点与叙事核心） ──
        if (grandLandmarks != null)
        {
            foreach (var gl in grandLandmarks)
            {
                switch (gl.Type)
                {
                    case PlannedLandmarkType.SnowPeakSummit:
                        // 极北雪峰绝顶：主山系最巍峨雪巅与书法题名
                        landmarks.Add(new LandmarkStyle
                        {
                            Id = nextId++,
                            Name = "苍冥雪峰",
                            Position = gl.Position + new PolyVec2(0, -18.0),
                            Type = LandmarkType.SacredPeak,
                            IconType = BrushType.SnowCap,
                            Importance = 4,
                            FontSize = 16,
                            HasHalo = true,
                            TextColor = CartographyColor.InkCharcoal,
                            Scale = gl.VisualScale,
                            ShowLabel = true,
                            SettlementTier = CartographySettlementTier.Capital
                        });
                        break;

                    case PlannedLandmarkType.DesertRelicPyramid:
                        // 狂沙大荒·古文明金字塔神殿遗迹
                        brushes.Add(new BrushInstruction
                        {
                            Type = BrushType.CityIcon,
                            Position = gl.Position,
                            Scale = gl.VisualScale * 1.15f,
                            Opacity = 0.96f,
                            YOrder = (float)gl.Position.Y + 8.0f,
                            Tint = CartographyColor.SandyOchre,
                            VariantKey = "pyramid_relic",
                            Tag = gl.Name
                        });

                        landmarks.Add(new LandmarkStyle
                        {
                            Id = nextId++,
                            Name = "金戈古城",
                            Position = gl.Position,
                            Type = LandmarkType.NaturalWonder,
                            IconType = BrushType.CityIcon,
                            Importance = 3,
                            FontSize = 14,
                            HasHalo = true,
                            TextColor = CartographyColor.SandyOchre.Lerp(CartographyColor.InkCharcoal, 0.45f),
                            Scale = gl.VisualScale,
                            ShowLabel = true,
                            SettlementTier = CartographySettlementTier.Town
                        });
                        break;
                }
            }
        }

        return (landmarks, brushes);
    }
}
