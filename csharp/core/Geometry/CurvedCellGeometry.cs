using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Domain;

namespace PlanetGeneration.Core.Geometry;

/// <summary>
/// 预三角剖分的矢量网格拓扑（纯 C# 数据），用于 Godot ArrayMesh 高速渲染。
/// </summary>
public sealed class CurvedMeshTopology
{
    public PolyVec2[] Vertices { get; }
    public int[] Indices { get; }
    public int[] VertexToCell { get; }

    public CurvedMeshTopology(PolyVec2[] vertices, int[] indices, int[] vertexToCell)
    {
        Vertices = vertices;
        Indices = indices;
        VertexToCell = vertexToCell;
    }
}

/// <summary>
/// 规范化平滑曲线边及其所属地块拓扑。
/// </summary>
public sealed class CanonicalCurvedEdge
{
    public required PolyVec2[] Points { get; init; }
    public int CellA { get; set; } = -1;
    public int CellB { get; set; } = -1;
}

/// <summary>
/// 平滑曲线地块几何构建器：
/// 实现基于规范化共享边（Canonical Shared Edge）的三次曲线插值，
/// 严格保证相邻地块水密闭合（0 缝隙、0 重叠），并提供全图矢量网格拓扑生成。
/// </summary>
public static class CurvedCellGeometry
{
    private const double Tolerance = 1e-4;

    private readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        public readonly int X0;
        public readonly int Y0;
        public readonly int X1;
        public readonly int Y1;

        public EdgeKey(double x0, double y0, double x1, double y1)
        {
            X0 = (int)Math.Round(x0 * 100.0);
            Y0 = (int)Math.Round(y0 * 100.0);
            X1 = (int)Math.Round(x1 * 100.0);
            Y1 = (int)Math.Round(y1 * 100.0);
        }

        public bool Equals(EdgeKey other)
            => X0 == other.X0 && Y0 == other.Y0 && X1 == other.X1 && Y1 == other.Y1;

        public override bool Equals(object? obj)
            => obj is EdgeKey other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(X0, Y0, X1, Y1);
    }

    /// <summary>
    /// 取单个地块的平滑曲线多边形（未展开帧，围绕地块质心对齐）。
    /// </summary>
    public static PolyVec2[] GetCurvedPolygon(CellGeometry geom, int cellId, int subdivisions = 3)
    {
        if (cellId < 0 || cellId >= geom.Count)
        {
            return Array.Empty<PolyVec2>();
        }

        var vertexCount = geom.GetVertexCount(cellId);
        if (vertexCount < 3)
        {
            return Array.Empty<PolyVec2>();
        }

        var start = geom.CellVertexStart[cellId];
        var refX = geom.CentroidX[cellId];
        var width = geom.Width;

        var baseVerts = new PolyVec2[vertexCount];
        for (var i = 0; i < vertexCount; i++)
        {
            var vx = geom.VertexX[start + i];
            vx -= Math.Round((vx - refX) / width) * width;
            baseVerts[i] = new PolyVec2(vx, geom.VertexY[start + i]);
        }

        var edgeCache = new Dictionary<EdgeKey, PolyVec2[]>(vertexCount * 2);
        var result = new List<PolyVec2>(vertexCount * subdivisions);

        for (var i = 0; i < vertexCount; i++)
        {
            var p0 = baseVerts[i];
            var p1 = baseVerts[(i + 1) % vertexCount];

            var curvedEdge = GetOrCreateCurvedEdge(p0, p1, width, subdivisions, edgeCache);
            // curvedEdge 的首点为 p0，尾点为 p1。多边形连接时只添加 [0 .. count - 1)
            for (var k = 0; k < curvedEdge.Length - 1; k++)
            {
                result.Add(curvedEdge[k]);
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// 取平滑曲线的高亮环（包含基准环与经度缝 ±Width 镜像环）。
    /// </summary>
    public static PolyVec2[][] GetCurvedHighlightRings(CellGeometry geom, int cellId, int subdivisions = 3)
    {
        var poly = GetCurvedPolygon(geom, cellId, subdivisions);
        var rings = new PolyVec2[3][];
        if (poly.Length < 3)
        {
            rings[0] = Array.Empty<PolyVec2>();
            rings[1] = Array.Empty<PolyVec2>();
            rings[2] = Array.Empty<PolyVec2>();
            return rings;
        }

        var width = geom.Width;
        rings[0] = poly;

        var ringMinus = new PolyVec2[poly.Length];
        var ringPlus = new PolyVec2[poly.Length];
        for (var i = 0; i < poly.Length; i++)
        {
            ringMinus[i] = new PolyVec2(poly[i].X - width, poly[i].Y);
            ringPlus[i] = new PolyVec2(poly[i].X + width, poly[i].Y);
        }

        rings[1] = ringMinus;
        rings[2] = ringPlus;
        return rings;
    }

    /// <summary>
    /// 获取全图所有规范化平滑曲线边及其所属地块拓扑（去重共享边，并关联 CellA 与 CellB）。
    /// </summary>
    public static CanonicalCurvedEdge[] GetCanonicalCurvedEdgesWithTopology(CellGeometry geom, int subdivisions = 3)
    {
        var count = geom.Count;
        var width = geom.Width;
        var edgeCache = new Dictionary<EdgeKey, CanonicalCurvedEdge>(count * 3);

        for (var cellId = 0; cellId < count; cellId++)
        {
            var vCount = geom.GetVertexCount(cellId);
            if (vCount < 3) continue;

            var start = geom.CellVertexStart[cellId];
            var refX = geom.CentroidX[cellId];

            var baseVerts = new PolyVec2[vCount];
            for (var i = 0; i < vCount; i++)
            {
                var vx = geom.VertexX[start + i];
                vx -= Math.Round((vx - refX) / width) * width;
                baseVerts[i] = new PolyVec2(vx, geom.VertexY[start + i]);
            }

            for (var i = 0; i < vCount; i++)
            {
                var a = baseVerts[i];
                var b = baseVerts[(i + 1) % vCount];

                var midX = (a.X + b.X) * 0.5d;
                var shift = Math.Floor(midX / width) * width;

                var aNorm = new PolyVec2(a.X - shift, a.Y);
                var bNorm = new PolyVec2(b.X - shift, b.Y);

                var isForward = (aNorm.X < bNorm.X) || (Math.Abs(aNorm.X - bNorm.X) < Tolerance && aNorm.Y < bNorm.Y);
                var p0 = isForward ? aNorm : bNorm;
                var p1 = isForward ? bNorm : aNorm;

                var key = new EdgeKey(p0.X, p0.Y, p1.X, p1.Y);

                if (edgeCache.TryGetValue(key, out var existing))
                {
                    if (existing.CellA != cellId && existing.CellB < 0)
                    {
                        existing.CellB = cellId;
                    }
                }
                else
                {
                    var canonicalCurve = SubdivideCubicEdge(p0, p1, subdivisions);
                    var newEdge = new CanonicalCurvedEdge
                    {
                        Points = canonicalCurve,
                        CellA = cellId,
                        CellB = -1
                    };
                    edgeCache[key] = newEdge;
                }
            }
        }

        var edges = new CanonicalCurvedEdge[edgeCache.Count];
        var idx = 0;
        foreach (var edge in edgeCache.Values)
        {
            edges[idx++] = edge;
        }

        return edges;
    }

    /// <summary>
    /// 获取全图所有规范化平滑曲线边（每条边严格唯一，已去重共享边）。
    /// </summary>
    public static PolyVec2[][] GetCanonicalCurvedEdges(CellGeometry geom, int subdivisions = 3)
    {
        var edgesWithTopology = GetCanonicalCurvedEdgesWithTopology(geom, subdivisions);
        var result = new PolyVec2[edgesWithTopology.Length][];
        for (var i = 0; i < edgesWithTopology.Length; i++)
        {
            result[i] = edgesWithTopology[i].Points;
        }
        return result;
    }

    /// <summary>
    /// 为整个地块几何生成 GPU 2D 矢量网格拓扑（水密无缝、带经度镜像无缝拼接）。
    /// </summary>
    public static CurvedMeshTopology BuildMeshTopology(CellGeometry geom, int subdivisions = 3)
    {
        var count = geom.Count;
        var width = geom.Width;
        var edgeCache = new Dictionary<EdgeKey, PolyVec2[]>(count * 6);

        var vertList = new List<PolyVec2>(count * 20);
        var indexList = new List<int>(count * 60);
        var cellMapping = new List<int>(count * 20);

        for (var cellId = 0; cellId < count; cellId++)
        {
            var vCount = geom.GetVertexCount(cellId);
            if (vCount < 3) continue;

            var start = geom.CellVertexStart[cellId];
            var refX = geom.CentroidX[cellId];
            var refY = geom.CentroidY[cellId];

            var baseVerts = new PolyVec2[vCount];
            for (var i = 0; i < vCount; i++)
            {
                var vx = geom.VertexX[start + i];
                vx -= Math.Round((vx - refX) / width) * width;
                baseVerts[i] = new PolyVec2(vx, geom.VertexY[start + i]);
            }

            var poly = new List<PolyVec2>(vCount * subdivisions);
            for (var i = 0; i < vCount; i++)
            {
                var p0 = baseVerts[i];
                var p1 = baseVerts[(i + 1) % vCount];
                var edge = GetOrCreateCurvedEdge(p0, p1, width, subdivisions, edgeCache);
                for (var k = 0; k < edge.Length - 1; k++)
                {
                    poly.Add(edge[k]);
                }
            }

            var centroid = new PolyVec2(refX, refY);

            // 添加本地几何
            AddCellTriangles(poly, centroid, cellId, 0d, vertList, indexList, cellMapping);

            // 检查经度缝镜像：如果地块跨经度缝或靠近边缘，在另一侧补充镜像三角形
            var minX = double.MaxValue;
            var maxX = double.MinValue;
            for (var i = 0; i < poly.Count; i++)
            {
                if (poly[i].X < minX) minX = poly[i].X;
                if (poly[i].X > maxX) maxX = poly[i].X;
            }

            if (minX < 0d)
            {
                AddCellTriangles(poly, centroid, cellId, width, vertList, indexList, cellMapping);
            }
            if (maxX >= width)
            {
                AddCellTriangles(poly, centroid, cellId, -width, vertList, indexList, cellMapping);
            }
        }

        return new CurvedMeshTopology(vertList.ToArray(), indexList.ToArray(), cellMapping.ToArray());
    }

    private static void AddCellTriangles(
        List<PolyVec2> poly,
        PolyVec2 centroid,
        int cellId,
        double offsetX,
        List<PolyVec2> vertices,
        List<int> indices,
        List<int> mapping)
    {
        var n = poly.Count;
        if (n < 3) return;

        var centerIndex = vertices.Count;
        vertices.Add(new PolyVec2(centroid.X + offsetX, centroid.Y));
        mapping.Add(cellId);

        var firstRingIndex = vertices.Count;
        for (var i = 0; i < n; i++)
        {
            vertices.Add(new PolyVec2(poly[i].X + offsetX, poly[i].Y));
            mapping.Add(cellId);
        }

        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            indices.Add(centerIndex);
            indices.Add(firstRingIndex + i);
            indices.Add(firstRingIndex + next);
        }
    }

    /// <summary>
    /// 获取或计算两点之间的规范化平滑曲线点链。
    /// </summary>
    private static PolyVec2[] GetOrCreateCurvedEdge(
        PolyVec2 a,
        PolyVec2 b,
        double width,
        int subdivisions,
        Dictionary<EdgeKey, PolyVec2[]> cache)
    {
        // 将边的中点归一化到 [0, width)
        var midX = (a.X + b.X) * 0.5d;
        var shift = Math.Floor(midX / width) * width;

        var aNorm = new PolyVec2(a.X - shift, a.Y);
        var bNorm = new PolyVec2(b.X - shift, b.Y);

        // 确定规范方向：按坐标严格升序
        var isForward = (aNorm.X < bNorm.X) || (Math.Abs(aNorm.X - bNorm.X) < Tolerance && aNorm.Y < bNorm.Y);
        var p0 = isForward ? aNorm : bNorm;
        var p1 = isForward ? bNorm : aNorm;

        var key = new EdgeKey(p0.X, p0.Y, p1.X, p1.Y);

        if (!cache.TryGetValue(key, out var canonicalCurve))
        {
            canonicalCurve = SubdivideCubicEdge(p0, p1, subdivisions);
            cache[key] = canonicalCurve;
        }

        // 根据原方向返回点集，并平移还原回原始 shift 坐标
        var len = canonicalCurve.Length;
        var result = new PolyVec2[len];

        if (isForward)
        {
            for (var i = 0; i < len; i++)
            {
                result[i] = new PolyVec2(canonicalCurve[i].X + shift, canonicalCurve[i].Y);
            }
        }
        else
        {
            for (var i = 0; i < len; i++)
            {
                result[i] = new PolyVec2(canonicalCurve[len - 1 - i].X + shift, canonicalCurve[len - 1 - i].Y);
            }
        }

        return result;
    }

    /// <summary>
    /// 用三次贝塞尔曲线生成平滑有机曲线，两端严格固定在 p0 与 p1。
    /// </summary>
    private static PolyVec2[] SubdivideCubicEdge(PolyVec2 p0, PolyVec2 p1, int subdivisions)
    {
        if (subdivisions <= 1)
        {
            return new[] { p0, p1 };
        }

        var dx = p1.X - p0.X;
        var dy = p1.Y - p0.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));

        if (length < 1e-5)
        {
            return new[] { p0, p1 };
        }

        // 法向量（单位长度）
        var nx = -dy / length;
        var ny = dx / length;

        // 确定性伪随机哈希，仅依赖规范端点
        var h = HashEdge(p0.X, p0.Y, p1.X, p1.Y);
        var curveRatio = (((h & 0xFFFF) / 65535.0) * 2.0 - 1.0) * 0.14;
        var sCurveRatio = ((((h >> 16) & 0xFFFF) / 65535.0) * 2.0 - 1.0) * 0.06;

        var offset1 = (curveRatio + sCurveRatio) * length;
        var offset2 = (curveRatio - sCurveRatio) * length;

        var c1 = new PolyVec2(p0.X + (dx * (1.0 / 3.0)) + (nx * offset1), p0.Y + (dy * (1.0 / 3.0)) + (ny * offset1));
        var c2 = new PolyVec2(p0.X + (dx * (2.0 / 3.0)) + (nx * offset2), p0.Y + (dy * (2.0 / 3.0)) + (ny * offset2));

        var points = new PolyVec2[subdivisions + 1];
        points[0] = p0;
        points[subdivisions] = p1;

        for (var i = 1; i < subdivisions; i++)
        {
            var t = (double)i / subdivisions;
            var omt = 1.0 - t;
            var b0 = omt * omt * omt;
            var b1 = 3.0 * omt * omt * t;
            var b2 = 3.0 * omt * t * t;
            var b3 = t * t * t;

            var px = (b0 * p0.X) + (b1 * c1.X) + (b2 * c2.X) + (b3 * p1.X);
            var py = (b0 * p0.Y) + (b1 * c1.Y) + (b2 * c2.Y) + (b3 * p1.Y);
            points[i] = new PolyVec2(px, py);
        }

        return points;
    }

    private static ulong HashEdge(double x0, double y0, double x1, double y1)
    {
        var h = 14695981039346656037UL;
        h = (h ^ (ulong)(long)Math.Round(x0 * 100.0)) * 1099511628211UL;
        h = (h ^ (ulong)(long)Math.Round(y0 * 100.0)) * 1099511628211UL;
        h = (h ^ (ulong)(long)Math.Round(x1 * 100.0)) * 1099511628211UL;
        h = (h ^ (ulong)(long)Math.Round(y1 * 100.0)) * 1099511628211UL;
        return h;
    }
}
