using Godot;
using PlanetGeneration.Rendering;
using PlanetGeneration.Core.Application;
using PlanetGeneration.Core.Domain;
using PlanetGeneration.Core.Layers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PlanetGeneration.Tests;

/// <summary>Deterministic tests: no external artwork and no subjective screenshot-only pass.</summary>
public partial class GroundDecalVerification : Node
{
    private int _checks;
    private readonly List<IDisposable> _owned = new();
    private readonly Color _ground = new(0.25f, 0.5f, 0.25f, 1f);
    private readonly Color _decal = new(0.9f, 0.15f, 0.1f, 1f);
    private readonly Color _front = new(0.1f, 0.2f, 0.9f, 1f);

    public override async void _Ready()
    {
        var exitCode = 0;
        try
        {
            VerifySplitTextures();
            VerifyLayerContract();
            if (DisplayServer.GetName() != "headless")
            {
                await VerifyRenderedPixels();
                if (OS.GetCmdlineUserArgs().Contains("--map")) await VerifyMapIntegration();
            }
            else
                GD.Print("[GroundDecal] Headless: GPU pixel checks NOT run (run with a display to verify them).");
            GD.Print($"[GroundDecal] PASS: {_checks} assertions.");
        }
        catch (Exception ex)
        {
            exitCode = 1;
            GD.PrintErr($"[GroundDecal] FAIL: {ex}");
        }
        finally
        {
            foreach (var resource in _owned) resource.Dispose();
            GetTree().Quit(exitCode);
        }
    }

    private void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        _checks++;
    }

    private ImageTexture Texture(int width, int height, Color color)
    {
        using var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(color);
        var texture = ImageTexture.CreateFromImage(image);
        _owned.Add(texture);
        return texture;
    }

    private void VerifySplitTextures()
    {
        var source = Texture(8, 8, new Color(0.2f, 0.7f, 0.3f, 0.6f));
        using var maskImage = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
        maskImage.Fill(Colors.Black);
        // Contact shadow around a trunk: trunk itself reaches the bottom, but is NOT ground.
        for (var y = 5; y < 8; y++)
        for (var x = 0; x < 8; x++)
            if (x != 3 && x != 4) maskImage.SetPixel(x, y, Colors.White);
        var mask = ImageTexture.CreateFromImage(maskImage);
        _owned.Add(mask);
        using var split = GroundDecalTextures.Create(source, new(GroundDecalMode.ExplicitMask), mask: mask);
        using var original = source.GetImage();
        using var decal = split.Decal.GetImage();
        using var body = split.Body.GetImage();
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            var a = decal.GetPixel(x, y).A;
            var b = body.GetPixel(x, y).A;
            Check(Mathf.IsEqualApprox(a + b, original.GetPixel(x, y).A), "Source alpha lost or doubled");
            Check(a == 0f || b == 0f, "A decal texel leaked into body pass");
        }
        Check(body.GetPixel(3, 7).A > 0 && decal.GetPixel(3, 7).A == 0, "Explicit mask cut away trunk");
        using var missing = GroundDecalTextures.Create(source, new(GroundDecalMode.ExplicitMask));
        using var missingDecal = missing.Decal.GetImage();
        Check(missingDecal.GetPixel(0, 7).A == 0, "Missing mask must fail closed");
        using var grass = GroundDecalTextures.Create(source, GroundDecalProfile.Grass);
        using var grassBody = grass.Body.GetImage();
        Check(grassBody.GetPixel(0, 0).A > 0 && grassBody.GetPixel(0, 7).A == 0, "Grass ownership not disjoint");
        using var zero = GroundDecalTextures.Create(source, new(GroundDecalMode.BottomRatio, 0f));
        using var zeroDecal = zero.Decal.GetImage();
        Check(zeroDecal.GetPixel(0, 7).A == 0, "Zero ratio still produces a decal");
        using var cache = new GroundDecalTextureCache();
        var region = new Rect2(2, 4, 4, 4);
        var first = cache.Get(source, region, new(GroundDecalMode.BottomRatio, 0.5f));
        Check(ReferenceEquals(first, cache.Get(source, region, new(GroundDecalMode.BottomRatio, 0.5f))), "Cache did not reuse split");
        Check(first.Decal.GetSize() == new Vector2(4, 4), "Atlas region was not cropped");
        using var regionDecal = first.Decal.GetImage();
        Check(regionDecal.GetPixel(0, 0).A == 0 && regionDecal.GetPixel(0, 3).A > 0, "Split used atlas UV instead of region UV");
        cache.Clear();
        Check(cache.Count == 0, "Snapshot cache did not release entries");
    }

    private void VerifyLayerContract()
    {
        var parent = new Node2D { ZIndex = -100, Position = new Vector2(50, 60), Rotation = 0.3f, Scale = new Vector2(2, 3) };
        AddChild(parent);
        var obj = new UniversalDecoupledObject { BottomZIndex = 1000, Offset = new Vector2(2, -8) };
        parent.AddChild(obj);
        var bottom = obj.GetNode<Sprite2D>("BottomSprite");
        var body = obj.GetNode<Sprite2D>("BodySprite");
        Check(!obj.ZAsRelative && obj.ZIndex == GroundDecalLayers.Body, "Parent depth can lower body under decal");
        Check(!bottom.ZAsRelative && bottom.ZIndex == GroundDecalLayers.Decal, "Decal can escape ground layer");
        Check(body.GlobalTransform.IsEqualApprox(bottom.GlobalTransform), "Body/decal transforms diverged");
        Check(body.Offset == bottom.Offset, "Offsets diverged");
        parent.RemoveChild(obj);
        parent.AddChild(obj);
        Check(obj.GetChildCount() == 2, "Re-entering tree duplicates passes");
        var relief = new DecoupledRelief { SkirtZIndex = 999 };
        parent.AddChild(relief);
        Check(relief.BottomZIndex == GroundDecalLayers.Decal && relief.ZIndex == GroundDecalLayers.Body,
            "Legacy relief violates the layer contract");
        parent.Free();
    }

    private async Task VerifyMapIntegration()
    {
        var options = new GenerationOptions
        {
            Seed = 20260919, TargetCellCount = 1200, Extent = new WorldExtent(2048, 1024),
            SeaLevel = 0.35f, EnableRivers = true, RiverDensity = 1.2f, PlateCount = 8,
            OceanicRatio = 0.35f, ContinentCount = 3, ContinentBias = 0.5f,
            EnableCartographyDesigner = true, BlueprintName = "FantasyContinent01"
        };
        var snapshot = await new WorldGenerationService(new BaseFieldGeneratorAdapter()).GenerateAsync(options);
        var viewport = new SubViewport { Size = new Vector2I(1600, 900), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(viewport);
        var canvas = new MapCanvas { Size = new Vector2(1600, 900) };
        viewport.AddChild(canvas);
        var layers = new LayerStackState();
        Check(LayerPresetCatalog.ApplyPreset(LayerPresetCatalog.PresetGuohua, layers), "Cannot apply Guohua preset");
        canvas.AttachSnapshot(snapshot, layers);
        for (var i = 0; i < 5; i++) await Frame();
        using var image = viewport.GetTexture().GetImage();
        var output = ProjectSettings.GlobalizePath("user://ground_decal_map_verification.png");
        Check(image != null && image.SavePng(output) == Error.Ok, "Cannot render full map integration");
        GD.Print($"[GroundDecal] Map integration evidence: {output}");
        viewport.Free();
    }

    private UniversalDecoupledObject Object(Texture2D texture, Vector2 position, Texture2D? mask = null)
        => new()
        {
            Texture = texture, Position = position, Centered = false,
            SplitMode = UniversalDecoupledObject.DecoupledSplitMode.ExplicitMask,
            DecalMask = mask, TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };

    private async Task Frame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    }

    private void Pixel(Image image, int x, int y, Color expected, string message)
    {
        var actual = image.GetPixel(x, y);
        var diff = Mathf.Abs(actual.R - expected.R) + Mathf.Abs(actual.G - expected.G) + Mathf.Abs(actual.B - expected.B);
        Check(diff < 0.035f, $"{message}: ({x},{y}) expected={expected}, actual={actual}");
    }

    private static float ColorDiff(Color a, Color b)
        => Mathf.Abs(a.R - b.R) + Mathf.Abs(a.G - b.G) + Mathf.Abs(a.B - b.B);

    private static int CountColor(Image image, Rect2I rect, Color expected)
    {
        var count = 0;
        for (var y = rect.Position.Y; y < rect.End.Y; y++)
        for (var x = rect.Position.X; x < rect.End.X; x++)
            if (ColorDiff(image.GetPixel(x, y), expected) < 0.05f) count++;
        return count;
    }

    private async Task VerifyRenderedPixels()
    {
        var viewport = new SubViewport { Size = new Vector2I(320, 160), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(viewport);
        viewport.AddChild(new ColorRect { Size = new Vector2(320, 160), Color = _ground, ZIndex = GroundDecalLayers.Terrain });
        var world = new Node2D { YSortEnabled = true, ZIndex = -80 };
        viewport.AddChild(world);
        var source = Texture(64, 32, _decal);
        var mask = Texture(64, 32, Colors.White);
        var wallTexture = Texture(48, 64, _ground); // Wall RGB deliberately equals the terrain!
        var frontTexture = Texture(24, 24, _front);
        using var atlasImage = Image.CreateEmpty(32, 16, false, Image.Format.Rgba8);
        atlasImage.Fill(Colors.Magenta);
        atlasImage.FillRect(new Rect2I(16, 0, 16, 16), Colors.Yellow);
        var atlas = ImageTexture.CreateFromImage(atlasImage);
        _owned.Add(atlas);
        // Reverse insertion order: the Y sort, not insertion order, must decide body depth.
        var foreground = Object(frontTexture, new Vector2(40, 48));
        world.AddChild(foreground);
        var owner = Object(source, new Vector2(8, 32), mask);
        world.AddChild(owner);
        world.AddChild(Object(wallTexture, new Vector2(24, 0)));
        var shadow = new GroundDecal { Texture = source, Centered = false, Position = new Vector2(24, 0) };
        world.AddChild(shadow);
        // Test the main renderer's CPU split + actual draw helper on the right.
        using var split = GroundDecalTextures.Create(source, new(GroundDecalMode.ExplicitMask), mask: mask);
        var canvas = new GroundDecalVerificationCanvas
        {
            Position = new Vector2(144, 0), Decal = split.Decal, Body = split.Body,
            Wall = wallTexture, Foreground = frontTexture, Atlas = atlas, TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        viewport.AddChild(canvas);
        // Item 1 — magenta sentinel body placed over the exposed ground decal. A later,
        // higher upright body must fully occlude the decal beneath it; nothing may float on top.
        var sentinelTexture = Texture(14, 12, Colors.Magenta);
        world.AddChild(Object(sentinelTexture, new Vector2(8, 50)));
        // Item 2 — a body-only twin of the real-draw canvas for the side-view silhouette diff.
        // Bodies are tinted so the (identically colored) wall is separable from the terrain.
        var bodyViewport = new SubViewport { Size = new Vector2I(320, 160), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(bodyViewport);
        bodyViewport.AddChild(new ColorRect { Size = new Vector2(320, 160), Color = _ground, ZIndex = GroundDecalLayers.Terrain });
        bodyViewport.AddChild(new GroundDecalVerificationCanvas
        {
            Position = new Vector2(144, 0), Decal = split.Decal, Body = split.Body,
            Wall = wallTexture, Foreground = frontTexture, Atlas = atlas,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest, DrawDecals = false, Tint = Colors.Magenta
        });
        for (var i = 0; i < 4; i++) await Frame();
        using (var image = viewport.GetTexture().GetImage())
        {
            Pixel(image, 12, 40, _decal, "Exposed ground decal missing");
            Pixel(image, 40, 40, _ground, "Decal painted a same-colored wall");
            Pixel(image, 48, 56, _front, "Y-sorted front body is occluded");
            Pixel(image, 40, 12, _ground, "Standalone shadow painted wall");
            Pixel(image, 156, 40, _decal, "Main draw path omitted ground pass");
            Pixel(image, 184, 40, _ground, "Main draw path painted wall");
            Pixel(image, 192, 56, _front, "Main draw path lost foreground");
            Pixel(image, 160, 104, Colors.Yellow, "Rotated atlas/offscreen-anchor sprite disappeared or UV drifted");
            Pixel(image, 168, 104, _ground, "Coastal clipping failed after rotation");
            // Item 1 — a magenta body sits directly over a ground decal; after compositing
            // not one decal-colored pixel may remain visible on top of that body.
            var sentinelRect = new Rect2I(10, 52, 8, 6);
            Check(CountColor(image, sentinelRect, _decal) == 0, "Ground decal floated on top of a body (sentinel)");
            Check(CountColor(image, sentinelRect, Colors.Magenta) == sentinelRect.Size.X * sentinelRect.Size.Y,
                "Sentinel body not fully drawn — occlusion test is vacuous");
            // Item 2 — side-view diff: inside every upright body silhouette (from the tinted
            // body-only render) the real render must never show a ground-decal pixel.
            using (var bodyMask = bodyViewport.GetTexture().GetImage())
            {
                var bodyPixels = 0;
                var violations = 0;
                for (var y = 0; y < 128; y++)
                for (var x = 144; x < 284; x++)
                {
                    if (ColorDiff(bodyMask.GetPixel(x, y), _ground) <= 0.05f) continue;
                    bodyPixels++;
                    if (ColorDiff(image.GetPixel(x, y), _decal) < 0.05f) violations++;
                }
                Check(bodyPixels > 200, "Body silhouette empty — side-view diff is vacuous");
                Check(violations == 0, $"Ground decal visible within body silhouette: {violations}/{bodyPixels}px");
            }
            var output = ProjectSettings.GlobalizePath("user://ground_decal_verification.png");
            Check(image.SavePng(output) == Error.Ok, "Cannot save GPU evidence");
            GD.Print($"[GroundDecal] GPU evidence: {output}");
        }
        bodyViewport.Free();
        owner.Position += new Vector2(80, 32);
        owner.Modulate = new Color(1f, 1f, 1f, 0.5f);
        await Frame(); await Frame();
        using (var image = viewport.GetTexture().GetImage())
        {
            Pixel(image, 12, 40, _ground, "Old decal remained after moving owner");
            Pixel(image, 96, 72, _ground.Lerp(_decal, 0.5f), "Moved decal lost transform or modulation");
        }
        owner.Visible = false;
        await Frame(); await Frame();
        using (var image = viewport.GetTexture().GetImage())
            Pixel(image, 96, 72, _ground, "Hidden owner left a decal behind");
        viewport.Free();
    }
}

/// <summary>Uses the same geometry/UV/clipping helper as GuohuaMapRenderer, not a test substitute.</summary>
public partial class GroundDecalVerificationCanvas : Node2D
{
    public Texture2D Decal = null!;
    public Texture2D Body = null!;
    public Texture2D Wall = null!;
    public Texture2D Foreground = null!;
    public Texture2D Atlas = null!;
    // Test knobs: the body-only twin skips ground decals and tints upright bodies so their
    // silhouette is separable from the (identically colored) terrain in the side-view diff.
    public bool DrawDecals = true;
    public Color Tint = Colors.White;
    public override void _Draw()
    {
        var visible = new Rect2(0, 0, 140, 128);
        void Draw(Texture2D texture, Vector2 position) => GuohuaMapRenderer.DrawSurfaceTexture(this,
            texture, position, texture.GetSize(), Vector2.Zero, 0, Tint, null, null, visible);
        if (DrawDecals) Draw(Decal, new Vector2(8, 32));
        Draw(Wall, new Vector2(24, 0));
        Draw(Body, new Vector2(8, 32));
        Draw(Foreground, new Vector2(40, 48));
        GuohuaMapRenderer.DrawSurfaceTexture(this, Atlas, new Vector2(30, 100),
            new Vector2(16, 16), new Vector2(20, 0), 0.2f, Tint, new Rect2(16, 0, 16, 16),
            new() { (new Vector2(20, 0), Vector2.Right) }, new Rect2(0, 96, 20, 24));
    }
}
