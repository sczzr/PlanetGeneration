using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PlanetGeneration.Core.Application;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Persistence;
using PlanetGeneration.Application;

namespace PlanetGeneration.Core.SelfTest;

internal static partial class Program
{
    private static void TestSnapshotArchiveRoundTrip()
    {
        var service = new WorldGenerationService(new DeterministicFieldFixture());
        var options = new GenerationOptions { Seed = 34, TargetCellCount = 128, Extent = new WorldExtent(256, 128),
            EnableCartographyDesigner = true, BlueprintName = "FantasyContinent01" };
        var a = service.GenerateAsync(options).GetAwaiter().GetResult();
        var b = service.GenerateAsync(options with { Seed = 35, Tuning = WorldTuningSnapshot.Legacy }).GetAwaiter().GetResult();
        a.ContinuousWind![3, 4] = (1.25f, -0.5f); // 明确设置测试夹具，防止全零风场掩盖轴序错误。
        using var stream = new MemoryStream();
        WorldSnapshotArchive.Write(stream, a, b);
        stream.Position = 0;
        var result = WorldSnapshotArchive.Read(stream);
        Assert(result.Comparison != null, "A/B 存档必须恢复两组");
        AssertSnapshotEquivalent(a, result.Primary);
        AssertSnapshotEquivalent(b, result.Comparison);
        Assert(result.CacheKey == WorldGenerationCacheKey.BuildSession(options, b.Options), "会话缓存键必须保留 B 组输入");
        Assert(result.CacheKey != WorldGenerationCacheKey.BuildSession(options, b.Options with { WindCellCount = 8 }), "B 组参数变化必须使缓存失效");
        var text = Encoding.UTF8.GetString(stream.ToArray());
        Assert(WorldArchiveHeader.TryParse(text[..Math.Min(4096, text.Length)], out var header), "新快照头部仍可快速预览");
        Assert(header == new WorldArchiveHeader(34, 256, 128, true), "快照头部内容正确");
    }

    private static void AssertSnapshotEquivalent(WorldSnapshot expected, WorldSnapshot actual)
    {
        Assert(expected.SnapshotId != actual.SnapshotId, "载入后必须获得新运行时 ID，避免旧渲染缓存误命中");
        Assert(expected.CreatedAt == actual.CreatedAt && expected.Options == actual.Options, "创建时间和参数应保留");
        AssertArraysEqual(actual.Geometry, expected.Geometry, "存档几何");
        AssertArraysEqual(actual.Fields, expected.Fields, "存档字段");
        var jsonOptions = new JsonSerializerOptions { IncludeFields = true };
        string Entities(WorldSnapshot snapshot) => JsonSerializer.Serialize(new
        {
            snapshot.Settlements, snapshot.PlateSummary, snapshot.Stats, snapshot.Ecology, snapshot.Civilization, snapshot.MegaRegions,
            snapshot.Cartography?.Brushes, snapshot.Cartography?.Regions, snapshot.Cartography?.Landmarks,
            snapshot.Cartography?.Commands, snapshot.Cartography?.RoadGraph
        }, jsonOptions);
        Assert(Entities(expected) == Entities(actual), "实体、模拟与制图指令应完整保留");
        Assert(expected.ContinuousWind!.Cast<(float X, float Y)>().SequenceEqual(actual.ContinuousWind!.Cast<(float X, float Y)>()), "连续风场应逐元素相同");
        for (var x = -10; x < 300; x += 7)
            Assert(expected.Geometry.FindCell(x, 43.2) == actual.Geometry.FindCell(x, 43.2), "恢复的空间索引必须拾取一致");
        Assert(!ReferenceEquals(expected.Fields.Height, actual.Fields.Height), "恢复后不能共享可变数组");
    }

    private static void TestSnapshotArchiveValidationAndAtomicSave()
    {
        var source = new WorldGenerationService(new DeterministicFieldFixture()).GenerateAsync(
            new GenerationOptions { Seed = 35, TargetCellCount = 64, Extent = new WorldExtent(64, 32) }).GetAwaiter().GetResult();
        using var data = new MemoryStream();
        WorldSnapshotArchive.Write(data, source);
        var json = Encoding.UTF8.GetString(data.ToArray());
        foreach (Action<JsonNode> corrupt in new Action<JsonNode>[]
        {
            root => root["snapshot_format"] = 999,
            root => root["cache_key"] = "invalid",
            root => root["primary"]!["Fields"]!["Height"]!.AsArray().RemoveAt(0),
            root => root["primary"]!["Geometry"]!["CellNeighbors"]![0] = source.CellCount + 1,
            root => root["primary"]!["Geometry"]!["CellVertexStart"]![0] = 2,
            root => root["primary"]!["Geometry"]!["Rows"] = 0,
            root => root["primary"]!["Geometry"]!["SpacingX"] = 0.00000001,
            root => root["primary"]!["Fields"]!["Biome"] = "AA==",
            root => root["primary"]!["PlateSummary"] = null
        })
        {
            var root = JsonNode.Parse(json)!;
            corrupt(root);
            using var corrupted = new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString()));
            var rejected = false;
            try { WorldSnapshotArchive.Read(corrupted); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "损坏的快照必须被拒绝");
        }
        var folder = Path.Combine(Path.GetTempPath(), "PlanetSnapshotTest-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "world.pgarchive.json");
        try
        {
            WorldSnapshotArchive.Save(path, source);
            Assert(WorldSnapshotArchive.HasSnapshotHeader(path), "必须识别新快照档案");
            var original = File.ReadAllBytes(path);
            var fields = source.Fields.Clone();
            fields.Height[0] = float.NaN;
            var invalid = source.WithSimulation(source.Options, fields, source.Ecology, source.Civilization);
            var rejected = false;
            try { WorldSnapshotArchive.Save(path, invalid); }
            catch (ArgumentException) { rejected = true; }
            Assert(rejected, "非有限数据应导致保存失败");
            Assert(original.SequenceEqual(File.ReadAllBytes(path)), "失败的保存不得覆盖旧档案");
            Assert(!Directory.EnumerateFiles(folder, "*.tmp").Any(), "临时文件必须清理");
            AssertSnapshotEquivalent(source, WorldSnapshotArchive.Load(path).Primary);
            File.WriteAllText(path, "{\"cache_key\":\"ver:5|64x32|seed:35\",\"primary\":{}}");
            Assert(!WorldSnapshotArchive.HasSnapshotHeader(path), "旧档案应转交兼容导入器");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(folder)) Directory.Delete(folder);
        }
    }
}
