using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 区域艺术风格生成器（RegionStyleGenerator）。
/// 
/// 遍历世界级宏观地貌实体（MegaRegions）与物理微观场，
/// 提炼并生成每个地理区域的艺术风格、专属色彩、排布密度与艺术夸张系数。
/// </summary>
public static class RegionStyleGenerator
{
    public static Dictionary<int, RegionStyle> GenerateStyles(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        IReadOnlyList<MegaTerrainRegion> megaRegions)
    {
        var result = new Dictionary<int, RegionStyle>();

        // 默认全球基底平原风格 (RegionId = 0)
        result[0] = new RegionStyle
        {
            RegionId = 0,
            Name = "中原沃野",
            Style = TerrainStyle.Plain,
            Palette = CartographyColor.EmeraldGreen,
            SecondaryColor = CartographyColor.InkCharcoal,
            Density = 1.0f,
            Exaggeration = 1.0f,
            Fog = false
        };

        if (megaRegions == null || megaRegions.Count == 0)
        {
            return result;
        }

        var seaLevel = options.SeaLevel;

        foreach (var r in megaRegions)
        {
            var style = ResolveRegionStyle(r, fields, seaLevel);
            result[r.Id] = style;
        }

        return result;
    }

    private static RegionStyle ResolveRegionStyle(MegaTerrainRegion region, CellFields fields, float seaLevel)
    {
        var style = new RegionStyle
        {
            RegionId = region.Id,
            Name = region.Name
        };

        // 计算该区域的平均温度与海拔
        var avgElev = 0f;
        var avgTemp = 0f;
        var avgMoist = 0f;
        var cellCount = region.Cells.Length;

        if (cellCount > 0)
        {
            var sumElev = 0f;
            var sumTemp = 0f;
            var sumMoist = 0f;
            foreach (var c in region.Cells)
            {
                if (c >= 0 && c < fields.Count)
                {
                    sumElev += fields.Height[c];
                    sumTemp += fields.Temperature[c];
                    sumMoist += fields.Moisture[c];
                }
            }
            avgElev = sumElev / cellCount;
            avgTemp = sumTemp / cellCount;
            avgMoist = sumMoist / cellCount;
        }

        var isSnowy = avgElev > seaLevel + 0.45f || avgTemp < 0.22f;
        var isHumid = avgMoist > 0.55f;

        switch (region.Type)
        {
            case MegaTerrainType.MegaMountain:
                if (isSnowy)
                {
                    style.Style = TerrainStyle.SnowMountain;
                    style.Palette = CartographyColor.ColdBlue;
                    style.SecondaryColor = CartographyColor.FromRgb(0.70f, 0.82f, 0.90f);
                    style.Density = 0.95f;
                    style.Exaggeration = 1.25f;
                    style.Fog = true;
                    style.MistColor = CartographyColor.FromRgba(0.93f, 0.96f, 0.99f, 0.35f);
                }
                else
                {
                    style.Style = TerrainStyle.RockMountain;
                    style.Palette = CartographyColor.EmeraldGreen;
                    style.SecondaryColor = CartographyColor.InkCharcoal;
                    style.Density = 0.90f;
                    style.Exaggeration = 1.15f;
                    style.Fog = true;
                    style.MistColor = CartographyColor.MistIvory;
                }
                break;

            case MegaTerrainType.MegaForest:
                if (isHumid && avgTemp > 0.40f)
                {
                    style.Style = TerrainStyle.AncientForest;
                    style.Palette = CartographyColor.DeepForest;
                    style.SecondaryColor = CartographyColor.EmeraldGreen;
                    style.Density = 1.05f;
                    style.Exaggeration = 1.10f;
                    style.Fog = true;
                    style.MistColor = CartographyColor.FromRgba(0.90f, 0.94f, 0.90f, 0.28f);
                }
                else
                {
                    style.Style = TerrainStyle.DenseForest;
                    style.Palette = CartographyColor.EmeraldGreen;
                    style.SecondaryColor = CartographyColor.DeepForest;
                    style.Density = 0.95f;
                    style.Exaggeration = 1.02f;
                    style.Fog = false;
                }
                break;

            case MegaTerrainType.MegaDesert:
                style.Style = TerrainStyle.Desert;
                style.Palette = CartographyColor.SandyOchre;
                style.SecondaryColor = CartographyColor.FromRgb(0.70f, 0.52f, 0.30f);
                style.Density = 0.85f;
                style.Exaggeration = 0.95f;
                style.Fog = false;
                break;

            case MegaTerrainType.MegaPlateau:
                style.Style = TerrainStyle.Plateau;
                style.Palette = CartographyColor.FromRgb(0.75f, 0.60f, 0.42f);
                style.SecondaryColor = CartographyColor.InkCharcoal;
                style.Density = 0.90f;
                style.Exaggeration = 1.20f;
                style.Fog = false;
                break;

            case MegaTerrainType.MegaWetland:
                style.Style = TerrainStyle.Swamp;
                style.Palette = CartographyColor.ColdBlue.Lerp(CartographyColor.EmeraldGreen, 0.5f);
                style.SecondaryColor = CartographyColor.DeepForest;
                style.Density = 1.10f;
                style.Exaggeration = 0.85f;
                style.Fog = true;
                style.MistColor = CartographyColor.FromRgba(0.88f, 0.92f, 0.92f, 0.40f);
                break;

            case MegaTerrainType.MegaHills:
                style.Style = TerrainStyle.Hills;
                style.Palette = CartographyColor.EmeraldGreen;
                style.SecondaryColor = CartographyColor.InkCharcoal;
                style.Density = 1.05f;
                style.Exaggeration = 1.05f;
                style.Fog = false;
                break;

            case MegaTerrainType.MegaBasin:
                style.Style = TerrainStyle.Plain;
                style.Palette = CartographyColor.EmeraldGreen;
                style.SecondaryColor = CartographyColor.InkCharcoal;
                style.Density = 1.0f;
                style.Exaggeration = 1.0f;
                style.Fog = true;
                style.MistColor = CartographyColor.MistIvory;
                break;

            case MegaTerrainType.MegaGrassland:
            default:
                style.Style = TerrainStyle.Plain;
                style.Palette = CartographyColor.FromRgb(0.48f, 0.65f, 0.35f);
                style.SecondaryColor = CartographyColor.InkCharcoal;
                style.Density = 0.90f;
                style.Exaggeration = 0.90f;
                style.Fog = false;
                break;
        }

        return style;
    }
}
