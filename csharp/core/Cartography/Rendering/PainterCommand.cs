using System.Collections.Generic;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 绘制命令结构（PainterCommand）。
/// 将特定图层的笔刷指令打包，具备层级优先级（LayerPriority）与不透明度配置。
/// </summary>
public sealed class PainterCommand
{
    /// <summary>所属图层标识（如 painter_mountain, painter_forest 等）。</summary>
    public string LayerId { get; set; } = string.Empty;

    /// <summary>绘制优先级层级（较小的值先画，较大的值后画）。</summary>
    public int LayerPriority { get; set; } = 0;

    /// <summary>图层整体不透明度调节系数。</summary>
    public float LayerOpacity { get; set; } = 1.0f;

    /// <summary>该批次下的笔刷指令集合。</summary>
    public List<BrushInstruction> Brushes { get; set; } = new();

    /// <summary>附加地标数据集合（仅在 Landmark/Label 图层有效）。</summary>
    public List<LandmarkStyle> Landmarks { get; set; } = new();

    public override string ToString() => $"PainterCommand[{LayerId}]: Priority={LayerPriority}, Brushes={Brushes.Count}, Landmarks={Landmarks.Count}";
}
