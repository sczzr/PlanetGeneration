using Godot;
using PlanetGeneration.Core.Cartography;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;
using PlanetGeneration.Core.Layers;
using PlanetGeneration.Rendering.Macro;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PlanetGeneration.Rendering;

/// <summary>
/// 中国国画风格手绘地图渲染引擎 (Guohua Hand-Drawn Map Renderer)
///
/// 将任意世界快照转化为古代青绿水墨手绘山水舆图：
/// 1. 宣纸地子与淡青蓝水晕、细密水波纹与游弋帆船；
/// 2. 碧蓝江河水系、跨江古石拱桥与江河题记；
/// 3. 青绿水墨群峰（天柱峰、青云山、云海岭），采用 Y 轴景深遮挡排序（前山掩后山）；
/// 4. 密林松影、修竹篁丛与田畴梯田；
/// 5. 虚线古道驿径连接聚落与津渡；
/// 6. 四级古建聚落（重郭宝塔城池、方城合院城镇、人字茅舍村庄、宝塔古刹寺庙）与书法题名；
/// 7. 右下角古典木框图例与宣纸卷轴边框。
/// </summary>
public static class GuohuaMapRenderer
{
    // ── 配色常数 ──
    private static readonly Color PaperTone = Color.FromHtml("#efe3cb");
    private static readonly Color InkBrush = Color.FromHtml("#26221d");
    private static readonly Color InkBrushSoft = new(0.20f, 0.17f, 0.14f, 0.65f);
    private static readonly Color InkWaterWave = new(0.38f, 0.58f, 0.68f, 0.42f);
    private static readonly Color InkRiverFill = Color.FromHtml("#5ba3c6");
    private static readonly Color InkRiverBorder = new(0.20f, 0.32f, 0.40f, 0.70f);
    private static readonly Color InkTrail = new(0.28f, 0.22f, 0.16f, 0.85f);
    private static readonly Color InkLabelColor = Color.FromHtml("#24201a");
    private static readonly Color InkLabelHalo = new(0.96f, 0.92f, 0.84f, 0.88f);
    private static readonly Color InkCinnabar = Color.FromHtml("#ad3323");
    private static readonly Color InkBorderColor = Color.FromHtml("#42382e");

    // ── 纹理资源缓存 ──
    private static bool _texturesLoaded;
    private static Texture2D? _texMountainAtlasSingle;
    private static Texture2D? _texMountainAtlasCluster;
    private static Texture2D? _texMountainPeak;
    private static Texture2D? _texMountainRidge;
    private static Texture2D? _texMountainHill;
    private static Texture2D? _texMountainSnow;
    private static Texture2D? _texForestPine;
    private static Texture2D? _texForestBamboo;
    private static Texture2D? _texTreeAtlasSingle;
    private static Texture2D? _texTreeAtlasCluster;
    private static Texture2D? _texTreeSingle;
    private static Texture2D? _texTreeWillow;
    private static Texture2D? _texTreeCluster;
    private static Texture2D? _texFieldTerraced;
    private static Texture2D? _texMistStripe;
    private static Texture2D? _texCity;
    private static Texture2D? _texLargeCity;
    private static Texture2D? _texTown;
    private static Texture2D? _texVillage;
    private static Texture2D? _texTemple;
    private static Texture2D? _texPass;
    private static Texture2D? _texPort;
    private static Texture2D? _texPagoda;
    private static Texture2D? _texCompassRose;
    private static Texture2D? _texDesertDune;
    private static Texture2D? _texDesertCliff;
    private static Texture2D? _texDesertOasis;
    private static Texture2D? _texDeadTree;
    private static Texture2D? _texLegendBox;
    private static Texture2D? _texPyramidRelic;

    // ── 衍生元素快照缓存 ──
    private static long _cachedSnapshotId = -1;
    private static CartographySnapshot? _cachedCartography;
    private static readonly List<GuohuaSpriteElement> _cachedSortedSprites = new();
    private static readonly GroundDecalTextureCache _groundDecalTextures = new();
    private static readonly List<GuohuaWaveLine> _cachedWaveLines = new();
    private static readonly List<GuohuaLabelElement> _cachedLabels = new();
    private static readonly List<Vector2[]> _cachedTrailPolylines = new();
    private static readonly List<ForestMacroElement> _cachedForests = new();
    private static readonly List<DesertMacroElement> _cachedDeserts = new();
    private static readonly List<GuohuaNauticalElement> _cachedNautical = new();
    private static readonly List<GuohuaHachureSegment> _cachedTolkienHachures = new();
    private static readonly List<GuohuaOxbowLake> _cachedOxbowLakes = new();
    private static readonly List<GuohuaChasmAbyss> _cachedChasmAbysses = new();
    private static readonly List<GuohuaChasmCliff> _cachedChasmCliffs = new();
    private static readonly List<GuohuaDeltaIsland> _cachedDeltaIslands = new();

    private sealed class GuohuaHachureSegment
    {
        public required Vector2 Start { get; init; }
        public required Vector2 End { get; init; }
        public required Color Color { get; init; }
        public float Width { get; init; } = 1.2f;
    }

    private sealed class GuohuaOxbowLake
    {
        public required Vector2[] Points { get; init; }
        public float StrokeWidth { get; init; } = 4f;
        public Color FillColor { get; init; }
        public Color BorderColor { get; init; }
    }

    private sealed class GuohuaChasmAbyss
    {
        public required Vector2[] Polygon { get; init; }
        public Color FillColor { get; init; }
        public Color BorderColor { get; init; }
    }

    private sealed class GuohuaChasmCliff
    {
        public required Vector2[] Points { get; init; }
        public float Width { get; init; } = 1.6f;
        public Color Color { get; init; }
    }

    private sealed class GuohuaDeltaIsland
    {
        public required Vector2 Center { get; init; }
        public required Vector2 Size { get; init; }
        public float Rotation { get; init; }
        public Color FillColor { get; init; }
        public Color BorderColor { get; init; }
    }

    private sealed class GuohuaNauticalElement
    {
        public required Vector2 Position { get; init; }
        public required Vector2 Size { get; init; }
        public required Texture2D Texture { get; init; }
        public float Rotation { get; init; } = 0f;
        public Color Modulate { get; init; } = Colors.White;
    }

    private sealed class GuohuaSpriteElement
    {
        public required Vector2 Position { get; init; }
        public required Texture2D Texture { get; init; }
        public required Vector2 Size { get; init; }
        public required Vector2 Origin { get; init; }
        public required float YOrder { get; init; }
        public Color Modulate { get; init; } = Colors.White;
        public Rect2? SourceRegion { get; init; }
        public float Rotation { get; init; } = 0f;
        public List<(Vector2 pt, Vector2 normal)>? CoastalClipPlanes { get; init; }
        public GroundDecalProfile DecalProfile { get; set; }
        public GroundDecalTextures? SurfaceTextures { get; set; }
        public float DepthSortBias { get; set; } = 0f;

        /// <summary>
        /// 基于图元旋转与中心锚点，计算其底边接地线在屏幕上的真实最大 Y 坐标。
        /// 彻底杜绝中心点偏移导致的 2.5D 深度反向穿帮（前山遮后山，近景叠远景）。
        /// </summary>
        public float FootprintY
        {
            get
            {
                if (Size.X <= 0 || Size.Y <= 0) return Position.Y;
                var localBL = new Vector2(-Origin.X, Size.Y - Origin.Y);
                var localBR = new Vector2(Size.X - Origin.X, Size.Y - Origin.Y);
                var localBC = new Vector2(Size.X * 0.5f - Origin.X, Size.Y - Origin.Y);
                if (Mathf.Abs(Rotation) < 0.001f)
                {
                    return Position.Y + (Size.Y - Origin.Y);
                }
                var sin = Mathf.Sin(Rotation);
                var cos = Mathf.Cos(Rotation);
                var yBL = Position.Y + localBL.X * sin + localBL.Y * cos;
                var yBR = Position.Y + localBR.X * sin + localBR.Y * cos;
                var yBC = Position.Y + localBC.X * sin + localBC.Y * cos;
                return Mathf.Max(yBC, Mathf.Max(yBL, yBR));
            }
        }

        public float DepthSortKey => FootprintY + DepthSortBias;
    }

    private sealed class GuohuaWaveLine
    {
        public required Vector2[] Points { get; init; }
        public float StrokeWidth { get; init; } = 1.0f;
        public Color? OverrideColor { get; init; }
    }

    private sealed class GuohuaLabelElement
    {
        public required Vector2 Position { get; init; }
        public required string Text { get; init; }
        public required int FontSize { get; init; }
        public bool IsSea { get; init; }
        public bool HasHalo { get; init; } = true;
    }

    private static void EnsureTexturesLoaded()
    {
        if (_texturesLoaded) return;
        _texturesLoaded = true;

        // 加载宏观自然地貌图集与目录
        _texMountainAtlasSingle = LoadGuohuaTexture("mountain_atlas_single.png");
        _texMountainAtlasCluster = LoadGuohuaTexture("mountain_atlas_cluster.png");
        TerrainCatalog.EnsureInitialized();

        // 优先加载方案 A 层次化手绘单木、双木交柯与三木微丛图集
        _texTreeAtlasSingle = LoadGuohuaTexture("tree_atlas_single.png");
        _texTreeAtlasCluster = LoadGuohuaTexture("tree_atlas_cluster.png");
        _texMountainPeak = LoadGuohuaTexture("mountain_peak.png") ?? GuohuaFallbackTextures.CreateDefaultMountainTexture();
        _texMountainRidge = LoadGuohuaTexture("mountain_peak.png");
        _texMountainHill = LoadGuohuaTexture("mountain_hill.png");
        _texMountainSnow = LoadGuohuaTexture("mountain_snow.png");
        _texForestPine = LoadGuohuaTexture("forest_pine.png") ?? LoadGuohuaTexture("tree_single.png");
        _texForestBamboo = LoadGuohuaTexture("forest_bamboo.png") ?? LoadGuohuaTexture("tree_cluster.png");
        _texTreeSingle = LoadGuohuaTexture("tree_single.png");
        _texTreeWillow = LoadGuohuaTexture("tree_willow.png");
        _texTreeCluster = LoadGuohuaTexture("tree_cluster.png");
        _texFieldTerraced = null;
        _texMistStripe = GuohuaFallbackTextures.CreateDefaultMistStripeTexture();
        _texCity = LoadGuohuaTexture("city_capital.png") ?? LoadGuohuaTexture("city.png") ?? GuohuaFallbackTextures.CreateDefaultCityTexture();
        _texLargeCity = LoadGuohuaTexture("city_large.png") ?? _texCity;
        _texTown = LoadGuohuaTexture("town.png") ?? GuohuaFallbackTextures.CreateDefaultTownTexture();
        _texVillage = LoadGuohuaTexture("village.png") ?? GuohuaFallbackTextures.CreateDefaultVillageTexture();
        _texTemple = GuohuaFallbackTextures.CreateDefaultTempleTexture();
        _texPass = GuohuaFallbackTextures.CreateDefaultPassTexture();
        _texPort = GuohuaFallbackTextures.CreateDefaultPortTexture();
        _texPagoda = null;
        _texCompassRose = LoadGuohuaTexture("compass_rose.png");
        _texDesertDune = null;
        _texDesertCliff = null;
        _texDesertOasis = null;
        _texDeadTree = LoadGuohuaTexture("dead_tree.png");
        _texLegendBox = LoadGuohuaTexture("legend_box.png");
    }

    private static readonly Dictionary<string, Texture2D> _atlasCache = new();

    private static Texture2D? GetTerrainTexture(TerrainDef? def)
    {
        if (def == null) return _texMountainAtlasSingle ?? _texMountainAtlasCluster;
        if (!string.IsNullOrEmpty(def.AtlasName))
        {
            if (_atlasCache.TryGetValue(def.AtlasName, out var cached))
                return cached;
            var loaded = LoadGuohuaTexture(def.AtlasName);
            if (loaded != null)
            {
                _atlasCache[def.AtlasName] = loaded;
                return loaded;
            }
        }
        return _texMountainAtlasSingle ?? _texMountainAtlasCluster;
    }

    // ── 古代舆图规范符号图元（Ancient Map Symbols） ──

    private static Texture2D? LoadGuohuaTexture(string filename)
    {
        var resPath = $"res://resources/textures/guohua/{filename}";
        var globalPath = ProjectSettings.GlobalizePath(resPath);
        if (!File.Exists(globalPath))
        {
            var fallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"../../../../csharp/resources/textures/guohua/{filename}");
            if (File.Exists(fallback)) globalPath = fallback;
        }

        // 优先从文件系统直接读取无损原始文件，避免 Godot 导入器压缩与陈旧缓存偏差
        if (File.Exists(globalPath))
        {
            try
            {
                var img = Image.LoadFromFile(globalPath);
                if (img != null)
                {
                    var tex = ImageTexture.CreateFromImage(img);
                    GD.Print($"[GuohuaMapRenderer] 加载物理原图 '{filename}' 成功: {tex.GetWidth()}x{tex.GetHeight()}");
                    return GroundDecalAssets.WithOptionalMask(tex, globalPath);
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GuohuaMapRenderer] 读取物理原图 '{globalPath}' 失败: {ex.Message}");
            }
        }

        try
        {
            if (ResourceLoader.Exists(resPath))
            {
                var loaded = ResourceLoader.Load<Texture2D>(resPath);
                if (loaded != null) return GroundDecalAssets.WithOptionalMask(loaded, resPath);
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// 当世界快照变更时重建国风手绘元素空间缓存。
    /// </summary>
    private static void EnsureSnapshotCached(WorldSnapshot snapshot)
    {
        if (_cachedSnapshotId == snapshot.SnapshotId) return;
        _cachedSnapshotId = snapshot.SnapshotId;

        EnsureTexturesLoaded();
        _cachedSortedSprites.Clear();
        _groundDecalTextures.Clear();
        _cachedWaveLines.Clear();
        _cachedLabels.Clear();
        _cachedTrailPolylines.Clear();
        _cachedForests.Clear();
        _cachedDeserts.Clear();
        _cachedNautical.Clear();
        _cachedTolkienHachures.Clear();
        _cachedOxbowLakes.Clear();
        _cachedChasmAbysses.Clear();
        _cachedChasmCliffs.Clear();
        _cachedDeltaIslands.Clear();

        // 1. 获取或生成幻想制图层快照 (CartographySnapshot)
        var cartography = snapshot.Cartography ?? CartographyGenerator.Generate(snapshot);
        _cachedCartography = cartography;

        // 2. 构建宏观森林与沙漠面状地貌层
        _cachedForests.AddRange(ForestMacroRenderer.BuildForestElements(snapshot));
        _cachedDeserts.AddRange(DesertMacroRenderer.BuildDesertElements(snapshot));

        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var seaLevel = snapshot.Options.SeaLevel;
        var rand = new Random((int)(snapshot.Options.Seed ^ 0x6611));

        // 3. 消费水纹笔刷 (SeaWave 及智能海岸 CoastlineWave)
        foreach (var wave in cartography.Brushes.Where(b => b.Type is BrushType.SeaWave or BrushType.CoastlineWave))
        {
            if (wave.Points != null && wave.Points.Length > 0)
            {
                var pts = new Vector2[wave.Points.Length];
                for (var i = 0; i < wave.Points.Length; i++)
                {
                    pts[i] = new Vector2((float)wave.Points[i].X, (float)wave.Points[i].Y);
                }
                var wColor = wave.Type == BrushType.CoastlineWave
                    ? new Color(0.24f, 0.46f, 0.60f, Math.Max(0.42f, wave.Opacity))
                    : InkWaterWave;
                _cachedWaveLines.Add(new GuohuaWaveLine
                {
                    Points = pts,
                    StrokeWidth = wave.StrokeWidth,
                    OverrideColor = wColor
                });
            }
        }

        // 4. 提取水域题名 ("海" 与 大湖 "镜湖")
        BuildSeaAndLakeLabels(snapshot, (float)geom.Width, (float)geom.Height, seaLevel);

        // 5. 消费地标与题名 (LandmarkStyle)
        foreach (var lm in cartography.Landmarks)
        {
            if (!lm.ShowLabel) continue; // 小村落仅存符号点，不生成文字标签，杜绝文字噪点
            var lPos = new Vector2((float)lm.Position.X, (float)lm.Position.Y);
            if (lm.IconType == BrushType.CityIcon)
            {
                lPos += new Vector2(10f * lm.Scale, 3f);
            }
            _cachedLabels.Add(new GuohuaLabelElement
            {
                Position = lPos,
                Text = lm.Name,
                FontSize = lm.FontSize,
                HasHalo = lm.HasHalo,
                IsSea = lm.TextColor.B > 0.6f && lm.TextColor.R < 0.45f
            });
        }

        // 6. 提取海域古典航海图元 (罗盘)
        BuildNauticalElements(snapshot, seaLevel);

        // 7. 消费制图层全量笔刷指令 (BrushInstruction -> GuohuaSpriteElement)
        foreach (var brush in cartography.Brushes)
        {
            DrawBrushToCache(brush, snapshot, rand);
        }

        // 8. 提取古道驿径
        BuildTrails(snapshot, cartography, seaLevel);

        // 9. 浮动如意祥云（已按需求移除）
        // BuildAuspiciousClouds(snapshot);

        // 10. 将全部地貌、植被、建筑与船只按真实接地线（DepthSortKey）严格排序（前山遮后山，近景叠远景）
        // 确保靠南的前景山峦、丘陵与城郭绝对绘制于后方地貌之上。
        var sorted = _cachedSortedSprites.OrderBy(s => s.DepthSortKey).ThenBy(s => s.Position.X).ToArray();
        _cachedSortedSprites.Clear();
        _cachedSortedSprites.AddRange(sorted);
        foreach (var sprite in _cachedSortedSprites)
        {
            if (sprite.DecalProfile.Mode != GroundDecalMode.None || GroundDecalAssets.GetMask(sprite.Texture) != null)
                sprite.SurfaceTextures = _groundDecalTextures.Get(sprite.Texture, sprite.SourceRegion, sprite.DecalProfile);
        }
    }

    private static void DrawBrushToCache(BrushInstruction brush, WorldSnapshot snapshot, Random rand)
    {
        var firstSprite = _cachedSortedSprites.Count;
        var bPos = new Vector2((float)brush.Position.X, (float)brush.Position.Y);
        var bModulate = new Color(brush.Tint.R, brush.Tint.G, brush.Tint.B, brush.Opacity);

        switch (brush.Type)
        {
            case BrushType.MountainMain:
            {
                var isSnow = brush.VariantKey?.Contains("snow") == true;
                var isVolcano = brush.VariantKey?.Contains("volcano") == true;
                var def = TerrainCatalog.PickMountain(rand, isSnow, isVolcano, isMainPeak: true);
                var tex = GetTerrainTexture(def);
                if (def != null && tex != null)
                {
                    var size = def.WorldSize * brush.Scale;
                    var origin = new Vector2(size.X * def.Pivot.X, size.Y * def.Pivot.Y);
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = tex,
                        SourceRegion = def.Region,
                        Size = size,
                        Origin = origin,
                        YOrder = brush.YOrder,
                        Rotation = brush.Rotation,
                        Modulate = bModulate
                    });
                }
                else if (_texMountainPeak != null)
                {
                    var size = new Vector2(58f, 28.5f) * brush.Scale;
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = _texMountainPeak,
                        Size = size,
                        Origin = new Vector2(size.X * 0.5f, size.Y * 0.88f),
                        YOrder = brush.YOrder,
                        Rotation = brush.Rotation,
                        Modulate = bModulate
                    });
                }
                break;
            }

            case BrushType.MountainSecondary:
            {
                var isSnow = brush.VariantKey?.Contains("snow") == true;
                var def = TerrainCatalog.PickCompanionPeak(rand, isSnow);
                var tex = GetTerrainTexture(def);
                if (def != null && tex != null)
                {
                    var size = def.WorldSize * brush.Scale;
                    var origin = new Vector2(size.X * def.Pivot.X, size.Y * def.Pivot.Y);
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = tex,
                        SourceRegion = def.Region,
                        Size = size,
                        Origin = origin,
                        YOrder = brush.YOrder,
                        Rotation = brush.Rotation,
                        Modulate = bModulate
                    });
                }
                break;
            }

            case BrushType.MountainRidge:
            {
                var isSnow = brush.VariantKey?.Contains("snow") == true;
                var def = TerrainCatalog.PickRidge(rand, isSnow);
                var tex = GetTerrainTexture(def);
                if (def != null && tex != null)
                {
                    var size = def.WorldSize * brush.Scale;
                    var origin = new Vector2(size.X * def.Pivot.X, size.Y * def.Pivot.Y);
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = tex,
                        SourceRegion = def.Region,
                        Size = size,
                        Origin = origin,
                        YOrder = brush.YOrder,
                        Rotation = brush.Rotation,
                        Modulate = bModulate
                    });
                }
                break;
            }

            case BrushType.Hill:
            {
                var def = TerrainCatalog.PickHills(rand);
                var tex = GetTerrainTexture(def);
                if (def != null && tex != null)
                {
                    var size = def.WorldSize * brush.Scale;
                    var origin = new Vector2(size.X * def.Pivot.X, size.Y * def.Pivot.Y);
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = tex,
                        SourceRegion = def.Region,
                        Size = size,
                        Origin = origin,
                        YOrder = brush.YOrder,
                        Rotation = brush.Rotation,
                        Modulate = bModulate
                    });
                }
                break;
            }

            case BrushType.MountainFar:
            {
                var def = TerrainCatalog.PickFarMountain(rand);
                var tex = GetTerrainTexture(def);
                if (def != null && tex != null)
                {
                    var size = def.WorldSize * brush.Scale;
                    var origin = new Vector2(size.X * def.Pivot.X, size.Y * def.Pivot.Y);
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = tex,
                        SourceRegion = def.Region,
                        Size = size,
                        Origin = origin,
                        YOrder = brush.YOrder,
                        Rotation = brush.Rotation,
                        Modulate = bModulate
                    });
                }
                break;
            }

            case BrushType.SnowCap:
            {
                var def = TerrainCatalog.PickMountain(rand, isSnow: true, isVolcano: false, isMainPeak: true);
                var tex = GetTerrainTexture(def);
                if (def != null && tex != null)
                {
                    var size = def.WorldSize * brush.Scale;
                    var origin = new Vector2(size.X * def.Pivot.X, size.Y * def.Pivot.Y);
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = tex,
                        SourceRegion = def.Region,
                        Size = size,
                        Origin = origin,
                        YOrder = brush.YOrder,
                        Rotation = brush.Rotation,
                        Modulate = Colors.White * brush.Opacity
                    });
                }
                break;
            }

            case BrushType.PlateauCliff:
            {
                var isEdge = brush.VariantKey?.Contains("edge") == true;
                var def = TerrainCatalog.PickPlateau(rand, isEdge);
                var tex = GetTerrainTexture(def);
                if (def != null && tex != null)
                {
                    var size = def.WorldSize * brush.Scale;
                    var origin = new Vector2(size.X * def.Pivot.X, size.Y * def.Pivot.Y);
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = tex,
                        SourceRegion = def.Region,
                        Size = size,
                        Origin = origin,
                        YOrder = brush.YOrder,
                        Rotation = brush.Rotation,
                        Modulate = bModulate
                    });
                }
                break;
            }

            case BrushType.ForestCluster:
            {
                // 使用三木顾盼/双木交柯/单木手绘树木微丛
                var tree = TreeCatalog.PickTrio(rand) ?? TreeCatalog.PickDuo(rand) ?? TreeCatalog.PickSingle(rand, TreeFamily.Broadleaf);
                if (tree != null)
                {
                    var tex = tree.AtlasName == "tree_atlas_cluster.png" ? _texTreeAtlasCluster : _texTreeAtlasSingle;
                    if (tex != null)
                    {
                        var size = tree.WorldSize * brush.Scale;
                        var origin = new Vector2(size.X * tree.Pivot.X, size.Y * tree.Pivot.Y);
                        // 手绘真彩图元：保持自然材质宣纸色泽，避免暗黑矢量墨色压暗
                        var treeModulate = new Color(1f, 1f, 1f, brush.Opacity);
                        _cachedSortedSprites.Add(new GuohuaSpriteElement
                        {
                            Position = bPos,
                            Texture = tex,
                            SourceRegion = tree.Region,
                            Size = size,
                            Origin = origin,
                            YOrder = brush.YOrder,
                            Modulate = treeModulate
                        });
                    }
                }
                break;
            }

            case BrushType.TreeGroup:
            {
                if (brush.VariantKey?.Contains("dead") == true && _texDeadTree != null)
                {
                    var dSize = new Vector2(7f, 8f) * brush.Scale;
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = _texDeadTree,
                        Size = dSize,
                        Origin = new Vector2(dSize.X * 0.5f, dSize.Y * 0.85f),
                        YOrder = brush.YOrder,
                        Modulate = bModulate
                    });
                    break;
                }

                if (brush.VariantKey?.Contains("willow") == true && _texTreeWillow != null)
                {
                    var wSize = new Vector2(6f, 7f) * brush.Scale;
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = _texTreeWillow,
                        Size = wSize,
                        Origin = new Vector2(wSize.X * 0.5f, wSize.Y * 0.85f),
                        YOrder = brush.YOrder,
                        Modulate = new Color(1f, 1f, 1f, brush.Opacity)
                    });
                    break;
                }

                TreeDef? tree;
                if (brush.VariantKey?.Contains("willow") == true)
                {
                    tree = TreeCatalog.PickWillow(rand) ?? TreeCatalog.PickSingle(rand, TreeFamily.Willow) ?? TreeCatalog.PickSingle(rand, TreeFamily.Broadleaf);
                }
                else if (brush.VariantKey?.Contains("copse") == true)
                {
                    tree = TreeCatalog.PickDuo(rand) ?? TreeCatalog.PickTrio(rand) ?? TreeCatalog.PickSingle(rand, TreeFamily.Broadleaf);
                }
                else if (brush.VariantKey?.Contains("single") == true)
                {
                    var cell = snapshot.Geometry.FindCell(brush.Position.X, brush.Position.Y);
                    var biome = (cell >= 0 && cell < snapshot.Fields.Count) ? (BiomeType)snapshot.Fields.Biome[cell] : BiomeType.TemperateSeasonalForest;
                    var isConifer = biome is BiomeType.Taiga or BiomeType.BorealForest or BiomeType.Tundra;
                    if (isConifer)
                    {
                        tree = TreeCatalog.PickSingle(rand, TreeFamily.Pine) ?? TreeCatalog.PickSingle(rand, TreeFamily.Cypress) ?? TreeCatalog.PickSingle(rand);
                    }
                    else
                    {
                        tree = TreeCatalog.PickSingle(rand) ?? TreeCatalog.PickSingle(rand, TreeFamily.Broadleaf);
                    }
                }
                else
                {
                    tree = TreeCatalog.PickSingle(rand, TreeFamily.Broadleaf) ?? TreeCatalog.PickBush(rand);
                }

                if (tree != null)
                {
                    var tex = tree.AtlasName == "tree_atlas_cluster.png" ? _texTreeAtlasCluster : _texTreeAtlasSingle;
                    if (tex != null)
                    {
                        var size = tree.WorldSize * brush.Scale;
                        var origin = new Vector2(size.X * tree.Pivot.X, size.Y * tree.Pivot.Y);
                        var treeModulate = new Color(1f, 1f, 1f, brush.Opacity);
                        _cachedSortedSprites.Add(new GuohuaSpriteElement
                        {
                            Position = bPos,
                            Texture = tex,
                            SourceRegion = tree.Region,
                            Size = size,
                            Origin = origin,
                            YOrder = brush.YOrder,
                            Modulate = treeModulate
                        });
                    }
                }
                else if (_texTreeSingle != null)
                {
                    var sSize = new Vector2(3.5f, 5.0f) * brush.Scale;
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = _texTreeSingle,
                        Size = sSize,
                        Origin = new Vector2(sSize.X * 0.5f, sSize.Y * 0.90f),
                        YOrder = brush.YOrder,
                        Modulate = new Color(1f, 1f, 1f, brush.Opacity)
                    });
                }
                break;
            }

            case BrushType.GrassTussock:
            {
                var isShrub = brush.VariantKey?.Contains("shrub") == true;
                var isWideMeadow = brush.VariantKey?.Contains("plain") == true;
                var def = isShrub
                    ? (TerrainCatalog.PickGrassland(rand, "Shrub") ?? TerrainCatalog.PickGrassland(rand, "Tussock"))
                    : (isWideMeadow
                        ? (TerrainCatalog.PickGrassland(rand, "MeadowPlain") ?? TerrainCatalog.PickGrassland(rand, "Tussock"))
                        : (TerrainCatalog.PickGrassland(rand, "Tussock") ?? TerrainCatalog.PickGrassland(rand, "MeadowPlain")));

                var tex = GetTerrainTexture(def);
                if (def != null && tex != null)
                {
                    var size = def.WorldSize * brush.Scale;
                    var origin = new Vector2(size.X * def.Pivot.X, size.Y * def.Pivot.Y);
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = tex,
                        SourceRegion = def.Region,
                        Size = size,
                        Origin = origin,
                        YOrder = brush.YOrder,
                        Rotation = brush.Rotation,
                        Modulate = bModulate
                    });
                }
                break;
            }

            case BrushType.WetlandReeds:
            {
                var isIslet = brush.VariantKey?.Contains("islet") == true;
                var def = isIslet
                    ? TerrainCatalog.PickWetland(rand, preferIslet: true)
                    : (TerrainCatalog.PickWetland(rand, "Reeds") ?? TerrainCatalog.PickWetland(rand, "Cattails"));

                var tex = GetTerrainTexture(def);
                if (def != null && tex != null)
                {
                    var size = def.WorldSize * brush.Scale;
                    var origin = new Vector2(size.X * def.Pivot.X, size.Y * def.Pivot.Y);
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = tex,
                        SourceRegion = def.Region,
                        Size = size,
                        Origin = origin,
                        YOrder = brush.YOrder,
                        Rotation = brush.Rotation,
                        Modulate = bModulate
                    });
                }
                break;
            }

            case BrushType.LakePond:
            {
                var def = TerrainCatalog.PickWetland(rand, preferIslet: true);
                var tex = GetTerrainTexture(def);
                if (def != null && tex != null)
                {
                    var size = def.WorldSize * brush.Scale * 1.25f;
                    var origin = new Vector2(size.X * def.Pivot.X, size.Y * def.Pivot.Y);
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = tex,
                        SourceRegion = def.Region,
                        Size = size,
                        Origin = origin,
                        YOrder = brush.YOrder - 1.0f,
                        Rotation = brush.Rotation,
                        Modulate = bModulate
                    });
                }
                break;
            }

            case BrushType.DesertDune:
            {
                var def = TerrainCatalog.PickDesert(rand, isOasis: false);
                var tex = GetTerrainTexture(def);
                if (def != null && tex != null)
                {
                    var size = def.WorldSize * brush.Scale;
                    var origin = new Vector2(size.X * def.Pivot.X, size.Y * def.Pivot.Y);
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = tex,
                        SourceRegion = def.Region,
                        Size = size,
                        Origin = origin,
                        YOrder = brush.YOrder,
                        Rotation = brush.Rotation,
                        Modulate = bModulate
                    });
                }
                break;
            }

            case BrushType.FieldTerraced:
            {
                // 彻底取缔机械倾斜条形码农田（////）！
                // 中央河谷与平原生活区已由底层青绿水墨地表完整烘染。
                // 此处仅以极低频率（约每4处取1处）点缀散落的古朴村舍或河岸垂柳，其余保持开阔通透的青绿田畴
                var hash = ((int)bPos.X * 73856093) ^ ((int)bPos.Y * 19349663);
                if ((hash & 3) == 0 && _texVillage != null)
                {
                    var vSize = new Vector2(10f, 10f) * brush.Scale;
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = _texVillage,
                        Size = vSize,
                        Origin = vSize * 0.5f,
                        YOrder = brush.YOrder,
                        Modulate = bModulate
                    });
                }
                else if ((hash & 7) == 1 && _texTreeWillow != null)
                {
                    var wSize = new Vector2(4.5f, 5.5f) * brush.Scale;
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = _texTreeWillow,
                        Size = wSize,
                        Origin = new Vector2(wSize.X * 0.5f, wSize.Y * 0.85f),
                        YOrder = brush.YOrder,
                        Modulate = bModulate
                    });
                }
                break;
            }

            case BrushType.CityIcon:
            {
                Texture2D? cTex;
                Vector2 sz;
                if (brush.VariantKey is "city_capital" or "symbol_capital")
                {
                    cTex = _texCity ?? (_texCity = GuohuaFallbackTextures.CreateDefaultCityTexture());
                    sz = new Vector2(22f, 22f) * brush.Scale;
                }
                else if (brush.VariantKey is "city_large" or "symbol_city" or "city_state")
                {
                    cTex = _texLargeCity ?? (_texLargeCity = GuohuaFallbackTextures.CreateDefaultLargeCityTexture());
                    sz = new Vector2(18f, 18f) * brush.Scale;
                }
                else if (brush.VariantKey is "pass_garrison" or "city_pass" or "symbol_pass")
                {
                    cTex = _texPass ?? (_texPass = GuohuaFallbackTextures.CreateDefaultPassTexture());
                    sz = new Vector2(15f, 15f) * brush.Scale;
                }
                else if (brush.VariantKey is "symbol_port" or "city_port" or "port")
                {
                    cTex = _texPort ?? (_texPort = GuohuaFallbackTextures.CreateDefaultPortTexture());
                    sz = new Vector2(14f, 14f) * brush.Scale;
                }
                else if (brush.VariantKey is "city_town" or "city_prefecture" or "symbol_town")
                {
                    cTex = _texTown ?? (_texTown = GuohuaFallbackTextures.CreateDefaultTownTexture());
                    sz = new Vector2(14f, 14f) * brush.Scale;
                }
                else if (brush.VariantKey is "temple_pagoda" or "temple" or "symbol_temple")
                {
                    cTex = _texTemple ?? (_texTemple = GuohuaFallbackTextures.CreateDefaultTempleTexture());
                    sz = new Vector2(13f, 15f) * brush.Scale;
                }
                else if (brush.VariantKey is "pyramid_relic" or "relic_pyramid" or "desert_relic")
                {
                    cTex = _texPyramidRelic ?? (_texPyramidRelic = GuohuaFallbackTextures.CreateDefaultPyramidRelicTexture());
                    sz = new Vector2(16f, 16f) * brush.Scale;
                }
                else
                {
                    cTex = _texVillage ?? (_texVillage = GuohuaFallbackTextures.CreateDefaultVillageTexture());
                    sz = new Vector2(9f, 9f) * brush.Scale;
                }

                if (cTex != null)
                {
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = cTex,
                        Size = sz,
                        Origin = sz * 0.5f,
                        YOrder = brush.YOrder,
                        Modulate = bModulate
                    });
                }
                break;
            }

            case BrushType.Fog:
            {
                // 云朵与云雾图元已按需求移除
                break;
            }

            // ── Map Effects (https://www.mapeffects.co/learn) 专属手绘图元渲染 ──
            case BrushType.MountainHachure:
            {
                // 托尔金山脉背光侧斜向阴影排线 (Tolkien Hachures)
                // 沿山体背阴侧（东南坡）绘制 5~7 条平行斜向焦墨排线，与山脊呈 45 度角
                var hCount = 5 + rand.Next(3);
                var hSpacing = 3.2f * brush.Scale;
                var baseDir = new Vector2(0.707f, 0.707f); // 东南向
                var normalDir = new Vector2(-baseDir.Y, baseDir.X); // 法向
                var lineLen = 14.0f * brush.Scale;
                var hColor = new Color(0.12f, 0.10f, 0.08f, brush.Opacity * 0.85f); // 焦墨阴影排线

                for (var k = 0; k < hCount; k++)
                {
                    var offset = normalDir * ((k - (hCount - 1) * 0.5f) * hSpacing);
                    var pStart = bPos + offset - baseDir * (lineLen * 0.5f);
                    var pEnd = bPos + offset + baseDir * (lineLen * 0.5f);
                    _cachedTolkienHachures.Add(new GuohuaHachureSegment
                    {
                        Start = pStart,
                        End = pEnd,
                        Width = brush.StrokeWidth > 0.1f ? brush.StrokeWidth : 1.4f,
                        Color = hColor
                    });
                }
                break;
            }

            case BrushType.DeltaIsland:
            {
                var sz = new Vector2(20f, 10f) * brush.Scale;
                _cachedDeltaIslands.Add(new GuohuaDeltaIsland
                {
                    Center = bPos,
                    Size = sz,
                    Rotation = brush.Rotation,
                    FillColor = new Color(0.82f, 0.78f, 0.65f, 0.95f), // 温暖宣纸浅赭冲积沙洲
                    BorderColor = new Color(0.32f, 0.28f, 0.22f, 0.85f)
                });
                break;
            }

            case BrushType.SeaArch:
            {
                var def = TerrainCatalog.PickCompanionPeak(rand, isSnow: false);
                var tex = GetTerrainTexture(def) ?? _texMountainPeak;
                if (tex != null)
                {
                    var sz = new Vector2(22f, 16f) * brush.Scale;
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = tex,
                        Size = sz,
                        Origin = sz * 0.5f,
                        YOrder = brush.YOrder,
                        Rotation = brush.Rotation,
                        Modulate = bModulate
                    });
                }
                break;
            }

            case BrushType.SwampPool:
            {
                var sz = new Vector2(20f, 10f) * brush.Scale;
                var poolTex = _texMistStripe ?? (_texMistStripe = GuohuaFallbackTextures.CreateDefaultMistStripeTexture());
                _cachedSortedSprites.Add(new GuohuaSpriteElement
                {
                    Position = bPos,
                    Texture = poolTex,
                    Size = sz,
                    Origin = sz * 0.5f,
                    YOrder = brush.YOrder - 0.5f,
                    Modulate = new Color(0.18f, 0.28f, 0.22f, 0.65f)
                });
                break;
            }

            case BrushType.SpanishMoss:
            {
                var sz = new Vector2(5f, 9f) * brush.Scale;
                var mossTex = _texTreeWillow ?? _texForestPine;
                if (mossTex != null)
                {
                    _cachedSortedSprites.Add(new GuohuaSpriteElement
                    {
                        Position = bPos,
                        Texture = mossTex,
                        Size = sz,
                        Origin = sz * 0.5f,
                        YOrder = brush.YOrder + 0.5f,
                        Modulate = bModulate
                    });
                }
                break;
            }

            case BrushType.ChasmAbyss:
            {
                if (brush.Points != null && brush.Points.Length >= 3)
                {
                    var poly = new Vector2[brush.Points.Length];
                    for (var i = 0; i < brush.Points.Length; i++)
                    {
                        poly[i] = new Vector2((float)brush.Points[i].X, (float)brush.Points[i].Y);
                    }
                    _cachedChasmAbysses.Add(new GuohuaChasmAbyss
                    {
                        Polygon = poly,
                        FillColor = new Color(0.08f, 0.06f, 0.05f, 0.98f), // 漆黑幽暗深渊
                        BorderColor = new Color(0.20f, 0.16f, 0.12f, 0.92f)
                    });
                }
                break;
            }

            case BrushType.ChasmCliff:
            {
                if (brush.Points != null && brush.Points.Length >= 2)
                {
                    var pts = new Vector2[brush.Points.Length];
                    for (var i = 0; i < brush.Points.Length; i++)
                    {
                        pts[i] = new Vector2((float)brush.Points[i].X, (float)brush.Points[i].Y);
                    }
                    _cachedChasmCliffs.Add(new GuohuaChasmCliff
                    {
                        Points = pts,
                        Width = brush.StrokeWidth > 0.1f ? brush.StrokeWidth : 1.6f,
                        Color = new Color(0.26f, 0.20f, 0.15f, brush.Opacity)
                    });
                }
                break;
            }

            case BrushType.OxbowLake:
            {
                if (brush.Points != null && brush.Points.Length >= 2)
                {
                    var pts = new Vector2[brush.Points.Length];
                    for (var i = 0; i < brush.Points.Length; i++)
                    {
                        pts[i] = new Vector2((float)brush.Points[i].X, (float)brush.Points[i].Y);
                    }
                    _cachedOxbowLakes.Add(new GuohuaOxbowLake
                    {
                        Points = pts,
                        StrokeWidth = brush.StrokeWidth > 0.1f ? brush.StrokeWidth : 4.5f,
                        FillColor = InkRiverFill,
                        BorderColor = InkRiverBorder
                    });
                }
                break;
            }
        }

        var decalProfile = brush.Type switch
        {
            BrushType.MountainMain or BrushType.MountainSecondary or BrushType.MountainRidge or
            BrushType.MountainFar or BrushType.Hill or BrushType.SnowCap => GroundDecalProfile.Contact,
            BrushType.ForestCluster or BrushType.TreeGroup or BrushType.CityIcon or
            BrushType.FieldTerraced => GroundDecalProfile.Contact,
            _ => default
        };
        var depthBias = brush.Type switch
        {
            BrushType.MountainFar => -1000f,
            BrushType.SnowCap => 20f,
            _ => 0f
        };
        for (var i = firstSprite; i < _cachedSortedSprites.Count; i++)
        {
            _cachedSortedSprites[i].DecalProfile = decalProfile;
            if (depthBias != 0f) _cachedSortedSprites[i].DepthSortBias = depthBias;
        }
    }

    private static void BuildSeaWaveLines(WorldSnapshot snapshot, float width, float height, float seaLevel)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var rand = new Random((int)(snapshot.Options.Seed ^ 0x5a5a));

        var stepY = 22f;
        var stepX = 28f;

        for (var y = 14f; y < height - 14f; y += stepY)
        {
            var offset = (rand.NextSingle() - 0.5f) * 10f;
            for (var x = 14f; x < width - 14f; x += stepX)
            {
                var curX = x + offset + (rand.NextSingle() - 0.5f) * 6f;
                var curY = y + (rand.NextSingle() - 0.5f) * 4f;

                var cell = geom.FindCell(curX, curY);
                if (cell < 0 || cell >= fields.Count) continue;
                if (fields.Height[cell] > seaLevel * 0.98f) continue; // 仅在海水中生成

                // 2~3 段正弦微波
                var pts = new Vector2[6];
                var waveLen = 16f + rand.NextSingle() * 10f;
                var waveAmp = 1.4f + rand.NextSingle() * 0.8f;
                for (var k = 0; k < pts.Length; k++)
                {
                    var t = k / (float)(pts.Length - 1);
                    var px = curX - waveLen * 0.5f + t * waveLen;
                    var py = curY + Mathf.Sin(t * Mathf.Pi * 2.2f) * waveAmp;
                    pts[k] = new Vector2(px, py);
                }
                _cachedWaveLines.Add(new GuohuaWaveLine { Points = pts });
                x += 16f; // 避开相邻重叠
            }
        }
    }

    private static void BuildSeaAndLakeLabels(WorldSnapshot snapshot, float width, float height, float seaLevel)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;

        // 挑选 2~4 个离岸较远的大海域点标注 "海"
        var seaCandidates = new List<Vector2>();
        var sampleStep = 65f;
        for (var y = sampleStep * 0.7f; y < height; y += sampleStep)
        {
            for (var x = sampleStep * 0.7f; x < width; x += sampleStep)
            {
                var cell = geom.FindCell(x, y);
                if (cell >= 0 && cell < fields.Count && fields.Height[cell] < seaLevel * 0.55f)
                {
                    seaCandidates.Add(new Vector2(x, y));
                }
            }
        }

        // 随机挑选几个代表性海域位置
        var rand = new Random((int)(snapshot.Options.Seed ^ 0x7788));
        var chosenCount = Math.Min(4, seaCandidates.Count);
        for (var i = 0; i < chosenCount; i++)
        {
            var idx = rand.Next(seaCandidates.Count);
            var pos = seaCandidates[idx];
            seaCandidates.RemoveAt(idx);
            _cachedLabels.Add(new GuohuaLabelElement
            {
                Position = pos,
                Text = "海",
                FontSize = 18,
                IsSea = true,
                HasHalo = true
            });
        }
    }

    private static void BuildNauticalElements(WorldSnapshot snapshot, float seaLevel)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var width = (float)geom.Width;
        var height = (float)geom.Height;

        // 1. 八卦指北古典罗盘 (Compass Rose)
        if (_texCompassRose != null)
        {
            // 左上角海域罗盘
            var compPos1 = new Vector2(width * 0.085f, height * 0.155f);
            var compSize1 = new Vector2(110f, 110f);
            _cachedNautical.Add(new GuohuaNauticalElement
            {
                Position = compPos1,
                Size = compSize1,
                Texture = _texCompassRose,
                Modulate = new Color(1f, 1f, 1f, 0.90f)
            });

            // 右下角海域罗盘
            var compPos2 = new Vector2(width * 0.925f, height * 0.865f);
            var compSize2 = new Vector2(100f, 100f);
            _cachedNautical.Add(new GuohuaNauticalElement
            {
                Position = compPos2,
                Size = compSize2,
                Texture = _texCompassRose,
                Modulate = new Color(1f, 1f, 1f, 0.88f)
            });
        }
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

    private readonly struct PlacedClusterRecord
    {
        public readonly Vector2 Center;
        public readonly Vector2 Radius;

        public PlacedClusterRecord(Vector2 center, Vector2 radius)
        {
            Center = center;
            Radius = radius;
        }
    }

    private static bool CanPlaceCluster(
        Vector2 center,
        Vector2 radius,
        List<PlacedClusterRecord> placedList,
        float tolerance = 0.88f)
    {
        var tolSq = tolerance * tolerance;
        for (var i = 0; i < placedList.Count; i++)
        {
            var p = placedList[i];
            var rx = p.Radius.X + radius.X;
            var ry = p.Radius.Y + radius.Y;
            var dx = center.X - p.Center.X;
            var dy = center.Y - p.Center.Y;

            var normDistSq = (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry);
            if (normDistSq < tolSq)
            {
                return false;
            }
        }
        return true;
    }

    private static List<Vector2> ClipPolygonAgainstHalfPlane(List<Vector2> poly, Vector2 linePt, Vector2 outwardNormal)
    {
        var output = new List<Vector2>(poly.Count + 2);
        if (poly.Count == 0) return output;

        for (var i = 0; i < poly.Count; i++)
        {
            var cur = poly[i];
            var next = poly[(i + 1) % poly.Count];

            var curDist = (cur - linePt).Dot(outwardNormal);
            var nextDist = (next - linePt).Dot(outwardNormal);

            var curInside = curDist <= 0.001f;
            var nextInside = nextDist <= 0.001f;

            if (curInside)
            {
                output.Add(cur);
                if (!nextInside)
                {
                    var t = curDist / (curDist - nextDist);
                    output.Add(cur + (next - cur) * t);
                }
            }
            else if (nextInside)
            {
                var t = curDist / (curDist - nextDist);
                output.Add(cur + (next - cur) * t);
            }
        }
        return output;
    }

    private static List<(Vector2 pt, Vector2 normal)>? GetCoastalClipPlanes(WorldSnapshot snapshot, int cellId, float seaLevel)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var start = geom.CellNeighborStart[cellId];
        var end = geom.CellNeighborStart[cellId + 1];

        var cPos = new Vector2((float)geom.CentroidX[cellId], (float)geom.CentroidY[cellId]);
        List<(Vector2, Vector2)>? planes = null;

        for (var k = start; k < end; k++)
        {
            var nb = geom.CellNeighbors[k];
            if (fields.Height[nb] <= seaLevel)
            {
                var nbX = (float)geom.CentroidX[nb];
                var nbY = (float)geom.CentroidY[nb];
                var dx = nbX - cPos.X;
                if (dx > geom.Width * 0.5f) dx -= (float)geom.Width;
                else if (dx < -geom.Width * 0.5f) dx += (float)geom.Width;

                var nbPos = new Vector2(cPos.X + dx, nbY);
                var toSea = nbPos - cPos;
                if (toSea.LengthSquared() > 0.001f)
                {
                    var normal = toSea.Normalized();
                    var mid = (cPos + nbPos) * 0.5f - normal * 1.0f; // 微偏向陆地 1 像素，确保海水边缘绝无树木透出

                    planes ??= new List<(Vector2, Vector2)>();
                    planes.Add((mid, normal));
                }
            }
        }

        return planes;
    }

    private static void BuildMegaRegionLabels(WorldSnapshot snapshot)
    {
        var regions = snapshot.MegaRegions;
        if (regions == null || regions.Count == 0) return;

        foreach (var r in regions)
        {
            if (r.Rank < MegaRegionRank.MajorRegion) continue;
            var pos = new Vector2((float)r.Centroid.X, (float)r.Centroid.Y);

            var fontSize = r.Rank switch
            {
                MegaRegionRank.WorldLandmark => 16,
                MegaRegionRank.MegaRegion => 14,
                _ => 12
            };

            switch (r.Type)
            {
                case MegaTerrainType.MegaPlateau:
                    _cachedLabels.Add(new GuohuaLabelElement
                    {
                        Position = pos,
                        Text = r.Name,
                        FontSize = fontSize,
                        HasHalo = true
                    });
                    break;
                case MegaTerrainType.MegaBasin:
                    _cachedLabels.Add(new GuohuaLabelElement
                    {
                        Position = pos,
                        Text = r.Name,
                        FontSize = fontSize,
                        HasHalo = true
                    });
                    break;
                case MegaTerrainType.MegaDesert:
                    _cachedLabels.Add(new GuohuaLabelElement
                    {
                        Position = pos,
                        Text = r.Name,
                        FontSize = fontSize,
                        HasHalo = true
                    });
                    break;
                case MegaTerrainType.MegaWetland:
                    _cachedLabels.Add(new GuohuaLabelElement
                    {
                        Position = pos,
                        Text = r.Name,
                        FontSize = fontSize,
                        HasHalo = true,
                        IsSea = true
                    });
                    break;
                case MegaTerrainType.MegaGrassland:
                    _cachedLabels.Add(new GuohuaLabelElement
                    {
                        Position = pos,
                        Text = r.Name,
                        FontSize = fontSize,
                        HasHalo = true
                    });
                    break;
            }
        }
    }

    private static void AddTerrainSprite(
        Vector2 pos,
        TerrainDef def,
        float scaleMultiplier,
        List<Vector2> placedCenters)
    {
        var tex = GetTerrainTexture(def);
        if (tex == null) return;

        var size = def.WorldSize * scaleMultiplier;
        var origin = new Vector2(size.X * def.Pivot.X, size.Y * def.Pivot.Y);

        _cachedSortedSprites.Add(new GuohuaSpriteElement
        {
            DecalProfile = def.Category is TerrainCategory.Mountain or TerrainCategory.Hills or
                TerrainCategory.SnowMountain or TerrainCategory.Volcano or TerrainCategory.Basin
                ? GroundDecalProfile.Contact : default,
            Position = pos,
            Texture = tex,
            SourceRegion = def.Region,
            Size = size,
            Origin = origin,
            YOrder = pos.Y
        });
        placedCenters.Add(pos);
    }

    private static void BuildMacroTerrains(WorldSnapshot snapshot, float seaLevel, List<Vector2> placedMountainCenters)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var rand = new Random((int)(snapshot.Options.Seed ^ 0x6611));

        // 1. 优先绘制大型山脉（基于 Spline 样条脊线与连绵群峰大模组排布）
        var macroMountainSprites = MountainMacroRenderer.BuildMountainSprites(snapshot, seaLevel, placedMountainCenters);
        foreach (var ms in macroMountainSprites)
        {
            _cachedSortedSprites.Add(new GuohuaSpriteElement
            {
                DecalProfile = GroundDecalProfile.Contact,
                Position = ms.Position,
                Texture = ms.Texture,
                SourceRegion = ms.SourceRegion,
                Size = ms.Size,
                Origin = ms.Origin,
                YOrder = ms.YOrder,
                Rotation = ms.Rotation,
                Modulate = ms.Modulate
            });
        }

        // 为各大山脉最高脊线处题写地标题名
        foreach (var m in snapshot.MegaRegions)
        {
            if (m.Type != MegaTerrainType.MegaMountain || m.Spine == null || m.Spine.Count == 0) continue;
            var highest = m.Spine.OrderByDescending(n => n.Elevation).First();
            var hp = new Vector2((float)highest.Position.X, (float)highest.Position.Y);
            _cachedLabels.Add(new GuohuaLabelElement
            {
                Position = hp + new Vector2(18f, -22f),
                Text = m.Name,
                FontSize = m.Rank >= MegaRegionRank.MegaRegion ? 14 : 12,
                HasHalo = true
            });
        }

        // 2. 巨型丘陵 (MegaHills)
        var megaHills = snapshot.MegaRegions.Where(r => r.Type == MegaTerrainType.MegaHills).ToList();
        foreach (var h in megaHills)
        {
            foreach (var cell in h.Cells)
            {
                if (rand.NextSingle() > 0.40f) continue;
                var pos = new Vector2((float)geom.CentroidX[cell], (float)geom.CentroidY[cell]);
                if (IsFarFromPlaced(pos, placedMountainCenters, 36f))
                {
                    var hillDef = TerrainCatalog.PickHills(rand);
                    if (hillDef != null)
                    {
                        var scale = 0.85f + rand.NextSingle() * 0.25f;
                        AddTerrainSprite(pos, hillDef, scale, placedMountainCenters);
                    }
                }
            }
        }

        // 3. 超大型高原 (MegaPlateau) - 边缘断崖立柱与中心平顶桌状山
        var megaPlateaus = snapshot.MegaRegions.Where(r => r.Type == MegaTerrainType.MegaPlateau).ToList();
        foreach (var p in megaPlateaus)
        {
            foreach (var cell in p.Cells)
            {
                if (rand.NextSingle() > 0.35f) continue;
                var pos = new Vector2((float)geom.CentroidX[cell], (float)geom.CentroidY[cell]);
                if (IsFarFromPlaced(pos, placedMountainCenters, 34f))
                {
                    var depth = p.ComputeNormalizedDepth(new PolyVec2(pos.X, pos.Y));
                    var isEdge = depth < 0.35f;
                    var plateauDef = TerrainCatalog.PickPlateau(rand, isEdge);
                    if (plateauDef != null)
                    {
                        var scale = 0.90f + rand.NextSingle() * 0.25f;
                        AddTerrainSprite(pos, plateauDef, scale, placedMountainCenters);
                    }
                }
            }
        }

        // 4. 超大型荒漠 (MegaDesert) - 大面积水晕沙垄由 DesertMacroRenderer 统一绘制，此处仅点缀极少数清泉绿洲
        var megaDeserts = snapshot.MegaRegions.Where(r => r.Type == MegaTerrainType.MegaDesert).ToList();
        foreach (var d in megaDeserts)
        {
            foreach (var cell in d.Cells)
            {
                var isOasis = fields.Moisture[cell] > 0.38f || fields.River[cell] > 0.06f;
                if (!isOasis || rand.NextSingle() > 0.35f) continue;

                var pos = new Vector2((float)geom.CentroidX[cell], (float)geom.CentroidY[cell]);
                if (IsFarFromPlaced(pos, placedMountainCenters, 36f))
                {
                    var desertDef = TerrainCatalog.PickDesert(rand, isOasis: true);
                    if (desertDef != null)
                    {
                        var scale = 0.85f + rand.NextSingle() * 0.20f;
                        AddTerrainSprite(pos, desertDef, scale, placedMountainCenters);
                    }
                }
            }
        }

        // 5. 超大型草原 (MegaGrassland) - 风草丛生与微丘草甸
        var megaGrasslands = snapshot.MegaRegions.Where(r => r.Type == MegaTerrainType.MegaGrassland).ToList();
        foreach (var g in megaGrasslands)
        {
            foreach (var cell in g.Cells)
            {
                if (rand.NextSingle() > 0.28f) continue;
                var pos = new Vector2((float)geom.CentroidX[cell], (float)geom.CentroidY[cell]);
                if (IsFarFromPlaced(pos, placedMountainCenters, 32f))
                {
                    var grassDef = TerrainCatalog.PickGrassland(rand);
                    if (grassDef != null)
                    {
                        var scale = 0.85f + rand.NextSingle() * 0.25f;
                        AddTerrainSprite(pos, grassDef, scale, placedMountainCenters);
                    }
                }
            }
        }

        // 6. 超大型湿地 (MegaWetland) - 蒹葭芦荡与香蒲浅渚
        var megaWetlands = snapshot.MegaRegions.Where(r => r.Type == MegaTerrainType.MegaWetland).ToList();
        foreach (var w in megaWetlands)
        {
            foreach (var cell in w.Cells)
            {
                if (rand.NextSingle() > 0.35f) continue;
                var pos = new Vector2((float)geom.CentroidX[cell], (float)geom.CentroidY[cell]);
                if (IsFarFromPlaced(pos, placedMountainCenters, 28f))
                {
                    var wetDef = TerrainCatalog.PickWetland(rand, preferIslet: rand.NextSingle() < 0.45f);
                    if (wetDef != null)
                    {
                        var scale = 0.85f + rand.NextSingle() * 0.25f;
                        AddTerrainSprite(pos, wetDef, scale, placedMountainCenters);
                    }
                }
            }
        }

        // 7. 超大型盆地 (MegaBasin) - 环山合抱之臂
        var megaBasins = snapshot.MegaRegions.Where(r => r.Type == MegaTerrainType.MegaBasin).ToList();
        foreach (var b in megaBasins)
        {
            foreach (var cell in b.Cells)
            {
                var pos = new Vector2((float)geom.CentroidX[cell], (float)geom.CentroidY[cell]);
                var depth = b.ComputeNormalizedDepth(new PolyVec2(pos.X, pos.Y));
                // 环山臂分布在盆地外环 (depth < 0.45f)
                if (depth > 0.45f) continue;
                if (rand.NextSingle() > 0.35f) continue;

                if (IsFarFromPlaced(pos, placedMountainCenters, 38f))
                {
                    var basinDef = TerrainCatalog.PickBasin(rand, isRim: true);
                    if (basinDef != null)
                    {
                        var scale = 0.95f + rand.NextSingle() * 0.20f;
                        AddTerrainSprite(pos, basinDef, scale, placedMountainCenters);
                    }
                }
            }
        }

        // 8. 仅补充未被 MegaMountain 覆盖的稀有独立火山与极高孤峰（杜绝密集散点峰）
        for (var cell = 0; cell < fields.Count; cell++)
        {
            if (fields.Height[cell] <= seaLevel) continue;
            var lf = (LandformType)fields.Landform[cell];
            var isVolcano = lf == LandformType.Volcano;
            var isExtremePeak = lf == LandformType.Peak || (lf == LandformType.Mountain && fields.Height[cell] > seaLevel + 0.62f);

            if (!isVolcano && !isExtremePeak) continue;

            var pos = new Vector2((float)geom.CentroidX[cell], (float)geom.CentroidY[cell]);
            // 严格间距控制，避免与主山脉及已有地貌挤压冲突
            if (!IsFarFromPlaced(pos, placedMountainCenters, 80f)) continue;
            if (!isVolcano && rand.NextSingle() > 0.15f) continue; // 仅极小概率出现天然独立孤峰

            var isSnow = fields.Height[cell] > seaLevel + 0.54f || fields.Biome[cell] == (byte)BiomeType.SnowyMountain;
            var peakDef = TerrainCatalog.PickMountain(rand, isSnow, isVolcano, isMainPeak: true);
            if (peakDef != null)
            {
                var scale = 0.85f + rand.NextSingle() * 0.20f;
                AddTerrainSprite(pos, peakDef, scale, placedMountainCenters);
            }
        }
    }

    private static void BuildForestsAndBamboo(WorldSnapshot snapshot, float seaLevel, List<Vector2> placedMountainCenters)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var rand = new Random((int)(snapshot.Options.Seed ^ 0x9922));
        var placedClusters = new List<PlacedClusterRecord>();

        var megaForests = snapshot.MegaRegions.Where(r => r.Type == MegaTerrainType.MegaForest).ToList();
        if (megaForests.Count == 0) return;

        var useTreeCatalog = (_texTreeAtlasSingle != null || _texTreeAtlasCluster != null) && TreeCatalog.AllTrees.Count > 0;

        foreach (var forest in megaForests)
        {
            // 题注大森林名称
            var centerPos = new Vector2((float)forest.Centroid.X, (float)forest.Centroid.Y);
            _cachedLabels.Add(new GuohuaLabelElement
            {
                Position = centerPos + new Vector2(12f, -12f),
                Text = forest.Name,
                FontSize = forest.Rank >= MegaRegionRank.MegaRegion ? 14 : 12,
                HasHalo = true
            });

            // 仅在此 MegaForest 拥有的单元格内，按 Core / Transition / Edge 渐变深度生成树群
            for (var k = 0; k < forest.Cells.Length; k++)
            {
                var cell = forest.Cells[k];
                var h = fields.Height[cell];
                if (h <= seaLevel || h > seaLevel + 0.54f) continue;

                var cPos = new Vector2((float)geom.CentroidX[cell], (float)geom.CentroidY[cell]);
                if (!IsFarFromPlaced(cPos, placedMountainCenters, 20f)) continue;

                var depth = forest.ComputeNormalizedDepth(new PolyVec2(cPos.X, cPos.Y));
                if (depth < 0.08f) continue; // 边界外边缘适度留白

                // 核心腹地生成密度较高，边缘过渡区适度疏朗（破除平滑多边形感，呈现天然密林呼吸感）
                var spawnProb = depth >= 0.25f ? 0.65f : 0.40f;
                if (rand.NextSingle() > spawnProb) continue;

                var pt = cPos + new Vector2((rand.NextSingle() - 0.5f) * 8f, (rand.NextSingle() - 0.5f) * 8f);

                if (useTreeCatalog)
                {
                    var biome = (BiomeType)fields.Biome[cell];
                    var isNearRiver = fields.River[cell] > 0.03f;
                    var landDepthInt = depth >= 0.35f ? 3 : (depth >= 0.18f ? 2 : 1);
                    var tree = TreeCatalog.PickForContext(biome, isCoastal: false, isNearRiver, landDepthInt, rand)
                               ?? TreeCatalog.PickSingle(rand, TreeFamily.Broadleaf)
                               ?? TreeCatalog.PickBush(rand);
                    if (tree == null) continue;

                    var tex = tree.AtlasName == "tree_atlas_cluster.png" ? _texTreeAtlasCluster : _texTreeAtlasSingle;
                    if (tex == null) continue;

                    var size = tree.WorldSize * 0.88f;
                    var origin = new Vector2(size.X * tree.Pivot.X, size.Y * tree.Pivot.Y);
                    var visualCenter = pt - origin + size * 0.5f;
                    var visualRadius = new Vector2(size.X * 0.38f, size.Y * 0.24f);

                    if (CanPlaceCluster(visualCenter, visualRadius, placedClusters, 0.75f))
                    {
                        _cachedSortedSprites.Add(new GuohuaSpriteElement
                        {
                            DecalProfile = GroundDecalProfile.Contact,
                            Position = pt,
                            Texture = tex,
                            Size = size,
                            Origin = origin,
                            YOrder = pt.Y,
                            Rotation = 0f,
                            SourceRegion = tree.Region,
                            Modulate = new Color(1f, 1f, 1f, 0.88f),
                            CoastalClipPlanes = null
                        });
                        placedClusters.Add(new PlacedClusterRecord(visualCenter, visualRadius));
                    }
                }
            }
        }
    }

    private static void BuildFarmlands(WorldSnapshot snapshot, float seaLevel, List<Vector2> placedMountainCenters)
    {
        // 保持画面雅致纯粹，不放置带方框底色的农田装饰
        return;
    }

    private static void BuildSettlementsAndTemples(WorldSnapshot snapshot)
    {
        var settlements = snapshot.Settlements;
        if (settlements.Count == 0) return;

        var templeCount = 0;
        foreach (var s in settlements)
        {
            var pos = new Vector2((float)s.Position.X, (float)s.Position.Y);
            Texture2D? tex;
            Vector2 size;

            // 部分城镇/边陲聚落若名称含寺/驿/武，或者特定村落，按寺庙宝塔展示
            if (s.Rank == SettlementRank.CityState && _texCity != null)
            {
                tex = _texCity;
                size = new Vector2(24f, 22f); // 宏伟重郭
            }
            else if (s.Rank == SettlementRank.Town && _texTown != null)
            {
                tex = _texTown;
                size = new Vector2(15f, 13f); // 古典方城城镇符号
            }
            else if ((s.Name.Contains("寺") || s.Name.Contains("驿") || templeCount < 2) && _texTemple != null)
            {
                tex = _texTemple;
                size = new Vector2(13f, 15f); // 寺庙符号
                templeCount++;
            }
            else
            {
                tex = _texVillage ?? _texTown;
                size = new Vector2(9f, 9f); // 茅舍村庄圆点符号
            }

            if (tex != null)
            {
                _cachedSortedSprites.Add(new GuohuaSpriteElement
                {
                    DecalProfile = GroundDecalProfile.Contact,
                    Position = pos,
                    Texture = tex,
                    Size = size,
                    Origin = new Vector2(size.X * 0.5f, size.Y * 0.82f),
                    YOrder = pos.Y
                });
            }

            // 书法题名
            _cachedLabels.Add(new GuohuaLabelElement
            {
                Position = pos + new Vector2(size.X * 0.5f + 3f, 2f),
                Text = s.Name,
                FontSize = s.Rank == SettlementRank.CityState ? 13 : (s.Rank == SettlementRank.Town ? 11 : 9),
                HasHalo = true
            });
        }
    }

    private static void BuildTrails(WorldSnapshot snapshot, CartographySnapshot? cartography, float seaLevel)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;

        IReadOnlyList<SettlementInfo> settlements = snapshot.Settlements;
        if (cartography?.Landmarks != null && cartography.Landmarks.Count >= 2)
        {
            var strategic = cartography.Landmarks
                .Where(l => l.Type is LandmarkType.Capital or LandmarkType.Town or LandmarkType.Port or LandmarkType.Temple or LandmarkType.Village)
                .ToList();

            if (strategic.Count >= 2)
            {
                var customList = new List<SettlementInfo>();
                foreach (var st in strategic)
                {
                    var c = geom.FindCell(st.Position.X, st.Position.Y);
                    if (c >= 0 && c < geom.Count)
                    {
                        customList.Add(new SettlementInfo
                        {
                            CellId = c,
                            Name = st.Name,
                            Position = st.Position,
                            Score = 100f,
                            Rank = st.Type == LandmarkType.Capital ? SettlementRank.CityState : SettlementRank.Town
                        });
                    }
                }
                if (customList.Count >= 2)
                {
                    settlements = customList;
                }
            }
        }

        if (settlements.Count < 2) return;

        // 1. 构建候选聚落商贸网络：优先采用 CartographySnapshot 规划的精准官道网络
        var candidatePairs = new HashSet<(int, int)>();

        if (cartography?.RoadGraph != null && cartography.RoadGraph.Segments.Count > 0)
        {
            var nameToIdx = new Dictionary<string, int>();
            for (var idx = 0; idx < settlements.Count; idx++)
            {
                nameToIdx[settlements[idx].Name] = idx;
            }

            foreach (var seg in cartography.RoadGraph.Segments)
            {
                if (nameToIdx.TryGetValue(seg.FromSettlement, out var u) &&
                    nameToIdx.TryGetValue(seg.ToSettlement, out var v))
                {
                    candidatePairs.Add((Math.Min(u, v), Math.Max(u, v)));
                }
            }
        }

        // 回退模式：若无规划道路图则使用邻近距离拓扑
        if (candidatePairs.Count == 0)
        {
            var maxSearchDist = Math.Min(geom.Width, geom.Height) * 0.42f;

            for (var i = 0; i < settlements.Count; i++)
            {
                var pA = new Vector2((float)settlements[i].Position.X, (float)settlements[i].Position.Y);
                var distList = new List<(int idx, float d)>();

                for (var j = 0; j < settlements.Count; j++)
                {
                    if (i == j) continue;
                    var pB = new Vector2((float)settlements[j].Position.X, (float)settlements[j].Position.Y);
                    var d = pA.DistanceTo(pB);
                    if (d < maxSearchDist)
                    {
                        distList.Add((j, d));
                    }
                }

                distList.Sort((a, b) => a.d.CompareTo(b.d));
                var takeCount = Math.Min(2, distList.Count);
                for (var k = 0; k < takeCount; k++)
                {
                    var u = Math.Min(i, distList[k].idx);
                    var v = Math.Max(i, distList[k].idx);
                    candidatePairs.Add((u, v));
                }
            }
        }

        // 2. 在多边形单元格邻接图上执行基于地形代价值的 A* 寻路
        var gScore = new float[geom.Count];
        var cameFrom = new int[geom.Count];
        var visited = new int[geom.Count];
        var searchSession = 0;

        foreach (var (u, v) in candidatePairs)
        {
            var sA = settlements[u];
            var sB = settlements[v];
            var startCell = sA.CellId;
            var goalCell = sB.CellId;

            if (startCell < 0 || startCell >= geom.Count || goalCell < 0 || goalCell >= geom.Count) continue;
            if (fields.Height[startCell] <= seaLevel || fields.Height[goalCell] <= seaLevel) continue;

            searchSession++;
            var path = FindAStarPath(geom, fields, startCell, goalCell, seaLevel, gScore, cameFrom, visited, searchSession);
            if (path == null || path.Count < 2) continue;

            // 3. 将单元格路径组装为折线序列
            var rawPoints = new List<Vector2>(path.Count + 2);
            rawPoints.Add(new Vector2((float)sA.Position.X, (float)sA.Position.Y));
            for (var pIdx = 1; pIdx < path.Count - 1; pIdx++)
            {
                var c = path[pIdx];
                rawPoints.Add(new Vector2((float)geom.CentroidX[c], (float)geom.CentroidY[c]));
            }
            rawPoints.Add(new Vector2((float)sB.Position.X, (float)sB.Position.Y));

            // 4. 对古道折线进行 Chaikin 算法平滑，生成自然贴山傍水的流畅古道
            var smoothed = SmoothPolylineChaikin(rawPoints, iterations: 2);
            _cachedTrailPolylines.Add(smoothed);
        }
    }

    private static List<int>? FindAStarPath(
        CellGeometry geom,
        CellFields fields,
        int startCell,
        int goalCell,
        float seaLevel,
        float[] gScore,
        int[] cameFrom,
        int[] visited,
        int session)
    {
        var goalX = (float)geom.CentroidX[goalCell];
        var goalY = (float)geom.CentroidY[goalCell];

        float Heuristic(int cell)
        {
            var dx = (float)geom.CentroidX[cell] - goalX;
            var dy = (float)geom.CentroidY[cell] - goalY;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        var pq = new PriorityQueue<int, float>();
        gScore[startCell] = 0f;
        visited[startCell] = session;
        cameFrom[startCell] = -1;
        pq.Enqueue(startCell, Heuristic(startCell));

        var maxExpansions = 1500;
        var expansions = 0;

        while (pq.Count > 0 && expansions++ < maxExpansions)
        {
            var curr = pq.Dequeue();
            if (curr == goalCell)
            {
                var path = new List<int>();
                var step = curr;
                while (step != -1)
                {
                    path.Add(step);
                    step = cameFrom[step];
                }
                path.Reverse();
                return path;
            }

            var currG = gScore[curr];
            var currX = (float)geom.CentroidX[curr];
            var currY = (float)geom.CentroidY[curr];

            var startEdge = geom.CellNeighborStart[curr];
            var endEdge = geom.CellNeighborStart[curr + 1];

            for (var k = startEdge; k < endEdge; k++)
            {
                var nb = geom.CellNeighbors[k];
                if (fields.Height[nb] <= seaLevel) continue; // 严禁下海

                var nbX = (float)geom.CentroidX[nb];
                var nbY = (float)geom.CentroidY[nb];
                var dist = MathF.Sqrt((nbX - currX) * (nbX - currX) + (nbY - currY) * (nbY - currY));

                // 地形能耗权重军规：平原 1.0, 鞍部关隘 2.5, 密林核心 80.0, 高山主脊 999.0 (迫使古道绕山走隘口与平野)
                var landform = (LandformType)fields.Landform[nb];
                var h = fields.Height[nb];
                var isMountainPass = (landform == LandformType.Mountain && h < seaLevel + 0.28f);

                var landformCost = landform switch
                {
                    LandformType.Plain or LandformType.Basin or LandformType.Coast
                        or LandformType.Floodplain or LandformType.Delta or LandformType.Island => 1.0f,
                    LandformType.Hill or LandformType.Karst => 2.2f,
                    LandformType.Wetland => 2.8f,
                    LandformType.Plateau or LandformType.DryBasin => 3.8f,
                    LandformType.DesertDune or LandformType.Badlands => 4.2f,
                    LandformType.Canyon or LandformType.RiftValley or LandformType.Fjord => 5.0f,
                    LandformType.Mountain => isMountainPass ? 2.5f : 999.0f,
                    LandformType.Peak or LandformType.Volcano or LandformType.Glacier => 999.0f,
                    _ => 2.0f
                };

                var biome = (BiomeType)fields.Biome[nb];
                var isDenseForest = biome is BiomeType.TemperateRainForest or BiomeType.TropicalRainForest or BiomeType.BorealForest
                                    || (biome == BiomeType.TemperateSeasonalForest && fields.Moisture[nb] > 0.65f);
                var biomeCost = isDenseForest ? 80.0f : (biome == BiomeType.TemperateSeasonalForest ? 2.5f : 1.0f);

                // 渡河代价：仅在需要跨越宽大水体时增加代价
                var riverCost = (fields.River[nb] > 0.15f) ? 12.0f : 0.0f;
                var tentativeG = currG + (dist * landformCost * biomeCost) + riverCost;

                if (visited[nb] != session || tentativeG < gScore[nb])
                {
                    visited[nb] = session;
                    gScore[nb] = tentativeG;
                    cameFrom[nb] = curr;
                    var fScore = tentativeG + Heuristic(nb);
                    pq.Enqueue(nb, fScore);
                }
            }
        }

        return null;
    }

    private static Vector2[] SmoothPolylineChaikin(List<Vector2> pts, int iterations)
    {
        if (pts.Count <= 2) return pts.ToArray();

        var current = pts;
        for (var it = 0; it < iterations; it++)
        {
            var next = new List<Vector2>(current.Count * 2);
            next.Add(current[0]);
            for (var i = 0; i < current.Count - 1; i++)
            {
                var p0 = current[i];
                var p1 = current[i + 1];
                var q = p0 * 0.75f + p1 * 0.25f;
                var r = p0 * 0.25f + p1 * 0.75f;
                next.Add(q);
                next.Add(r);
            }
            next.Add(current[^1]);
            current = next;
        }

        return current.ToArray();
    }

    private static void DrawSurfaceSprite(CanvasItem item, GuohuaSpriteElement sprite, Rect2 visibleRect, bool groundPass)
    {
        if (groundPass && sprite.SurfaceTextures == null) return;
        var texture = sprite.SurfaceTextures == null ? sprite.Texture
            : groundPass ? sprite.SurfaceTextures.Decal : sprite.SurfaceTextures.Body;
        // Split textures are cropped to their atlas entry, hence use local UVs.
        var region = sprite.SurfaceTextures == null ? sprite.SourceRegion : null;
        DrawSurfaceTexture(item, texture, sprite.Position, sprite.Size, sprite.Origin,
            sprite.Rotation, sprite.Modulate, region, sprite.CoastalClipPlanes, visibleRect);
    }

    internal static void DrawSurfaceTexture(CanvasItem item, Texture2D texture, Vector2 position,
        Vector2 size, Vector2 origin, float rotation, Color tint, Rect2? region,
        List<(Vector2 pt, Vector2 normal)>? clipPlanes, Rect2 visibleRect)
    {
        if (size.X <= 0 || size.Y <= 0) return;
        var transform = new Transform2D(rotation, position);
        var corners = new List<Vector2>
        {
            transform * -origin,
            transform * (new Vector2(size.X, 0) - origin),
            transform * (size - origin),
            transform * (new Vector2(0, size.Y) - origin)
        };
        var bounds = new Rect2(corners[0], Vector2.Zero);
        foreach (var point in corners) bounds = bounds.Expand(point);
        // The anchor may be off screen while a tall mountain remains visible.
        if (!visibleRect.Intersects(bounds, true)) return;
        if (clipPlanes != null)
            foreach (var (point, normal) in clipPlanes)
            {
                corners = ClipPolygonAgainstHalfPlane(corners, point, normal);
                if (corners.Count < 3) return;
            }
        var inverse = transform.AffineInverse();
        var uvs = new Vector2[corners.Count];
        var colors = new Color[corners.Count];
        var textureSize = texture.GetSize();
        for (var i = 0; i < corners.Count; i++)
        {
            var uv = (inverse * corners[i] + origin) / size;
            uvs[i] = region.HasValue ? (region.Value.Position + uv * region.Value.Size) / textureSize : uv;
            colors[i] = tint;
        }
        item.DrawPolygon(corners.ToArray(), colors, uvs, texture);
    }

    /// <summary>
    /// 在当前 CanvasItem 上执行全套国风手绘地图矢量 + 精灵渲染。
    /// </summary>
    public static void Draw(
        CanvasItem item,
        WorldSnapshot snapshot,
        Rect2 visibleRect,
        float screenScale,
        Font? font)
    {
        EnsureSnapshotCached(snapshot);
        var labelFont = font ?? ThemeDB.FallbackFont;

        // 1. 绘制海域细密水纹线 (Wave Curves) 与智能海岸多阶水波 (Coastline Waves)
        foreach (var wave in _cachedWaveLines)
        {
            if (!visibleRect.HasPoint(wave.Points[0])) continue;
            var waveStroke = (wave.StrokeWidth > 0.1f ? wave.StrokeWidth : 1.0f) / screenScale;
            var waveColor = wave.OverrideColor ?? InkWaterWave;
            item.DrawPolyline(wave.Points, waveColor, waveStroke, true);
        }

        // 1.5 绘制海域舆图古典元素（八卦罗盘、沧海蛟龙、游弋古帆船）
        foreach (var n in _cachedNautical)
        {
            if (!visibleRect.HasPoint(n.Position)) continue;
            var origin = n.Size * 0.5f;
            var dest = new Rect2(n.Position - origin, n.Size);
            if (Mathf.Abs(n.Rotation) > 0.01f)
            {
                var cos = Mathf.Cos(n.Rotation);
                var sin = Mathf.Sin(n.Rotation);
                Vector2 Rot(Vector2 p) => new(p.X * cos - p.Y * sin, p.X * sin + p.Y * cos);
                var p0 = n.Position + Rot(-origin);
                var p1 = n.Position + Rot(new Vector2(origin.X, -origin.Y));
                var p2 = n.Position + Rot(origin);
                var p3 = n.Position + Rot(new Vector2(-origin.X, origin.Y));
                var uvs = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
                item.DrawPolygon(
                    new[] { p0, p1, p2, p3 },
                    new[] { n.Modulate, n.Modulate, n.Modulate, n.Modulate },
                    uvs,
                    n.Texture);
            }
            else
            {
                item.DrawTextureRect(n.Texture, dest, false, n.Modulate);
            }
        }

        // 2. 绘制大漠淡赭藤黄水墨水晕基底与沙丘流线 (Desert Macro Layer)
        DesertMacroRenderer.Draw(item, _cachedDeserts, visibleRect, screenScale);

        // Ground-only pass: all subsequent forest crowns, cliff walls and Y-sorted
        // bodies occlude these pixels, even when their Y order is behind the owner.
        foreach (var sprite in _cachedSortedSprites)
            DrawSurfaceSprite(item, sprite, visibleRect, groundPass: true);

        // 3. 绘制巨型宏观水墨林海 (Forest Macro Layer)
        ForestMacroRenderer.Draw(item, _cachedForests, visibleRect, screenScale);

        // 3.4 绘制 Map Effects 大地深渊裂谷底与断崖 (Chasm Abyss & Cliffs)
        foreach (var chasm in _cachedChasmAbysses)
        {
            if (chasm.Polygon.Length < 3) continue;
            var colors = new Color[chasm.Polygon.Length];
            Array.Fill(colors, chasm.FillColor);
            item.DrawPolygon(chasm.Polygon, colors);
            var closed = new Vector2[chasm.Polygon.Length + 1];
            Array.Copy(chasm.Polygon, closed, chasm.Polygon.Length);
            closed[^1] = chasm.Polygon[0];
            item.DrawPolyline(closed, chasm.BorderColor, 1.8f / screenScale, true);
        }

        foreach (var cliff in _cachedChasmCliffs)
        {
            if (cliff.Points.Length < 2) continue;
            item.DrawPolyline(cliff.Points, cliff.Color, cliff.Width / screenScale, true);
        }

        // 3.5 绘制 Map Effects 河口冲积沙洲群岛 (Delta Islands)
        foreach (var island in _cachedDeltaIslands)
        {
            var halfW = island.Size.X * 0.5f;
            var halfH = island.Size.Y * 0.5f;
            const int count = 16;
            var pts = new Vector2[count];
            var cos = MathF.Cos(island.Rotation);
            var sin = MathF.Sin(island.Rotation);
            for (var k = 0; k < count; k++)
            {
                var angle = k / (float)count * MathF.PI * 2f;
                var lx = MathF.Cos(angle) * halfW;
                var ly = MathF.Sin(angle) * halfH;
                var rx = lx * cos - ly * sin;
                var ry = lx * sin + ly * cos;
                pts[k] = island.Center + new Vector2(rx, ry);
            }
            var colors = new Color[count];
            Array.Fill(colors, island.FillColor);
            item.DrawPolygon(pts, colors);
            var closed = new Vector2[count + 1];
            Array.Copy(pts, closed, count);
            closed[count] = pts[0];
            item.DrawPolyline(closed, island.BorderColor, 1.2f / screenScale, true);
        }

        // 4. 绘制江河水系 (Rivers - 碧蓝水体与墨边)
        DrawGuohuaRivers(item, snapshot, visibleRect, screenScale);

        // 5. 绘制古道驿径 (Dashed Trails)
        DrawGuohuaTrails(item, screenScale);

        // All upright bodies are drawn after EVERY ground decal, never interleaved per object.
        foreach (var sprite in _cachedSortedSprites)
            DrawSurfaceSprite(item, sprite, visibleRect, groundPass: false);

        // 5.2 绘制 Map Effects 托尔金山脉背光侧斜向阴影排线 (Tolkien Mountain Hachures)
        foreach (var h in _cachedTolkienHachures)
        {
            item.DrawLine(h.Start, h.End, h.Color, h.Width / screenScale, true);
        }

        // 5.5 群山与天际缭绕的如意祥云（已按需求移除）

        // 6. 绘制书法题名与海域注记 (Calligraphy Labels)
        if (labelFont != null)
        {
            DrawGuohuaLabels(item, labelFont, screenScale, visibleRect);
        }

        // 7. 绘制右下角古典木框图例 (Legend Box)
        DrawLegendBox(item, visibleRect, screenScale);

        // 8. 绘制全景仿古卷轴边框 (Parchment Border)
        DrawScrollFrame(item, visibleRect, screenScale);
    }

    private static void DrawGuohuaRivers(CanvasItem item, WorldSnapshot snapshot, Rect2 visibleRect, float screenScale)
    {
        var cartography = _cachedCartography ?? snapshot.Cartography;
        if (cartography != null)
        {
            var riverStrokes = cartography.Brushes.Where(b => b.Type == BrushType.RiverStroke).ToList();
            if (riverStrokes.Count > 0)
            {
                foreach (var rs in riverStrokes)
                {
                    if (rs.Points == null || rs.Points.Length < 2) continue;

                    var pts = new Vector2[rs.Points.Length];
                    var minX = float.MaxValue; var minY = float.MaxValue;
                    var maxX = float.MinValue; var maxY = float.MinValue;
                    for (var i = 0; i < rs.Points.Length; i++)
                    {
                        var px = (float)rs.Points[i].X;
                        var py = (float)rs.Points[i].Y;
                        pts[i] = new Vector2(px, py);
                        if (px < minX) minX = px;
                        if (py < minY) minY = py;
                        if (px > maxX) maxX = px;
                        if (py > maxY) maxY = py;
                    }

                    var riverBounds = new Rect2(minX, minY, Math.Max(1f, maxX - minX), Math.Max(1f, maxY - minY));
                    if (!visibleRect.Intersects(riverBounds)) continue;

                    var baseWidth = rs.StrokeWidth / screenScale;

                    if (rs.VariantKey == "river_main_stem")
                    {
                        // 主干大江：源头细、河口阔（宽度平滑渐变 0.50x -> 1.65x），分段平滑 Polyline 渲染，杜绝圆点珠串伪影
                        const int runCount = 8;
                        var runLength = Math.Max(2, (pts.Length - 1) / runCount);

                        for (var r = 0; r < runCount; r++)
                        {
                            var startIdx = r * runLength;
                            var endIdx = (r == runCount - 1) ? pts.Length - 1 : Math.Min(pts.Length - 1, (r + 1) * runLength);
                            if (endIdx <= startIdx) break;

                            var count = endIdx - startIdx + 1;
                            var runPts = new Vector2[count];
                            Array.Copy(pts, startIdx, runPts, 0, count);

                            var tMid = ((startIdx + endIdx) * 0.5f) / (pts.Length - 1);
                            var w = Mathf.Lerp(baseWidth * 0.50f, baseWidth * 1.65f, tMid);

                            item.DrawPolyline(runPts, InkRiverBorder, w + 1.8f / screenScale, true);
                            item.DrawPolyline(runPts, InkRiverFill, w, true);
                        }

                        // 圆润修整江源与入海口
                        var wSource = baseWidth * 0.50f;
                        var wMouth = baseWidth * 1.65f;
                        item.DrawCircle(pts[0], (wSource + 1.8f / screenScale) * 0.5f, InkRiverBorder);
                        item.DrawCircle(pts[0], wSource * 0.5f, InkRiverFill);
                        item.DrawCircle(pts[^1], (wMouth + 1.8f / screenScale) * 0.5f, InkRiverBorder);
                        item.DrawCircle(pts[^1], wMouth * 0.5f, InkRiverFill);
                    }
                    else
                    {
                        // 支流与常规水墨笔触：平滑多段线连续绘制
                        item.DrawPolyline(pts, InkRiverBorder, baseWidth + 1.4f / screenScale, true);
                        item.DrawPolyline(pts, InkRiverFill, baseWidth, true);
                    }
                }
            }
        }

        // 绘制平原蛇曲牛轭湖 (Oxbow Lakes)
        foreach (var oxbow in _cachedOxbowLakes)
        {
            if (oxbow.Points.Length < 2) continue;
            var w = oxbow.StrokeWidth / screenScale;
            item.DrawPolyline(oxbow.Points, oxbow.BorderColor, w + 1.8f / screenScale, true);
            item.DrawPolyline(oxbow.Points, oxbow.FillColor, w, true);
            item.DrawCircle(oxbow.Points[0], (w + 1.8f / screenScale) * 0.5f, oxbow.BorderColor);
            item.DrawCircle(oxbow.Points[0], w * 0.5f, oxbow.FillColor);
            item.DrawCircle(oxbow.Points[^1], (w + 1.8f / screenScale) * 0.5f, oxbow.BorderColor);
            item.DrawCircle(oxbow.Points[^1], w * 0.5f, oxbow.FillColor);
        }

        if (cartography != null && cartography.Brushes.Any(b => b.Type == BrushType.RiverStroke))
        {
            return;
        }

        var fields = snapshot.Fields;
        var geom = snapshot.Geometry;
        var downslope = fields.Downslope;
        var flux = fields.Flux;
        var maxFlux = 1f;
        for (var i = 0; i < fields.Count; i++)
        {
            if (flux[i] > maxFlux) maxFlux = flux[i];
        }

        var seaLevel = snapshot.Options.SeaLevel;
        for (var i = 0; i < fields.Count; i++)
        {
            var r = fields.River[i];
            if (r <= 0.05f) continue;
            // 海洋水体内部不绘制河流线
            if (fields.Height[i] <= seaLevel) continue;

            var next = downslope[i];
            if (next < 0 || next >= fields.Count) continue;

            var p1 = new Vector2((float)geom.CentroidX[i], (float)geom.CentroidY[i]);
            var p2 = new Vector2((float)geom.CentroidX[next], (float)geom.CentroidY[next]);

            // 若下一跳已入海，截断在海岸线交汇处，避免硬直线插进深海
            if (fields.Height[next] <= seaLevel)
            {
                p2 = p1 + (p2 - p1) * 0.42f;
            }

            if (!visibleRect.HasPoint(p1) && !visibleRect.HasPoint(p2)) continue;

            var widthRatio = Mathf.Clamp(flux[i] / maxFlux, 0.05f, 1f);
            var riverWidth = (2.2f + widthRatio * 4.8f) / screenScale;

            // 碧蓝水面 + 墨边
            item.DrawLine(p1, p2, InkRiverBorder, riverWidth + 1.2f / screenScale, true);
            item.DrawLine(p1, p2, InkRiverFill, riverWidth, true);
        }
    }

    private static void DrawGuohuaTrails(CanvasItem item, float screenScale)
    {
        var stroke = 1.35f / screenScale;
        foreach (var poly in _cachedTrailPolylines)
        {
            for (var k = 0; k < poly.Length - 1; k++)
            {
                DrawDashedLine(item, poly[k], poly[k + 1], InkTrail, stroke, 4f / screenScale, 3f / screenScale);
            }
        }
    }

    private static void DrawDashedLine(CanvasItem item, Vector2 from, Vector2 to, Color color, float width, float dashLen, float gapLen)
    {
        var diff = to - from;
        var dist = diff.Length();
        if (dist <= 0.001f) return;
        var dir = diff / dist;
        var totalStep = dashLen + gapLen;
        var curr = 0f;

        while (curr < dist)
        {
            var end = Math.Min(curr + dashLen, dist);
            item.DrawLine(from + dir * curr, from + dir * end, color, width, true);
            curr += totalStep;
        }
    }

    private static void DrawGuohuaLabels(CanvasItem item, Font font, float screenScale, Rect2 visibleRect)
    {
        var haloOffset = 1.3f / screenScale;
        Vector2[] haloDirs = { new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f) };

        foreach (var lbl in _cachedLabels)
        {
            if (!visibleRect.HasPoint(lbl.Position)) continue;
            var sz = Mathf.Clamp((int)(lbl.FontSize / screenScale), 9, 24);

            if (lbl.HasHalo)
            {
                foreach (var d in haloDirs)
                {
                    item.DrawString(font, lbl.Position + d * haloOffset, lbl.Text, HorizontalAlignment.Left, -1, sz, InkLabelHalo);
                }
            }

            var fontColor = lbl.IsSea ? InkWaterWave : InkLabelColor;
            item.DrawString(font, lbl.Position, lbl.Text, HorizontalAlignment.Left, -1, sz, fontColor);
        }
    }

    private static void DrawLegendBox(CanvasItem item, Rect2 visibleRect, float screenScale)
    {
        if (_texLegendBox == null) return;
        var boxWidth = 92f;
        var boxHeight = 114f;

        // 放置在右下角
        var margin = 26f;
        var pos = new Vector2(visibleRect.End.X - boxWidth - margin, visibleRect.End.Y - boxHeight - margin);
        var dest = new Rect2(pos, new Vector2(boxWidth, boxHeight));

        // 纸底阴影
        item.DrawRect(new Rect2(pos + new Vector2(3f, 3f), new Vector2(boxWidth, boxHeight)), new Color(0.12f, 0.08f, 0.05f, 0.35f));
        item.DrawTextureRect(_texLegendBox, dest, false);
    }

    private static void DrawScrollFrame(CanvasItem item, Rect2 visibleRect, float screenScale)
    {
        var strokeOuter = 3.2f;
        var strokeInner = 1.4f;
        var insetOuter = 6f;
        var insetInner = 14f;

        var outerRect = new Rect2(visibleRect.Position + Vector2.One * insetOuter, visibleRect.Size - Vector2.One * (insetOuter * 2f));
        var innerRect = new Rect2(visibleRect.Position + Vector2.One * insetInner, visibleRect.Size - Vector2.One * (insetInner * 2f));

        item.DrawRect(outerRect, InkBorderColor, false, strokeOuter);
        item.DrawRect(innerRect, InkBorderColor * 0.85f, false, strokeInner);
    }
}
