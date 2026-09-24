using System;
using PlanetGeneration.Core.Domain;

namespace PlanetGeneration.WorldGen.Polygon;

/// <summary>
/// 地貌类型。对齐 25 种典型地球地貌。
/// </summary>
public enum PolygonLandform : byte
{
    Ocean = 0,
    DeepOcean = 1,
    Trench = 2,
    Coast = 3,
    Plain = 4,
    Basin = 5,
    Plateau = 6,
    Hill = 7,
    Mountain = 8,
    Volcano = 9,
    Island = 10,
    ShallowOcean = 11,
    MidOceanRidge = 12,
    Floodplain = 13,
    Delta = 14,
    Canyon = 15,
    DryBasin = 16,
    Peak = 17,
    RiftValley = 18,
    Karst = 19,
    DesertDune = 20,
    Badlands = 21,
    Glacier = 22,
    Fjord = 23,
    Wetland = 24,
}

/// <summary>
/// 地块地貌分类：用**真实的多边形邻接**（<c>Cells.C</c>）替代栅格版的 8 邻域像素统计。
/// </summary>
public static class PolygonLandformClassifier
{
    public static PolygonLandform Classify(PolygonGrid grid, int cellId, float seaLevel, float basinSensitivity)
        => (PolygonLandform)Core.Simulation.PolygonLandformClassifier.Classify(
            grid.Geometry, grid.Fields, cellId, seaLevel, basinSensitivity);

    public static PolygonLandform Classify(PolygonGrid grid, int cellId, float seaLevel, LandformOptions options)
        => (PolygonLandform)Core.Simulation.PolygonLandformClassifier.Classify(
            grid.Geometry, grid.Fields, cellId, seaLevel, options);

    public static void ClassifyAll(PolygonGrid grid, float seaLevel, float basinSensitivity, byte[] destination)
        => Core.Simulation.PolygonLandformClassifier.ClassifyAll(
            grid.Geometry, grid.Fields, seaLevel, basinSensitivity, destination);

    public static void ClassifyAll(PolygonGrid grid, float seaLevel, LandformOptions options, byte[] destination)
        => Core.Simulation.PolygonLandformClassifier.ClassifyAll(
            grid.Geometry, grid.Fields, seaLevel, options, destination);
}
