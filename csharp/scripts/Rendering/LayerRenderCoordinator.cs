using Godot;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;
using PlanetGeneration.Core.Layers;
using System;
using System.Collections.Generic;
using System.IO;

namespace PlanetGeneration.Rendering;

/// <summary>
/// 图层渲染协调器：
/// 管理 13 种基础底图主题与 8 种叠加图层的合成、预设切换与原生多分辨率导出（1K / 2K / 4K）。
/// </summary>
public sealed class LayerRenderCoordinator
{
    public LayerStackState StackState { get; } = new();

    // 缓存归属图以加速同分辨率下的换色
    private PolygonCellMap? _cachedCellMap;
    private CellGeometry? _cachedCellMapGeometry;
    private int _cachedCellMapWidth;
    private int _cachedCellMapHeight;

    // 板块边界折线只依赖几何与板块场，逐次重生成会把每次换色拖成秒级
    private PlateBoundarySegment[]? _cachedPlateSegments;
    private CellGeometry? _cachedPlateSegmentsGeometry;
    private CellFields? _cachedPlateSegmentsFields;

    public LayerRenderCoordinator()
    {
    }

    /// <summary>
    /// 获取或构建当前分辨率下的地块归属图。
    /// </summary>
    public PolygonCellMap GetOrCreateCellMap(CellGeometry geometry, int targetWidth, int targetHeight)
    {
        if (_cachedCellMap != null &&
            ReferenceEquals(_cachedCellMapGeometry, geometry) &&
            _cachedCellMapWidth == targetWidth &&
            _cachedCellMapHeight == targetHeight)
        {
            return _cachedCellMap;
        }

        var cellMap = PolygonRasterizer.BuildCellMap(geometry, targetWidth, targetHeight);
        _cachedCellMap = cellMap;
        _cachedCellMapGeometry = geometry;
        _cachedCellMapWidth = targetWidth;
        _cachedCellMapHeight = targetHeight;
        return cellMap;
    }

    /// <summary>
    /// 渲染基础底图为 Image（尺寸与 cellMap 一致）。
    /// </summary>
    public Image RenderBaseThemeImage(WorldSnapshot snapshot, int width, int height)
    {
        var geom = snapshot.Geometry;
        var cellMap = GetOrCreateCellMap(geom, width, height);
        var activeTheme = StackState.ActiveBaseThemeId;

        var count = geom.Count;
        var cellRgba = new byte[count * 4];

        for (var i = 0; i < count; i++)
        {
            var color = BaseThemeColorPalette.GetCellColor(snapshot, activeTheme, i);
            var offset = i * 4;
            cellRgba[offset] = (byte)Math.Clamp(Math.Round(color.R * 255f), 0, 255);
            cellRgba[offset + 1] = (byte)Math.Clamp(Math.Round(color.G * 255f), 0, 255);
            cellRgba[offset + 2] = (byte)Math.Clamp(Math.Round(color.B * 255f), 0, 255);
            cellRgba[offset + 3] = 255;
        }

        var rawBytes = PolygonRasterizer.RasterizeCells(cellMap, cellRgba);

        // 如果开启了地块轮廓底图描边
        if (StackState.IsOverlayActive("cell_outlines"))
        {
            var outlineOpacity = StackState.GetOpacity("cell_outlines");
            if (outlineOpacity > 0.05f)
            {
                PolygonRasterizer.DrawCellBorders(geom, rawBytes, width, height, new byte[] { 26, 28, 32, (byte)(outlineOpacity * 255) });
            }
        }

        DrawPlateBoundaryLines(snapshot, rawBytes, width, height);

        return Image.CreateFromData(width, height, false, Image.Format.Rgba8, rawBytes);
    }

    /// <summary>
    /// 光栅路径（小地图底图与 PNG 导出）没有矢量绘制层，
    /// 板块交界在这里按屏幕上同一套折线几何逐像素描出，否则导出的图只剩平涂色块。
    /// </summary>
    private void DrawPlateBoundaryLines(WorldSnapshot snapshot, byte[] buffer, int width, int height)
    {
        var overlayActive = StackState.IsOverlayActive(LayerRegistry.LayerPlateBorders);
        if (!overlayActive && StackState.ActiveBaseThemeId != LayerRegistry.LayerPlates)
        {
            return;
        }

        var opacity = overlayActive ? StackState.GetOpacity(LayerRegistry.LayerPlateBorders) : 0.95f;
        if (opacity <= 0.001f)
        {
            return;
        }

        var segments = GetOrCreatePlateSegments(snapshot);
        if (segments.Length == 0)
        {
            return;
        }

        var geom = snapshot.Geometry;
        var scaleX = width / (double)Math.Max(geom.Width, 1);
        var scaleY = height / (double)Math.Max(geom.Height, 1);

        // 屏幕上的线宽按物理像素恒定，这里以 1K 出图为基准换算，避免小地图被线条糊成一团。
        var unit = Math.Max(1.0, width / 1024.0);
        var coreThickness = Math.Max(1, (int)Math.Round(unit));
        var glowThickness = Math.Max(coreThickness, (int)Math.Round(unit * 2.2));

        var xs = new double[256];
        var ys = new double[256];

        foreach (var seg in segments)
        {
            var pts = seg.Points;
            if (pts.Length < 2) continue;

            if (xs.Length < pts.Length)
            {
                xs = new double[pts.Length];
                ys = new double[pts.Length];
            }

            for (var i = 0; i < pts.Length; i++)
            {
                xs[i] = pts[i].X * scaleX;
                ys[i] = pts[i].Y * scaleY;
            }

            if (seg.IsUniformColor)
            {
                StrokePolylineBoth(buffer, width, height, xs, ys, pts.Length, seg.Colors[0], opacity, coreThickness, glowThickness);
                continue;
            }

            // 渐变段按屏幕上 DrawPolylineColors 的语义逐子段取色
            for (var i = 0; i < pts.Length - 1; i++)
            {
                if (Math.Abs(xs[i + 1] - xs[i]) > width * 0.5) continue;

                var c = seg.Colors[i];
                var cr = ToChannelByte(c.R);
                var cg = ToChannelByte(c.G);
                var cb = ToChannelByte(c.B);
                PolygonRasterizer.StrokeSegment(buffer, width, height, xs[i], ys[i], xs[i + 1], ys[i + 1], cr, cg, cb, opacity * 0.32f, glowThickness);
                PolygonRasterizer.StrokeSegment(buffer, width, height, xs[i], ys[i], xs[i + 1], ys[i + 1], cr, cg, cb, opacity * 0.95f, coreThickness);
            }
        }
    }

    private static void StrokePolylineBoth(
        byte[] buffer,
        int width,
        int height,
        double[] xs,
        double[] ys,
        int count,
        Color color,
        float opacity,
        int coreThickness,
        int glowThickness)
    {
        var r = ToChannelByte(color.R);
        var g = ToChannelByte(color.G);
        var b = ToChannelByte(color.B);
        PolygonRasterizer.StrokePolyline(buffer, width, height, xs, ys, count, r, g, b, opacity * 0.32f, glowThickness);
        PolygonRasterizer.StrokePolyline(buffer, width, height, xs, ys, count, r, g, b, opacity * 0.95f, coreThickness);
    }

    private static byte ToChannelByte(float channel)
        => (byte)Math.Clamp(Math.Round(channel * 255f), 0, 255);

    private PlateBoundarySegment[] GetOrCreatePlateSegments(WorldSnapshot snapshot)
    {
        if (_cachedPlateSegments != null && ReferenceEquals(_cachedPlateSegmentsGeometry, snapshot.Geometry)
            && ReferenceEquals(_cachedPlateSegmentsFields, snapshot.Fields))
        {
            return _cachedPlateSegments;
        }

        _cachedPlateSegments = OverlayVectorRenderer.BuildPlateBoundaries(snapshot, null);
        _cachedPlateSegmentsGeometry = snapshot.Geometry;
        _cachedPlateSegmentsFields = snapshot.Fields;
        return _cachedPlateSegments;
    }

    /// <summary>
    /// 导出包含底图与叠加层的完整 PNG 到指定文件。
    /// </summary>
    public void ExportPng(WorldSnapshot snapshot, string outputPath, int targetWidth, int targetHeight)
    {
        var image = RenderBaseThemeImage(snapshot, targetWidth, targetHeight);
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var error = image.SavePng(outputPath);
        if (error != Error.Ok)
        {
            throw new IOException($"保存 PNG 失败: {error}");
        }
    }
}
