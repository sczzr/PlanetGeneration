using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PlanetGeneration.LLM;

public sealed class LlmRuntimeProfile
{
    public required long SystemMemoryGB { get; init; }
    public required string GpuName { get; init; }
    public required int? GpuMemoryMB { get; init; }
    public required bool BackendSupportsGpuOffload { get; init; }
    public required bool UseGpuOffload { get; init; }
    public required int ContextSize { get; init; }
    public required int GpuLayerCount { get; init; }
    public required string DecisionReason { get; init; }

    public string RuntimeLabel => UseGpuOffload
        ? $"GPU({GpuLayerCount}层)"
        : "CPU";

    public LlmLoadOptions ToLoadOptions()
    {
        return new LlmLoadOptions
        {
            ContextSize = ContextSize,
            GpuLayerCount = UseGpuOffload ? Math.Max(0, GpuLayerCount) : 0,
            UseMemorymap = false,
            UseMemoryLock = false,
            AllowGpuFallbackToCpu = true
        };
    }
}

public sealed class LlmModelCandidate
{
    public required string FullPath { get; init; }
    public required string FileName { get; init; }
    public required long FileSizeBytes { get; init; }
    public required double FileSizeGB { get; init; }
    public required double? ParamsBillions { get; init; }
}

public static class LlmRuntimeSelector
{
    private static readonly Regex ParamSizeRegex = new(@"(?<!\d)(\d+(?:\.\d+)?)\s*[bB](?![a-zA-Z])", RegexOptions.Compiled);
    private static readonly string[] GpuBackendMarkers =
    {
        "backend.cuda",
        "backend.vulkan",
        "backend.metal",
        "backend.opencl"
    };
    private static readonly string[] GpuBackendNameHints =
    {
        "vulkan",
        "cuda",
        "metal",
        "opencl"
    };
    private static readonly string[] GpuNativeBackendFolders =
    {
        "vulkan",
        "cuda",
        "metal",
        "opencl"
    };

    public static LlmRuntimeProfile BuildRuntimeProfile(long systemMemoryGB)
    {
        var gpuProbe = LocalHardwareProbe.ProbeGpu();
        var backendSupportsGpu = DetectGpuBackendSupport();

        var useGpu = backendSupportsGpu && gpuProbe.HasGpu;
        var contextSize = systemMemoryGB >= 28
            ? 4096
            : systemMemoryGB >= 16
                ? 3072
                : 2048;

        var gpuLayerCount = 0;
        if (useGpu)
        {
            var layersByMemory = systemMemoryGB switch
            {
                >= 32 => 64,
                >= 24 => 48,
                >= 16 => 32,
                >= 12 => 20,
                _ => 12
            };

            var layersByVram = gpuProbe.VramMB switch
            {
                >= 16384 => 64,
                >= 12288 => 48,
                >= 8192 => 32,
                >= 6144 => 24,
                >= 4096 => 16,
                > 0 => 8,
                _ => 20
            };

            gpuLayerCount = Math.Min(layersByMemory, layersByVram);
            if (systemMemoryGB < 12)
            {
                gpuLayerCount = 0;
            }
        }

        if (gpuLayerCount <= 0)
        {
            useGpu = false;
        }

        var reason = useGpu
            ? "检测到可用 GPU 与后端，启用分层卸载。"
            : !backendSupportsGpu
                ? "未检测到 GPU 后端包，回退 CPU 推理。"
                : !gpuProbe.HasGpu
                    ? "未检测到可用 GPU，回退 CPU 推理。"
                    : "硬件预算不足，回退 CPU 推理。";

        return new LlmRuntimeProfile
        {
            SystemMemoryGB = Math.Max(1, systemMemoryGB),
            GpuName = gpuProbe.Name,
            GpuMemoryMB = gpuProbe.VramMB,
            BackendSupportsGpuOffload = backendSupportsGpu,
            UseGpuOffload = useGpu,
            ContextSize = contextSize,
            GpuLayerCount = useGpu ? gpuLayerCount : 0,
            DecisionReason = reason
        };
    }

    public static IReadOnlyList<LlmModelCandidate> DiscoverModelCandidates(IEnumerable<string> searchPaths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<LlmModelCandidate>();

        foreach (var searchPath in searchPaths)
        {
            if (string.IsNullOrWhiteSpace(searchPath))
            {
                continue;
            }

            var resolved = ResolveSearchPath(searchPath);
            if (string.IsNullOrWhiteSpace(resolved) || !Directory.Exists(resolved))
            {
                continue;
            }

            foreach (var modelPath in Directory.EnumerateFiles(resolved, "*.gguf", SearchOption.TopDirectoryOnly))
            {
                if (!seen.Add(modelPath))
                {
                    continue;
                }

                var fileName = Path.GetFileName(modelPath);
                if (fileName.IndexOf("embedding", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                var fileInfo = new FileInfo(modelPath);
                var sizeGb = fileInfo.Length / 1024d / 1024d / 1024d;
                candidates.Add(new LlmModelCandidate
                {
                    FullPath = modelPath,
                    FileName = fileName,
                    FileSizeBytes = fileInfo.Length,
                    FileSizeGB = sizeGb,
                    ParamsBillions = ParseParamsBillions(fileName)
                });
            }
        }

        return candidates;
    }

    public static LlmModelCandidate? SelectBestModel(IEnumerable<LlmModelCandidate> candidates, LlmRuntimeProfile profile)
    {
        var candidateList = candidates?.ToList() ?? new List<LlmModelCandidate>();
        if (candidateList.Count == 0)
        {
            return null;
        }

        var memoryBudgetGb = profile.SystemMemoryGB * (profile.UseGpuOffload ? 0.72 : 0.62);
        var feasible = candidateList
            .Where(c => EstimateRequiredMemoryGB(c, profile) <= memoryBudgetGb)
            .ToList();

        var pool = feasible.Count > 0
            ? feasible
            : candidateList.OrderBy(c => c.FileSizeGB).Take(1).ToList();

        return pool
            .OrderByDescending(c => c.ParamsBillions ?? 0d)
            .ThenByDescending(c => c.FileSizeGB)
            .FirstOrDefault();
    }

    public static string BuildProfileSummary(LlmRuntimeProfile profile)
    {
        var gpuPart = string.IsNullOrWhiteSpace(profile.GpuName)
            ? "GPU: 未检测到"
            : $"GPU: {profile.GpuName}" + (profile.GpuMemoryMB.HasValue ? $" ({profile.GpuMemoryMB.Value} MB)" : string.Empty);
        return $"{profile.RuntimeLabel} | ctx={profile.ContextSize} | {gpuPart}";
    }

    private static bool DetectGpuBackendSupport()
    {
        var assemblyNames = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetName().Name ?? string.Empty)
            .ToList();

        if (assemblyNames.Any(name =>
            GpuBackendMarkers.Any(marker => name.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0) ||
            (name.IndexOf("llamasharp.backend", StringComparison.OrdinalIgnoreCase) >= 0 &&
             GpuBackendNameHints.Any(hint => name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0))))
        {
            return true;
        }

        try
        {
            var probeDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                AppDomain.CurrentDomain.BaseDirectory,
                AppContext.BaseDirectory,
                Environment.CurrentDirectory,
                Path.Combine(Environment.CurrentDirectory, ".godot", "mono", "temp", "bin", "Debug"),
                Path.Combine(Environment.CurrentDirectory, ".godot", "mono", "temp", "bin", "Release")
            };

            try
            {
                var resPath = Godot.ProjectSettings.GlobalizePath("res://");
                if (!string.IsNullOrWhiteSpace(resPath))
                {
                    probeDirectories.Add(resPath);
                }
            }
            catch
            {
                // Ignore Godot path probing failures.
            }

            foreach (var directory in probeDirectories)
            {
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                {
                    continue;
                }

                foreach (var dllPath in SafeEnumerateFiles(directory, "*LLamaSharp.Backend*.dll", SearchOption.AllDirectories))
                {
                    var dllName = Path.GetFileNameWithoutExtension(dllPath);
                    if (string.IsNullOrWhiteSpace(dllName))
                    {
                        continue;
                    }

                    if (GpuBackendMarkers.Any(marker => dllName.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (dllName.IndexOf("llamasharp.backend", StringComparison.OrdinalIgnoreCase) >= 0 &&
                         GpuBackendNameHints.Any(hint => dllName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)))
                    {
                        return true;
                    }
                }

                foreach (var nativeLlamaPath in SafeEnumerateFiles(directory, "llama.dll", SearchOption.AllDirectories))
                {
                    if (HasGpuNativeBackendFolder(nativeLlamaPath))
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            // Ignore probe failures and treat as no GPU backend.
        }

        return false;
    }

    private static IEnumerable<string> SafeEnumerateFiles(string directory, string pattern, SearchOption searchOption)
    {
        try
        {
            return Directory.EnumerateFiles(directory, pattern, searchOption);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static bool HasGpuNativeBackendFolder(string nativeLibraryPath)
    {
        if (string.IsNullOrWhiteSpace(nativeLibraryPath))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(nativeLibraryPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        var normalized = directory.Replace('\\', '/');
        return GpuNativeBackendFolders.Any(folder =>
            normalized.IndexOf($"/native/{folder}", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static double EstimateRequiredMemoryGB(LlmModelCandidate candidate, LlmRuntimeProfile profile)
    {
        var runtimeMultiplier = profile.UseGpuOffload ? 1.45d : 2.40d;
        var contextOverhead = profile.ContextSize switch
        {
            <= 2048 => 0.50d,
            <= 3072 => 0.80d,
            _ => 1.20d
        };

        return candidate.FileSizeGB * runtimeMultiplier + contextOverhead;
    }

    private static string ResolveSearchPath(string path)
    {
        if (path.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        {
            return Godot.ProjectSettings.GlobalizePath(path);
        }

        return path;
    }

    private static double? ParseParamsBillions(string fileName)
    {
        var match = ParamSizeRegex.Match(fileName);
        if (!match.Success)
        {
            return null;
        }

        if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return value;
    }
}

internal sealed class LocalGpuProbeResult
{
    public bool HasGpu { get; init; }
    public string Name { get; init; } = string.Empty;
    public int? VramMB { get; init; }
}

internal static class LocalHardwareProbe
{
    public static LocalGpuProbeResult ProbeGpu()
    {
        if (TryProbeNvidiaSmi(out var nvidia))
        {
            return nvidia;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && TryProbeWindowsWmi(out var wmi))
        {
            return wmi;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && TryProbeLinuxLspci(out var lspci))
        {
            return lspci;
        }

        return new LocalGpuProbeResult
        {
            HasGpu = false,
            Name = string.Empty,
            VramMB = null
        };
    }

    private static bool TryProbeNvidiaSmi(out LocalGpuProbeResult result)
    {
        result = new LocalGpuProbeResult();
        if (!TryRunCommand("nvidia-smi", "--query-gpu=name,memory.total --format=csv,noheader,nounits", 1200, out var output))
        {
            return false;
        }

        var firstLine = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return false;
        }

        var parts = firstLine.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var name = parts.Length > 0 ? parts[0] : "NVIDIA GPU";
        int? memory = null;
        if (parts.Length > 1 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var vramMb))
        {
            memory = vramMb;
        }

        result = new LocalGpuProbeResult
        {
            HasGpu = true,
            Name = name,
            VramMB = memory
        };
        return true;
    }

    private static bool TryProbeWindowsWmi(out LocalGpuProbeResult result)
    {
        result = new LocalGpuProbeResult();
        if (!TryRunCommand("wmic", "path win32_VideoController get Name /value", 1200, out var output))
        {
            return false;
        }

        var names = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(raw => raw.StartsWith("Name=", StringComparison.OrdinalIgnoreCase))
            .Select(raw => raw.Substring("Name=".Length).Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (names.Count == 0)
        {
            return false;
        }

        var blockedKeywords = new[]
        {
            "microsoft basic",
            "llvmpipe",
            "orayidddriver",
            "remote display",
            "rdp",
            "virtual display",
            "parsec",
            "citrix",
            "vmware",
            "hyper-v"
        };

        var preferredKeywords = new[]
        {
            "nvidia",
            "geforce",
            "rtx",
            "amd",
            "radeon",
            "intel",
            "arc"
        };

        var candidates = names
            .Where(name => !blockedKeywords.Any(keyword => name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
            .ToList();
        if (candidates.Count == 0)
        {
            return false;
        }

        var name = candidates
            .FirstOrDefault(candidate => preferredKeywords.Any(keyword => candidate.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
            ?? candidates[0];

        result = new LocalGpuProbeResult
        {
            HasGpu = true,
            Name = name,
            VramMB = null
        };
        return true;
    }

    private static bool TryProbeLinuxLspci(out LocalGpuProbeResult result)
    {
        result = new LocalGpuProbeResult();
        if (!TryRunCommand("lspci", string.Empty, 1200, out var output))
        {
            return false;
        }

        var line = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(raw =>
                raw.Contains("VGA", StringComparison.OrdinalIgnoreCase) ||
                raw.Contains("3D controller", StringComparison.OrdinalIgnoreCase) ||
                raw.Contains("Display controller", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        result = new LocalGpuProbeResult
        {
            HasGpu = true,
            Name = line,
            VramMB = null
        };
        return true;
    }

    private static bool TryRunCommand(string fileName, string arguments, int timeoutMs, out string output)
    {
        output = string.Empty;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            if (!process.WaitForExit(timeoutMs))
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                    // Ignore kill errors.
                }
                return false;
            }

            output = process.StandardOutput.ReadToEnd();
            return !string.IsNullOrWhiteSpace(output);
        }
        catch
        {
            return false;
        }
    }
}
