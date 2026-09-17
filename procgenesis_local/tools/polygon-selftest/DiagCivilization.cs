using System;
using System.Collections.Generic;
using PlanetGeneration.WorldGen.Polygon;

namespace PolygonSelfTest;

/// <summary>
/// 地块文明模拟的诊断工具。用法：<c>PolygonSelfTest.dll diagciv</c>
///
/// 存在意义：自检能回答"实现是否满足我列出的判据"，但回答不了
/// "图为什么长这样"。P4 第二阶段的「内陆环状空心」就是自检全绿、
/// 只有看图才发现的——当时靠这个工具把根因从
/// 「地形惩罚过重」（错误假设）锁定到「乘性链天花板低于阈值」（真根因）。
///
/// **设计约束：只观察，不重算。**
/// 一度在这里复制过模拟器的评分公式来算"理论天花板"，结果修好模拟器后
/// 这份副本成了会误导人的陈旧数字。凡是与模拟器同源的量，
/// 一律读它的**实际输出**（<c>fields.Influence</c> 等），不在这里重新推导。
/// </summary>
internal static class DiagCivilization
{
    /// <summary>与模拟器同式的阈值换算：Lerp(0.20, 0.34, aggressionNorm)。仅用于标注参考线。</summary>
    private static float ClaimThreshold(float aggressionNorm)
        => 0.20f + ((0.34f - 0.20f) * Math.Clamp(aggressionNorm, 0f, 1f));

    public static void Run()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        const int width = 1024;
        const int height = 512;
        const float seaLevel = 0.5f;
        const float aggressionNorm = 0.42f;

        // 必须与预览走同一条字段准备路径（FillFields），否则诊断结论不适用于预览图。
        var grid = PreviewRenderer.BuildDiagnosticGrid(width, height, 20260917, 4096);
        var fields = grid.Fields;

        PolygonEcologySimulator.Simulate(grid, 20260917, 450, 68, 42, 75, seaLevel);
        var civ = PolygonCivilizationSimulator.Simulate(grid, 20260917, 450, 42, 68, seaLevel);

        var threshold = ClaimThreshold(aggressionNorm);
        Console.WriteLine($"地块 {grid.Count}，点距 {grid.SpacingX:0.00}，参考阈值 {threshold:0.0000}");
        Console.WriteLine($"政体 {civ.PolityCount} 个，控制率 {civ.ControlledLandPercent:0.0}%");
        Console.WriteLine($"聚落 村/镇/城邦 {civ.HamletCount}/{civ.TownCount}/{civ.CityStateCount}");

        // 城市候选数 = 政体数的硬上限（种子从城市里选），单独报出来避免误判成模拟器缺陷。
        var cityCells = 0;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (fields.CityId[cell] >= 0)
            {
                cityCells++;
            }
        }

        Console.WriteLine($"城市候选地块 {cityCells} 个（政体数的硬上限就在这，不是模拟器的问题）");

        PrintBucketTable(grid, fields, seaLevel, threshold);
        PrintInfluenceHistogram(fields, seaLevel);
        PrintUnclaimedInland(grid, fields, seaLevel);
    }

    /// <summary>按"入海距离"分桶：看影响力是否随离海变远而系统性塌陷。</summary>
    private static void PrintBucketTable(
        PolygonGrid grid, PolygonFields fields, float seaLevel, float threshold)
    {
        var ringDist = BuildOceanDistance(grid, seaLevel, 8);
        var sum = new double[9];
        var count = new int[9];
        var heightSum = new double[9];
        var potentialSum = new double[9];
        var claimed = new int[9];

        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (!IsLand(fields, cell, seaLevel))
            {
                continue;
            }

            var b = ringDist[cell];
            sum[b] += fields.Influence[cell];
            heightSum[b] += fields.Height[cell];
            potentialSum[b] += fields.CivilizationPotential[cell];
            count[b]++;
            if (fields.PolityId[cell] >= 0)
            {
                claimed[b]++;
            }
        }

        Console.WriteLine();
        Console.WriteLine("入海距离   块数   平均影响力   平均高程   平均潜力   声明率");
        for (var b = 0; b <= 8; b++)
        {
            if (count[b] == 0)
            {
                continue;
            }

            var label = b >= 8 ? ">=8 圈" : $"{b} 圈";
            Console.WriteLine(
                $"{label,-9} {count[b],5}   {sum[b] / count[b]:0.0000}      "
                + $"{heightSum[b] / count[b]:0.0000}     {potentialSum[b] / count[b]:0.0000}     "
                + $"{100.0 * claimed[b] / count[b]:0.0}%");
        }
    }

    /// <summary>影响力直方图：0 附近有多少块，直接反映"有多少陆地根本够不着"。</summary>
    private static void PrintInfluenceHistogram(PolygonFields fields, float seaLevel)
    {
        var buckets = new int[10];
        var landCells = 0;
        for (var cell = 0; cell < fields.Height.Length; cell++)
        {
            if (!IsLand(fields, cell, seaLevel))
            {
                continue;
            }

            landCells++;
            buckets[Math.Clamp((int)(fields.Influence[cell] * 10f), 0, 9)]++;
        }

        Console.WriteLine();
        Console.WriteLine($"陆地 {landCells} 块的影响力分布：");
        for (var i = 0; i < buckets.Length; i++)
        {
            var bar = new string('#', Math.Min(60, buckets[i] / 3));
            Console.WriteLine($"  [{i * 0.1:0.0}~{(i + 1) * 0.1:0.0}) {buckets[i],5}  {bar}");
        }
    }

    /// <summary>列出未归属的内陆地块及其各项输入，用于判断"是潜力不够还是距离太远"。</summary>
    private static void PrintUnclaimedInland(
        PolygonGrid grid, PolygonFields fields, float seaLevel)
    {
        var ringDist = BuildOceanDistance(grid, seaLevel, 8);
        var unclaimed = new List<(int Cell, int Ring)>();
        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (IsLand(fields, cell, seaLevel) && fields.PolityId[cell] < 0 && ringDist[cell] >= 3)
            {
                unclaimed.Add((cell, ringDist[cell]));
            }
        }

        Console.WriteLine();
        if (unclaimed.Count == 0)
        {
            Console.WriteLine("没有未归属的内陆地块（≥3 圈）——内陆已被完整填充。");
            return;
        }

        unclaimed.Sort((a, b) => b.Ring.CompareTo(a.Ring));
        Console.WriteLine($"未归属内陆地块（≥3 圈）共 {unclaimed.Count} 块，最深的几块：");
        for (var i = 0; i < unclaimed.Count && i < 6; i++)
        {
            var cell = unclaimed[i].Cell;
            Console.WriteLine(
                $"  地块 {cell}（{unclaimed[i].Ring} 圈）：高程 {fields.Height[cell]:0.000}，"
                + $"潜力 {fields.CivilizationPotential[cell]:0.0000}，"
                + $"河因子 {MathF.Sqrt(Math.Clamp(fields.River[cell], 0f, 1f)):0.0000}，"
                + $"影响力 {fields.Influence[cell]:0.0000}");
        }
    }

    /// <summary>陆地块判定：与模拟器口径一致（高于海平面且非海洋群系）。</summary>
    private static bool IsLand(PolygonFields fields, int cell, float seaLevel)
        => fields.Height[cell] > seaLevel && fields.Biome[cell] > 1;

    /// <summary>算每个陆地/海洋地块到最近海洋的圈数（BFS 环数，封顶 cap）。</summary>
    private static int[] BuildOceanDistance(PolygonGrid grid, float seaLevel, int cap)
    {
        var dist = new int[grid.Count];
        var queue = new Queue<int>();
        for (var cell = 0; cell < grid.Count; cell++)
        {
            var isOcean = !IsLand(grid.Fields, cell, seaLevel);
            dist[cell] = isOcean ? 0 : int.MaxValue;
            if (isOcean)
            {
                queue.Enqueue(cell);
            }
        }

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (dist[cell] >= cap)
            {
                continue;
            }

            var start = grid.CellNeighborStart[cell];
            var end = grid.CellNeighborStart[cell + 1];
            for (var k = start; k < end; k++)
            {
                var neighbor = grid.CellNeighbors[k];
                if (dist[neighbor] > dist[cell] + 1)
                {
                    dist[neighbor] = dist[cell] + 1;
                    queue.Enqueue(neighbor);
                }
            }
        }

        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (dist[cell] == int.MaxValue)
            {
                dist[cell] = cap;
            }
        }

        return dist;
    }
}
