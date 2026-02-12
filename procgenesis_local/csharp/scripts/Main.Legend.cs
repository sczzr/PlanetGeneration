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
				SetGradientLegend("降水图例", "少", "多",
					new Color(0.851f, 0.925f, 1.0f),
					new Color(0.353f, 0.663f, 1.0f),
					new Color(0.051f, 0.247f, 0.584f));
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
			("#7acb33", "热带雨林")
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
		var entries = new (string ColorHex, string Name)[]
		{
			("#0a1f4d", "深海盆地"),
			("#2f5f88", "大陆架浅海"),
			("#c9d8ae", "滨海平原"),
			("#98c47a", "内陆平原"),
			("#86b472", "内陆盆地"),
			("#b0be77", "丘陵"),
			("#b79f73", "高地"),
			("#9e8d63", "高原台地"),
			("#7e6b57", "山地")
		};

		var builder = new StringBuilder(512);
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
