using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PlanetGeneration.LLM;

public sealed class LlmGenerationRequest
{
    public string? PolicyKey { get; init; }
    public required string CacheKey { get; init; }
    public required string Prompt { get; init; }
    public required string SystemPrompt { get; init; }
    public required int MaxTokens { get; init; }
    public required float Temperature { get; init; }
    public int RetryCount { get; init; } = 0;
    public int TimeoutSeconds { get; init; } = 30;
    public int CacheTtlSeconds { get; init; } = 300;
    public Func<string>? FallbackFactory { get; init; }
}

public sealed class LlmGenerationScheduler
{
    private sealed class CacheEntry
    {
        public required string Value { get; init; }
        public required DateTime ExpiresAt { get; init; }
    }

    private readonly LLamaInferenceService _inference;
    private readonly SemaphoreSlim _inferenceGate = new(1, 1);
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

    public LlmGenerationScheduler(LLamaInferenceService inference)
    {
        _inference = inference;
    }

    public async Task<string> GenerateTextAsync(LlmGenerationRequest request, CancellationToken ct = default)
    {
        var policy = LlmGenerationPolicy.Resolve(
            request.PolicyKey,
            new LlmGenerationPolicyEntry
            {
                MaxTokens = request.MaxTokens,
                Temperature = request.Temperature,
                RetryCount = request.RetryCount,
                TimeoutSeconds = request.TimeoutSeconds,
                CacheTtlSeconds = request.CacheTtlSeconds
            });

        if (TryGetCached(request.CacheKey, out var cached))
        {
            return cached;
        }

        await _inferenceGate.WaitAsync(ct);
        try
        {
            if (TryGetCached(request.CacheKey, out cached))
            {
                return cached;
            }

            var attempts = Math.Max(1, policy.RetryCount + 1);
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, policy.TimeoutSeconds)));

                string result;
                try
                {
                    result = await _inference.InferAsync(
                        request.Prompt,
                        request.SystemPrompt,
                        maxTokens: Math.Max(32, policy.MaxTokens),
                        temperature: policy.Temperature,
                        ct: timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(result) &&
                    !result.StartsWith("[错误", StringComparison.Ordinal) &&
                    !result.StartsWith("[生成已取消]", StringComparison.Ordinal))
                {
                    SetCache(request.CacheKey, result, policy.CacheTtlSeconds);
                    return result;
                }
            }
        }
        finally
        {
            _inferenceGate.Release();
        }

        return request.FallbackFactory?.Invoke() ?? "[错误] LLM 生成失败";
    }

    private bool TryGetCached(string key, out string value)
    {
        value = string.Empty;

        lock (_cacheLock)
        {
            if (!_cache.TryGetValue(key, out var entry))
            {
                return false;
            }

            if (entry.ExpiresAt <= DateTime.UtcNow)
            {
                _cache.Remove(key);
                return false;
            }

            value = entry.Value;
            return true;
        }
    }

    private void SetCache(string key, string value, int ttlSeconds)
    {
        var expiresAt = DateTime.UtcNow.AddSeconds(Math.Max(10, ttlSeconds));

        lock (_cacheLock)
        {
            _cache[key] = new CacheEntry
            {
                Value = value,
                ExpiresAt = expiresAt
            };

            PruneExpiredLocked();
        }
    }

    private void PruneExpiredLocked()
    {
        var now = DateTime.UtcNow;
        var staleKeys = new List<string>();
        foreach (var pair in _cache)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                staleKeys.Add(pair.Key);
            }
        }

        foreach (var key in staleKeys)
        {
            _cache.Remove(key);
        }
    }
}
