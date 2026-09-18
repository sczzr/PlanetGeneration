using Godot;
using PlanetGeneration.WorldGen;
using PlanetGeneration.WorldGen.Polygon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using IOPath = System.IO.Path;
using IOFile = System.IO.File;
using IODirectory = System.IO.Directory;
using IOFileInfo = System.IO.FileInfo;
using CryptoSha256 = System.Security.Cryptography.SHA256;

namespace PlanetGeneration;

public partial class Main : Control
{
	[Export] public int MapWidth { get; set; } = 2048;
	[Export] public int MapHeight { get; set; } = 1024;
	[Export] public int Seed { get; set; } = 0;
	[Export] public int PlateCount { get; set; } = 20;
	[Export] public int WindCellCount { get; set; } = 10;
	[Export(PropertyHint.Range, "0,1,0.01")] public float SeaLevel { get; set; } = 0.5f;
	[Export(PropertyHint.Range, "0.01,1,0.01")] public float HeatFactor { get; set; } = 0.5f;
	[Export(PropertyHint.Range, "0.2,2.5,0.01")] public float MoistureFactor { get; set; } = 1.0f;
	[Export(PropertyHint.Range, "1,20,1")] public int MoistureIterations { get; set; } = 8;
	[Export(PropertyHint.Range, "0,20,1")] public int ErosionIterations { get; set; } = 5;
	[Export(PropertyHint.Range, "0.4,2.5,0.01")] public float RiverDensity { get; set; } = 1.0f;
	[Export(PropertyHint.Range, "0.5,2.5,0.01")] public float WindArrowDensity { get; set; } = 1.0f;
	[Export(PropertyHint.Range, "0.5,2.0,0.01")] public float BasinSensitivity { get; set; } = 1.0f;
	[Export] public bool EnableRivers { get; set; } = true;

	private TextureRect _mapTexture = null!;
	private Button _generateButton = null!;
	private Button _randomButton = null!;
	private SpinBox _seedSpin = null!;
	private HSlider _seaLevelSlider = null!;
	private HSlider _heatSlider = null!;
	private HSlider _moistureSlider = null!;
	private HSlider _erosionSlider = null!;
	private HSlider _riverDensitySlider = null!;
	private HSlider _windArrowDensitySlider = null!;
	private HSlider _basinSensitivitySlider = null!;
	private HSlider _interiorReliefSlider = null!;
	private HSlider _orogenyStrengthSlider = null!;
	private HSlider _subductionArcRatioSlider = null!;
	private HSlider _continentalAgeSlider = null!;
	private HSlider _magicSlider = null!;
	private HSlider _aggressionSlider = null!;
	private HSlider _diversitySlider = null!;
	private HSlider _timelineSlider = null!;
	private HSlider _uiFontScaleSlider = null!;
	private Button _prevEpochButton = null!;
	private Button _nextEpochButton = null!;
	private Label _seaLevelValue = null!;
	private Label _heatValue = null!;
	private Label _moistureValue = null!;
	private Label _erosionValue = null!;
	private Label _riverDensityValue = null!;
	private Label _windArrowDensityValue = null!;
	private Label _basinSensitivityValue = null!;
	private Label _interiorReliefValue = null!;
	private Label _orogenyStrengthValue = null!;
	private Label _subductionArcRatioValue = null!;
	private Label _continentalAgeValue = null!;
	private Label _magicValue = null!;
	private Label _aggressionValue = null!;
	private Label _diversityValue = null!;
	private Label _uiFontScaleValue = null!;
	private Label _epochLabel = null!;
	private Label _epochEventIndexLabel = null!;
	private Label _loreStateLabel = null!;
	private Label _threatLabel = null!;
	private Label _infoLabel = null!;
	private Label _compareStatsLabel = null!;
	private RichTextLabel _cityNamesLabel = null!;
	private RichTextLabel _loreText = null!;
	private Control _legendPanel = null!;
	private Label _legendTitle = null!;
	private TextureRect _legendTexture = null!;
	private Label _legendMinLabel = null!;
	private Label _legendMaxLabel = null!;
	private Control _biomeLegendPanel = null!;
	private RichTextLabel _biomeLegendText = null!;
	private OptionButton _layerOption = null!;
	private OptionButton _mapSizeOption = null!;
	private OptionButton _terrainPresetOption = null!;
	private OptionButton _mountainPresetOption = null!;
	private OptionButton _elevationStyleOption = null!;
	private OptionButton _continentCountOption = null!;
	private OptionButton _archiveOption = null!;
	private Button _advancedSettingsButton = null!;
	private Button _resetAdvancedSettingsButton = null!;
	private Button _persistCacheGroupButton = null!;
	private Button _clearCacheButton = null!;
	private BaseButton _riverToggle = null!;
	private BaseButton _compareToggle = null!;
	private Button _exportPngButton = null!;
	private Button _exportJsonButton = null!;
	private Button _themeToggleButton = null!;
	private ProgressBar _generateProgress = null!;
	private Label _progressStatus = null!;
	private Label _cacheStatsLabel = null!;
	private Control _progressOverlay = null!;
	private Tree _layerTree = null!;
	private Control _mapCenter = null!;
	private Control _mapRoot = null!;
	private Control _biomeHoverPanel = null!;
	private Label _biomeHoverText = null!;
	private Control _continentCountWrap = null!;
	private AspectRatioContainer _mapAspect = null!;
	private Control _consolePanel = null!;
	private Button _consoleCollapseTab = null!;
	private Button _consoleSummonTab = null!;
	private bool _consolePanelVisible = true;
	private Control _minimapPanel = null!;
	private TextureRect _minimapTexture = null!;
	private ReferenceRect _minimapViewRect = null!;
	private bool _minimapDragging;
	private Texture? _minimapSourceTexture;
	private Rect2 _lastMinimapViewRect = new(0f, 0f, -1f, -1f);
	private FileDialog _saveFileDialog = null!;
	private ConfirmationDialog _resetAdvancedConfirmDialog = null!;
	private ConfirmationDialog _mapInfoWarningDialog = null!;
	private CheckBox _mapInfoWarningSkipCheck = null!;
	private readonly Dictionary<int, TreeItem> _layerTreeItems = new();
	private readonly Dictionary<Control, int> _baseFontSizeByControl = new();
	private readonly Dictionary<RichTextLabel, int> _baseRichTextFontSizeByControl = new();

	private static readonly Vector2I[] MapSizePresets =
	{
		new Vector2I(1024, 512),
		new Vector2I(2048, 1024),
		new Vector2I(4096, 2048)
	};

	private static readonly int[] CellCountPresets =
	{
		2048,
		5000,
		10000,
		20000,
		32768
	};

	private static readonly int[] NaturalLayerIds =
	{
		(int)MapLayer.Satellite,
		(int)MapLayer.Plates,
		(int)MapLayer.Temperature,
		(int)MapLayer.Rivers,
		(int)MapLayer.Moisture,
		(int)MapLayer.Wind,
		(int)MapLayer.Elevation,
		(int)MapLayer.RockTypes,
		(int)MapLayer.Landform,
		(int)MapLayer.Ecology,
		(int)MapLayer.PolygonGrid
	};

	private static readonly int[] HumanLayerIds =
	{
		(int)MapLayer.Cities,
		(int)MapLayer.Ores,
		(int)MapLayer.Civilization,
		(int)MapLayer.TradeRoutes
	};

	private static readonly int[] ArcaneLayerIds =
	{
		(int)MapLayer.Biomes
	};

	private const int OutputWidth = 4096;
	private const int OutputHeight = 2048;
	private const string FixedUltraOutputText = "固定超清输出 4096x2048：地图信息级别越高，细节越多但速度越慢。";
	private const string HighInfoPointWarningText = "⚠ 地图信息级别较高：生成更慢、细节更多。";
	private const float TemperatureMinCelsius = -45f;
	private const float TemperatureMaxCelsius = 55f;
	private const float EarthHighestPeakMeters = 8848f;
	private const float EarthDeepestTrenchMeters = 10994f;
	private const float ReliefExaggerationMin = 1.5f;
	private const float ReliefExaggerationMax = 3.2f;
	private const float MapZoomMin = 1.0f;
	private const float MapZoomMax = 40.0f;
	private const float MapZoomStep = 0.25f;
	private const float MapZoomFactor = 1.15f;
	private const float DragThreshold = 4.0f;
	private const bool DefaultEnableRivers = true;
	private const float DefaultRiverDensity = 1.0f;
	private const float DefaultWindArrowDensity = 1.0f;
	private const float DefaultBasinSensitivity = 1.0f;
	private const float DefaultInteriorRelief = 1.0f;
	private const float DefaultOrogenyStrength = 1.0f;
	private const float DefaultSubductionArcRatio = 0.72f;
	private const int DefaultContinentalAge = 58;
	private const ElevationStyle DefaultElevationStyle = ElevationStyle.Realistic;
	private const int DefaultMagicDensity = 75;
	private const int DefaultCivilAggression = 42;
	private const int DefaultSpeciesDiversity = 68;
	private const int DefaultEpoch = 450;
	private const int DefaultOracleAutoUnloadIdleSeconds = 120;
	private const int MinOracleAutoUnloadIdleSeconds = 30;
	private const int MaxOracleAutoUnloadIdleSeconds = 1800;
	private const float DefaultUiFontScale = 0.92f;
	private const float MinUiFontScale = 0.60f;
	private const float MaxUiFontScale = 1.40f;
	private const int MaxEpoch = 1000;
	private const float BiomeHoverPanelOffsetX = 18f;
	private const float BiomeHoverPanelOffsetY = 6f;
	private const float BiomeHoverPanelMargin = 8f;
	private const string AdvancedSettingsPath = "user://advanced_settings.cfg";
	private const string AdvancedSettingsSection = "advanced";
	private const string PerformanceSection = "performance";
	private const string PerformanceCpuScoreKey = "cpu_score";
	private const string PerformanceSecondsPerUnitKey = "seconds_per_unit";
	private const double DefaultSecondsPerWorkUnit = 0.55;
	private const double MinSecondsPerWorkUnit = 0.05;
	private const double MaxSecondsPerWorkUnit = 20.0;
	private const double CpuBenchmarkBaselineScore = 110_000_000.0;
	private const double MinCpuPerformanceScore = 0.35;
	private const double MaxCpuPerformanceScore = 3.5;
	private const int LayerRenderCacheCapacity = 20;
	private const int WorldGenerationCacheCapacity = 6;
	private const long WorldGenerationCacheMaxCells = 16_777_216;

	/// <summary>
	/// 世界生成算法版本。多边形地块层引入了新的数据（地块网格 + 像素归属图），
	/// 旧缓存里没有这些数据，所以必须升版本让旧缓存整体失效，而不是读到半成品。
	/// 3 → 4：新增多边形地块层。
	/// </summary>
	private const int WorldGenerationAlgorithmVersion = 4;

	/// <summary>多边形地块数的自动取值上下限：地块边长目标 4 个源像素。</summary>
	private const int AutoCellsTargetSpacingPixels = 4;
	private const int MinAutoCellsDesired = 2048;
	private const int MaxAutoCellsDesired = 32768;
	private const string ArchiveSection = "archive";
	private const string LastArchivePathKey = "last_path";
	private const string CacheDirectoryName = "cache";
	private const string CacheFileExtension = ".json";
	private const string CacheDataDirectoryName = "world_cache";
	private const string ArchiveDataDirectoryName = "world_archives";
	private const string ArchiveFilePrefix = "archive_";
	private const string ArchiveFileExtension = ".pgarchive.json";
	private const long ApproxBytesPerCachedCell = 40;

	private bool _isGenerating;
	private Tween? _generationUiTween;
	private Tween? _progressTween;
	private bool _pendingRegenerate;
	private ulong _generationStartedMsec;
	private double _cpuPerformanceScore = 1.0;
	private bool _performanceSampleReady;
	private bool _hasHistoricalThroughput;
	private double _secondsPerWorkUnit = DefaultSecondsPerWorkUnit;
	private double _currentGenerationWorkUnits;
	private double _predictedTotalSeconds;
	private string _lastArchivePath = string.Empty;
	private ExportKind _pendingExportKind;
	private int _lastConfirmedMapSizeIndex;
	private int _pendingMapSizeIndex = -1;
	private bool _suppressMapSizeSelectionHandler;
	private bool _suppressMountainPresetSelectionHandler;
	private bool _skipMapInfoWarningForSession;
	private bool _suppressArchiveSelectionHandler;
	private bool _archiveOptionSignalBound;
	private long _renderCacheAccessCounter;
	private long _worldCacheAccessCounter;
	private int _preferredLayerId = (int)MapLayer.Satellite;
	private float _terrainOceanicRatio = 0.48f;
	private float _terrainContinentBias = 0.18f;
	private float _interiorRelief = DefaultInteriorRelief;
	private float _orogenyStrength = DefaultOrogenyStrength;
	private float _subductionArcRatio = DefaultSubductionArcRatio;
	private int _continentalAge = DefaultContinentalAge;
	private MountainPresetId _mountainPresetId = MountainPresetId.EarthLike;
	private TerrainMorphology _terrainMorphology = TerrainMorphology.Balanced;
	private int _continentCount = 3;
	private float _currentReliefExaggeration = ReliefExaggerationMin;
	private ElevationStyle _elevationStyle = ElevationStyle.Realistic;
	private int _magicDensity = DefaultMagicDensity;
	private int _civilAggression = DefaultCivilAggression;
	private int _speciesDiversity = DefaultSpeciesDiversity;
	private float _uiFontScale = DefaultUiFontScale;
	private float _mapZoom = 1.0f;
	private bool _isMapDragging;
	private bool _isMapMouseDown;
	private MouseButton _activeDragButton = MouseButton.None;
	private Vector2 _dragStartGlobalPos = Vector2.Zero;
	private Vector2 _lastDragGlobalPos = Vector2.Zero;
	private int _currentEpoch = DefaultEpoch;
	private int _oracleAutoUnloadIdleSeconds = DefaultOracleAutoUnloadIdleSeconds;
	private int _selectedTimelineEventEpoch = -1;

	/// <summary>
	/// 地块系统模式。默认 <see cref="PolygonTileMode.Cells"/>：
	/// 游戏地图以多边形单元格呈现——所有能按地块属性着色的图层都用多边形填充。
	/// 设为 <see cref="PolygonTileMode.Raster"/> 时地块层不构建，等同于改造前的行为。
	/// </summary>
	private PolygonTileMode _polygonTileMode = PolygonTileMode.Cells;

	/// <summary>目标地块数；默认 10,000 块。</summary>
	private int _cellsDesired = 10000;
	private int _targetCellCount = 10000;
	private int _resolutionWidth = 2048;
	private int _resolutionHeight = 1024;

	private PlanetGeneration.UI.LayerPanelController? _layerPanelController;
	private OptionButton? _cellScaleOption;
	private SpinBox? _cellScaleSpin;
	private Label? _cellCountDisplayLabel;

	private PlanetGeneration.Core.Domain.WorldSnapshot? _primarySnapshot;
	private PlanetGeneration.Core.Domain.WorldSnapshot? _compareSnapshot;
	private readonly PlanetGeneration.Rendering.LayerRenderCoordinator _layerCoordinator = new();
	private PlanetGeneration.Rendering.MapCanvas _mapCanvas = null!;

	private enum ExportKind
	{
		None,
		Png,
		Json
	}

	private enum TerrainMorphology
	{
		Balanced,
		Supercontinent,
		Continents,
		Archipelago,
		FracturedIslands,
		ShallowFragments,
		ColdContinent,
		HotWasteland
	}

	private enum MountainPresetId
	{
		EarthLike = 0,
		YoungOrogeny = 1,
		AncientStable = 2,
		EdgeArcs = 3,
		Custom = 99
	}

	private enum LandformType
	{
		DeepOcean,
		ShallowSea,
		CoastalPlain,
		Plain,
		Basin,
		DryBasin,
		Valley,
		RollingHills,
		Upland,
		Plateau,
		Mountain
	}

	private readonly struct TimelineHotspotPoint
	{
		public int X { get; }
		public int Y { get; }
		public float Score { get; }

		public TimelineHotspotPoint(int x, int y, float score)
		{
			X = x;
			Y = y;
			Score = score;
		}
	}

	private readonly PlateGenerator _plateGenerator = new();
	private readonly ElevationGenerator _elevationGenerator = new();
	private readonly TemperatureGenerator _temperatureGenerator = new();
	private readonly MoistureGenerator _moistureGenerator = new();
	private readonly RiverGenerator _riverGenerator = new();
	private readonly BiomeGenerator _biomeGenerator = new();
	private readonly ErosionSimulator _erosionSimulator = new();
	private readonly ResourceGenerator _resourceGenerator = new();
	private readonly CityGenerator _cityGenerator = new();
	private readonly EcologySimulator _ecologySimulator = new();
	private readonly CivilizationSimulator _civilizationSimulator = new();
	private readonly StatsCalculator _statsCalculator = new();
	private readonly WorldRenderer _renderer = new();

	private WorldTuning _tuning = WorldTuning.Legacy();
	private bool _compareMode;
	private readonly Dictionary<string, WorldGenerationCacheEntry> _worldGenerationCache = new();
	private readonly Dictionary<int, string> _archivePathByOptionId = new();

	private GeneratedWorldData? _primaryWorld;
	private GeneratedWorldData? _compareWorld;
	private Image? _lastRenderedImage;
	private Image? _lastCompareImage;

	private sealed class LayerRenderCacheEntry
	{
		public int Signature { get; init; }
		public Image Image { get; init; } = null!;
		public Texture2D Texture { get; init; } = null!;
		public long LastAccessTick { get; set; }
	}

	private sealed class GeneratedWorldData
	{
		public PlateResult PlateResult { get; init; } = null!;
		public float[,] Elevation { get; init; } = null!;
		public float[,] Temperature { get; init; } = null!;
		public float[,] Moisture { get; init; } = null!;
		public Vector2[,] Wind { get; init; } = null!;
		public float[,] River { get; init; } = null!;
		public BiomeType[,] Biome { get; init; } = null!;
		public RockType[,] Rock { get; init; } = null!;
		public OreType[,] Ore { get; init; } = null!;
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
		/// 地块层未构建时为空数组。反向映射是 <c>PolygonFields.CityId</c>。
		/// </summary>
		public int[] CityCell { get; set; } = Array.Empty<int>();

		/// <summary>地块版生态模拟的聚合结果；为 null 表示尚未计算。</summary>
		public PolygonEcologyResult? PolygonEcology { get; set; }

		/// <summary>地块版生态模拟的参数签名；与当前设置不一致时重算。</summary>
		public int PolygonEcologySignature { get; set; } = int.MinValue;

		/// <summary>地块版文明模拟的聚合结果；为 null 表示尚未计算。</summary>
		public PolygonCivilizationResult? PolygonCivilization { get; set; }

		/// <summary>
		/// 地块版文明模拟的参数签名；与当前设置不一致时重算。
		/// 注意它必须**覆盖生态参数**（物种多样性/魔法密度）——因为文明模拟吃生态模拟的产出，
		/// 只比对文明自己的参数会让"只改多样性"时文明层不重算。
		/// </summary>
		public int PolygonCivilizationSignature { get; set; } = int.MinValue;
	}

	private sealed class WorldGenerationCacheEntry
	{
		public string Key { get; init; } = string.Empty;
		public GeneratedWorldData PrimaryWorld { get; init; } = null!;
		public GeneratedWorldData? CompareWorld { get; init; }
		public long EstimatedCells { get; init; }
		public long LastAccessTick { get; set; }
	}

	private sealed class PersistedWorldCacheEntry
	{
		public int Version { get; set; }
		public string CacheKey { get; set; } = string.Empty;
		public int Seed { get; set; }
		public int MapWidth { get; set; }
		public int MapHeight { get; set; }
		public bool CompareMode { get; set; }
		public PersistedWorldData Primary { get; set; } = new();
		public PersistedWorldData? Compare { get; set; }
	}

	private sealed class PersistedWorldData
	{
		public string TuningName { get; set; } = string.Empty;
		public PersistedWorldStats Stats { get; set; } = new();
		public int[,] PlateIds { get; set; } = new int[0, 0];
		public int[,] BoundaryTypes { get; set; } = new int[0, 0];
		public float[,] Elevation { get; set; } = new float[0, 0];
		public float[,] Temperature { get; set; } = new float[0, 0];
		public float[,] Moisture { get; set; } = new float[0, 0];
		public float[,] River { get; set; } = new float[0, 0];
		public Vector2[,] Wind { get; set; } = new Vector2[0, 0];
		public int[,] Biome { get; set; } = new int[0, 0];
		public int[,] Rock { get; set; } = new int[0, 0];
		public int[,] Ore { get; set; } = new int[0, 0];
		public PersistedCityInfo[] Cities { get; set; } = Array.Empty<PersistedCityInfo>();
		public PersistedPlateSite[] PlateSites { get; set; } = Array.Empty<PersistedPlateSite>();
	}

	private sealed class PersistedWorldStats
	{
		public int Width { get; set; }
		public int Height { get; set; }
		public int CityCount { get; set; }
		public float OceanPercent { get; set; }
		public float RiverPercent { get; set; }
		public float AvgTemperature { get; set; }
		public float AvgMoisture { get; set; }
	}

	private sealed class PersistedCityInfo
	{
		public int X { get; set; }
		public int Y { get; set; }
		public float Score { get; set; }
		public string Name { get; set; } = string.Empty;
		public int Population { get; set; }
	}

	private sealed class PersistedPlateSite
	{
		public int Id { get; set; }
		public int X { get; set; }
		public int Y { get; set; }
		public float MotionX { get; set; }
		public float MotionY { get; set; }
		public bool IsOceanic { get; set; }
		public float BaseElevation { get; set; }
		public float ColorR { get; set; }
		public float ColorG { get; set; }
		public float ColorB { get; set; }
		public float ColorA { get; set; }
	}

	private sealed class TerrainPreset
	{
		public string Name { get; init; } = string.Empty;
		public TerrainMorphology Morphology { get; init; }
		public float SeaLevel { get; init; }
		public int PlateCount { get; init; }
		public int WindCellCount { get; init; }
		public float HeatFactor { get; init; }
		public int ErosionIterations { get; init; }
		public float OceanicRatio { get; init; }
		public float ContinentBias { get; init; }
		public int ContinentCount { get; init; } = 3;
	}

	private sealed class MountainPreset
	{
		public MountainPresetId Id { get; init; }
		public string Name { get; init; } = string.Empty;
		public float InteriorRelief { get; init; }
		public float OrogenyStrength { get; init; }
		public float SubductionArcRatio { get; init; }
		public int ContinentalAge { get; init; }
	}

	private static readonly TerrainPreset[] TerrainPresets =
	{
		new TerrainPreset { Name = "均衡大陆", Morphology = TerrainMorphology.Balanced, SeaLevel = 0.50f, PlateCount = 20, WindCellCount = 10, HeatFactor = 0.50f, ErosionIterations = 5, OceanicRatio = 0.48f, ContinentBias = 0.18f, ContinentCount = 3 },
		new TerrainPreset { Name = "超级大陆", Morphology = TerrainMorphology.Supercontinent, SeaLevel = 0.18f, PlateCount = 8, WindCellCount = 7, HeatFactor = 0.50f, ErosionIterations = 3, OceanicRatio = 0.22f, ContinentBias = 0.92f, ContinentCount = 2 },
		new TerrainPreset { Name = "大陆群", Morphology = TerrainMorphology.Continents, SeaLevel = 0.42f, PlateCount = 20, WindCellCount = 9, HeatFactor = 0.50f, ErosionIterations = 4, OceanicRatio = 0.40f, ContinentBias = 0.66f, ContinentCount = 3 },
		new TerrainPreset { Name = "经典群岛", Morphology = TerrainMorphology.Archipelago, SeaLevel = 0.62f, PlateCount = 30, WindCellCount = 12, HeatFactor = 0.56f, ErosionIterations = 6, OceanicRatio = 0.56f, ContinentBias = 0.10f, ContinentCount = 3 },
		new TerrainPreset { Name = "破碎岛链", Morphology = TerrainMorphology.FracturedIslands, SeaLevel = 0.70f, PlateCount = 42, WindCellCount = 14, HeatFactor = 0.58f, ErosionIterations = 7, OceanicRatio = 0.64f, ContinentBias = 0.04f, ContinentCount = 4 },
		new TerrainPreset { Name = "浅海碎陆", Morphology = TerrainMorphology.ShallowFragments, SeaLevel = 0.57f, PlateCount = 28, WindCellCount = 11, HeatFactor = 0.54f, ErosionIterations = 5, OceanicRatio = 0.52f, ContinentBias = 0.20f, ContinentCount = 3 },
	};

	private static readonly MountainPreset[] MountainPresets =
	{
		new MountainPreset { Id = MountainPresetId.EarthLike, Name = "地球式均衡", InteriorRelief = 1.00f, OrogenyStrength = 1.00f, SubductionArcRatio = 0.72f, ContinentalAge = 58 },
		new MountainPreset { Id = MountainPresetId.YoungOrogeny, Name = "年轻褶皱山系", InteriorRelief = 1.36f, OrogenyStrength = 1.72f, SubductionArcRatio = 0.88f, ContinentalAge = 22 },
		new MountainPreset { Id = MountainPresetId.AncientStable, Name = "古老稳定地盾", InteriorRelief = 0.78f, OrogenyStrength = 0.72f, SubductionArcRatio = 0.40f, ContinentalAge = 86 },
		new MountainPreset { Id = MountainPresetId.EdgeArcs, Name = "边缘岛弧造山", InteriorRelief = 1.08f, OrogenyStrength = 1.48f, SubductionArcRatio = 0.95f, ContinentalAge = 46 }
	};

	private static readonly Vector2[] ContinentCenters2 =
	{
		new Vector2(0.30f, 0.44f),
		new Vector2(0.74f, 0.56f)
	};

	private static readonly Vector2[] ContinentCenters3 =
	{
		new Vector2(0.34f, 0.56f),
		new Vector2(0.68f, 0.45f),
		new Vector2(0.18f, 0.42f)
	};

	private static readonly Vector2[] ContinentCenters4 =
	{
		new Vector2(0.12f, 0.34f),
		new Vector2(0.37f, 0.70f),
		new Vector2(0.63f, 0.30f),
		new Vector2(0.88f, 0.66f)
	};


}
