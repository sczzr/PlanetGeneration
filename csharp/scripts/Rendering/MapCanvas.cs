using Godot;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;
using PlanetGeneration.Core.Layers;
using System;
using System.Collections.Generic;

namespace PlanetGeneration.Rendering;

/// <summary>
/// 地图画布控制器：
/// 封装屏幕/视口到世界逻辑坐标变换（CanvasToWorld / WorldToCanvas）、
/// GPU 2D 矢量网格底图渲染（ArrayMesh）、
/// 矢量覆盖层绘制（河流、边界、海岸线、城市、贸易、网格、风向）、
/// 地块拾取与平滑曲线高亮。
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

    private ArrayMesh? _cellMesh;
    private CurvedMeshTopology? _topology;
    private Vector2[][]? _cachedCurvedEdges;

    private static readonly Texture2D WhiteTexture = ImageTexture.CreateFromImage(
        Image.CreateFromData(1, 1, false, Image.Format.Rgba8, new byte[] { 255, 255, 255, 255 }));

    public WorldSnapshot? Snapshot => _snapshot;
    public int HoveredCellId => _hoveredCellId;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        ClipContents = true;
    }

    public void AttachSnapshot(WorldSnapshot snapshot, LayerStackState layerStack, Font? font = null)
    {
        var isSameGeometry = _snapshot?.Geometry == snapshot.Geometry && _topology != null;
        _snapshot = snapshot;
        _layerStack = layerStack;
        _labelFont = font;
        _hoveredCellId = -1;
        _highlightRings.Clear();

        if (isSameGeometry && _cachedCurvedEdges != null)
        {
            UpdateMeshColors();
        }
        else
        {
            _topology = CurvedCellGeometry.BuildMeshTopology(snapshot.Geometry, 3);
            BuildCachedCurvedEdges(snapshot.Geometry);
            RebuildCellMesh();
        }

        QueueRedraw();
    }

    private void BuildCachedCurvedEdges(CellGeometry geom)
    {
        var raw = CurvedCellGeometry.GetCanonicalCurvedEdges(geom, 3);
        var list = new List<Vector2[]>(raw.Length);
        var width = (float)geom.Width;

        for (var i = 0; i < raw.Length; i++)
        {
            var pts = raw[i];
            if (pts.Length < 2) continue;

            var vpts = new Vector2[pts.Length];
            var cross = false;
            for (var k = 0; k < pts.Length; k++)
            {
                vpts[k] = new Vector2((float)pts[k].X, (float)pts[k].Y);
                if (k > 0 && Math.Abs(vpts[k].X - vpts[k - 1].X) > width * 0.5f)
                {
                    cross = true;
                    break;
                }
            }

            if (!cross)
            {
                list.Add(vpts);
            }
        }

        _cachedCurvedEdges = list.ToArray();
    }

    public void UpdateLayerStack(LayerStackState layerStack)
    {
        _layerStack = layerStack;
        UpdateMeshColors();
        QueueRedraw();
    }

    private void RebuildCellMesh()
    {
        if (_snapshot == null || _layerStack == null || _topology == null)
        {
            return;
        }

        var vertices = new Vector2[_topology.Vertices.Length];
        for (var i = 0; i < _topology.Vertices.Length; i++)
        {
            vertices[i] = new Vector2((float)_topology.Vertices[i].X, (float)_topology.Vertices[i].Y);
        }

        var colors = BuildMeshColors();
        var indices = _topology.Indices;

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        _cellMesh = mesh;
    }

    private Color[] BuildMeshColors()
    {
        if (_snapshot == null || _layerStack == null || _topology == null)
        {
            return Array.Empty<Color>();
        }

        var themeId = _layerStack.ActiveBaseThemeId;
        var count = _snapshot.Geometry.Count;

        var cellColors = new Color[count];
        for (var i = 0; i < count; i++)
        {
            cellColors[i] = BaseThemeColorPalette.GetCellColor(_snapshot, themeId, i);
        }

        var vertCount = _topology.Vertices.Length;
        var colors = new Color[vertCount];
        for (var v = 0; v < vertCount; v++)
        {
            var cellId = _topology.VertexToCell[v];
            colors[v] = cellColors[cellId];
        }

        return colors;
    }

    private void UpdateMeshColors()
    {
        if (_snapshot == null || _layerStack == null || _topology == null || _cellMesh == null)
        {
            RebuildCellMesh();
            return;
        }

        var colors = BuildMeshColors();
        var vertices = new Vector2[_topology.Vertices.Length];
        for (var i = 0; i < _topology.Vertices.Length; i++)
        {
            vertices[i] = new Vector2((float)_topology.Vertices[i].X, (float)_topology.Vertices[i].Y);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.Index] = _topology.Indices;

        _cellMesh.ClearSurfaces();
        _cellMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
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
            var rawRings = _snapshot.Geometry.GetCurvedHighlightRings(cellId, 3);
            for (var r = 0; r < rawRings.Length; r++)
            {
                var ring = rawRings[r];
                if (ring.Length < 3) continue;

                var vRing = new Vector2[ring.Length];
                for (var i = 0; i < ring.Length; i++)
                {
                    vRing[i] = new Vector2((float)ring[i].X, (float)ring[i].Y);
                }
                _highlightRings.Add(vRing);
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

        var geom = _snapshot.Geometry;
        if (geom.Width <= 0 || geom.Height <= 0 || Size.X <= 0 || Size.Y <= 0)
        {
            return;
        }

        var scaleX = Size.X / (float)geom.Width;
        var scaleY = Size.Y / (float)geom.Height;
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(scaleX, scaleY));

        // 精确计算世界坐标到最终屏幕物理像素的缩放比例
        var globalTransform = GetGlobalTransform();
        var screenScale = Mathf.Max((globalTransform.BasisXform(new Vector2(scaleX, 0f))).Length(), 0.0001f);

        // 1. 绘制 GPU 2D 矢量网格底图（平滑曲线地块，放大无像素锯齿）
        if (_cellMesh != null)
        {
            DrawMesh(_cellMesh, WhiteTexture);
        }

        // 2. 绘制矢量叠加图层
        var visibleRect = new Rect2(Vector2.Zero, new Vector2((float)geom.Width, (float)geom.Height));
        OverlayVectorRenderer.DrawOverlays(this, _snapshot, _layerStack, visibleRect, screenScale, _labelFont, _cachedCurvedEdges);

        // 3. 绘制平滑高亮环
        if (_highlightRings.Count > 0)
        {
            var strokeWidth = 2.5f / screenScale;
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
                DrawPolyline(closed, _highlightStroke, strokeWidth, true);
            }
        }
    }
}
