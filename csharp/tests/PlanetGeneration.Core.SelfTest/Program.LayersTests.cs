using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PlanetGeneration.Core.Cartography;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Cartography.Generator;
using PlanetGeneration.Core.Cartography.Planning;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;
using PlanetGeneration.Core.Layers;
using PlanetGeneration.Core.Simulation;

namespace PlanetGeneration.Core.SelfTest;

internal static partial class Program
{
    private static void TestLayerSystemAndPresets()
    {
        var stack = new LayerStackState();
        Assert(stack.ActiveBaseThemeId == LayerRegistry.LayerTerrainOverview, "默认底图应为地形总览");

        // 底图单选互斥
        stack.SetBaseTheme(LayerRegistry.LayerBiomes);
        Assert(stack.ActiveBaseThemeId == LayerRegistry.LayerBiomes, "底图切换为群系失败");

        stack.SetBaseTheme(LayerRegistry.LayerElevation);
        Assert(stack.ActiveBaseThemeId == LayerRegistry.LayerElevation, "底图切换为高程失败");

        // 叠加层多选与排序
        stack.SetOverlayActive(LayerRegistry.LayerRivers, true);
        stack.SetOverlayActive(LayerRegistry.LayerCities, true);
        stack.SetOverlayActive(LayerRegistry.LayerPolityBorders, true);

        Assert(stack.IsOverlayActive(LayerRegistry.LayerRivers), "河流叠加层未激活");
        Assert(stack.IsOverlayActive(LayerRegistry.LayerPolityBorders), "政体边界叠加层未激活");

        var oldTop = stack.ActiveOverlayIds[0];
        stack.MoveOverlayDown(oldTop);
        Assert(stack.ActiveOverlayIds[1] == oldTop, "叠加层下移顺序错误");

        // 预设应用：验证河流默认只在地形总览中开启显示
        var ok = LayerPresetCatalog.ApplyPreset(LayerPresetCatalog.PresetPolitical, stack);
        Assert(ok, "政治文明预设应用失败");
        Assert(stack.ActiveBaseThemeId == LayerRegistry.LayerCivilization, "政治文明预设底图应为文明疆域");
        Assert(stack.IsOverlayActive(LayerRegistry.LayerPolityBorders), "政治文明预设应包含政体边界");
        Assert(!stack.IsOverlayActive(LayerRegistry.LayerRivers), "政治文明预设默认不应包含河流（河流仅在地形总览中默认开启）");

        var okClimate = LayerPresetCatalog.ApplyPreset(LayerPresetCatalog.PresetClimate, stack);
        Assert(okClimate, "气候分析预设应用失败");
        Assert(!stack.IsOverlayActive(LayerRegistry.LayerRivers), "气候分析预设默认不应包含河流");
        Assert(stack.IsOverlayActive(LayerRegistry.LayerWindArrows), "气候分析预设应包含风向箭头");

        var okWind = LayerPresetCatalog.ApplyPreset(LayerPresetCatalog.PresetWindPrecipitation, stack);
        Assert(okWind, "风场降水预设应用失败");
        Assert(stack.ActiveBaseThemeId == LayerRegistry.LayerMoisture, "风场降水预设底图应为降水湿度");
        Assert(stack.IsOverlayActive(LayerRegistry.LayerWindArrows), "风场降水预设应包含风向洋流");
        Assert(stack.IsOverlayActive(LayerRegistry.LayerCoastlines), "风场降水预设应包含海岸轮廓");

        // 验证 CellFields Wind 属性
        var testFields = CellFields.Create(16);
        Assert(testFields.WindX.Length == 16, "CellFields.WindX 长度不匹配");
        Assert(testFields.WindY.Length == 16, "CellFields.WindY 长度不匹配");
        testFields.WindX[0] = 5.5f;
        testFields.WindY[0] = -3.2f;
        var cloneFields = testFields.Clone();
        Assert(cloneFields.WindX[0] == 5.5f, "CellFields.WindX 克隆不正确");
        Assert(cloneFields.WindY[0] == -3.2f, "CellFields.WindY 克隆不正确");

        var okPhysical = LayerPresetCatalog.ApplyPreset(LayerPresetCatalog.PresetPhysical, stack);
        Assert(okPhysical, "自然地理预设应用失败");
        Assert(stack.IsOverlayActive(LayerRegistry.LayerRivers), "自然地理（地形总览）预设应默认开启河流");

        var okGuohua = LayerPresetCatalog.ApplyPreset(LayerPresetCatalog.PresetGuohua, stack);
        Assert(okGuohua, "国风手绘预设应用失败");
        Assert(stack.ActiveBaseThemeId == LayerRegistry.LayerGuohuaHanddrawn, "国风手绘预设底图应为国风手绘舆图");
        Assert(stack.IsOverlayActive(LayerRegistry.LayerRivers), "国风手绘预设应包含江河水系");
        Assert(stack.IsOverlayActive(LayerRegistry.LayerCities), "国风手绘预设应包含城镇聚落");

        // 验证城市图层与 city_labels 的关闭联动，防止城市点点无法关闭
        stack.SetOverlayActive(LayerRegistry.LayerCities, false);
        Assert(!stack.IsOverlayActive(LayerRegistry.LayerCities), "城市图层关闭失败");
        Assert(!stack.IsOverlayActive(LayerRegistry.LayerCityLabels), "关闭城市时必须同步清理 city_labels，防止孤立残留");

        stack.SetOverlayActive(LayerRegistry.LayerCities, true);
        Assert(stack.IsOverlayActive(LayerRegistry.LayerCities), "城市图层开启失败");

        // 未知图层 ID 容错
        var prevBaseTheme = stack.ActiveBaseThemeId;
        stack.SetBaseTheme("non_existent_theme_id");
        Assert(stack.ActiveBaseThemeId == prevBaseTheme, "未知底图 ID 应被安全忽略并保持原状");

        stack.SetOverlayActive("non_existent_overlay_id", true);
        Assert(!stack.IsOverlayActive("non_existent_overlay_id"), "未知叠加层 ID 应被安全忽略");
    }
}
