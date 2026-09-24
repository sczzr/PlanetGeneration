using System;

namespace PlanetGeneration.Core.Domain;

/// <summary>
/// 25 种地球典型地貌分类微调配置（相对基准乘数，默认 1.0f）。
/// </summary>
public readonly record struct LandformOptions(
    float BasinSensitivity = 1.0f,
    float WetlandAbundance = 1.0f,
    float CanyonDepth = 1.0f,
    float DeltaScale = 1.0f,
    float KarstFrequency = 1.0f,
    float DesertDuneScale = 1.0f,
    float BadlandsFrequency = 1.0f,
    float GlacierExtent = 1.0f,
    float FjordDepth = 1.0f,
    float PeakFrequency = 1.0f,
    float IslandDensity = 1.0f,
    float VolcanoFrequency = 1.0f,
    float RiftFrequency = 1.0f,
    float PlateauExtent = 1.0f,
    float FloodplainScale = 1.0f
)
{
    public LandformOptions() : this(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f)
    {
    }

    public static readonly LandformOptions Default = new(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f);
}

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
    /// <summary>
    /// 6 → 7：矿产资源体系重构为三大类28种资源（工业11种/超自然10种/卡牌7种）与「天地灵凡」四阶，
    /// 旧缓存里按旧整数存的 ore 会串成别的矿种，必须整体作废。
    /// </summary>
    // 7 → 8：旧多边形入口统一到 Core，修复旧网格接缝与河网顺序，并补全 tuning 映射。
    // 8 → 9：主界面只生成 Core 快照，旧栅格数据改为单向投影；自动缓存保存完整快照。
    public const int CurrentAlgorithmVersion = 9;
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
    public float MoistureFactor { get; init; } = 1.0f;

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
    public LandformOptions LandformTuning { get; init; } = LandformOptions.Default;

    public LandformOptions GetEffectiveLandformTuning()
    {
        if (LandformTuning.BasinSensitivity == 1.0f && BasinSensitivity != 1.0f)
        {
            return LandformTuning with { BasinSensitivity = BasinSensitivity };
        }
        return LandformTuning;
    }

    public int SpeciesDiversity { get; init; } = 50;
    public int CivilAggression { get; init; } = 50;
    public int MagicDensity { get; init; } = 50;
    public int Epoch { get; init; } = 100;
    public int AlgorithmVersion { get; init; } = CurrentAlgorithmVersion;

    /// <summary>是否启用地图构图导演层（CartographyDesigner）。</summary>
    public bool EnableCartographyDesigner { get; init; } = false;

    /// <summary>指定构图蓝图名称（为 null 时走纯程序化自然演化管线）。</summary>
    public string? BlueprintName { get; init; } = null;

    /// <summary>
    /// 计算稳定的世界生成缓存键（仅包含影响世界形态与属性的参数，
    /// 绝不包含显示分辨率、图层透明度或视图开关）。
    /// </summary>
    public string BuildCacheKey() => WorldGenerationCacheKey.Build(this);
}
