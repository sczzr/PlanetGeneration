import os
import json
import cv2
import numpy as np
from PIL import Image
from scipy import ndimage

def slice_all_trees():
    img_dir = r"f:\GameDev\Original\PlanetGenerationCore\csharp\resources\textures\guohua"
    out_dir = img_dir
    os.makedirs(out_dir, exist_ok=True)

    sources = [
        {"file": "single.png", "default_cat": "Single"},
        {"file": "single1.png", "default_cat": "Single"},
        {"file": "double.png", "default_cat": "Duo"},
        {"file": "double1.png", "default_cat": "Duo"},
        {"file": "three.png", "default_cat": "Trio"},
    ]

    all_sprites = []
    sprite_id = 0

    for src in sources:
        fname = src["file"]
        default_cat = src["default_cat"]
        fpath = os.path.join(img_dir, fname)
        if not os.path.exists(fpath):
            print(f"Skipping missing file: {fpath}")
            continue

        print(f"\nProcessing {fname} ({default_cat})...")
        img = Image.open(fpath).convert("RGB")
        arr = np.array(img, dtype=np.float32)
        h_total, w_total, _ = arr.shape

        # Estimate white background
        corners = np.vstack([arr[:100, :100], arr[:100, -100:], arr[-100:, :100], arr[-100:, -100:]])
        bg = np.mean(corners, axis=(0, 1))

        # Binary mask of non-background
        diff = np.linalg.norm(arr - bg, axis=2)
        bin_mask = (diff > 18.0).astype(np.uint8) * 255

        # Ignore calligraphy title on top-right of single1.png
        if fname == "single1.png":
            bin_mask[:900, 5000:] = 0

        # Close branches and leaves to group components
        kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (15, 15))
        closed = cv2.morphologyEx(bin_mask, cv2.MORPH_CLOSE, kernel)

        num_labels, labels, stats, centroids = cv2.connectedComponentsWithStats(closed, connectivity=8)
        print(f"  Detected {num_labels - 1} raw components in {fname}.")

        for i in range(1, num_labels):
            s = stats[i]
            x = int(s[cv2.CC_STAT_LEFT])
            y = int(s[cv2.CC_STAT_TOP])
            w = int(s[cv2.CC_STAT_WIDTH])
            h = int(s[cv2.CC_STAT_HEIGHT])
            area = int(s[cv2.CC_STAT_AREA])

            # Filter small noise / isolated specks
            if area < 8000 or w < 70 or h < 80:
                continue
            # Skip cut tree touching the calligraphy box border in single1.png
            if fname == "single1.png" and (x + w > 5000 or (x > 4700 and y < 1500)):
                print(f"  Skipping clipped/boundary sprite at x={x}, y={y}, w={w}, h={h} in {fname}")
                continue

            pad = 25
            x0_pad = max(0, x - pad)
            y0_pad = max(0, y - pad)
            x1_pad = min(w_total, x + w + pad)
            y1_pad = min(h_total, y + h + pad)

            crop_arr = arr[y0_pad:y1_pad, x0_pad:x1_pad].copy()
            sub_labels = labels[y0_pad:y1_pad, x0_pad:x1_pad]
            comp_mask = (sub_labels == i)
            comp_dilated = cv2.dilate(comp_mask.astype(np.uint8), np.ones((15, 15), np.uint8)) > 0

            # ── 1. Flood-fill background from outside borders (gemini_media pipeline) ──
            d = np.linalg.norm(crop_arr - 255.0, axis=2)
            bg_like = (d < 30.0) | (~comp_dilated)
            seed = np.zeros_like(bg_like, dtype=bool)
            seed[0, :] = seed[-1, :] = seed[:, 0] = seed[:, -1] = True
            seed = seed & bg_like
            bg_connected = ndimage.binary_propagation(seed, mask=bg_like)

            fg_mask = ~bg_connected

            # ── 2. Fill enclosed interior cavities (< 2500 px) in canopy ──
            hole_labels, num_holes = ndimage.label(~fg_mask)
            for h_idx in range(1, num_holes + 1):
                h_area = np.count_nonzero(hole_labels == h_idx)
                touches_border = (hole_labels[0, :] == h_idx).any() or (hole_labels[-1, :] == h_idx).any() or (hole_labels[:, 0] == h_idx).any() or (hole_labels[:, -1] == h_idx).any()
                if not touches_border and h_area < 2500:
                    fg_mask[hole_labels == h_idx] = True

            # ── 3. Binary erosion by 1 pixel to strip off white anti-aliased perimeter ──
            fg_eroded = ndimage.binary_erosion(fg_mask, iterations=1)

            # ── 4. Anti-aliasing via distance transform ──
            dist = cv2.distanceTransform(fg_eroded.astype(np.uint8), cv2.DIST_L2, 3)
            alpha = np.clip(dist / 1.2, 0.0, 1.0)

            # ── 5. Defringe neutral: eliminate low-saturation bright edge pixels (gemini_media defringe_neutral) ──
            rgb = crop_arr.copy()
            mx = rgb.max(axis=2)
            mn = rgb.min(axis=2)
            sat = (mx - mn) / 255.0
            fringe = (alpha > 0) & (alpha < 0.95) & (sat < 0.12) & (mx > 175)
            alpha[fringe] = 0.0

            solid_white = (alpha > 0.8) & (sat < 0.08) & (mx > 225)
            alpha[solid_white] = 0.0

            # ── 6. De-spill / un-premultiply white from boundary RGB ──
            edge_mask = (alpha > 0.01) & (alpha < 0.99)
            a = alpha[..., np.newaxis]
            unmixed_rgb = np.clip((rgb - 255.0 * (1.0 - a)) / np.maximum(a, 0.15), 0.0, 255.0)
            final_rgb = np.where(edge_mask[..., np.newaxis], unmixed_rgb, rgb)

            rgba_crop = np.dstack([final_rgb, alpha * 255.0]).astype(np.uint8)

            # Tight crop
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

            # Skip any residual sliced sprite with abnormal vertical aspect ratio
            if fname == "single1.png" and aspect < 0.35 and th > 600:
                print(f"  Skipping cut tree with aspect {aspect}: x={orig_x}, y={orig_y}")
                continue

            # Category & Family Classification
            cat = default_cat
            family = "Mixed"

            # Bush detection: short height or wide aspect ratio with low height
            if th < 550 and (th < 360 or aspect > 1.25):
                cat = "Bush"
                family = "Bush"
            elif "single" in fname and orig_y > 1700 and (3000 <= orig_x <= 4800 or (fname == "single1.png" and orig_x < 1800)):
                cat = "Willow"
                family = "Willow"
            elif cat == "Single":
                if aspect < 0.48:
                    family = "Cypress"
                elif (orig_x < 2400 and orig_y < 1200) or (orig_x < 1200 and orig_y < 2200):
                    family = "Pine"
                else:
                    family = "Broadleaf"
            elif cat == "Duo":
                # Classify 17 duos into proper families
                if "double1.png" in fname and (2800 <= orig_x <= 4200 and orig_y > 1100):
                    family = "Willow"
                elif "double1.png" in fname and orig_x < 1600 and orig_y > 1800:
                    family = "Willow"
                elif "double.png" in fname and orig_x > 2500 and orig_y < 1500:
                    family = "Pine"
                elif "double1.png" in fname and orig_x < 1600 and orig_y < 1200:
                    family = "Pine"
                elif "double.png" in fname and orig_x < 1500 and orig_y > 1200:
                    family = "Broadleaf"
                elif "double1.png" in fname and orig_x > 3800 and orig_y > 1200:
                    family = "Broadleaf"
                elif "double1.png" in fname and orig_x > 3500 and orig_y < 1200:
                    family = "Broadleaf"
                elif aspect > 1.15:
                    family = "Broadleaf"
                else:
                    family = "Mixed"
            elif cat == "Trio":
                # Trios in three.png: classify into Cypress or Pine
                # ID 71, 72, 73, 80 in three.png have columnar/conical foliage
                if (1500 <= orig_x <= 3500 and orig_y < 1200) or (orig_x < 1200 and orig_y > 2000):
                    family = "Cypress"
                else:
                    family = "Pine"

            # Compute precise trunk base pivot point (contact with ground)
            tree_rows = np.where(tight[:, :, 3] > 120)[0]
            if len(tree_rows) > 0:
                bottom_y = tree_rows.max()
                low_y_start = max(0, bottom_y - int(th * 0.08))
                low_pixels_x = np.where(tight[low_y_start:bottom_y+1, :, 3] > 120)[1]
                if len(low_pixels_x) > 0:
                    base_x = float(np.mean(low_pixels_x)) / float(tw)
                else:
                    base_x = 0.5
                base_y = float(bottom_y) / float(th)
            else:
                base_x = 0.5
                base_y = 0.95

            # World physical size: 0.020 scale
            uniform_scale = 0.020
            world_w = round(tw * uniform_scale, 2)
            world_h = round(th * uniform_scale, 2)

            all_sprites.append({
                "id": sprite_id,
                "source_file": fname,
                "orig_x": orig_x,
                "orig_y": orig_y,
                "category": cat,
                "family": family,
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

    print(f"\nExtracted a total of {len(all_sprites)} sprites.")

    # Group into single (Single + Bush + Willow) vs cluster (Duo + Trio)
    singles = [s for s in all_sprites if s["category"] in ("Single", "Bush", "Willow")]
    clusters = [s for s in all_sprites if s["category"] in ("Duo", "Trio")]

    print(f"Single/Bush/Willow sprites: {len(singles)}")
    print(f"Duo/Trio cluster sprites: {len(clusters)}")

    # Pack an atlas using Shelf / Skyline algorithm
    def pack_atlas(sprite_list, atlas_name, atlas_w=5120, padding=24):
        sorted_sprites = sorted(sprite_list, key=lambda s: s["th"], reverse=True)

        current_x = padding
        current_y = padding
        shelf_height = 0
        placed = []

        for s in sorted_sprites:
            w, h = s["tw"], s["th"]
            if current_x + w + padding > atlas_w:
                current_x = padding
                current_y += shelf_height + padding
                shelf_height = 0

            placed.append((s, current_x, current_y, w, h))
            current_x += w + padding
            if h > shelf_height:
                shelf_height = h

        total_h = current_y + shelf_height + padding
        print(f"Atlas {atlas_name}: {atlas_w} x {total_h}")

        atlas_canvas = np.zeros((total_h, atlas_w, 4), dtype=np.uint8)
        meta_list = []

        for s, px, py, pw, ph in placed:
            atlas_canvas[py:py+ph, px:px+pw] = s["img"]
            meta_list.append({
                "id": s["id"],
                "source": s["source_file"],
                "category": s["category"],
                "family": s["family"],
                "atlas": atlas_name,
                "rect": [px, py, pw, ph],
                "pivot": [s["pivot_x"], s["pivot_y"]],
                "native_size": [pw, ph],
                "world_size": [s["world_w"], s["world_h"]],
                "aspect": s["aspect"],
                "area": s["area"]
            })

        atlas_path = os.path.join(out_dir, atlas_name)
        Image.fromarray(atlas_canvas, "RGBA").save(atlas_path, optimize=True)
        print(f"Saved {atlas_path}")
        return meta_list

    meta_singles = pack_atlas(singles, "tree_atlas_single.png", atlas_w=5120)
    meta_clusters = pack_atlas(clusters, "tree_atlas_cluster.png", atlas_w=5120)

    # Sort metadata back by ID
    all_meta = sorted(meta_singles + meta_clusters, key=lambda m: m["id"])

    # Save tree_catalog.json
    catalog_json_path = os.path.join(out_dir, "tree_catalog.json")
    with open(catalog_json_path, "w", encoding="utf-8") as f:
        json.dump({
            "total_sprites": len(all_meta),
            "atlases": ["tree_atlas_single.png", "tree_atlas_cluster.png"],
            "sprites": all_meta
        }, f, indent=2, ensure_ascii=False)
    print(f"Saved catalog metadata to {catalog_json_path}")

if __name__ == "__main__":
    slice_all_trees()
