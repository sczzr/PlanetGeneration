using Godot;
using System;

namespace PlanetGeneration.Rendering;

/// <summary>
/// 国画渲染器的程序化备用图元。只负责像素生成，不访问世界快照、图层或渲染缓存。
/// 像素基线见 tests/baselines/guohua-fallback-textures.json。
/// </summary>
internal static class GuohuaFallbackTextures
{
    private static readonly Color SymbolSoftInk = new(0.23f, 0.19f, 0.16f, 0.90f);
    private static readonly Color SymbolIvoryBacking = new(0.96f, 0.93f, 0.86f, 0.92f);
    private static readonly Color SymbolCinnabar = new(0.68f, 0.22f, 0.16f, 0.95f);

    internal static Texture2D CreateDefaultMountainTexture()
    {
        var img = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
        for (var y = 0; y < 64; y++)
        {
            var ny = y / 64f;
            for (var x = 0; x < 64; x++)
            {
                var nx = (x - 32f) / 32f;
                var mainPeakY = MathF.Abs(nx) * 1.45f + 0.12f;
                var subPeakY = MathF.Abs((x - 18f) / 20f) * 1.35f + 0.36f;
                var peakLimit = MathF.Min(mainPeakY, subPeakY);

                if (ny >= peakLimit)
                {
                    var isOutline = (ny - peakLimit) < 0.065f;
                    var shaded = nx > 0.04f;

                    Color c;
                    if (isOutline)
                    {
                        c = new Color(0.14f, 0.12f, 0.10f, 0.95f); // 焦墨勾边
                    }
                    else if (shaded)
                    {
                        c = new Color(0.18f, 0.26f, 0.24f, 0.90f); // 黛绿阴面
                    }
                    else
                    {
                        c = new Color(0.24f, 0.44f, 0.38f, 0.88f); // 石青石绿阳面
                    }

                    // 山脚云气雾化消隐
                    if (ny > 0.72f)
                    {
                        c.A *= (1f - ny) / 0.28f;
                    }
                    img.SetPixel(x, y, c);
                }
                else
                {
                    img.SetPixel(x, y, new Color(0, 0, 0, 0));
                }
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    internal static Texture2D CreateDefaultMistStripeTexture()
    {
        var w = 128;
        var h = 48;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        var mistBase = new Color(0.96f, 0.94f, 0.88f); // 宣纸暖象牙白，严禁纯白

        for (var y = 0; y < h; y++)
        {
            var ny = (y - h * 0.5f) / (h * 0.5f);
            var yFalloff = MathF.Max(0f, 1f - ny * ny);
            for (var x = 0; x < w; x++)
            {
                var nx = (x - w * 0.5f) / (w * 0.5f);
                var xFalloff = MathF.Max(0f, 1f - nx * nx);

                // 水墨横向流岚轮廓：边缘平缓，中间聚集
                var profile = MathF.Pow(xFalloff, 1.5f) * MathF.Pow(yFalloff, 1.2f);
                if (profile > 0.01f)
                {
                    var c = mistBase;
                    c.A = profile * 0.78f;
                    img.SetPixel(x, y, c);
                }
                else
                {
                    img.SetPixel(x, y, new Color(0, 0, 0, 0));
                }
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>王都/国都符号：◎（双重同心圆，上竖朱砂旌旗飞幡，中嵌朱砂微核，带宣纸内衬）。</summary>
    internal static Texture2D CreateDefaultCityTexture()
    {
        const int size = 40;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var cx = size * 0.5f;
        var cy = 23.5f; // 同心圆中心略微下移，为上方旌旗留出空间

        for (var y = 0; y < size; y++)
        {
            var dy = y - cy;
            for (var x = 0; x < size; x++)
            {
                var dx = x - cx;
                var dist = MathF.Sqrt(dx * dx + dy * dy);

                // 1. 宣纸衬底遮罩（圆形主体）
                if (dist <= 14.5f)
                {
                    var bgAlpha = Mathf.Clamp((14.5f - dist) * 1.5f, 0f, 1f);
                    img.SetPixel(x, y, new Color(SymbolIvoryBacking.R, SymbolIvoryBacking.G, SymbolIvoryBacking.B, SymbolIvoryBacking.A * bgAlpha));
                }

                // 2. 外同心圆环（半径 12.0，线宽 2.0）
                var outerDiff = MathF.Abs(dist - 12.0f);
                if (outerDiff <= 1.3f)
                {
                    var a = Mathf.Clamp(1f - (outerDiff - 0.4f) / 0.9f, 0f, 1f);
                    var cur = img.GetPixel(x, y);
                    img.SetPixel(x, y, cur.Blend(new Color(SymbolSoftInk.R, SymbolSoftInk.G, SymbolSoftInk.B, a * 0.95f)));
                }

                // 3. 内同心圆环（半径 6.0，线宽 1.5）
                var innerDiff = MathF.Abs(dist - 6.0f);
                if (innerDiff <= 1.2f)
                {
                    var a = Mathf.Clamp(1f - (innerDiff - 0.3f) / 0.9f, 0f, 1f);
                    var cur = img.GetPixel(x, y);
                    img.SetPixel(x, y, cur.Blend(new Color(SymbolSoftInk.R, SymbolSoftInk.G, SymbolSoftInk.B, a * 0.95f)));
                }

                // 4. 正中朱砂微核
                if (dist <= 2.6f)
                {
                    var a = Mathf.Clamp((2.6f - dist) * 1.8f, 0f, 1f);
                    var cur = img.GetPixel(x, y);
                    img.SetPixel(x, y, cur.Blend(new Color(SymbolCinnabar.R, SymbolCinnabar.G, SymbolCinnabar.B, a)));
                }

                // 5. 顶端华盖旌旗杆（垂直墨线：X: 19..20, Y: 2..12）
                if (x is 19 or 20 && y is >= 2 and <= 12)
                {
                    img.SetPixel(x, y, SymbolSoftInk);
                }

                // 6. 顶端迎风招展朱砂三角旗（Y: 3..9, X 从 20 向右展开）
                if (y is >= 3 and <= 9)
                {
                    var flagLen = (9 - y) * 1.5f + 2.0f; // 渐窄飘带
                    if (x >= 20 && x <= 20 + flagLen)
                    {
                        var isEdge = x == (int)(20 + flagLen) || y == 3 || y == 9;
                        var col = isEdge ? SymbolSoftInk : SymbolCinnabar;
                        img.SetPixel(x, y, col);
                    }
                }
            }
        }

        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>大都城/州府大都符号：□（重郭双层方框，角楼微起，中嵌朱砂微核，带宣纸内衬）。</summary>
    internal static Texture2D CreateDefaultLargeCityTexture()
    {
        const int size = 36;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var cx = size * 0.5f;
        var cy = size * 0.5f;
        const float outerHalf = 12.0f;
        const float innerHalf = 6.5f;

        for (var y = 0; y < size; y++)
        {
            var dy = MathF.Abs(y - cy + 0.5f);
            for (var x = 0; x < size; x++)
            {
                var dx = MathF.Abs(x - cx + 0.5f);
                var boxDist = MathF.Max(dx, dy);

                // 1. 宣纸衬底遮罩
                if (boxDist <= outerHalf + 1.2f)
                {
                    var bgAlpha = Mathf.Clamp((outerHalf + 1.2f - boxDist) * 1.5f, 0f, 1f);
                    img.SetPixel(x, y, new Color(SymbolIvoryBacking.R, SymbolIvoryBacking.G, SymbolIvoryBacking.B, SymbolIvoryBacking.A * bgAlpha));
                }

                // 2. 外郭重框（边长 24，线宽 1.8）
                var outerDiff = MathF.Abs(boxDist - outerHalf);
                if (outerDiff <= 1.2f)
                {
                    var a = Mathf.Clamp(1f - (outerDiff - 0.4f) / 0.8f, 0f, 1f);
                    var cur = img.GetPixel(x, y);
                    img.SetPixel(x, y, cur.Blend(new Color(SymbolSoftInk.R, SymbolSoftInk.G, SymbolSoftInk.B, a * 0.95f)));
                }

                // 3. 内郭方框（边长 13，线宽 1.4）
                var innerDiff = MathF.Abs(boxDist - innerHalf);
                if (innerDiff <= 1.1f)
                {
                    var a = Mathf.Clamp(1f - (innerDiff - 0.3f) / 0.8f, 0f, 1f);
                    var cur = img.GetPixel(x, y);
                    img.SetPixel(x, y, cur.Blend(new Color(SymbolSoftInk.R, SymbolSoftInk.G, SymbolSoftInk.B, a * 0.90f)));
                }

                // 4. 四角角楼凸起（Corner Watchtowers: 在四角外扩的小方块）
                if (dx is >= 10.5f and <= 13.5f && dy is >= 10.5f and <= 13.5f)
                {
                    var cur = img.GetPixel(x, y);
                    img.SetPixel(x, y, cur.Blend(new Color(SymbolSoftInk.R, SymbolSoftInk.G, SymbolSoftInk.B, 0.92f)));
                }

                // 5. 正中朱砂官署印点
                var rDist = MathF.Sqrt(dx * dx + dy * dy);
                if (rDist <= 2.4f)
                {
                    var a = Mathf.Clamp((2.4f - rDist) * 1.8f, 0f, 1f);
                    var cur = img.GetPixel(x, y);
                    img.SetPixel(x, y, cur.Blend(new Color(SymbolCinnabar.R, SymbolCinnabar.G, SymbolCinnabar.B, a * 0.95f)));
                }
            }
        }

        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>城镇/府县符号：□（端正细墨线方框，中嵌微圆点，带宣纸内衬）。</summary>
    internal static Texture2D CreateDefaultTownTexture()
    {
        const int size = 32;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var cx = size * 0.5f;
        var cy = size * 0.5f;
        const float half = 9.5f;

        for (var y = 0; y < size; y++)
        {
            var dy = MathF.Abs(y - cy + 0.5f);
            for (var x = 0; x < size; x++)
            {
                var dx = MathF.Abs(x - cx + 0.5f);
                var boxDist = MathF.Max(dx, dy);

                // 宣纸衬底
                if (boxDist <= half + 1.0f)
                {
                    var bgAlpha = Mathf.Clamp((half + 1.0f - boxDist) * 1.5f, 0f, 1f);
                    img.SetPixel(x, y, new Color(SymbolIvoryBacking.R, SymbolIvoryBacking.G, SymbolIvoryBacking.B, SymbolIvoryBacking.A * bgAlpha));
                }

                // 方框墨线（边长 19，线宽 1.8）
                var edgeDiff = MathF.Abs(boxDist - half);
                if (edgeDiff <= 1.3f)
                {
                    var a = Mathf.Clamp(1f - (edgeDiff - 0.4f) / 0.9f, 0f, 1f);
                    var cur = img.GetPixel(x, y);
                    img.SetPixel(x, y, cur.Blend(new Color(SymbolSoftInk.R, SymbolSoftInk.G, SymbolSoftInk.B, a * 0.95f)));
                }

                // 中心微墨点
                var rDist = MathF.Sqrt(dx * dx + dy * dy);
                if (rDist <= 2.2f)
                {
                    var a = Mathf.Clamp((2.2f - rDist) * 1.6f, 0f, 1f);
                    var cur = img.GetPixel(x, y);
                    img.SetPixel(x, y, cur.Blend(new Color(SymbolSoftInk.R, SymbolSoftInk.G, SymbolSoftInk.B, a * 0.92f)));
                }
            }
        }

        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>村落/聚落符号：●（清秀实心圆点，带极细宣纸微晕）。</summary>
    internal static Texture2D CreateDefaultVillageTexture()
    {
        const int size = 20;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var cx = size * 0.5f;
        var cy = size * 0.5f;

        for (var y = 0; y < size; y++)
        {
            var dy = y - cy + 0.5f;
            for (var x = 0; x < size; x++)
            {
                var dx = x - cx + 0.5f;
                var dist = MathF.Sqrt(dx * dx + dy * dy);

                // 外缘宣纸微晕
                if (dist <= 6.8f)
                {
                    var bgAlpha = Mathf.Clamp((6.8f - dist) * 1.5f, 0f, 1f);
                    img.SetPixel(x, y, new Color(SymbolIvoryBacking.R, SymbolIvoryBacking.G, SymbolIvoryBacking.B, SymbolIvoryBacking.A * bgAlpha));
                }

                // 实心墨圆点（半径 4.2）
                if (dist <= 4.4f)
                {
                    var a = Mathf.Clamp((4.4f - dist) * 1.8f, 0f, 1f);
                    var cur = img.GetPixel(x, y);
                    img.SetPixel(x, y, cur.Blend(new Color(SymbolSoftInk.R, SymbolSoftInk.G, SymbolSoftInk.B, a * 0.90f)));
                }
            }
        }

        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>关隘/隘口符号：☲（古典双轨横杠城堞关防记号）。</summary>
    internal static Texture2D CreateDefaultPassTexture()
    {
        const int size = 32;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var cx = size * 0.5f;
        var cy = size * 0.5f;

        // 宣纸衬底椭圆
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x - cx) / 12f;
                var dy = (y - cy) / 8f;
                var d = dx * dx + dy * dy;
                if (d <= 1.0f)
                {
                    img.SetPixel(x, y, SymbolIvoryBacking);
                }
            }
        }

        // 上横木与下横木（中间留出通行缺口）
        int[] barY = { 10, 11, 20, 21 };
        foreach (var by in barY)
        {
            for (var x = 6; x <= 25; x++)
            {
                if (x is >= 14 and <= 17) continue; // 关门缺口
                img.SetPixel(x, by, SymbolSoftInk);
            }
        }

        // 左右侧防卫敌台
        for (var y = 9; y <= 22; y++)
        {
            img.SetPixel(6, y, SymbolSoftInk);
            img.SetPixel(7, y, SymbolSoftInk);
            img.SetPixel(24, y, SymbolSoftInk);
            img.SetPixel(25, y, SymbolSoftInk);
        }

        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>津渡/港口符号：⚓（古典舟楫铁锚记号）。</summary>
    internal static Texture2D CreateDefaultPortTexture()
    {
        const int size = 32;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var cx = size * 0.5f;
        var cy = size * 0.5f;

        // 宣纸衬底
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x - cx) / 11f;
                var dy = (y - cy) / 11f;
                if (dx * dx + dy * dy <= 1.0f)
                {
                    img.SetPixel(x, y, SymbolIvoryBacking);
                }
            }
        }

        // 顶环（中心 16, 8）
        for (var y = 5; y <= 11; y++)
        {
            for (var x = 13; x <= 19; x++)
            {
                var d = MathF.Sqrt((x - 16) * (x - 16) + (y - 8) * (y - 8));
                if (MathF.Abs(d - 2.8f) <= 1.1f)
                {
                    img.SetPixel(x, y, SymbolSoftInk);
                }
            }
        }

        // 垂直锚杆 (X: 15..17, Y: 10..24)
        for (var y = 10; y <= 24; y++)
        {
            img.SetPixel(15, y, SymbolSoftInk);
            img.SetPixel(16, y, SymbolSoftInk);
        }

        // 横档 (X: 10..22, Y: 13..14)
        for (var x = 10; x <= 22; x++)
        {
            img.SetPixel(x, 13, SymbolSoftInk);
            img.SetPixel(x, 14, SymbolSoftInk);
        }

        // 底部弧形锚爪 (弯钩)
        for (var x = 7; x <= 25; x++)
        {
            var nx = (x - 16) / 8.5f;
            var curve = (int)(MathF.Pow(nx, 2.0f) * 4.5f);
            var y = 24 - curve;
            img.SetPixel(x, y, SymbolSoftInk);
            img.SetPixel(x, y + 1, SymbolSoftInk);
        }

        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>仙山洞府/寺庙符号：⛩（清秀古雅山门/宝刹微记）。</summary>
    internal static Texture2D CreateDefaultTempleTexture()
    {
        const int size = 28;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var cx = size * 0.5f;
        var cy = size * 0.5f;

        // 宣纸衬底
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x - cx) / 10f;
                var dy = (y - cy) / 10f;
                if (dx * dx + dy * dy <= 1.0f) img.SetPixel(x, y, SymbolIvoryBacking);
            }
        }

        // 上重飞檐横梁 (X: 4..23, Y: 8..10)
        for (var x = 4; x <= 23; x++)
        {
            var curve = (int)(MathF.Pow((x - 13.5f) / 9.5f, 2.0f) * 1.8f);
            var y = 9 - curve;
            img.SetPixel(x, y, SymbolSoftInk);
            img.SetPixel(x, y + 1, SymbolSoftInk);
        }

        // 下重直梁 (X: 6..21, Y: 13)
        for (var x = 6; x <= 21; x++)
        {
            img.SetPixel(x, 13, SymbolSoftInk);
        }

        // 双立柱 (X: 9, 18, Y: 10..24)
        for (var y = 10; y <= 24; y++)
        {
            img.SetPixel(9, y, SymbolSoftInk);
            img.SetPixel(10, y, SymbolSoftInk);
            img.SetPixel(17, y, SymbolSoftInk);
            img.SetPixel(18, y, SymbolSoftInk);
        }

        // 正中朱砂微点
        img.SetPixel(13, 11, SymbolCinnabar);
        img.SetPixel(14, 11, SymbolCinnabar);

        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>瀚海古城金字塔/神殿遗迹符号：阶梯石台、朱砂残标与新月沙丘微晕。</summary>
    internal static Texture2D CreateDefaultPyramidRelicTexture()
    {
        const int size = 36;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var cx = size * 0.5f;
        var cy = size * 0.5f;

        // 宣纸衬底
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x - cx) / 14f;
                var dy = (y - cy) / 12f;
                if (dx * dx + dy * dy <= 1.0f) img.SetPixel(x, y, SymbolIvoryBacking);
            }
        }

        // 阶梯金字塔石台 (4层梯级：顶宽 -> 底宽)
        var tiers = new[]
        {
            (10, 13, 3.5f, 5.0f),  // 顶殿
            (14, 17, 5.5f, 8.5f),  // 第1阶
            (18, 22, 9.0f, 12.5f), // 第2阶
            (23, 27, 13.0f, 16.0f) // 底基
        };

        var sandOchre = new Color(0.76f, 0.62f, 0.38f, 0.92f);
        var sandShadow = new Color(0.42f, 0.32f, 0.22f, 0.95f);

        foreach (var (y0, y1, halfW0, halfW1) in tiers)
        {
            for (var y = y0; y <= y1; y++)
            {
                var t = (y - y0) / (float)Math.Max(1, y1 - y0);
                var curHW = halfW0 + (halfW1 - halfW0) * t;
                for (var x = (int)(cx - curHW); x <= (int)(cx + curHW); x++)
                {
                    if (x < 0 || x >= size) continue;
                    var isEdge = x == (int)(cx - curHW) || x == (int)(cx + curHW) || y == y0 || y == y1;
                    var col = isEdge ? SymbolSoftInk : (x > cx ? sandShadow : sandOchre);
                    img.SetPixel(x, y, col);
                }
            }
        }

        // 顶部残破神殿朱砂残标
        img.SetPixel((int)cx, 9, SymbolCinnabar);
        img.SetPixel((int)cx, 8, SymbolCinnabar);

        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>农田水网水浇地符号：//// 梯田/水浇地肌理（古风整饬田垄纹理）。</summary>
    internal static Texture2D CreateDefaultFieldTerracedTexture()
    {
        const int w = 36;
        const int h = 24;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        var cx = w * 0.5f;
        var cy = h * 0.5f;

        var washColor = new Color(0.92f, 0.90f, 0.82f, 0.60f);
        var ridgeColor = new Color(0.20f, 0.32f, 0.22f, 0.92f);
        var ridgeLight = new Color(0.34f, 0.46f, 0.30f, 0.68f);

        // 1. 宣纸微晕染椭圆衬底
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var dx = (x - cx) / 16.0f;
                var dy = (y - cy) / 10.5f;
                var distSq = dx * dx + dy * dy;
                if (distSq <= 1.0f)
                {
                    var fade = 1.0f - MathF.Sqrt(distSq);
                    img.SetPixel(x, y, new Color(washColor.R, washColor.G, washColor.B, washColor.A * fade));
                }
            }
        }

        // 2. 四道微微倾斜弯曲的平行水墨田垄主纹 (////)
        var lineYs = new[] { 6, 10, 14, 18 };
        foreach (var baseY in lineYs)
        {
            for (var x = 4; x < w - 4; x++)
            {
                var dx = (x - cx) / 15.5f;
                var dy = (baseY - cy) / 10.0f;
                if (dx * dx + dy * dy > 0.95f) continue;

                var offset = (int)(MathF.Sin(x * 0.32f) * 1.2f + (x - cx) * 0.22f);
                var y = baseY + offset;
                if (y >= 1 && y < h - 1)
                {
                    img.SetPixel(x, y, ridgeColor);
                    img.SetPixel(x, y + 1, ridgeLight);
                }
            }
        }

        // 3. 散落田埂短接缝（交错垂直小梗）
        var dividers = new[] { (11, 7, 9), (20, 8, 10), (15, 11, 13), (24, 12, 14), (9, 15, 17), (18, 15, 17) };
        foreach (var (dx, y0, y1) in dividers)
        {
            for (var y = y0; y <= y1; y++)
            {
                if (y >= 0 && y < h && dx >= 0 && dx < w)
                {
                    img.SetPixel(dx, y, ridgeColor);
                }
            }
        }

        return ImageTexture.CreateFromImage(img);
    }
}
