using System;
using System.Collections.Generic;

namespace PlanetGeneration.Core.Layers;

/// <summary>图层组合预设。</summary>
public sealed record LayerPreset
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string BaseThemeId { get; init; }
    public required IReadOnlyList<string> ActiveOverlayIds { get; init; }
    public bool IsBuiltIn { get; init; }
}

/// <summary>
/// 图层预设目录管理。
/// </summary>
public static class LayerPresetCatalog
{
    public const string PresetPhysical = "physical_geography";
    public const string PresetPolitical = "political_civilization";
    public const string PresetTrade = "trade_network";
    public const string PresetClimate = "climate_analysis";
    public const string PresetWindPrecipitation = "wind_precipitation";
    public const string PresetGeology = "geology_resources";
    public const string PresetCellDebug = "cell_debug";

    private static readonly Dictionary<string, LayerPreset> _presets = new(StringComparer.OrdinalIgnoreCase);

    static LayerPresetCatalog()
    {
        Register(new LayerPreset
        {
            Id = PresetPhysical,
            DisplayName = "自然地理",
            BaseThemeId = LayerRegistry.LayerTerrainOverview,
            ActiveOverlayIds = new[] { LayerRegistry.LayerRivers, LayerRegistry.LayerCities, LayerRegistry.LayerCityLabels },
            IsBuiltIn = true
        });

        Register(new LayerPreset
        {
            Id = PresetPolitical,
            DisplayName = "政治文明",
            BaseThemeId = LayerRegistry.LayerCivilization,
            ActiveOverlayIds = new[] { LayerRegistry.LayerCities, LayerRegistry.LayerCityLabels, LayerRegistry.LayerPolityBorders },
            IsBuiltIn = true
        });

        Register(new LayerPreset
        {
            Id = PresetTrade,
            DisplayName = "贸易网络",
            BaseThemeId = LayerRegistry.LayerTradeFlow,
            ActiveOverlayIds = new[] { LayerRegistry.LayerCities, LayerRegistry.LayerTradeRoutes, LayerRegistry.LayerPolityBorders },
            IsBuiltIn = true
        });

        Register(new LayerPreset
        {
            Id = PresetWindPrecipitation,
            DisplayName = "风场降水",
            BaseThemeId = LayerRegistry.LayerMoisture,
            ActiveOverlayIds = new[] { LayerRegistry.LayerCoastlines, LayerRegistry.LayerWindArrows },
            IsBuiltIn = true
        });

        Register(new LayerPreset
        {
            Id = PresetClimate,
            DisplayName = "气候分析",
            BaseThemeId = LayerRegistry.LayerTemperature,
            ActiveOverlayIds = new[] { LayerRegistry.LayerCoastlines, LayerRegistry.LayerWindArrows },
            IsBuiltIn = true
        });

        Register(new LayerPreset
        {
            Id = PresetGeology,
            DisplayName = "地质资源",
            BaseThemeId = LayerRegistry.LayerPlates,
            ActiveOverlayIds = new[] { LayerRegistry.LayerPlateBorders, LayerRegistry.LayerCellBorders },
            IsBuiltIn = true
        });

        Register(new LayerPreset
        {
            Id = PresetCellDebug,
            DisplayName = "地块调试",
            BaseThemeId = LayerRegistry.LayerCellGrid,
            ActiveOverlayIds = new[] { LayerRegistry.LayerCellBorders, LayerRegistry.LayerCities },
            IsBuiltIn = true
        });
    }

    public static void Register(LayerPreset preset)
    {
        _presets[preset.Id] = preset;
    }

    public static bool TryGet(string presetId, out LayerPreset preset)
        => _presets.TryGetValue(presetId, out preset!);

    public static IEnumerable<LayerPreset> GetAllPresets() => _presets.Values;

    public static bool ApplyPreset(string presetId, LayerStackState state)
    {
        if (!TryGet(presetId, out var preset))
        {
            return false;
        }

        state.SetBaseTheme(preset.BaseThemeId);

        // 刷新叠加层
        var activeSet = new HashSet<string>(preset.ActiveOverlayIds, StringComparer.OrdinalIgnoreCase);
        foreach (var def in LayerRegistry.GetAllOverlays())
        {
            state.SetOverlayActive(def.Id, activeSet.Contains(def.Id));
        }

        return true;
    }
}
