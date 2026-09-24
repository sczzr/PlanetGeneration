using PlanetGeneration.Application;
using Godot;
using LandformType = PlanetGeneration.Core.Domain.LandformType;
using LandformCategory = PlanetGeneration.Core.Domain.LandformCategory;
using LandformTypeExtensions = PlanetGeneration.Core.Domain.LandformTypeExtensions;
using PlanetGeneration.WorldGen;
using CivilizationEvent = PlanetGeneration.Core.Domain.CivilizationEvent;
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
	private void UpdateLorePanel()
	{
		var modeText = GetViewModeText();

		var timelineEvents = GetTimelineEventsForCurrentWorld();
		UpdateTimelineReplayCursor(timelineEvents);

		_loreStateLabel.Text = $"模式：{modeText} | 纪元：{_currentEpoch} | {BuildReplayStatusText(timelineEvents)}";
		var baseThreat = Mathf.Clamp(1 + _civilAggression / 40 + _magicDensity / 60, 1, 5);
		_threatLabel.Text = $"生存威胁指数: {BuildThreatIcons(baseThreat)}";

		if (_primaryWorld == null)
		{
			_loreText.Text = "[b]选定区域地质：[/b] 请先生成世界，再点击地图查看叙事详情。";
			return;
		}

		_loreText.Text = BuildNarrativeOverviewText();
	}

	private int ComputeThreatSkulls(CellSample sample)
	{
		if (_primaryWorld == null)
		{
			return 1;
		}

		var threat = 1;
		threat += _civilAggression > 55 ? 1 : 0;
		threat += _magicDensity > 70 ? 1 : 0;
		threat += _speciesDiversity > 80 ? 1 : 0;

		if (sample.Biome == BiomeType.TropicalDesert || sample.Biome == BiomeType.TemperateDesert || sample.Biome == BiomeType.SnowyMountain)
		{
			threat += 1;
		}

		if (sample.Landform is LandformType.Mountain or LandformType.Peak or LandformType.Volcano
			or LandformType.DeepOcean or LandformType.Trench or LandformType.Glacier or LandformType.Badlands)
		{
			threat += 1;
		}

		if (sample.Temperature < 0.22f || sample.Temperature > 0.82f)
		{
			threat += 1;
		}

		if (sample.Elevation < SeaLevel + 0.02f && sample.River > 0.12f)
		{
			threat += 1;
		}

		return Mathf.Clamp(threat, 1, 5);
	}

	private string BuildNarrativeOverviewText()
	{
		if (_primaryWorld == null)
		{
			return "[b]选定区域地质：[/b] 请先生成世界。";
		}

		var stats = _primaryWorld.Stats;
		var civilizationProfile = _civilAggression switch
		{
			< 30 => "更倾向贸易协作，城邦冲突频率较低",
			< 65 => "保持竞争与联盟并存，边界稳定性中等",
			_ => "战争动员能力强，边境摩擦频繁升级"
		};

		var arcaneProfile = _magicDensity switch
		{
			< 30 => "以经验技术为主，奥术仅限宗教礼仪",
			< 70 => "以太网络已介入交通、冶炼与医疗",
			_ => "高密度灵脉重塑生产体系，出现法术垄断阶层"
		};

		var diversityProfile = _speciesDiversity switch
		{
			< 30 => "族群结构单一，文化演化路径集中",
			< 70 => "多族群共存，区域文化呈带状分布",
			_ => "高多样性交汇，边境语言与信仰高度混融"
		};

		EnsureCivilizationSimulation(_primaryWorld);
		var civilization = _primaryWorld.CivilizationSimulation;
		var timelineText = BuildCivilizationTimelineText(civilization);

		return string.Concat(
			"[b]世界编年概览：[/b]\n",
			"当前纪元：", _currentEpoch.ToString(), " / ", MaxEpoch.ToString(), "\n",
			"海洋占比：", stats.OceanPercent.ToString("0.0"), "%\n",
			"城市规模：", stats.CityCount.ToString(), " 个聚落核心\n",
			"[b]文明趋势：[/b] ", civilizationProfile, "\n",
			"[b]奥术格局：[/b] ", arcaneProfile, "\n",
			"[b]族群生态：[/b] ", diversityProfile, "\n",
			timelineText,
			"\n提示：点击地图后将切换为区域级叙事。"
		);
	}

	private string BuildCivilizationTimelineText(CivilizationSimulationResult? civilization)
	{
		if (civilization == null || civilization.RecentEvents.Length == 0)
		{
			return "[b]近纪元事件：[/b] 暂无可回放事件。";
		}

		var selectedEpoch = _selectedTimelineEventEpoch >= 0 ? _selectedTimelineEventEpoch : _currentEpoch;
		var selectedIndex = ResolveTimelineEventIndex(civilization.RecentEvents, selectedEpoch);
		if (selectedIndex >= 0)
		{
			_selectedTimelineEventEpoch = civilization.RecentEvents[selectedIndex].Epoch;
		}

		var builder = new StringBuilder();
		builder.Append("[b]近纪元事件：[/b]\n");
		var maxEvents = civilization.RecentEvents.Length;
		for (var i = civilization.RecentEvents.Length - maxEvents; i < civilization.RecentEvents.Length; i++)
		{
			if (i < 0)
			{
				continue;
			}

			var evt = civilization.RecentEvents[i];
			var impactStars = BuildImpactIcons(evt.ImpactLevel);
			var isSelected = i == selectedIndex;
			var prefix = isSelected ? "▶ " : "- ";
			builder.Append(prefix).Append("第 ").Append(evt.Epoch).Append(" 纪元 [").Append(evt.Category).Append("] ").Append(evt.Summary).Append(" ").Append(impactStars);
			if (isSelected)
			{
				builder.Append(" [color=#ffd27a]◀ 回放焦点[/color]");
			}
			builder.Append("\n");
		}

		return builder.ToString();
	}

	private CivilizationEpochEvent[] GetTimelineEventsForCurrentWorld()
	{
		if (_primaryWorld == null)
		{
			return Array.Empty<CivilizationEpochEvent>();
		}

		EnsureCivilizationSimulation(_primaryWorld);
		return _primaryWorld.CivilizationSimulation?.RecentEvents ?? Array.Empty<CivilizationEpochEvent>();
	}

	private void UpdateTimelineReplayCursor(CivilizationEpochEvent[] events)
	{
		if (events.Length == 0)
		{
			if (_selectedTimelineEventEpoch < 0)
			{
				_selectedTimelineEventEpoch = _currentEpoch;
			}

			_epochEventIndexLabel.Text = "事件 --/--";
			_prevEpochButton.Disabled = _currentEpoch <= 0;
			_nextEpochButton.Disabled = _currentEpoch >= MaxEpoch;
			return;
		}

		var selectedEpoch = _selectedTimelineEventEpoch >= 0 ? _selectedTimelineEventEpoch : _currentEpoch;
		var selectedIndex = ResolveTimelineEventIndex(events, selectedEpoch);
		if (selectedIndex < 0)
		{
			_epochEventIndexLabel.Text = "事件 --/--";
			_prevEpochButton.Disabled = false;
			_nextEpochButton.Disabled = false;
			return;
		}

		_selectedTimelineEventEpoch = events[selectedIndex].Epoch;
		_epochEventIndexLabel.Text = $"事件 {selectedIndex + 1}/{events.Length}";
		_prevEpochButton.Disabled = selectedIndex <= 0;
		_nextEpochButton.Disabled = selectedIndex >= events.Length - 1;
	}

	private static int ResolveTimelineEventIndex(CivilizationEpochEvent[] events, int targetEpoch)
	{
		if (events.Length == 0)
		{
			return -1;
		}

		var bestIndex = 0;
		var bestDistance = Math.Abs(events[0].Epoch - targetEpoch);

		for (var i = 1; i < events.Length; i++)
		{
			var distance = Math.Abs(events[i].Epoch - targetEpoch);
			if (distance > bestDistance)
			{
				continue;
			}

			if (distance == bestDistance && events[i].Epoch < events[bestIndex].Epoch)
			{
				continue;
			}

			bestDistance = distance;
			bestIndex = i;
		}

		return bestIndex;
	}

	/// <summary>
	/// 地块版文明事件的同名重载。
	///
	/// 两份事件类型字段完全同构，之所以不合并成一个类型：
	/// <see cref="CivilizationEvent"/> 在**不引用 Godot 的核心层**，
	/// 而 <see cref="CivilizationEpochEvent"/> 在 Godot 侧——合并会把 Godot 依赖带进核心层。
	/// 这里只做一次转发，逻辑仍然是同一套（见上面的栅格版实现）。
	/// </summary>
	private static int ResolveTimelineEventIndex(CivilizationEvent[] events, int targetEpoch)
	{
		if (events.Length == 0)
		{
			return -1;
		}

		var bestIndex = 0;
		var bestDistance = Math.Abs(events[0].Epoch - targetEpoch);

		for (var i = 1; i < events.Length; i++)
		{
			var distance = Math.Abs(events[i].Epoch - targetEpoch);
			if (distance > bestDistance)
			{
				continue;
			}

			if (distance == bestDistance && events[i].Epoch < events[bestIndex].Epoch)
			{
				continue;
			}

			bestDistance = distance;
			bestIndex = i;
		}

		return bestIndex;
	}

	private string BuildReplayStatusText(CivilizationEpochEvent[] events)
	{
		if (events.Length == 0)
		{
			return "回放: --/--";
		}

		var index = ResolveTimelineEventIndex(events, _selectedTimelineEventEpoch >= 0 ? _selectedTimelineEventEpoch : _currentEpoch);
		if (index < 0)
		{
			return "回放: --/--";
		}

		return $"回放: {index + 1}/{events.Length}";
	}

	private CivilizationEpochEvent? GetFocusedTimelineEvent(CivilizationEpochEvent[] events)
	{
		if (events.Length == 0)
		{
			return null;
		}

		var index = ResolveTimelineEventIndex(events, _selectedTimelineEventEpoch >= 0 ? _selectedTimelineEventEpoch : _currentEpoch);
		if (index < 0 || index >= events.Length)
		{
			return null;
		}

		return events[index];
	}

	/// <summary>
	/// 地块版文明事件的同名重载；返回类型是 <see cref="CivilizationEvent"/>。
	/// 索引逻辑与栅格版共用同一套（<see cref="ResolveTimelineEventIndex(CivilizationEvent[], int)"/>），
	/// 因此两条路径的"回放焦点"永远指向同一个纪元的同一类事件。
	/// </summary>
	private CivilizationEvent? GetFocusedTimelineEvent(CivilizationEvent[] events)
	{
		if (events.Length == 0)
		{
			return null;
		}

		var index = ResolveTimelineEventIndex(events, _selectedTimelineEventEpoch >= 0 ? _selectedTimelineEventEpoch : _currentEpoch);
		if (index < 0 || index >= events.Length)
		{
			return null;
		}

		return events[index];
	}

	private void ApplyTimelineHotspotOverlay(Image image, GeneratedWorldData world, MapLayer layer)
	{
		var civilization = world.CivilizationSimulation;
		if (civilization == null)
		{
			return;
		}

		var focusedEvent = GetFocusedTimelineEvent(civilization.RecentEvents);
		if (focusedEvent == null)
		{
			return;
		}

		var hotspots = FindEventHotspots(world, civilization, focusedEvent);
		if (hotspots.Count == 0)
		{
			return;
		}

		var baseColor = focusedEvent.Category switch
		{
			"战争" => new Color(1f, 0.34f, 0.30f, 1f),
			"联盟" => new Color(0.40f, 0.82f, 1f, 1f),
			"贸易" => new Color(1f, 0.80f, 0.36f, 1f),
			_ => new Color(0.95f, 0.70f, 0.42f, 1f)
		};

		for (var i = 0; i < hotspots.Count; i++)
		{
			var hotspot = hotspots[i];
			var mappedX = Mathf.RoundToInt((hotspot.X + 0.5f) * image.GetWidth() / Mathf.Max(MapWidth, 1)) - 1;
			var mappedY = Mathf.RoundToInt((hotspot.Y + 0.5f) * image.GetHeight() / Mathf.Max(MapHeight, 1)) - 1;
			var scale = Mathf.Max(image.GetWidth() / (float)Mathf.Max(MapWidth, 1), image.GetHeight() / (float)Mathf.Max(MapHeight, 1));
			var radius = Mathf.Clamp(Mathf.RoundToInt((layer == MapLayer.Civilization ? 4f : 3f) * scale), 2, 20);
			var intensity = Mathf.Clamp(0.28f + hotspot.Score * 0.46f, 0.22f, 0.72f);
			DrawHotspotCircle(image, mappedX, mappedY, radius, baseColor, intensity);
		}
	}

	private List<TimelineHotspotPoint> FindEventHotspots(
		GeneratedWorldData world,
		CivilizationSimulationResult civilization,
		CivilizationEpochEvent focusedEvent)
	{
		var points = new List<TimelineHotspotPoint>(64);
		var width = MapWidth;
		var height = MapHeight;

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				if (world.Elevation[x, y] <= SeaLevel)
				{
					continue;
				}

				var influence = civilization.Influence[x, y];
				var border = civilization.BorderMask[x, y];
				var route = civilization.TradeRouteMask[x, y];
				var flow = civilization.TradeFlow[x, y];

				float score;
				switch (focusedEvent.Category)
				{
					case "战争":
						if (!border)
						{
							continue;
						}
						score = 0.56f * influence + 0.26f * flow + 0.18f * HashNoise01(Seed ^ focusedEvent.Epoch, x, y);
						break;
					case "联盟":
						if (!border && !route)
						{
							continue;
						}
						score = 0.46f * influence + 0.34f * flow + 0.20f * HashNoise01(Seed ^ (focusedEvent.Epoch * 3), x, y);
						break;
					default:
						if (!route)
						{
							continue;
						}
						score = 0.30f * influence + 0.50f * flow + 0.20f * HashNoise01(Seed ^ (focusedEvent.Epoch * 5), x, y);
						break;
				}

				score = Mathf.Clamp(score, 0f, 1f);
				if (score < 0.60f)
				{
					continue;
				}

				points.Add(new TimelineHotspotPoint(x, y, score));
			}
		}

		if (points.Count <= 8)
		{
			return points;
		}

		points.Sort((left, right) => right.Score.CompareTo(left.Score));
		var selected = new List<TimelineHotspotPoint>(8);
		for (var i = 0; i < points.Count && selected.Count < 8; i++)
		{
			var candidate = points[i];
			var tooClose = false;
			for (var j = 0; j < selected.Count; j++)
			{
				var existing = selected[j];
				var dx = Math.Abs(candidate.X - existing.X);
				dx = Math.Min(dx, width - dx);
				var dy = Math.Abs(candidate.Y - existing.Y);
				if (dx * dx + dy * dy < 64)
				{
					tooClose = true;
					break;
				}
			}

			if (!tooClose)
			{
				selected.Add(candidate);
			}
		}

		return selected;
	}

	private static void DrawHotspotCircle(Image image, int centerX, int centerY, int radius, Color tint, float intensity)
	{
		var width = image.GetWidth();
		var height = image.GetHeight();
		var radiusSq = radius * radius;

		for (var oy = -radius; oy <= radius; oy++)
		{
			for (var ox = -radius; ox <= radius; ox++)
			{
				var distSq = ox * ox + oy * oy;
				if (distSq > radiusSq)
				{
					continue;
				}

				var x = WrapX(centerX + ox, width);
				var y = ClampY(centerY + oy, height);
				var local = 1f - Mathf.Clamp(Mathf.Sqrt(distSq) / Mathf.Max(radius, 1), 0f, 1f);
				var alpha = Mathf.Clamp(intensity * (0.45f + 0.55f * local), 0f, 0.85f);

				var original = image.GetPixel(x, y);
				image.SetPixel(x, y, LerpColor(original, tint, alpha));
			}
		}
	}

	private static float HashNoise01(int seed, int x, int y)
	{
		uint hash = (uint)seed;
		hash ^= (uint)x * 1597334677u;
		hash ^= (uint)y * 3812015801u;
		hash = (hash ^ (hash >> 16)) * 2246822519u;
		hash ^= hash >> 13;
		return (hash & 0x00FFFFFFu) / 16777215f;
	}

	private static string BuildImpactIcons(int level)
	{
		var clamped = Mathf.Clamp(level, 1, 5);
		var builder = new StringBuilder(clamped);
		for (var i = 0; i < clamped; i++)
		{
			builder.Append("◆");
		}

		return builder.ToString();
	}

	private string BuildNarrativeText(CellSample sample, int threatSkulls)
	{
		if (_primaryWorld == null)
		{
			return "[b]选定区域地质：[/b] 数据不可用。";
		}

		var elevationText = BuildAltitudeDisplayText(sample.Elevation, SeaLevel, _currentReliefExaggeration);
		var biomeName = GetBiomeDisplayName(sample.Biome);
		var landformName = GetLandformDisplayName(sample.Landform);
		var geoCause = sample.Landform switch
		{
			LandformType.Basin => "地势封闭促使水汽滞留，形成稳定内陆聚落带",
			LandformType.DryBasin => "封闭低地蒸发强于补给，形成季节性水系与盐沼盆地",
			LandformType.Canyon => "河流下切与侧蚀塑造狭长险谷，交通险阻而水力充沛",
			LandformType.Coast => "海陆热力差驱动贸易港与潮汐渔业并行发展",
			LandformType.Mountain => "垂直高差切割交通，形成堡垒化山口城邦",
			LandformType.DeepOcean => "深水地形阻隔大陆接触，远洋文明长期隔离演化",
			LandformType.Trench => "板块俯冲剧烈断陷，深海极渊蕴藏地幔能量与古老沉降",
			LandformType.MidOceanRidge => "海底扩张裂缝热液喷涌，形成深海热液喷口与新洋壳",
			LandformType.ShallowOcean => "平缓大陆架阳光透射，浮游生物繁盛，构筑天然海洋渔场",
			LandformType.Island => "大洋孤岛阻断大陆掠食者，孕育独立而奇异的群岛演化生态",
			LandformType.Floodplain => "季候性大河漫溢沉积深厚腐殖沃土，孕育人口密集的核心农耕文明",
			LandformType.Delta => "江河入海口泥沙受顶托分流堆积的扇状水网低湿地，水运枢纽与商贸港口林立",
			LandformType.Peak => "万仞极峰终年极寒冰雪封冻，成为神明信仰图腾与修真禁区",
			LandformType.Volcano => "炽热岩浆喷发塑造锥体，地热温泉与金属矿脉促成特色工坊聚集",
			LandformType.RiftValley => "板块拉张断陷成阶梯裂谷，串联内陆深水湖泊与地堑通道",
			LandformType.Karst => "溶蚀石林暗河密布，奇峰突兀隐匿古修洞府与天坑秘境",
			LandformType.DesertDune => "强劲风力吹拂黄沙连绵如浪，水源断绝制约商队通行与绿洲据点",
			LandformType.Badlands => "暴雨风蚀刻蚀出千沟万壑绝壁，道路崎岖不生草木",
			LandformType.Glacier => "万年冰川如白龙沿山谷蠕动刨蚀，阻绝生息却蕴藏纯净极寒灵气",
			LandformType.Fjord => "古冰川U形深槽通连大洋，两侧峭壁千仞避风良港",
			LandformType.Wetland => "平缓低洼滞水孕育广袤沼泽，水草丛生阻碍大军推进却庇护珍稀物种",
			LandformType.Plateau => "隆升台地天高地阔，日光充裕但气压稀薄，适于耐寒游牧聚居",
			LandformType.Hill => "和缓冈峦绵延起伏，梯田茶山与散落山村依山傍水",
			LandformType.Plain => "沃野千里坦荡无垠，交通四通八达成为帝国争霸主战场",
			_ => "地势缓变塑造了扩张可达性与资源分布边界"
		};

		var societyConsequence = GetViewModeText() switch
		{
			"政区" => "政体在资源瓶颈下向同盟或征服两极分化",
			"奥术" => "灵脉走向决定法术学院与禁区的权力半径",
			_ => "地理约束主导人口迁移与产业布局"
		};

		var arcaneSignal = _magicDensity >= 70
			? "该区存在高能以太回廊，稀有矿脉与仪式遗迹重叠。"
			: "该区以低能以太背景为主，奥术活动受地貌限制。";

		// 地块来源时把地块编号也写进叙事，方便与"地块划分"图层对照。
		var regionTag = sample.IsCell ? $"地块 #{sample.CellId}" : "像素区域";

		return string.Concat(
			"[b]选定区域地质：[/b] ", landformName, " / ", biomeName, "（", regionTag, "）\n",
			"高度：", elevationText, "\n",
			"坐标：", sample.AnchorX.ToString(), ", ", sample.AnchorY.ToString(), "\n",
			"[b]地理因果：[/b] ", geoCause, "。\n",
			"[b]社会演化：[/b] ", societyConsequence, "。\n",
			"[b]奥术线索：[/b] ", arcaneSignal, "\n",
			"威胁评估：", BuildThreatIcons(threatSkulls), "（纪元 ", _currentEpoch.ToString(), "）"
		);
	}

	private static string BuildThreatIcons(int count)
	{
		var clamped = Mathf.Clamp(count, 1, 5);
		var builder = new StringBuilder(clamped * 2);
		for (var index = 0; index < clamped; index++)
		{
			builder.Append("💀");
		}

		return builder.ToString();
	}

	private static string BuildAltitudeDisplayText(float elevationValue, float seaLevel, float reliefExaggeration)
	{
		var safeSea = Mathf.Clamp(seaLevel, 0.0001f, 0.9999f);
		var exaggeration = Mathf.Clamp(reliefExaggeration, ReliefExaggerationMin, ReliefExaggerationMax);
		float meters;

		if (elevationValue >= safeSea)
		{
			var landNormalized = (elevationValue - safeSea) / Mathf.Max(1f - safeSea, 0.0001f);
			meters = landNormalized * EarthHighestPeakMeters * exaggeration;
		}
		else
		{
			var seaNormalized = (safeSea - elevationValue) / Mathf.Max(safeSea, 0.0001f);
			meters = -seaNormalized * EarthDeepestTrenchMeters * exaggeration;
		}

		return FormatAltitude(meters);
	}

	private static string GetBiomeDisplayName(BiomeType biome)
	{
		return biome switch
		{
			BiomeType.Ocean => "海洋",
			BiomeType.ShallowOcean => "浅海",
			BiomeType.Coastland => "海岸",
			BiomeType.Ice => "冰川",
			BiomeType.Tundra => "苔原",
			BiomeType.BorealForest => "北方针叶林",
			BiomeType.Taiga => "泰加林",
			BiomeType.Steppe => "寒漠",
			BiomeType.Grassland => "草原气候",
			BiomeType.Chaparral => "灌木地",
			BiomeType.TemperateDesert => "温带荒漠",
			BiomeType.TemperateSeasonalForest => "温带落叶林",
			BiomeType.TemperateRainForest => "温带雨林",
			BiomeType.Savanna => "热带草原气候",
			BiomeType.Shrubland => "湿地",
			BiomeType.TropicalDesert => "热带沙漠",
			BiomeType.TropicalSeasonalForest => "热带季雨林",
			BiomeType.TropicalRainForest => "热带雨林",
			BiomeType.RockyMountain => "岩石山地",
			BiomeType.SnowyMountain => "雪山",
			BiomeType.River => "河流",
			_ => biome.ToString()
		};
	}

	private static string GetBiomeDetailText(BiomeType biome)
	{
		return biome switch
		{
			BiomeType.Ocean => "深水海域，光照弱、温度低",
			BiomeType.ShallowOcean => "大陆架区域，营养盐相对丰富",
			BiomeType.Coastland => "海陆交汇，湿润多风",
			BiomeType.Ice => "常年冰雪覆盖，生态稀疏",
			BiomeType.Tundra => "冻土显著，低矮植被",
			BiomeType.BorealForest => "寒温带针叶林，冬季漫长",
			BiomeType.Taiga => "寒冷针叶林，生长期较短",
			BiomeType.Steppe => "冷干环境，植被稀少",
			BiomeType.Grassland => "半湿润带，草本植被为主",
			BiomeType.Chaparral => "夏干冬湿，灌丛为主",
			BiomeType.TemperateDesert => "温带干旱区，植被低覆盖",
			BiomeType.TemperateSeasonalForest => "四季分明，阔叶林主导",
			BiomeType.TemperateRainForest => "全年较湿，林下苔藓丰富",
			BiomeType.Savanna => "干湿季明显，草木交错",
			BiomeType.Shrubland => "低洼积水区，灌丛与草甸混生",
			BiomeType.TropicalDesert => "极端干旱，蒸发强",
			BiomeType.TropicalSeasonalForest => "季节性降雨，雨季旺盛",
			BiomeType.TropicalRainForest => "高温高湿，物种最丰富",
			BiomeType.RockyMountain => "裸岩地形，坡陡且土层薄",
			BiomeType.SnowyMountain => "高海拔寒冷，山顶常年积雪",
			BiomeType.River => "地表径流明显，水源持续",
			_ => "—"
		};
	}

	private static string GetLandformDisplayName(LandformType landform) => LandformTypeExtensions.GetDisplayName(landform);

	private static string GetLandformDetailText(LandformType landform) => LandformTypeExtensions.GetDescription(landform);

	private static Color GetLandformColor(LandformType landform) => PlanetGeneration.Rendering.BaseThemeColorPalette.GetLandformColor(landform);

}
