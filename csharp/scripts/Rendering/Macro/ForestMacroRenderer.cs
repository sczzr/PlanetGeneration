using Godot;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;
using System;
using System.Collections.Generic;

namespace PlanetGeneration.Rendering.Macro;

/// <summary>
/// 宏观森林渲染元素。
/// </summary>
public sealed class ForestMacroElement
{
    public required int RegionId { get; init; }
    public required Rect2 WorldRect { get; init; }
    public required Texture2D MacroTexture { get; init; }
    public required RegionMaskData MaskData { get; init; }
    public Color Tint { get; init; } = Colors.White;
    public float DepthBias { get; init; } = 1.0f;
}

/// <summary>
/// 大型宏观森林专用渲染器 (ForestMacroRenderer)
/// 
/// 遵循“区域遮罩 (Region Mask) + 宏观水墨林冠贴图 (Macro Cluster) + 边缘疏朗散木”架构：
/// 彻底废弃成百上千小树散点，将巨型森林表现为具有整体呼吸感的连绵水墨林海。
/// </summary>
public static class ForestMacroRenderer
{
    private static Shader? _maskShader;
    private static ShaderMaterial? _sharedMaterial;

    private static Texture2D? _texMacroForestBroadleaf;
    private static Texture2D? _texMacroForestPine;
    private static Texture2D? _texMacroForestBamboo;

    private static bool _texturesInitialized;

    public static void EnsureInitialized()
    {
        if (_texturesInitialized) return;
        _texturesInitialized = true;

        // 尝试加载用户或系统提供的 2048x2048 宏观森林图
        _texMacroForestBroadleaf = LoadMacroTexture("forest_macro_01.png") 
            ?? CreateDefaultMacroForestTexture(new Color(0.18f, 0.36f, 0.28f, 0.92f));

        _texMacroForestPine = LoadMacroTexture("forest_pine_macro_01.png")
            ?? CreateDefaultMacroForestTexture(new Color(0.14f, 0.28f, 0.24f, 0.95f));

        _texMacroForestBamboo = LoadMacroTexture("forest_bamboo_macro_01.png")
            ?? CreateDefaultMacroForestTexture(new Color(0.25f, 0.45f, 0.30f, 0.88f));

        var shaderPath = "res://resources/shaders/terrain_macro_mask.gdshader";
        if (ResourceLoader.Exists(shaderPath))
        {
            _maskShader = ResourceLoader.Load<Shader>(shaderPath);
            if (_maskShader != null)
            {
                _sharedMaterial = new ShaderMaterial { Shader = _maskShader };
            }
        }
    }

    /// <summary>
    /// 为指定快照中的全部 MegaForest 构建宏观森林元素集合。
    /// </summary>
    public static List<ForestMacroElement> BuildForestElements(WorldSnapshot snapshot)
    {
        EnsureInitialized();
        var elements = new List<ForestMacroElement>();
        var megaForests = snapshot.MegaRegions;
        if (megaForests == null || megaForests.Count == 0) return elements;

        var rand = new Random((int)(snapshot.Options.Seed ^ 0x464f52)); // "FOR"

        foreach (var r in megaForests)
        {
            if (r.Type != MegaTerrainType.MegaForest) continue;
            if (r.Cells.Length < 3) continue;

            // 获取该森林的平滑边缘与归一化深度 Mask
            var maskData = RegionMaskGenerator.GetOrCreateMask(r, snapshot.SnapshotId, 256);

            // 根据平均气候与名字特征选择宏观水墨贴图
            var tex = _texMacroForestBroadleaf!;
            if (r.Name.Contains("竹") || r.Name.Contains("篁"))
            {
                tex = _texMacroForestBamboo!;
            }
            else if (r.Name.Contains("雪") || r.Name.Contains("夜") || r.ElevationMean > snapshot.Options.SeaLevel + 0.35f)
            {
                tex = _texMacroForestPine!;
            }

            // 依据森林评级赋予温润青绿水墨淡彩色调（与宣纸地子自然相溶）
            var tint = r.Rank switch
            {
                MegaRegionRank.WorldLandmark => new Color(0.28f, 0.52f, 0.38f, 0.45f),
                MegaRegionRank.MegaRegion => new Color(0.32f, 0.56f, 0.42f, 0.40f),
                _ => new Color(0.36f, 0.60f, 0.46f, 0.35f)
            };

            elements.Add(new ForestMacroElement
            {
                RegionId = r.Id,
                WorldRect = maskData.WorldRect,
                MacroTexture = tex,
                MaskData = maskData,
                Tint = tint,
                DepthBias = 1.0f
            });
        }

        return elements;
    }

    /// <summary>
    /// 在 CanvasItem 上绘制宏观森林层。
    /// </summary>
    public static void Draw(
        CanvasItem item,
        IReadOnlyList<ForestMacroElement> forests,
        Rect2 visibleRect,
        float screenScale)
    {
        if (forests == null || forests.Count == 0) return;

        foreach (var f in forests)
        {
            if (!visibleRect.Intersects(f.WorldRect)) continue;

            // 绘制带 Mask 裁切与平滑羽化的宏观水墨林冠
            // 采用 2D 多边形三角网格或带 Mask 的 DrawMesh
            DrawMaskedForest(item, f);
        }
    }

    private static bool _hasRealMacroTexture;

    private static void DrawMaskedForest(CanvasItem item, ForestMacroElement f)
    {
        // 仅当外部真正配置了 2048x2048 高清水墨林冠原画时才绘制覆盖层；
        // 默认状态下依托 MapCanvas 的高保真青瓷苔绿地晕，避免在宣纸上产生发灰或发白的遮罩方块。
        if (!_hasRealMacroTexture || _sharedMaterial == null)
        {
            return;
        }

        var rect = f.WorldRect;
        var maskTex = f.MaskData.MaskTexture;

        var p0 = rect.Position;
        var p1 = new Vector2(rect.End.X, rect.Position.Y);
        var p2 = rect.End;
        var p3 = new Vector2(rect.Position.X, rect.End.Y);

        var points = new[] { p0, p1, p2, p3 };
        var colors = new[] { f.Tint, f.Tint, f.Tint, f.Tint };
        var uvs = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };

        item.DrawPolygon(points, colors, uvs, maskTex);
    }

    private static Texture2D? LoadMacroTexture(string filename)
    {
        var resPath = $"res://resources/textures/guohua/{filename}";
        try
        {
            if (ResourceLoader.Exists(resPath))
            {
                return ResourceLoader.Load<Texture2D>(resPath);
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 当外部 2048x2048 AI 宏观林冠图尚未置入时，
    /// 自动生成具有国画青绿水墨淡彩质感的高清平滑林冠基底贴图（占位保障开箱即用）。
    /// </summary>
    private static Texture2D CreateDefaultMacroForestTexture(Color baseInk)
    {
        const int size = 256;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var rand = new Random(0x4d616372); // "Macr"

        for (var y = 0; y < size; y++)
        {
            var ny = (y - size * 0.5f) / (size * 0.5f);
            for (var x = 0; x < size; x++)
            {
                var nx = (x - size * 0.5f) / (size * 0.5f);
                var dist = MathF.Sqrt(nx * nx + ny * ny);

                if (dist < 0.95f)
                {
                    // 模拟国画水墨晕染淡彩与松针细密聚散
                    var noise = MathF.Sin(nx * 12f) * MathF.Cos(ny * 12f) * 0.18f
                              + MathF.Sin(nx * 26f + ny * 18f) * 0.12f
                              + (rand.NextSingle() - 0.5f) * 0.08f;

                    var c = baseInk;
                    c.R = Math.Clamp(c.R + noise * 0.5f, 0.05f, 0.95f);
                    c.G = Math.Clamp(c.G + noise * 0.7f, 0.08f, 0.98f);
                    c.B = Math.Clamp(c.B + noise * 0.4f, 0.05f, 0.90f);

                    var edgeFade = Math.Clamp((0.95f - dist) / 0.25f, 0f, 1f);
                    c.A *= edgeFade;

                    img.SetPixel(x, y, c);
                }
                else
                {
                    img.SetPixel(x, y, new Color(0, 0, 0, 0));
                }
            }
        }

        return ImageTexture.CreateFromImage(img);
    }
}
