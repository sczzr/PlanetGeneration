using System;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>
/// 地貌类型。序号与 <c>Main.LandformType</c> **逐项一致**，因此可以直接按序号互转。
/// 这里单独定义一份是为了让分类器留在 Godot 无关的核心层，从而能被自检覆盖。
/// </summary>
public enum PolygonLandform
{
    DeepOcean,
    ShallowSea,
    CoastalPlain,
    Plain,
    Basin,
    DryBasin,
    Valley,
    RollingHills,
    Upland,
    Plateau,
    Mountain,
}

/// <summary>
/// 地块地貌分类：用**真实的多边形邻接**（<c>Cells.C</c>）替代栅格版的 8 邻域像素统计。
///
/// 这是"地块化"最直接的一处收益：
///   · 栅格版只能看 8 个固定方向，且方向与地形无关；
///   · 地块版看的是真实的 Voronoi 邻居，方向由点阵决定、更接近各向同性，
///     而且统计量（局部起伏、被包围程度、坡度信号）天然按"地块"这个地理单元来算。
///
/// 判定阈值与栅格版逐条对应，只做了一处必要调整：
/// **邻接数不是固定的 8**（抖动方格下是 5~7），所以"有多少个邻居更高/更低"这类判据
/// 改成按**比例**比较，否则地块会因为邻居少而永远达不到阈值。
/// </summary>
public static class PolygonLandformClassifier
{
    /// <summary>判定"邻居更高/更低"的高度差阈值；与栅格版一致。</summary>
    private const float NeighborHeightDelta = 0.018f;

    /// <summary>
    /// 对单个地块分类。
    /// </summary>
    /// <param name="grid">地块网格。</param>
    /// <param name="cellId">地块编号。</param>
    /// <param name="seaLevel">海平面。</param>
    /// <param name="basinSensitivity">盆地灵敏度（0.5~2.0），与栅格版的同名参数一致。</param>
    public static PolygonLandform Classify(PolygonGrid grid, int cellId, float seaLevel, float basinSensitivity)
    {
        var fields = grid.Fields;
        var safeSea = Math.Clamp(seaLevel, 0.0001f, 0.9999f);
        var sensitivity = Math.Clamp(basinSensitivity, 0.5f, 2.0f);
        var current = fields.Height[cellId];

        if (current < safeSea)
        {
            var depth = (safeSea - current) / Math.Max(safeSea, 0.0001f);
            return depth > 0.45f ? PolygonLandform.DeepOcean : PolygonLandform.ShallowSea;
        }

        var relativeHeight = (current - safeSea) / Math.Max(1f - safeSea, 0.0001f);

        var start = grid.CellNeighborStart[cellId];
        var end = grid.CellNeighborStart[cellId + 1];
        var neighborCount = end - start;
        if (neighborCount == 0)
        {
            return PolygonLandform.Plain;
        }

        var minHeight = current;
        var maxHeight = current;
        var sum = 0f;
        var higherCount = 0;
        var lowerCount = 0;
        var nearSea = false;

        for (var k = start; k < end; k++)
        {
            var neighbor = grid.CellNeighbors[k];
            var height = fields.Height[neighbor];

            if (height < minHeight)
            {
                minHeight = height;
            }

            if (height > maxHeight)
            {
                maxHeight = height;
            }

            sum += height;

            if (height <= safeSea)
            {
                nearSea = true;
            }

            var diff = height - current;
            if (diff > NeighborHeightDelta)
            {
                higherCount++;
            }
            else if (diff < -NeighborHeightDelta)
            {
                lowerCount++;
            }
        }

        var meanHeight = sum / neighborCount;
        var localRelief = maxHeight - minHeight;
        var depression = Math.Max(meanHeight - current, 0f);
        var slopeSignal = Math.Max(maxHeight - current, current - minHeight);
        var normalizedSensitivity = (sensitivity - 0.5f) / 1.5f;

        // 栅格版用的是"8 个邻居里有 5~7 个更高"，这里换成比例。
        var higherFraction = higherCount / (float)neighborCount;
        var lowerFraction = lowerCount / (float)neighborCount;
        var enclosedByHigher = higherFraction >= Lerp(5f / 8f, 7f / 8f, normalizedSensitivity)
            && lowerFraction <= Lerp(2f / 8f, 0f, normalizedSensitivity);

        var basinHeightThreshold = Lerp(0.32f, 0.18f, normalizedSensitivity);
        var basinMoistureThreshold = Lerp(0.42f, 0.55f, normalizedSensitivity);
        var basinRiverThreshold = Lerp(0.02f, 0.05f, normalizedSensitivity);

        if (relativeHeight < basinHeightThreshold
            && enclosedByHigher
            && (fields.Moisture[cellId] > basinMoistureThreshold
                || fields.River[cellId] > basinRiverThreshold
                || depression > 0.009f))
        {
            return PolygonLandform.Basin;
        }

        var dryBasinHeightThreshold = basinHeightThreshold * 1.15f;
        if (relativeHeight < dryBasinHeightThreshold
            && enclosedByHigher
            && fields.Moisture[cellId] < 0.32f
            && fields.River[cellId] < 0.03f
            && depression > 0.006f)
        {
            return PolygonLandform.DryBasin;
        }

        if (!nearSea
            && fields.River[cellId] > 0.20f
            && relativeHeight > 0.10f
            && relativeHeight < 0.66f
            && slopeSignal > 0.014f)
        {
            return PolygonLandform.Valley;
        }

        if (nearSea && relativeHeight < 0.12f)
        {
            return PolygonLandform.CoastalPlain;
        }

        if (relativeHeight < 0.30f)
        {
            return PolygonLandform.Plain;
        }

        if (relativeHeight < 0.50f)
        {
            return PolygonLandform.RollingHills;
        }

        if (relativeHeight > 0.78f
            || (relativeHeight > 0.68f && (localRelief > 0.045f || slopeSignal > 0.050f)))
        {
            return PolygonLandform.Mountain;
        }

        if (relativeHeight > 0.64f && localRelief < 0.026f)
        {
            return PolygonLandform.Plateau;
        }

        if (relativeHeight > 0.52f)
        {
            return PolygonLandform.Upland;
        }

        return PolygonLandform.RollingHills;
    }

    /// <summary>
    /// 对全部地块分类。邻接是只读的，所以可以直接并行。
    /// </summary>
    /// <param name="grid">地块网格。</param>
    /// <param name="seaLevel">海平面。</param>
    /// <param name="basinSensitivity">盆地灵敏度。</param>
    /// <param name="destination">输出数组，长度为地块数。</param>
    public static void ClassifyAll(PolygonGrid grid, float seaLevel, float basinSensitivity, byte[] destination)
    {
        if (destination.Length < grid.Count)
        {
            throw new ArgumentException("输出数组长度小于地块数。", nameof(destination));
        }

        for (var cell = 0; cell < grid.Count; cell++)
        {
            destination[cell] = (byte)Classify(grid, cell, seaLevel, basinSensitivity);
        }
    }

    private static float Lerp(float from, float to, float t) => from + ((to - from) * Math.Clamp(t, 0f, 1f));
}
