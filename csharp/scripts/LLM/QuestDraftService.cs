using Godot;
using PlanetGeneration.Gameplay.Validation;
using PlanetGeneration.WorldGen;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlanetGeneration.LLM;

public sealed class QuestDraftService
{
    private readonly LLamaInferenceService _inference;

    public QuestDraftService(LLamaInferenceService inference)
    {
        _inference = inference;
    }

    public async Task<QuestDraftGenerationResult> GenerateAsync(
        CivilizationSimulationResult civilization,
        int epoch,
        CancellationToken ct = default)
    {
        var width = civilization.Influence.GetLength(0);
        var height = civilization.Influence.GetLength(1);

        var modelFile = Path.GetFileName(_inference.ModelPath);
        var preferFastFallback = !string.IsNullOrWhiteSpace(modelFile) &&
            modelFile.IndexOf("thinking", StringComparison.OrdinalIgnoreCase) >= 0;
        if (preferFastFallback)
        {
            var fastFallback = BuildFallbackQuest(civilization, epoch, width, height);
            return new QuestDraftGenerationResult
            {
                Draft = fastFallback,
                UsedFallback = true,
                StatusMessage = "检测到 Thinking 模型，启用极速任务草案模板",
                RawOutput = string.Empty
            };
        }

        var prompt = QuestPromptBuilder.BuildQuestDraftPrompt(civilization, epoch);
        var systemPrompt = QuestPromptBuilder.GetSystemPrompt();

        var parseResult = await _inference.InferJsonAsync<QuestDraft>(
            prompt,
            systemPrompt,
            maxTokens: 128,
            temperature: 0.1f,
            retryCount: 0,
            validator: draft => QuestDraftValidator.Validate(draft, width, height),
            ct: ct);

        if (parseResult.IsSuccess && parseResult.Value != null)
        {
            var normalized = NormalizeDraft(parseResult.Value, epoch);
            return new QuestDraftGenerationResult
            {
                Draft = normalized,
                UsedFallback = false,
                StatusMessage = "LLM 任务草案生成成功",
                RawOutput = parseResult.RawOutput
            };
        }

        var fallback = BuildFallbackQuest(civilization, epoch, width, height);
        var error = string.IsNullOrWhiteSpace(parseResult.Error)
            ? "结构化任务草案生成失败，已回退模板"
            : $"结构化任务草案失败: {parseResult.Error}，已回退模板";

        return new QuestDraftGenerationResult
        {
            Draft = fallback,
            UsedFallback = true,
            StatusMessage = error,
            RawOutput = parseResult.RawOutput
        };
    }

    private static QuestDraft NormalizeDraft(QuestDraft raw, int epoch)
    {
        raw.Title = raw.Title.Trim();
        raw.ObjectiveType = raw.ObjectiveType.Trim().ToLowerInvariant();
        raw.FailureConsequence = raw.FailureConsequence.Trim();

        if (string.IsNullOrWhiteSpace(raw.QuestId))
        {
            raw.QuestId = BuildQuestId(raw, epoch);
        }

        return raw;
    }

    private static QuestDraft BuildFallbackQuest(CivilizationSimulationResult civilization, int epoch, int width, int height)
    {
        var objectiveType = civilization.ConflictHeatPercent >= civilization.AllianceCohesionPercent
            ? "defense"
            : "trade";
        var (targetX, targetY) = FindBestTargetCell(civilization, objectiveType, width, height);
        var baseReward = objectiveType == "defense" ? 280 : 220;
        var rewardGold = baseReward + Mathf.RoundToInt(civilization.ConflictHeatPercent * 2.4f);

        var draft = new QuestDraft
        {
            Title = objectiveType == "defense" ? "边境警戒令" : "商路补给委托",
            ObjectiveType = objectiveType,
            TargetX = targetX,
            TargetY = targetY,
            TimeLimitEpochs = objectiveType == "defense" ? 2 : 3,
            RewardGold = Mathf.Clamp(rewardGold, 120, 900),
            FailureConsequence = objectiveType == "defense"
                ? "边境治安下降，冲突热度上升。"
                : "贸易网络效率下降，城镇发展放缓。"
        };

        draft.QuestId = BuildQuestId(draft, epoch);
        return draft;
    }

    private static (int X, int Y) FindBestTargetCell(CivilizationSimulationResult civilization, string objectiveType, int width, int height)
    {
        var bestX = width / 2;
        var bestY = height / 2;
        var bestScore = -1f;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var influence = civilization.Influence[x, y];
                var score = influence;

                if (objectiveType == "defense")
                {
                    if (!civilization.BorderMask[x, y])
                    {
                        continue;
                    }

                    score += civilization.TradeFlow[x, y] * 0.35f;
                }
                else
                {
                    if (!civilization.TradeRouteMask[x, y])
                    {
                        continue;
                    }

                    score += civilization.TradeFlow[x, y] * 0.55f;
                }

                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestX = x;
                bestY = y;
            }
        }

        return (bestX, bestY);
    }

    private static string BuildQuestId(QuestDraft draft, int epoch)
    {
        var raw = $"{epoch}|{draft.ObjectiveType}|{draft.TargetX}|{draft.TargetY}|{draft.Title}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        var token = Convert.ToHexString(bytes).Substring(0, 12).ToLowerInvariant();
        return $"q{epoch:D4}_{token}";
    }
}

public static class QuestPromptBuilder
{
    private static readonly string DefaultSystemPrompt = @"你是游戏任务设计师。
你只能输出 JSON，不允许输出任何解释文本、推理过程、思考过程、markdown 或代码块。
输出必须严格符合字段定义，且数值范围合法。
不要先分析，不要复述要求，直接输出最终 JSON。";

    public static string BuildQuestDraftPrompt(CivilizationSimulationResult civilization, int epoch)
    {
        var sb = new StringBuilder();
        sb.AppendLine("请基于当前世界状态生成 1 个可执行任务草案。");
        sb.AppendLine();
        sb.AppendLine("约束:");
        sb.AppendLine("- 只输出单个 JSON 对象");
        sb.AppendLine("- title: 1..28 字符");
        sb.AppendLine("- objectiveType 只能是 escort/trade/defense/recon");
        sb.AppendLine("- targetX/targetY 必须是地图上的有效坐标");
        sb.AppendLine("- timeLimitEpochs 必须在 1..8");
        sb.AppendLine("- rewardGold 必须在 50..1200");
        sb.AppendLine("- failureConsequence 必须在 1..80 字符");
        sb.AppendLine();
        sb.AppendLine($"当前纪元: {epoch}");
        sb.AppendLine($"政体数量: {civilization.PolityCount}");
        sb.AppendLine($"冲突热度: {civilization.ConflictHeatPercent:F1}%");
        sb.AppendLine($"联盟凝聚力: {civilization.AllianceCohesionPercent:F1}%");
        sb.AppendLine($"边境波动性: {civilization.BorderVolatilityPercent:F1}%");
        sb.AppendLine($"贸易连通性: {civilization.ConnectedHubPercent:F1}%");
        sb.AppendLine($"地图宽高: {civilization.Influence.GetLength(0)} x {civilization.Influence.GetLength(1)}");
        sb.AppendLine();
        sb.AppendLine("近期事件:");

        var maxEvents = Math.Min(4, civilization.RecentEvents.Length);
        for (var i = civilization.RecentEvents.Length - maxEvents; i < civilization.RecentEvents.Length; i++)
        {
            if (i < 0)
            {
                continue;
            }

            var evt = civilization.RecentEvents[i];
            sb.AppendLine($"- 第{evt.Epoch}纪元 [{evt.Category}] 影响{evt.ImpactLevel}: {evt.Summary}");
        }

        sb.AppendLine();
        sb.AppendLine("输出 JSON 字段:");
        sb.AppendLine("{");
        sb.AppendLine("  \"questId\": \"可留空字符串\",");
        sb.AppendLine("  \"title\": \"任务标题\",");
        sb.AppendLine("  \"objectiveType\": \"escort|trade|defense|recon\",");
        sb.AppendLine("  \"targetX\": 0,");
        sb.AppendLine("  \"targetY\": 0,");
        sb.AppendLine("  \"timeLimitEpochs\": 1,");
        sb.AppendLine("  \"rewardGold\": 200,");
        sb.AppendLine("  \"failureConsequence\": \"失败后果\"");
        sb.AppendLine("}");

        return sb.ToString();
    }

    public static string GetSystemPrompt() => DefaultSystemPrompt;
}
