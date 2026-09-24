using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PlanetGeneration.Rendering;

/// <summary>
/// 森林群落几何形态分类。
/// </summary>
public enum ForestClusterShape
{
    CircleOval,     // 圆形 / 椭圆密集块
    LinearStrip,    // 长条形 / 笔直带状
    CurvedS,        // S形 / 蜿蜒波浪
    CurvedC,        // C形 / 半月环抱
    CornerL,        // L形 / 直角拐角
    ForkY,          // Y形分叉 / 三叉汇流
    Dumbbell,       // 双核心哑铃状（中间缩窄）
    HollowRing,     // 中心留白甜甜圈状
    DenseCore,      // 核心大块不规则密林
    SparseEdge,     // 边缘羽化稀疏过渡
    SingleScatter   // 散落孤树 / 微型散丛
}

/// <summary>
/// 单个手绘森林群落切片元数据。
/// </summary>
public sealed class ForestClusterDef
{
    public required int Id { get; init; }
    public required ForestClusterShape Shape { get; init; }
    public required Rect2 Region { get; init; }
    public Vector2 PivotRatio { get; init; } = new(0.5f, 0.85f);
    public required Vector2 NativeSize { get; init; }
    public Vector2 WorldSize { get; init; }
    public int Area { get; init; }
    public float Aspect { get; init; } = 1.0f;
}

/// <summary>
/// 手绘森林群落切片图库目录管理器。
/// </summary>
public static class ForestClusterCatalog
{
    private static bool _initialized;
    private static readonly Dictionary<ForestClusterShape, List<ForestClusterDef>> _clustersByShape = new();
    private static readonly List<ForestClusterDef> _allClusters = new();

    public static IReadOnlyList<ForestClusterDef> AllClusters => _allClusters;

    static ForestClusterCatalog()
    {
        EnsureInitialized();
    }

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        foreach (ForestClusterShape shape in Enum.GetValues(typeof(ForestClusterShape)))
        {
            _clustersByShape[shape] = new List<ForestClusterDef>();
        }

        var loaded = TryLoadFromJson();
        if (!loaded || _allClusters.Count == 0)
        {
            LoadFallbackCatalog();
        }
    }

    private static bool TryLoadFromJson()
    {
        const string resPath = "res://resources/textures/guohua/forest_atlas.json";
        if (!AssetTextLoader.TryRead(resPath, out var jsonContent))
        {
            GD.PushWarning($"[ForestClusterCatalog] 无法读取森林图集配置: {resPath}");
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;
            if (!root.TryGetProperty("clusters", out var clustersElem)) return false;

            foreach (var item in clustersElem.EnumerateArray())
            {
                var id = item.GetProperty("id").GetInt32();
                var shapeStr = item.GetProperty("shape").GetString();
                if (!Enum.TryParse<ForestClusterShape>(shapeStr, out var shape))
                {
                    shape = ForestClusterShape.CircleOval;
                }

                var rectArr = item.GetProperty("rect");
                var rx = rectArr[0].GetSingle();
                var ry = rectArr[1].GetSingle();
                var rw = rectArr[2].GetSingle();
                var rh = rectArr[3].GetSingle();

                var pivot = new Vector2(0.5f, 0.85f);
                if (item.TryGetProperty("pivot", out var pivArr) && pivArr.GetArrayLength() == 2)
                {
                    pivot = new Vector2(pivArr[0].GetSingle(), pivArr[1].GetSingle());
                }

                var area = item.TryGetProperty("area", out var a) ? a.GetInt32() : (int)(rw * rh);
                var aspect = item.TryGetProperty("aspect", out var asp) ? asp.GetSingle() : (rw / Math.Max(rh, 1f));

                var worldSize = new Vector2(40f * aspect, 40f);
                if (item.TryGetProperty("world_size", out var wsArr) && wsArr.GetArrayLength() == 2)
                {
                    worldSize = new Vector2(wsArr[0].GetSingle(), wsArr[1].GetSingle());
                }

                var def = new ForestClusterDef
                {
                    Id = id,
                    Shape = shape,
                    Region = new Rect2(rx, ry, rw, rh),
                    PivotRatio = pivot,
                    NativeSize = new Vector2(rw, rh),
                    WorldSize = worldSize,
                    Area = area,
                    Aspect = aspect
                };

                _allClusters.Add(def);
                _clustersByShape[shape].Add(def);
            }

            return _allClusters.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void LoadFallbackCatalog()
    {
        // 最小保底切片（当 JSON 丢失时防止崩溃）
        var fallbacks = new[]
        {
            new ForestClusterDef { Id = 0, Shape = ForestClusterShape.CircleOval, Region = new Rect2(116, 13, 58, 57), NativeSize = new Vector2(58, 57) },
            new ForestClusterDef { Id = 1, Shape = ForestClusterShape.LinearStrip, Region = new Rect2(293, 13, 111, 32), NativeSize = new Vector2(111, 32) },
            new ForestClusterDef { Id = 2, Shape = ForestClusterShape.ForkY, Region = new Rect2(526, 13, 78, 99), NativeSize = new Vector2(78, 99) },
            new ForestClusterDef { Id = 3, Shape = ForestClusterShape.CornerL, Region = new Rect2(911, 13, 95, 99), NativeSize = new Vector2(95, 99) },
            new ForestClusterDef { Id = 4, Shape = ForestClusterShape.DenseCore, Region = new Rect2(294, 234, 115, 87), NativeSize = new Vector2(115, 87) },
        };

        foreach (var def in fallbacks)
        {
            _allClusters.Add(def);
            _clustersByShape[def.Shape].Add(def);
        }
    }

    /// <summary>
    /// 根据几何形态随机抽取一个簇切片（若指定形态无可用切片则退化查找）。
    /// </summary>
    public static ForestClusterDef? PickCluster(ForestClusterShape shape, Random rand)
    {
        EnsureInitialized();

        if (_clustersByShape.TryGetValue(shape, out var list) && list.Count > 0)
        {
            return list[rand.Next(list.Count)];
        }

        // 退化查找
        if (_clustersByShape.TryGetValue(ForestClusterShape.CircleOval, out var fallback) && fallback.Count > 0)
        {
            return fallback[rand.Next(fallback.Count)];
        }

        return _allClusters.Count > 0 ? _allClusters[rand.Next(_allClusters.Count)] : null;
    }

    /// <summary>
    /// 获取指定形态的全部簇。
    /// </summary>
    public static IReadOnlyList<ForestClusterDef> GetClusters(ForestClusterShape shape)
    {
        EnsureInitialized();
        return _clustersByShape.TryGetValue(shape, out var list) ? list : Array.Empty<ForestClusterDef>();
    }
}
