using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>
/// 像素归属图：记录每个像素属于哪个地块。
///
/// 这是栅格与地块之间的**唯一接缝**——采样（栅格→地块）与投影（地块→栅格）都走这张图，
/// 因此两个方向天然一致：`Sample(Splat(x)) == x` 严格成立。
///
/// 用扁平 <c>int[]</c> 而不是 <c>int[,]</c>：这张图在 4096×2048 下有 840 万个元素，
/// 扁平数组既省一次索引乘法，也能直接交给 <c>Parallel.For</c> 分段处理。
/// </summary>
public sealed class PolygonCellMap
{
    internal PolygonCellMap(int width, int height, int cellCount, int[] cells, int[] pixelCounts, int unassignedPixels)
    {
        Width = width;
        Height = height;
        CellCount = cellCount;
        Cells = cells;
        PixelCounts = pixelCounts;
        UnassignedPixels = unassignedPixels;
    }

    /// <summary>归属图的宽度（等于源栅格宽度）。</summary>
    public int Width { get; }

    /// <summary>归属图的高度（等于源栅格高度）。</summary>
    public int Height { get; }

    /// <summary>地块总数。</summary>
    public int CellCount { get; }

    /// <summary>扁平归属数组，下标为 <c>y * Width + x</c>，值为地块编号。</summary>
    public int[] Cells { get; }

    /// <summary>每个地块覆盖的像素数量；面积加权采样直接用它当权重。</summary>
    public int[] PixelCounts { get; }

    /// <summary>
    /// 扫描线填充后、用最近站点兜底之前，没有被任何多边形覆盖的像素数量。
    /// 正常情况下应该是 0 或极小（相邻多边形共享边时的取整误差）；数值偏大说明填充有洞。
    /// </summary>
    public int UnassignedPixels { get; }

    /// <summary>取像素所属的地块。</summary>
    public int CellAt(int x, int y) => Cells[(y * Width) + x];

    /// <summary>总像素数。</summary>
    public long TotalPixels => (long)Width * Height;
}

/// <summary>
/// 多边形光栅化。
///
/// 刻意不引用 Godot（颜色用裸 RGBA 字节表示），因此可以在独立工程里验证：
/// 归属图有没有洞、覆盖是否恰好一次、颜色填充与描边是否正确。
/// Godot 侧的调用者把 <c>Color</c> 拆成字节传进来即可。
/// </summary>
public static class PolygonRasterizer
{
    /// <summary>
    /// 构建像素归属图：逐个地块扫描线填充其多边形。
    ///
    /// 扫描线按"像素中心是否落在区间内"判定（左闭右开），
    /// 相邻地块共享的那条边因此只会归给其中一个，不会出现重复覆盖。
    /// 极少数因取整漏掉的像素再用最近站点查询兜底，保证覆盖完整。
    /// </summary>
    public static PolygonCellMap BuildCellMap(PolygonGrid grid, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "归属图尺寸必须为正。");
        }

        var cellCount = grid.Count;
        var cells = new int[width * height];
        var pixelCounts = new int[cellCount];

        // 未填充标记用 -1；0 是合法地块编号，不能当哨兵。
        for (var i = 0; i < cells.Length; i++)
        {
            cells[i] = -1;
        }

        for (var cell = 0; cell < cellCount; cell++)
        {
            FillCell(grid, cell, width, height, cells, pixelCounts);
        }

        // 兜底：取整误差可能留下零星空洞，用精确拾取补上。
        var unassigned = 0;
        for (var index = 0; index < cells.Length; index++)
        {
            if (cells[index] >= 0)
            {
                continue;
            }

            unassigned++;
            var x = index % width;
            var y = index / width;
            var cell = grid.FindCell(x + 0.5d, y + 0.5d);
            cells[index] = cell;
            pixelCounts[cell]++;
        }

        return new PolygonCellMap(width, height, cellCount, cells, pixelCounts, unassigned);
    }

    /// <summary>
    /// 扫描线填充单个地块的多边形。
    /// 多边形保存在"未展开帧"里，骑在经度缝上的地块需要按 ±地图宽度补画，
    /// 这里对三个平移各试一次并裁剪到画布内。
    /// </summary>
    private static void FillCell(
        PolygonGrid grid,
        int cell,
        int width,
        int height,
        int[] cells,
        int[] pixelCounts)
    {
        var start = grid.CellVertexStart[cell];
        var end = grid.CellVertexStart[cell + 1];
        var vertexCount = end - start;
        if (vertexCount < 3)
        {
            return;
        }

        // 多边形整体的纵向范围，直接决定要扫描哪些行。
        var minY = double.MaxValue;
        var maxY = double.MinValue;
        for (var k = start; k < end; k++)
        {
            var y = grid.VertexY[k];
            if (y < minY)
            {
                minY = y;
            }

            if (y > maxY)
            {
                maxY = y;
            }
        }

        var firstRow = Math.Max(0, (int)Math.Floor(minY));
        var lastRow = Math.Min(height - 1, (int)Math.Ceiling(maxY));

        // 交点缓冲在整格内复用，避免每条扫描线都分配一次。
        var crossings = new double[vertexCount + 2];
        var isSeam = grid.CellSeam[cell];

        for (var row = firstRow; row <= lastRow; row++)
        {
            var scanY = row + 0.5d;

            FillScanline(grid, cell, start, end, scanY, 0d, width, row, cells, pixelCounts, crossings);

            // 骑在经度缝上的地块还要按 ±地图宽度各补画一份，否则缝两侧会缺一条。
            if (isSeam)
            {
                FillScanline(grid, cell, start, end, scanY, -grid.Width, width, row, cells, pixelCounts, crossings);
                FillScanline(grid, cell, start, end, scanY, grid.Width, width, row, cells, pixelCounts, crossings);
            }
        }
    }

    /// <summary>
    /// 填充一条扫描线：求多边形与水平线的交点，排序后按"左闭右开"区间落像素。
    /// </summary>
    private static void FillScanline(
        PolygonGrid grid,
        int cell,
        int start,
        int end,
        double scanY,
        double shift,
        int width,
        int row,
        int[] cells,
        int[] pixelCounts,
        double[] crossings)
    {
        var vertexCount = end - start;
        var crossingCount = 0;

        for (var k = 0; k < vertexCount; k++)
        {
            var index = start + k;
            var next = start + ((k + 1) % vertexCount);

            var y0 = grid.VertexY[index];
            var y1 = grid.VertexY[next];

            // 半开区间判定，保证水平边只被计入一次、顶点不被重复计入。
            if ((y0 <= scanY) == (y1 <= scanY))
            {
                continue;
            }

            var t = (scanY - y0) / (y1 - y0);
            crossings[crossingCount++] = grid.VertexX[index] + (t * (grid.VertexX[next] - grid.VertexX[index])) + shift;
        }

        if (crossingCount < 2)
        {
            return;
        }

        Array.Sort(crossings, 0, crossingCount);

        for (var i = 0; i + 1 < crossingCount; i += 2)
        {
            var left = crossings[i];
            var right = crossings[i + 1];

            // 像素中心 x + 0.5 落在 [left, right) 内。
            var firstX = (int)Math.Ceiling(left - 0.5d);
            var lastX = (int)Math.Ceiling(right - 0.5d) - 1;

            if (firstX < 0)
            {
                firstX = 0;
            }

            if (lastX > width - 1)
            {
                lastX = width - 1;
            }

            var rowOffset = row * width;
            for (var x = firstX; x <= lastX; x++)
            {
                var index = rowOffset + x;
                if (cells[index] >= 0)
                {
                    // 共享边上的像素归先画的那个地块，不重复计数。
                    continue;
                }

                cells[index] = cell;
                pixelCounts[cell]++;
            }
        }
    }

    /// <summary>
    /// 把一个任意多边形环填进 RGBA 缓冲（裁剪到画布内，不做经度环绕补画）。
    /// 用于高亮覆盖层与调试叠加——调用方负责把骑缝地块的多份副本都传进来。
    /// </summary>
    public static void FillRing(
        byte[] buffer,
        int width,
        int height,
        IReadOnlyList<PolyVec2> ring,
        byte red,
        byte green,
        byte blue,
        byte alpha)
    {
        if (ring.Count < 3 || width <= 0 || height <= 0)
        {
            return;
        }

        var minY = double.MaxValue;
        var maxY = double.MinValue;
        for (var i = 0; i < ring.Count; i++)
        {
            var y = ring[i].Y;
            if (y < minY)
            {
                minY = y;
            }

            if (y > maxY)
            {
                maxY = y;
            }
        }

        var firstRow = Math.Max(0, (int)Math.Floor(minY));
        var lastRow = Math.Min(height - 1, (int)Math.Ceiling(maxY));
        var crossings = new double[ring.Count + 2];

        for (var row = firstRow; row <= lastRow; row++)
        {
            var scanY = row + 0.5d;
            var crossingCount = 0;

            for (var k = 0; k < ring.Count; k++)
            {
                var a = ring[k];
                var b = ring[(k + 1) % ring.Count];

                if ((a.Y <= scanY) == (b.Y <= scanY))
                {
                    continue;
                }

                var t = (scanY - a.Y) / (b.Y - a.Y);
                crossings[crossingCount++] = a.X + (t * (b.X - a.X));
            }

            if (crossingCount < 2)
            {
                continue;
            }

            Array.Sort(crossings, 0, crossingCount);

            for (var i = 0; i + 1 < crossingCount; i += 2)
            {
                var firstX = Math.Max(0, (int)Math.Ceiling(crossings[i] - 0.5d));
                var lastX = Math.Min(width - 1, (int)Math.Ceiling(crossings[i + 1] - 0.5d) - 1);
                var rowOffset = row * width;

                for (var x = firstX; x <= lastX; x++)
                {
                    var target = (rowOffset + x) * 4;
                    buffer[target] = red;
                    buffer[target + 1] = green;
                    buffer[target + 2] = blue;
                    buffer[target + 3] = alpha;
                }
            }
        }
    }

    /// <summary>
    /// 用归属图与每地块的 RGBA 填满整块缓冲。
    /// <paramref name="cellRgba"/> 长度为 <c>地块数 × 4</c>，顺序为 R、G、B、A。
    /// </summary>
    public static void FillRgba(
        byte[] buffer,
        int width,
        int height,
        PolygonCellMap map,
        byte[] cellRgba)
    {
        if (buffer.Length < width * height * 4)
        {
            throw new ArgumentException("输出缓冲过小。", nameof(buffer));
        }

        if (cellRgba.Length < map.CellCount * 4)
        {
            throw new ArgumentException("颜色表过小。", nameof(cellRgba));
        }

        if (width != map.Width || height != map.Height)
        {
            throw new ArgumentException("归属图尺寸与输出尺寸不一致。", nameof(width));
        }

        Parallel.For(0, height, row =>
        {
            var cellRow = row * width;
            var pixelRow = row * width * 4;

            for (var x = 0; x < width; x++)
            {
                var cell = map.Cells[cellRow + x];
                var source = cell * 4;
                var target = pixelRow + (x * 4);
                buffer[target] = cellRgba[source];
                buffer[target + 1] = cellRgba[source + 1];
                buffer[target + 2] = cellRgba[source + 2];
                buffer[target + 3] = cellRgba[source + 3];
            }
        });
    }

    /// <summary>
    /// 给地块边界描边：只要四邻域里有不同地块，这个像素就算边界像素。
    /// 描边是"地块感"最直接的来源，也是栅格渲染给不出的效果。
    /// </summary>
    public static void StrokeBorders(
        byte[] buffer,
        int width,
        int height,
        PolygonCellMap map,
        byte red,
        byte green,
        byte blue,
        byte alpha)
    {
        Parallel.For(0, height, row =>
        {
            var cellRow = row * width;

            for (var x = 0; x < width; x++)
            {
                var cell = map.Cells[cellRow + x];
                var isBorder = false;

                // 经度方向环绕：左右两侧互为邻居。
                var left = x == 0 ? map.Cells[cellRow + width - 1] : map.Cells[cellRow + x - 1];
                var right = x == width - 1 ? map.Cells[cellRow] : map.Cells[cellRow + x + 1];
                if (left != cell || right != cell)
                {
                    isBorder = true;
                }
                else if (row > 0 && map.Cells[cellRow - width + x] != cell)
                {
                    isBorder = true;
                }
                else if (row < height - 1 && map.Cells[cellRow + width + x] != cell)
                {
                    isBorder = true;
                }

                if (!isBorder)
                {
                    continue;
                }

                var target = ((cellRow + x) * 4);
                buffer[target] = red;
                buffer[target + 1] = green;
                buffer[target + 2] = blue;
                buffer[target + 3] = alpha;
            }
        });
    }
}
