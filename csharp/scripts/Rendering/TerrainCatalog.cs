using Godot;
using PlanetGeneration.Core.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PlanetGeneration.Rendering;

/// <summary>
/// 手绘宏观地貌图元分类。
/// </summary>
public enum TerrainCategory
{
    Mountain,       // 青绿群峰（天柱峰/主脊/副峰）
    Hills,          // 连绵丘陵（翠峦/梯田小品/茶亭）
    Plateau,        // 巍峨高原（桌状绝壁/红砂平顶台地）
    Desert,         // 金沙荒漠（新月沙丘/波状沙垄/绿洲胡杨）
    Grassland,      // 辽阔草原（丛生风草/灌木/草甸微丘）
    Wetland,        // 烟雨湿地（蒹葭芦苇/香蒲浅渚/栈桥莲叶）
    Basin,          // 环抱盆地（环形山臂/敞口平野）
    SnowMountain,   // 积雪冰峰（雪帽尖峰/冰川脊线）
    Volcano         // 火山赤峦（玄武岩火山锥/熔岩火山口）
}

/// <summary>
/// 单个手绘宏观地貌图元元数据定义。
/// </summary>
public sealed class TerrainDef
{
    public required int Id { get; init; }
    public required TerrainCategory Category { get; init; }
    public required string SubCategory { get; init; }
    public required string AtlasName { get; init; }
    public required Rect2 Region { get; init; }
    public required Vector2 Pivot { get; init; }
    public required Vector2 NativeSize { get; init; }
    public required Vector2 WorldSize { get; init; }
    public float Aspect { get; init; }
}

/// <summary>
/// 手绘地貌图元目录管理器：负责从 terrain_catalog.json 加载 120+ 种宏观地貌图元并提供智能选取策略。
/// </summary>
public static class TerrainCatalog
{
    private static bool _initialized;
    private static readonly List<TerrainDef> _allSprites = new();
    private static readonly Dictionary<TerrainCategory, List<TerrainDef>> _byCategory = new();
    private static readonly Dictionary<string, List<TerrainDef>> _bySubCategory = new();

    public static IReadOnlyList<TerrainDef> AllSprites => _allSprites;

    static TerrainCatalog()
    {
        EnsureInitialized();
    }

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        foreach (TerrainCategory cat in Enum.GetValues(typeof(TerrainCategory)))
        {
            _byCategory[cat] = new List<TerrainDef>();
        }

        LoadFromJson();
    }

    private static void LoadFromJson()
    {
        const string resPath = "res://resources/textures/guohua/terrain_catalog.json";
        var fallback = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "../../../../csharp/resources/textures/guohua/terrain_catalog.json");

        if (!AssetTextLoader.TryRead(resPath, out var jsonContent, fallback))
        {
            GD.PrintErr($"[TerrainCatalog] 无法读取地貌图集配置: {resPath}");
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;
            if (!root.TryGetProperty("sprites", out var spritesElem)) return;

            var atlasName = root.TryGetProperty("texture", out var texElem) ? texElem.GetString() ?? "mountain_atlas_single.png" : "mountain_atlas_single.png";

            foreach (var item in spritesElem.EnumerateArray())
            {
                var id = item.GetProperty("id").GetInt32();
                var catStr = item.GetProperty("category").GetString();
                if (!Enum.TryParse<TerrainCategory>(catStr, out var cat))
                {
                    cat = TerrainCategory.Mountain;
                }

                var subCat = item.TryGetProperty("sub_category", out var subElem) ? subElem.GetString() ?? "General" : "General";

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

                var spriteAtlas = item.TryGetProperty("atlas", out var aElem) ? aElem.GetString() ?? atlasName : atlasName;
                var aspect = item.TryGetProperty("aspect", out var aspElem) ? aspElem.GetSingle() : (nw / Mathf.Max(nh, 1f));

                var def = new TerrainDef
                {
                    Id = id,
                    Category = cat,
                    SubCategory = subCat,
                    AtlasName = spriteAtlas,
                    Region = new Rect2(rx, ry, rw, rh),
                    Pivot = new Vector2(px, py),
                    NativeSize = new Vector2(nw, nh),
                    WorldSize = new Vector2(ww, wh),
                    Aspect = aspect
                };

                _allSprites.Add(def);
                _byCategory[cat].Add(def);

                if (!_bySubCategory.TryGetValue(subCat, out var subList))
                {
                    subList = new List<TerrainDef>();
                    _bySubCategory[subCat] = subList;
                }
                subList.Add(def);
            }

            GD.Print($"[TerrainCatalog] 成功加载 {_allSprites.Count} 种手绘宏观地貌图元 (山脉:{_byCategory[TerrainCategory.Mountain].Count}, 雪峰:{_byCategory[TerrainCategory.SnowMountain].Count}, 火山:{_byCategory[TerrainCategory.Volcano].Count}, 丘陵:{_byCategory[TerrainCategory.Hills].Count}, 高原:{_byCategory[TerrainCategory.Plateau].Count}, 沙漠:{_byCategory[TerrainCategory.Desert].Count}, 草原:{_byCategory[TerrainCategory.Grassland].Count}, 湿地:{_byCategory[TerrainCategory.Wetland].Count}, 盆地:{_byCategory[TerrainCategory.Basin].Count})");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TerrainCatalog] 解析地貌图元配置文件失败: {ex.Message}");
        }
    }

    public static TerrainDef? Pick(TerrainCategory cat, Random rand)
    {
        EnsureInitialized();
        if (_byCategory.TryGetValue(cat, out var list) && list.Count > 0)
        {
            return list[rand.Next(list.Count)];
        }
        return null;
    }

    public static TerrainDef? Pick(TerrainCategory cat, string subCat, Random rand)
    {
        EnsureInitialized();
        if (_byCategory.TryGetValue(cat, out var list))
        {
            var filtered = list.FindAll(s => string.Equals(s.SubCategory, subCat, StringComparison.OrdinalIgnoreCase));
            if (filtered.Count > 0) return filtered[rand.Next(filtered.Count)];
        }
        return Pick(cat, rand);
    }

    /// <summary>
    /// 选取山脉图元（支持高山雪线自动转雪峰、火山构造自适应）。
    /// </summary>
    public static TerrainDef? PickMountain(Random rand, bool isSnow = false, bool isVolcano = false, bool isMainPeak = true)
    {
        EnsureInitialized();
        if (isVolcano)
        {
            return Pick(TerrainCategory.Volcano, "VolcanoCrater", rand) 
                   ?? Pick(TerrainCategory.Volcano, "VolcanoCone", rand) 
                   ?? Pick(TerrainCategory.Volcano, rand);
        }
        if (isSnow)
        {
            return isMainPeak 
                ? (Pick(TerrainCategory.SnowMountain, "GlacierPeak", rand) ?? Pick(TerrainCategory.SnowMountain, "SnowMassif", rand) ?? Pick(TerrainCategory.SnowMountain, rand))
                : Pick(TerrainCategory.SnowMountain, rand);
        }
        return isMainPeak 
            ? (Pick(TerrainCategory.Mountain, "MainPeak", rand) ?? Pick(TerrainCategory.Mountain, "MassifCluster", rand) ?? Pick(TerrainCategory.Mountain, rand))
            : Pick(TerrainCategory.Mountain, rand);
    }

    /// <summary>
    /// 选取高原断崖台地图元。
    /// </summary>
    public static TerrainDef? PickPlateau(Random rand, bool isEdge = false)
    {
        EnsureInitialized();
        if (isEdge && rand.NextSingle() < 0.65f)
        {
            return Pick(TerrainCategory.Plateau, "CliffPillar", rand) ?? Pick(TerrainCategory.Plateau, rand);
        }
        return Pick(TerrainCategory.Plateau, "PlateauMassif", rand) 
               ?? Pick(TerrainCategory.Plateau, "TableMesa", rand) 
               ?? Pick(TerrainCategory.Plateau, rand);
    }

    /// <summary>
    /// 选取拱卫伴峰图元 (CompanionPeak)。
    /// </summary>
    public static TerrainDef? PickCompanionPeak(Random rand, bool isSnow = false)
    {
        EnsureInitialized();
        if (isSnow)
        {
            return Pick(TerrainCategory.SnowMountain, "SnowCompanionPeak", rand) 
                   ?? Pick(TerrainCategory.SnowMountain, "SnowRidge", rand) 
                   ?? Pick(TerrainCategory.SnowMountain, rand);
        }
        return Pick(TerrainCategory.Mountain, "CompanionPeak", rand) 
               ?? Pick(TerrainCategory.Mountain, rand);
    }

    /// <summary>
    /// 选取连绵横展山脊图元 (Ridge / GrandRange / SnowRidge / SnowGrandRange)。
    /// </summary>
    public static TerrainDef? PickRidge(Random rand, bool isSnow = false)
    {
        EnsureInitialized();
        if (isSnow)
        {
            return Pick(TerrainCategory.SnowMountain, "SnowGrandRange", rand)
                   ?? Pick(TerrainCategory.SnowMountain, "SnowRidge", rand) 
                   ?? Pick(TerrainCategory.SnowMountain, rand);
        }
        return Pick(TerrainCategory.Mountain, "GrandRange", rand)
               ?? Pick(TerrainCategory.Mountain, "Ridge", rand) 
               ?? Pick(TerrainCategory.Mountain, rand);
    }

    /// <summary>
    /// 选取极目远山浅黛图元。
    /// </summary>
    public static TerrainDef? PickFarMountain(Random rand)
    {
        EnsureInitialized();
        if (rand.NextSingle() < 0.55f)
        {
            return Pick(TerrainCategory.Mountain, "CompanionPeak", rand) 
                   ?? Pick(TerrainCategory.Mountain, rand);
        }
        return Pick(TerrainCategory.Hills, "Knoll", rand) 
               ?? Pick(TerrainCategory.Hills, rand);
    }

    /// <summary>
    /// 选取丘陵小品（包含起伏丘陵带/圆丘/茶丘簇等）。
    /// </summary>
    public static TerrainDef? PickHills(Random rand)
    {
        EnsureInitialized();
        return Pick(TerrainCategory.Hills, "HillChain", rand)
               ?? Pick(TerrainCategory.Hills, "Knoll", rand) 
               ?? Pick(TerrainCategory.Hills, "HillCopse", rand) 
               ?? Pick(TerrainCategory.Hills, rand);
    }

    /// <summary>
    /// 选取沙漠图元（绿洲/波浪沙丘/新月沙丘）。
    /// </summary>
    public static TerrainDef? PickDesert(Random rand, bool isOasis = false)
    {
        EnsureInitialized();
        if (isOasis)
        {
            return Pick(TerrainCategory.Desert, "Oasis", rand) ?? Pick(TerrainCategory.Desert, rand);
        }
        return Pick(TerrainCategory.Desert, rand);
    }

    /// <summary>
    /// 选取草原风草/草甸图元（支持指定子类如 Tussock, MeadowPlain, Shrub）。
    /// </summary>
    public static TerrainDef? PickGrassland(Random rand, string? subCat = null)
    {
        EnsureInitialized();
        if (!string.IsNullOrEmpty(subCat))
        {
            return Pick(TerrainCategory.Grassland, subCat, rand) ?? Pick(TerrainCategory.Grassland, rand);
        }
        return Pick(TerrainCategory.Grassland, rand);
    }

    /// <summary>
    /// 选取湿地浅滩/芦苇/香蒲/水渚图元（支持指定子类如 Reeds, Cattails, Islet）。
    /// </summary>
    public static TerrainDef? PickWetland(Random rand, string? subCat = null, bool preferIslet = false)
    {
        EnsureInitialized();
        if (!string.IsNullOrEmpty(subCat))
        {
            return Pick(TerrainCategory.Wetland, subCat, rand) ?? Pick(TerrainCategory.Wetland, rand);
        }
        if (preferIslet)
        {
            return Pick(TerrainCategory.Wetland, "Islet", rand) ?? Pick(TerrainCategory.Wetland, rand);
        }
        return Pick(TerrainCategory.Wetland, "Reeds", rand) ?? Pick(TerrainCategory.Wetland, rand);
    }

    /// <summary>
    /// 选取盆地环山臂图元。
    /// </summary>
    public static TerrainDef? PickBasin(Random rand, bool isRim = true)
    {
        EnsureInitialized();
        if (isRim)
        {
            return Pick(TerrainCategory.Basin, "EncirclingArm", rand) ?? Pick(TerrainCategory.Basin, rand);
        }
        return Pick(TerrainCategory.Basin, rand);
    }
}
