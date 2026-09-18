using System;
using System.Security.Cryptography;
using System.Text;

namespace PlanetGeneration.Core.Domain;

/// <summary>世界微调参数快照。</summary>
public sealed record WorldTuningSnapshot
{
    public string Name { get; init; } = "Balanced";
    public float DeepOceanFactor { get; init; } = 0.60f;
    public float CoastBand { get; init; } = 0.022f;
    public float MountainThreshold { get; init; } = 0.70f;
    public float RiverSourceElevationThreshold { get; init; } = 0.57f;
    public float RiverSourceMoistureThreshold { get; init; } = 0.17f;
    public float RiverSourceChance { get; init; } = 0.0028f;

    public static WorldTuningSnapshot Balanced => new();
    public static WorldTuningSnapshot Legacy => new()
    {
        Name = "Legacy",
        DeepOceanFactor = 0.5714f,
        CoastBand = 0.027f,
        MountainThreshold = 0.68f,
        RiverSourceElevationThreshold = 0.55f,
        RiverSourceMoistureThreshold = 0.15f,
        RiverSourceChance = 0.0035f,
    };
}

/// <summary>
/// 不可变生成参数快照。
///
/// 生成任务开始时从 UI / 配置一次性捕获并冻结，
/// 保证生成流程中的计算与缓存键不受后续 UI 交互变化影响。
/// </summary>
public sealed record GenerationOptions
{
    public const int CurrentAlgorithmVersion = 5;
    public const int DefaultTargetCellCount = 10000;

    public int Seed { get; init; } = 123456;
    public int TargetCellCount { get; init; } = DefaultTargetCellCount;
    public WorldExtent Extent { get; init; } = WorldExtent.Default;

    public float SeaLevel { get; init; } = 0.45f;
    public float HeatFactor { get; init; } = 1.0f;
    public bool EnableRivers { get; init; } = true;
    public float RiverDensity { get; init; } = 1.0f;
    public int ErosionIterations { get; init; } = 4;
    public int MoistureIterations { get; init; } = 3;

    public int PlateCount { get; init; } = 18;
    public float OceanicRatio { get; init; } = 0.60f;
    public float ContinentBias { get; init; } = 0.50f;
    public float InteriorRelief { get; init; } = 1.0f;
    public float OrogenyStrength { get; init; } = 1.0f;
    public float SubductionArcRatio { get; init; } = 0.55f;
    public int ContinentalAge { get; init; } = 50;
    public TerrainMorphology Morphology { get; init; } = TerrainMorphology.Balanced;
    public int ContinentCount { get; init; } = 4;
    public WorldTuningSnapshot Tuning { get; init; } = WorldTuningSnapshot.Balanced;
    public int WindCellCount { get; init; } = 64;

    public float BasinSensitivity { get; init; } = 1.0f;
    public int SpeciesDiversity { get; init; } = 50;
    public int CivilAggression { get; init; } = 50;
    public int MagicDensity { get; init; } = 50;
    public int Epoch { get; init; } = 100;
    public int AlgorithmVersion { get; init; } = CurrentAlgorithmVersion;

    /// <summary>
    /// 计算稳定的世界生成缓存键（仅包含影响世界形态与属性的参数，
    /// 绝不包含显示分辨率、图层透明度或视图开关）。
    /// </summary>
    public string BuildCacheKey()
    {
        var sb = new StringBuilder(256);
        sb.Append($"v:{AlgorithmVersion}");
        sb.Append($"|seed:{Seed}");
        sb.Append($"|cells:{TargetCellCount}");
        sb.Append($"|ext:{Extent.Width:0}x{Extent.Height:0}");
        sb.Append($"|sea:{(int)MathF.Round(SeaLevel * 10000f)}");
        sb.Append($"|heat:{(int)MathF.Round(HeatFactor * 10000f)}");
        sb.Append($"|riv:{(EnableRivers ? 1 : 0)}:{(int)MathF.Round(RiverDensity * 10000f)}");
        sb.Append($"|ero:{ErosionIterations}");
        sb.Append($"|moi:{MoistureIterations}");
        sb.Append($"|plt:{PlateCount}:{(int)MathF.Round(OceanicRatio * 10000f)}");
        sb.Append($"|morph:{(int)Morphology}:{ContinentCount}:{(int)MathF.Round(ContinentBias * 10000f)}");
        sb.Append($"|rel:{(int)MathF.Round(InteriorRelief * 10000f)}:{(int)MathF.Round(OrogenyStrength * 10000f)}:{(int)MathF.Round(SubductionArcRatio * 10000f)}:{ContinentalAge}");
        sb.Append($"|tun:{Tuning.Name}");
        sb.Append($"|eco:{SpeciesDiversity}:{CivilAggression}:{MagicDensity}:{Epoch}");
        sb.Append($"|basin:{(int)MathF.Round(BasinSensitivity * 10000f)}");
        return sb.ToString();
    }
}
