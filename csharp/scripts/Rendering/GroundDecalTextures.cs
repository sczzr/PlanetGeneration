using Godot;
using System;
using System.Collections.Generic;
using System.IO;

namespace PlanetGeneration.Rendering;

/// <summary>One canvas: terrain (-1), ground decals (0), upright objects (1).</summary>
public static class GroundDecalLayers
{
    public const int Terrain = -1;
    public const int Decal = 0;
    public const int Body = 1;
}

public enum GroundDecalMode { None, BottomRatio, ColorFeature, ExplicitMask }

/// <summary>Heuristics are fallbacks, not semantic recognition. Prefer an authored mask.</summary>
public readonly record struct GroundDecalProfile(
    GroundDecalMode Mode, float BottomRatio = 0.04f, float FeatureYStart = 0.88f,
    float GrassRatioR = 0.95f, float GrassRatioB = 1.08f)
{
    public static GroundDecalProfile Grass => new(GroundDecalMode.ColorFeature, BottomRatio: 0.04f, FeatureYStart: 0.88f);
    public static GroundDecalProfile Contact => new(GroundDecalMode.BottomRatio, BottomRatio: 0.04f);
}

/// <summary>
/// Disjoint RGBA textures: every source pixel belongs to exactly one pass.
/// Keep the original alpha; multiplying complementary alpha weights would leave holes
/// after source-over blending. Filtering provides antialiasing at the split boundary.
/// </summary>
public sealed class GroundDecalTextures : IDisposable
{
    public ImageTexture Decal { get; }
    public ImageTexture Body { get; }

    private GroundDecalTextures(ImageTexture decal, ImageTexture body)
    {
        Decal = decal;
        Body = body;
    }

    public static GroundDecalTextures Create(Texture2D source, GroundDecalProfile profile,
        Rect2? region = null, Texture2D? mask = null)
    {
        using var image = source.GetImage() ?? throw new ArgumentException("Source has no CPU-readable image.", nameof(source));
        if (image.IsCompressed() && image.Decompress() != Error.Ok)
            throw new ArgumentException("Cannot decompress decal source.", nameof(source));
        image.Convert(Image.Format.Rgba8);
        var rect = region.HasValue
            ? new Rect2I((Vector2I)region.Value.Position, (Vector2I)region.Value.Size)
            : new Rect2I(0, 0, image.GetWidth(), image.GetHeight());
        if (rect.Size.X <= 0 || rect.Size.Y <= 0 || rect.Position.X < 0 || rect.Position.Y < 0 ||
            rect.End.X > image.GetWidth() || rect.End.Y > image.GetHeight())
            throw new ArgumentOutOfRangeException(nameof(region));
        using var cropped = image.GetRegion(rect);
        using var maskImage = mask?.GetImage();
        if (maskImage != null)
        {
            if (maskImage.IsCompressed() && maskImage.Decompress() != Error.Ok)
                throw new ArgumentException("Cannot decompress decal mask.", nameof(mask));
            if (maskImage.GetSize() != image.GetSize())
                throw new ArgumentException("Decal mask must match the full source texture dimensions.", nameof(mask));
            maskImage.Convert(Image.Format.Rgba8);
        }
        using var croppedMask = maskImage?.GetRegion(rect);
        var maskBytes = croppedMask?.GetData();
        var decal = cropped.GetData();
        var body = (byte[])decal.Clone();
        for (var y = 0; y < rect.Size.Y; y++)
        for (var x = 0; x < rect.Size.X; x++)
        {
            var i = (y * rect.Size.X + x) * 4;
            var v = (y + 0.5f) / rect.Size.Y;
            var isDecal = profile.Mode switch
            {
                GroundDecalMode.BottomRatio => v >= 1f - Mathf.Clamp(profile.BottomRatio, 0f, 1f),
                GroundDecalMode.ColorFeature => v >= profile.FeatureYStart &&
                    decal[i + 1] > decal[i] * profile.GrassRatioR && decal[i + 1] > decal[i + 2] * profile.GrassRatioB,
                GroundDecalMode.ExplicitMask => maskBytes != null && maskBytes[i] * maskBytes[i + 3] >= 32512.5f,
                _ => false
            };
            if (isDecal) body[i + 3] = 0;
            else decal[i + 3] = 0;
        }
        using var decalImage = Image.CreateFromData(rect.Size.X, rect.Size.Y, false, Image.Format.Rgba8, decal);
        using var bodyImage = Image.CreateFromData(rect.Size.X, rect.Size.Y, false, Image.Format.Rgba8, body);
        return new GroundDecalTextures(ImageTexture.CreateFromImage(decalImage), ImageTexture.CreateFromImage(bodyImage));
    }

    public void Dispose() { Decal.Dispose(); Body.Dispose(); }
}

/// <summary>Optional authored sidecar for original images AND atlases: name.decal-mask.png.</summary>
public static class GroundDecalAssets
{
    private static readonly StringName MaskKey = "ground_decal_mask";

    public static Texture2D WithOptionalMask(Texture2D source, string sourcePath)
    {
        var maskPath = Path.ChangeExtension(sourcePath, ".decal-mask.png");
        Texture2D? mask = null;
        var globalMaskPath = ProjectSettings.GlobalizePath(maskPath);
        if (File.Exists(globalMaskPath))
        {
            using var image = Image.LoadFromFile(globalMaskPath);
            if (image != null) mask = ImageTexture.CreateFromImage(image);
        }
        else if (ResourceLoader.Exists(maskPath))
            mask = ResourceLoader.Load<Texture2D>(maskPath);
        if (mask != null)
        {
            if (mask.GetSize() == source.GetSize()) source.SetMeta(MaskKey, mask);
            else GD.PushWarning($"Ignoring ground decal mask with mismatched dimensions: {maskPath}");
        }
        return source;
    }

    public static Texture2D? GetMask(Texture2D source)
        => source.HasMeta(MaskKey) ? source.GetMeta(MaskKey).AsGodotObject() as Texture2D : null;
}

/// <summary>Snapshot-owned cache; only unique asset/region pairs are split, never every draw.</summary>
public sealed class GroundDecalTextureCache : IDisposable
{
    private readonly Dictionary<(ulong, Rect2?, GroundDecalProfile, ulong), GroundDecalTextures> _entries = new();
    public int Count => _entries.Count;
    public GroundDecalTextures Get(Texture2D source, Rect2? region, GroundDecalProfile profile)
    {
        var mask = GroundDecalAssets.GetMask(source);
        if (mask != null) profile = new GroundDecalProfile(GroundDecalMode.ExplicitMask);
        var key = (source.GetInstanceId(), region, profile, mask?.GetInstanceId() ?? 0);
        if (!_entries.TryGetValue(key, out var value))
            _entries.Add(key, value = GroundDecalTextures.Create(source, profile, region, mask));
        return value;
    }
    public void Clear()
    {
        foreach (var pair in _entries.Values) pair.Dispose();
        _entries.Clear();
    }
    public void Dispose() => Clear();
}
