using System;
using System.Globalization;
using System.Text.Json;
using PlanetGeneration.Application;
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
    private static void TestCacheKeyStability()
    {
        var opt1 = new GenerationOptions
        {
            Seed = 100,
            TargetCellCount = 10000,
            SeaLevel = 0.45f,
        };

        var opt2 = opt1 with { TargetCellCount = 10000 };
        Assert(opt1.BuildCacheKey() == opt2.BuildCacheKey(), "相同参数的缓存键必须完全一致");

        var optDiffSeed = opt1 with { Seed = 101 };
        Assert(opt1.BuildCacheKey() != optDiffSeed.BuildCacheKey(), "不同种子的缓存键必须不同");

        var optDiffCells = opt1 with { TargetCellCount = 20000 };
        Assert(opt1.BuildCacheKey() != optDiffCells.BuildCacheKey(), "不同地块数的缓存键必须不同");

        var optDiffHeat = opt1 with { HeatFactor = 0.8f };
        Assert(opt1.BuildCacheKey() != optDiffHeat.BuildCacheKey(), "不同热量参数的缓存键必须不同");

        var optDiffMoisture = opt1 with { MoistureFactor = 0.8f };
        Assert(opt1.BuildCacheKey() != optDiffMoisture.BuildCacheKey(), "不同湿度参数的缓存键必须不同");

        var optDiffRivers = opt1 with { EnableRivers = false };
        Assert(opt1.BuildCacheKey() != optDiffRivers.BuildCacheKey(), "不同河流开关的缓存键必须不同");

        var optDiffTuning = opt1 with { Tuning = WorldTuningSnapshot.Legacy };
        Assert(opt1.BuildCacheKey() != optDiffTuning.BuildCacheKey(), "不同地形调优预设的缓存键必须不同");
    }
    private static void TestCacheKeyCoverage()
    {
        var baseline = new GenerationOptions { Seed = -123, Extent = new WorldExtent(257, 129) };
        var key = baseline.BuildCacheKey();
        // 枚举所有选项属性；以后增加参数却遗漏缓存键时，本测试必须失败。
        foreach (var property in typeof(GenerationOptions).GetProperties())
        {
            var changed = baseline with { };
            var value = property.GetValue(baseline);
            object replacement = value switch
            {
                int number => number + 1,
                float number => number + 0.01f,
                bool flag => !flag,
                Enum item => Enum.ToObject(item.GetType(), Convert.ToInt32(item) + 1),
                WorldExtent extent => extent with { Width = extent.Width + 0.25 },
                WorldTuningSnapshot tuning => tuning with { DeepOceanFactor = tuning.DeepOceanFactor + 0.01f },
                LandformOptions landform => landform with { BasinSensitivity = 1.2f },
                string text => text + "|seed:999",
                null => "FantasyContinent01",
                _ => throw new InvalidOperationException($"请为新参数增加测试: {property.Name}")
            };
            property.SetValue(changed, replacement);
            Assert(key != changed.BuildCacheKey(), $"缓存键遗漏参数 {property.Name}");
        }
        foreach (var property in typeof(WorldTuningSnapshot).GetProperties())
        {
            if (property.GetMethod!.IsStatic) continue;
            var tuning = baseline.Tuning with { };
            var value = property.GetValue(tuning);
            property.SetValue(tuning, value is float number ? (object)(number + 0.01f) : "Custom|name");
            Assert(key != (baseline with { Tuning = tuning }).BuildCacheKey(), $"缓存键遗漏微调参数 {property.Name}");
        }
        foreach (var property in typeof(LandformOptions).GetProperties())
        {
            object landform = baseline.LandformTuning;
            property.SetValue(landform, (float)property.GetValue(landform)! + 0.1f);
            Assert(key != (baseline with { LandformTuning = (LandformOptions)landform }).BuildCacheKey(),
                $"缓存键遗漏地貌参数 {property.Name}");
        }
        Assert(key != (baseline with { SeaLevel = MathF.BitIncrement(baseline.SeaLevel) }).BuildCacheKey(),
            "浮点量化不得把不同生成输入合并为相同缓存键");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var name in new[] { "en-US", "fr-FR", "zh-CN" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                Assert(baseline.BuildCacheKey() == key, "缓存键不能依赖系统区域设置");
            }
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    private static void TestArchiveHeaderCompatibility()
    {
        var options = new GenerationOptions { Seed = -17, Extent = new WorldExtent(512, 256) };
        foreach (var key in new[] { "ver:5|512x256|seed:-17", options.BuildCacheKey() })
        {
            foreach (var compare in new[] { false, true })
            {
                var prefix = "{\"cache_key\": " + JsonSerializer.Serialize(key) +
                    ", \"compare_mode\": " + (compare ? "true" : "false") + ", \"primary\": {\"data\": [";
                Assert(WorldArchiveHeader.TryParse(prefix, out var header), "截断的大型档案头必须仍可读取");
                Assert(header == new WorldArchiveHeader(-17, 512, 256, compare), "档案摘要错误");
            }
        }
        foreach (var input in new[] { "", "{", "not-json", "{\"cache_key\":\"ver:5|0x256|seed:1\"}",
            "{\"cache_key\":\"ver:5|9999999999999999x256|seed:1\"}",
            "{\"cache_key\":\"v:7|ext:512x256\"}" })
            Assert(!WorldArchiveHeader.TryParse(input, out _), "损坏档案不得抛异常或产生有效摘要");
    }

}
