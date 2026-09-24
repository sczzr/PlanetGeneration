using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Simulation;

/// <summary>
/// 大型自然生态地貌分析器与宏观裁决引擎 (MegaTerrainAnalyzer)
///
/// 遵循核心准则：
/// 1. 基础地形覆盖全球（Layer 0），大型生态地貌只标识少数具有巨大规模和地理意义的区域（Layer 1）；
/// 2. 严禁逐 Cell 贴图，执行“连通图聚合 -> 形态学指标打分 -> Top-N 配额竞争 -> 边界平滑去网格化”；
/// 3. 线状山脉基于脊线骨架（Mountain Spine）驱动，面状地貌基于连续平滑 Mask 与 4 层深度渐变驱动；
/// 4. 实施优先级消歧（山脉 > 高原/盆地 > 沙漠 > 森林 > 丘陵 > 草原/湿地）与层级附着关系。
/// </summary>
public static class MegaTerrainAnalyzer
{
    private static readonly string[] MountainNames =
    {
        "苍冥群岳", "太虚绝岭", "昆仑神脉", "断云巨障", "天柱山脉",
        "九嶷龙脊", "赤霞天险", "幽寒雪峰", "凌霄崇峦", "万仞崇山",
        "青峰古脉", "回雁绝嶂", "玉龙冰脊", "北极玄岭"
    };

    private static readonly string[] ForestNames =
    {
        "大荒林海", "青木古森", "碧落密林", "苍溟翠障", "若木古泽林",
        "千叶神木林", "玄夜原始林", "青藤古幽海", "迷雾青海", "朝露幽篁"
    };

    private static readonly string[] DesertNames =
    {
        "赤炎瀚海", "狂沙绝境", "焚天赤漠", "流沙荒原", "焦土旷野",
        "沉沙古海", "金精瀚海", "伏龙沙海"
    };

    private static readonly string[] PlateauNames =
    {
        "万仞高原", "昆仑玄天台", "太微古塬", "极地冻土塬", "苍茫瀚塬", "凌霄重台"
    };

    private static readonly string[] BasinNames =
    {
        "九泽大盆地", "灵枢陷谷", "太和沉渊", "万泉巨盆", "洞天深壑"
    };

    private static readonly string[] HillNames =
    {
        "青岚连丘", "翠微叠嶂", "罗霄连冈", "沧溟浅冈", "秋月层峦"
    };

    private static readonly string[] GrasslandNames =
    {
        "瀚海青野", "苍苍莽原", "天苍大原", "风牧原野", "回风碧野"
    };

    private static readonly string[] WetlandNames =
    {
        "大荒云梦泽", "瀚海古泽", "九渊沉泽", "白萍灵沼", "碧水涵幽泽"
    };

    /// <summary>
    /// 对世界快照执行大型宏观地貌分析，输出裁决通过的世界级 MegaTerrainRegion 列表。
    /// </summary>
    public static IReadOnlyList<MegaTerrainRegion> Analyze(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options)
    {
        var count = geometry.Count;
        var seaLevel = options.SeaLevel;

        // 1. 统计全图陆地总面积
        var totalLandArea = 0.0;
        for (var i = 0; i < count; i++)
        {
            if (fields.Height[i] > seaLevel)
            {
                totalLandArea += geometry.Area[i];
            }
        }
        if (totalLandArea <= 1e-5) return Array.Empty<MegaTerrainRegion>();

        var rand = new Random((int)(options.Seed ^ 0x4d656761)); // "Mega"
        var candidatesByType = new Dictionary<MegaTerrainType, List<CandidateRegion>>();

        // 2. 依次聚集各类连通块
        candidatesByType[MegaTerrainType.MegaMountain] = DetectMountainCandidates(geometry, fields, seaLevel, totalLandArea);
        candidatesByType[MegaTerrainType.MegaPlateau] = DetectArealCandidates(geometry, fields, MegaTerrainType.MegaPlateau, c => fields.Landform[c] == (byte)LandformType.Plateau, totalLandArea, seaLevel);
        candidatesByType[MegaTerrainType.MegaBasin] = DetectArealCandidates(geometry, fields, MegaTerrainType.MegaBasin, c => fields.Landform[c] == (byte)LandformType.Basin || fields.Landform[c] == (byte)LandformType.DryBasin, totalLandArea, seaLevel);
        candidatesByType[MegaTerrainType.MegaDesert] = DetectArealCandidates(geometry, fields, MegaTerrainType.MegaDesert, c => fields.Height[c] > seaLevel && (fields.Biome[c] == (byte)BiomeType.TropicalDesert || fields.Biome[c] == (byte)BiomeType.TemperateDesert || fields.Landform[c] == (byte)LandformType.DesertDune || fields.Landform[c] == (byte)LandformType.Badlands), totalLandArea, seaLevel);
        candidatesByType[MegaTerrainType.MegaForest] = DetectArealCandidates(geometry, fields, MegaTerrainType.MegaForest, c => fields.Height[c] > seaLevel && IsForestBiome((BiomeType)fields.Biome[c]), totalLandArea, seaLevel);
        candidatesByType[MegaTerrainType.MegaHills] = DetectArealCandidates(geometry, fields, MegaTerrainType.MegaHills, c => fields.Landform[c] == (byte)LandformType.Hill || fields.Landform[c] == (byte)LandformType.Karst, totalLandArea, seaLevel);
        candidatesByType[MegaTerrainType.MegaGrassland] = DetectArealCandidates(geometry, fields, MegaTerrainType.MegaGrassland, c => fields.Height[c] > seaLevel && IsGrassBiome((BiomeType)fields.Biome[c]), totalLandArea, seaLevel);
        candidatesByType[MegaTerrainType.MegaWetland] = DetectArealCandidates(geometry, fields, MegaTerrainType.MegaWetland, c => fields.Height[c] > seaLevel && (fields.Landform[c] == (byte)LandformType.Wetland || (fields.Moisture[c] > 0.70f && fields.River[c] > 0.03f && fields.Height[c] < seaLevel + 0.20f)), totalLandArea, seaLevel);

        // 3. 按类型配额竞争筛选 Top N
        var quota = new Dictionary<MegaTerrainType, int>
        {
            [MegaTerrainType.MegaMountain] = 6,
            [MegaTerrainType.MegaPlateau] = 3,
            [MegaTerrainType.MegaBasin] = 3,
            [MegaTerrainType.MegaDesert] = 3,
            [MegaTerrainType.MegaForest] = 4,
            [MegaTerrainType.MegaHills] = 4,
            [MegaTerrainType.MegaGrassland] = 4,
            [MegaTerrainType.MegaWetland] = 2,
        };

        var selectedCandidates = new List<CandidateRegion>();
        foreach (var (type, candidates) in candidatesByType)
        {
            var maxCount = quota[type];
            candidates.Sort((a, b) => b.MegaScore.CompareTo(a.MegaScore));
            var taken = candidates.Take(maxCount).Where(c => c.Rank >= MegaRegionRank.MajorRegion).ToList();
            selectedCandidates.AddRange(taken);
        }

        // 4. 层级消歧与附着处理 (山脉 > 高原/盆地 > 沙漠 > 森林 > 丘陵 > 草原/湿地)
        var priorityOrder = new[]
        {
            MegaTerrainType.MegaMountain,
            MegaTerrainType.MegaPlateau,
            MegaTerrainType.MegaBasin,
            MegaTerrainType.MegaDesert,
            MegaTerrainType.MegaForest,
            MegaTerrainType.MegaHills,
            MegaTerrainType.MegaGrassland,
            MegaTerrainType.MegaWetland,
        };

        var cellOwner = new int[count];
        Array.Fill(cellOwner, -1);

        var finalRegions = new List<MegaTerrainRegion>();
        var regionIdCounter = 1;

        var nameIndices = new Dictionary<MegaTerrainType, int>();
        foreach (var t in Enum.GetValues<MegaTerrainType>()) nameIndices[t] = 0;

        foreach (var type in priorityOrder)
        {
            var typeCandidates = selectedCandidates.Where(c => c.Type == type).ToList();
            foreach (var cand in typeCandidates)
            {
                var validCells = new List<int>(cand.Cells.Length);
                var attachedParentId = -1;

                for (var i = 0; i < cand.Cells.Length; i++)
                {
                    var c = cand.Cells[i];
                    var owner = cellOwner[c];
                    if (owner < 0)
                    {
                        validCells.Add(c);
                    }
                    else if (attachedParentId < 0)
                    {
                        attachedParentId = owner;
                    }
                }

                // 若被高层级地貌压制削减后不足原先 40% 或不足 5 个 Cell，则弃权
                if (validCells.Count < 5 || (validCells.Count / (float)cand.Cells.Length) < 0.38f)
                {
                    continue;
                }

                var regionId = regionIdCounter++;
                foreach (var c in validCells)
                {
                    cellOwner[c] = regionId;
                }

                // 重新计算削减后的面积与多边形轮廓
                var finalCells = validCells.ToArray();
                var area = 0.0;
                var sumX = 0.0;
                var sumY = 0.0;
                var minX = double.MaxValue;
                var minY = double.MaxValue;
                var maxX = double.MinValue;
                var maxY = double.MinValue;

                for (var i = 0; i < finalCells.Length; i++)
                {
                    var c = finalCells[i];
                    var ca = geometry.Area[c];
                    area += ca;
                    var cx = geometry.CentroidX[c];
                    var cy = geometry.CentroidY[c];
                    sumX += cx * ca;
                    sumY += cy * ca;

                    if (cx < minX) minX = cx;
                    if (cy < minY) minY = cy;
                    if (cx > maxX) maxX = cx;
                    if (cy > maxY) maxY = cy;
                }

                var centroid = area > 0d ? new PolyVec2(sumX / area, sumY / area) : cand.Centroid;
                var smoothedPolys = ExtractSmoothedBoundaries(geometry, finalCells);

                var namePool = GetNamePool(type);
                var nIdx = nameIndices[type] % namePool.Length;
                nameIndices[type]++;
                var regionName = namePool[nIdx];

                var region = new MegaTerrainRegion
                {
                    Id = regionId,
                    Type = type,
                    Rank = cand.Rank,
                    Name = regionName,
                    Cells = finalCells,
                    TotalArea = area,
                    AreaShare = (float)(area / totalLandArea),
                    Centroid = centroid,
                    BoundsMin = new PolyVec2(minX, minY),
                    BoundsMax = new PolyVec2(maxX, maxY),
                    Compactness = cand.Compactness,
                    Continuity = cand.Continuity,
                    MainDirectionAngle = cand.MainDirectionAngle,
                    Spine = cand.Spine,
                    ElevationMean = cand.ElevationMean,
                    ElevationVariance = cand.ElevationVariance,
                    MoistureMean = cand.MoistureMean,
                    MegaScore = cand.MegaScore,
                    SmoothedPolygons = smoothedPolys,
                    ParentRegionId = attachedParentId,
                };

                if (attachedParentId > 0)
                {
                    var parent = finalRegions.FirstOrDefault(r => r.Id == attachedParentId);
                    parent?.ChildRegionIds.Add(regionId);
                }

                finalRegions.Add(region);
            }
        }

        return finalRegions;
    }

    private static bool IsForestBiome(BiomeType b) =>
        b is BiomeType.TemperateRainForest
          or BiomeType.TemperateSeasonalForest
          or BiomeType.Taiga
          or BiomeType.BorealForest
          or BiomeType.TropicalRainForest
          or BiomeType.TropicalSeasonalForest;

    private static bool IsGrassBiome(BiomeType b) =>
        b is BiomeType.Grassland or BiomeType.Steppe or BiomeType.Savanna;

    private static string[] GetNamePool(MegaTerrainType type) => type switch
    {
        MegaTerrainType.MegaMountain => MountainNames,
        MegaTerrainType.MegaForest => ForestNames,
        MegaTerrainType.MegaDesert => DesertNames,
        MegaTerrainType.MegaPlateau => PlateauNames,
        MegaTerrainType.MegaBasin => BasinNames,
        MegaTerrainType.MegaHills => HillNames,
        MegaTerrainType.MegaGrassland => GrasslandNames,
        MegaTerrainType.MegaWetland => WetlandNames,
        _ => MountainNames
    };

    private sealed class CandidateRegion
    {
        public required MegaTerrainType Type { get; init; }
        public required MegaRegionRank Rank { get; init; }
        public required int[] Cells { get; init; }
        public required double TotalArea { get; init; }
        public required float AreaShare { get; init; }
        public required PolyVec2 Centroid { get; init; }
        public float Compactness { get; init; }
        public float Continuity { get; init; }
        public float MainDirectionAngle { get; init; }
        public IReadOnlyList<MountainSpineNode>? Spine { get; init; }
        public float ElevationMean { get; init; }
        public float ElevationVariance { get; init; }
        public float MoistureMean { get; init; }
        public float MegaScore { get; init; }
    }

    /// <summary>
    /// 检测线状山脉候选区域与脊线骨架。
    /// </summary>
    private static List<CandidateRegion> DetectMountainCandidates(
        CellGeometry geometry,
        CellFields fields,
        float seaLevel,
        double totalLandArea)
    {
        var count = geometry.Count;
        var isMountainCell = new bool[count];
        for (var i = 0; i < count; i++)
        {
            var h = fields.Height[i];
            var landform = (LandformType)fields.Landform[i];
            isMountainCell[i] = (landform is LandformType.Mountain or LandformType.Peak or LandformType.Volcano || h > seaLevel + 0.32f)
                                && landform != LandformType.Plateau;
        }

        var components = FindConnectedComponents(geometry, isMountainCell);
        var candidates = new List<CandidateRegion>();

        foreach (var comp in components)
        {
            if (comp.Count < 4) continue;

            var area = 0.0;
            var sumX = 0.0;
            var sumY = 0.0;
            var sumElev = 0f;

            for (var k = 0; k < comp.Count; k++)
            {
                var c = comp[k];
                var a = geometry.Area[c];
                area += a;
                sumX += geometry.CentroidX[c] * a;
                sumY += geometry.CentroidY[c] * a;
                sumElev += fields.Height[c];
            }

            var areaShare = (float)(area / totalLandArea);
            var centroid = new PolyVec2(sumX / area, sumY / area);
            var avgElev = sumElev / comp.Count;

            // 提取主脊线 (Spine Traversal via Graph Diameter)
            var spine = ExtractMountainSpine(geometry, fields, comp);
            var spineLength = ComputeSpineLength(spine);

            // 山脉不仅看面积，更重连续性与脊线长度 (MountainScore)
            var lenNorm = (float)Math.Clamp(spineLength / (geometry.Width * 0.22), 0.0, 1.0);
            var continuity = ComputeGraphContinuity(geometry, comp);
            var heightNorm = Math.Clamp((avgElev - seaLevel) / 0.40f, 0f, 1f);
            var avgWidth = spineLength > 1e-4 ? (float)(area / spineLength) : 20f;
            var widthNorm = (float)Math.Clamp(avgWidth / (geometry.Width * 0.05), 0.0, 1.0);

            var score = (lenNorm * 0.45f) + (continuity * 0.30f) + (heightNorm * 0.15f) + (widthNorm * 0.10f);

            // 评级
            var rank = MegaRegionRank.LocalMinor;
            if (score >= 0.70f || lenNorm >= 0.55f || areaShare >= 0.05f) rank = MegaRegionRank.WorldLandmark;
            else if (score >= 0.48f || lenNorm >= 0.32f || areaShare >= 0.025f) rank = MegaRegionRank.MegaRegion;
            else if (score >= 0.30f || lenNorm >= 0.16f || areaShare >= 0.01f) rank = MegaRegionRank.MajorRegion;

            candidates.Add(new CandidateRegion
            {
                Type = MegaTerrainType.MegaMountain,
                Rank = rank,
                Cells = comp.ToArray(),
                TotalArea = area,
                AreaShare = areaShare,
                Centroid = centroid,
                Compactness = 0.2f, // 山脉天然细长
                Continuity = continuity,
                MainDirectionAngle = ComputeSpineAngle(spine),
                Spine = spine,
                ElevationMean = avgElev,
                ElevationVariance = 0.08f,
                MoistureMean = 0.4f,
                MegaScore = score
            });
        }

        return candidates;
    }

    /// <summary>
    /// 检测面状宏观地貌候选（高原、盆地、森林、沙漠、丘陵等）。
    /// </summary>
    private static List<CandidateRegion> DetectArealCandidates(
        CellGeometry geometry,
        CellFields fields,
        MegaTerrainType type,
        Predicate<int> predicate,
        double totalLandArea,
        float seaLevel)
    {
        var count = geometry.Count;
        var mask = new bool[count];
        for (var i = 0; i < count; i++)
        {
            mask[i] = predicate(i);
        }

        var components = FindConnectedComponents(geometry, mask);
        var candidates = new List<CandidateRegion>();

        foreach (var comp in components)
        {
            if (comp.Count < 5) continue;

            var area = 0.0;
            var sumX = 0.0;
            var sumY = 0.0;
            var sumElev = 0f;
            var sumMoist = 0f;

            for (var k = 0; k < comp.Count; k++)
            {
                var c = comp[k];
                var a = geometry.Area[c];
                area += a;
                sumX += geometry.CentroidX[c] * a;
                sumY += geometry.CentroidY[c] * a;
                sumElev += fields.Height[c];
                sumMoist += fields.Moisture[c];
            }

            var areaShare = (float)(area / totalLandArea);
            var centroid = new PolyVec2(sumX / area, sumY / area);
            var avgElev = sumElev / comp.Count;
            var avgMoist = sumMoist / comp.Count;

            var perimeter = EstimatePerimeter(geometry, comp);
            var compactness = perimeter > 1e-4 ? (float)Math.Clamp((4.0 * Math.PI * area) / (perimeter * perimeter), 0.0, 1.0) : 0.5f;
            var continuity = ComputeGraphContinuity(geometry, comp);

            // 一致性
            var varElev = 0f;
            var varMoist = 0f;
            for (var k = 0; k < comp.Count; k++)
            {
                var c = comp[k];
                var de = fields.Height[c] - avgElev;
                var dm = fields.Moisture[c] - avgMoist;
                varElev += de * de;
                varMoist += dm * dm;
            }
            varElev /= comp.Count;
            varMoist /= comp.Count;

            var areaScore = Math.Clamp(areaShare / 0.06f, 0f, 1f);
            var consistencyScore = 1f - Math.Clamp((type == MegaTerrainType.MegaPlateau ? varElev * 15f : varMoist * 8f), 0f, 0.8f);

            var megaScore = (areaScore * 0.40f)
                          + (continuity * 0.20f)
                          + (compactness * 0.15f)
                          + (consistencyScore * 0.15f)
                          + (Math.Clamp((float)comp.Count / 50f, 0f, 1f) * 0.10f);

            var rank = MegaRegionRank.LocalMinor;
            if (areaShare >= 0.065f && megaScore >= 0.65f) rank = MegaRegionRank.WorldLandmark;
            else if (areaShare >= 0.025f && megaScore >= 0.45f) rank = MegaRegionRank.MegaRegion;
            else if (areaShare >= 0.008f && megaScore >= 0.28f) rank = MegaRegionRank.MajorRegion;

            candidates.Add(new CandidateRegion
            {
                Type = type,
                Rank = rank,
                Cells = comp.ToArray(),
                TotalArea = area,
                AreaShare = areaShare,
                Centroid = centroid,
                Compactness = compactness,
                Continuity = continuity,
                MainDirectionAngle = 0f,
                Spine = null,
                ElevationMean = avgElev,
                ElevationVariance = varElev,
                MoistureMean = avgMoist,
                MegaScore = megaScore
            });
        }

        return candidates;
    }

    /// <summary>
    /// 利用 CSR 图邻接关系进行连通分量聚集 (BFS Flood-Fill)。
    /// </summary>
    private static List<List<int>> FindConnectedComponents(CellGeometry geometry, bool[] predicate)
    {
        var count = geometry.Count;
        var visited = new bool[count];
        var components = new List<List<int>>();
        var queue = new Queue<int>();

        for (var i = 0; i < count; i++)
        {
            if (visited[i] || !predicate[i]) continue;
            visited[i] = true;

            var comp = new List<int>();
            queue.Enqueue(i);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                comp.Add(cur);

                var start = geometry.CellNeighborStart[cur];
                var end = geometry.CellNeighborStart[cur + 1];
                for (var k = start; k < end; k++)
                {
                    var nb = geometry.CellNeighbors[k];
                    if (!visited[nb] && predicate[nb])
                    {
                        visited[nb] = true;
                        queue.Enqueue(nb);
                    }
                }
            }

            components.Add(comp);
        }

        return components;
    }

    /// <summary>
    /// 提取山脉子图图直径，生成连续山脉脊线 (Spine)。
    /// </summary>
    private static List<MountainSpineNode> ExtractMountainSpine(
        CellGeometry geometry,
        CellFields fields,
        List<int> cells)
    {
        if (cells.Count == 0) return new List<MountainSpineNode>();
        if (cells.Count <= 2)
        {
            return cells.Select(c => new MountainSpineNode(
                new PolyVec2(geometry.CentroidX[c], geometry.CentroidY[c]),
                fields.Height[c],
                (float)Math.Sqrt(geometry.Area[c] / Math.PI) * 2f,
                c)).ToList();
        }

        var cellSet = new HashSet<int>(cells);

        // 第一次 BFS 寻找最远端点 A
        var startNode = cells[0];
        var nodeA = FindFarthestNode(geometry, cellSet, startNode, out _);

        // 第二次 BFS 从 A 出发寻找最远端点 B，并记录父节点路径
        var nodeB = FindFarthestNode(geometry, cellSet, nodeA, out var parentMap);

        // 回溯出 A -> B 的最短路径（即图直径）
        var path = new List<int>();
        var curr = nodeB;
        while (curr != -1)
        {
            path.Add(curr);
            if (curr == nodeA) break;
            if (!parentMap.TryGetValue(curr, out curr)) break;
        }
        path.Reverse();

        var spineNodes = new List<MountainSpineNode>(path.Count);
        foreach (var c in path)
        {
            var pos = new PolyVec2(geometry.CentroidX[c], geometry.CentroidY[c]);
            var elev = fields.Height[c];
            var width = (float)Math.Sqrt(geometry.Area[c] / Math.PI) * 2f;
            spineNodes.Add(new MountainSpineNode(pos, elev, width, c));
        }

        return spineNodes;
    }

    private static int FindFarthestNode(
        CellGeometry geometry,
        HashSet<int> cellSet,
        int startNode,
        out Dictionary<int, int> parentMap)
    {
        parentMap = new Dictionary<int, int> { [startNode] = -1 };
        var distMap = new Dictionary<int, int> { [startNode] = 0 };
        var queue = new Queue<int>();
        queue.Enqueue(startNode);

        var farthestNode = startNode;
        var maxDist = 0;

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            var d = distMap[cur];
            if (d > maxDist)
            {
                maxDist = d;
                farthestNode = cur;
            }

            var start = geometry.CellNeighborStart[cur];
            var end = geometry.CellNeighborStart[cur + 1];
            for (var k = start; k < end; k++)
            {
                var nb = geometry.CellNeighbors[k];
                if (cellSet.Contains(nb) && !distMap.ContainsKey(nb))
                {
                    distMap[nb] = d + 1;
                    parentMap[nb] = cur;
                    queue.Enqueue(nb);
                }
            }
        }

        return farthestNode;
    }

    private static double ComputeSpineLength(IReadOnlyList<MountainSpineNode> spine)
    {
        if (spine.Count < 2) return 0.0;
        var len = 0.0;
        for (var i = 0; i < spine.Count - 1; i++)
        {
            len += spine[i].Position.DistanceTo(spine[i + 1].Position);
        }
        return len;
    }

    private static float ComputeSpineAngle(IReadOnlyList<MountainSpineNode> spine)
    {
        if (spine.Count < 2) return 0f;
        var p0 = spine[0].Position;
        var p1 = spine[^1].Position;
        return (float)Math.Atan2(p1.Y - p0.Y, p1.X - p0.X);
    }

    private static float ComputeGraphContinuity(CellGeometry geometry, List<int> comp)
    {
        if (comp.Count <= 1) return 1f;
        var cellSet = new HashSet<int>(comp);
        var interiorCells = 0;

        foreach (var c in comp)
        {
            var start = geometry.CellNeighborStart[c];
            var end = geometry.CellNeighborStart[c + 1];
            var allIn = true;
            for (var k = start; k < end; k++)
            {
                if (!cellSet.Contains(geometry.CellNeighbors[k]))
                {
                    allIn = false;
                    break;
                }
            }
            if (allIn) interiorCells++;
        }

        return (float)interiorCells / comp.Count;
    }

    private static double EstimatePerimeter(CellGeometry geometry, List<int> comp)
    {
        var cellSet = new HashSet<int>(comp);
        var perimeter = 0.0;

        foreach (var c in comp)
        {
            var approxRadius = Math.Sqrt(geometry.Area[c] / Math.PI);
            var edgeApprox = approxRadius * 1.15;

            var start = geometry.CellNeighborStart[c];
            var end = geometry.CellNeighborStart[c + 1];
            for (var k = start; k < end; k++)
            {
                var nb = geometry.CellNeighbors[k];
                if (!cellSet.Contains(nb))
                {
                    perimeter += edgeApprox;
                }
            }
        }

        return perimeter;
    }

    /// <summary>
    /// 提取区域连通多边形的外轮廓闭合环，并应用 Chaikin 细分平滑算法消除 Voronoi 格子棱角。
    /// </summary>
    public static IReadOnlyList<PolyVec2[]> ExtractSmoothedBoundaries(
        CellGeometry geometry,
        int[] cells)
    {
        if (cells.Length == 0) return Array.Empty<PolyVec2[]>();

        var cellSet = new HashSet<int>(cells);
        var exteriorEdges = new List<(PolyVec2 A, PolyVec2 B)>();

        // 收集所有边界单向边 (在当前区域内部未被邻居反向共享的边)
        var edgeSet = new HashSet<long>();
        var directedEdgeList = new List<(PolyVec2 A, PolyVec2 B)>();

        for (var i = 0; i < cells.Length; i++)
        {
            var c = cells[i];
            var vStart = geometry.CellVertexStart[c];
            var vEnd = geometry.CellVertexStart[c + 1];
            var vCount = vEnd - vStart;
            if (vCount < 3) continue;

            for (var k = 0; k < vCount; k++)
            {
                var i0 = vStart + k;
                var i1 = vStart + ((k + 1) % vCount);
                var p0 = new PolyVec2(geometry.VertexX[i0], geometry.VertexY[i0]);
                var p1 = new PolyVec2(geometry.VertexX[i1], geometry.VertexY[i1]);

                var key = QuantizeEdgeKey(p0, p1);
                var revKey = QuantizeEdgeKey(p1, p0);

                if (edgeSet.Contains(revKey))
                {
                    // 内部共享边，消除
                    edgeSet.Remove(revKey);
                }
                else
                {
                    edgeSet.Add(key);
                }
                directedEdgeList.Add((p0, p1));
            }
        }

        foreach (var (p0, p1) in directedEdgeList)
        {
            if (edgeSet.Contains(QuantizeEdgeKey(p0, p1)))
            {
                exteriorEdges.Add((p0, p1));
            }
        }

        // 将离散边界边串联为闭合环
        var loops = StitchLoops(exteriorEdges);
        var smoothedLoops = new List<PolyVec2[]>();

        foreach (var loop in loops)
        {
            if (loop.Length < 3) continue;
            // 执行 2 轮 Chaikin 细分平滑
            var smoothed = ChaikinSmooth(loop, 2);
            smoothedLoops.Add(smoothed);
        }

        return smoothedLoops;
    }

    private static long QuantizeEdgeKey(PolyVec2 a, PolyVec2 b)
    {
        var ax = (long)Math.Round(a.X * 10.0);
        var ay = (long)Math.Round(a.Y * 10.0);
        var bx = (long)Math.Round(b.X * 10.0);
        var by = (long)Math.Round(b.Y * 10.0);

        var h1 = (ax * 73856093) ^ (ay * 19349663);
        var h2 = (bx * 83492791) ^ (by * 42345677);
        return (h1 << 32) | (h2 & 0xFFFFFFFFL);
    }

    private static List<PolyVec2[]> StitchLoops(List<(PolyVec2 A, PolyVec2 B)> edges)
    {
        var result = new List<PolyVec2[]>();
        if (edges.Count == 0) return result;

        var remaining = new HashSet<int>(Enumerable.Range(0, edges.Count));

        while (remaining.Count > 0)
        {
            var firstIdx = remaining.First();
            remaining.Remove(firstIdx);

            var loop = new List<PolyVec2> { edges[firstIdx].A, edges[firstIdx].B };
            var currentEnd = edges[firstIdx].B;
            var maxSteps = edges.Count;

            while (maxSteps-- > 0)
            {
                var foundNext = -1;
                var bestDistSq = 4.0; // 容差 2.0 像素以内

                foreach (var idx in remaining)
                {
                    var dSq = currentEnd.DistanceSquaredTo(edges[idx].A);
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        foundNext = idx;
                    }
                }

                if (foundNext < 0) break;

                remaining.Remove(foundNext);
                currentEnd = edges[foundNext].B;
                loop.Add(currentEnd);

                if (currentEnd.DistanceSquaredTo(loop[0]) < 4.0)
                {
                    // 闭合
                    break;
                }
            }

            if (loop.Count >= 3)
            {
                result.Add(loop.ToArray());
            }
        }

        return result;
    }

    /// <summary>
    /// Chaikin 切角平滑细分算法。
    /// </summary>
    public static PolyVec2[] ChaikinSmooth(PolyVec2[] loop, int iterations = 2)
    {
        if (loop.Length < 3) return loop;
        var current = loop;

        for (var it = 0; it < iterations; it++)
        {
            var n = current.Length;
            var next = new PolyVec2[n * 2];

            for (var i = 0; i < n; i++)
            {
                var p0 = current[i];
                var p1 = current[(i + 1) % n];

                next[i * 2] = (p0 * 0.75) + (p1 * 0.25);
                next[(i * 2) + 1] = (p0 * 0.25) + (p1 * 0.75);
            }

            current = next;
        }

        return current;
    }
}
