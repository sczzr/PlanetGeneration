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

}
