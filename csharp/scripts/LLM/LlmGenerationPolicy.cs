using Godot;
using System;
using System.Collections.Generic;

namespace PlanetGeneration.LLM;

public readonly struct LlmGenerationPolicyEntry
{
    public int MaxTokens { get; init; }
    public float Temperature { get; init; }
    public int RetryCount { get; init; }
    public int TimeoutSeconds { get; init; }
    public int CacheTtlSeconds { get; init; }
}

public static class LlmGenerationPolicy
{
    private const string PolicyConfigPath = "user://llm_generation.cfg";
    private static bool _defaultFileEnsured;

    private static readonly Dictionary<string, LlmGenerationPolicyEntry> Defaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["world_analysis"] = new LlmGenerationPolicyEntry
        {
            MaxTokens = 160,
            Temperature = 0.55f,
            RetryCount = 0,
            TimeoutSeconds = 45,
            CacheTtlSeconds = 240
        },
        ["region_analysis"] = new LlmGenerationPolicyEntry
        {
            MaxTokens = 128,
            Temperature = 0.55f,
            RetryCount = 0,
            TimeoutSeconds = 30,
            CacheTtlSeconds = 180
        },
        ["quest_draft"] = new LlmGenerationPolicyEntry
        {
            MaxTokens = 128,
            Temperature = 0.10f,
            RetryCount = 0,
            TimeoutSeconds = 20,
            CacheTtlSeconds = 300
        }
    };

    public static LlmGenerationPolicyEntry Resolve(string? key, LlmGenerationPolicyEntry fallback)
    {
        EnsureDefaultFile();

        if (string.IsNullOrWhiteSpace(key))
        {
            return ClampEntry(fallback);
        }

        var baseEntry = Defaults.TryGetValue(key, out var predefined)
            ? predefined
            : fallback;

        var config = new ConfigFile();
        if (config.Load(PolicyConfigPath) != Error.Ok)
        {
            return ClampEntry(baseEntry);
        }

        var resolved = new LlmGenerationPolicyEntry
        {
            MaxTokens = (int)(long)config.GetValue(key, "max_tokens", (long)baseEntry.MaxTokens),
            Temperature = (float)(double)config.GetValue(key, "temperature", (double)baseEntry.Temperature),
            RetryCount = (int)(long)config.GetValue(key, "retry_count", (long)baseEntry.RetryCount),
            TimeoutSeconds = (int)(long)config.GetValue(key, "timeout_seconds", (long)baseEntry.TimeoutSeconds),
            CacheTtlSeconds = (int)(long)config.GetValue(key, "cache_ttl_seconds", (long)baseEntry.CacheTtlSeconds)
        };

        return ClampEntry(resolved);
    }

    private static LlmGenerationPolicyEntry ClampEntry(LlmGenerationPolicyEntry entry)
    {
        return new LlmGenerationPolicyEntry
        {
            MaxTokens = Math.Clamp(entry.MaxTokens, 32, 1024),
            Temperature = Math.Clamp(entry.Temperature, 0.0f, 1.5f),
            RetryCount = Math.Clamp(entry.RetryCount, 0, 3),
            TimeoutSeconds = Math.Clamp(entry.TimeoutSeconds, 5, 180),
            CacheTtlSeconds = Math.Clamp(entry.CacheTtlSeconds, 10, 3600)
        };
    }

    private static void EnsureDefaultFile()
    {
        if (_defaultFileEnsured)
        {
            return;
        }

        _defaultFileEnsured = true;

        var config = new ConfigFile();
        if (config.Load(PolicyConfigPath) == Error.Ok)
        {
            return;
        }

        foreach (var pair in Defaults)
        {
            var key = pair.Key;
            var value = pair.Value;
            config.SetValue(key, "max_tokens", (long)value.MaxTokens);
            config.SetValue(key, "temperature", (double)value.Temperature);
            config.SetValue(key, "retry_count", (long)value.RetryCount);
            config.SetValue(key, "timeout_seconds", (long)value.TimeoutSeconds);
            config.SetValue(key, "cache_ttl_seconds", (long)value.CacheTtlSeconds);
        }

        _ = config.Save(PolicyConfigPath);
    }
}
