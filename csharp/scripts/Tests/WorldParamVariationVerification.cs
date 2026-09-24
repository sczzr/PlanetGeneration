using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using PlanetGeneration.Core.Application;
using PlanetGeneration.Core.Cartography;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Layers;
using PlanetGeneration.Rendering;

namespace PlanetGeneration.Tests;

public partial class WorldParamVariationVerification : Node
{
    public override async void _Ready()
    {
        GD.Print("[ParamTest] === 开始世界配置参数与古风地图布局动态演化自动化测试 ===");
        try
        {
            var adapter = new BaseFieldGeneratorAdapter();
            var genService = new WorldGenerationService(adapter);

            // ── 1. 世界 A：超大陆形态 (Supercontinent)，低海平面，较少板块 ──
            var optA = new GenerationOptions
            {
                Seed = 10001,
                TargetCellCount = 2048,
                Extent = new WorldExtent(2048, 1024),
                SeaLevel = 0.30f,
                Morphology = TerrainMorphology.Supercontinent,
                PlateCount = 10,
                EnableRivers = true,
                RiverDensity = 1.0f,
                ContinentCount = 1,
                OrogenyStrength = 1.3f
            };

            GD.Print("[ParamTest] 生成世界 A (超大陆 Supercontinent, Seed=10001)...");
            var snapA = await genService.GenerateAsync(optA);
            var cartA = snapA.Cartography ?? CartographyGenerator.Generate(snapA);
            GD.Print($"[ParamTest] 世界 A 完成: 地块={snapA.Geometry.Count}, 笔刷={cartA.Brushes.Count}, 聚落={snapA.Settlements.Count}");

            // ── 2. 世界 B：多大陆形态 (Continents)，4块大陆，多板块 ──
            var optB = new GenerationOptions
            {
                Seed = 88888,
                TargetCellCount = 2048,
                Extent = new WorldExtent(2048, 1024),
                SeaLevel = 0.38f,
                OceanicRatio = 0.45f,
                Morphology = TerrainMorphology.Continents,
                PlateCount = 20,
                EnableRivers = true,
                RiverDensity = 1.2f,
                ContinentCount = 4,
                OrogenyStrength = 1.1f
            };

            GD.Print("[ParamTest] 生成世界 B (多大陆 Continents, Seed=88888)...");
            var snapB = await genService.GenerateAsync(optB);
            var cartB = snapB.Cartography ?? CartographyGenerator.Generate(snapB);
            GD.Print($"[ParamTest] 世界 B 完成: 地块={snapB.Geometry.Count}, 笔刷={cartB.Brushes.Count}, 聚落={snapB.Settlements.Count}");

            // ── 3. 校验世界 A 与世界 B 的布局差异 ──
            // 3.1 陆地占比与平均高度显著不同
            var landRatioA = 100f - snapA.Stats.OceanPercent;
            var landRatioB = 100f - snapB.Stats.OceanPercent;
            GD.Print($"[ParamTest] 陆地占比对比: 世界A={landRatioA:F1}%, 世界B={landRatioB:F1}%");
            if (Math.Abs(landRatioA - landRatioB) < 5.0f)
            {
                throw new InvalidOperationException($"超大陆与群岛的陆地占比差异过小: A={landRatioA}%, B={landRatioB}%");
            }

            // 3.2 笔刷分布质心与范围显著不同
            var mtA = cartA.Brushes.Where(b => b.Type is BrushType.MountainRidge or BrushType.MountainFar or BrushType.MountainSecondary).ToList();
            var mtB = cartB.Brushes.Where(b => b.Type is BrushType.MountainRidge or BrushType.MountainFar or BrushType.MountainSecondary).ToList();
            GD.Print($"[ParamTest] 山脉笔刷对比: 世界A={mtA.Count}个, 世界B={mtB.Count}个");
            if (mtA.Count == 0 || mtB.Count == 0)
            {
                throw new InvalidOperationException("山脉笔刷未正常生成！");
            }

            var avgMtPosA = new Vector2(
                (float)mtA.Average(b => b.Position.X),
                (float)mtA.Average(b => b.Position.Y));
            var avgMtPosB = new Vector2(
                (float)mtB.Average(b => b.Position.X),
                (float)mtB.Average(b => b.Position.Y));
            GD.Print($"[ParamTest] 山脉质心对比: 世界A=({avgMtPosA.X:F1},{avgMtPosA.Y:F1}), 世界B=({avgMtPosB.X:F1},{avgMtPosB.Y:F1})");

            var mtPosDiff = avgMtPosA.DistanceTo(avgMtPosB);
            GD.Print($"[ParamTest] 山脉质心空间距离: {mtPosDiff:F1} 像素");
            if (mtPosDiff < 20.0f && mtA.Count == mtB.Count)
            {
                throw new InvalidOperationException($"世界A与世界B的山脉布局没有发生显著变化！");
            }

            // 3.3 聚落与地标坐标对比
            if (snapA.Settlements.Count > 0 && snapB.Settlements.Count > 0)
            {
                var firstA = snapA.Settlements[0].Position;
                var firstB = snapB.Settlements[0].Position;
                var distSettlement = firstA.DistanceTo(firstB);
                GD.Print($"[ParamTest] 首要聚落坐标对比: A=({firstA.X:F1},{firstA.Y:F1}), B=({firstB.X:F1},{firstB.Y:F1}), 间距={distSettlement:F1}");
                if (distSettlement < 5.0f && snapA.Settlements[0].Name == snapB.Settlements[0].Name)
                {
                    throw new InvalidOperationException("聚落坐标与名称在不同参数下完全雷同！");
                }
            }

            // ── 4. 验证蓝图兼容模式 (Blueprint Compatibility Mode) ──
            var optBlueprint = new GenerationOptions
            {
                Seed = 12345,
                TargetCellCount = 2048,
                Extent = new WorldExtent(2048, 1024),
                SeaLevel = 0.35f,
                EnableCartographyDesigner = true,
                BlueprintName = "FantasyContinent01"
            };

            GD.Print("[ParamTest] 测试显式启用蓝图样板模式 (FantasyContinent01)...");
            var snapBlueprint = await genService.GenerateAsync(optBlueprint);
            var cartBlueprint = snapBlueprint.Cartography ?? CartographyGenerator.Generate(snapBlueprint);
            var hasCapital = cartBlueprint.Landmarks.Any(l => l.Name == "神京天都");
            GD.Print($"[ParamTest] 样板模式包含'神京天都': {hasCapital}, 笔刷数: {cartBlueprint.Brushes.Count}");
            if (!hasCapital)
            {
                throw new InvalidOperationException("显式启用样板模式时未能生成样板地标！");
            }

            // ── 5. 渲染出图验证 ──
            var currentArtifactDir = @"C:\Users\shawn\.gemini\antigravity\brain\027774a2-3a2d-4918-aa96-df2b7c94ba0d";
            var coordinator = new LayerRenderCoordinator();
            LayerPresetCatalog.ApplyPreset(LayerPresetCatalog.PresetGuohua, coordinator.StackState);

            // 渲染并保存世界 A
            var imgA = coordinator.RenderBaseThemeImage(snapA, 2048, 1024);
            var pathA = Path.Combine(currentArtifactDir, "guohua_world_a_supercontinent.png");
            imgA.SavePng(pathA);
            GD.Print($"[ParamTest] 已导出世界 A (超大陆) 渲染底图: {pathA}");

            // 渲染并保存世界 B
            var imgB = coordinator.RenderBaseThemeImage(snapB, 2048, 1024);
            var pathB = Path.Combine(currentArtifactDir, "guohua_world_b_continents.png");
            imgB.SavePng(pathB);
            GD.Print($"[ParamTest] 已导出世界 B (多大陆) 渲染底图: {pathB}");

            GD.Print("[ParamTest] === 全部验证项均通过！参数修改已成功动态改变古风地图布局！ ===");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[ParamTest] 验证失败: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            GetTree().Quit();
        }
    }
}
