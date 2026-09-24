using System;
using System.Collections.Generic;
using PlanetGeneration.Core.Geometry;

namespace PlanetGeneration.Core.Cartography.Design;

/// <summary>
/// 农田水网水浇田区域定义（AgriculturalZoneDefinition）。
/// </summary>
public sealed class AgriculturalZoneDefinition
{
    public string Name { get; init; } = "天府水浇田";
    public PolyVec2 Center { get; init; }
    public float RadiusX { get; init; } = 0.08f;
    public float RadiusY { get; init; } = 0.06f;
    public int PatchCount { get; init; } = 6;
}

/// <summary>
/// 中央平原微地貌与生活细节层设计定义（PlainsDetailDefinition）。
/// 
/// 彻底消除中央平原“死白空旷”，注入古代地图精湛的腹地景观肌理：
/// 1. 农田水网（AgriculturalZones: 都城与大江两岸的水墨水田/梯田符号 ////）；
/// 2. 散落独头小孤丘（SolitaryKnolls: 清秀小巧的平原孤峰 /\，不压平原）；
/// 3. 清平水泽镜湖（MirrorLakes: 汇水内陆湖 ~~~，四周留白）；
/// 4. 通风草甸波纹（GrassWaveZones: 稀疏优雅的风草微纹 ~~~~~~，赋予宣纸呼吸感）。
/// </summary>
public sealed class PlainsDetailDefinition
{
    public int Id { get; init; } = 3;
    public string Name { get; init; } = "中原天府平原细节层";

    /// <summary>平原腹地中心坐标（归一化 [0, 1]）。</summary>
    public PolyVec2 HeartlandCenter { get; init; } = new(0.48, 0.50);

    /// <summary>农田水网聚集区列表。</summary>
    public List<AgriculturalZoneDefinition> AgriculturalZones { get; init; } = new();

    /// <summary>散落独头小孤丘坐标列表（归一化 [0, 1]）。</summary>
    public List<PolyVec2> SolitaryKnolls { get; init; } = new()
    {
        new(0.41, 0.54),
        new(0.58, 0.44),
        new(0.52, 0.62)
    };

    /// <summary>清平镜湖坐标列表（归一化 [0, 1]）。</summary>
    public List<PolyVec2> MirrorLakes { get; init; } = new()
    {
        new(0.44, 0.43),
        new(0.55, 0.58)
    };

    /// <summary>风草波纹走廊中心坐标列表（归一化 [0, 1]）。</summary>
    public List<PolyVec2> GrassWaveZones { get; init; } = new()
    {
        new(0.46, 0.53),
        new(0.52, 0.48)
    };
}
