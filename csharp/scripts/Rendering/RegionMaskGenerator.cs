using Godot;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace PlanetGeneration.Rendering;

/// <summary>
/// 区域遮罩与平滑距离场数据。
/// </summary>
public sealed class RegionMaskData
{
    public required int RegionId { get; init; }
    public required Rect2 WorldRect { get; init; }
    public required Texture2D MaskTexture { get; init; }
    public required Vector2 Resolution { get; init; }
}

/// <summary>
/// 大型宏观地貌平滑遮罩生成器 (RegionMaskGenerator)
/// 
/// 负责将 MegaTerrainRegion 的连通 Cell 几何与平滑轮廓，
/// 烘焙为带有亚像素边缘距离场与羽化过渡的局部 Alpha/SDF 遮罩纹理，
/// 供 CanvasItem Shader 实现“AI 宏观材质按程序地理轮廓精准裁切”。
/// </summary>
public static class RegionMaskGenerator
{
    private static long _cachedSnapshotId = -1;
    private static readonly Dictionary<int, RegionMaskData> _cache = new();

    /// <summary>
    /// 获取或烘焙指定宏观地貌区域的局部平滑遮罩纹理。
    /// </summary>
    public static RegionMaskData GetOrCreateMask(MegaTerrainRegion region, long snapshotId, int targetResolution = 192)
    {
        if (_cachedSnapshotId != snapshotId)
        {
            _cache.Clear();
            _cachedSnapshotId = snapshotId;
        }

        if (_cache.TryGetValue(region.Id, out var existing))
        {
            return existing;
        }

        var mask = BakeRegionMask(region, targetResolution);
        _cache[region.Id] = mask;
        return mask;
    }

    /// <summary>
    /// 清除所有遮罩缓存。
    /// </summary>
    public static void ClearCache()
    {
        _cache.Clear();
        _cachedSnapshotId = -1;
    }

    private static RegionMaskData BakeRegionMask(MegaTerrainRegion region, int res)
    {
        var bMin = region.BoundsMin;
        var bMax = region.BoundsMax;

        // 留出 15% 或至少 24 单位的羽化安全边距 (Padding)
        var padX = Math.Max((bMax.X - bMin.X) * 0.15, 24.0);
        var padY = Math.Max((bMax.Y - bMin.Y) * 0.15, 24.0);

        var worldMinX = (float)(bMin.X - padX);
        var worldMinY = (float)(bMin.Y - padY);
        var worldMaxX = (float)(bMax.X + padX);
        var worldMaxY = (float)(bMax.Y + padY);

        var worldWidth = worldMaxX - worldMinX;
        var worldHeight = worldMaxY - worldMinY;

        if (worldWidth <= 1f) worldWidth = 1f;
        if (worldHeight <= 1f) worldHeight = 1f;

        // 保持宽高比适配分辨率
        var aspect = worldWidth / worldHeight;
        int resX, resY;
        if (aspect >= 1.0f)
        {
            resX = res;
            resY = Math.Max(16, (int)Math.Round(res / aspect));
        }
        else
        {
            resY = res;
            resX = Math.Max(16, (int)Math.Round(res * aspect));
        }

        var img = Image.CreateEmpty(resX, resY, false, Image.Format.Rgba8);
        var polys = region.SmoothedPolygons;

        var stepX = worldWidth / resX;
        var stepY = worldHeight / resY;

        for (var py = 0; py < resY; py++)
        {
            var worldY = worldMinY + (py + 0.5f) * stepY;
            for (var px = 0; px < resX; px++)
            {
                var worldX = worldMinX + (px + 0.5f) * stepX;
                var pt = new PolyVec2(worldX, worldY);

                // 判定是否在任一平滑环内
                var inside = false;
                if (polys.Count > 0)
                {
                    for (var i = 0; i < polys.Count; i++)
                    {
                        if (IsPointInPolygon(pt, polys[i]))
                        {
                            inside = true;
                            break;
                        }
                    }
                }
                else
                {
                    // 容错：若无平滑多边形，基于中心与半径做椭圆软衰减
                    var d = pt.DistanceTo(region.Centroid);
                    var maxR = Math.Max(worldWidth, worldHeight) * 0.4;
                    if (d < maxR) inside = true;
                }

                if (!inside)
                {
                    // 区域外部必须严格全透明，确保无黑边黑框
                    img.SetPixel(px, py, new Color(0f, 0f, 0f, 0f));
                }
                else
                {
                    // 采样连续归一化相对深度 [0.0 ~ 1.0]
                    var depth = region.ComputeNormalizedDepth(pt);
                    // 边缘平滑羽化 (0.0 ~ 0.12 之间平滑过渡至 1.0)
                    var alpha = Math.Clamp(depth / 0.12f, 0.0f, 1.0f);
                    // RGB 必须保持纯白 (1, 1, 1)，Alpha 控制羽化，严禁降低 RGB 造成边缘死黑
                    img.SetPixel(px, py, new Color(1.0f, 1.0f, 1.0f, alpha));
                }
            }
        }

        var tex = ImageTexture.CreateFromImage(img);

        return new RegionMaskData
        {
            RegionId = region.Id,
            WorldRect = new Rect2(worldMinX, worldMinY, worldWidth, worldHeight),
            MaskTexture = tex,
            Resolution = new Vector2(resX, resY)
        };
    }

    private static bool IsPointInPolygon(PolyVec2 pt, PolyVec2[] poly)
    {
        if (poly.Length < 3) return false;
        var inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if (((poly[i].Y > pt.Y) != (poly[j].Y > pt.Y)) &&
                (pt.X < (poly[j].X - poly[i].X) * (pt.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
            {
                inside = !inside;
            }
        }
        return inside;
    }
}
