using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PlanetGeneration.Core.SelfTest;

internal static partial class Program
{
    private sealed record TestCase(string Name, Action Run, string Suite, bool Quick = false);

    public static int Main(string[] args)
    {
        var tests = new TestCase[]
        {
            new("1. 多边形几何与守恒性自检 (2048 ~ 32768 档位)", TestGeometricIntegrity, "geometry", false),
            new("2. 目标地块数与实际地块数映射验证", TestTargetVsActualCounts, "geometry", false),
            new("3. 水文管线正确性与河流开关修复验证", TestHydrologyPipeline, "simulation", false),
            new("4. 分辨率解耦与全图精确拾取一致性 (1K / 2K / 4K)", TestResolutionDecoupling, "geometry", false),
            new("5. 组合图层栈、底图互斥与预设往返验证", TestLayerSystemAndPresets, "layers", true),
            new("6. 模拟确定性与无竞争双源验证", TestSimulationDeterminism, "simulation", false),
            new("7. 缓存键稳定性与生成参数不可变性验证", TestCacheKeyStability, "cache", true),
            new("8. 平滑曲线几何与网格拓扑水密性验证", TestCurvedCellGeometryAndMeshTopology, "geometry", false),
            new("9. 大型生态地貌识别、脊线骨架与边界平滑验证", TestMegaTerrainRegions, "terrain", false),
            new("10. 幻想制图层（Cartography）笔刷指令、区域风格与图层构建验证", TestFantasyCartographySystem, "cartography", false),
            new("11. Phase 4 美术控制（MountainRangePainter 2.0 / ForestTransition / 古地图符号 / 留白微地貌）验证", TestPhase4ArtisticControl, "cartography", false),
            new("12. CartographyDesigner 与 FantasyContinent01 构图蓝图编译验证", TestCartographyDesignerAndFantasyContinent01, "cartography", false),
            new("13. NarrativeRegion 叙事大区与 6 阶段流水线验证", TestNarrativeRegionsAnd6StagePipeline, "cartography", false),
            new("14. 世界规划式生成架构 (World Layout Graph / 连绵山墙 / 高山起源九曲水系 / 体块林海) 验证", TestWorldPlannerArchitecture, "cartography", false),
            new("15. 25 种地球典型地貌分类完整性、物理成因与守恒性验证", TestEarthGeomorphologyClassification, "terrain", false),
            new("16. Map Effects 奇幻制图方法论 (蛇曲/牛轭湖/三角洲/地裂/智能海岸/沼泽/托尔金排线) 验证", TestMapEffectsProceduralCartography, "cartography", false),
            new("缓存键字段覆盖与区域无关性", TestCacheKeyCoverage, "cache", true),
            new("新旧档案头部兼容性", TestArchiveHeaderCompatibility, "cache", true),
            new("旧几何入口与 Core 对照", TestLegacyGeometryCompatibility, "compatibility", true),
            new("旧模拟入口与 Core 逐字段对照", TestLegacySimulationCompatibility, "compatibility", true),
            new("生成服务、制图与模拟的快照隔离", TestSnapshotIsolation, "snapshot"),
            new("世界规划器搬移前后固定输入指纹", TestPlanningBaseline, "planning", true),
            new("A/B 完整快照存档往返", TestSnapshotArchiveRoundTrip, "persistence"),
            new("快照档案校验和原子保存", TestSnapshotArchiveValidationAndAtomicSave, "persistence"),
        };

        IEnumerable<TestCase> selected = tests;
        if (args.Length == 1 && args[0] == "--list")
        {
            foreach (var test in tests) Console.WriteLine($"{test.Suite}: {test.Name}");
            return 0;
        }
        if (args.Length == 1 && args[0] == "--quick") selected = tests.Where(t => t.Quick);
        else if (args.Length == 2 && args[0] == "--suite")
            selected = tests.Where(t => t.Suite.Equals(args[1], StringComparison.OrdinalIgnoreCase));
        else if (args.Length > 0 && !(args.Length == 1 && args[0] == "--full"))
        {
            Console.Error.WriteLine("用法: [--full | --quick | --list | --suite <suite>]");
            return 2;
        }

        var cases = selected.ToArray();
        if (cases.Length == 0)
        {
            Console.Error.WriteLine("没有匹配的测试；使用 --list 查看可用分组。");
            return 2;
        }
        Console.WriteLine($"PlanetGeneration 自检: {cases.Length} 项");
        var total = Stopwatch.StartNew();
        var failed = 0;
        foreach (var test in cases)
        {
            var timer = Stopwatch.StartNew();
            Console.Write($"[测试] {test.Name} ... ");
            try
            {
                test.Run();
                Console.WriteLine($"PASS ({timer.ElapsedMilliseconds} ms)");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"FAIL ({timer.ElapsedMilliseconds} ms)\n{ex}");
            }
        }
        Console.WriteLine($"自检完成: {cases.Length - failed} 通过, {failed} 失败 ({total.ElapsedMilliseconds} ms)");
        return failed == 0 ? 0 : 1;
    }

    private static void Assert([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"断言失败: {message}");
    }
}
