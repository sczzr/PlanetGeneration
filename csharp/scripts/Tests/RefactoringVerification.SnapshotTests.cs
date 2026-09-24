using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using PlanetGeneration.Application;
using PlanetGeneration.Core.Application;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Generation;
using PlanetGeneration.Core.Persistence;
using PlanetGeneration.Rendering;
using File = System.IO.File;

namespace PlanetGeneration.Tests;

public partial class RefactoringVerification
{
    private static async Task<WorldSnapshot> VerifySingleSourceGenerationAsync()
    {
        var options = new GenerationOptions
        {
            Seed = 132, TargetCellCount = 128, Extent = new WorldExtent(256, 128),
            PlateCount = 8, WindCellCount = 8, MoistureIterations = 1, ErosionIterations = 0
        };
        var generator = new CountingFieldGenerator();
        var controller = new SnapshotGenerationController(generator);
        var pair = await controller.GenerateAsync(options, options with { Seed = 133, Tuning = WorldTuningSnapshot.Legacy });
        Check(generator.Seeds.SequenceEqual(new[] { 132, 133 }), "每组只生成一次连续场，且 A/B 各自使用正确种子");
        Check(pair.Comparison != null && pair.Primary.Snapshot != null && pair.Comparison.Snapshot != null, "两组都有权威快照");
        Check(!pair.Primary.Snapshot.Geometry.SiteX.SequenceEqual(pair.Comparison.Snapshot.Geometry.SiteX), "两组几何应来自各自种子");
        VerifyProjection(pair.Primary);
        VerifyProjection(pair.Comparison);
        VerifyMemoryCacheIsolation(pair);
        VerifyDiskCacheAndLegacyImport(pair.Primary);

        var singleGenerator = new CountingFieldGenerator();
        var single = await new SnapshotGenerationController(singleGenerator).GenerateAsync(options, null);
        Check(single.Comparison == null && singleGenerator.Seeds.Count == 1, "单组生成只调用一次基础场");
        Check(SnapshotBufferSize.Measure(single.Primary.Snapshot!) > single.Primary.Snapshot!.CellCount * sizeof(float), "缓存缓冲统计必须包含字段与几何");
        var calls = generator.Seeds.Count;
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var rejected = false;
        try { await controller.GenerateAsync(options, null, cancellationToken: canceled.Token); }
        catch (OperationCanceledException) { rejected = true; }
        Check(rejected && generator.Seeds.Count == calls, "取消请求不能额外生成世界");

        // 后半组失败不会返回一个可发布的半成品 pair。
        var failOnB = new CountingFieldGenerator { FailOnSeed = 133 };
        rejected = false;
        try { await new SnapshotGenerationController(failOnB).GenerateAsync(options, options with { Seed = 133 }); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected && failOnB.Seeds.SequenceEqual(new[] { 132, 133 }), "B 组失败必须向上传播");
        return pair.Primary.Snapshot;
    }

    private static void VerifyProjection(GeneratedWorldData world)
    {
        var snapshot = world.Snapshot!;
        var map = world.PolygonCellMap!;
        Check(ReferenceEquals(world.PolygonGrid!.Geometry, snapshot.Geometry), "投影复用权威几何");
        Check(!ReferenceEquals(world.PolygonGrid.Fields.Height, snapshot.Fields.Height), "兼容字段不得写穿快照");
        Check(world.CityCell.SequenceEqual(snapshot.Settlements.Select(s => s.CellId)), "城市归属来自同一快照");
        Check(world.Stats.OceanPercent == snapshot.Stats.OceanPercent && world.Stats.CityCount == snapshot.Stats.CityCount, "旧统计必须与快照一致");
        Check(world.CivilizationSimulation!.RecentEvents.Select(e => e.Summary).SequenceEqual(snapshot.Civilization!.RecentEvents.Select(e => e.Summary)), "旧叙事事件来自 Core");
        for (var y = 0; y < map.Height; y += 7)
            for (var x = 0; x < map.Width; x += 11)
            {
                var cell = map.CellAt(x, y);
                Check(world.Elevation[x, y] == snapshot.Fields.Height[cell] && world.River[x, y] == snapshot.Fields.River[cell], "地形、水系投影一致");
                Check((byte)world.Biome[x, y] == snapshot.Fields.Biome[cell] && (byte)world.Ore[x, y] == snapshot.Fields.Ore[cell], "分类投影一致");
                Check(world.CivilizationSimulation.PolityId[x, y] == snapshot.Fields.PolityId[cell], "政体投影一致");
                Check(world.EcologySimulation!.EcologyHealth[x, y] == snapshot.Fields.EcologyHealth[cell], "生态投影一致");
            }
        using var archiveBytes = new MemoryStream();
        WorldSnapshotArchive.Write(archiveBytes, snapshot);
        archiveBytes.Position = 0;
        var restored = WorldSnapshotArchive.Read(archiveBytes).Primary;
        var restoredProjection = SnapshotWorldProjection.Create(restored);
        using var before = new LayerRenderCoordinator().RenderBaseThemeImage(snapshot, 256, 128);
        using var after = new LayerRenderCoordinator().RenderBaseThemeImage(restored, 256, 128);
        Check(before.GetData().SequenceEqual(after.GetData()), "磁盘往返后底图像素一致");
        Check(restoredProjection.Elevation.Cast<float>().SequenceEqual(world.Elevation.Cast<float>()), "恢复投影逐像素一致");
    }

    private static void VerifyMemoryCacheIsolation(GeneratedWorldPair pair)
    {
        var main = new Main();
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var cacheKey = WorldGenerationCacheKey.BuildSession(pair.Primary.Snapshot!.Options, pair.Comparison!.Snapshot!.Options);
            var original = pair.Primary.Snapshot;
            var heights = (float[])original.Fields.Height.Clone();
            typeof(Main).GetMethod("StoreWorldGenerationCache", flags)!.Invoke(main, new object?[] { cacheKey, pair.Primary, pair.Comparison, false });
            SnapshotWorldProjection.EnsureSimulation(pair.Primary, 200, 65, 40, 55);
            Check(pair.Primary.Snapshot!.Options.Epoch == 200, "时间轴生成新快照");
            Check(original.Options.Epoch == 100 && original.Fields.Height.SequenceEqual(heights), "时间轴不修改缓存原始世界");
            Check(ReferenceEquals(original.Geometry, pair.Primary.Snapshot.Geometry), "时间轴不重建几何");
            VerifyProjection(pair.Primary);
            var args = new object?[] { cacheKey, null, null };
            var hit = (bool)typeof(Main).GetMethod("TryGetWorldGenerationCache", flags)!.Invoke(main, args)!;
            Check(hit, "应命中内存缓存");
            var cached = (GeneratedWorldData)args[1]!;
            Check(cached.Snapshot!.Options.Epoch == 100 && !ReferenceEquals(cached, pair.Primary), "缓存命中返回独立会话，不继承活动纪元");
            Check(((GeneratedWorldData)args[2]!).Snapshot!.Options.Seed == 133, "缓存 B 组种子完整保留");
            typeof(Main).GetField("_primaryWorld", flags)!.SetValue(main, cached);
            Check(ReferenceEquals(typeof(Main).GetProperty("_primarySnapshot", flags)!.GetValue(main), cached.Snapshot), "Main 快照引用从活动世界派生");
            typeof(Main).GetMethod("ApplySnapshotOptions", flags)!.Invoke(main, new object[] { cached });
            var captured = (GenerationOptions)typeof(Main).GetMethod("BuildGenerationOptions", flags)!.Invoke(main, new object[] { cached.Tuning, cached.Snapshot.Options.Seed })!;
            Check(captured.BuildCacheKey() == cached.Snapshot.Options.BuildCacheKey(), "载入后的 UI 输入与快照一致");
        }
        finally { main.Free(); }
    }

    private static void VerifyDiskCacheAndLegacyImport(GeneratedWorldData world)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var main = new Main();
        var legacyPath = Path.GetTempFileName();
        string? cachePath = null;
        var ownsCachePath = false;
        try
        {
            // 使用随机测试身份，绝不覆盖或清理已有的用户缓存。
            var source = world.Snapshot!;
            var options = source.Options with { Seed = Guid.NewGuid().GetHashCode() };
            var isolated = source.WithSimulation(options, source.Fields.Clone(), source.Ecology, source.Civilization);
            var key = WorldGenerationCacheKey.BuildSession(options);
            cachePath = (string)typeof(Main).GetMethod("BuildCacheFilePath", flags)!.Invoke(main, new object[] { key })!;
            Check(!File.Exists(cachePath), "测试缓存路径不能与用户数据冲突");
            ownsCachePath = true;
            // 使用同一持久化服务，但不调用自动修剪，以免测试影响用户已有缓存。
            WorldSnapshotArchive.Save(cachePath, isolated);
            var arguments = new object?[] { key, null };
            Check((bool)typeof(Main).GetMethod("TryLoadWorldGenerationCacheFromDisk", flags)!.Invoke(main, arguments)!, "Main 磁盘缓存命中完整快照");
            var restored = (SnapshotArchiveContent)arguments[1]!;
            Check(restored.Primary.Fields.Height.SequenceEqual(source.Fields.Height), "磁盘缓存恢复领域数据，不重跑生成");
            var headerArgs = new object?[] { cachePath, 0, 0, 0, false };
            Check((bool)typeof(Main).GetMethod("TryReadPersistedCacheHeaderFromFile", flags)!.Invoke(main, headerArgs)!, "新缓存的 UI 摘要仍可读取");
            Check((int)headerArgs[1]! == options.Seed, "新缓存摘要种子正确");

            // 原有字典序列化/导入入口仍保留；旧手动档案不伪装成新快照。
            main.Seed = source.Options.Seed;
            main.MapWidth = world.Stats.Width;
            main.MapHeight = world.Stats.Height;
            var payload = typeof(Main).GetMethod("BuildPersistedWorldCacheEntry", flags)!.Invoke(main,
                new object?[] { $"ver:5|{main.MapWidth}x{main.MapHeight}|seed:{main.Seed}", world, null })!;
            var dictionary = (Godot.Collections.Dictionary)typeof(Main).GetMethod("ConvertPersistedCacheToGodotDictionary", flags)!.Invoke(main, new[] { payload })!;
            File.WriteAllText(legacyPath, Json.Stringify(dictionary));
            Check(!WorldSnapshotArchive.HasSnapshotHeader(legacyPath), "旧档案选择旧导入器");
            var readArgs = new object?[] { legacyPath, null };
            Check((bool)typeof(Main).GetMethod("TryReadPersistedCacheEntryFromFile", flags)!.Invoke(main, readArgs)!, "旧 JSON 档案继续可读取");
            var importedPayload = readArgs[1]!;
            var importedData = importedPayload.GetType().GetProperty("Primary")!.GetValue(importedPayload)!;
            var legacy = (GeneratedWorldData)typeof(Main).GetMethod("RestoreGeneratedWorldData", flags)!.Invoke(main, new[] { importedData })!;
            Check(legacy.Snapshot == null, "旧档案缺失快照，不能伪造事实源");
            Check(legacy.Elevation.Cast<float>().SequenceEqual(world.Elevation.Cast<float>()), "旧档案高程数据不丢失");
            typeof(Main).GetField("_primaryWorld", flags)!.SetValue(main, legacy);
            Check(typeof(Main).GetProperty("_primarySnapshot", flags)!.GetValue(main) == null, "切回旧档案不会残留新快照");
        }
        finally
        {
            main.Free();
            File.Delete(legacyPath);
            if (ownsCachePath && cachePath != null && File.Exists(cachePath)) File.Delete(cachePath);
        }
    }

    private sealed class CountingFieldGenerator : IBaseFieldGenerator
    {
        private readonly BaseFieldGeneratorAdapter _inner = new();
        public List<int> Seeds { get; } = new();
        public int? FailOnSeed { get; init; }
        public BaseContinuousFields GenerateFields(GenerationOptions options, int width, int height)
        {
            Seeds.Add(options.Seed);
            if (options.Seed == FailOnSeed) throw new InvalidOperationException("Injected generation failure");
            return _inner.GenerateFields(options, width, height);
        }
    }
}
