using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Domain;

namespace PlanetGeneration.Core.Geometry;

/// <summary>
/// 像素归属图：记录任意指定分辨率下每个像素属于哪个地块。
/// </summary>
public sealed class PolygonCellMap
{
    public int Width { get; }
    public int Height { get; }
    public int CellCount { get; }
    public int[] Cells { get; }
    public int[] PixelCounts { get; }
    public int UnassignedPixels { get; }

    internal PolygonCellMap(int width, int height, int cellCount, int[] cells, int[] pixelCounts, int unassignedPixels)
    {
        Width = width;
        Height = height;
        CellCount = cellCount;
        Cells = cells;
        PixelCounts = pixelCounts;
        UnassignedPixels = unassignedPixels;
    }

    public int CellAt(int x, int y) => Cells[(y * Width) + x];
}

/// <summary>
/// 纯 C# 多边形光栅化与离线投影器（零引擎依赖）。
/// 支持将任意尺寸的 CellGeometry 投影到指定宽高（如 1024×512、2048×1024、4096×2048）的像素图。
/// </summary>
public static class PolygonRasterizer
{
    public static PolygonCellMap BuildCellMap(CellGeometry geometry, int targetWidth, int targetHeight)
    {
        if (targetWidth <= 0 || targetHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetWidth), "目标栅格尺寸必须为正数。");
        }

        var cellCount = geometry.Count;
        var cells = new int[targetWidth * targetHeight];
        var pixelCounts = new int[cellCount];

        for (var i = 0; i < cells.Length; i++) cells[i] = -1;

        var scaleX = targetWidth / geometry.Width;
        var scaleY = targetHeight / geometry.Height;

        for (var cell = 0; cell < cellCount; cell++)
        {
            FillCell(geometry, cell, targetWidth, targetHeight, scaleX, scaleY, cells, pixelCounts);
        }

        // 极少数因取整漏掉的像素由最近站点拾取补齐
        var unassigned = 0;
        for (var idx = 0; idx < cells.Length; idx++)
        {
            if (cells[idx] >= 0) continue;

            unassigned++;
            var px = idx % targetWidth;
            var py = idx / targetWidth;
            var wx = (px + 0.5) / scaleX;
            var wy = (py + 0.5) / scaleY;

            var assigned = geometry.FindCell(wx, wy);
            cells[idx] = assigned;
            pixelCounts[assigned]++;
        }

        return new PolygonCellMap(targetWidth, targetHeight, cellCount, cells, pixelCounts, unassigned);
    }

    private static void FillCell(
        CellGeometry geometry,
        int cell,
        int targetWidth,
        int targetHeight,
        double scaleX,
        double scaleY,
        int[] cells,
        int[] pixelCounts)
    {
        var poly = geometry.GetPolygon(cell);
        if (poly.Length < 3) return;

        var referenceX = geometry.CentroidX[cell];
        var localPoly = new PolyVec2[poly.Length];
        var worldWidth = geometry.Width;

        for (var i = 0; i < poly.Length; i++)
        {
            var vx = poly[i].X;
            vx -= Math.Round((vx - referenceX) / worldWidth) * worldWidth;
            localPoly[i] = new PolyVec2(vx, poly[i].Y);
        }

        // 针对经度缝，至多展开 3 份（0, -W, +W）
        for (var shift = -1; shift <= 1; shift++)
        {
            var offsetX = shift * worldWidth;
            FillSinglePolygon(localPoly, offsetX, targetWidth, targetHeight, scaleX, scaleY, cell, cells, pixelCounts);
        }
    }

    private static void FillSinglePolygon(
        PolyVec2[] localPoly,
        double offsetX,
        int targetWidth,
        int targetHeight,
        double scaleX,
        double scaleY,
        int cell,
        int[] cells,
        int[] pixelCounts)
    {
        var n = localPoly.Length;
        var minY = double.MaxValue;
        var maxY = double.MinValue;

        var pixelPoly = new PolyVec2[n];
        for (var i = 0; i < n; i++)
        {
            var px = (localPoly[i].X + offsetX) * scaleX;
            var py = localPoly[i].Y * scaleY;
            pixelPoly[i] = new PolyVec2(px, py);

            if (py < minY) minY = py;
            if (py > maxY) maxY = py;
        }

        var startY = Math.Max(0, (int)Math.Floor(minY));
        var endY = Math.Min(targetHeight - 1, (int)Math.Ceiling(maxY));

        var xIntersections = new List<double>(8);

        for (var y = startY; y <= endY; y++)
        {
            var scanY = y + 0.5;
            xIntersections.Clear();

            for (var i = 0; i < n; i++)
            {
                var p1 = pixelPoly[i];
                var p2 = pixelPoly[(i + 1) % n];

                if ((p1.Y <= scanY && p2.Y > scanY) || (p2.Y <= scanY && p1.Y > scanY))
                {
                    var t = (scanY - p1.Y) / (p2.Y - p1.Y);
                    var x = p1.X + (t * (p2.X - p1.X));
                    xIntersections.Add(x);
                }
            }

            if (xIntersections.Count < 2) continue;
            xIntersections.Sort();

            for (var i = 0; i < xIntersections.Count - 1; i += 2)
            {
                var x0 = Math.Max(0, (int)Math.Ceiling(xIntersections[i] - 0.5));
                var x1 = Math.Min(targetWidth - 1, (int)Math.Floor(xIntersections[i + 1] - 0.5));

                for (var x = x0; x <= x1; x++)
                {
                    var idx = (y * targetWidth) + x;
                    if (cells[idx] < 0)
                    {
                        cells[idx] = cell;
                        pixelCounts[cell]++;
                    }
                }
            }
        }
    }

    /// <summary>地块连续属性投影到二维栅格数组。</summary>
    public static void SplatContinuous(PolygonCellMap cellMap, float[] cellValues, float[,] targetRaster)
    {
        var width = cellMap.Width;
        var height = cellMap.Height;
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * width;
            for (var x = 0; x < width; x++)
            {
                var cell = cellMap.Cells[rowStart + x];
                targetRaster[x, y] = cell >= 0 && cell < cellValues.Length ? cellValues[cell] : 0f;
            }
        }
    }

    /// <summary>地块离散类型属性投影到二维栅格数组。</summary>
    public static void SplatDiscrete(PolygonCellMap cellMap, byte[] cellValues, byte[,] targetRaster)
    {
        var width = cellMap.Width;
        var height = cellMap.Height;
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * width;
            for (var x = 0; x < width; x++)
            {
                var cell = cellMap.Cells[rowStart + x];
                targetRaster[x, y] = cell >= 0 && cell < cellValues.Length ? cellValues[cell] : (byte)0;
            }
        }
    }

    /// <summary>将每个地块的 RGBA 颜色光栅化为完整的图像字节数组。</summary>
    public static byte[] RasterizeCells(PolygonCellMap cellMap, byte[] cellRgba)
    {
        var width = cellMap.Width;
        var height = cellMap.Height;
        var totalPixels = width * height;
        var buffer = new byte[totalPixels * 4];

        for (var idx = 0; idx < totalPixels; idx++)
        {
            var cell = cellMap.Cells[idx];
            var src = cell * 4;
            var dst = idx * 4;
            if (cell >= 0 && src + 3 < cellRgba.Length)
            {
                buffer[dst] = cellRgba[src];
                buffer[dst + 1] = cellRgba[src + 1];
                buffer[dst + 2] = cellRgba[src + 2];
                buffer[dst + 3] = cellRgba[src + 3];
            }
        }

        return buffer;
    }

    /// <summary>在像素缓冲中绘制地块边界线。</summary>
    public static void DrawCellBorders(CellGeometry geometry, byte[] imageRgba, int width, int height, byte[] borderRgba)
    {
        var scaleX = width / geometry.Width;
        var scaleY = height / geometry.Height;

        for (var i = 0; i < geometry.Count; i++)
        {
            var vCount = geometry.GetVertexCount(i);
            if (vCount < 3) continue;

            var start = geometry.CellVertexStart[i];
            for (var k = 0; k < vCount; k++)
            {
                var nextK = (k + 1) % vCount;
                var x0 = (int)Math.Round(geometry.VertexX[start + k] * scaleX);
                var y0 = (int)Math.Round(geometry.VertexY[start + k] * scaleY);
                var x1 = (int)Math.Round(geometry.VertexX[start + nextK] * scaleX);
                var y1 = (int)Math.Round(geometry.VertexY[start + nextK] * scaleY);

                if (Math.Abs(x0 - x1) > width * 0.5) continue;
                DrawLineBresenham(imageRgba, width, height, x0, y0, x1, y1, borderRgba);
            }
        }
    }

    private static void DrawLineBresenham(byte[] buffer, int width, int height, int x0, int y0, int x1, int y1, byte[] color)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;

        while (true)
        {
            if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height)
            {
                var offset = ((y0 * width) + x0) * 4;
                buffer[offset] = color[0];
                buffer[offset + 1] = color[1];
                buffer[offset + 2] = color[2];
                buffer[offset + 3] = color[3];
            }

            if (x0 == x1 && y0 == y1) break;
            var e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }
}
