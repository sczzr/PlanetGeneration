using System;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PlanetGeneration.Application;

/// <summary>无需反序列化地图数组即可读取的档案摘要，兼容迁移前后的缓存键。</summary>
internal readonly record struct WorldArchiveHeader(int Seed, int Width, int Height, bool CompareMode)
{
    public static bool TryParse(string header, out WorldArchiveHeader summary)
    {
        summary = default;
        try
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(header), isFinalBlock: false, state: default);
            string? key = null;
            bool? compare = null;
            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1) continue;
                var name = reader.GetString();
                // Json.Stringify 按键名排序；遇到大数组所属的 primary 即停止读取。
                if (name == "primary") break;
                if (!reader.Read()) break;
                if (name == "cache_key" && reader.TokenType == JsonTokenType.String)
                    key = reader.GetString();
                else if (name == "compare_mode" && reader.TokenType is JsonTokenType.True or JsonTokenType.False)
                    compare = reader.GetBoolean();
            }
            if (key == null) return false;
            int? seed = null;
            var width = 0;
            var height = 0;
            foreach (var part in key.Split('|'))
            {
                if (part.StartsWith("seed:", StringComparison.Ordinal) &&
                    int.TryParse(part.AsSpan(5), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSeed))
                    seed = parsedSeed;
                if (part == "compare:1") compare ??= true;
                var dimensions = part.StartsWith("ext:", StringComparison.Ordinal) ? part.Substring(4) : part;
                var separator = dimensions.IndexOf('x');
                if (separator > 0 &&
                    int.TryParse(dimensions.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var w) &&
                    int.TryParse(dimensions.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var h))
                {
                    width = w;
                    height = h;
                }
            }
            if (seed == null || width <= 0 || height <= 0) return false;
            summary = new WorldArchiveHeader(seed.Value, width, height, compare ?? false);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
