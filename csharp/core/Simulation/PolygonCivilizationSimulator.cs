using System;
using System.Collections.Generic;

namespace PlanetGeneration.Core.Simulation;

using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;


/// <summary>
/// 地块文明模拟：把逐像素的 <c>CivilizationSimulator</c>（1137 行）搬到地块图上。
///
/// ## 为什么这不是"换个循环"
///
/// 栅格版有三类运算必须逐个改造，而不是机械平移：
///
/// 1. **邻域**。栅格版硬编码 8 邻域（<c>for oy in -1..1</c>）判断"是否临海/临河/有外国邻居"；
///    地块版一律改成遍历 <c>grid.CellNeighbors</c> 的真实多边形邻接。
///    副作用是**邻接数不再是固定 8**（抖动方格下 5~7），所以"有几个外国邻居"这类判据
///    必须改成按**比例**比较，否则边疆地块会因邻居少而永远达不到阈值——
///    这与 P4 第一阶段地貌分类踩到的坑是同一个。
///
/// 2. **扩张评分**。栅格版用 `distance = sqrt(dx² + dy²)`，且政体站点是像素坐标；
///    地块版用 `grid.WrappedDistance`（经度环绕的测地距离）。
///    更关键的是，栅格版的 `expansionRange` 是**像素**量纲，直接搬到地块图上会让
///    扩张半径缩水到 1/spacing 倍。所以这里显式把射程与距离都换算成
///    **"平均地块直径"** 作为度量单位——这是个纯量纲变换，不改变算法的形状。
///
/// 3. **贸易走廊**。栅格版在两个枢纽之间做**像素直线光栅化**（Bresenham 风格插值）；
///    地块版改成**沿真实邻接做最短路**（Dijkstra，代价含地形与海陆惩罚）。
///    这不只是"更地块化"——直线走廊会直接穿过山脉与海峡（栅格版用"跳过海洋像素"掩盖了这一点，
///    代价是走廊会断成几截），而沿邻接的最短路天然连续，且能绕过地形障碍。
///
/// 与 <c>PolygonEcologySimulator</c> 一样，本类所在的命名空间**不引用 Godot**，可独立自检。
/// </summary>

/// <summary>文明模拟运行的几何与属性上下文桥接。</summary>
internal readonly struct SimGrid
{
    public readonly CellGeometry Geometry;
    public readonly CellFields Fields;
    public SimGrid(CellGeometry geometry, CellFields fields)
    {
        Geometry = geometry;
        Fields = fields;
    }
    public int Count => Geometry.Count;
    public double SpacingX => Geometry.SpacingX;
    public double Width => Geometry.Width;
    public double Height => Geometry.Height;
    public double[] SiteX => Geometry.SiteX;
    public double[] SiteY => Geometry.SiteY;
    public int[] CellNeighborStart => Geometry.CellNeighborStart;
    public int[] CellNeighbors => Geometry.CellNeighbors;
    public double[] Area => Geometry.Area;
    public double WrappedDistance(double x0, double y0, double x1, double y1)
        => Geometry.Extent.DistanceWrapped(new PolyVec2(x0, y0), new PolyVec2(x1, y1));
}

public static class PolygonCivilizationSimulator
{
    public static CivilizationResult Simulate(
        CellGeometry geometry,
        CellFields fields,
        int seed,
        int epoch,
        int civilAggression,
        int speciesDiversity,
        float seaLevel)
    {
        return Simulate(new SimGrid(geometry, fields), seed, epoch, civilAggression, speciesDiversity, seaLevel);
    }

    /// <summary>政体原型；决定扩张时对不同地形的适应度。</summary>
    private enum PolityArchetype
    {
        Generic,
        Naval,
        River,
        Highland,
        Nomadic,
    }

    /// <summary>政体种子：一个政体的发源地与禀赋。</summary>
    private readonly struct PolitySeed
    {
        public int Id { get; }
        public int Cell { get; }
        public float Strength { get; }
        public float Expansionism { get; }
        public byte NativeBiome { get; }
        public PolityArchetype Archetype { get; }

        public PolitySeed(int id, int cell, float strength, float expansionism, byte nativeBiome, PolityArchetype archetype)
        {
            Id = id;
            Cell = cell;
            Strength = strength;
            Expansionism = expansionism;
            NativeBiome = nativeBiome;
            Archetype = archetype;
        }
    }

    /// <summary>贸易枢纽：一个城市地块及其等级。</summary>
    private readonly struct TradeHub
    {
        public int Cell { get; }
        public byte Tier { get; }
        public int PolityId { get; }
        public float Strength { get; }

        public TradeHub(int cell, byte tier, int polityId, float strength)
        {
            Cell = cell;
            Tier = tier;
            PolityId = polityId;
            Strength = strength;
        }
    }

    /// <summary>影响力达到这个值算作"核心腹地"。</summary>
    private const float CoreInfluenceThreshold = 0.64f;

    /// <summary>河流流量高于此值算作"临河"。</summary>
    private const float RiverProximityThreshold = 0.16f;

    /// <summary>海平面下 0.08 以内算"深海"之外的浅海，与栅格版口径一致。</summary>
    private const byte ShallowOceanBiome = 1;

    /// <summary>
    /// 运行地块文明模拟，结果写入 <c>Fields.Influence</c> / <c>PolityId</c> /
    /// <c>BorderMask</c> / <c>TradeRouteMask</c> / <c>TradeFlow</c>。
    /// </summary>
    /// <param name="grid">地块网格；需已采样好 Height/Temperature/Moisture/River/Biome/CityId，
    /// 且已完成生态模拟（<c>CivilizationPotential</c> 是输入的种子权重）。</param>
    /// <param name="seed">世界种子。</param>
    /// <param name="epoch">纪元。</param>
    /// <param name="civilAggression">文明侵略性 0~100。</param>
    /// <param name="speciesDiversity">物种多样性 0~100。</param>
    /// <param name="seaLevel">海平面。</param>
    internal static CivilizationResult Simulate(SimGrid grid,
        int seed,
        int epoch,
        int civilAggression,
        int speciesDiversity,
        float seaLevel)
    {
        var fields = grid.Fields;
        var count = grid.Count;

        var aggressionNorm = Clamp01(civilAggression / 100f);
        var diversityNorm = Clamp01(speciesDiversity / 100f);
        var epochFactor = 1f - MathF.Exp(-Math.Max(epoch, 0) * (0.008f + (0.012f * diversityNorm)));
        var safeSea = Math.Clamp(seaLevel, 0.0001f, 0.9999f);

        // ── 射程的量纲：以"多少圈地块"为单位，而不是像素 ──
        //
        // 这是本类唯一一处必须偏离栅格版的地方，也是踩过两次坑的地方。
        //
        // 栅格版的 expansionRange 是"10~48 像素"。在地块图上照搬会坏掉：
        // 地块边长（spacing）随地图尺寸与目标地块数变化（实测 4~16 px），
        // 于是同一个 48 在细图上覆盖 12 圈地块、在粗图上只覆盖 3 圈——
        // **同一套参数在不同地图上表现完全不同**，这正是"像素量纲"的根本问题。
        //
        // 解法是把射程定义成**地块圈数**，再乘 spacing 换算回像素：
        // 于是"政体能扩张多远"始终是"约多少圈邻居"，与分辨率、地块密度都无关。
        // 这比"隐式依赖像素"更正确，也是地块模型相对栅格模型的真实收益之一。
        //
        // （第一次尝试是把射程除以 spacing、却忘了归一化距离，导致射程缩到 1/spacing
        //   而距离仍是像素值，distanceFactor 直接归零——症状是"政体只有 4 个、控制率 1.6%"。
        //   **两边必须同时换算。**）
        var spacing = Math.Max(grid.SpacingX, 1e-6d);
        var expansionRings = Lerp(3.5f, 11f, epochFactor) * Lerp(0.82f, 1.2f, diversityNorm);
        var expansionRange = (float)(expansionRings * spacing);
        var claimThreshold = Lerp(0.20f, 0.34f, aggressionNorm);

        var landMask = BuildLandMask(grid, safeSea);
        var landCells = CountTrue(landMask);

        var seeds = BuildSeeds(
            grid,
            landMask,
            seed,
            safeSea,
            diversityNorm,
            aggressionNorm);

        // ── 逐地块归属：取影响力最大的政体 ──
        var influence = fields.Influence;
        var polityIdMap = fields.PolityId;
        Array.Clear(influence);
        for (var cell = 0; cell < count; cell++)
        {
            polityIdMap[cell] = -1;
        }

        for (var cell = 0; cell < count; cell++)
        {
            if (!landMask[cell])
            {
                continue;
            }

            var potential = Clamp01(fields.CivilizationPotential[cell]);
            var riverFactor = Clamp01(MathF.Sqrt(Clamp01(fields.River[cell])));
            var terrainPenalty = Clamp01((fields.Height[cell] - safeSea) / Math.Max(1f - safeSea, 0.0001f));

            // ── 河流是加成，不是必要条件 ──
            //
            // 旧式 localSupport = potential*0.72 + riverFactor*0.28 直接抄自栅格版。
            // 它在栅格图上不明显，因为那里"内陆"只是若干像素；但在地块图上，
            // 这 0.28 的权重意味着：**一个不下雨的肥沃平原，得分永远比一条瘦河边的
            // 贫瘠地低 28%**。无河内陆的潜力被无条件打七折，
            // 于是刚好卡在 claimThreshold 下方——这就是预览图上高原整块留白的最后一环。
            //
            // 河流的真实作用是"提升可达性上限"（引水、航运），属于加成项：
            // 旱地也能有文明（游牧、绿洲、雨水农业），只是不如河谷稠密。
            // 因此把 potential 的系数提到 0.88，河流保留 0.12 作为纯加成，
            // 并额外给一个乘性小加成，使得"临河"依然明显优于"不临河"，
            // 但"不临河"不再被罚到阈值之外。
            var riverBonus = 1f + (riverFactor * 0.10f);
            var localSupport = Clamp01(
                ((potential * 0.88f) + (riverFactor * 0.12f)) * riverBonus);
            var localCoastal = IsAdjacentToOcean(grid, cell, landMask);
            var localBiome = fields.Biome[cell];

            var bestScore = 0f;
            var bestPolity = -1;

            for (var i = 0; i < seeds.Count; i++)
            {
                var polity = seeds[i];
                var distance = grid.WrappedDistance(
                    grid.SiteX[cell], grid.SiteY[cell],
                    grid.SiteX[polity.Cell], grid.SiteY[polity.Cell]);

                // WrappedDistance 返回 double（顶点精度要求），这里显式收窄成 float 参与评分。
                var effectiveRange = Math.Max(expansionRange * polity.Strength * polity.Expansionism, 0.0001f);

                // ── 距离衰减：平台 + 长尾，而不是单一幂次 ──
                //
                // 旧式 1/(1 + (d/R)^1.35 * 2.8) 在 d=R 处就掉到 0.263，配合后面的
                // 地形阻尼与适应度，乘积天花板低于 claimThreshold——症状就是
                // "内陆高原整块留在阈值之下，地图上呈现环状空心"。
                //
                // 第一版修正把 d<=R 的平台直接给成 1.0，矫枉过正：
                // 射程内完全不衰减，强政体可以一路碾压到射程边界，
                // 政体数从 14 掉到 8、单块领土吃掉整个大陆——
                // 那是"没有竞争"的地图，同样不真实。
                //
                // 现在折中：平台期**不满值**（0.82 起步）且随距离温和下滑，
                // 使"近处明显强于远处"依然成立，政体之间重新产生可比较的竞争；
                // 但下滑足够慢（0.18/R 的斜率），不至于在射程内就跌破阈值，
                // 从而内陆仍能被填充。两条要求在同一个式子里同时满足。
                var distanceRatio = (float)distance / effectiveRange;
                float distanceFactor;
                if (distanceRatio <= 1f)
                {
                    // 平台期：0.82 ～ 1.0 的线性缓降（越近越强，但差距不悬殊）。
                    distanceFactor = 1f - (distanceRatio * 0.18f);
                }
                else
                {
                    // 尾部：从 0.82 继续按幂次衰减，保证"射程之外"迅速衰弱。
                    distanceFactor = 0.82f / (1f + MathF.Pow((distanceRatio - 1f) * 2.6f, 1.5f));
                }

                var terrainCost = ComputeExpansionPenalty(
                    polity.Archetype,
                    polity.NativeBiome,
                    localBiome,
                    terrainPenalty,
                    riverFactor,
                    localCoastal);
                var terrainAdaptation = 1f / (1f + (terrainCost * 0.55f));
                var nativeBiomeBonus = polity.NativeBiome == localBiome ? 1.07f : 1f;

                // ── 地形惩罚只施加一次 ──
                //
                // 旧式在乘性链上同时挂了 terrainAdaptation（由 terrainCost 展开，内含地形项）
                // 与 (1 - terrainPenalty*0.42)，等于对同一份海拔惩罚了两次。
                // 乘性链里的重复惩罚是"乘积陷阱"：每个因子单看都温和（0.58 / 0.86），
                // 相乘后把高原内陆整体推到阈值之下，而且**任何单项调参都救不回来**
                // ——这是诊断给出的实测结论（distanceFactor 置 1 时高原越阈值率仍只有 9.2%）。
                // 现在海拔只通过 terrainAdaptation 起作用。
                var score = localSupport
                    * polity.Strength
                    * terrainAdaptation
                    * nativeBiomeBonus
                    * distanceFactor
                    * (0.85f + (0.15f * epochFactor))
                    * (0.72f + (0.28f * (1f - (aggressionNorm * 0.45f))));

                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestPolity = polity.Id;
            }

            var finalInfluence = Clamp01(bestScore);
            influence[cell] = finalInfluence;
            if (bestPolity >= 0 && finalInfluence >= claimThreshold)
            {
                polityIdMap[cell] = (short)bestPolity;
            }
        }

        // ── 纪元动态：边界冲突 / 联盟缓和 / 征服易主 ──
        ApplyEpochDynamics(
            grid,
            landMask,
            seed,
            epoch,
            aggressionNorm,
            diversityNorm,
            out var conflictHeatPercent,
            out var allianceCohesionPercent,
            out var borderVolatilityPercent);

        // ── 编号规范化：剔除零领土政体，把编号压紧成 0..N-1 ──
        //
        // 为什么必须有这一步：种子编号是在归属阶段一次性分配的（0..seedCount-1），
        // 而之后还要跑纪元动态（征服、弃守），一个政体完全可能在中途丢掉全部领土。
        // 若不处理，polityIdMap 里就会出现"编号 >= 活跃政体数"的值——
        // 自检的"政体编号在范围内"正是用来抓这个的（它抓到了两次）。
        //
        // 曾经试过在评分阶段给发祥地加保底（BirthplaceHoldBonus），想从源头避免
        // 政体消失。那个方向是错的：它只是把归属阶段和纪元阶段两个独立环节
        // 强行对齐，结果纪元动态仍能造出零领土政体，
        // 而且保底还让本该被吞并的政体活了下来，破坏了
        // "纪元推进 → 政体数不增"的单调性（自检里表现为"出现反弹"）。
        //
        // 正确的位置是**出口处**：所有会改变归属的环节跑完之后，统一做一次规范化。
        // 这样不变量由结构保证，而不是靠各环节自觉。
        RenumberPolities(polityIdMap, landMask, count);

        // ── 统计：政体规模、核心腹地、边界掩码 ──
        var polityCellCount = new Dictionary<short, int>();
        var claimedCells = 0;
        var coreCells = 0;
        var borderMask = fields.BorderMask;
        Array.Clear(borderMask);

        for (var cell = 0; cell < count; cell++)
        {
            if (!landMask[cell])
            {
                continue;
            }

            var polityId = polityIdMap[cell];
            if (polityId < 0)
            {
                continue;
            }

            claimedCells++;
            if (influence[cell] >= CoreInfluenceThreshold)
            {
                coreCells++;
            }

            polityCellCount.TryGetValue(polityId, out var existing);
            polityCellCount[polityId] = existing + 1;

            if (HasForeignNeighbor(grid, cell, polityId, polityIdMap))
            {
                borderMask[cell] = true;
            }
        }

        var maxPolityCells = 0;
        foreach (var pair in polityCellCount)
        {
            if (pair.Value > maxPolityCells)
            {
                maxPolityCells = pair.Value;
            }
        }

        // ── 城市枢纽分级 ──
        var hubs = BuildCityHubs(
            grid,
            landMask,
            epochFactor,
            diversityNorm,
            aggressionNorm,
            out var hamletCount,
            out var townCount,
            out var cityStateCount);

        // ── 贸易走廊：沿真实邻接的最短路 ──
        var routePaths = new List<TradeRoutePath>();
        var connectedHubPercent = BuildTradeRoutes(
            grid,
            landMask,
            safeSea,
            epochFactor,
            aggressionNorm,
            hubs,
            routePaths);

        var tradeRouteCells = CountTrue(fields.TradeRouteMask);

        var recentEvents = BuildEpochEventLog(
            seed,
            epoch,
            aggressionNorm,
            diversityNorm,
            conflictHeatPercent,
            allianceCohesionPercent,
            borderVolatilityPercent,
            polityCellCount.Count,
            connectedHubPercent);

        return new CivilizationResult
        {
            PolityCount = polityCellCount.Count,
            HamletCount = hamletCount,
            TownCount = townCount,
            CityStateCount = cityStateCount,
            TradeRouteCells = tradeRouteCells,
            ControlledLandPercent = landCells > 0 ? 100f * claimedCells / landCells : 0f,
            CoreCellPercent = landCells > 0 ? 100f * coreCells / landCells : 0f,
            DominantPolitySharePercent = claimedCells > 0 ? 100f * maxPolityCells / claimedCells : 0f,
            ConnectedHubPercent = connectedHubPercent,
            ConflictHeatPercent = conflictHeatPercent,
            AllianceCohesionPercent = allianceCohesionPercent,
            BorderVolatilityPercent = borderVolatilityPercent,
            LandCellCount = landCells,
            RecentEvents = recentEvents,
            Routes = routePaths,
        };
    }

    // ────────────────────────────────────────────────────────────────
    // 基础判定：海陆、邻接
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 陆地掩码。口径与栅格版逐条一致：高程高于海平面，且群系不是海洋/浅海。
    /// </summary>
    private static bool[] BuildLandMask(SimGrid grid, float seaLevel)
    {
        var mask = new bool[grid.Count];
        var height = grid.Fields.Height;
        var biome = grid.Fields.Biome;

        for (var cell = 0; cell < grid.Count; cell++)
        {
            mask[cell] = height[cell] > seaLevel && biome[cell] > ShallowOceanBiome;
        }

        return mask;
    }

    /// <summary>
    /// 是否有海洋邻居。用真实多边形邻接替代栅格版的 8 邻域——这是地块化最直接的收益：
    /// 8 邻域会漏掉斜向但确实相邻的地块，而邻接表里没有这个问题。
    /// </summary>
    private static bool IsAdjacentToOcean(SimGrid grid, int cell, bool[] landMask)
    {
        var start = grid.CellNeighborStart[cell];
        var end = grid.CellNeighborStart[cell + 1];
        for (var k = start; k < end; k++)
        {
            if (!landMask[grid.CellNeighbors[k]])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>是否有"外国"邻居——即属于别的政体。无归属的邻居不算。</summary>
    private static bool HasForeignNeighbor(SimGrid grid, int cell, short polityId, short[] polityIdMap)
    {
        var start = grid.CellNeighborStart[cell];
        var end = grid.CellNeighborStart[cell + 1];
        for (var k = start; k < end; k++)
        {
            var neighborPolity = polityIdMap[grid.CellNeighbors[k]];
            if (neighborPolity >= 0 && neighborPolity != polityId)
            {
                return true;
            }
        }

        return false;
    }

    // ────────────────────────────────────────────────────────────────
    // 纪元动态
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 剔除零领土政体，把编号重排成连续的 0..N-1（保持原有相对顺序，保证确定性）。
    ///
    /// 调用时机必须是"所有会改动归属的环节都跑完之后"——否则新产生的零领土政体
    /// 又会把编号撑开。这个约束是结构性的，不能靠调用方自觉，
    /// 所以把它紧贴在统计之前，而不是散落在各处。
    /// </summary>
    private static void RenumberPolities(short[] polityIdMap, bool[] landMask, int count)
    {
        // 先收集"实际占有至少一个陆地地块"的编号，按升序处理以保持确定性。
        var occupied = new HashSet<short>();
        for (var cell = 0; cell < count; cell++)
        {
            if (!landMask[cell])
            {
                continue;
            }

            var polityId = polityIdMap[cell];
            if (polityId >= 0)
            {
                occupied.Add(polityId);
            }
        }

        if (occupied.Count == 0)
        {
            // 没有任何政体：把所有非负编号都清掉，避免留下悬空引用。
            for (var cell = 0; cell < count; cell++)
            {
                polityIdMap[cell] = -1;
            }

            return;
        }

        var ordered = new List<short>(occupied);
        ordered.Sort();

        // 旧编号 → 新编号。因为 ordered 已升序，映射必然单调，
        // 于是"纪元越晚编号越小/大"这类隐含顺序不会被这次重排打乱。
        var remap = new Dictionary<short, short>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            remap[ordered[i]] = (short)i;
        }

        for (var cell = 0; cell < count; cell++)
        {
            var polityId = polityIdMap[cell];
            polityIdMap[cell] = polityId >= 0 ? remap[polityId] : (short)-1;
        }
    }

    /// <summary>
    /// 纪元推进：在边界地块上做若干轮"冲突 vs 联盟"的局部博弈，可能发生征服易主。
    ///
    /// 与栅格版的差异：邻域改为真实邻接；"边界压力"由
    /// "外国邻居数 / 总邻居数"改为**按比例**计算——地块邻接数不是固定 8，
    /// 用绝对数量会让邻接少的地块永远判不出边界压力。
    /// </summary>
    private static void ApplyEpochDynamics(
        SimGrid grid,
        bool[] landMask,
        int seed,
        int epoch,
        float aggressionNorm,
        float diversityNorm,
        out float conflictHeatPercent,
        out float allianceCohesionPercent,
        out float borderVolatilityPercent)
    {
        var count = grid.Count;
        var influence = grid.Fields.Influence;
        var polityIdMap = grid.Fields.PolityId;

        var epochFactor = 1f - MathF.Exp(-Math.Max(epoch, 0) * 0.0095f);
        var turns = Math.Clamp((int)MathF.Round(Lerp(1f, 4f, epochFactor)), 1, 4);

        double conflictAccum = 0d;
        double allianceAccum = 0d;
        var borderSampleCount = 0;
        var changedEvents = 0;

        for (var turn = 0; turn < turns; turn++)
        {
            // 每轮都要重新扫描边界：上一轮的征服会改变政体归属，
            // 若沿用同一份边界集合，冲突就会在"已经统一"的地方继续计算。
            for (var cell = 0; cell < count; cell++)
            {
                if (!landMask[cell])
                {
                    continue;
                }

                var polity = polityIdMap[cell];
                if (polity < 0)
                {
                    continue;
                }

                var sameCount = 0;
                var foreignCount = 0;
                var bestForeignPolity = (short)-1;
                var bestForeignInfluence = 0f;

                var start = grid.CellNeighborStart[cell];
                var end = grid.CellNeighborStart[cell + 1];
                for (var k = start; k < end; k++)
                {
                    var neighbor = grid.CellNeighbors[k];
                    var neighborPolity = polityIdMap[neighbor];
                    if (neighborPolity < 0)
                    {
                        continue;
                    }

                    if (neighborPolity == polity)
                    {
                        sameCount++;
                        continue;
                    }

                    foreignCount++;
                    var neighborInfluence = influence[neighbor];
                    if (neighborInfluence > bestForeignInfluence)
                    {
                        bestForeignInfluence = neighborInfluence;
                        bestForeignPolity = neighborPolity;
                    }
                }

                if (foreignCount == 0)
                {
                    continue;
                }

                borderSampleCount++;

                // 按比例而不是绝对数量：见方法头的说明。
                var borderPressure = foreignCount / (float)(sameCount + foreignCount);
                var noise = HashNoise01(seed ^ unchecked((turn + 1) * 0x45d9f3b), cell);

                var localConflict = aggressionNorm
                    * (0.68f + (0.42f * noise))
                    * (0.55f + (0.45f * epochFactor))
                    * borderPressure;

                var localAlliance = (1f - aggressionNorm)
                    * (0.46f + (0.54f * diversityNorm))
                    * (0.74f + (0.26f * (1f - noise)))
                    * (1f - (borderPressure * 0.58f));

                conflictAccum += localConflict;
                allianceAccum += localAlliance;

                var currentInfluence = influence[cell];
                var nextInfluence = Math.Clamp(
                    currentInfluence + (localAlliance * 0.12f) - (localConflict * 0.16f),
                    0f,
                    1f);

                var conquestGap = Lerp(0.09f, 0.04f, aggressionNorm);
                if (bestForeignPolity >= 0 && bestForeignInfluence > nextInfluence + conquestGap)
                {
                    if (localConflict > localAlliance * 0.72f)
                    {
                        polityIdMap[cell] = bestForeignPolity;
                        nextInfluence = Math.Clamp(bestForeignInfluence * 0.90f, 0f, 1f);
                        changedEvents++;
                    }
                }
                else if (nextInfluence < 0.16f && borderPressure > 0.64f && localConflict > 0.18f)
                {
                    polityIdMap[cell] = -1;
                    nextInfluence *= 0.72f;
                    changedEvents++;
                }

                influence[cell] = nextInfluence;
            }
        }

        conflictHeatPercent = borderSampleCount > 0
            ? Math.Clamp((float)(100d * conflictAccum / borderSampleCount), 0f, 100f)
            : 0f;
        allianceCohesionPercent = borderSampleCount > 0
            ? Math.Clamp((float)(100d * allianceAccum / borderSampleCount), 0f, 100f)
            : 0f;
        borderVolatilityPercent = borderSampleCount > 0
            ? Math.Clamp(100f * changedEvents / borderSampleCount, 0f, 100f)
            : 0f;
    }

    // ────────────────────────────────────────────────────────────────
    // 政体种子
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 选政体发源地。
    ///
    /// 栅格版的间距控制用像素距离（<c>spacing = (width + height) * 0.5 / ...</c>）；
    /// 地块版改用 <c>grid.WrappedDistance</c>，并且**间距阈值按平均直径归一**，
    /// 否则同一份参数在不同地块密度下会选出数量差一个数量级的政体。
    /// </summary>
    private static List<PolitySeed> BuildSeeds(
        SimGrid grid,
        bool[] landMask,
        int seed,
        float seaLevel,
        float diversityNorm,
        float aggressionNorm)
    {
        var fields = grid.Fields;

        // 候选：有城市的地块（按重要性排序）→ 无城市时退化为"文明潜力高的地块"。
        var candidates = new List<(int Cell, float Importance)>();
        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (!landMask[cell] || fields.CityId[cell] < 0)
            {
                continue;
            }

            // 城市下标越小 = 评分越高（城市列表已按评分降序），所以用倒序权重。
            var orderBonus = 1f / (1f + (fields.CityId[cell] * 0.08f));
            var importance = (orderBonus * 0.70f) + (Clamp01(fields.CivilizationPotential[cell]) * 0.30f);
            candidates.Add((cell, importance));
        }

        candidates.Sort((left, right) => right.Importance.CompareTo(left.Importance));

        var seeds = new List<PolitySeed>();
        var chosen = new List<int>();
        var seen = new HashSet<int>();
        var nextId = 0;

        // 间距阈值写作"多少圈地块"再乘 spacing：与射程同一套量纲，
        // 保证"政体之间隔多远"在任何地块密度下都是同一个地理尺度。
        var cellSpacing = Math.Max(grid.SpacingX, 1e-6d);
        var mapRings = Math.Max(grid.Width, grid.Height) / cellSpacing;
        var cityBudget = Math.Clamp(
            4 + (int)MathF.Round((MathF.Sqrt(Math.Max(candidates.Count, 1)) * 1.4f)
                + (diversityNorm * 6f) - (aggressionNorm * 1.2f)),
            4,
            22);
        cityBudget = Math.Min(cityBudget, candidates.Count);

        if (cityBudget > 0)
        {
            var spacingRings = Math.Max(3f, (float)mapRings * 0.5f / MathF.Max(2.2f * MathF.Sqrt(cityBudget), 1f));
            var minSpacingRings = Math.Max(1.5f, spacingRings * 0.45f);

            while (seeds.Count < cityBudget && spacingRings >= minSpacingRings)
            {
                // 每轮都把"圈数"换算成像素距离——spacingRings 会在轮末收缩，不能只算一次。
                var spacing = spacingRings * (float)cellSpacing;
                var addedThisRound = false;
                for (var i = 0; i < candidates.Count && seeds.Count < cityBudget; i++)
                {
                    var cell = candidates[i].Cell;
                    if (!seen.Add(cell))
                    {
                        continue;
                    }

                    if (!IsFarEnough(grid, cell, chosen, spacing))
                    {
                        seen.Remove(cell);
                        continue;
                    }

                    seeds.Add(CreateSeed(grid, cell, nextId++, seed, seaLevel, aggressionNorm, true));
                    chosen.Add(cell);
                    addedThisRound = true;
                }

                spacingRings *= addedThisRound ? 0.90f : 0.76f;
            }
        }

        // 补足：均匀撒点，只在"文明潜力够高"的陆地上落子。
        // 步长按地块网格的列/行数推，而不是按像素——否则在粗地块图上一个 stride 会跨过整张地图。
        var fallbackBudget = Math.Clamp(6 + (int)MathF.Round(diversityNorm * 12f), 6, 20);
        var stride = Math.Max(1, (int)MathF.Round(MathF.Sqrt(grid.Count / (float)Math.Max(fallbackBudget, 1))));
        for (var index = 0; index < grid.Count && seeds.Count < fallbackBudget; index += stride)
        {
            if (!landMask[index])
            {
                continue;
            }

            // ── 门槛从"固定 0.62"改为"随海拔放宽" ──
            //
            // 旧条件是 CivilizationPotential >= 0.62，而高海拔地块的潜力天然偏低
            // （地形不稳 → settlementSuitability 低），实测高原仅 0.21~0.30。
            // 于是高地上**永远不会播下种子**——即便评分公式允许，也没有政体从那里出发。
            // 内陆因此只能靠远处政体"伸过来"，一旦射程衰减就把整块高原留白。
            //
            // 现在把门槛做成海拔的函数：低地仍要求优质（0.62 不变，保持原有的
            // "良田优先"取向），高海拔按比例放宽到 0.30。
            // 这不是"为了填满而填满"：山地上确实存在以牧业/矿业为生的政体，
            // 只是密度低——放宽门槛正是在表达这个意思。
            var altitudeT = Math.Clamp((fields.Height[index] - seaLevel) / 0.45f, 0f, 1f);
            var potentialGate = 0.62f - (altitudeT * 0.32f);
            if (fields.CivilizationPotential[index] < potentialGate)
            {
                continue;
            }

            if (!seen.Add(index))
            {
                continue;
            }

            seeds.Add(CreateSeed(grid, index, nextId++, seed, seaLevel, aggressionNorm, false));
            chosen.Add(index);
        }

        if (seeds.Count > 0)
        {
            return seeds;
        }

        // 兜底：整张图找不到合格落点时，在最大的陆地块上立一个政体。
        var fallbackCell = FindLargestLandCell(grid, landMask);
        if (fallbackCell >= 0)
        {
            seeds.Add(CreateSeed(grid, fallbackCell, 0, seed, seaLevel, aggressionNorm, false));
        }

        return seeds;
    }

    /// <summary>间距检查：到所有已选站点都要够远（经度环绕的测地距离）。</summary>
    private static bool IsFarEnough(SimGrid grid, int cell, List<int> chosen, float spacing)
    {
        for (var i = 0; i < chosen.Count; i++)
        {
            if (grid.WrappedDistance(grid.SiteX[cell], grid.SiteY[cell], grid.SiteX[chosen[i]], grid.SiteY[chosen[i]]) < spacing)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>造一个政体种子：原型由当地地形决定，强度由城市重要性与噪声共同决定。</summary>
    private static PolitySeed CreateSeed(
        SimGrid grid,
        int cell,
        int id,
        int seed,
        float seaLevel,
        float aggressionNorm,
        bool isCity)
    {
        var fields = grid.Fields;
        var archetype = DeterminePolityArchetype(grid, cell, seaLevel);

        var expansionism = archetype switch
        {
            PolityArchetype.Naval => 1.18f,
            PolityArchetype.River => 1.10f,
            // 高原政体原本是 0.92（全场最低）。但山地政体在地理上恰恰是"以点控面"
            // 的类型——高原哨所辐射的草场极广，扩张欲不该是最弱的。
            // 旧值叠加"高原潜力低 + 高原地形成本高"，三重压制下高原必然空置。
            PolityArchetype.Highland => 1.06f,
            PolityArchetype.Nomadic => 1.12f,
            _ => 1.00f,
        };

        float strength;
        if (isCity)
        {
            // 城市地块：城市下标越小（评分越高）越强。
            var cityRank = Math.Max(fields.CityId[cell], 0);
            strength = 1.24f - (cityRank * 0.02f);
            strength += Clamp01(fields.CivilizationPotential[cell]) * 0.24f;
        }
        else
        {
            strength = 0.86f + (HashNoise01(seed, cell) * 0.36f) - (aggressionNorm * 0.08f);
        }

        strength = Math.Clamp(strength, 0.72f, 1.58f);
        return new PolitySeed(id, cell, strength, expansionism, fields.Biome[cell], archetype);
    }

    /// <summary>找最大的陆地块；没有任何陆地时返回 -1。</summary>
    private static int FindLargestLandCell(SimGrid grid, bool[] landMask)
    {
        var best = -1;
        var bestArea = 0d;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (!landMask[cell] || grid.Area[cell] <= bestArea)
            {
                continue;
            }

            bestArea = grid.Area[cell];
            best = cell;
        }

        return best;
    }

    /// <summary>
    /// 判定政体原型。全用真实邻接判断"临河/临海"，与栅格版的 8 邻域对应。
    /// </summary>
    private static PolityArchetype DeterminePolityArchetype(SimGrid grid, int cell, float seaLevel)
    {
        var fields = grid.Fields;
        var adjacentRiver = IsAdjacentToRiver(grid, cell);

        if (fields.Height[cell] > seaLevel + 0.36f && !adjacentRiver)
        {
            return PolityArchetype.Highland;
        }

        if (fields.River[cell] > 0.24f || adjacentRiver)
        {
            return PolityArchetype.River;
        }

        if (IsAdjacentToOceanCell(grid, cell, seaLevel))
        {
            return PolityArchetype.Naval;
        }

        if (IsAridBiome(fields.Biome[cell]))
        {
            return PolityArchetype.Nomadic;
        }

        return PolityArchetype.Generic;
    }

    /// <summary>是否有流量够大的河流邻居。</summary>
    private static bool IsAdjacentToRiver(SimGrid grid, int cell)
    {
        var river = grid.Fields.River;
        var start = grid.CellNeighborStart[cell];
        var end = grid.CellNeighborStart[cell + 1];
        for (var k = start; k < end; k++)
        {
            if (river[grid.CellNeighbors[k]] > RiverProximityThreshold)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>是否有海洋邻居（按高程与群系双重判定，口径与陆地掩码相反）。</summary>
    private static bool IsAdjacentToOceanCell(SimGrid grid, int cell, float seaLevel)
    {
        var start = grid.CellNeighborStart[cell];
        var end = grid.CellNeighborStart[cell + 1];
        for (var k = start; k < end; k++)
        {
            var neighbor = grid.CellNeighbors[k];
            if (grid.Fields.Height[neighbor] <= seaLevel || grid.Fields.Biome[neighbor] <= ShallowOceanBiome)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>扩张代价：地形与政体原型匹配得越好，代价越低。</summary>
    private static float ComputeExpansionPenalty(
        PolityArchetype archetype,
        byte nativeBiome,
        byte localBiome,
        float terrainPenalty,
        float riverFactor,
        bool localCoastal)
    {
        var biomePenalty = localBiome == nativeBiome ? 0.08f : 0.24f;

        // ── 原型适配：高原政体必须"真的擅长高原" ──
        //
        // 旧式给高原的适配档是 terrainPenalty > 0.60 ? 0.06 : 0.28，
        // 而高原政体的种子本身落在高地上（terrainPenalty 必然 > 0.6），
        // 于是它对高原永远拿 0.06，对低地拿 0.28——**反向激励**：
        // 高原政体更愿意往低地扩张，高原本土反而无人认领。
        // 这与 Naval（沿海 0.10 / 内陆 0.34）、River（临河 0.06 / 不临河 0.20）一样，
        // 是"原型擅长域"的正当表达，问题在于高原政体的擅长域**只有**高海拔一点，
        // 而高海拔地块的潜力本身还低——两头相乘就没了。
        // 现在改为随海拔连续变化：高原政体在越高处越轻松（下限 0.04），
        // 且在中等海拔也不受重罚，避免"高不成低不就"的死区。
        var terrainCost = archetype switch
        {
            PolityArchetype.Naval => localCoastal ? 0.10f : 0.34f,
            PolityArchetype.River => riverFactor > 0.25f ? 0.06f : 0.20f,
            PolityArchetype.Highland => 0.04f + ((1f - terrainPenalty) * 0.16f),
            PolityArchetype.Nomadic => IsAridBiome(localBiome) ? 0.10f : 0.30f,
            _ => 0.18f,
        };

        // 海拔项从 0.24 降到 0.14：它只是"通用不适居"的轻度修正，
        // 真正的海拔惩罚已由 terrainAdaptation 承担一次（见评分处的注释）。
        return Math.Clamp(terrainCost + biomePenalty + (terrainPenalty * 0.14f), 0f, 1.2f);
    }

    /// <summary>干旱群系：游牧政体适应，其他政体不适。</summary>
    private static bool IsAridBiome(byte biome)
        => biome is 7 or 10 or 15; // Steppe / TemperateDesert / TropicalDesert

    // ────────────────────────────────────────────────────────────────
    // 城市枢纽与贸易
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 城市枢纽分级：影响力越高、纪元越晚、多样性越高，等级越高。
    /// 分级统计仍是"数城市个数"，所以这里生成的枢纽表与列表长度无关地可复现。
    /// </summary>
    private static List<TradeHub> BuildCityHubs(
        SimGrid grid,
        bool[] landMask,
        float epochFactor,
        float diversityNorm,
        float aggressionNorm,
        out int hamletCount,
        out int townCount,
        out int cityStateCount)
    {
        var fields = grid.Fields;
        var hubs = new List<TradeHub>();
        hamletCount = 0;
        townCount = 0;
        cityStateCount = 0;

        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (!landMask[cell] || fields.CityId[cell] < 0)
            {
                continue;
            }

            var localInfluence = Clamp01(fields.Influence[cell]);
            var polityId = fields.PolityId[cell];

            // 规模项：栅格版用城市的 Population 档位（0.26 / 0.50 / 0.74）拉开差距，
            // 地块核心层拿不到 CityInfo.Population（那在 Godot 侧），所以改用
            // **当地条件**合成一个同量纲的规模项：水土越好、影响力越高，聚落越大。
            //
            // ⚠ 这里踩过一次坑：最初用"城市排名"做规模项（1/(1+rank*0.04)*0.30），
            // 排名靠前的城市该项恒 ≈0.30，比栅格版的 Small 档（0.26）还高，
            // 于是**所有城市都被抬到"镇"以上，一个村落都没有**（实测 0/11/1）。
            // 改用当地条件后分布才拉开——判据本身没写错，是这里的取值区间错了。
            var localVitality = (Clamp01(fields.CivilizationPotential[cell]) * 0.34f)
                + (Clamp01(fields.Moisture[cell]) * 0.16f)
                + ((1f - Clamp01(fields.River[cell])) * 0.10f);

            // 可变部分：本地影响力 + 当地水土条件。这一项决定聚落等级，
            // 量程约 0.3~1.3（实测量级），阈值按它标定。
            var vitalityScore = (localInfluence * 0.88f)
                + (Clamp01(fields.CivilizationPotential[cell]) * 0.20f)
                + (localVitality * 0.55f);

            if (polityId < 0)
            {
                // 无归属的孤立聚落，等级下调一档。
                vitalityScore -= 0.12f;
            }

            // 整体成熟度：纪元与多样性只做**平移**，不参与分级——
            // 否则纪元一高，"村落"这个档位会整个消失（见下方阈值处的说明）。
            var maturity = (epochFactor * 0.58f) + (diversityNorm * 0.24f) - (aggressionNorm * 0.20f);

            // tierScore 保留为综合分值，供 strength 使用。
            var tierScore = vitalityScore + maturity;

            // 分级阈值必须与**本版本**的分数分布对齐，不能照抄栅格版的 1.05 / 1.58。
            //
            // 原因：栅格版的分数里有一项 populationBase（Small=0.26 起），
            // 而地块核心层拿不到城市规模档位，用当地条件合成的规模项**下界更高**；
            // 更要命的是 epochFactor*0.58 在纪元 ≥400 时已经饱和到 ≈0.58，
            // 于是"固定项"单独就有 0.66，任何城市都越过 1.05 ——实测 0 村落 / 12 镇。
            //
            // 正确做法是让阈值描述**分布**而不是绝对水平：
            // 用"影响力 + 当地规模"这两项可变部分（下称 vitalityScore，量程约 0.3~1.3）
            // 来分级，纪元与多样性作为整体成熟度只做平移，不参与分级。
            var tier = vitalityScore switch
            {
                < 0.62f => (byte)0,
                < 0.96f => (byte)1,
                _ => (byte)2,
            };

            switch (tier)
            {
                case 0:
                    hamletCount++;
                    break;
                case 1:
                    townCount++;
                    break;
                default:
                    cityStateCount++;
                    break;
            }

            var strength = Math.Clamp(0.72f + (tier * 0.30f) + (localInfluence * 0.42f) + (localVitality * 0.18f), 0.65f, 1.75f);
            hubs.Add(new TradeHub(cell, tier, polityId, strength));
        }

        hubs.Sort((left, right) => right.Strength.CompareTo(left.Strength));
        return hubs;
    }

    /// <summary>
    /// 贸易走廊。与栅格版的根本区别：**沿真实邻接做最短路**，而不是直线光栅化。
    ///
    /// 直线光栅化在两个枢纽之间插值像素，遇到海洋/山脉就跳过——
    /// 结果是走廊会断成几截（栅格版靠"跳过"掩盖，视觉上看起来是虚线）。
    /// 沿邻接的最短路则天然连续，且会主动绕过不可通行的地块。
    ///
    /// 连通性判据是精确的结构不变量：走廊地块构成一条从起点到终点的连续路径，
    /// 因此"每个走廊地块的下一步可达"可以在自检里逐条断言。
    /// </summary>
    private static float BuildTradeRoutes(
        SimGrid grid,
        bool[] landMask,
        float seaLevel,
        float epochFactor,
        float aggressionNorm,
        List<TradeHub> hubs,
        List<TradeRoutePath>? routePaths = null)
    {
        var fields = grid.Fields;
        var tradeRouteMask = fields.TradeRouteMask;
        var tradeFlow = fields.TradeFlow;
        Array.Clear(tradeRouteMask);
        Array.Clear(tradeFlow);

        if (hubs.Count < 2)
        {
            return 0f;
        }

        var maxLinksPerHub = Math.Clamp(
            (int)MathF.Round(Lerp(1f, 3f, epochFactor) - (aggressionNorm * 0.6f)),
            1,
            3);

        // 射程同样以"地块圈数"为单位，理由见 Simulate 方法头关于量纲的说明。
        var spacing = Math.Max(grid.SpacingX, 1e-6d);
        var baseRange = (float)(Lerp(6f, 22f, epochFactor) * spacing);

        var linkedPairs = new HashSet<long>();
        var connectedHub = new bool[hubs.Count];

        for (var i = 0; i < hubs.Count; i++)
        {
            var fromHub = hubs[i];
            var links = 0;

            for (var attempt = 0; attempt < 8 && links < maxLinksPerHub; attempt++)
            {
                var bestIndex = -1;
                var bestDistance = float.MaxValue;

                for (var j = 0; j < hubs.Count; j++)
                {
                    if (j == i)
                    {
                        continue;
                    }

                    var toHub = hubs[j];
                    var samePolity = fromHub.PolityId >= 0 && fromHub.PolityId == toHub.PolityId;
                    var diplomaticBridge = fromHub.Tier >= 2 || toHub.Tier >= 2 || aggressionNorm < 0.55f;
                    if (!samePolity && !diplomaticBridge)
                    {
                        continue;
                    }

                    var pairKey = BuildPairKey(i, j);
                    if (linkedPairs.Contains(pairKey))
                    {
                        continue;
                    }

                    var distance = (float)grid.WrappedDistance(
                        grid.SiteX[fromHub.Cell], grid.SiteY[fromHub.Cell],
                        grid.SiteX[toHub.Cell], grid.SiteY[toHub.Cell]);

                    var maxDistance = baseRange * Lerp(0.92f, 1.35f, (fromHub.Tier + toHub.Tier) * 0.25f);
                    if (distance > maxDistance || distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    bestIndex = j;
                }

                if (bestIndex < 0)
                {
                    break;
                }

                linkedPairs.Add(BuildPairKey(i, bestIndex));
                var drewAny = CarveTradeRoute(
                    grid,
                    landMask,
                    seaLevel,
                    hubs[i],
                    hubs[bestIndex],
                    routePaths);

                if (drewAny)
                {
                    connectedHub[i] = true;
                    connectedHub[bestIndex] = true;
                }

                links++;
            }
        }

        var connectedCount = 0;
        for (var i = 0; i < connectedHub.Length; i++)
        {
            if (connectedHub[i])
            {
                connectedCount++;
            }
        }

        return 100f * connectedCount / hubs.Count;
    }

    /// <summary>
    /// 在两个枢纽之间沿地块邻接找一条最省力的通路并标记为贸易走廊。
    ///
    /// 代价函数：基础 1 + 地形起伏惩罚 + 海陆惩罚。
    /// 用 <c>PriorityQueue</c> 做 Dijkstra；地块图规模（几千到几万）远小于栅格（百万级像素），
    /// 这里不需要 A* 之类的加速。
    /// </summary>
    /// <returns>是否真的画出了走廊（两端不连通时为 false）。</returns>
    private static bool CarveTradeRoute(
        SimGrid grid,
        bool[] landMask,
        float seaLevel,
        TradeHub fromHub,
        TradeHub toHub,
        List<TradeRoutePath>? routePaths = null)
    {
        var start = fromHub.Cell;
        var goal = toHub.Cell;
        if (start == goal || !landMask[start] || !landMask[goal])
        {
            return false;
        }

        var count = grid.Count;
        var height = grid.Fields.Height;

        var distance = new float[count];
        var previous = new int[count];
        var visited = new bool[count];
        Array.Fill(distance, float.MaxValue);
        Array.Fill(previous, -1);

        distance[start] = 0f;
        var queue = new PriorityQueue<int, float>();
        queue.Enqueue(start, 0f);

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (visited[cell])
            {
                continue;
            }

            visited[cell] = true;
            if (cell == goal)
            {
                break;
            }

            var startSlot = grid.CellNeighborStart[cell];
            var endSlot = grid.CellNeighborStart[cell + 1];
            for (var k = startSlot; k < endSlot; k++)
            {
                var neighbor = grid.CellNeighbors[k];
                if (visited[neighbor])
                {
                    continue;
                }

                var stepCost = ComputeRouteStepCost(
                    grid,
                    landMask,
                    seaLevel,
                    height,
                    cell,
                    neighbor);

                var candidate = distance[cell] + stepCost;

                // 允许穿过水域，但代价很高——这样"两片大陆之间"仍能找到通路，
                // 只是会优先贴着陆地走。栅格版是直接跳过海洋像素，走廊因此断裂。
                if (candidate >= distance[neighbor])
                {
                    continue;
                }

                distance[neighbor] = candidate;
                previous[neighbor] = cell;
                queue.Enqueue(neighbor, candidate);
            }
        }

        if (previous[goal] < 0)
        {
            return false;
        }

        // 回溯路径并写入掩码与流量。
        var corridorLength = 0;
        for (var cursor = goal; cursor >= 0; cursor = previous[cursor])
        {
            corridorLength++;
            if (previous[cursor] < 0)
            {
                break;
            }
        }

        if (corridorLength <= 0)
        {
            return false;
        }

        var step = 0;
        var tradeRouteMask = grid.Fields.TradeRouteMask;
        var tradeFlow = grid.Fields.TradeFlow;
        var pathCells = new int[corridorLength];
        var pathIdx = corridorLength - 1;
        var maxFlow = 0f;

        for (var cursor = goal; cursor >= 0; cursor = previous[cursor])
        {
            pathCells[pathIdx--] = cursor;

            // 沿路径的归一化位置：用于让走廊中段比两端更强（枢纽之间人流最密）。
            var t = corridorLength > 1 ? step / (float)(corridorLength - 1) : 0.5f;
            var corridorStrength = Math.Clamp(
                0.35f + (fromHub.Strength * 0.25f) + (toHub.Strength * 0.25f) - (MathF.Abs(t - 0.5f) * 0.32f),
                0f,
                1f);

            tradeRouteMask[cursor] = true;
            tradeFlow[cursor] = MathF.Max(tradeFlow[cursor], corridorStrength);
            maxFlow = MathF.Max(maxFlow, corridorStrength);

            step++;
            if (previous[cursor] < 0)
            {
                break;
            }
        }

        if (routePaths != null && pathCells.Length > 0)
        {
            routePaths.Add(new TradeRoutePath
            {
                FromHubCell = start,
                ToHubCell = goal,
                Cells = pathCells,
                Flow = maxFlow
            });
        }

        return true;
    }

    /// <summary>
    /// 单步通路代价。陆地内部便宜，山地略贵，水域很贵（鼓励沿海岸绕行）。
    /// </summary>
    private static float ComputeRouteStepCost(
        SimGrid grid,
        bool[] landMask,
        float seaLevel,
        float[] height,
        int from,
        int to)
    {
        // 代价按"多少个平均地块直径"计：让单步代价与地块密度解耦，
        // 这样同一套参数在粗地块图与细地块图上都能走出形态相近的走廊。
        var meanDiameter = Math.Max(grid.SpacingX, 1e-6d);
        var distance = grid.WrappedDistance(grid.SiteX[from], grid.SiteY[from], grid.SiteX[to], grid.SiteY[to]);
        var baseCost = Math.Max((float)(distance / meanDiameter), 0.1f);

        if (!landMask[to])
        {
            // 过水：代价显著抬高，但不设成无穷——否则岛屿政体之间永远无法通商。
            return baseCost * 6f;
        }

        var relativeHeight = (height[to] - seaLevel) / Math.Max(1f - seaLevel, 0.0001f);
        var reliefCost = Clamp01(relativeHeight) * 1.6f;
        return baseCost * (1f + reliefCost);
    }

    private static long BuildPairKey(int a, int b)
    {
        var min = Math.Min(a, b);
        var max = Math.Max(a, b);
        return ((long)min << 32) | (uint)max;
    }

    // ────────────────────────────────────────────────────────────────
    // 纪元事件日志
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 生成最近若干纪元的文明事件。逻辑与栅格版一致（同为"按分数择一分类"），
    /// 但多了一个口径修正：**政体数量为 0 时不生成事件**——
    /// 栅格版在这种情况下会退化成纯噪声驱动的日志，读起来像"无中生有"。
    /// </summary>
    private static CivilizationEvent[] BuildEpochEventLog(
        int seed,
        int epoch,
        float aggressionNorm,
        float diversityNorm,
        float conflictHeatPercent,
        float allianceCohesionPercent,
        float borderVolatilityPercent,
        int polityCount,
        float connectedHubPercent)
    {
        if (epoch <= 0 || polityCount <= 0)
        {
            return Array.Empty<CivilizationEvent>();
        }

        const int lookback = 6;
        var startEpoch = Math.Max(1, epoch - lookback + 1);
        var events = new List<CivilizationEvent>(lookback);

        for (var currentEpoch = startEpoch; currentEpoch <= epoch; currentEpoch++)
        {
            var phase = (currentEpoch - startEpoch) / (float)Math.Max(1, epoch - startEpoch);
            var noise = HashNoise01(
                seed ^ unchecked((int)0x27d4eb2d),
                currentEpoch + Mathf_RoundToInt(connectedHubPercent),
                polityCount);

            var warScore = (aggressionNorm * 0.58f)
                + (conflictHeatPercent / 170f)
                + (borderVolatilityPercent / 230f)
                + (noise * 0.18f);
            var allianceScore = ((1f - aggressionNorm) * (0.50f + (diversityNorm * 0.24f)))
                + (allianceCohesionPercent / 180f)
                + ((1f - noise) * 0.12f);
            var tradeScore = (connectedHubPercent / 150f)
                + ((1f - aggressionNorm) * 0.20f)
                + (phase * 0.16f)
                + (noise * 0.06f);

            string category;
            string summary;
            float dominantScore;

            if (warScore >= allianceScore && warScore >= tradeScore)
            {
                category = "战争";
                dominantScore = warScore;
                summary = warScore > 0.95f
                    ? "边境冲突升级，多处要塞易手。"
                    : "边境摩擦加剧，前线发生局部推进。";
            }
            else if (allianceScore >= tradeScore)
            {
                category = "联盟";
                dominantScore = allianceScore;
                summary = allianceScore > 0.90f
                    ? "多政体缔结互保公约，边境趋稳。"
                    : "区域协约扩张，防务协同增强。";
            }
            else
            {
                category = "贸易";
                dominantScore = tradeScore;
                summary = tradeScore > 0.92f
                    ? "贸易走廊扩容，跨域物资流显著增长。"
                    : "商路维持畅通，城镇交换网络稳步扩张。";
            }

            var impact = Math.Clamp(
                (int)MathF.Round(1f + (dominantScore * 3.8f) + ((noise - 0.5f) * 1.2f)),
                1,
                5);

            events.Add(new CivilizationEvent
            {
                Epoch = currentEpoch,
                Category = category,
                Summary = summary,
                ImpactLevel = impact,
            });
        }

        return events.ToArray();
    }

    // ────────────────────────────────────────────────────────────────
    // 小工具
    // ────────────────────────────────────────────────────────────────

    /// <summary>数一数掩码里有多少个 true。</summary>
    private static int CountTrue(bool[] mask)
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
    /// 按地块编号取 0~1 的哈希噪声。
    /// 栅格版是 <c>HashNoise01(seed, x, y)</c>；地块版只需要"每个单元一个稳定的随机数"，
    /// 因此降成单参数——语义一致（都是逐单元的斑块扰动）。
    /// </summary>
    private static float HashNoise01(int seed, int cell)
    {
        var hash = (uint)seed;
        hash ^= (uint)cell * 1597334677u;
        hash = (hash ^ (hash >> 16)) * 2246822519u;
        hash ^= hash >> 13;
        return (hash & 0x00FFFFFFu) / 16777215f;
    }

    /// <summary>两参数的噪声：用于"按纪元 + 政体数"取事件的随机性。</summary>
    private static float HashNoise01(int seed, int a, int b)
    {
        var hash = (uint)seed;
        hash ^= (uint)a * 374761393u;
        hash ^= (uint)b * 668265263u;
        hash = (hash ^ (hash >> 13)) * 1274126177u;
        hash ^= hash >> 16;
        return (hash & 0x00FFFFFFu) / 16777215f;
    }

    /// <summary>四舍五入到 int；避免依赖 Godot 的 Mathf。</summary>
    private static int Mathf_RoundToInt(float value) => (int)MathF.Round(value);

    private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

    private static float Lerp(float from, float to, float t) => from + ((to - from) * Math.Clamp(t, 0f, 1f));
}
