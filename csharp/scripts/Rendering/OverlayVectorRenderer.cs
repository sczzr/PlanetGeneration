using Godot;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;
using PlanetGeneration.Core.Layers;
using System;
using System.Collections.Generic;

namespace PlanetGeneration.Rendering;

/// <summary>
/// 渲染用规范化边拓扑（附带所属 Cell 关系）。
/// </summary>
public sealed class RenderEdge
{
    public required Vector2[] Points { get; init; }
    public required int CellA { get; init; }
    public required int CellB { get; init; }
}

/// <summary>
/// 平滑板块边界渲染段（包含顶点坐标、平滑渐变颜色与视口包围盒）。
/// </summary>
public sealed class PlateBoundarySegment
{
    public required Vector2[] Points { get; init; }
    public required Color[] Colors { get; init; }
    public required Rect2 BoundingBox { get; init; }
    public required bool IsUniformColor { get; init; }
}

/// <summary>
/// 8 种叠加图层矢量绘制器：
/// 河流、政体边界、海岸线、城市、贸易路线、地块轮廓、风向箭头、板块边界。
/// 支持屏幕像素线宽归一化（无论放大缩小，线条始终保持纤细平滑）。
/// </summary>
public static class OverlayVectorRenderer
{
    private static readonly Color RiverColor = Color.FromHtml("#1a75d2");
    private static readonly Color CoastlineColor = Color.FromHtml("#111111");
    private static readonly Color BorderColor = Color.FromHtml("#ffe066");
    private static readonly Color TradeRouteGlowColor = new(1.0f, 0.72f, 0.15f);
    private static readonly Color TradeRouteCoreColor = new(1.0f, 0.90f, 0.35f);
    private static readonly Color CellOutlineColor = new(0.12f, 0.14f, 0.18f, 0.45f);

    private static readonly Color PlateConvergent = new(0.98f, 0.22f, 0.22f);
    private static readonly Color PlateDivergent = new(0.22f, 0.92f, 0.95f);
    private static readonly Color PlateTransform = new(0.98f, 0.80f, 0.28f);

    public static void DrawOverlays(
        CanvasItem item,
        WorldSnapshot snapshot,
        LayerStackState layerStack,
        Rect2 visibleRect,
        float screenScale = 1.0f,
        Font? font = null,
        Vector2[][]? cachedCurvedEdges = null,
        RenderEdge[]? cachedRenderEdges = null,
        PlateBoundarySegment[]? cachedPlateBoundaries = null)
    {
        var safeScale = Mathf.Max(screenScale, 0.0001f);

        foreach (var overlayId in layerStack.ActiveOverlayIds)
        {
            var alpha = layerStack.GetOpacity(overlayId);
            if (alpha <= 0.001f)
            {
                continue;
            }

            var widthScale = layerStack.GetWidthOrSize(overlayId);

            switch (overlayId)
            {
                case "rivers":
                    DrawRivers(item, snapshot, alpha, widthScale, visibleRect, safeScale);
                    break;
                case LayerRegistry.LayerCoastlines or "coastlines":
                    DrawCoastlines(item, snapshot, alpha, widthScale, visibleRect, safeScale, cachedRenderEdges);
                    break;
                case "polity_borders" or "borders":
                    DrawBorders(item, snapshot, alpha, widthScale, visibleRect, safeScale, cachedRenderEdges);
                    break;
                case "cities":
                    DrawCities(item, snapshot, alpha, font, visibleRect, safeScale);
                    break;
                case "city_labels":
                    if (!layerStack.IsOverlayActive("cities") && font != null)
                    {
                        DrawCityLabelsOnly(item, snapshot, alpha, font, visibleRect, safeScale);
                    }
                    break;
                case "trade_routes" or "trade_network":
                    DrawTradeRoutes(item, snapshot, alpha, widthScale, visibleRect, safeScale);
                    break;
                case "cell_borders" or "cell_outlines":
                    DrawCellOutlines(item, snapshot, alpha, widthScale, visibleRect, safeScale, cachedCurvedEdges);
                    break;
                case "wind_arrows":
                    DrawWindArrows(item, snapshot, alpha, widthScale, visibleRect, safeScale, font);
                    break;
                case "plate_borders" or "plate_boundaries":
                    DrawPlateBoundaries(item, snapshot, alpha, widthScale, visibleRect, safeScale, cachedPlateBoundaries, cachedRenderEdges);
                    break;
            }
        }
    }

    public static void DrawOverlays(
        CanvasItem item,
        WorldSnapshot snapshot,
        LayerStackState layerStack,
        Rect2 visibleRect,
        float screenScale,
        Font? font,
        Vector2[][]? cachedCurvedEdges,
        RenderEdge[]? cachedRenderEdges)
        => DrawOverlays(item, snapshot, layerStack, visibleRect, screenScale, font, cachedCurvedEdges, cachedRenderEdges, null);

    public static void DrawOverlays(
        CanvasItem item,
        WorldSnapshot snapshot,
        LayerStackState layerStack,
        Rect2 visibleRect,
        float screenScale,
        Font? font,
        Vector2[][]? cachedCurvedEdges)
        => DrawOverlays(item, snapshot, layerStack, visibleRect, screenScale, font, cachedCurvedEdges, null, null);

    public static void DrawOverlays(
        CanvasItem item,
        WorldSnapshot snapshot,
        LayerStackState layerStack,
        Rect2 visibleRect,
        Font? font)
        => DrawOverlays(item, snapshot, layerStack, visibleRect, 1.0f, font, null, null, null);

    private static void DrawRivers(CanvasItem item, WorldSnapshot snapshot, float alpha, float widthScale, Rect2 visibleRect, float screenScale)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var color = new Color(RiverColor.R, RiverColor.G, RiverColor.B, alpha * 0.92f);

        for (var i = 0; i < geom.Count; i++)
        {
            if (fields.River[i] <= 0) continue;

            var target = fields.Downslope[i];
            if (target < 0 || target == i) continue;

            var p0 = new Vector2((float)geom.CentroidX[i], (float)geom.CentroidY[i]);
            var p1 = new Vector2((float)geom.CentroidX[target], (float)geom.CentroidY[target]);

            // 视口粗裁剪
            if (!visibleRect.HasPoint(p0) && !visibleRect.HasPoint(p1)) continue;

            // 经度缝跨越防拉丝：横向位移过大则不画单一直线跨屏
            if (Math.Abs(p0.X - p1.X) > geom.Width * 0.5f) continue;

            var flux = fields.Flux[i];
            var targetPx = Mathf.Clamp(Mathf.Log(1f + flux) * 0.65f * widthScale, 1.0f, 5.0f * widthScale);
            var w = targetPx / screenScale;
            item.DrawLine(p0, p1, color, w, true);
        }
    }

    private static void DrawCoastlines(
        CanvasItem item,
        WorldSnapshot snapshot,
        float alpha,
        float widthScale,
        Rect2 visibleRect,
        float screenScale,
        RenderEdge[]? cachedEdges = null)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var seaLevel = snapshot.Options.SeaLevel;
        var color = new Color(CoastlineColor.R, CoastlineColor.G, CoastlineColor.B, alpha * 0.85f);
        var lineWidth = Mathf.Clamp(1.3f * widthScale, 0.6f, 4.0f) / screenScale;

        if (cachedEdges != null && cachedEdges.Length > 0)
        {
            for (var i = 0; i < cachedEdges.Length; i++)
            {
                var edge = cachedEdges[i];
                var a = edge.CellA;
                var b = edge.CellB;
                if (a < 0 && b < 0) continue;

                var landA = a >= 0 && fields.Height[a] >= seaLevel;
                var landB = b >= 0 && fields.Height[b] >= seaLevel;

                if (landA == landB) continue;

                var pts = edge.Points;
                if (pts.Length < 2) continue;

                var minX = Mathf.Min(pts[0].X, pts[^1].X);
                var maxX = Mathf.Max(pts[0].X, pts[^1].X);
                var minY = Mathf.Min(pts[0].Y, pts[^1].Y);
                var maxY = Mathf.Max(pts[0].Y, pts[^1].Y);

                if (maxX < visibleRect.Position.X || minX > visibleRect.End.X ||
                    maxY < visibleRect.Position.Y || minY > visibleRect.End.Y)
                {
                    continue;
                }

                item.DrawPolyline(pts, color, lineWidth, true);
            }
            return;
        }

        // Fallback: 若未构建拓扑边，按邻接画线
        for (var i = 0; i < geom.Count; i++)
        {
            var isLand = fields.Height[i] >= seaLevel;
            var start = geom.CellNeighborStart[i];
            var end = geom.CellNeighborStart[i + 1];
            var cx = (float)geom.CentroidX[i];
            var cy = (float)geom.CentroidY[i];
            if (!visibleRect.HasPoint(new Vector2(cx, cy))) continue;

            for (var k = start; k < end; k++)
            {
                var j = geom.CellNeighbors[k];
                if (j <= i) continue;

                var neighborLand = fields.Height[j] >= seaLevel;
                if (isLand != neighborLand)
                {
                    var nx = (float)geom.CentroidX[j];
                    var ny = (float)geom.CentroidY[j];
                    if (Math.Abs(cx - nx) > geom.Width * 0.5f) continue;

                    var mid = (new Vector2(cx, cy) + new Vector2(nx, ny)) * 0.5f;
                    var normal = (new Vector2(nx, ny) - new Vector2(cx, cy)).Orthogonal().Normalized();
                    var span = (float)Math.Min(geom.SpacingX, geom.SpacingY) * 0.45f;
                    item.DrawLine(mid - (normal * span), mid + (normal * span), color, lineWidth, true);
                }
            }
        }
    }

    private static void DrawBorders(
        CanvasItem item,
        WorldSnapshot snapshot,
        float alpha,
        float widthScale,
        Rect2 visibleRect,
        float screenScale,
        RenderEdge[]? cachedEdges = null)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var color = new Color(BorderColor.R, BorderColor.G, BorderColor.B, alpha * 0.95f);
        var lineWidth = Mathf.Clamp(1.8f * widthScale, 0.8f, 6.0f) / screenScale;

        if (cachedEdges != null && cachedEdges.Length > 0)
        {
            for (var i = 0; i < cachedEdges.Length; i++)
            {
                var edge = cachedEdges[i];
                var a = edge.CellA;
                var b = edge.CellB;
                if (a < 0 && b < 0) continue;

                var polA = a >= 0 ? fields.PolityId[a] : -1;
                var polB = b >= 0 ? fields.PolityId[b] : -1;

                // 同一政体内部不画边界；同为无政体荒野不画边界
                if (polA == polB) continue;
                if (polA < 0 && polB < 0) continue;

                var pts = edge.Points;
                if (pts.Length < 2) continue;

                var minX = Mathf.Min(pts[0].X, pts[^1].X);
                var maxX = Mathf.Max(pts[0].X, pts[^1].X);
                var minY = Mathf.Min(pts[0].Y, pts[^1].Y);
                var maxY = Mathf.Max(pts[0].Y, pts[^1].Y);

                if (maxX < visibleRect.Position.X || minX > visibleRect.End.X ||
                    maxY < visibleRect.Position.Y || minY > visibleRect.End.Y)
                {
                    continue;
                }

                item.DrawPolyline(pts, color, lineWidth, true);
            }
            return;
        }

        // Fallback: 修正面向荒野的国界判定
        for (var i = 0; i < geom.Count; i++)
        {
            var polityI = fields.PolityId[i];
            var start = geom.CellNeighborStart[i];
            var end = geom.CellNeighborStart[i + 1];
            var cx = (float)geom.CentroidX[i];
            var cy = (float)geom.CentroidY[i];

            for (var k = start; k < end; k++)
            {
                var j = geom.CellNeighbors[k];
                if (j <= i) continue;

                var polityJ = fields.PolityId[j];
                if (polityI == polityJ) continue;
                if (polityI < 0 && polityJ < 0) continue;

                var nx = (float)geom.CentroidX[j];
                var ny = (float)geom.CentroidY[j];
                if (Math.Abs(cx - nx) > geom.Width * 0.5f) continue;

                var mid = (new Vector2(cx, cy) + new Vector2(nx, ny)) * 0.5f;
                var normal = (new Vector2(nx, ny) - new Vector2(cx, cy)).Orthogonal().Normalized();
                var span = (float)Math.Min(geom.SpacingX, geom.SpacingY) * 0.48f;
                item.DrawLine(mid - (normal * span), mid + (normal * span), color, lineWidth, true);
            }
        }
    }

    private static void DrawCities(CanvasItem item, WorldSnapshot snapshot, float alpha, Font? font, Rect2 visibleRect, float screenScale)
    {
        var settlements = snapshot.Settlements;
        if (settlements.Count == 0) return;

        var capitalColor = new Color(1f, 0.85f, 0.2f, alpha);
        var majorColor = new Color(0.95f, 0.95f, 0.98f, alpha);
        var townColor = new Color(0.85f, 0.85f, 0.88f, alpha * 0.85f);
        var textColor = new Color(1f, 1f, 1f, alpha);
        var shadowColor = new Color(0f, 0f, 0f, alpha * 0.75f);

        foreach (var s in settlements)
        {
            var pos = new Vector2((float)s.Position.X, (float)s.Position.Y);
            if (!visibleRect.HasPoint(pos)) continue;

            var (baseRadius, color) = s.Rank switch
            {
                SettlementRank.CityState => (6.5f, capitalColor),
                SettlementRank.Town => (5.0f, majorColor),
                _ => (3.8f, townColor)
            };

            var radius = baseRadius / screenScale;
            var innerRadius = radius * 0.52f;
            var stroke = 1.0f / screenScale;

            // 绘制外圈与中心实心点
            item.DrawCircle(pos, radius, Colors.Black * 0.85f);
            item.DrawCircle(pos, Mathf.Max(radius - stroke, 0.5f), color);
            item.DrawCircle(pos, innerRadius, Colors.Black * 0.75f);

            // 绘制城市名称文本
            if (font != null && !string.IsNullOrEmpty(s.Name))
            {
                var fontSize = Mathf.Clamp((int)(11f / screenScale), 8, 16);
                var textPos = pos + new Vector2(radius + (4f / screenScale), 4f / screenScale);
                item.DrawString(font, textPos + (Vector2.One / screenScale), s.Name, HorizontalAlignment.Left, -1, fontSize, shadowColor);
                item.DrawString(font, textPos, s.Name, HorizontalAlignment.Left, -1, fontSize, textColor);
            }
        }
    }

    private static void DrawCityLabelsOnly(CanvasItem item, WorldSnapshot snapshot, float alpha, Font font, Rect2 visibleRect, float screenScale)
    {
        var settlements = snapshot.Settlements;
        if (settlements.Count == 0) return;

        var textColor = new Color(1f, 1f, 1f, alpha);
        var shadowColor = new Color(0f, 0f, 0f, alpha * 0.75f);

        foreach (var s in settlements)
        {
            var pos = new Vector2((float)s.Position.X, (float)s.Position.Y);
            if (!visibleRect.HasPoint(pos)) continue;

            if (!string.IsNullOrEmpty(s.Name))
            {
                var fontSize = Mathf.Clamp((int)(11f / screenScale), 8, 16);
                var textPos = pos + new Vector2(8f / screenScale, 4f / screenScale);
                item.DrawString(font, textPos + (Vector2.One / screenScale), s.Name, HorizontalAlignment.Left, -1, fontSize, shadowColor);
                item.DrawString(font, textPos, s.Name, HorizontalAlignment.Left, -1, fontSize, textColor);
            }
        }
    }

    /// <summary>
    /// 绘制贸易走廊：
    /// 无论地图放大还是缩小，均保持为屏幕空间 1.5px 左右的优美平滑发光金线。
    /// 彻底消除巨型方块重叠与生硬直角。
    /// </summary>
    private static void DrawTradeRoutes(
        CanvasItem item,
        WorldSnapshot snapshot,
        float alpha,
        float widthScale,
        Rect2 visibleRect,
        float screenScale)
    {
        var geom = snapshot.Geometry;
        var width = (float)geom.Width;

        var basePx = Mathf.Clamp(1.5f * widthScale, 0.7f, 4.0f);
        var lineWidth = basePx / screenScale;
        var glowWidth = lineWidth * 2.2f;

        var glowColor = new Color(TradeRouteGlowColor.R, TradeRouteGlowColor.G, TradeRouteGlowColor.B, alpha * 0.38f);
        var coreColor = new Color(TradeRouteCoreColor.R, TradeRouteCoreColor.G, TradeRouteCoreColor.B, alpha * 0.95f);

        // 1. 优先消费快照中记录的结构化路线
        var routes = snapshot.Civilization?.Routes;
        if (routes != null && routes.Count > 0)
        {
            foreach (var route in routes)
            {
                if (route.Cells == null || route.Cells.Length < 2) continue;

                var controlPoints = ExtractRouteControlPoints(snapshot, route.Cells);
                DrawSmoothRouteLine(item, controlPoints, width, glowColor, coreColor, glowWidth, lineWidth, visibleRect);
            }
            return;
        }

        // 2. Fallback: 从 TradeRouteMask 重建路径链，并平滑渲染
        var chains = ExtractChainsFromMask(snapshot);
        foreach (var chain in chains)
        {
            if (chain.Length < 2) continue;
            var controlPoints = ExtractRouteControlPoints(snapshot, chain);
            DrawSmoothRouteLine(item, controlPoints, width, glowColor, coreColor, glowWidth, lineWidth, visibleRect);
        }
    }

    private static List<Vector2> ExtractRouteControlPoints(WorldSnapshot snapshot, int[] cells)
    {
        var geom = snapshot.Geometry;
        var pts = new List<Vector2>(cells.Length);

        // 建立聚落地块映射表，让路线端点直接平滑对齐城市点
        var settlementPositions = new Dictionary<int, Vector2>();
        foreach (var s in snapshot.Settlements)
        {
            settlementPositions[s.CellId] = new Vector2((float)s.Position.X, (float)s.Position.Y);
        }

        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            if (cell < 0 || cell >= geom.Count) continue;

            if ((i == 0 || i == cells.Length - 1) && settlementPositions.TryGetValue(cell, out var sPos))
            {
                pts.Add(sPos);
            }
            else
            {
                pts.Add(new Vector2((float)geom.CentroidX[cell], (float)geom.CentroidY[cell]));
            }
        }

        return pts;
    }

    private static void DrawSmoothRouteLine(
        CanvasItem item,
        List<Vector2> controlPoints,
        float width,
        Color glowColor,
        Color coreColor,
        float glowWidth,
        float lineWidth,
        Rect2 visibleRect)
    {
        if (controlPoints.Count < 2) return;

        // 经度缝相位解包 (Unwrap)
        var unwrapped = new List<Vector2>(controlPoints.Count);
        unwrapped.Add(controlPoints[0]);
        var shiftX = 0f;

        for (var i = 1; i < controlPoints.Count; i++)
        {
            var cur = controlPoints[i];
            var prev = controlPoints[i - 1];
            var dx = cur.X - prev.X;

            if (dx > width * 0.5f)
            {
                shiftX -= width;
            }
            else if (dx < -width * 0.5f)
            {
                shiftX += width;
            }

            unwrapped.Add(new Vector2(cur.X + shiftX, cur.Y));
        }

        // Chaikin 切角平滑 (2 轮平滑，生成流畅二次 B 样条般曲线)
        var smoothed = SmoothPolylineChaikin(unwrapped, 2);

        // 分割并环绕回 [0, width)
        var segments = SplitAndWrapPolyline(smoothed, width);

        foreach (var seg in segments)
        {
            if (seg.Length < 2) continue;

            // 视口包围盒裁剪
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            for (var k = 0; k < seg.Length; k++)
            {
                var p = seg[k];
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }

            var segRect = new Rect2(minX, minY, maxX - minX, maxY - minY);
            if (!visibleRect.Intersects(segRect) && !visibleRect.HasPoint(seg[0]))
            {
                continue;
            }

            // 双层抗锯齿发光金线
            item.DrawPolyline(seg, glowColor, glowWidth, true);
            item.DrawPolyline(seg, coreColor, lineWidth, true);
        }
    }

    private static List<Vector2> SmoothPolylineChaikin(IReadOnlyList<Vector2> points, int iterations)
    {
        if (points.Count < 3 || iterations <= 0)
        {
            return new List<Vector2>(points);
        }

        var current = new List<Vector2>(points);
        for (var it = 0; it < iterations; it++)
        {
            var next = new List<Vector2>(current.Count * 2);
            next.Add(current[0]);

            for (var i = 0; i < current.Count - 1; i++)
            {
                var p0 = current[i];
                var p1 = current[i + 1];

                var q = (p0 * 0.75f) + (p1 * 0.25f);
                var r = (p0 * 0.25f) + (p1 * 0.75f);
                next.Add(q);
                next.Add(r);
            }

            next.Add(current[^1]);
            current = next;
        }

        return current;
    }

    private static List<Vector2[]> SplitAndWrapPolyline(List<Vector2> points, float width)
    {
        var segments = new List<Vector2[]>();
        if (points.Count < 2) return segments;

        var currentSegment = new List<Vector2>();
        for (var i = 0; i < points.Count; i++)
        {
            var pt = points[i];
            var shift = Mathf.Floor(pt.X / width) * width;
            var wrappedPt = new Vector2(pt.X - shift, pt.Y);

            if (currentSegment.Count > 0)
            {
                var prevPt = currentSegment[^1];
                if (Mathf.Abs(wrappedPt.X - prevPt.X) > width * 0.5f)
                {
                    if (currentSegment.Count >= 2)
                    {
                        segments.Add(currentSegment.ToArray());
                    }
                    currentSegment.Clear();
                }
            }

            currentSegment.Add(wrappedPt);
        }

        if (currentSegment.Count >= 2)
        {
            segments.Add(currentSegment.ToArray());
        }

        return segments;
    }

    private static List<int[]> ExtractChainsFromMask(WorldSnapshot snapshot)
    {
        var geom = snapshot.Geometry;
        var mask = snapshot.Fields.TradeRouteMask;
        var count = geom.Count;

        var adj = new Dictionary<int, List<int>>();
        for (var i = 0; i < count; i++)
        {
            if (!mask[i]) continue;
            var start = geom.CellNeighborStart[i];
            var end = geom.CellNeighborStart[i + 1];

            for (var k = start; k < end; k++)
            {
                var j = geom.CellNeighbors[k];
                if (mask[j])
                {
                    if (!adj.TryGetValue(i, out var listI))
                    {
                        listI = new List<int>(4);
                        adj[i] = listI;
                    }
                    listI.Add(j);
                }
            }
        }

        var visitedEdges = new HashSet<long>();
        var chains = new List<int[]>();

        // 优先从度数为 1 的端点出发构建链
        foreach (var startNode in adj.Keys)
        {
            if (adj[startNode].Count != 1) continue;

            foreach (var next in adj[startNode])
            {
                var edgeKey = BuildEdgeKey(startNode, next);
                if (visitedEdges.Contains(edgeKey)) continue;

                var chain = TraceChain(startNode, next, adj, visitedEdges);
                if (chain.Count >= 2)
                {
                    chains.Add(chain.ToArray());
                }
            }
        }

        // 处理闭合环或多分支链
        foreach (var startNode in adj.Keys)
        {
            foreach (var next in adj[startNode])
            {
                var edgeKey = BuildEdgeKey(startNode, next);
                if (visitedEdges.Contains(edgeKey)) continue;

                var chain = TraceChain(startNode, next, adj, visitedEdges);
                if (chain.Count >= 2)
                {
                    chains.Add(chain.ToArray());
                }
            }
        }

        return chains;
    }

    private static long BuildEdgeKey(int a, int b) => a < b ? (((long)a << 32) | (uint)b) : (((long)b << 32) | (uint)a);

    private static List<int> TraceChain(int start, int next, Dictionary<int, List<int>> adj, HashSet<long> visitedEdges)
    {
        var chain = new List<int> { start, next };
        visitedEdges.Add(BuildEdgeKey(start, next));

        var prev = start;
        var curr = next;

        while (true)
        {
            if (!adj.TryGetValue(curr, out var neighbors)) break;

            int nextCandidate = -1;
            foreach (var nb in neighbors)
            {
                if (nb == prev) continue;
                var eKey = BuildEdgeKey(curr, nb);
                if (!visitedEdges.Contains(eKey))
                {
                    nextCandidate = nb;
                    break;
                }
            }

            if (nextCandidate < 0) break;

            visitedEdges.Add(BuildEdgeKey(curr, nextCandidate));
            chain.Add(nextCandidate);
            prev = curr;
            curr = nextCandidate;
        }

        return chain;
    }

    /// <summary>
    /// 绘制地块线框：
    /// 去重共享边，抗锯齿绘制规范平滑曲线边；
    /// 线宽保持屏幕恒定 0.95px 纤细线条。
    /// </summary>
    private static void DrawCellOutlines(
        CanvasItem item,
        WorldSnapshot snapshot,
        float alpha,
        float widthScale,
        Rect2 visibleRect,
        float screenScale,
        Vector2[][]? cachedCurvedEdges = null)
    {
        var geom = snapshot.Geometry;
        var color = new Color(CellOutlineColor.R, CellOutlineColor.G, CellOutlineColor.B, alpha * CellOutlineColor.A);

        var targetPx = Mathf.Clamp(0.95f * widthScale, 0.4f, 3.0f);
        var lineWidth = targetPx / screenScale;

        if (cachedCurvedEdges != null)
        {
            for (var i = 0; i < cachedCurvedEdges.Length; i++)
            {
                var edge = cachedCurvedEdges[i];
                if (edge.Length < 2) continue;

                var p0 = edge[0];
                var p1 = edge[^1];
                var minX = Mathf.Min(p0.X, p1.X);
                var maxX = Mathf.Max(p0.X, p1.X);
                var minY = Mathf.Min(p0.Y, p1.Y);
                var maxY = Mathf.Max(p0.Y, p1.Y);

                if (maxX < visibleRect.Position.X || minX > visibleRect.End.X ||
                    maxY < visibleRect.Position.Y || minY > visibleRect.End.Y)
                {
                    continue;
                }

                item.DrawPolyline(edge, color, lineWidth, true);
            }
            return;
        }

        var rawEdges = CurvedCellGeometry.GetCanonicalCurvedEdges(geom, 3);
        var width = (float)geom.Width;

        for (var i = 0; i < rawEdges.Length; i++)
        {
            var raw = rawEdges[i];
            if (raw.Length < 2) continue;

            var p0 = new Vector2((float)raw[0].X, (float)raw[0].Y);
            var p1 = new Vector2((float)raw[^1].X, (float)raw[^1].Y);
            var minX = Mathf.Min(p0.X, p1.X);
            var maxX = Mathf.Max(p0.X, p1.X);
            var minY = Mathf.Min(p0.Y, p1.Y);
            var maxY = Mathf.Max(p0.Y, p1.Y);

            if (maxX < visibleRect.Position.X || minX > visibleRect.End.X ||
                maxY < visibleRect.Position.Y || minY > visibleRect.End.Y)
            {
                continue;
            }

            var pts = new Vector2[raw.Length];
            var cross = false;
            for (var k = 0; k < raw.Length; k++)
            {
                pts[k] = new Vector2((float)raw[k].X, (float)raw[k].Y);
                if (k > 0 && Math.Abs(pts[k].X - pts[k - 1].X) > width * 0.5f)
                {
                    cross = true;
                    break;
                }
            }

            if (cross) continue;
            item.DrawPolyline(pts, color, lineWidth, true);
        }
    }

    private static void DrawWindArrows(
        CanvasItem item,
        WorldSnapshot snapshot,
        float alpha,
        float widthScale,
        Rect2 visibleRect,
        float screenScale,
        Font? font = null)
    {
        var geom = snapshot.Geometry;
        var width = (float)geom.Width;
        var height = (float)geom.Height;
        if (width <= 0f || height <= 0f) return;

        // 经典气象绿 (Meteorological Green: #007a24)
        var arrowColor = new Color(0.0f, 0.48f, 0.14f, alpha * 0.95f);
        var shadowColor = new Color(0.95f, 0.98f, 0.95f, alpha * 0.85f);
        var textColor = new Color(0.05f, 0.20f, 0.08f, alpha * 0.95f);

        var userScale = Mathf.Clamp(widthScale, 0.4f, 2.5f);
        var targetScreenLineWidth = Mathf.Clamp(1.05f * userScale, 0.6f, 2.0f);
        var lineWidth = targetScreenLineWidth / screenScale;

        // 1. 经纬度规则网格稀疏度计算（大幅提升开阔度，避免重叠粘连）
        // 默认全球约 28~32 列 x 14~16 行，晶格单元留白约 50%
        var baseSpacing = 70f / userScale;
        var cols = Mathf.Clamp(Mathf.RoundToInt(width / baseSpacing), 16, 44);
        var rows = Mathf.Clamp(Mathf.RoundToInt(height / baseSpacing), 8, 22);
        var stepX = width / cols;
        var stepY = height / rows;
        var refLength = Mathf.Min(stepX, stepY) * 0.50f;

        const float RefSpeed = 10.0f; // 10 m/s 气象标尺基准速度
        const float BarbAngle = 0.50f; // ~28.6° 气象经典倒钩张角

        var continuous = snapshot.ContinuousWind;
        var contW = continuous?.GetLength(0) ?? 0;
        var contH = continuous?.GetLength(1) ?? 0;

        // 2. 遍历网格点绘制风场矢量
        for (var r = 0; r < rows; r++)
        {
            var cy = (r + 0.5f) * stepY;
            if (cy < visibleRect.Position.Y - refLength || cy > visibleRect.End.Y + refLength)
            {
                continue;
            }

            var vNorm = cy / height;

            for (var c = 0; c < cols; c++)
            {
                var cx = (c + 0.5f) * stepX;
                if (cx < visibleRect.Position.X - refLength || cx > visibleRect.End.X + refLength)
                {
                    continue;
                }

                float vx, vy;

                if (continuous != null && contW > 0 && contH > 0)
                {
                    // 连续场双线性平滑插值 (经度横向无缝循环)
                    var uNorm = cx / width;
                    var fx = uNorm * contW;
                    var fy = vNorm * contH;
                    var x0 = (int)Mathf.Floor(fx);
                    var y0 = (int)Mathf.Floor(fy);
                    var tx = fx - x0;
                    var ty = fy - y0;

                    var x0w = ((x0 % contW) + contW) % contW;
                    var x1w = (((x0 + 1) % contW) + contW) % contW;
                    var y0c = Math.Clamp(y0, 0, contH - 1);
                    var y1c = Math.Clamp(y0 + 1, 0, contH - 1);

                    var v00 = continuous[x0w, y0c];
                    var v10 = continuous[x1w, y0c];
                    var v01 = continuous[x0w, y1c];
                    var v11 = continuous[x1w, y1c];

                    var topX = (v00.X * (1f - tx)) + (v10.X * tx);
                    var bottomX = (v01.X * (1f - tx)) + (v11.X * tx);
                    var topY = (v00.Y * (1f - tx)) + (v10.Y * tx);
                    var bottomY = (v01.Y * (1f - tx)) + (v11.Y * tx);

                    vx = (topX * (1f - ty)) + (bottomX * ty);
                    vy = (topY * (1f - ty)) + (bottomY * ty);
                }
                else
                {
                    // 回退：查询最近地块属性
                    var cellId = geom.FindCell(cx, cy);
                    if (cellId >= 0 && cellId < snapshot.Fields.Count && snapshot.Fields.WindX.Length > cellId)
                    {
                        vx = snapshot.Fields.WindX[cellId];
                        vy = snapshot.Fields.WindY[cellId];
                    }
                    else
                    {
                        var latNorm = (cy - (height * 0.5f)) / (height * 0.5f);
                        vx = latNorm > 0.3f || latNorm < -0.3f ? 12f : -12f;
                        vy = 0f;
                    }
                }

                var speed = Mathf.Sqrt((vx * vx) + (vy * vy));
                var pt = new Vector2(cx, cy);

                if (speed < 0.35f)
                {
                    // 静风/微风区：点绘纤细标记
                    item.DrawCircle(pt, 0.85f / screenScale, arrowColor);
                    continue;
                }

                var dir = new Vector2(vx, vy) / speed;
                var ratio = Mathf.Clamp(speed / RefSpeed, 0.10f, 1.45f);
                var arrowLength = refLength * ratio;
                var tip = pt + (dir * arrowLength);

                // 细线条箭杆（屏幕恒定 1.05px 纤细线条）
                item.DrawLine(pt, tip, arrowColor, lineWidth, true);

                // 经典气象 V 型回钩箭头（屏幕自适应 3.2~7.0px 锐利倒钩）
                var headLen = Mathf.Clamp(arrowLength * 0.28f, 3.2f / screenScale, 7.0f / screenScale);
                var barbLeft = tip - (dir.Rotated(BarbAngle) * headLen);
                var barbRight = tip - (dir.Rotated(-BarbAngle) * headLen);

                item.DrawLine(tip, barbLeft, arrowColor, lineWidth, true);
                item.DrawLine(tip, barbRight, arrowColor, lineWidth, true);
            }
        }

        // 3. 右上角气象风标标尺 (→ 10 m/s)
        DrawWindScaleLegend(item, geom, arrowColor, textColor, shadowColor, refLength, lineWidth, screenScale, font);

        // 4. 经纬度刻度边框 (Graticule Neatline Frame)
        DrawGraticuleFrame(item, geom, screenScale, font);
    }

    private static void DrawWindScaleLegend(
        CanvasItem item,
        CellGeometry geom,
        Color arrowColor,
        Color textColor,
        Color shadowColor,
        float refLength,
        float lineWidth,
        float screenScale,
        Font? font)
    {
        var padX = 24f / screenScale;
        var padY = 18f / screenScale;
        var scaleShaftLen = Mathf.Max(refLength, 28f / screenScale);

        var refStartX = (float)geom.Width - padX - scaleShaftLen - (65f / screenScale);
        var refY = padY + (8f / screenScale);
        var refEndX = refStartX + scaleShaftLen;

        var startPt = new Vector2(refStartX, refY);
        var endPt = new Vector2(refEndX, refY);
        var dir = Vector2.Right;

        // 标尺微发光半透明底色，确保任何底图下均清晰可见
        var bgRect = new Rect2(refStartX - (8f / screenScale), padY - (4f / screenScale), scaleShaftLen + (76f / screenScale), 24f / screenScale);
        item.DrawRect(bgRect, new Color(1f, 1f, 1f, 0.78f), true);
        item.DrawRect(bgRect, new Color(0.1f, 0.1f, 0.1f, 0.35f), false, 1.0f / screenScale);

        // 标尺风矢
        item.DrawLine(startPt, endPt, arrowColor, lineWidth * 1.15f, true);
        var headLen = Mathf.Clamp(scaleShaftLen * 0.32f, 5.0f / screenScale, 10.0f / screenScale);
        var barbLeft = endPt - (dir.Rotated(0.50f) * headLen);
        var barbRight = endPt - (dir.Rotated(-0.50f) * headLen);
        item.DrawLine(endPt, barbLeft, arrowColor, lineWidth * 1.15f, true);
        item.DrawLine(endPt, barbRight, arrowColor, lineWidth * 1.15f, true);

        // 标尺文本 "10 m/s"
        if (font != null)
        {
            var textPos = new Vector2(refEndX + (8f / screenScale), refY + (4f / screenScale));
            var fontSize = Mathf.Clamp((int)(12f / screenScale), 9, 18);
            item.DrawString(font, textPos + (Vector2.One / screenScale), "10 m/s", HorizontalAlignment.Left, -1, fontSize, shadowColor);
            item.DrawString(font, textPos, "10 m/s", HorizontalAlignment.Left, -1, fontSize, textColor);
        }
    }

    private static void DrawGraticuleFrame(
        CanvasItem item,
        CellGeometry geom,
        float screenScale,
        Font? font)
    {
        var width = (float)geom.Width;
        var height = (float)geom.Height;
        var frameColor = Color.FromHtml("#111111");
        var tickColor = Color.FromHtml("#222222");
        var textColor = Color.FromHtml("#222222");
        var shadowColor = new Color(1f, 1f, 1f, 0.85f);
        var frameWidth = 1.6f / screenScale;
        var tickLen = 6.0f / screenScale;
        var fontSize = Mathf.Clamp((int)(10f / screenScale), 8, 14);

        // 外围整饰框
        item.DrawRect(new Rect2(0, 0, width, height), frameColor, false, frameWidth);

        // 经度刻度（底边与顶边：每 30° 一个主刻度，全图 360° 共 12 格）
        var lonLabels = new[] { "180°", "150°W", "120°W", "90°W", "60°W", "30°W", "0°", "30°E", "60°E", "90°E", "120°E", "150°E", "180°" };
        var lonSteps = lonLabels.Length - 1;
        for (var i = 0; i <= lonSteps; i++)
        {
            var x = (width / lonSteps) * i;
            // 顶边内向刻度
            item.DrawLine(new Vector2(x, 0), new Vector2(x, tickLen), tickColor, 1.2f / screenScale);
            // 底边内向刻度
            item.DrawLine(new Vector2(x, height), new Vector2(x, height - tickLen), tickColor, 1.2f / screenScale);

            if (font != null && i > 0 && i < lonSteps && (i % 2 == 0))
            {
                var label = lonLabels[i];
                var textPos = new Vector2(x - (14f / screenScale), height - tickLen - (3f / screenScale));
                item.DrawString(font, textPos + (Vector2.One / screenScale), label, HorizontalAlignment.Center, -1, fontSize, shadowColor);
                item.DrawString(font, textPos, label, HorizontalAlignment.Center, -1, fontSize, textColor);
            }
        }

        // 纬度刻度（左侧与右侧：每 30° 一个主刻度，全图 180° 共 6 格：90°N ~ 90°S）
        var latLabels = new[] { "90°N", "60°N", "30°N", "0°", "30°S", "60°S", "90°S" };
        var latSteps = latLabels.Length - 1;
        for (var j = 0; j <= latSteps; j++)
        {
            var y = (height / latSteps) * j;
            // 左边内向刻度
            item.DrawLine(new Vector2(0, y), new Vector2(tickLen, y), tickColor, 1.2f / screenScale);
            // 右边内向刻度
            item.DrawLine(new Vector2(width, y), new Vector2(width - tickLen, y), tickColor, 1.2f / screenScale);

            if (font != null && j > 0 && j < latSteps)
            {
                var label = latLabels[j];
                var textPos = new Vector2(tickLen + (4f / screenScale), y + (4f / screenScale));
                item.DrawString(font, textPos + (Vector2.One / screenScale), label, HorizontalAlignment.Left, -1, fontSize, shadowColor);
                item.DrawString(font, textPos, label, HorizontalAlignment.Left, -1, fontSize, textColor);
            }
        }
    }

    private static void DrawPlateBoundaries(
        CanvasItem item,
        WorldSnapshot snapshot,
        float alpha,
        float widthScale,
        Rect2 visibleRect,
        float screenScale,
        PlateBoundarySegment[]? cachedSegments = null,
        RenderEdge[]? cachedEdges = null)
    {
        var segments = cachedSegments ?? BuildPlateBoundaries(snapshot, cachedEdges);
        if (segments.Length == 0) return;

        var basePx = Mathf.Clamp(1.8f * widthScale, 1.0f, 5.0f);
        var lineWidth = basePx / screenScale;
        var glowWidth = lineWidth * 2.2f;

        for (var i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            if (seg.Points.Length < 2) continue;

            // 视口包围盒裁剪
            if (!visibleRect.Intersects(seg.BoundingBox) && !visibleRect.HasPoint(seg.Points[0]))
            {
                continue;
            }

            if (seg.IsUniformColor)
            {
                var baseCol = seg.Colors[0];
                var glowCol = new Color(baseCol.R, baseCol.G, baseCol.B, alpha * 0.32f);
                var coreCol = new Color(baseCol.R, baseCol.G, baseCol.B, alpha * 0.95f);

                item.DrawPolyline(seg.Points, glowCol, glowWidth, true);
                item.DrawPolyline(seg.Points, coreCol, lineWidth, true);
            }
            else
            {
                var glowCols = new Color[seg.Colors.Length];
                var coreCols = new Color[seg.Colors.Length];
                for (var k = 0; k < seg.Colors.Length; k++)
                {
                    var c = seg.Colors[k];
                    glowCols[k] = new Color(c.R, c.G, c.B, alpha * 0.32f);
                    coreCols[k] = new Color(c.R, c.G, c.B, alpha * 0.95f);
                }

                item.DrawPolylineColors(seg.Points, glowCols, glowWidth, true);
                item.DrawPolylineColors(seg.Points, coreCols, lineWidth, true);
            }
        }
    }

    /// <summary>
    /// 构建并预处理平滑板块边界矢量段：
    /// 提取宏观连贯断裂带链条、消除微观多边形折角（拉普拉斯松弛 + 2轮 Chaikin 二次样条平滑）、
    /// 边界类型颜色平滑渐变过渡、经度跨缝无缝相位解包与切分。
    /// </summary>
    public static PlateBoundarySegment[] BuildPlateBoundaries(
        WorldSnapshot snapshot,
        RenderEdge[]? cachedEdges = null)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var width = (float)geom.Width;

        // 若未提供拓扑渲染边，从核心几何自动获取规范边
        if (cachedEdges == null || cachedEdges.Length == 0)
        {
            var rawCanonical = CurvedCellGeometry.GetCanonicalCurvedEdgesWithTopology(geom, 3);
            var converted = new RenderEdge[rawCanonical.Length];
            for (var i = 0; i < rawCanonical.Length; i++)
            {
                var cPts = rawCanonical[i].Points;
                var vpts = new Vector2[cPts.Length];
                for (var k = 0; k < cPts.Length; k++)
                {
                    vpts[k] = new Vector2((float)cPts[k].X, (float)cPts[k].Y);
                }
                converted[i] = new RenderEdge
                {
                    Points = vpts,
                    CellA = rawCanonical[i].CellA,
                    CellB = rawCanonical[i].CellB
                };
            }
            cachedEdges = converted;
        }

        // 1. 提取全图所有属于板块交界的唯一边
        var rawPlateEdges = new List<RenderEdge>(cachedEdges.Length / 6);
        for (var i = 0; i < cachedEdges.Length; i++)
        {
            var e = cachedEdges[i];
            var a = e.CellA;
            var b = e.CellB;
            if (a < 0 || b < 0) continue;
            if (fields.PlateId[a] == fields.PlateId[b]) continue;

            // 过滤经度缝镜像拷贝（只保留主窗口 [0, width) 内的规范主边，后续统一经度解包平滑）
            var midX = (e.Points[0].X + e.Points[^1].X) * 0.5f;
            if (midX < 0f || midX >= width) continue;

            rawPlateEdges.Add(e);
        }

        if (rawPlateEdges.Count == 0)
        {
            return Array.Empty<PlateBoundarySegment>();
        }

        // 2. 空间哈希拓扑合并微观 Voronoi 顶点，建立交点拓扑图
        var reg = new PlateVertexRegistry(width, Math.Max((float)geom.SpacingX * 0.25f, 0.5f));
        var edges = new List<RegisteredPlateEdge>(rawPlateEdges.Count);
        var adj = new Dictionary<int, List<int>>(rawPlateEdges.Count * 2);

        for (var i = 0; i < rawPlateEdges.Count; i++)
        {
            var e = rawPlateEdges[i];
            var v0 = reg.GetOrCreateVertex(e.Points[0]);
            var v1 = reg.GetOrCreateVertex(e.Points[^1]);
            if (v0 == v1) continue;

            var color = GetPlateBoundaryColor(e.CellA, e.CellB, fields.PlateBoundary);
            var regEdge = new RegisteredPlateEdge(v0, v1, color, e.Points);
            var eIdx = edges.Count;
            edges.Add(regEdge);

            if (!adj.TryGetValue(v0, out var list0))
            {
                list0 = new List<int>(4);
                adj[v0] = list0;
            }
            list0.Add(eIdx);

            if (!adj.TryGetValue(v1, out var list1))
            {
                list1 = new List<int>(4);
                adj[v1] = list1;
            }
            list1.Add(eIdx);
        }

        // 3. 追踪连贯的板块断裂带链（Chains）
        var visitedEdges = new bool[edges.Count];
        var rawChains = new List<List<int>>();

        // 3a. 优先从三联点（度数 != 2）或端点出发
        foreach (var (nodeId, edgeIndices) in adj)
        {
            if (edgeIndices.Count == 2) continue;

            for (var k = 0; k < edgeIndices.Count; k++)
            {
                var eIdx = edgeIndices[k];
                if (visitedEdges[eIdx]) continue;

                var chain = TracePlateEdgeChain(nodeId, eIdx, adj, edges, visitedEdges);
                if (chain.Count > 0)
                {
                    rawChains.Add(chain);
                }
            }
        }

        // 3b. 处理无三联点的闭合孤立环（所有节点度数均为 2）
        for (var eIdx = 0; eIdx < edges.Count; eIdx++)
        {
            if (visitedEdges[eIdx]) continue;
            var startNode = edges[eIdx].NodeA;
            var chain = TracePlateEdgeChain(startNode, eIdx, adj, edges, visitedEdges);
            if (chain.Count > 0)
            {
                rawChains.Add(chain);
            }
        }

        // 4. 针对每条宏观板块链条进行点串拼接、经度解包、颜色渐变平滑与几何样条平滑
        var resultSegments = new List<PlateBoundarySegment>();

        foreach (var chain in rawChains)
        {
            if (chain.Count == 0) continue;

            var chainPts = new List<Vector2>();
            var chainCols = new List<Color>();

            var firstEdge = edges[chain[0]];
            var startNode = firstEdge.NodeA;
            if (chain.Count > 1)
            {
                var secondEdge = edges[chain[1]];
                if (firstEdge.NodeA == secondEdge.NodeA || firstEdge.NodeA == secondEdge.NodeB)
                {
                    startNode = firstEdge.NodeB;
                }
                else
                {
                    startNode = firstEdge.NodeA;
                }
            }

            var currNode = startNode;
            for (var i = 0; i < chain.Count; i++)
            {
                var edge = edges[chain[i]];
                var forward = edge.NodeA == currNode;
                var pts = edge.Points;
                var col = edge.Color;

                var startK = (i == 0) ? 0 : 1;
                if (forward)
                {
                    for (var k = startK; k < pts.Length; k++)
                    {
                        chainPts.Add(pts[k]);
                        chainCols.Add(col);
                    }
                    currNode = edge.NodeB;
                }
                else
                {
                    for (var k = pts.Length - 1 - startK; k >= 0; k--)
                    {
                        chainPts.Add(pts[k]);
                        chainCols.Add(col);
                    }
                    currNode = edge.NodeA;
                }
            }

            if (chainPts.Count < 2) continue;

            // 4a. 经度相位解包 (Unwrap)
            var unwrappedPts = new List<Vector2>(chainPts.Count);
            unwrappedPts.Add(chainPts[0]);
            var shiftX = 0f;
            for (var i = 1; i < chainPts.Count; i++)
            {
                var cur = chainPts[i];
                var prev = chainPts[i - 1];
                var dx = cur.X - prev.X;
                if (dx > width * 0.5f)
                {
                    shiftX -= width;
                }
                else if (dx < -width * 0.5f)
                {
                    shiftX += width;
                }
                unwrappedPts.Add(new Vector2(cur.X + shiftX, cur.Y));
            }

            // 4b. 颜色在交界处的平滑混合过渡 (Blend colors)
            var blendedCols = BlendBoundaryColors(chainCols, 2);

            // 4c. 几何平滑：拉普拉斯轻度滤波消除蜂窝网格折角噪声（端点固定）
            if (unwrappedPts.Count >= 4)
            {
                var lapPts = new List<Vector2>(unwrappedPts.Count);
                var lapCols = new List<Color>(blendedCols.Count);

                lapPts.Add(unwrappedPts[0]);
                lapCols.Add(blendedCols[0]);

                for (var i = 1; i < unwrappedPts.Count - 1; i++)
                {
                    var pPrev = unwrappedPts[i - 1];
                    var pCurr = unwrappedPts[i];
                    var pNext = unwrappedPts[i + 1];
                    lapPts.Add((pCurr * 0.5f) + ((pPrev + pNext) * 0.25f));

                    var cPrev = blendedCols[i - 1];
                    var cCurr = blendedCols[i];
                    var cNext = blendedCols[i + 1];
                    lapCols.Add(new Color(
                        (cCurr.R * 0.5f) + ((cPrev.R + cNext.R) * 0.25f),
                        (cCurr.G * 0.5f) + ((cPrev.G + cNext.G) * 0.25f),
                        (cCurr.B * 0.5f) + ((cPrev.B + cNext.B) * 0.25f),
                        (cCurr.A * 0.5f) + ((cPrev.A + cNext.A) * 0.25f)
                    ));
                }

                lapPts.Add(unwrappedPts[^1]);
                lapCols.Add(blendedCols[^1]);

                unwrappedPts = lapPts;
                blendedCols = lapCols;
            }

            // 4d. Chaikin 切角样条细分（2轮平滑，端点固定，生成二次样条般的优雅曲线）
            var (smoothedPts, smoothedCols) = SmoothPolylineAndColorsChaikin(unwrappedPts, blendedCols, 2);

            // 4e. 重新切分并环绕回 [0, width)
            var segs = SplitAndWrapBoundary(smoothedPts, smoothedCols, width);
            resultSegments.AddRange(segs);
        }

        return resultSegments.ToArray();
    }

    private static List<int> TracePlateEdgeChain(
        int startNode,
        int firstEdgeIdx,
        Dictionary<int, List<int>> adj,
        List<RegisteredPlateEdge> edges,
        bool[] visitedEdges)
    {
        var chain = new List<int>();
        var currEdgeIdx = firstEdgeIdx;
        var currNode = startNode;

        while (true)
        {
            visitedEdges[currEdgeIdx] = true;
            chain.Add(currEdgeIdx);

            var edge = edges[currEdgeIdx];
            var nextNode = edge.NodeA == currNode ? edge.NodeB : edge.NodeA;

            if (!adj.TryGetValue(nextNode, out var neighborEdges) || neighborEdges.Count != 2 || nextNode == startNode)
            {
                break;
            }

            var foundNext = false;
            for (var i = 0; i < neighborEdges.Count; i++)
            {
                var candidate = neighborEdges[i];
                if (!visitedEdges[candidate])
                {
                    currEdgeIdx = candidate;
                    currNode = nextNode;
                    foundNext = true;
                    break;
                }
            }

            if (!foundNext) break;
        }

        return chain;
    }

    private static Color GetPlateBoundaryColor(int a, int b, byte[] plateBoundary)
    {
        var typeA = (PlateBoundaryType)plateBoundary[a];
        var typeB = (PlateBoundaryType)plateBoundary[b];

        var bType = PlateBoundaryType.Transform;
        if (typeA == PlateBoundaryType.Convergent || typeB == PlateBoundaryType.Convergent)
        {
            bType = PlateBoundaryType.Convergent;
        }
        else if (typeA == PlateBoundaryType.Divergent || typeB == PlateBoundaryType.Divergent)
        {
            bType = PlateBoundaryType.Divergent;
        }
        else if (typeA != PlateBoundaryType.None)
        {
            bType = typeA;
        }
        else if (typeB != PlateBoundaryType.None)
        {
            bType = typeB;
        }

        return bType switch
        {
            PlateBoundaryType.Convergent => PlateConvergent,
            PlateBoundaryType.Divergent => PlateDivergent,
            _ => PlateTransform
        };
    }

    private static List<Color> BlendBoundaryColors(List<Color> colors, int passes)
    {
        if (colors.Count < 3 || passes <= 0)
        {
            return new List<Color>(colors);
        }

        var cur = new List<Color>(colors);
        for (var p = 0; p < passes; p++)
        {
            var next = new List<Color>(cur.Count);
            next.Add(cur[0]);
            for (var i = 1; i < cur.Count - 1; i++)
            {
                var c0 = cur[i - 1];
                var c1 = cur[i];
                var c2 = cur[i + 1];
                next.Add(new Color(
                    (c0.R * 0.25f) + (c1.R * 0.5f) + (c2.R * 0.25f),
                    (c0.G * 0.25f) + (c1.G * 0.5f) + (c2.G * 0.25f),
                    (c0.B * 0.25f) + (c1.B * 0.5f) + (c2.B * 0.25f),
                    (c0.A * 0.25f) + (c1.A * 0.5f) + (c2.A * 0.25f)
                ));
            }
            next.Add(cur[^1]);
            cur = next;
        }
        return cur;
    }

    private static (List<Vector2> Points, List<Color> Colors) SmoothPolylineAndColorsChaikin(
        IReadOnlyList<Vector2> points,
        IReadOnlyList<Color> colors,
        int iterations)
    {
        if (points.Count < 3 || iterations <= 0)
        {
            return (new List<Vector2>(points), new List<Color>(colors));
        }

        var curPts = new List<Vector2>(points);
        var curCols = new List<Color>(colors);

        for (var it = 0; it < iterations; it++)
        {
            var nextPts = new List<Vector2>(curPts.Count * 2);
            var nextCols = new List<Color>(curCols.Count * 2);

            nextPts.Add(curPts[0]);
            nextCols.Add(curCols[0]);

            for (var i = 0; i < curPts.Count - 1; i++)
            {
                var p0 = curPts[i];
                var p1 = curPts[i + 1];
                var c0 = curCols[i];
                var c1 = curCols[i + 1];

                var qPt = (p0 * 0.75f) + (p1 * 0.25f);
                var rPt = (p0 * 0.25f) + (p1 * 0.75f);
                nextPts.Add(qPt);
                nextPts.Add(rPt);

                var qCol = new Color(
                    (c0.R * 0.75f) + (c1.R * 0.25f),
                    (c0.G * 0.75f) + (c1.G * 0.25f),
                    (c0.B * 0.75f) + (c1.B * 0.25f),
                    (c0.A * 0.75f) + (c1.A * 0.25f)
                );
                var rCol = new Color(
                    (c0.R * 0.25f) + (c1.R * 0.75f),
                    (c0.G * 0.25f) + (c1.G * 0.75f),
                    (c0.B * 0.25f) + (c1.B * 0.75f),
                    (c0.A * 0.25f) + (c1.A * 0.75f)
                );
                nextCols.Add(qCol);
                nextCols.Add(rCol);
            }

            nextPts.Add(curPts[^1]);
            nextCols.Add(curCols[^1]);

            curPts = nextPts;
            curCols = nextCols;
        }

        return (curPts, curCols);
    }

    private static List<PlateBoundarySegment> SplitAndWrapBoundary(
        List<Vector2> points,
        List<Color> colors,
        float width)
    {
        var segments = new List<PlateBoundarySegment>();
        if (points.Count < 2) return segments;

        var curPts = new List<Vector2>();
        var curCols = new List<Color>();

        for (var i = 0; i < points.Count; i++)
        {
            var pt = points[i];
            var col = colors[i];
            var shift = Mathf.Floor(pt.X / width) * width;
            var wrappedPt = new Vector2(pt.X - shift, pt.Y);

            if (curPts.Count > 0)
            {
                var prevPt = curPts[^1];
                if (Mathf.Abs(wrappedPt.X - prevPt.X) > width * 0.5f)
                {
                    if (curPts.Count >= 2)
                    {
                        segments.Add(CreateBoundarySegment(curPts, curCols));
                    }
                    curPts.Clear();
                    curCols.Clear();
                }
            }

            curPts.Add(wrappedPt);
            curCols.Add(col);
        }

        if (curPts.Count >= 2)
        {
            segments.Add(CreateBoundarySegment(curPts, curCols));
        }

        return segments;
    }

    private static PlateBoundarySegment CreateBoundarySegment(List<Vector2> pts, List<Color> cols)
    {
        var minX = float.MaxValue;
        var maxX = float.MinValue;
        var minY = float.MaxValue;
        var maxY = float.MinValue;

        var isUniform = true;
        var firstCol = cols[0];

        for (var i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;

            if (Math.Abs(cols[i].R - firstCol.R) > 0.01f ||
                Math.Abs(cols[i].G - firstCol.G) > 0.01f ||
                Math.Abs(cols[i].B - firstCol.B) > 0.01f)
            {
                isUniform = false;
            }
        }

        return new PlateBoundarySegment
        {
            Points = pts.ToArray(),
            Colors = cols.ToArray(),
            BoundingBox = new Rect2(minX, minY, maxX - minX, maxY - minY),
            IsUniformColor = isUniform
        };
    }

    private readonly struct RegisteredPlateEdge
    {
        public readonly int NodeA;
        public readonly int NodeB;
        public readonly Color Color;
        public readonly Vector2[] Points;

        public RegisteredPlateEdge(int nodeA, int nodeB, Color color, Vector2[] points)
        {
            NodeA = nodeA;
            NodeB = nodeB;
            Color = color;
            Points = points;
        }
    }

    private sealed class PlateVertexRegistry
    {
        private readonly float _width;
        private readonly float _cellSize;
        private readonly Dictionary<long, List<int>> _grid = new();
        private readonly List<Vector2> _vertices = new();

        public PlateVertexRegistry(float width, float cellSize = 1.0f)
        {
            _width = width;
            _cellSize = cellSize;
        }

        public int GetOrCreateVertex(Vector2 pos)
        {
            var x = pos.X;
            x -= MathF.Floor(x / _width) * _width;
            var y = pos.Y;

            var gx = (int)MathF.Floor(x / _cellSize);
            var gy = (int)MathF.Floor(y / _cellSize);
            var maxGx = Math.Max((int)MathF.Ceiling(_width / _cellSize), 1);

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    var queryGx = gx + dx;
                    if (queryGx < 0) queryGx += maxGx;
                    else if (queryGx >= maxGx) queryGx -= maxGx;

                    var queryGy = gy + dy;
                    var key = ((long)queryGx << 32) | (uint)queryGy;

                    if (_grid.TryGetValue(key, out var list))
                    {
                        for (var i = 0; i < list.Count; i++)
                        {
                            var idx = list[i];
                            var v = _vertices[idx];
                            var diffX = MathF.Abs(v.X - x);
                            if (diffX > _width * 0.5f) diffX = _width - diffX;
                            if (diffX < 0.25f && MathF.Abs(v.Y - y) < 0.25f)
                            {
                                return idx;
                            }
                        }
                    }
                }
            }

            var newIdx = _vertices.Count;
            _vertices.Add(new Vector2(x, y));
            var selfKey = ((long)gx << 32) | (uint)gy;
            if (!_grid.TryGetValue(selfKey, out var selfList))
            {
                selfList = new List<int>(4);
                _grid[selfKey] = selfList;
            }
            selfList.Add(newIdx);
            return newIdx;
        }
    }
}
