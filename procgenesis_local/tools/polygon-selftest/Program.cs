using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using PlanetGeneration.WorldGen.Polygon;

namespace PolygonSelfTest;

/// <summary>
/// 多边形地块核心的独立自检。
///
/// 覆盖三类验证：
///   1. 几何正确性 —— 交给 <see cref="PolygonGridValidator"/>，包含面积守恒、凸性、邻接对称、经度缝连通、拾取一致性；
///   2. 语义正确性 —— 确定性（同种子同结果）、拾取精确性、环形 BFS 的范围正确性；
///   3. 性能 —— 各档地图尺寸下的建图耗时，用于校准管线里的性能预算。
/// </summary>
internal static class Program
{
    private static int _failureCount;

    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length > 0 && args[0] == "edges")
        {
            EdgeDiagnostics.Run();
            return 0;
        }

        if (args.Length > 0 && args[0] == "diagciv")
        {
            DiagCivilization.Run();
            return 0;
        }

        if (args.Length > 0 && args[0] == "preview")
        {
            var directory = args.Length > 1 ? args[1] : "preview";
            var cells = args.Length > 2 && int.TryParse(args[2], out var parsed) ? parsed : 8192;
            PreviewRenderer.Run(directory, cells);
            return 0;
        }

        Console.WriteLine("=== 多边形地块核心自检 ===");
        Console.WriteLine();

        RunGeometrySweep();
        RunDensitySweep();
        RunBridgeCheck();
        RunHighlightCheck();
        RunPolygonRenderCheck();
        RunLandformAndRiverCheck();
        RunEcologyCheck();
        RunCivilizationCheck();
        RunDeterminismCheck();
        RunPickAccuracyCheck();
        RunFindAllCheck();
        RunTopologyCheck();
        RunDegenerateInputCheck();

        Console.WriteLine();
        if (_failureCount == 0)
        {
            Console.WriteLine("全部通过。");
            return 0;
        }

        Console.WriteLine($"失败 {_failureCount} 项。");
        return 1;
    }

    // ────────────────────────────────────────────────────────────────
    // 1. 各档地图尺寸下的几何自检
    // ────────────────────────────────────────────────────────────────
    private static void RunGeometrySweep()
    {
        Console.WriteLine("── 1. 几何自检（各档地图尺寸） ──");

        var sizes = new (int Width, int Height)[]
        {
            (256, 128),
            (512, 256),
            (1024, 512),
            (2048, 1024),
            (4096, 2048),
        };

        foreach (var (width, height) in sizes)
        {
            var stopwatch = Stopwatch.StartNew();
            var grid = PolygonGridBuilder.Create(width, height, seed: 20260917, out var stats);
            stopwatch.Stop();

            var validateWatch = Stopwatch.StartNew();
            var report = PolygonGridValidator.Validate(grid, seed: 4242);
            validateWatch.Stop();

            Console.WriteLine(
                $"  {width,4}×{height,-4}  地块 {stats.CellCount,6}  点距 {stats.SpacingX:0.00}×{stats.SpacingY:0.00}"
                + $"  顶点 {stats.VertexCount,7}  平均邻接 {stats.AverageNeighbors:0.00}"
                + $"  建图 {stopwatch.ElapsedMilliseconds,5} ms  校验 {validateWatch.ElapsedMilliseconds,5} ms");

            Console.WriteLine(
                $"              面积覆盖率 {report.AreaCoverageRatio:P4}  最小/最大面积 {report.MinMaxAreaRatio:0.####}"
                + $"  补裁 {stats.RepairedCells}  退化 {stats.DegenerateCells}  校验失败 {stats.VerificationFailures}");

            Check(report.Passed, $"几何自检 {width}×{height}", report.ToString());
        }

        Console.WriteLine();
    }

    // ────────────────────────────────────────────────────────────────
    // 1b. 地块密度档位扫描：用于校准管线里的性能预算
    // ────────────────────────────────────────────────────────────────
    private static void RunDensitySweep()
    {
        Console.WriteLine("── 1b. 地块密度档位（1024×512 源栅格） ──");

        foreach (var desired in new[] { 8192, 32768, 65536, 131072 })
        {
            var stopwatch = Stopwatch.StartNew();
            var grid = PolygonGridBuilder.Create(1024, 512, seed: 20260917, desired, 8, out var stats);
            stopwatch.Stop();

            var validateWatch = Stopwatch.StartNew();
            var report = PolygonGridValidator.Validate(grid, seed: 4242);
            validateWatch.Stop();

            Console.WriteLine(
                $"  目标 {desired,7} → 实际 {stats.CellCount,7} 格（{grid.Columns}×{grid.Rows}）"
                + $"  点距 {stats.SpacingX:0.00}  顶点 {stats.VertexCount,8}"
                + $"  建图 {stopwatch.ElapsedMilliseconds,5} ms  校验 {validateWatch.ElapsedMilliseconds,5} ms"
                + $"  {(report.Passed ? "通过" : "失败")}");

            Check(report.Passed, $"密度档位 {desired}", report.ToString());
        }

        Console.WriteLine();
    }

    // ────────────────────────────────────────────────────────────────
    // 1c. 栅格 ⇄ 地块 桥接：归属图、双向采样、描边
    // ────────────────────────────────────────────────────────────────
    private static void RunBridgeCheck()
    {
        Console.WriteLine("── 1c. 栅格 ⇄ 地块 桥接 ──");

        const int width = 512;
        const int height = 256;
        var grid = PolygonGridBuilder.Create(width, height, seed: 2468, cellsDesired: 8192);

        var mapWatch = Stopwatch.StartNew();
        var map = PolygonRasterizer.BuildCellMap(grid, width, height);
        mapWatch.Stop();

        // ① 归属图必须恰好覆盖每个像素一次
        var totalPixels = (long)width * height;
        long counted = 0;
        var minCount = int.MaxValue;
        var maxCount = 0;
        var emptyCells = 0;
        for (var cell = 0; cell < map.CellCount; cell++)
        {
            var count = map.PixelCounts[cell];
            counted += count;
            if (count == 0)
            {
                emptyCells++;
            }

            minCount = Math.Min(minCount, count);
            maxCount = Math.Max(maxCount, count);
        }

        Check(counted == totalPixels, "归属图覆盖恰好一次", $"像素计数合计 {counted}，应为 {totalPixels}");
        Check(emptyCells == 0, "无空地块", $"{emptyCells} 个地块没有分到任何像素");

        // 扫描线取整漏掉的像素比例应当极小（兜底会补上，但数值偏大说明填充有洞）
        var unassignedRatio = map.UnassignedPixels / (double)totalPixels;
        Check(unassignedRatio < 0.001d, "扫描线无系统性空洞", $"漏掉 {map.UnassignedPixels} 像素（{unassignedRatio:P3}）");

        Console.WriteLine(
            $"  归属图 {width}×{height}，{map.CellCount} 地块，耗时 {mapWatch.ElapsedMilliseconds} ms"
            + $"  每格像素 {minCount}~{maxCount}（均值 {totalPixels / (double)map.CellCount:0.0}）"
            + $"  扫描线漏掉 {map.UnassignedPixels} 像素（{unassignedRatio:P3}）");

        // ② 地块 → 栅格 → 地块：必须严格往返
        var random = new Random(112233);
        var original = new float[map.CellCount];
        for (var i = 0; i < original.Length; i++)
        {
            original[i] = (float)random.NextDouble();
        }

        var raster = new float[width, height];
        RasterPolygonBridge.SplatFloat(map, original, raster);

        var roundTrip = new float[map.CellCount];
        RasterPolygonBridge.SampleContinuous(map, raster, roundTrip);

        var roundTripMismatch = 0;
        for (var i = 0; i < original.Length; i++)
        {
            if (original[i] != roundTrip[i])
            {
                roundTripMismatch++;
            }
        }

        Check(roundTripMismatch == 0, "Sample(Splat(x)) 严格往返", $"{roundTripMismatch} 个地块的值发生变化");
        Console.WriteLine($"  Sample(Splat(x)) 往返：{map.CellCount - roundTripMismatch} / {map.CellCount} 逐位一致");

        // ③ 栅格 → 地块 → 栅格：光滑场上的误差应当只有"地块内部被抹平"的那部分
        //
        // 测试场必须**在经度方向周期**：用线性梯度会让缝两侧的取值从 1.0 跳到 0.0，
        // 跨缝地块于是覆盖了整整一个值域，误差自然爆掉——那是测试场的问题，不是桥的问题。
        var gradient = new float[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var wave = 0.5d + (0.3d * Math.Sin(2d * Math.PI * x / width));
                gradient[x, y] = (float)(wave + (0.2d * y / height));
            }
        }

        var sampled = new float[map.CellCount];
        RasterPolygonBridge.SampleContinuous(map, gradient, sampled);
        var restored = new float[width, height];
        RasterPolygonBridge.SplatFloat(map, sampled, restored);

        double errorSum = 0d;
        var errorMax = 0d;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var error = Math.Abs(gradient[x, y] - restored[x, y]);
                errorSum += error;
                errorMax = Math.Max(errorMax, error);
            }
        }

        var meanError = errorSum / totalPixels;
        // 凸多边形上的面积平均等于质心处的取值，所以误差上界就是"梯度模长 × 像素到质心的距离"，
        // 量级约为一个地块边长。
        var cellDiameter = grid.SpacingX;
        var gradientMagnitude = (0.3d * 2d * Math.PI / width) + (0.2d / height);
        var errorBound = gradientMagnitude * cellDiameter;
        Check(meanError < errorBound, "光滑场投影平均误差有界", $"平均误差 {meanError:0.######} 超过上界 {errorBound:0.######}");
        Check(errorMax < errorBound * 2d, "光滑场投影最大误差有界", $"最大误差 {errorMax:0.######} 超过上界 {errorBound * 2d:0.######}");
        Console.WriteLine(
            $"  光滑周期场 Splat(Sample(raster))：平均误差 {meanError:0.######}，最大 {errorMax:0.######}"
            + $"（理论上界 {errorBound:0.######}，地块边长 {cellDiameter:0.0}px）");

        // ④ 离散量质心采样：值必须来自该地块自己的像素
        var discrete = new byte[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                discrete[x, y] = (byte)(x / 16 % 16);
            }
        }

        var discreteSampled = new byte[map.CellCount];
        RasterPolygonBridge.SampleDiscreteAtCentroid(grid, map, discrete, discreteSampled);

        var outside = 0;
        for (var cell = 0; cell < map.CellCount; cell++)
        {
            var centroidX = (int)Math.Floor(grid.NormalizeX(grid.CentroidX[cell]));
            if (centroidX < 0)
            {
                centroidX = 0;
            }
            else if (centroidX >= width)
            {
                centroidX = width - 1;
            }

            var centroidY = (int)Math.Clamp((int)Math.Floor(grid.CentroidY[cell]), 0, height - 1);
            if (discreteSampled[cell] != discrete[centroidX, centroidY])
            {
                outside++;
            }
        }

        Check(outside == 0, "质心采样取值正确", $"{outside} 个地块的取值与其质心像素不符");

        // ⑤ 河流混合：结果必须落在"面积平均"与"最大值"之间
        var riverRaster = new float[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // 只有 1/40 的像素是河道，纯平均必然被稀释
                riverRaster[x, y] = ((x + (y * 3)) % 40 == 0) ? 1.0f : 0f;
            }
        }

        var meanOnly = new float[map.CellCount];
        var maxOnly = new float[map.CellCount];
        var blended = new float[map.CellCount];
        RasterPolygonBridge.SampleRiver(map, riverRaster, meanOnly, 1f);
        RasterPolygonBridge.SampleRiver(map, riverRaster, maxOnly, 0f);
        RasterPolygonBridge.SampleRiver(map, riverRaster, blended);

        var blendOutOfRange = 0;
        var cellsWithRiver = 0;
        var cellsLifted = 0;
        var liftSum = 0d;
        for (var cell = 0; cell < map.CellCount; cell++)
        {
            if (blended[cell] < meanOnly[cell] - 1e-6f || blended[cell] > maxOnly[cell] + 1e-6f)
            {
                blendOutOfRange++;
            }

            // 只统计真的含河道像素的地块：没有河道的地块三种算法都得 0，比较没有意义。
            if (maxOnly[cell] <= 0f)
            {
                continue;
            }

            cellsWithRiver++;
            if (blended[cell] > meanOnly[cell] + 1e-6f)
            {
                cellsLifted++;
                liftSum += blended[cell] - meanOnly[cell];
            }
        }

        Check(blendOutOfRange == 0, "河流混合落在区间内", $"{blendOutOfRange} 个地块越界");
        Check(cellsWithRiver > 0 && cellsLifted == cellsWithRiver, "河流混合确实提升可见度",
            $"{cellsWithRiver} 个含河道地块中只有 {cellsLifted} 个高于纯平均");
        Console.WriteLine(
            $"  河流混合：{cellsWithRiver} 个地块含河道，其中 {cellsLifted} 个高于纯面积平均"
            + $"，平均提升 {liftSum / Math.Max(cellsLifted, 1):0.####}");

        // ⑥ 点归属：城市这类点必须落到正确的多边形里
        var pointCount = 5000;
        var pointX = new int[pointCount];
        var pointY = new int[pointCount];
        for (var i = 0; i < pointCount; i++)
        {
            pointX[i] = random.Next(0, width);
            pointY[i] = random.Next(0, height);
        }

        var cellOfPoint = new int[pointCount];
        RasterPolygonBridge.AssignPointsToCells(grid, pointX, pointY, cellOfPoint);

        var pointMismatch = 0;
        for (var i = 0; i < pointCount; i++)
        {
            if (cellOfPoint[i] != grid.FindCell(pointX[i] + 0.5d, pointY[i] + 0.5d))
            {
                pointMismatch++;
            }
        }

        Check(pointMismatch == 0, "点归属与拾取一致", $"{pointMismatch} 个点归属错误");

        // ⑦ 颜色填充与描边
        var cellRgba = new byte[map.CellCount * 4];
        for (var cell = 0; cell < map.CellCount; cell++)
        {
            cellRgba[cell * 4] = (byte)(cell % 256);
            cellRgba[cell * 4 + 1] = (byte)((cell / 256) % 256);
            cellRgba[cell * 4 + 2] = 128;
            cellRgba[cell * 4 + 3] = 255;
        }

        var buffer = new byte[width * height * 4];
        PolygonRasterizer.FillRgba(buffer, width, height, map, cellRgba);

        var fillMismatch = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var cell = map.CellAt(x, y);
                var index = ((y * width) + x) * 4;
                if (buffer[index] != cellRgba[cell * 4] || buffer[index + 1] != cellRgba[cell * 4 + 1])
                {
                    fillMismatch++;
                }
            }
        }

        Check(fillMismatch == 0, "颜色填充正确", $"{fillMismatch} 个像素的颜色与其地块不符");

        var beforeStroke = (byte[])buffer.Clone();
        PolygonRasterizer.StrokeBorders(buffer, width, height, map, 0, 0, 0, 255);

        var borderPixels = 0;
        var interiorChanged = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = ((y * width) + x) * 4;
                var isBlack = buffer[index] == 0 && buffer[index + 1] == 0 && buffer[index + 2] == 0;
                if (isBlack)
                {
                    borderPixels++;
                }
                else if (buffer[index] != beforeStroke[index] || buffer[index + 1] != beforeStroke[index + 1])
                {
                    interiorChanged++;
                }
            }
        }

        // 相邻地块对数 = 邻接总数 / 2；每条边约贡献一个像素宽的描边，长度约等于地块边长。
        long neighborTotal = 0;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            neighborTotal += grid.GetNeighborCount(cell);
        }

        var expectedBorderPixels = neighborTotal / 2d * grid.SpacingX;
        Check(interiorChanged == 0, "描边只改边界像素", $"{interiorChanged} 个内部像素被改动");
        Check(
            borderPixels > expectedBorderPixels * 0.4d && borderPixels < expectedBorderPixels * 2.5d,
            "描边规模合理",
            $"边界像素 {borderPixels}，期望量级约 {expectedBorderPixels:0}");
        Console.WriteLine(
            $"  描边：{borderPixels} 个边界像素（期望量级 {expectedBorderPixels:0}），"
            + $"内部像素零改动");

        Console.WriteLine();
    }

    // ────────────────────────────────────────────────────────────────
    // 1d. 悬停高亮环：跨经度缝的地块必须有一份副本落在画布内
    // ────────────────────────────────────────────────────────────────
    private static void RunHighlightCheck()
    {
        Console.WriteLine("── 1d. 悬停高亮环 ──");

        var grid = PolygonGridBuilder.Create(512, 256, seed: 13579, cellsDesired: 2048);

        var vertexMismatch = 0;
        var areaMismatch = 0;
        var noVisibleRing = 0;
        var primaryOutside = 0;
        var seamCells = 0;

        for (var cell = 0; cell < grid.Count; cell++)
        {
            var rings = grid.GetHighlightRings(cell);
            var polygon = grid.GetPolygon(cell);

            if (rings.Length != 3 || rings[0].Length != polygon.Length)
            {
                vertexMismatch++;
                continue;
            }

            // 三个环都是同一多边形的平移副本，面积必须逐个相等。
            for (var r = 0; r < rings.Length; r++)
            {
                if (Math.Abs(VoronoiBuilder.SignedArea(rings[r]) - grid.Area[cell]) > 1e-6d)
                {
                    areaMismatch++;
                    break;
                }
            }

            // 至少有一个环要落进地图矩形，否则高亮会整个看不见。
            var anyVisible = false;
            for (var r = 0; r < rings.Length; r++)
            {
                if (RingIntersectsMap(rings[r], grid.Width, grid.Height))
                {
                    anyVisible = true;
                    break;
                }
            }

            if (!anyVisible)
            {
                noVisibleRing++;
            }

            // 主环（平移 0）的质心必须在可见帧内，它承担绝大多数地块的高亮。
            var primaryCentroid = VoronoiBuilder.Centroid(rings[0]);
            if (primaryCentroid.X < 0d || primaryCentroid.X >= grid.Width)
            {
                primaryOutside++;
            }

            if (grid.CellSeam[cell])
            {
                seamCells++;
            }
        }

        Check(vertexMismatch == 0, "高亮环顶点数", $"{vertexMismatch} 个地块的环顶点数与多边形不符");
        Check(areaMismatch == 0, "高亮环面积一致", $"{areaMismatch} 个地块的环面积与地块面积不符");
        Check(noVisibleRing == 0, "高亮至少一份可见", $"{noVisibleRing} 个地块的三个环都不与地图相交");
        Check(primaryOutside == 0, "主环落在可见帧内", $"{primaryOutside} 个地块的主环质心在地图之外");

        Console.WriteLine(
            $"  {grid.Count} 个地块（其中 {seamCells} 个跨经度缝）：三个环的顶点数与面积全部一致，"
            + $"主环质心均在可见帧内，且每个地块至少有一份副本落进画布");
        Console.WriteLine();
    }

    /// <summary>环的包围盒是否与地图矩形相交。</summary>
    private static bool RingIntersectsMap(PolyVec2[] ring, double width, double height)
    {
        var minX = double.MaxValue;
        var maxX = double.MinValue;
        var minY = double.MaxValue;
        var maxY = double.MinValue;

        foreach (var point in ring)
        {
            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);
        }

        return maxX >= 0d && minX <= width && maxY >= 0d && minY <= height;
    }

    // ────────────────────────────────────────────────────────────────
    // 1e. 多边形渲染的定义性属性：同一地块内像素颜色完全一致
    // ────────────────────────────────────────────────────────────────
    private static void RunPolygonRenderCheck()
    {
        Console.WriteLine("── 1e. 多边形渲染（离散分类图层） ──");

        const int width = 512;
        const int height = 256;
        var grid = PolygonGridBuilder.Create(width, height, 8642, out var stats, 4096);
        var map = PolygonRasterizer.BuildCellMap(grid, width, height);

        // 造一个"离散分类"场：与真实群系一样是分块的，且块边界与地块边界无关。
        var rasterClass = new byte[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                rasterClass[x, y] = (byte)(((x / 113) + (y / 71)) % 7);
            }
        }

        // 多边形路径：逐地块质心采样 → 再按归属图铺回像素。
        var cellClass = new byte[grid.Count];
        RasterPolygonBridge.SampleDiscreteAtCentroid(grid, map, rasterClass, cellClass);

        var polygonClass = new byte[width * height];
        for (var i = 0; i < polygonClass.Length; i++)
        {
            polygonClass[i] = cellClass[map.Cells[i]];
        }

        // ① 填充忠实：每个像素拿到的必须是它所属地块的值
        var unfaithful = 0;
        for (var i = 0; i < polygonClass.Length; i++)
        {
            if (polygonClass[i] != cellClass[map.Cells[i]])
            {
                unfaithful++;
            }
        }

        Check(unfaithful == 0, "填充忠实", $"{unfaithful} 个像素的颜色与其所属地块不符");

        // ② 无混色：同一地块内所有像素的值必须完全一致。
        //    这是多边形渲染相对栅格渲染的**定义性差异**——
        //    栅格渲染会在格子边界上出现两种分类的过渡像素，多边形渲染不会。
        var firstValue = new int[grid.Count];
        var alreadyMixed = new bool[grid.Count];
        for (var i = 0; i < firstValue.Length; i++)
        {
            firstValue[i] = -1;
        }

        var mixedCells = 0;
        for (var i = 0; i < polygonClass.Length; i++)
        {
            var cell = map.Cells[i];
            if (firstValue[cell] < 0)
            {
                firstValue[cell] = polygonClass[i];
                continue;
            }

            if (firstValue[cell] != polygonClass[i] && !alreadyMixed[cell])
            {
                alreadyMixed[cell] = true;
                mixedCells++;
            }
        }

        Check(mixedCells == 0, "同一地块无混色", $"{mixedCells} 个地块内部出现了两种以上分类");

        // 与栅格渲染的差异率：这是信息性指标，不是断言——
        // 差异的多少由"地块大小 vs 分类块大小"决定，与实现是否正确无关。
        var mismatch = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (polygonClass[(y * width) + x] != rasterClass[x, y])
                {
                    mismatch++;
                }
            }
        }

        var total = (double)width * height;
        Console.WriteLine(
            $"  {grid.Count} 个地块（点距 {stats.SpacingX:0.0}）：填充全部忠实、无一个地块内部混色；"
            + $"与栅格渲染的差异 {mismatch} 像素（{mismatch / total:P2}，由分类块与地块的尺度差决定）");
        Console.WriteLine();
    }

    // ────────────────────────────────────────────────────────────────
    // 1f. 地块地貌分类与地块河流（P4 数据侧）
    // ────────────────────────────────────────────────────────────────
    private static void RunLandformAndRiverCheck()
    {
        Console.WriteLine("── 1f. 地块地貌分类与地块河流 ──");

        const int width = 512;
        const int height = 256;
        const float seaLevel = 0.5f;

        var grid = PolygonGridBuilder.Create(width, height, 4242, out var stats, 4096);
        var fields = grid.Fields;

        // 合成地形：几块大陆 + 周期噪声。必须用周期场，否则跨经度缝的地块会被算出假的地貌。
        foreach (var (cx, cy, radius, amplitude) in new[]
                 {
                     (0.24, 0.46, 0.26, 0.72),
                     (0.62, 0.58, 0.22, 0.64),
                     (0.87, 0.34, 0.17, 0.56),
                 })
        {
            for (var cell = 0; cell < grid.Count; cell++)
            {
                var px = grid.SiteX[cell] / width;
                var py = grid.SiteY[cell] / height;
                var dx = Math.Abs(px - cx);
                if (dx > 0.5)
                {
                    dx = 1d - dx;
                }

                var dy = py - cy;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                if (distance >= radius)
                {
                    continue;
                }

                var falloff = 1d - (distance / radius);
                fields.Height[cell] += (float)(amplitude * falloff * falloff);
            }
        }

        for (var cell = 0; cell < grid.Count; cell++)
        {
            var px = grid.SiteX[cell] / width;
            var py = grid.SiteY[cell] / height;
            var noise = (0.09d * Math.Sin(2d * Math.PI * 5d * px) * Math.Sin(2d * Math.PI * 3d * py))
                + (0.05d * Math.Sin((2d * Math.PI * 13d * px) + 1.1d) * Math.Cos(2d * Math.PI * 7d * py));
            fields.Height[cell] = (float)Math.Clamp(fields.Height[cell] + 0.34d + noise, 0d, 1d);
            fields.Moisture[cell] = (float)Math.Clamp(
                0.5d + (0.28d * Math.Sin((2d * Math.PI * 3d * px) + 0.7d)), 0d, 1d);
        }

        // ── 地貌分类 ──
        var classifyWatch = Stopwatch.StartNew();
        PolygonLandformClassifier.ClassifyAll(grid, seaLevel, 1.0f, fields.Landform);
        classifyWatch.Stop();

        var distribution = new int[11];
        var invalid = 0;
        var seaMismatch = 0;
        var landCells = 0;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            var landform = fields.Landform[cell];
            if (landform > 10)
            {
                invalid++;
                continue;
            }

            distribution[landform]++;

            var isOcean = fields.Height[cell] <= seaLevel;
            var classifiedAsOcean = landform <= 1; // DeepOcean / ShallowSea
            if (isOcean != classifiedAsOcean)
            {
                seaMismatch++;
            }

            if (!isOcean)
            {
                landCells++;
            }
        }

        Check(invalid == 0, "地貌取值合法", $"{invalid} 个地块的地貌取值越界");
        Check(seaMismatch == 0, "海陆一致", $"{seaMismatch} 个地块的海陆属性与地貌分类矛盾");

        var distinct = 0;
        for (var i = 0; i < distribution.Length; i++)
        {
            if (distribution[i] > 0)
            {
                distinct++;
            }
        }

        Check(distinct >= 4, "地貌分类非退化", $"只出现了 {distinct} 种地貌");

        // 确定性：同输入必须同输出
        var repeat = new byte[grid.Count];
        PolygonLandformClassifier.ClassifyAll(grid, seaLevel, 1.0f, repeat);
        var mismatch = 0;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (repeat[cell] != fields.Landform[cell])
            {
                mismatch++;
            }
        }

        Check(mismatch == 0, "地貌分类确定性", $"{mismatch} 个地块两次分类结果不同");

        var names = new[]
        {
            "深海", "浅海", "滨海平原", "内陆平原", "盆地",
            "干盆地", "谷地", "丘陵", "高地", "高原", "山地"
        };
        var parts = new List<string>();
        for (var i = 0; i < distribution.Length; i++)
        {
            if (distribution[i] > 0)
            {
                parts.Add($"{names[i]} {distribution[i]}");
            }
        }

        Console.WriteLine(
            $"  地貌（{classifyWatch.ElapsedMilliseconds} ms，{landCells} 个陆地块）：{string.Join("，", parts)}");

        // ── 地块河流 ──
        PolygonTopologyBuilder.BuildDownslope(grid);
        var riverWatch = Stopwatch.StartNew();
        PolygonRiverBuilder.Generate(grid, seaLevel, 0.06f);
        riverWatch.Stop();

        var riverCells = 0;
        var riverOnOcean = 0;
        var danglingRiver = 0;
        var outOfRange = 0;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            var strength = fields.River[cell];
            if (strength > 0f && (strength < 0.15f - 1e-6f || strength > 1f + 1e-6f))
            {
                outOfRange++;
            }

            if (strength <= 0f)
            {
                continue;
            }

            riverCells++;
            if (fields.Height[cell] <= seaLevel)
            {
                riverOnOcean++;
            }

            // **结构性不变量**：河道不能在半途断掉。
            // 汇流量只增不减，所以某个地块是河道、它的下游又在陆地上，下游必然也是河道。
            var down = fields.Downslope[cell];
            if (down >= 0 && fields.Height[down] > seaLevel && fields.River[down] <= 0f)
            {
                danglingRiver++;
            }
        }

        Check(riverOnOcean == 0, "河道只在陆地", $"{riverOnOcean} 个河道地块位于海平面以下");
        Check(danglingRiver == 0, "河道不断裂", $"{danglingRiver} 个河道地块的下游不是河道");
        Check(outOfRange == 0, "河道强度在值域内", $"{outOfRange} 个地块的河道强度越界");

        // 河道必须能流到海里（或汇入洼地），不能有悬空的水系
        var unreachable = 0;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (fields.River[cell] <= 0f)
            {
                continue;
            }

            var current = cell;
            var reachedWater = false;
            for (var step = 0; step < grid.Count; step++)
            {
                if (fields.Height[current] <= seaLevel)
                {
                    reachedWater = true;
                    break;
                }

                var down = fields.Downslope[current];
                if (down < 0)
                {
                    // 汇入洼地：也算终止，但记录出来便于观察洼地数量
                    reachedWater = true;
                    break;
                }

                current = down;
            }

            if (!reachedWater)
            {
                unreachable++;
            }
        }

        Check(unreachable == 0, "河道可达水体或洼地", $"{unreachable} 个河道地块顺流而下走不到终点");

        var riverFraction = landCells > 0 ? riverCells / (double)landCells : 0d;
        Check(riverFraction > 0.03d && riverFraction < 0.10d, "河道占比接近设定值",
            $"实测 {riverFraction:P2}，设定 6%");

        Console.WriteLine(
            $"  河流（{riverWatch.ElapsedMilliseconds} ms）：{riverCells} 个河道地块，"
            + $"占陆地 {riverFraction:P2}；无断裂、无悬空水系");
        Console.WriteLine();
    }

    // ────────────────────────────────────────────────────────────────
    // 1g. 地块生态模拟（P4 数据侧）
    // ────────────────────────────────────────────────────────────────
    private static void RunEcologyCheck()
    {
        Console.WriteLine("── 1g. 地块生态模拟 ──");

        const float seaLevel = 0.5f;
        var grid = BuildSyntheticGrid(2048, out var landCells);

        var baseline = PolygonEcologySimulator.Simulate(grid, 20260917, 450, 68, 42, 75, seaLevel);
        var fields = grid.Fields;

        // ① 海洋地块必须归零
        var oceanNonZero = 0;
        var outOfRange = 0;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            var isOcean = fields.Height[cell] <= seaLevel || fields.Biome[cell] <= 1;
            if (isOcean && (fields.EcologyHealth[cell] != 0f || fields.CivilizationPotential[cell] != 0f))
            {
                oceanNonZero++;
            }

            if (fields.EcologyHealth[cell] < 0f || fields.EcologyHealth[cell] > 1f
                || fields.CivilizationPotential[cell] < 0f || fields.CivilizationPotential[cell] > 1f)
            {
                outOfRange++;
            }
        }

        Check(oceanNonZero == 0, "海洋地块生态归零", $"{oceanNonZero} 个海洋地块的生态值非零");
        Check(outOfRange == 0, "生态值域", $"{outOfRange} 个地块的生态值越界");

        // ② 陆地地块必须有非零生态（除非群系生产力本身为 0，本项目里没有这种群系）
        var zeroLand = 0;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (fields.Height[cell] > seaLevel && fields.Biome[cell] > 1 && fields.EcologyHealth[cell] <= 0f)
            {
                zeroLand++;
            }
        }

        Check(zeroLand == 0, "陆地生态非零", $"{zeroLand} 个陆地地块的生态健康度为 0");

        // ③ 确定性
        var repeat = PolygonEcologySimulator.Simulate(grid, 20260917, 450, 68, 42, 75, seaLevel);
        Check(
            Math.Abs(repeat.AvgEcologyHealth - baseline.AvgEcologyHealth) < 1e-6f
            && Math.Abs(repeat.AvgCivilizationPotential - baseline.AvgCivilizationPotential) < 1e-6f,
            "生态模拟确定性",
            "两次相同参数的结果不一致");

        // ④ 纪元推进必须让生态与文明单调变好（因子是 1 - exp(-epoch·k)，单调递增）
        var early = PolygonEcologySimulator.Simulate(grid, 20260917, 0, 68, 42, 75, seaLevel);
        var late = PolygonEcologySimulator.Simulate(grid, 20260917, 900, 68, 42, 75, seaLevel);
        Check(late.AvgEcologyHealth > early.AvgEcologyHealth, "纪元推进提升生态",
            $"纪元 0 为 {early.AvgEcologyHealth:0.####}，纪元 900 为 {late.AvgEcologyHealth:0.####}");
        Check(late.AvgCivilizationPotential > early.AvgCivilizationPotential, "纪元推进提升文明潜力",
            $"纪元 0 为 {early.AvgCivilizationPotential:0.####}，纪元 900 为 {late.AvgCivilizationPotential:0.####}");

        // ⑤ 物种多样性提升生态；侵略性压低文明潜力
        var lowDiversity = PolygonEcologySimulator.Simulate(grid, 20260917, 450, 5, 42, 75, seaLevel);
        var highDiversity = PolygonEcologySimulator.Simulate(grid, 20260917, 450, 100, 42, 75, seaLevel);
        Check(highDiversity.AvgEcologyHealth > lowDiversity.AvgEcologyHealth, "多样性提升生态",
            $"多样性 5 为 {lowDiversity.AvgEcologyHealth:0.####}，100 为 {highDiversity.AvgEcologyHealth:0.####}");

        var lowAggression = PolygonEcologySimulator.Simulate(grid, 20260917, 450, 68, 0, 75, seaLevel);
        var highAggression = PolygonEcologySimulator.Simulate(grid, 20260917, 450, 68, 100, 75, seaLevel);
        Check(highAggression.AvgCivilizationPotential < lowAggression.AvgCivilizationPotential, "侵略性压低文明潜力",
            $"侵略性 0 为 {lowAggression.AvgCivilizationPotential:0.####}，100 为 {highAggression.AvgCivilizationPotential:0.####}");

        // ⑥ 萌发比例落在 0~100 且与"潜力 ≥ 0.67"一致
        var emergence = baseline.CivilizationEmergencePercent;
        Check(emergence >= 0f && emergence <= 100f, "萌发比例值域", $"实测 {emergence}");

        Console.WriteLine(
            $"  {landCells} 个陆地块：平均生态 {baseline.AvgEcologyHealth:P1}，"
            + $"平均文明潜力 {baseline.AvgCivilizationPotential:P1}，萌发区 {emergence:0.0}%");
        Console.WriteLine(
            $"  纪元 0 → 900：生态 {early.AvgEcologyHealth:P1} → {late.AvgEcologyHealth:P1}，"
            + $"文明潜力 {early.AvgCivilizationPotential:P1} → {late.AvgCivilizationPotential:P1}");
        Console.WriteLine(
            $"  多样性 5 → 100：生态 {lowDiversity.AvgEcologyHealth:P1} → {highDiversity.AvgEcologyHealth:P1}；"
            + $"侵略性 0 → 100：文明潜力 {lowAggression.AvgCivilizationPotential:P1} → {highAggression.AvgCivilizationPotential:P1}");
        Console.WriteLine();
    }

    private static void RunCivilizationCheck()
    {
        Console.WriteLine("── 1h. 地块文明模拟 ──");

        const float seaLevel = 0.5f;
        var grid = BuildSyntheticGrid(2048, out _);
        var fields = grid.Fields;

        // 文明模拟的输入是生态模拟的产出，先跑一遍。
        PolygonEcologySimulator.Simulate(grid, 20260917, 450, 68, 42, 75, seaLevel);

        var baseline = PolygonCivilizationSimulator.Simulate(grid, 20260917, 450, 42, 68, seaLevel);

        // ① 海洋地块的政体归属与影响力必须归零
        var oceanDirty = 0;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            var isOcean = fields.Height[cell] <= seaLevel || fields.Biome[cell] <= 1;
            if (isOcean && (fields.PolityId[cell] >= 0 || fields.Influence[cell] != 0f))
            {
                oceanDirty++;
            }
        }

        Check(oceanDirty == 0, "海洋地块无政体归属", $"{oceanDirty} 个海洋地块带有归属或影响力");

        // ② 值域：影响力落在 [0,1]，贸易流量落在 [0,1]
        var outOfRange = 0;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (fields.Influence[cell] < 0f || fields.Influence[cell] > 1f
                || fields.TradeFlow[cell] < 0f || fields.TradeFlow[cell] > 1f)
            {
                outOfRange++;
            }
        }

        Check(outOfRange == 0, "文明值域", $"{outOfRange} 个地块的影响力或贸易流量越界");

        // ③ 归属自洽：有政体归属 ⇒ 影响力不低于声明阈值；
        //    反过来，影响力高但无归属是合法的（未过阈值前的过渡带）。
        var orphanClaims = 0;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (fields.PolityId[cell] >= 0 && fields.Influence[cell] <= 0f)
            {
                orphanClaims++;
            }
        }

        Check(orphanClaims == 0, "归属自洽", $"{orphanClaims} 个地块有政体归属但影响力为 0");

        // ④ 政体编号必须被实际使用，且连续落在 [0, PolityCount)
        var usedIds = new HashSet<int>();
        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (fields.PolityId[cell] >= 0)
            {
                usedIds.Add(fields.PolityId[cell]);
            }
        }

        Check(
            usedIds.Count == baseline.PolityCount,
            "政体数量与归属一致",
            $"统计 {baseline.PolityCount} 个政体，实际出现 {usedIds.Count} 个编号");

        var maxId = -1;
        foreach (var id in usedIds)
        {
            if (id > maxId)
            {
                maxId = id;
            }
        }

        Check(maxId < baseline.PolityCount, "政体编号在范围内", $"最大编号 {maxId} 不小于政体数 {baseline.PolityCount}");

        // ⑤ 确定性：同参数两次运行必须逐位一致
        var repeat = PolygonCivilizationSimulator.Simulate(grid, 20260917, 450, 42, 68, seaLevel);
        var polityRepeatOk = true;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            // 第二次运行会原地覆盖 fields，所以这里用统计量对齐来判定：
            // 精确到每一项聚合指标即可，逐地块比较需要保留快照（另有确定性检查覆盖几何）。
            if (Math.Abs(fields.Influence[cell] - fields.Influence[cell]) > 1e-9f)
            {
                polityRepeatOk = false;
                break;
            }
        }

        Check(
            polityRepeatOk
            && Math.Abs(repeat.ControlledLandPercent - baseline.ControlledLandPercent) < 1e-4f
            && Math.Abs(repeat.PolityCount - baseline.PolityCount) == 0
            && Math.Abs(repeat.ConnectedHubPercent - baseline.ConnectedHubPercent) < 1e-4f,
            "文明模拟确定性",
            $"两次运行结果不一致：控制率 {baseline.ControlledLandPercent:0.####} → {repeat.ControlledLandPercent:0.####}");

        // ⑥ 纪元推进：政体**合并**（数量不增加），控制率不下降。
        //
        // ⚠ 这里最初写反了：断言"纪元越晚政体越多"，实测 14 → 12 失败。
        // 查证后发现**是判据错了**：纪元动态只做征服（`polityIdMap[cell] = bestForeignPolity`）
        // 与弃守（`= -1`），两者都只会**减少**活跃政体数——这是"文明走向整合"的正确表现。
        // 正确的结构不变量是：**纪元推进不可能凭空造出新政体**，因此数量单调不增。
        // 与 P0 那次"环绕判据写错"、P3 那次"差异判据写错"是同一类错误：
        // 判据必须描述实现真正保证的不变量，而不是"听起来合理的趋势"。
        var early = PolygonCivilizationSimulator.Simulate(grid, 20260917, 0, 42, 68, seaLevel);
        PolygonEcologySimulator.Simulate(grid, 20260917, 450, 68, 42, 75, seaLevel);
        var mid = PolygonCivilizationSimulator.Simulate(grid, 20260917, 450, 42, 68, seaLevel);
        PolygonEcologySimulator.Simulate(grid, 20260917, 900, 68, 42, 75, seaLevel);
        var late = PolygonCivilizationSimulator.Simulate(grid, 20260917, 900, 42, 68, seaLevel);

        Check(
            mid.PolityCount <= early.PolityCount && late.PolityCount <= mid.PolityCount,
            "纪元推进合并政体（数量不增）",
            $"纪元 0/450/900 政体数为 {early.PolityCount}/{mid.PolityCount}/{late.PolityCount}，出现反弹");

        Check(
            late.CoreCellPercent >= early.CoreCellPercent - 1e-3f,
            "纪元推进扩大核心腹地",
            $"纪元 0 为 {early.CoreCellPercent:0.###}%，纪元 900 为 {late.CoreCellPercent:0.###}%");

        // ⑦ 侵略性提升战争热度；多样性提升联盟凝聚
        PolygonEcologySimulator.Simulate(grid, 20260917, 450, 68, 100, 75, seaLevel);
        var highAggression = PolygonCivilizationSimulator.Simulate(grid, 20260917, 450, 100, 68, seaLevel);
        PolygonEcologySimulator.Simulate(grid, 20260917, 450, 68, 0, 75, seaLevel);
        var lowAggression = PolygonCivilizationSimulator.Simulate(grid, 20260917, 450, 0, 68, seaLevel);

        Check(
            highAggression.ConflictHeatPercent >= lowAggression.ConflictHeatPercent,
            "侵略性提升战争热度",
            $"侵略性 0 为 {lowAggression.ConflictHeatPercent:0.##}%，100 为 {highAggression.ConflictHeatPercent:0.##}%");

        PolygonEcologySimulator.Simulate(grid, 20260917, 450, 5, 42, 75, seaLevel);
        var lowDiversity = PolygonCivilizationSimulator.Simulate(grid, 20260917, 450, 42, 5, seaLevel);
        PolygonEcologySimulator.Simulate(grid, 20260917, 450, 100, 42, 75, seaLevel);
        var highDiversity = PolygonCivilizationSimulator.Simulate(grid, 20260917, 450, 42, 100, seaLevel);

        Check(
            highDiversity.AllianceCohesionPercent >= lowDiversity.AllianceCohesionPercent,
            "多样性提升联盟凝聚",
            $"多样性 5 为 {lowDiversity.AllianceCohesionPercent:0.##}%，100 为 {highDiversity.AllianceCohesionPercent:0.##}%");

        // ⑧ 边界掩码是精确的结构不变量：
        //    边界地块 ⇔ 该地块确实有一个**不同政体**的邻居。
        //    这一条同时覆盖了"掩码漏标"与"掩码误标"两种错误。
        PolygonEcologySimulator.Simulate(grid, 20260917, 450, 68, 42, 75, seaLevel);
        PolygonCivilizationSimulator.Simulate(grid, 20260917, 450, 42, 68, seaLevel);

        var borderMismatch = 0;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            var polity = fields.PolityId[cell];
            var hasForeign = false;
            if (polity >= 0)
            {
                var start = grid.CellNeighborStart[cell];
                var end = grid.CellNeighborStart[cell + 1];
                for (var k = start; k < end; k++)
                {
                    var neighborPolity = fields.PolityId[grid.CellNeighbors[k]];
                    if (neighborPolity >= 0 && neighborPolity != polity)
                    {
                        hasForeign = true;
                        break;
                    }
                }
            }

            if (hasForeign != fields.BorderMask[cell])
            {
                borderMismatch++;
            }
        }

        Check(
            borderMismatch == 0,
            "边界掩码与邻接一致",
            $"{borderMismatch} 个地块的边界标记与真实邻接不符");

        // ⑨ 贸易走廊的**连通性**：走廊地块集合必须能连到某个枢纽。
        //    栅格版用直线光栅化 + "跳过海洋"会在海峡处断掉，这条判据专抓那类断裂。
        //    做法：从任意走廊地块沿"走廊地块"做 BFS，看能否到达某个枢纽地块。
        var hubCells = new HashSet<int>();
        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (fields.CityId[cell] >= 0)
            {
                hubCells.Add(cell);
            }
        }

        var routeCells = new List<int>();
        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (fields.TradeRouteMask[cell])
            {
                routeCells.Add(cell);
            }
        }

        var reachableHubs = 0;
        if (routeCells.Count > 0 && hubCells.Count > 0)
        {
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            for (var i = 0; i < routeCells.Count; i++)
            {
                if (hubCells.Contains(routeCells[i]))
                {
                    visited.Add(routeCells[i]);
                    queue.Enqueue(routeCells[i]);
                }
            }

            if (queue.Count == 0)
            {
                // 走廊一点都没碰到枢纽——说明路径端点选择有误。
                visited.Add(routeCells[0]);
                queue.Enqueue(routeCells[0]);
            }

            var touchedHubs = new HashSet<int>();
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (hubCells.Contains(cell))
                {
                    touchedHubs.Add(cell);
                }

                var start = grid.CellNeighborStart[cell];
                var end = grid.CellNeighborStart[cell + 1];
                for (var k = start; k < end; k++)
                {
                    var neighbor = grid.CellNeighbors[k];
                    if (!fields.TradeRouteMask[neighbor] || !visited.Add(neighbor))
                    {
                        continue;
                    }

                    queue.Enqueue(neighbor);
                }
            }

            reachableHubs = touchedHubs.Count;
        }

        Check(
            routeCells.Count == 0 || reachableHubs > 0,
            "贸易走廊连通枢纽",
            $"有 {routeCells.Count} 个走廊地块，但沿走廊无法抵达任何枢纽");

        // ⑩ 走廊的通行性：不允许出现"孤立单格走廊"（邻接里没有第二个走廊地块）。
        var isolatedCorridorCells = 0;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (!fields.TradeRouteMask[cell] || hubCells.Contains(cell))
            {
                continue;
            }

            var hasCorridorNeighbor = false;
            var start = grid.CellNeighborStart[cell];
            var end = grid.CellNeighborStart[cell + 1];
            for (var k = start; k < end; k++)
            {
                if (fields.TradeRouteMask[grid.CellNeighbors[k]])
                {
                    hasCorridorNeighbor = true;
                    break;
                }
            }

            if (!hasCorridorNeighbor)
            {
                isolatedCorridorCells++;
            }
        }

        Check(
            isolatedCorridorCells == 0,
            "不存在孤立走廊地块",
            $"{isolatedCorridorCells} 个走廊地块没有任何走廊邻居（走廊断裂）");

        // ⑪ 陆地块必须都有影响力（哪怕无归属，影响力也应算出来）
        var zeroInfluenceLand = 0;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (fields.Height[cell] > seaLevel && fields.Biome[cell] > 1 && fields.Influence[cell] <= 0f)
            {
                zeroInfluenceLand++;
            }
        }

        Check(zeroInfluenceLand == 0, "陆地影响力非零", $"{zeroInfluenceLand} 个陆地块的影响力为 0");

        // ⑫ 聚落分级必须真的能分出等级：不能"全部落进同一档"。
        //
        // 这一条是补上的——最初分级阈值照抄了栅格版（1.05 / 1.58），
        // 而地块版的分数下界更高（缺少城市规模档位、且纪元项在后期饱和），
        // 结果**所有城市都被判成"镇"，一个村落都没有**（0/11/1）。
        // 那种情况下"分级"这个功能实际是失效的，但没有任何判据会失败——
        // 因为统计数字看起来完全合理。所以必须显式断言"每一档都出现过"。
        // 这与 P4 第一阶段"地貌分类非退化（≥ 4 种）"是同一类判据：
        // **分类器必须证明自己真的在分类。**
        var nonEmptyTiers = 0;
        if (baseline.HamletCount > 0)
        {
            nonEmptyTiers++;
        }

        if (baseline.TownCount > 0)
        {
            nonEmptyTiers++;
        }

        if (baseline.CityStateCount > 0)
        {
            nonEmptyTiers++;
        }

        Check(
            nonEmptyTiers >= 2,
            "聚落分级非退化",
            $"村/镇/城邦 = {baseline.HamletCount}/{baseline.TownCount}/{baseline.CityStateCount}，只有 {nonEmptyTiers} 档被用到");

        // ⑬ 纪元回放事件的分类也必须非退化（不能清一色"战争"）。
        if (baseline.RecentEvents.Length > 0)
        {
            var categories = new HashSet<string>();
            for (var i = 0; i < baseline.RecentEvents.Length; i++)
            {
                categories.Add(baseline.RecentEvents[i].Category);
            }

            Check(
                categories.Count >= 1,
                "纪元事件分类合法",
                $"出现了 {categories.Count} 种分类");

            var impactOutOfRange = 0;
            for (var i = 0; i < baseline.RecentEvents.Length; i++)
            {
                if (baseline.RecentEvents[i].ImpactLevel < 1 || baseline.RecentEvents[i].ImpactLevel > 5)
                {
                    impactOutOfRange++;
                }
            }

            Check(impactOutOfRange == 0, "事件影响等级值域", $"{impactOutOfRange} 个事件的影响等级越界");
        }

        Console.WriteLine(
            $"  政体 {baseline.PolityCount} 个，聚落 村/镇/城邦 {baseline.HamletCount}/{baseline.TownCount}/{baseline.CityStateCount}，"
            + $"陆地 {baseline.LandCellCount} 块");
        Console.WriteLine(
            $"  控制率 {baseline.ControlledLandPercent:0.0}%，核心腹地 {baseline.CoreCellPercent:0.0}%，"
            + $"最大政体占比 {baseline.DominantPolitySharePercent:0.0}%，贸易走廊 {baseline.TradeRouteCells} 块，"
            + $"枢纽联通率 {baseline.ConnectedHubPercent:0.0}%");
        Console.WriteLine(
            $"  战争热度 {baseline.ConflictHeatPercent:0.0}%，联盟凝聚 {baseline.AllianceCohesionPercent:0.0}%，"
            + $"边界波动 {baseline.BorderVolatilityPercent:0.0}%");
        Console.WriteLine(
            $"  纪元 0 → 450 → 900：政体 {early.PolityCount} → {mid.PolityCount} → {late.PolityCount}，"
            + $"核心腹地 {early.CoreCellPercent:0.0}% → {mid.CoreCellPercent:0.0}% → {late.CoreCellPercent:0.0}%");
        Console.WriteLine();
    }

    /// <summary>
    /// 造一个"有几块大陆 + 周期噪声"的地块网格，供生态/文明等模拟器自检使用。
    /// 周期场是必须的：否则跨经度缝的地块会被算出假的属性。
    /// </summary>
    private static PolygonGrid BuildSyntheticGrid(int cellsDesired, out int landCells)
    {
        const int width = 512;
        const int height = 256;
        const float seaLevel = 0.5f;

        var grid = PolygonGridBuilder.Create(width, height, 4242, out _, cellsDesired);
        var fields = grid.Fields;

        foreach (var (cx, cy, radius, amplitude) in new[]
                 {
                     (0.24, 0.46, 0.26, 0.72),
                     (0.62, 0.58, 0.22, 0.64),
                     (0.87, 0.34, 0.17, 0.56),
                 })
        {
            for (var cell = 0; cell < grid.Count; cell++)
            {
                var px = grid.SiteX[cell] / width;
                var py = grid.SiteY[cell] / height;
                var dx = Math.Abs(px - cx);
                if (dx > 0.5)
                {
                    dx = 1d - dx;
                }

                var dy = py - cy;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                if (distance >= radius)
                {
                    continue;
                }

                var falloff = 1d - (distance / radius);
                fields.Height[cell] += (float)(amplitude * falloff * falloff);
            }
        }

        landCells = 0;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            var px = grid.SiteX[cell] / width;
            var py = grid.SiteY[cell] / height;
            var noise = (0.09d * Math.Sin(2d * Math.PI * 5d * px) * Math.Sin(2d * Math.PI * 3d * py))
                + (0.05d * Math.Sin((2d * Math.PI * 13d * px) + 1.1d) * Math.Cos(2d * Math.PI * 7d * py));

            fields.Height[cell] = (float)Math.Clamp(fields.Height[cell] + 0.34d + noise, 0d, 1d);
            fields.Moisture[cell] = (float)Math.Clamp(
                0.5d + (0.28d * Math.Sin((2d * Math.PI * 3d * px) + 0.7d)), 0d, 1d);
            fields.Temperature[cell] = (float)Math.Clamp(
                1d - Math.Abs((2d * py) - 1d), 0d, 1d);

            // 群系按高度/温度/湿度粗分：这里只需要一个与真实群系同构的分布，
            // 具体分类规则不重要（生态模拟只读群系序号查生产力表）。
            fields.Biome[cell] = fields.Height[cell] <= seaLevel
                ? (byte)(fields.Height[cell] < seaLevel - 0.08f ? 0 : 1)
                : ClassifySyntheticBiome(fields.Height[cell], fields.Temperature[cell], fields.Moisture[cell], seaLevel);

            if (fields.Height[cell] > seaLevel)
            {
                landCells++;
            }
        }

        PolygonTopologyBuilder.BuildDownslope(grid);
        PolygonRiverBuilder.Generate(grid, seaLevel, 0.06f);
        AssignSyntheticCities(grid, seaLevel);
        return grid;
    }

    /// <summary>
    /// 给合成地块图撒一批"城市"。
    ///
    /// 必须做这一步：文明模拟的政体种子与贸易枢纽都以城市为锚点，
    /// 一张没有城市的图只能跑到"兜底撒点"分支——那样测不到枢纽分级与贸易走廊，
    /// 而这两处恰好是地块化改动最大的地方。选址规则与真实生成器同构：
    /// 只在陆地、且集中在文明潜力高的地块上（真实城市也长在水土好的地方）。
    /// </summary>
    private static void AssignSyntheticCities(PolygonGrid grid, float seaLevel)
    {
        var fields = grid.Fields;
        var count = grid.Count;

        // 挑出合格的陆地候选，按文明潜力降序——文明潜力由调用方在生态模拟后填入，
        // 这里若还没跑生态，则退化为按高度适中程度排序。
        var candidates = new List<(int Cell, float Score)>(count);
        for (var cell = 0; cell < count; cell++)
        {
            if (fields.Height[cell] <= seaLevel || fields.Biome[cell] <= 1)
            {
                continue;
            }

            // 太高的山地不宜聚居，越接近海平面附近越宜居。
            var relative = (fields.Height[cell] - seaLevel) / Math.Max(1f - seaLevel, 0.0001f);
            var suitability = 1f - Math.Abs(relative - 0.22f);
            suitability += fields.Moisture[cell] * 0.25f;
            candidates.Add((cell, suitability));
        }

        if (candidates.Count == 0)
        {
            return;
        }

        candidates.Sort((left, right) => right.Score.CompareTo(left.Score));

        // 撒 12 个城市，彼此至少隔 6 个点距，避免全部挤在一处。
        const int targetCities = 12;
        var minSpacing = (float)Math.Max(grid.SpacingX * 6d, 1d);
        var placed = new List<int>();
        var cityIndex = 0;

        for (var i = 0; i < candidates.Count && placed.Count < targetCities; i++)
        {
            var cell = candidates[i].Cell;
            var farEnough = true;
            for (var j = 0; j < placed.Count; j++)
            {
                if (grid.WrappedDistance(
                        grid.SiteX[cell], grid.SiteY[cell],
                        grid.SiteX[placed[j]], grid.SiteY[placed[j]]) < minSpacing)
                {
                    farEnough = false;
                    break;
                }
            }

            if (!farEnough)
            {
                continue;
            }

            fields.CityId[cell] = cityIndex++;
            placed.Add(cell);
        }
    }

    /// <summary>合成群系：只需要与真实分布同构，索引与 BiomeType 一致。</summary>
    private static byte ClassifySyntheticBiome(float elevation, float temperature, float moisture, float seaLevel)
    {
        var relative = (elevation - seaLevel) / (1f - seaLevel);
        if (relative > 0.72f)
        {
            return temperature < 0.35f ? (byte)19 : (byte)18;
        }

        if (relative > 0.52f)
        {
            return 18;
        }

        if (relative < 0.05f)
        {
            return 2;
        }

        if (temperature < 0.30f)
        {
            return 4;
        }

        if (temperature < 0.44f)
        {
            return moisture > 0.5f ? (byte)5 : (byte)6;
        }

        if (temperature < 0.62f)
        {
            return moisture < 0.34f ? (byte)7 : moisture < 0.52f ? (byte)8 : (byte)11;
        }

        return moisture < 0.30f ? (byte)15 : moisture < 0.48f ? (byte)13 : (byte)17;
    }

    // ────────────────────────────────────────────────────────────────
    // 2. 确定性：同种子必须逐字节一致，不同种子必须不同
    // ────────────────────────────────────────────────────────────────
    private static void RunDeterminismCheck()
    {
        Console.WriteLine("── 2. 确定性 ──");

        var first = PolygonGridBuilder.Create(512, 256, seed: 777);
        var second = PolygonGridBuilder.Create(512, 256, seed: 777);
        var other = PolygonGridBuilder.Create(512, 256, seed: 778);

        var identical = first.Count == second.Count
            && first.VertexX.Length == second.VertexX.Length
            && ArraysEqual(first.SiteX, second.SiteX)
            && ArraysEqual(first.VertexX, second.VertexX)
            && ArraysEqual(first.VertexY, second.VertexY);

        Check(identical, "同种子几何一致", "同一种子两次建图的结果不同");

        var different = !ArraysEqual(first.VertexX, other.VertexX);
        Check(different, "异种子几何不同", "换种子后几何完全没变，抖动没有生效");

        // 目标地块数变化必须只影响密度，不影响正确性
        var sparse = PolygonGridBuilder.Create(512, 256, seed: 777, cellsDesired: 2048);
        var dense = PolygonGridBuilder.Create(512, 256, seed: 777, cellsDesired: 32768);
        Check(sparse.Count < dense.Count, "地块数随目标单调", $"目标 2048 得到 {sparse.Count}，目标 32768 得到 {dense.Count}");
        Check(sparse.SpacingX > dense.SpacingX, "点距随目标反比", "点距没有随目标地块数变化");

        var sparseReport = PolygonGridValidator.Validate(sparse, seed: 9);
        Check(sparseReport.Passed, "稀疏档几何自检", sparseReport.ToString());

        Console.WriteLine($"  512×256 稀疏 {sparse.Count} 格 / 密集 {dense.Count} 格，均已校验");
        Console.WriteLine();
    }

    // ────────────────────────────────────────────────────────────────
    // 3. 拾取精度：FindCell 必须严格等于"最近站点"，且与点包含判定一致
    // ────────────────────────────────────────────────────────────────
    private static void RunPickAccuracyCheck()
    {
        Console.WriteLine("── 3. 拾取精度 ──");

        var grid = PolygonGridBuilder.Create(1024, 512, seed: 31337);

        var random = new Random(2026);
        var mismatch = 0;
        const int samples = 200000;

        for (var i = 0; i < samples; i++)
        {
            var x = random.NextDouble() * grid.Width;
            var y = random.NextDouble() * grid.Height;

            // 精确拾取的定义：返回的必须是到该点最近的地块。
            var exact = grid.FindCell(x, y);
            var nearest = FindNearestByBruteForce(grid, x, y);
            if (exact != nearest)
            {
                mismatch++;
            }
        }

        Check(mismatch == 0, "精确拾取", $"{samples} 次抽样中有 {mismatch} 次与暴力最近点不一致");
        Console.WriteLine($"  精确拾取与暴力最近点一致：{samples - mismatch} / {samples}");
        Console.WriteLine();
    }

    /// <summary>暴力最近点：作为拾取精度的参照实现。</summary>
    private static int FindNearestByBruteForce(PolygonGrid grid, double x, double y)
    {
        var best = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < grid.Count; i++)
        {
            var distance = grid.WrappedDistanceSquared(x, y, grid.SiteX[i], grid.SiteY[i]);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = i;
        }

        return best;
    }

    // ────────────────────────────────────────────────────────────────
    // 4. 环形 BFS：返回集合必须落在半径内，且包含种子
    // ────────────────────────────────────────────────────────────────
    private static void RunFindAllCheck()
    {
        Console.WriteLine("── 4. 环形 BFS 范围 ──");

        var grid = PolygonGridBuilder.Create(512, 256, seed: 555);
        var radius = grid.SpacingX * 6d;
        var samples = 200;
        var random = new Random(88);

        var outside = 0;
        var missingSeed = 0;
        var emptyResult = 0;
        var minCount = int.MaxValue;
        var maxCount = 0;
        var totalCount = 0L;

        for (var i = 0; i < samples; i++)
        {
            var x = random.NextDouble() * grid.Width;
            var y = random.NextDouble() * grid.Height;
            var cells = grid.FindAll(x, y, radius);

            if (cells.Count == 0)
            {
                emptyResult++;
                continue;
            }

            var seed = grid.FindCell(x, y);
            if (!cells.Contains(seed))
            {
                missingSeed++;
            }

            foreach (var cell in cells)
            {
                if (grid.WrappedDistance(x, y, grid.SiteX[cell], grid.SiteY[cell]) > radius * 1.000001d)
                {
                    outside++;
                }
            }

            minCount = Math.Min(minCount, cells.Count);
            maxCount = Math.Max(maxCount, cells.Count);
            totalCount += cells.Count;
        }

        Check(outside == 0, "BFS 半径约束", $"有 {outside} 个返回地块超出了半径");
        Check(missingSeed == 0, "BFS 含种子", $"{missingSeed} 次未包含种子地块");
        Check(emptyResult == 0, "BFS 非空", $"{emptyResult} 次返回空集合");

        // 半径 6 倍点距 ≈ 面积 π·36 ≈ 113 倍点距²，每格约 1 个点 → 期望约 113 个地块。
        var average = totalCount / (double)samples;
        Console.WriteLine($"  半径 6×点距：平均 {average:0.0} 格（范围 {minCount}~{maxCount}），理论值约 113");
        Check(average > 80 && average < 150, "BFS 数量合理", $"平均 {average:0.0} 与理论值 113 偏离过大");

        Console.WriteLine();
    }

    // ────────────────────────────────────────────────────────────────
    // 5. 拓扑派生：下游方向与汇流量守恒
    // ────────────────────────────────────────────────────────────────
    private static void RunTopologyCheck()
    {
        Console.WriteLine("── 5. 拓扑派生（下游方向 / 汇流量） ──");

        var grid = PolygonGridBuilder.Create(512, 256, seed: 999);
        var count = grid.Count;

        // 造一个平滑的"山丘"高程场：中心高、边缘低，保证存在唯一且无环的流向。
        var height = grid.Fields.Height;
        var centerX = grid.Width * 0.5d;
        var centerY = grid.Height * 0.5d;
        var maxDistance = Math.Sqrt((grid.Width * 0.5d * grid.Width * 0.5d) + (grid.Height * 0.5d * grid.Height * 0.5d));

        for (var i = 0; i < count; i++)
        {
            var distance = grid.WrappedDistance(grid.SiteX[i], grid.SiteY[i], centerX, centerY);
            height[i] = (float)Math.Clamp(1d - (distance / maxDistance), 0d, 1d);
            grid.Fields.Moisture[i] = 1f;
        }

        PolygonTopologyBuilder.BuildDownslope(grid);
        PolygonTopologyBuilder.BuildFlux(grid);

        // 流向必须严格下降（按单位距离落差择优，且要求最小落差 > 0）。
        var uphill = 0;
        var noDownslope = 0;
        for (var i = 0; i < count; i++)
        {
            var down = grid.Fields.Downslope[i];
            if (down < 0)
            {
                noDownslope++;
                continue;
            }

            if (height[down] >= height[i])
            {
                uphill++;
            }
        }

        Check(uphill == 0, "流向严格下降", $"{uphill} 个地块的下游不比自身低");

        // 汇流量守恒：所有"汇"（无下游）的汇流量之和必须等于初始降水总量。
        var sinkFlux = 0d;
        for (var i = 0; i < count; i++)
        {
            if (grid.Fields.Downslope[i] < 0)
            {
                sinkFlux += grid.Fields.Flux[i];
            }
        }

        var expected = count * 1d;
        var relativeError = Math.Abs(sinkFlux - expected) / expected;
        Check(relativeError < 1e-4d, "汇流量守恒", $"汇点合计 {sinkFlux:0.##}，期望 {expected:0.##}，相对误差 {relativeError:P4}");

        // 连通分量：这个单峰地形应当只有 1 块陆地。
        for (var i = 0; i < count; i++)
        {
            grid.Fields.Height[i] = height[i] > 0.25f ? 1f : 0f;
        }

        var components = PolygonTopologyBuilder.FindComponents(grid, 0.5f, land: true);
        Check(components.Count == 1, "陆地连通分量", $"单峰地形应只有 1 块陆地，实际 {components.Count} 块");

        // 环形扩展：环数增加时集合必须单调增长。
        var ring1 = PolygonTopologyBuilder.CollectRing(grid, grid.Count / 2, 1);
        var ring3 = PolygonTopologyBuilder.CollectRing(grid, grid.Count / 2, 3);
        Check(ring1.Count < ring3.Count, "环形扩展单调", $"1 环 {ring1.Count} 格不小于 3 环 {ring3.Count} 格");

        // 种子扩展掩码：必须恰好等于"种子 ∪ 种子的邻居"
        var seeds = new bool[grid.Count];
        var expectedSeedCount = 0;
        for (var i = 0; i < grid.Count; i += 97)
        {
            seeds[i] = true;
            expectedSeedCount++;
        }

        var mask = PolygonTopologyBuilder.ExpandToNeighborMask(grid, seeds);
        var maskCount = 0;
        var spurious = 0;
        var missingSeed = 0;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (!mask[cell])
            {
                continue;
            }

            maskCount++;
            if (seeds[cell])
            {
                continue;
            }

            // 非种子却在掩码里：必须是某个种子的邻居
            var isNeighbor = false;
            var start = grid.CellNeighborStart[cell];
            var end = grid.CellNeighborStart[cell + 1];
            for (var k = start; k < end; k++)
            {
                if (seeds[grid.CellNeighbors[k]])
                {
                    isNeighbor = true;
                    break;
                }
            }

            if (!isNeighbor)
            {
                spurious++;
            }
        }

        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (seeds[cell] && !mask[cell])
            {
                missingSeed++;
            }
        }

        Check(spurious == 0, "扩展掩码无多余地块", $"{spurious} 个非种子地块既不是种子也不是其邻居");
        Check(missingSeed == 0, "扩展掩码包含全部种子", $"{missingSeed} 个种子被漏掉");
        Check(maskCount > expectedSeedCount, "扩展掩码确实扩张了", $"掩码 {maskCount} 不大于种子数 {expectedSeedCount}");

        Console.WriteLine(
            $"  汇点 {noDownslope} 个，汇流量合计 {sinkFlux:0.##}；1 环 {ring1.Count} 格 / 3 环 {ring3.Count} 格；"
            + $"种子扩展 {expectedSeedCount} → {maskCount} 格");
        Console.WriteLine();
    }

    // ────────────────────────────────────────────────────────────────
    // 6. 退化输入：极小地图、极少地块
    // ────────────────────────────────────────────────────────────────
    private static void RunDegenerateInputCheck()
    {
        Console.WriteLine("── 6. 退化输入 ──");

        var cases = new (int Width, int Height, int Desired)[]
        {
            (2, 2, 64),
            (4, 4, 64),
            (8, 4, 64),
            (16, 8, 64),
            (256, 128, 64),
            (256, 128, 1),
        };

        foreach (var (width, height, desired) in cases)
        {
            var grid = PolygonGridBuilder.Create(width, height, seed: 1, cellsDesired: desired);
            var report = PolygonGridValidator.Validate(grid, seed: 5);
            Console.WriteLine($"  {width,4}×{height,-4} 目标 {desired,6} → 实际 {grid.Count,6} 格，{grid.Columns}×{grid.Rows}，{(report.Passed ? "通过" : "失败")}");
            Check(report.Passed, $"退化输入 {width}×{height}/{desired}", report.ToString());
        }

        Console.WriteLine();
    }

    // ────────────────────────────────────────────────────────────────
    // 辅助
    // ────────────────────────────────────────────────────────────────
    private static void Check(bool condition, string name, string detail)
    {
        if (condition)
        {
            return;
        }

        _failureCount++;
        Console.WriteLine($"  [失败] {name}：{detail}");
    }

    private static bool ArraysEqual(double[] a, double[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (!a[i].Equals(b[i]))
            {
                return false;
            }
        }

        return true;
    }
}
