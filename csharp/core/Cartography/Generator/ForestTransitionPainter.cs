using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Cartography.Design;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 森林多层生态渐变画师 2.0 (ForestTransitionPainter 2.0)。
/// 
/// 彻底解决“森林太像散点贴图、边界死板、无体块感”的痛点：
/// 1. 结构化体块系统（Forest Mass）：
///    - 外轮廓多边形（Hull Polygon）：有机凸凹自然起伏；
///    - 密林核心区（ForestCore）：以大型水墨林冠簇（ForestCluster）紧密咬合搭接，形成万顷浩荡苍翠林海；
///    - 林间隙地（Clearings）：在林腹穿插自然留白空地（如道观幽谷、青泉草坪），形成山水画的“气孔”；
///    - 林缘过渡带（ForestEdge）：仅在边缘 42px 范围内微量生成单木/双木（TreeGroup），向外羽化消融；
///    - 外缘草甸缓冲（GrasslandBuffer）：树木完全停止，点缀稀疏草甸风草纹。
/// 2. 平原零树木军规：平原严禁生成杂乱树木散点，所有树木严格依附于林海大区与水岸生态；
/// 3. 空气林岚（Canopy Mist）：在密林深处升腾微润水雾，消除林脚硬边。
/// </summary>
public static class ForestTransitionPainter
{
    public static List<BrushInstruction> Paint(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        IReadOnlyList<MegaTerrainRegion>? megaRegions,
        Dictionary<int, RegionStyle> regionStyles,
        ForestTransitionProfile? profile = null,
        IReadOnlyList<NarrativeRegion>? narrativeRegions = null)
    {
        profile ??= new ForestTransitionProfile();
        var instructions = new List<BrushInstruction>();
        var placedTreeCenters = new List<PolyVec2>();
        var placedBufferGrass = new List<PolyVec2>();
        var treeGrid = new TreeSpatialGrid(6.0);

        var rand = new Random((int)(options.Seed ^ 0x9922));
        var seaLevel = options.SeaLevel;

        // 1. 优先消费显式叙事大区（NarrativeRegion: ForestMass）
        var hasRenderedNarrativeForest = false;
        if (narrativeRegions != null)
        {
            foreach (var nr in narrativeRegions)
            {
                if (nr.Type != NarrativeRegionType.ForestMass || nr.ForestMass == null) continue;
                var mass = nr.ForestMass;
                if (mass.HullPolygon.Count < 3) continue;

                hasRenderedNarrativeForest = true;
                regionStyles.TryGetValue(nr.Id, out var style);
                var density = Math.Clamp(style?.Density ?? 1.0f, 0.5f, 2.0f);
                var isFogEnabled = style?.Fog ?? true;
                var mistColor = style?.MistColor ?? CartographyColor.MistIvory;
                var palette = style?.Palette ?? nr.Palette;

                // 计算森林多边形 AABB
                var minX = double.MaxValue;
                var minY = double.MaxValue;
                var maxX = double.MinValue;
                var maxY = double.MinValue;
                foreach (var pt in mass.HullPolygon)
                {
                    if (pt.X < minX) minX = pt.X;
                    if (pt.Y < minY) minY = pt.Y;
                    if (pt.X > maxX) maxX = pt.X;
                    if (pt.Y > maxY) maxY = pt.Y;
                }

                var clusterStep = profile.CoreClusterStep / density;

                var rowIdx = 0;
                var rowStep = clusterStep * 0.60;

                // ── Pass 1: 主林冠簇与树群铺设 (Macro Clusters) ──
                for (var y = minY; y <= maxY; y += rowStep)
                {
                    var xOffset = (rowIdx % 2 == 1) ? (clusterStep * 0.50) : 0.0;
                    rowIdx++;
                    for (var x = minX - clusterStep; x <= maxX + clusterStep; x += clusterStep)
                    {
                        var jX = x + xOffset + (rand.NextDouble() - 0.5) * clusterStep * 0.40;
                        var jY = y + (rand.NextDouble() - 0.5) * rowStep * 0.40;
                        var pt = new PolyVec2(jX, jY);

                        // 判定点是否落在森林多边形实体内部
                        if (!ForestMassGenerator.IsInside(pt, mass.HullPolygon)) continue;

                        // 判定是否落在林间留白空地（Clearings）内，空地内严禁放树！
                        if (ForestMassGenerator.IsInAnyClearing(pt, mass.Clearings, margin: 4f)) continue;

                        var cell = geometry.FindCell(pt.X, pt.Y);
                        if (cell >= 0 && cell < fields.Count && fields.Height[cell] <= seaLevel) continue;

                        var distToEdge = ForestMassGenerator.ComputeDistanceToBoundary(pt, mass.HullPolygon);
                        var normDepth = distToEdge / Math.Max(1f, mass.EdgeMarginWidth);

                        // ── Level 1: 密林核心 (ForestCore: 高密度水墨林冠连成片) ──
                        if (normDepth >= 0.35f)
                        {
                            if (treeGrid.IsFarFromPlaced(pt, 4.5f / density))
                            {
                                instructions.Add(new BrushInstruction
                                {
                                    Type = BrushType.ForestCluster,
                                    Position = pt,
                                    Scale = 1.0f + (float)rand.NextDouble() * 0.18f,
                                    Opacity = 0.94f,
                                    YOrder = (float)pt.Y,
                                    Tint = palette,
                                    RegionId = nr.Id,
                                    VariantKey = "forest_mass_core"
                                });
                                treeGrid.Add(pt);
                                placedTreeCenters.Add(pt);
                            }
                        }
                        // ── Level 2: 茂密树林过渡 (Woodland: 高密度树林紧密包裹密林) ──
                        else if (normDepth >= 0.10f)
                        {
                            var spawnProb = 0.85f;
                            if (rand.NextSingle() < spawnProb && treeGrid.IsFarFromPlaced(pt, profile.EdgeTreeStep / density))
                            {
                                var isSingle = rand.NextSingle() < 0.50f;
                                var variant = isSingle ? "tree_single" : "tree_cluster_small";

                                instructions.Add(new BrushInstruction
                                {
                                    Type = BrushType.TreeGroup,
                                    Position = pt,
                                    Scale = 0.85f + (float)rand.NextDouble() * 0.15f,
                                    Opacity = 0.90f,
                                    YOrder = (float)pt.Y,
                                    Tint = palette,
                                    RegionId = nr.Id,
                                    VariantKey = variant
                                });
                                treeGrid.Add(pt);
                                placedTreeCenters.Add(pt);
                            }
                        }
                    }
                }

                // ── Pass 2: 隙缝单木紧密补全 (Gap-Filling Single Trees) ──
                // 使用单颗精致手绘树木图元，无缝填充大型林冠簇之间的空白隙缝与空洞
                var gapStep = 2.4 / density;
                var gapRowStep = gapStep * 0.70;
                var gapRowIdx = 0;

                for (var y = minY; y <= maxY; y += gapRowStep)
                {
                    var xOffset = (gapRowIdx % 2 == 1) ? (gapStep * 0.50) : 0.0;
                    gapRowIdx++;
                    for (var x = minX - gapStep; x <= maxX + gapStep; x += gapStep)
                    {
                        var jX = x + xOffset + (rand.NextDouble() - 0.5) * gapStep * 0.35;
                        var jY = y + (rand.NextDouble() - 0.5) * gapRowStep * 0.35;
                        var pt = new PolyVec2(jX, jY);

                        if (!ForestMassGenerator.IsInside(pt, mass.HullPolygon)) continue;
                        if (ForestMassGenerator.IsInAnyClearing(pt, mass.Clearings, margin: 2.5f)) continue;

                        var cell = geometry.FindCell(pt.X, pt.Y);
                        if (cell >= 0 && cell < fields.Count && fields.Height[cell] <= seaLevel) continue;

                        var distToEdge = ForestMassGenerator.ComputeDistanceToBoundary(pt, mass.HullPolygon);
                        var normDepth = distToEdge / Math.Max(1f, mass.EdgeMarginWidth);
                        if (normDepth < 0.08f) continue;

                        if (treeGrid.IsFarFromPlaced(pt, 2.2f / density))
                        {
                            instructions.Add(new BrushInstruction
                            {
                                Type = BrushType.TreeGroup,
                                Position = pt,
                                Scale = 0.82f + (float)rand.NextDouble() * 0.16f,
                                Opacity = 0.92f,
                                YOrder = (float)pt.Y,
                                Tint = palette,
                                RegionId = nr.Id,
                                VariantKey = "tree_single"
                            });
                            treeGrid.Add(pt);
                            placedTreeCenters.Add(pt);
                        }
                    }
                }

                // 潮湿深林水雾层（林岚蒸腾）
                if (isFogEnabled)
                {
                    var fogCount = Math.Max(3, (int)(mass.HullPolygon.Count * 0.15f * density));
                    for (var i = 0; i < fogCount; i++)
                    {
                        var angle = rand.NextSingle() * MathF.PI * 2f;
                        var dist = rand.NextSingle() * ((maxX - minX) * 0.35f);
                        var fPos = nr.Center + new PolyVec2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);

                        if (!ForestMassGenerator.IsInside(fPos, mass.HullPolygon)) continue;
                        if (ForestMassGenerator.IsInAnyClearing(fPos, mass.Clearings)) continue;

                        instructions.Add(new BrushInstruction
                        {
                            Type = BrushType.Fog,
                            Position = fPos,
                            Rotation = (rand.NextSingle() - 0.5f) * 0.15f,
                            Scale = 1.30f + rand.NextSingle() * 0.30f,
                            ScaleY = 0.45f,
                            Opacity = 0.25f,
                            YOrder = (float)fPos.Y + 2f,
                            Tint = mistColor,
                            RegionId = nr.Id,
                            VariantKey = "mist_forest_canopy"
                        });
                    }
                }
            }
        }

        // 2. 如果未配置显式叙事林海，回退到 legacy MegaTerrainRegion 渲染
        if (!hasRenderedNarrativeForest && megaRegions != null)
        {
            foreach (var forest in megaRegions)
            {
                if (forest.Type != MegaTerrainType.MegaForest || forest.Cells.Length == 0) continue;

                regionStyles.TryGetValue(forest.Id, out var style);
                var density = Math.Clamp(style?.Density ?? 1.0f, 0.5f, 2.0f);
                var palette = style?.Palette ?? CartographyColor.DeepForest;

                var forestCellSet = new HashSet<int>(forest.Cells);
                var clusterStep = profile.CoreClusterStep / density;
                var boundsMin = forest.BoundsMin;
                var boundsMax = forest.BoundsMax;

                var rowIdx = 0;
                var rowStep = clusterStep * 0.60;

                for (var y = boundsMin.Y; y <= boundsMax.Y; y += rowStep)
                {
                    var xOffset = (rowIdx % 2 == 1) ? (clusterStep * 0.50) : 0.0;
                    rowIdx++;
                    for (var x = boundsMin.X - clusterStep; x <= boundsMax.X + clusterStep; x += clusterStep)
                    {
                        var jX = x + xOffset + (rand.NextDouble() - 0.5) * clusterStep * 0.40;
                        var jY = y + (rand.NextDouble() - 0.5) * rowStep * 0.40;
                        var pt = new PolyVec2(jX, jY);

                        var cell = geometry.FindCell(pt.X, pt.Y);
                        if (cell < 0 || cell >= fields.Count || fields.Height[cell] <= seaLevel) continue;

                        // 严格校验：当前网格单元必须真正属于该森林大区，杜绝边界向外渗漏到平原
                        if (!forestCellSet.Contains(cell)) continue;

                        var depth = forest.ComputeNormalizedDepth(pt);
                        // 超出深度下限严格不绘制任何树木
                        if (depth <= 0.05f) continue;

                        if (depth >= profile.CoreDepthThreshold)
                        {
                            if (treeGrid.IsFarFromPlaced(pt, 4.5f / density))
                            {
                                instructions.Add(new BrushInstruction
                                {
                                    Type = BrushType.ForestCluster,
                                    Position = pt,
                                    Scale = 1.0f + (float)rand.NextDouble() * 0.18f,
                                    Opacity = 0.94f,
                                    YOrder = (float)pt.Y,
                                    Tint = palette,
                                    RegionId = forest.Id,
                                    VariantKey = "macro_cluster"
                                });
                                treeGrid.Add(pt);
                                placedTreeCenters.Add(pt);
                            }
                        }
                        else if (depth >= profile.EdgeDepthThreshold)
                        {
                            var t = (depth - profile.EdgeDepthThreshold) / (profile.CoreDepthThreshold - profile.EdgeDepthThreshold);
                            var spawnProb = profile.EdgeTreeSpawnRatio * (0.50f + 0.50f * t);

                            if (rand.NextDouble() < spawnProb && treeGrid.IsFarFromPlaced(pt, profile.EdgeTreeStep / density))
                            {
                                instructions.Add(new BrushInstruction
                                {
                                    Type = BrushType.TreeGroup,
                                    Position = pt,
                                    Scale = 0.85f + (float)rand.NextDouble() * 0.15f,
                                    Opacity = 0.90f,
                                    YOrder = (float)pt.Y,
                                    Tint = palette,
                                    RegionId = forest.Id,
                                    VariantKey = "tree_cluster_small"
                                });
                                treeGrid.Add(pt);
                                placedTreeCenters.Add(pt);
                            }
                        }
                    }
                }

                // ── Pass 2: 隙缝单木紧密补全 (Gap-Filling Single Trees) ──
                // 使用手绘单颗树木图元，填补大林冠咬合间隙与林内地块空隙（连成片，零秃斑）
                var gapStep = 2.4 / density;
                var gapRowStep = gapStep * 0.70;
                var gapRowIdx = 0;

                for (var y = boundsMin.Y; y <= boundsMax.Y; y += gapRowStep)
                {
                    var xOffset = (gapRowIdx % 2 == 1) ? (gapStep * 0.50) : 0.0;
                    gapRowIdx++;
                    for (var x = boundsMin.X - gapStep; x <= boundsMax.X + gapStep; x += gapStep)
                    {
                        var jX = x + xOffset + (rand.NextDouble() - 0.5) * gapStep * 0.35;
                        var jY = y + (rand.NextDouble() - 0.5) * gapRowStep * 0.35;
                        var pt = new PolyVec2(jX, jY);

                        var cell = geometry.FindCell(pt.X, pt.Y);
                        if (cell < 0 || cell >= fields.Count || fields.Height[cell] <= seaLevel) continue;
                        if (!forestCellSet.Contains(cell)) continue;

                        var depth = forest.ComputeNormalizedDepth(pt);
                        if (depth <= 0.04f) continue;

                        if (treeGrid.IsFarFromPlaced(pt, 2.2f / density))
                        {
                            instructions.Add(new BrushInstruction
                            {
                                Type = BrushType.TreeGroup,
                                Position = pt,
                                Scale = 0.82f + (float)rand.NextDouble() * 0.16f,
                                Opacity = 0.92f,
                                YOrder = (float)pt.Y,
                                Tint = palette,
                                RegionId = forest.Id,
                                VariantKey = "tree_single"
                            });
                            treeGrid.Add(pt);
                            placedTreeCenters.Add(pt);
                        }
                    }
                }
            }
        }

        // 3. 旧版兜底（仅在未启用 NarrativeRegion 且无 MegaForest 时的旧版模式点缀）
        if (!hasRenderedNarrativeForest && instructions.Count == 0)
        {
            var forestBiomeCells = new HashSet<int>();
            for (var cell = 0; cell < fields.Count; cell++)
            {
                if (fields.Height[cell] <= seaLevel) continue;
                var b = (BiomeType)fields.Biome[cell];
                if (b is BiomeType.TemperateSeasonalForest or BiomeType.TemperateRainForest
                    or BiomeType.TropicalRainForest or BiomeType.BorealForest)
                {
                    forestBiomeCells.Add(cell);
                }
            }

            var visited = new HashSet<int>();
            foreach (var seedCell in forestBiomeCells)
            {
                if (!visited.Add(seedCell)) continue;

                var patch = new HashSet<int> { seedCell };
                var queue = new Queue<int>();
                queue.Enqueue(seedCell);

                var minX = geometry.CentroidX[seedCell];
                var maxX = minX;
                var minY = geometry.CentroidY[seedCell];
                var maxY = minY;

                while (queue.Count > 0)
                {
                    var curr = queue.Dequeue();
                    var nStart = geometry.CellNeighborStart[curr];
                    var nEnd = geometry.CellNeighborStart[curr + 1];
                    for (var n = nStart; n < nEnd; n++)
                    {
                        var neighbor = geometry.CellNeighbors[n];
                        if (forestBiomeCells.Contains(neighbor) && visited.Add(neighbor))
                        {
                            patch.Add(neighbor);
                            queue.Enqueue(neighbor);
                            var cx = geometry.CentroidX[neighbor];
                            var cy = geometry.CentroidY[neighbor];
                            if (cx < minX) minX = cx;
                            if (cx > maxX) maxX = cx;
                            if (cy < minY) minY = cy;
                            if (cy > maxY) maxY = cy;
                        }
                    }
                }

                // 孤立散点（少于 3 个相连地块）直接丢弃，严禁在开阔草原生成孤立树木图元
                if (patch.Count < 3) continue;

                var edgeCells = new HashSet<int>();
                foreach (var c in patch)
                {
                    var nStart = geometry.CellNeighborStart[c];
                    var nEnd = geometry.CellNeighborStart[c + 1];
                    for (var n = nStart; n < nEnd; n++)
                    {
                        if (!patch.Contains(geometry.CellNeighbors[n]))
                        {
                            edgeCells.Add(c);
                            break;
                        }
                    }
                }

                var clusterStep = profile.CoreClusterStep;
                var rowStep = clusterStep * 0.60;
                var rowIdx = 0;

                // ── Pass 1: 斑块主林冠填充 ──
                for (var y = minY - 6.0; y <= maxY + 6.0; y += rowStep)
                {
                    var xOffset = (rowIdx % 2 == 1) ? (clusterStep * 0.50) : 0.0;
                    rowIdx++;
                    for (var x = minX - 6.0; x <= maxX + 6.0; x += clusterStep)
                    {
                        var jX = x + xOffset + (rand.NextDouble() - 0.5) * clusterStep * 0.40;
                        var jY = y + (rand.NextDouble() - 0.5) * rowStep * 0.40;
                        var pt = new PolyVec2(jX, jY);

                        var cell = geometry.FindCell(pt.X, pt.Y);
                        if (cell < 0 || !patch.Contains(cell) || fields.Height[cell] <= seaLevel) continue;

                        var isEdge = edgeCells.Contains(cell);
                        var minDist = isEdge ? profile.EdgeTreeStep : 4.5f;

                        if (treeGrid.IsFarFromPlaced(pt, minDist))
                        {
                            var brushType = (isEdge && rand.NextDouble() < 0.60) ? BrushType.TreeGroup : BrushType.ForestCluster;
                            var variant = brushType == BrushType.TreeGroup ? "tree_cluster_small" : "macro_cluster";

                            instructions.Add(new BrushInstruction
                            {
                                Type = brushType,
                                Position = pt,
                                Scale = 0.90f + (float)rand.NextDouble() * 0.18f,
                                Opacity = 0.92f,
                                YOrder = (float)pt.Y,
                                Tint = CartographyColor.DeepForest,
                                RegionId = -1,
                                VariantKey = variant
                            });
                            treeGrid.Add(pt);
                            placedTreeCenters.Add(pt);
                        }
                    }
                }

                // ── Pass 2: 斑块内部隙缝单木紧密补全 ──
                var gapStep = 2.4;
                var gapRowStep = gapStep * 0.70;
                var gapRowIdx = 0;

                for (var y = minY - 4.0; y <= maxY + 4.0; y += gapRowStep)
                {
                    var xOffset = (gapRowIdx % 2 == 1) ? (gapStep * 0.50) : 0.0;
                    gapRowIdx++;
                    for (var x = minX - 4.0; x <= maxX + 4.0; x += gapStep)
                    {
                        var jX = x + xOffset + (rand.NextDouble() - 0.5) * gapStep * 0.35;
                        var jY = y + (rand.NextDouble() - 0.5) * gapRowStep * 0.35;
                        var pt = new PolyVec2(jX, jY);

                        var cell = geometry.FindCell(pt.X, pt.Y);
                        if (cell < 0 || !patch.Contains(cell) || fields.Height[cell] <= seaLevel) continue;

                        if (treeGrid.IsFarFromPlaced(pt, 2.2f))
                        {
                            instructions.Add(new BrushInstruction
                            {
                                Type = BrushType.TreeGroup,
                                Position = pt,
                                Scale = 0.82f + (float)rand.NextDouble() * 0.16f,
                                Opacity = 0.92f,
                                YOrder = (float)pt.Y,
                                Tint = CartographyColor.DeepForest,
                                RegionId = -1,
                                VariantKey = "tree_single"
                            });
                            treeGrid.Add(pt);
                            placedTreeCenters.Add(pt);
                        }
                    }
                }
            }
        }

        return instructions;
    }

    private static bool IsFarFromPlaced(PolyVec2 pos, List<PolyVec2> placed, float minDist)
    {
        var minDistSq = (double)(minDist * minDist);
        for (var i = 0; i < placed.Count; i++)
        {
            if (pos.DistanceSquaredTo(placed[i]) < minDistSq) return false;
        }
        return true;
    }
}

/// <summary>
/// 树木二维空间分桶网格索引，将最近邻排斥测试由 O(N) 优化至 O(1)。
/// </summary>
internal sealed class TreeSpatialGrid
{
    private readonly double _cellSize;
    private readonly Dictionary<long, List<PolyVec2>> _grid = new();

    public TreeSpatialGrid(double cellSize = 6.0)
    {
        _cellSize = cellSize;
    }

    private static long GetKey(int gx, int gy) => ((long)gx << 32) | (uint)gy;

    public void Add(PolyVec2 pt)
    {
        var gx = (int)Math.Floor(pt.X / _cellSize);
        var gy = (int)Math.Floor(pt.Y / _cellSize);
        var key = GetKey(gx, gy);
        if (!_grid.TryGetValue(key, out var list))
        {
            list = new List<PolyVec2>(4);
            _grid[key] = list;
        }
        list.Add(pt);
    }

    public bool IsFarFromPlaced(PolyVec2 pos, float minDist)
    {
        var minDistSq = (double)(minDist * minDist);
        var r = (int)Math.Ceiling(minDist / _cellSize) + 1;
        var cx = (int)Math.Floor(pos.X / _cellSize);
        var cy = (int)Math.Floor(pos.Y / _cellSize);

        for (var dy = -r; dy <= r; dy++)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                if (_grid.TryGetValue(GetKey(cx + dx, cy + dy), out var list))
                {
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (pos.DistanceSquaredTo(list[i]) < minDistSq) return false;
                    }
                }
            }
        }
        return true;
    }
}
