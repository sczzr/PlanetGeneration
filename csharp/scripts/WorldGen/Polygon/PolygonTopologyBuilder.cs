using PolygonComponentResult = PlanetGeneration.Core.Geometry.PolygonComponentResult;
using System;
using System.Collections.Generic;

namespace PlanetGeneration.WorldGen.Polygon;

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
        => Core.Geometry.PolygonTopologyBuilder.BuildDownslope(grid.Geometry, grid.Fields, minDrop);

    /// <summary>保留旧 API 直接使用湿度的默认值，避免改成 Core 的最低降水阈值。</summary>
    public static void BuildFlux(PolygonGrid grid, float[]? initialWater = null)
        => Core.Geometry.PolygonTopologyBuilder.BuildFlux(
            grid.Geometry, grid.Fields, initialWater ?? grid.Fields.Moisture);

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
        var mask = new bool[grid.Count];
        for (var cell = 0; cell < grid.Count; cell++)
            mask[cell] = (grid.Fields.Height[cell] > seaLevel) == land;
        return Core.Geometry.PolygonTopologyBuilder.FindConnectedComponents(grid.Geometry, mask);
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
