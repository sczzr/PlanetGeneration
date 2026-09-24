using Godot;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;
using System;
using System.Collections.Generic;
using System.IO;

namespace PlanetGeneration.Rendering.Macro;

/// <summary>
/// 宏观沙漠渲染数据。
/// </summary>
public sealed class DesertMacroElement
{
    public required int RegionId { get; init; }
    public required Rect2 WorldRect { get; init; }
    public required RegionMaskData MaskData { get; init; }
    public Color TintWash { get; init; } = new(0.92f, 0.82f, 0.62f, 0.45f);
    public List<DesertDuneWave> Dunes { get; init; } = new();
}

public sealed class DesertDuneWave
{
    public required Vector2 Position { get; init; }
    public required Vector2 Size { get; init; }
    public required float Rotation { get; init; }
    public Color Color { get; init; }
}

/// <summary>
/// 大型宏观沙漠专用渲染器 (DesertMacroRenderer)
/// 
/// 遵循“区域淡赭水墨水晕基底 (Ochre Wash) + 宏观流线沙丘 (Macro Dune Lines) + 局部绿洲微点缀”架构：
/// 彻底去除散落的小沙堆贴图，呈现大漠孤烟、瀚海无垠的大气中国古画意境。
/// </summary>
public static class DesertMacroRenderer
{
    private static Texture2D? _texDuneAtlas;
    private static bool _initialized;

    // 赭石藤黄大漠水墨色调
    private static readonly Color OchreWash = new(0.88f, 0.76f, 0.54f, 0.38f);
    private static readonly Color DuneInk = new(0.68f, 0.54f, 0.34f, 0.45f);

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        _texDuneAtlas = LoadTexture("terrain_desert_sheet.jpg") ?? LoadTexture("mountain_atlas_single.png");
    }

    /// <summary>
    /// 为指定快照中的全部 MegaDesert 生成宏观大漠水晕与沙丘流线。
    /// </summary>
    public static List<DesertMacroElement> BuildDesertElements(WorldSnapshot snapshot)
    {
        EnsureInitialized();
        var elements = new List<DesertMacroElement>();
        var megaDeserts = snapshot.MegaRegions;
        if (megaDeserts == null || megaDeserts.Count == 0) return elements;

        var rand = new Random((int)(snapshot.Options.Seed ^ 0x444553)); // "DES"
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;

        foreach (var d in megaDeserts)
        {
            if (d.Type != MegaTerrainType.MegaDesert) continue;
            if (d.Cells.Length < 3) continue;

            // 1. 生成大漠平滑遮罩与距离场
            var maskData = RegionMaskGenerator.GetOrCreateMask(d, snapshot.SnapshotId, 192);

            // 2. 根据大漠走向生成 3~8 组宏观沙丘流线
            var dunes = new List<DesertDuneWave>();
            var stepDist = Math.Max(45.0, Math.Sqrt(d.TotalArea / Math.Max(1, d.Cells.Length)) * 1.6);
            var angle = d.MainDirectionAngle + 0.35f; // 沿主风向与地貌走向交角

            for (var cIdx = 0; cIdx < d.Cells.Length; cIdx += Math.Max(1, d.Cells.Length / 12))
            {
                var cell = d.Cells[cIdx];
                var cPos = new Vector2((float)geom.CentroidX[cell], (float)geom.CentroidY[cell]);
                var depth = d.ComputeNormalizedDepth(new PolyVec2(cPos.X, cPos.Y));

                // 仅在大漠较深腹地产生醒目沙垄（depth > 0.25）
                if (depth < 0.25f) continue;

                var duneWidth = (float)(stepDist * (1.2 + rand.NextDouble() * 0.8));
                var duneHeight = duneWidth * 0.32f;
                var offset = new Vector2((rand.NextSingle() - 0.5f) * 12f, (rand.NextSingle() - 0.5f) * 12f);

                dunes.Add(new DesertDuneWave
                {
                    Position = cPos + offset,
                    Size = new Vector2(duneWidth, duneHeight),
                    Rotation = angle + (rand.NextSingle() - 0.5f) * 0.18f,
                    Color = DuneInk
                });
            }

            elements.Add(new DesertMacroElement
            {
                RegionId = d.Id,
                WorldRect = maskData.WorldRect,
                MaskData = maskData,
                TintWash = OchreWash,
                Dunes = dunes
            });
        }

        return elements;
    }

    /// <summary>
    /// 在 CanvasItem 上绘制宏观沙漠基底水晕与沙丘流线。
    /// </summary>
    public static void Draw(
        CanvasItem item,
        IReadOnlyList<DesertMacroElement> deserts,
        Rect2 visibleRect,
        float screenScale)
    {
        if (deserts == null || deserts.Count == 0) return;

        foreach (var d in deserts)
        {
            if (!visibleRect.Intersects(d.WorldRect)) continue;

            // 1. 大漠淡赭藤黄平滑水晕已由底层 MapCanvas 顶点平滑管线完美呈现，无需重复绘制带白底的遮罩矩形
            // 2. 绘制宏观水墨沙丘流线 (Dune Waves)
            var strokeWidth = 1.4f / screenScale;
            foreach (var dune in d.Dunes)
            {
                if (!visibleRect.HasPoint(dune.Position)) continue;

                // 绘制新月形微弧曲线
                var halfW = dune.Size.X * 0.5f;
                var cos = Mathf.Cos(dune.Rotation);
                var sin = Mathf.Sin(dune.Rotation);
                Vector2 Rot(Vector2 p) => new(p.X * cos - p.Y * sin, p.X * sin + p.Y * cos);

                var arcPoints = new Vector2[7];
                for (var k = 0; k < arcPoints.Length; k++)
                {
                    var t = (k / (float)(arcPoints.Length - 1)) * 2.0f - 1.0f; // -1 to +1
                    var localX = t * halfW;
                    var localY = (1.0f - t * t) * (dune.Size.Y * 0.45f); // 抛物线弧度
                    arcPoints[k] = dune.Position + Rot(new Vector2(localX, localY));
                }

                item.DrawPolyline(arcPoints, dune.Color, strokeWidth, true);
            }
        }
    }

    private static Texture2D? LoadTexture(string filename)
    {
        var resPath = $"res://resources/textures/guohua/{filename}";
        var globalPath = ProjectSettings.GlobalizePath(resPath);
        if (!File.Exists(globalPath))
        {
            var fallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"../../../../csharp/resources/textures/guohua/{filename}");
            if (File.Exists(fallback)) globalPath = fallback;
        }

        if (File.Exists(globalPath))
        {
            try
            {
                var img = Image.LoadFromFile(globalPath);
                if (img != null) return ImageTexture.CreateFromImage(img);
            }
            catch { }
        }

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
}
