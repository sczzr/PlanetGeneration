using Godot;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Collections.Generic;
using System.Text.Json;
using System.Security.Cryptography;
using PlanetGeneration.Application;
using PlanetGeneration.Core.Application;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;
using PlanetGeneration.Rendering;
using PlanetGeneration.WorldGen;
using Legacy = PlanetGeneration.WorldGen.Polygon;
using File = System.IO.File;

namespace PlanetGeneration.Tests;

/// <summary>无需窗口或 LLM 模型的 Godot 应用边界回归；失败时进程返回非零退出码。</summary>
public partial class RefactoringVerification : Node
{
    public override async void _Ready()
    {
        try
        {
            VerifyCatalogLoading();
            VerifyGenerationOptions();
            VerifyInferenceLifecycle();
            VerifyCellMapCache();
            VerifyFallbackTextures();
            var snapshot = await VerifySingleSourceGenerationAsync();
            VerifySimulationInvalidation(snapshot);
            using var image = new LayerRenderCoordinator().RenderBaseThemeImage(snapshot, 256, 128);
            Check(image.GetWidth() == 256 && image.GetHeight() == 128, "真实生成与底图渲染尺寸");
            Check(image.GetData().Length == 256 * 128 * 4, "底图像素输出完整");
            GD.Print("[RefactorTest] PASS: 资源加载、参数映射、LLM 请求状态、A/B 归属缓存、模拟缓存失效、备用纹理逐像素基线、单次生成、快照投影/存档/时间轴、底图渲染");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[RefactorTest] FAIL: {ex}");
            GetTree().Quit(1);
        }
    }

    private static void VerifyCatalogLoading()
    {
        TerrainCatalog.EnsureInitialized();
        TreeCatalog.EnsureInitialized();
        ForestClusterCatalog.EnsureInitialized();
        Check(TerrainCatalog.AllSprites.Count > 0, "地貌目录不能为空");
        Check(TreeCatalog.AllTrees.Count > 0, "树木目录不能为空");
        Check(ForestClusterCatalog.AllClusters.Count > 0, "森林目录或回退目录不能为空");
        var empty = Path.GetTempFileName();
        var fallback = Path.GetTempFileName();
        try
        {
            File.WriteAllText(fallback, "fallback-value");
            Check(AssetTextLoader.TryRead(empty, out var content, fallback) && content == "fallback-value",
                "空资源必须继续尝试备用路径");
            Check(!AssetTextLoader.TryRead(empty, out _), "全部为空时必须报告失败");
        }
        finally
        {
            File.Delete(empty);
            File.Delete(fallback);
        }
    }

    private static void VerifyGenerationOptions()
    {
        // 测试真实 UI → Core 边界，防止参数存在于缓存键却未从 UI 传入。
        var main = new Main();
        try
        {
            var method = typeof(Main).GetMethod("BuildGenerationOptions", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var balanced = (GenerationOptions)method.Invoke(main, new object[] { WorldTuning.Balanced(), 123 })!;
            var legacy = (GenerationOptions)method.Invoke(main, new object[] { WorldTuning.Legacy(), 123 })!;
            Check(balanced.Tuning == WorldTuningSnapshot.Balanced, "Balanced 参数映射");
            Check(legacy.Tuning == WorldTuningSnapshot.Legacy, "Legacy 参数映射");
            Check(balanced.BuildCacheKey() != legacy.BuildCacheKey(), "不同预设不能共享缓存");
        }
        finally { main.Free(); }
    }

    private static void VerifyInferenceLifecycle()
    {
        // 不加载模型，只验证替换/取消/结束请求的状态归属。
        var oracle = new CivilizationOracle();
        var changes = new List<bool>();
        oracle.InferenceStateChanged += changes.Add;
        var begin = typeof(CivilizationOracle).GetMethod("BeginInferenceOperation", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var complete = typeof(CivilizationOracle).GetMethod("CompleteInferenceOperation", BindingFlags.NonPublic | BindingFlags.Instance)!;
        try
        {
            var old = (CancellationTokenSource)begin.Invoke(oracle, null)!;
            var oldToken = old.Token;
            var current = (CancellationTokenSource)begin.Invoke(oracle, null)!;
            Check(oldToken.IsCancellationRequested, "新请求应取消旧请求");
            Check(!current.Token.IsCancellationRequested, "新请求不应继承旧取消状态");
            complete.Invoke(oracle, new object[] { old });
            Check(oracle.IsInferring && changes.All(v => v), "旧 finally 不能清理新请求状态");
            oracle.CancelCurrentInference();
            Check(current.Token.IsCancellationRequested, "取消后令牌仍可访问，直到所属请求 finally");
            complete.Invoke(oracle, new object[] { current });
            Check(!oracle.IsInferring && changes.Last() == false, "当前请求结束时恢复空闲");
            Check(changes.Count(v => !v) == 1, "只应收到当前请求的一次结束通知");
        }
        finally { oracle.Dispose(); }
    }

    private static void VerifyCellMapCache()
    {
        var a = PolygonGridBuilder.Create(new WorldExtent(256, 128), 1, 128);
        var b = PolygonGridBuilder.Create(new WorldExtent(256, 128), 2, 128);
        Check(a.Count == b.Count, "测试要求相同地块数、不同几何");
        var coordinator = new LayerRenderCoordinator();
        var mapA = coordinator.GetOrCreateCellMap(a, 256, 128);
        Check(ReferenceEquals(mapA, coordinator.GetOrCreateCellMap(a, 256, 128)), "同几何应命中缓存");
        var mapB = coordinator.GetOrCreateCellMap(b, 256, 128);
        Check(!ReferenceEquals(mapA, mapB), "A/B 世界不能因地块数相同而复用归属图");
        Check(mapB.Cells.SequenceEqual(PolygonRasterizer.BuildCellMap(b, 256, 128).Cells), "B 组归属图必须匹配 B 组几何");
        Check(!ReferenceEquals(mapB, coordinator.GetOrCreateCellMap(b, 128, 64)), "分辨率变化必须重建归属图");
    }

    private static void VerifyFallbackTextures()
    {
        var path = ProjectSettings.GlobalizePath("res://tests/baselines/guohua-fallback-textures.json");
        var expected = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
        var verified = 0;
        foreach (var method in typeof(GuohuaFallbackTextures).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(m => m.Name.StartsWith("CreateDefault", StringComparison.Ordinal)).OrderBy(m => m.Name))
        {
            using var texture = (Texture2D)method.Invoke(null, null)!;
            using var image = texture.GetImage();
            Check(image != null && !image.IsEmpty(), $"备用纹理输出: {method.Name}");
            var fingerprint = Convert.ToHexString(SHA256.HashData(image.GetData()));
            Check(expected.TryGetValue(method.Name, out var baseline) && fingerprint == baseline,
                $"备用纹理像素发生变化: {method.Name} {fingerprint}");
            verified++;
        }
        Check(verified == expected.Count, "不能遗漏备用纹理工厂");
        GD.Print($"[FallbackBaseline] {verified} 个备用图元像素指纹保持一致");
    }

    private static void VerifySimulationInvalidation(WorldSnapshot snapshot)
    {
        foreach (var name in new[] { "primary", "compare" })
        {
            var grid = new Legacy.PolygonGrid(snapshot.Geometry, snapshot.Fields);
            var world = new GeneratedWorldData
            {
                PolygonGrid = grid, PolygonEcology = snapshot.Ecology, PolygonCivilization = snapshot.Civilization,
                EcologySignature = 3, CivilizationSignature = 4,
                PolygonEcologySignature = 5, PolygonCivilizationSignature = 6
            };
            foreach (var layer in new[] { MapLayer.Ecology, MapLayer.Civilization, MapLayer.TradeRoutes, MapLayer.Elevation })
                world.LayerRenderCache[layer] = new LayerRenderCacheEntry();
            world.InvalidateSimulationCaches();
            Check(world.PolygonEcology == null && world.PolygonCivilization == null, $"{name}: 清理模拟结果");
            Check(world.EcologySignature == int.MinValue && world.CivilizationSignature == int.MinValue &&
                world.PolygonEcologySignature == int.MinValue && world.PolygonCivilizationSignature == int.MinValue,
                $"{name}: 所有签名失效");
            Check(world.LayerRenderCache.Count == 1 && world.LayerRenderCache.ContainsKey(MapLayer.Elevation),
                $"{name}: 仅清理依赖模拟的图层");
            Check(ReferenceEquals(world.PolygonGrid, grid), $"{name}: 保留地理数据");
        }
    }

    private static void Check([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
