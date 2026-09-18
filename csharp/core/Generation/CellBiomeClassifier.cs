using System;
using PlanetGeneration.Core.Domain;

namespace PlanetGeneration.Core.Generation;

/// <summary>
/// 地块生物群系分类器。
/// 基于地块的真实高程、温度、湿度、河流流量与纬度进行多边形层级的群系归属。
/// </summary>
public static class CellBiomeClassifier
{
    public static void ClassifyAll(
        CellGeometry geometry,
        CellFields fields,
        float seaLevel,
        WorldTuningSnapshot tuning)
    {
        var count = geometry.Count;
        var height = fields.Height;
        var moisture = fields.Moisture;
        var temperature = fields.Temperature;
        var river = fields.River;
        var biome = fields.Biome;
        var worldHeight = geometry.Height;

        for (var cell = 0; cell < count; cell++)
        {
            var e = height[cell];
            var m = moisture[cell];
            var t = temperature[cell];
            var r = river[cell];

            var cy = geometry.CentroidY[cell];
            var latitude = worldHeight <= 0d ? 0f : (float)Math.Abs((2d * cy / worldHeight) - 1d);
            var polarBand = Math.Clamp((latitude - 0.76f) / 0.24f, 0f, 1f);

            biome[cell] = (byte)ClassifySingle(e, m, t, r, seaLevel, polarBand, tuning, geometry, fields, cell);
        }
    }

    public static BiomeType ClassifySingle(
        float e,
        float m,
        float t,
        float r,
        float seaLevel,
        float polarBand,
        WorldTuningSnapshot tuning,
        CellGeometry geometry,
        CellFields fields,
        int cell)
    {
        if (t < 0.03f && e < 0.65f && e >= seaLevel)
        {
            return BiomeType.Ice;
        }

        if (e < (tuning.DeepOceanFactor * seaLevel))
        {
            return BiomeType.Ocean;
        }

        if (e < seaLevel)
        {
            return BiomeType.ShallowOcean;
        }

        if (e < seaLevel + tuning.CoastBand)
        {
            return BiomeType.Coastland;
        }

        // 计算相邻地块局部高差
        var localRelief = ComputeCellRelief(geometry, fields, cell);
        var relativeHeight = (e - seaLevel) / Math.Max(1f - seaLevel, 0.0001f);
        var mountainFloor = tuning.MountainThreshold - 0.035f;
        var ridgeReliefThreshold = Math.Clamp(0.020f + (0.016f * Math.Clamp((relativeHeight - 0.45f) / 0.55f, 0f, 1f)), 0f, 1f);

        var isMountain = e >= tuning.MountainThreshold + 0.04f ||
                         (e >= mountainFloor && localRelief >= ridgeReliefThreshold);

        if (isMountain)
        {
            return t > 0.2f ? BiomeType.RockyMountain : BiomeType.SnowyMountain;
        }

        var effectiveMoisture = r > 0.02f
            ? Math.Clamp(m + (MathF.Sqrt(Math.Clamp(r, 0f, 1.5f)) * 0.28f), 0f, 1.2f)
            : m;

        if (polarBand > 0f)
        {
            var polarIceCutoff = 0.11f + (0.11f * polarBand);
            if (t <= polarIceCutoff)
            {
                return effectiveMoisture < 0.10f ? BiomeType.Tundra : BiomeType.Ice;
            }
        }

        if (t > 0.6f)
        {
            if (effectiveMoisture < 0.15f) return BiomeType.TropicalDesert;
            if (effectiveMoisture < seaLevel) return BiomeType.Savanna;
            if (effectiveMoisture < 0.5f) return BiomeType.Shrubland;
            if (effectiveMoisture < 0.75f) return BiomeType.TropicalSeasonalForest;
            return BiomeType.TropicalRainForest;
        }

        if (t > 0.3f)
        {
            if (effectiveMoisture < 0.15f) return BiomeType.TemperateDesert;
            if (effectiveMoisture < 0.35f) return BiomeType.Steppe;
            if (effectiveMoisture < 0.55f) return BiomeType.Grassland;
            if (effectiveMoisture < 0.75f) return BiomeType.TemperateSeasonalForest;
            return BiomeType.TemperateRainForest;
        }

        if (effectiveMoisture < 0.2f) return BiomeType.Tundra;
        if (effectiveMoisture < 0.45f) return BiomeType.Taiga;
        return BiomeType.BorealForest;
    }

    private static float ComputeCellRelief(CellGeometry geometry, CellFields fields, int cell)
    {
        var h = fields.Height[cell];
        var start = geometry.CellNeighborStart[cell];
        var end = geometry.CellNeighborStart[cell + 1];
        if (end <= start) return 0f;

        var maxDiff = 0f;
        for (var k = start; k < end; k++)
        {
            var neighbor = geometry.CellNeighbors[k];
            var diff = Math.Abs(h - fields.Height[neighbor]);
            if (diff > maxDiff) maxDiff = diff;
        }

        return maxDiff;
    }
}
