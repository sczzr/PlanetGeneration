using System.Collections.Generic;
using PlanetGeneration.Core.Domain;

namespace PlanetGeneration.Core.Generation;

/// <summary>基础板块场输出。</summary>
public sealed class BasePlateField
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int[,] PlateIds { get; init; }
    public required PlateBoundaryType[,] BoundaryTypes { get; init; }
    public required List<PlateSiteInfo> Sites { get; init; }
}

/// <summary>连续物理场集合。</summary>
public sealed class BaseContinuousFields
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required BasePlateField Plates { get; init; }
    public required float[,] Elevation { get; init; }
    public required float[,] Temperature { get; init; }
    public required float[,] Moisture { get; init; }
    public required (float X, float Y)[,] Wind { get; init; }
    public required byte[,] Rock { get; init; }
    public required byte[,] Ore { get; init; }
    public byte[,]? IndustrialOre { get; init; }
    public byte[,]? SupernaturalOre { get; init; }
    public byte[,]? CardOre { get; init; }
    public byte[,]? Leyline { get; init; }
}

/// <summary>
/// 基础连续场生成器适配契约。
/// </summary>
public interface IBaseFieldGenerator
{
    BaseContinuousFields GenerateFields(GenerationOptions options, int fieldWidth, int fieldHeight);
}
