using Godot;
using PlanetGeneration.WorldGen;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PlanetGeneration;

public partial class Main : Control
{
	[Export] public int MapWidth { get; set; } = 256;
	[Export] public int MapHeight { get; set; } = 128;
	[Export] public int Seed { get; set; } = 1337;
	[Export] public int PlateCount { get; set; } = 20;
	[Export] public int WindCellCount { get; set; } = 10;
	[Export(PropertyHint.Range, "0,1,0.01")] public float SeaLevel { get; set; } = 0.35f;
	[Export(PropertyHint.Range, "0.01,1,0.01")] public float HeatFactor { get; set; } = 0.5f;
	[Export] public bool RandomHeatFactor { get; set; } = false;
	[Export(PropertyHint.Range, "1,20,1")] public int MoistureIterations { get; set; } = 8;
	[Export(PropertyHint.Range, "0,20,1")] public int ErosionIterations { get; set; } = 5;
	[Export] public bool EnableRivers { get; set; } = true;

	private TextureRect _mapTexture = null!;
	private SpinBox _seedSpin = null!;
	private SpinBox _plateSpin = null!;
	private SpinBox _windSpin = null!;
	private HSlider _seaLevelSlider = null!;
	private HSlider _heatSlider = null!;
	private HSlider _erosionSlider = null!;
	private Label _seaLevelValue = null!;
	private Label _heatValue = null!;
	private Label _erosionValue = null!;
	private Label _infoLabel = null!;
	private Label _compareStatsLabel = null!;
	private RichTextLabel _cityNamesLabel = null!;
	private OptionButton _layerOption = null!;
	private OptionButton _tuningOption = null!;
	private OptionButton _mapSizeOption = null!;
	private CheckBox _riverToggle = null!;
	private CheckBox _randomHeatToggle = null!;
	private CheckBox _compareToggle = null!;
	private Button _exportPngButton = null!;
	private Button _exportJsonButton = null!;
	private ProgressBar _generateProgress = null!;
	private Label _progressStatus = null!;
	private Control _progressOverlay = null!;
	private Container _layerButtons = null!;
	private Control _layerRow = null!;
	private readonly Dictionary<int, Button> _layerButtonsById = new();

	private static readonly Vector2I[] MapSizePresets =
	{
		new Vector2I(256, 128),
		new Vector2I(512, 256),
		new Vector2I(1024, 512),
		new Vector2I(2048, 1024),
		new Vector2I(4096, 2048)
	};

	private const string UltraResolutionWarningText = "⚠ 超高分辨率 4096x2048：生成与导出会更慢，并占用更多内存。";

	private bool _isGenerating;
	private bool _pendingRegenerate;
	private ulong _generationStartedMsec;

	private readonly PlateGenerator _plateGenerator = new();
	private readonly ElevationGenerator _elevationGenerator = new();
	private readonly TemperatureGenerator _temperatureGenerator = new();
	private readonly MoistureGenerator _moistureGenerator = new();
	private readonly RiverGenerator _riverGenerator = new();
	private readonly BiomeGenerator _biomeGenerator = new();
	private readonly ErosionSimulator _erosionSimulator = new();
	private readonly ResourceGenerator _resourceGenerator = new();
	private readonly CityGenerator _cityGenerator = new();
	private readonly StatsCalculator _statsCalculator = new();
	private readonly WorldRenderer _renderer = new();

	private WorldTuning _tuning = WorldTuning.Legacy();
	private bool _compareMode;

	private GeneratedWorldData? _primaryWorld;
	private GeneratedWorldData? _compareWorld;
	private Image? _lastRenderedImage;
	private Image? _lastCompareImage;

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
	}

	public override void _Ready()
	{
		_mapTexture = GetNodeByName<TextureRect>("MapTexture");
		_seedSpin = GetNodeByName<SpinBox>("SeedSpin");
		_plateSpin = GetNodeByName<SpinBox>("PlateSpin");
		_windSpin = GetNodeByName<SpinBox>("WindSpin");
		_seaLevelSlider = GetNodeByName<HSlider>("SeaLevelSlider");
		_heatSlider = GetNodeByName<HSlider>("HeatSlider");
		_erosionSlider = GetNodeByName<HSlider>("ErosionSlider");
		_seaLevelValue = GetNodeByName<Label>("SeaLevelValue");
		_heatValue = GetNodeByName<Label>("HeatValue");
		_erosionValue = GetNodeByName<Label>("ErosionValue");
		_infoLabel = GetNodeByName<Label>("InfoLabel");
		_compareStatsLabel = GetNodeByName<Label>("CompareStatsLabel");
		_cityNamesLabel = GetNodeByName<RichTextLabel>("CityNamesLabel");
		_layerOption = GetNodeByName<OptionButton>("LayerOption");
		_tuningOption = GetNodeByName<OptionButton>("TuningOption");
		_mapSizeOption = GetNodeByName<OptionButton>("MapSizeOption");
		_riverToggle = GetNodeByName<CheckBox>("RiverToggle");
		_randomHeatToggle = GetNodeByName<CheckBox>("RandomHeatToggle");
		_compareToggle = GetNodeByName<CheckBox>("CompareToggle");
		_exportPngButton = GetNodeByName<Button>("ExportPngButton");
		_exportJsonButton = GetNodeByName<Button>("ExportJsonButton");
		_generateProgress = GetNodeByName<ProgressBar>("GenerateProgress");
		_progressStatus = GetNodeByName<Label>("ProgressStatus");
		_progressOverlay = GetNodeByName<Control>("ProgressOverlay");
		_layerButtons = GetNodeByName<Container>("LayerButtons");
		_layerRow = GetNodeByName<Control>("LayerRow");

		_generateProgress.Value = 0;
		_progressStatus.Text = "待命";
		_progressOverlay.Visible = false;

		SetupLayerOptions();
		SetupTuningOptions();
		SetupMapSizeOptions();

		GetNodeByName<Button>("GenerateButton").Pressed += OnGeneratePressed;
		GetNodeByName<Button>("RandomButton").Pressed += OnRandomPressed;
		_exportPngButton.Pressed += OnExportPngPressed;
		_exportJsonButton.Pressed += OnExportJsonPressed;

		_seaLevelSlider.ValueChanged += OnSeaLevelChanged;
		_heatSlider.ValueChanged += OnHeatChanged;
		_erosionSlider.ValueChanged += OnErosionChanged;
		_layerOption.ItemSelected += _ =>
		{
			RedrawCurrentLayer();
			UpdateLayerQuickButtons();
		};

		_riverToggle.Toggled += value =>
		{
			EnableRivers = value;
			GenerateWorld();
		};

		_randomHeatToggle.Toggled += value =>
		{
			RandomHeatFactor = value;
			GenerateWorld();
		};

		_compareToggle.Toggled += value =>
		{
			_compareMode = value;
			GenerateWorld();
		};

		_seedSpin.Value = Seed;
		_plateSpin.Value = PlateCount;
		_windSpin.Value = WindCellCount;
		_seaLevelSlider.Value = SeaLevel;
		_heatSlider.Value = HeatFactor;
		_erosionSlider.Value = ErosionIterations;
		_riverToggle.ButtonPressed = EnableRivers;
		_randomHeatToggle.ButtonPressed = RandomHeatFactor;
		_compareToggle.ButtonPressed = false;
		_compareToggle.Visible = false;
		_compareToggle.Disabled = true;

		_compareStatsLabel.Visible = false;
		_cityNamesLabel.Visible = false;
		_layerOption.Visible = false;
		_layerRow.Visible = true;
		_layerRow.ZIndex = 10;
		UpdateLayerQuickButtons();

		UpdateLabels();
		GenerateWorld();
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
		_layerOption.AddItem("Rock Types", (int)MapLayer.RockTypes);
		_layerOption.AddItem("Ores", (int)MapLayer.Ores);
		_layerOption.AddItem("Biomes", (int)MapLayer.Biomes);
		_layerOption.AddItem("Cities", (int)MapLayer.Cities);
		_layerOption.Select(0);
		BuildLayerButtons();
	}

	private void SelectLayerById(int layerId)
	{
		var index = _layerOption.GetItemIndex(layerId);
		if (index < 0)
		{
			return;
		}

		_layerOption.Select(index);
		RedrawCurrentLayer();
		UpdateLayerQuickButtons();
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
		var normalStyle = CreateLayerButtonStyle(new Color(0.090196f, 0.145098f, 0.231373f, 0.96f), new Color(0.658824f, 0.756863f, 0.937255f, 0.18f));
		var hoverStyle = CreateLayerButtonStyle(new Color(0.129412f, 0.196078f, 0.301961f, 0.98f), new Color(0.658824f, 0.756863f, 0.937255f, 0.28f));
		var activeStyle = CreateLayerButtonStyle(new Color(0.180392f, 0.415686f, 0.862745f, 1f), new Color(0.764706f, 0.858824f, 1f, 0.45f));

		for (var index = 0; index < _layerOption.ItemCount; index++)
		{
			var layerId = _layerOption.GetItemId(index);
			var label = GetLayerButtonText(layerId, _layerOption.GetItemText(index));
			var button = new Button
			{
				Text = label,
				ToggleMode = true,
				FocusMode = Control.FocusModeEnum.None,
				CustomMinimumSize = new Vector2(66f, 28f),
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

			_layerButtons.AddChild(button);
			_layerButtonsById[layerId] = button;
		}
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
			pair.Value.Modulate = Colors.White;
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

	private void SetupTuningOptions()
	{
		_tuningOption.Clear();
		_tuningOption.AddItem("Legacy", 0);
		_tuningOption.AddItem("Balanced", 1);
		_tuningOption.Select(0);
		_tuningOption.ItemSelected += id =>
		{
			_tuning = id == 0 ? WorldTuning.Legacy() : WorldTuning.Balanced();
			GenerateWorld();
		};
	}

	private void SetupMapSizeOptions()
	{
		_mapSizeOption.Clear();

		for (var index = 0; index < MapSizePresets.Length; index++)
		{
			var size = MapSizePresets[index];
			_mapSizeOption.AddItem($"{size.X}x{size.Y} (2:1)", index);
		}

		var selectedIndex = FindMapSizePresetIndex(MapWidth, MapHeight);
		if (selectedIndex < 0)
		{
			selectedIndex = 0;
		}

		ApplyMapSizePreset(selectedIndex);
		_mapSizeOption.Select(selectedIndex);

		_mapSizeOption.ItemSelected += id =>
		{
			ApplyMapSizePreset((int)id);
			ShowMapSizeWarningIfNeeded();
			GenerateWorld();
		};
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

	private bool IsUltraHighResolutionSelected()
	{
		return MapWidth >= 4096 || MapHeight >= 2048;
	}

	private void ShowMapSizeWarningIfNeeded()
	{
		if (!IsUltraHighResolutionSelected())
		{
			return;
		}

		_infoLabel.Text = UltraResolutionWarningText;
	}

	private void OnGeneratePressed()
	{
		Seed = (int)_seedSpin.Value;
		PlateCount = Mathf.Clamp((int)_plateSpin.Value, 1, 200);
		WindCellCount = Mathf.Clamp((int)_windSpin.Value, 1, 100);
		ApplyMapSizePreset(_mapSizeOption.GetSelectedId());
		ShowMapSizeWarningIfNeeded();
		GenerateWorld();
	}

	private void OnRandomPressed()
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		Seed = rng.RandiRange(int.MinValue, int.MaxValue);
		_seedSpin.Value = Seed;
		GenerateWorld();
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

	private void UpdateLabels()
	{
		_seaLevelValue.Text = SeaLevel.ToString("0.00");
		_heatValue.Text = HeatFactor.ToString("0.00");
		_erosionValue.Text = ErosionIterations.ToString();
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

		try
		{
			await SetProgressAsync(2f, IsUltraHighResolutionSelected() ? "准备中（超高分辨率）" : "准备中");

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

			await SetProgressAsync(97f, "渲染中");
			RedrawCurrentLayer();
			await SetProgressAsync(100f, "完成");
		}
		finally
		{
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

	private async Task<GeneratedWorldData> BuildWorldAsync(WorldTuning tuning, string label, float startProgress, float endProgress)
	{
		const int totalSteps = 10;
		var step = 0;

		var plateResult = await Task.Run(() => _plateGenerator.Generate(MapWidth, MapHeight, PlateCount, Seed, 0.5f));
		await SetBuildProgressAsync(label, "板块", ++step, totalSteps, startProgress, endProgress);

		var resourceTask = Task.Run(() => _resourceGenerator.Generate(MapWidth, MapHeight, Seed, plateResult.BoundaryTypes));

		var elevation = await Task.Run(() => _elevationGenerator.Generate(MapWidth, MapHeight, Seed, SeaLevel, plateResult));
		await SetBuildProgressAsync(label, "地形", ++step, totalSteps, startProgress, endProgress);

		var waterLayer = Array2D.Create(MapWidth, MapHeight, 1f);
		var emptyRiverLayer = Array2D.Create(MapWidth, MapHeight, 0f);
		await Task.Run(() => _erosionSimulator.Run(MapWidth, MapHeight, ErosionIterations, elevation, waterLayer, emptyRiverLayer));
		var targetOceanRatio = MapSeaLevelToTargetOceanRatio(SeaLevel);
		elevation = NormalizeElevationForPipeline(elevation, MapWidth, MapHeight, SeaLevel, targetOceanRatio);
		await SetBuildProgressAsync(label, "侵蚀", ++step, totalSteps, startProgress, endProgress);

		var temperatureTask = Task.Run(() => _temperatureGenerator.Generate(MapWidth, MapHeight, Seed, elevation, HeatFactor, RandomHeatFactor));
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
			? await Task.Run(() => _riverGenerator.Generate(MapWidth, MapHeight, Seed, SeaLevel, elevation, moisture, tuning))
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
		if (elapsedSeconds < 0.05)
		{
			return $"{status} | 预计剩余 --";
		}

		var totalSeconds = elapsedSeconds / (progress / 100.0);
		var remainingSeconds = Math.Max(totalSeconds - elapsedSeconds, 0.0);
		return $"{status} | 预计剩余 {FormatDuration(remainingSeconds)}";
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


	private float GetEffectiveHeatFactorPreview()
	{
		if (!RandomHeatFactor)
		{
			return Mathf.Pow(Mathf.Clamp(HeatFactor, 0.01f, 1f), 1f / 3f);
		}

		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(uint)(Seed ^ 0x45d9f3b);
		return Mathf.Pow(rng.Randf(), 1f / 3f);
	}

	private string BuildHeatExpectationText()
	{
		var effectiveHeat = Mathf.Clamp(GetEffectiveHeatFactorPreview(), 0.01f, 1f);
		var warmBandStart = Mathf.Clamp(0.5f * effectiveHeat, 0f, 1f);
		var warmBandEnd = Mathf.Clamp(1f - 0.5f * effectiveHeat, 0f, 1f);
		var modeText = RandomHeatFactor ? "RandomBySeed" : "Manual";

		return $"HeatMode:{modeText} | HeatEff:{effectiveHeat:0.000} | WarmBand:{warmBandStart * 100f:0.0}-{warmBandEnd * 100f:0.0}%";
	}


	private void RedrawCurrentLayer()
	{
		if (_primaryWorld == null)
		{
			return;
		}

		var selectedId = _layerOption.GetSelectedId();
		var layer = Enum.IsDefined(typeof(MapLayer), selectedId) ? (MapLayer)selectedId : MapLayer.Satellite;

		var primaryImage = RenderLayer(_primaryWorld, layer);
		_mapTexture.Texture = ImageTexture.CreateFromImage(primaryImage);
		_lastRenderedImage = primaryImage;

		if (_compareMode && _compareWorld != null)
		{
			var compareImage = RenderLayer(_compareWorld, layer);
			_lastCompareImage = compareImage;

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

		var stats = _primaryWorld.Stats;
		var warningPrefix = IsUltraHighResolutionSelected() ? "⚠UltraRes | " : string.Empty;
		var oceanTargetPercent = 100f * MapSeaLevelToTargetOceanRatio(SeaLevel);
		var heatExpectation = BuildHeatExpectationText();
		_infoLabel.Text =
			$"{warningPrefix}Preset:{_primaryWorld.Tuning.Name} | Seed:{Seed} | Size:{MapWidth}x{MapHeight} | Plates:{PlateCount} | Wind:{WindCellCount} | Erosion:{ErosionIterations} | Cities:{_primaryWorld.Cities.Count} | Sea:{SeaLevel:0.00} | OceanTarget:{oceanTargetPercent:0.0}% | Heat:{HeatFactor:0.00} | {heatExpectation} | Layer:{layer} | Ocean:{stats.OceanPercent:0.0}% | River:{stats.RiverPercent:0.00}% | Tavg:{stats.AvgTemperature:0.000} | Mavg:{stats.AvgMoisture:0.000}";
	}

	private Image RenderLayer(GeneratedWorldData world, MapLayer layer)
	{
		return _renderer.Render(
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
			SeaLevel);
	}

	private string BuildCompareSummary(GeneratedWorldData primary, GeneratedWorldData compare)
	{
		var dOcean = primary.Stats.OceanPercent - compare.Stats.OceanPercent;
		var dRiver = primary.Stats.RiverPercent - compare.Stats.RiverPercent;
		var dTemp = primary.Stats.AvgTemperature - compare.Stats.AvgTemperature;
		var dMoist = primary.Stats.AvgMoisture - compare.Stats.AvgMoisture;
		var dCities = primary.Stats.CityCount - compare.Stats.CityCount;

		return $"A:{primary.Tuning.Name}  B:{compare.Tuning.Name} | ΔOcean:{dOcean:+0.00;-0.00;0.00}%  ΔRiver:{dRiver:+0.00;-0.00;0.00}%  ΔTemp:{dTemp:+0.000;-0.000;0.000}  ΔMoist:{dMoist:+0.000;-0.000;0.000}  ΔCities:{dCities:+0;-0;0}";
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

		EnsureExportDirectory();
		var timestamp = (long)Time.GetUnixTimeFromSystem();

		var primaryPath = $"user://exports/world_{Seed}_{timestamp}_A.png";
		var primaryError = _lastRenderedImage.SavePng(primaryPath);

		if (_compareMode && _lastCompareImage != null)
		{
			var comparePath = $"user://exports/world_{Seed}_{timestamp}_B.png";
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

	private void OnExportJsonPressed()
	{
		if (_primaryWorld == null)
		{
			_infoLabel.Text = "Nothing to export yet. Generate first.";
			return;
		}

		EnsureExportDirectory();
		var timestamp = (long)Time.GetUnixTimeFromSystem();
		var path = $"user://exports/world_{Seed}_{timestamp}.json";

		var payload = new Godot.Collections.Dictionary
		{
			["seed"] = Seed,
			["width"] = MapWidth,
			["height"] = MapHeight,
			["plates"] = PlateCount,
			["wind_cells"] = WindCellCount,
			["sea_level"] = SeaLevel,
			["heat"] = HeatFactor,
			["erosion"] = ErosionIterations,
			["compare_mode"] = _compareMode,
			["primary"] = BuildWorldDictionary(_primaryWorld)
		};

		if (_compareMode && _compareWorld != null)
		{
			payload["compare"] = BuildWorldDictionary(_compareWorld);
		}

		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			_infoLabel.Text = "JSON export failed: cannot open file.";
			return;
		}

		file.StoreString(Json.Stringify(payload, "  "));
		_infoLabel.Text = $"JSON exported: {path}";
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


	private T GetNodeByName<T>(string name) where T : Node
	{
		var node = FindChild(name, true, false);
		if (node is not T typed)
		{
			throw new InvalidOperationException($"Node '{name}' not found or not of type {typeof(T).Name}.");
		}

		return typed;
	}

	private void EnsureExportDirectory()
	{
		var dir = DirAccess.Open("user://");
		if (dir == null)
		{
			return;
		}

		if (!dir.DirExists("exports"))
		{
			dir.MakeDir("exports");
		}
	}
}
