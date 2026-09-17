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
	private void OnMapTextureGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton zoomButton && zoomButton.Pressed)
		{
			if (zoomButton.ButtonIndex == MouseButton.WheelUp)
			{
				SetMapZoom(_mapZoom + GetMapZoomStep());
				_mapTexture.AcceptEvent();
				return;
			}
			if (zoomButton.ButtonIndex == MouseButton.WheelDown)
			{
				SetMapZoom(_mapZoom - GetMapZoomStep());
				_mapTexture.AcceptEvent();
				return;
			}
		}

		if (@event is InputEventMouseButton loreMouseButton &&
			loreMouseButton.ButtonIndex == MouseButton.Left &&
			loreMouseButton.Pressed)
		{
			UpdateLoreFromMapSelection(loreMouseButton.Position);
		}

		var currentLayer = GetCurrentLayer();
		if (_primaryWorld == null || (currentLayer != MapLayer.Biomes && currentLayer != MapLayer.Landform))
		{
			ResetBiomeHoverState();
			return;
		}

		if (@event is not InputEventMouseButton mouseButton ||
			mouseButton.ButtonIndex != MouseButton.Left ||
			!mouseButton.Pressed)
		{
			return;
		}

		var local = mouseButton.Position;
		if (!TrySampleAtLocalPosition(local, out var hoverSample))
		{
			ResetBiomeHoverState();
			return;
		}

		UpdateOracleHoverFromSample(hoverSample);

		var detailText = BuildCellHoverText(hoverSample);
		if (_biomeHoverText.Text != detailText)
		{
			_biomeHoverText.Text = detailText;
		}

		UpdateCellHighlight(hoverSample.IsCell ? hoverSample.CellId : -1);

		PositionBiomeHoverPanel(local);
		_biomeHoverPanel.Visible = true;
	}

	private float GetMapZoomStep()
	{
		// Increase the step at high magnification so users do not need many wheel ticks.
		return MapZoomStep * Mathf.Max(1f, _mapZoom * 0.18f);
	}

	private void OnMapTextureMouseExited()
	{
		ResetBiomeHoverState();
	}

	private void ResetBiomeHoverState()
	{
		_biomeHoverPanel.Visible = false;
		UpdateCellHighlight(-1);
	}

	private void UpdateLoreFromMapSelection(Vector2 localMousePosition)
	{
		if (_primaryWorld == null)
		{
			UpdateLorePanel();
			return;
		}

		if (!TrySampleAtLocalPosition(localMousePosition, out var sample))
		{
			return;
		}

		// 点击地图就要更新 Oracle 的"最近一次选定区域"，与当前图层无关——
		// 改造前这条链路在 TrySampleBiome 里，改造后必须显式保留。
		UpdateOracleHoverFromSample(sample);

		var hazardSkulls = ComputeThreatSkulls(sample);
		_threatLabel.Text = $"生存威胁指数: {BuildThreatIcons(hazardSkulls)}";

		var modeText = GetViewModeText();
		var timelineEvents = GetTimelineEventsForCurrentWorld();
		_loreStateLabel.Text = $"模式：{modeText} | 纪元：{_currentEpoch} | {BuildReplayStatusText(timelineEvents)}";

		_loreText.Text = BuildNarrativeText(sample, hazardSkulls);
	}

	private LandformType ClassifyLandform(int x, int y, float seaLevel, float[,] elevation, float[,] moisture, float[,] river)
	{
		var safeSea = Mathf.Clamp(seaLevel, 0.0001f, 0.9999f);
		var basinSensitivity = Mathf.Clamp(BasinSensitivity, 0.5f, 2.0f);
		var current = elevation[x, y];

		if (current < safeSea)
		{
			var depth = (safeSea - current) / Mathf.Max(safeSea, 0.0001f);
			return depth > 0.45f ? LandformType.DeepOcean : LandformType.ShallowSea;
		}

		var relativeHeight = (current - safeSea) / Mathf.Max(1f - safeSea, 0.0001f);
		var localRelief = ComputeLocalRelief(elevation, x, y, MapWidth, MapHeight);
		var nearSea = IsNearSea(elevation, x, y, safeSea, MapWidth, MapHeight);
		var normalizedSensitivity = (basinSensitivity - 0.5f) / 1.5f;
		var (meanNeighbor, minNeighbor, maxNeighbor) = SampleNeighborStats(elevation, x, y, MapWidth, MapHeight);
		var depression = Mathf.Max(meanNeighbor - current, 0f);
		var slopeSignal = Mathf.Max(maxNeighbor - current, current - minNeighbor);

		var higherNeighborCount = 0;
		var lowerNeighborCount = 0;
		for (var oy = -1; oy <= 1; oy++)
		{
			for (var ox = -1; ox <= 1; ox++)
			{
				if (ox == 0 && oy == 0)
				{
					continue;
				}

				var nx = WrapX(x + ox, MapWidth);
				var ny = ClampY(y + oy, MapHeight);
				var diff = elevation[nx, ny] - current;
				if (diff > 0.018f)
				{
					higherNeighborCount++;
				}
				else if (diff < -0.018f)
				{
					lowerNeighborCount++;
				}
			}
		}

		var enclosedByHigher = higherNeighborCount >= Mathf.RoundToInt(Mathf.Lerp(5f, 7f, normalizedSensitivity)) &&
			lowerNeighborCount <= Mathf.RoundToInt(Mathf.Lerp(2f, 0f, normalizedSensitivity));
		var basinHeightThreshold = Mathf.Lerp(0.32f, 0.18f, normalizedSensitivity);
		var basinMoistureThreshold = Mathf.Lerp(0.42f, 0.55f, normalizedSensitivity);
		var basinRiverThreshold = Mathf.Lerp(0.02f, 0.05f, normalizedSensitivity);
		if (relativeHeight < basinHeightThreshold && enclosedByHigher && (moisture[x, y] > basinMoistureThreshold || river[x, y] > basinRiverThreshold || depression > 0.009f))
		{
			return LandformType.Basin;
		}

		var dryBasinHeightThreshold = basinHeightThreshold * 1.15f;
		if (relativeHeight < dryBasinHeightThreshold && enclosedByHigher && moisture[x, y] < 0.32f && river[x, y] < 0.03f && depression > 0.006f)
		{
			return LandformType.DryBasin;
		}

		if (!nearSea && river[x, y] > 0.20f && relativeHeight > 0.10f && relativeHeight < 0.66f && slopeSignal > 0.014f)
		{
			return LandformType.Valley;
		}

		if (nearSea && relativeHeight < 0.12f)
		{
			return LandformType.CoastalPlain;
		}

		if (relativeHeight < 0.30f)
		{
			return LandformType.Plain;
		}

		if (relativeHeight < 0.50f)
		{
			return LandformType.RollingHills;
		}

		if (relativeHeight > 0.78f || (relativeHeight > 0.68f && (localRelief > 0.045f || slopeSignal > 0.050f)))
		{
			return LandformType.Mountain;
		}

		if (relativeHeight > 0.64f && localRelief < 0.026f)
		{
			return LandformType.Plateau;
		}

		if (relativeHeight > 0.52f)
		{
			return LandformType.Upland;
		}

		return LandformType.RollingHills;
	}

	private static (float Mean, float Min, float Max) SampleNeighborStats(float[,] elevation, int x, int y, int width, int height)
	{
		var minValue = elevation[x, y];
		var maxValue = elevation[x, y];
		var sum = 0f;
		var count = 0;

		for (var oy = -1; oy <= 1; oy++)
		{
			for (var ox = -1; ox <= 1; ox++)
			{
				if (ox == 0 && oy == 0)
				{
					continue;
				}

				var nx = WrapX(x + ox, width);
				var ny = ClampY(y + oy, height);
				var value = elevation[nx, ny];
				if (value < minValue)
				{
					minValue = value;
				}

				if (value > maxValue)
				{
					maxValue = value;
				}

				sum += value;
				count++;
			}
		}

		if (count == 0)
		{
			return (elevation[x, y], elevation[x, y], elevation[x, y]);
		}

		return (sum / count, minValue, maxValue);
	}

	private static float ComputeLocalRelief(float[,] elevation, int x, int y, int width, int height)
	{
		var minValue = elevation[x, y];
		var maxValue = elevation[x, y];

		for (var oy = -1; oy <= 1; oy++)
		{
			for (var ox = -1; ox <= 1; ox++)
			{
				var nx = WrapX(x + ox, width);
				var ny = ClampY(y + oy, height);
				var value = elevation[nx, ny];
				if (value < minValue)
				{
					minValue = value;
				}
				if (value > maxValue)
				{
					maxValue = value;
				}
			}
		}

		return maxValue - minValue;
	}

	private static bool IsNearSea(float[,] elevation, int x, int y, float seaLevel, int width, int height)
	{
		for (var oy = -1; oy <= 1; oy++)
		{
			for (var ox = -1; ox <= 1; ox++)
			{
				if (ox == 0 && oy == 0)
				{
					continue;
				}

				var nx = WrapX(x + ox, width);
				var ny = ClampY(y + oy, height);
				if (elevation[nx, ny] <= seaLevel)
				{
					return true;
				}
			}
		}

		return false;
	}

	private static int WrapX(int x, int width)
	{
		var wrapped = x % width;
		if (wrapped < 0)
		{
			wrapped += width;
		}
		return wrapped;
	}

	private static int ClampY(int y, int height)
	{
		return Mathf.Clamp(y, 0, height - 1);
	}

	private void PositionBiomeHoverPanel(Vector2 localMousePosition)
	{
		var panel = _biomeHoverPanel;
		var mapSize = _mapRoot.Size;

		var targetX = localMousePosition.X + BiomeHoverPanelOffsetX;
		var targetY = localMousePosition.Y + BiomeHoverPanelOffsetY;

		var panelSize = panel.Size;
		if (panelSize.X <= 1f || panelSize.Y <= 1f)
		{
			panelSize = panel.GetCombinedMinimumSize();
		}

		var maxX = Mathf.Max(BiomeHoverPanelMargin, mapSize.X - panelSize.X - BiomeHoverPanelMargin);
		var maxY = Mathf.Max(BiomeHoverPanelMargin, mapSize.Y - panelSize.Y - BiomeHoverPanelMargin);
		var clampedX = Mathf.Clamp(targetX, BiomeHoverPanelMargin, maxX);
		var clampedY = Mathf.Clamp(targetY, BiomeHoverPanelMargin, maxY);

		panel.Position = new Vector2(clampedX, clampedY);
	}

}
