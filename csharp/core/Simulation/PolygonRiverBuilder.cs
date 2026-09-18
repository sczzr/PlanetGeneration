using System;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Simulation;

/// <summary>
/// 地块河流生成器：在地块邻接图上沿最陡下降方向累积汇流并形成连续河网。
///
/// 正确性修复：
///   1. 明确前置条件：必须在 Downslope 构建完毕后执行；
///   2. 汇流与河流单一职责产生：单次遍历同时累积排水面积与 Flux，不再被后置运算覆盖；
///   3. 严格遵循 EnableRivers 开关：关闭河流时 River 属性彻底清零，不被最低密度钳制重新激活。
/// </summary>
public static class PolygonRiverBuilder
{
    private const float MinRiverStrength = 0.15f;
    public const float DefaultRiverCellFraction = 0.06f;

    /// <summary>
    /// 生成地块河流与汇流场。
    /// </summary>
    public static void Generate(
        CellGeometry geometry,
        CellFields fields,
        float seaLevel,
        bool enableRivers,
        float riverDensity)
    {
        var count = geometry.Count;
        var height = fields.Height;
        var moisture = fields.Moisture;
        var downslope = fields.Downslope;
        var flux = fields.Flux;
        var river = fields.River;

        // 1. 确保下游方向已建立
        var hasDownslope = false;
        for (var i = 0; i < Math.Min(count, 100); i++)
        {
            if (downslope[i] != -1) { hasDownslope = true; break; }
        }
        if (!hasDownslope)
        {
            PolygonTopologyBuilder.BuildDownslope(geometry, fields);
        }

        // 2. 按高程降序：上游先处理
        var order = new int[count];
        for (var i = 0; i < count; i++) order[i] = i;
        Array.Sort(order, (a, b) => height[b].CompareTo(height[a]));

        var drainage = new float[count];
        Array.Clear(flux);
        Array.Clear(river);

        for (var index = 0; index < count; index++)
        {
            var cell = order[index];
            if (height[cell] <= seaLevel) continue;

            drainage[cell] += 1f;
            flux[cell] += Math.Max(moisture[cell], 0.02f);

            var down = downslope[cell];
            if (down >= 0 && down < count)
            {
                drainage[down] += drainage[cell];
                flux[down] += flux[cell];
            }
        }

        // 若河流开关关闭或密度为 0，仅保留 flux 汇流量供生态与地形查询，河道全部置 0
        if (!enableRivers || riverDensity <= 0f)
        {
            return;
        }

        var fraction = Math.Clamp(DefaultRiverCellFraction * riverDensity, 0.005f, 0.40f);

        // 3. 统计陆地地块排水面积分布，求分位数阈值
        var landDrainage = new float[count];
        var landCount = 0;
        for (var cell = 0; cell < count; cell++)
        {
            if (height[cell] > seaLevel)
            {
                landDrainage[landCount++] = drainage[cell];
            }
        }

        if (landCount == 0) return;

        Array.Sort(landDrainage, 0, landCount);
        var thresholdIndex = Math.Clamp((int)(landCount * (1f - fraction)), 0, landCount - 1);
        var minDrainage = Math.Max(2f, landDrainage[thresholdIndex]);

        // 4. 找到河道集合内的最大流量用于归一化
        var maxFlux = 0f;
        for (var cell = 0; cell < count; cell++)
        {
            if (height[cell] > seaLevel && drainage[cell] >= minDrainage && flux[cell] > maxFlux)
            {
                maxFlux = flux[cell];
            }
        }

        if (maxFlux <= 0f) return;

        // 5. 写入河道强度
        for (var cell = 0; cell < count; cell++)
        {
            if (height[cell] <= seaLevel || drainage[cell] < minDrainage)
            {
                river[cell] = 0f;
                continue;
            }

            var t = MathF.Sqrt(Math.Clamp(flux[cell] / maxFlux, 0f, 1f));
            river[cell] = Math.Clamp(MinRiverStrength + ((1f - MinRiverStrength) * t), 0f, 1f);
        }
    }
}
