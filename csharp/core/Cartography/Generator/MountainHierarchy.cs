using System;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 山体金字塔三级层级。
/// 严格控制比例：主峰 10%、中山与连脊 30%、低山丘陵与漫坡 60%。
/// </summary>
public enum MountainTier
{
    /// <summary>天柱主峰 / 绝顶孤峰（占 10%）。</summary>
    DominantPeak = 0,

    /// <summary>中山躯干 / 连脊与伴峰（占 30%）。</summary>
    SecondarySpine = 1,

    /// <summary>山麓余脉 / 连绵缓丘（占 60%）。</summary>
    LowFoothill = 2
}

/// <summary>
/// 山脉绘制与密度控制配置参数 (MountainDensityProfile)。
/// 遵循“山脉是地理骨架而非主体填充物”的原则，实现 30% 密度瘦身与留白控制。
/// </summary>
public sealed class MountainDensityProfile
{
    /// <summary>全图山峰生成基准密度缩减因子（0.85 保持疏朗但骨架连贯）。</summary>
    public float GlobalDensityMultiplier { get; init; } = 0.85f;

    /// <summary>主脊平滑样条采样步长（像素，16f 确保相邻山脊能够自然重叠搭接）。</summary>
    public float SpineSampleDistance { get; init; } = 16.0f;

    /// <summary>主峰互斥最小距离（像素，确保主峰不扎堆，突显孤高气势）。</summary>
    public float DominantPeakMinDistance { get; init; } = 45.0f;

    /// <summary>伴峰互斥最小距离（像素）。</summary>
    public float SecondaryPeakMinDistance { get; init; } = 22.0f;

    /// <summary>连脊互斥最小距离（像素，18f 产生 30% 重叠形成连续岩石脊梁）。</summary>
    public float RidgeMinDistance { get; init; } = 18.0f;

    /// <summary>缓丘互斥最小距离（像素）。</summary>
    public float HillMinDistance { get; init; } = 20.0f;

    /// <summary>主峰目标配比（10%）。</summary>
    public float DominantRatio { get; init; } = 0.10f;

    /// <summary>中山与连脊目标配比（30%）。</summary>
    public float SecondaryRatio { get; init; } = 0.30f;

    /// <summary>低山丘陵目标配比（60%）。</summary>
    public float LowHillRatio { get; init; } = 0.60f;

    /// <summary>山体腹地填补概率（仅高海拔开阔区微量点缀）。</summary>
    public float InteriorInfillProbability { get; init; } = 0.05f;
}
