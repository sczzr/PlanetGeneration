import os
import json
import cv2
import numpy as np
from PIL import Image
from scipy import ndimage

def slice_all_terrains():
    raw_dir = r"f:\GameDev\Original\PlanetGenerationCore\csharp\resources\textures\guohua\raw_terrain"
    out_dir = r"f:\GameDev\Original\PlanetGenerationCore\csharp\resources\textures\guohua"
    os.makedirs(out_dir, exist_ok=True)

    sources = [
        {"file": "terrain_mountain_peaks_sheet.jpg", "category": "Mountain"},
        {"file": "terrain_hills_sheet.jpg", "category": "Hills"},
        {"file": "terrain_plateau_sheet.jpg", "category": "Plateau"},
        {"file": "terrain_desert_sheet.jpg", "category": "Desert"},
        {"file": "terrain_grassland_sheet.jpg", "category": "Grassland"},
        {"file": "terrain_wetland_sheet.jpg", "category": "Wetland"},
        {"file": "terrain_basin_valley_sheet.jpg", "category": "Basin"},
        {"file": "terrain_snow_mountain_sheet.jpg", "category": "SnowMountain"},
        {"file": "terrain_volcano_rift_sheet.jpg", "category": "Volcano"},
    ]

    all_sprites = []
    sprite_id = 0

    for src in sources:
        fname = src["file"]
        category = src["category"]
        fpath = os.path.join(raw_dir, fname)
        if not os.path.exists(fpath):
            print(f"Warning: source file not found: {fpath}")
            continue

        print(f"\nProcessing {fname} ({category})...")
        img = Image.open(fpath).convert("RGB")
        arr = np.array(img, dtype=np.float32)
        h_total, w_total, _ = arr.shape

        # 1. Estimate background color from corners
        corners = np.vstack([arr[:20, :20], arr[:20, -20:], arr[-20:, :20], arr[-20:, -20:]])
        bg = np.mean(corners, axis=(0, 1))

        # 2. Binary mask of foreground features
        diff = np.linalg.norm(arr - bg, axis=2)
        bin_mask = (diff > 18.0).astype(np.uint8) * 255
        kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7))
        closed = cv2.morphologyEx(bin_mask, cv2.MORPH_CLOSE, kernel)

        num_labels, labels, stats, centroids = cv2.connectedComponentsWithStats(closed, connectivity=8)
        print(f"  Detected {num_labels - 1} raw components.")

        category_sprites = []

        for i in range(1, num_labels):
            s = stats[i]
            x = int(s[cv2.CC_STAT_LEFT])
            y = int(s[cv2.CC_STAT_TOP])
            w = int(s[cv2.CC_STAT_WIDTH])
            h = int(s[cv2.CC_STAT_HEIGHT])
            area = int(s[cv2.CC_STAT_AREA])

            # Filter small noise / stray marks
            if area < 1000 or w < 35 or h < 35:
                continue

            pad = 22
            x0_pad = max(0, x - pad)
            y0_pad = max(0, y - pad)
            x1_pad = min(w_total, x + w + pad)
            y1_pad = min(h_total, y + h + pad)

            crop_arr = arr[y0_pad:y1_pad, x0_pad:x1_pad].copy()
            sub_labels = labels[y0_pad:y1_pad, x0_pad:x1_pad]
            comp_mask = (sub_labels == i)
            comp_dilated = cv2.dilate(comp_mask.astype(np.uint8), np.ones((11, 11), np.uint8)) > 0
            
            # Mask out other neighboring components
            crop_arr[~comp_dilated] = bg

            # 3. Flood-fill background propagation from perimeter
            d_bg = np.linalg.norm(crop_arr - bg, axis=2)
            bg_seed = (d_bg < 15.0) | (~comp_dilated)
            seed = np.zeros_like(bg_seed, dtype=bool)
            seed[0, :] = seed[-1, :] = seed[:, 0] = seed[:, -1] = True
            seed = seed & bg_seed
            bg_connected = ndimage.binary_propagation(seed, mask=bg_seed)
            fg_mask = ~bg_connected

            # 4. Fill internal cavities inside solid rock / terrain
            hole_labels, num_holes = ndimage.label(~fg_mask)
            for h_idx in range(1, num_holes + 1):
                h_area = np.count_nonzero(hole_labels == h_idx)
                touches = (hole_labels[0, :] == h_idx).any() or (hole_labels[-1, :] == h_idx).any() or (hole_labels[:, 0] == h_idx).any() or (hole_labels[:, -1] == h_idx).any()
                if not touches and h_area < 6000:
                    fg_mask[hole_labels == h_idx] = True

            # 5. Distance transform for subpixel anti-aliasing & soft mist fade
            dist_in = cv2.distanceTransform(fg_mask.astype(np.uint8), cv2.DIST_L2, 3)
            alpha = np.clip(dist_in / 1.5, 0.0, 1.0)
            
            # Soft fade at base where ink/wash fades to white
            fade_factor = np.clip((d_bg - 12.0) / 25.0, 0.0, 1.0)
            alpha = np.where(dist_in < 8.0, alpha * fade_factor, alpha)

            # 6. Defringe neutral: strip low-saturation bright edge pixels
            rgb = crop_arr.copy()
            mx = rgb.max(axis=2)
            mn = rgb.min(axis=2)
            sat = (mx - mn) / 255.0
            fringe = (alpha > 0) & (alpha < 0.9) & (sat < 0.08) & (mx > 230)
            alpha[fringe] = 0.0

            # 7. Un-premultiply / despill white background from boundary
            a = alpha[..., np.newaxis]
            unmixed_rgb = np.clip((rgb - bg * (1.0 - a)) / np.maximum(a, 0.15), 0.0, 255.0)
            final_rgb = np.where((alpha > 0.02)[:, :, np.newaxis], unmixed_rgb, rgb)

            rgba_crop = np.dstack([final_rgb, alpha * 255.0]).astype(np.uint8)

            # 8. Tight crop
            alphas = rgba_crop[:, :, 3]
            ys, xs = np.where(alphas > 5)
            if len(xs) == 0 or len(ys) == 0:
                continue
            cx0, cx1 = int(xs.min()), int(xs.max()) + 1
            cy0, cy1 = int(ys.min()), int(ys.max()) + 1
            tight = rgba_crop[cy0:cy1, cx0:cx1]

            tw = cx1 - cx0
            th = cy1 - cy0
            aspect = round(tw / max(th, 1), 2)
            alpha_area = int(np.count_nonzero(tight[:, :, 3] > 20))

            orig_x = x0_pad + cx0
            orig_y = y0_pad + cy0

            # 9. Compute ground contact pivot point
            opaque_rows = np.where(tight[:, :, 3] > 100)[0]
            if len(opaque_rows) > 0:
                bottom_y = opaque_rows.max()
                low_y_start = max(0, bottom_y - int(th * 0.12))
                low_pixels_x = np.where(tight[low_y_start:bottom_y + 1, :, 3] > 80)[1]
                if len(low_pixels_x) > 0:
                    base_x = float(np.mean(low_pixels_x)) / float(tw)
                else:
                    base_x = 0.5
                base_y = float(bottom_y) / float(th)
            else:
                base_x = 0.5
                base_y = 0.90

            # 10. Sub-category classification based on morphology & aspect
            sub_category = "General"
            if category == "Mountain":
                if aspect > 1.8:
                    sub_category = "Ridge"
                elif th > 230:
                    sub_category = "MainPeak"
                else:
                    sub_category = "CompanionPeak"
            elif category == "Hills":
                if tw > 380:
                    sub_category = "Terrace"
                elif tw < 220:
                    sub_category = "SmallKnoll"
                else:
                    sub_category = "Knoll"
            elif category == "Plateau":
                if aspect > 1.5:
                    sub_category = "TableMesa"
                elif th > 220:
                    sub_category = "CliffPillar"
                else:
                    sub_category = "Mesa"
            elif category == "Desert":
                # Detect oasis by presence of blue/green pixels
                hsv = cv2.cvtColor(tight[:, :, :3], cv2.COLOR_RGB2HSV)
                blue_green = np.count_nonzero((hsv[:, :, 0] > 60) & (hsv[:, :, 0] < 140) & (hsv[:, :, 1] > 40))
                if blue_green > 600:
                    sub_category = "Oasis"
                elif aspect > 2.0:
                    sub_category = "BarchanDune"
                else:
                    sub_category = "SandRipple"
            elif category == "Grassland":
                if tw > 500:
                    sub_category = "MeadowPlain"
                elif th < 160:
                    sub_category = "Shrub"
                else:
                    sub_category = "Tussock"
            elif category == "Wetland":
                # Detect islets vs reeds
                if alpha_area > 30000 or aspect > 1.6:
                    sub_category = "Islet"
                elif th > 140:
                    sub_category = "Cattails"
                else:
                    sub_category = "Reeds"
            elif category == "Basin":
                if aspect > 1.8:
                    sub_category = "EncirclingArm"
                else:
                    sub_category = "BasinValley"
            elif category == "SnowMountain":
                if th > 175:
                    sub_category = "GlacierPeak"
                else:
                    sub_category = "SnowRidge"
            elif category == "Volcano":
                if th > 260:
                    sub_category = "ActiveCaldera"
                else:
                    sub_category = "BasaltCone"

            # World scale calibrated according to 6 : 3 : 1 ratio (Mountain ~28.5, Hill ~14.0, Tree ~4.6)
            category_target_heights = {
                ("Mountain", "MainPeak"): 28.5,
                ("Mountain", "CompanionPeak"): 23.5,
                ("Mountain", "Ridge"): 21.0,
                ("Mountain", "GrandRange"): 29.0,
                ("Mountain", "MassifCluster"): 25.0,
                ("SnowMountain", "GlacierPeak"): 28.5,
                ("SnowMountain", "SnowCompanionPeak"): 23.5,
                ("SnowMountain", "SnowRidge"): 21.0,
                ("SnowMountain", "SnowGrandRange"): 29.0,
                ("Volcano", "VolcanoCone"): 28.0,
                ("Volcano", "VolcanoCrater"): 26.0,
                ("Basin", "CalderaRing"): 26.0,
                ("Basin", "EncirclingArm"): 26.0,
                ("Hills", "Knoll"): 14.0,
                ("Plateau", "Plateau"): 22.0,
                ("Plateau", "CliffPillar"): 23.0,
                ("Grassland", "Shrub"): 3.6,
                ("Grassland", "Tussock"): 3.5,
                ("Grassland", "MeadowPlain"): 8.5,
                ("Wetland", "Reeds"): 5.0,
                ("Wetland", "Islet"): 9.5,
                ("Desert", "CrescentDune"): 10.5,
                ("Desert", "SandRipple"): 7.5,
                ("Desert", "SandRidge"): 12.5,
            }
            target_h = category_target_heights.get((category, sub_category))
            if target_h is not None:
                world_h = round(target_h, 1)
                world_w = round(world_h * aspect, 1)
            else:
                uniform_scale = 0.18
                world_w = round(tw * uniform_scale, 2)
                world_h = round(th * uniform_scale, 2)

            category_sprites.append({
                "id": sprite_id,
                "source_file": fname,
                "orig_x": orig_x,
                "orig_y": orig_y,
                "category": category,
                "sub_category": sub_category,
                "tw": tw,
                "th": th,
                "aspect": aspect,
                "area": alpha_area,
                "pivot_x": round(base_x, 3),
                "pivot_y": round(base_y, 3),
                "world_w": world_w,
                "world_h": world_h,
                "img": tight
            })
            sprite_id += 1

        print(f"  Extracted {len(category_sprites)} valid sprites for {category}.")
        all_sprites.extend(category_sprites)

    print(f"\nTotal extracted terrain sprites across all categories: {len(all_sprites)}")

    # 11. Skyline Shelf Bin Packing into single unified terrain_atlas.png
    atlas_w = 4096
    padding = 24

    sorted_sprites = sorted(all_sprites, key=lambda s: s["th"], reverse=True)

    skyline = [0] * atlas_w
    max_atlas_h = 0

    for s in sorted_sprites:
        cw = s["tw"] + padding
        ch = s["th"] + padding
        best_x = 0
        best_y = 999999
        for x in range(0, atlas_w - cw + 1, 16):
            y = max(skyline[x:x + cw])
            if y < best_y:
                best_y = y
                best_x = x
        px = best_x + padding // 2
        py = best_y + padding // 2
        for x in range(best_x, best_x + cw):
            skyline[x] = best_y + ch
        max_atlas_h = max(max_atlas_h, best_y + ch)
        s["packed_rect"] = [px, py, s["tw"], s["th"]]

    atlas_h = int(max_atlas_h + padding)
    print(f"Packed unified Atlas size: {atlas_w} x {atlas_h}")

    atlas_canvas = np.zeros((atlas_h, atlas_w, 4), dtype=np.uint8)
    catalog_sprites = []

    for s in all_sprites:
        px, py, pw, ph = s["packed_rect"]
        atlas_canvas[py:py + ph, px:px + pw] = s["img"]
        catalog_sprites.append({
            "id": s["id"],
            "category": s["category"],
            "sub_category": s["sub_category"],
            "source": s["source_file"],
            "rect": [px, py, pw, ph],
            "pivot": [s["pivot_x"], s["pivot_y"]],
            "native_size": [pw, ph],
            "world_size": [s["world_w"], s["world_h"]],
            "aspect": s["aspect"],
            "area": s["area"]
        })

    # Save terrain_atlas.png
    out_png = os.path.join(out_dir, "terrain_atlas.png")
    Image.fromarray(atlas_canvas, "RGBA").save(out_png, optimize=True)
    print(f"Saved {out_png}")

    # Save terrain_catalog.json
    out_json = os.path.join(out_dir, "terrain_catalog.json")
    catalog_data = {
        "version": 1,
        "texture": "terrain_atlas.png",
        "atlas_size": [atlas_w, atlas_h],
        "total_sprites": len(catalog_sprites),
        "sprites": catalog_sprites
    }
    with open(out_json, "w", encoding="utf-8") as f:
        json.dump(catalog_data, f, indent=2, ensure_ascii=False)
    print(f"Saved catalog JSON to {out_json}")

if __name__ == "__main__":
    slice_all_terrains()
