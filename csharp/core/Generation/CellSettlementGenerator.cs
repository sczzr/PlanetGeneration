using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Generation;

/// <summary>
/// 地块聚落选址与生成器（纯 C#，无 Godot 依赖）。
/// 直接在地块图上根据地形、水文、气候及空间排斥度选址。
/// </summary>
public static class CellSettlementGenerator
{
    private static readonly string[] Prefixes =
    {
        "艾", "诺", "维", "奥", "罗", "卡", "塞", "达", "莫", "莱",
        "特", "伊", "加", "菲", "贝", "安", "索", "瓦", "利", "格",
        "温", "兰", "杜", "巴", "克", "米", "赞", "吉", "哈", "拉"
    };

    private static readonly string[] Middles =
    {
        "尔", "斯", "达", "林", "恩", "克", "拉", "特", "亚", "玛",
        "文", "德", "姆", "洛", "隆", "加", "纳", "佩", "雷", "塞"
    };

    private static readonly string[] Suffixes =
    {
        "堡", "港", "城", "顿", "利亚", "威尔", "萨斯", "克", "斯",
        "都", "邦", "原", "岗", "镇", "境", "郡", "谷", "林", "海", "湾"
    };

    public static List<SettlementInfo> Generate(
        CellGeometry geometry,
        CellFields fields,
        int seed,
        float seaLevel,
        int minCount = 12,
        int maxCount = 40)
    {
        var count = geometry.Count;
        var rng = new PolygonRandom((ulong)seed ^ 0x3C6EF372UL);

        // 1. 评估陆地地块聚落适宜度
        var candidates = new List<(int CellId, float Score)>(count / 4);
        var height = fields.Height;
        var moisture = fields.Moisture;
        var river = fields.River;
        var biome = fields.Biome;

        for (var cell = 0; cell < count; cell++)
        {
            var h = height[cell];
            if (h <= seaLevel) continue;

            var b = (BiomeType)biome[cell];
            if (b is BiomeType.Ocean or BiomeType.ShallowOcean or BiomeType.Ice or BiomeType.SnowyMountain)
            {
                continue;
            }

            var nearOcean = false;
            var nearRiver = river[cell] > 0.05f;

            var start = geometry.CellNeighborStart[cell];
            var end = geometry.CellNeighborStart[cell + 1];
            for (var k = start; k < end; k++)
            {
                var neighbor = geometry.CellNeighbors[k];
                if (height[neighbor] <= seaLevel) nearOcean = true;
                if (river[neighbor] > 0.05f) nearRiver = true;
            }

            var m = moisture[cell];
            var moistureScore = 1f - Math.Abs(m - 0.55f) * 1.8f;
            var heightScore = 1f - Math.Abs(h - (seaLevel + 0.12f)) * 2.2f;

            var biomeScore = b switch
            {
                BiomeType.Coastland => 1.4f,
                BiomeType.Grassland => 1.35f,
                BiomeType.TemperateSeasonalForest => 1.25f,
                BiomeType.Savanna => 1.15f,
                BiomeType.TropicalSeasonalForest => 1.1f,
                BiomeType.Steppe => 0.95f,
                BiomeType.BorealForest => 0.85f,
                BiomeType.Taiga => 0.8f,
                BiomeType.TropicalDesert => 0.35f,
                BiomeType.RockyMountain => 0.4f,
                _ => 0.8f
            };

            var waterBonus = (nearRiver ? 0.35f : 0f) + (nearOcean ? 0.30f : 0f);
            var jitter = (float)(rng.NextDouble() * 0.15d);

            var totalScore = (moistureScore * 0.35f) + (heightScore * 0.25f) + (biomeScore * 0.25f) + waterBonus + jitter;
            if (totalScore > 0.6f)
            {
                candidates.Add((cell, totalScore));
            }
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

        // 2. 空间排斥度选址
        var targetCount = Math.Clamp((int)Math.Round(count * 0.0035f), minCount, maxCount);
        var selected = new List<SettlementInfo>(targetCount);
        var selectedCellIds = new HashSet<int>();
        var minSpacing = geometry.SpacingX * 2.5d;
        var minSpacingSq = minSpacing * minSpacing;

        var nameRng = new PolygonRandom((ulong)seed ^ 0x9E3779B1UL);

        for (var i = 0; i < candidates.Count && selected.Count < targetCount; i++)
        {
            var cand = candidates[i];
            var site = geometry.GetSite(cand.CellId);

            var tooClose = false;
            foreach (var existing in selected)
            {
                var dsq = geometry.Extent.DistanceSquaredWrapped(site, existing.Position);
                if (dsq < minSpacingSq)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose) continue;

            selectedCellIds.Add(cand.CellId);
            var rank = selected.Count switch
            {
                < 4 => SettlementRank.CityState,
                < 12 => SettlementRank.Town,
                _ => SettlementRank.Hamlet,
            };

            selected.Add(new SettlementInfo
            {
                CellId = cand.CellId,
                Name = GenerateCityName(ref nameRng),
                Score = cand.Score,
                Rank = rank,
                Position = site,
            });
        }

        // 3. 将城市归属写回 CellFields.CityId
        for (var i = 0; i < selected.Count; i++)
        {
            fields.CityId[selected[i].CellId] = i;
        }

        return selected;
    }

    private static string GenerateCityName(ref PolygonRandom rng)
    {
        var prefix = Prefixes[(int)(rng.NextUInt64() % (ulong)Prefixes.Length)];
        var middle = (rng.NextDouble() > 0.45)
            ? Middles[(int)(rng.NextUInt64() % (ulong)Middles.Length)]
            : string.Empty;
        var suffix = Suffixes[(int)(rng.NextUInt64() % (ulong)Suffixes.Length)];
        return prefix + middle + suffix;
    }
}
