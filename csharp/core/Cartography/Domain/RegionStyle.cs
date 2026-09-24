using System;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 区域艺术风格与视觉规则（RegionStyle）。
/// 
/// 赋予每一个地理分区（巨型山系、林海、盆地、旷野等）独立的艺术身份，
/// 控制该区域内部图元排布的密度、形体夸张程度、专属色彩基调与氛围烟霭。
/// </summary>
public sealed class RegionStyle
{
    public int RegionId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>整体幻想地形风格。</summary>
    public TerrainStyle Style { get; set; } = TerrainStyle.Plain;

    /// <summary>主调色体系（主色）。</summary>
    public CartographyColor Palette { get; set; } = CartographyColor.EmeraldGreen;

    /// <summary>辅助阴影或副调色。</summary>
    public CartographyColor SecondaryColor { get; set; } = CartographyColor.InkCharcoal;

    /// <summary>细节与图元密度系数（0.2 ~ 2.0，默认 1.0）。</summary>
    public float Density { get; set; } = 1.0f;

    /// <summary>艺术夸张程度（0.5 ~ 2.5，默认 1.0）。山峦更高、林冠更阔。</summary>
    public float Exaggeration { get; set; } = 1.0f;

    /// <summary>是否在该区域产生烟霭流岚。</summary>
    public bool Fog { get; set; } = false;

    /// <summary>烟霭雾气浓度（0.0 ~ 1.0）。</summary>
    public float MistDensity { get; set; } = 0.35f;

    /// <summary>雾气主色调。</summary>
    public CartographyColor MistColor { get; set; } = CartographyColor.MistIvory;

    public override string ToString() => $"RegionStyle[{RegionId}]: {Name} ({Style}, Density={Density:F2}, Exagg={Exaggeration:F2}, Fog={Fog})";
}
