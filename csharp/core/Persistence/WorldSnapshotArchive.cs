using System.Text.Json;
using System.Text.Json.Serialization;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Persistence;

public sealed record SnapshotArchiveContent(string CacheKey, WorldSnapshot Primary, WorldSnapshot? Comparison);

/// <summary>
/// 引擎无关的快照档案。记录世界事实与制图指令，不保存像素归属缓存和 Godot 对象。
/// 文件使用同目录临时文件原子替换；保存失败不会破坏旧档案。
/// </summary>
public static class WorldSnapshotArchive
{
    public const int FormatVersion = 1;
    internal static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static void Save(string path, WorldSnapshot primary, WorldSnapshot? comparison = null)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                Write(file, primary, comparison);
                file.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public static void Write(Stream stream, WorldSnapshot primary, WorldSnapshot? comparison = null)
    {
        ArgumentNullException.ThrowIfNull(primary);
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteNumber("snapshot_format", FormatVersion);
        writer.WriteString("cache_key", WorldGenerationCacheKey.BuildSession(primary.Options, comparison?.Options));
        writer.WriteBoolean("compare_mode", comparison != null);
        writer.WritePropertyName("primary");
        JsonSerializer.Serialize(writer, SnapshotDocument.Capture(primary), JsonOptions);
        writer.WritePropertyName("comparison");
        JsonSerializer.Serialize(writer, comparison == null ? null : SnapshotDocument.Capture(comparison), JsonOptions);
        writer.WriteEndObject();
    }

    public static SnapshotArchiveContent Load(string path)
    {
        using var file = File.OpenRead(path);
        return Read(file);
    }

    public static SnapshotArchiveContent Read(Stream stream)
    {
        using var json = JsonDocument.Parse(stream);
        var root = json.RootElement;
        if (!root.TryGetProperty("snapshot_format", out var version) || version.GetInt32() != FormatVersion)
            throw new InvalidDataException("不支持的快照档案版本。");
        var primary = root.GetProperty("primary").Deserialize<SnapshotDocument>(JsonOptions)?.Restore()
            ?? throw new InvalidDataException("快照档案缺少主世界。");
        var comparisonData = root.GetProperty("comparison");
        var comparison = comparisonData.ValueKind == JsonValueKind.Null ? null
            : comparisonData.Deserialize<SnapshotDocument>(JsonOptions)?.Restore()
                ?? throw new InvalidDataException("对比世界内容无效。");
        var key = root.GetProperty("cache_key").GetString();
        var expected = WorldGenerationCacheKey.BuildSession(primary.Options, comparison?.Options);
        if (key != expected || root.GetProperty("compare_mode").GetBoolean() != (comparison != null))
            throw new InvalidDataException("档案头部与世界参数不一致。");
        return new SnapshotArchiveContent(expected, primary, comparison);
    }

    /// <summary>仅探测头部。旧栅格档案由旧导入器处理，不对大型旧文件重复反序列化。</summary>
    public static bool HasSnapshotHeader(string path)
    {
        using var file = File.OpenRead(path);
        var buffer = new byte[4096];
        var length = file.Read(buffer);
        var reader = new Utf8JsonReader(buffer.AsSpan(0, length), isFinalBlock: false, state: default);
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1) continue;
            if (reader.ValueTextEquals("snapshot_format")) return true;
            if (reader.ValueTextEquals("primary")) return false;
        }
        return false;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new PolyVec2Converter());
        return options;
    }

    private sealed class PolyVec2Converter : JsonConverter<PolyVec2>
    {
        public override PolyVec2 Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray || !reader.Read()) throw new JsonException("二维坐标必须是数组。");
            var x = reader.GetDouble();
            if (!reader.Read()) throw new JsonException();
            var y = reader.GetDouble();
            if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray || !double.IsFinite(x) || !double.IsFinite(y))
                throw new JsonException("二维坐标无效。");
            return new PolyVec2(x, y);
        }
        public override void Write(Utf8JsonWriter writer, PolyVec2 value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.X);
            writer.WriteNumberValue(value.Y);
            writer.WriteEndArray();
        }
    }
}
