using Godot;
using PlanetGeneration.LLM;
using PlanetGeneration.WorldGen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlanetGeneration
{
	public sealed class CivilizationOracle
	{
		private LLamaInferenceService? _llmService;
		private QuestDraftService? _questDraftService;
		private LlmGenerationScheduler? _generationScheduler;
		private LlmRuntimeProfile? _runtimeProfile;
		private readonly string _modelsDirectory;
		private CancellationTokenSource? _currentInferenceCts;
		private bool _isInferring;

		public bool IsModelLoaded => _llmService?.IsLoaded ?? false;
		public bool IsInferring => _isInferring;
		public string ModelPath => _llmService?.ModelPath ?? "";

		public event Action<string>? StatusChanged;
		public event Action<string>? OutputReceived;
		public event Action<bool>? InferenceStateChanged;

		private static void OracleLog(string message)
		{
			GD.Print($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Oracle] {message}");
		}

		private static void OracleLogError(string message)
		{
			GD.PrintErr($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Oracle] {message}");
		}

	public CivilizationOracle()
	{
		_modelsDirectory = Path.Combine(OS.GetUserDataDir(), "..", "..", "models");
	}

		private void EnsureService()
		{
			_llmService ??= new LLamaInferenceService();
			_questDraftService ??= new QuestDraftService(_llmService);
			_generationScheduler ??= new LlmGenerationScheduler(_llmService);
		}

	public bool LoadModel(string? customPath = null)
	{
		EnsureService();

		_runtimeProfile = LlmRuntimeSelector.BuildRuntimeProfile(SystemRequirements.GetTotalMemoryGB());
		var runtimeProfile = _runtimeProfile;
		var selectedModel = string.IsNullOrWhiteSpace(customPath)
			? FindBestModel(runtimeProfile)
			: BuildCandidateFromPath(customPath!);
		if (selectedModel == null)
		{
			OracleLogError("No model file found!");
			StatusChanged?.Invoke("未找到可用模型（.gguf）");
			return false;
		}

		var modelPath = selectedModel.FullPath;
		var modelSizeMB = selectedModel.FileSizeBytes / 1024 / 1024;
		var profileSummary = LlmRuntimeSelector.BuildProfileSummary(runtimeProfile);
		StatusChanged?.Invoke($"正在加载: {selectedModel.FileName} ({modelSizeMB} MB) | {profileSummary}");

		Task.Run(async () =>
		{
			try
			{
					var loadOptions = runtimeProfile.ToLoadOptions();
				var loaded = await _llmService!.LoadModelAsync(modelPath, loadOptions);

					var activeOptions = _llmService.ActiveLoadOptions ?? loadOptions;
					var runtimeBackend = _llmService.RuntimeBackend;
					var runtimeMode = activeOptions.GpuLayerCount > 0
						? $"GPU({runtimeBackend}, {activeOptions.GpuLayerCount}层)"
						: "CPU";
					var decisionReason = runtimeProfile.DecisionReason;
					if (runtimeProfile.UseGpuOffload && activeOptions.GpuLayerCount == 0)
					{
						decisionReason = "GPU 加载失败，已自动回退 CPU 推理。";
					}

					var status = loaded
						? $"已加载: {selectedModel.FileName} ({modelSizeMB} MB) | {runtimeMode} | ctx={activeOptions.ContextSize} | {decisionReason}"
						: "模型加载失败";
				StatusChanged?.Invoke(status);
				if (loaded)
				{
					OracleLog($"Runtime profile: {profileSummary}");
					OracleLog($"Decision reason: {runtimeProfile.DecisionReason}");
				}
				LoadComplete?.Invoke(loaded);
			}
			catch (Exception ex)
			{
				OracleLogError($"Load error: {ex.Message}");
				StatusChanged?.Invoke($"加载错误: {ex.Message}");
				LoadComplete?.Invoke(false);
			}
		});

		return true;
	}
	
	public event Action<bool>? LoadComplete;

	public void UnloadModel()
	{
		CancelCurrentInference();
		_llmService?.UnloadModel();
		_runtimeProfile = null;
		StatusChanged?.Invoke("模型已卸载");
	}

	private LlmModelCandidate? FindBestModel(LlmRuntimeProfile profile)
	{
		var searchPaths = BuildModelSearchPaths();
		var candidates = LlmRuntimeSelector.DiscoverModelCandidates(searchPaths);
		if (candidates.Count == 0)
		{
			OracleLog("No model file found in search paths");
			return null;
		}

		var selected = LlmRuntimeSelector.SelectBestModel(candidates, profile);
		if (selected == null)
		{
			OracleLog("No model candidate selected");
			return null;
		}

		var paramInfo = selected.ParamsBillions.HasValue
			? $"{selected.ParamsBillions.Value:0.##}B"
			: "未知参数量";
		OracleLog($"Selected model: {selected.FileName} ({selected.FileSizeBytes / 1024 / 1024} MB, {paramInfo})");
		return selected;
	}

	private static LlmModelCandidate? BuildCandidateFromPath(string modelPath)
	{
		var resolvedPath = modelPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
			? ProjectSettings.GlobalizePath(modelPath)
			: modelPath;
		if (!File.Exists(resolvedPath))
		{
			return null;
		}

		var info = new FileInfo(resolvedPath);
		return new LlmModelCandidate
		{
			FullPath = resolvedPath,
			FileName = info.Name,
			FileSizeBytes = info.Length,
			FileSizeGB = info.Length / 1024d / 1024d / 1024d,
			ParamsBillions = null
		};
	}

	private IEnumerable<string> BuildModelSearchPaths()
	{
		var paths = new[]
		{
			ProjectSettings.GlobalizePath("res://models"),
			_modelsDirectory,
			Path.Combine(OS.GetUserDataDir(), "..", "..", "models"),
			Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models"),
			"E:\\source\\PlanetGeneration\\procgenesis_local\\csharp\\models\\"
		};

		return paths
			.Where(path => !string.IsNullOrWhiteSpace(path))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	private static string NormalizeAnalysisOutput(string raw, int maxChars, int maxListItems)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return string.Empty;
		}

		var text = raw.Replace("\r", string.Empty).Trim();
		var lines = text
			.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(line => !string.IsNullOrWhiteSpace(line))
			.ToList();

		var bulletLines = new List<string>();
		foreach (var line in lines)
		{
			if (char.IsDigit(line[0]) && line.Length > 1 && line[1] == '.')
			{
				bulletLines.Add(line);
				continue;
			}

			if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("• ", StringComparison.Ordinal))
			{
				bulletLines.Add(line);
				continue;
			}

			if (bulletLines.Count > 0)
			{
				break;
			}
		}

		if (bulletLines.Count > 0)
		{
			var compact = string.Join("\n", bulletLines.Take(Math.Max(1, maxListItems))).Trim();
			return compact.Length <= maxChars
				? compact
				: compact.Substring(0, Math.Max(0, maxChars)).TrimEnd();
		}

		var metaMarkers = new[]
		{
			"这个回答符合要求",
			"但是，我需要检查",
			"但是我需要检查",
			"让我检查",
			"我需要检查",
			"思考过程",
			"推理过程"
		};

		var cutIndex = -1;
		foreach (var marker in metaMarkers)
		{
			var index = text.IndexOf(marker, StringComparison.Ordinal);
			if (index < 0)
			{
				continue;
			}

			if (cutIndex < 0 || index < cutIndex)
			{
				cutIndex = index;
			}
		}

		var normalized = cutIndex > 0 ? text.Substring(0, cutIndex).Trim() : text;
		if (normalized.Length > maxChars)
		{
			normalized = normalized.Substring(0, maxChars).TrimEnd();
		}

		return normalized;
	}

	private static string BuildWorldAnalysisCacheKey(CivilizationSimulationResult civilization, int epoch)
	{
		return string.Join(
			"|",
			"world",
			epoch,
			civilization.PolityCount,
			civilization.CityStateCount,
			MathF.Round(civilization.ConflictHeatPercent, 1),
			MathF.Round(civilization.AllianceCohesionPercent, 1),
			MathF.Round(civilization.BorderVolatilityPercent, 1),
			MathF.Round(civilization.ControlledLandPercent, 1));
	}

	private static string BuildRegionAnalysisCacheKey(CivilizationSimulationResult civilization, int x, int y, int epoch)
	{
		return string.Join(
			"|",
			"region",
			epoch,
			x,
			y,
			civilization.PolityId[x, y],
			MathF.Round(civilization.Influence[x, y], 2),
			civilization.BorderMask[x, y],
			civilization.TradeRouteMask[x, y]);
	}

	public async Task<string> AnalyzeCurrentWorldState(
		CivilizationSimulationResult? civilization,
		int epoch)
	{
		if (!IsModelLoaded)
		{
			return "[错误] 请先加载模型";
		}

		if (civilization == null)
		{
			return "[错误] 文明模拟数据不可用";
		}

		CancelCurrentInference();
		_currentInferenceCts = new CancellationTokenSource();
		_isInferring = true;
		InferenceStateChanged?.Invoke(true);

		try
		{
			EnsureService();
			StatusChanged?.Invoke("正在分析世界状态...");

			var prompt = CivilizationPromptBuilder.BuildEpochAnalysisPrompt(
				epoch: epoch,
				polityCount: civilization.PolityCount,
				hamletCount: civilization.HamletCount,
				townCount: civilization.TownCount,
				cityStateCount: civilization.CityStateCount,
				controlledLandPercent: civilization.ControlledLandPercent,
				conflictHeatPercent: civilization.ConflictHeatPercent,
				allianceCohesionPercent: civilization.AllianceCohesionPercent,
				borderVolatilityPercent: civilization.BorderVolatilityPercent,
				recentEvents: civilization.RecentEvents
			);

			var rawResult = await _generationScheduler!.GenerateTextAsync(
				new LlmGenerationRequest
				{
					PolicyKey = "world_analysis",
					CacheKey = BuildWorldAnalysisCacheKey(civilization, epoch),
					Prompt = prompt,
					SystemPrompt = CivilizationPromptBuilder.GetSystemPrompt(),
					MaxTokens = 160,
					Temperature = 0.55f,
					RetryCount = 0,
					TimeoutSeconds = 45,
					CacheTtlSeconds = 240,
					FallbackFactory = static () => "1. 世界局势短期稳定\n2. 建议继续观察贸易与边境变化\n3. 下一纪元重点关注联盟波动"
				},
				_currentInferenceCts.Token);

			var result = NormalizeAnalysisOutput(rawResult, maxChars: 800, maxListItems: 8);

			StatusChanged?.Invoke("分析完成");
			OutputReceived?.Invoke(result);
			
			return result;
		}
		catch (OperationCanceledException)
		{
			return "[已取消]";
		}
		catch (Exception ex)
		{
			StatusChanged?.Invoke($"错误: {ex.Message}");
			return $"[错误: {ex.Message}]";
		}
		finally
		{
			_isInferring = false;
			InferenceStateChanged?.Invoke(false);
		}
	}

	public async Task<string> AnalyzeRegion(
		CivilizationSimulationResult? civilization, 
		int x, 
		int y,
		int epoch,
		string biomeName,
		string landformName)
	{
		if (!IsModelLoaded)
		{
			return "[错误] 请先加载模型";
		}

		if (civilization == null)
		{
			return "[错误] 文明模拟数据不可用";
		}

		CancelCurrentInference();
		_currentInferenceCts = new CancellationTokenSource();
		_isInferring = true;
		InferenceStateChanged?.Invoke(true);

		try
		{
			EnsureService();
			StatusChanged?.Invoke($"正在分析区域 ({x}, {y})...");

			var polityId = civilization.PolityId[x, y];
			var influence = civilization.Influence[x, y];
			var isBorder = civilization.BorderMask[x, y];
			var hasTradeRoute = civilization.TradeRouteMask[x, y];

			var prompt = CivilizationPromptBuilder.BuildRegionAnalysisPrompt(
				x, y, biomeName, landformName,
				polityId, influence, isBorder, hasTradeRoute,
				epoch
			);

			var rawResult = await _generationScheduler!.GenerateTextAsync(
				new LlmGenerationRequest
				{
					PolicyKey = "region_analysis",
					CacheKey = BuildRegionAnalysisCacheKey(civilization, x, y, epoch),
					Prompt = prompt,
					SystemPrompt = CivilizationPromptBuilder.GetSystemPrompt(),
					MaxTokens = 128,
					Temperature = 0.55f,
					RetryCount = 0,
					TimeoutSeconds = 30,
					CacheTtlSeconds = 180,
					FallbackFactory = () => $"1. 区域({x},{y})维持当前格局\n2. 建议关注边境与贸易通路变化"
				},
				_currentInferenceCts.Token);

			var result = NormalizeAnalysisOutput(rawResult, maxChars: 500, maxListItems: 6);

			StatusChanged?.Invoke("区域分析完成");
			OutputReceived?.Invoke(result);
			
			return result;
		}
		catch (OperationCanceledException)
		{
			return "[已取消]";
		}
		catch (Exception ex)
		{
			StatusChanged?.Invoke($"错误: {ex.Message}");
			return $"[错误: {ex.Message}]";
		}
		finally
		{
			_isInferring = false;
			InferenceStateChanged?.Invoke(false);
		}
	}

	public async Task<string> GenerateHistoricalEvent(
		CivilizationSimulationResult? civilization,
		int epoch,
		string category,
		string baseEvent)
	{
		if (!IsModelLoaded)
		{
			return "[错误] 请先加载模型";
		}

		CancelCurrentInference();
		_currentInferenceCts = new CancellationTokenSource();
		_isInferring = true;
		InferenceStateChanged?.Invoke(true);

		try
		{
			StatusChanged?.Invoke("正在生成历史事件...");

			var conflictLevel = civilization?.ConflictHeatPercent ?? 50f;
			var allianceLevel = civilization?.AllianceCohesionPercent ?? 50f;
			var tradeLevel = civilization?.ConnectedHubPercent ?? 50f;

			var prompt = $@"基于以下背景信息，生成一个第{epoch}纪元的{category}事件描述：

当前世界状态：
- 冲突热度: {conflictLevel:F1}%
- 联盟凝聚力: {allianceLevel:F1}%
- 贸易连通性: {tradeLevel:F1}%

基础设定: {baseEvent}

请生成3-5个可能的事件发展，用中文描述，每个事件不超过50字。";

			var result = await _llmService!.InferAsync(
				prompt,
				"你是一个奇幻世界观的历史记录者，擅长生成引人入胜的历史事件描述。",
				maxTokens: 300,
				temperature: 0.8f,
				ct: _currentInferenceCts.Token
			);

			StatusChanged?.Invoke("事件生成完成");
			OutputReceived?.Invoke(result);
			
			return result;
		}
		catch (OperationCanceledException)
		{
			return "[已取消]";
		}
		catch (Exception ex)
		{
			StatusChanged?.Invoke($"错误: {ex.Message}");
			return $"[错误: {ex.Message}]";
		}
		finally
		{
			_isInferring = false;
			InferenceStateChanged?.Invoke(false);
		}
	}

		public async Task<QuestDraftGenerationResult?> GenerateQuestDraft(
			CivilizationSimulationResult? civilization,
			int epoch)
		{
			if (!IsModelLoaded)
			{
				StatusChanged?.Invoke("请先加载模型");
				return null;
			}

			if (civilization == null)
			{
				StatusChanged?.Invoke("文明模拟数据不可用");
				return null;
			}

			EnsureService();
			CancelCurrentInference();
			_currentInferenceCts = new CancellationTokenSource();
			_isInferring = true;
			InferenceStateChanged?.Invoke(true);

			try
			{
				StatusChanged?.Invoke("正在生成结构化任务草案...");
				var result = await _questDraftService!.GenerateAsync(civilization, epoch, _currentInferenceCts.Token);
				StatusChanged?.Invoke(result.UsedFallback ? "任务草案已回退模板" : "任务草案生成完成");
				return result;
			}
			catch (OperationCanceledException)
			{
				StatusChanged?.Invoke("任务草案生成已取消");
				return null;
			}
			catch (Exception ex)
			{
				StatusChanged?.Invoke($"任务草案生成错误: {ex.Message}");
				return null;
			}
			finally
			{
				_isInferring = false;
				InferenceStateChanged?.Invoke(false);
			}
		}

	public void CancelCurrentInference()
	{
		_currentInferenceCts?.Cancel();
		_currentInferenceCts?.Dispose();
		_currentInferenceCts = null;
	}

	public void Dispose()
	{
		CancelCurrentInference();
		_generationScheduler = null;
		_questDraftService = null;
		_runtimeProfile = null;
		_llmService?.Dispose();
	}
}

public static class SystemRequirements
{
	private static long? _cachedMemoryMB;

	private static void RequirementsLog(string message)
	{
		GD.Print($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [SystemRequirements] {message}");
	}

	private static void RequirementsLogError(string message)
	{
		GD.PrintErr($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [SystemRequirements] {message}");
	}

	public static long GetTotalMemoryMB()
	{
		if (_cachedMemoryMB.HasValue)
		{
			return _cachedMemoryMB.Value;
		}

		try
		{
			var gcInfo = GC.GetGCMemoryInfo();
			if (gcInfo.TotalAvailableMemoryBytes > 0)
			{
				_cachedMemoryMB = gcInfo.TotalAvailableMemoryBytes / 1024 / 1024;
				return _cachedMemoryMB.Value;
			}
		}
		catch (Exception ex)
		{
			RequirementsLogError($"Failed to get memory via GC: {ex.Message}");
		}

		_cachedMemoryMB = 8192;
		RequirementsLog("Using default memory: 8GB");
		return _cachedMemoryMB.Value;
	}

	public static long GetTotalMemoryGB()
	{
		return GetTotalMemoryMB() / 1024;
	}
}
}
