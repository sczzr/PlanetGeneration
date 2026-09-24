using System;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Generation;

/// <summary>
/// 基础连续场与资源到地块属性列的采样适配器。
/// 严格保证坐标映射正确，小地块零覆盖时采用质心插值回退。
/// </summary>
public static class CellFieldSampler
{
    public static void SampleAll(
        CellGeometry geometry,
        PolygonCellMap cellMap,
        BaseContinuousFields fields,
        CellFields output)
    {
        var count = geometry.Count;
        var width = fields.Width;
        var height = fields.Height;

        SampleContinuous(geometry, cellMap, fields.Elevation, output.Height);
        SampleContinuous(geometry, cellMap, fields.Temperature, output.Temperature);
        SampleContinuous(geometry, cellMap, fields.Moisture, output.Moisture);
        SampleContinuousVector(geometry, cellMap, fields.Wind, output.WindX, output.WindY);

        SampleDiscrete(geometry, fields.Plates.PlateIds, output.PlateId, width, height);
        SampleDiscrete(geometry, fields.Plates.BoundaryTypes, output.PlateBoundary, width, height);
        SampleDiscrete(geometry, fields.Rock, output.Rock, width, height);
        SampleDiscrete(geometry, fields.Ore, output.Ore, width, height);

        if (fields.IndustrialOre != null)
        {
            SampleDiscrete(geometry, fields.IndustrialOre, output.IndustrialOre, width, height);
        }
        if (fields.SupernaturalOre != null)
        {
            SampleDiscrete(geometry, fields.SupernaturalOre, output.SupernaturalOre, width, height);
        }
        if (fields.CardOre != null)
        {
            SampleDiscrete(geometry, fields.CardOre, output.CardOre, width, height);
        }
        if (fields.Leyline != null)
        {
            SampleDiscrete(geometry, fields.Leyline, output.Leyline, width, height);
        }
    }

    /// <summary>连续量面积加权采样，零像素覆盖时质心双线性插值回退。</summary>
    public static void SampleContinuous(
        CellGeometry geometry,
        PolygonCellMap cellMap,
        float[,] source,
        float[] output)
    {
        var count = geometry.Count;
        var width = cellMap.Width;
        var height = cellMap.Height;

        var accum = new double[count];
        var weights = new int[count];

        for (var y = 0; y < height; y++)
        {
            var rowStart = y * width;
            for (var x = 0; x < width; x++)
            {
                var cell = cellMap.Cells[rowStart + x];
                if (cell >= 0 && cell < count)
                {
                    accum[cell] += source[x, y];
                    weights[cell]++;
                }
            }
        }

        var scaleX = (double)width / geometry.Width;
        var scaleY = (double)height / geometry.Height;

        for (var cell = 0; cell < count; cell++)
        {
            if (weights[cell] > 0)
            {
                output[cell] = (float)(accum[cell] / weights[cell]);
            }
            else
            {
                // 质心插值兜底，避免极小地块写零
                var cx = geometry.CentroidX[cell] * scaleX;
                var cy = geometry.CentroidY[cell] * scaleY;
                output[cell] = SampleBilinear(source, width, height, cx, cy);
            }
        }
    }

    /// <summary>离散属性质心最近点采样。</summary>
    public static void SampleDiscrete<T>(
        CellGeometry geometry,
        T[,] source,
        T[] output,
        int sourceWidth,
        int sourceHeight)
    {
        var count = geometry.Count;
        var scaleX = (double)sourceWidth / geometry.Width;
        var scaleY = (double)sourceHeight / geometry.Height;

        for (var cell = 0; cell < count; cell++)
        {
            var cx = geometry.CentroidX[cell] * scaleX;
            var cy = geometry.CentroidY[cell] * scaleY;

            var px = Math.Clamp((int)Math.Floor(cx), 0, sourceWidth - 1);
            var py = Math.Clamp((int)Math.Floor(cy), 0, sourceHeight - 1);
            output[cell] = source[px, py];
        }
    }

    public static void SampleDiscrete(
        CellGeometry geometry,
        PlateBoundaryType[,] source,
        byte[] output,
        int sourceWidth,
        int sourceHeight)
    {
        var count = geometry.Count;
        var scaleX = (double)sourceWidth / geometry.Width;
        var scaleY = (double)sourceHeight / geometry.Height;

        for (var cell = 0; cell < count; cell++)
        {
            var cx = geometry.CentroidX[cell] * scaleX;
            var cy = geometry.CentroidY[cell] * scaleY;

            var px = Math.Clamp((int)Math.Floor(cx), 0, sourceWidth - 1);
            var py = Math.Clamp((int)Math.Floor(cy), 0, sourceHeight - 1);
            output[cell] = (byte)source[px, py];
        }
    }

    private static float SampleBilinear(float[,] source, int width, int height, double x, double y)
    {
        var x0 = Math.Clamp((int)Math.Floor(x), 0, width - 1);
        var y0 = Math.Clamp((int)Math.Floor(y), 0, height - 1);
        var x1 = Math.Clamp(x0 + 1, 0, width - 1);
        var y1 = Math.Clamp(y0 + 1, 0, height - 1);

        var fx = (float)(x - x0);
        var fy = (float)(y - y0);

        var top = (source[x0, y0] * (1f - fx)) + (source[x1, y0] * fx);
        var bottom = (source[x0, y1] * (1f - fx)) + (source[x1, y1] * fx);
        return (top * (1f - fy)) + (bottom * fy);
    }

    /// <summary>连续矢量场面积加权采样，零像素覆盖时质心双线性插值回退。</summary>
    public static void SampleContinuousVector(
        CellGeometry geometry,
        PolygonCellMap cellMap,
        (float X, float Y)[,] source,
        float[] outputX,
        float[] outputY)
    {
        var count = geometry.Count;
        var width = cellMap.Width;
        var height = cellMap.Height;

        var accumX = new double[count];
        var accumY = new double[count];
        var weights = new int[count];

        for (var y = 0; y < height; y++)
        {
            var rowStart = y * width;
            for (var x = 0; x < width; x++)
            {
                var cell = cellMap.Cells[rowStart + x];
                if (cell >= 0 && cell < count)
                {
                    accumX[cell] += source[x, y].X;
                    accumY[cell] += source[x, y].Y;
                    weights[cell]++;
                }
            }
        }

        var scaleX = (double)width / geometry.Width;
        var scaleY = (double)height / geometry.Height;

        for (var cell = 0; cell < count; cell++)
        {
            if (weights[cell] > 0)
            {
                outputX[cell] = (float)(accumX[cell] / weights[cell]);
                outputY[cell] = (float)(accumY[cell] / weights[cell]);
            }
            else
            {
                var cx = geometry.CentroidX[cell] * scaleX;
                var cy = geometry.CentroidY[cell] * scaleY;
                var (vx, vy) = SampleBilinearVector(source, width, height, cx, cy);
                outputX[cell] = vx;
                outputY[cell] = vy;
            }
        }
    }

    private static (float X, float Y) SampleBilinearVector(
        (float X, float Y)[,] source,
        int width,
        int height,
        double x,
        double y)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var tx = (float)(x - x0);
        var ty = (float)(y - y0);

        var x0w = ((x0 % width) + width) % width;
        var x1w = (((x0 + 1) % width) + width) % width;
        var y0c = Math.Clamp(y0, 0, height - 1);
        var y1c = Math.Clamp(y0 + 1, 0, height - 1);

        var v00 = source[x0w, y0c];
        var v10 = source[x1w, y0c];
        var v01 = source[x0w, y1c];
        var v11 = source[x1w, y1c];

        var topX = (v00.X * (1f - tx)) + (v10.X * tx);
        var bottomX = (v01.X * (1f - tx)) + (v11.X * tx);
        var topY = (v00.Y * (1f - tx)) + (v10.Y * tx);
        var bottomY = (v01.Y * (1f - tx)) + (v11.Y * tx);

        return ((topX * (1f - ty)) + (bottomX * ty), (topY * (1f - ty)) + (bottomY * ty));
    }
}
