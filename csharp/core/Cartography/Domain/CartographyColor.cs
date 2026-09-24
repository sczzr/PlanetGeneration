using System;
using System.Globalization;

namespace PlanetGeneration.Core.Cartography;

/// <summary>
/// 纯 C# RGBA 颜色结构（零 Godot 依赖）。
/// 为幻想制图层（Cartography）提供统一的调色板、笔刷着色及色彩插值支持。
/// </summary>
public readonly record struct CartographyColor(float R, float G, float B, float A = 1.0f)
{
    public static readonly CartographyColor White = new(1.0f, 1.0f, 1.0f, 1.0f);
    public static readonly CartographyColor Black = new(0.0f, 0.0f, 0.0f, 1.0f);
    public static readonly CartographyColor Transparent = new(0.0f, 0.0f, 0.0f, 0.0f);

    // 常用古典山水与幻想制图调色盘（降低饱和度 20%，增灰与增暖，契合古画舆图）
    public static readonly CartographyColor ColdBlue = new(0.42f, 0.52f, 0.60f, 1.0f);     // 黛蓝 / 沉静石青
    public static readonly CartographyColor EmeraldGreen = new(0.30f, 0.44f, 0.36f, 1.0f); // 松黛绿 / 青瓷苔绿
    public static readonly CartographyColor DeepForest = new(0.20f, 0.30f, 0.24f, 1.0f);   // 墨松绿 / 沉古水墨
    public static readonly CartographyColor SandyOchre = new(0.80f, 0.68f, 0.48f, 1.0f);   // 暖赭石 / 金沙
    public static readonly CartographyColor WarmOchre = new(0.74f, 0.58f, 0.38f, 1.0f);    // 深赭石 / 苍沙
    public static readonly CartographyColor CinnabarRed = new(0.72f, 0.26f, 0.20f, 1.0f);  // 朱砂 / 绛红
    public static readonly CartographyColor InkCharcoal = new(0.22f, 0.19f, 0.16f, 1.0f);  // 温润焦墨（非死黑）
    public static readonly CartographyColor MistIvory = new(0.95f, 0.94f, 0.90f, 0.42f);   // 云岚象牙白
    public static readonly CartographyColor RicePaper = new(0.94f, 0.89f, 0.81f, 1.0f);   // 宣纸温润底色
    public static readonly CartographyColor SeaWave = new(0.48f, 0.60f, 0.68f, 0.60f);     // 沧海微澜浅青

    public static CartographyColor FromRgb(float r, float g, float B) => new(r, g, B, 1.0f);
    public static CartographyColor FromRgba(float r, float g, float b, float a) => new(r, g, b, a);

    public static CartographyColor FromHtml(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return White;
        var clean = hex.TrimStart('#');
        if (clean.Length == 6)
        {
            if (uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            {
                var r = ((rgb >> 16) & 0xFF) / 255.0f;
                var g = ((rgb >> 8) & 0xFF) / 255.0f;
                var b = (rgb & 0xFF) / 255.0f;
                return new CartographyColor(r, g, b, 1.0f);
            }
        }
        else if (clean.Length == 8)
        {
            if (uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgba))
            {
                var r = ((rgba >> 24) & 0xFF) / 255.0f;
                var g = ((rgba >> 16) & 0xFF) / 255.0f;
                var b = ((rgba >> 8) & 0xFF) / 255.0f;
                var a = (rgba & 0xFF) / 255.0f;
                return new CartographyColor(r, g, b, a);
            }
        }
        return White;
    }

    public CartographyColor WithAlpha(float a) => new(R, G, B, a);

    public CartographyColor Lerp(CartographyColor target, float t)
    {
        var clamped = Math.Clamp(t, 0.0f, 1.0f);
        return new CartographyColor(
            R + (target.R - R) * clamped,
            G + (target.G - G) * clamped,
            B + (target.B - B) * clamped,
            A + (target.A - A) * clamped
        );
    }

    public string ToHtml() => $"#{((byte)Math.Clamp(R * 255f, 0f, 255f)):X2}{((byte)Math.Clamp(G * 255f, 0f, 255f)):X2}{((byte)Math.Clamp(B * 255f, 0f, 255f)):X2}";

    public override string ToString() => $"rgba({R:0.00}, {G:0.00}, {B:0.00}, {A:0.00})";
}
