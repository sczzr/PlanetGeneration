using Godot;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;
using System;
using System.Collections.Generic;
using System.IO;

namespace PlanetGeneration.Rendering.Macro;

/// <summary>
/// 宏观山脉排布元素元数据。
/// </summary>
public sealed class MountainMacroSprite
{
    public required Vector2 Position { get; init; }
    public required Texture2D Texture { get; init; }
    public required Vector2 Size { get; init; }
    public required Vector2 Origin { get; init; }
    public required float YOrder { get; init; }
    public Rect2? SourceRegion { get; init; }
    public float Rotation { get; init; } = 0f;
    public Color Modulate { get; init; } = Colors.White;
}

/// <summary>
/// 大型宏观山脉专用渲染器 (MountainMacroRenderer)
/// 
/// 遵循“脊线样条 (Spline Path) + 连绵群峰模块 (Mountain Cluster) + 地标主峰 + 山麓云雾留白”架构：
/// 告别单点散峰，生成气势磅礴、层峦叠嶂的中国山水大山脉。
/// </summary>
public static class MountainMacroRenderer
{
    private static Texture2D? _texMountainAtlasSingle;
    private static Texture2D? _texMountainAtlasCluster;
    private static Texture2D? _texDefaultPeak;
    private static Texture2D? _texDefaultCluster;
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        _texMountainAtlasSingle = LoadTexture("mountain_atlas_single.png");
        _texMountainAtlasCluster = LoadTexture("mountain_atlas_cluster.png");
        _texDefaultPeak = LoadTexture("mountain_peak.png") ?? CreateDefaultPeakTexture();
        _texDefaultCluster = CreateDefaultClusterTexture();
    }

    private static readonly Dictionary<string, Texture2D> _mountainAtlasCache = new();

    private static Texture2D? GetMountainTexture(TerrainDef? def)
    {
        if (def == null) return _texMountainAtlasCluster ?? _texMountainAtlasSingle;
        if (!string.IsNullOrEmpty(def.AtlasName))
        {
            if (_mountainAtlasCache.TryGetValue(def.AtlasName, out var cached))
                return cached;
            var loaded = LoadTexture(def.AtlasName);
            if (loaded != null)
            {
                _mountainAtlasCache[def.AtlasName] = loaded;
                return loaded;
            }
        }
        return _texMountainAtlasCluster ?? _texMountainAtlasSingle;
    }

    /// <summary>
    /// 为所有 MegaMountain 生成基于样条脊线与大模组排布的山水群峰。
    /// </summary>
    public static List<MountainMacroSprite> BuildMountainSprites(
        WorldSnapshot snapshot,
        float seaLevel,
        List<Vector2> placedCenters)
    {
        EnsureInitialized();
        var result = new List<MountainMacroSprite>();
        var megaMountains = snapshot.MegaRegions;
        if (megaMountains == null || megaMountains.Count == 0) return result;

        var rand = new Random((int)(snapshot.Options.Seed ^ 0x4d4f55)); // "MOU"
        var fields = snapshot.Fields;

        foreach (var m in megaMountains)
        {
            if (m.Type != MegaTerrainType.MegaMountain) continue;
            if (m.Spine == null || m.Spine.Count == 0) continue;

            var spine = m.Spine;
            var rankScale = m.Rank switch
            {
                MegaRegionRank.WorldLandmark => 1.35f,
                MegaRegionRank.MegaRegion => 1.15f,
                _ => 0.95f
            };

            // 1. 寻找山脉最高主峰位置
            var highestElev = float.MinValue;
            var highestIdx = 0;
            for (var i = 0; i < spine.Count; i++)
            {
                if (spine[i].Elevation > highestElev)
                {
                    highestElev = spine[i].Elevation;
                    highestIdx = i;
                }
            }

            // 2. 利用 Catmull-Rom 样条对山脊骨架进行连续平滑高密度插值
            var splinePoints = SampleSpline(spine, 16f);

            // 3. 沿样条线排布连绵群峰模组
            for (var i = 0; i < splinePoints.Count; i++)
            {
                var pt = splinePoints[i];
                var pos = pt.Position;
                var elev = pt.Elevation;
                var isSnow = elev > seaLevel + 0.48f;
                var isHighest = pos.DistanceTo(new Vector2((float)spine[highestIdx].Position.X, (float)spine[highestIdx].Position.Y)) < 36f;

                var scale = (0.90f + Math.Clamp((elev - seaLevel) * 0.7f, 0f, 0.40f)) * rankScale;

                // 挑选群峰图元或地标主峰图元
                var terrainDef = isHighest
                    ? TerrainCatalog.PickMountain(rand, isSnow, false, isMainPeak: true)
                    : (TerrainCatalog.PickRidge(rand, isSnow) 
                       ?? TerrainCatalog.PickMountain(rand, isSnow, false, isMainPeak: false) 
                       ?? TerrainCatalog.PickHills(rand));

                var peakScale = isHighest ? scale * 1.25f : scale; // 地标主峰更显巍峨
                var defWidth = terrainDef?.WorldSize.X ?? 50f;
                var minDist = Mathf.Max(28f * scale, defWidth * peakScale * 0.35f); // 根据模组真实宽度动态计算安全间距

                if (!IsFarFromPlaced(pos, placedCenters, minDist))
                {
                    continue;
                }

                Vector2 mountainSize;

                var mTex = GetMountainTexture(terrainDef);
                if (terrainDef != null && mTex != null)
                {
                    var size = terrainDef.WorldSize * peakScale;
                    mountainSize = size;
                    var origin = new Vector2(size.X * terrainDef.Pivot.X, size.Y * terrainDef.Pivot.Y);
                    var rot = Math.Clamp(pt.TangentAngle * 0.25f, -0.12f, 0.12f);

                    result.Add(new MountainMacroSprite
                    {
                        Position = pos,
                        Texture = mTex,
                        SourceRegion = terrainDef.Region,
                        Size = size,
                        Origin = origin,
                        YOrder = pos.Y,
                        Rotation = rot,
                        Modulate = isSnow ? new Color(0.92f, 0.96f, 1.0f, 1.0f) : Colors.White
                    });
                    placedCenters.Add(pos);
                }
                else
                {
                    // 备用：程序化连绵水墨峰模组
                    var tex = isHighest ? _texDefaultPeak! : _texDefaultCluster!;
                    var size = isHighest ? new Vector2(65f * peakScale, 34f * peakScale) : new Vector2(56f * scale, 28f * scale);
                    mountainSize = size;
                    var origin = new Vector2(size.X * 0.5f, size.Y * 0.88f);

                    result.Add(new MountainMacroSprite
                    {
                        Position = pos,
                        Texture = tex,
                        Size = size,
                        Origin = origin,
                        YOrder = pos.Y,
                        Rotation = 0f,
                        Modulate = Colors.White
                    });
                    placedCenters.Add(pos);
                }

                // 4. 若山脊宽阔，在法线方向布置侧翼副峰（形成三维景深与群山合抱感）
                if (pt.RidgeWidth > 16f && rand.NextSingle() < 0.65f)
                {
                    var flankOffset = (pt.RidgeWidth * 0.45f + 14f) * (rand.NextSingle() > 0.5f ? 1f : -1f);
                    var flankPos = pos + pt.Normal * flankOffset;
                    var flankScale = scale * 0.78f;

                    if (IsFarFromPlaced(flankPos, placedCenters, 28f * flankScale) && IsOnLand(snapshot, flankPos, seaLevel))
                    {
                        var flankDef = TerrainCatalog.PickCompanionPeak(rand, isSnow) 
                            ?? TerrainCatalog.PickHills(rand) 
                            ?? TerrainCatalog.PickMountain(rand, isSnow, false, isMainPeak: false);
                        var fTex = GetMountainTexture(flankDef);
                        if (flankDef != null && fTex != null)
                        {
                            var fSize = flankDef.WorldSize * flankScale;
                            var fOrigin = new Vector2(fSize.X * flankDef.Pivot.X, fSize.Y * flankDef.Pivot.Y);
                            result.Add(new MountainMacroSprite
                            {
                                Position = flankPos,
                                Texture = fTex,
                                SourceRegion = flankDef.Region,
                                Size = fSize,
                                Origin = fOrigin,
                                YOrder = flankPos.Y,
                                Rotation = 0f,
                                Modulate = new Color(0.95f, 0.95f, 0.95f, 0.92f)
                            });
                            placedCenters.Add(flankPos);
                        }
                    }
                }
            }
        }

        return result;
    }

    private readonly struct SplineSample
    {
        public readonly Vector2 Position;
        public readonly Vector2 Normal;
        public readonly float TangentAngle;
        public readonly float Elevation;
        public readonly float RidgeWidth;

        public SplineSample(Vector2 pos, Vector2 normal, float angle, float elev, float width)
        {
            Position = pos;
            Normal = normal;
            TangentAngle = angle;
            Elevation = elev;
            RidgeWidth = width;
        }
    }

    private static List<SplineSample> SampleSpline(IReadOnlyList<MountainSpineNode> spine, float stepDist)
    {
        var samples = new List<SplineSample>();
        if (spine.Count == 0) return samples;
        if (spine.Count == 1)
        {
            var p = new Vector2((float)spine[0].Position.X, (float)spine[0].Position.Y);
            samples.Add(new SplineSample(p, new Vector2(0f, 1f), 0f, spine[0].Elevation, spine[0].RidgeWidth));
            return samples;
        }

        for (var i = 0; i < spine.Count - 1; i++)
        {
            var p0 = new Vector2((float)spine[Math.Max(0, i - 1)].Position.X, (float)spine[Math.Max(0, i - 1)].Position.Y);
            var p1 = new Vector2((float)spine[i].Position.X, (float)spine[i].Position.Y);
            var p2 = new Vector2((float)spine[i + 1].Position.X, (float)spine[i + 1].Position.Y);
            var p3 = new Vector2((float)spine[Math.Min(spine.Count - 1, i + 2)].Position.X, (float)spine[Math.Min(spine.Count - 1, i + 2)].Position.Y);

            var dist = p1.DistanceTo(p2);
            var steps = Math.Max(1, (int)Math.Ceiling(dist / stepDist));

            for (var s = 0; s < steps; s++)
            {
                var t = s / (float)steps;
                var pos = CatmullRom(p0, p1, p2, p3, t);

                // 计算样条切线与法线
                var nextPos = CatmullRom(p0, p1, p2, p3, Math.Min(1.0f, t + 0.05f));
                var tangent = (nextPos - pos).Normalized();
                if (tangent.LengthSquared() < 0.001f) tangent = (p2 - p1).Normalized();
                var normal = new Vector2(-tangent.Y, tangent.X);
                var angle = tangent.Angle();

                var elev = Mathf.Lerp(spine[i].Elevation, spine[i + 1].Elevation, t);
                var ridgeWidth = Mathf.Lerp(spine[i].RidgeWidth, spine[i + 1].RidgeWidth, t);

                samples.Add(new SplineSample(pos, normal, angle, elev, ridgeWidth));
            }
        }

        // 加入尾节点
        var last = spine[^1];
        var lastPos = new Vector2((float)last.Position.X, (float)last.Position.Y);
        var prevPos = new Vector2((float)spine[^2].Position.X, (float)spine[^2].Position.Y);
        var lastTangent = (lastPos - prevPos).Normalized();
        samples.Add(new SplineSample(lastPos, new Vector2(-lastTangent.Y, lastTangent.X), lastTangent.Angle(), last.Elevation, last.RidgeWidth));

        return samples;
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        return 0.5f * (
            (2.0f * p1) +
            (-p0 + p2) * t +
            (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 +
            (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3
        );
    }

    private static bool IsFarFromPlaced(Vector2 pos, List<Vector2> placed, float minDist)
    {
        var minDistSq = minDist * minDist;
        for (var i = 0; i < placed.Count; i++)
        {
            if (pos.DistanceSquaredTo(placed[i]) < minDistSq) return false;
        }
        return true;
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
                if (img != null) return GroundDecalAssets.WithOptionalMask(ImageTexture.CreateFromImage(img), globalPath);
            }
            catch { }
        }

        try
        {
            if (ResourceLoader.Exists(resPath))
            {
                var loaded = ResourceLoader.Load<Texture2D>(resPath);
                return loaded == null ? null : GroundDecalAssets.WithOptionalMask(loaded, resPath);
            }
        }
        catch { }
        return null;
    }

    private static Texture2D CreateDefaultPeakTexture()
    {
        const int w = 96;
        const int h = 112;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (var y = 0; y < h; y++)
        {
            var ny = y / (float)h;
            for (var x = 0; x < w; x++)
            {
                var nx = (x - w * 0.5f) / (w * 0.5f);
                var peakLimit = MathF.Abs(nx) * 1.35f + 0.10f;
                if (ny >= peakLimit)
                {
                    var isOutline = (ny - peakLimit) < 0.055f;
                    var shaded = nx > 0.05f;
                    Color c;
                    if (isOutline) c = new Color(0.14f, 0.12f, 0.10f, 0.98f);
                    else if (shaded) c = new Color(0.18f, 0.27f, 0.25f, 0.92f);
                    else c = new Color(0.24f, 0.46f, 0.40f, 0.90f);

                    // 山脚云气雾化留白
                    if (ny > 0.75f) c.A *= (1f - ny) / 0.25f;
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

    private static Texture2D CreateDefaultClusterTexture()
    {
        const int w = 140;
        const int h = 90;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (var y = 0; y < h; y++)
        {
            var ny = y / (float)h;
            for (var x = 0; x < w; x++)
            {
                var nx = (x - w * 0.5f) / (w * 0.5f);
                // 3 座相连峰峦轮廓
                var p1 = MathF.Abs(nx) * 1.4f + 0.15f;
                var p2 = MathF.Abs(nx - 0.45f) * 1.6f + 0.30f;
                var p3 = MathF.Abs(nx + 0.45f) * 1.5f + 0.35f;
                var peakLimit = MathF.Min(p1, MathF.Min(p2, p3));

                if (ny >= peakLimit)
                {
                    var isOutline = (ny - peakLimit) < 0.055f;
                    var shaded = nx > 0.02f;
                    Color c;
                    if (isOutline) c = new Color(0.15f, 0.13f, 0.10f, 0.98f);
                    else if (shaded) c = new Color(0.20f, 0.29f, 0.27f, 0.92f);
                    else c = new Color(0.26f, 0.48f, 0.42f, 0.88f);

                    if (ny > 0.78f) c.A *= (1f - ny) / 0.22f;
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


    private static bool IsOnLand(WorldSnapshot snapshot, Vector2 pos, float seaLevel)
    {
        var geom = snapshot.Geometry;
        var cell = geom.FindCell(pos.X, pos.Y);
        if (cell < 0 || cell >= geom.Count) return false;
        return snapshot.Fields.Height[cell] > seaLevel;
    }
}
