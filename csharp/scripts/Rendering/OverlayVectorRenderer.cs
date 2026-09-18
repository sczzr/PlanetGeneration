using Godot;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Layers;
using System;
using System.Collections.Generic;

namespace PlanetGeneration.Rendering;

/// <summary>
/// 8 种叠加图层矢量绘制器：
/// 河流、政体边界、海岸线、城市、贸易路线、地块轮廓、风向箭头、板块边界。
/// </summary>
public static class OverlayVectorRenderer
{
    private static readonly Color RiverColor = Color.FromHtml("#1a75d2");
    private static readonly Color CoastlineColor = Color.FromHtml("#0b2247");
    private static readonly Color BorderColor = Color.FromHtml("#ffe066");
    private static readonly Color TradeRouteColor = Color.FromHtml("#ffb732");
    private static readonly Color CellOutlineColor = new(0.12f, 0.14f, 0.18f, 0.45f);

    private static readonly Color PlateConvergent = new(0.98f, 0.22f, 0.22f);
    private static readonly Color PlateDivergent = new(0.22f, 0.92f, 0.95f);
    private static readonly Color PlateTransform = new(0.98f, 0.80f, 0.28f);

    public static void DrawOverlays(
        CanvasItem item,
        WorldSnapshot snapshot,
        LayerStackState layerStack,
        Rect2 visibleRect,
        Font? font = null)
    {
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
                    DrawRivers(item, snapshot, alpha, widthScale, visibleRect);
                    break;
                case "coastlines":
                    DrawCoastlines(item, snapshot, alpha, widthScale, visibleRect);
                    break;
                case "polity_borders" or "borders":
                    DrawBorders(item, snapshot, alpha, widthScale, visibleRect);
                    break;
                case "cities" or "city_labels":
                    DrawCities(item, snapshot, alpha, font, visibleRect);
                    break;
                case "trade_routes" or "trade_network":
                    DrawTradeRoutes(item, snapshot, alpha, widthScale, visibleRect);
                    break;
                case "cell_borders" or "cell_outlines":
                    DrawCellOutlines(item, snapshot, alpha, widthScale, visibleRect);
                    break;
                case "wind_arrows":
                    DrawWindArrows(item, snapshot, alpha, widthScale, visibleRect);
                    break;
                case "plate_borders" or "plate_boundaries":
                    DrawPlateBoundaries(item, snapshot, alpha, widthScale, visibleRect);
                    break;
            }
        }
    }

    private static void DrawRivers(CanvasItem item, WorldSnapshot snapshot, float alpha, float widthScale, Rect2 visibleRect)
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
            var w = Mathf.Clamp(Mathf.Log(1f + flux) * 0.75f * widthScale, 1.2f, 9.0f * widthScale);
            item.DrawLine(p0, p1, color, w);
        }
    }

    private static void DrawCoastlines(CanvasItem item, WorldSnapshot snapshot, float alpha, float widthScale, Rect2 visibleRect)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var seaLevel = snapshot.Options.SeaLevel;
        var color = new Color(CoastlineColor.R, CoastlineColor.G, CoastlineColor.B, alpha * 0.85f);
        var lineWidth = Mathf.Clamp(1.8f * widthScale, 1f, 6f);

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
                    item.DrawLine(mid - (normal * span), mid + (normal * span), color, lineWidth);
                }
            }
        }
    }

    private static void DrawBorders(CanvasItem item, WorldSnapshot snapshot, float alpha, float widthScale, Rect2 visibleRect)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var color = new Color(BorderColor.R, BorderColor.G, BorderColor.B, alpha * 0.95f);
        var lineWidth = Mathf.Clamp(2.0f * widthScale, 1.2f, 7f);

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
                    item.DrawLine(mid - (normal * span), mid + (normal * span), color, lineWidth);
                }
            }
        }
    }

    private static void DrawCities(CanvasItem item, WorldSnapshot snapshot, float alpha, Font? font, Rect2 visibleRect)
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

            var (radius, color, innerRadius) = s.Rank switch
            {
                SettlementRank.CityState => (7.0f, capitalColor, 4.0f),
                SettlementRank.Town => (5.5f, majorColor, 3.0f),
                _ => (4.0f, townColor, 2.0f)
            };

            // 绘制外圈与中心实心点
            item.DrawCircle(pos, radius, Colors.Black * 0.85f);
            item.DrawCircle(pos, radius - 1.2f, color);
            item.DrawCircle(pos, innerRadius, Colors.Black * 0.75f);

            // 绘制城市名称文本
            if (font != null && !string.IsNullOrEmpty(s.Name))
            {
                var textPos = pos + new Vector2(radius + 4f, 4f);
                item.DrawString(font, textPos + new Vector2(1f, 1f), s.Name, HorizontalAlignment.Left, -1, 11, shadowColor);
                item.DrawString(font, textPos, s.Name, HorizontalAlignment.Left, -1, 11, textColor);
            }
        }
    }

    private static void DrawTradeRoutes(CanvasItem item, WorldSnapshot snapshot, float alpha, float widthScale, Rect2 visibleRect)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var color = new Color(TradeRouteColor.R, TradeRouteColor.G, TradeRouteColor.B, alpha * 0.90f);

        for (var i = 0; i < geom.Count; i++)
        {
            if (!fields.TradeRouteMask[i]) continue;

            var start = geom.CellNeighborStart[i];
            var end = geom.CellNeighborStart[i + 1];
            var cx = (float)geom.CentroidX[i];
            var cy = (float)geom.CentroidY[i];

            for (var k = start; k < end; k++)
            {
                var j = geom.CellNeighbors[k];
                if (j <= i) continue;

                if (fields.TradeRouteMask[j])
                {
                    var nx = (float)geom.CentroidX[j];
                    var ny = (float)geom.CentroidY[j];
                    if (Math.Abs(cx - nx) > geom.Width * 0.5f) continue;

                    var flow = Math.Max(fields.TradeFlow[i], fields.TradeFlow[j]);
                    var w = Mathf.Clamp((1.2f + (flow * 3.5f)) * widthScale, 1.2f, 8f);
                    item.DrawLine(new Vector2(cx, cy), new Vector2(nx, ny), color, w);
                }
            }
        }
    }

    private static void DrawCellOutlines(CanvasItem item, WorldSnapshot snapshot, float alpha, float widthScale, Rect2 visibleRect)
    {
        var geom = snapshot.Geometry;
        var color = new Color(CellOutlineColor.R, CellOutlineColor.G, CellOutlineColor.B, alpha * CellOutlineColor.A);
        var lineWidth = Mathf.Clamp(1.0f * widthScale, 0.5f, 3.0f);

        for (var i = 0; i < geom.Count; i++)
        {
            var cx = (float)geom.CentroidX[i];
            var cy = (float)geom.CentroidY[i];
            if (!visibleRect.HasPoint(new Vector2(cx, cy))) continue;

            var poly = geom.GetCurvedPolygon(i, 3);
            if (poly.Length < 3) continue;

            for (var k = 0; k < poly.Length; k++)
            {
                var nextK = (k + 1) % poly.Length;
                var p0 = new Vector2((float)poly[k].X, (float)poly[k].Y);
                var p1 = new Vector2((float)poly[nextK].X, (float)poly[nextK].Y);

                if (Math.Abs(p0.X - p1.X) > geom.Width * 0.5f) continue;
                item.DrawLine(p0, p1, color, lineWidth);
            }
        }
    }

    private static void DrawWindArrows(CanvasItem item, WorldSnapshot snapshot, float alpha, float widthScale, Rect2 visibleRect)
    {
        var geom = snapshot.Geometry;
        var color = new Color(1f, 1f, 1f, alpha * 0.65f);
        var step = Math.Max(1, geom.Count / 300); // 适度稀疏抽样，避免过密

        for (var i = 0; i < geom.Count; i += step)
        {
            var cx = (float)geom.CentroidX[i];
            var cy = (float)geom.CentroidY[i];
            var center = new Vector2(cx, cy);
            if (!visibleRect.HasPoint(center)) continue;

            // 纬度大尺度环流简易矢量方向（信风、西风带、极地东风）
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

            item.DrawLine(tail, tip, color, 1.2f * widthScale);
            var arrowLeft = tip - (dir.Rotated(0.5f) * len * 0.35f);
            var arrowRight = tip - (dir.Rotated(-0.5f) * len * 0.35f);
            item.DrawLine(tip, arrowLeft, color, 1.2f * widthScale);
            item.DrawLine(tip, arrowRight, color, 1.2f * widthScale);
        }
    }

    private static void DrawPlateBoundaries(CanvasItem item, WorldSnapshot snapshot, float alpha, float widthScale, Rect2 visibleRect)
    {
        var geom = snapshot.Geometry;
        var fields = snapshot.Fields;
        var lineWidth = Mathf.Clamp(2.2f * widthScale, 1.2f, 6.0f);

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
                    item.DrawLine(mid - (normal * span), mid + (normal * span), color, lineWidth);
                }
            }
        }
    }
}
