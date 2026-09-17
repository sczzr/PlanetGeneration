namespace PlanetGeneration.UI.State;

/// <summary>
/// Runtime session information shared by UI panels. World payloads remain owned by the
/// generation/cache services; this object only carries lifecycle and selection state.
/// </summary>
public sealed class WorldSessionState
{
	public bool IsGenerating { get; set; }
	public bool PendingRegenerate { get; set; }
	public float GenerationProgress { get; set; }
	public string GenerationStatus { get; set; } = string.Empty;
	public bool HasPrimaryWorld { get; set; }
	public bool HasCompareWorld { get; set; }
	public int Seed { get; set; }
	public int MapWidth { get; set; }
	public int MapHeight { get; set; }
	public int CurrentEpoch { get; set; } = 450;
	public int SelectedTimelineEventEpoch { get; set; } = -1;
	public string LastArchivePath { get; set; } = string.Empty;
	public long RenderCacheAccessCounter { get; set; }
	public long WorldCacheAccessCounter { get; set; }
	public int CachedWorldCount { get; set; }
}
