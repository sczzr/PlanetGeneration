using System;
using PlanetGeneration.Core.Domain;

namespace PlanetGeneration.Core.Simulation;

/// <summary>
/// 地块地貌分类器（纯领域无引擎依赖）。
/// 基于多边形真实邻接与局部起伏分析高程形态。
/// </summary>
public static class PolygonLandformClassifier
{
    private const float NeighborHeightDelta = 0.018f;

    public static LandformType Classify(
        CellGeometry geometry,
        CellFields fields,
        int cellId,
        float seaLevel,
        float basinSensitivity)
    {
        var safeSea = Math.Clamp(seaLevel, 0.0001f, 0.9999f);
        var sensitivity = Math.Clamp(basinSensitivity, 0.5f, 2.0f);
        var current = fields.Height[cellId];

        if (current < safeSea)
        {
            var depth = (safeSea - current) / Math.Max(safeSea, 0.0001f);
            return depth > 0.45f ? LandformType.DeepOcean : LandformType.Ocean;
        }

        var relativeHeight = (current - safeSea) / Math.Max(1f - safeSea, 0.0001f);

        var start = geometry.CellNeighborStart[cellId];
        var end = geometry.CellNeighborStart[cellId + 1];
        var neighborCount = end - start;
        if (neighborCount == 0)
        {
            return LandformType.Plain;
        }

        var minHeight = current;
        var maxHeight = current;
        var sum = 0f;
        var higherCount = 0;
        var lowerCount = 0;
        var nearSea = false;

        for (var k = start; k < end; k++)
        {
            var neighbor = geometry.CellNeighbors[k];
            var height = fields.Height[neighbor];

            if (height < minHeight) minHeight = height;
            if (height > maxHeight) maxHeight = height;
            sum += height;

            if (height <= safeSea) nearSea = true;

            var diff = height - current;
            if (diff > NeighborHeightDelta) higherCount++;
            else if (diff < -NeighborHeightDelta) lowerCount++;
        }

        var meanHeight = sum / neighborCount;
        var localRelief = maxHeight - minHeight;
        var depression = Math.Max(meanHeight - current, 0f);
        var slopeSignal = Math.Max(maxHeight - current, current - minHeight);
        var normalizedSensitivity = (sensitivity - 0.5f) / 1.5f;

        var higherFraction = higherCount / (float)neighborCount;
        var lowerFraction = lowerCount / (float)neighborCount;
        var enclosedByHigher = higherFraction >= Lerp(5f / 8f, 7f / 8f, normalizedSensitivity)
            && lowerFraction <= Lerp(2f / 8f, 0f, normalizedSensitivity);

        var basinHeightThreshold = Lerp(0.32f, 0.18f, normalizedSensitivity);
        var basinMoistureThreshold = Lerp(0.42f, 0.55f, normalizedSensitivity);
        var basinRiverThreshold = Lerp(0.02f, 0.05f, normalizedSensitivity);

        if (relativeHeight < basinHeightThreshold
            && enclosedByHigher
            && (fields.Moisture[cellId] > basinMoistureThreshold
                || fields.River[cellId] > basinRiverThreshold
                || depression > 0.009f))
        {
            return LandformType.Basin;
        }

        var dryBasinHeightThreshold = basinHeightThreshold * 1.15f;
        if (relativeHeight < dryBasinHeightThreshold
            && enclosedByHigher
            && fields.Moisture[cellId] < 0.32f
            && fields.River[cellId] < 0.03f
            && depression > 0.006f)
        {
            return LandformType.Basin;
        }

        if (nearSea && relativeHeight < 0.12f)
        {
            return LandformType.Coast;
        }

        if (relativeHeight < 0.30f)
        {
            return LandformType.Plain;
        }

        if (relativeHeight > 0.78f
            || (relativeHeight > 0.68f && (localRelief > 0.045f || slopeSignal > 0.050f)))
        {
            return LandformType.Mountain;
        }

        if (relativeHeight > 0.64f && localRelief < 0.026f)
        {
            return LandformType.Plateau;
        }

        if (relativeHeight > 0.52f || localRelief > 0.030f)
        {
            return LandformType.Hill;
        }

        return LandformType.Plain;
    }

    public static void ClassifyAll(
        CellGeometry geometry,
        CellFields fields,
        float seaLevel,
        float basinSensitivity,
        byte[] destination)
    {
        var count = geometry.Count;
        for (var cell = 0; cell < count; cell++)
        {
            destination[cell] = (byte)Classify(geometry, fields, cell, seaLevel, basinSensitivity);
        }
    }

    private static float Lerp(float from, float to, float t) => from + ((to - from) * Math.Clamp(t, 0f, 1f));
}
