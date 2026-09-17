using System;
using System.Collections.Generic;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>连通分量划分结果。</summary>
public sealed class PolygonComponentResult
{
    /// <summary>每个地块所属的连通分量编号；不属于任何分量时为 -1。</summary>
    public required int[] ComponentId { get; init; }

    /// <summary>连通分量数量。</summary>
    public required int Count { get; init; }

    /// <summary>每个分量的地块数量，下标即分量编号。</summary>
    public required int[] ComponentSize { get; init; }
}

/// <summary>
/// 地块拓扑运算：邻接关系之上派生出来的结构。
///
/// 这些运算在栅格模型里要么很别扭（8 邻域择优、BFS 要找像素邻居），
/// 要么根本没法做（面积加权、真正的"下游方向"）。换成地块拓扑之后就都是图上的常规操作。
/// </summary>
public static class PolygonTopologyBuilder
{
    /// <summary>
    /// 为每个地块求"最陡下降邻居"。
    ///
    /// 这是栅格河流算法里 <c>RiverGenerator.SelectNextCell</c> 那套"8 邻域择优"的地块版：
    /// 邻接表取代了硬编码的 8 个偏移量，坡度计算也用上了地块中心距离，
    /// 因此河流可以沿任意方向的邻接流动，不再受 8 个固定方向限制。
    /// </summary>
    /// <param name="grid">地块网格。</param>
    /// <param name="minDrop">最小落差；低于此值视为平地，不形成流向（避免抖动噪声造出假河道）。</param>
    public static void BuildDownslope(PolygonGrid grid, double minDrop = 1e-4d)
    {
        var count = grid.Count;
        var height = grid.Fields.Height;
        var downslope = grid.Fields.Downslope;

        for (var i = 0; i < count; i++)
        {
            var best = -1;
            var bestDrop = minDrop;
            var currentHeight = height[i];

            var start = grid.CellNeighborStart[i];
            var end = grid.CellNeighborStart[i + 1];
            for (var k = start; k < end; k++)
            {
                var neighbor = grid.CellNeighbors[k];

                // 按"单位距离的落差"比较，而不是绝对落差：
                // 地块面积不均，用绝对落差会让大地块上的河流跑偏。
                var distance = grid.WrappedDistance(grid.SiteX[i], grid.SiteY[i], grid.SiteX[neighbor], grid.SiteY[neighbor]);
                if (distance <= 1e-6d)
                {
                    continue;
                }

                var drop = (currentHeight - height[neighbor]) / distance;
                if (drop <= bestDrop)
                {
                    continue;
                }

                bestDrop = drop;
                best = neighbor;
            }

            downslope[i] = best;
        }
    }

    /// <summary>
    /// 按流向累积汇流量（FMG 的 <c>cells.flux</c> 思路）。
    ///
    /// 做法：把地块按高程降序排列，然后从高到低把每个地块的累积水量推给它的下游。
    /// 因为处理到某个地块时，所有比它高的上游一定已经处理完，所以一次遍历即可，无需迭代收敛。
    /// </summary>
    /// <param name="grid">地块网格，需先调用 <see cref="BuildDownslope"/>。</param>
    /// <param name="initialWater">每个地块的初始降水；为 null 时按湿度取值。</param>
    public static void BuildFlux(PolygonGrid grid, float[]? initialWater = null)
    {
        var count = grid.Count;
        var height = grid.Fields.Height;
        var moisture = grid.Fields.Moisture;
        var downslope = grid.Fields.Downslope;
        var flux = grid.Fields.Flux;

        var order = new int[count];
        for (var i = 0; i < count; i++)
        {
            order[i] = i;
            flux[i] = 0f;
        }

        // 按高程降序：保证"上游先算完"。
        Array.Sort(order, (a, b) => height[b].CompareTo(height[a]));

        for (var index = 0; index < count; index++)
        {
            var cell = order[index];
            var water = initialWater != null ? initialWater[cell] : moisture[cell];
            flux[cell] += water;

            var down = downslope[cell];
            if (down >= 0)
            {
                flux[down] += flux[cell];
            }
        }
    }

    /// <summary>
    /// 从起点出发按邻接扩展指定环数，返回途中的全部地块。
    /// 这是"刷选"与"以某地为中心的影响范围"的通用原语。
    /// </summary>
    public static List<int> CollectRing(PolygonGrid grid, int startCell, int rings)
    {
        var result = new List<int> { startCell };
        if (rings <= 0)
        {
            return result;
        }

        var visited = new HashSet<int> { startCell };
        var frontier = new List<int> { startCell };

        for (var ring = 0; ring < rings && frontier.Count > 0; ring++)
        {
            var next = new List<int>();
            foreach (var cell in frontier)
            {
                var start = grid.CellNeighborStart[cell];
                var end = grid.CellNeighborStart[cell + 1];
                for (var k = start; k < end; k++)
                {
                    var neighbor = grid.CellNeighbors[k];
                    if (!visited.Add(neighbor))
                    {
                        continue;
                    }

                    result.Add(neighbor);
                    next.Add(neighbor);
                }
            }

            frontier = next;
        }

        return result;
    }

    /// <summary>
    /// 按邻接划分连通分量。陆地（高程高于海平面）与水域各算一套，
    /// 于是"有几块大陆""有几座岛""湖泊是否封闭"都可以直接读出来。
    /// </summary>
    /// <param name="grid">地块网格。</param>
    /// <param name="seaLevel">海平面阈值。</param>
    /// <param name="land">true 统计陆地连通分量，false 统计水域。</param>
    public static PolygonComponentResult FindComponents(PolygonGrid grid, float seaLevel, bool land = true)
    {
        var count = grid.Count;
        var height = grid.Fields.Height;
        var componentId = new int[count];
        var sizes = new List<int>();

        for (var i = 0; i < count; i++)
        {
            componentId[i] = -1;
        }

        var queue = new Queue<int>();

        for (var seed = 0; seed < count; seed++)
        {
            if (componentId[seed] >= 0)
            {
                continue;
            }

            var isLand = height[seed] > seaLevel;
            if (isLand != land)
            {
                continue;
            }

            var id = sizes.Count;
            var size = 0;
            componentId[seed] = id;
            queue.Enqueue(seed);

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                size++;

                var start = grid.CellNeighborStart[cell];
                var end = grid.CellNeighborStart[cell + 1];
                for (var k = start; k < end; k++)
                {
                    var neighbor = grid.CellNeighbors[k];
                    if (componentId[neighbor] >= 0)
                    {
                        continue;
                    }

                    if (height[neighbor] > seaLevel != land)
                    {
                        continue;
                    }

                    componentId[neighbor] = id;
                    queue.Enqueue(neighbor);
                }
            }

            sizes.Add(size);
        }

        return new PolygonComponentResult
        {
            ComponentId = componentId,
            Count = sizes.Count,
            ComponentSize = sizes.ToArray(),
        };
    }

    /// <summary>
    /// 把一组"种子地块"扩展成"种子 + 其全部直接邻居"的掩码。
    ///
    /// 用于城市标记、兴趣点高亮，以及后续的刷选预览。
    /// 用邻接而不是像素半径，好处是标记的大小与地块尺度一致——
    /// 不会出现"大地块上的标记小得看不见、小地块上又糊成一片"。
    /// </summary>
    /// <param name="grid">地块网格。</param>
    /// <param name="seeds">种子掩码，长度不小于地块数。</param>
    public static bool[] ExpandToNeighborMask(PolygonGrid grid, bool[] seeds)
    {
        var mask = new bool[grid.Count];
        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (cell >= seeds.Length || !seeds[cell])
            {
                continue;
            }

            mask[cell] = true;

            var start = grid.CellNeighborStart[cell];
            var end = grid.CellNeighborStart[cell + 1];
            for (var k = start; k < end; k++)
            {
                mask[grid.CellNeighbors[k]] = true;
            }
        }

        return mask;
    }

    /// <summary>
    /// 按面积给地块排序并返回降序下标，配合 <see cref="PolygonGrid.Area"/> 做面积加权统计。
    /// 旧栅格模型里每个单元权重相同，换成地块之后"面积"才第一次成为可用维度。
    /// </summary>
    public static int[] OrderByAreaDescending(PolygonGrid grid)
    {
        var order = new int[grid.Count];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (a, b) => grid.Area[b].CompareTo(grid.Area[a]));
        return order;
    }
}
