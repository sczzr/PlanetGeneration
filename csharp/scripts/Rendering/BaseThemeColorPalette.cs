using Godot;
using PlanetGeneration.Core.Domain;
using System;

namespace PlanetGeneration.Rendering;

/// <summary>
/// 13 种基础底图主题的配色方案与像素/地块着色映射器。
/// </summary>
public static class BaseThemeColorPalette
{
    private static readonly Color DeepOcean = Color.FromHtml("#1a2482");
    private static readonly Color ShallowOcean = Color.FromHtml("#0059b3");
    private static readonly Color DarkOcean = Color.FromHtml("#0a2044");

    private static readonly Color ElevationOceanDeep = Color.FromHtml("#03123a");
    private static readonly Color ElevationOceanMid = Color.FromHtml("#0a316f");
    private static readonly Color ElevationOceanShallow = Color.FromHtml("#1674c4");
    private static readonly Color ElevationLandLow = Color.FromHtml("#2e9143");
    private static readonly Color ElevationLandMidLow = Color.FromHtml("#58ab51");
    private static readonly Color ElevationLandMid = Color.FromHtml("#8db462");
    private static readonly Color ElevationLandHigh = Color.FromHtml("#c5bc92");
    private static readonly Color ElevationLandVeryHigh = Color.FromHtml("#ddd3b6");
    private static readonly Color ElevationLandPeak = Color.FromHtml("#f0e8d7");
    private static readonly Color ElevationLandSnow = Color.FromHtml("#f9fafb");

    private static readonly Color TopographicShelf = Color.FromHtml("#2c8fd6");
    private static readonly Color TopographicCoast = Color.FromHtml("#a7c872");
    private static readonly Color TopographicContour = Color.FromHtml("#5e7b49");

    private static readonly Color TemperatureCold = Color.FromHtml("#004cff");
    private static readonly Color TemperatureMild = Color.FromHtml("#ffe45c");
    private static readonly Color TemperatureHot = Color.FromHtml("#ff2a00");

    private static readonly Color MoistureLight = Color.FromHtml("#d9ecff");
    private static readonly Color MoistureMedium = Color.FromHtml("#5aa9ff");
    private static readonly Color MoistureHeavy = Color.FromHtml("#0d3f95");

    private static readonly Color EcologyBarren = Color.FromHtml("#8a4f2b");
    private static readonly Color EcologyDry = Color.FromHtml("#d69a45");
    private static readonly Color EcologyGrass = Color.FromHtml("#74b152");
    private static readonly Color EcologyLush = Color.FromHtml("#2bcf74");

    private static readonly Color CivilizationNeutralDark = Color.FromHtml("#3a3f47");
    private static readonly Color CivilizationNeutralLight = Color.FromHtml("#5e6672");
    private static readonly Color CivilizationTintBase = Color.FromHtml("#1f232a");

    private static readonly Color TradeGroundDark = Color.FromHtml("#2f3f34");
    private static readonly Color TradeGroundLight = Color.FromHtml("#4a5f47");
    private static readonly Color TradeRouteDim = Color.FromHtml("#d79a4a");
    private static readonly Color TradeRouteBright = Color.FromHtml("#f4df8c");

    private static readonly Color SatelliteDryHue = Color.FromHtml("#8f7b56");
    private static readonly Color SatelliteWetHue = Color.FromHtml("#2f7b43");
    private static readonly Color SatelliteRockLow = Color.FromHtml("#7f6e57");
    private static readonly Color SatelliteRockHigh = Color.FromHtml("#b9ab95");
    private static readonly Color SatelliteSeaIce = new(0.84f, 0.92f, 0.98f, 1f);
    private static readonly Color SatelliteCoastSand = new(222f / 255f, 232f / 255f, 187f / 255f, 1f);
    private static readonly Color SatelliteSnow = new(232f / 255f, 246f / 255f, 255f / 255f, 1f);
    private static readonly Color SatellitePolarSnow = new(236f / 255f, 248f / 255f, 255f / 255f, 1f);

    public static readonly Color[] BiomeColors =
    {
        Color.FromHtml("#2f5f88"), // Ocean
        Color.FromHtml("#4f7ea8"), // ShallowOcean
        Color.FromHtml("#dfe4c9"), // Coastland
        Color.FromHtml("#c2d3da"), // Ice
        Color.FromHtml("#a1814a"), // Tundra
        Color.FromHtml("#4f6e34"), // BorealForest
        Color.FromHtml("#5f8640"), // Taiga
        Color.FromHtml("#c7c5ac"), // Steppe
        Color.FromHtml("#b8c98a"), // Grassland
        Color.FromHtml("#a8a07f"), // Chaparral
        Color.FromHtml("#d7c691"), // TemperateDesert
        Color.FromHtml("#2fb95a"), // TemperateSeasonalForest
        Color.FromHtml("#46a857"), // TemperateRainForest
        Color.FromHtml("#cfd18a"), // Savanna
        Color.FromHtml("#7c8f53"), // Shrubland
        Color.FromHtml("#e9d79b"), // TropicalDesert
        Color.FromHtml("#aed45a"), // TropicalSeasonalForest
        Color.FromHtml("#7acb33"), // TropicalRainForest
        Color.FromHtml("#8f8067"), // RockyMountain
        Color.FromHtml("#e7edf0"), // SnowyMountain
        Color.FromHtml("#2ea3d4")  // River
    };

    public static readonly Color[] RockColors =
    {
        Color.FromHtml("#FFF307"), // Sedimentary
        Color.FromHtml("#4da0ab"), // Igneous
        Color.FromHtml("#EF6876")  // Metamorphic
    };

    public static readonly Color[] OreColors =
    {
        Colors.Black,              // None
        Color.FromHtml("#808080"), // Coal
        Color.FromHtml("#F7B946"), // Copper
        Color.FromHtml("#298970"), // Tin
        Color.FromHtml("#ea4545"), // Iron
        Color.FromHtml("#F3F029"), // Gold
        Color.FromHtml("#cb5bea"), // Diamond
        Color.FromHtml("#5bcd5e"), // Platinum
        Color.FromHtml("#34e5f5"), // Aluminum
        Color.FromHtml("#E7E7EE"), // Silver
        Color.FromHtml("#EAA19A")  // Lead
    };

    public static Color GetCellColor(WorldSnapshot snapshot, string themeId, int cellId)
    {
        var fields = snapshot.Fields;
        var options = snapshot.Options;
        var seaLevel = options.SeaLevel;
        var h = fields.Height[cellId];

        return themeId switch
        {
            "terrain_overview" or "satellite" => GetSatelliteColor(snapshot, cellId),
            "biomes" => GetBiomeColor((BiomeType)fields.Biome[cellId]),
            "elevation" => GetElevationColor(h, seaLevel, ElevationStyleId.Standard),
            "temperature" => GetTemperatureColor(fields.Temperature[cellId]),
            "moisture" => GetMoistureColor(fields.Moisture[cellId]),
            "landform" or "landforms" => GetLandformColor((LandformType)fields.Landform[cellId]),
            "plates" => GetPlateColor(snapshot, cellId),
            "rock_types" => GetRockColor((RockType)fields.Rock[cellId], h, seaLevel),
            "ores" => GetOreColor((OreType)fields.Ore[cellId], h, seaLevel),
            "ecology" => GetEcologyColor(fields.EcologyHealth[cellId], h, seaLevel),
            "civilization" => GetCivilizationColor(fields.Influence[cellId], fields.PolityId[cellId], fields.BorderMask[cellId], h, seaLevel),
            "trade_flow" or "trade_routes" => GetTradeRouteColor(fields.TradeRouteMask[cellId], fields.TradeFlow[cellId], fields.Influence[cellId], h, seaLevel),
            "cell_grid" or "cell_debug" => GetCellDebugColor(cellId),
            _ => GetSatelliteColor(snapshot, cellId)
        };
    }

    public static Color GetBiomeColor(BiomeType biome)
    {
        var idx = (int)biome;
        if (idx >= 0 && idx < BiomeColors.Length)
        {
            return BiomeColors[idx];
        }
        return Colors.DarkGray;
    }

    public static Color GetElevationColor(float elevation, float seaLevel, ElevationStyleId style)
    {
        if (elevation <= seaLevel)
        {
            var depth = Mathf.Clamp((seaLevel - elevation) / Mathf.Max(seaLevel, 0.0001f), 0f, 1f);
            if (depth > 0.6f) return ElevationOceanDeep.Lerp(ElevationOceanMid, (1f - depth) / 0.4f);
            return ElevationOceanMid.Lerp(ElevationOceanShallow, (0.6f - depth) / 0.6f);
        }

        var landT = Mathf.Clamp((elevation - seaLevel) / Mathf.Max(1f - seaLevel, 0.0001f), 0f, 1f);
        if (style == ElevationStyleId.Topographic)
        {
            if (landT < 0.12f) return TopographicCoast.Lerp(TopographicShelf, landT / 0.12f);
            var bands = Mathf.Floor(landT * 12f) / 12f;
            return TopographicShelf.Lerp(TopographicContour, bands);
        }

        if (landT < 0.18f) return ElevationLandLow.Lerp(ElevationLandMidLow, landT / 0.18f);
        if (landT < 0.42f) return ElevationLandMidLow.Lerp(ElevationLandMid, (landT - 0.18f) / 0.24f);
        if (landT < 0.68f) return ElevationLandMid.Lerp(ElevationLandHigh, (landT - 0.42f) / 0.26f);
        if (landT < 0.85f) return ElevationLandHigh.Lerp(ElevationLandVeryHigh, (landT - 0.68f) / 0.17f);
        if (landT < 0.94f) return ElevationLandVeryHigh.Lerp(ElevationLandPeak, (landT - 0.85f) / 0.09f);
        return ElevationLandPeak.Lerp(ElevationLandSnow, (landT - 0.94f) / 0.06f);
    }

    public static Color GetTemperatureColor(float temperature)
    {
        var t = Mathf.Clamp(temperature, 0f, 1f);
        if (t < 0.5f) return TemperatureCold.Lerp(TemperatureMild, t * 2f);
        return TemperatureMild.Lerp(TemperatureHot, (t - 0.5f) * 2f);
    }

    public static Color GetMoistureColor(float moisture)
    {
        var m = Mathf.Clamp(moisture, 0f, 1f);
        if (m < 0.5f) return MoistureLight.Lerp(MoistureMedium, m * 2f);
        return MoistureMedium.Lerp(MoistureHeavy, (m - 0.5f) * 2f);
    }

    public static Color GetLandformColor(LandformType landform)
    {
        return landform switch
        {
            LandformType.Ocean => Color.FromHtml("#16325c"),
            LandformType.DeepOcean => Color.FromHtml("#0b1e3b"),
            LandformType.Trench => Color.FromHtml("#061124"),
            LandformType.Coast => Color.FromHtml("#a2c4c9"),
            LandformType.Plain => Color.FromHtml("#6aa84f"),
            LandformType.Basin => Color.FromHtml("#38761d"),
            LandformType.Plateau => Color.FromHtml("#e69138"),
            LandformType.Hill => Color.FromHtml("#b6d7a8"),
            LandformType.Mountain => Color.FromHtml("#b45f06"),
            LandformType.Volcano => Color.FromHtml("#990000"),
            LandformType.Island => Color.FromHtml("#76a5af"),
            _ => Colors.Gray
        };
    }

    public static Color GetPlateColor(WorldSnapshot snapshot, int cellId)
    {
        var plateId = snapshot.Fields.PlateId[cellId];
        var boundary = (PlateBoundaryType)snapshot.Fields.PlateBoundary[cellId];

        if (boundary == PlateBoundaryType.Convergent) return new Color(0.98f, 0.22f, 0.22f);
        if (boundary == PlateBoundaryType.Divergent) return new Color(0.22f, 0.92f, 0.95f);
        if (boundary == PlateBoundaryType.Transform) return new Color(0.98f, 0.80f, 0.28f);

        // 确定性伪随机板块主色
        var hash = unchecked((uint)(plateId * 2654435761u + 12345));
        var r = 0.25f + ((hash & 0xFFu) / 255f) * 0.65f;
        var g = 0.25f + (((hash >> 8) & 0xFFu) / 255f) * 0.65f;
        var b = 0.25f + (((hash >> 16) & 0xFFu) / 255f) * 0.65f;
        return new Color(r, g, b, 1f);
    }

    public static Color GetRockColor(RockType rock, float elevation, float seaLevel)
    {
        if (elevation <= seaLevel)
        {
            var depth = Mathf.Clamp((seaLevel - elevation) / Mathf.Max(seaLevel, 0.0001f), 0f, 1f);
            return DeepOcean.Lerp(ShallowOcean, 1f - depth * 0.6f);
        }

        var idx = (int)rock;
        if (idx >= 0 && idx < RockColors.Length)
        {
            var baseColor = RockColors[idx];
            var factor = Mathf.Clamp(0.7f + 0.6f * elevation, 0.5f, 1.4f);
            return baseColor * factor;
        }
        return Colors.DarkGray;
    }

    public static Color GetOreColor(OreType ore, float elevation, float seaLevel)
    {
        if (ore == OreType.None)
        {
            if (elevation <= seaLevel)
            {
                var depth = Mathf.Clamp((seaLevel - elevation) / Mathf.Max(seaLevel, 0.0001f), 0f, 1f);
                return DeepOcean.Lerp(ShallowOcean, 1f - depth * 0.6f);
            }
            return Color.FromHtml("#404552");
        }

        var idx = (int)ore;
        if (idx >= 0 && idx < OreColors.Length)
        {
            return OreColors[idx];
        }
        return Colors.White;
    }

    public static Color GetEcologyColor(float ecology, float elevation, float seaLevel)
    {
        if (elevation <= seaLevel)
        {
            var depth = Mathf.Clamp((seaLevel - elevation) / Mathf.Max(seaLevel, 0.0001f), 0f, 1f);
            return DarkOcean.Lerp(ShallowOcean, 1f - depth * 0.65f);
        }

        var t = Mathf.Clamp(ecology, 0f, 1f);
        if (t < 0.34f) return EcologyBarren.Lerp(EcologyDry, t / 0.34f);
        if (t < 0.67f) return EcologyDry.Lerp(EcologyGrass, (t - 0.34f) / 0.33f);
        return EcologyGrass.Lerp(EcologyLush, (t - 0.67f) / 0.33f);
    }

    public static Color GetCivilizationColor(float influence, int polityId, bool isBorder, float elevation, float seaLevel)
    {
        if (elevation <= seaLevel)
        {
            var depth = Mathf.Clamp((seaLevel - elevation) / Mathf.Max(seaLevel, 0.0001f), 0f, 1f);
            return DarkOcean.Lerp(ShallowOcean, 1f - depth * 0.55f);
        }

        if (polityId < 0 || influence < 0.16f)
        {
            var neutral = Mathf.Clamp(influence, 0f, 1f);
            return CivilizationNeutralDark.Lerp(CivilizationNeutralLight, neutral * 0.75f);
        }

        var polityColor = GetPolityColor(polityId);
        var tinted = CivilizationTintBase.Lerp(polityColor, Mathf.Clamp(influence * 0.95f + 0.05f, 0f, 1f));
        if (isBorder)
        {
            return tinted.Lerp(Colors.White, 0.26f);
        }
        return tinted;
    }

    public static Color GetPolityColor(int polityId)
    {
        var hash = unchecked((uint)(polityId * 2654435761u));
        var red = 0.28f + ((hash & 0xFFu) / 255f) * 0.62f;
        var green = 0.28f + (((hash >> 8) & 0xFFu) / 255f) * 0.62f;
        var blue = 0.28f + (((hash >> 16) & 0xFFu) / 255f) * 0.62f;
        return new Color(red, green, blue, 1f);
    }

    public static Color GetTradeRouteColor(bool hasRoute, float flow, float influence, float elevation, float seaLevel)
    {
        if (elevation <= seaLevel)
        {
            var depth = Mathf.Clamp((seaLevel - elevation) / Mathf.Max(seaLevel, 0.0001f), 0f, 1f);
            return DarkOcean.Lerp(ShallowOcean, 1f - depth * 0.55f);
        }

        var baseGround = TradeGroundDark.Lerp(TradeGroundLight, Mathf.Clamp(influence, 0f, 1f) * 0.55f);
        if (!hasRoute) return baseGround;

        var routeColor = TradeRouteDim.Lerp(TradeRouteBright, Mathf.Clamp(flow, 0f, 1f));
        return baseGround.Lerp(routeColor, 0.72f);
    }

    private static Color GetSatelliteColor(WorldSnapshot snapshot, int cellId)
    {
        var fields = snapshot.Fields;
        var options = snapshot.Options;
        var seaLevel = options.SeaLevel;
        var h = fields.Height[cellId];
        var cy = (float)snapshot.Geometry.CentroidY[cellId];
        var mapHeight = (float)snapshot.Geometry.Height;

        var lat = Mathf.Abs(cy - (mapHeight * 0.5f)) / (mapHeight * 0.5f);
        var polar = Mathf.Clamp((lat - 0.72f) / 0.28f, 0f, 1f);

        if (h <= seaLevel)
        {
            var oceanT = Mathf.Clamp(h / Mathf.Max(seaLevel, 0.0001f), 0f, 1f);
            var water = DeepOcean.Lerp(ShallowOcean, oceanT);
            if (polar > 0.35f)
            {
                var iceAmount = (polar - 0.35f) / 0.65f;
                return water.Lerp(SatelliteSeaIce, iceAmount * 0.85f);
            }
            return water;
        }

        var landT = Mathf.Clamp((h - seaLevel) / Mathf.Max(1f - seaLevel, 0.0001f), 0f, 1f);
        var moist = fields.Moisture[cellId];
        var terrainBase = SatelliteDryHue.Lerp(SatelliteWetHue, moist);

        if (landT < 0.06f)
        {
            terrainBase = terrainBase.Lerp(SatelliteCoastSand, 1f - (landT / 0.06f));
        }
        else if (landT > 0.65f)
        {
            var rockT = (landT - 0.65f) / 0.35f;
            terrainBase = terrainBase.Lerp(SatelliteRockHigh, rockT * 0.75f);
        }

        if (polar > 0.2f)
        {
            var snowT = (polar - 0.2f) / 0.8f;
            return terrainBase.Lerp(SatellitePolarSnow, snowT);
        }

        return terrainBase;
    }

    public static Color GetCellDebugColor(int cellId)
    {
        var hash = unchecked((uint)(cellId * 1103515245u + 12345));
        var r = 0.25f + ((hash & 0xFFu) / 255f) * 0.65f;
        var g = 0.25f + (((hash >> 8) & 0xFFu) / 255f) * 0.65f;
        var b = 0.25f + (((hash >> 16) & 0xFFu) / 255f) * 0.65f;
        return new Color(r, g, b, 1f);
    }
}
