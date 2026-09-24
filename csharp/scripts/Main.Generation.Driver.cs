using PlanetGeneration.Application;
using Godot;
using PlanetGeneration.WorldGen;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PlanetGeneration;

public partial class Main : Control
{
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
		var generationTimer = Stopwatch.StartNew();
		var requestId = ++_generationRequestId;
		_isGenerating = true;
		SetGenerationUiState(true);
		_pendingRegenerate = false;
		_generationStartedMsec = Time.GetTicksMsec();
		_progressOverlay.Visible = true;
		RandomizeReliefExaggeration();
		var generationSucceeded = false;
		var generatedFromScratch = false;

		try
		{
			// 在第一次 await 前捕获完整输入。缓存键和两组生成使用同一份不可变参数。
			var options = BuildGenerationOptions(_tuning, Seed);
			var comparisonOptions = _compareMode ? BuildGenerationOptions(GetAlternateTuning(_tuning), Seed + 1) : null;
			var generationCacheKey = PlanetGeneration.Core.Domain.WorldGenerationCacheKey.BuildSession(options, comparisonOptions);
			if (TryGetWorldGenerationCache(generationCacheKey, out var cachedPrimary, out var cachedCompare))
			{
				_primaryWorld = cachedPrimary;
				_compareWorld = cachedCompare;
				await SetProgressAsync(97f, "读取快照缓存并渲染");
				RedrawCurrentLayer();
				UpdateCellCountDisplay();
				await SetProgressAsync(100f, "完成（缓存）");
				generationSucceeded = true;
				return;
			}

			await EnsurePerformanceSampleAsync();
			_currentGenerationWorkUnits = EstimateGenerationWorkUnits();
			_predictedTotalSeconds = Math.Max(_currentGenerationWorkUnits * _secondsPerWorkUnit, 0.1);
			await SetProgressAsync(2f, "准备生成世界快照");
			var acceptingProgress = true;
			var progress = new Progress<(float Progress, string Stage)>(p =>
			{
				if (acceptingProgress && _isGenerating && requestId == _generationRequestId)
					ReportGenerationProgress(p.Progress * 0.94f, p.Stage);
			});
			var controller = new SnapshotGenerationController(new PlanetGeneration.Rendering.BaseFieldGeneratorAdapter());
			var worlds = await controller.GenerateAsync(options, comparisonOptions, progress);
			acceptingProgress = false;

			// 两组均成功后才发布，避免 B 组失败时留下半更新的会话。
			_primaryWorld = worlds.Primary;
			_compareWorld = worlds.Comparison;
			UpdateCellCountDisplay();
			StoreWorldGenerationCache(generationCacheKey, worlds.Primary, worlds.Comparison);
			generatedFromScratch = true;
			await SetProgressAsync(97f, "渲染中");
			RedrawCurrentLayer();
			await SetProgressAsync(100f, "完成");
			generationSucceeded = true;
			LogGenerationTiming("快照生成与投影", generationTimer.Elapsed);
		}
		catch (Exception ex)
		{
			GD.PushError($"[WorldGen] 生成失败: {ex}");
			_infoLabel.Text = $"世界生成失败：{ex.Message}";
		}
		finally
		{
			if (generationSucceeded && generatedFromScratch) RecordGenerationThroughput();
			_isGenerating = false;
			SetGenerationUiState(false);
			if (_pendingRegenerate)
			{
				_pendingRegenerate = false;
				GenerateWorld();
			}
			else
			{
				_progressOverlay.Visible = false;
				if (_mainMenu != null && _mainMenu.Visible)
				{
					_mainMenu.Call("end_loading");
					if (generationSucceeded) _mainMenu.Visible = false;
				}
				if (generationSucceeded)
				{
					SetConsolePanelVisible(true);
					SetInGameHudVisible(true);
				}
			}
		}
	}

	private void SetGenerationUiState(bool active)
	{
		if (!IsInstanceValid(_generateButton) || !IsInstanceValid(_randomButton) || !IsInstanceValid(_progressOverlay))
		{
			return;
		}

		_generateButton.Disabled = active;
		_randomButton.Disabled = active;
		_generationUiTween?.Kill();
		_generationUiTween = CreateTween().SetParallel(true);
		_generationUiTween.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		_generationUiTween.TweenProperty(_progressOverlay, "modulate:a", active ? 1.0f : 0.0f, active ? 0.18f : 0.24f);
		_generationUiTween.TweenProperty(_generateButton, "scale", active ? new Vector2(0.98f, 0.98f) : Vector2.One, 0.18f);
		if (active)
		{
			_progressOverlay.Visible = true;
		}
		else
		{
			_generationUiTween.Finished += () =>
			{
				if (!_isGenerating && IsInstanceValid(_progressOverlay))
				{
					_progressOverlay.Visible = false;
				}
			};
		}
	}


	private void LogGenerationTiming(string stage, TimeSpan elapsed)
	{
		GD.Print($"[WorldGen][地图 {MapWidth}x{MapHeight}] {stage}: {elapsed.TotalMilliseconds:0} ms ({elapsed.TotalSeconds:0.00} s)");
	}

	private async Task SetProgressAsync(float value, string status)
	{
		ReportGenerationProgress(value, status);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private void ReportGenerationProgress(float value, string status)
	{
		var clampedValue = Mathf.Clamp(value, 0f, 100f);
		if (_mainMenu != null && _mainMenu.Visible)
		{
			_mainMenu.Call("set_loading_progress", clampedValue / 100f);
		}
		_progressTween?.Kill();
		_progressTween = CreateTween();
		_progressTween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		_progressTween.TweenProperty(_generateProgress, "value", clampedValue, 0.16f);
		_progressStatus.Text = BuildProgressStatus(status, clampedValue);
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

	private PlanetGeneration.Core.Domain.GenerationOptions BuildGenerationOptions(WorldTuning tuning, int seed)
	{
		return new PlanetGeneration.Core.Domain.GenerationOptions
		{
			Seed = seed,
			Tuning = new PlanetGeneration.Core.Domain.WorldTuningSnapshot
			{
				Name = tuning.Name,
				DeepOceanFactor = tuning.DeepOceanFactor,
				CoastBand = tuning.CoastBand,
				MountainThreshold = tuning.MountainThreshold,
				RiverSourceElevationThreshold = tuning.RiverSourceElevationThreshold,
				RiverSourceMoistureThreshold = tuning.RiverSourceMoistureThreshold,
				RiverSourceChance = tuning.RiverSourceChance
			},
			TargetCellCount = _targetCellCount,
			Extent = new PlanetGeneration.Core.Domain.WorldExtent(MapWidth, MapHeight),
			SeaLevel = SeaLevel,
			HeatFactor = HeatFactor,
			MoistureFactor = MoistureFactor,
			EnableRivers = EnableRivers,
			RiverDensity = RiverDensity,
			ErosionIterations = ErosionIterations,
			MoistureIterations = MoistureIterations,
			PlateCount = PlateCount,
			OceanicRatio = _terrainOceanicRatio,
			ContinentBias = _terrainContinentBias,
			InteriorRelief = _interiorRelief,
			OrogenyStrength = _orogenyStrength,
			SubductionArcRatio = _subductionArcRatio,
			ContinentalAge = _continentalAge,
			Morphology = (PlanetGeneration.Core.Domain.TerrainMorphology)_terrainMorphology,
			ContinentCount = _continentCount,
			WindCellCount = WindCellCount,
			BasinSensitivity = BasinSensitivity,
			LandformTuning = LandformTuning with { BasinSensitivity = BasinSensitivity },
			SpeciesDiversity = _speciesDiversity,
			CivilAggression = _civilAggression,
			MagicDensity = _magicDensity,
			Epoch = _currentEpoch,
			EnableCartographyDesigner = _enableCartographyDesigner,
			BlueprintName = _blueprintName
		};
	}
}
