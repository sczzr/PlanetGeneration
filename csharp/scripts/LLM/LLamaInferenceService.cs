using Godot;
using PlanetGeneration.WorldGen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace PlanetGeneration.LLM;

public sealed class LLamaInferenceService : IDisposable
{
    private LLamaWeights? _model;
    private StatelessExecutor? _executor;
    private ModelParams? _modelParams;
    private bool _isLoaded;
    private string _modelPath = string.Empty;
    private readonly object _lock = new();
    private LlmLoadOptions? _activeLoadOptions;
    private string _runtimeBackend = "unknown";

    public bool IsLoaded => _isLoaded;
    public string ModelPath => _modelPath;
    public LlmLoadOptions? ActiveLoadOptions => _activeLoadOptions;
    public string RuntimeBackend => _runtimeBackend;

    private static void Log(string message)
    {
        GD.Print($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [LLM] {message}");
    }

    private static void LogError(string message)
    {
        GD.PrintErr($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [LLM] {message}");
    }

    public async Task<bool> LoadModelAsync(string modelPath, int contextSize = 2048, CancellationToken ct = default)
    {
        return await LoadModelAsync(
            modelPath,
            new LlmLoadOptions
            {
                ContextSize = contextSize,
                GpuLayerCount = 32,
                UseMemorymap = false,
                UseMemoryLock = false,
                AllowGpuFallbackToCpu = true
            },
            ct);
    }

    public async Task<bool> LoadModelAsync(string modelPath, LlmLoadOptions options, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var effectiveOptions = options;
            var attempts = new List<LlmLoadOptions> { effectiveOptions };

            if (effectiveOptions.AllowGpuFallbackToCpu && effectiveOptions.GpuLayerCount > 0)
            {
                attempts.Add(effectiveOptions.WithGpuLayerCount(0));
            }

            foreach (var attempt in attempts)
            {
                if (TryLoadModel(modelPath, attempt))
                {
                    return true;
                }
            }

            return false;
        }, ct);
    }

    private bool TryLoadModel(string modelPath, LlmLoadOptions options)
    {
        lock (_lock)
        {
            ResetModelStateLocked();
        }

        LLamaWeights? tempModel = null;
        StatelessExecutor? tempExecutor = null;

        try
        {
            Log($"Loading model from: {modelPath}");
            Log($"File exists: {File.Exists(modelPath)}");
            Log($"File size: {new FileInfo(modelPath).Length / 1024 / 1024} MB");
            Log($"Load options: ctx={options.ContextSize}, gpu_layers={options.GpuLayerCount}, mmap={options.UseMemorymap}, mlock={options.UseMemoryLock}");

            var modelParams = new ModelParams(modelPath)
            {
                ContextSize = (uint)Math.Max(512, options.ContextSize),
                GpuLayerCount = Math.Max(0, options.GpuLayerCount),
                UseMemorymap = options.UseMemorymap,
                UseMemoryLock = options.UseMemoryLock
            };

            Log("Creating LLamaWeights...");
            tempModel = LLamaWeights.LoadFromFile(modelParams);
            Log("LLamaWeights created successfully");
            Log("Creating executor...");
            tempExecutor = new StatelessExecutor(tempModel, modelParams);

            lock (_lock)
            {
                _model = tempModel;
                _executor = tempExecutor;
                _modelParams = modelParams;
                _modelPath = modelPath;
                _isLoaded = true;
                _activeLoadOptions = options;
                _runtimeBackend = DetectRuntimeBackend(modelParams.GpuLayerCount);
            }
            tempModel = null;
            tempExecutor = null;

            Log($"Model loaded successfully: {Path.GetFileName(modelPath)}");
            Log($"Runtime backend: {_runtimeBackend}, gpu_layers={modelParams.GpuLayerCount}, ctx={modelParams.ContextSize}");

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to load model (gpu_layers={options.GpuLayerCount}): {ex.Message}");
            LogError($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                LogError($"Inner exception: {ex.InnerException.Message}");
            }

            lock (_lock)
            {
                ResetModelStateLocked();
            }

            tempModel?.Dispose();

            return false;
        }
    }

    public void UnloadModel()
    {
        lock (_lock)
        {
            ResetModelStateLocked();
        }

        Log("Model unloaded");
    }

    private void ResetModelStateLocked()
    {
        _model?.Dispose();
        _model = null;
        _executor = null;
        _modelParams = null;
        _isLoaded = false;
        _modelPath = string.Empty;
        _activeLoadOptions = null;
        _runtimeBackend = "unknown";
    }

    private static string DetectRuntimeBackend(int gpuLayerCount)
    {
        if (gpuLayerCount <= 0)
        {
            return "CPU";
        }

        var backendHints = new[]
        {
            (Marker: "backend.vulkan", Label: "Vulkan"),
            (Marker: "backend.cuda", Label: "CUDA"),
            (Marker: "backend.metal", Label: "Metal"),
            (Marker: "backend.opencl", Label: "OpenCL")
        };

        try
        {
            var assemblyNames = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblyNames)
            {
                var name = assembly.GetName().Name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                foreach (var hint in backendHints)
                {
                    if (name.IndexOf(hint.Marker, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return hint.Label;
                    }
                }
            }

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDirectory) && Directory.Exists(baseDirectory))
            {
                foreach (var dllPath in Directory.EnumerateFiles(baseDirectory, "*LLamaSharp.Backend*.dll", SearchOption.TopDirectoryOnly))
                {
                    var dllName = Path.GetFileNameWithoutExtension(dllPath);
                    if (string.IsNullOrWhiteSpace(dllName))
                    {
                        continue;
                    }

                    foreach (var hint in backendHints)
                    {
                        if (dllName.IndexOf(hint.Marker, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return hint.Label;
                        }
                    }
                }

                foreach (var hint in backendHints)
                {
                    var nativePath = Path.Combine(baseDirectory, "runtimes", "win-x64", "native", hint.Label.ToLowerInvariant(), "llama.dll");
                    if (File.Exists(nativePath))
                    {
                        return hint.Label;
                    }
                }

                foreach (var llamaPath in Directory.EnumerateFiles(baseDirectory, "llama.dll", SearchOption.AllDirectories))
                {
                    var normalized = llamaPath.Replace('\\', '/');
                    foreach (var hint in backendHints)
                    {
                        if (normalized.IndexOf($"/native/{hint.Label.ToLowerInvariant()}/", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return hint.Label;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore probe failures and fall back to generic GPU.
        }

        return "GPU";
    }

    public async Task<string> InferAsync(
        string prompt,
        string systemPrompt,
        int maxTokens = 256,
        float temperature = 0.7f,
        float repeatPenalty = 1.1f,
        CancellationToken ct = default)
    {
        if (!_isLoaded || _executor == null || _model == null || _modelParams == null)
        {
            return "[错误] 模型未加载";
        }

        var fullPrompt = string.IsNullOrEmpty(systemPrompt)
            ? prompt
            : $"{systemPrompt}\n\nUser: {prompt}\nAssistant:";

        return await Task.Run(async () =>
        {
            try
            {
                Log("Starting inference...");
                
                var inferenceParams = new InferenceParams
                {
                    MaxTokens = maxTokens,
                    SamplingPipeline = new DefaultSamplingPipeline
                    {
                        Temperature = temperature,
                        RepeatPenalty = 1.15f,
                        PenaltyCount = 64
                    },
                    AntiPrompts = new List<string> { "User:", "user:", "```json" }
                };

                var sb = new StringBuilder();
                
                await foreach (var token in _executor.InferAsync(fullPrompt, inferenceParams).WithCancellation(ct))
                {
                    sb.Append(token);
                }

                Log("Inference complete");
                
                var result = sb.ToString().Trim();
                
                var endMarkers = new[] { "```json", "```" };
                foreach (var marker in endMarkers)
                {
                    var index = result.IndexOf(marker, StringComparison.Ordinal);
                    if (index > 0)
                    {
                        result = result.Substring(0, index).Trim();
                        break;
                    }
                }

                var preview = result.Length > 200
                    ? result.Substring(0, 200) + "..."
                    : result;
                Log($"Inference result length: {result.Length}");
                Log($"Inference preview: {preview}");
                
                return result;
            }
            catch (OperationCanceledException)
            {
                return "[生成已取消]";
            }
            catch (Exception ex)
            {
                LogError($"Inference error: {ex.Message}");
                return $"[错误: {ex.Message}]";
            }
        }, ct);
    }

    public async Task<StructuredInferenceResult<T>> InferJsonAsync<T>(
        string prompt,
        string systemPrompt,
        int maxTokens = 256,
        float temperature = 0.3f,
        int retryCount = 1,
        Func<T, string?>? validator = null,
        CancellationToken ct = default) where T : class
    {
        var attempts = Math.Max(1, retryCount + 1);
        StructuredInferenceResult<T>? lastFailure = null;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var raw = await InferAsync(
                prompt,
                systemPrompt,
                maxTokens: maxTokens,
                temperature: temperature,
                ct: ct);

            if (string.IsNullOrWhiteSpace(raw))
            {
                lastFailure = StructuredInferenceResult<T>.Failure(raw, "LLM 返回空结果");
                continue;
            }

            if (raw.StartsWith("[错误]", StringComparison.Ordinal) ||
                raw.StartsWith("[错误:", StringComparison.Ordinal) ||
                raw.StartsWith("[生成已取消]", StringComparison.Ordinal))
            {
                return StructuredInferenceResult<T>.Failure(raw, raw);
            }

            if (!TryExtractJson(raw, out var jsonPayload))
            {
                lastFailure = StructuredInferenceResult<T>.Failure(raw, "未检测到 JSON 结构");
                continue;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<T>(
                    jsonPayload,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (parsed == null)
                {
                    lastFailure = StructuredInferenceResult<T>.Failure(raw, "JSON 解析结果为空");
                    continue;
                }

                var validationError = validator?.Invoke(parsed);
                if (!string.IsNullOrEmpty(validationError))
                {
                    lastFailure = StructuredInferenceResult<T>.Failure(raw, validationError);
                    continue;
                }

                return StructuredInferenceResult<T>.Success(parsed, raw);
            }
            catch (JsonException ex)
            {
                lastFailure = StructuredInferenceResult<T>.Failure(raw, $"JSON 解析失败: {ex.Message}");
            }
        }

        return lastFailure ?? StructuredInferenceResult<T>.Failure(string.Empty, "结构化推理失败");
    }

    private static bool TryExtractJson(string raw, out string payload)
    {
        payload = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) ||
            trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            payload = trimmed;
            return true;
        }

        var fenceIndex = trimmed.IndexOf("```", StringComparison.Ordinal);
        while (fenceIndex >= 0)
        {
            var lineBreakIndex = trimmed.IndexOf('\n', fenceIndex + 3);
            if (lineBreakIndex < 0)
            {
                break;
            }

            var closeFence = trimmed.IndexOf("```", lineBreakIndex + 1, StringComparison.Ordinal);
            if (closeFence < 0)
            {
                break;
            }

            var block = trimmed.Substring(lineBreakIndex + 1, closeFence - lineBreakIndex - 1).Trim();
            if (block.StartsWith("{", StringComparison.Ordinal) || block.StartsWith("[", StringComparison.Ordinal))
            {
                payload = block;
                return true;
            }

            fenceIndex = trimmed.IndexOf("```", closeFence + 3, StringComparison.Ordinal);
        }

        var objectStart = trimmed.IndexOf('{');
        var objectEnd = trimmed.LastIndexOf('}');
        if (objectStart >= 0 && objectEnd > objectStart)
        {
            payload = trimmed.Substring(objectStart, objectEnd - objectStart + 1).Trim();
            return true;
        }

        var arrayStart = trimmed.IndexOf('[');
        var arrayEnd = trimmed.LastIndexOf(']');
        if (arrayStart >= 0 && arrayEnd > arrayStart)
        {
            payload = trimmed.Substring(arrayStart, arrayEnd - arrayStart + 1).Trim();
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        UnloadModel();
    }
}

public sealed class LlmLoadOptions
{
    public int ContextSize { get; init; } = 2048;
    public int GpuLayerCount { get; init; } = 0;
    public bool UseMemorymap { get; init; } = false;
    public bool UseMemoryLock { get; init; } = false;
    public bool AllowGpuFallbackToCpu { get; init; } = true;

    public LlmLoadOptions WithGpuLayerCount(int gpuLayerCount)
    {
        return new LlmLoadOptions
        {
            ContextSize = ContextSize,
            GpuLayerCount = gpuLayerCount,
            UseMemorymap = UseMemorymap,
            UseMemoryLock = UseMemoryLock,
            AllowGpuFallbackToCpu = AllowGpuFallbackToCpu
        };
    }
}

public sealed class StructuredInferenceResult<T> where T : class
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string RawOutput { get; }
    public string Error { get; }

    private StructuredInferenceResult(bool isSuccess, T? value, string rawOutput, string error)
    {
        IsSuccess = isSuccess;
        Value = value;
        RawOutput = rawOutput;
        Error = error;
    }

    public static StructuredInferenceResult<T> Success(T value, string rawOutput)
    {
        return new StructuredInferenceResult<T>(true, value, rawOutput, string.Empty);
    }

    public static StructuredInferenceResult<T> Failure(string rawOutput, string error)
    {
        return new StructuredInferenceResult<T>(false, null, rawOutput, error);
    }
}

public sealed class CivilizationPromptBuilder
{
    private static readonly string DefaultSystemPrompt = @"你是一个文明的推演分析师。根据提供的世界状态数据，分析文明的发展趋势，预测可能发生的事件，并用中文给出分析报告。

你需要基于以下信息进行分析：
- 当前纪元和文明发展程度
- 地形、资源分布
- 文明之间的贸易、战争、联盟状态
- 已知的历史事件

请给出专业的推演分析，包括：
1. 短期内可能发生的事件
2. 文明发展的主要趋势
3. 关键地区的发展潜力
4. 潜在的风险和机遇

输出要求：
- 不要展示思考过程或推理过程
- 直接给结论
- 总长度控制在 220 字以内
- 最多 4 条要点
";

    public static string BuildEpochAnalysisPrompt(
        int epoch,
        int polityCount,
        int hamletCount,
        int townCount,
        int cityStateCount,
        float controlledLandPercent,
        float conflictHeatPercent,
        float allianceCohesionPercent,
        float borderVolatilityPercent,
        CivilizationEpochEvent[] recentEvents)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## 当前世界状态 (第 {epoch} 纪元)");
        sb.AppendLine();
        sb.AppendLine("### 文明规模");
        sb.AppendLine($"- 政体数量: {polityCount}");
        sb.AppendLine($"- 聚落: {hamletCount}个村落, {townCount}个城镇, {cityStateCount}个城邦");
        sb.AppendLine($"- 控制领土: {controlledLandPercent:F1}%");
        sb.AppendLine();
        sb.AppendLine("### 局势指标");
        sb.AppendLine($"- 冲突热度: {conflictHeatPercent:F1}%");
        sb.AppendLine($"- 联盟凝聚力: {allianceCohesionPercent:F1}%");
        sb.AppendLine($"- 边境波动性: {borderVolatilityPercent:F1}%");
        sb.AppendLine();

        if (recentEvents.Length > 0)
        {
            sb.AppendLine("### 近期事件");
            var startIndex = Math.Max(0, recentEvents.Length - 4);
            for (var i = startIndex; i < recentEvents.Length; i++)
            {
                var evt = recentEvents[i];
                sb.AppendLine($"- 第{evt.Epoch}纪元 [{evt.Category}]: {evt.Summary}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("请分析以上数据，预测下一纪元可能发生的事件，使用精简中文要点输出（最多4条）。");

        return sb.ToString();
    }

    public static string BuildRegionAnalysisPrompt(
        int x, 
        int y, 
        string biomeName, 
        string landformName, 
        int polityId,
        float influence,
        bool isBorder,
        bool hasTradeRoute,
        int epoch)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## 区域分析 (坐标: {x}, {y})");
        sb.AppendLine();
        sb.AppendLine("### 地理特征");
        sb.AppendLine($"- 生物群系: {biomeName}");
        sb.AppendLine($"- 地形: {landformName}");
        sb.AppendLine();
        sb.AppendLine("### 文明状态");
        sb.AppendLine($"- 所属政体ID: {polityId}");
        sb.AppendLine($"- 文明影响力: {influence:P0}");
        sb.AppendLine($"- 边境状态: {(isBorder ? "是" : "否")}");
        sb.AppendLine($"- 贸易路线: {(hasTradeRoute ? "有" : "无")}");
        sb.AppendLine();
        sb.AppendLine($"当前为第 {epoch} 纪元。请用精简中文输出该区域推演（最多3条要点，总长度不超过160字）。");

        return sb.ToString();
    }

    public static string GetSystemPrompt() => DefaultSystemPrompt;
}
