using System;
using System.Threading;
using System.Threading.Tasks;
using PlanetGeneration.Core.Application;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Generation;

namespace PlanetGeneration.Application;

internal sealed record GeneratedWorldPair(GeneratedWorldData Primary, GeneratedWorldData? Comparison);

/// <summary>生成快照并构建旧界面投影；每组世界只调用一次领域生成服务。</summary>
internal sealed class SnapshotGenerationController
{
    private readonly WorldGenerationService _generation;
    public SnapshotGenerationController(IBaseFieldGenerator fieldGenerator) => _generation = new(fieldGenerator);

    public async Task<GeneratedWorldPair> GenerateAsync(
        GenerationOptions primaryOptions, GenerationOptions? comparisonOptions,
        IProgress<(float Progress, string Stage)>? progress = null, CancellationToken cancellationToken = default)
    {
        async Task<GeneratedWorldData> GenerateOne(GenerationOptions options, float offset, float scale, string label)
        {
            var reporter = new StageProgress(p => progress?.Report((offset + p.Progress * scale, $"{label}: {p.Stage}")));
            var snapshot = await _generation.GenerateAsync(options, reporter, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(() => SnapshotWorldProjection.Create(snapshot), cancellationToken);
        }
        var primary = await GenerateOne(primaryOptions, 0, comparisonOptions == null ? 0.95f : 0.46f, "A组");
        var comparison = comparisonOptions == null ? null : await GenerateOne(comparisonOptions, 48, 0.46f, "B组");
        progress?.Report((100, "生成与投影完成"));
        return new GeneratedWorldPair(primary, comparison);
    }

    // 同步转发阶段报告；只让最外层 Progress<T> 负责切回 UI，不引入嵌套 async void。
    private sealed class StageProgress(Action<(float Progress, string Stage)> report) : IProgress<(float Progress, string Stage)>
    {
        public void Report((float Progress, string Stage) value) => report(value);
    }
}
