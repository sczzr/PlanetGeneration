using Godot;
using PlanetGeneration.WorldGen;
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
	[Export] public int MapWidth { get; set; } = 256;
	[Export] public int MapHeight { get; set; } = 128;
	[Export] public int Seed { get; set; } = 0;
	[Export] public int PlateCount { get; set; } = 20;
	[Export] public int WindCellCount { get; set; } = 10;
	[Export(PropertyHint.Range, "0,1,0.01")] public float SeaLevel { get; set; } = 0.5f;
	[Export(PropertyHint.Range, "0.01,1,0.01")] public float HeatFactor { get; set; } = 0.5f;
	[Export(PropertyHint.Range, "1,20,1")] public int MoistureIterations { get; set; } = 8;
	[Export(PropertyHint.Range, "0,20,1")] public int ErosionIterations { get; set; } = 5;
	[Export(PropertyHint.Range, "0.4,2.5,0.01")] public float RiverDensity { get; set; } = 1.0f;
	[Export(PropertyHint.Range, "0.5,2.5,0.01")] public float WindArrowDensity { get; set; } = 1.0f;
	[Export(PropertyHint.Range, "0.5,2.0,0.01")] public float BasinSensitivity { get; set; } = 1.0f;
	[Export] public bool EnableRivers { get; set; } = true;

	private TextureRect _mapTexture = null!;
	private SpinBox _seedSpin = null!;
	private HSlider _plateSpin = null!;
	private HSlider _windSpin = null!;
	private HSlider _seaLevelSlider = null!;
	private HSlider _heatSlider = null!;
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
	private Button _prevEpochButton = null!;
	private Button _nextEpochButton = null!;
	private Label _plateValue = null!;
	private Label _windValue = null!;
	private Label _seaLevelValue = null!;
	private Label _heatValue = null!;
	private Label _erosionValue = null!;
	private Label _riverDensityValue = null!;
	private Label _windArrowDensityValue = null!;
	private Label _basinSensitivityValue = null!;
	private Label _interiorReliefValue = null!;
	private Label _orogenyStrengthValue = null!;
	private Label _subductionArcRatioValue = null!;
	private Label _continentalAgeValue = null!;
	private Button _mountainControlToggleButton = null!;
	private Control _mountainControlBody = null!;
	private Label _mountainControlSummaryLabel = null!;
	private Label _magicValue = null!;
	private Label _aggressionValue = null!;
	private Label _diversityValue = null!;
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
	private OptionButton _mapModeOption = null!;
	private Button _advancedSettingsButton = null!;
	private Button _resetAdvancedSettingsButton = null!;
	private Button _persistCacheGroupButton = null!;
	private Button _clearCacheButton = null!;
	private CheckBox _riverToggle = null!;
	private CheckBox _compareToggle = null!;
	private Button _exportPngButton = null!;
	private Button _exportJsonButton = null!;
	private ProgressBar _generateProgress = null!;
	private Label _progressStatus = null!;
	private Label _cacheStatsLabel = null!;
	private Control _progressOverlay = null!;
	private Container _layerButtons = null!;
	private Control _layerRow = null!;
	private Control _mapCenter = null!;
	private Control _mapRoot = null!;
	private Control _advancedSettingsPanel = null!;
	private Control _biomeHoverPanel = null!;
	private Label _biomeHoverText = null!;
	private Control _plateWindRow = null!;
	private Control _continentCountWrap = null!;
	private AspectRatioContainer _mapAspect = null!;
	private FileDialog _saveFileDialog = null!;
	private ConfirmationDialog _resetAdvancedConfirmDialog = null!;
	private ConfirmationDialog _mapInfoWarningDialog = null!;
	private CheckBox _mapInfoWarningSkipCheck = null!;
	private readonly Dictionary<int, Button> _layerButtonsById = new();

	private static readonly Vector2I[] MapSizePresets =
	{
		new Vector2I(256, 128),
		new Vector2I(512, 256),
		new Vector2I(1024, 512),
		new Vector2I(2048, 1024),
		new Vector2I(4096, 2048)
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
		(int)MapLayer.Ecology
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
	private const int WorldGenerationAlgorithmVersion = 2;
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
	private bool _preferredAdvancedPanelVisible;
	private float _terrainOceanicRatio = 0.48f;
	private float _terrainContinentBias = 0.18f;
	private float _interiorRelief = DefaultInteriorRelief;
	private float _orogenyStrength = DefaultOrogenyStrength;
	private float _subductionArcRatio = DefaultSubductionArcRatio;
	private int _continentalAge = DefaultContinentalAge;
	private MountainPresetId _mountainPresetId = MountainPresetId.EarthLike;
	private bool _mountainControlExpanded = true;
	private TerrainMorphology _terrainMorphology = TerrainMorphology.Balanced;
	private int _continentCount = 3;
	private float _currentReliefExaggeration = ReliefExaggerationMin;
	private ElevationStyle _elevationStyle = ElevationStyle.Realistic;
	private int _magicDensity = DefaultMagicDensity;
	private int _civilAggression = DefaultCivilAggression;
	private int _speciesDiversity = DefaultSpeciesDiversity;
	private int _currentEpoch = DefaultEpoch;
	private int _selectedTimelineEventEpoch = -1;
	private MapMode _mapMode = MapMode.Geographic;

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

	private enum MapMode
	{
		Geographic,
		Geopolitical,
		Arcane
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

	public override void _Ready()
	{
		_mapTexture = GetNodeByName<TextureRect>("MapTexture");
		_seedSpin = GetNodeByName<SpinBox>("SeedSpin");
		_plateSpin = GetNodeByName<HSlider>("PlateSpin");
		_windSpin = GetNodeByName<HSlider>("WindSpin");
		_seaLevelSlider = GetNodeByName<HSlider>("SeaLevelSlider");
		_heatSlider = GetNodeByName<HSlider>("HeatSlider");
		_erosionSlider = GetNodeByName<HSlider>("ErosionSlider");
		_plateValue = GetNodeByName<Label>("PlateValue");
		_windValue = GetNodeByName<Label>("WindValue");
		_seaLevelValue = GetNodeByName<Label>("SeaLevelValue");
		_heatValue = GetNodeByName<Label>("HeatValue");
		_erosionValue = GetNodeByName<Label>("ErosionValue");
		_infoLabel = GetNodeByName<Label>("InfoLabel");
		_compareStatsLabel = GetNodeByName<Label>("CompareStatsLabel");
		_cityNamesLabel = GetNodeByName<RichTextLabel>("CityNamesLabel");
		_legendPanel = GetNodeByName<Control>("LegendPanel");
		_legendTitle = GetNodeByName<Label>("LegendTitle");
		_legendTexture = GetNodeByName<TextureRect>("LegendTexture");
		_legendMinLabel = GetNodeByName<Label>("LegendMin");
		_legendMaxLabel = GetNodeByName<Label>("LegendMax");
		_biomeLegendPanel = GetNodeByName<Control>("BiomeLegendPanel");
		_biomeLegendText = GetNodeByName<RichTextLabel>("BiomeLegendText");
		_layerOption = GetNodeByName<OptionButton>("LayerOption");
		_mapSizeOption = GetNodeByName<OptionButton>("MapSizeOption");
		_terrainPresetOption = GetNodeByName<OptionButton>("TerrainPresetOption");
		_mountainPresetOption = GetNodeByName<OptionButton>("MountainPresetOption");
		_elevationStyleOption = GetNodeByName<OptionButton>("ElevationStyleOption");
		_continentCountOption = GetNodeByName<OptionButton>("ContinentCountOption");
		_archiveOption = GetNodeByName<OptionButton>("ArchiveOption");
		_mapModeOption = GetNodeByName<OptionButton>("MapModeOption");
		_advancedSettingsButton = GetNodeByName<Button>("AdvancedSettingsButton");
		_resetAdvancedSettingsButton = GetNodeByName<Button>("ResetAdvancedSettingsButton");
		_persistCacheGroupButton = GetNodeByName<Button>("PersistCacheGroupButton");
		_clearCacheButton = GetNodeByName<Button>("ClearCacheButton");
		_riverToggle = GetNodeByName<CheckBox>("RiverToggle");
		_compareToggle = GetNodeByName<CheckBox>("CompareToggle");
		_exportPngButton = GetNodeByName<Button>("ExportPngButton");
		_exportJsonButton = GetNodeByName<Button>("ExportJsonButton");
		_generateProgress = GetNodeByName<ProgressBar>("GenerateProgress");
		_progressStatus = GetNodeByName<Label>("ProgressStatus");
		_cacheStatsLabel = GetNodeByName<Label>("CacheStatsLabel");
		_progressOverlay = GetNodeByName<Control>("ProgressOverlay");
		_layerButtons = GetNodeByName<Container>("LayerButtons");
		_layerRow = GetNodeByName<Control>("LayerRow");
		_mapCenter = GetNodeByName<Control>("MapCenter");
		_mapRoot = GetNodeByName<Control>("MapRoot");
		_advancedSettingsPanel = GetNodeByName<Control>("AdvancedSettingsPanel");
		_biomeHoverPanel = GetNodeByName<Control>("BiomeHoverPanel");
		_biomeHoverText = GetNodeByName<Label>("BiomeHoverText");
		_plateWindRow = GetNodeByName<Control>("PlateWindRow");
		_continentCountWrap = GetNodeByName<Control>("ContinentCountWrap");
		_mapAspect = GetNodeByName<AspectRatioContainer>("MapAspect");
		_saveFileDialog = GetNodeByName<FileDialog>("SaveFileDialog");
		_resetAdvancedConfirmDialog = GetNodeByName<ConfirmationDialog>("ResetAdvancedConfirmDialog");
		_mapInfoWarningDialog = GetNodeByName<ConfirmationDialog>("MapInfoWarningDialog");
		_mapInfoWarningSkipCheck = GetNodeByName<CheckBox>("MapInfoWarningSkipCheck");
		_riverDensitySlider = GetNodeByName<HSlider>("RiverDensitySlider");
		_riverDensityValue = GetNodeByName<Label>("RiverDensityValue");
		_windArrowDensitySlider = GetNodeByName<HSlider>("WindArrowDensitySlider");
		_windArrowDensityValue = GetNodeByName<Label>("WindArrowDensityValue");
		_basinSensitivitySlider = GetNodeByName<HSlider>("BasinSensitivitySlider");
		_basinSensitivityValue = GetNodeByName<Label>("BasinSensitivityValue");
		_interiorReliefSlider = GetNodeByName<HSlider>("InteriorReliefSlider");
		_interiorReliefValue = GetNodeByName<Label>("InteriorReliefValue");
		_orogenyStrengthSlider = GetNodeByName<HSlider>("OrogenyStrengthSlider");
		_orogenyStrengthValue = GetNodeByName<Label>("OrogenyStrengthValue");
		_subductionArcRatioSlider = GetNodeByName<HSlider>("SubductionArcRatioSlider");
		_subductionArcRatioValue = GetNodeByName<Label>("SubductionArcRatioValue");
		_continentalAgeSlider = GetNodeByName<HSlider>("ContinentalAgeSlider");
		_continentalAgeValue = GetNodeByName<Label>("ContinentalAgeValue");
		_mountainControlToggleButton = GetNodeByName<Button>("MountainControlToggleButton");
		_mountainControlBody = GetNodeByName<Control>("MountainControlBody");
		_mountainControlSummaryLabel = GetNodeByName<Label>("MountainControlSummary");
		_magicSlider = GetNodeByName<HSlider>("MagicSlider");
		_magicValue = GetNodeByName<Label>("MagicValue");
		_aggressionSlider = GetNodeByName<HSlider>("AggressionSlider");
		_aggressionValue = GetNodeByName<Label>("AggressionValue");
		_diversitySlider = GetNodeByName<HSlider>("DiversitySlider");
		_diversityValue = GetNodeByName<Label>("DiversityValue");
		_timelineSlider = GetNodeByName<HSlider>("TimelineSlider");
		_prevEpochButton = GetNodeByName<Button>("PrevEpochButton");
		_nextEpochButton = GetNodeByName<Button>("NextEpochButton");
		_epochLabel = GetNodeByName<Label>("EpochLabel");
		_epochEventIndexLabel = GetNodeByName<Label>("EpochEventIndexLabel");
		_loreStateLabel = GetNodeByName<Label>("LoreStateLabel");
		_threatLabel = GetNodeByName<Label>("ThreatLabel");
		_loreText = GetNodeByName<RichTextLabel>("LoreText");

		_generateProgress.Value = 0;
		_progressStatus.Text = "待命";
		_progressOverlay.Visible = false;
		_advancedSettingsPanel.Visible = false;
		_biomeHoverPanel.Visible = false;
		_pendingExportKind = ExportKind.None;

		_saveFileDialog.FileMode = FileDialog.FileModeEnum.SaveFile;
		_saveFileDialog.Access = FileDialog.AccessEnum.Filesystem;
		_saveFileDialog.FileSelected += OnSaveFileSelected;
		_saveFileDialog.Canceled += () => _pendingExportKind = ExportKind.None;
		_resetAdvancedConfirmDialog.Confirmed += ResetAdvancedSettingsConfirmed;
		_mapInfoWarningDialog.Confirmed += ConfirmMapInfoSelection;
		_mapInfoWarningDialog.Canceled += () =>
		{
			_pendingMapSizeIndex = -1;
			_mapInfoWarningSkipCheck.ButtonPressed = false;
		};

		_mapCenter.Resized += SyncMapAspectToCenter;
		CallDeferred(nameof(SyncMapAspectToCenter));
		LoadAdvancedSettings();

		SetupLayerOptions();
		SetupMapSizeOptions();
		SetupContinentCountOptions();
		SetupTerrainPresetOptions();
		SetupMountainPresetOptions();
		SetupElevationStyleOptions();
		SetupArchiveOptions();
		SetupMapModeOptions();

		GetNodeByName<Button>("GenerateButton").Pressed += OnGeneratePressed;
		GetNodeByName<Button>("RandomButton").Pressed += OnRandomPressed;
		_advancedSettingsButton.Pressed += ToggleAdvancedSettings;
		_resetAdvancedSettingsButton.Pressed += OnResetAdvancedSettingsPressed;
		_exportPngButton.Pressed += OnExportPngPressed;
		_exportJsonButton.Pressed += OnExportJsonPressed;
		_persistCacheGroupButton.Pressed += OnSaveArchivePressed;
		_clearCacheButton.Pressed += OnClearCachePressed;

		_seaLevelSlider.ValueChanged += OnSeaLevelChanged;
		_heatSlider.ValueChanged += OnHeatChanged;
		_erosionSlider.ValueChanged += OnErosionChanged;
		_riverDensitySlider.ValueChanged += OnRiverDensityChanged;
		_windArrowDensitySlider.ValueChanged += OnWindArrowDensityChanged;
		_basinSensitivitySlider.ValueChanged += OnBasinSensitivityChanged;
		_interiorReliefSlider.ValueChanged += OnInteriorReliefChanged;
		_orogenyStrengthSlider.ValueChanged += OnOrogenyStrengthChanged;
		_subductionArcRatioSlider.ValueChanged += OnSubductionArcRatioChanged;
		_continentalAgeSlider.ValueChanged += OnContinentalAgeChanged;
		_mountainControlToggleButton.Pressed += ToggleMountainControlGroup;
		_magicSlider.ValueChanged += OnMagicDensityChanged;
		_aggressionSlider.ValueChanged += OnCivilAggressionChanged;
		_diversitySlider.ValueChanged += OnSpeciesDiversityChanged;
		_timelineSlider.ValueChanged += OnTimelineChanged;
		_prevEpochButton.Pressed += OnPrevEpochPressed;
		_nextEpochButton.Pressed += OnNextEpochPressed;
		_plateSpin.ValueChanged += value =>
		{
			PlateCount = Mathf.Clamp((int)Mathf.Round((float)value), 1, 200);
			UpdateLabels();
		};
		_windSpin.ValueChanged += value =>
		{
			WindCellCount = Mathf.Clamp((int)Mathf.Round((float)value), 1, 100);
			UpdateLabels();
		};
		_layerOption.ItemSelected += _ =>
		{
			SyncMapModeFromLayer(_layerOption.GetSelectedId());
			RedrawCurrentLayer();
			UpdateLayerQuickButtons();
			SaveAdvancedSettings();
		};

		_mapModeOption.ItemSelected += id =>
		{
			ApplyMapModeSelection((MapMode)_mapModeOption.GetItemId((int)id), persist: true, applyRecommendedLayer: true);
		};

		_riverToggle.Toggled += value =>
		{
			EnableRivers = value;
			UpdateRiverDensityControlState();
			UpdateRiverLayerAvailability();
			SaveAdvancedSettings();
			GenerateWorld();
		};

		_compareToggle.Toggled += value =>
		{
			_compareMode = value;
			GenerateWorld();
		};

		SetRandomSeed();
		_seedSpin.Value = Seed;
		_plateSpin.Value = PlateCount;
		_windSpin.Value = WindCellCount;
		_seaLevelSlider.Value = SeaLevel;
		_heatSlider.Value = HeatFactor;
		_erosionSlider.Value = ErosionIterations;
		_riverDensitySlider.Value = RiverDensity;
		_windArrowDensitySlider.Value = WindArrowDensity;
		_basinSensitivitySlider.Value = BasinSensitivity;
		_interiorReliefSlider.Value = _interiorRelief;
		_orogenyStrengthSlider.Value = _orogenyStrength;
		_subductionArcRatioSlider.Value = _subductionArcRatio;
		_continentalAgeSlider.Value = _continentalAge;
		_magicSlider.Value = _magicDensity;
		_aggressionSlider.Value = _civilAggression;
		_diversitySlider.Value = _speciesDiversity;
		_timelineSlider.Value = _currentEpoch;
		UpdateTimelineReplayCursor(Array.Empty<CivilizationEpochEvent>());
		_riverToggle.ButtonPressed = EnableRivers;
		UpdateRiverDensityControlState();
		UpdateRiverLayerAvailability();
		_compareToggle.ButtonPressed = false;
		_compareToggle.Visible = false;
		_compareToggle.Disabled = true;
		_legendPanel.Visible = false;
		_biomeLegendPanel.Visible = false;
		_plateWindRow.Visible = false;
		_plateSpin.Editable = false;
		_windSpin.Editable = false;

		_compareStatsLabel.Visible = false;
		_cityNamesLabel.Visible = false;
		_layerOption.Visible = false;
		_layerRow.Visible = true;
		_layerRow.ZIndex = 10;
		ApplySavedUiState();
		ApplyMapModeSelection(_mapMode, persist: false, applyRecommendedLayer: false);
		UpdateLayerQuickButtons();

		_mapTexture.MouseFilter = Control.MouseFilterEnum.Stop;
		_mapTexture.GuiInput += OnMapTextureGuiInput;
		_mapTexture.MouseExited += OnMapTextureMouseExited;

		UpdateLabels();
		UpdateLorePanel();
		RefreshCacheStatsLabel();
		GenerateWorld();
	}

	private void ToggleAdvancedSettings()
	{
		SetAdvancedSettingsPanelVisible(!_advancedSettingsPanel.Visible);
	}

	public override void _Input(InputEvent @event)
	{
		if (!_advancedSettingsPanel.Visible)
		{
			return;
		}

		if (@event is InputEventKey keyEvent &&
			keyEvent.Pressed &&
			!keyEvent.Echo &&
			keyEvent.Keycode == Key.Escape)
		{
			SetAdvancedSettingsPanelVisible(false);
			return;
		}

		if (@event is not InputEventMouseButton mouseButton ||
			mouseButton.ButtonIndex != MouseButton.Left ||
			!mouseButton.Pressed)
		{
			return;
		}

		var clickPosition = mouseButton.Position;
		if (IsPointInsideControl(_advancedSettingsPanel, clickPosition) ||
			IsPointInsideControl(_advancedSettingsButton, clickPosition))
		{
			return;
		}

		SetAdvancedSettingsPanelVisible(false);
	}

	private void SetAdvancedSettingsPanelVisible(bool visible, bool persist = true)
	{
		if (_advancedSettingsPanel.Visible == visible)
		{
			return;
		}

		_advancedSettingsPanel.Visible = visible;
		if (persist)
		{
			SaveAdvancedSettings();
		}
	}

	private void ToggleMountainControlGroup()
	{
		SetMountainControlExpanded(!_mountainControlExpanded);
	}

	private void SetMountainControlExpanded(bool expanded, bool persist = true)
	{
		_mountainControlExpanded = expanded;
		_mountainControlToggleButton.Text = expanded ? "收起山脉参数 ▲" : "展开山脉参数 ▼";
		UpdateMountainControlSummary();
		_mountainControlBody.Visible = expanded;
		_mountainControlSummaryLabel.Visible = !expanded;

		if (persist)
		{
			SaveAdvancedSettings();
		}
	}

	private void UpdateMountainControlSummary()
	{
		_mountainControlSummaryLabel.Text =
			$"起伏:{_interiorRelief:0.00} | 造山:{_orogenyStrength:0.00} | 俯冲:{_subductionArcRatio:0.00} | 年龄:{_continentalAge}";
	}

	private void ApplySavedUiState()
	{
		var savedMapSizeIndex = FindMapSizePresetIndex(MapWidth, MapHeight);
		if (savedMapSizeIndex >= 0)
		{
			_suppressMapSizeSelectionHandler = true;
			_mapSizeOption.Select(savedMapSizeIndex);
			_suppressMapSizeSelectionHandler = false;
			_lastConfirmedMapSizeIndex = savedMapSizeIndex;
		}

		var layerIndex = _layerOption.GetItemIndex(_preferredLayerId);
		var canUsePreferredLayer = layerIndex >= 0 && !_layerOption.IsItemDisabled(layerIndex);
		_magicSlider.SetValueNoSignal(_magicDensity);
		_aggressionSlider.SetValueNoSignal(_civilAggression);
		_diversitySlider.SetValueNoSignal(_speciesDiversity);
		_timelineSlider.SetValueNoSignal(_currentEpoch);
		SelectMapModeOption(_mapMode);
		SelectLayerById(canUsePreferredLayer ? _preferredLayerId : (int)MapLayer.Satellite, persist: false);
		SetAdvancedSettingsPanelVisible(_preferredAdvancedPanelVisible, persist: false);
		SetMountainControlExpanded(_mountainControlExpanded, persist: false);
	}

	private static bool IsPointInsideControl(Control control, Vector2 point)
	{
		return control.Visible && control.GetGlobalRect().HasPoint(point);
	}

	private void SetupLayerOptions()
	{
		_layerOption.Clear();
		_layerOption.AddItem("Satellite", (int)MapLayer.Satellite);
		_layerOption.AddItem("Plates", (int)MapLayer.Plates);

		_layerOption.AddItem("Temperature", (int)MapLayer.Temperature);
		_layerOption.AddItem("Rivers", (int)MapLayer.Rivers);
		_layerOption.AddItem("Moisture", (int)MapLayer.Moisture);
		_layerOption.AddItem("Wind", (int)MapLayer.Wind);
		_layerOption.AddItem("Elevation", (int)MapLayer.Elevation);
		_layerOption.AddItem("RockTypes", (int)MapLayer.RockTypes);

		_layerOption.AddItem("Ores", (int)MapLayer.Ores);
		_layerOption.AddItem("Biomes", (int)MapLayer.Biomes);
		_layerOption.AddItem("Cities", (int)MapLayer.Cities);
		_layerOption.AddItem("地貌", (int)MapLayer.Landform);
		_layerOption.AddItem("生态演化", (int)MapLayer.Ecology);
		_layerOption.AddItem("文明疆域", (int)MapLayer.Civilization);
		_layerOption.AddItem("贸易走廊", (int)MapLayer.TradeRoutes);
		_layerOption.Select(0);
		BuildLayerButtons();
	}

	private void SelectLayerById(int layerId, bool persist = true)
	{
		var index = _layerOption.GetItemIndex(layerId);
		if (index < 0)
		{
			return;
		}

		_layerOption.Select(index);
		SyncMapModeFromLayer(layerId);
		RedrawCurrentLayer();
		UpdateLayerQuickButtons();
		if (persist)
		{
			SaveAdvancedSettings();
		}
	}

	private void BuildLayerButtons()
	{
		var children = _layerButtons.GetChildren();
		foreach (Node child in children)
		{
			_layerButtons.RemoveChild(child);
			child.Free();
		}

		_layerButtonsById.Clear();
		BuildLayerCategorySection("自然 / NATURAL", NaturalLayerIds,
			new Color(0.090196f, 0.145098f, 0.231373f, 0.96f),
			new Color(0.129412f, 0.196078f, 0.301961f, 0.98f),
			new Color(0.180392f, 0.415686f, 0.862745f, 1f),
			new Color(0.764706f, 0.858824f, 1f, 0.45f));

		BuildLayerCategorySection("人文 / CIVIL", HumanLayerIds,
			new Color(0.254f, 0.196f, 0.118f, 0.95f),
			new Color(0.320f, 0.240f, 0.145f, 0.98f),
			new Color(0.780f, 0.522f, 0.145f, 1f),
			new Color(1f, 0.804f, 0.439f, 0.45f));

		BuildLayerCategorySection("神秘 / ARCANE", ArcaneLayerIds,
			new Color(0.176f, 0.106f, 0.286f, 0.95f),
			new Color(0.243f, 0.141f, 0.388f, 0.98f),
			new Color(0.525f, 0.286f, 0.902f, 1f),
			new Color(0.819f, 0.666f, 1f, 0.45f));
	}

	private void BuildLayerCategorySection(string title, int[] layerIds, Color normalBg, Color hoverBg, Color activeBg, Color activeBorder)
	{
		var section = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		section.AddThemeConstantOverride("separation", 4);

		var header = new Label
		{
			Text = title
		};
		header.AddThemeColorOverride("font_color", new Color(0.639f, 0.725f, 0.835f, 0.9f));
		header.AddThemeFontSizeOverride("font_size", 10);
		section.AddChild(header);

		var row = new HFlowContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("h_separation", 6);
		row.AddThemeConstantOverride("v_separation", 6);
		section.AddChild(row);

		var normalStyle = CreateLayerButtonStyle(normalBg, new Color(activeBorder.R, activeBorder.G, activeBorder.B, 0.18f));
		var hoverStyle = CreateLayerButtonStyle(hoverBg, new Color(activeBorder.R, activeBorder.G, activeBorder.B, 0.28f));
		var activeStyle = CreateLayerButtonStyle(activeBg, activeBorder);

		foreach (var layerId in layerIds)
		{
			var itemIndex = _layerOption.GetItemIndex(layerId);
			if (itemIndex < 0)
			{
				continue;
			}

			var button = new Button
			{
				Text = GetLayerButtonText(layerId, _layerOption.GetItemText(itemIndex)),
				ToggleMode = true,
				FocusMode = Control.FocusModeEnum.None,
				CustomMinimumSize = new Vector2(70f, 28f),
				ClipText = true
			};

			button.AddThemeStyleboxOverride("normal", normalStyle);
			button.AddThemeStyleboxOverride("hover", hoverStyle);
			button.AddThemeStyleboxOverride("pressed", activeStyle);
			button.AddThemeStyleboxOverride("focus", activeStyle);
			button.AddThemeStyleboxOverride("disabled", normalStyle);
			button.AddThemeColorOverride("font_color", new Color(0.84f, 0.9f, 0.98f, 0.95f));
			button.AddThemeColorOverride("font_hover_color", Colors.White);
			button.AddThemeColorOverride("font_pressed_color", Colors.White);
			button.AddThemeColorOverride("font_focus_color", Colors.White);
			button.AddThemeFontSizeOverride("font_size", 12);

			var capturedLayerId = layerId;
			button.Pressed += () => SelectLayerById(capturedLayerId);

			row.AddChild(button);
			_layerButtonsById[layerId] = button;
		}

		if (row.GetChildCount() > 0)
		{
			_layerButtons.AddChild(section);
		}
		else
		{
			section.QueueFree();
		}
	}

	private void SetupMapModeOptions()
	{
		_mapModeOption.Clear();
		_mapModeOption.AddItem("地理", (int)MapMode.Geographic);
		_mapModeOption.AddItem("政区", (int)MapMode.Geopolitical);
		_mapModeOption.AddItem("奥术", (int)MapMode.Arcane);
		SelectMapModeOption(_mapMode);
	}

	private void ApplyMapModeSelection(MapMode mode, bool persist, bool applyRecommendedLayer)
	{
		_mapMode = mode;
		SelectMapModeOption(mode);

		if (applyRecommendedLayer)
		{
			var recommendedLayer = GetRecommendedLayerForMode(mode);
			if (recommendedLayer >= 0)
			{
				SelectLayerById(recommendedLayer, persist: false);
			}
		}

		UpdateLorePanel();
		if (persist)
		{
			SaveAdvancedSettings();
		}
	}

	private void SelectMapModeOption(MapMode mode)
	{
		for (var index = 0; index < _mapModeOption.ItemCount; index++)
		{
			if (_mapModeOption.GetItemId(index) == (int)mode)
			{
				_mapModeOption.Select(index);
				return;
			}
		}

		if (_mapModeOption.ItemCount > 0)
		{
			_mapModeOption.Select(0);
		}
	}

	private void SyncMapModeFromLayer(int layerId)
	{
		var inferredMode = DetermineMapModeByLayer(layerId);
		if (_mapMode == inferredMode)
		{
			return;
		}

		_mapMode = inferredMode;
		SelectMapModeOption(_mapMode);
		UpdateLorePanel();
	}

	private static MapMode DetermineMapModeByLayer(int layerId)
	{
		if (Array.IndexOf(ArcaneLayerIds, layerId) >= 0)
		{
			return MapMode.Arcane;
		}

		if (Array.IndexOf(HumanLayerIds, layerId) >= 0)
		{
			return MapMode.Geopolitical;
		}

		return MapMode.Geographic;
	}

	private int GetRecommendedLayerForMode(MapMode mode)
	{
		return mode switch
		{
			MapMode.Geographic => (int)MapLayer.Satellite,
			MapMode.Geopolitical => (int)MapLayer.TradeRoutes,
			MapMode.Arcane => (int)MapLayer.Biomes,
			_ => -1
		};
	}

	private string GetLayerButtonText(int layerId, string fallback)
	{
		return layerId switch
		{
			(int)MapLayer.Satellite => "卫星",
			(int)MapLayer.Plates => "板块",
			(int)MapLayer.Temperature => "温度",
			(int)MapLayer.Rivers => "河流",
			(int)MapLayer.Moisture => "降水",
			(int)MapLayer.Wind => "风场",
			(int)MapLayer.Elevation => "高程",
			(int)MapLayer.RockTypes => "岩石",
			(int)MapLayer.Ores => "矿产",
			(int)MapLayer.Biomes => "群系",
			(int)MapLayer.Cities => "城市",
			(int)MapLayer.Landform => "地貌",
			(int)MapLayer.Ecology => "生态",
			(int)MapLayer.Civilization => "文明",
			(int)MapLayer.TradeRoutes => "贸易",
			_ => fallback
		};
	}

	private void UpdateLayerQuickButtons()
	{
		var selectedId = _layerOption.GetSelectedId();
		foreach (var pair in _layerButtonsById)
		{
			var isSelected = pair.Key == selectedId;
			pair.Value.ButtonPressed = isSelected;
			pair.Value.Modulate = pair.Value.Disabled
				? new Color(0.66f, 0.72f, 0.80f, 0.74f)
				: Colors.White;
		}
	}

	private static StyleBoxFlat CreateLayerButtonStyle(Color background, Color border)
	{
		return new StyleBoxFlat
		{
			BgColor = background,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			BorderColor = border,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6
		};
	}

	private void SetupMapSizeOptions()
	{
		_mapSizeOption.Clear();

		for (var index = 0; index < MapSizePresets.Length; index++)
		{
			var size = MapSizePresets[index];
			_mapSizeOption.AddItem(BuildInfoPointOptionText(index, size), index);
		}

		const int selectedIndex = 0;

		ApplyMapSizePreset(selectedIndex);
		_mapSizeOption.Select(selectedIndex);
		_lastConfirmedMapSizeIndex = selectedIndex;
		ShowInfoPointHint();

		_mapSizeOption.ItemSelected += id =>
		{
			if (_suppressMapSizeSelectionHandler)
			{
				return;
			}

			var selectedIndex = (int)id;
			if (RequiresMapInfoConfirmation(selectedIndex))
			{
				_pendingMapSizeIndex = selectedIndex;
				_mapInfoWarningDialog.DialogText = BuildMapInfoWarningText(selectedIndex);
				_mapInfoWarningDialog.PopupCentered();

				_suppressMapSizeSelectionHandler = true;
				_mapSizeOption.Select(_lastConfirmedMapSizeIndex);
				_suppressMapSizeSelectionHandler = false;
				return;
			}

			CommitMapSizeSelection(selectedIndex);
		};
	}

	private bool RequiresMapInfoConfirmation(int presetIndex)
	{
		if (_skipMapInfoWarningForSession)
		{
			return false;
		}

		if (presetIndex < 0 || presetIndex >= MapSizePresets.Length)
		{
			return false;
		}

		if (presetIndex == _lastConfirmedMapSizeIndex)
		{
			return false;
		}

		var profile = BuildInfoPointOptionText(presetIndex, MapSizePresets[presetIndex]);
		return profile is "高清" or "极致";
	}

	private static string BuildMapInfoWarningText(int presetIndex)
	{
		var level = presetIndex >= 0 && presetIndex < MapSizePresets.Length
			? BuildInfoPointOptionText(presetIndex, MapSizePresets[presetIndex])
			: "高等级";
		return $"你选择了“{level}”地图信息级别，生成耗时会明显增加。是否继续？";
	}

	private void ConfirmMapInfoSelection()
	{
		if (_pendingMapSizeIndex < 0)
		{
			return;
		}

		if (_mapInfoWarningSkipCheck.ButtonPressed)
		{
			_skipMapInfoWarningForSession = true;
		}
		_mapInfoWarningSkipCheck.ButtonPressed = false;

		CommitMapSizeSelection(_pendingMapSizeIndex);
		_pendingMapSizeIndex = -1;
	}

	private void CommitMapSizeSelection(int selectedIndex)
	{
		ApplyMapSizePreset(selectedIndex);
		_suppressMapSizeSelectionHandler = true;
		_mapSizeOption.Select(selectedIndex);
		_suppressMapSizeSelectionHandler = false;
		_lastConfirmedMapSizeIndex = selectedIndex;
		ShowInfoPointHint();
		GenerateWorld();
	}

	private void SetupTerrainPresetOptions()
	{
		_terrainPresetOption.Clear();

		for (var index = 0; index < TerrainPresets.Length; index++)
		{
			_terrainPresetOption.AddItem(TerrainPresets[index].Name, index);
		}

		const int defaultIndex = 0;
		_terrainPresetOption.Select(defaultIndex);
		ApplyTerrainPreset(defaultIndex, regenerate: false);

		_terrainPresetOption.ItemSelected += id =>
		{
			ApplyTerrainPreset((int)id, regenerate: true);
		};
	}

	private void SetupMountainPresetOptions()
	{
		_mountainPresetOption.Clear();
		for (var index = 0; index < MountainPresets.Length; index++)
		{
			var preset = MountainPresets[index];
			_mountainPresetOption.AddItem(preset.Name, (int)preset.Id);
		}

		_mountainPresetOption.AddItem("自定义", (int)MountainPresetId.Custom);

		if (_mountainPresetId != MountainPresetId.Custom && !TryGetMountainPreset(_mountainPresetId, out _))
		{
			_mountainPresetId = ResolveMountainPresetIdFromCurrentValues();
		}

		if (_mountainPresetId == MountainPresetId.Custom)
		{
			SyncMountainPresetFromCurrentValues();
		}
		else
		{
			ApplyMountainPreset(_mountainPresetId, regenerate: false, persist: false);
		}

		_mountainPresetOption.ItemSelected += id =>
		{
			if (_suppressMountainPresetSelectionHandler)
			{
				return;
			}

			var selectedId = _mountainPresetOption.GetItemId((int)id);
			if (!Enum.IsDefined(typeof(MountainPresetId), selectedId))
			{
				return;
			}

			var presetId = (MountainPresetId)selectedId;
			if (presetId == MountainPresetId.Custom)
			{
				_mountainPresetId = MountainPresetId.Custom;
				SaveAdvancedSettings();
				return;
			}

			ApplyMountainPreset(presetId, regenerate: true, persist: true);
		};
	}

	private void ApplyMountainPreset(MountainPresetId presetId, bool regenerate, bool persist)
	{
		if (!TryGetMountainPreset(presetId, out var preset))
		{
			_mountainPresetId = MountainPresetId.Custom;
			SelectMountainPresetOption(_mountainPresetId);
			return;
		}

		_mountainPresetId = presetId;
		_interiorRelief = Mathf.Clamp(preset.InteriorRelief, 0.5f, 2.0f);
		_orogenyStrength = Mathf.Clamp(preset.OrogenyStrength, 0.5f, 2.5f);
		_subductionArcRatio = Mathf.Clamp(preset.SubductionArcRatio, 0.2f, 1.0f);
		_continentalAge = Mathf.Clamp(preset.ContinentalAge, 0, 100);

		_interiorReliefSlider.SetValueNoSignal(_interiorRelief);
		_orogenyStrengthSlider.SetValueNoSignal(_orogenyStrength);
		_subductionArcRatioSlider.SetValueNoSignal(_subductionArcRatio);
		_continentalAgeSlider.SetValueNoSignal(_continentalAge);
		SelectMountainPresetOption(_mountainPresetId);

		UpdateLabels();
		if (persist)
		{
			SaveAdvancedSettings();
		}

		if (regenerate)
		{
			GenerateWorld();
		}
	}

	private bool TryGetMountainPreset(MountainPresetId presetId, out MountainPreset preset)
	{
		for (var index = 0; index < MountainPresets.Length; index++)
		{
			if (MountainPresets[index].Id == presetId)
			{
				preset = MountainPresets[index];
				return true;
			}
		}

		preset = null!;
		return false;
	}

	private void SelectMountainPresetOption(MountainPresetId presetId)
	{
		for (var index = 0; index < _mountainPresetOption.ItemCount; index++)
		{
			if (_mountainPresetOption.GetItemId(index) != (int)presetId)
			{
				continue;
			}

			_suppressMountainPresetSelectionHandler = true;
			_mountainPresetOption.Select(index);
			_suppressMountainPresetSelectionHandler = false;
			return;
		}

		for (var index = 0; index < _mountainPresetOption.ItemCount; index++)
		{
			if (_mountainPresetOption.GetItemId(index) != (int)MountainPresetId.Custom)
			{
				continue;
			}

			_suppressMountainPresetSelectionHandler = true;
			_mountainPresetOption.Select(index);
			_suppressMountainPresetSelectionHandler = false;
			return;
		}
	}

	private MountainPresetId ResolveMountainPresetIdFromCurrentValues()
	{
		for (var index = 0; index < MountainPresets.Length; index++)
		{
			var preset = MountainPresets[index];
			if (!AreNearlyEqual(_interiorRelief, preset.InteriorRelief))
			{
				continue;
			}

			if (!AreNearlyEqual(_orogenyStrength, preset.OrogenyStrength))
			{
				continue;
			}

			if (!AreNearlyEqual(_subductionArcRatio, preset.SubductionArcRatio))
			{
				continue;
			}

			if (_continentalAge != preset.ContinentalAge)
			{
				continue;
			}

			return preset.Id;
		}

		return MountainPresetId.Custom;
	}

	private void SyncMountainPresetFromCurrentValues()
	{
		var resolvedPreset = ResolveMountainPresetIdFromCurrentValues();
		if (_mountainPresetId == resolvedPreset)
		{
			return;
		}

		_mountainPresetId = resolvedPreset;
		SelectMountainPresetOption(_mountainPresetId);
	}

	private static bool AreNearlyEqual(float left, float right)
	{
		return Mathf.Abs(left - right) <= 0.005f;
	}

	private void SetupElevationStyleOptions()
	{
		_elevationStyleOption.Clear();
		_elevationStyleOption.AddItem("写实", (int)ElevationStyle.Realistic);
		_elevationStyleOption.AddItem("地形图", (int)ElevationStyle.Topographic);
		SelectElevationStyleOption(_elevationStyle);

		_elevationStyleOption.ItemSelected += index =>
		{
			var styleId = _elevationStyleOption.GetItemId((int)index);
			_elevationStyle = Enum.IsDefined(typeof(ElevationStyle), styleId)
				? (ElevationStyle)styleId
				: ElevationStyle.Realistic;
			SaveAdvancedSettings();
			RedrawCurrentLayer();
		};
	}

	private void SetupArchiveOptions()
	{
		_archivePathByOptionId.Clear();
		_archiveOption.Clear();
		_archiveOption.AddItem("(无) 仅自动缓存", 0);

		PopulateArchiveOptionItems();

		if (!_archiveOptionSignalBound)
		{
			_archiveOptionSignalBound = true;
			_archiveOption.ItemSelected += id =>
			{
				if (_suppressArchiveSelectionHandler)
				{
					return;
				}

				if (!_archivePathByOptionId.TryGetValue((int)id, out var archivePath) || string.IsNullOrEmpty(archivePath))
				{
					_lastArchivePath = string.Empty;
					SaveAdvancedSettings();
					return;
				}

				LoadArchiveByPath(archivePath);
			};
		}
	}

	private void PopulateArchiveOptionItems()
	{
		var archiveDir = BuildArchiveDirectoryPath();
		if (!IODirectory.Exists(archiveDir))
		{
			SelectArchiveOptionId(0);
			return;
		}

		var files = IODirectory.GetFiles(archiveDir, "*" + ArchiveFileExtension, System.IO.SearchOption.TopDirectoryOnly);
		Array.Sort(files, (left, right) => IOFile.GetLastWriteTimeUtc(right).CompareTo(IOFile.GetLastWriteTimeUtc(left)));

		var nextOptionId = 1;
		var selectedId = 0;
		for (var i = 0; i < files.Length; i++)
		{
			var filePath = files[i];
			var label = BuildArchiveOptionLabel(filePath);
			_archiveOption.AddItem(label, nextOptionId);
			_archivePathByOptionId[nextOptionId] = filePath;

			if (!string.IsNullOrEmpty(_lastArchivePath) && string.Equals(filePath, _lastArchivePath, StringComparison.OrdinalIgnoreCase))
			{
				selectedId = nextOptionId;
			}

			nextOptionId++;
		}

		if (selectedId == 0 && !string.IsNullOrEmpty(_lastArchivePath))
		{
			_lastArchivePath = string.Empty;
			SaveAdvancedSettings();
		}

		SelectArchiveOptionId(selectedId);
	}

	private void SelectArchiveOptionId(int optionId)
	{
		for (var index = 0; index < _archiveOption.ItemCount; index++)
		{
			if (_archiveOption.GetItemId(index) != optionId)
			{
				continue;
			}

			_suppressArchiveSelectionHandler = true;
			_archiveOption.Select(index);
			_suppressArchiveSelectionHandler = false;
			return;
		}

		_suppressArchiveSelectionHandler = true;
		_archiveOption.Select(0);
		_suppressArchiveSelectionHandler = false;
	}

	private string BuildArchiveOptionLabel(string archivePath)
	{
		var fileName = IOPath.GetFileName(archivePath);
		var timestampText = IOFile.GetLastWriteTime(archivePath).ToString("MM-dd HH:mm");
		if (!TryReadPersistedCacheEntryFromFile(archivePath, out var payload))
		{
			return $"{timestampText} | {fileName}";
		}

		var modeText = payload.CompareMode && payload.Compare != null ? "对比" : "单图";
		return $"{timestampText} | seed:{payload.Seed} | {payload.MapWidth}x{payload.MapHeight} | {modeText}";
	}

	private void SelectElevationStyleOption(ElevationStyle style)
	{
		for (var index = 0; index < _elevationStyleOption.ItemCount; index++)
		{
			if (_elevationStyleOption.GetItemId(index) != (int)style)
			{
				continue;
			}

			_elevationStyleOption.Select(index);
			return;
		}

		_elevationStyleOption.Select(0);
	}

	private void SetupContinentCountOptions()
	{
		_continentCountOption.Clear();
		_continentCountOption.AddItem("2 块", 2);
		_continentCountOption.AddItem("3 块", 3);
		_continentCountOption.AddItem("4 块", 4);
		SelectContinentCountOption(_continentCount);

		_continentCountOption.ItemSelected += index =>
		{
			_continentCount = Mathf.Clamp(_continentCountOption.GetItemId((int)index), 2, 4);
			UpdateContinentCountVisibility();

			if (_terrainMorphology == TerrainMorphology.Continents)
			{
				GenerateWorld();
			}
		};

		UpdateContinentCountVisibility();
	}

	private void SelectContinentCountOption(int count)
	{
		for (var index = 0; index < _continentCountOption.ItemCount; index++)
		{
			if (_continentCountOption.GetItemId(index) != count)
			{
				continue;
			}

			_continentCountOption.Select(index);
			return;
		}

		_continentCountOption.Select(1);
	}

	private void UpdateContinentCountVisibility()
	{
		_continentCountWrap.Visible = _terrainMorphology == TerrainMorphology.Continents;
	}

	private void ApplyTerrainPreset(int presetIndex, bool regenerate)
	{
		var index = Mathf.Clamp(presetIndex, 0, TerrainPresets.Length - 1);
		var preset = TerrainPresets[index];

		SeaLevel = Mathf.Clamp(preset.SeaLevel, 0f, 0.9f);
		PlateCount = Mathf.Clamp(preset.PlateCount, 1, 200);
		WindCellCount = Mathf.Clamp(preset.WindCellCount, 1, 100);
		HeatFactor = Mathf.Clamp(preset.HeatFactor, 0.01f, 1f);
		ErosionIterations = Mathf.Clamp(preset.ErosionIterations, 0, 20);
		_terrainOceanicRatio = Mathf.Clamp(preset.OceanicRatio, 0.05f, 0.95f);
		_terrainContinentBias = Mathf.Clamp(preset.ContinentBias, 0f, 1f);
		_terrainMorphology = preset.Morphology;
		_continentCount = Mathf.Clamp(preset.ContinentCount, 2, 4);

		_plateSpin.SetValueNoSignal(PlateCount);
		_windSpin.SetValueNoSignal(WindCellCount);
		_seaLevelSlider.SetValueNoSignal(SeaLevel);
		_heatSlider.SetValueNoSignal(HeatFactor);
		_erosionSlider.SetValueNoSignal(ErosionIterations);
		SelectContinentCountOption(_continentCount);
		UpdateContinentCountVisibility();

		UpdateLabels();

		if (regenerate)
		{
			GenerateWorld();
		}
	}

	private static string BuildInfoPointOptionText(int index, Vector2I size)
	{
		var profile = index switch
		{
			0 => "预览",
			1 => "标准",
			2 => "精细",
			3 => "高清",
			_ => "极致"
		};

		return profile;
	}

	private void ApplyMapSizePreset(int presetIndex)
	{
		var index = Mathf.Clamp(presetIndex, 0, MapSizePresets.Length - 1);
		MapWidth = MapSizePresets[index].X;
		MapHeight = MapSizePresets[index].Y;
	}

	private int FindMapSizePresetIndex(int width, int height)
	{
		for (var index = 0; index < MapSizePresets.Length; index++)
		{
			var size = MapSizePresets[index];
			if (size.X == width && size.Y == height)
			{
				return index;
			}
		}

		return -1;
	}

	private bool IsHighInfoPointSelected()
	{
		return MapWidth >= 2048 || MapHeight >= 1024;
	}

	private void ShowInfoPointHint()
	{
		_infoLabel.Text = IsHighInfoPointSelected()
			? HighInfoPointWarningText
			: FixedUltraOutputText;
	}

	private void OnGeneratePressed()
	{
		Seed = (int)_seedSpin.Value;
		PlateCount = Mathf.Clamp((int)Mathf.Round((float)_plateSpin.Value), 1, 200);
		WindCellCount = Mathf.Clamp((int)Mathf.Round((float)_windSpin.Value), 1, 100);
		ApplyMapSizePreset(_mapSizeOption.GetSelectedId());
		ShowInfoPointHint();
		GenerateWorld();
	}

	private void OnRandomPressed()
	{
		SetRandomSeed();
		_seedSpin.Value = Seed;
		GenerateWorld();
	}

	private void SetRandomSeed()
	{
		var rng = new Godot.RandomNumberGenerator();
		rng.Randomize();
		Seed = rng.RandiRange(int.MinValue, int.MaxValue);
	}

	private void RandomizeReliefExaggeration()
	{
		var rng = new Godot.RandomNumberGenerator();
		rng.Seed = (ulong)(uint)(Seed ^ unchecked((int)0x5bd1e995));
		_currentReliefExaggeration = rng.RandfRange(ReliefExaggerationMin, ReliefExaggerationMax);
	}

	private void OnSeaLevelChanged(double value)
	{
		SeaLevel = Mathf.Clamp((float)value, 0.1f, 0.9f);
		UpdateLabels();
		GenerateWorld();
	}

	private void OnHeatChanged(double value)
	{
		HeatFactor = Mathf.Clamp((float)value, 0.01f, 1f);
		UpdateLabels();
		GenerateWorld();
	}

	private void OnErosionChanged(double value)
	{
		ErosionIterations = Mathf.Clamp((int)Mathf.Round((float)value), 0, 20);
		UpdateLabels();
		GenerateWorld();
	}

	private void OnRiverDensityChanged(double value)
	{
		RiverDensity = Mathf.Clamp((float)value, 0.4f, 2.5f);
		SaveAdvancedSettings();
		UpdateLabels();
		GenerateWorld();
	}

	private void UpdateRiverDensityControlState()
	{
		var isEnabled = EnableRivers;
		_riverDensitySlider.Editable = isEnabled;
		_riverDensitySlider.Modulate = isEnabled
			? Colors.White
			: new Color(0.65f, 0.70f, 0.78f, 0.72f);
		_riverDensityValue.Modulate = isEnabled
			? Colors.White
			: new Color(0.68f, 0.73f, 0.80f, 0.9f);
	}

	private void UpdateRiverLayerAvailability()
	{
		const int riverLayerId = (int)MapLayer.Rivers;
		var riverEnabled = EnableRivers;

		var riverItemIndex = _layerOption.GetItemIndex(riverLayerId);
		if (riverItemIndex >= 0)
		{
			_layerOption.SetItemDisabled(riverItemIndex, !riverEnabled);
		}

		if (_layerButtonsById.TryGetValue(riverLayerId, out var riverButton))
		{
			riverButton.Disabled = !riverEnabled;
		}

		if (!riverEnabled && _layerOption.GetSelectedId() == riverLayerId)
		{
			SelectLayerById((int)MapLayer.Satellite);
			return;
		}

		UpdateLayerQuickButtons();
	}

	private void OnWindArrowDensityChanged(double value)
	{
		WindArrowDensity = Mathf.Clamp((float)value, 0.5f, 2.5f);
		SaveAdvancedSettings();
		UpdateLabels();
		if (GetCurrentLayer() == MapLayer.Wind)
		{
			RedrawCurrentLayer();
		}
	}

	private void OnBasinSensitivityChanged(double value)
	{
		BasinSensitivity = Mathf.Clamp((float)value, 0.5f, 2.0f);
		SaveAdvancedSettings();
		UpdateLabels();
		var layer = GetCurrentLayer();
		if (layer == MapLayer.Biomes || layer == MapLayer.Landform)
		{
			RedrawCurrentLayer();
		}
	}

	private void OnInteriorReliefChanged(double value)
	{
		_interiorRelief = Mathf.Clamp((float)value, 0.5f, 2.0f);
		SyncMountainPresetFromCurrentValues();
		SaveAdvancedSettings();
		UpdateLabels();
		GenerateWorld();
	}

	private void OnOrogenyStrengthChanged(double value)
	{
		_orogenyStrength = Mathf.Clamp((float)value, 0.5f, 2.5f);
		SyncMountainPresetFromCurrentValues();
		SaveAdvancedSettings();
		UpdateLabels();
		GenerateWorld();
	}

	private void OnSubductionArcRatioChanged(double value)
	{
		_subductionArcRatio = Mathf.Clamp((float)value, 0.2f, 1.0f);
		SyncMountainPresetFromCurrentValues();
		SaveAdvancedSettings();
		UpdateLabels();
		GenerateWorld();
	}

	private void OnContinentalAgeChanged(double value)
	{
		_continentalAge = Mathf.Clamp((int)Mathf.Round((float)value), 0, 100);
		SyncMountainPresetFromCurrentValues();
		SaveAdvancedSettings();
		UpdateLabels();
		GenerateWorld();
	}

	private void OnMagicDensityChanged(double value)
	{
		_magicDensity = Mathf.Clamp((int)Mathf.Round((float)value), 0, 100);
		InvalidateEcologyCaches();
		UpdateLabels();
		UpdateLorePanel();
		SaveAdvancedSettings();
	}

	private void OnCivilAggressionChanged(double value)
	{
		_civilAggression = Mathf.Clamp((int)Mathf.Round((float)value), 0, 100);
		InvalidateEcologyCaches();
		UpdateLabels();
		UpdateLorePanel();
		SaveAdvancedSettings();
	}

	private void OnSpeciesDiversityChanged(double value)
	{
		_speciesDiversity = Mathf.Clamp((int)Mathf.Round((float)value), 0, 100);
		InvalidateEcologyCaches();
		UpdateLabels();
		UpdateLorePanel();
		SaveAdvancedSettings();
	}

	private void OnTimelineChanged(double value)
	{
		_currentEpoch = Mathf.Clamp((int)Mathf.Round((float)value), 0, MaxEpoch);
		_selectedTimelineEventEpoch = _currentEpoch;
		InvalidateEcologyCaches();
		UpdateLabels();
		UpdateLorePanel();
		SaveAdvancedSettings();
	}

	private void OnPrevEpochPressed()
	{
		MoveTimelineEventCursor(-1);
	}

	private void OnNextEpochPressed()
	{
		MoveTimelineEventCursor(1);
	}

	private void MoveTimelineEventCursor(int direction)
	{
		if (_primaryWorld == null)
		{
			ApplyEpochFromReplayCursor(Mathf.Clamp(_currentEpoch + direction, 0, MaxEpoch));
			return;
		}

		EnsureCivilizationSimulation(_primaryWorld);
		var events = _primaryWorld.CivilizationSimulation?.RecentEvents ?? Array.Empty<CivilizationEpochEvent>();
		if (events.Length == 0)
		{
			ApplyEpochFromReplayCursor(Mathf.Clamp(_currentEpoch + direction, 0, MaxEpoch));
			return;
		}

		var currentIndex = ResolveTimelineEventIndex(events, _selectedTimelineEventEpoch >= 0 ? _selectedTimelineEventEpoch : _currentEpoch);
		var nextIndex = Mathf.Clamp(currentIndex + direction, 0, events.Length - 1);
		_selectedTimelineEventEpoch = events[nextIndex].Epoch;
		ApplyEpochFromReplayCursor(_selectedTimelineEventEpoch);
	}

	private void ApplyEpochFromReplayCursor(int targetEpoch)
	{
		var clampedEpoch = Mathf.Clamp(targetEpoch, 0, MaxEpoch);
		if (clampedEpoch == _currentEpoch)
		{
			UpdateLabels();
			RedrawCurrentLayer();
			SaveAdvancedSettings();
			return;
		}

		_currentEpoch = clampedEpoch;
		InvalidateEcologyCaches();
		UpdateLabels();
		UpdateLorePanel();
		SaveAdvancedSettings();
	}

	private void InvalidateEcologyCaches()
	{
		if (_primaryWorld != null)
		{
			_primaryWorld.EcologySimulation = null;
			_primaryWorld.EcologySignature = int.MinValue;
			_primaryWorld.CivilizationSimulation = null;
			_primaryWorld.CivilizationSignature = int.MinValue;
			_primaryWorld.LayerRenderCache.Remove(MapLayer.Ecology);
			_primaryWorld.LayerRenderCache.Remove(MapLayer.Civilization);
			_primaryWorld.LayerRenderCache.Remove(MapLayer.TradeRoutes);
		}

		if (_compareWorld != null)
		{
			_compareWorld.EcologySimulation = null;
			_compareWorld.EcologySignature = int.MinValue;
			_compareWorld.CivilizationSimulation = null;
			_compareWorld.CivilizationSignature = int.MinValue;
			_compareWorld.LayerRenderCache.Remove(MapLayer.Ecology);
			_compareWorld.LayerRenderCache.Remove(MapLayer.Civilization);
			_compareWorld.LayerRenderCache.Remove(MapLayer.TradeRoutes);
		}

		var layer = GetCurrentLayer();
		if (layer == MapLayer.Ecology || layer == MapLayer.Civilization || layer == MapLayer.TradeRoutes)
		{
			RedrawCurrentLayer();
		}
	}

	private void OnResetAdvancedSettingsPressed()
	{
		_resetAdvancedConfirmDialog.PopupCentered();
	}

	private void ResetAdvancedSettingsConfirmed()
	{
		ApplyDefaultAdvancedSettings();
		SaveAdvancedSettings();
		UpdateLabels();
		GenerateWorld();
	}

	private void ApplyDefaultAdvancedSettings()
	{
		EnableRivers = DefaultEnableRivers;
		RiverDensity = DefaultRiverDensity;
		WindArrowDensity = DefaultWindArrowDensity;
		BasinSensitivity = DefaultBasinSensitivity;
		_interiorRelief = DefaultInteriorRelief;
		_orogenyStrength = DefaultOrogenyStrength;
		_subductionArcRatio = DefaultSubductionArcRatio;
		_continentalAge = DefaultContinentalAge;
		_mountainPresetId = MountainPresetId.EarthLike;
		_elevationStyle = DefaultElevationStyle;
		_magicDensity = DefaultMagicDensity;
		_civilAggression = DefaultCivilAggression;
		_speciesDiversity = DefaultSpeciesDiversity;
		_currentEpoch = DefaultEpoch;
		_selectedTimelineEventEpoch = _currentEpoch;
		_mountainControlExpanded = true;
		_mapMode = MapMode.Geographic;

		_riverToggle.ButtonPressed = EnableRivers;
		_riverDensitySlider.SetValueNoSignal(RiverDensity);
		_windArrowDensitySlider.SetValueNoSignal(WindArrowDensity);
		_basinSensitivitySlider.SetValueNoSignal(BasinSensitivity);
		_interiorReliefSlider.SetValueNoSignal(_interiorRelief);
		_orogenyStrengthSlider.SetValueNoSignal(_orogenyStrength);
		_subductionArcRatioSlider.SetValueNoSignal(_subductionArcRatio);
		_continentalAgeSlider.SetValueNoSignal(_continentalAge);
		SelectMountainPresetOption(_mountainPresetId);
		_magicSlider.SetValueNoSignal(_magicDensity);
		_aggressionSlider.SetValueNoSignal(_civilAggression);
		_diversitySlider.SetValueNoSignal(_speciesDiversity);
		_timelineSlider.SetValueNoSignal(_currentEpoch);
		SelectElevationStyleOption(_elevationStyle);
		SelectMapModeOption(_mapMode);

		UpdateRiverDensityControlState();
		UpdateRiverLayerAvailability();
		UpdateLorePanel();
	}

	private void LoadAdvancedSettings()
	{
		var config = new ConfigFile();
		if (config.Load(AdvancedSettingsPath) != Error.Ok)
		{
			return;
		}

		EnableRivers = (bool)config.GetValue(AdvancedSettingsSection, "enable_rivers", DefaultEnableRivers);
		RiverDensity = Mathf.Clamp((float)(double)config.GetValue(AdvancedSettingsSection, "river_density", (double)DefaultRiverDensity), 0.4f, 2.5f);
		WindArrowDensity = Mathf.Clamp((float)(double)config.GetValue(AdvancedSettingsSection, "wind_arrow_density", (double)DefaultWindArrowDensity), 0.5f, 2.5f);
		BasinSensitivity = Mathf.Clamp((float)(double)config.GetValue(AdvancedSettingsSection, "basin_sensitivity", (double)DefaultBasinSensitivity), 0.5f, 2.0f);
		_interiorRelief = Mathf.Clamp((float)(double)config.GetValue(AdvancedSettingsSection, "interior_relief", (double)DefaultInteriorRelief), 0.5f, 2.0f);
		_orogenyStrength = Mathf.Clamp((float)(double)config.GetValue(AdvancedSettingsSection, "orogeny_strength", (double)DefaultOrogenyStrength), 0.5f, 2.5f);
		_subductionArcRatio = Mathf.Clamp((float)(double)config.GetValue(AdvancedSettingsSection, "subduction_arc_ratio", (double)DefaultSubductionArcRatio), 0.2f, 1.0f);
		_continentalAge = Mathf.Clamp((int)(long)config.GetValue(AdvancedSettingsSection, "continental_age", (long)DefaultContinentalAge), 0, 100);
		var defaultMountainPresetId = (long)ResolveMountainPresetIdFromCurrentValues();
		var mountainPresetId = (int)(long)config.GetValue(AdvancedSettingsSection, "mountain_preset", defaultMountainPresetId);
		_mountainPresetId = Enum.IsDefined(typeof(MountainPresetId), mountainPresetId)
			? (MountainPresetId)mountainPresetId
			: ResolveMountainPresetIdFromCurrentValues();

		var styleId = (int)(long)config.GetValue(AdvancedSettingsSection, "elevation_style", (long)DefaultElevationStyle);
		_elevationStyle = Enum.IsDefined(typeof(ElevationStyle), styleId)
			? (ElevationStyle)styleId
			: DefaultElevationStyle;

		_preferredLayerId = (int)(long)config.GetValue(AdvancedSettingsSection, "selected_layer", (long)_preferredLayerId);
		_preferredAdvancedPanelVisible = (bool)config.GetValue(AdvancedSettingsSection, "advanced_panel_visible", _preferredAdvancedPanelVisible);
		_mountainControlExpanded = (bool)config.GetValue(AdvancedSettingsSection, "mountain_control_expanded", _mountainControlExpanded);
		_magicDensity = Mathf.Clamp((int)(long)config.GetValue(AdvancedSettingsSection, "magic_density", (long)DefaultMagicDensity), 0, 100);
		_civilAggression = Mathf.Clamp((int)(long)config.GetValue(AdvancedSettingsSection, "civil_aggression", (long)DefaultCivilAggression), 0, 100);
		_speciesDiversity = Mathf.Clamp((int)(long)config.GetValue(AdvancedSettingsSection, "species_diversity", (long)DefaultSpeciesDiversity), 0, 100);
		_currentEpoch = Mathf.Clamp((int)(long)config.GetValue(AdvancedSettingsSection, "timeline_epoch", (long)DefaultEpoch), 0, MaxEpoch);
		_selectedTimelineEventEpoch = _currentEpoch;
		var mapModeId = (int)(long)config.GetValue(AdvancedSettingsSection, "map_mode", (long)MapMode.Geographic);
		_mapMode = Enum.IsDefined(typeof(MapMode), mapModeId)
			? (MapMode)mapModeId
			: MapMode.Geographic;
		_lastArchivePath = (string)config.GetValue(ArchiveSection, LastArchivePathKey, string.Empty);

		if (config.HasSectionKey(PerformanceSection, PerformanceCpuScoreKey))
		{
			_cpuPerformanceScore = ClampDouble((double)config.GetValue(PerformanceSection, PerformanceCpuScoreKey, 1.0), MinCpuPerformanceScore, MaxCpuPerformanceScore);
			_performanceSampleReady = true;
		}

		if (config.HasSectionKey(PerformanceSection, PerformanceSecondsPerUnitKey))
		{
			_secondsPerWorkUnit = ClampDouble((double)config.GetValue(PerformanceSection, PerformanceSecondsPerUnitKey, DefaultSecondsPerWorkUnit), MinSecondsPerWorkUnit, MaxSecondsPerWorkUnit);
			_hasHistoricalThroughput = true;
		}
	}

	private void SaveAdvancedSettings()
	{
		var config = new ConfigFile();
		_ = config.Load(AdvancedSettingsPath);

		config.SetValue(AdvancedSettingsSection, "enable_rivers", EnableRivers);
		config.SetValue(AdvancedSettingsSection, "river_density", (double)RiverDensity);
		config.SetValue(AdvancedSettingsSection, "wind_arrow_density", (double)WindArrowDensity);
		config.SetValue(AdvancedSettingsSection, "basin_sensitivity", (double)BasinSensitivity);
		config.SetValue(AdvancedSettingsSection, "interior_relief", (double)_interiorRelief);
		config.SetValue(AdvancedSettingsSection, "orogeny_strength", (double)_orogenyStrength);
		config.SetValue(AdvancedSettingsSection, "subduction_arc_ratio", (double)_subductionArcRatio);
		config.SetValue(AdvancedSettingsSection, "continental_age", (long)_continentalAge);
		config.SetValue(AdvancedSettingsSection, "mountain_preset", (long)_mountainPresetId);
		config.SetValue(AdvancedSettingsSection, "elevation_style", (long)_elevationStyle);
		config.SetValue(AdvancedSettingsSection, "selected_layer", (long)_layerOption.GetSelectedId());
		config.SetValue(AdvancedSettingsSection, "advanced_panel_visible", _advancedSettingsPanel.Visible);
		config.SetValue(AdvancedSettingsSection, "mountain_control_expanded", _mountainControlExpanded);
		config.SetValue(AdvancedSettingsSection, "magic_density", (long)_magicDensity);
		config.SetValue(AdvancedSettingsSection, "civil_aggression", (long)_civilAggression);
		config.SetValue(AdvancedSettingsSection, "species_diversity", (long)_speciesDiversity);
		config.SetValue(AdvancedSettingsSection, "timeline_epoch", (long)_currentEpoch);
		config.SetValue(AdvancedSettingsSection, "map_mode", (long)_mapMode);
		config.SetValue(ArchiveSection, LastArchivePathKey, _lastArchivePath);
		config.SetValue(PerformanceSection, PerformanceCpuScoreKey, _cpuPerformanceScore);
		config.SetValue(PerformanceSection, PerformanceSecondsPerUnitKey, _secondsPerWorkUnit);

		_ = config.Save(AdvancedSettingsPath);
	}

	private void UpdateLabels()
	{
		_plateValue.Text = PlateCount.ToString();
		_windValue.Text = WindCellCount.ToString();
		_seaLevelValue.Text = SeaLevel.ToString("0.00");
		_heatValue.Text = HeatFactor.ToString("0.00");
		_erosionValue.Text = ErosionIterations.ToString();
		_riverDensityValue.Text = RiverDensity.ToString("0.00");
		_windArrowDensityValue.Text = WindArrowDensity.ToString("0.00");
		_basinSensitivityValue.Text = BasinSensitivity.ToString("0.00");
		_interiorReliefValue.Text = _interiorRelief.ToString("0.00");
		_orogenyStrengthValue.Text = _orogenyStrength.ToString("0.00");
		_subductionArcRatioValue.Text = _subductionArcRatio.ToString("0.00");
		_continentalAgeValue.Text = _continentalAge.ToString();
		UpdateMountainControlSummary();
		_magicValue.Text = _magicDensity.ToString();
		_aggressionValue.Text = _civilAggression.ToString();
		_diversityValue.Text = _speciesDiversity.ToString();
		_epochLabel.Text = $"第 {_currentEpoch} 纪元";
		if (Mathf.Abs((float)_timelineSlider.Value - _currentEpoch) > 0.01f)
		{
			_timelineSlider.SetValueNoSignal(_currentEpoch);
		}
	}

	private void GenerateWorld()
	{
		if (_isGenerating)
		{
			_pendingRegenerate = true;
			return;
		}

		_ = GenerateWorldAsync();
	}

	private async Task GenerateWorldAsync()
	{
		_isGenerating = true;
		_pendingRegenerate = false;
		_generationStartedMsec = Time.GetTicksMsec();
		_progressOverlay.Visible = true;
		RandomizeReliefExaggeration();
		var generationSucceeded = false;
		var generatedFromScratch = false;
		var generationCacheKey = BuildWorldGenerationCacheKey();

		try
		{
			if (TryGetWorldGenerationCache(generationCacheKey, out var cachedPrimary, out var cachedCompare))
			{
				_primaryWorld = cachedPrimary;
				_compareWorld = _compareMode ? cachedCompare : null;
				await SetProgressAsync(92f, "读取缓存");
				await SetProgressAsync(97f, "渲染中");
				RedrawCurrentLayer();
				await SetProgressAsync(100f, "完成（缓存）");
				generationSucceeded = true;
				return;
			}

			if (!_performanceSampleReady)
			{
				await SetProgressAsync(1f, "准备中（性能检测）");
			}

			await EnsurePerformanceSampleAsync();
			_currentGenerationWorkUnits = EstimateGenerationWorkUnits();
			_predictedTotalSeconds = Math.Max(_currentGenerationWorkUnits * _secondsPerWorkUnit, 0.1);

			await SetProgressAsync(2f, IsHighInfoPointSelected() ? "准备中（高地图信息）" : "准备中");

			if (_compareMode)
			{
				_primaryWorld = await BuildWorldAsync(_tuning, "A组", 4f, 48f);
				_compareWorld = await BuildWorldAsync(GetAlternateTuning(_tuning), "B组", 50f, 94f);
			}
			else
			{
				_primaryWorld = await BuildWorldAsync(_tuning, "主世界", 4f, 94f);
				_compareWorld = null;
			}

			StoreWorldGenerationCache(generationCacheKey, _primaryWorld, _compareWorld);
			generatedFromScratch = true;

			await SetProgressAsync(97f, "渲染中");
			RedrawCurrentLayer();
			await SetProgressAsync(100f, "完成");
			generationSucceeded = true;
		}
		finally
		{
			if (generationSucceeded && generatedFromScratch)
			{
				RecordGenerationThroughput();
			}

			_isGenerating = false;

			if (_pendingRegenerate)
			{
				_pendingRegenerate = false;
				GenerateWorld();
			}
			else
			{
				_progressOverlay.Visible = false;
			}
		}
	}

	private string BuildWorldGenerationCacheKey()
	{
		var seaLevelQuantized = Mathf.RoundToInt(SeaLevel * 10000f);
		var heatFactorQuantized = Mathf.RoundToInt(HeatFactor * 10000f);
		var riverDensityQuantized = Mathf.RoundToInt(RiverDensity * 10000f);
		var oceanicRatioQuantized = Mathf.RoundToInt(_terrainOceanicRatio * 10000f);
		var continentBiasQuantized = Mathf.RoundToInt(_terrainContinentBias * 10000f);
		var interiorReliefQuantized = Mathf.RoundToInt(_interiorRelief * 10000f);
		var orogenyStrengthQuantized = Mathf.RoundToInt(_orogenyStrength * 10000f);
		var subductionArcRatioQuantized = Mathf.RoundToInt(_subductionArcRatio * 10000f);
		var continentalAgeQuantized = Mathf.Clamp(_continentalAge, 0, 100);
		var deepOceanFactorQuantized = Mathf.RoundToInt(_tuning.DeepOceanFactor * 10000f);
		var coastBandQuantized = Mathf.RoundToInt(_tuning.CoastBand * 10000f);
		var mountainThresholdQuantized = Mathf.RoundToInt(_tuning.MountainThreshold * 10000f);
		var riverSourceElevationQuantized = Mathf.RoundToInt(_tuning.RiverSourceElevationThreshold * 10000f);
		var riverSourceMoistureQuantized = Mathf.RoundToInt(_tuning.RiverSourceMoistureThreshold * 10000f);
		var riverSourceChanceQuantized = Mathf.RoundToInt(_tuning.RiverSourceChance * 1_000_000f);

		return $"ver:{WorldGenerationAlgorithmVersion}|{MapWidth}x{MapHeight}|seed:{Seed}|plates:{PlateCount}|wind:{WindCellCount}|sea:{seaLevelQuantized}|heat:{heatFactorQuantized}|moIter:{MoistureIterations}|erosion:{ErosionIterations}|rivers:{(EnableRivers ? 1 : 0)}|riverDensity:{riverDensityQuantized}|oceanic:{oceanicRatioQuantized}|continentBias:{continentBiasQuantized}|interiorRelief:{interiorReliefQuantized}|orogeny:{orogenyStrengthQuantized}|subductionArc:{subductionArcRatioQuantized}|age:{continentalAgeQuantized}|morph:{(int)_terrainMorphology}|continentCount:{_continentCount}|tuning:{_tuning.Name}|deepOcean:{deepOceanFactorQuantized}|coast:{coastBandQuantized}|mountain:{mountainThresholdQuantized}|riverSrcEl:{riverSourceElevationQuantized}|riverSrcMo:{riverSourceMoistureQuantized}|riverSrcChance:{riverSourceChanceQuantized}|compare:{(_compareMode ? 1 : 0)}";
	}

	private bool TryGetWorldGenerationCache(string cacheKey, out GeneratedWorldData primaryWorld, out GeneratedWorldData? compareWorld)
	{
		if (_worldGenerationCache.TryGetValue(cacheKey, out var entry))
		{
			entry.LastAccessTick = ++_worldCacheAccessCounter;
			primaryWorld = entry.PrimaryWorld;
			compareWorld = entry.CompareWorld;
			return true;
		}

		if (TryLoadWorldGenerationCacheFromDisk(cacheKey, out primaryWorld, out compareWorld))
		{
			StoreWorldGenerationCache(cacheKey, primaryWorld, compareWorld, persistToDisk: false);
			return true;
		}

		primaryWorld = null!;
		compareWorld = null;
		return false;
	}

	private void StoreWorldGenerationCache(string cacheKey, GeneratedWorldData primaryWorld, GeneratedWorldData? compareWorld, bool persistToDisk = true)
	{
		var estimatedCells = (long)primaryWorld.Stats.Width * primaryWorld.Stats.Height;
		if (compareWorld != null)
		{
			estimatedCells += (long)compareWorld.Stats.Width * compareWorld.Stats.Height;
		}

		_worldGenerationCache[cacheKey] = new WorldGenerationCacheEntry
		{
			Key = cacheKey,
			PrimaryWorld = primaryWorld,
			CompareWorld = compareWorld,
			EstimatedCells = estimatedCells,
			LastAccessTick = ++_worldCacheAccessCounter
		};

		while (_worldGenerationCache.Count > WorldGenerationCacheCapacity || GetWorldCacheTotalCells() > WorldGenerationCacheMaxCells)
		{
			var oldestKey = string.Empty;
			var oldestTick = long.MaxValue;

			foreach (var pair in _worldGenerationCache)
			{
				if (pair.Value.LastAccessTick >= oldestTick)
				{
					continue;
				}

				oldestTick = pair.Value.LastAccessTick;
				oldestKey = pair.Key;
			}

			if (string.IsNullOrEmpty(oldestKey))
			{
				break;
			}

			_worldGenerationCache.Remove(oldestKey);
		}

		if (persistToDisk)
		{
			SaveWorldGenerationCacheToDisk(cacheKey, primaryWorld, compareWorld);
		}

		RefreshCacheStatsLabel();
	}

	private string BuildAutoCacheDirectoryPath()
	{
		return ProjectSettings.GlobalizePath($"user://{CacheDirectoryName}/{CacheDataDirectoryName}");
	}

	private string BuildArchiveDirectoryPath()
	{
		return ProjectSettings.GlobalizePath($"user://{CacheDirectoryName}/{ArchiveDataDirectoryName}");
	}

	private string BuildCacheFilePath(string cacheKey)
	{
		var bytes = CryptoSha256.HashData(Encoding.UTF8.GetBytes(cacheKey));
		var fileHash = Convert.ToHexString(bytes).ToLowerInvariant();
		return IOPath.Combine(BuildAutoCacheDirectoryPath(), fileHash + CacheFileExtension);
	}

	private void SaveWorldGenerationCacheToDisk(string cacheKey, GeneratedWorldData primaryWorld, GeneratedWorldData? compareWorld)
	{
		try
		{
			var cacheDir = BuildAutoCacheDirectoryPath();
			IODirectory.CreateDirectory(cacheDir);

			var payload = BuildPersistedWorldCacheEntry(cacheKey, primaryWorld, compareWorld);
			var jsonText = Json.Stringify(ConvertPersistedCacheToGodotDictionary(payload));
			IOFile.WriteAllText(BuildCacheFilePath(cacheKey), jsonText, Encoding.UTF8);
		}
		catch
		{
		}
	}

	private bool TryReadPersistedCacheEntryFromFile(string filePath, out PersistedWorldCacheEntry payload)
	{
		if (!IOFile.Exists(filePath))
		{
			payload = null!;
			return false;
		}

		try
		{
			var text = IOFile.ReadAllText(filePath, Encoding.UTF8);
			var parsed = Json.ParseString(text);
			if (parsed.VariantType != Variant.Type.Dictionary)
			{
				payload = null!;
				return false;
			}

			var converted = ConvertGodotDictionaryToPersistedCache((Godot.Collections.Dictionary)parsed);
			if (converted == null || string.IsNullOrEmpty(converted.CacheKey))
			{
				payload = null!;
				return false;
			}

			payload = converted;
			return true;
		}
		catch
		{
			payload = null!;
			return false;
		}
	}

	private bool TryLoadWorldGenerationCacheFromDisk(string cacheKey, out GeneratedWorldData primaryWorld, out GeneratedWorldData? compareWorld)
	{
		var filePath = BuildCacheFilePath(cacheKey);

		if (!TryReadPersistedCacheEntryFromFile(filePath, out var payload) || payload.CacheKey != cacheKey)
		{
			primaryWorld = null!;
			compareWorld = null;
			return false;
		}

		primaryWorld = RestoreGeneratedWorldData(payload.Primary);
		compareWorld = payload.CompareMode && payload.Compare != null
			? RestoreGeneratedWorldData(payload.Compare)
			: null;

		return true;
	}

	private PersistedWorldCacheEntry BuildPersistedWorldCacheEntry(string cacheKey, GeneratedWorldData primaryWorld, GeneratedWorldData? compareWorld)
	{
		return new PersistedWorldCacheEntry
		{
			Version = 1,
			CacheKey = cacheKey,
			Seed = Seed,
			MapWidth = MapWidth,
			MapHeight = MapHeight,
			CompareMode = compareWorld != null,
			Primary = SerializeGeneratedWorld(primaryWorld),
			Compare = compareWorld != null ? SerializeGeneratedWorld(compareWorld) : null
		};
	}

	private PersistedWorldData SerializeGeneratedWorld(GeneratedWorldData world)
	{
		var width = world.Stats.Width;
		var height = world.Stats.Height;

		var boundaryTypes = new int[width, height];
		var biome = new int[width, height];
		var rock = new int[width, height];
		var ore = new int[width, height];

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				boundaryTypes[x, y] = (int)world.PlateResult.BoundaryTypes[x, y];
				biome[x, y] = (int)world.Biome[x, y];
				rock[x, y] = (int)world.Rock[x, y];
				ore[x, y] = (int)world.Ore[x, y];
			}
		}

		var cities = new PersistedCityInfo[world.Cities.Count];
		for (var i = 0; i < world.Cities.Count; i++)
		{
			var city = world.Cities[i];
			cities[i] = new PersistedCityInfo
			{
				X = city.Position.X,
				Y = city.Position.Y,
				Score = city.Score,
				Name = city.Name,
				Population = (int)city.Population
			};
		}

		var sites = new PersistedPlateSite[world.PlateResult.Sites.Count];
		for (var i = 0; i < world.PlateResult.Sites.Count; i++)
		{
			var site = world.PlateResult.Sites[i];
			sites[i] = new PersistedPlateSite
			{
				Id = site.Id,
				X = site.Position.X,
				Y = site.Position.Y,
				MotionX = site.Motion.X,
				MotionY = site.Motion.Y,
				IsOceanic = site.IsOceanic,
				BaseElevation = site.BaseElevation,
				ColorR = site.DebugColor.R,
				ColorG = site.DebugColor.G,
				ColorB = site.DebugColor.B,
				ColorA = site.DebugColor.A
			};
		}

		return new PersistedWorldData
		{
			TuningName = world.Tuning.Name,
			Stats = new PersistedWorldStats
			{
				Width = width,
				Height = height,
				CityCount = world.Stats.CityCount,
				OceanPercent = world.Stats.OceanPercent,
				RiverPercent = world.Stats.RiverPercent,
				AvgTemperature = world.Stats.AvgTemperature,
				AvgMoisture = world.Stats.AvgMoisture
			},
			PlateIds = world.PlateResult.PlateIds,
			BoundaryTypes = boundaryTypes,
			Elevation = world.Elevation,
			Temperature = world.Temperature,
			Moisture = world.Moisture,
			River = world.River,
			Wind = world.Wind,
			Biome = biome,
			Rock = rock,
			Ore = ore,
			Cities = cities,
			PlateSites = sites
		};
	}

	private GeneratedWorldData RestoreGeneratedWorldData(PersistedWorldData world)
	{
		var width = Math.Max(world.Stats.Width, 1);
		var height = Math.Max(world.Stats.Height, 1);

		var boundaryTypes = new PlateBoundaryType[width, height];
		var biome = new BiomeType[width, height];
		var rock = new RockType[width, height];
		var ore = new OreType[width, height];

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				boundaryTypes[x, y] = (PlateBoundaryType)world.BoundaryTypes[x, y];
				biome[x, y] = (BiomeType)world.Biome[x, y];
				rock[x, y] = (RockType)world.Rock[x, y];
				ore[x, y] = (OreType)world.Ore[x, y];
			}
		}

		var sites = new List<PlateSite>(Math.Max(world.PlateSites.Length, 1));
		for (var i = 0; i < world.PlateSites.Length; i++)
		{
			var site = world.PlateSites[i];
			sites.Add(new PlateSite
			{
				Id = site.Id,
				Position = new Vector2I(site.X, site.Y),
				Motion = new Vector2(site.MotionX, site.MotionY),
				IsOceanic = site.IsOceanic,
				BaseElevation = site.BaseElevation,
				DebugColor = new Color(site.ColorR, site.ColorG, site.ColorB, site.ColorA)
			});
		}

		if (sites.Count == 0)
		{
			sites.Add(new PlateSite
			{
				Id = 0,
				Position = new Vector2I(0, 0),
				Motion = new Vector2(1f, 0f),
				IsOceanic = true,
				BaseElevation = 0.5f,
				DebugColor = Colors.White
			});
		}

		var plateIds = world.PlateIds;
		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				if (plateIds[x, y] < 0 || plateIds[x, y] >= sites.Count)
				{
					plateIds[x, y] = 0;
				}
			}
		}

		var cities = new List<CityInfo>(world.Cities.Length);
		for (var i = 0; i < world.Cities.Length; i++)
		{
			var city = world.Cities[i];
			cities.Add(new CityInfo
			{
				Position = new Vector2I(city.X, city.Y),
				Score = city.Score,
				Name = city.Name,
				Population = Enum.IsDefined(typeof(CityPopulation), city.Population)
					? (CityPopulation)city.Population
					: CityPopulation.Medium
			});
		}

		var plateResult = new PlateResult
		{
			PlateIds = plateIds,
			PlateBaseElevation = Array2D.Create(width, height, 0f),
			BoundaryTypes = boundaryTypes,
			StressMap = new PlateStressCell[width, height],
			Neighbors = new List<PlateNeighborInfo>(),
			BorderPoints = new List<PlateEdgePoint>(),
			Sites = sites
		};

		var tuning = ResolveTuningByName(world.TuningName);
		var stats = new WorldStats
		{
			Width = width,
			Height = height,
			CityCount = world.Stats.CityCount,
			OceanPercent = world.Stats.OceanPercent,
			RiverPercent = world.Stats.RiverPercent,
			AvgTemperature = world.Stats.AvgTemperature,
			AvgMoisture = world.Stats.AvgMoisture
		};

		return new GeneratedWorldData
		{
			PlateResult = plateResult,
			Elevation = world.Elevation,
			Temperature = world.Temperature,
			Moisture = world.Moisture,
			Wind = world.Wind,
			River = world.River,
			Biome = biome,
			Rock = rock,
			Ore = ore,
			Cities = cities,
			Stats = stats,
			Tuning = tuning
		};
	}

	private static WorldTuning ResolveTuningByName(string name)
	{
		return name == "Legacy" ? WorldTuning.Legacy() : WorldTuning.Balanced();
	}

	private Godot.Collections.Dictionary ConvertPersistedCacheToGodotDictionary(PersistedWorldCacheEntry entry)
	{
		var result = new Godot.Collections.Dictionary
		{
			["version"] = entry.Version,
			["cache_key"] = entry.CacheKey,
			["seed"] = entry.Seed,
			["map_width"] = entry.MapWidth,
			["map_height"] = entry.MapHeight,
			["compare_mode"] = entry.CompareMode,
			["primary"] = ConvertPersistedWorldToGodotDictionary(entry.Primary)
		};

		if (entry.Compare != null)
		{
			result["compare"] = ConvertPersistedWorldToGodotDictionary(entry.Compare);
		}

		return result;
	}

	private static float[,] BuildOrogenyMask(PlateResult plateResult, float[,] elevation, int width, int height, float seaLevel, TerrainMorphology morphology, int seed, float subductionArcRatio)
	{
		var mask = new float[width, height];
		var arcRatio = Mathf.Clamp(subductionArcRatio, 0.2f, 1.0f);

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				if (elevation[x, y] <= seaLevel)
				{
					continue;
				}

				var boundaryType = plateResult.BoundaryTypes[x, y];
				float baseWeight;
				switch (boundaryType)
				{
					case PlateBoundaryType.Convergent:
						baseWeight = 1.0f;
						break;
					case PlateBoundaryType.Transform:
						baseWeight = 0.55f;
						break;
					case PlateBoundaryType.Divergent:
						baseWeight = 0.20f;
						break;
					default:
						baseWeight = 0f;
						break;
				}

				if (baseWeight <= 0f)
				{
					continue;
				}

				if (boundaryType == PlateBoundaryType.Convergent)
				{
					var arcNoise = HashNoise01(seed ^ unchecked((int)0x3c6ef35f), x, y);
					if (arcNoise > arcRatio)
					{
						baseWeight *= 0.36f;
					}
				}

				if (IsNearSeaEdge(x, y, elevation, width, height, seaLevel, 4))
				{
					baseWeight *= 1.26f;
				}

				baseWeight *= morphology switch
				{
					TerrainMorphology.Archipelago => 0.82f,
					TerrainMorphology.FracturedIslands => 0.78f,
					TerrainMorphology.ShallowFragments => 0.84f,
					_ => 1f
				};

				if (baseWeight > mask[x, y])
				{
					mask[x, y] = Mathf.Clamp(baseWeight, 0f, 1.2f);
				}
			}
		}

		return BlurMask(mask, width, height, 3);
	}

	private static bool IsNearSeaEdge(int x, int y, float[,] elevation, int width, int height, float seaLevel, int radius)
	{
		for (var oy = -radius; oy <= radius; oy++)
		{
			for (var ox = -radius; ox <= radius; ox++)
			{
				if (ox == 0 && oy == 0)
				{
					continue;
				}

				var nx = WrapX(x + ox, width);
				var ny = ClampY(y + oy, height);
				if (elevation[nx, ny] <= seaLevel)
				{
					return true;
				}
			}
		}

		return false;
	}

	private static float[,] BlurMask(float[,] source, int width, int height, int radius)
	{
		if (radius <= 0)
		{
			return source;
		}

		var blurred = new float[width, height];
		var sigma = Mathf.Max(radius * 0.65f, 0.5f);

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var accum = 0f;
				var weightSum = 0f;

				for (var oy = -radius; oy <= radius; oy++)
				{
					for (var ox = -radius; ox <= radius; ox++)
					{
						var nx = WrapX(x + ox, width);
						var ny = ClampY(y + oy, height);
						var distSq = ox * ox + oy * oy;
						var weight = Mathf.Exp(-distSq / (2f * sigma * sigma));

						accum += source[nx, ny] * weight;
						weightSum += weight;
					}
				}

				blurred[x, y] = weightSum > 0f ? accum / weightSum : source[x, y];
			}
		}

		return blurred;
	}

	private Godot.Collections.Dictionary ConvertPersistedWorldToGodotDictionary(PersistedWorldData world)
	{
		var width = Math.Max(world.Stats.Width, 1);
		var height = Math.Max(world.Stats.Height, 1);

		var cityArray = new Godot.Collections.Array();
		for (var i = 0; i < world.Cities.Length; i++)
		{
			var city = world.Cities[i];
			cityArray.Add(new Godot.Collections.Dictionary
			{
				["x"] = city.X,
				["y"] = city.Y,
				["score"] = city.Score,
				["name"] = city.Name,
				["population"] = city.Population
			});
		}

		var siteArray = new Godot.Collections.Array();
		for (var i = 0; i < world.PlateSites.Length; i++)
		{
			var site = world.PlateSites[i];
			siteArray.Add(new Godot.Collections.Dictionary
			{
				["id"] = site.Id,
				["x"] = site.X,
				["y"] = site.Y,
				["motion_x"] = site.MotionX,
				["motion_y"] = site.MotionY,
				["is_oceanic"] = site.IsOceanic,
				["base_elevation"] = site.BaseElevation,
				["color_r"] = site.ColorR,
				["color_g"] = site.ColorG,
				["color_b"] = site.ColorB,
				["color_a"] = site.ColorA
			});
		}

		return new Godot.Collections.Dictionary
		{
			["tuning_name"] = world.TuningName,
			["stats"] = new Godot.Collections.Dictionary
			{
				["width"] = world.Stats.Width,
				["height"] = world.Stats.Height,
				["city_count"] = world.Stats.CityCount,
				["ocean_percent"] = world.Stats.OceanPercent,
				["river_percent"] = world.Stats.RiverPercent,
				["avg_temperature"] = world.Stats.AvgTemperature,
				["avg_moisture"] = world.Stats.AvgMoisture
			},
			["plate_ids"] = FlattenInt2D(world.PlateIds, width, height),
			["boundary_types"] = FlattenInt2D(world.BoundaryTypes, width, height),
			["elevation"] = FlattenFloat2D(world.Elevation, width, height),
			["temperature"] = FlattenFloat2D(world.Temperature, width, height),
			["moisture"] = FlattenFloat2D(world.Moisture, width, height),
			["river"] = FlattenFloat2D(world.River, width, height),
			["wind"] = FlattenVector2_2D(world.Wind, width, height),
			["biome"] = FlattenInt2D(world.Biome, width, height),
			["rock"] = FlattenInt2D(world.Rock, width, height),
			["ore"] = FlattenInt2D(world.Ore, width, height),
			["cities"] = cityArray,
			["plate_sites"] = siteArray
		};
	}

	private PersistedWorldCacheEntry? ConvertGodotDictionaryToPersistedCache(Godot.Collections.Dictionary root)
	{
		if (!root.ContainsKey("cache_key") || !root.ContainsKey("primary"))
		{
			return null;
		}

		var entry = new PersistedWorldCacheEntry
		{
			Version = ReadIntFromDictionary(root, "version", 1),
			CacheKey = ReadStringFromDictionary(root, "cache_key", string.Empty),
			Seed = ReadIntFromDictionary(root, "seed", 0),
			MapWidth = ReadIntFromDictionary(root, "map_width", 0),
			MapHeight = ReadIntFromDictionary(root, "map_height", 0),
			CompareMode = ReadBoolFromDictionary(root, "compare_mode", false),
			Primary = ConvertGodotDictionaryToPersistedWorld((Godot.Collections.Dictionary)root["primary"])
		};

		if (root.ContainsKey("compare"))
		{
			entry.Compare = ConvertGodotDictionaryToPersistedWorld((Godot.Collections.Dictionary)root["compare"]);
		}

		return entry;
	}

	private PersistedWorldData ConvertGodotDictionaryToPersistedWorld(Godot.Collections.Dictionary dict)
	{
		var statsDict = (Godot.Collections.Dictionary)dict["stats"];
		var width = ReadIntFromDictionary(statsDict, "width", 1);
		var height = ReadIntFromDictionary(statsDict, "height", 1);

		var citiesRaw = ReadArrayFromDictionary(dict, "cities");
		var cities = new PersistedCityInfo[citiesRaw.Count];
		for (var i = 0; i < citiesRaw.Count; i++)
		{
			var cityDict = (Godot.Collections.Dictionary)citiesRaw[i];
			cities[i] = new PersistedCityInfo
			{
				X = ReadIntFromDictionary(cityDict, "x", 0),
				Y = ReadIntFromDictionary(cityDict, "y", 0),
				Score = ReadFloatFromDictionary(cityDict, "score", 0f),
				Name = ReadStringFromDictionary(cityDict, "name", string.Empty),
				Population = ReadIntFromDictionary(cityDict, "population", 1)
			};
		}

		var sitesRaw = ReadArrayFromDictionary(dict, "plate_sites");
		var sites = new PersistedPlateSite[sitesRaw.Count];
		for (var i = 0; i < sitesRaw.Count; i++)
		{
			var siteDict = (Godot.Collections.Dictionary)sitesRaw[i];
			sites[i] = new PersistedPlateSite
			{
				Id = ReadIntFromDictionary(siteDict, "id", i),
				X = ReadIntFromDictionary(siteDict, "x", 0),
				Y = ReadIntFromDictionary(siteDict, "y", 0),
				MotionX = ReadFloatFromDictionary(siteDict, "motion_x", 1f),
				MotionY = ReadFloatFromDictionary(siteDict, "motion_y", 0f),
				IsOceanic = ReadBoolFromDictionary(siteDict, "is_oceanic", false),
				BaseElevation = ReadFloatFromDictionary(siteDict, "base_elevation", 0.5f),
				ColorR = ReadFloatFromDictionary(siteDict, "color_r", 1f),
				ColorG = ReadFloatFromDictionary(siteDict, "color_g", 1f),
				ColorB = ReadFloatFromDictionary(siteDict, "color_b", 1f),
				ColorA = ReadFloatFromDictionary(siteDict, "color_a", 1f)
			};
		}

		return new PersistedWorldData
		{
			TuningName = ReadStringFromDictionary(dict, "tuning_name", "Balanced"),
			Stats = new PersistedWorldStats
			{
				Width = width,
				Height = height,
				CityCount = ReadIntFromDictionary(statsDict, "city_count", 0),
				OceanPercent = ReadFloatFromDictionary(statsDict, "ocean_percent", 0f),
				RiverPercent = ReadFloatFromDictionary(statsDict, "river_percent", 0f),
				AvgTemperature = ReadFloatFromDictionary(statsDict, "avg_temperature", 0f),
				AvgMoisture = ReadFloatFromDictionary(statsDict, "avg_moisture", 0f)
			},
			PlateIds = UnflattenInt2D((Godot.Collections.Array)dict["plate_ids"], width, height),
			BoundaryTypes = UnflattenInt2D((Godot.Collections.Array)dict["boundary_types"], width, height),
			Elevation = UnflattenFloat2D((Godot.Collections.Array)dict["elevation"], width, height),
			Temperature = UnflattenFloat2D((Godot.Collections.Array)dict["temperature"], width, height),
			Moisture = UnflattenFloat2D((Godot.Collections.Array)dict["moisture"], width, height),
			River = UnflattenFloat2D((Godot.Collections.Array)dict["river"], width, height),
			Wind = UnflattenVector2_2D((Godot.Collections.Array)dict["wind"], width, height),
			Biome = UnflattenInt2D((Godot.Collections.Array)dict["biome"], width, height),
			Rock = UnflattenInt2D((Godot.Collections.Array)dict["rock"], width, height),
			Ore = UnflattenInt2D((Godot.Collections.Array)dict["ore"], width, height),
			Cities = cities,
			PlateSites = sites
		};
	}

	private static Godot.Collections.Array FlattenInt2D(int[,] source, int width, int height)
	{
		var output = new Godot.Collections.Array();
		output.Resize(width * height);
		var index = 0;
		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				output[index++] = source[x, y];
			}
		}

		return output;
	}

	private static Godot.Collections.Array FlattenFloat2D(float[,] source, int width, int height)
	{
		var output = new Godot.Collections.Array();
		output.Resize(width * height);
		var index = 0;
		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				output[index++] = source[x, y];
			}
		}

		return output;
	}

	private static Godot.Collections.Array FlattenVector2_2D(Vector2[,] source, int width, int height)
	{
		var output = new Godot.Collections.Array();
		output.Resize(width * height * 2);
		var index = 0;
		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var value = source[x, y];
				output[index++] = value.X;
				output[index++] = value.Y;
			}
		}

		return output;
	}

	private static int[,] UnflattenInt2D(Godot.Collections.Array source, int width, int height)
	{
		var result = new int[width, height];
		var max = Math.Min(source.Count, width * height);
		for (var i = 0; i < max; i++)
		{
			var x = i % width;
			var y = i / width;
			result[x, y] = ConvertVariantToInt(source[i]);
		}

		return result;
	}

	private static float[,] UnflattenFloat2D(Godot.Collections.Array source, int width, int height)
	{
		var result = new float[width, height];
		var max = Math.Min(source.Count, width * height);
		for (var i = 0; i < max; i++)
		{
			var x = i % width;
			var y = i / width;
			result[x, y] = ConvertVariantToFloat(source[i]);
		}

		return result;
	}

	private static Vector2[,] UnflattenVector2_2D(Godot.Collections.Array source, int width, int height)
	{
		var result = new Vector2[width, height];
		var cellCount = Math.Min(source.Count / 2, width * height);
		for (var i = 0; i < cellCount; i++)
		{
			var x = i % width;
			var y = i / width;
			var index = i * 2;
			result[x, y] = new Vector2(ConvertVariantToFloat(source[index]), ConvertVariantToFloat(source[index + 1]));
		}

		return result;
	}

	private static int ReadIntFromDictionary(Godot.Collections.Dictionary dict, string key, int fallback)
	{
		if (!dict.ContainsKey(key))
		{
			return fallback;
		}

		return ConvertVariantToInt(dict[key]);
	}

	private static float ReadFloatFromDictionary(Godot.Collections.Dictionary dict, string key, float fallback)
	{
		if (!dict.ContainsKey(key))
		{
			return fallback;
		}

		return ConvertVariantToFloat(dict[key]);
	}

	private static bool ReadBoolFromDictionary(Godot.Collections.Dictionary dict, string key, bool fallback)
	{
		if (!dict.ContainsKey(key))
		{
			return fallback;
		}

		var value = dict[key];
		if (value.VariantType == Variant.Type.Bool)
		{
			return (bool)value;
		}

		return fallback;
	}

	private static string ReadStringFromDictionary(Godot.Collections.Dictionary dict, string key, string fallback)
	{
		if (!dict.ContainsKey(key))
		{
			return fallback;
		}

		var value = dict[key];
		if (value.VariantType == Variant.Type.String)
		{
			return (string)value;
		}

		return fallback;
	}

	private static Godot.Collections.Array ReadArrayFromDictionary(Godot.Collections.Dictionary dict, string key)
	{
		if (!dict.ContainsKey(key))
		{
			return new Godot.Collections.Array();
		}

		var value = dict[key];
		if (value.VariantType == Variant.Type.Array)
		{
			return (Godot.Collections.Array)value;
		}

		return new Godot.Collections.Array();
	}

	private static int ConvertVariantToInt(Variant value)
	{
		return value.VariantType switch
		{
			Variant.Type.Int => (int)(long)value,
			Variant.Type.Float => Mathf.RoundToInt((float)(double)value),
			Variant.Type.Bool => (bool)value ? 1 : 0,
			_ => 0
		};
	}

	private static float ConvertVariantToFloat(Variant value)
	{
		return value.VariantType switch
		{
			Variant.Type.Float => (float)(double)value,
			Variant.Type.Int => (long)value,
			_ => 0f
		};
	}

	private void OnSaveArchivePressed()
	{
		if (_isGenerating)
		{
			_infoLabel.Text = "正在生成中，稍后再保存存档。";
			return;
		}

		if (_primaryWorld == null)
		{
			_infoLabel.Text = "当前没有可保存的地图。";
			return;
		}

		try
		{
			var archiveDir = BuildArchiveDirectoryPath();
			IODirectory.CreateDirectory(archiveDir);

			var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
			var fileName = $"{ArchiveFilePrefix}{timestamp}_{Seed}_{MapWidth}x{MapHeight}{ArchiveFileExtension}";
			var archivePath = IOPath.Combine(archiveDir, fileName);

			var cacheKey = BuildWorldGenerationCacheKey();
			var payload = BuildPersistedWorldCacheEntry(cacheKey, _primaryWorld, _compareWorld);
			var jsonText = Json.Stringify(ConvertPersistedCacheToGodotDictionary(payload));
			IOFile.WriteAllText(archivePath, jsonText, Encoding.UTF8);

			_lastArchivePath = archivePath;
			SaveAdvancedSettings();
			SetupArchiveOptions();
			RefreshCacheStatsLabel();
			_infoLabel.Text = $"存档已保存：{fileName}";
		}
		catch
		{
			_infoLabel.Text = "存档保存失败（权限或磁盘异常）。";
		}
	}

	private void OnClearCachePressed()
	{
		if (_isGenerating)
		{
			_infoLabel.Text = "正在生成中，稍后再清理缓存。";
			return;
		}

		var memoryCount = _worldGenerationCache.Count;
		_worldGenerationCache.Clear();

		try
		{
			var cacheDir = BuildAutoCacheDirectoryPath();
			if (IODirectory.Exists(cacheDir))
			{
				IODirectory.Delete(cacheDir, true);
			}
		}
		catch
		{
			_infoLabel.Text = "自动缓存内存已清理，磁盘清理失败（权限或文件占用）。";
			RefreshCacheStatsLabel();
			return;
		}

		RefreshCacheStatsLabel();
		_infoLabel.Text = $"自动缓存已清理（内存 {memoryCount} 条，磁盘目录已删除）。";
	}

	private void LoadArchiveByPath(string archivePath)
	{
		if (_isGenerating)
		{
			_infoLabel.Text = "正在生成中，稍后再读取存档。";
			return;
		}

		if (!TryReadPersistedCacheEntryFromFile(archivePath, out var payload))
		{
			_infoLabel.Text = "读取存档失败（文件损坏或格式不兼容）。";
			return;
		}

		try
		{
			var primary = RestoreGeneratedWorldData(payload.Primary);
			GeneratedWorldData? compare = null;
			if (payload.CompareMode && payload.Compare != null)
			{
				compare = RestoreGeneratedWorldData(payload.Compare);
			}

			_primaryWorld = primary;
			_compareWorld = compare;
			_compareMode = compare != null;
			_compareToggle.ButtonPressed = _compareMode;
			_compareToggle.Visible = _compareMode;
			_compareToggle.Disabled = !_compareMode;

			Seed = payload.Seed;
			_seedSpin.Value = Seed;
			MapWidth = payload.MapWidth;
			MapHeight = payload.MapHeight;
			var mapSizeIndex = FindMapSizePresetIndex(MapWidth, MapHeight);
			if (mapSizeIndex >= 0)
			{
				_suppressMapSizeSelectionHandler = true;
				_mapSizeOption.Select(mapSizeIndex);
				_suppressMapSizeSelectionHandler = false;
				_lastConfirmedMapSizeIndex = mapSizeIndex;
			}

			StoreWorldGenerationCache(payload.CacheKey, primary, compare, persistToDisk: false);
			_lastArchivePath = archivePath;
			SaveAdvancedSettings();
			ShowInfoPointHint();
			RedrawCurrentLayer();
			RefreshCacheStatsLabel();
			_infoLabel.Text = $"已读取存档：{IOPath.GetFileName(archivePath)}";
		}
		catch
		{
			_infoLabel.Text = "读取存档失败（数据恢复异常）。";
		}
	}

	private void RefreshCacheStatsLabel()
	{
		if (_cacheStatsLabel == null)
		{
			return;
		}

		var memoryEntries = _worldGenerationCache.Count;
		long memoryBytes = 0;
		foreach (var pair in _worldGenerationCache)
		{
			memoryBytes += pair.Value.EstimatedCells * ApproxBytesPerCachedCell;
		}

		GetDirectoryStats(BuildAutoCacheDirectoryPath(), "*" + CacheFileExtension, out var autoCacheFiles, out var autoCacheBytes);
		GetDirectoryStats(BuildArchiveDirectoryPath(), "*" + ArchiveFileExtension, out var archiveFiles, out var archiveBytes);

		_cacheStatsLabel.Text = $"自动缓存 | 内存:{memoryEntries} 条（约 {FormatByteSize(memoryBytes)}）| 磁盘:{autoCacheFiles} 文件（{FormatByteSize(autoCacheBytes)}）\n存档:{archiveFiles} 份（{FormatByteSize(archiveBytes)}）";
	}

	private static void GetDirectoryStats(string directoryPath, string searchPattern, out int fileCount, out long totalBytes)
	{
		fileCount = 0;
		totalBytes = 0;

		if (!IODirectory.Exists(directoryPath))
		{
			return;
		}

		var files = IODirectory.GetFiles(directoryPath, searchPattern, System.IO.SearchOption.TopDirectoryOnly);
		fileCount = files.Length;
		for (var i = 0; i < files.Length; i++)
		{
			var info = new IOFileInfo(files[i]);
			totalBytes += info.Length;
		}
	}

	private static string FormatByteSize(long bytes)
	{
		if (bytes < 1024)
		{
			return $"{bytes} B";
		}

		var kb = bytes / 1024.0;
		if (kb < 1024.0)
		{
			return $"{kb:0.0} KB";
		}

		var mb = kb / 1024.0;
		if (mb < 1024.0)
		{
			return $"{mb:0.0} MB";
		}

		var gb = mb / 1024.0;
		return $"{gb:0.00} GB";
	}

	private long GetWorldCacheTotalCells()
	{
		long total = 0;
		foreach (var entry in _worldGenerationCache.Values)
		{
			total += entry.EstimatedCells;
		}

		return total;
	}

	private async Task<GeneratedWorldData> BuildWorldAsync(WorldTuning tuning, string label, float startProgress, float endProgress)
	{
		const int totalSteps = 10;
		var step = 0;

		var plateResult = await Task.Run(() => _plateGenerator.Generate(MapWidth, MapHeight, PlateCount, Seed, _terrainOceanicRatio));
		await SetBuildProgressAsync(label, "板块", ++step, totalSteps, startProgress, endProgress);

		var resourceTask = Task.Run(() => _resourceGenerator.Generate(MapWidth, MapHeight, Seed, plateResult.BoundaryTypes));

		var elevation = await Task.Run(() => _elevationGenerator.Generate(MapWidth, MapHeight, Seed, SeaLevel, plateResult));
		elevation = ApplyTerrainMorphologyMask(elevation, plateResult, MapWidth, MapHeight, SeaLevel, _terrainContinentBias, _interiorRelief, _orogenyStrength, _subductionArcRatio, _continentalAge, _terrainMorphology, Seed, _continentCount);
		await SetBuildProgressAsync(label, "地形", ++step, totalSteps, startProgress, endProgress);

		var waterLayer = Array2D.Create(MapWidth, MapHeight, 1f);
		var emptyRiverLayer = Array2D.Create(MapWidth, MapHeight, 0f);
		await Task.Run(() => _erosionSimulator.Run(MapWidth, MapHeight, ErosionIterations, elevation, waterLayer, emptyRiverLayer));
		var targetOceanRatio = MapSeaLevelToTargetOceanRatio(SeaLevel);
		elevation = NormalizeElevationForPipeline(elevation, MapWidth, MapHeight, SeaLevel, targetOceanRatio);
		await SetBuildProgressAsync(label, "侵蚀", ++step, totalSteps, startProgress, endProgress);

		var temperatureTask = Task.Run(() => _temperatureGenerator.Generate(MapWidth, MapHeight, Seed, elevation, HeatFactor));
		var windTask = Task.Run(() => _moistureGenerator.GenerateBaseWind(MapWidth, MapHeight, Seed, WindCellCount));
		var temperature = await temperatureTask;
		await SetBuildProgressAsync(label, "温度", ++step, totalSteps, startProgress, endProgress);

		var baseMoistureTask = Task.Run(() => _moistureGenerator.GenerateBaseMoisture(MapWidth, MapHeight, SeaLevel, elevation, temperature));
		var wind = await windTask;
		var baseMoisture = await baseMoistureTask;
		await SetBuildProgressAsync(label, "湿度基础", ++step, totalSteps, startProgress, endProgress);

		var moisture = await Task.Run(() => _moistureGenerator.DistributeMoisture(MapWidth, MapHeight, SeaLevel, elevation, baseMoisture, temperature, wind, MoistureIterations, Seed));
		await SetBuildProgressAsync(label, "湿度扩散", ++step, totalSteps, startProgress, endProgress);

		var river = EnableRivers
			? await Task.Run(() => _riverGenerator.Generate(MapWidth, MapHeight, Seed, SeaLevel, elevation, moisture, tuning, RiverDensity))
			: Array2D.Create(MapWidth, MapHeight, 0f);
		await SetBuildProgressAsync(label, EnableRivers ? "河流" : "河流关闭", ++step, totalSteps, startProgress, endProgress);

		var biome = await Task.Run(() => _biomeGenerator.Generate(MapWidth, MapHeight, SeaLevel, elevation, moisture, temperature, river, tuning));
		await SetBuildProgressAsync(label, "生物群系", ++step, totalSteps, startProgress, endProgress);

		var resource = await resourceTask;
		var cities = await Task.Run(() => _cityGenerator.Generate(MapWidth, MapHeight, Seed, SeaLevel, elevation, moisture, river, biome));
		await SetBuildProgressAsync(label, "资源与城市", ++step, totalSteps, startProgress, endProgress);

		var stats = await Task.Run(() => _statsCalculator.Calculate(MapWidth, MapHeight, biome, moisture, temperature, river, cities.Count));
		await SetBuildProgressAsync(label, "统计", ++step, totalSteps, startProgress, endProgress);

		return new GeneratedWorldData
		{
			PlateResult = plateResult,
			Elevation = elevation,
			Temperature = temperature,
			Moisture = moisture,
			Wind = wind,
			River = river,
			Biome = biome,
			Rock = resource.Rock,
			Ore = resource.Ore,
			Cities = cities,
			Stats = stats,
			Tuning = tuning
		};
	}

	private async Task SetBuildProgressAsync(string label, string stage, int step, int totalSteps, float startProgress, float endProgress)
	{
		var t = totalSteps <= 0 ? 1f : Mathf.Clamp((float)step / totalSteps, 0f, 1f);
		var value = Mathf.Lerp(startProgress, endProgress, t);
		await SetProgressAsync(value, $"{label}: {stage}");
	}

	private async Task SetProgressAsync(float value, string status)
	{
		var clampedValue = Mathf.Clamp(value, 0f, 100f);
		_generateProgress.Value = clampedValue;
		_progressStatus.Text = BuildProgressStatus(status, clampedValue);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private string BuildProgressStatus(string status, float progress)
	{
		if (progress <= 0f || progress >= 100f || _generationStartedMsec == 0)
		{
			return status;
		}

		var elapsedSeconds = Math.Max((Time.GetTicksMsec() - _generationStartedMsec) / 1000.0, 0.0);
		if (elapsedSeconds < 0.05 && _predictedTotalSeconds <= 0.1)
		{
			return $"{status} | 预计剩余 --";
		}

		var totalSeconds = EstimateTotalSeconds(progress, elapsedSeconds);
		var remainingSeconds = Math.Max(totalSeconds - elapsedSeconds, 0.0);
		var perfText = _performanceSampleReady ? $"性能x{_cpuPerformanceScore:0.00}" : "性能待测";
		return $"{status} | {perfText} | 总计约{FormatDuration(totalSeconds)} | 剩余{FormatDuration(remainingSeconds)}";
	}

	private async Task EnsurePerformanceSampleAsync()
	{
		if (!_performanceSampleReady)
		{
			var sampledScore = await Task.Run(SampleCpuPerformanceScore);
			_cpuPerformanceScore = ClampDouble(sampledScore, MinCpuPerformanceScore, MaxCpuPerformanceScore);
			_performanceSampleReady = true;

			if (!_hasHistoricalThroughput)
			{
				_secondsPerWorkUnit = ClampDouble(DefaultSecondsPerWorkUnit / _cpuPerformanceScore, MinSecondsPerWorkUnit, MaxSecondsPerWorkUnit);
			}

			SaveAdvancedSettings();
		}
	}

	private double EstimateGenerationWorkUnits()
	{
		var pixelMegas = (MapWidth * MapHeight) / 1_000_000.0;
		var perWorldUnits = pixelMegas *
			(5.1 +
			(PlateCount / 20.0) * 0.90 +
			(WindCellCount / 10.0) * 0.30 +
			ErosionIterations * 0.46 +
			MoistureIterations * 0.38 +
			(EnableRivers ? 0.90 + RiverDensity * 0.70 : 0.12));

		if (_elevationStyle == ElevationStyle.Topographic)
		{
			perWorldUnits *= 0.93;
		}

		var totalUnits = perWorldUnits * (_compareMode ? 2.03 : 1.0);
		totalUnits += ((OutputWidth * OutputHeight) / 1_000_000.0) * 0.18;

		return Math.Max(totalUnits, 0.05);
	}

	private double EstimateTotalSeconds(float progress, double elapsedSeconds)
	{
		var modelTotal = Math.Max(_predictedTotalSeconds, elapsedSeconds + 0.01);
		if (progress <= 0.01f)
		{
			return modelTotal;
		}

		var observedTotal = elapsedSeconds / Math.Max(progress / 100.0, 0.0001);
		var blend = Mathf.Clamp(progress / 100f, 0.20f, 0.88f);
		var blended = (modelTotal * (1.0 - blend)) + (observedTotal * blend);
		_predictedTotalSeconds = Math.Max(blended, elapsedSeconds);

		return _predictedTotalSeconds;
	}

	private void RecordGenerationThroughput()
	{
		if (_generationStartedMsec == 0 || _currentGenerationWorkUnits <= 0.0)
		{
			return;
		}

		var elapsedSeconds = Math.Max((Time.GetTicksMsec() - _generationStartedMsec) / 1000.0, 0.0);
		if (elapsedSeconds <= 0.0)
		{
			return;
		}

		var measuredSecondsPerUnit = ClampDouble(elapsedSeconds / _currentGenerationWorkUnits, MinSecondsPerWorkUnit, MaxSecondsPerWorkUnit);
		_secondsPerWorkUnit = _hasHistoricalThroughput
			? (_secondsPerWorkUnit * 0.70) + (measuredSecondsPerUnit * 0.30)
			: measuredSecondsPerUnit;
		_hasHistoricalThroughput = true;

		SaveAdvancedSettings();
	}

	private static double SampleCpuPerformanceScore()
	{
		const int sampleCount = 32768;
		const int rounds = 160;

		var buffer = new double[sampleCount];
		for (var i = 0; i < sampleCount; i++)
		{
			buffer[i] = (i + 1) * 0.0001;
		}

		var sw = Stopwatch.StartNew();
		double checksum = 0.0;
		for (var round = 0; round < rounds; round++)
		{
			for (var i = 0; i < sampleCount; i++)
			{
				var value = buffer[i];
				value = value * 1.0000013 + 0.61803398875;
				value = (Math.Sin(value) * 0.72) + (Math.Sqrt(Math.Abs(value) + 1.0) * 0.28);
				buffer[i] = value;
				checksum += value;
			}
		}
		sw.Stop();

		if (checksum < -1_000_000_000)
		{
			return 1.0;
		}

		var operationCount = sampleCount * rounds * 6.0;
		var seconds = Math.Max(sw.Elapsed.TotalSeconds, 0.001);
		var operationsPerSecond = operationCount / seconds;
		return operationsPerSecond / CpuBenchmarkBaselineScore;
	}

	private static double ClampDouble(double value, double min, double max)
	{
		if (value < min)
		{
			return min;
		}

		if (value > max)
		{
			return max;
		}

		return value;
	}

	private static string FormatDuration(double seconds)
	{
		var totalSeconds = (int)Math.Ceiling(seconds);
		if (totalSeconds < 60)
		{
			return $"{totalSeconds}秒";
		}

		var minutes = totalSeconds / 60;
		var remain = totalSeconds % 60;
		return remain == 0 ? $"{minutes}分" : $"{minutes}分{remain}秒";
	}

	private WorldTuning GetAlternateTuning(WorldTuning tuning)
	{
		return tuning.Name == "Legacy" ? WorldTuning.Balanced() : WorldTuning.Legacy();
	}

	private float[,] NormalizeElevationForPipeline(float[,] source, int width, int height, float seaLevel, float targetOceanRatio)
	{
		var samples = new float[width * height];
		var count = 0;

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var value = source[x, y];
				if (float.IsNaN(value) || float.IsInfinity(value))
				{
					continue;
				}

				samples[count++] = value;
			}
		}

		if (count <= 1)
		{
			return source;
		}

		Array.Sort(samples, 0, count);
		var lowIndex = Mathf.Clamp(Mathf.FloorToInt(count * 0.02f), 0, count - 1);
		var highIndex = Mathf.Clamp(Mathf.FloorToInt(count * 0.98f), lowIndex + 1, count - 1);
		var oceanIndex = Mathf.Clamp(
			Mathf.FloorToInt((count - 1) * Mathf.Clamp(targetOceanRatio, 0.02f, 0.98f)),
			lowIndex,
			highIndex - 1);

		var min = samples[lowIndex];
		var max = samples[highIndex];
		var oceanPivot = samples[oceanIndex];

		var lowerRange = Mathf.Max(oceanPivot - min, 0.00001f);
		var upperRange = Mathf.Max(max - oceanPivot, 0.00001f);
		var normalized = new float[width, height];

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var value = source[x, y];
				if (float.IsNaN(value) || float.IsInfinity(value))
				{
					normalized[x, y] = 0f;
					continue;
				}

				if (value <= oceanPivot)
				{
					var waterT = Mathf.Clamp((value - min) / lowerRange, 0f, 1f);
					normalized[x, y] = waterT * seaLevel;
					continue;
				}

				var landT = Mathf.Clamp((value - oceanPivot) / upperRange, 0f, 1f);
				normalized[x, y] = seaLevel + (1f - seaLevel) * Mathf.Pow(landT, 1.05f);
			}
		}

		return normalized;
	}

	private float[,] ApplyTerrainMorphologyMask(float[,] source, PlateResult plateResult, int width, int height, float seaLevel, float continentBias, float interiorRelief, float orogenyStrength, float subductionArcRatio, int continentalAge, TerrainMorphology morphology, int seed, int continentCount)
	{
		if (continentBias <= 0.001f && morphology == TerrainMorphology.Balanced)
		{
			return source;
		}

		var bias = Mathf.Clamp(continentBias, 0f, 1f);
		var relief = Mathf.Clamp(interiorRelief, 0.5f, 2.0f);
		var orogenyScale = Mathf.Clamp(orogenyStrength, 0.5f, 2.5f);
		var ageNorm = Mathf.Clamp(continentalAge / 100f, 0f, 1f);
		var ageRoughnessFactor = Mathf.Lerp(1.24f, 0.72f, ageNorm);
		var result = new float[width, height];
		var orogenyMask = BuildOrogenyMask(plateResult, source, width, height, seaLevel, morphology, seed, subductionArcRatio);

		var contourNoise = new FastNoiseLite
		{
			Seed = seed ^ unchecked((int)0x6f1d3a89),
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 1f
		};

		var fragmentNoise = new FastNoiseLite
		{
			Seed = seed ^ unchecked((int)0x3f84d5b5),
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 1f
		};

		var (shapePower, upliftMax, edgeDropMax, contourAmp, fragmentAmp) = morphology switch
		{
			TerrainMorphology.Supercontinent => (0.82f, 0.40f, 0.22f, 0.14f, 0.04f),
			TerrainMorphology.Continents => (1.12f, 0.30f, 0.20f, 0.18f, 0.12f),
			TerrainMorphology.Archipelago => (1.48f, 0.14f, 0.24f, 0.24f, 0.22f),
			TerrainMorphology.FracturedIslands => (1.65f, 0.11f, 0.27f, 0.28f, 0.30f),
			TerrainMorphology.ShallowFragments => (1.32f, 0.16f, 0.20f, 0.20f, 0.16f),
			TerrainMorphology.ColdContinent => (1.00f, 0.29f, 0.19f, 0.17f, 0.10f),
			TerrainMorphology.HotWasteland => (1.08f, 0.27f, 0.17f, 0.15f, 0.09f),
			_ => (1.20f, 0.24f, 0.16f, 0.16f, 0.08f)
		};

		for (var y = 0; y < height; y++)
		{
			var ny = 4f * y / Mathf.Max(height, 1);
			var py = height <= 1 ? 0f : (float)y / (height - 1);

			for (var x = 0; x < width; x++)
			{
				var px = width <= 1 ? 0f : (float)x / (width - 1);
				var radial = ComputeWrappedRadial(px, py, 0.5f, 0.5f);
				var lobeA = ComputeWrappedRadial(px, py, 0.34f, 0.56f);
				var lobeB = ComputeWrappedRadial(px, py, 0.68f, 0.45f);
				var lobeC = ComputeWrappedRadial(px, py, 0.18f, 0.42f);

				var nx = Mathf.Cos((x * 2f * Mathf.Pi) / Mathf.Max(width, 1));
				var nz = Mathf.Sin((x * 2f * Mathf.Pi) / Mathf.Max(width, 1));

				var morphologyBase = morphology switch
				{
					TerrainMorphology.Supercontinent => Mathf.Clamp(1f - 1.24f * radial, 0f, 1f),
					TerrainMorphology.Continents => BuildContinentsBase(radial, fragmentNoise, nx, ny, nz, px, py, continentCount, seed),
					TerrainMorphology.Archipelago => Mathf.Clamp(0.56f - 0.42f * radial, 0f, 1f),
					TerrainMorphology.FracturedIslands => Mathf.Clamp(0.52f - 0.34f * radial, 0f, 1f),
					TerrainMorphology.ShallowFragments => Mathf.Clamp(0.62f - 0.48f * radial, 0f, 1f),
					TerrainMorphology.ColdContinent => Mathf.Max(
						Mathf.Clamp(1f - 1.55f * radial, 0f, 1f),
						Mathf.Clamp(1f - 1.95f * lobeA, 0f, 1f) * 0.65f),
					TerrainMorphology.HotWasteland => Mathf.Max(
						Mathf.Clamp(1f - 1.62f * radial, 0f, 1f),
						Mathf.Clamp(1f - 2.10f * lobeC, 0f, 1f) * 0.45f),
					_ => Mathf.Clamp(1f - 1.45f * radial, 0f, 1f)
				};

				var contour = contourNoise.GetNoise3D(2.6f * nx, 2.6f * ny, 2.6f * nz);
				var fragments = fragmentNoise.GetNoise3D(6.2f * nx, 6.2f * ny, 6.2f * nz);

				var falloff = morphologyBase;
				falloff += contour * contourAmp * (0.55f + 0.45f * bias);
				falloff += fragments * fragmentAmp;
				falloff = Mathf.Clamp(falloff, 0f, 1f);
				falloff = Mathf.Pow(falloff, shapePower);

				var uplift = falloff * Mathf.Lerp(0.05f, upliftMax, bias);
				var edgeDrop = (1f - falloff) * Mathf.Lerp(0.02f, edgeDropMax, bias);
				var shifted = source[x, y] + uplift - edgeDrop;

				if (falloff > 0.48f)
				{
					var interiorMask = (falloff - 0.48f) / 0.52f;
					var interiorNoise = 0.5f + 0.5f * fragmentNoise.GetNoise3D(
						8.4f * nx + 13.7f,
						8.4f * ny - 9.2f,
						8.4f * nz + 4.6f);

					var ridgeBias = morphology switch
					{
						TerrainMorphology.Supercontinent => 0.58f,
						TerrainMorphology.Continents => 0.50f,
						TerrainMorphology.ColdContinent => 0.54f,
						_ => 0.46f
					};

					var ridgeStrength = Mathf.Clamp((interiorNoise - ridgeBias) / Mathf.Max(1f - ridgeBias, 0.0001f), 0f, 1f);
					var basinStrength = Mathf.Clamp((ridgeBias - interiorNoise) / Mathf.Max(ridgeBias, 0.0001f), 0f, 1f);

					shifted += interiorMask * ridgeStrength * Mathf.Lerp(0.01f, 0.08f, bias) * relief * ageRoughnessFactor;
					shifted -= interiorMask * basinStrength * Mathf.Lerp(0.01f, 0.06f, bias) * relief * ageRoughnessFactor;
				}

				var edgeBand = Mathf.Clamp((0.62f - falloff) / 0.34f, 0f, 1f);
				var orogeny = orogenyMask[x, y];
				if (orogeny > 0.001f)
				{
					var edgeMountainBoost = Mathf.Lerp(0.02f, 0.14f, bias) * relief;
					var youngEdgeBoost = Mathf.Lerp(1.18f, 0.86f, ageNorm);
					shifted += orogeny * (0.55f + 0.45f * edgeBand) * edgeMountainBoost * orogenyScale * youngEdgeBoost;

					var inlandSuppression = Mathf.Clamp((falloff - 0.68f) / 0.32f, 0f, 1f);
					var oldContinentSmoothing = Mathf.Lerp(0.90f, 1.35f, ageNorm);
					shifted -= orogeny * inlandSuppression * Mathf.Lerp(0.005f, 0.032f, bias) * (2.2f - relief) * Mathf.Lerp(0.8f, 1.3f, Mathf.Clamp(orogenyScale - 0.5f, 0f, 2f) / 2f) * oldContinentSmoothing;
				}
				else if (falloff > 0.70f)
				{
					var deepInterior = Mathf.Clamp((falloff - 0.70f) / 0.30f, 0f, 1f);
					shifted -= deepInterior * Mathf.Lerp(0.004f, 0.030f, bias) * (2.1f - relief) * Mathf.Lerp(0.92f, 1.38f, ageNorm);
				}

				if (falloff < 0.22f)
				{
					shifted -= (0.22f - falloff) * Mathf.Lerp(0.05f, 0.20f, bias);
				}

				result[x, y] = Mathf.Clamp(shifted, 0f, 1f);
			}
		}

		return result;
	}

	private static float BuildContinentsBase(float radial, FastNoiseLite fragmentNoise, float nx, float ny, float nz, float px, float py, int continentCount, int seed)
	{
		var normalizedCount = Mathf.Clamp(continentCount, 2, 4);
		var centers = normalizedCount switch
		{
			2 => ContinentCenters2,
			4 => ContinentCenters4,
			_ => ContinentCenters3
		};

		var lobeScale = normalizedCount switch
		{
			2 => 1.70f,
			4 => 1.78f,
			_ => 1.84f
		};

		var baseShape = 0f;
		for (var index = 0; index < centers.Length; index++)
		{
			var center = centers[index];
			var lobe = ComputeWrappedRadial(px, py, center.X, center.Y);
			var continent = Mathf.Clamp(1f - lobeScale * lobe, 0f, 1f);

			if (normalizedCount == 4)
			{
				var core = Mathf.Clamp(1f - 2.30f * lobe, 0f, 1f);
				continent = Mathf.Max(continent, 0.92f * core);
			}

			baseShape = Mathf.Max(baseShape, continent);
		}

		var centerBridge = Mathf.Clamp(1f - (radial / 0.20f), 0f, 1f);
		var centerSuppression = normalizedCount switch
		{
			2 => 0.24f,
			4 => 0.26f,
			_ => 0.36f
		};
		var centerNoise = 0.5f + 0.5f * fragmentNoise.GetNoise3D(3.1f * nx + seed * 0.0003f, 3.1f * ny, 3.1f * nz - seed * 0.0002f);
		var centerSuppressionScale = Mathf.Lerp(0.70f, 1.20f, centerNoise);
		baseShape -= centerBridge * centerSuppression * centerSuppressionScale;

		var split = fragmentNoise.GetNoise3D(5.8f * nx, 5.8f * ny, 5.8f * nz);
		baseShape += split * (normalizedCount == 4 ? 0.08f : 0.10f);

		return Mathf.Clamp(baseShape, 0f, 1f);
	}

	private static float ComputeWrappedRadial(float x, float y, float cx, float cy)
	{
		var dx = Mathf.Abs(x - cx);
		if (dx > 0.5f)
		{
			dx = 1f - dx;
		}

		var dy = Mathf.Abs(y - cy);
		return Mathf.Sqrt(dx * dx + dy * dy);
	}

	private float MapSeaLevelToTargetOceanRatio(float seaLevel)
	{
		var sea = Mathf.Clamp(seaLevel, 0.1f, 0.9f);
		var t = (sea - 0.1f) / 0.8f;
		var oneMinusT = 1f - t;

		var y0 = 0.30f;
		var y1 = 0.815f;
		var y2 = 0.95f;

		var ratio =
			oneMinusT * oneMinusT * y0 +
			2f * oneMinusT * t * y1 +
			t * t * y2;

		return Mathf.Clamp(ratio, 0.30f, 0.95f);
	}

	private void SyncMapAspectToCenter()
	{
		var size = _mapCenter.Size;
		_mapAspect.CustomMinimumSize = new Vector2(Mathf.Max(size.X, 0f), Mathf.Max(size.Y, 0f));
	}


	private void RedrawCurrentLayer()
	{
		if (_primaryWorld == null)
		{
			UpdateLorePanel();
			return;
		}

		var selectedId = _layerOption.GetSelectedId();
		var layer = Enum.IsDefined(typeof(MapLayer), selectedId) ? (MapLayer)selectedId : MapLayer.Satellite;
		SyncMapModeFromLayer(selectedId);

		var primaryRender = GetOrRenderLayer(_primaryWorld, layer);
		_mapTexture.Texture = primaryRender.Texture;
		_lastRenderedImage = primaryRender.Image;
		UpdateLegend(layer);

		if (_compareMode && _compareWorld != null)
		{
			var compareRender = GetOrRenderLayer(_compareWorld, layer);
			_lastCompareImage = compareRender.Image;

			_compareStatsLabel.Visible = true;
			_compareStatsLabel.Text = BuildCompareSummary(_primaryWorld, _compareWorld);
		}
		else
		{
			_lastCompareImage = null;
			_compareStatsLabel.Visible = false;
			_compareStatsLabel.Text = string.Empty;
		}

		if (layer == MapLayer.Cities)
		{
			_cityNamesLabel.Visible = true;
			_cityNamesLabel.Text = BuildCitiesText();
		}
		else
		{
			_cityNamesLabel.Visible = false;
			_cityNamesLabel.Text = string.Empty;
		}

		if (layer != MapLayer.Biomes && layer != MapLayer.Landform)
		{
			ResetBiomeHoverState();
		}

		var stats = _primaryWorld.Stats;
		var morphologyText = GetMorphologyText(_terrainMorphology);
		var continentSuffix = _terrainMorphology == TerrainMorphology.Continents ? $"（{_continentCount}块）" : string.Empty;
		var averageTempCelsius = NormalizedTemperatureToCelsius(stats.AvgTemperature);
		var forestPercent = ComputeForestCoveragePercent(_primaryWorld.Biome, MapWidth, MapHeight);
		var (peakHeightMeters, deepSeaHeightMeters) = ComputeTerrainExtremesMeters(_primaryWorld.Elevation, MapWidth, MapHeight, SeaLevel, _currentReliefExaggeration);
		var baseInfoText =
			$"地形形态:{morphologyText}{continentSuffix} | 海洋占比:{stats.OceanPercent:0.0}% | 森林占比:{forestPercent:0.0}% | 平均温度:{averageTempCelsius:0.0}℃ | 最高山峰高度:{FormatAltitude(peakHeightMeters)} | 最低深海高度:{FormatAltitude(deepSeaHeightMeters)}";

		if (layer == MapLayer.Ecology)
		{
			EnsureEcologySimulation(_primaryWorld);
			var ecology = _primaryWorld.EcologySimulation;
			if (ecology != null)
			{
				baseInfoText += $" | 生态健康:{ecology.AvgEcologyHealth * 100f:0.0}% | 文明潜力:{ecology.AvgCivilizationPotential * 100f:0.0}% | 文明萌发区:{ecology.CivilizationEmergencePercent:0.0}%";
			}
		}

		if (layer == MapLayer.Civilization)
		{
			EnsureCivilizationSimulation(_primaryWorld);
			var civilization = _primaryWorld.CivilizationSimulation;
			if (civilization != null)
			{
				baseInfoText += $" | 政体数量:{civilization.PolityCount} | 领土覆盖:{civilization.ControlledLandPercent:0.0}% | 核心腹地:{civilization.CoreCellPercent:0.0}% | 最大政体占比:{civilization.DominantPolitySharePercent:0.0}% | 聚落分级(村/镇/城邦):{civilization.HamletCount}/{civilization.TownCount}/{civilization.CityStateCount} | 战争热度:{civilization.ConflictHeatPercent:0.0}% | 联盟凝聚:{civilization.AllianceCohesionPercent:0.0}% | 边界波动:{civilization.BorderVolatilityPercent:0.0}%";
				var focusedEvent = GetFocusedTimelineEvent(civilization.RecentEvents);
				if (focusedEvent != null)
				{
					baseInfoText += $" | 回放焦点:第{focusedEvent.Epoch}纪元-{focusedEvent.Category}";
				}
			}
		}

		if (layer == MapLayer.TradeRoutes)
		{
			EnsureCivilizationSimulation(_primaryWorld);
			var civilization = _primaryWorld.CivilizationSimulation;
			if (civilization != null)
			{
				baseInfoText += $" | 贸易走廊覆盖:{civilization.TradeRouteCells} 格 | 枢纽联通率:{civilization.ConnectedHubPercent:0.0}% | 政体数量:{civilization.PolityCount} | 战争热度:{civilization.ConflictHeatPercent:0.0}% | 联盟凝聚:{civilization.AllianceCohesionPercent:0.0}%";
				var focusedEvent = GetFocusedTimelineEvent(civilization.RecentEvents);
				if (focusedEvent != null)
				{
					baseInfoText += $" | 回放焦点:第{focusedEvent.Epoch}纪元-{focusedEvent.Category}";
				}
			}
		}

		_infoLabel.Text = baseInfoText;
		UpdateLorePanel();
	}


	private static string GetMorphologyText(TerrainMorphology morphology)
	{
		return morphology switch
		{
			TerrainMorphology.Balanced => "均衡大陆",
			TerrainMorphology.Supercontinent => "超级大陆",
			TerrainMorphology.Continents => "大陆群",
			TerrainMorphology.Archipelago => "经典群岛",
			TerrainMorphology.FracturedIslands => "破碎岛链",
			TerrainMorphology.ShallowFragments => "浅海碎陆",
			TerrainMorphology.ColdContinent => "寒冷大陆",
			TerrainMorphology.HotWasteland => "炎热荒原",
			_ => morphology.ToString()
		};
	}

	private static float NormalizedTemperatureToCelsius(float normalizedTemperature)
	{
		var t = Mathf.Clamp(normalizedTemperature, 0f, 1f);
		return Mathf.Lerp(TemperatureMinCelsius, TemperatureMaxCelsius, t);
	}

	private static float ComputeForestCoveragePercent(BiomeType[,] biome, int width, int height)
	{
		var landCount = 0;
		var forestCount = 0;

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var current = biome[x, y];
				if (current == BiomeType.Ocean || current == BiomeType.ShallowOcean)
				{
					continue;
				}

				landCount++;
				if (IsForestBiome(current))
				{
					forestCount++;
				}
			}
		}

		if (landCount <= 0)
		{
			return 0f;
		}

		return 100f * forestCount / landCount;
	}

	private static bool IsForestBiome(BiomeType biome)
	{
		return biome switch
		{
			BiomeType.TropicalRainForest => true,
			BiomeType.TropicalSeasonalForest => true,
			BiomeType.TemperateRainForest => true,
			BiomeType.TemperateSeasonalForest => true,
			BiomeType.BorealForest => true,
			BiomeType.Taiga => true,
			_ => false
		};
	}

	private static string FormatAltitude(float meters)
	{
		var kilometers = meters / 1000f;
		return $"{meters:0,0} m（{kilometers:0.0} km）";
	}

	private static (float PeakHeightMeters, float DeepSeaHeightMeters) ComputeTerrainExtremesMeters(float[,] elevation, int width, int height, float seaLevel, float reliefExaggeration)
	{
		var safeSea = Mathf.Clamp(seaLevel, 0.0001f, 0.9999f);
		var maxLandNormalized = 0f;
		var maxSeaDepthNormalized = 0f;

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var value = elevation[x, y];
				if (float.IsNaN(value) || float.IsInfinity(value))
				{
					continue;
				}

				if (value >= safeSea)
				{
					var landHeight = (value - safeSea) / Mathf.Max(1f - safeSea, 0.0001f);
					if (landHeight > maxLandNormalized)
					{
						maxLandNormalized = landHeight;
					}
					continue;
				}

				var seaDepth = (safeSea - value) / Mathf.Max(safeSea, 0.0001f);
				if (seaDepth > maxSeaDepthNormalized)
				{
					maxSeaDepthNormalized = seaDepth;
				}
			}
		}

		var exaggeration = Mathf.Clamp(reliefExaggeration, ReliefExaggerationMin, ReliefExaggerationMax);
		var exaggeratedPeakMeters = maxLandNormalized * EarthHighestPeakMeters * exaggeration;
		var exaggeratedDeepMeters = -maxSeaDepthNormalized * EarthDeepestTrenchMeters * exaggeration;
		return (Mathf.Max(exaggeratedPeakMeters, 0f), Mathf.Min(exaggeratedDeepMeters, 0f));
	}

	private LayerRenderCacheEntry GetOrRenderLayer(GeneratedWorldData world, MapLayer layer)
	{
		var signature = BuildLayerRenderSignature(layer);
		if (world.LayerRenderCache.TryGetValue(layer, out var cached) && cached.Signature == signature)
		{
			cached.LastAccessTick = ++_renderCacheAccessCounter;
			return cached;
		}

		var image = RenderLayer(world, layer);
		var entry = new LayerRenderCacheEntry
		{
			Signature = signature,
			Image = image,
			Texture = ImageTexture.CreateFromImage(image),
			LastAccessTick = ++_renderCacheAccessCounter
		};

		StoreLayerRenderCache(world, layer, entry);
		return entry;
	}

	private int BuildLayerRenderSignature(MapLayer layer)
	{
		return layer switch
		{
			MapLayer.Elevation => (int)_elevationStyle,
			MapLayer.Wind => Mathf.RoundToInt(WindArrowDensity * 1000f),
			MapLayer.Landform => Mathf.RoundToInt(BasinSensitivity * 1000f),
			MapLayer.Ecology => BuildEcologySignature(),
			MapLayer.Civilization => BuildCivilizationSignature(),
			MapLayer.TradeRoutes => BuildCivilizationSignature(),
			_ => 0
		};
	}

	private int BuildEcologySignature()
	{
		return HashCode.Combine(
			Seed,
			_currentEpoch,
			_speciesDiversity,
			_civilAggression,
			_magicDensity,
			MapWidth,
			MapHeight,
			Mathf.RoundToInt(SeaLevel * 10000f));
	}

	private int BuildCivilizationSignature()
	{
		return BuildEcologySignature();
	}

	private void StoreLayerRenderCache(GeneratedWorldData world, MapLayer layer, LayerRenderCacheEntry entry)
	{
		world.LayerRenderCache[layer] = entry;
		if (world.LayerRenderCache.Count <= LayerRenderCacheCapacity)
		{
			return;
		}

		var hasOldest = false;
		var oldestLayer = layer;
		var oldestTick = long.MaxValue;

		foreach (var pair in world.LayerRenderCache)
		{
			if (pair.Key == layer)
			{
				continue;
			}

			if (pair.Value.LastAccessTick >= oldestTick)
			{
				continue;
			}

			hasOldest = true;
			oldestLayer = pair.Key;
			oldestTick = pair.Value.LastAccessTick;
		}

		if (hasOldest)
		{
			world.LayerRenderCache.Remove(oldestLayer);
		}
	}

	private Image RenderLayer(GeneratedWorldData world, MapLayer layer)
	{
		if (layer == MapLayer.Landform)
		{
			var landformImage = BuildLandformImage(world.Elevation, world.Moisture, world.River, SeaLevel, MapWidth, MapHeight);
			return MapWidth == OutputWidth && MapHeight == OutputHeight
				? landformImage
				: UpscaleImageNearest(landformImage, OutputWidth, OutputHeight);
		}

		if (layer == MapLayer.Ecology)
		{
			EnsureEcologySimulation(world);
		}

		if (layer == MapLayer.Civilization)
		{
			EnsureCivilizationSimulation(world);
		}

		if (layer == MapLayer.TradeRoutes)
		{
			EnsureCivilizationSimulation(world);
		}

		var sourceImage = _renderer.Render(
			MapWidth,
			MapHeight,
			layer,
			world.PlateResult,
			world.Elevation,
			world.Temperature,
			world.Moisture,
			world.Wind,
			world.River,
			world.Biome,
			world.Rock,
			world.Ore,
			world.Cities,
			SeaLevel,
			_elevationStyle,
			world.EcologySimulation?.EcologyHealth,
			world.CivilizationSimulation?.Influence,
			world.CivilizationSimulation?.PolityId,
			world.CivilizationSimulation?.BorderMask,
			world.CivilizationSimulation?.TradeRouteMask,
			world.CivilizationSimulation?.TradeFlow);

		var finalImage = MapWidth == OutputWidth && MapHeight == OutputHeight
			? sourceImage
			: UpscaleImageNearest(sourceImage, OutputWidth, OutputHeight);

		if (layer == MapLayer.Wind)
		{
			_renderer.OverlayWindArrows(finalImage, world.Wind, MapWidth, MapHeight, WindArrowDensity);
		}

		if (layer == MapLayer.Civilization || layer == MapLayer.TradeRoutes)
		{
			ApplyTimelineHotspotOverlay(finalImage, world, layer);
		}

		return finalImage;
	}

	private void EnsureEcologySimulation(GeneratedWorldData world)
	{
		var signature = BuildEcologySignature();
		if (world.EcologySimulation != null && world.EcologySignature == signature)
		{
			return;
		}

		world.EcologySimulation = _ecologySimulator.Simulate(
			MapWidth,
			MapHeight,
			Seed,
			_currentEpoch,
			_speciesDiversity,
			_civilAggression,
			_magicDensity,
			SeaLevel,
			world.Elevation,
			world.Temperature,
			world.Moisture,
			world.River,
			world.Biome);
		world.EcologySignature = signature;
	}

	private void EnsureCivilizationSimulation(GeneratedWorldData world)
	{
		EnsureEcologySimulation(world);

		var signature = BuildCivilizationSignature();
		if (world.CivilizationSimulation != null && world.CivilizationSignature == signature)
		{
			return;
		}

		if (world.EcologySimulation == null)
		{
			world.CivilizationSimulation = null;
			world.CivilizationSignature = int.MinValue;
			return;
		}

		world.CivilizationSimulation = _civilizationSimulator.Simulate(
			MapWidth,
			MapHeight,
			Seed,
			_currentEpoch,
			_civilAggression,
			_speciesDiversity,
			SeaLevel,
			world.Elevation,
			world.River,
			world.Biome,
			world.Cities,
			world.EcologySimulation.CivilizationPotential);
		world.CivilizationSignature = signature;
	}

	private Image BuildLandformImage(float[,] elevation, float[,] moisture, float[,] river, float seaLevel, int width, int height)
	{
		var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var landform = ClassifyLandform(x, y, seaLevel, elevation, moisture, river);
				var color = GetLandformColor(landform);
				image.SetPixel(x, y, color);
			}
		}

		return image;
	}

	private static Image UpscaleImageNearest(Image source, int targetWidth, int targetHeight)
	{
		var sourceWidth = source.GetWidth();
		var sourceHeight = source.GetHeight();

		if (sourceWidth == targetWidth && sourceHeight == targetHeight)
		{
			return source;
		}

		var scaled = Image.CreateEmpty(targetWidth, targetHeight, false, Image.Format.Rgba8);

		for (var y = 0; y < targetHeight; y++)
		{
			var sampleY = Mathf.Clamp((int)((long)y * sourceHeight / targetHeight), 0, sourceHeight - 1);
			for (var x = 0; x < targetWidth; x++)
			{
				var sampleX = Mathf.Clamp((int)((long)x * sourceWidth / targetWidth), 0, sourceWidth - 1);
				scaled.SetPixel(x, y, source.GetPixel(sampleX, sampleY));
			}
		}

		return scaled;
	}

	private string BuildCompareSummary(GeneratedWorldData primary, GeneratedWorldData compare)
	{
		var dOcean = primary.Stats.OceanPercent - compare.Stats.OceanPercent;
		var dRiver = primary.Stats.RiverPercent - compare.Stats.RiverPercent;
		var dTemp = primary.Stats.AvgTemperature - compare.Stats.AvgTemperature;
		var dMoist = primary.Stats.AvgMoisture - compare.Stats.AvgMoisture;
		var dCities = primary.Stats.CityCount - compare.Stats.CityCount;

		return $"A:{primary.Tuning.Name}  B:{compare.Tuning.Name} | Δ海洋占比:{dOcean:+0.00;-0.00;0.00}%  Δ河流占比:{dRiver:+0.00;-0.00;0.00}%  Δ平均温度:{dTemp:+0.000;-0.000;0.000}  Δ平均湿度:{dMoist:+0.000;-0.000;0.000}  Δ城市:{dCities:+0;-0;0}";
	}

	private string BuildCitiesText()
	{
		if (_primaryWorld == null)
		{
			return string.Empty;
		}

		var builder = new StringBuilder();
		builder.AppendLine(BuildCityListSection(_primaryWorld.Tuning.Name, _primaryWorld.Cities));

		if (_compareMode && _compareWorld != null)
		{
			builder.AppendLine();
			builder.AppendLine(BuildCityListSection(_compareWorld.Tuning.Name, _compareWorld.Cities));
		}

		return builder.ToString();
	}

	private static string CityPopulationText(CityPopulation population)
	{
		return population switch
		{
			CityPopulation.Small => "small",
			CityPopulation.Medium => "medium",
			_ => "large"
		};
	}

	private string BuildCityListSection(string title, List<CityInfo> cities)
	{
		var builder = new StringBuilder();
		builder.AppendLine($"Cities ({title}):");

		if (cities.Count == 0)
		{
			builder.AppendLine("- none");
			return builder.ToString();
		}

		var count = Mathf.Min(16, cities.Count);
		for (var i = 0; i < count; i++)
		{
			var city = cities[i];
			builder.AppendLine($"{i + 1}. {city.Name} ({city.Position.X}, {city.Position.Y}) [{CityPopulationText(city.Population)}]");
		}

		if (cities.Count > count)
		{
			builder.AppendLine($"...and {cities.Count - count} more");
		}

		return builder.ToString();
	}

	private void OnExportPngPressed()
	{
		if (_lastRenderedImage == null)
		{
			_infoLabel.Text = "Nothing to export yet. Generate first.";
			return;
		}

		var defaultName = BuildDefaultMapExportName();
		ConfigureAndShowSaveDialog(ExportKind.Png, "保存 PNG", defaultName, "*.png");
	}

	private void OnExportJsonPressed()
	{
		if (_primaryWorld == null)
		{
			_infoLabel.Text = "Nothing to export yet. Generate first.";
			return;
		}

		var defaultName = BuildDefaultExportName("data", ".json");
		ConfigureAndShowSaveDialog(ExportKind.Json, "保存 JSON", defaultName, "*.json");
	}

	private string BuildDefaultExportName(string prefix, string extension)
	{
		var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
		return $"{prefix}_{Seed}_{timestamp}{extension}";
	}

	private string BuildDefaultMapExportName()
	{
		var layerTag = GetLayerFileTag(GetCurrentLayer());
		return BuildDefaultExportName($"map_{layerTag}", ".png");
	}

	private void ConfigureAndShowSaveDialog(ExportKind exportKind, string title, string defaultName, string filter)
	{
		_pendingExportKind = exportKind;
		_saveFileDialog.Title = title;
		_saveFileDialog.ClearFilters();
		_saveFileDialog.AddFilter(filter);
		_saveFileDialog.CurrentFile = defaultName;
		_saveFileDialog.PopupCentered(new Vector2I(920, 560));
	}

	private void OnSaveFileSelected(string selectedPath)
	{
		switch (_pendingExportKind)
		{
			case ExportKind.Png:
				SavePngToPath(selectedPath);
				break;
			case ExportKind.Json:
				SaveJsonToPath(selectedPath);
				break;
		}

		_pendingExportKind = ExportKind.None;
	}

	private void SavePngToPath(string selectedPath)
	{
		if (_lastRenderedImage == null)
		{
			_infoLabel.Text = "PNG export failed: no image.";
			return;
		}

		var layerTag = GetLayerFileTag(GetCurrentLayer());
		var primaryPath = EnsureLayerTagInPath(EnsureFileExtension(selectedPath, ".png"), layerTag);
		var primaryError = _lastRenderedImage.SavePng(primaryPath);

		if (_compareMode && _lastCompareImage != null)
		{
			var comparePath = InsertSuffixBeforeExtension(primaryPath, "_B");
			var compareError = _lastCompareImage.SavePng(comparePath);

			_infoLabel.Text = primaryError == Error.Ok && compareError == Error.Ok
				? $"PNG exported: {primaryPath} + {comparePath}"
				: $"PNG export failed: A={primaryError}, B={compareError}";
			return;
		}

		_infoLabel.Text = primaryError == Error.Ok
			? $"PNG exported: {primaryPath}"
			: $"PNG export failed: {primaryError}";
	}

	private void SaveJsonToPath(string selectedPath)
	{
		if (_primaryWorld == null)
		{
			_infoLabel.Text = "JSON export failed: no world.";
			return;
		}

		var path = EnsureFileExtension(selectedPath, ".json");

		var payload = new Godot.Collections.Dictionary
		{
			["seed"] = Seed,
			["width"] = OutputWidth,
			["height"] = OutputHeight,
			["info_width"] = MapWidth,
			["info_height"] = MapHeight,
			["plates"] = PlateCount,
			["wind_cells"] = WindCellCount,
			["sea_level"] = SeaLevel,
			["heat"] = HeatFactor,
			["river_density"] = RiverDensity,
			["erosion"] = ErosionIterations,
			["continent_count"] = _continentCount,
			["compare_mode"] = _compareMode,
			["primary"] = BuildWorldDictionary(_primaryWorld)
		};

		if (_compareMode && _compareWorld != null)
		{
			payload["compare"] = BuildWorldDictionary(_compareWorld);
		}

		using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
		if (file == null)
		{
			_infoLabel.Text = "JSON export failed: cannot open file.";
			return;
		}

		file.StoreString(Json.Stringify(payload, "  "));
		_infoLabel.Text = $"JSON exported: {path}";
	}

	private static string EnsureFileExtension(string path, string extension)
	{
		if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
		{
			return path;
		}

		return path + extension;
	}

	private static string InsertSuffixBeforeExtension(string path, string suffix)
	{
		var slashIndex = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
		var dotIndex = path.LastIndexOf('.');

		if (dotIndex <= slashIndex)
		{
			return path + suffix;
		}

		return string.Concat(path.AsSpan(0, dotIndex), suffix, path.AsSpan(dotIndex));
	}

	private MapLayer GetCurrentLayer()
	{
		var selectedId = _layerOption.GetSelectedId();
		return Enum.IsDefined(typeof(MapLayer), selectedId) ? (MapLayer)selectedId : MapLayer.Satellite;
	}

	private static string GetLayerFileTag(MapLayer layer)
	{
		return layer switch
		{
			MapLayer.Satellite => "satellite",
			MapLayer.Plates => "plates",
			MapLayer.Temperature => "temperature",
			MapLayer.Rivers => "rivers",
			MapLayer.Moisture => "moisture",
			MapLayer.Wind => "wind",
			MapLayer.Elevation => "elevation",
			MapLayer.RockTypes => "rocktypes",
			MapLayer.Ores => "ores",
			MapLayer.Biomes => "biomes",
			MapLayer.Cities => "cities",
			MapLayer.Landform => "landform",
			MapLayer.Ecology => "ecology",
			MapLayer.Civilization => "civilization",
			MapLayer.TradeRoutes => "trade_routes",
			_ => "satellite"
		};
	}

	private static string EnsureLayerTagInPath(string path, string layerTag)
	{
		var slashIndex = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
		var dotIndex = path.LastIndexOf('.');
		if (dotIndex <= slashIndex)
		{
			dotIndex = path.Length;
		}

		var fileNameStart = slashIndex + 1;
		var fileNameLength = dotIndex - fileNameStart;
		if (fileNameLength <= 0)
		{
			return path;
		}

		var fileNameWithoutExt = path.Substring(fileNameStart, fileNameLength);
		var token = $"_{layerTag}";
		if (fileNameWithoutExt.Contains(token, StringComparison.OrdinalIgnoreCase))
		{
			return path;
		}

		return path.Insert(dotIndex, token);
	}

	private Godot.Collections.Dictionary BuildWorldDictionary(GeneratedWorldData world)
	{
		var cities = new Godot.Collections.Array<Godot.Collections.Dictionary>();
		foreach (var city in world.Cities)
		{
			cities.Add(new Godot.Collections.Dictionary
			{
				["name"] = city.Name,
				["x"] = city.Position.X,
				["y"] = city.Position.Y,
				["score"] = city.Score,
				["population"] = CityPopulationText(city.Population)
			});
		}

		return new Godot.Collections.Dictionary
		{
			["preset"] = world.Tuning.Name,
			["city_count"] = world.Cities.Count,
			["cities"] = cities,
			["stats"] = new Godot.Collections.Dictionary
			{
				["ocean_percent"] = world.Stats.OceanPercent,
				["river_percent"] = world.Stats.RiverPercent,
				["avg_temperature"] = world.Stats.AvgTemperature,
				["avg_moisture"] = world.Stats.AvgMoisture
			}
		};
	}



	private void UpdateLegend(MapLayer layer)
	{
		switch (layer)
		{
			case MapLayer.Temperature:
				SetGradientLegend("温度图例", "低温", "高温",
					new Color(0.0f, 0.298f, 1.0f),
					new Color(1.0f, 0.894f, 0.361f),
					new Color(1.0f, 0.165f, 0.0f));
				break;
			case MapLayer.Moisture:
				SetGradientLegend("降水图例", "少", "多",
					new Color(0.851f, 0.925f, 1.0f),
					new Color(0.353f, 0.663f, 1.0f),
					new Color(0.051f, 0.247f, 0.584f));
				break;
			case MapLayer.Rivers:
				SetGradientLegend("河流图例", "弱", "强",
					new Color(0.055f, 0.247f, 0.584f),
					new Color(0.0f, 0.0f, 1.0f));
				break;
			case MapLayer.Elevation:
				if (_elevationStyle == ElevationStyle.Realistic)
				{
					SetGradientLegend("高程图例", "海沟", "雪山",
						new Color(0.02f, 0.15f, 0.39f),
						new Color(0.09f, 0.46f, 0.77f),
						new Color(0.39f, 0.68f, 0.34f),
						new Color(0.77f, 0.72f, 0.58f),
						new Color(0.95f, 0.93f, 0.86f));
				}
				else
				{
					SetGradientLegend("高程图例", "海沟", "雪山",
						new Color(0.02f, 0.07f, 0.23f),
						new Color(0.09f, 0.46f, 0.77f),
						new Color(0.35f, 0.67f, 0.32f),
						new Color(0.78f, 0.74f, 0.57f),
						new Color(0.98f, 0.98f, 0.98f));
				}
				break;
			case MapLayer.Biomes:
				SetBiomeLegend();
				break;
			case MapLayer.Landform:
				SetLandformLegend();
				break;
			case MapLayer.Ecology:
				SetGradientLegend("生态健康图例", "脆弱", "繁荣",
					new Color(0.54f, 0.31f, 0.17f),
					new Color(0.84f, 0.62f, 0.27f),
					new Color(0.46f, 0.69f, 0.32f),
					new Color(0.17f, 0.81f, 0.45f));
				break;
			case MapLayer.Civilization:
				SetGradientLegend("文明影响图例", "边缘", "核心",
					new Color(0.22f, 0.25f, 0.32f),
					new Color(0.44f, 0.56f, 0.74f),
					new Color(0.78f, 0.50f, 0.26f),
					new Color(0.95f, 0.85f, 0.56f));
				break;
			case MapLayer.TradeRoutes:
				SetGradientLegend("贸易走廊图例", "弱", "强",
					new Color(0.25f, 0.29f, 0.34f),
					new Color(0.64f, 0.53f, 0.36f),
					new Color(0.92f, 0.73f, 0.38f),
					new Color(0.97f, 0.89f, 0.62f));
				break;
			default:
				_legendPanel.Visible = false;
				_biomeLegendPanel.Visible = false;
				break;
		}
	}

	private void SetGradientLegend(string title, string minText, string maxText, params Color[] stops)
	{
		if (stops.Length < 2)
		{
			_legendPanel.Visible = false;
			return;
		}

		_legendTitle.Text = title;
		_legendMinLabel.Text = minText;
		_legendMaxLabel.Text = maxText;

		var legendImage = BuildGradientLegendImage(220, 14, stops);
		_legendTexture.Texture = ImageTexture.CreateFromImage(legendImage);
		_legendPanel.Visible = true;
		_biomeLegendPanel.Visible = false;
	}

	private void SetBiomeLegend()
	{
		_legendPanel.Visible = false;
		_biomeLegendText.Text = BuildBiomeLegendText();
		_biomeLegendPanel.Visible = true;
	}

	private void SetLandformLegend()
	{
		_legendPanel.Visible = false;
		_biomeLegendText.Text = BuildLandformLegendText();
		_biomeLegendPanel.Visible = true;
	}

	private static string BuildBiomeLegendText()
	{
		var entries = new (string ColorHex, string Name)[]
		{
			("#2f5f88", "海洋"),
			("#4f7ea8", "浅海"),
			("#dfe4c9", "海岸"),
			("#2fb95a", "温带落叶林"),
			("#b8c98a", "草原气候"),
			("#4f6e34", "北方针叶林"),
			("#46a857", "温带雨林"),
			("#7c8f53", "湿地"),
			("#c2d3da", "冰川"),
			("#a1814a", "苔原"),
			("#cfd18a", "热带草原气候"),
			("#c7c5ac", "寒漠"),
			("#e9d79b", "热带沙漠"),
			("#aed45a", "热带季雨林"),
			("#7acb33", "热带雨林")
		};

		var builder = new StringBuilder(768);
		foreach (var entry in entries)
		{
			builder.Append("[color=")
				.Append(entry.ColorHex)
				.Append("]■[/color] ")
				.Append(entry.Name)
				.Append('\n');
		}

		return builder.ToString();
	}

	private static string BuildLandformLegendText()
	{
		var entries = new (string ColorHex, string Name)[]
		{
			("#0a1f4d", "深海盆地"),
			("#2f5f88", "大陆架浅海"),
			("#c9d8ae", "滨海平原"),
			("#98c47a", "内陆平原"),
			("#86b472", "内陆盆地"),
			("#b0be77", "丘陵"),
			("#b79f73", "高地"),
			("#9e8d63", "高原台地"),
			("#7e6b57", "山地")
		};

		var builder = new StringBuilder(512);
		foreach (var entry in entries)
		{
			builder.Append("[color=")
				.Append(entry.ColorHex)
				.Append("]■[/color] ")
				.Append(entry.Name)
				.Append('\n');
		}

		return builder.ToString();
	}

	private void OnMapTextureGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton loreMouseButton &&
			loreMouseButton.ButtonIndex == MouseButton.Left &&
			loreMouseButton.Pressed)
		{
			UpdateLoreFromMapSelection(loreMouseButton.Position);
		}

		var currentLayer = GetCurrentLayer();
		if (_primaryWorld == null || (currentLayer != MapLayer.Biomes && currentLayer != MapLayer.Landform))
		{
			ResetBiomeHoverState();
			return;
		}

		if (@event is not InputEventMouseButton mouseButton ||
			mouseButton.ButtonIndex != MouseButton.Left ||
			!mouseButton.Pressed)
		{
			return;
		}

		var local = mouseButton.Position;
		if (!TrySampleBiome(local, out var sampleX, out var sampleY, out var biome))
		{
			ResetBiomeHoverState();
			return;
		}

		var detailText = BuildBiomeHoverText(sampleX, sampleY, biome);
		if (_biomeHoverText.Text != detailText)
		{
			_biomeHoverText.Text = detailText;
		}

		PositionBiomeHoverPanel(local);
		_biomeHoverPanel.Visible = true;
	}

	private void OnMapTextureMouseExited()
	{
		ResetBiomeHoverState();
	}

	private void ResetBiomeHoverState()
	{
		_biomeHoverPanel.Visible = false;
	}

	private bool TrySampleBiome(Vector2 localPosition, out int sampleX, out int sampleY, out BiomeType biome)
	{
		sampleX = 0;
		sampleY = 0;
		biome = BiomeType.Ocean;

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
		sampleX = Mathf.Clamp((int)(tX * MapWidth), 0, MapWidth - 1);
		sampleY = Mathf.Clamp((int)(tY * MapHeight), 0, MapHeight - 1);
		biome = _primaryWorld.Biome[sampleX, sampleY];
		return true;
	}

	private string BuildBiomeHoverText(int x, int y, BiomeType biome)
	{
		if (_primaryWorld == null)
		{
			return string.Empty;
		}

		var elevationValue = _primaryWorld.Elevation[x, y];
		var temperatureValue = _primaryWorld.Temperature[x, y];
		var moistureValue = _primaryWorld.Moisture[x, y];
		var riverValue = _primaryWorld.River[x, y];
		var landform = ClassifyLandform(x, y, SeaLevel, _primaryWorld.Elevation, _primaryWorld.Moisture, _primaryWorld.River);
		var biomeName = GetBiomeDisplayName(biome);
		var biomeInfo = GetBiomeDetailText(biome);
		var landformName = GetLandformDisplayName(landform);
		var landformInfo = GetLandformDetailText(landform);
		var altitudeText = BuildAltitudeDisplayText(elevationValue, SeaLevel, _currentReliefExaggeration);

		return string.Concat(
			"群系：", biomeName, "\n",
			"说明：", biomeInfo, "\n",
			"地貌：", landformName, "\n",
			"地貌说明：", landformInfo, "\n",
			"坐标：", x.ToString(), ", ", y.ToString(), "\n",
			"高度：", altitudeText, "\n",
			"温度：", NormalizedTemperatureToCelsius(temperatureValue).ToString("0.0"), "℃\n",
			"湿度：", moistureValue.ToString("0.00"), "\n",
			"河流强度：", riverValue.ToString("0.00"));
	}

	private LandformType ClassifyLandform(int x, int y, float seaLevel, float[,] elevation, float[,] moisture, float[,] river)
	{
		var safeSea = Mathf.Clamp(seaLevel, 0.0001f, 0.9999f);
		var basinSensitivity = Mathf.Clamp(BasinSensitivity, 0.5f, 2.0f);
		var current = elevation[x, y];

		if (current < safeSea)
		{
			var depth = (safeSea - current) / Mathf.Max(safeSea, 0.0001f);
			return depth > 0.45f ? LandformType.DeepOcean : LandformType.ShallowSea;
		}

		var relativeHeight = (current - safeSea) / Mathf.Max(1f - safeSea, 0.0001f);
		var localRelief = ComputeLocalRelief(elevation, x, y, MapWidth, MapHeight);
		var nearSea = IsNearSea(elevation, x, y, safeSea, MapWidth, MapHeight);
		var normalizedSensitivity = (basinSensitivity - 0.5f) / 1.5f;

		var higherNeighborCount = 0;
		var lowerNeighborCount = 0;
		for (var oy = -1; oy <= 1; oy++)
		{
			for (var ox = -1; ox <= 1; ox++)
			{
				if (ox == 0 && oy == 0)
				{
					continue;
				}

				var nx = WrapX(x + ox, MapWidth);
				var ny = ClampY(y + oy, MapHeight);
				var diff = elevation[nx, ny] - current;
				if (diff > 0.018f)
				{
					higherNeighborCount++;
				}
				else if (diff < -0.018f)
				{
					lowerNeighborCount++;
				}
			}
		}

		var enclosedByHigher = higherNeighborCount >= Mathf.RoundToInt(Mathf.Lerp(5f, 7f, normalizedSensitivity)) &&
			lowerNeighborCount <= Mathf.RoundToInt(Mathf.Lerp(2f, 0f, normalizedSensitivity));
		var basinHeightThreshold = Mathf.Lerp(0.32f, 0.18f, normalizedSensitivity);
		var basinMoistureThreshold = Mathf.Lerp(0.42f, 0.55f, normalizedSensitivity);
		var basinRiverThreshold = Mathf.Lerp(0.02f, 0.05f, normalizedSensitivity);
		if (relativeHeight < basinHeightThreshold && enclosedByHigher && (moisture[x, y] > basinMoistureThreshold || river[x, y] > basinRiverThreshold))
		{
			return LandformType.Basin;
		}

		if (nearSea && relativeHeight < 0.12f)
		{
			return LandformType.CoastalPlain;
		}

		if (relativeHeight < 0.28f && localRelief < 0.018f)
		{
			return LandformType.Plain;
		}

		if (relativeHeight < 0.45f && localRelief < 0.032f)
		{
			return LandformType.RollingHills;
		}

		if (relativeHeight > 0.62f && localRelief < 0.022f)
		{
			return LandformType.Plateau;
		}

		if (relativeHeight > 0.70f || localRelief > 0.055f)
		{
			return LandformType.Mountain;
		}

		return LandformType.Upland;
	}

	private static float ComputeLocalRelief(float[,] elevation, int x, int y, int width, int height)
	{
		var minValue = elevation[x, y];
		var maxValue = elevation[x, y];

		for (var oy = -1; oy <= 1; oy++)
		{
			for (var ox = -1; ox <= 1; ox++)
			{
				var nx = WrapX(x + ox, width);
				var ny = ClampY(y + oy, height);
				var value = elevation[nx, ny];
				if (value < minValue)
				{
					minValue = value;
				}
				if (value > maxValue)
				{
					maxValue = value;
				}
			}
		}

		return maxValue - minValue;
	}

	private static bool IsNearSea(float[,] elevation, int x, int y, float seaLevel, int width, int height)
	{
		for (var oy = -1; oy <= 1; oy++)
		{
			for (var ox = -1; ox <= 1; ox++)
			{
				if (ox == 0 && oy == 0)
				{
					continue;
				}

				var nx = WrapX(x + ox, width);
				var ny = ClampY(y + oy, height);
				if (elevation[nx, ny] <= seaLevel)
				{
					return true;
				}
			}
		}

		return false;
	}

	private static int WrapX(int x, int width)
	{
		var wrapped = x % width;
		if (wrapped < 0)
		{
			wrapped += width;
		}
		return wrapped;
	}

	private static int ClampY(int y, int height)
	{
		return Mathf.Clamp(y, 0, height - 1);
	}

	private void PositionBiomeHoverPanel(Vector2 localMousePosition)
	{
		var panel = _biomeHoverPanel;
		var mapSize = _mapRoot.Size;

		var targetX = localMousePosition.X + BiomeHoverPanelOffsetX;
		var targetY = localMousePosition.Y + BiomeHoverPanelOffsetY;

		var panelSize = panel.Size;
		if (panelSize.X <= 1f || panelSize.Y <= 1f)
		{
			panelSize = panel.GetCombinedMinimumSize();
		}

		var maxX = Mathf.Max(BiomeHoverPanelMargin, mapSize.X - panelSize.X - BiomeHoverPanelMargin);
		var maxY = Mathf.Max(BiomeHoverPanelMargin, mapSize.Y - panelSize.Y - BiomeHoverPanelMargin);
		var clampedX = Mathf.Clamp(targetX, BiomeHoverPanelMargin, maxX);
		var clampedY = Mathf.Clamp(targetY, BiomeHoverPanelMargin, maxY);

		panel.Position = new Vector2(clampedX, clampedY);
	}

	private void UpdateLoreFromMapSelection(Vector2 localMousePosition)
	{
		if (_primaryWorld == null)
		{
			UpdateLorePanel();
			return;
		}

		if (!TrySampleBiome(localMousePosition, out var sampleX, out var sampleY, out var biome))
		{
			return;
		}

		var landform = ClassifyLandform(sampleX, sampleY, SeaLevel, _primaryWorld.Elevation, _primaryWorld.Moisture, _primaryWorld.River);
		var hazardSkulls = ComputeThreatSkulls(sampleX, sampleY, biome, landform);
		_threatLabel.Text = $"生存威胁指数: {BuildThreatIcons(hazardSkulls)}";

		var modeText = _mapMode switch
		{
			MapMode.Geographic => "地理",
			MapMode.Geopolitical => "政区",
			MapMode.Arcane => "奥术",
			_ => "地理"
		};
		var timelineEvents = GetTimelineEventsForCurrentWorld();
		_loreStateLabel.Text = $"模式：{modeText} | 纪元：{_currentEpoch} | {BuildReplayStatusText(timelineEvents)}";

		_loreText.Text = BuildNarrativeText(sampleX, sampleY, biome, landform, hazardSkulls);
	}

	private void UpdateLorePanel()
	{
		var modeText = _mapMode switch
		{
			MapMode.Geographic => "地理",
			MapMode.Geopolitical => "政区",
			MapMode.Arcane => "奥术",
			_ => "地理"
		};

		var timelineEvents = GetTimelineEventsForCurrentWorld();
		UpdateTimelineReplayCursor(timelineEvents);

		_loreStateLabel.Text = $"模式：{modeText} | 纪元：{_currentEpoch} | {BuildReplayStatusText(timelineEvents)}";
		var baseThreat = Mathf.Clamp(1 + _civilAggression / 40 + _magicDensity / 60, 1, 5);
		_threatLabel.Text = $"生存威胁指数: {BuildThreatIcons(baseThreat)}";

		if (_primaryWorld == null)
		{
			_loreText.Text = "[b]选定区域地质：[/b] 请先生成世界，再点击地图查看叙事详情。";
			return;
		}

		_loreText.Text = BuildNarrativeOverviewText();
	}

	private int ComputeThreatSkulls(int x, int y, BiomeType biome, LandformType landform)
	{
		if (_primaryWorld == null)
		{
			return 1;
		}

		var elevation = _primaryWorld.Elevation[x, y];
		var river = _primaryWorld.River[x, y];
		var temperature = _primaryWorld.Temperature[x, y];

		var threat = 1;
		threat += _civilAggression > 55 ? 1 : 0;
		threat += _magicDensity > 70 ? 1 : 0;
		threat += _speciesDiversity > 80 ? 1 : 0;

		if (biome == BiomeType.TropicalDesert || biome == BiomeType.TemperateDesert || biome == BiomeType.SnowyMountain)
		{
			threat += 1;
		}

		if (landform == LandformType.Mountain || landform == LandformType.DeepOcean)
		{
			threat += 1;
		}

		if (temperature < 0.22f || temperature > 0.82f)
		{
			threat += 1;
		}

		if (elevation < SeaLevel + 0.02f && river > 0.12f)
		{
			threat += 1;
		}

		return Mathf.Clamp(threat, 1, 5);
	}

	private string BuildNarrativeOverviewText()
	{
		if (_primaryWorld == null)
		{
			return "[b]选定区域地质：[/b] 请先生成世界。";
		}

		var stats = _primaryWorld.Stats;
		var civilizationProfile = _civilAggression switch
		{
			< 30 => "更倾向贸易协作，城邦冲突频率较低",
			< 65 => "保持竞争与联盟并存，边界稳定性中等",
			_ => "战争动员能力强，边境摩擦频繁升级"
		};

		var arcaneProfile = _magicDensity switch
		{
			< 30 => "以经验技术为主，奥术仅限宗教礼仪",
			< 70 => "以太网络已介入交通、冶炼与医疗",
			_ => "高密度灵脉重塑生产体系，出现法术垄断阶层"
		};

		var diversityProfile = _speciesDiversity switch
		{
			< 30 => "族群结构单一，文化演化路径集中",
			< 70 => "多族群共存，区域文化呈带状分布",
			_ => "高多样性交汇，边境语言与信仰高度混融"
		};

		EnsureCivilizationSimulation(_primaryWorld);
		var civilization = _primaryWorld.CivilizationSimulation;
		var timelineText = BuildCivilizationTimelineText(civilization);

		return string.Concat(
			"[b]世界编年概览：[/b]\n",
			"当前纪元：", _currentEpoch.ToString(), " / ", MaxEpoch.ToString(), "\n",
			"海洋占比：", stats.OceanPercent.ToString("0.0"), "%\n",
			"城市规模：", stats.CityCount.ToString(), " 个聚落核心\n",
			"[b]文明趋势：[/b] ", civilizationProfile, "\n",
			"[b]奥术格局：[/b] ", arcaneProfile, "\n",
			"[b]族群生态：[/b] ", diversityProfile, "\n",
			timelineText,
			"\n提示：点击地图后将切换为区域级叙事。"
		);
	}

	private string BuildCivilizationTimelineText(CivilizationSimulationResult? civilization)
	{
		if (civilization == null || civilization.RecentEvents.Length == 0)
		{
			return "[b]近纪元事件：[/b] 暂无可回放事件。";
		}

		var selectedEpoch = _selectedTimelineEventEpoch >= 0 ? _selectedTimelineEventEpoch : _currentEpoch;
		var selectedIndex = ResolveTimelineEventIndex(civilization.RecentEvents, selectedEpoch);
		if (selectedIndex >= 0)
		{
			_selectedTimelineEventEpoch = civilization.RecentEvents[selectedIndex].Epoch;
		}

		var builder = new StringBuilder();
		builder.Append("[b]近纪元事件：[/b]\n");
		var maxEvents = civilization.RecentEvents.Length;
		for (var i = civilization.RecentEvents.Length - maxEvents; i < civilization.RecentEvents.Length; i++)
		{
			if (i < 0)
			{
				continue;
			}

			var evt = civilization.RecentEvents[i];
			var impactStars = BuildImpactIcons(evt.ImpactLevel);
			var isSelected = i == selectedIndex;
			var prefix = isSelected ? "▶ " : "- ";
			builder.Append(prefix).Append("第 ").Append(evt.Epoch).Append(" 纪元 [").Append(evt.Category).Append("] ").Append(evt.Summary).Append(" ").Append(impactStars);
			if (isSelected)
			{
				builder.Append(" [color=#ffd27a]◀ 回放焦点[/color]");
			}
			builder.Append("\n");
		}

		return builder.ToString();
	}

	private CivilizationEpochEvent[] GetTimelineEventsForCurrentWorld()
	{
		if (_primaryWorld == null)
		{
			return Array.Empty<CivilizationEpochEvent>();
		}

		EnsureCivilizationSimulation(_primaryWorld);
		return _primaryWorld.CivilizationSimulation?.RecentEvents ?? Array.Empty<CivilizationEpochEvent>();
	}

	private void UpdateTimelineReplayCursor(CivilizationEpochEvent[] events)
	{
		if (events.Length == 0)
		{
			if (_selectedTimelineEventEpoch < 0)
			{
				_selectedTimelineEventEpoch = _currentEpoch;
			}

			_epochEventIndexLabel.Text = "事件 --/--";
			_prevEpochButton.Disabled = _currentEpoch <= 0;
			_nextEpochButton.Disabled = _currentEpoch >= MaxEpoch;
			return;
		}

		var selectedEpoch = _selectedTimelineEventEpoch >= 0 ? _selectedTimelineEventEpoch : _currentEpoch;
		var selectedIndex = ResolveTimelineEventIndex(events, selectedEpoch);
		if (selectedIndex < 0)
		{
			_epochEventIndexLabel.Text = "事件 --/--";
			_prevEpochButton.Disabled = false;
			_nextEpochButton.Disabled = false;
			return;
		}

		_selectedTimelineEventEpoch = events[selectedIndex].Epoch;
		_epochEventIndexLabel.Text = $"事件 {selectedIndex + 1}/{events.Length}";
		_prevEpochButton.Disabled = selectedIndex <= 0;
		_nextEpochButton.Disabled = selectedIndex >= events.Length - 1;
	}

	private static int ResolveTimelineEventIndex(CivilizationEpochEvent[] events, int targetEpoch)
	{
		if (events.Length == 0)
		{
			return -1;
		}

		var bestIndex = 0;
		var bestDistance = Math.Abs(events[0].Epoch - targetEpoch);

		for (var i = 1; i < events.Length; i++)
		{
			var distance = Math.Abs(events[i].Epoch - targetEpoch);
			if (distance > bestDistance)
			{
				continue;
			}

			if (distance == bestDistance && events[i].Epoch < events[bestIndex].Epoch)
			{
				continue;
			}

			bestDistance = distance;
			bestIndex = i;
		}

		return bestIndex;
	}

	private string BuildReplayStatusText(CivilizationEpochEvent[] events)
	{
		if (events.Length == 0)
		{
			return "回放: --/--";
		}

		var index = ResolveTimelineEventIndex(events, _selectedTimelineEventEpoch >= 0 ? _selectedTimelineEventEpoch : _currentEpoch);
		if (index < 0)
		{
			return "回放: --/--";
		}

		return $"回放: {index + 1}/{events.Length}";
	}

	private CivilizationEpochEvent? GetFocusedTimelineEvent(CivilizationEpochEvent[] events)
	{
		if (events.Length == 0)
		{
			return null;
		}

		var index = ResolveTimelineEventIndex(events, _selectedTimelineEventEpoch >= 0 ? _selectedTimelineEventEpoch : _currentEpoch);
		if (index < 0 || index >= events.Length)
		{
			return null;
		}

		return events[index];
	}

	private void ApplyTimelineHotspotOverlay(Image image, GeneratedWorldData world, MapLayer layer)
	{
		var civilization = world.CivilizationSimulation;
		if (civilization == null)
		{
			return;
		}

		var focusedEvent = GetFocusedTimelineEvent(civilization.RecentEvents);
		if (focusedEvent == null)
		{
			return;
		}

		var hotspots = FindEventHotspots(world, civilization, focusedEvent);
		if (hotspots.Count == 0)
		{
			return;
		}

		var baseColor = focusedEvent.Category switch
		{
			"战争" => new Color(1f, 0.34f, 0.30f, 1f),
			"联盟" => new Color(0.40f, 0.82f, 1f, 1f),
			"贸易" => new Color(1f, 0.80f, 0.36f, 1f),
			_ => new Color(0.95f, 0.70f, 0.42f, 1f)
		};

		for (var i = 0; i < hotspots.Count; i++)
		{
			var hotspot = hotspots[i];
			var mappedX = Mathf.RoundToInt((hotspot.X + 0.5f) * image.GetWidth() / Mathf.Max(MapWidth, 1)) - 1;
			var mappedY = Mathf.RoundToInt((hotspot.Y + 0.5f) * image.GetHeight() / Mathf.Max(MapHeight, 1)) - 1;
			var scale = Mathf.Max(image.GetWidth() / (float)Mathf.Max(MapWidth, 1), image.GetHeight() / (float)Mathf.Max(MapHeight, 1));
			var radius = Mathf.Clamp(Mathf.RoundToInt((layer == MapLayer.Civilization ? 4f : 3f) * scale), 2, 20);
			var intensity = Mathf.Clamp(0.28f + hotspot.Score * 0.46f, 0.22f, 0.72f);
			DrawHotspotCircle(image, mappedX, mappedY, radius, baseColor, intensity);
		}
	}

	private List<TimelineHotspotPoint> FindEventHotspots(
		GeneratedWorldData world,
		CivilizationSimulationResult civilization,
		CivilizationEpochEvent focusedEvent)
	{
		var points = new List<TimelineHotspotPoint>(64);
		var width = MapWidth;
		var height = MapHeight;

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				if (world.Elevation[x, y] <= SeaLevel)
				{
					continue;
				}

				var influence = civilization.Influence[x, y];
				var border = civilization.BorderMask[x, y];
				var route = civilization.TradeRouteMask[x, y];
				var flow = civilization.TradeFlow[x, y];

				float score;
				switch (focusedEvent.Category)
				{
					case "战争":
						if (!border)
						{
							continue;
						}
						score = 0.56f * influence + 0.26f * flow + 0.18f * HashNoise01(Seed ^ focusedEvent.Epoch, x, y);
						break;
					case "联盟":
						if (!border && !route)
						{
							continue;
						}
						score = 0.46f * influence + 0.34f * flow + 0.20f * HashNoise01(Seed ^ (focusedEvent.Epoch * 3), x, y);
						break;
					default:
						if (!route)
						{
							continue;
						}
						score = 0.30f * influence + 0.50f * flow + 0.20f * HashNoise01(Seed ^ (focusedEvent.Epoch * 5), x, y);
						break;
				}

				score = Mathf.Clamp(score, 0f, 1f);
				if (score < 0.60f)
				{
					continue;
				}

				points.Add(new TimelineHotspotPoint(x, y, score));
			}
		}

		if (points.Count <= 8)
		{
			return points;
		}

		points.Sort((left, right) => right.Score.CompareTo(left.Score));
		var selected = new List<TimelineHotspotPoint>(8);
		for (var i = 0; i < points.Count && selected.Count < 8; i++)
		{
			var candidate = points[i];
			var tooClose = false;
			for (var j = 0; j < selected.Count; j++)
			{
				var existing = selected[j];
				var dx = Math.Abs(candidate.X - existing.X);
				dx = Math.Min(dx, width - dx);
				var dy = Math.Abs(candidate.Y - existing.Y);
				if (dx * dx + dy * dy < 64)
				{
					tooClose = true;
					break;
				}
			}

			if (!tooClose)
			{
				selected.Add(candidate);
			}
		}

		return selected;
	}

	private static void DrawHotspotCircle(Image image, int centerX, int centerY, int radius, Color tint, float intensity)
	{
		var width = image.GetWidth();
		var height = image.GetHeight();
		var radiusSq = radius * radius;

		for (var oy = -radius; oy <= radius; oy++)
		{
			for (var ox = -radius; ox <= radius; ox++)
			{
				var distSq = ox * ox + oy * oy;
				if (distSq > radiusSq)
				{
					continue;
				}

				var x = WrapX(centerX + ox, width);
				var y = ClampY(centerY + oy, height);
				var local = 1f - Mathf.Clamp(Mathf.Sqrt(distSq) / Mathf.Max(radius, 1), 0f, 1f);
				var alpha = Mathf.Clamp(intensity * (0.45f + 0.55f * local), 0f, 0.85f);

				var original = image.GetPixel(x, y);
				image.SetPixel(x, y, LerpColor(original, tint, alpha));
			}
		}
	}

	private static float HashNoise01(int seed, int x, int y)
	{
		uint hash = (uint)seed;
		hash ^= (uint)x * 1597334677u;
		hash ^= (uint)y * 3812015801u;
		hash = (hash ^ (hash >> 16)) * 2246822519u;
		hash ^= hash >> 13;
		return (hash & 0x00FFFFFFu) / 16777215f;
	}

	private static string BuildImpactIcons(int level)
	{
		var clamped = Mathf.Clamp(level, 1, 5);
		var builder = new StringBuilder(clamped);
		for (var i = 0; i < clamped; i++)
		{
			builder.Append("◆");
		}

		return builder.ToString();
	}

	private string BuildNarrativeText(int x, int y, BiomeType biome, LandformType landform, int threatSkulls)
	{
		if (_primaryWorld == null)
		{
			return "[b]选定区域地质：[/b] 数据不可用。";
		}

		var elevationText = BuildAltitudeDisplayText(_primaryWorld.Elevation[x, y], SeaLevel, _currentReliefExaggeration);
		var biomeName = GetBiomeDisplayName(biome);
		var landformName = GetLandformDisplayName(landform);
		var geoCause = landform switch
		{
			LandformType.Basin => "地势封闭促使水汽滞留，形成稳定内陆聚落带",
			LandformType.CoastalPlain => "海陆热力差驱动贸易港与潮汐农业并行发展",
			LandformType.Mountain => "垂直高差切割交通，形成堡垒化山口城邦",
			LandformType.DeepOcean => "深水地形阻隔大陆接触，远洋文明长期隔离演化",
			_ => "地势缓变塑造了扩张可达性与资源分布边界"
		};

		var societyConsequence = _mapMode switch
		{
			MapMode.Geographic => "地理约束主导人口迁移与产业布局",
			MapMode.Geopolitical => "政体在资源瓶颈下向同盟或征服两极分化",
			MapMode.Arcane => "灵脉走向决定法术学院与禁区的权力半径",
			_ => "地理约束主导人口迁移与产业布局"
		};

		var arcaneSignal = _magicDensity >= 70
			? "该区存在高能以太回廊，稀有矿脉与仪式遗迹重叠。"
			: "该区以低能以太背景为主，奥术活动受地貌限制。";

		return string.Concat(
			"[b]选定区域地质：[/b] ", landformName, " / ", biomeName, "\n",
			"高度：", elevationText, "\n",
			"坐标：", x.ToString(), ", ", y.ToString(), "\n",
			"[b]地理因果：[/b] ", geoCause, "。\n",
			"[b]社会演化：[/b] ", societyConsequence, "。\n",
			"[b]奥术线索：[/b] ", arcaneSignal, "\n",
			"威胁评估：", BuildThreatIcons(threatSkulls), "（纪元 ", _currentEpoch.ToString(), "）"
		);
	}

	private static string BuildThreatIcons(int count)
	{
		var clamped = Mathf.Clamp(count, 1, 5);
		var builder = new StringBuilder(clamped * 2);
		for (var index = 0; index < clamped; index++)
		{
			builder.Append("💀");
		}

		return builder.ToString();
	}

	private static string BuildAltitudeDisplayText(float elevationValue, float seaLevel, float reliefExaggeration)
	{
		var safeSea = Mathf.Clamp(seaLevel, 0.0001f, 0.9999f);
		var exaggeration = Mathf.Clamp(reliefExaggeration, ReliefExaggerationMin, ReliefExaggerationMax);
		float meters;

		if (elevationValue >= safeSea)
		{
			var landNormalized = (elevationValue - safeSea) / Mathf.Max(1f - safeSea, 0.0001f);
			meters = landNormalized * EarthHighestPeakMeters * exaggeration;
		}
		else
		{
			var seaNormalized = (safeSea - elevationValue) / Mathf.Max(safeSea, 0.0001f);
			meters = -seaNormalized * EarthDeepestTrenchMeters * exaggeration;
		}

		return FormatAltitude(meters);
	}

	private static string GetBiomeDisplayName(BiomeType biome)
	{
		return biome switch
		{
			BiomeType.Ocean => "海洋",
			BiomeType.ShallowOcean => "浅海",
			BiomeType.Coastland => "海岸",
			BiomeType.Ice => "冰川",
			BiomeType.Tundra => "苔原",
			BiomeType.BorealForest => "北方针叶林",
			BiomeType.Taiga => "泰加林",
			BiomeType.Steppe => "寒漠",
			BiomeType.Grassland => "草原气候",
			BiomeType.Chaparral => "灌木地",
			BiomeType.TemperateDesert => "温带荒漠",
			BiomeType.TemperateSeasonalForest => "温带落叶林",
			BiomeType.TemperateRainForest => "温带雨林",
			BiomeType.Savanna => "热带草原气候",
			BiomeType.Shrubland => "湿地",
			BiomeType.TropicalDesert => "热带沙漠",
			BiomeType.TropicalSeasonalForest => "热带季雨林",
			BiomeType.TropicalRainForest => "热带雨林",
			BiomeType.RockyMountain => "岩石山地",
			BiomeType.SnowyMountain => "雪山",
			BiomeType.River => "河流",
			_ => biome.ToString()
		};
	}

	private static string GetBiomeDetailText(BiomeType biome)
	{
		return biome switch
		{
			BiomeType.Ocean => "深水海域，光照弱、温度低",
			BiomeType.ShallowOcean => "大陆架区域，营养盐相对丰富",
			BiomeType.Coastland => "海陆交汇，湿润多风",
			BiomeType.Ice => "常年冰雪覆盖，生态稀疏",
			BiomeType.Tundra => "冻土显著，低矮植被",
			BiomeType.BorealForest => "寒温带针叶林，冬季漫长",
			BiomeType.Taiga => "寒冷针叶林，生长期较短",
			BiomeType.Steppe => "冷干环境，植被稀少",
			BiomeType.Grassland => "半湿润带，草本植被为主",
			BiomeType.Chaparral => "夏干冬湿，灌丛为主",
			BiomeType.TemperateDesert => "温带干旱区，植被低覆盖",
			BiomeType.TemperateSeasonalForest => "四季分明，阔叶林主导",
			BiomeType.TemperateRainForest => "全年较湿，林下苔藓丰富",
			BiomeType.Savanna => "干湿季明显，草木交错",
			BiomeType.Shrubland => "低洼积水区，灌丛与草甸混生",
			BiomeType.TropicalDesert => "极端干旱，蒸发强",
			BiomeType.TropicalSeasonalForest => "季节性降雨，雨季旺盛",
			BiomeType.TropicalRainForest => "高温高湿，物种最丰富",
			BiomeType.RockyMountain => "裸岩地形，坡陡且土层薄",
			BiomeType.SnowyMountain => "高海拔寒冷，山顶常年积雪",
			BiomeType.River => "地表径流明显，水源持续",
			_ => "—"
		};
	}

	private static string GetLandformDisplayName(LandformType landform)
	{
		return landform switch
		{
			LandformType.DeepOcean => "深海盆地",
			LandformType.ShallowSea => "大陆架浅海",
			LandformType.CoastalPlain => "滨海平原",
			LandformType.Plain => "内陆平原",
			LandformType.Basin => "内陆盆地",
			LandformType.RollingHills => "丘陵",
			LandformType.Upland => "高地",
			LandformType.Plateau => "高原台地",
			LandformType.Mountain => "山地",
			_ => "—"
		};
	}

	private static string GetLandformDetailText(LandformType landform)
	{
		return landform switch
		{
			LandformType.DeepOcean => "海底较深，地势封闭度高，水压大",
			LandformType.ShallowSea => "靠近大陆架的浅海区域，受陆源影响明显",
			LandformType.CoastalPlain => "近海低地，地势平缓，沉积作用明显",
			LandformType.Plain => "低起伏广阔地表，坡度小，连通性高",
			LandformType.Basin => "周边略高、中心偏低的汇水低地",
			LandformType.RollingHills => "中低起伏地形，坡度温和",
			LandformType.Upland => "高于平原的稳定地表，起伏中等",
			LandformType.Plateau => "高海拔且相对平坦的抬升地面",
			LandformType.Mountain => "高差大、坡陡、地形破碎度高",
			_ => "—"
		};
	}

	private static Color GetLandformColor(LandformType landform)
	{
		return landform switch
		{
			LandformType.DeepOcean => new Color(0.039f, 0.122f, 0.302f, 1f),
			LandformType.ShallowSea => new Color(0.184f, 0.373f, 0.533f, 1f),
			LandformType.CoastalPlain => new Color(0.788f, 0.847f, 0.682f, 1f),
			LandformType.Plain => new Color(0.596f, 0.769f, 0.478f, 1f),
			LandformType.Basin => new Color(0.525f, 0.706f, 0.447f, 1f),
			LandformType.RollingHills => new Color(0.690f, 0.745f, 0.467f, 1f),
			LandformType.Upland => new Color(0.718f, 0.624f, 0.451f, 1f),
			LandformType.Plateau => new Color(0.620f, 0.553f, 0.388f, 1f),
			LandformType.Mountain => new Color(0.494f, 0.420f, 0.341f, 1f),
			_ => Colors.Black
		};
	}

	private static Image BuildGradientLegendImage(int width, int height, Color[] stops)
	{
		var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
		if (width <= 0 || height <= 0)
		{
			return image;
		}

		for (var x = 0; x < width; x++)
		{
			var t = width <= 1 ? 0f : (float)x / (width - 1);
			var scaled = t * (stops.Length - 1);
			var segment = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, stops.Length - 2);
			var localT = scaled - segment;
			var color = LerpColor(stops[segment], stops[segment + 1], localT);

			for (var y = 0; y < height; y++)
			{
				image.SetPixel(x, y, color);
			}
		}

		return image;
	}

	private static Color LerpColor(Color a, Color b, float t)
	{
		var f = Mathf.Clamp(t, 0f, 1f);
		return new Color(
			Mathf.Lerp(a.R, b.R, f),
			Mathf.Lerp(a.G, b.G, f),
			Mathf.Lerp(a.B, b.B, f),
			1f);
	}
	private T GetNodeByName<T>(string name) where T : Node
	{
		var node = FindChild(name, true, false);
		if (node is not T typed)
		{
			throw new InvalidOperationException($"Node '{name}' not found or not of type {typeof(T).Name}.");
		}

		return typed;
	}
}
