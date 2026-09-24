import os
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance, ImageOps

def feather_mask(img_rgba, feather_bottom=40, feather_sides=25):
    """Softly feather bottom and lateral edges for organic ink blending."""
    w, h = img_rgba.size
    alpha = np.array(img_rgba.split()[-1], dtype=np.float32)
    if feather_bottom > 0:
        fade_b = np.linspace(1.0, 0.0, min(feather_bottom, h))
        alpha[h - len(fade_b):, :] *= fade_b[:, None]
    if feather_sides > 0:
        n_side = min(feather_sides, w // 2)
        fade_s = np.linspace(0.0, 1.0, n_side)
        alpha[:, :n_side] *= fade_s[None, :]
        alpha[:, w - n_side:] *= fade_s[::-1][None, :]
    alpha_img = Image.fromarray(np.clip(alpha, 0, 255).astype(np.uint8))
    img_rgba.putalpha(alpha_img)
    return img_rgba

def create_game_logo(out_path: str, size: int = 512):
    # Render at 2x (1024x1024) for ultra-crisp supersampling
    scale = 2
    W, H = size * scale, size * scale
    cx, cy = W // 2, H // 2
    radius = int(W * 0.40)  # ~410 px
    ring_radius = int(W * 0.45)  # ~460 px

    guohua_dir = r"f:\GameDev\Original\PlanetGenerationCore\csharp\resources\textures\guohua"
    setup_dir = r"f:\GameDev\Original\PlanetGenerationCore\csharp\resources\textures\world_setup"

    # 1. Base Canvas
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    # 2. Ambient Celestial Shadow & Golden Aura
    ambient = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    amb_draw = ImageDraw.Draw(ambient)
    amb_draw.ellipse(
        (cx - ring_radius - 20, cy - ring_radius - 20, cx + ring_radius + 20, cy + ring_radius + 20),
        fill=(195, 150, 70, 75)
    )
    ambient = ambient.filter(ImageFilter.GaussianBlur(32 * scale))
    canvas.alpha_composite(ambient)

    # 3. Celestial World Base Disc (Ink-Wash Parchment & Emerald-Celadon Water)
    parchment_path = os.path.join(setup_dir, "bg_parchment_scroll.png")
    if os.path.exists(parchment_path):
        parch = Image.open(parchment_path).convert("RGBA")
        parch_crop = parch.crop((350, 100, 1250, 1000)).resize((W, H), Image.Resampling.LANCZOS)
    else:
        parch_crop = Image.new("RGBA", (W, H), (230, 218, 190, 255))

    # Radial Water Gradient: Dark green-gold ink at rim, glowing emerald mist in center
    water_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    w_draw = ImageDraw.Draw(water_layer)
    for r in range(radius, 0, -2):
        t = r / radius
        # Rich Shan Shui palette:
        # Rim: Deep jade ink (#122622, 245) -> Center: Celadon river glow (#366C5E, 230)
        r_c = int(18 * t + 42 * (1 - t))
        g_c = int(38 * t + 102 * (1 - t))
        b_c = int(34 * t + 88 * (1 - t))
        a_c = int(248 * (0.85 + 0.15 * t))
        w_draw.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(r_c, g_c, b_c, a_c))

    # Sphere Mask
    mask_sphere = Image.new("L", (W, H), 0)
    m_draw = ImageDraw.Draw(mask_sphere)
    m_draw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=255)

    world_base = Image.blend(parch_crop, water_layer, 0.70)
    world_masked = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    world_masked.paste(world_base, (0, 0), mask_sphere)

    # 4. Landscape & World Composition (Layered from back to front)
    interior = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    # 4.1 Sky & Celestial Mythical Dragon (Subtle, soaring in high clouds)
    dragon_path = os.path.join(guohua_dir, "sea_dragon_4.png")
    if os.path.exists(dragon_path):
        drag = Image.open(dragon_path).convert("RGBA")
        # Soften and tint dragon with jade & gold
        drag = drag.resize((int(drag.width * 1.25 * scale), int(drag.height * 1.25 * scale)), Image.Resampling.LANCZOS)
        # Soft alpha for celestial mythic quality
        d_alpha = np.array(drag.split()[-1], dtype=np.float32) * 0.85
        drag.putalpha(Image.fromarray(d_alpha.astype(np.uint8)))
        interior.paste(drag, (cx - 280, cy - 360), drag)

    # 4.2 Winding Calligraphy River Valley
    river_img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    riv_draw = ImageDraw.Draw(river_img)
    curve_points = [
        (cx - 50, cy - 60),
        (cx - 20, cy + 30),
        (cx - 80, cy + 130),
        (cx - 30, cy + 220),
        (cx - 100, cy + 310),
        (cx - 40, cy + 400)
    ]
    # Smooth river ribbons
    for width, alpha, col in [
        (48 * scale, 55, (30, 95, 90)),
        (26 * scale, 140, (65, 160, 150)),
        (10 * scale, 220, (190, 235, 225))
    ]:
        for i in range(len(curve_points) - 1):
            p1, p2 = curve_points[i], curve_points[i + 1]
            riv_draw.line([p1, p2], fill=(col[0], col[1], col[2], alpha), width=width, joint="curve")
            riv_draw.ellipse((p1[0]-width//2, p1[1]-width//2, p1[0]+width//2, p1[1]+width//2), fill=(col[0], col[1], col[2], alpha))
    interior.alpha_composite(river_img)

    # 4.3 Background Distant Blue-Green Peaks & Main Mountain Range from Atlas
    atlas_path = os.path.join(guohua_dir, "terrain_atlas.png")
    city_path = os.path.join(guohua_dir, "city_capital.png")
    pagoda_path = os.path.join(guohua_dir, "pagoda.png")
    pine_path = os.path.join(guohua_dir, "forest_pine.png")

    if os.path.exists(atlas_path):
        atlas = Image.open(atlas_path).convert("RGBA")
        # Mountain Ridge (ID 5 from terrain_catalog.json: 440x253)
        ridge = atlas.crop((2172, 334, 2172 + 440, 334 + 253))
        ridge_w = int(ridge.width * 1.55 * scale)
        ridge_h = int(ridge.height * 1.55 * scale)
        ridge_scaled = ridge.resize((ridge_w, ridge_h), Image.Resampling.LANCZOS)
        ridge_scaled = feather_mask(ridge_scaled, feather_bottom=60 * scale, feather_sides=45 * scale)
        interior.paste(ridge_scaled, (cx - 310, cy - 230), ridge_scaled)

        # Secondary Distant Peak (ID 0: 213x272)
        dist_p = atlas.crop((3628, 12, 3628 + 213, 12 + 272))
        dist_scaled = dist_p.resize((int(dist_p.width * 1.2 * scale), int(dist_p.height * 1.2 * scale)), Image.Resampling.LANCZOS)
        dist_scaled = feather_mask(dist_scaled, feather_bottom=50 * scale, feather_sides=30 * scale)
        interior.paste(dist_scaled, (cx - 110, cy - 280), dist_scaled)

    # 4.4 Capital City & Pagoda on Right Flank (Overlooking the celestial river)
    if os.path.exists(city_path):
        city = Image.open(city_path).convert("RGBA")
        city = city.resize((int(city.width * 0.68 * scale), int(city.height * 0.68 * scale)), Image.Resampling.LANCZOS)
        city = feather_mask(city, feather_bottom=45 * scale, feather_sides=30 * scale)
        interior.paste(city, (cx + 35, cy + 40), city)

    if os.path.exists(pagoda_path):
        pag = Image.open(pagoda_path).convert("RGBA")
        pag = pag.resize((int(pag.width * 1.3 * scale), int(pag.height * 1.3 * scale)), Image.Resampling.LANCZOS)
        pag = feather_mask(pag, feather_bottom=40 * scale, feather_sides=20 * scale)
        interior.paste(pag, (cx + 175, cy - 60), pag)

    # 4.5 Foreground Ancient Pine Trees along the valley
    if os.path.exists(pine_path):
        pine = Image.open(pine_path).convert("RGBA")
        p1 = pine.resize((int(pine.width * 0.9 * scale), int(pine.height * 0.9 * scale)), Image.Resampling.LANCZOS)
        p1 = feather_mask(p1, feather_bottom=30 * scale, feather_sides=20 * scale)
        interior.paste(p1, (cx - 240, cy + 100), p1)
        p2 = pine.resize((int(pine.width * 0.75 * scale), int(pine.height * 0.75 * scale)), Image.Resampling.LANCZOS)
        p2 = feather_mask(p2, feather_bottom=30 * scale, feather_sides=20 * scale)
        interior.paste(p2, (cx - 160, cy + 140), p2)

    # Clip interior inside the circular sphere
    interior_masked = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    interior_masked.paste(interior, (0, 0), mask_sphere)

    canvas.alpha_composite(world_masked)
    canvas.alpha_composite(interior_masked)

    # 5. Inner Spherical Shading & Rim Vignette
    vignette = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    v_draw = ImageDraw.Draw(vignette)
    v_depth = 60 * scale
    for r in range(radius, radius - v_depth, -1):
        p = (radius - r) / float(v_depth)
        alpha_v = int(160 * (p ** 0.75))
        v_draw.ellipse((cx - r, cy - r, cx + r, cy + r), outline=(12, 24, 22, alpha_v), width=2)
    canvas.alpha_composite(vignette)

    # Soft top-left ivory highlight (strictly off-white #F8F2DE, NO pure #FFFFFF)
    hl_mask = Image.new("L", (W, H), 0)
    hl_draw = ImageDraw.Draw(hl_mask)
    hl_draw.ellipse((cx - radius + 50*scale, cy - radius + 40*scale, cx, cy - 20*scale), fill=75)
    hl_mask = hl_mask.filter(ImageFilter.GaussianBlur(40 * scale))
    hl_layer = Image.new("RGBA", (W, H), (248, 242, 222, 0))
    hl_layer.putalpha(hl_mask)
    canvas.alpha_composite(hl_layer)

    # 6. Celestial Armillary Sphere Gold Rings (浑天仪天球环与经纬轨道 - 闭合椭圆无缺口)
    ring_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    r_draw = ImageDraw.Draw(ring_layer)

    gold_dark = (150, 110, 45, 255)
    gold_main = (215, 175, 85, 255)
    gold_light = (248, 225, 145, 255)

    # Outer bezel & concentric medallion rings
    r_draw.ellipse((cx - ring_radius, cy - ring_radius, cx + ring_radius, cy + ring_radius), outline=gold_dark, width=12*scale)
    r_draw.ellipse((cx - ring_radius + 3*scale, cy - ring_radius + 3*scale, cx + ring_radius - 3*scale, cy + ring_radius - 3*scale), outline=gold_main, width=6*scale)
    r_draw.ellipse((cx - ring_radius + 6*scale, cy - ring_radius + 6*scale, cx + ring_radius - 6*scale, cy + ring_radius - 6*scale), outline=gold_light, width=2*scale)

    # Inner boundary ring around sphere
    r_draw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), outline=gold_dark, width=8*scale)
    r_draw.ellipse((cx - radius + 2*scale, cy - radius + 2*scale, cx + radius - 2*scale, cy + radius - 2*scale), outline=gold_main, width=4*scale)

    # 8 Cardinal celestial diamonds and 24 planetary nodes
    mid_r = (radius + ring_radius) / 2.0
    for deg in range(0, 360, 15):
        rad = math.radians(deg)
        bx = cx + mid_r * math.cos(rad)
        by = cy + mid_r * math.sin(rad)
        if deg % 45 == 0:
            sz = 6 * scale
            r_draw.polygon([(bx, by - sz), (bx + sz, by), (bx, by + sz), (bx - sz, by)], fill=gold_light, outline=gold_dark)
        else:
            sz = 2.5 * scale
            r_draw.ellipse((bx - sz, by - sz, bx + sz, by + sz), fill=gold_main)

    # Armillary Equatorial Orbit Ring (Continuous ellipse)
    eq_w = ring_radius + 35 * scale
    eq_h = 105 * scale
    r_draw.ellipse((cx - eq_w, cy - eq_h, cx + eq_w, cy + eq_h), outline=gold_dark, width=7*scale)
    r_draw.ellipse((cx - eq_w + 1*scale, cy - eq_h + 1*scale, cx + eq_w - 1*scale, cy + eq_h - 1*scale), outline=gold_main, width=4*scale)
    r_draw.ellipse((cx - eq_w + 3*scale, cy - eq_h + 3*scale, cx + eq_w - 3*scale, cy + eq_h - 3*scale), outline=gold_light, width=2*scale)

    # Inclined Ecliptic Orbit Ring (rotated -26 degrees, continuous ellipse)
    ecl_img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    e_draw = ImageDraw.Draw(ecl_img)
    ecl_w = ring_radius + 45 * scale
    ecl_h = 125 * scale
    e_draw.ellipse((cx - ecl_w, cy - ecl_h, cx + ecl_w, cy + ecl_h), outline=gold_dark, width=8*scale)
    e_draw.ellipse((cx - ecl_w + 2*scale, cy - ecl_h + 2*scale, cx + ecl_w - 2*scale, cy + ecl_h - 2*scale), outline=gold_main, width=4*scale)
    e_draw.ellipse((cx - ecl_w + 4*scale, cy - ecl_h + 4*scale, cx + ecl_w - 4*scale, cy + ecl_h - 4*scale), outline=gold_light, width=2*scale)
    ecl_rotated = ecl_img.rotate(-26, resample=Image.Resampling.BICUBIC, center=(cx, cy))
    ring_layer.alpha_composite(ecl_rotated)

    canvas.alpha_composite(ring_layer)

    # 7. Auspicious Ruyi Clouds with Soft Drop Shadows (3D Floating Over Rim)
    cloud_path1 = os.path.join(guohua_dir, "cloud_ruyi_01.png")
    cloud_path3 = os.path.join(guohua_dir, "cloud_ruyi_03.png")

    clouds_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    clouds_shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    if os.path.exists(cloud_path1):
        c1 = Image.open(cloud_path1).convert("RGBA")

        # Bottom-left wrapping cloud (majestic foundation)
        c1_bot = c1.resize((int(c1.width * 2.3 * scale), int(c1.height * 2.3 * scale)), Image.Resampling.LANCZOS)
        pos_b = (cx - 430, cy + 250)
        # Drop shadow
        sh1 = c1_bot.copy()
        sh1.putalpha(Image.fromarray((np.array(c1_bot.split()[-1], dtype=np.float32) * 0.40).astype(np.uint8)))
        sh1_dark = Image.new("RGBA", sh1.size, (12, 22, 18, 255))
        sh1_dark.putalpha(sh1.split()[-1])
        clouds_shadow.paste(sh1_dark, (pos_b[0] + 5*scale, pos_b[1] + 7*scale), sh1_dark)
        clouds_layer.paste(c1_bot, pos_b, c1_bot)

        # Top-right breaking cloud (delicate, letting sphere breathe)
        c1_top = c1.resize((int(c1.width * 1.35 * scale), int(c1.height * 1.35 * scale)), Image.Resampling.LANCZOS)
        c1_top = c1_top.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        pos_t = (cx + 175, cy - 350)
        clouds_layer.paste(c1_top, pos_t, c1_top)

    if os.path.exists(cloud_path3):
        c3 = Image.open(cloud_path3).convert("RGBA")
        # Bottom-right wrapping cloud
        c3_bot = c3.resize((int(c3.width * 2.0 * scale), int(c3.height * 2.0 * scale)), Image.Resampling.LANCZOS)
        c3_bot = c3_bot.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        pos_br = (cx + 90, cy + 300)
        clouds_layer.paste(c3_bot, pos_br, c3_bot)

    clouds_shadow = clouds_shadow.filter(ImageFilter.GaussianBlur(10 * scale))
    canvas.alpha_composite(clouds_shadow)
    canvas.alpha_composite(clouds_layer)

    # 8. Vermilion Archaic Seal Stamp (朱砂方印 - 苍玄)
    seal_size = 96 * scale
    seal_x = cx - seal_size // 2
    seal_y = cy + radius - 45 * scale
    seal_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    s_draw = ImageDraw.Draw(seal_layer)

    vermilion = (185, 48, 38, 245)
    ivory_seal = (248, 240, 222, 245)

    # Soft shadow behind seal
    s_draw.rectangle((seal_x + 4*scale, seal_y + 6*scale, seal_x + seal_size + 4*scale, seal_y + seal_size + 6*scale), fill=(10, 15, 12, 120))
    seal_layer = seal_layer.filter(ImageFilter.GaussianBlur(4 * scale))
    s_draw = ImageDraw.Draw(seal_layer)

    # Seal body & border
    s_draw.rectangle((seal_x, seal_y, seal_x + seal_size, seal_y + seal_size), fill=vermilion, outline=gold_dark, width=3*scale)
    s_draw.rectangle((seal_x + 4*scale, seal_y + 4*scale, seal_x + seal_size - 4*scale, seal_y + seal_size - 4*scale), outline=ivory_seal, width=2*scale)

    # Stylized Seal Geometry (Classic Han-dynasty seal characters 苍玄)
    lw = 3 * scale
    # Left character '苍'
    col1 = seal_x + 16 * scale
    col2 = seal_x + seal_size // 2 - 8 * scale
    s_draw.line([(col1, seal_y + 20*scale), (col2, seal_y + 20*scale)], fill=ivory_seal, width=lw)
    s_draw.line([(col1 + (col2-col1)//2, seal_y + 14*scale), (col1 + (col2-col1)//2, seal_y + seal_size - 16*scale)], fill=ivory_seal, width=lw)
    s_draw.line([(col1, seal_y + 44*scale), (col2, seal_y + 44*scale)], fill=ivory_seal, width=lw)
    s_draw.line([(col1, seal_y + 68*scale), (col2, seal_y + 68*scale)], fill=ivory_seal, width=lw)

    # Right character '玄'
    col3 = seal_x + seal_size // 2 + 8 * scale
    col4 = seal_x + seal_size - 16 * scale
    s_draw.line([(col3, seal_y + 20*scale), (col4, seal_y + 20*scale)], fill=ivory_seal, width=lw)
    s_draw.line([(col3 + (col4-col3)//2, seal_y + 14*scale), (col3 + (col4-col3)//2, seal_y + 36*scale)], fill=ivory_seal, width=lw)
    # Diamond bow in lower Xuan
    mid_x = col3 + (col4 - col3) // 2
    s_draw.polygon([
        (mid_x, seal_y + 42*scale),
        (col4 - 4*scale, seal_y + 56*scale),
        (mid_x, seal_y + 70*scale),
        (col3 + 4*scale, seal_y + 56*scale)
    ], outline=ivory_seal, width=lw)

    canvas.alpha_composite(seal_layer)

    # 9. Downsample to target size (512x512) with high quality LANCZOS
    final_img = canvas.resize((size, size), Image.Resampling.LANCZOS)

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    final_img.save(out_path, "PNG", optimize=True)
    print(f"Successfully generated refined game logo at: {out_path} ({size}x{size} RGBA)")

if __name__ == "__main__":
    out_target = r"f:\GameDev\Original\PlanetGenerationCore\csharp\logo.png"
    create_game_logo(out_target, size=512)
