namespace PlanetGeneration.UI.State;

/// <summary>
/// User-facing preferences that can be persisted independently from generation inputs.
/// </summary>
public sealed class UiPreferences
{
	public int PreferredLayerId { get; set; }
	public bool AdvancedPanelVisible { get; set; }
	public bool ConsolePanelVisible { get; set; } = true;
	public float UiFontScale { get; set; } = 0.92f;
	public float MapZoom { get; set; } = 1.0f;
	public int CurrentEpoch { get; set; } = 450;
	public int SelectedTimelineEventEpoch { get; set; } = -1;
	public int OracleAutoUnloadIdleSeconds { get; set; } = 120;
	public bool CompareMode { get; set; }
}
