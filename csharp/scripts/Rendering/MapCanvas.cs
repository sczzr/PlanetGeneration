using Godot;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Layers;
using System;
using System.Collections.Generic;

namespace PlanetGeneration.Rendering;

/// <summary>
/// 地图画布控制器：
/// 封装屏幕/视口到世界逻辑坐标变换（ScreenToWorld / WorldToScreen）、
/// 矢量覆盖层绘制（河流、边界、海岸线、城市、贸易、网格、风向）、
/// 地块拾取与高亮。
/// </summary>
public partial class MapCanvas : Control
{
    private WorldSnapshot? _snapshot;
    private LayerStackState? _layerStack;
    private Font? _labelFont;
    private int _hoveredCellId = -1;

    public event Action<int>? CellHovered;
    public event Action<int, Vector2>? CellClicked;

    private readonly List<Vector2[]> _highlightRings = new(3);
    private readonly Color _highlightFill = new(1f, 0.96f, 0.60f, 0.24f);
    private readonly Color _highlightStroke = new(1f, 0.94f, 0.46f, 0.95f);

    public WorldSnapshot? Snapshot => _snapshot;
    public int HoveredCellId => _hoveredCellId;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        ClipContents = true;
    }

    public void AttachSnapshot(WorldSnapshot snapshot, LayerStackState layerStack, Font? font = null)
    {
        _snapshot = snapshot;
        _layerStack = layerStack;
        _labelFont = font;
        _hoveredCellId = -1;
        _highlightRings.Clear();
        QueueRedraw();
    }

    public void UpdateLayerStack(LayerStackState layerStack)
    {
        _layerStack = layerStack;
        QueueRedraw();
    }

    /// <summary>
    /// 将画布局部坐标映射到世界逻辑坐标 [0, Width) × [0, Height)。
    /// </summary>
    public Vector2 CanvasToWorld(Vector2 canvasPos)
    {
        if (_snapshot == null || Size.X <= 0f || Size.Y <= 0f)
        {
            return Vector2.Zero;
        }

        var geom = _snapshot.Geometry;
        var normX = Mathf.Clamp(canvasPos.X / Size.X, 0f, 0.999999f);
        var normY = Mathf.Clamp(canvasPos.Y / Size.Y, 0f, 0.999999f);

        return new Vector2((float)(normX * geom.Width), (float)(normY * geom.Height));
    }

    /// <summary>
    /// 将世界逻辑坐标映射到画布局部坐标。
    /// </summary>
    public Vector2 WorldToCanvas(Vector2 worldPos)
    {
        if (_snapshot == null)
        {
            return Vector2.Zero;
        }

        var geom = _snapshot.Geometry;
        var normX = worldPos.X / (float)geom.Width;
        var normY = worldPos.Y / (float)geom.Height;

        return new Vector2(normX * Size.X, normY * Size.Y);
    }

    /// <summary>
    /// 精确拾取指定画布坐标处的地块 ID。
    /// </summary>
    public int PickCell(Vector2 canvasPos)
    {
        if (_snapshot == null) return -1;
        var world = CanvasToWorld(canvasPos);
        return _snapshot.Geometry.FindCell(world.X, world.Y);
    }

    /// <summary>
    /// 设置高亮地块。
    /// </summary>
    public void SetHoveredCell(int cellId)
    {
        if (_hoveredCellId == cellId) return;

        _hoveredCellId = cellId;
        _highlightRings.Clear();

        if (_snapshot != null && cellId >= 0 && cellId < _snapshot.Geometry.Count)
        {
            var rawRings = _snapshot.Geometry.GetHighlightRings(cellId);
            for (var r = 0; r < rawRings.Length; r++)
            {
                var ring = rawRings[r];
                if (ring.Length < 3) continue;

                var canvasRing = new Vector2[ring.Length];
                for (var i = 0; i < ring.Length; i++)
                {
                    canvasRing[i] = WorldToCanvas(new Vector2((float)ring[i].X, (float)ring[i].Y));
                }
                _highlightRings.Add(canvasRing);
            }
        }

        CellHovered?.Invoke(cellId);
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_snapshot == null) return;

        if (@event is InputEventMouseMotion motion)
        {
            var cellId = PickCell(motion.Position);
            SetHoveredCell(cellId);
        }
        else if (@event is InputEventMouseButton button && button.Pressed && button.ButtonIndex == MouseButton.Left)
        {
            var cellId = PickCell(button.Position);
            CellClicked?.Invoke(cellId, button.Position);
        }
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationMouseExit)
        {
            SetHoveredCell(-1);
        }
    }

    public override void _Draw()
    {
        if (_snapshot == null || _layerStack == null)
        {
            return;
        }

        var visibleRect = new Rect2(Vector2.Zero, new Vector2((float)_snapshot.Geometry.Width, (float)_snapshot.Geometry.Height));

        // 绘制矢量叠加图层
        OverlayVectorRenderer.DrawOverlays(this, _snapshot, _layerStack, visibleRect, _labelFont);

        // 绘制地块高亮环
        if (_highlightRings.Count > 0)
        {
            foreach (var ring in _highlightRings)
            {
                if (ring.Length < 3) continue;

                DrawColoredPolygon(ring, _highlightFill);

                var closed = new Vector2[ring.Length + 1];
                for (var i = 0; i < ring.Length; i++)
                {
                    closed[i] = ring[i];
                }
                closed[ring.Length] = ring[0];
                DrawPolyline(closed, _highlightStroke, 2.0f);
            }
        }
    }
}
