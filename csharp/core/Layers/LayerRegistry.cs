using System;
using System.Collections.Generic;

namespace PlanetGeneration.Core.Layers;

/// <summary>
/// 全局可用图层清单注册表。
/// 使用稳定字符串 ID 注册，不依赖枚举顺序。
/// </summary>
public static class LayerRegistry
{
    // ── 稳定图层 ID 常量 ──
    public const string LayerTerrainOverview = "terrain_overview";
    public const string LayerElevation = "elevation";
    public const string LayerLandform = "landform";
    public const string LayerBiomes = "biomes";
    public const string LayerTemperature = "temperature";
    public const string LayerMoisture = "moisture";
    public const string LayerPlates = "plates";
    public const string LayerRockTypes = "rock_types";
    public const string LayerOres = "ores";
    public const string LayerEcology = "ecology";
    public const string LayerCivilization = "civilization";
    public const string LayerTradeFlow = "trade_flow";
    public const string LayerCellGrid = "cell_grid";

    public const string LayerRivers = "rivers";
    public const string LayerCoastlines = "coastlines";
    public const string LayerCities = "cities";
    public const string LayerCityLabels = "city_labels";
    public const string LayerPolityBorders = "polity_borders";
    public const string LayerTradeRoutes = "trade_routes";
    public const string LayerWindArrows = "wind_arrows";
    public const string LayerCellBorders = "cell_borders";
    public const string LayerPlateBorders = "plate_borders";

    private static readonly Dictionary<string, LayerDefinition> _layers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<LayerDefinition> _baseThemes = new();
    private static readonly List<LayerDefinition> _overlays = new();

    static LayerRegistry()
    {
        // ── 主题底图（单选互斥）──
        Register(new LayerDefinition
        {
            Id = LayerTerrainOverview,
            DisplayName = "地形总览",
            GroupName = "地理",
            Category = LayerCategory.BaseTheme,
            DrawBand = LayerDrawBand.BaseMesh,
            IsDefaultActive = true,
            DataDependencies = new[] { "Height", "Temperature", "Moisture", "Biome", "River" }
        });

        Register(new LayerDefinition
        {
            Id = LayerElevation,
            DisplayName = "高程",
            GroupName = "地理",
            Category = LayerCategory.BaseTheme,
            DrawBand = LayerDrawBand.BaseMesh,
            DataDependencies = new[] { "Height" }
        });

        Register(new LayerDefinition
        {
            Id = LayerLandform,
            DisplayName = "地貌",
            GroupName = "地理",
            Category = LayerCategory.BaseTheme,
            DrawBand = LayerDrawBand.BaseMesh,
            DataDependencies = new[] { "Landform" }
        });

        Register(new LayerDefinition
        {
            Id = LayerBiomes,
            DisplayName = "生物群系",
            GroupName = "地理",
            Category = LayerCategory.BaseTheme,
            DrawBand = LayerDrawBand.BaseMesh,
            DataDependencies = new[] { "Biome" }
        });

        Register(new LayerDefinition
        {
            Id = LayerTemperature,
            DisplayName = "温度",
            GroupName = "气候",
            Category = LayerCategory.BaseTheme,
            DrawBand = LayerDrawBand.BaseMesh,
            DataDependencies = new[] { "Temperature" }
        });

        Register(new LayerDefinition
        {
            Id = LayerMoisture,
            DisplayName = "湿度",
            GroupName = "气候",
            Category = LayerCategory.BaseTheme,
            DrawBand = LayerDrawBand.BaseMesh,
            DataDependencies = new[] { "Moisture" }
        });

        Register(new LayerDefinition
        {
            Id = LayerPlates,
            DisplayName = "板块构造",
            GroupName = "地质",
            Category = LayerCategory.BaseTheme,
            DrawBand = LayerDrawBand.BaseMesh,
            DataDependencies = new[] { "PlateId", "PlateBoundary" }
        });

        Register(new LayerDefinition
        {
            Id = LayerRockTypes,
            DisplayName = "岩石类型",
            GroupName = "地质",
            Category = LayerCategory.BaseTheme,
            DrawBand = LayerDrawBand.BaseMesh,
            DataDependencies = new[] { "Rock", "Height" }
        });

        Register(new LayerDefinition
        {
            Id = LayerOres,
            DisplayName = "矿产资源",
            GroupName = "地质",
            Category = LayerCategory.BaseTheme,
            DrawBand = LayerDrawBand.BaseMesh,
            DataDependencies = new[] { "Ore", "Height" }
        });

        Register(new LayerDefinition
        {
            Id = LayerEcology,
            DisplayName = "生态状态",
            GroupName = "人文",
            Category = LayerCategory.BaseTheme,
            DrawBand = LayerDrawBand.BaseMesh,
            DataDependencies = new[] { "EcologyHealth", "Height" }
        });

        Register(new LayerDefinition
        {
            Id = LayerCivilization,
            DisplayName = "文明疆域",
            GroupName = "人文",
            Category = LayerCategory.BaseTheme,
            DrawBand = LayerDrawBand.BaseMesh,
            DataDependencies = new[] { "Influence", "PolityId", "BorderMask", "Height" }
        });

        Register(new LayerDefinition
        {
            Id = LayerTradeFlow,
            DisplayName = "贸易强度",
            GroupName = "人文",
            Category = LayerCategory.BaseTheme,
            DrawBand = LayerDrawBand.BaseMesh,
            DataDependencies = new[] { "TradeRouteMask", "TradeFlow", "Height" }
        });

        Register(new LayerDefinition
        {
            Id = LayerCellGrid,
            DisplayName = "地块网格",
            GroupName = "诊断",
            Category = LayerCategory.BaseTheme,
            DrawBand = LayerDrawBand.BaseMesh,
            DataDependencies = Array.Empty<string>()
        });

        // ── 叠加层（独立多选）──
        Register(new LayerDefinition
        {
            Id = LayerRivers,
            DisplayName = "河流水系",
            GroupName = "水文",
            Category = LayerCategory.Overlay,
            DrawBand = LayerDrawBand.Lines,
            IsDefaultActive = true,
            DefaultWidthOrSize = 1.6f,
            DataDependencies = new[] { "River", "Flux" }
        });

        Register(new LayerDefinition
        {
            Id = LayerCoastlines,
            DisplayName = "海岸轮廓",
            GroupName = "水文",
            Category = LayerCategory.Overlay,
            DrawBand = LayerDrawBand.Lines,
            IsDefaultActive = false,
            DefaultWidthOrSize = 1.0f,
            DataDependencies = new[] { "Height" }
        });

        Register(new LayerDefinition
        {
            Id = LayerCities,
            DisplayName = "城市聚落",
            GroupName = "人文",
            Category = LayerCategory.Overlay,
            DrawBand = LayerDrawBand.Symbols,
            IsDefaultActive = true,
            DefaultWidthOrSize = 6.0f,
            DataDependencies = new[] { "CityId" }
        });

        Register(new LayerDefinition
        {
            Id = LayerCityLabels,
            DisplayName = "城市名称",
            GroupName = "人文",
            Category = LayerCategory.Overlay,
            DrawBand = LayerDrawBand.Labels,
            IsDefaultActive = true,
            DefaultWidthOrSize = 12.0f,
            DataDependencies = new[] { "CityId" }
        });

        Register(new LayerDefinition
        {
            Id = LayerPolityBorders,
            DisplayName = "政体边界",
            GroupName = "人文",
            Category = LayerCategory.Overlay,
            DrawBand = LayerDrawBand.Lines,
            IsDefaultActive = false,
            DefaultWidthOrSize = 2.0f,
            DataDependencies = new[] { "PolityId", "BorderMask" }
        });

        Register(new LayerDefinition
        {
            Id = LayerTradeRoutes,
            DisplayName = "贸易走廊",
            GroupName = "人文",
            Category = LayerCategory.Overlay,
            DrawBand = LayerDrawBand.Lines,
            IsDefaultActive = false,
            DefaultWidthOrSize = 2.0f,
            DataDependencies = new[] { "TradeRouteMask", "TradeFlow" }
        });

        Register(new LayerDefinition
        {
            Id = LayerWindArrows,
            DisplayName = "风向箭头",
            GroupName = "诊断",
            Category = LayerCategory.Overlay,
            DrawBand = LayerDrawBand.Symbols,
            IsDefaultActive = false,
            DefaultWidthOrSize = 1.0f,
            DataDependencies = Array.Empty<string>()
        });

        Register(new LayerDefinition
        {
            Id = LayerCellBorders,
            DisplayName = "地块线框",
            GroupName = "诊断",
            Category = LayerCategory.Overlay,
            DrawBand = LayerDrawBand.Lines,
            IsDefaultActive = false,
            DefaultOpacity = 0.45f,
            DefaultWidthOrSize = 1.0f,
            DataDependencies = Array.Empty<string>()
        });

        Register(new LayerDefinition
        {
            Id = LayerPlateBorders,
            DisplayName = "板块边界",
            GroupName = "诊断",
            Category = LayerCategory.Overlay,
            DrawBand = LayerDrawBand.Lines,
            IsDefaultActive = false,
            DefaultWidthOrSize = 2.4f,
            DataDependencies = new[] { "PlateBoundary" }
        });
    }

    private static void Register(LayerDefinition layer)
    {
        _layers[layer.Id] = layer;
        if (layer.Category == LayerCategory.BaseTheme)
        {
            _baseThemes.Add(layer);
        }
        else
        {
            _overlays.Add(layer);
        }
    }

    public static bool TryGet(string id, out LayerDefinition definition)
        => _layers.TryGetValue(id, out definition!);

    public static IReadOnlyList<LayerDefinition> GetAllBaseThemes() => _baseThemes;
    public static IReadOnlyList<LayerDefinition> GetAllOverlays() => _overlays;
    public static IReadOnlyCollection<LayerDefinition> GetAllLayers() => _layers.Values;
}
