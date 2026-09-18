using Godot;
using PlanetGeneration.Core.Domain;
using System;
using System.Collections.Generic;

namespace PlanetGeneration.Rendering;

/// <summary>
/// 多边形地块网格构建器：
/// 将 <see cref="CellGeometry"/> 凸多边形三角化为 Godot 2D <see cref="ArrayMesh"/>。
/// 支持经度跨缝两侧镜像展开与色彩缓冲快速换色。
/// </summary>
public sealed class PolygonMeshBuilder
{
    private readonly CellGeometry _geometry;
    private readonly Vector2[] _vertices;
    private readonly int[] _indices;
    private readonly int[] _vertexCellIndex; // 每个顶点对应的 cellId
    private ArrayMesh? _cachedMesh;

    public int TotalVertices => _vertices.Length;
    public int TotalIndices => _indices.Length;

    public PolygonMeshBuilder(CellGeometry geometry)
    {
        _geometry = geometry;
        var count = geometry.Count;
        var width = (float)geometry.Width;

        var vertexList = new List<Vector2>(count * 7);
        var indexList = new List<int>(count * 18);
        var cellIndexList = new List<int>(count * 7);

        for (var i = 0; i < count; i++)
        {
            var vCount = geometry.GetVertexCount(i);
            if (vCount < 3) continue;

            var start = geometry.CellVertexStart[i];
            var cx = (float)geometry.CentroidX[i];
            var cy = (float)geometry.CentroidY[i];

            // 基础副本
            AppendCellPolygon(i, cx, cy, start, vCount, 0f, vertexList, indexList, cellIndexList);

            // 若地块跨经度缝，向左右各铺一份镜像副本，消除圆柱两端接缝空隙
            if (geometry.CellSeam[i])
            {
                AppendCellPolygon(i, cx - width, cy, start, vCount, -width, vertexList, indexList, cellIndexList);
                AppendCellPolygon(i, cx + width, cy, start, vCount, width, vertexList, indexList, cellIndexList);
            }
        }

        _vertices = vertexList.ToArray();
        _indices = indexList.ToArray();
        _vertexCellIndex = cellIndexList.ToArray();
    }

    private void AppendCellPolygon(
        int cellId,
        float cx,
        float cy,
        int vertexStart,
        int vCount,
        float xShift,
        List<Vector2> vertices,
        List<int> indices,
        List<int> cellIndices)
    {
        var centerIdx = vertices.Count;
        vertices.Add(new Vector2(cx, cy));
        cellIndices.Add(cellId);

        var firstRingIdx = vertices.Count;
        for (var k = 0; k < vCount; k++)
        {
            var vx = (float)_geometry.VertexX[vertexStart + k] + xShift;
            var vy = (float)_geometry.VertexY[vertexStart + k];
            vertices.Add(new Vector2(vx, vy));
            cellIndices.Add(cellId);
        }

        for (var k = 0; k < vCount; k++)
        {
            var nextK = (k + 1) % vCount;
            indices.Add(centerIdx);
            indices.Add(firstRingIdx + k);
            indices.Add(firstRingIdx + nextK);
        }
    }

    /// <summary>
    /// 根据每个地块的色彩数组构建/更新网格。
    /// </summary>
    public ArrayMesh BuildMesh(Color[] cellColors)
    {
        var colors = new Color[_vertices.Length];
        for (var v = 0; v < _vertices.Length; v++)
        {
            var cellId = _vertexCellIndex[v];
            colors[v] = cellId >= 0 && cellId < cellColors.Length ? cellColors[cellId] : Colors.DarkGray;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = _vertices;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.Index] = _indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        _cachedMesh = mesh;
        return mesh;
    }
}
