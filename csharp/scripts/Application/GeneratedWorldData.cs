using PolygonCellMap = PlanetGeneration.Core.Geometry.PolygonCellMap;
using Godot;
using System;
using System.Collections.Generic;
using PlanetGeneration.WorldGen;
using PlanetGeneration.WorldGen.Polygon;

namespace PlanetGeneration.Application;

internal sealed class LayerRenderCacheEntry
{
	public int Signature { get; init; }
	public Image Image { get; init; } = null!;
	public Texture2D Texture { get; init; } = null!;
	public long LastAccessTick { get; set; }
}

/// <summary>旧栅格兼容路径的运行时数据；不是 WorldSnapshot 的替代事实源。</summary>
internal sealed class GeneratedWorldData
{
    /// <summary>非空时，本对象只是此快照的旧界面投影；禁止重新运行栅格算法。</summary>
    public PlanetGeneration.Core.Domain.WorldSnapshot? Snapshot { get; internal set; }

	public PlateResult PlateResult { get; init; } = null!;
	public float[,] Elevation { get; init; } = null!;
	public float[,] Temperature { get; init; } = null!;
	public float[,] Moisture { get; init; } = null!;
	public Vector2[,] Wind { get; init; } = null!;
	public float[,] River { get; init; } = null!;
	public BiomeType[,] Biome { get; init; } = null!;
	public RockType[,] Rock { get; init; } = null!;
	public OreType[,] Ore { get; init; } = null!;
	public OreType[,]? IndustrialOre { get; init; }
	public OreType[,]? SupernaturalOre { get; init; }
	public OreType[,]? CardOre { get; init; }
	public byte[,]? Leyline { get; init; }
	public List<CityInfo> Cities { get; init; } = null!;
	public WorldStats Stats { get; init; } = null!;
	public WorldTuning Tuning { get; init; } = null!;
	public EcologySimulationResult? EcologySimulation { get; set; }
	public int EcologySignature { get; set; } = int.MinValue;
	public CivilizationSimulationResult? CivilizationSimulation { get; set; }
	public int CivilizationSignature { get; set; } = int.MinValue;
	public Dictionary<MapLayer, LayerRenderCacheEntry> LayerRenderCache { get; } = new();

	/// <summary>
	/// 多边形地块层。为 null 表示尚未构建（模式为 Raster，或刚从旧缓存恢复）。
	/// 它只是 (宽, 高, 种子, 目标地块数) 的纯函数，所以随时可以按需重建。
	/// </summary>
	public PolygonGrid? PolygonGrid { get; set; }

	/// <summary>像素归属图：栅格与地块之间的唯一接缝。与 <see cref="PolygonGrid"/> 同生同灭。</summary>
	public PolygonCellMap? PolygonCellMap { get; set; }

	/// <summary>构建多边形层时实际使用的目标地块数，缓存恢复时据此复现同一张地块图。</summary>
	public int PolygonCellsDesired { get; set; }

	/// <summary>
	/// 城市 → 地块 的归属（下标为城市在 <see cref="Cities"/> 中的位置，值为地块编号）。
	/// 地块层未构建时为空数组。反向映射是 <c>Core.Domain.CellFields.CityId</c>。
	/// </summary>
	public int[] CityCell { get; set; } = Array.Empty<int>();

	/// <summary>地块版生态模拟的聚合结果；为 null 表示尚未计算。</summary>
	public PlanetGeneration.Core.Domain.EcologyResult? PolygonEcology { get; set; }

	/// <summary>地块版生态模拟的参数签名；与当前设置不一致时重算。</summary>
	public int PolygonEcologySignature { get; set; } = int.MinValue;

	/// <summary>地块版文明模拟的聚合结果；为 null 表示尚未计算。</summary>
	public PlanetGeneration.Core.Domain.CivilizationResult? PolygonCivilization { get; set; }

	/// <summary>
	/// 地块版文明模拟的参数签名；与当前设置不一致时重算。
	/// 注意它必须**覆盖生态参数**（物种多样性/魔法密度）——因为文明模拟吃生态模拟的产出，
	/// 只比对文明自己的参数会让"只改多样性"时文明层不重算。
	/// </summary>
	public int PolygonCivilizationSignature { get; set; } = int.MinValue;

	/// <summary>
	/// 使依赖纪元、生态和文明参数的运行时结果全部失效。
	/// 地块版文明依赖地块版生态，因此两者必须和栅格模拟一起清理。
	/// </summary>
	public void InvalidateSimulationCaches()
	{
		EcologySimulation = null;
		EcologySignature = int.MinValue;
		CivilizationSimulation = null;
		CivilizationSignature = int.MinValue;
		PolygonEcology = null;
		PolygonEcologySignature = int.MinValue;
		PolygonCivilization = null;
		PolygonCivilizationSignature = int.MinValue;

		LayerRenderCache.Remove(MapLayer.Ecology);
		LayerRenderCache.Remove(MapLayer.Civilization);
		LayerRenderCache.Remove(MapLayer.TradeRoutes);
	}
}
