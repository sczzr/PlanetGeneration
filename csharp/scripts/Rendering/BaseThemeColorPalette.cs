using Godot;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Layers;
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

    private static readonly Color MoistureDry = Color.FromHtml("#f4f8fc");
    private static readonly Color MoistureLight = Color.FromHtml("#a8d0f0");
    private static readonly Color MoistureMedium = Color.FromHtml("#438ecf");
    private static readonly Color MoistureHeavy = Color.FromHtml("#104b8f");
    private static readonly Color MoistureTorrential = Color.FromHtml("#041c42");

    private static readonly Color EcologyBarren = Color.FromHtml("#8a4f2b");
    private static readonly Color EcologyDry = Color.FromHtml("#d69a45");
    private static readonly Color EcologyGrass = Color.FromHtml("#74b152");
    private static readonly Color EcologyLush = Color.FromHtml("#2bcf74");

    private static readonly Color CivilizationNeutralDark = Color.FromHtml("#3a3f47");
    private static readonly Color CivilizationNeutralLight = Color.FromHtml("#5e6672");
    private static readonly Color CivilizationTintBase = Color.FromHtml("#1f232a");

    private static readonly Color SatelliteDryHue = Color.FromHtml("#8f7b56");
    private static readonly Color SatelliteWetHue = Color.FromHtml("#2f7b43");
    private static readonly Color SatelliteRockLow = Color.FromHtml("#7f6e57");
    private static readonly Color SatelliteRockHigh = Color.FromHtml("#b9ab95");
    private static readonly Color SatelliteSeaIce = new(0.84f, 0.92f, 0.98f, 1f);
    private static readonly Color SatelliteCoastSand = new(222f / 255f, 232f / 255f, 187f / 255f, 1f);
    private static readonly Color SatelliteSnow = new(232f / 255f, 246f / 255f, 255f / 255f, 1f);
    private static readonly Color SatellitePolarSnow = new(236f / 255f, 248f / 255f, 255f / 255f, 1f);

    // ── 中国古代手绘山水舆图：水墨青绿 + 浅绛 + 宣纸地子 ──
    private static readonly Color InkSeaShallow = Color.FromHtml("#9cbdd0");
    private static readonly Color InkSeaMid = Color.FromHtml("#5a86ab");
    private static readonly Color InkSeaAbyss = Color.FromHtml("#33526f");
    private static readonly Color InkSeaTrench = Color.FromHtml("#243c56");

    private static readonly Color InkPaperPlain = Color.FromHtml("#f0e4c9");
    private static readonly Color InkPlainWet = Color.FromHtml("#d5dec0");
    private static readonly Color InkPlainArid = Color.FromHtml("#e6cf9c");
    private static readonly Color InkDesertGold = Color.FromHtml("#e3c887");
    private static readonly Color InkHeartlandGold = Color.FromHtml("#e8d5a3");
    private static readonly Color InkCoastSand = Color.FromHtml("#ddcda6");

    private static readonly Color InkHillGreen = Color.FromHtml("#7a9c7c");
    private static readonly Color InkOchreFoothill = Color.FromHtml("#a08055");
    private static readonly Color InkMountainIndigo = Color.FromHtml("#71838f");
    private static readonly Color InkMountainDark = Color.FromHtml("#3b4a57");
    private static readonly Color InkPeakWhite = Color.FromHtml("#f6f2e6");
    private static readonly Color InkPolarWash = Color.FromHtml("#eef2ee");

    private static readonly Color InkRiverAzure = Color.FromHtml("#3f8fb8");
    private static readonly Color InkCinnabar = Color.FromHtml("#a83a2b");

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

    /// <summary>
    /// <summary>
    /// 矿种配色表（与 OreType 索引逐项对齐，共 29 项）。
    /// 涵盖基础工业（凡/灵/地）、超自然（灵/地/天）、卡牌（灵/地/天）三大分类。
    /// 严禁使用纯白 #FFFFFF，高光色采用象牙白/米白 #EAE6DF。
    /// 与 WorldRenderer.OreColors 逐项对齐，两条渲染路径必须同色。
    /// </summary>
    public static readonly Color[] OreColors =
    {
        Colors.Black,              // 0: None
        // ── 基础工业矿产 (1..11) ──
        Color.FromHtml("#8E929A"), // 1: Stone 石材 [凡]
        Color.FromHtml("#C28B62"), // 2: Clay 黏土 [凡]
        Color.FromHtml("#D5CFBE"), // 3: Limestone 石灰石 [凡]
        Color.FromHtml("#38393D"), // 4: Coal 煤矿 [凡]
        Color.FromHtml("#B74134"), // 5: Iron 铁矿 [凡]
        Color.FromHtml("#D97838"), // 6: Copper 铜矿 [凡]
        Color.FromHtml("#A8B7C9"), // 7: Aluminum 铝矿 [灵]
        Color.FromHtml("#E5D08C"), // 8: Silicon 硅矿 [灵]
        Color.FromHtml("#22262E"), // 9: Oil 石油 [地]
        Color.FromHtml("#58A4B0"), // 10: NaturalGas 天然气 [地]
        Color.FromHtml("#C79F3B"), // 11: RareMetal 稀有金属 [地]

        // ── 超自然矿产 (12..21) ──
        Color.FromHtml("#34D399"), // 12: SpiritCrystal 灵晶 [灵]
        Color.FromHtml("#F97316"), // 13: SunfireCrystal 炎曜晶 [灵]
        Color.FromHtml("#67E8F9"), // 14: FrostSoulCrystal 寒魄晶 [灵]
        Color.FromHtml("#A855F7"), // 15: ThunderMarrow 雷髓矿 [灵]
        Color.FromHtml("#4ADE80"), // 16: LifePith 生灵髓 [灵]
        Color.FromHtml("#4338CA"), // 17: NetherCrystal 幽冥晶 [地]
        Color.FromHtml("#818CF8"), // 18: VoidCrystal 空冥晶 [地]
        Color.FromHtml("#38BDF8"), // 19: AstralPith 星髓 [地]
        Color.FromHtml("#F43F5E"), // 20: LawStone 律纹石 [天]
        Color.FromHtml("#FACC15"), // 21: GenesisOre 源质矿 [天]

        // ── 卡牌资源 (22..28) ──
        Color.FromHtml("#FBBF24"), // 22: MemorySand 忆晶砂 [灵]
        Color.FromHtml("#2DD4BF"), // 23: RuneOre 灵纹矿 [灵]
        Color.FromHtml("#E879F9"), // 24: ResonanceCrystal 共鸣晶 [地]
        Color.FromHtml("#94A3B8"), // 25: EchoStone 回响石 [地]
        Color.FromHtml("#EA580C"), // 26: OrderedGold 定序金 [地]
        Color.FromHtml("#C084FC"), // 27: RealmCasketCrystal 界匣晶 [天]
        Color.FromHtml("#E11D48"), // 28: KarmaStone 因律石 [天]
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
            "guohua_handdrawn" or LayerRegistry.LayerGuohuaHanddrawn => GetGuohuaLandscapeBaseColor(snapshot, cellId),
            "inkwash_landscape" => GetInkWashLandscapeColor(snapshot, cellId),
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
        if (m < 0.25f) return MoistureDry.Lerp(MoistureLight, m * 4f);
        if (m < 0.50f) return MoistureLight.Lerp(MoistureMedium, (m - 0.25f) * 4f);
        if (m < 0.75f) return MoistureMedium.Lerp(MoistureHeavy, (m - 0.50f) * 4f);
        return MoistureHeavy.Lerp(MoistureTorrential, (m - 0.75f) * 4f);
    }

    public static Color GetLandformColor(LandformType landform)
    {
        return landform switch
        {
            // ── 海洋构造 ──
            LandformType.Ocean => Color.FromHtml("#16325c"),
            LandformType.DeepOcean => Color.FromHtml("#0b1e3b"),
            LandformType.Trench => Color.FromHtml("#050e1c"),
            LandformType.ShallowOcean => Color.FromHtml("#2e6396"),
            LandformType.MidOceanRidge => Color.FromHtml("#1c4475"),
            LandformType.Island => Color.FromHtml("#76a5af"),
            LandformType.Coast => Color.FromHtml("#a2c4c9"),

            // ── 大地构造 ──
            LandformType.Plain => Color.FromHtml("#6aa84f"),
            LandformType.Basin => Color.FromHtml("#38761d"),
            LandformType.Plateau => Color.FromHtml("#e69138"),
            LandformType.Hill => Color.FromHtml("#b6d7a8"),
            LandformType.Mountain => Color.FromHtml("#b45f06"),
            LandformType.Peak => Color.FromHtml("#e6e0d5"),
            LandformType.Volcano => Color.FromHtml("#990000"),
            LandformType.RiftValley => Color.FromHtml("#7d5435"),

            // ── 流水水文 ──
            LandformType.Floodplain => Color.FromHtml("#549b38"),
            LandformType.Delta => Color.FromHtml("#459e74"),
            LandformType.Canyon => Color.FromHtml("#a07447"),
            LandformType.Wetland => Color.FromHtml("#387d60"),

            // ── 气候与特殊岩性 ──
            LandformType.DryBasin => Color.FromHtml("#a69566"),
            LandformType.Karst => Color.FromHtml("#689c62"),
            LandformType.DesertDune => Color.FromHtml("#dfbc60"),
            LandformType.Badlands => Color.FromHtml("#b86e3f"),
            LandformType.Glacier => Color.FromHtml("#cbe3f5"),
            LandformType.Fjord => Color.FromHtml("#3f738a"),

            _ => Colors.Gray
        };
    }

    /// <summary>
    /// 板块归属底色。交界信息不在这里表达——逐地块涂色只会糊出一条与多边形网格同宽的锯齿带，
    /// 断裂带走向由矢量折线叠加层绘制。
    /// </summary>
    public static Color GetPlateColor(WorldSnapshot snapshot, int cellId)
    {
        var plateId = snapshot.Fields.PlateId[cellId];

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
        var baseColor = idx >= 0 && idx < OreColors.Length ? OreColors[idx] : Color.FromHtml("#EAE6DF");

        // 海洋中的矿产（如大陆架石油/天然气、海底裂谷稀有金属/空冥晶）：微透海色以示水下
        if (elevation <= seaLevel)
        {
            return baseColor.Lerp(ShallowOcean, 0.22f);
        }

        return baseColor;
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

    /// <summary>
    /// 中国古代手绘山水舆图着色：以宣纸地子承托青绿设色。
    /// 海拔驱动"青绿丘陵 → 浅绛赭石 → 靛墨山脊 → 浓墨高山"的晕染阶，湿度驱动冷暖，
    /// 水系敷碧蓝，大漠铺淡金，火山点朱砂，京畿染暖金以暗示古都宫室；
    /// 留白只落在又高又寒的山巅，避免中低纬高山被误读成雪原。
    /// </summary>
    private static Color GetInkWashLandscapeColor(WorldSnapshot snapshot, int cellId)
    {
        var fields = snapshot.Fields;
        var seaLevel = snapshot.Options.SeaLevel;
        var h = fields.Height[cellId];
        var landform = (LandformType)fields.Landform[cellId];
        var biome = (BiomeType)fields.Biome[cellId];

        var mapHeight = (float)snapshot.Geometry.Height;
        var lat = Mathf.Abs((float)snapshot.Geometry.CentroidY[cellId] - mapHeight * 0.5f) / Mathf.Max(mapHeight * 0.5f, 0.0001f);
        var polar = Mathf.Clamp((lat - 0.68f) / 0.32f, 0f, 1f);

        if (h <= seaLevel)
        {
            var depth = Mathf.Clamp((seaLevel - h) / Mathf.Max(seaLevel, 0.0001f), 0f, 1f);
            var water = InkSeaShallow.Lerp(InkSeaMid, InkBand(depth, 0.02f, 0.34f));
            water = water.Lerp(InkSeaAbyss, InkBand(depth, 0.42f, 1f));
            if (landform == LandformType.Trench) water = water.Lerp(InkSeaTrench, 0.6f);
            if (biome == BiomeType.Ice) water = water.Lerp(InkPolarWash, 0.86f);
            else water = water.Lerp(InkPolarWash, polar * 0.55f);
            return ApplyRicePaperGrain(water, cellId);
        }

        var landT = Mathf.Clamp((h - seaLevel) / Mathf.Max(1f - seaLevel, 0.0001f), 0f, 1f);
        var moist = Mathf.Clamp(fields.Moisture[cellId], 0f, 1f);

        // 宣纸地子：湿润渗淡青绿，干旱铺淡金
        var color = InkPaperPlain.Lerp(InkPlainWet, InkBand(moist, 0.5f, 0.95f) * 0.72f);
        color = color.Lerp(InkPlainArid, InkBand(1f - moist, 0.6f, 0.98f) * 0.8f);

        // 滨海沙嘴
        color = color.Lerp(InkCoastSand, (1f - InkBand(landT, 0f, 0.035f)) * 0.68f);

        // 京畿腹地：文明核心区在低地染淡金
        var lowland = 1f - InkBand(landT, 0.16f, 0.34f);
        color = color.Lerp(InkHeartlandGold, InkBand(fields.Influence[cellId], 0.28f, 0.92f) * 0.4f * lowland);

        // 山势阶次：青绿丘陵 → 浅绛山麓 → 靛墨山脊 → 浓墨高山
        color = color.Lerp(InkHillGreen, InkBand(landT, 0.09f, 0.40f) * 0.86f);
        var ochre = InkBand(landT, 0.26f, 0.52f) * (1f - InkBand(landT, 0.56f, 0.80f));
        color = color.Lerp(InkOchreFoothill, ochre * 0.48f);
        color = color.Lerp(InkMountainIndigo, InkBand(landT, 0.42f, 0.72f) * 0.9f);
        color = color.Lerp(InkMountainDark, InkBand(landT, 0.62f, 0.90f) * 0.88f);

        // 高寒留白：只有又高又冷的山巅才提亮纸面，中低纬高山保留浓墨
        var chill = Mathf.Clamp(1f - fields.Temperature[cellId], 0f, 1f);
        color = color.Lerp(InkPeakWhite, InkBand(landT, 0.80f, 0.98f) * InkBand(chill, 0.42f, 0.78f) * 0.94f);

        // 高纬苦寒：墨色褪为淡墨留白
        color = color.Lerp(InkPolarWash, polar * 0.62f);

        if (landT < 0.45f)
        {
            if (biome == BiomeType.TropicalDesert) color = color.Lerp(InkDesertGold, 0.5f);
            else if (biome == BiomeType.TemperateDesert) color = color.Lerp(InkDesertGold, 0.34f);
        }
        if (biome == BiomeType.Ice) color = color.Lerp(InkPolarWash, 0.85f);
        else if (biome == BiomeType.Tundra) color = color.Lerp(InkPolarWash, 0.72f);
        else if (biome == BiomeType.SnowyMountain) color = color.Lerp(InkPeakWhite, 0.66f);

        // 碧蓝水系：汇流越强，青蓝越饱和
        var river = Mathf.Clamp(fields.River[cellId], 0f, 1f);
        if (river > 0.04f)
        {
            color = color.Lerp(InkRiverAzure, 0.42f + 0.46f * InkBand(river, 0.04f, 0.42f));
        }

        // 朱砂火山
        if (landform == LandformType.Volcano) color = color.Lerp(InkCinnabar, 0.6f);

        return ApplyRicePaperGrain(color, cellId);
    }

    /// <summary>平滑阶次权重，用于模拟水墨在纸面上的晕染过渡。</summary>
    private static float InkBand(float value, float start, float end)
    {
        var t = Mathf.Clamp((value - start) / Mathf.Max(end - start, 0.0001f), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// 叠加确定性的宣纸纤维颗粒与暖色偏差。
    /// 以地块 ID 作哈希种子，保证矢量网格、光栅底图与 PNG 导出三处采样完全一致。
    private static readonly Color ForestGroundWash = Color.FromHtml("#cdd0bc");
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<WorldSnapshot, bool[]> _forestMaskCache = new();

    private static bool[] BuildForestAndEnclosedMask(WorldSnapshot snapshot)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var count = geom.Count;
        var seaLevel = snapshot.Options.SeaLevel;
        var isForest = new bool[count];

        for (var i = 0; i < count; i++)
        {
            if (fields.Height[i] <= seaLevel) continue;
            var biome = (BiomeType)fields.Biome[i];
            isForest[i] = biome is BiomeType.TemperateRainForest
                                or BiomeType.TemperateSeasonalForest
                                or BiomeType.Taiga
                                or BiomeType.BorealForest
                                or BiomeType.TropicalRainForest
                                or BiomeType.TropicalSeasonalForest;
        }

        // 识别被森林环绕/封闭的内部空地（森林包围区）
        // 迭代 2 轮形态学邻域扩张：若非森林陆地地块的绝大部分陆地邻居都是森林，则归并为森林地色
        var enclosed = (bool[])isForest.Clone();
        for (var pass = 0; pass < 2; pass++)
        {
            var next = (bool[])enclosed.Clone();
            for (var i = 0; i < count; i++)
            {
                if (enclosed[i] || fields.Height[i] <= seaLevel) continue;

                var start = geom.CellNeighborStart[i];
                var end = geom.CellNeighborStart[i + 1];
                var landNeighbors = 0;
                var forestNeighbors = 0;

                for (var k = start; k < end; k++)
                {
                    var nb = geom.CellNeighbors[k];
                    if (fields.Height[nb] > seaLevel)
                    {
                        landNeighbors++;
                        if (enclosed[nb]) forestNeighbors++;
                    }
                }

                // 若该陆地地块的大半陆地邻居(>= 50%)或至少 3 个邻居已被森林占据，属于被森林环抱的林间空隙
                if (landNeighbors > 0 && (forestNeighbors * 2 >= landNeighbors || forestNeighbors >= 3))
                {
                    next[i] = true;
                }
            }
            enclosed = next;
        }

        return enclosed;
    }

    /// <summary>
    /// 国风手绘舆图宣纸淡彩底色：以古法宣纸为底，海岸线四重自然渐变（陆地→沙滩→浅海→深海），
    /// 降低饱和度 20%，增温微暖宣纸绢丝基底，森林敷温润青瓷苔绿地晕。
    /// </summary>
    private static Color GetGuohuaLandscapeBaseColor(WorldSnapshot snapshot, int cellId)
    {
        var fields = snapshot.Fields;
        var seaLevel = snapshot.Options.SeaLevel;
        var h = fields.Height[cellId];

        // ── 1. 水域与海岸四重渐变阶（Ocean & Coastal Layer） ──
        if (h <= seaLevel)
        {
            var depth = Mathf.Clamp((seaLevel - h) / Mathf.Max(seaLevel, 0.0001f), 0f, 1f);
            
            // 沧溟深海：深沉温润的靛蓝墨韵 (#1e3e5c ~ #28567a)，浅海清澈碧蓝 (#3a6f94)，潮间带洁净浪花白 (#e4e0d4)
            var waterTidalSand = new Color(0.89f, 0.87f, 0.80f, 1f);
            var waterShallow = new Color(0.24f, 0.46f, 0.62f, 1f);   // 浅海青碧蓝
            var waterDeep = new Color(0.12f, 0.25f, 0.38f, 1f);      // 深海墨蓝

            Color water;
            if (depth < 0.08f)
            {
                water = waterTidalSand.Lerp(waterShallow, depth / 0.08f);
            }
            else if (depth < 0.40f)
            {
                water = waterShallow.Lerp(waterDeep, (depth - 0.08f) / 0.32f);
            }
            else
            {
                water = waterDeep;
            }

            return ApplyGuohuaColorGrading(water, cellId);
        }

        var mapWidth = (float)snapshot.Geometry.Width;
        var mapHeight = (float)snapshot.Geometry.Height;
        var cx = (float)snapshot.Geometry.CentroidX[cellId];
        var cy = (float)snapshot.Geometry.CentroidY[cellId];
        var nx = cx / Mathf.Max(mapWidth, 1.0f);
        var ny = cy / Mathf.Max(mapHeight, 1.0f);

        var landT = Mathf.Clamp((h - seaLevel) / Mathf.Max(1f - seaLevel, 0.0001f), 0f, 1f);
        var moist = Mathf.Clamp(fields.Moisture[cellId], 0f, 1f);
        var temp = Mathf.Clamp(fields.Temperature[cellId], 0f, 1f);
        var landform = (LandformType)fields.Landform[cellId];
        var biome = (BiomeType)fields.Biome[cellId];

        // ── 2. 区分三大核心地貌底色：荒漠金黄、高山寒雪、平原与森林青绿 ──
        
        // 判断是否为沙漠与干旱荒芜区（根据生态群系与气候温湿分布判定）
        var isDesert = biome == BiomeType.TropicalDesert 
                    || biome == BiomeType.TemperateDesert 
                    || (moist < 0.22f && temp > 0.45f && landT < 0.55f);

        // 判断是否为高洁雪原与极顶山系（根据生态群系与高山积雪线判定）
        var isSnowMountain = biome == BiomeType.SnowyMountain 
                          || biome == BiomeType.Tundra 
                          || biome == BiomeType.Ice 
                          || (temp < 0.20f && landT > 0.35f)
                          || landT > 0.78f;

        Color color;

        if (isDesert)
        {
            // ── 【沙漠地区以黄色作为底图】 ──
            // 明净温暖的大漠金黄与赭黄藤黄水墨色 (#dfba6c ~ #ebcd7e)
            var desertGold = new Color(0.88f, 0.74f, 0.44f, 1f);
            var desertOchre = new Color(0.84f, 0.68f, 0.38f, 1f);
            var aridWeight = Math.Clamp((0.40f - moist) / 0.35f, 0.2f, 1f);
            color = desertGold.Lerp(desertOchre, aridWeight * 0.4f);
        }
        else if (isSnowMountain)
        {
            // ── 【北方极顶雪域与寒原】 ──
            // 纯净冷白与山石青黛淡墨
            var snowIvory = new Color(0.92f, 0.94f, 0.93f, 1f);
            var mountainSlate = new Color(0.72f, 0.78f, 0.76f, 1f);
            color = mountainSlate.Lerp(snowIvory, Math.Clamp((landT - 0.40f) * 2.5f, 0f, 1f));
        }
        else
        {
            // ── 【森林和生活区域以绿地作为底图】 ──
            // 涵盖中原河谷文明走廊（神京、江陵、丰泽、浔阳周边农田生活区）、西北密林、东部古老森林、南部湿地
            if (!_forestMaskCache.TryGetValue(snapshot, out var forestMask))
            {
                forestMask = BuildForestAndEnclosedMask(snapshot);
                _forestMaskCache.AddOrUpdate(snapshot, forestMask);
            }

            // 生活与农业平原：清润柔和的草绿与水稻沃土色调 (#9fbe7c ~ #a8c886)
            var livingPlainsGreen = new Color(0.61f, 0.75f, 0.48f, 1f);
            
            // 森林腹地：更加沉静苍翠的黛绿 (#789f64)
            var forestDeepGreen = new Color(0.47f, 0.63f, 0.39f, 1f);

            // 湿地区域：青碧水草绿 (#82aa82)
            var wetlandGreen = new Color(0.51f, 0.69f, 0.51f, 1f);

            if (forestMask[cellId])
            {
                color = forestDeepGreen;
            }
            else if (moist >= 0.70f || landform == LandformType.Basin)
            {
                color = livingPlainsGreen.Lerp(wetlandGreen, 0.45f);
            }
            else
            {
                // 生活区 / 平原 / 农田 / 山麓平缓区：统一为青绿地表
                color = livingPlainsGreen;
                if (landT > 0.30f)
                {
                    // 丘陵微带青黛浅绛
                    var hillOchre = new Color(0.66f, 0.72f, 0.50f, 1f);
                    color = color.Lerp(hillOchre, (landT - 0.30f) * 1.5f);
                }
            }
        }

        // ── 3. 沿海沙滩过渡环带（Beach Sand Buffer） ──
        // 在濒临海水的陆地边缘（landT < 0.085f），平滑过渡到象牙米白金沙色，与范例图中的金色海岸完全吻合
        if (landT < 0.085f)
        {
            var beachSand = new Color(0.92f, 0.86f, 0.72f, 1f); // 象牙暖金沙色
            var beachWeight = 1f - (landT / 0.085f);
            color = color.Lerp(beachSand, beachWeight * 0.85f);
        }

        return ApplyGuohuaColorGrading(color, cellId);
    }

    /// <summary>
    /// 全局古地图色调校正 (Color Grading)：
    /// 微调纸性与微暖色调，保留矿物青绿与大漠藤黄自然鲜活度（不过度去饱和），并叠加纸质纤维微粒。
    /// </summary>
    private static Color ApplyGuohuaColorGrading(Color c, int cellId)
    {
        // 1. 保留 95% 原生饱和度（杜绝过度灰白化）
        var gray = 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
        var r = Mathf.Lerp(gray, c.R, 0.95f);
        var g = Mathf.Lerp(gray, c.G, 0.95f);
        var b = Mathf.Lerp(gray, c.B, 0.95f);

        // 2. 宣纸古雅微温韵味
        r += 0.008f;
        g += 0.006f;
        b -= 0.006f;

        var graded = new Color(Mathf.Clamp(r, 0f, 1f), Mathf.Clamp(g, 0f, 1f), Mathf.Clamp(b, 0f, 1f), c.A);
        return ApplyRicePaperGrain(graded, cellId);
    }

    /// <summary>
    /// 叠加确定性的宣纸纤维颗粒与暖色偏差。
    /// 以地块 ID 作哈希种子，保证矢量网格、光栅底图与 PNG 导出三处采样完全一致。
    /// </summary>
    private static Color ApplyRicePaperGrain(Color color, int cellId)
    {
        var hash = unchecked((uint)(cellId * 2654435761u + 4073u));
        var fiber = ((hash & 0xFFFFu) / 65535f) * 2f - 1f;
        var tone = 1f + fiber * 0.032f;
        var warm = (((hash >> 16) & 0xFFu) / 255f - 0.5f) * 0.016f;

        return new Color(
            Mathf.Clamp(color.R * tone + warm, 0f, 1f),
            Mathf.Clamp(color.G * tone + warm * 0.35f, 0f, 1f),
            Mathf.Clamp(color.B * tone - warm * 0.45f, 0f, 1f),
            1f);
    }
}
