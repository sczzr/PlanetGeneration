using Godot;
using PlanetGeneration.WorldGen;
using PlanetGeneration.WorldGen.Polygon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace PlanetGeneration;

/// <summary>
/// 多边形地块层的构建与渲染。
///
/// 单独放一个文件是为了把改造的"新增面"集中在一处：既有文件只加接线，逻辑都在这里。
///
/// 设计要点：
///   · 地块网格是 (宽, 高, 种子, 目标地块数) 的**纯函数**，因此不写进缓存，
///     缓存命中时按需重建即可（约 300ms），代价远小于把拓扑序列化进去；
///   · 构建是幂等的，栅格渲染路径完全不碰它——模式为 <see cref="PolygonTileMode.Raster"/> 时零开销；
///   · 渲染复用 <see cref="WorldRenderer"/> 的配色，保证与栅格图层可以做逐像素 A/B 对照。
/// </summary>
public partial class Main : Control
{
    /// <summary>地块边界描边颜色（近黑，压住地块之间的接缝）。</summary>
    private static readonly byte[] PolygonBorderRgba = { 26, 28, 32, 255 };

    /// <summary>
    /// 河流密度为 1.0 时，陆地块中被判为河道的比例。
    /// 6% 大约对应"每 4 个地块里有 1 个与河道相邻"的观感。
    /// </summary>
    private const float DefaultRiverCellFraction = 0.06f;

    /// <summary>
    /// 解析实际使用的目标地块数。
    /// 默认按"地块边长约 4 个源像素"反推，并夹在 [2048, 32768] 之间：
    /// 太小地块没有形状可言，太大则超过源栅格分辨率、纯属浪费。
    /// </summary>
    private int ResolveCellsDesired()
    {
        if (_cellsDesired > 0)
        {
            return _cellsDesired;
        }

        var suggested = PolygonGridBuilder.SuggestCellsDesired(MapWidth, MapHeight, AutoCellsTargetSpacingPixels);
        return Mathf.Clamp(suggested, MinAutoCellsDesired, MaxAutoCellsDesired);
    }

    /// <summary>
    /// 构建多边形地块层。幂等：已经构建过就直接返回。
    /// 模式为 <see cref="PolygonTileMode.Raster"/> 时不做任何事，等同于改造前的行为。
    /// </summary>
    private void BuildPolygonLayer(GeneratedWorldData world)
    {
        if (_polygonTileMode == PolygonTileMode.Raster || world.PolygonGrid != null)
        {
            return;
        }

        var totalTimer = Stopwatch.StartNew();

        // 缓存恢复的世界上带着当初用的目标地块数，必须沿用，否则地块图与已缓存的数据对不上。
        var cellsDesired = world.PolygonCellsDesired > 0 ? world.PolygonCellsDesired : ResolveCellsDesired();

        var buildTimer = Stopwatch.StartNew();
        var grid = PolygonGridBuilder.Create(MapWidth, MapHeight, Seed, cellsDesired, 8, out var stats);
        buildTimer.Stop();

        world.PolygonCellsDesired = cellsDesired;

        var mapTimer = Stopwatch.StartNew();
        var cellMap = PolygonRasterizer.BuildCellMap(grid, MapWidth, MapHeight);
        mapTimer.Stop();

        var sampleTimer = Stopwatch.StartNew();
        var fields = grid.Fields;

        // 连续量：面积加权平均，保留梯度。
        RasterPolygonBridge.SampleContinuous(cellMap, world.Elevation, fields.Height);
        RasterPolygonBridge.SampleContinuous(cellMap, world.Temperature, fields.Temperature);
        RasterPolygonBridge.SampleContinuous(cellMap, world.Moisture, fields.Moisture);

        // 河流：不再采样栅格河流，而是在地块图上按最陡下降方向重新生成河网。
        // 河道占比直接由"河流密度"滑杆线性换算——密度是用户可解释的量，不需要按地图尺寸重新标定。
        // 栅格河流（world.River）保持不变，继续供栅格渲染与文明模拟使用，等 P4 收尾时再统一。
        PolygonRiverBuilder.Generate(grid, SeaLevel, DefaultRiverCellFraction * RiverDensity);

        // 离散量：质心采样，避免一个地块里出现两个群系。
        RasterPolygonBridge.SampleDiscreteAtCentroid(grid, cellMap, ConvertToByteRaster(world.Biome), fields.Biome);
        RasterPolygonBridge.SampleDiscreteAtCentroid(grid, cellMap, ConvertToByteRaster(world.Rock), fields.Rock);
        RasterPolygonBridge.SampleDiscreteAtCentroid(grid, cellMap, ConvertToByteRaster(world.Ore), fields.Ore);
        RasterPolygonBridge.SampleDiscreteAtCentroid(grid, cellMap, world.PlateResult.PlateIds, fields.PlateId);
        RasterPolygonBridge.SampleDiscreteAtCentroid(
            grid, cellMap, ConvertToByteRaster(world.PlateResult.BoundaryTypes), fields.PlateBoundary);

        // 地貌：用**地块邻接**（Cells.C）分类，而不是在质心像素上跑栅格分类器。
        // 这样地貌区域的边界会贴着地块边界走，与"地块划分"图层对齐；
        // 悬停面板读的也是同一份结果（见 SampleFromCell），不会出现"图层画的和悬停说的不一致"。
        PolygonLandformClassifier.ClassifyAll(grid, SeaLevel, BasinSensitivity, fields.Landform);

        AssignCitiesToCells(grid, world.Cities, fields.CityId, out var cityCell);
        world.CityCell = cityCell;

        // 拓扑派生：下游方向与汇流量。这是旧栅格模型给不出的结构。
        PolygonTopologyBuilder.BuildDownslope(grid);
        PolygonTopologyBuilder.BuildFlux(grid);
        sampleTimer.Stop();

        world.PolygonGrid = grid;
        world.PolygonCellMap = cellMap;

        GD.Print(
            $"[Polygon][地图 {MapWidth}x{MapHeight}] 地块 {stats.CellCount}（目标 {cellsDesired}，点距 {stats.SpacingX:0.00}x{stats.SpacingY:0.00}）"
            + $" 平均邻接 {stats.AverageNeighbors:0.00} 建图 {buildTimer.ElapsedMilliseconds} ms"
            + $" 归属图 {mapTimer.ElapsedMilliseconds} ms 采样与拓扑 {sampleTimer.ElapsedMilliseconds} ms"
            + $" 扫描线漏像素 {cellMap.UnassignedPixels} 补裁 {stats.RepairedCells} 退化 {stats.DegenerateCells}");
    }

    /// <summary>确保地块层已构建；返回地块网格（模式为 Raster 时返回 null）。</summary>
    private PolygonGrid? EnsurePolygonLayer(GeneratedWorldData world)
    {
        BuildPolygonLayer(world);
        return world.PolygonGrid;
    }

    /// <summary>
    /// 把城市点归属到地块：<c>cityId[cell] = 城市下标</c>（无城市的保持 -1），
    /// 同时输出反向映射 <c>cityCell[城市下标] = 地块编号</c>。
    /// </summary>
    private static void AssignCitiesToCells(
        PolygonGrid grid,
        List<CityInfo> cities,
        int[] cityId,
        out int[] cityCell)
    {
        cityCell = new int[cities.Count];
        if (cities.Count == 0)
        {
            return;
        }

        var pointX = new int[cities.Count];
        var pointY = new int[cities.Count];
        for (var i = 0; i < cities.Count; i++)
        {
            pointX[i] = cities[i].Position.X;
            pointY[i] = cities[i].Position.Y;
        }

        RasterPolygonBridge.AssignPointsToCells(grid, pointX, pointY, cityCell);

        for (var i = 0; i < cities.Count; i++)
        {
            // 多个城市落进同一个地块时，保留靠前的那个（城市列表本身已按评分排序）。
            if (cityId[cityCell[i]] < 0)
            {
                cityId[cityCell[i]] = i;
            }
        }
    }

    /// <summary>
    /// 把枚举栅格转成字节栅格，供离散量采样使用。
    /// 尺寸直接从数组自身取，避免与调用处的宽高不一致。
    /// </summary>
    private static byte[,] ConvertToByteRaster<T>(T[,] source)
        where T : struct, Enum
    {
        var width = source.GetLength(0);
        var height = source.GetLength(1);
        var result = new byte[width, height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                result[x, y] = Convert.ToByte(source[x, y]);
            }
        }

        return result;
    }

    /// <summary>
    /// 哪些图层用多边形渲染。
    ///
    /// · <see cref="PolygonTileMode.Raster"/>：全部栅格，地块层不参与渲染；
    /// · <see cref="PolygonTileMode.Hybrid"/>：只迁移**离散分类**图层——一个地块只有一种取值，
    ///   多边形填充不会在格子边界上混色；
    /// · <see cref="PolygonTileMode.Cells"/> / <see cref="PolygonTileMode.Outlined"/>：
    ///   **所有能按地块属性着色的图层**，连续场在这里是"逐地块取一个采样值再平铺"。
    ///
    /// 无论哪个模式，生态 / 文明 / 贸易 / 城市 / 风场 都留在栅格：
    /// 前四者的数据源是逐像素的模拟器输出（P4 后续迁移），风场需要在地图上叠加箭头。
    /// </summary>
    private bool ShouldRenderAsPolygon(MapLayer layer)
    {
        if (_polygonTileMode == PolygonTileMode.Raster)
        {
            return false;
        }

        if (layer is MapLayer.Wind)
        {
            return false;
        }

        return _polygonTileMode != PolygonTileMode.Hybrid || IsDiscreteClassificationLayer(layer);
    }

    /// <summary>
    /// 城市高亮掩码：城市所在的地块**以及它的全部直接邻居**。
    /// 栅格版是按像素半径画一个圆点；地块版直接用邻接（见
    /// <see cref="PolygonTopologyBuilder.ExpandToNeighborMask"/>）。
    /// </summary>
    private static bool[] BuildCityHighlightMask(PolygonGrid grid)
    {
        var seeds = new bool[grid.Count];
        var cityId = grid.Fields.CityId;

        for (var cell = 0; cell < grid.Count; cell++)
        {
            seeds[cell] = cityId[cell] >= 0;
        }

        return PolygonTopologyBuilder.ExpandToNeighborMask(grid, seeds);
    }

    /// <summary>
    /// 确保地块版生态模拟已按当前参数算过。
    ///
    /// 栅格版生态模拟（<c>world.EcologySimulation</c>）继续保留，供栅格渲染与文明模拟使用；
    /// 地块版是独立的另一份结果，只在需要多边形渲染生态图层时才算——它是闭式的、很快，重复计算不值得优化。
    /// </summary>
    private void EnsurePolygonEcology(GeneratedWorldData world)
    {
        var grid = EnsurePolygonLayer(world);
        if (grid == null)
        {
            return;
        }

        var signature = BuildEcologySignature();
        if (world.PolygonEcology != null && world.PolygonEcologySignature == signature)
        {
            return;
        }

        world.PolygonEcology = PolygonEcologySimulator.Simulate(
            grid,
            Seed,
            _currentEpoch,
            _speciesDiversity,
            _civilAggression,
            _magicDensity,
            SeaLevel);
        world.PolygonEcologySignature = signature;
    }

    /// <summary>
    /// 确保地块版文明模拟已按当前参数算过。
    ///
    /// 依赖链：地块文明模拟吃地块**生态**模拟的产出（<c>CivilizationPotential</c>），
    /// 所以这里先确保生态算过——否则文明会拿着上一次参数或全零的潜力场去算，静默出错。
    ///
    /// 栅格版文明模拟（<c>world.CivilizationSimulation</c>）继续保留，
    /// 供栅格渲染、时间轴热点与叙事使用；地块版是独立的另一份结果。
    /// </summary>
    private void EnsurePolygonCivilization(GeneratedWorldData world)
    {
        var grid = EnsurePolygonLayer(world);
        if (grid == null)
        {
            return;
        }

        EnsurePolygonEcology(world);

        // 签名必须并入生态的参数（多样性/魔法/侵略性），理由见字段注释。
        var signature = HashCode.Combine(BuildEcologySignature(), (int)_polygonTileMode);
        if (world.PolygonCivilization != null && world.PolygonCivilizationSignature == signature)
        {
            return;
        }

        world.PolygonCivilization = PolygonCivilizationSimulator.Simulate(
            grid,
            Seed,
            _currentEpoch,
            _civilAggression,
            _speciesDiversity,
            SeaLevel);
        world.PolygonCivilizationSignature = signature;
    }

    /// <summary>离散分类图层：一个地块只有一种取值，与连续场相对。</summary>
    private static bool IsDiscreteClassificationLayer(MapLayer layer)    {
        return layer switch
        {
            MapLayer.Biomes => true,
            MapLayer.RockTypes => true,
            MapLayer.Ores => true,
            MapLayer.Landform => true,
            MapLayer.Plates => true,
            _ => false,
        };
    }

    /// <summary>
    /// 用多边形渲染一个图层：逐地块取色（复用栅格渲染的同一套配色）→ 填充 → 可选描边。
    /// 地块层不可用时返回 null，调用方退回栅格渲染。
    /// </summary>
    private Image? RenderPolygonAttributeLayer(GeneratedWorldData world, MapLayer layer)
    {
        var grid = EnsurePolygonLayer(world);
        if (grid == null || world.PolygonCellMap == null)
        {
            return null;
        }

        var fields = grid.Fields;
        var sites = world.PlateResult.Sites;

        // 生态图层需要先按当前参数把地块生态算出来（闭式模拟，很快）。
        if (layer == MapLayer.Ecology)
        {
            EnsurePolygonEcology(world);
        }

        // 文明与贸易两个图层吃文明模拟的产出。
        // 注意贸易图**同样**需要跑完整的文明模拟——不能只跑贸易，因为走廊的端点（枢纽）
        // 与可行性（政体关系）都由文明模拟决定。这是栅格版就有的依赖，这里保持一致。
        if (layer is MapLayer.Civilization or MapLayer.TradeRoutes)
        {
            EnsurePolygonCivilization(world);
        }

        // 城市图层需要一张"城市及其邻居"的掩码，先算一次再进主循环。
        var cityHighlight = layer == MapLayer.Cities ? BuildCityHighlightMask(grid) : null;
        var cityMarker = _renderer.GetCityMarkerColor();
        var cityMarkerRgba = new byte[]
        {
            ToChannelByte(cityMarker.R),
            ToChannelByte(cityMarker.G),
            ToChannelByte(cityMarker.B),
            255,
        };

        var cellRgba = new byte[grid.Count * 4];

        for (var cell = 0; cell < grid.Count; cell++)
        {
            var color = layer switch
            {
                // ── 离散分类：一个地块一种取值 ──
                MapLayer.Biomes => _renderer.GetBiomeColor((BiomeType)fields.Biome[cell]),
                MapLayer.RockTypes => _renderer.GetRockColor((RockType)fields.Rock[cell], fields.Height[cell], SeaLevel),
                MapLayer.Ores => _renderer.GetOreColor((OreType)fields.Ore[cell], fields.Height[cell], SeaLevel),
                MapLayer.Landform => GetLandformColor((LandformType)fields.Landform[cell]),
                MapLayer.Plates => _renderer.GetPlateColor(
                    PlateDebugColor(sites, fields.PlateId[cell]),
                    (PlateBoundaryType)fields.PlateBoundary[cell]),

                // ── 模拟器输出：P4 地块化后由地块属性直接着色 ──
                MapLayer.Civilization => _renderer.GetCivilizationColor(
                    fields.Influence[cell],
                    fields.PolityId[cell],
                    fields.BorderMask[cell],
                    fields.Height[cell],
                    SeaLevel),
                MapLayer.TradeRoutes => _renderer.GetTradeRouteColor(
                    fields.TradeRouteMask[cell],
                    fields.TradeFlow[cell],
                    fields.Influence[cell],
                    fields.Height[cell],
                    SeaLevel),

                // ── 连续场：逐地块取一个采样值再平铺（Cells 模式） ──
                MapLayer.Satellite => _renderer.GetSatelliteColor(
                    WorldRenderer.ComputePolarMask(GetCellAnchor(grid, cell).Y, MapHeight),
                    fields.Height[cell],
                    fields.Temperature[cell],
                    fields.Moisture[cell],
                    (BiomeType)fields.Biome[cell],
                    fields.River[cell],
                    SeaLevel),
                MapLayer.Temperature => _renderer.GetTemperatureColor(fields.Temperature[cell]),
                MapLayer.Moisture => _renderer.GetMoistureColor(fields.Moisture[cell]),
                MapLayer.Elevation => _renderer.GetElevationColor(fields.Height[cell], SeaLevel, _elevationStyle),
                MapLayer.Rivers => _renderer.GetRiverColor(fields.Height[cell], SeaLevel, fields.River[cell]),
                MapLayer.Ecology => _renderer.GetEcologyColor(
                    fields.EcologyHealth[cell], fields.Height[cell], SeaLevel),
                MapLayer.Cities => cityHighlight![cell]
                    ? cityMarker
                    : _renderer.GetSatelliteColor(
                        WorldRenderer.ComputePolarMask(GetCellAnchor(grid, cell).Y, MapHeight),
                        fields.Height[cell],
                        fields.Temperature[cell],
                        fields.Moisture[cell],
                        (BiomeType)fields.Biome[cell],
                        fields.River[cell],
                        SeaLevel).Lerp(Colors.Black, 0.45f),

                _ => Colors.Magenta,
            };

            var index = cell * 4;
            cellRgba[index] = ToChannelByte(color.R);
            cellRgba[index + 1] = ToChannelByte(color.G);
            cellRgba[index + 2] = ToChannelByte(color.B);
            cellRgba[index + 3] = 255;
        }

        return ComposePolygonImage(world, cellRgba, _polygonTileMode == PolygonTileMode.Outlined);
    }

    /// <summary>取板块的调色；编号越界时退回灰色而不是抛异常（缓存恢复过的世界可能没有站点）。</summary>
    private static Color PlateDebugColor(List<PlateSite> sites, int plateId)
    {
        if (plateId < 0 || plateId >= sites.Count)
        {
            return new Color(0.5f, 0.5f, 0.55f);
        }

        return sites[plateId].DebugColor;
    }

    /// <summary>
    /// "地块划分"图层：中性底色 + 地块边界。
    ///
    /// 它不再按属性着色——那是"生物群系"等图层的职责，重复着色反而看不出地块结构。
    /// 这一层专门用来看地块划分本身：网格是否均匀、边界是否连续、经度缝处是否接得上。
    /// </summary>
    private Image? RenderPolygonGridLayer(GeneratedWorldData world)
    {
        var grid = EnsurePolygonLayer(world);
        if (grid == null || world.PolygonCellMap == null)
        {
            return null;
        }

        var map = world.PolygonCellMap;
        var buffer = new byte[MapWidth * MapHeight * 4];

        for (var i = 0; i < buffer.Length; i += 4)
        {
            buffer[i] = 236;
            buffer[i + 1] = 236;
            buffer[i + 2] = 232;
            buffer[i + 3] = 255;
        }

        PolygonRasterizer.StrokeBorders(buffer, MapWidth, MapHeight, map, 40, 44, 52, 255);

        var image = Image.CreateFromData(MapWidth, MapHeight, false, Image.Format.Rgba8, buffer);
        return MapWidth == OutputWidth && MapHeight == OutputHeight
            ? image
            : UpscaleImageNearest(image, OutputWidth, OutputHeight);
    }

    /// <summary>把每地块颜色填成图，并按需描边，最后统一放大到输出尺寸。</summary>
    private Image ComposePolygonImage(GeneratedWorldData world, byte[] cellRgba, bool outlined)
    {
        var map = world.PolygonCellMap!;
        var buffer = new byte[MapWidth * MapHeight * 4];
        PolygonRasterizer.FillRgba(buffer, MapWidth, MapHeight, map, cellRgba);

        if (outlined)
        {
            PolygonRasterizer.StrokeBorders(
                buffer,
                MapWidth,
                MapHeight,
                map,
                PolygonBorderRgba[0],
                PolygonBorderRgba[1],
                PolygonBorderRgba[2],
                PolygonBorderRgba[3]);
        }

        var image = Image.CreateFromData(MapWidth, MapHeight, false, Image.Format.Rgba8, buffer);
        return MapWidth == OutputWidth && MapHeight == OutputHeight
            ? image
            : UpscaleImageNearest(image, OutputWidth, OutputHeight);
    }

    /// <summary>地块图层的状态摘要，拼进信息栏。</summary>
    private string BuildPolygonLayerSummary(GeneratedWorldData world)
    {
        var grid = EnsurePolygonLayer(world);
        if (grid == null)
        {
            return " | 地块层未启用";
        }

        long neighborTotal = 0;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            neighborTotal += grid.GetNeighborCount(cell);
        }

        var coverage = world.PolygonCellMap != null
            ? (double)MapWidth * MapHeight / Math.Max(grid.Count, 1)
            : 0d;

        return $" | 地块数:{grid.Count}（目标 {world.PolygonCellsDesired}）"
            + $" | 平均邻接:{neighborTotal / (double)grid.Count:0.00}"
            + $" | 地块边长:{grid.SpacingX:0.0}×{grid.SpacingY:0.0} 源像素"
            + $" | 每格约 {coverage:0.0} 像素";
    }

    // ── 交互：把鼠标位置解析成地块 ────────────────────────────────────
    //
    // 改造前拾取是"局部坐标 → 纹理比例 → 像素下标"，交互单元就是像素。
    // 改造后统一走地块：FindCell 是精确的最近站点查询，均摊 O(1)，
    // 而且"最近站点"与"点落在哪个多边形内"严格等价，所以拾取结果不可能落到多边形之外。
    //
    // 栅格模式（PolygonTileMode.Raster）下地块层不存在，自动退回像素路径。

    /// <summary>
    /// 一次采样的结果。
    /// 把"鼠标落在哪里"统一成地块或像素两种来源，下游的叙事、威胁评估、悬停面板
    /// 只吃这个结构，因此两条路径不需要各写一份逻辑。
    /// </summary>
    private readonly record struct CellSample(
        bool IsCell,
        int CellId,
        int AnchorX,
        int AnchorY,
        float Elevation,
        float Temperature,
        float Moisture,
        float River,
        BiomeType Biome,
        LandformType Landform);

    /// <summary>
    /// 把地图纹理上的局部坐标解析成一次采样。
    /// 地块层可用时按地块取（精确），否则按像素取（改造前的行为）。
    /// </summary>
    private bool TrySampleAtLocalPosition(Vector2 localPosition, out CellSample sample)
    {
        sample = default;

        if (_primaryWorld == null)
        {
            return false;
        }

        var textureSize = _mapTexture.Size;
        if (textureSize.X <= 1f || textureSize.Y <= 1f)
        {
            return false;
        }

        var tX = Mathf.Clamp(localPosition.X / textureSize.X, 0f, 0.999999f);
        var tY = Mathf.Clamp(localPosition.Y / textureSize.Y, 0f, 0.999999f);

        var grid = EnsurePolygonLayer(_primaryWorld);
        if (grid != null)
        {
            // FindCell 吃的是源栅格像素空间的连续坐标，不需要先取整。
            var cellId = grid.FindCell(tX * MapWidth, tY * MapHeight);
            sample = SampleFromCell(_primaryWorld, grid, cellId);
            return true;
        }

        var pixelX = Mathf.Clamp((int)(tX * MapWidth), 0, MapWidth - 1);
        var pixelY = Mathf.Clamp((int)(tY * MapHeight), 0, MapHeight - 1);
        sample = SampleFromPixel(_primaryWorld, pixelX, pixelY);
        return true;
    }

    /// <summary>按地块取属性。世界显式传入，避免调用方与内部字段不一致。</summary>
    private CellSample SampleFromCell(GeneratedWorldData world, PolygonGrid grid, int cellId)
    {
        var fields = grid.Fields;
        var anchor = GetCellAnchor(grid, cellId);

        // 地貌读的是地块自己那份分类结果（由 Cells.C 邻接算出），
        // 因此悬停面板显示的与"地貌"图层画出来的必然一致。
        var landform = (LandformType)fields.Landform[cellId];

        return new CellSample(
            true,
            cellId,
            anchor.X,
            anchor.Y,
            fields.Height[cellId],
            fields.Temperature[cellId],
            fields.Moisture[cellId],
            fields.River[cellId],
            (BiomeType)fields.Biome[cellId],
            landform);
    }

    /// <summary>按像素取属性（改造前的口径，地块层不可用时使用）。</summary>
    private CellSample SampleFromPixel(GeneratedWorldData world, int x, int y)
    {
        return new CellSample(
            false,
            -1,
            x,
            y,
            world.Elevation[x, y],
            world.Temperature[x, y],
            world.Moisture[x, y],
            world.River[x, y],
            world.Biome[x, y],
            ClassifyLandform(x, y, SeaLevel, world.Elevation, world.Moisture, world.River));
    }

    /// <summary>
    /// 取地块的锚点像素（质心取整）。
    /// 质心一定落在自己的多边形内（几何自检判据之一），所以这个像素必然属于该地块；
    /// 骑在经度缝上的地块要先归一化横坐标。
    /// </summary>
    private (int X, int Y) GetCellAnchor(PolygonGrid grid, int cellId)
    {
        var centroidX = grid.NormalizeX(grid.CentroidX[cellId]);
        var x = Mathf.Clamp((int)Math.Floor(centroidX), 0, MapWidth - 1);
        var y = Mathf.Clamp((int)Math.Floor(grid.CentroidY[cellId]), 0, MapHeight - 1);
        return (x, y);
    }

    /// <summary>悬停面板文本。地块来源时额外给出地块自身的结构信息。</summary>
    private string BuildCellHoverText(CellSample sample)
    {
        var builder = new StringBuilder(384);
        builder.Append("群系：").Append(GetBiomeDisplayName(sample.Biome)).Append('\n');
        builder.Append("说明：").Append(GetBiomeDetailText(sample.Biome)).Append('\n');
        builder.Append("地貌：").Append(GetLandformDisplayName(sample.Landform)).Append('\n');
        builder.Append("地貌说明：").Append(GetLandformDetailText(sample.Landform)).Append('\n');

        if (sample.IsCell)
        {
            builder.Append(BuildCellStructureText(sample.CellId));
        }

        builder.Append("坐标：").Append(sample.AnchorX).Append(", ").Append(sample.AnchorY).Append('\n');
        builder.Append("高度：").Append(BuildAltitudeDisplayText(sample.Elevation, SeaLevel, _currentReliefExaggeration)).Append('\n');
        builder.Append("温度：").Append(NormalizedTemperatureToCelsius(sample.Temperature).ToString("0.0")).Append("℃\n");
        builder.Append("湿度：").Append(sample.Moisture.ToString("0.00")).Append('\n');
        builder.Append("河流强度：").Append(sample.River.ToString("0.00"));

        return builder.ToString();
    }

    /// <summary>地块自身的结构信息：编号、面积、邻接数，以及两个只有地块模型才有的标记。</summary>
    private string BuildCellStructureText(int cellId)
    {
        var grid = _primaryWorld?.PolygonGrid;
        if (grid == null || cellId < 0 || cellId >= grid.Count)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(160);
        builder.Append("地块：#").Append(cellId).Append('\n');
        builder.Append("面积：").Append(grid.Area[cellId].ToString("0.0")).Append(" 源像素²\n");
        builder.Append("邻接数：").Append(grid.GetNeighborCount(cellId));

        if (grid.CellPole[cellId])
        {
            builder.Append("（贴极圈）");
        }

        if (grid.CellSeam[cellId])
        {
            builder.Append("（跨经度缝）");
        }

        builder.Append('\n');
        return builder.ToString();
    }

    // ── 悬停高亮 ─────────────────────────────────────────────────────

    /// <summary>裁剪容器：跨经度缝的地块会多画一份副本，超出画布的部分必须裁掉。</summary>
    private Control? _cellHighlightClip;

    private MapCellOverlay? _cellHighlight;

    /// <summary>当前高亮的地块编号；窗口尺寸变化时据此重建。</summary>
    private int _highlightedCellId = -1;

    /// <summary>更新悬停高亮；<paramref name="cellId"/> 传 -1 表示清除。</summary>
    private void UpdateCellHighlight(int cellId)
    {
        _highlightedCellId = cellId;
        if (_mapCanvas != null && IsInstanceValid(_mapCanvas))
        {
            _mapCanvas.SetHoveredCell(cellId);
        }
    }

    private void HideCellHighlight()
    {
        _highlightedCellId = -1;
        if (_mapCanvas != null && IsInstanceValid(_mapCanvas))
        {
            _mapCanvas.SetHoveredCell(-1);
        }
    }

    /// <summary>
    /// 懒创建高亮节点。挂成 <c>MapTexture</c> 的子节点，
    /// 这样局部坐标系就是纹理空间，不必处理缩放祖先带来的变换。
    /// </summary>
    private MapCellOverlay? EnsureCellHighlight()
    {
        if (_cellHighlight != null && IsInstanceValid(_cellHighlight))
        {
            return _cellHighlight;
        }

        if (_mapTexture == null || !IsInstanceValid(_mapTexture))
        {
            return null;
        }

        _cellHighlightClip = new Control
        {
            Name = "CellHighlightClip",
            MouseFilter = MouseFilterEnum.Ignore,
            ClipContents = true,
            Visible = false
        };
        _mapTexture.AddChild(_cellHighlightClip);

        _cellHighlight = new MapCellOverlay
        {
            Name = "CellHighlight",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _cellHighlightClip.AddChild(_cellHighlight);
        _cellHighlight.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // 窗口尺寸变化会改变纹理的实际绘制区域，需要重算裁剪矩形与多边形坐标。
        _mapTexture.Resized += OnMapTextureResizedForCellHighlight;
        return _cellHighlight;
    }

    private void OnMapTextureResizedForCellHighlight()
    {
        if (_highlightedCellId >= 0)
        {
            UpdateCellHighlight(_highlightedCellId);
        }
    }

    /// <summary>让裁剪容器对齐纹理在 TextureRect 内的实际绘制矩形（保持宽高比居中）。</summary>
    private void SyncCellHighlightRect()
    {
        if (_cellHighlightClip == null || !IsInstanceValid(_cellHighlightClip) || _mapTexture?.Texture == null)
        {
            return;
        }

        var textureSize = _mapTexture.Texture.GetSize();
        var painted = FitRectInside(textureSize, _mapTexture.Size);
        _cellHighlightClip.Position = painted.Position;
        _cellHighlightClip.Size = painted.Size;
    }

    /// <summary>
    /// 构造高亮用的多边形环（纹理局部坐标）。
    ///
    /// 几何部分（对齐到最近镜像副本 + 按 ±地图宽度补两份）在核心层
    /// <see cref="PolygonGrid.GetHighlightRings"/> 里，可被自检覆盖；
    /// 这里只负责把源栅格坐标映射到纹理局部坐标。
    /// </summary>
    private List<Vector2[]>? BuildCellHighlightRings(int cellId)
    {
        var grid = _primaryWorld?.PolygonGrid;
        if (grid == null || cellId < 0 || cellId >= grid.Count || _mapTexture?.Texture == null)
        {
            return null;
        }

        var textureSize = _mapTexture.Texture.GetSize();
        if (textureSize.X < 1f || textureSize.Y < 1f)
        {
            return null;
        }

        var painted = FitRectInside(textureSize, _mapTexture.Size);
        if (painted.Size.X <= 0f || painted.Size.Y <= 0f)
        {
            return null;
        }

        var rings = grid.GetHighlightRings(cellId);
        if (rings.Length == 0 || rings[0].Length < 3)
        {
            return null;
        }

        // 输出图是源栅格的最近邻放大，两者之间的缩放比就是纹理尺寸比。
        var scale = new Vector2(textureSize.X / MapWidth, textureSize.Y / MapHeight);
        var result = new List<Vector2[]>(rings.Length);

        foreach (var ring in rings)
        {
            var points = new Vector2[ring.Length];
            for (var i = 0; i < ring.Length; i++)
            {
                var source = new Vector2((float)ring[i].X, (float)ring[i].Y);
                points[i] = (source * scale) / textureSize * painted.Size;
            }

            result.Add(points);
        }

        return result;
    }
}
