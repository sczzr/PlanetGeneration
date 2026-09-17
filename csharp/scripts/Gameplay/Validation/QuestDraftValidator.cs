using PlanetGeneration.LLM;
using System;
using System.Collections.Generic;

namespace PlanetGeneration.Gameplay.Validation;

public static class QuestDraftValidator
{
    private static readonly HashSet<string> AllowedObjectiveTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "escort",
        "trade",
        "defense",
        "recon"
    };

    public static string? Validate(QuestDraft? draft, int width, int height)
    {
        if (draft == null)
        {
            return "任务草案为空";
        }

        if (string.IsNullOrWhiteSpace(draft.Title) || draft.Title.Length > 28)
        {
            return "任务标题长度需在 1..28 字符";
        }

        if (string.IsNullOrWhiteSpace(draft.ObjectiveType) || !AllowedObjectiveTypes.Contains(draft.ObjectiveType))
        {
            return "任务类型不在允许集合内";
        }

        if (draft.TargetX < 0 || draft.TargetX >= width || draft.TargetY < 0 || draft.TargetY >= height)
        {
            return "任务目标坐标越界";
        }

        if (draft.TimeLimitEpochs < 1 || draft.TimeLimitEpochs > 8)
        {
            return "任务期限需在 1..8 纪元";
        }

        if (draft.RewardGold < 50 || draft.RewardGold > 1200)
        {
            return "任务奖励需在 50..1200 金币";
        }

        if (string.IsNullOrWhiteSpace(draft.FailureConsequence) || draft.FailureConsequence.Length > 80)
        {
            return "失败后果描述长度需在 1..80 字符";
        }

        return null;
    }
}
