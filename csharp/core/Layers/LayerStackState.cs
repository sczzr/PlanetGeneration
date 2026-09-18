using System;
using System.Collections.Generic;

namespace PlanetGeneration.Core.Layers;

/// <summary>
/// 图层栈运行时状态（底图单选 + 叠加层多选与排序）。
/// </summary>
public sealed class LayerStackState
{
    public string ActiveBaseThemeId { get; private set; } = LayerRegistry.LayerTerrainOverview;
    public List<string> ActiveOverlayIds { get; private set; } = new();
    public Dictionary<string, float> Opacities { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, float> WidthOrSizes { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public LayerStackState()
    {
        ActiveBaseThemeId = LayerRegistry.LayerTerrainOverview;
        ActiveOverlayIds = new List<string>
        {
            LayerRegistry.LayerRivers,
            LayerRegistry.LayerCities,
            LayerRegistry.LayerCityLabels
        };

        foreach (var def in LayerRegistry.GetAllLayers())
        {
            Opacities[def.Id] = def.DefaultOpacity;
            WidthOrSizes[def.Id] = def.DefaultWidthOrSize;
        }
    }

    public void SetBaseTheme(string id)
    {
        if (LayerRegistry.TryGet(id, out var def) && def.Category == LayerCategory.BaseTheme)
        {
            ActiveBaseThemeId = def.Id;
        }
    }

    public bool IsOverlayActive(string id) => ActiveOverlayIds.Contains(id);

    public void SetOverlayActive(string id, bool active)
    {
        if (!LayerRegistry.TryGet(id, out var def) || def.Category != LayerCategory.Overlay)
        {
            return;
        }

        if (active && !ActiveOverlayIds.Contains(def.Id))
        {
            ActiveOverlayIds.Add(def.Id);
        }
        else if (!active && ActiveOverlayIds.Contains(def.Id))
        {
            ActiveOverlayIds.Remove(def.Id);
        }

        // 聚落城镇与城市名称联动，防止关闭城市时 city_labels 孤立残留继续绘制
        if (string.Equals(def.Id, LayerRegistry.LayerCities, StringComparison.OrdinalIgnoreCase))
        {
            if (active && !ActiveOverlayIds.Contains(LayerRegistry.LayerCityLabels))
            {
                ActiveOverlayIds.Add(LayerRegistry.LayerCityLabels);
            }
            else if (!active && ActiveOverlayIds.Contains(LayerRegistry.LayerCityLabels))
            {
                ActiveOverlayIds.Remove(LayerRegistry.LayerCityLabels);
            }
        }
    }

    public void ToggleOverlay(string id) => SetOverlayActive(id, !IsOverlayActive(id));

    public bool MoveOverlayUp(string id)
    {
        var idx = ActiveOverlayIds.IndexOf(id);
        if (idx > 0)
        {
            (ActiveOverlayIds[idx], ActiveOverlayIds[idx - 1]) = (ActiveOverlayIds[idx - 1], ActiveOverlayIds[idx]);
            return true;
        }
        return false;
    }

    public bool MoveOverlayDown(string id)
    {
        var idx = ActiveOverlayIds.IndexOf(id);
        if (idx >= 0 && idx < ActiveOverlayIds.Count - 1)
        {
            (ActiveOverlayIds[idx], ActiveOverlayIds[idx + 1]) = (ActiveOverlayIds[idx + 1], ActiveOverlayIds[idx]);
            return true;
        }
        return false;
    }

    public float GetOpacity(string id)
    {
        return Opacities.TryGetValue(id, out var val) ? val : 1.0f;
    }

    public void SetOpacity(string id, float opacity)
    {
        Opacities[id] = Math.Clamp(opacity, 0f, 1f);
    }

    public float GetWidthOrSize(string id)
    {
        return WidthOrSizes.TryGetValue(id, out var val) ? val : 1.0f;
    }

    public void SetWidthOrSize(string id, float val)
    {
        WidthOrSizes[id] = Math.Max(val, 0.1f);
    }

    public LayerStackState Clone()
    {
        var clone = new LayerStackState
        {
            ActiveBaseThemeId = ActiveBaseThemeId,
            ActiveOverlayIds = new List<string>(ActiveOverlayIds),
            Opacities = new Dictionary<string, float>(Opacities, StringComparer.OrdinalIgnoreCase),
            WidthOrSizes = new Dictionary<string, float>(WidthOrSizes, StringComparer.OrdinalIgnoreCase),
        };
        return clone;
    }
}
