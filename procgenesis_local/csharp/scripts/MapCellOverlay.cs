using Godot;
using System.Collections.Generic;

namespace PlanetGeneration;

/// <summary>
/// 地块高亮覆盖层：把当前悬停（或选中）的地块多边形画出来。
///
/// 为什么需要它：改造前"高亮"只有一个悬停信息面板，地图上没有任何视觉反馈。
/// 有了地块之后，高亮的自然表达就是沿多边形描边——这也是多边形相对像素最直观的收益。
///
/// 它被挂成 <c>MapTexture</c> 的子节点，所以**局部坐标系就是纹理的局部空间**，
/// 不需要任何全局变换换算，与缩放的祖先节点（MapAspect 的 Scale）完全解耦。
/// 跨经度缝的地块会多画一份副本，超出的部分由外层带 ClipContents 的容器裁掉。
/// </summary>
public partial class MapCellOverlay : Control
{
    private readonly List<Vector2[]> _rings = new(3);
    private readonly Color _fill = new(1f, 0.96f, 0.60f, 0.24f);
    private readonly Color _stroke = new(1f, 0.94f, 0.46f, 0.95f);

    /// <summary>设置要高亮的多边形环（局部坐标；传多份用于跨经度缝的地块）。</summary>
    public void SetRings(List<Vector2[]>? rings)
    {
        _rings.Clear();
        if (rings != null)
        {
            _rings.AddRange(rings);
        }

        QueueRedraw();
    }

    /// <summary>清空高亮。</summary>
    public void Clear() => SetRings(null);

    public override void _Draw()
    {
        if (_rings.Count == 0)
        {
            return;
        }

        foreach (var ring in _rings)
        {
            if (ring.Length < 3)
            {
                continue;
            }

            // 地块是 Voronoi 单元，必然为凸，所以可以直接用 DrawColoredPolygon。
            DrawColoredPolygon(ring, _fill);

            // DrawPolyline 不会自动闭合，手动补一个回到起点的顶点。
            var closed = new Vector2[ring.Length + 1];
            for (var i = 0; i < ring.Length; i++)
            {
                closed[i] = ring[i];
            }

            closed[ring.Length] = ring[0];
            DrawPolyline(closed, _stroke, 1.5f, true);
        }
    }
}
