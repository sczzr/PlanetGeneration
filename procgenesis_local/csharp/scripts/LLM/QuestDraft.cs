namespace PlanetGeneration.LLM;

public sealed class QuestDraft
{
    public string QuestId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ObjectiveType { get; set; } = string.Empty;
    public int TargetX { get; set; }
    public int TargetY { get; set; }
    public int TimeLimitEpochs { get; set; }
    public int RewardGold { get; set; }
    public string FailureConsequence { get; set; } = string.Empty;
}

public sealed class QuestDraftGenerationResult
{
    public required QuestDraft Draft { get; init; }
    public required bool UsedFallback { get; init; }
    public required string StatusMessage { get; init; }
    public required string RawOutput { get; init; }
}
