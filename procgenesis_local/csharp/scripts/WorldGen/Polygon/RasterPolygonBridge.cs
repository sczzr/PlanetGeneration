using System;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>
/// 栅格 ⇄ 地块 的双向桥。
///
/// 这是"改造后原有功能正常运行"的核心机制：
///   · <c>Sample*</c>（栅格 → 地块）在生成期把连续场搬到多边形上，多边形因此成为地块属性的真源；
///   · <c>Splat*</c>（地块 → 栅格）按需把地块属性投影回栅格，
///     让尚未迁移的旧消费者（<c>WorldRenderer</c> 旧重载、<c>StatsCalculator</c>、小地图）零改动继续工作。
///
/// 两个方向共用同一张 <see cref="PolygonCellMap"/>，因此互为逆运算：
/// <c>SampleContinuous(SplatFloat(x)) == x</c> 严格成立（见自检工程的不变量测试）。
///
/// 采样规则按属性性质分两类：
///   · 连续量（高度、温度、湿度、生态、文明影响）用**面积加权平均**，保留梯度；
///   · 离散量（群系、岩石、矿产、板块）用**质心采样**，避免一个地块里出现两个群系。
/// 河流单独一档：河道很细，纯平均会被稀释到看不见，所以用"平均与最大值"混合。
/// </summary>
public static class RasterPolygonBridge
{
    /// <summary>河流采样中"面积平均"的权重，其余给最大值。</summary>
    public const float DefaultRiverMeanWeight = 0.7f;

    /// <summary>
    /// 连续量采样：按地块覆盖的像素做面积加权平均。
    /// </summary>
    /// <param name="map">像素归属图。</param>
    /// <param name="raster">源栅格，索引为 <c>[x, y]</c>。</param>
    /// <param name="destination">输出地块属性，长度须不小于地块数。</param>
    public static void SampleContinuous(PolygonCellMap map, float[,] raster, float[] destination)
    {
        ValidateRaster(map, raster, destination.Length, nameof(destination));

        var width = map.Width;
        var height = map.Height;
        var sums = new double[map.CellCount];

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                sums[map.Cells[rowOffset + x]] += raster[x, y];
            }
        }

        for (var cell = 0; cell < map.CellCount; cell++)
        {
            var count = map.PixelCounts[cell];
            destination[cell] = count > 0 ? (float)(sums[cell] / count) : 0f;
        }
    }

    /// <summary>
    /// 离散量采样：取地块质心所在像素的值。
    /// 质心一定落在自己的多边形内部（自检判据之一），所以这个取值是稳定的。
    /// </summary>
    public static void SampleDiscreteAtCentroid(PolygonGrid grid, PolygonCellMap map, byte[,] raster, byte[] destination)
    {
        if (destination.Length < grid.Count)
        {
            throw new ArgumentException("输出数组长度小于地块数。", nameof(destination));
        }

        for (var cell = 0; cell < grid.Count; cell++)
        {
            var (x, y) = CentroidPixel(grid, map, cell);
            destination[cell] = raster[x, y];
        }
    }

    /// <summary>离散量采样（int 版本）。</summary>
    public static void SampleDiscreteAtCentroid(PolygonGrid grid, PolygonCellMap map, int[,] raster, int[] destination)
    {
        if (destination.Length < grid.Count)
        {
            throw new ArgumentException("输出数组长度小于地块数。", nameof(destination));
        }

        for (var cell = 0; cell < grid.Count; cell++)
        {
            var (x, y) = CentroidPixel(grid, map, cell);
            destination[cell] = raster[x, y];
        }
    }

    /// <summary>
    /// 河流采样：面积平均与最大值混合。
    /// 河道只占少数像素，纯平均会把流量稀释到看不见；纯最大值又会让河道宽度失真。
    /// </summary>
    public static void SampleRiver(
        PolygonCellMap map,
        float[,] raster,
        float[] destination,
        float meanWeight = DefaultRiverMeanWeight)
    {
        ValidateRaster(map, raster, destination.Length, nameof(destination));

        var weight = Math.Clamp(meanWeight, 0f, 1f);
        var width = map.Width;
        var height = map.Height;
        var sums = new double[map.CellCount];
        var maxima = new float[map.CellCount];

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                var value = raster[x, y];
                var cell = map.Cells[rowOffset + x];
                sums[cell] += value;
                if (value > maxima[cell])
                {
                    maxima[cell] = value;
                }
            }
        }

        for (var cell = 0; cell < map.CellCount; cell++)
        {
            var count = map.PixelCounts[cell];
            var mean = count > 0 ? (float)(sums[cell] / count) : 0f;
            destination[cell] = (weight * mean) + ((1f - weight) * maxima[cell]);
        }
    }

    /// <summary>地块 → 栅格：把每个地块的值铺到它覆盖的像素上。</summary>
    public static void SplatFloat(PolygonCellMap map, float[] source, float[,] destination)
    {
        ValidateSplat(map, source.Length, destination, nameof(destination));

        var width = map.Width;
        var height = map.Height;
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                destination[x, y] = source[map.Cells[rowOffset + x]];
            }
        }
    }

    /// <summary>地块 → 栅格（byte 版本）。</summary>
    public static void SplatByte(PolygonCellMap map, byte[] source, byte[,] destination)
    {
        ValidateSplat(map, source.Length, destination, nameof(destination));

        var width = map.Width;
        var height = map.Height;
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                destination[x, y] = source[map.Cells[rowOffset + x]];
            }
        }
    }

    /// <summary>地块 → 栅格（int 版本）。</summary>
    public static void SplatInt(PolygonCellMap map, int[] source, int[,] destination)
    {
        ValidateSplat(map, source.Length, destination, nameof(destination));

        var width = map.Width;
        var height = map.Height;
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                destination[x, y] = source[map.Cells[rowOffset + x]];
            }
        }
    }

    /// <summary>地块 → 栅格（bool 版本）。</summary>
    public static void SplatBool(PolygonCellMap map, bool[] source, bool[,] destination)
    {
        ValidateSplat(map, source.Length, destination, nameof(destination));

        var width = map.Width;
        var height = map.Height;
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                destination[x, y] = source[map.Cells[rowOffset + x]];
            }
        }
    }

    /// <summary>
    /// 地块 → 栅格（双分量，用于风向这类二维量）。
    /// 刻意不直接吃 <c>Vector2[,]</c>：那会把 Godot 依赖带进这个可独立验证的模块。
    /// </summary>
    public static void SplatFloatPair(
        PolygonCellMap map,
        float[] sourceA,
        float[] sourceB,
        float[,] destinationA,
        float[,] destinationB)
    {
        ValidateSplat(map, sourceA.Length, destinationA, nameof(destinationA));
        ValidateSplat(map, sourceB.Length, destinationB, nameof(destinationB));

        var width = map.Width;
        var height = map.Height;
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                var cell = map.Cells[rowOffset + x];
                destinationA[x, y] = sourceA[cell];
                destinationB[x, y] = sourceB[cell];
            }
        }
    }

    /// <summary>
    /// 把一组点（城市、地标、出生点等）归属到地块。
    /// 传裸坐标而不是 <c>CityInfo</c>，同样是为了不引入 Godot 依赖。
    /// </summary>
    /// <param name="grid">地块网格。</param>
    /// <param name="pointX">点的横坐标（源栅格像素空间）。</param>
    /// <param name="pointY">点的纵坐标。</param>
    /// <param name="cellOfPoint">输出：每个点所属的地块编号。</param>
    public static void AssignPointsToCells(
        PolygonGrid grid,
        ReadOnlySpan<int> pointX,
        ReadOnlySpan<int> pointY,
        int[] cellOfPoint)
    {
        if (pointX.Length != pointY.Length)
        {
            throw new ArgumentException("横纵坐标数量不一致。", nameof(pointY));
        }

        if (cellOfPoint.Length < pointX.Length)
        {
            throw new ArgumentException("输出数组过小。", nameof(cellOfPoint));
        }

        for (var i = 0; i < pointX.Length; i++)
        {
            cellOfPoint[i] = grid.FindCell(pointX[i] + 0.5d, pointY[i] + 0.5d);
        }
    }

    /// <summary>把地块编号反向铺到每个点上（点没有归属时保留 -1）。</summary>
    public static void SplatCellToPoint(int[] cellOfPoint, int[] cellValue, int[] destination)
    {
        if (destination.Length < cellOfPoint.Length)
        {
            throw new ArgumentException("输出数组过小。", nameof(destination));
        }

        for (var i = 0; i < cellOfPoint.Length; i++)
        {
            var cell = cellOfPoint[i];
            destination[i] = cell >= 0 && cell < cellValue.Length ? cellValue[cell] : -1;
        }
    }

    /// <summary>取地块质心所在的像素坐标（已归一化到画布内）。</summary>
    private static (int X, int Y) CentroidPixel(PolygonGrid grid, PolygonCellMap map, int cell)
    {
        var cx = grid.NormalizeX(grid.CentroidX[cell]);
        var cy = grid.CentroidY[cell];

        var x = (int)Math.Floor(cx);
        var y = (int)Math.Floor(cy);

        // 质心理论上落在多边形内，这里只是防浮点越界。
        if (x < 0)
        {
            x = 0;
        }
        else if (x >= map.Width)
        {
            x = map.Width - 1;
        }

        if (y < 0)
        {
            y = 0;
        }
        else if (y >= map.Height)
        {
            y = map.Height - 1;
        }

        return (x, y);
    }

    private static void ValidateRaster(PolygonCellMap map, Array raster, int destinationLength, string parameterName)
    {
        if (raster.GetLength(0) != map.Width || raster.GetLength(1) != map.Height)
        {
            throw new ArgumentException("栅格尺寸与归属图不一致。", nameof(raster));
        }

        if (destinationLength < map.CellCount)
        {
            throw new ArgumentException("输出数组长度小于地块数。", parameterName);
        }
    }

    private static void ValidateSplat(PolygonCellMap map, int sourceLength, Array destination, string parameterName)
    {
        if (destination.GetLength(0) != map.Width || destination.GetLength(1) != map.Height)
        {
            throw new ArgumentException("目标栅格尺寸与归属图不一致。", nameof(destination));
        }

        if (sourceLength < map.CellCount)
        {
            throw new ArgumentException("输入数组长度小于地块数。", parameterName);
        }
    }
}
