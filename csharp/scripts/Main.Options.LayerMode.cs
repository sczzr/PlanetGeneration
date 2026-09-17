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
	private void SetupLayerOptions()
	{
		_layerOption.Clear();
		_layerOption.AddItem("地形总览", (int)MapLayer.Satellite);
		_layerOption.AddItem("板块构造", (int)MapLayer.Plates);
		_layerOption.AddItem("温度", (int)MapLayer.Temperature);
		_layerOption.AddItem("河流", (int)MapLayer.Rivers);
		_layerOption.AddItem("湿度", (int)MapLayer.Moisture);
		_layerOption.AddItem("风场", (int)MapLayer.Wind);
		_layerOption.AddItem("高程", (int)MapLayer.Elevation);
		_layerOption.AddItem("岩石类型", (int)MapLayer.RockTypes);
		_layerOption.AddItem("城市分布", (int)MapLayer.Cities);
		_layerOption.AddItem("矿产资源", (int)MapLayer.Ores);
		_layerOption.AddItem("生物群系", (int)MapLayer.Biomes);
		_layerOption.AddItem("地貌", (int)MapLayer.Landform);
		_layerOption.AddItem("生态演化", (int)MapLayer.Ecology);
		_layerOption.AddItem("文明疆域", (int)MapLayer.Civilization);
		_layerOption.AddItem("贸易走廊", (int)MapLayer.TradeRoutes);
		_layerOption.AddItem("地块划分", (int)MapLayer.PolygonGrid);
		_layerOption.Select(0);
		BuildLayerTree();
	}

	private void SelectLayerById(int layerId, bool persist = true)
	{
		var index = _layerOption.GetItemIndex(layerId);
		if (index < 0)
		{
			return;
		}

		_layerOption.Select(index);
		SyncLayerTreeSelection();
		RedrawCurrentLayer();
		UpdateLorePanel();
		if (persist)
		{
			SaveAdvancedSettings();
		}
	}

	private static string GetLayerCategoryName(int layerId)
	{
		if (Array.IndexOf(ArcaneLayerIds, layerId) >= 0)
		{
			return "神秘";
		}

		if (Array.IndexOf(HumanLayerIds, layerId) >= 0)
		{
			return "人文";
		}

		return "自然";
	}

	/// <summary>Builds the layer list table shown on the 图层 page.</summary>
	private void BuildLayerTree()
	{
		_layerTree.Clear();
		_layerTreeItems.Clear();
		_layerTree.Columns = 2;
		_layerTree.SetColumnTitle(0, "图层");
		_layerTree.SetColumnTitle(1, "类别");
		_layerTree.SetColumnExpand(0, true);
		_layerTree.SetColumnExpand(1, false);
		_layerTree.SetColumnCustomMinimumWidth(1, 56);

		var root = _layerTree.CreateItem();

		for (var index = 0; index < _layerOption.ItemCount; index++)
		{
			var layerId = _layerOption.GetItemId(index);
			var item = _layerTree.CreateItem(root);
			item.SetText(0, _layerOption.GetItemText(index));
			item.SetText(1, GetLayerCategoryName(layerId));
			item.SetMetadata(0, layerId);
			_layerTreeItems[layerId] = item;
		}

		SyncLayerTreeSelection();
	}

	private void OnLayerTreeItemSelected()
	{
		var selected = _layerTree.GetSelected();
		if (selected == null)
		{
			return;
		}

		var layerId = selected.GetMetadata(0).AsInt32();
		if (layerId == _layerOption.GetSelectedId())
		{
			return;
		}

		if (_layerOption.GetItemIndex(layerId) >= 0)
		{
			SelectLayerById(layerId);
		}
	}

	private void SyncLayerTreeSelection()
	{
		var selectedId = _layerOption.GetSelectedId();
		foreach (var pair in _layerTreeItems)
		{
			if (pair.Key == selectedId)
			{
				pair.Value.Select(0);
			}
			else
			{
				pair.Value.Deselect(0);
			}
		}
	}

	/// <summary>Display mode text for the lore panel, inferred from the active layer's category.</summary>
	private string GetViewModeText()
	{
		var layerId = _layerOption.GetSelectedId();
		if (Array.IndexOf(ArcaneLayerIds, layerId) >= 0)
		{
			return "奥术";
		}

		if (Array.IndexOf(HumanLayerIds, layerId) >= 0)
		{
			return "政区";
		}

		return "地理";
	}

	private void UpdateLayerQuickButtons()
	{
		SyncLayerTreeSelection();
	}

	private int ScaleUiFontSize(int baseSize)
	{
		return Mathf.Clamp(Mathf.RoundToInt(baseSize * _uiFontScale), 8, 72);
	}

	private void DisableMouseWheelForAllSliders()
	{
		DisableMouseWheelForAllSlidersRecursive(this);
	}

	private static void DisableMouseWheelForAllSlidersRecursive(Node node)
	{
		if (node is HSlider slider)
		{
			slider.Scrollable = false;
		}

		foreach (Node child in node.GetChildren())
		{
			DisableMouseWheelForAllSlidersRecursive(child);
		}
	}

}
