using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlanetGeneration.WorldGen.Polygon;

namespace PolygonSelfTest;

/// <summary>
/// 离线预览：造一个合成的世界，把"栅格版生物群系"与"多边形版生物群系"都渲染出来，
/// 落成裸 RGBA 文件（用 ffmpeg 转 PNG 即可查看）。
///
/// 目的：不启动 Godot 也能肉眼确认地块划分是否正确、地块属性与栅格是否一致。
/// 合成世界的群系判定是简化版，配色表镜像自 WorldRenderer.BiomeColors（仅用于预览）。
/// </summary>
internal static class PreviewRenderer
{
    private const int Width = 1024;
    private const int Height = 512;
    private const int CellsDesired = 8192;
    private const double SeaLevel = 0.5d;

    /// <summary>镜像自 <c>WorldRenderer.BiomeColors</c>，顺序与 BiomeType 一致（仅预览用）。</summary>
    private static readonly byte[][] BiomePalette =
    {
        new byte[] { 0x2f, 0x5f, 0x88 }, // Ocean
        new byte[] { 0x4f, 0x7e, 0xa8 }, // ShallowOcean
        new byte[] { 0xdf, 0xe4, 0xc9 }, // Coastland
        new byte[] { 0xc2, 0xd3, 0xda }, // Ice
        new byte[] { 0xa1, 0x81, 0x4a }, // Tundra
        new byte[] { 0x4f, 0x6e, 0x34 }, // BorealForest
        new byte[] { 0x5f, 0x86, 0x40 }, // Taiga
        new byte[] { 0xc7, 0xc5, 0xac }, // Steppe
        new byte[] { 0xb8, 0xc9, 0x8a }, // Grassland
        new byte[] { 0xa8, 0xa0, 0x7f }, // Chaparral
        new byte[] { 0xd7, 0xc6, 0x91 }, // TemperateDesert
        new byte[] { 0x2f, 0xb9, 0x5a }, // TemperateSeasonalForest
        new byte[] { 0x46, 0xa8, 0x57 }, // TemperateRainForest
        new byte[] { 0xcf, 0xd1, 0x8a }, // Savanna
        new byte[] { 0x7c, 0x8f, 0x53 }, // Shrubland
        new byte[] { 0xe9, 0xd7, 0x9b }, // TropicalDesert
        new byte[] { 0xae, 0xd4, 0x5a }, // TropicalSeasonalForest
        new byte[] { 0x7a, 0xcb, 0x33 }, // TropicalRainForest
        new byte[] { 0x8f, 0x80, 0x67 }, // RockyMountain
        new byte[] { 0xe7, 0xed, 0xf0 }, // SnowyMountain
        new byte[] { 0x4f, 0x7e, 0xa8 }  // River
    };

    public static void Run(string outputDirectory, int cellsDesired = CellsDesired)
    {
        Directory.CreateDirectory(outputDirectory);

        var world = BuildSyntheticWorld();

        var grid = PolygonGridBuilder.Create(Width, Height, 20260917, out var stats, cellsDesired);
        var map = PolygonRasterizer.BuildCellMap(grid, Width, Height);

        // 地块属性：离散量用质心采样，河流用平均/最大值混合（与 Main.Polygon.cs 的规则一致）。
        var cellBiome = new byte[grid.Count];
        RasterPolygonBridge.SampleDiscreteAtCentroid(grid, map, world.Biome, cellBiome);
        var cellRiver = new float[grid.Count];
        RasterPolygonBridge.SampleRiver(map, world.River, cellRiver);

        Console.WriteLine($"合成世界 {Width}×{Height}，地块 {stats.CellCount}（点距 {stats.SpacingX:0.00}）");
        Console.WriteLine($"  归属图漏像素 {map.UnassignedPixels}，平均邻接 {stats.AverageNeighbors:0.00}");

        // ① 栅格版：逐像素群系（模拟现有的"生物群系"图层）
        var rasterBuffer = new byte[Width * Height * 4];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var index = ((y * Width) + x) * 4;
                var biome = world.River[x, y] > 0.3f ? 20 : world.Biome[x, y];
                WriteColor(rasterBuffer, index, BiomePalette[biome]);
            }
        }

        // ② 多边形版：逐地块填充 + 边界描边
        var cellRgba = new byte[grid.Count * 4];
        for (var cell = 0; cell < grid.Count; cell++)
        {
            var biome = cellRiver[cell] > 0.3f ? 20 : cellBiome[cell];
            var color = BiomePalette[biome];
            var index = cell * 4;
            cellRgba[index] = color[0];
            cellRgba[index + 1] = color[1];
            cellRgba[index + 2] = color[2];
            cellRgba[index + 3] = 255;
        }

        var polygonBuffer = new byte[Width * Height * 4];
        PolygonRasterizer.FillRgba(polygonBuffer, Width, Height, map, cellRgba);
        PolygonRasterizer.StrokeBorders(polygonBuffer, Width, Height, map, 26, 28, 32, 255);

        // ③ 线框版：灰底 + 地块边界，用来看清地块形状本身
        var wireBuffer = new byte[Width * Height * 4];
        for (var i = 0; i < wireBuffer.Length; i += 4)
        {
            wireBuffer[i] = 232;
            wireBuffer[i + 1] = 232;
            wireBuffer[i + 2] = 228;
            wireBuffer[i + 3] = 255;
        }

        PolygonRasterizer.StrokeBorders(wireBuffer, Width, Height, map, 40, 44, 52, 255);

        // ④ 差异统计：地块的群系与它覆盖像素的群系有多不一致
        long mismatchedPixels = 0;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var cell = map.CellAt(x, y);
                var polygonBiome = cellRiver[cell] > 0.3f ? (byte)20 : cellBiome[cell];
                var rasterBiome = world.River[x, y] > 0.3f ? (byte)20 : world.Biome[x, y];
                if (polygonBiome != rasterBiome)
                {
                    mismatchedPixels++;
                }
            }
        }

        var totalPixels = (long)Width * Height;
        Console.WriteLine($"  栅格群系 vs 地块群系：不一致像素 {mismatchedPixels} / {totalPixels}（{mismatchedPixels / (double)totalPixels:P2}）");
        Console.WriteLine("  说明：地块只有一种群系，边界处必然与像素级分类有差异；比例过高则说明采样有误。");

        // ⑤ 高亮预览：抽一批地块（显式包含贴缝与贴极圈的）画上去，
        //    用来肉眼确认高亮范围正确、且跨经度缝的地块会被正确裁剪。
        var highlightBuffer = (byte[])polygonBuffer.Clone();
        var highlightCells = new List<int>();
        var stride = Math.Max(1, grid.Count / 60);
        for (var cell = 0; cell < grid.Count; cell += stride)
        {
            highlightCells.Add(cell);
        }

        for (var row = 0; row < grid.Rows; row += 5)
        {
            highlightCells.Add(row * grid.Columns);
            highlightCells.Add((row * grid.Columns) + grid.Columns - 1);
        }

        for (var column = 0; column < grid.Columns; column += 7)
        {
            highlightCells.Add(column);
            highlightCells.Add(((grid.Rows - 1) * grid.Columns) + column);
        }

        var seamHighlighted = 0;
        foreach (var cell in highlightCells.Distinct())
        {
            if (cell < 0 || cell >= grid.Count)
            {
                continue;
            }

            if (grid.CellSeam[cell])
            {
                seamHighlighted++;
            }

            foreach (var ring in grid.GetHighlightRings(cell))
            {
                PolygonRasterizer.FillRing(highlightBuffer, Width, Height, ring, 255, 240, 120, 200);
            }
        }

        Console.WriteLine($"  高亮预览：绘制 {highlightCells.Distinct().Count()} 个地块（其中 {seamHighlighted} 个跨经度缝）");

        // ⑥ 单元格地图预览：与游戏里 Cells 模式一致——地块图上重新生成河网 + 地块邻接地貌分类，
        //    再按"地貌色 + 河道色"逐地块填充。这张图就是"游戏地图 = 多边形单元格"的样子。
        var cellMapBuffer = BuildCellMapPreview(grid, map, world);

        // ⑦ 文明预览：政体着色 + 贸易走廊 + 边界描边。
        //    这张图是 P4-b 的"看图"环节——文明模拟的失败模式（政体糊成一团、
        //    走廊断成几截、边界全挤在海岸线）都是断言看不出、只有看图才发现的。
        var civilizationBuffer = BuildCivilizationPreview(grid, map, world);

        var suffix = cellsDesired.ToString();
        WriteRaw(Path.Combine(outputDirectory, $"preview_raster_biomes_{suffix}.raw"), rasterBuffer);
        WriteRaw(Path.Combine(outputDirectory, $"preview_polygon_biomes_{suffix}.raw"), polygonBuffer);
        WriteRaw(Path.Combine(outputDirectory, $"preview_polygon_wire_{suffix}.raw"), wireBuffer);
        WriteRaw(Path.Combine(outputDirectory, $"preview_highlight_{suffix}.raw"), highlightBuffer);
        WriteRaw(Path.Combine(outputDirectory, $"preview_cellmap_{suffix}.raw"), cellMapBuffer);
        WriteRaw(Path.Combine(outputDirectory, $"preview_civilization_{suffix}.raw"), civilizationBuffer);

        Console.WriteLine($"  已写出 6 个裸 RGBA 文件到 {outputDirectory}（尺寸 {Width}×{Height}，后缀 _{suffix}）");
    }

    /// <summary>
    /// 文明预览：政体色 + 贸易走廊 + 边界高亮。
    ///
    /// 与游戏里 <c>Cells</c> 模式的着色规则对齐（政体色由编号哈希得到、
    /// 影响力决定色浓、边界提亮），但**不复用** WorldRenderer——那是 Godot 侧代码，
    /// 核心层不能引用。配色规则在两边各自实现，因此这里只用于观察形态是否正确，
    /// 不作为配色一致性的证据。
    /// </summary>
    private static byte[] BuildCivilizationPreview(PolygonGrid grid, PolygonCellMap map, SyntheticWorld world)
    {
        var fields = grid.Fields;

        // ⚠ 这里踩过一次坑：文明预览最初直接调用生态模拟，结果"政体 0 个、平均生态 0.0%"。
        // 根因不是模拟器有 bug，而是**预览网格的字段没填全**——
        // 前面的 BuildCellMapPreview 只采样了 Height / Moisture，群系进了局部数组 cellBiome
        // 而没有写进 fields.Biome，Temperature 与 River 更是从未采样。
        // 生态模拟看到温度 0、群系 0（Ocean），于是把每个地块都判成海洋并归零。
        //
        // 教训：模拟器自检与预览必须走**同一套字段准备流程**。自检里 BuildSyntheticGrid
        // 自己填全了字段，所以是绿的；预览少填几个字段就静默变成全零，
        // 而且不报错、不越界——**只会在图上一眼看出"地图是空的"**。
        // 现在统一走 FillFields，避免再次分叉。
        FillFields(grid, map, world);

        // 文明模拟吃生态的产出，先把生态跑出来（与游戏里 EnsurePolygonCivilization 的顺序一致）。
        var ecology = PolygonEcologySimulator.Simulate(grid, 20260917, 450, 68, 42, 75, (float)SeaLevel);
        var civilization = PolygonCivilizationSimulator.Simulate(grid, 20260917, 450, 42, 68, (float)SeaLevel);

        var cellRgba = new byte[grid.Count * 4];

        for (var cell = 0; cell < grid.Count; cell++)
        {
            byte[] color;
            if (fields.Height[cell] <= SeaLevel)
            {
                // 海洋：深→浅的蓝。
                var depth = Math.Clamp((SeaLevel - fields.Height[cell]) / SeaLevel, 0d, 1d);
                color = new[]
                {
                    (byte)8,
                    (byte)(28 + ((1 - depth) * 40)),
                    (byte)(64 + ((1 - depth) * 60)),
                };
            }
            else if (fields.PolityId[cell] >= 0)
            {
                // 政体：编号哈希取色，影响力决定色浓（与 ColorForPolity 同构）。
                var baseColor = PolityColor(fields.PolityId[cell]);
                var strength = Math.Clamp(fields.Influence[cell], 0f, 1f);
                color = new[]
                {
                    (byte)(baseColor[0] * (0.35f + (0.65f * strength))),
                    (byte)(baseColor[1] * (0.35f + (0.65f * strength))),
                    (byte)(baseColor[2] * (0.35f + (0.65f * strength))),
                };
            }
            else
            {
                // 无归属的陆地：中性灰。
                var neutral = (byte)(74 + (Math.Clamp(fields.Influence[cell], 0f, 1f) * 40));
                color = new[] { neutral, (byte)(neutral + 6), (byte)(neutral + 12) };
            }

            // 贸易走廊：叠一层暖黄，强度按流量。
            if (fields.TradeRouteMask[cell])
            {
                var flow = Math.Clamp(fields.TradeFlow[cell], 0f, 1f);
                color = new[]
                {
                    (byte)Math.Min(255, color[0] + (60 + (flow * 120))),
                    (byte)Math.Min(255, color[1] + (40 + (flow * 90))),
                    (byte)Math.Min(255, color[2] + (10 + (flow * 30))),
                };
            }

            // 政体边界：提亮。
            if (fields.BorderMask[cell])
            {
                color = new[]
                {
                    (byte)Math.Min(255, color[0] + 60),
                    (byte)Math.Min(255, color[1] + 60),
                    (byte)Math.Min(255, color[2] + 60),
                };
            }

            var index = cell * 4;
            cellRgba[index] = color[0];
            cellRgba[index + 1] = color[1];
            cellRgba[index + 2] = color[2];
            cellRgba[index + 3] = 255;
        }

        var buffer = new byte[Width * Height * 4];
        PolygonRasterizer.FillRgba(buffer, Width, Height, map, cellRgba);

        Console.WriteLine(
            $"  文明预览：政体 {civilization.PolityCount} 个，控制率 {civilization.ControlledLandPercent:0.0}%，"
            + $"走廊 {civilization.TradeRouteCells} 块，边界 {CountMask(fields.BorderMask)} 块");
        Console.WriteLine(
            $"  生态输入：平均生态 {ecology.AvgEcologyHealth:P1}，平均文明潜力 {ecology.AvgCivilizationPotential:P1}");

        return buffer;
    }

    /// <summary>政体编号 → 颜色（与 WorldRenderer.ColorForPolity 同一套哈希，仅预览用）。</summary>
    private static byte[] PolityColor(int polityId)
    {
        var hash = unchecked((uint)(polityId * 2654435761));
        return new[]
        {
            (byte)((0.28f + (((hash & 0xFFu) / 255f) * 0.62f)) * 255f),
            (byte)((0.28f + ((((hash >> 8) & 0xFFu) / 255f) * 0.62f)) * 255f),
            (byte)((0.28f + ((((hash >> 16) & 0xFFu) / 255f) * 0.62f)) * 255f),
        };
    }

    /// <summary>
    /// 把合成世界的栅格场填进地块网格的字段（连续量面积加权、离散量质心、河流在地块图上重生成）。
    /// 抽成公开方法是为了让诊断工具与预览走**完全相同**的字段准备流程——
    /// 少填一个字段的后果是静默全零，而不是报错（曾经踩过）。
    /// </summary>
    internal static void FillFields(PolygonGrid grid, PolygonCellMap map, SyntheticWorld world)
    {
        var fields = grid.Fields;

        RasterPolygonBridge.SampleContinuous(map, world.Elevation, fields.Height);
        RasterPolygonBridge.SampleContinuous(map, world.Temperature, fields.Temperature);
        RasterPolygonBridge.SampleContinuous(map, world.Moisture, fields.Moisture);
        RasterPolygonBridge.SampleDiscreteAtCentroid(grid, map, world.Biome, fields.Biome);

        PolygonTopologyBuilder.BuildDownslope(grid);
        PolygonRiverBuilder.Generate(grid, (float)SeaLevel, 0.06f);

        AssignPreviewCities(grid);
    }

    /// <summary>造一份合成世界并填进地块字段；供诊断工具一次性调用。</summary>
    public static PolygonGrid BuildDiagnosticGrid(int width, int height, int seed, int cellsDesired)
    {
        var world = BuildSyntheticWorld();
        var grid = PolygonGridBuilder.Create(width, height, seed, out _, cellsDesired);
        var map = PolygonRasterizer.BuildCellMap(grid, width, height);
        FillFields(grid, map, world);
        return grid;
    }

    /// <summary>
    /// 数一数掩码里有多少个 true。
    /// </summary>
    private static int CountMask(bool[] mask)
    {
        var total = 0;
        for (var i = 0; i < mask.Length; i++)
        {
            if (mask[i])
            {
                total++;
            }
        }

        return total;
    }

    /// <summary>
    /// 给预览网格撒城市。没有城市时文明模拟只能走"兜底撒点"分支，
    /// 预览图上看不到枢纽与贸易走廊——而这两处正是地块化改动最大的地方。
    /// </summary>
    private static void AssignPreviewCities(PolygonGrid grid)
    {
        var fields = grid.Fields;
        var candidates = new List<(int Cell, float Score)>();

        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (fields.Height[cell] <= SeaLevel || fields.Biome[cell] <= 1)
            {
                continue;
            }

            // 宜居度：不太高、不太干。（SeaLevel 是 double，这里显式收窄成 float）
            var relative = (float)((fields.Height[cell] - SeaLevel) / (1d - SeaLevel));
            var suitability = 1f - Math.Abs(relative - 0.22f);
            suitability += fields.Moisture[cell] * 0.25f;
            candidates.Add((cell, suitability));
        }

        if (candidates.Count == 0)
        {
            return;
        }

        candidates.Sort((left, right) => right.Score.CompareTo(left.Score));

        var minSpacing = (float)Math.Max(grid.SpacingX * 6d, 1d);
        var placed = new List<int>();
        var cityIndex = 0;

        for (var i = 0; i < candidates.Count && placed.Count < 14; i++)
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

    /// <summary>镜像自 <c>Main.GetLandformColor</c>，顺序与 PolygonLandform 一致（仅预览用）。</summary>
    private static readonly byte[][] LandformPalette =
    {
        new byte[] { 10, 31, 77 },    // DeepOcean
        new byte[] { 47, 95, 136 },   // ShallowSea
        new byte[] { 201, 216, 174 }, // CoastalPlain
        new byte[] { 152, 196, 122 }, // Plain
        new byte[] { 134, 180, 114 }, // Basin
        new byte[] { 189, 170, 114 }, // DryBasin
        new byte[] { 116, 166, 104 }, // Valley
        new byte[] { 176, 190, 119 }, // RollingHills
        new byte[] { 183, 159, 115 }, // Upland
        new byte[] { 158, 141, 99 },  // Plateau
        new byte[] { 126, 107, 87 },  // Mountain
    };

    /// <summary>
    /// 单元格地图：与游戏 Cells 模式同一套流程——先在地块图上生成河网与地貌，再逐地块取色填充。
    /// </summary>
    private static byte[] BuildCellMapPreview(PolygonGrid grid, PolygonCellMap map, SyntheticWorld world)
    {
        var fields = grid.Fields;

        // 连续场采样到地块（面积加权），这是"地块成为属性真源"的第一步。
        RasterPolygonBridge.SampleContinuous(map, world.Elevation, fields.Height);
        RasterPolygonBridge.SampleContinuous(map, world.Moisture, fields.Moisture);

        // 河流与地貌都在地块图上算，不再读栅格。
        PolygonTopologyBuilder.BuildDownslope(grid);
        PolygonRiverBuilder.Generate(grid, (float)SeaLevel, 0.06f);
        PolygonLandformClassifier.ClassifyAll(grid, (float)SeaLevel, 1.0f, fields.Landform);

        var riverCells = 0;
        var landCells = 0;
        var landformCounts = new int[11];
        var cellRgba = new byte[grid.Count * 4];

        for (var cell = 0; cell < grid.Count; cell++)
        {
            var landform = fields.Landform[cell];
            landformCounts[landform]++;
            var isLand = fields.Height[cell] > SeaLevel;
            if (isLand)
            {
                landCells++;
            }

            byte[] color;
            if (isLand && fields.River[cell] > 0f)
            {
                // 河道：按强度在浅蓝到深蓝之间插值。
                riverCells++;
                var t = Math.Clamp(fields.River[cell], 0f, 1f);
                color = new[]
                {
                    (byte)(14 + (0 * t)),
                    (byte)(63 - (63 * t)),
                    (byte)(149 + (106 * t)),
                };
            }
            else
            {
                color = LandformPalette[landform];
            }

            var index = cell * 4;
            cellRgba[index] = color[0];
            cellRgba[index + 1] = color[1];
            cellRgba[index + 2] = color[2];
            cellRgba[index + 3] = 255;
        }

        var buffer = new byte[Width * Height * 4];
        PolygonRasterizer.FillRgba(buffer, Width, Height, map, cellRgba);
        PolygonRasterizer.StrokeBorders(buffer, Width, Height, map, 26, 28, 32, 255);

        Console.WriteLine(
            $"  单元格地图：{landCells} 个陆地块，{riverCells} 个河道地块（{riverCells / (double)Math.Max(landCells, 1):P2}）");
        Console.WriteLine(
            "    地貌分布：" + string.Join("，", LandformPalette
                .Select((_, i) => (Index: i, Count: landformCounts[i]))
                .Where(entry => entry.Count > 0)
                .Select(entry => $"{entry.Index}:{entry.Count}")));

        return buffer;
    }

    /// <summary>合成一个在经度方向周期的世界：大陆块 + 纬度气候带 + 一条蜿蜒河流。</summary>
    private static SyntheticWorld BuildSyntheticWorld()
    {
        var elevation = new float[Width, Height];
        var temperature = new float[Width, Height];
        var moisture = new float[Width, Height];
        var river = new float[Width, Height];
        var biome = new byte[Width, Height];

        // 三块大陆：横向用环绕距离，保证经度缝两侧连续。
        var continents = new (double X, double Y, double Radius, double Height)[]
        {
            (0.22, 0.44, 0.26, 0.74),
            (0.58, 0.62, 0.22, 0.66),
            (0.86, 0.36, 0.17, 0.58),
        };

        for (var y = 0; y < Height; y++)
        {
            var latitude = 1d - Math.Abs((2d * y / (Height - 1)) - 1d); // 赤道 1，两极 0
            for (var x = 0; x < Width; x++)
            {
                var px = (double)x / Width;
                var py = (double)y / Height;

                var value = 0.36d;
                foreach (var continent in continents)
                {
                    var dx = Math.Abs(px - continent.X);
                    if (dx > 0.5d)
                    {
                        dx = 1d - dx;
                    }

                    var dy = py - continent.Y;
                    var distance = Math.Sqrt((dx * dx) + (dy * dy));
                    if (distance < continent.Radius)
                    {
                        var falloff = 1d - (distance / continent.Radius);
                        value += continent.Height * falloff * falloff;
                    }
                }

                // 周期噪声：用两组正弦代替 Perlin，保证缝两侧连续。
                value += 0.10d * Math.Sin(2d * Math.PI * 5d * px) * Math.Sin(2d * Math.PI * 3d * py);
                value += 0.06d * Math.Sin(2d * Math.PI * 13d * px + 1.1d) * Math.Cos(2d * Math.PI * 7d * py);
                value += 0.03d * Math.Sin(2d * Math.PI * 29d * px + 2.4d) * Math.Cos(2d * Math.PI * 17d * py);

                elevation[x, y] = (float)Math.Clamp(value, 0d, 1d);
                temperature[x, y] = (float)Math.Clamp((latitude * 1.12d) - 0.06d, 0d, 1d);
                moisture[x, y] = (float)Math.Clamp(
                    0.5d + (0.28d * Math.Sin(2d * Math.PI * 3d * px + 0.7d)) + (0.18d * Math.Cos(2d * Math.PI * 4d * py)),
                    0d,
                    1d);
            }
        }

        // 河流：两条正弦河道，只落在陆地上。
        for (var x = 0; x < Width; x++)
        {
            var t = (double)x / Width;
            foreach (var (offset, amplitude, frequency) in new[] { (0.30d, 0.05d, 3d), (0.62d, 0.04d, 4d) })
            {
                var center = (int)((offset + (amplitude * Math.Sin(2d * Math.PI * frequency * t))) * Height);
                for (var dy = -1; dy <= 1; dy++)
                {
                    var y = center + dy;
                    if (y < 0 || y >= Height)
                    {
                        continue;
                    }

                    if (elevation[x, y] <= SeaLevel)
                    {
                        continue;
                    }

                    river[x, y] = dy == 0 ? 0.9f : 0.4f;
                }
            }
        }

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                biome[x, y] = ClassifyBiome(elevation[x, y], temperature[x, y], moisture[x, y]);
            }
        }

        return new SyntheticWorld(elevation, temperature, moisture, river, biome);
    }

    /// <summary>简化版群系判定；索引顺序与 BiomeType 一致。</summary>
    private static byte ClassifyBiome(float elevation, float temperature, float moisture)
    {
        if (elevation < SeaLevel - 0.08f)
        {
            return 0; // Ocean
        }

        if (elevation < SeaLevel)
        {
            return 1; // ShallowOcean
        }

        var height = (elevation - SeaLevel) / (1f - SeaLevel);
        if (height > 0.72f)
        {
            return temperature < 0.35f ? (byte)19 : (byte)18;
        }

        if (height > 0.52f)
        {
            return 18; // RockyMountain
        }

        if (height < 0.05f)
        {
            return 2; // Coastland
        }

        if (temperature < 0.16f)
        {
            return 3; // Ice
        }

        if (temperature < 0.30f)
        {
            return 4; // Tundra
        }

        if (temperature < 0.44f)
        {
            return moisture > 0.5f ? (byte)5 : (byte)6;
        }

        if (temperature < 0.62f)
        {
            if (moisture < 0.34f)
            {
                return 7; // Steppe
            }

            if (moisture < 0.52f)
            {
                return 8; // Grassland
            }

            return moisture > 0.72f ? (byte)12 : (byte)11;
        }

        if (moisture < 0.30f)
        {
            return 15; // TropicalDesert
        }

        if (moisture < 0.48f)
        {
            return 13; // Savanna
        }

        return moisture > 0.70f ? (byte)17 : (byte)16;
    }

    private static void WriteColor(byte[] buffer, int index, byte[] color)
    {
        buffer[index] = color[0];
        buffer[index + 1] = color[1];
        buffer[index + 2] = color[2];
        buffer[index + 3] = 255;
    }

    private static void WriteRaw(string path, byte[] buffer) => File.WriteAllBytes(path, buffer);

    // internal 而非 private：BuildDiagnosticGrid 与 FillFields 要暴露给同程序集的诊断工具，
    // 而它们的签名里带这个类型（CS0051 可访问性不一致）。
    internal readonly record struct SyntheticWorld(
        float[,] Elevation,
        float[,] Temperature,
        float[,] Moisture,
        float[,] River,
        byte[,] Biome);
}
