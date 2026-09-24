using System.Text.Json;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;
using PlanetGeneration.Core.Simulation;
using Legacy = PlanetGeneration.WorldGen.Polygon;

namespace PlanetGeneration.Core.SelfTest;

internal static partial class Program
{
    private static void TestLegacyGeometryCompatibility()
    {
        foreach (var seed in new[] { 123, -17 })
        {
            var legacy = Legacy.PolygonGridBuilder.Create(257, 129, seed, 256, 8, out var stats);
            var core = PolygonGridBuilder.CreateForRaster(257, 129, seed, 256, 8, out var expectedStats);
            Assert(JsonSerializer.Serialize(stats) == JsonSerializer.Serialize(expectedStats), "兼容入口构建统计不同");
            Assert(ReferenceEquals(legacy.VertexX, legacy.Geometry.VertexX), "适配器不得复制几何数组");
            AssertArraysEqual(legacy.Geometry, core, "几何结果");
            Assert(legacy.Fields.CityId.All(id => id == -1), "城市哨兵值必须为 -1");
            Assert(legacy.Fields.Downslope.All(id => id == -1), "流向哨兵值必须为 -1");
            foreach (var x in new[] { -514.1, -0.1, 0.1, 256.9, 257.1, 771.1 })
            {
                Assert(legacy.FindCell(x, 30) == core.FindCell(x, 30), "跨缝拾取必须与 Core 一致");
                Assert(legacy.FindCell(x, 30) == legacy.FindCell(x + 257, 30), "拾取必须满足水平周期性");
            }
            Assert(Legacy.PolygonGridValidator.Validate(legacy, seed).Passed, "迁移后几何校验失败");
            for (var c = 0; c < legacy.Count; c++)
                Assert(legacy.GetHighlightRings(c).SelectMany(r => r).SequenceEqual(core.GetHighlightRings(c).SelectMany(r => r)),
                    "高亮环坐标必须与 Core 一致");

            var map = Legacy.PolygonRasterizer.BuildCellMap(legacy, 257, 129);
            var palette = Enumerable.Range(0, legacy.Count * 4).Select(i => (byte)(i % 251)).ToArray();
            var pixels = new byte[257 * 129 * 4];
            Legacy.PolygonRasterizer.FillRgba(pixels, 257, 129, map, palette);
            Assert(pixels.SequenceEqual(PolygonRasterizer.RasterizeCells(map, palette)), "旧颜色填充必须逐字节匹配 Core");
            var scaled = Legacy.PolygonRasterizer.BuildCellMap(legacy, 128, 64);
            Assert(scaled.Cells.SequenceEqual(PolygonRasterizer.BuildCellMap(core, 128, 64).Cells), "兼容入口支持缩放归属图");
            for (var y = 0; y < 129; y += 9)
                for (var x = 0; x < 257; x += 7)
                    Assert(map.CellAt(x, y) == core.FindCell(x + 0.5, y + 0.5), "旧栅格归属必须与 Core 拾取一致");
            var raster = new float[257, 129];
            for (var y = 0; y < 129; y++)
                for (var x = 0; x < 257; x++) raster[x, y] = 0.375f;
            Legacy.RasterPolygonBridge.SampleContinuous(map, raster, legacy.Fields.Height);
            Assert(legacy.Fields.Height.All(value => value == 0.375f), "旧采样桥接必须写入 Core 字段");
        }
        var tiny = Legacy.PolygonGridBuilder.Create(2, 2, 1, 32768);
        Assert(tiny.Count == 4, "保留旧入口的微型地图上限");
        Assert(Legacy.PolygonGridBuilder.SuggestCellsDesired(2, 2, 4) == 4, "微型地图档位不能产生 min > max");
        Assert(Legacy.PolygonGridBuilder.DefaultCellsDesired == 32768, "旧默认档位不能被 Core 默认值改变");
    }

    private static void TestLegacySimulationCompatibility()
    {
        var grid = Legacy.PolygonGridBuilder.Create(257, 129, 132, 256);
        var fields = grid.Fields;
        for (var c = 0; c < grid.Count; c++)
        {
            fields.Height[c] = 0.25f + 0.6f * (float)(grid.SiteY[c] / grid.Height);
            fields.Temperature[c] = 0.6f;
            fields.Moisture[c] = c % 7 == 0 ? 0f : 0.7f;
            fields.Biome[c] = (byte)(fields.Height[c] <= 0.45f ? BiomeType.Ocean : BiomeType.TemperateSeasonalForest);
            fields.CityId[c] = c % 20 == 0 ? c / 20 : -1;
        }
        var expected = fields.Clone();
        foreach (var land in new[] { true, false })
        {
            var components = Legacy.PolygonTopologyBuilder.FindComponents(grid, 0.45f, land);
            var expectedComponents = PolygonTopologyBuilder.FindConnectedComponents(grid.Geometry,
                fields.Height.Select(h => (h > 0.45f) == land).ToArray());
            AssertArraysEqual(components, expectedComponents, "连通分量");
            Assert(components.Count == expectedComponents.Count, "连通分量数");
        }
        Legacy.PolygonTopologyBuilder.BuildDownslope(grid);
        PolygonTopologyBuilder.BuildDownslope(grid.Geometry, expected);
        AssertArraysEqual(fields, expected, "下游关系");
        // 旧 BuildFlux 默认直接使用湿度；Core 默认另有最低降水值。
        Legacy.PolygonTopologyBuilder.BuildFlux(grid);
        PolygonTopologyBuilder.BuildFlux(grid.Geometry, expected, expected.Moisture);
        AssertArraysEqual(fields, expected, "兼容汇流");
        foreach (var enabled in new[] { true, false })
        {
            Legacy.PolygonRiverBuilder.Generate(grid, 0.45f, 0.09f, enabled);
            PolygonRiverBuilder.Generate(grid.Geometry, expected, 0.45f, enabled, 0.09f / PolygonRiverBuilder.DefaultRiverCellFraction);
            AssertArraysEqual(fields, expected, "河网和汇流");
            if (!enabled) Assert(fields.River.All(v => v == 0), "关闭河流必须清零河网");
        }
        Legacy.PolygonLandformClassifier.ClassifyAll(grid, 0.45f, LandformOptions.Default, fields.Landform);
        PolygonLandformClassifier.ClassifyAll(grid.Geometry, expected, 0.45f, LandformOptions.Default, expected.Landform);
        AssertArraysEqual(fields, expected, "地貌分类");
        var ecology = Legacy.PolygonEcologySimulator.Simulate(grid, 132, 100, 65, 45, 55, 0.45f);
        var coreEcology = PolygonEcologySimulator.Simulate(grid.Geometry, expected, 132, 100, 65, 45, 55, 0.45f);
        Assert(ecology == coreEcology, "生态汇总结果必须一致");
        AssertArraysEqual(fields, expected, "生态字段");
        var civilization = Legacy.PolygonCivilizationSimulator.Simulate(grid, 132, 100, 45, 65, 0.45f);
        var coreCivilization = PolygonCivilizationSimulator.Simulate(grid.Geometry, expected, 132, 100, 45, 65, 0.45f);
        Assert(JsonSerializer.Serialize(civilization) == JsonSerializer.Serialize(coreCivilization), "文明事件及贸易路线必须一致");
        AssertArraysEqual(fields, expected, "文明字段");
        foreach (var value in Enum.GetValues<Legacy.PolygonLandform>())
            Assert(Enum.GetName((LandformType)(byte)value) == value.ToString(), "旧地貌枚举的持久化编号不得改变");
    }

    private static void AssertArraysEqual(object actual, object expected, string context)
    {
        foreach (var property in actual.GetType().GetProperties().Where(p => p.PropertyType.IsArray))
        {
            var a = ((Array)property.GetValue(actual)!).Cast<object>();
            var b = ((Array)property.GetValue(expected)!).Cast<object>();
            Assert(a.SequenceEqual(b), $"{context}: {property.Name} 不同");
        }
    }
}
