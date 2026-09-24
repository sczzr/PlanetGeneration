using System;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 森林多层生态渐变配置 (ForestTransitionProfile)。
/// 
/// 遵循 ForestCore(100%树) -> ForestEdge(30%树+70%草) -> Grassland 的平滑过渡，
/// 消除方块贴图裁切硬边，赋予手绘地图自然生态呼吸感。
/// </summary>
public sealed class ForestTransitionProfile
{
    /// <summary>密林核心区归一化深度阈值（高于此值 100% 树木密度，放置宏观水墨林冠）。</summary>
    public float CoreDepthThreshold { get; init; } = 0.20f;

    /// <summary>林缘过渡区最低深度阈值（低于此值停止树木生成，转为风草草甸缓冲）。</summary>
    public float EdgeDepthThreshold { get; init; } = 0.05f;

    /// <summary>林缘过渡区的树木稀疏系数。</summary>
    public float EdgeTreeSpawnRatio { get; init; } = 0.85f;

    /// <summary>过渡区单木/微丛优先比例（杜绝边缘出现大块林冠贴图）。</summary>
    public float SingleTreePreference { get; init; } = 0.50f;

    /// <summary>核心林冠簇采样步长（像素）。与 ~10px 林冠图元形成紧密咬合重叠。</summary>
    public float CoreClusterStep { get; init; } = 5.5f;

    /// <summary>过渡区单木采样步长（像素）。</summary>
    public float EdgeTreeStep { get; init; } = 4.5f;
}
