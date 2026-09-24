using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>
/// 旧栅格消费者的兼容视图。几何和字段均由 Core 持有，不再复制几何算法或属性模型。
/// 数组按引用共享；使用 Geometry/Fields 可以直接调用 Core 服务。
/// </summary>
public sealed class PolygonGrid
{
    public CellGeometry Geometry { get; }
    public CellFields Fields { get; }

    internal PolygonGrid(CellGeometry geometry, CellFields? fields = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (fields != null && fields.Count != geometry.Count)
            throw new ArgumentException("地块字段数量必须与几何一致。", nameof(fields));
        Geometry = geometry;
        Fields = fields ?? CellFields.Create(geometry.Count);
    }

    public double Width => Geometry.Width;
    public double Height => Geometry.Height;
    public double SpacingX => Geometry.SpacingX;
    public double SpacingY => Geometry.SpacingY;
    public int Columns => Geometry.Columns;
    public int Rows => Geometry.Rows;
    public int Count => Geometry.Count;
    public double[] SiteX => Geometry.SiteX;
    public double[] SiteY => Geometry.SiteY;
    public double[] VertexX => Geometry.VertexX;
    public double[] VertexY => Geometry.VertexY;
    public int[] CellVertexStart => Geometry.CellVertexStart;
    public int[] CellNeighborStart => Geometry.CellNeighborStart;
    public int[] CellNeighbors => Geometry.CellNeighbors;
    public bool[] CellPole => Geometry.CellPole;
    public bool[] CellSeam => Geometry.CellSeam;
    public double[] Area => Geometry.Area;
    public double[] CentroidX => Geometry.CentroidX;
    public double[] CentroidY => Geometry.CentroidY;
    public int GetVertexCount(int cellId) => Geometry.GetVertexCount(cellId);
    public int GetNeighborCount(int cellId) => Geometry.GetNeighborCount(cellId);
    public int GetNeighbor(int cellId, int slot) => Geometry.GetNeighbor(cellId, slot);
    public PolyVec2[] GetPolygon(int cellId) => Geometry.GetPolygon(cellId);
    public PolyVec2 GetCentroid(int cellId) => Geometry.GetCentroid(cellId);
    public PolyVec2[][] GetHighlightRings(int cellId) => Geometry.GetHighlightRings(cellId);
    public int FindCell(double x, double y) => Geometry.FindCell(x, y);
    internal int FindNearestCellExcluding(double x, double y, int excludeCellId, out double distance)
        => Geometry.FindNearestCellExcluding(x, y, excludeCellId, out distance);
    public List<int> FindAll(double x, double y, double radius) => Geometry.FindAll(x, y, radius);
    public double WrappedDistanceSquared(double x0, double y0, double x1, double y1)
        => Geometry.Extent.DistanceSquaredWrapped(new PolyVec2(x0, y0), new PolyVec2(x1, y1));
    public double WrappedDistance(double x0, double y0, double x1, double y1)
        => Math.Sqrt(WrappedDistanceSquared(x0, y0, x1, y1));
    public double NormalizeX(double x) => Geometry.Extent.WrapX(x);
}
