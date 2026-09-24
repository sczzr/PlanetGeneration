using System;
using PlanetGeneration.Core.Domain;

namespace PlanetGeneration.Application;

/// <summary>可准确计数的基础数组字节量；不包含引用对象、索引和制图图元的堆开销。</summary>
internal static class SnapshotBufferSize
{
    public static long Measure(WorldSnapshot snapshot)
    {
        static long Columns(object value)
        {
            long bytes = 0;
            foreach (var property in value.GetType().GetProperties())
                if (property.PropertyType.IsArray && property.GetValue(value) is Array array)
                    bytes += Buffer.ByteLength(array);
            return bytes;
        }
        return Columns(snapshot.Geometry) + Columns(snapshot.Fields)
            + (snapshot.ContinuousWind?.LongLength ?? 0) * sizeof(float) * 2;
    }
}
