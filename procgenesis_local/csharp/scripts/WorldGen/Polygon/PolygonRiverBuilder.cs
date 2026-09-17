using System;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>
/// 地块河流生成：在**地块图**上按最陡下降方向累积汇流，得到真正的河网。
///
/// 与栅格版 <c>RiverGenerator</c> 的区别：
///   · 栅格版是"从源头出发随机游走"——每一步只在 8 个固定方向里择优，
///     而且路径带随机抖动，所以河道方向受像素栅格约束、同一条河可能走出锯齿；
///   · 地块版是"全域累积"——每个地块把降水推给它的最陡下降邻居，
///     汇流自然汇集出主干，河道沿真实的多边形邻接流动，不受 8 方向限制。
///
/// 判定"是不是河道"用的是**排水面积**（有多少地块的水从这里流出去），而不是汇流量本身。
/// 这一点很关键：汇流量沿程单调递增，若按它的分位数取阈值，选出来的必然是"离海最近的那一批"，
/// 河道会全部挤在海岸线上、不向内陆延伸（这是先按汇流量实现、再靠离线预览图发现的问题）。
/// 按排水面积设阈值则天然给出树枝状河网：主流向内陆延伸得远，支流短。
///
/// 做法：
///   1. <see cref="PolygonTopologyBuilder.BuildDownslope"/> 求每个地块的最陡下降邻居；
///   2. 按高程降序一次遍历，同时累积"排水面积"（每块计 1）与"湿度加权汇流量"；
///      处理到某个地块时所有比它高的上游一定已经处理完，所以一次遍历即可，无需迭代收敛；
///   3. 排水面积 ≥ 阈值的地块算河道；强度由汇流量在河道集合内归一化得到。
///
/// 第 3 步的阈值直接由"河道占比"换算（阈值 ≈ 1 / 占比），所以密度参数是可解释的，
/// 不会因为地图尺寸或湿度基准不同而需要重新标定。
/// </summary>
public static class PolygonRiverBuilder
{
    /// <summary>河道强度的下限：让最细的河道也清晰可见，不至于淡到看不见。</summary>
    private const float MinRiverStrength = 0.15f;

    /// <summary>
    /// 生成地块河流，结果写入 <c>Fields.River</c>，同时把湿度加权汇流量留在 <c>Fields.Flux</c>。
    /// 需要先调用过 <see cref="PolygonTopologyBuilder.BuildDownslope"/>。
    /// </summary>
    /// <param name="grid">地块网格。</param>
    /// <param name="seaLevel">海平面；低于它的地块不参与河流。</param>
    /// <param name="riverFraction">
    /// 河道占比（0~1）：排水面积排在前这个比例内的陆地块算河道。
    /// 0.06 大约对应"河道地块彼此间隔 4 个地块"的观感。
    /// 注意排水面积是**整数**，分位数上会有一批并列值一起入选，
    /// 所以实际占比会略高于设定值（实测 6% 设定通常落在 6%~10%）。
    /// </param>
    public static void Generate(PolygonGrid grid, float seaLevel, float riverFraction)
    {
        var count = grid.Count;
        var height = grid.Fields.Height;
        var moisture = grid.Fields.Moisture;
        var downslope = grid.Fields.Downslope;
        var flux = grid.Fields.Flux;
        var river = grid.Fields.River;
        var fraction = Math.Clamp(riverFraction, 0.002f, 0.5f);

        // 按高程降序排列：保证"上游先算完"。
        var order = new int[count];
        for (var i = 0; i < count; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (a, b) => height[b].CompareTo(height[a]));

        var drainage = new float[count];
        Array.Clear(flux);

        for (var index = 0; index < count; index++)
        {
            var cell = order[index];
            if (height[cell] <= seaLevel)
            {
                continue;
            }

            // 自身先计入，再把累积结果推给下游。
            drainage[cell] += 1f;
            flux[cell] += Math.Max(moisture[cell], 0.02f);

            var down = downslope[cell];
            if (down < 0)
            {
                continue;
            }

            drainage[down] += drainage[cell];
            flux[down] += flux[cell];
        }

        // 阈值取"排水面积的 (1−占比) 分位数"。
        //
        // 排水面积沿程单调递增，所以取分位数等价于"保留排水面积最大的那一部分"，
        // 河网因此天然连通（某地块是河道 ⇒ 它的下游排水面积更大 ⇒ 下游必然也是河道）。
        // 用分位数而不是绝对阈值，是为了让"河道占比"这个参数与地形起伏无关：
        // 绝对阈值在穹顶地形上会一个河道都选不出来，在起伏剧烈的地形上又会漫山遍野都是河。
        var landDrainage = new float[count];
        var landCount = 0;
        for (var cell = 0; cell < count; cell++)
        {
            if (height[cell] > seaLevel)
            {
                landDrainage[landCount++] = drainage[cell];
            }
        }

        if (landCount == 0)
        {
            Array.Clear(river);
            return;
        }

        Array.Sort(landDrainage, 0, landCount);
        var thresholdIndex = Math.Clamp((int)(landCount * (1f - fraction)), 0, landCount - 1);
        var minDrainage = Math.Max(2f, landDrainage[thresholdIndex]);

        // 强度归一化用的上界：只在河道地块里取最大值，避免被内陆极端值压扁。
        var maxFlux = 0f;
        for (var cell = 0; cell < count; cell++)
        {
            if (height[cell] > seaLevel && drainage[cell] >= minDrainage && flux[cell] > maxFlux)
            {
                maxFlux = flux[cell];
            }
        }

        if (maxFlux <= 0f)
        {
            Array.Clear(river);
            return;
        }

        for (var cell = 0; cell < count; cell++)
        {
            if (height[cell] <= seaLevel || drainage[cell] < minDrainage)
            {
                river[cell] = 0f;
                continue;
            }

            // 开方压缩动态范围：否则支流会被主流的汇流量压到看不见。
            var t = MathF.Sqrt(Math.Clamp(flux[cell] / maxFlux, 0f, 1f));
            river[cell] = Math.Clamp(MinRiverStrength + ((1f - MinRiverStrength) * t), 0f, 1f);
        }
    }
}
