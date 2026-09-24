using System;
using System.Threading.Tasks;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Simulation;

namespace PlanetGeneration.Core.Application;

/// <summary>
/// 纪元推进与模拟重算服务。
/// 当用户仅调节时间轴（纪元）、物种多样性、侵略性或魔法密度时，
/// 复用已有地理几何与物理场，仅重新运行生态与文明模拟并发布新快照。
/// </summary>
public sealed class WorldSimulationService
{
    public Task<WorldSnapshot> ReSimulateEpochAsync(
        WorldSnapshot baseSnapshot,
        int newEpoch,
        int? speciesDiversity = null,
        int? civilAggression = null,
        int? magicDensity = null)
    {
        return Task.Run(() => ReSimulateEpoch(baseSnapshot, newEpoch, speciesDiversity, civilAggression, magicDensity));
    }

    /// <summary>同步领域运算；调用方需要后台执行时使用 ReSimulateEpochAsync。</summary>
    public WorldSnapshot ReSimulateEpoch(
        WorldSnapshot baseSnapshot, int newEpoch, int? speciesDiversity = null,
        int? civilAggression = null, int? magicDensity = null)
    {
        ArgumentNullException.ThrowIfNull(baseSnapshot);
        var options = baseSnapshot.Options with
        {
            Epoch = newEpoch,
            SpeciesDiversity = speciesDiversity ?? baseSnapshot.Options.SpeciesDiversity,
            CivilAggression = civilAggression ?? baseSnapshot.Options.CivilAggression,
            MagicDensity = magicDensity ?? baseSnapshot.Options.MagicDensity,
        };

        var fields = baseSnapshot.Fields.Clone();

        var ecology = PolygonEcologySimulator.Simulate(
            baseSnapshot.Geometry,
            fields,
            options.Seed,
            options.Epoch,
            options.SpeciesDiversity,
            options.CivilAggression,
            options.MagicDensity,
            options.SeaLevel);

        var civilization = PolygonCivilizationSimulator.Simulate(
            baseSnapshot.Geometry,
            fields,
            options.Seed,
            options.Epoch,
            options.CivilAggression,
            options.SpeciesDiversity,
            options.SeaLevel);

        return baseSnapshot.WithSimulation(options, fields, ecology, civilization);
    }
}
