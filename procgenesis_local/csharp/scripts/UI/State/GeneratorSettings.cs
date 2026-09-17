namespace PlanetGeneration.UI.State;

/// <summary>
/// Parameters consumed by world generation. Enum-like values are stored as IDs so this
/// DTO does not depend on Main's private enums or Godot UI controls.
/// </summary>
public sealed class GeneratorSettings
{
	public int MapWidth { get; set; } = 256;
	public int MapHeight { get; set; } = 128;
	public int Seed { get; set; }
	public int PlateCount { get; set; } = 20;
	public int WindCellCount { get; set; } = 10;
	public float SeaLevel { get; set; } = 0.5f;
	public float HeatFactor { get; set; } = 0.5f;
	public int MoistureIterations { get; set; } = 8;
	public int ErosionIterations { get; set; } = 5;
	public float RiverDensity { get; set; } = 1.0f;
	public float WindArrowDensity { get; set; } = 1.0f;
	public float BasinSensitivity { get; set; } = 1.0f;
	public bool EnableRivers { get; set; } = true;

	public float InteriorRelief { get; set; } = 1.0f;
	public float OrogenyStrength { get; set; } = 1.0f;
	public float SubductionArcRatio { get; set; } = 0.72f;
	public int ContinentalAge { get; set; } = 58;
	public int TerrainMorphologyId { get; set; }
	public int ContinentCount { get; set; } = 3;
	public int MountainPresetId { get; set; }
	public int ElevationStyleId { get; set; }

	public int MagicDensity { get; set; } = 75;
	public int CivilAggression { get; set; } = 42;
	public int SpeciesDiversity { get; set; } = 68;

	/// <summary>地块系统模式，取值对应 PolygonTileMode（0=栅格 1=混合 2=单元格 3=单元格+描边）。</summary>
	public int PolygonTileModeId { get; set; } = 2;

	/// <summary>目标地块数；0 表示按地图尺寸自动取值。</summary>
	public int CellsDesired { get; set; }
}
