using System.Text.Json;
using System.Security.Cryptography;
using PlanetGeneration.Core.Geometry;
using PlanetGeneration.Core.Cartography.Planning;
using PlanetGeneration.Core.Application;
using PlanetGeneration.Core.Cartography;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Generation;

namespace PlanetGeneration.Core.SelfTest;

internal static partial class Program
{
    private static void TestSnapshotIsolation()
    {
        var generator = new DeterministicFieldFixture();
        var service = new WorldGenerationService(generator);
        var options = new GenerationOptions { Seed = 98, TargetCellCount = 256, Extent = new WorldExtent(1024, 512) };
        var snapshot = service.GenerateAsync(options).GetAwaiter().GetResult();
        var baseline = snapshot.Fields.Clone();
        var first = CartographyGenerator.Generate(snapshot, FantasyContinent01Blueprint.Create());
        AssertArraysEqual(snapshot.Fields, baseline, "制图不得污染已发布快照");
        var second = CartographyGenerator.Generate(snapshot, FantasyContinent01Blueprint.Create());
        AssertArraysEqual(snapshot.Fields, baseline, "重复制图不得累积改动");
        var jsonOptions = new JsonSerializerOptions { IncludeFields = true };
        Assert(JsonSerializer.Serialize(first.Brushes, jsonOptions) == JsonSerializer.Serialize(second.Brushes, jsonOptions),
            "同一快照重复制图应逐笔刷一致");

        var designed = service.GenerateAsync(options with
        {
            EnableCartographyDesigner = true, BlueprintName = "FantasyContinent01"
        }).GetAwaiter().GetResult();
        AssertArraysEqual(designed.Fields, baseline, "开启纯表现蓝图不应改变物理和模拟字段");
        Assert(designed.Stats == snapshot.Stats, "表现蓝图不应使统计与领域字段脱节");
        Assert(designed.Cartography != null && designed.Cartography.Brushes.Count > 0, "仍必须产出蓝图笔刷");
        var evolved = new WorldSimulationService().ReSimulateEpochAsync(snapshot, 200).GetAwaiter().GetResult();
        AssertArraysEqual(snapshot.Fields, baseline, "纪元推进不得覆盖原始快照");
        Assert(!ReferenceEquals(evolved.Fields, snapshot.Fields), "模拟必须使用独立字段");
        Assert(ReferenceEquals(evolved.Geometry, snapshot.Geometry), "纪元推进不必复制几何");
        Assert(evolved.Options.Epoch == 200 && snapshot.Options.Epoch == options.Epoch, "纪元状态必须独立");

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var previousCalls = generator.CallCount;
        try
        {
            service.GenerateAsync(options, cancellationToken: canceled.Token).GetAwaiter().GetResult();
            throw new InvalidOperationException("已取消的请求不应生成世界");
        }
        catch (OperationCanceledException) { }
        Assert(generator.CallCount == previousCalls, "已取消的请求不能进入基础场生成器");
    }

    private static void TestPlanningBaseline()
    {
        var options = new GenerationOptions { Seed = 98, TargetCellCount = 256, Extent = new WorldExtent(1024, 512) };
        var geometry = PolygonGridBuilder.Create(options.Extent, options.Seed, options.TargetCellCount);
        var fields = CellFields.Create(geometry.Count);
        for (var c = 0; c < geometry.Count; c++)
        {
            fields.Height[c] = 0.25f + 0.5f * (float)(geometry.SiteX[c] / geometry.Width);
            fields.Moisture[c] = 0.7f;
            fields.Temperature[c] = 0.6f;
        }
        var plan = WorldPlanner.Plan(geometry, fields, options, FantasyContinent01Blueprint.Create());
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { Plan = plan, Fields = fields },
            new JsonSerializerOptions { IncludeFields = true });
        var fingerprint = Convert.ToHexString(SHA256.HashData(bytes));
        // 2026-09-23：在拆分 WorldPlanner 前记录；覆盖所有规划输出及被规划阶段修改的字段。
        const string expected = "C65FCFCF84AA2C0C0C30352CABEA28180363D7F972E031179563CE3A9D559194";
        Assert(fingerprint == expected, $"规划基线变化: {fingerprint}；算法变更需显式审核，而非静默更新基线。");
    }

    /// <summary>无需 Godot、模型文件或随机噪声库的服务级集成测试夹具。</summary>
    private sealed class DeterministicFieldFixture : IBaseFieldGenerator
    {
        public int CallCount { get; private set; }
        public BaseContinuousFields GenerateFields(GenerationOptions options, int fieldWidth, int fieldHeight)
        {
            CallCount++;
            var elevation = new float[fieldWidth, fieldHeight];
            var temperature = new float[fieldWidth, fieldHeight];
            var moisture = new float[fieldWidth, fieldHeight];
            for (var y = 0; y < fieldHeight; y++)
                for (var x = 0; x < fieldWidth; x++)
                {
                    elevation[x, y] = 0.25f + 0.5f * x / fieldWidth;
                    temperature[x, y] = 0.6f;
                    moisture[x, y] = 0.7f;
                }
            return new BaseContinuousFields
            {
                Width = fieldWidth, Height = fieldHeight,
                Plates = new BasePlateField
                {
                    Width = fieldWidth, Height = fieldHeight,
                    PlateIds = new int[fieldWidth, fieldHeight],
                    BoundaryTypes = new PlateBoundaryType[fieldWidth, fieldHeight], Sites = new()
                },
                Elevation = elevation, Temperature = temperature, Moisture = moisture,
                Wind = new (float X, float Y)[fieldWidth, fieldHeight],
                Rock = new byte[fieldWidth, fieldHeight], Ore = new byte[fieldWidth, fieldHeight]
            };
        }
    }
}
