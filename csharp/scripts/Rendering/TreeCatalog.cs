using Godot;
using PlanetGeneration.Core.Domain;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PlanetGeneration.Rendering;

/// <summary>
/// 手绘树木图元层级类型。
/// </summary>
public enum TreeCategory
{
    Single,     // 单木孤植（多种姿态）
    Duo,        // 双木交柯（二株呼应小品）
    Trio,       // 三木顾盼（三株微型丛林）
    Bush,       // 低矮灌木与地被杂木
    Willow      // 水岸垂柳
}

/// <summary>
/// 树种科属分类。
/// </summary>
public enum TreeFamily
{
    Pine,       // 苍松
    Broadleaf,  // 阔叶（古槐/古榆/枫树）
    Cypress,    // 冷杉/古柏
    Willow,     // 垂柳
    Bush,       // 灌木
    Mixed       // 混交
}

/// <summary>
/// 单个手绘树木图元元数据。
/// </summary>
public sealed class TreeDef
{
    public required int Id { get; init; }
    public required TreeCategory Category { get; init; }
    public required TreeFamily Family { get; init; }
    public required string AtlasName { get; init; }
    public required Rect2 Region { get; init; }
    public required Vector2 Pivot { get; init; }
    public required Vector2 NativeSize { get; init; }
    public required Vector2 WorldSize { get; init; }
    public float Aspect { get; init; }
}

/// <summary>
/// 手绘树木图库目录管理器：负责从 tree_catalog.json 加载 80+ 种手绘单木、双木与三木小品。
/// </summary>
public static class TreeCatalog
{
    private static bool _initialized;
    private static readonly List<TreeDef> _allTrees = new();
    private static readonly Dictionary<TreeCategory, List<TreeDef>> _byCategory = new();
    private static readonly Dictionary<TreeFamily, List<TreeDef>> _byFamily = new();

    public static IReadOnlyList<TreeDef> AllTrees => _allTrees;

    static TreeCatalog()
    {
        EnsureInitialized();
    }

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        foreach (TreeCategory cat in Enum.GetValues(typeof(TreeCategory)))
        {
            _byCategory[cat] = new List<TreeDef>();
        }

        foreach (TreeFamily fam in Enum.GetValues(typeof(TreeFamily)))
        {
            _byFamily[fam] = new List<TreeDef>();
        }

        LoadFromJson();
    }

    private static void LoadFromJson()
    {
        const string resPath = "res://resources/textures/guohua/tree_catalog.json";
        if (!AssetTextLoader.TryRead(resPath, out var jsonContent))
        {
            GD.PrintErr($"[TreeCatalog] 无法读取树木图集配置: {resPath}");
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;
            if (!root.TryGetProperty("sprites", out var spritesElem)) return;

            foreach (var item in spritesElem.EnumerateArray())
            {
                var id = item.GetProperty("id").GetInt32();
                var catStr = item.GetProperty("category").GetString();
                if (!Enum.TryParse<TreeCategory>(catStr, out var cat))
                {
                    cat = TreeCategory.Single;
                }

                var famStr = item.GetProperty("family").GetString();
                if (!Enum.TryParse<TreeFamily>(famStr, out var fam))
                {
                    fam = TreeFamily.Mixed;
                }

                var atlas = item.GetProperty("atlas").GetString() ?? "tree_atlas_single.png";

                var rectArr = item.GetProperty("rect");
                var rx = rectArr[0].GetSingle();
                var ry = rectArr[1].GetSingle();
                var rw = rectArr[2].GetSingle();
                var rh = rectArr[3].GetSingle();

                var pivArr = item.GetProperty("pivot");
                var px = pivArr[0].GetSingle();
                var py = pivArr[1].GetSingle();

                var natArr = item.GetProperty("native_size");
                var nw = natArr[0].GetSingle();
                var nh = natArr[1].GetSingle();

                var wldArr = item.GetProperty("world_size");
                var ww = wldArr[0].GetSingle();
                var wh = wldArr[1].GetSingle();

                var aspect = item.TryGetProperty("aspect", out var aspElem) ? aspElem.GetSingle() : (nw / Mathf.Max(nh, 1f));

                var tree = new TreeDef
                {
                    Id = id,
                    Category = cat,
                    Family = fam,
                    AtlasName = atlas,
                    Region = new Rect2(rx, ry, rw, rh),
                    Pivot = new Vector2(px, py),
                    NativeSize = new Vector2(nw, nh),
                    WorldSize = new Vector2(ww, wh),
                    Aspect = aspect
                };

                _allTrees.Add(tree);
                _byCategory[cat].Add(tree);
                _byFamily[fam].Add(tree);
            }

            GD.Print($"[TreeCatalog] 成功加载 {AllTrees.Count} 种手绘树木图元 (单木:{_byCategory[TreeCategory.Single].Count}, 双木:{_byCategory[TreeCategory.Duo].Count}, 三木:{_byCategory[TreeCategory.Trio].Count}, 灌木:{_byCategory[TreeCategory.Bush].Count}, 垂柳:{_byCategory[TreeCategory.Willow].Count})");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TreeCatalog] 解析树木配置文件失败: {ex.Message}");
        }
    }

    public static TreeDef? PickSingle(Random rand, TreeFamily? family = null)
    {
        EnsureInitialized();
        var list = _byCategory[TreeCategory.Single];
        if (family.HasValue)
        {
            var filtered = list.FindAll(t => t.Family == family.Value);
            if (filtered.Count > 0) return filtered[rand.Next(filtered.Count)];
        }
        return list.Count > 0 ? list[rand.Next(list.Count)] : null;
    }

    public static TreeDef? PickDuo(Random rand, TreeFamily? family = null)
    {
        EnsureInitialized();
        var list = _byCategory[TreeCategory.Duo];
        if (family.HasValue)
        {
            var filtered = list.FindAll(t => t.Family == family.Value);
            if (filtered.Count > 0) return filtered[rand.Next(filtered.Count)];
        }
        return list.Count > 0 ? list[rand.Next(list.Count)] : null;
    }

    public static TreeDef? PickTrio(Random rand, TreeFamily? family = null)
    {
        EnsureInitialized();
        var list = _byCategory[TreeCategory.Trio];
        if (family.HasValue)
        {
            var filtered = list.FindAll(t => t.Family == family.Value);
            if (filtered.Count > 0) return filtered[rand.Next(filtered.Count)];
        }
        return list.Count > 0 ? list[rand.Next(list.Count)] : null;
    }

    public static TreeDef? PickBush(Random rand)
    {
        EnsureInitialized();
        var list = _byCategory[TreeCategory.Bush];
        return list.Count > 0 ? list[rand.Next(list.Count)] : null;
    }

    public static TreeDef? PickWillow(Random rand)
    {
        EnsureInitialized();
        var list = _byCategory[TreeCategory.Willow];
        return list.Count > 0 ? list[rand.Next(list.Count)] : null;
    }

    /// <summary>
    /// 根据生态环境与地块空间语境智能推选具有纯正林相的手绘树木。
    /// </summary>
    public static TreeDef? PickForContext(
        BiomeType biome,
        bool isCoastal,
        bool nearRiver,
        int landDepth,
        Random rand)
    {
        EnsureInitialized();

        // 1. 水岸近河：自适应推选水岸垂柳（单木或双木）
        if (nearRiver && rand.NextSingle() < 0.50f)
        {
            var willowDuo = PickDuo(rand, TreeFamily.Willow);
            if (willowDuo != null && rand.NextSingle() < 0.60f) return willowDuo;
            var willowSingle = PickWillow(rand);
            if (willowSingle != null) return willowSingle;
        }

        // 2. 根据生物群系确定主导科属（林相统一）
        var isConiferBiome = biome is BiomeType.Taiga or BiomeType.BorealForest or BiomeType.Tundra;
        var dominantFamily = isConiferBiome
            ? (rand.NextSingle() < 0.75f ? TreeFamily.Pine : TreeFamily.Cypress)
            : TreeFamily.Broadleaf;

        if (biome == BiomeType.TemperateRainForest && rand.NextSingle() < 0.35f)
        {
            dominantFamily = TreeFamily.Pine;
        }

        // 3. 沿海第一线（isCoastal 或 landDepth <= 0）：严格只选单木或灌木，绝不下海、不放大丛林
        if (isCoastal || landDepth <= 0)
        {
            if (rand.NextSingle() < 0.35f)
            {
                return PickBush(rand);
            }
            return PickSingle(rand, dominantFamily);
        }

        // 4. 内陆核心腹地（landDepth >= 2）：
        if (landDepth >= 2)
        {
            if (isConiferBiome || dominantFamily == TreeFamily.Pine || dominantFamily == TreeFamily.Cypress)
            {
                var roll = rand.NextSingle();
                if (roll < 0.55f) return PickTrio(rand, dominantFamily);
                if (roll < 0.85f) return PickDuo(rand, dominantFamily);
                return PickSingle(rand, dominantFamily);
            }
            else
            {
                // 阔叶林群落（温带/热带季雨林）：以茂盛林海连绵为主，辅以伴生林丛与孤植
                var roll = rand.NextSingle();
                if (roll < 0.60f) return PickTrio(rand, TreeFamily.Broadleaf) ?? PickTrio(rand, TreeFamily.Mixed);
                if (roll < 0.88f) return PickDuo(rand, TreeFamily.Broadleaf) ?? PickDuo(rand, TreeFamily.Mixed);
                return PickSingle(rand, TreeFamily.Broadleaf);
            }
        }

        // 5. 次沿海过渡带（landDepth == 1）：
        {
            var roll = rand.NextSingle();
            if (roll < 0.55f) return PickDuo(rand, dominantFamily) ?? PickDuo(rand, TreeFamily.Mixed);
            if (roll < 0.88f) return PickSingle(rand, dominantFamily);
            return PickBush(rand);
        }
    }
}
