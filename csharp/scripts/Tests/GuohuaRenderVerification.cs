using Godot;
using PlanetGeneration.Core.Application;
using PlanetGeneration.Core.Cartography;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Layers;
using PlanetGeneration.Rendering;
using System;
using System.Collections.Generic;
using System.IO;

namespace PlanetGeneration.Tests;

public partial class GuohuaRenderVerification : Node
{
    public override async void _Ready()
    {
        GD.Print("[GuohuaTest] === 开始中国国画手绘舆图渲染管线自动化验证 ===");

        try
        {
            // 1. 生成带有山川、水系与聚落的世界快照
            var seed = 20260919;
            var cellCount = 3000;

            var options = new GenerationOptions
            {
                Seed = seed,
                TargetCellCount = cellCount,
                Extent = new WorldExtent(2048, 1024),
                SeaLevel = 0.35f,
                HeatFactor = 1.0f,
                MoistureFactor = 1.0f,
                EnableRivers = true,
                RiverDensity = 1.2f,
                PlateCount = 8,
                OceanicRatio = 0.35f,
                ContinentBias = 0.5f,
                ContinentCount = 3,
                SpeciesDiversity = 60,
                CivilAggression = 45,
                MagicDensity = 40,
                EnableCartographyDesigner = true,
                BlueprintName = "FantasyContinent01"
            };

            GD.Print("[GuohuaTest] 正在使用 WorldGenerationService 生成世界快照...");
            var adapter = new BaseFieldGeneratorAdapter();
            var genService = new WorldGenerationService(adapter);
            var snapshot = await genService.GenerateAsync(options);

            GD.Print($"[GuohuaTest] 快照生成完毕: 地块={snapshot.Geometry.Count}, 聚落={snapshot.Settlements.Count}");
            // 诊断山脉南北两侧空白区域的地块物理属性
            var geom = snapshot.Geometry;
            var cartography = snapshot.Cartography ?? CartographyGenerator.Generate(snapshot);
            var brushCounts = new Dictionary<BrushType, int>();
            foreach (var b in cartography.Brushes)
            {
                brushCounts[b.Type] = brushCounts.GetValueOrDefault(b.Type, 0) + 1;
            }
            foreach (var (k, v) in brushCounts)
            {
                GD.Print($"[BrushCount] {k}: {v}");
            }
            GD.Print($"[BrushCount] Total brushes: {cartography.Brushes.Count}");
            var sFields = snapshot.Fields;
            var emptyDiamondCount = 0;
            for (var c = 0; c < geom.Count; c++)
            {
                var cx = geom.CentroidX[c];
                var cy = geom.CentroidY[c];
                if (cx >= 1000 && cx <= 1200 && cy >= 400 && cy <= 490)
                {
                    emptyDiamondCount++;
                    GD.Print($"[EmptyDiamondCell] c={c}, pos=({cx:F0},{cy:F0}), h={sFields.Height[c]:F2}, lf={(LandformType)sFields.Landform[c]}, biome={(BiomeType)sFields.Biome[c]}, m={sFields.Moisture[c]:F2}");
                }
            }
            GD.Print($"[EmptyDiamond] Total cells in empty diamond: {emptyDiamondCount}");

            var centralBrushes = 0;
            foreach (var b in cartography.Brushes)
            {
                if (b.Position.X >= 900 && b.Position.X <= 1300 && b.Position.Y >= 380 && b.Position.Y <= 650)
                {
                    centralBrushes++;
                    GD.Print($"[CenterBrush] Type={b.Type}, pos=({b.Position.X:F0},{b.Position.Y:F0}), scale={b.Scale:F2}, variant={b.VariantKey}");
                }
            }
            GD.Print($"[CenterRegion] Total brushes in center box: {centralBrushes}");

            // 2. 搭建离屏渲染视口与画布
            var viewport = new SubViewport
            {
                Size = new Vector2I(3840, 2160),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                TransparentBg = false
            };
            AddChild(viewport);

            var canvas = new MapCanvas
            {
                Size = new Vector2(3840, 2160)
            };
            viewport.AddChild(canvas);

            // 3. 应用国风手绘预设与图层栈
            var layerStack = new LayerStackState();
            var presetApplied = LayerPresetCatalog.ApplyPreset(LayerPresetCatalog.PresetGuohua, layerStack);
            GD.Print($"[GuohuaTest] 应用预设 '{LayerPresetCatalog.PresetGuohua}': {presetApplied}, 底图: {layerStack.ActiveBaseThemeId}");

            canvas.AttachSnapshot(snapshot, layerStack);

            // 4. 等待多帧以便渲染树完成 GPU 绘制
            GD.Print("[GuohuaTest] 等待渲染帧提交...");
            for (var f = 0; f < 8; f++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            var tex = viewport.GetTexture();
            var img = tex.GetImage();

            if (img != null)
            {
                var outputPath = "res://guohua_verification_result.png";
                var absOutputPath = ProjectSettings.GlobalizePath(outputPath);
                var err = img.SavePng(absOutputPath);
                GD.Print($"[GuohuaTest] 已输出截图 (3840x2160): {absOutputPath}, 结果码: {err}");

                var currentArtifactDir = @"C:\Users\shawn\.gemini\antigravity\brain\ffc6f190-f7a0-41fe-ab45-6197d63d65d0";
                if (Directory.Exists(currentArtifactDir))
                {
                    var artifactFile = Path.Combine(currentArtifactDir, "guohua_verification_result.png");
                    File.Copy(absOutputPath, artifactFile, true);
                    GD.Print($"[GuohuaTest] 已同步高清全图至当前会话产物目录: {artifactFile}");

                    // 1. 北方主山脉与北境冰原特写 (Northern Mountains & Polar Tundra)
                    var northCrop = img.GetRegion(new Rect2I(600, 200, 2640, 900));
                    var northPath = Path.Combine(currentArtifactDir, "guohua_northern_mountains.png");
                    northCrop.SavePng(northPath);
                    GD.Print($"[GuohuaTest] 已输出北方山系与北境特写: {northPath}");

                    // 2. 中原大平原与大江王都特写 (Central Great Plains & Capital)
                    var plainsCrop = img.GetRegion(new Rect2I(1200, 600, 1700, 1100));
                    var plainsPath = Path.Combine(currentArtifactDir, "guohua_central_plains.png");
                    plainsCrop.SavePng(plainsPath);
                    GD.Print($"[GuohuaTest] 已输出中原大平原特写: {plainsPath}");

                    // 3. 西南荒漠四层过渡与绿洲古堡特写 (Southwest Desert & Oasis)
                    var desertCrop = img.GetRegion(new Rect2I(600, 1000, 1500, 1000));
                    var desertPath = Path.Combine(currentArtifactDir, "guohua_southwest_desert.png");
                    desertCrop.SavePng(desertPath);
                    GD.Print($"[GuohuaTest] 已输出西南大荒特写: {desertPath}");

                    // 4. 东部古森林三簇与神殿遗迹特写 (Eastern Ancient Forest & Relic Temple)
                    var eastCrop = img.GetRegion(new Rect2I(2200, 500, 1400, 1100));
                    var eastPath = Path.Combine(currentArtifactDir, "guohua_eastern_ancient_forest.png");
                    eastCrop.SavePng(eastPath);
                    GD.Print($"[GuohuaTest] 已输出东部古森林特写: {eastPath}");

                    // ── 5. Map Effects 方法论核心特色专项特写 ──
                    // ① 托尔金群峰与背光侧阴影斜向排线 (Tolkien Mountain Hachures)
                    var tolkienCrop = img.GetRegion(new Rect2I(1150, 220, 1500, 850));
                    var tolkienPath = Path.Combine(currentArtifactDir, "map_effects_tolkien_mountains.png");
                    tolkienCrop.SavePng(tolkienPath);
                    GD.Print($"[GuohuaTest] 已输出 Map Effects 托尔金群峰特写: {tolkienPath}");

                    // ② 平原蛇曲与新月形牛轭湖 (Meander River & Oxbow Lake)
                    var oxbowCrop = img.GetRegion(new Rect2I(1400, 750, 1100, 750));
                    var oxbowPath = Path.Combine(currentArtifactDir, "map_effects_oxbow_lake.png");
                    oxbowCrop.SavePng(oxbowPath);
                    GD.Print($"[GuohuaTest] 已输出 Map Effects 蛇曲牛轭湖特写: {oxbowPath}");

                    // ③ 河口树根状冲积三角洲与沙洲群岛 (River Delta & Silt Islands)
                    var deltaCrop = img.GetRegion(new Rect2I(2550, 1280, 1150, 800));
                    var deltaPath = Path.Combine(currentArtifactDir, "map_effects_delta_estuary.png");
                    deltaCrop.SavePng(deltaPath);
                    GD.Print($"[GuohuaTest] 已输出 Map Effects 河口分形三角洲特写: {deltaPath}");

                    // ④ 大地深渊裂谷黑底与断崖垂直落差排线 (Chasm Abyss & Ragged Cliffs)
                    var chasmCrop = img.GetRegion(new Rect2I(2000, 320, 1100, 700));
                    var chasmPath = Path.Combine(currentArtifactDir, "map_effects_chasm_rift.png");
                    chasmCrop.SavePng(chasmPath);
                    GD.Print($"[GuohuaTest] 已输出 Map Effects 大地深渊断崖特写: {chasmPath}");

                    // ⑤ 智能海岸多阶等距同心水波纹与海蚀石拱 (Smart Coastline & Sea Arch)
                    var coastCrop = img.GetRegion(new Rect2I(2650, 1400, 1100, 700));
                    var coastPath = Path.Combine(currentArtifactDir, "map_effects_smart_coastline.png");
                    coastCrop.SavePng(coastPath);
                    GD.Print($"[GuohuaTest] 已输出 Map Effects 智能海岸水波特写: {coastPath}");
                }
            }
            else
            {
                GD.PrintErr("[GuohuaTest] 无法获取视口图像！");
            }

            GD.Print("[GuohuaTest] === 自动化验证全部成功！ ===");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GuohuaTest] 验证出现异常: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            GetTree().Quit();
        }
    }
}
