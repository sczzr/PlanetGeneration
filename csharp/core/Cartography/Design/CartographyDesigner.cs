using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 地图导演层与构图设计编译器（CartographyDesigner）。
/// 
/// 负责将画师设定的构图蓝图（MapBlueprint）精确编译并拟合至多边形网格空间：
/// 1. 将宏观山系（MountainSpineBlueprint）编译为连贯骨架脊线（Spine Nodes）与巍峨主脉；
/// 2. 将宏观林海（ForestZoneBlueprint）编译为大斑块水墨林区与自然衰减深度场；
/// 3. 将大漠瀚海（DesertZoneBlueprint）编译为流动金沙沙垄地貌；
/// 4. 将母亲河（RiverCorridorBlueprint）沿画卷走廊冲积穿行并滋养两岸；
/// 5. 将战略聚落（StrategicSettlementBlueprint）准确落子于天下神都、江汉要邑、锁山隘口与半月巨港；
/// 6. 生成全局区域调色盘与艺术风格规则（RegionStyle）。
/// </summary>
public static class CartographyDesigner
{
    public static DesignedCartographyPlan Design(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        MapBlueprint? blueprint = null)
    {
        blueprint ??= FantasyContinent01Blueprint.Create();

        var w = geometry.Width;
        var h = geometry.Height;
        var seaLevel = options.SeaLevel;
        var nextRegionId = 100;

        var designedMegaRegions = new List<MegaTerrainRegion>();
        var designedSettlements = new List<SettlementInfo>();
        var regionStyles = new Dictionary<int, RegionStyle>();

        // 0. 初始化默认中原与各生态大区风格
        regionStyles[0] = new RegionStyle
        {
            RegionId = 0,
            Name = "中原",
            Palette = CartographyColor.EmeraldGreen,
            Density = 1.0f,
            Exaggeration = 1.0f,
            Fog = true,
            MistColor = CartographyColor.MistIvory
        };

        foreach (var r in blueprint.Regions)
        {
            regionStyles[r.Id] = new RegionStyle
            {
                RegionId = r.Id,
                Name = r.Name,
                Palette = r.Palette,
                Density = 1.0f,
                Exaggeration = 1.0f,
                Fog = true,
                MistColor = CartographyColor.MistIvory
            };
        }

        // ── 0.5. 蓝图大洲陆基筑底（Continent Foundation Carving） ──
        // 将蓝图定义的大陆宏观地理包络筑实为陆地，使山脉、林海、大漠与水系拥有完整的陆基承载
        CarveContinentLandmass(geometry, fields, options, blueprint);

        // ── 1. 编译宏观山系脊线（MountainSpines -> MegaMountain） ──
        foreach (var ms in blueprint.MountainSpines)
        {
            var pStart = ToWorld(ms.StartPoint, w, h);
            var pEnd = ToWorld(ms.EndPoint, w, h);
            var pControls = ms.ControlPoints.Select(p => ToWorld(p, w, h)).ToList();

            // 生成平滑样条曲线样点
            var spineSpline = BuildSpineCurve(pStart, pControls, pEnd, sampleCount: 40);
            var spineNodes = new List<MountainSpineNode>(spineSpline.Count);
            var spineCells = new HashSet<int>();

            for (var i = 0; i < spineSpline.Count; i++)
            {
                var pt = spineSpline[i];
                var t = i / (float)(spineSpline.Count - 1);

                // 依据 MajorPeakRatios 与 PassRatios 调节海拔 profile
                var elev = seaLevel + 0.42f;

                // 主峰拔高
                foreach (var peakRatio in ms.MajorPeakRatios)
                {
                    var distToPeak = MathF.Abs(t - peakRatio);
                    if (distToPeak < 0.12f)
                    {
                        var boost = (1.0f - distToPeak / 0.12f) * 0.45f;
                        elev += boost;
                    }
                }

                // 隘口降低
                foreach (var passRatio in ms.PassRatios)
                {
                    var distToPass = MathF.Abs(t - passRatio);
                    if (distToPass < 0.08f)
                    {
                        var drop = (1.0f - distToPass / 0.08f) * 0.22f;
                        elev = MathF.Max(seaLevel + 0.18f, elev - drop);
                    }
                }

                var cell = geometry.FindCell(pt.X, pt.Y);
                if (cell >= 0 && cell < geometry.Count)
                {
                    spineCells.Add(cell);
                    // 确保主脊下方单元具有巍峨山脉地貌属性
                    fields.Landform[cell] = (byte)LandformType.Mountain;
                    fields.Height[cell] = MathF.Max(fields.Height[cell], elev);
                    fields.Biome[cell] = (ms.HasSnowCap && elev > seaLevel + 0.70f)
                        ? (byte)BiomeType.SnowyMountain
                        : (byte)BiomeType.RockyMountain;
                }

                spineNodes.Add(new MountainSpineNode(pt, elev, ms.SpineWidth, cell));
            }

            // 辐射扩充主山脊覆盖单元格
            var spineArr = spineNodes.Select(n => n.Position).ToList();
            var searchDistSq = (ms.SpineWidth * 0.85) * (ms.SpineWidth * 0.85);
            for (var c = 0; c < geometry.Count; c++)
            {
                var cPos = new PolyVec2(geometry.CentroidX[c], geometry.CentroidY[c]);
                for (var s = 0; s < spineArr.Count; s += 2)
                {
                    if (cPos.DistanceSquaredTo(spineArr[s]) <= searchDistSq)
                    {
                        spineCells.Add(c);
                        fields.Height[c] = MathF.Max(fields.Height[c], seaLevel + 0.22f);
                        if (fields.Landform[c] != (byte)LandformType.Mountain)
                        {
                            fields.Landform[c] = (byte)LandformType.Hill;
                        }
                        break;
                    }
                }
            }

            var cellList = spineCells.ToArray();
            var bMin = new PolyVec2(cellList.Min(c => geometry.CentroidX[c]), cellList.Min(c => geometry.CentroidY[c]));
            var bMax = new PolyVec2(cellList.Max(c => geometry.CentroidX[c]), cellList.Max(c => geometry.CentroidY[c]));
            var centroid = new PolyVec2((bMin.X + bMax.X) * 0.5, (bMin.Y + bMax.Y) * 0.5);

            var mountainRegion = new MegaTerrainRegion
            {
                Id = ms.Id,
                Type = MegaTerrainType.MegaMountain,
                Rank = ms.Rank >= 3 ? MegaRegionRank.WorldLandmark : MegaRegionRank.MegaRegion,
                Name = ms.Name,
                Cells = cellList,
                TotalArea = cellList.Length * 35.0,
                AreaShare = cellList.Length / (float)geometry.Count,
                Centroid = centroid,
                BoundsMin = bMin,
                BoundsMax = bMax,
                Spine = spineNodes,
                ElevationMean = seaLevel + 0.50f,
                MoistureMean = 0.50f,
                SmoothedPolygons = new List<PolyVec2[]> { spineArr.ToArray() }
            };

            designedMegaRegions.Add(mountainRegion);
            regionStyles[ms.Id] = new RegionStyle
            {
                RegionId = ms.Id,
                Name = ms.Name,
                Palette = ms.Palette,
                Density = 1.0f,
                Exaggeration = 1.0f,
                Fog = true,
                MistColor = CartographyColor.MistIvory
            };
        }

        // ── 2. 编译宏观林海大斑块（ForestZones -> MegaForest） ──
        foreach (var fz in blueprint.ForestZones)
        {
            var center = ToWorld(fz.Center, w, h);
            var rx = fz.RadiusX * w;
            var ry = fz.RadiusY * h;
            var forestCells = new List<int>();

            // 采样椭圆轮廓
            var polyPts = new List<PolyVec2>();
            const int segs = 24;
            var cos = MathF.Cos(fz.Rotation);
            var sin = MathF.Sin(fz.Rotation);

            for (var i = 0; i < segs; i++)
            {
                var theta = (i / (float)segs) * MathF.PI * 2.0f;
                var lx = MathF.Cos(theta) * rx;
                var ly = MathF.Sin(theta) * ry;
                var gx = center.X + (lx * cos - ly * sin);
                var gy = center.Y + (lx * sin + ly * cos);
                polyPts.Add(new PolyVec2(gx, gy));
            }

            for (var c = 0; c < geometry.Count; c++)
            {
                if (fields.Landform[c] == (byte)LandformType.Mountain) continue;

                var cPos = new PolyVec2(geometry.CentroidX[c], geometry.CentroidY[c]);
                var dx = cPos.X - center.X;
                var dy = cPos.Y - center.Y;
                var localX = dx * cos + dy * sin;
                var localY = -dx * sin + dy * cos;

                var normDist = (localX * localX) / (rx * rx) + (localY * localY) / (ry * ry);
                if (normDist <= 1.0)
                {
                    forestCells.Add(c);
                    fields.Height[c] = MathF.Max(fields.Height[c], seaLevel + 0.12f);
                    if (fields.Landform[c] == (byte)LandformType.Ocean)
                    {
                        fields.Landform[c] = (byte)LandformType.Plain;
                    }
                    fields.Biome[c] = (byte)BiomeType.TemperateSeasonalForest;
                    fields.Moisture[c] = MathF.Max(fields.Moisture[c], 0.70f);
                }
            }

            if (forestCells.Count > 0)
            {
                var fArr = forestCells.ToArray();
                var bMin = new PolyVec2(fArr.Min(c => geometry.CentroidX[c]), fArr.Min(c => geometry.CentroidY[c]));
                var bMax = new PolyVec2(fArr.Max(c => geometry.CentroidX[c]), fArr.Max(c => geometry.CentroidY[c]));

                designedMegaRegions.Add(new MegaTerrainRegion
                {
                    Id = nextRegionId++,
                    Type = MegaTerrainType.MegaForest,
                    Rank = MegaRegionRank.MegaRegion,
                    Name = fz.Name,
                    Cells = fArr,
                    TotalArea = fArr.Length * 35.0,
                    AreaShare = fArr.Length / (float)geometry.Count,
                    Centroid = center,
                    BoundsMin = bMin,
                    BoundsMax = bMax,
                    MoistureMean = 0.70f,
                    SmoothedPolygons = new List<PolyVec2[]> { polyPts.ToArray() }
                });
            }
        }

        // ── 3. 编译大漠瀚海（DesertZones -> MegaDesert） ──
        foreach (var dz in blueprint.DesertZones)
        {
            var center = ToWorld(dz.Center, w, h);
            var rx = dz.RadiusX * w;
            var ry = dz.RadiusY * h;
            var desertCells = new List<int>();

            var polyPts = new List<PolyVec2>();
            const int segs = 20;
            for (var i = 0; i < segs; i++)
            {
                var theta = (i / (float)segs) * MathF.PI * 2.0f;
                polyPts.Add(new PolyVec2(center.X + MathF.Cos(theta) * rx, center.Y + MathF.Sin(theta) * ry));
            }

            for (var c = 0; c < geometry.Count; c++)
            {
                if (fields.Landform[c] == (byte)LandformType.Mountain) continue;

                var cPos = new PolyVec2(geometry.CentroidX[c], geometry.CentroidY[c]);
                var dx = (cPos.X - center.X) / rx;
                var dy = (cPos.Y - center.Y) / ry;
                if (dx * dx + dy * dy <= 1.0)
                {
                    desertCells.Add(c);
                    fields.Height[c] = MathF.Max(fields.Height[c], seaLevel + 0.14f);
                    if (fields.Landform[c] == (byte)LandformType.Ocean)
                    {
                        fields.Landform[c] = (byte)LandformType.Plain;
                    }
                    fields.Biome[c] = (byte)BiomeType.TropicalDesert;
                    fields.Moisture[c] = MathF.Min(fields.Moisture[c], 0.15f);
                }
            }

            if (desertCells.Count > 0)
            {
                var dArr = desertCells.ToArray();
                var bMin = new PolyVec2(dArr.Min(c => geometry.CentroidX[c]), dArr.Min(c => geometry.CentroidY[c]));
                var bMax = new PolyVec2(dArr.Max(c => geometry.CentroidX[c]), dArr.Max(c => geometry.CentroidY[c]));

                designedMegaRegions.Add(new MegaTerrainRegion
                {
                    Id = nextRegionId++,
                    Type = MegaTerrainType.MegaDesert,
                    Rank = MegaRegionRank.MegaRegion,
                    Name = dz.Name,
                    Cells = dArr,
                    TotalArea = dArr.Length * 35.0,
                    AreaShare = dArr.Length / (float)geometry.Count,
                    Centroid = center,
                    BoundsMin = bMin,
                    BoundsMax = bMax,
                    MainDirectionAngle = dz.WindAngle,
                    MoistureMean = 0.15f,
                    SmoothedPolygons = new List<PolyVec2[]> { polyPts.ToArray() }
                });
            }
        }

        // ── 4. 编译母亲河走廊（RiverCorridors） ──
        foreach (var rc in blueprint.RiverCorridors)
        {
            var pSource = ToWorld(rc.SourcePoint, w, h);
            var pMouth = ToWorld(rc.MouthPoint, w, h);
            var pWaypoints = rc.Waypoints.Select(p => ToWorld(p, w, h)).ToList();

            var riverSpline = BuildSpineCurve(pSource, pWaypoints, pMouth, sampleCount: 50);
            foreach (var rPt in riverSpline)
            {
                var cell = geometry.FindCell(rPt.X, rPt.Y);
                if (cell >= 0 && cell < geometry.Count)
                {
                    fields.River[cell] = MathF.Max(fields.River[cell], 0.28f * rc.WidthScale);
                    fields.Flux[cell] = MathF.Max(fields.Flux[cell], 2.2f * rc.WidthScale);
                    // 平原沿河地带
                    if (fields.Landform[cell] != (byte)LandformType.Mountain)
                    {
                        fields.Height[cell] = MathF.Max(fields.Height[cell], seaLevel + 0.05f);
                        fields.Landform[cell] = (byte)LandformType.Plain;
                    }
                }
            }
        }

        // ── 5. 编译战略枢纽聚落（Settlements） ──
        var usedCells = new HashSet<int>();
        foreach (var sb in blueprint.Settlements)
        {
            var targetPos = ToWorld(sb.Position, w, h);
            var targetCell = geometry.FindCell(targetPos.X, targetPos.Y);

            // 若选点恰好落在水体或已被占用，通过广度优先搜寻临近最佳陆地单元
            targetCell = FindBestLandCell(geometry, fields, seaLevel, targetCell, usedCells);
            if (targetCell < 0) continue;

            usedCells.Add(targetCell);
            var actualPos = new PolyVec2(geometry.CentroidX[targetCell], geometry.CentroidY[targetCell]);

            var rank = sb.Tier switch
            {
                CartographySettlementTier.Capital => SettlementRank.CityState,
                CartographySettlementTier.City => SettlementRank.CityState,
                CartographySettlementTier.Town => SettlementRank.Town,
                CartographySettlementTier.Harbor => SettlementRank.Town,
                _ => SettlementRank.Hamlet
            };

            designedSettlements.Add(new SettlementInfo
            {
                CellId = targetCell,
                Name = sb.Name,
                Position = actualPos,
                Rank = rank,
                Score = sb.Importance * 25f
            });
        }

        // ── 6. 编译显式叙事大区 (Narrative Regions) ──
        var narrativeRegions = new List<NarrativeRegion>();

        // 6.1 苍冥群岳与骨架山系 (MountainRange)
        if (blueprint.MountainRanges != null && blueprint.MountainRanges.Count > 0)
        {
            foreach (var mr in blueprint.MountainRanges)
            {
                var pStart = ToWorld(mr.StartPoint, w, h);
                var pEnd = ToWorld(mr.EndPoint, w, h);
                var pControls = mr.ControlPoints.Select(p => ToWorld(p, w, h)).ToList();

                var chainData = MountainChainGenerator.Generate(
                    pStart,
                    pControls,
                    pEnd,
                    mr.RidgeWidth,
                    mr.MajorPeakRatios,
                    mr.PassRatios,
                    spurCount: mr.SpurCount,
                    spurLength: mr.SpurLength,
                    seed: (int)options.Seed + mr.Id * 23);

                narrativeRegions.Add(new NarrativeRegion
                {
                    Id = mr.Id,
                    Name = mr.Name,
                    Type = NarrativeRegionType.MountainRange,
                    Center = new PolyVec2((pStart.X + pEnd.X) * 0.5, (pStart.Y + pEnd.Y) * 0.5),
                    ExtentRadius = 0.32f,
                    Palette = mr.Palette,
                    Description = mr.Description,
                    MountainChain = chainData
                });
            }
        }
        else
        {
            foreach (var ms in blueprint.MountainSpines)
            {
                var pStart = ToWorld(ms.StartPoint, w, h);
                var pEnd = ToWorld(ms.EndPoint, w, h);
                var pControls = ms.ControlPoints.Select(p => ToWorld(p, w, h)).ToList();

                var chainData = MountainChainGenerator.Generate(
                    pStart,
                    pControls,
                    pEnd,
                    ms.SpineWidth,
                    ms.MajorPeakRatios,
                    ms.PassRatios,
                    spurCount: 5,
                    spurLength: 55f,
                    seed: (int)options.Seed + ms.Id * 23);

                narrativeRegions.Add(new NarrativeRegion
                {
                    Id = ms.Id,
                    Name = ms.Name,
                    Type = NarrativeRegionType.MountainRange,
                    Center = new PolyVec2((pStart.X + pEnd.X) * 0.5, (pStart.Y + pEnd.Y) * 0.5),
                    ExtentRadius = 0.32f,
                    Palette = ms.Palette,
                    Description = "天下主山龙脉，主脊深接，侧脉南舒。",
                    MountainChain = chainData
                });
            }
        }

        // 6.2 太古青岚林海 (ForestMass)
        var forestBp = blueprint.ForestZones.FirstOrDefault();
        if (forestBp != null)
        {
            var fCenter = ToWorld(forestBp.Center, w, h);
            var rx = (float)(forestBp.RadiusX * w);
            var ry = (float)(forestBp.RadiusY * h);

            var forestMassData = ForestMassGenerator.Generate(
                fCenter,
                rx,
                ry,
                forestBp.Rotation,
                (int)options.Seed,
                clearingCount: 3);

            var massBp = blueprint.ForestMasses?.FirstOrDefault();
            if (massBp != null && massBp.Clearings != null && massBp.Clearings.Count > 0)
            {
                forestMassData.Clearings.Clear();
                foreach (var c in massBp.Clearings)
                {
                    forestMassData.Clearings.Add(new ForestClearing
                    {
                        Position = ToWorld(c.Position, w, h),
                        Radius = c.Radius,
                        Name = c.Name
                    });
                }
            }

            narrativeRegions.Add(new NarrativeRegion
            {
                Id = forestBp.Id,
                Name = forestBp.Name,
                Type = NarrativeRegionType.ForestMass,
                Center = fCenter,
                ExtentRadius = 0.20f,
                Palette = forestBp.Palette,
                Description = "万顷浩荡苍翠古林，实体剪影，林间透气留白。",
                BoundaryPolygon = forestMassData.HullPolygon,
                ForestMass = forestMassData
            });
        }

        // 6.3 狂沙金墟 (DesertField)
        var desertBp = blueprint.DesertZones.FirstOrDefault();
        if (desertBp != null)
        {
            var dCenter = ToWorld(desertBp.Center, w, h);
            var rx = desertBp.RadiusX * w;
            var ry = desertBp.RadiusY * h;

            var corridors = new List<List<PolyVec2>>();
            var windAngle = desertBp.WindAngle;
            var windDir = new PolyVec2(Math.Cos(windAngle), Math.Sin(windAngle));
            var perpDir = new PolyVec2(-windDir.Y, windDir.X);

            for (var cIdx = -1; cIdx <= 1; cIdx++)
            {
                var corridorAxis = new List<PolyVec2>();
                var rowOrigin = dCenter + perpDir * (cIdx * ry * 0.45);
                for (var s = -3; s <= 3; s++)
                {
                    var pt = rowOrigin + windDir * (s * (rx * 0.28));
                    corridorAxis.Add(pt);
                }
                corridors.Add(corridorAxis);
            }

            narrativeRegions.Add(new NarrativeRegion
            {
                Id = desertBp.Id,
                Name = desertBp.Name,
                Type = NarrativeRegionType.DesertField,
                Center = dCenter,
                ExtentRadius = 0.22f,
                Palette = desertBp.Palette,
                Description = "金浪滔天的无垠狂沙，流动风纹新月沙垄延绵。",
                DesertField = new DesertFieldData
                {
                    WindAngle = windAngle,
                    DuneRidgeCorridors = corridors,
                    Oases = new List<PolyVec2> { dCenter + new PolyVec2(25.0, 15.0) }
                }
            });
        }

        // 6.4 中原天府神都原 (PlainsBasin)
        var plainsCenter = ToWorld(new PolyVec2(0.48, 0.50), w, h);
        narrativeRegions.Add(new NarrativeRegion
        {
            Id = 3,
            Name = "中原天府神都原",
            Type = NarrativeRegionType.PlainsBasin,
            Center = plainsCenter,
            ExtentRadius = 0.28f,
            Palette = CartographyColor.EmeraldGreen,
            Description = "龙脉汇聚、沃野千里的大陆中枢平原，留白透气，清泽孤丘。",
            PlainsBasin = new PlainsBasinData
            {
                HeartlandCenter = plainsCenter,
                MirrorLakes = new List<PolyVec2>
                {
                    ToWorld(new PolyVec2(0.44, 0.43), w, h),
                    ToWorld(new PolyVec2(0.55, 0.58), w, h)
                },
                MeadowRippleZones = new List<PolyVec2>
                {
                    ToWorld(new PolyVec2(0.46, 0.53), w, h),
                    ToWorld(new PolyVec2(0.52, 0.48), w, h)
                },
                SolitaryKnolls = new List<PolyVec2>
                {
                    ToWorld(new PolyVec2(0.41, 0.54), w, h),
                    ToWorld(new PolyVec2(0.58, 0.44), w, h),
                    ToWorld(new PolyVec2(0.52, 0.62), w, h)
                }
            }
        });

        return new DesignedCartographyPlan
        {
            Blueprint = blueprint,
            NarrativeRegions = narrativeRegions,
            MegaRegions = designedMegaRegions,
            Settlements = designedSettlements,
            RegionStyles = regionStyles
        };
    }

    private static PolyVec2 ToWorld(PolyVec2 pt, double w, double h)
    {
        if (pt.X <= 1.05 && pt.Y <= 1.05)
        {
            return new PolyVec2(pt.X * w, pt.Y * h);
        }
        return pt;
    }

    private static List<PolyVec2> BuildSpineCurve(PolyVec2 start, List<PolyVec2> controls, PolyVec2 end, int sampleCount)
    {
        var allPts = new List<PolyVec2>(controls.Count + 2);
        allPts.Add(start);
        allPts.AddRange(controls);
        allPts.Add(end);

        var result = new List<PolyVec2>(sampleCount);
        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (float)(sampleCount - 1);
            result.Add(EvaluateMultiPointSpline(allPts, t));
        }
        return result;
    }

    private static PolyVec2 EvaluateMultiPointSpline(List<PolyVec2> pts, float t)
    {
        if (pts.Count == 1) return pts[0];
        if (pts.Count == 2) return pts[0].Lerp(pts[1], t);

        // 分段 Catmull-Rom 插值
        var segCount = pts.Count - 1;
        var scaledT = t * segCount;
        var idx = Math.Min((int)Math.Floor(scaledT), segCount - 1);
        var subT = scaledT - idx;

        var p0 = idx > 0 ? pts[idx - 1] : pts[idx];
        var p1 = pts[idx];
        var p2 = pts[idx + 1];
        var p3 = (idx + 2 < pts.Count) ? pts[idx + 2] : pts[idx + 1];

        // Catmull-Rom 公式
        var t2 = subT * subT;
        var t3 = t2 * subT;

        var x = 0.5 * ((2.0 * p1.X) +
                       (-p0.X + p2.X) * subT +
                       (2.0 * p0.X - 5.0 * p1.X + 4.0 * p2.X - p3.X) * t2 +
                       (-p0.X + 3.0 * p1.X - 3.0 * p2.X + p3.X) * t3);

        var y = 0.5 * ((2.0 * p1.Y) +
                       (-p0.Y + p2.Y) * subT +
                       (2.0 * p0.Y - 5.0 * p1.Y + 4.0 * p2.Y - p3.Y) * t2 +
                       (-p0.Y + 3.0 * p1.Y - 3.0 * p2.Y + p3.Y) * t3);

        return new PolyVec2(x, y);
    }

    private static int FindBestLandCell(
        CellGeometry geometry,
        CellFields fields,
        float seaLevel,
        int startCell,
        HashSet<int> used)
    {
        if (startCell >= 0 && startCell < geometry.Count && fields.Height[startCell] > seaLevel && !used.Contains(startCell))
        {
            return startCell;
        }

        var queue = new Queue<int>();
        var visited = new HashSet<int>();
        if (startCell >= 0 && startCell < geometry.Count)
        {
            queue.Enqueue(startCell);
            visited.Add(startCell);
        }

        var maxSearch = 80;
        var searched = 0;

        while (queue.Count > 0 && searched++ < maxSearch)
        {
            var curr = queue.Dequeue();
            if (fields.Height[curr] > seaLevel && !used.Contains(curr))
            {
                return curr;
            }

            var startEdge = geometry.CellNeighborStart[curr];
            var endEdge = geometry.CellNeighborStart[curr + 1];
            for (var k = startEdge; k < endEdge; k++)
            {
                var nb = geometry.CellNeighbors[k];
                if (visited.Add(nb))
                {
                    queue.Enqueue(nb);
                }
            }
        }

        return startCell;
    }

    private static void CarveContinentLandmass(
        CellGeometry geometry,
        CellFields fields,
        GenerationOptions options,
        MapBlueprint blueprint)
    {
        var w = geometry.Width;
        var h = geometry.Height;
        var seaLevel = options.SeaLevel;

        for (var c = 0; c < geometry.Count; c++)
        {
            var nx = (float)(geometry.CentroidX[c] / w);
            var ny = (float)(geometry.CentroidY[c] / h);

            // 计算该单元格到蓝图大陆各个核心区域的归一化加权势能 (Continent Potential)
            var maxPotential = 0f;

            foreach (var r in blueprint.Regions)
            {
                var dx = (nx - (float)r.Center.X);
                var dy = (ny - (float)r.Center.Y);
                var rad = r.ExtentRadius;
                var distSq = (dx * dx) / (rad * rad) + (dy * dy) / (rad * rad * 0.85f);
                if (distSq < 1.0f)
                {
                    var p = 1.0f - distSq;
                    if (p > maxPotential) maxPotential = p;
                }
            }

            // 也将大森林、大漠、主山轴线纳入陆地支撑
            foreach (var fz in blueprint.ForestZones)
            {
                var dx = (nx - (float)fz.Center.X) / fz.RadiusX;
                var dy = (ny - (float)fz.Center.Y) / fz.RadiusY;
                var distSq = dx * dx + dy * dy;
                if (distSq < 1.2f)
                {
                    var p = MathF.Max(0f, 1.0f - distSq / 1.2f);
                    if (p > maxPotential) maxPotential = p;
                }
            }

            foreach (var dz in blueprint.DesertZones)
            {
                var dx = (nx - (float)dz.Center.X) / dz.RadiusX;
                var dy = (ny - (float)dz.Center.Y) / dz.RadiusY;
                var distSq = dx * dx + dy * dy;
                if (distSq < 1.2f)
                {
                    var p = MathF.Max(0f, 1.0f - distSq / 1.2f);
                    if (p > maxPotential) maxPotential = p;
                }
            }

            // 东南半月港湾：在东南外缘形成自然海湾水域
            var isBayWater = false;
            var bayDx = (nx - 0.92f);
            var bayDy = (ny - 0.76f);
            if (bayDx * bayDx + bayDy * bayDy < 0.018f)
            {
                isBayWater = true;
            }

            // 极外围海岸带自然截断，保证大陆四周环海的宣纸留白
            if (nx < 0.06f || nx > 0.94f || ny < 0.10f || ny > 0.90f)
            {
                maxPotential *= 0.25f;
            }

            if (maxPotential > 0.10f && !isBayWater)
            {
                // 确保为坚实陆地
                var landElev = seaLevel + 0.08f + maxPotential * 0.22f;
                fields.Height[c] = MathF.Max(fields.Height[c], landElev);
                if (fields.Landform[c] == (byte)LandformType.Ocean)
                {
                    fields.Landform[c] = (byte)LandformType.Plain;
                    fields.Biome[c] = (byte)BiomeType.Grassland;
                }
            }
            else if (maxPotential <= 0.04f || isBayWater)
            {
                // 蓝图规划的大洋水域
                fields.Height[c] = MathF.Min(fields.Height[c], seaLevel - 0.15f);
                fields.Landform[c] = (byte)LandformType.Ocean;
                fields.Biome[c] = (byte)BiomeType.Ocean;
                fields.River[c] = 0f;
            }
        }
    }
}
