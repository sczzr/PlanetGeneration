using System;
using PlanetGeneration.Core.Domain;

namespace PlanetGeneration.Core.Generation;

/// <summary>
/// 内部连续场采样精度策略。
/// 采样分辨率由地块数量反推标定，完全与前端视口/导出分辨率解耦。
/// </summary>
public static class FieldSamplingPolicy
{
    public static (int Width, int Height) ResolveInternalResolution(GenerationOptions options)
    {
        var targetCells = options.TargetCellCount;
        if (targetCells <= 3000)
        {
            return (512, 256);
        }

        if (targetCells <= 20000)
        {
            return (1024, 512);
        }

        return (2048, 1024);
    }
}
