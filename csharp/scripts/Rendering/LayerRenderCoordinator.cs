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
    private int _cachedCellMapTargetCells;
    private int _cachedCellMapWidth;
    private int _cachedCellMapHeight;

    public LayerRenderCoordinator()
    {
    }

    /// <summary>
    /// 获取或构建当前分辨率下的地块归属图。
    /// </summary>
    public PolygonCellMap GetOrCreateCellMap(CellGeometry geometry, int targetWidth, int targetHeight)
    {
        if (_cachedCellMap != null &&
            _cachedCellMapTargetCells == geometry.Count &&
            _cachedCellMapWidth == targetWidth &&
            _cachedCellMapHeight == targetHeight)
        {
            return _cachedCellMap;
        }

        var cellMap = PolygonRasterizer.BuildCellMap(geometry, targetWidth, targetHeight);
        _cachedCellMap = cellMap;
        _cachedCellMapTargetCells = geometry.Count;
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

        return Image.CreateFromData(width, height, false, Image.Format.Rgba8, rawBytes);
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
