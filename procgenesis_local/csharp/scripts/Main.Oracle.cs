using Godot;
using PlanetGeneration.LLM;
using PlanetGeneration.WorldGen;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PlanetGeneration;

public partial class Main
{
    private CivilizationOracle? _oracle;
    private RichTextLabel? _oracleOutput;
    private RichTextLabel? _oracleSystemOutput;
    private RichTextLabel? _oracleWorldOutput;
    private RichTextLabel? _oracleRegionOutput;
    private RichTextLabel? _oracleQuestOutput;
    private RichTextLabel? _oracleErrorOutput;
    private Label? _oracleStatus;
    private Button? _oracleLoadButton;
    private Button? _oracleAnalyzeWorldButton;
    private Button? _oracleAnalyzeRegionButton;
    private Button? _oracleCancelButton;
    private TabContainer? _loreTabs;
    private bool _oracleInitialized;
    private int _oracleLastX = -1;
    private int _oracleLastY = -1;
    private CancellationTokenSource? _oracleAutoUnloadCts;
    private TaskCompletionSource<bool>? _oracleLoadAwaiter;
    private CancellationTokenSource? _oraclePrewarmCts;
    private static readonly bool OracleDebug = false;
    private const string OraclePanelDivider = "[code]─────────────────────────────[/code]";

    private T? FindOracleNode<T>(string name) where T : Node
    {
        return FindChild(name, true, false) as T;
    }

    private static void OracleLog(string message, bool verbose = false)
    {
        if (verbose && !OracleDebug)
        {
            return;
        }

        GD.Print($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Oracle] {message}");
    }

    private static void OracleLogError(string message)
    {
        GD.PrintErr($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Oracle] {message}");
    }

    private void ResolveOracleNodes()
    {
        _oracleOutput = FindOracleNode<RichTextLabel>("OracleOutput");
        _oracleSystemOutput = FindOracleNode<RichTextLabel>("OracleSystemOutput");
        _oracleWorldOutput = FindOracleNode<RichTextLabel>("OracleWorldOutput");
        _oracleRegionOutput = FindOracleNode<RichTextLabel>("OracleRegionOutput");
        _oracleQuestOutput = FindOracleNode<RichTextLabel>("OracleQuestOutput");
        _oracleErrorOutput = FindOracleNode<RichTextLabel>("OracleErrorOutput");
        _oracleStatus = FindOracleNode<Label>("OracleStatus");
        _oracleLoadButton = FindOracleNode<Button>("OracleLoadButton");
        _oracleAnalyzeWorldButton = FindOracleNode<Button>("AnalyzeWorldButton");
        _oracleAnalyzeRegionButton = FindOracleNode<Button>("AnalyzeRegionButton");
        _oracleCancelButton = FindOracleNode<Button>("CancelButton");
    }

    private void InitializeOracleUI()
    {
        if (_oracleInitialized)
        {
            return;
        }

        _oracle = new CivilizationOracle();

        OracleLog("Starting UI initialization...");

        _loreTabs = FindOracleNode<TabContainer>("LoreTabs");
        OracleLog(
            _loreTabs == null
                ? "LoreTabs not found, using legacy node lookup."
                : "Found LoreTabs, using tabbed layout.",
            verbose: true);

        ResolveOracleNodes();

        OracleLog(
            $"Node status: load={_oracleLoadButton != null}, output={_oracleOutput != null}, system={_oracleSystemOutput != null}, world={_oracleWorldOutput != null}, region={_oracleRegionOutput != null}, quest={_oracleQuestOutput != null}, error={_oracleErrorOutput != null}",
            verbose: true);

        ConfigureOracleOutputLabel(_oracleOutput);
        ConfigureOracleOutputLabel(_oracleSystemOutput);
        ConfigureOracleOutputLabel(_oracleWorldOutput);
        ConfigureOracleOutputLabel(_oracleRegionOutput);
        ConfigureOracleOutputLabel(_oracleQuestOutput);
        ConfigureOracleOutputLabel(_oracleErrorOutput);

        if (_oracleLoadButton == null)
        {
            OracleLog("UI nodes not found. Skipping Oracle UI initialization.");
            return;
        }

        _oracleLoadButton.Visible = false;
        OracleLog("Binding button events...", verbose: true);
        _oracleLoadButton.Pressed += OnOracleLoadModelPressed;

        if (_oracleAnalyzeWorldButton != null)
        {
            _oracleAnalyzeWorldButton.Pressed += OnOracleAnalyzeWorldPressed;
            _oracleAnalyzeWorldButton.Disabled = true;
        }

        if (_oracleAnalyzeRegionButton != null)
        {
            _oracleAnalyzeRegionButton.Pressed += OnOracleAnalyzeRegionPressed;
            _oracleAnalyzeRegionButton.Disabled = true;
        }

        if (_oracleCancelButton != null)
        {
            _oracleCancelButton.Pressed += OnOracleCancelPressed;
            _oracleCancelButton.Disabled = true;
        }

        if (_oracle != null)
        {
            _oracle.StatusChanged += OnOracleStatusChanged;
            _oracle.OutputReceived += OnOracleOutputReceived;
            _oracle.InferenceStateChanged += OnOracleInferenceStateChanged;
            _oracle.LoadComplete += OnOracleLoadComplete;
        }

        UpdateOracleUI();
        _oracleInitialized = true;
        StartOraclePrewarm();

        OracleLog("UI initialized successfully");
    }

    public void UpdateOracleHoverPosition(int x, int y)
    {
        _oracleLastX = x;
        _oracleLastY = y;
    }

    private static void ConfigureOracleOutputLabel(RichTextLabel? label)
    {
        if (label == null)
        {
            return;
        }

        label.BbcodeEnabled = true;
        label.FitContent = false;
    }

    private bool HasSegmentedOraclePanels()
    {
        return _oracleSystemOutput != null ||
            _oracleWorldOutput != null ||
            _oracleRegionOutput != null ||
            _oracleQuestOutput != null ||
            _oracleErrorOutput != null;
    }

    private void ClearOracleMessagePanels()
    {
        _oracleSystemOutput?.Clear();
        _oracleWorldOutput?.Clear();
        _oracleRegionOutput?.Clear();
        _oracleQuestOutput?.Clear();
        _oracleErrorOutput?.Clear();
        _oracleOutput?.Clear();
    }

    private void SetOraclePanelMessage(RichTextLabel? targetPanel, string bbcodeText)
    {
        Callable.From(() => SetOraclePanelMessageDeferred(targetPanel, bbcodeText)).CallDeferred();
    }

    private void SetOraclePanelMessageDeferred(RichTextLabel? targetPanel, string bbcodeText)
    {
        if (targetPanel != null)
        {
            targetPanel.Clear();
            targetPanel.AppendText(bbcodeText);
        }

        if (!HasSegmentedOraclePanels() && _oracleOutput != null && !ReferenceEquals(targetPanel, _oracleOutput))
        {
            _oracleOutput.Clear();
            _oracleOutput.AppendText(bbcodeText);
        }
    }

    private void AppendOraclePanelMessage(RichTextLabel? targetPanel, string bbcodeText)
    {
        Callable.From(() => AppendOraclePanelMessageDeferred(targetPanel, bbcodeText)).CallDeferred();
    }

    private void AppendOraclePanelMessageDeferred(RichTextLabel? targetPanel, string bbcodeText)
    {
        if (targetPanel != null)
        {
            targetPanel.AppendText(bbcodeText);
        }

        if (!HasSegmentedOraclePanels() && _oracleOutput != null && !ReferenceEquals(targetPanel, _oracleOutput))
        {
            _oracleOutput.AppendText(bbcodeText);
        }
    }

    private void ShowOracleError(string message)
    {
        var text = $"[color=#ff6b6b]{message}[/color]";
        SetOraclePanelMessage(_oracleErrorOutput, text);
    }

    private bool TryGetOracleContext(
        out CivilizationOracle oracle,
        out GeneratedWorldData world,
        out CivilizationSimulationResult civilization)
    {
        oracle = null!;
        world = null!;
        civilization = null!;

        if (_oracle == null)
        {
            OracleLogError("_oracle is null");
            return false;
        }

        if (_primaryWorld == null)
        {
            OracleLogError("_primaryWorld is null");
            ShowOracleError("错误: 世界数据不可用");
            return false;
        }

        EnsureCivilizationSimulation(_primaryWorld);
        if (_primaryWorld.CivilizationSimulation == null)
        {
            OracleLogError("CivilizationSimulation is null");
            ShowOracleError("错误: 文明模拟数据不可用");
            return false;
        }

        oracle = _oracle;
        world = _primaryWorld;
        civilization = _primaryWorld.CivilizationSimulation;
        return true;
    }

    private bool HandleOracleAnalysisFailure(
        string? result,
        string emptyResultError,
        RichTextLabel? targetPanel,
        string failedPanelMessage)
    {
        if (!string.IsNullOrEmpty(result) && !result.StartsWith("[错误", StringComparison.Ordinal))
        {
            return false;
        }

        ShowOracleError(string.IsNullOrEmpty(result) ? emptyResultError : result);
        SetOraclePanelMessage(targetPanel, failedPanelMessage);
        return true;
    }

    private async Task<bool> EnsureOracleModelReadyAsync(CivilizationOracle oracle)
    {
        CancelOracleAutoUnload();

        if (oracle.IsModelLoaded)
        {
            return true;
        }

        if (_oracleLoadAwaiter != null)
        {
            return await _oracleLoadAwaiter.Task;
        }

        SetOraclePanelMessage(_oracleSystemOutput, "[color=#9ab0c9]正在准备推演引擎...[/color]");

        var awaiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _oracleLoadAwaiter = awaiter;

        if (!oracle.LoadModel())
        {
            _oracleLoadAwaiter = null;
            ShowOracleError("错误: 推演引擎初始化失败");
            return false;
        }

        var loaded = await awaiter.Task;
        _oracleLoadAwaiter = null;

        if (!loaded)
        {
            ShowOracleError("错误: 推演引擎初始化失败");
            return false;
        }

        return true;
    }

    private void StartOraclePrewarm()
    {
        if (_oracle == null)
        {
            return;
        }

        _oraclePrewarmCts?.Cancel();
        _oraclePrewarmCts?.Dispose();
        _oraclePrewarmCts = new CancellationTokenSource();
        _ = PrewarmOracleModelAsync(_oraclePrewarmCts.Token);
    }

    private async Task PrewarmOracleModelAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ct.IsCancellationRequested || _oracle == null || _oracle.IsModelLoaded || _oracleLoadAwaiter != null)
        {
            return;
        }

        var awaiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _oracleLoadAwaiter = awaiter;

        if (!_oracle.LoadModel())
        {
            _oracleLoadAwaiter = null;
            return;
        }

        var loaded = await awaiter.Task;
        if (_oracleLoadAwaiter == awaiter)
        {
            _oracleLoadAwaiter = null;
        }

        if (loaded)
        {
            ScheduleOracleAutoUnload();
        }
    }

    private void CancelOracleAutoUnload()
    {
        _oracleAutoUnloadCts?.Cancel();
        _oracleAutoUnloadCts?.Dispose();
        _oracleAutoUnloadCts = null;
    }

    private void ScheduleOracleAutoUnload()
    {
        if (_oracle == null || !_oracle.IsModelLoaded)
        {
            return;
        }

        CancelOracleAutoUnload();
        _oracleAutoUnloadCts = new CancellationTokenSource();
        _ = AutoUnloadOracleAfterIdleAsync(_oracleAutoUnloadIdleSeconds, _oracleAutoUnloadCts.Token);
    }

    private async Task AutoUnloadOracleAfterIdleAsync(int idleSeconds, CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(MinOracleAutoUnloadIdleSeconds, idleSeconds)), ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ct.IsCancellationRequested)
        {
            return;
        }

        CallDeferred(nameof(AutoUnloadOracleIfIdleDeferred));
    }

    private void AutoUnloadOracleIfIdleDeferred()
    {
        if (_oracle == null || !_oracle.IsModelLoaded || _oracle.IsInferring)
        {
            return;
        }

        _oracle.UnloadModel();
        SetOraclePanelMessage(_oracleSystemOutput, "[color=#9ab0c9]推演引擎待命中。[/color]");
        UpdateOracleUI();
    }

    private static bool IsInternalLifecycleStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status.Contains("加载", StringComparison.Ordinal) ||
            status.Contains("卸载", StringComparison.Ordinal) ||
            status.Contains("GPU", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("ctx=", StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureOraclePanelPlaceholders()
    {
        if (_oracleSystemOutput != null && string.IsNullOrWhiteSpace(_oracleSystemOutput.Text))
        {
            _oracleSystemOutput.Text = "[color=#9ab0c9]等待操作...[/color]";
        }

        if (_oracleWorldOutput != null && string.IsNullOrWhiteSpace(_oracleWorldOutput.Text))
        {
            _oracleWorldOutput.Text = "[color=#9ab0c9]尚未进行世界推演。[/color]";
        }

        if (_oracleRegionOutput != null && string.IsNullOrWhiteSpace(_oracleRegionOutput.Text))
        {
            _oracleRegionOutput.Text = "[color=#9ab0c9]尚未进行区域推演。[/color]";
        }

        if (_oracleQuestOutput != null && string.IsNullOrWhiteSpace(_oracleQuestOutput.Text))
        {
            _oracleQuestOutput.Text = "[color=#9ab0c9]尚未生成任务草案。[/color]";
        }

        if (_oracleErrorOutput != null && string.IsNullOrWhiteSpace(_oracleErrorOutput.Text))
        {
            _oracleErrorOutput.Text = "[color=#9ab0c9]当前无错误。[/color]";
        }
    }

    private void OnOracleLoadModelPressed()
    {
        OracleLog("Load button pressed!", verbose: true);
        
        if (_oracle == null)
        {
            return;
        }

        if (_oracle.IsModelLoaded)
        {
            CancelOracleAutoUnload();
            _oracle.UnloadModel();
            ClearOracleMessagePanels();
            EnsureOraclePanelPlaceholders();
            UpdateOracleUI();
            return;
        }

        CancelOracleAutoUnload();
        ClearOracleMessagePanels();
        EnsureOraclePanelPlaceholders();
        _oracle.LoadModel();
        UpdateOracleUI();
    }

    private void OnOracleLoadComplete(bool loaded)
    {
        OracleLog($"Load complete: {loaded}", verbose: true);
        _oracleLoadAwaiter?.TrySetResult(loaded);
        CallDeferred(nameof(OnOracleLoadCompleteDeferred), loaded);
    }
    
    private void OnOracleLoadCompleteDeferred(bool loaded)
    {
        UpdateOracleUI();
        
        if (loaded && _oracleAnalyzeWorldButton != null)
        {
            _oracleAnalyzeWorldButton.Disabled = false;
        }

        if (loaded && _oracleAnalyzeRegionButton != null)
        {
            _oracleAnalyzeRegionButton.Disabled = false;
        }
    }

    private async void OnOracleAnalyzeWorldPressed()
    {
        OracleLog("AnalyzeWorld button pressed", verbose: true);

        if (!TryGetOracleContext(out var oracle, out _, out var civilization))
        {
            return;
        }

        if (!await EnsureOracleModelReadyAsync(oracle))
        {
            return;
        }

        SetOraclePanelMessage(_oracleSystemOutput, "[color=#aaaaaa]正在分析世界状态...[/color]");
        SetOraclePanelMessage(_oracleWorldOutput, "[color=#9ab0c9]推演中...[/color]");
        SetOraclePanelMessage(_oracleQuestOutput, "[color=#9ab0c9]等待任务草案生成...[/color]");
        SetOraclePanelMessage(_oracleErrorOutput, "[color=#9ab0c9]当前无错误。[/color]");
        
        var result = await oracle.AnalyzeCurrentWorldState(
            civilization,
            _currentEpoch);

        if (HandleOracleAnalysisFailure(
            result,
            "错误: 世界推演返回空结果",
            _oracleWorldOutput,
            "[color=#9ab0c9]世界推演失败。[/color]"))
        {
            return;
        }

        var formatted = FormatAnalysisResult(result, "世界推演分析");
        SetOraclePanelMessage(_oracleWorldOutput, formatted);

        var questDraft = await oracle.GenerateQuestDraft(
            civilization,
            _currentEpoch);
        if (questDraft != null)
        {
            SetOraclePanelMessage(_oracleQuestOutput, FormatQuestDraftResult(questDraft));
        }
        else
        {
            SetOraclePanelMessage(_oracleQuestOutput, "[color=#ffb347]任务草案未生成。[/color]");
        }

        SetOraclePanelMessage(_oracleSystemOutput, $"[color=#9ab0c9]世界推演完成（纪元 {_currentEpoch}）。[/color]");
        ScheduleOracleAutoUnload();
    }

    private async void OnOracleAnalyzeRegionPressed()
    {
        OracleLog("AnalyzeRegion button pressed", verbose: true);

        if (!TryGetOracleContext(out var oracle, out var world, out var civilization))
        {
            return;
        }

        if (!await EnsureOracleModelReadyAsync(oracle))
        {
            return;
        }

        if (_oracleLastX < 0 || _oracleLastY < 0)
        {
            OracleLog("No region selected", verbose: true);
            _oracleStatus?.SetText("请先在地图上选择一个区域");
            ShowOracleError("错误: 请先在地图上选择一个区域");
            return;
        }

        SetOraclePanelMessage(_oracleSystemOutput, $"[color=#aaaaaa]正在分析区域 ({_oracleLastX}, {_oracleLastY})...[/color]");
        SetOraclePanelMessage(_oracleRegionOutput, "[color=#9ab0c9]区域推演中...[/color]");
        SetOraclePanelMessage(_oracleErrorOutput, "[color=#9ab0c9]当前无错误。[/color]");

        var biome = world.Biome[_oracleLastX, _oracleLastY];
        var landform = ClassifyLandform(_oracleLastX, _oracleLastY, SeaLevel, world.Elevation, world.Moisture, world.River);
        var biomeName = GetBiomeDisplayName(biome);
        var landformName = GetLandformDisplayName(landform);
        
        OracleLog($"Analyzing region ({_oracleLastX}, {_oracleLastY}) - {biomeName}, {landformName}", verbose: true);

        var result = await oracle.AnalyzeRegion(
            civilization,
            _oracleLastX, 
            _oracleLastY,
            _currentEpoch,
            biomeName,
            landformName
        );

        if (HandleOracleAnalysisFailure(
            result,
            "错误: 区域分析返回空结果",
            _oracleRegionOutput,
            "[color=#9ab0c9]区域推演失败。[/color]"))
        {
            return;
        }

        var formatted = FormatAnalysisResult(result, "区域分析");
        SetOraclePanelMessage(_oracleRegionOutput, formatted);
        SetOraclePanelMessage(_oracleSystemOutput, $"[color=#9ab0c9]区域推演完成（{_oracleLastX}, {_oracleLastY}）。[/color]");
        ScheduleOracleAutoUnload();
    }

    private void OnOracleCancelPressed()
    {
        _oracle?.CancelCurrentInference();
    }

    private void OnOracleStatusChanged(string status)
    {
        CallDeferred(nameof(OnOracleStatusChangedDeferred), status);
    }

    private void OnOracleStatusChangedDeferred(string status)
    {
        if (IsInternalLifecycleStatus(status))
        {
            if (OracleDebug)
            {
                OracleLog($"Lifecycle status: {status}", verbose: true);
            }
            return;
        }

        if (_oracleStatus != null)
        {
            _oracleStatus.Text = status;
        }

        if (_oracleSystemOutput != null)
        {
            SetOraclePanelMessage(_oracleSystemOutput, $"[color=#9ab0c9]{status}[/color]");
        }
    }

    private void OnOracleOutputReceived(string output)
    {
        if (_oracleSystemOutput == null || string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        var preview = output.Length > 120 ? output.Substring(0, 120) + "..." : output;
        AppendOraclePanelMessage(_oracleSystemOutput, $"\n[color=#6f8aa8]收到模型输出片段：{preview}[/color]");
    }

    private void OnOracleInferenceStateChanged(bool isInferring)
    {
        CallDeferred(nameof(OnOracleInferenceStateChangedDeferred), isInferring);
    }

    private void OnOracleInferenceStateChangedDeferred(bool isInferring)
    {
        if (isInferring)
        {
            CancelOracleAutoUnload();
        }
        else
        {
            ScheduleOracleAutoUnload();
        }

        if (_oracleCancelButton != null)
        {
            _oracleCancelButton.Disabled = !isInferring;
        }

        if (_oracleAnalyzeWorldButton != null)
        {
            _oracleAnalyzeWorldButton.Disabled = isInferring || !_oracle!.IsModelLoaded;
        }

        if (_oracleAnalyzeRegionButton != null)
        {
            _oracleAnalyzeRegionButton.Disabled = isInferring || !_oracle!.IsModelLoaded;
        }

        if (_oracleLoadButton != null)
        {
            _oracleLoadButton.Disabled = isInferring;
        }
    }

    private void UpdateOracleUI()
    {
        if (_oracleLoadButton != null)
        {
            _oracleLoadButton.Text = _oracle?.IsModelLoaded == true ? "卸载模型" : "加载模型";
        }

        if (_oracleStatus != null && _oracle != null)
        {
            _oracleStatus.Text = _oracle.IsInferring
                ? "推演中..."
                : "推演引擎待命";
        }

        if (_oracleAnalyzeWorldButton != null)
        {
            _oracleAnalyzeWorldButton.Disabled = !(_oracle?.IsModelLoaded == true);
        }

        if (_oracleAnalyzeRegionButton != null)
        {
            _oracleAnalyzeRegionButton.Disabled = !(_oracle?.IsModelLoaded == true);
        }

        EnsureOraclePanelPlaceholders();
    }

    private string FormatAnalysisResult(string result, string title = "推演分析")
    {
        var lines = result.Split('\n');
        var formatted = new System.Text.StringBuilder();
        
        formatted.AppendLine($"[color=#ff6b6b][b]{title}[/b][/color]");
        formatted.AppendLine(OraclePanelDivider);
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            if (trimmed.StartsWith("###") || trimmed.StartsWith("##"))
            {
                var sectionTitle = trimmed.TrimStart('#').Trim();
                formatted.AppendLine($"\n[color=#ffd700][b]{sectionTitle}[/b][/color]");
            }
            else if (trimmed.StartsWith("1.") || trimmed.StartsWith("2.") || trimmed.StartsWith("3.") || trimmed.StartsWith("4."))
            {
                formatted.AppendLine($"[color=#87ceeb][b]{trimmed}[/b][/color]");
            }
            else if (trimmed.StartsWith("- ") || trimmed.StartsWith("• "))
            {
                formatted.AppendLine($"  [color=#98fb98]•[/color] {trimmed.TrimStart('-', '•', ' ')}");
            }
            else if (trimmed.EndsWith(":") && !trimmed.StartsWith("http"))
            {
                formatted.AppendLine($"\n[color=#da70d6][b]{trimmed}[/b][/color]");
            }
            else if (trimmed.Contains(":") && trimmed.Length < 50)
            {
                var colonIndex = trimmed.IndexOf(':');
                var key = trimmed.Substring(0, colonIndex + 1);
                var value = trimmed.Substring(colonIndex + 1);
                formatted.AppendLine($"[color=#90ee90]{key}[/color][color=#e0e0e0]{value}[/color]");
            }
            else
            {
                formatted.AppendLine($"[color=#e0e0e0]{trimmed}[/color]");
            }
        }
        
        return formatted.ToString();
    }

    private static string FormatQuestDraftResult(QuestDraftGenerationResult result)
    {
        var badgeColor = result.UsedFallback ? "#ffb347" : "#7ed957";
        var statusLabel = result.UsedFallback ? "模板回退" : "结构化输出";
        var quest = result.Draft;
        var builder = new System.Text.StringBuilder();

        builder.AppendLine($"[color={badgeColor}][b]任务草案（{statusLabel}）[/b][/color]");
        builder.AppendLine(OraclePanelDivider);
        builder.AppendLine($"[color=#90ee90]ID:[/color] [color=#e0e0e0]{quest.QuestId}[/color]");
        builder.AppendLine($"[color=#90ee90]标题:[/color] [color=#e0e0e0]{quest.Title}[/color]");
        builder.AppendLine($"[color=#90ee90]类型:[/color] [color=#e0e0e0]{quest.ObjectiveType}[/color]");
        builder.AppendLine($"[color=#90ee90]目标:[/color] [color=#e0e0e0]({quest.TargetX}, {quest.TargetY})[/color]");
        builder.AppendLine($"[color=#90ee90]时限:[/color] [color=#e0e0e0]{quest.TimeLimitEpochs} 纪元[/color]");
        builder.AppendLine($"[color=#90ee90]奖励:[/color] [color=#e0e0e0]{quest.RewardGold} 金币[/color]");
        builder.AppendLine($"[color=#90ee90]失败后果:[/color] [color=#e0e0e0]{quest.FailureConsequence}[/color]");
        builder.AppendLine($"[color=#aaaaaa]{result.StatusMessage}[/color]");
        return builder.ToString();
    }

    private void ShutdownOracle()
    {
        CancelOracleAutoUnload();
        _oraclePrewarmCts?.Cancel();
        _oraclePrewarmCts?.Dispose();
        _oraclePrewarmCts = null;
        _oracleLoadAwaiter?.TrySetCanceled();
        _oracleLoadAwaiter = null;
        _oracle?.Dispose();
        _oracle = null;
    }
}
