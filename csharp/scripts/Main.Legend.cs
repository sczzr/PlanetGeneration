using Godot;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Rendering;
using PlanetGeneration.WorldGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using CoreOre = PlanetGeneration.Core.Domain.OreType;
using IOPath = System.IO.Path;
using IOFile = System.IO.File;
using IODirectory = System.IO.Directory;
using IOFileInfo = System.IO.FileInfo;
using CryptoSha256 = System.Security.Cryptography.SHA256;

namespace PlanetGeneration;

public partial class Main : Control
{
	private void UpdateLegend(MapLayer layer)
	{
		switch (layer)
		{
			case MapLayer.Temperature:
				SetGradientLegend("温度图例", "低温", "高温",
					new Color(0.0f, 0.298f, 1.0f),
					new Color(1.0f, 0.894f, 0.361f),
					new Color(1.0f, 0.165f, 0.0f));
				break;
			case MapLayer.Moisture:
				SetGradientLegend("降水量图例 (mm)", "0 mm", "25+ mm",
					new Color(0.957f, 0.973f, 0.988f),
					new Color(0.659f, 0.816f, 0.941f),
					new Color(0.263f, 0.557f, 0.812f),
					new Color(0.063f, 0.294f, 0.561f),
					new Color(0.016f, 0.110f, 0.259f));
				break;
			case MapLayer.Wind:
				SetGradientLegend("风场降水 (10 m/s 标尺)", "0 mm", "25+ mm",
					new Color(0.957f, 0.973f, 0.988f),
					new Color(0.659f, 0.816f, 0.941f),
					new Color(0.263f, 0.557f, 0.812f),
					new Color(0.063f, 0.294f, 0.561f),
					new Color(0.016f, 0.110f, 0.259f));
				break;
			case MapLayer.Rivers:
				SetGradientLegend("河流图例", "弱", "强",
					new Color(0.055f, 0.247f, 0.584f),
					new Color(0.0f, 0.0f, 1.0f));
				break;
			case MapLayer.Elevation:
				if (_elevationStyle == ElevationStyle.Realistic)
				{
					SetGradientLegend("高程图例", "海沟", "雪山",
						new Color(0.02f, 0.15f, 0.39f),
						new Color(0.09f, 0.46f, 0.77f),
						new Color(0.39f, 0.68f, 0.34f),
						new Color(0.77f, 0.72f, 0.58f),
						new Color(0.95f, 0.93f, 0.86f));
				}
				else
				{
					SetGradientLegend("高程图例", "海沟", "雪山",
						new Color(0.02f, 0.07f, 0.23f),
						new Color(0.09f, 0.46f, 0.77f),
						new Color(0.35f, 0.67f, 0.32f),
						new Color(0.78f, 0.74f, 0.57f),
						new Color(0.98f, 0.98f, 0.98f));
				}
				break;
			case MapLayer.Biomes:
				SetBiomeLegend();
				break;
			case MapLayer.PolygonGrid:
				// 地块划分图层用的就是生物群系配色，图例可以完全复用——
				// 这样与"生物群系"栅格图层并排看时，差异只可能来自几何。
				SetBiomeLegend();
				break;
			case MapLayer.Landform:
				SetLandformLegend();
				break;
			case MapLayer.Ecology:
				SetGradientLegend("生态健康图例", "脆弱", "繁荣",
					new Color(0.54f, 0.31f, 0.17f),
					new Color(0.84f, 0.62f, 0.27f),
					new Color(0.46f, 0.69f, 0.32f),
					new Color(0.17f, 0.81f, 0.45f));
				break;
			case MapLayer.Civilization:
				SetGradientLegend("文明影响图例", "边缘", "核心",
					new Color(0.22f, 0.25f, 0.32f),
					new Color(0.44f, 0.56f, 0.74f),
					new Color(0.78f, 0.50f, 0.26f),
					new Color(0.95f, 0.85f, 0.56f));
				break;
			case MapLayer.TradeRoutes:
				SetGradientLegend("贸易走廊图例", "弱", "强",
					new Color(0.25f, 0.29f, 0.34f),
					new Color(0.64f, 0.53f, 0.36f),
					new Color(0.92f, 0.73f, 0.38f),
					new Color(0.97f, 0.89f, 0.62f));
				break;
			default:
				_legendPanel.Visible = false;
				_biomeLegendPanel.Visible = false;
				break;
		}
	}

	private void SetGradientLegend(string title, string minText, string maxText, params Color[] stops)
	{
		if (stops.Length < 2)
		{
			_legendPanel.Visible = false;
			return;
		}

		_legendTitle.Text = title;
		_legendMinLabel.Text = minText;
		_legendMaxLabel.Text = maxText;

		var legendImage = BuildGradientLegendImage(220, 14, stops);
		_legendTexture.Texture = ImageTexture.CreateFromImage(legendImage);
		_legendPanel.Visible = true;
		_biomeLegendPanel.Visible = false;
	}

	private void SetBiomeLegend()
	{
		_legendPanel.Visible = false;
		_biomeLegendText.Text = BuildBiomeLegendText();
		_biomeLegendPanel.Visible = true;
	}

	private void SetLandformLegend()
	{
		_legendPanel.Visible = false;
		_biomeLegendText.Text = BuildLandformLegendText();
		_biomeLegendPanel.Visible = true;
	}

	private void SetInkWashLegend()
	{
		_legendPanel.Visible = false;
		_biomeLegendText.Text = BuildInkWashLegendText();
		_biomeLegendPanel.Visible = true;
	}

	private void SetGuohuaLegend()
	{
		_legendPanel.Visible = false;
		_biomeLegendText.Text = BuildGuohuaLegendText();
		_biomeLegendPanel.Visible = true;
	}

	private void SetOreLegend()
	{
		_legendPanel.Visible = false;
		_biomeLegendText.Text = BuildOreLegendText();
		_biomeLegendPanel.Visible = true;
	}

	/// <summary>
	/// 矿产图例。色块直接取 BaseThemeColorPalette.OreColors，按工业、超自然、卡牌三类与天地灵凡四阶呈现。
	/// </summary>
	private static string BuildOreLegendText()
	{
		var builder = new StringBuilder(1536);

		foreach (var (category, items) in OreLegendCategories)
		{
			builder.Append("[b]").Append(category).Append("[/b]\n");
			foreach (var (tier, ore) in items)
			{
				builder.Append("[color=")
					.Append(BaseThemeColorPalette.OreColors[(int)ore].ToHtml())
					.Append("]■[/color] ")
					.Append(ore.GetDisplayName())
					.Append(" [color=#9ca3af][")
					.Append(tier)
					.Append("][/color]\n");
			}
			builder.Append('\n');
		}

		builder.Append("[color=#d9b96c]工业看地质，超凡看灵脉，卡牌看文明。\n三类资源垂直叠加。品阶：天(S)·地(A)·灵(B)·凡(C/D)。[/color]");
		return builder.ToString();
	}

	private static readonly (string Category, (string Tier, CoreOre Ore)[] Items)[] OreLegendCategories =
	{
		("基础工业矿产 (地质)", new[]
		{
			("凡", CoreOre.Stone),
			("凡", CoreOre.Clay),
			("凡", CoreOre.Limestone),
			("凡", CoreOre.Coal),
			("凡", CoreOre.Iron),
			("凡", CoreOre.Copper),
			("灵", CoreOre.Aluminum),
			("灵", CoreOre.Silicon),
			("地", CoreOre.Oil),
			("地", CoreOre.NaturalGas),
			("地", CoreOre.RareMetal)
		}),
		("超自然矿产 (灵脉)", new[]
		{
			("灵", CoreOre.SpiritCrystal),
			("灵", CoreOre.SunfireCrystal),
			("灵", CoreOre.FrostSoulCrystal),
			("灵", CoreOre.ThunderMarrow),
			("灵", CoreOre.LifePith),
			("地", CoreOre.NetherCrystal),
			("地", CoreOre.VoidCrystal),
			("地", CoreOre.AstralPith),
			("天", CoreOre.LawStone),
			("天", CoreOre.GenesisOre)
		}),
		("卡牌资源 (文明)", new[]
		{
			("灵", CoreOre.MemorySand),
			("灵", CoreOre.RuneOre),
			("地", CoreOre.ResonanceCrystal),
			("地", CoreOre.EchoStone),
			("地", CoreOre.OrderedGold),
			("天", CoreOre.RealmCasketCrystal),
			("天", CoreOre.KarmaStone)
		})
	};

	/// <summary>矿种中文全名，包含单字品阶。</summary>
	internal static string GetOreDisplayName(CoreOre ore)
	{
		if (ore == CoreOre.None) return "无矿";
		return $"{ore.GetDisplayName()} [{ore.GetTierName()}]";
	}

	/// <summary>格式化地块矿产详情（包含主显矿及多层叠加资源）。</summary>
	internal static string FormatCellOreDetail(PlanetGeneration.Core.Application.CellSampleDetails sample)
	{
		var primary = GetOreDisplayName(sample.Ore);
		var extras = new List<string>(3);

		if (sample.IndustrialOre != CoreOre.None && sample.IndustrialOre != sample.Ore)
		{
			extras.Add($"工业:{sample.IndustrialOre.GetDisplayName()}[{sample.IndustrialOre.GetTierName()}]");
		}
		if (sample.SupernaturalOre != CoreOre.None && sample.SupernaturalOre != sample.Ore)
		{
			extras.Add($"超凡:{sample.SupernaturalOre.GetDisplayName()}[{sample.SupernaturalOre.GetTierName()}]");
		}
		if (sample.CardOre != CoreOre.None && sample.CardOre != sample.Ore)
		{
			extras.Add($"遗迹:{sample.CardOre.GetDisplayName()}[{sample.CardOre.GetTierName()}]");
		}

		if (extras.Count > 0)
		{
			return $"{primary} ({string.Join(" | ", extras)})";
		}
		return primary;
	}

	private static string BuildGuohuaLegendText()
	{
		var builder = new StringBuilder(512);
		builder.Append("[b][color=#2c2621]中国古代青绿山水手绘舆图[/color][/b]\n\n");
		builder.Append("[color=#ad3323]●[/color] [b]城池[/b]：重郭石垣、飞檐宝塔楼阁\n");
		builder.Append("[color=#8b5a2b]●[/color] [b]城镇[/b]：方城合院、门楼寨堡\n");
		builder.Append("[color=#665c4f]●[/color] [b]村庄[/b]：人字瓦舍、乡村茅居聚落\n");
		builder.Append("[color=#a83a2b]●[/color] [b]寺庙[/b]：古刹名寺、八角佛塔\n");
		builder.Append("[color=#2d5a3f]●[/color] [b]山峰[/b]：工笔青绿群峰、奇峰主脊\n\n");
		builder.Append("[color=#3a7391]≋ 沧海碧波、细密水纹与游弋木帆船\n");
		builder.Append("─ 虚线古道驿径，跨江古石拱桥\n");
		builder.Append("☘ 翠竹篁丛、松柏密林与层叠水田[/color]");
		return builder.ToString();
	}

	private static string BuildInkWashLegendText()
	{
		var entries = new (string ColorHex, string Name)[]
		{
			("#f0e4c9", "宣纸平原"),
			("#e8d5a3", "京畿腹地"),
			("#e3c887", "大漠淡金"),
			("#ddcda6", "滨海沙嘴"),
			("#7a9c7c", "青绿丘陵"),
			("#a08055", "浅绛山麓"),
			("#71838f", "靛墨山脊"),
			("#3b4a57", "浓墨高山"),
			("#f6f2e6", "留白雪峰"),
			("#eef2ee", "极地淡墨"),
			("#3f8fb8", "碧蓝水系"),
			("#5a86ab", "石青海域"),
			("#243c56", "海沟深渊"),
			("#a83a2b", "朱砂火山")
		};

		var builder = new StringBuilder(768);
		foreach (var entry in entries)
		{
			builder.Append("[color=")
				.Append(entry.ColorHex)
				.Append("]■[/color] ")
				.Append(entry.Name)
				.Append('\n');
		}

		return builder.ToString();
	}

	private static string BuildBiomeLegendText()
	{
		var entries = new (string ColorHex, string Name)[]
		{
			("#2f5f88", "海洋"),
			("#4f7ea8", "浅海"),
			("#dfe4c9", "海岸"),
			("#2fb95a", "温带落叶林"),
			("#b8c98a", "草原气候"),
			("#4f6e34", "北方针叶林"),
			("#46a857", "温带雨林"),
			("#7c8f53", "湿地"),
			("#c2d3da", "冰川"),
			("#a1814a", "苔原"),
			("#cfd18a", "热带草原气候"),
			("#c7c5ac", "寒漠"),
			("#e9d79b", "热带沙漠"),
			("#aed45a", "热带季雨林"),
			("#7acb33", "热带雨林"),
			("#2ea3d4", "河流/湿润河谷")
		};

		var builder = new StringBuilder(768);
		foreach (var entry in entries)
		{
			builder.Append("[color=")
				.Append(entry.ColorHex)
				.Append("]■[/color] ")
				.Append(entry.Name)
				.Append('\n');
		}

		return builder.ToString();
	}

	private static string BuildLandformLegendText()
	{
		var builder = new StringBuilder(1024);

		void AppendCategory(string categoryTitle, LandformType[] landforms)
		{
			builder.Append("[b][color=#dcdcdc]").Append(categoryTitle).Append("[/color][/b]\n");
			foreach (var lf in landforms)
			{
				var color = BaseThemeColorPalette.GetLandformColor(lf);
				var hex = "#" + color.ToHtml(false);
				builder.Append("[color=")
					.Append(hex)
					.Append("]■[/color] ")
					.Append(lf.GetDisplayName())
					.Append('\n');
			}
			builder.Append('\n');
		}

		AppendCategory("海洋构造地貌", new[]
		{
			LandformType.Ocean,
			LandformType.DeepOcean,
			LandformType.Trench,
			LandformType.ShallowOcean,
			LandformType.MidOceanRidge,
			LandformType.Island,
			LandformType.Coast
		});

		AppendCategory("大地构造地貌", new[]
		{
			LandformType.Plain,
			LandformType.Basin,
			LandformType.Plateau,
			LandformType.Hill,
			LandformType.Mountain,
			LandformType.Peak,
			LandformType.Volcano,
			LandformType.RiftValley
		});

		AppendCategory("流水侵蚀沉积", new[]
		{
			LandformType.Floodplain,
			LandformType.Delta,
			LandformType.Canyon,
			LandformType.Wetland
		});

		AppendCategory("气候与特殊岩性", new[]
		{
			LandformType.DryBasin,
			LandformType.Karst,
			LandformType.DesertDune,
			LandformType.Badlands,
			LandformType.Glacier,
			LandformType.Fjord
		});

		return builder.ToString();
	}

	private static Image BuildGradientLegendImage(int width, int height, Color[] stops)
	{
		var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
		if (width <= 0 || height <= 0)
		{
			return image;
		}

		for (var x = 0; x < width; x++)
		{
			var t = width <= 1 ? 0f : (float)x / (width - 1);
			var scaled = t * (stops.Length - 1);
			var segment = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, stops.Length - 2);
			var localT = scaled - segment;
			var color = LerpColor(stops[segment], stops[segment + 1], localT);

			for (var y = 0; y < height; y++)
			{
				image.SetPixel(x, y, color);
			}
		}

		return image;
	}

	private static Color LerpColor(Color a, Color b, float t)
	{
		return new Color(
			Mathf.Lerp(a.R, b.R, t),
			Mathf.Lerp(a.G, b.G, t),
			Mathf.Lerp(a.B, b.B, t),
			Mathf.Lerp(a.A, b.A, t)
		);
	}

}
