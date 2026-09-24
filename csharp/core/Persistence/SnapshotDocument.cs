using System.Reflection;
using System.Text.Json;
using PlanetGeneration.Core.Cartography;
using PlanetGeneration.Core.Cartography.Planning;
using PlanetGeneration.Core.Domain;

namespace PlanetGeneration.Core.Persistence;

/// <summary>仅包含可序列化事实；运行时 SnapshotId 与空间索引在恢复时重新创建。</summary>
internal sealed class SnapshotDocument
{
    public required GenerationOptions Options { get; init; }
    public required GeometryDocument Geometry { get; init; }
    public required JsonElement Fields { get; init; }
    public required List<SettlementInfo> Settlements { get; init; }
    public required PlateSystemSummary PlateSummary { get; init; }
    public required WorldStatsSummary Stats { get; init; }
    public EcologyResult? Ecology { get; init; }
    public CivilizationResult? Civilization { get; init; }
    public List<MegaTerrainRegion> MegaRegions { get; init; } = new();
    public CartographyDocument? Cartography { get; init; }
    public WindDocument? Wind { get; init; }
    public DateTime CreatedAt { get; init; }

    public static SnapshotDocument Capture(WorldSnapshot snapshot) => new()
    {
        Options = snapshot.Options, Geometry = GeometryDocument.Capture(snapshot.Geometry),
        Fields = JsonSerializer.SerializeToElement(snapshot.Fields, WorldSnapshotArchive.JsonOptions),
        Settlements = snapshot.Settlements.ToList(), PlateSummary = snapshot.PlateSummary, Stats = snapshot.Stats,
        Ecology = snapshot.Ecology, Civilization = snapshot.Civilization, MegaRegions = snapshot.MegaRegions.ToList(),
        Cartography = snapshot.Cartography == null ? null : CartographyDocument.Capture(snapshot.Cartography),
        Wind = snapshot.ContinuousWind == null ? null : WindDocument.Capture(snapshot.ContinuousWind), CreatedAt = snapshot.CreatedAt
    };

    public WorldSnapshot Restore()
    {
        var geometry = Geometry.Restore();
        if (Options == null || Options.Extent != geometry.Extent || Stats == null || Stats.CellCount != geometry.Count)
            throw new InvalidDataException("快照参数、统计与几何不一致。");
        if (Settlements == null || Settlements.Any(s => s.CellId < 0 || s.CellId >= geometry.Count))
            throw new InvalidDataException("聚落地块编号无效。");
        if (PlateSummary?.Sites == null || MegaRegions == null ||
            (Civilization != null && Civilization.RecentEvents == null) ||
            MegaRegions.Any(region => region.Cells == null || region.Cells.Any(id => id < 0 || id >= geometry.Count)))
            throw new InvalidDataException("快照实体引用无效。");
        var fields = RestoreFields(Fields, geometry.Count);
        return new WorldSnapshot(Options, geometry, fields, Settlements, PlateSummary, Stats, Ecology, Civilization,
            createdAt: CreatedAt, megaRegions: MegaRegions, cartography: Cartography?.Restore())
        {
            ContinuousWind = Wind?.Restore()
        };
    }

    // CellFields 只有固定长度的公开数组列。集中按列恢复，避免新增字段时遗漏一份手写 DTO。
    private static readonly PropertyInfo[] FieldColumns = typeof(CellFields).GetProperties()
        .Where(property => property.PropertyType.IsArray).ToArray();

    private static CellFields RestoreFields(JsonElement data, int count)
    {
        if (data.GetProperty(nameof(CellFields.Count)).GetInt32() != count)
            throw new InvalidDataException("属性列数量与几何不一致。");
        var fields = CellFields.Create(count);
        foreach (var column in FieldColumns)
        {
            if (!data.TryGetProperty(column.Name, out var values))
                throw new InvalidDataException($"属性列 {column.Name} 缺失。");
            // System.Text.Json 将 byte[] 编码为 Base64，其余列为普通 JSON 数组。
            Array source;
            if (column.PropertyType == typeof(byte[]) && values.ValueKind == JsonValueKind.String)
                source = values.GetBytesFromBase64();
            else if (values.ValueKind == JsonValueKind.Array && values.GetArrayLength() == count)
                source = (Array?)values.Deserialize(column.PropertyType, WorldSnapshotArchive.JsonOptions)
                    ?? throw new InvalidDataException($"属性列 {column.Name} 无效。");
            else
                throw new InvalidDataException($"属性列 {column.Name} 类型或长度错误。");
            if (source.Length != count) throw new InvalidDataException($"属性列 {column.Name} 长度错误。");
            if (source is float[] numbers && numbers.Any(value => !float.IsFinite(value)))
                throw new InvalidDataException($"属性列 {column.Name} 包含非有限值。");
            Array.Copy(source, (Array)column.GetValue(fields)!, count);
        }
        if (fields.Downslope.Any(id => id < -1 || id >= count))
            throw new InvalidDataException("下游地块编号越界。");
        return fields;
    }
}

internal sealed class CartographyDocument
{
    public required Dictionary<int, RegionStyle> Regions { get; init; }
    public required List<BrushInstruction> Brushes { get; init; }
    public required List<LandmarkStyle> Landmarks { get; init; }
    public required List<PainterCommand> Commands { get; init; }
    public PlannedRoadGraph? RoadGraph { get; init; }
    public DateTime CreatedAt { get; init; }
    public static CartographyDocument Capture(CartographySnapshot snapshot) => new()
    {
        Regions = snapshot.Regions, Brushes = snapshot.Brushes, Landmarks = snapshot.Landmarks,
        Commands = snapshot.Commands, RoadGraph = snapshot.RoadGraph, CreatedAt = snapshot.CreatedAt
    };
    public CartographySnapshot Restore()
    {
        if (Regions == null || Brushes == null || Landmarks == null || Commands == null)
            throw new InvalidDataException("制图内容不完整。");
        return new(Regions, Brushes, Landmarks, Commands, createdAt: CreatedAt, roadGraph: RoadGraph);
    }
}

internal sealed class WindDocument
{
    public int Width { get; init; }
    public int Height { get; init; }
    public required float[] X { get; init; }
    public required float[] Y { get; init; }
    public static WindDocument Capture((float X, float Y)[,] wind)
    {
        var width = wind.GetLength(0);
        var height = wind.GetLength(1);
        var x = new float[checked(width * height)];
        var y = new float[x.Length];
        for (var row = 0; row < height; row++)
            for (var column = 0; column < width; column++)
            {
                var index = row * width + column;
                (x[index], y[index]) = wind[column, row];
            }
        return new WindDocument { Width = width, Height = height, X = x, Y = y };
    }
    public (float X, float Y)[,] Restore()
    {
        var count = (long)Width * Height;
        if (Width <= 0 || Height <= 0 || count > 16_777_216 || X == null || Y == null || X.Length != count || Y.Length != count)
            throw new InvalidDataException("连续风场尺寸错误。");
        var wind = new (float X, float Y)[Width, Height];
        for (var row = 0; row < Height; row++)
            for (var column = 0; column < Width; column++)
                wind[column, row] = (X[row * Width + column], Y[row * Width + column]);
        return wind;
    }
}
