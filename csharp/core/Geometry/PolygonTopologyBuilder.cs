using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Domain;

namespace PlanetGeneration.Core.Geometry;

/// <summary>连通分量划分结果。</summary>
public sealed class PolygonComponentResult
{
    public required int[] ComponentId { get; init; }
    public required int Count { get; init; }
    public required int[] ComponentSize { get; init; }
}

/// <summary>
/// 地块拓扑运算：最陡下降、汇流累积、连通分量划分等图算法。
/// </summary>
public static class PolygonTopologyBuilder
{
    /// <summary>
    /// 为每个地块求最陡下降邻居（用于地貌分析与水文下泄）。
    /// </summary>
    public static void BuildDownslope(CellGeometry geometry, CellFields fields, double minDrop = 1e-4d)
    {
        var count = geometry.Count;
        var height = fields.Height;
        var downslope = fields.Downslope;

        for (var i = 0; i < count; i++)
        {
            var best = -1;
            var bestDrop = minDrop;
            var currentHeight = height[i];

            var start = geometry.CellNeighborStart[i];
            var end = geometry.CellNeighborStart[i + 1];
            for (var k = start; k < end; k++)
            {
                var neighbor = geometry.CellNeighbors[k];
                var distance = geometry.Extent.DistanceWrapped(
                    new PolyVec2(geometry.SiteX[i], geometry.SiteY[i]),
                    new PolyVec2(geometry.SiteX[neighbor], geometry.SiteY[neighbor]));

                if (distance <= 1e-6d) continue;

                var drop = (currentHeight - height[neighbor]) / distance;
                if (drop <= bestDrop) continue;

                bestDrop = drop;
                best = neighbor;
            }

            downslope[i] = best;
        }
    }

    /// <summary>
    /// 累积汇流量（基于已有 Downslope 与湿度）。
    /// </summary>
    public static void BuildFlux(CellGeometry geometry, CellFields fields, float[]? initialWater = null)
    {
        var count = geometry.Count;
        var height = fields.Height;
        var moisture = fields.Moisture;
        var downslope = fields.Downslope;
        var flux = fields.Flux;

        var order = new int[count];
        for (var i = 0; i < count; i++)
        {
            order[i] = i;
            flux[i] = 0f;
        }

        Array.Sort(order, (a, b) => height[b].CompareTo(height[a]));

        for (var idx = 0; idx < count; idx++)
        {
            var cell = order[idx];
            var baseWater = initialWater != null ? initialWater[cell] : Math.Max(moisture[cell], 0.05f);
            flux[cell] += baseWater;

            var target = downslope[cell];
            if (target >= 0 && target < count)
            {
                flux[target] += flux[cell];
            }
        }
    }

    /// <summary>
    /// 基于布尔掩码的图连通分量划分（BFS）。
    /// </summary>
    public static PolygonComponentResult FindConnectedComponents(CellGeometry geometry, bool[] mask)
    {
        var count = geometry.Count;
        var componentId = new int[count];
        for (var i = 0; i < count; i++) componentId[i] = -1;

        var componentSizes = new List<int>();
        var currentComponent = 0;
        var queue = new Queue<int>(64);

        for (var seed = 0; seed < count; seed++)
        {
            if (!mask[seed] || componentId[seed] >= 0) continue;

            var size = 0;
            componentId[seed] = currentComponent;
            queue.Enqueue(seed);

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                size++;

                var start = geometry.CellNeighborStart[cell];
                var end = geometry.CellNeighborStart[cell + 1];
                for (var k = start; k < end; k++)
                {
                    var neighbor = geometry.CellNeighbors[k];
                    if (mask[neighbor] && componentId[neighbor] < 0)
                    {
                        componentId[neighbor] = currentComponent;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            componentSizes.Add(size);
            currentComponent++;
        }

        return new PolygonComponentResult
        {
            ComponentId = componentId,
            Count = currentComponent,
            ComponentSize = componentSizes.ToArray(),
        };
    }
}
