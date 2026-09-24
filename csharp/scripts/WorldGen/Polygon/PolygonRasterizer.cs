using System;
using System.Threading.Tasks;
using PolygonCellMap = PlanetGeneration.Core.Geometry.PolygonCellMap;
using CoreRasterizer = PlanetGeneration.Core.Geometry.PolygonRasterizer;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>
/// 栅格兼容绘制入口。归属图、扫描线和颜色填充统一由 Core 提供；
/// 四邻域像素描边保留旧外观，不替换为 Core 的几何线段描边。
/// </summary>
public static class PolygonRasterizer
{
    public static PolygonCellMap BuildCellMap(PolygonGrid grid, int width, int height)
        => CoreRasterizer.BuildCellMap(grid.Geometry, width, height);

    public static void FillRgba(byte[] buffer, int width, int height, PolygonCellMap map, byte[] cellRgba)
    {
        if (width != map.Width || height != map.Height)
            throw new ArgumentException("归属图尺寸与输出尺寸不一致。", nameof(width));
        if (cellRgba.Length < map.CellCount * 4)
            throw new ArgumentException("颜色表过小。", nameof(cellRgba));
        CoreRasterizer.FillRgba(map, cellRgba, buffer);
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
