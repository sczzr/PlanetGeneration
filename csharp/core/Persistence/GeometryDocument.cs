using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Persistence;

internal sealed class GeometryDocument
{
    public required WorldExtent Extent { get; init; }
    public double SpacingX { get; init; }
    public double SpacingY { get; init; }
    public int Columns { get; init; }
    public int Rows { get; init; }
    public required double[] SiteX { get; init; }
    public required double[] SiteY { get; init; }
    public required double[] VertexX { get; init; }
    public required double[] VertexY { get; init; }
    public required double[] Area { get; init; }
    public required double[] CentroidX { get; init; }
    public required double[] CentroidY { get; init; }
    public required int[] CellVertexStart { get; init; }
    public required int[] CellNeighborStart { get; init; }
    public required int[] CellNeighbors { get; init; }
    public required bool[] CellPole { get; init; }
    public required bool[] CellSeam { get; init; }
    public static GeometryDocument Capture(CellGeometry geometry) => new()
    {
        Extent = geometry.Extent, SpacingX = geometry.SpacingX, SpacingY = geometry.SpacingY,
        Columns = geometry.Columns, Rows = geometry.Rows,
        SiteX = geometry.SiteX,
        SiteY = geometry.SiteY,
        VertexX = geometry.VertexX,
        VertexY = geometry.VertexY,
        Area = geometry.Area,
        CentroidX = geometry.CentroidX,
        CentroidY = geometry.CentroidY,
        CellVertexStart = geometry.CellVertexStart,
        CellNeighborStart = geometry.CellNeighborStart,
        CellNeighbors = geometry.CellNeighbors,
        CellPole = geometry.CellPole,
        CellSeam = geometry.CellSeam
    };

    public CellGeometry Restore()
    {
        if (Extent == null || !double.IsFinite(Extent.Width) || !double.IsFinite(Extent.Height) || Extent.Width <= 0 || Extent.Height <= 0 ||
            !double.IsFinite(SpacingX) || !double.IsFinite(SpacingY) || SpacingX <= 0 || SpacingY <= 0)
            throw new InvalidDataException("世界范围或网格间距无效。");
        var count = SiteX?.Length ?? 0;
        if (count < 1 || count > 2_000_000 || Columns < 1 || Rows < 1 || (long)Columns * Rows != count)
            throw new InvalidDataException("网格地块数无效。");
        if (Math.Abs(SpacingX * Columns - Extent.Width) > Extent.Width * 1e-8 ||
            Math.Abs(SpacingY * Rows - Extent.Height) > Extent.Height * 1e-8)
            throw new InvalidDataException("网格间距与范围不一致，不能安全重建空间索引。");
        RequireLength(SiteY, count, nameof(SiteY));
        RequireLength(Area, count, nameof(Area));
        RequireLength(CentroidX, count, nameof(CentroidX));
        RequireLength(CentroidY, count, nameof(CentroidY));
        RequireLength(CellPole, count, nameof(CellPole));
        RequireLength(CellSeam, count, nameof(CellSeam));
        if (VertexX == null || CellNeighbors == null) throw new InvalidDataException("缺少顶点或邻接。");
        RequireLength(VertexY, VertexX.Length, nameof(VertexY));
        ValidateOffsets(CellVertexStart, count, VertexX.Length, nameof(CellVertexStart));
        ValidateOffsets(CellNeighborStart, count, CellNeighbors.Length, nameof(CellNeighborStart));
        if (CellNeighbors.Any(id => id < 0 || id >= count)) throw new InvalidDataException("邻接地块编号越界。");
        foreach (var column in new[] { SiteX!, SiteY, VertexX, VertexY, Area, CentroidX, CentroidY })
            if (column.Any(value => !double.IsFinite(value))) throw new InvalidDataException("几何包含非有限坐标。");
        var index = new SiteIndex(Extent.Width, Extent.Height, (SpacingX + SpacingY) * 0.5, SiteX!, SiteY);
        return new CellGeometry(Extent, SpacingX, SpacingY, Columns, Rows, SiteX!, SiteY, VertexX, VertexY,
            CellVertexStart, CellNeighborStart, CellNeighbors, CellPole, CellSeam, Area, CentroidX, CentroidY, index);
    }

    private static void RequireLength(Array? values, int length, string name)
    {
        if (values == null || values.Length != length) throw new InvalidDataException($"几何列 {name} 长度错误。");
    }
    private static void ValidateOffsets(int[]? offsets, int count, int total, string name)
    {
        RequireLength(offsets, count + 1, name);
        if (offsets![0] != 0 || offsets[^1] != total) throw new InvalidDataException($"{name} 边界错误。");
        for (var i = 1; i < offsets.Length; i++)
            if (offsets[i] < offsets[i - 1] || offsets[i] > total) throw new InvalidDataException($"{name} 必须单调且在范围内。");
    }
}
