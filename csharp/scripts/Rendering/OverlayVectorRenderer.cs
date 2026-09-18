using Godot;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;
using PlanetGeneration.Core.Layers;
using System;
using System.Collections.Generic;

namespace PlanetGeneration.Rendering;

/// <summary>
/// 8 种叠加图层矢量绘制器：
/// 河流、政体边界、海岸线、城市、贸易路线、地块轮廓、风向箭头、板块边界。
/// 支持屏幕像素线宽归一化（无论放大缩小，线条始终保持纤细平滑）。
/// </summary>
public static class OverlayVectorRenderer
{
    private static readonly Color RiverColor = Color.FromHtml("#1a75d2");
    private static readonly Color CoastlineColor = Color.FromHtml("#0b2247");
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
        Vector2[][]? cachedCurvedEdges = null)
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
                case "coastlines":
                    DrawCoastlines(item, snapshot, alpha, widthScale, visibleRect, safeScale);
                    break;
                case "polity_borders" or "borders":
                    DrawBorders(item, snapshot, alpha, widthScale, visibleRect, safeScale);
                    break;
                case "cities" or "city_labels":
                    DrawCities(item, snapshot, alpha, font, visibleRect, safeScale);
                    break;
                case "trade_routes" or "trade_network":
                    DrawTradeRoutes(item, snapshot, alpha, widthScale, visibleRect, safeScale);
                    break;
                case "cell_borders" or "cell_outlines":
                    DrawCellOutlines(item, snapshot, alpha, widthScale, visibleRect, safeScale, cachedCurvedEdges);
                    break;
                case "wind_arrows":
                    DrawWindArrows(item, snapshot, alpha, widthScale, visibleRect, safeScale);
                    break;
                case "plate_borders" or "plate_boundaries":
                    DrawPlateBoundaries(item, snapshot, alpha, widthScale, visibleRect, safeScale);
                    break;
            }
        }
    }

    public static void DrawOverlays(
        CanvasItem item,
        WorldSnapshot snapshot,
        LayerStackState layerStack,
        Rect2 visibleRect,
        Font? font)
        => DrawOverlays(item, snapshot, layerStack, visibleRect, 1.0f, font, null);

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

    private static void DrawCoastlines(CanvasItem item, WorldSnapshot snapshot, float alpha, float widthScale, Rect2 visibleRect, float screenScale)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var seaLevel = snapshot.Options.SeaLevel;
        var color = new Color(CoastlineColor.R, CoastlineColor.G, CoastlineColor.B, alpha * 0.85f);
        var lineWidth = Mathf.Clamp(1.3f * widthScale, 0.6f, 4.0f) / screenScale;

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
                if (j <= i) continue; // 避免重复画

                var neighborLand = fields.Height[j] >= seaLevel;
                if (isLand != neighborLand)
                {
                    var nx = (float)geom.CentroidX[j];
                    var ny = (float)geom.CentroidY[j];
                    if (Math.Abs(cx - nx) > geom.Width * 0.5f) continue;

                    // 在两地块中心连线的中垂线处画出分界线段
                    var mid = (new Vector2(cx, cy) + new Vector2(nx, ny)) * 0.5f;
                    var normal = (new Vector2(nx, ny) - new Vector2(cx, cy)).Orthogonal().Normalized();
                    var span = (float)Math.Min(geom.SpacingX, geom.SpacingY) * 0.45f;
                    item.DrawLine(mid - (normal * span), mid + (normal * span), color, lineWidth, true);
                }
            }
        }
    }

    private static void DrawBorders(CanvasItem item, WorldSnapshot snapshot, float alpha, float widthScale, Rect2 visibleRect, float screenScale)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var color = new Color(BorderColor.R, BorderColor.G, BorderColor.B, alpha * 0.95f);
        var lineWidth = Mathf.Clamp(1.5f * widthScale, 0.7f, 5.0f) / screenScale;

        for (var i = 0; i < geom.Count; i++)
        {
            var polityI = fields.PolityId[i];
            if (polityI < 0) continue;

            var start = geom.CellNeighborStart[i];
            var end = geom.CellNeighborStart[i + 1];
            var cx = (float)geom.CentroidX[i];
            var cy = (float)geom.CentroidY[i];

            for (var k = start; k < end; k++)
            {
                var j = geom.CellNeighbors[k];
                if (j <= i) continue;

                var polityJ = fields.PolityId[j];
                if (polityI != polityJ && polityJ >= 0)
                {
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

    private static void DrawWindArrows(CanvasItem item, WorldSnapshot snapshot, float alpha, float widthScale, Rect2 visibleRect, float screenScale)
    {
        var geom = snapshot.Geometry;
        var color = new Color(1f, 1f, 1f, alpha * 0.65f);
        var step = Math.Max(1, geom.Count / 300);
        var lineWidth = Mathf.Clamp(1.1f * widthScale, 0.6f, 3.5f) / screenScale;

        for (var i = 0; i < geom.Count; i += step)
        {
            var cx = (float)geom.CentroidX[i];
            var cy = (float)geom.CentroidY[i];
            var center = new Vector2(cx, cy);
            if (!visibleRect.HasPoint(center)) continue;

            var mapH = (float)geom.Height;
            var latNorm = (cy - (mapH * 0.5f)) / (mapH * 0.5f);
            var dir = (latNorm switch
            {
                > 0.6f => new Vector2(-1f, 0.2f),
                > 0.3f => new Vector2(1f, -0.3f),
                > 0.0f => new Vector2(-1f, 0.1f),
                > -0.3f => new Vector2(-1f, -0.1f),
                > -0.6f => new Vector2(1f, 0.3f),
                _ => new Vector2(-1f, -0.2f)
            }).Normalized();

            var len = (float)Math.Min(geom.SpacingX, geom.SpacingY) * 0.8f * widthScale;
            var tip = center + (dir * len * 0.5f);
            var tail = center - (dir * len * 0.5f);

            item.DrawLine(tail, tip, color, lineWidth, true);
            var arrowLeft = tip - (dir.Rotated(0.5f) * len * 0.35f);
            var arrowRight = tip - (dir.Rotated(-0.5f) * len * 0.35f);
            item.DrawLine(tip, arrowLeft, color, lineWidth, true);
            item.DrawLine(tip, arrowRight, color, lineWidth, true);
        }
    }

    private static void DrawPlateBoundaries(CanvasItem item, WorldSnapshot snapshot, float alpha, float widthScale, Rect2 visibleRect, float screenScale)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var lineWidth = Mathf.Clamp(1.8f * widthScale, 1.0f, 5.0f) / screenScale;

        for (var i = 0; i < geom.Count; i++)
        {
            var bType = (PlateBoundaryType)fields.PlateBoundary[i];
            if (bType == PlateBoundaryType.None) continue;

            var c = bType switch
            {
                PlateBoundaryType.Convergent => PlateConvergent,
                PlateBoundaryType.Divergent => PlateDivergent,
                _ => PlateTransform
            };
            var color = new Color(c.R, c.G, c.B, alpha * 0.90f);

            var start = geom.CellNeighborStart[i];
            var end = geom.CellNeighborStart[i + 1];
            var cx = (float)geom.CentroidX[i];
            var cy = (float)geom.CentroidY[i];

            for (var k = start; k < end; k++)
            {
                var j = geom.CellNeighbors[k];
                if (j <= i) continue;

                if (fields.PlateId[i] != fields.PlateId[j])
                {
                    var nx = (float)geom.CentroidX[j];
                    var ny = (float)geom.CentroidY[j];
                    if (Math.Abs(cx - nx) > geom.Width * 0.5f) continue;

                    var mid = (new Vector2(cx, cy) + new Vector2(nx, ny)) * 0.5f;
                    var normal = (new Vector2(nx, ny) - new Vector2(cx, cy)).Orthogonal().Normalized();
                    var span = (float)Math.Min(geom.SpacingX, geom.SpacingY) * 0.5f;
                    item.DrawLine(mid - (normal * span), mid + (normal * span), color, lineWidth, true);
                }
            }
        }
    }
}
