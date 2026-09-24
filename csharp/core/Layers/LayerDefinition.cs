using System;

namespace PlanetGeneration.Core.Layers;

/// <summary>图层类别：底图互斥单选，叠加层独立多选。</summary>
public enum LayerCategory
{
    BaseTheme = 0,
    Overlay = 1,
    PainterLayer = 2,
}

/// <summary>图层绘制层级带，保证几何图层间正确的遮挡与层叠关系。</summary>
public enum LayerDrawBand
{
    BaseMesh = 0,    // 多边形面填充
    Lines = 1,       // 线状要素（河网、边界、网格线、走廊）
    Symbols = 2,     // 点状符号（城市标记、风向箭头）
    Labels = 3,      // 文本标注（城市名称）
    Interaction = 4, // 交互覆盖（选中高亮环）
}

/// <summary>图层定义规范。</summary>
public sealed record LayerDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string GroupName { get; init; }
    public required LayerCategory Category { get; init; }
    public required LayerDrawBand DrawBand { get; init; }
    public bool IsDefaultActive { get; init; }
    public float DefaultOpacity { get; init; } = 1.0f;
    public float DefaultWidthOrSize { get; init; } = 1.0f;
    public string[] DataDependencies { get; init; } = Array.Empty<string>();
}
