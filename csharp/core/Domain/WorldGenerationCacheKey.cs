using System.Globalization;
using System.Text;

namespace PlanetGeneration.Core.Domain;

/// <summary>
/// 世界参数的稳定标识。格式版本与算法版本分离；浮点数使用无损、区域无关的格式，
/// 文本值转义后不会混入字段分隔符。这里只包含世界输入，不包含显示分辨率或图层状态。
/// </summary>
public static class WorldGenerationCacheKey
{
    public const int FormatVersion = 2;

    /// <summary>A/B 会话身份，包含两组完整输入；头部仍保留主世界的尺寸与种子。</summary>
    public static string BuildSession(GenerationOptions primary, GenerationOptions? comparison = null)
    {
        var key = Build(primary);
        if (comparison == null) return key + "|compare:0";
        var comparisonHash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Build(comparison)));
        return key + "|compare:1|secondary:" + Convert.ToHexString(comparisonHash);
    }

    public static string Build(GenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var key = new StringBuilder(768);
        void Add(string name, params object[] values)
        {
            if (key.Length > 0) key.Append('|');
            key.Append(name).Append(':');
            for (var i = 0; i < values.Length; i++)
            {
                if (i > 0) key.Append(':');
                key.Append(values[i] switch
                {
                    float value => value.ToString("R", CultureInfo.InvariantCulture),
                    double value => value.ToString("R", CultureInfo.InvariantCulture),
                    bool value => value ? "1" : "0",
                    string value => Uri.EscapeDataString(value),
                    IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
                    _ => throw new ArgumentException("Unsupported cache-key value.")
                });
            }
        }

        Add("v", options.AlgorithmVersion);
        Add("key", FormatVersion);
        Add("seed", options.Seed);
        Add("cells", options.TargetCellCount);
        // 档案头部需要读取范围；保持明确的 ext:宽x高 格式。
        key.Append(CultureInfo.InvariantCulture, $"|ext:{options.Extent.Width:R}x{options.Extent.Height:R}");
        Add("sea", options.SeaLevel);
        Add("heat", options.HeatFactor);
        Add("moif", options.MoistureFactor);
        Add("riv", options.EnableRivers, options.RiverDensity);
        Add("ero", options.ErosionIterations);
        Add("moi", options.MoistureIterations);
        Add("wind", options.WindCellCount);
        Add("plt", options.PlateCount, options.OceanicRatio);
        Add("morph", (int)options.Morphology, options.ContinentCount, options.ContinentBias);
        Add("rel", options.InteriorRelief, options.OrogenyStrength, options.SubductionArcRatio, options.ContinentalAge);
        var tuning = options.Tuning;
        Add("tun", tuning.Name, tuning.DeepOceanFactor, tuning.CoastBand, tuning.MountainThreshold,
            tuning.RiverSourceElevationThreshold, tuning.RiverSourceMoistureThreshold, tuning.RiverSourceChance);
        Add("eco", options.SpeciesDiversity, options.CivilAggression, options.MagicDensity, options.Epoch);
        Add("basin", options.BasinSensitivity);
        var landform = options.GetEffectiveLandformTuning();
        Add("lf", landform.BasinSensitivity, landform.WetlandAbundance, landform.CanyonDepth,
            landform.DeltaScale, landform.KarstFrequency, landform.DesertDuneScale,
            landform.BadlandsFrequency, landform.GlacierExtent, landform.FjordDepth,
            landform.PeakFrequency, landform.IslandDensity, landform.VolcanoFrequency,
            landform.RiftFrequency, landform.PlateauExtent, landform.FloodplainScale);
        Add("cart", options.EnableCartographyDesigner, options.BlueprintName ?? "");
        return key.ToString();
    }
}
