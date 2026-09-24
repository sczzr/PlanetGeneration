"""
Process hand-drawn mountains from csharp/architect/
(hill.png, 山峰.png, 雪山1.png, 雪山2.png, 奇异山貌.png, 奇异山貌2.png)
and reconstruct the entire mountain atlas ecosystem.

Completely discards legacy terrain_atlas, terrain_atlas_cluster, terrain_atlas_single.

Outputs:
  - csharp/resources/textures/guohua/mountain_atlas_single.png (Atomic single mountain pieces)
  - csharp/resources/textures/guohua/mountain_atlas_cluster.png (Composite mountain ranges, massifs, hill chains, snow ranges, plateau massifs, stone pillar forests)
  - csharp/resources/textures/guohua/terrain_catalog.json (Catalog metadata)
  - Standalone fallback textures:
      mountain_peak.png, mountain_hill.png, mountain_snow.png
"""

import os
import json
import random
import math
import cv2
import numpy as np
from PIL import Image

def main():
    base_dir = r"f:\GameDev\Original\PlanetGenerationCore"
    architect_dir = os.path.join(base_dir, "csharp", "architect")
    guohua_dir = os.path.join(base_dir, "csharp", "resources", "textures", "guohua")
    os.makedirs(guohua_dir, exist_ok=True)

    print("=========================================================")
    print(" Step 1: Extracting All Single Peaks from architect/ ")
    print("=========================================================")
    single_sprites = extract_all_single_peaks(architect_dir)
    print(f"Total atomic single mountain units (including flips): {len(single_sprites)}")

    print("\n=========================================================")
    print(" Step 2: Synthesizing Composite Mountain Ranges & Clusters ")
    print("=========================================================")
    cluster_sprites = synthesize_composite_clusters(single_sprites)
    print(f"Total composite mountain clusters created: {len(cluster_sprites)}")

    print("\n=========================================================")
    print(" Step 3: Deriving Auxiliary Landforms (Desert, Grass, Wetland) ")
    print("=========================================================")
    aux_singles, aux_clusters = derive_auxiliary_landforms(single_sprites, cluster_sprites)
    all_singles = single_sprites + aux_singles
    all_clusters = cluster_sprites + aux_clusters
    print(f"Final Singles count: {len(all_singles)}")
    print(f"Final Clusters count: {len(all_clusters)}")

    print("\n=========================================================")
    print(" Step 4: Packing Atlases & Exporting Metadata ")
    print("=========================================================")
    pack_all_mountain_atlases(all_singles, all_clusters, guohua_dir)

    print("\n=========================================================")
    print(" Step 5: Exporting Representative Fallback Textures ")
    print("=========================================================")
    export_fallback_textures(all_singles, guohua_dir)

    print("\n=========================================================")
    print(" Step 6: Cleaning Legacy terrain_atlas Files ")
    print("=========================================================")
    clean_legacy_atlases(guohua_dir)

    print("\n=========================================================")
    print(" Mountain Atlas Ecosystem Reconstruction Complete! ")
    print("=========================================================")


def compute_terrain_pivot(tight_rgba):
    th, tw = tight_rgba.shape[:2]
    alpha = tight_rgba[:, :, 3]
    opaque_rows = np.where(alpha > 80)[0]
    if len(opaque_rows) > 0:
        bottom_y = opaque_rows.max()
        low_y_start = max(0, bottom_y - int(th * 0.10))
        low_pixels_x = np.where(tight_rgba[low_y_start:bottom_y + 1, :, 3] > 80)[1]
        px = float(np.mean(low_pixels_x)) / float(tw) if len(low_pixels_x) > 0 else 0.5
        py = float(bottom_y) / float(th)
    else:
        px, py = 0.5, 0.95
    return round(px, 3), round(py, 3)


def feather_perimeter(rgba, pad_lr=30, pad_bottom=18):
    """
    Apply smooth cosine feathering to lateral flanks (left/right) and base (bottom)
    to guarantee zero hard cuts, zero boundary staircasing, and natural soft ground integration.
    """
    h, w = rgba.shape[:2]
    out = rgba.copy()
    alpha = out[:, :, 3].astype(np.float32)

    # 1. Lateral flanks (left & right)
    actual_lr = min(pad_lr, w // 4)
    if actual_lr > 0:
        for x in range(actual_lr):
            factor = 0.5 * (1.0 - math.cos(math.pi * x / float(actual_lr)))
            alpha[:, x] *= factor
            alpha[:, w - 1 - x] *= factor

    # 2. Base ground contact (bottom)
    actual_b = min(pad_bottom, h // 4)
    if actual_b > 0:
        for y in range(actual_b):
            factor = 0.5 * (1.0 - math.cos(math.pi * y / float(actual_b)))
            alpha[h - 1 - y, :] *= factor

    out[:, :, 3] = np.clip(alpha, 0, 255).astype(np.uint8)
    return out


def extract_isolated_components(img_path, min_area=3000):
    im = Image.open(img_path)
    arr = np.array(im)
    alpha = arr[:, :, 3]
    bin_mask = (alpha > 20).astype(np.uint8)
    num, labels, stats, _ = cv2.connectedComponentsWithStats(bin_mask)
    components = []
    for i in range(1, num):
        s = stats[i]
        area = int(s[cv2.CC_STAT_AREA])
        if area < min_area:
            continue
        x = int(s[cv2.CC_STAT_LEFT])
        y = int(s[cv2.CC_STAT_TOP])
        w = int(s[cv2.CC_STAT_WIDTH])
        h = int(s[cv2.CC_STAT_HEIGHT])

        crop = arr[y:y + h, x:x + w].copy()
        mask = (labels[y:y + h, x:x + w] == i)
        crop[~mask] = 0

        alphas = crop[:, :, 3]
        ys, xs = np.where(alphas > 5)
        if len(xs) == 0 or len(ys) == 0:
            continue
        cx0, cx1 = int(xs.min()), int(xs.max()) + 1
        cy0, cy1 = int(ys.min()), int(ys.max()) + 1
        tight = crop[cy0:cy1, cx0:cx1]

        components.append({
            "img": tight,
            "orig_x": x + cx0,
            "orig_y": y + cy0,
            "w": cx1 - cx0,
            "h": cy1 - cy0,
            "area": area
        })
    # Sort top-to-bottom, left-to-right
    components.sort(key=lambda c: (c["orig_y"] // 400, c["orig_x"]))
    return components


TEX_SCALE = 0.30


def extract_all_single_peaks(architect_dir):
    sources = [
        # (filename, category, scale, subcat_rules)
        ("hill.png", "Hills", 0.022),
        ("山峰.png", "Mountain", 0.030),
        ("雪山1.png", "SnowMountain", 0.030),
        ("雪山2.png", "SnowMountain", 0.030),
        ("奇异山貌.png", "Exotic1", 0.026),
        ("奇异山貌2.png", "Exotic2", 0.026)
    ]

    all_singles = []

    for fname, family, default_scale in sources:
        fpath = os.path.join(architect_dir, fname)
        if not os.path.exists(fpath):
            print(f"Warning: {fpath} does not exist!")
            continue
        comps = extract_isolated_components(fpath, min_area=3000)
        print(f"Extracted {len(comps)} raw components from {fname}")

        for idx, c in enumerate(comps):
            raw_img = c["img"]
            tw, th = c["w"], c["h"]
            aspect = round(tw / max(th, 1), 2)

            # Downsample high-res piece for game runtime atlas (max 4K sheet)
            target_w = max(4, int(round(tw * TEX_SCALE)))
            target_h = max(4, int(round(th * TEX_SCALE)))
            scaled_raw = np.array(Image.fromarray(raw_img).resize((target_w, target_h), Image.Resampling.LANCZOS))

            pad_lr = max(2, int(round(30 * TEX_SCALE)))
            pad_bottom = max(2, int(round(18 * TEX_SCALE)))
            feathered = feather_perimeter(scaled_raw, pad_lr=pad_lr, pad_bottom=pad_bottom)
            px, py = compute_terrain_pivot(feathered)

            # Detailed classification per source file
            if family == "Hills":
                cat = "Hills"
                sub_cat = "Knoll"
                scale = 0.022
                name = f"hill_knoll_{idx:02d}"

            elif family == "Mountain":
                cat = "Mountain"
                scale = 0.030
                if idx in (0, 1, 4, 5):
                    sub_cat = "MainPeak"
                    name = f"mountain_main_peak_{idx:02d}"
                elif idx in (7, 8):
                    sub_cat = "Ridge"
                    name = f"mountain_ridge_{idx:02d}"
                else:
                    sub_cat = "CompanionPeak"
                    name = f"mountain_companion_peak_{idx:02d}"

            elif family == "SnowMountain":
                cat = "SnowMountain"
                scale = 0.030
                # Check sharpness and aspect
                if fname == "雪山1.png":
                    if idx in (0, 1, 5):
                        sub_cat = "GlacierPeak"
                        name = f"snow_glacier_peak_1_{idx:02d}"
                    elif idx == 4:
                        sub_cat = "SnowRidge"
                        name = f"snow_ridge_1_{idx:02d}"
                    else:
                        sub_cat = "SnowCompanionPeak"
                        name = f"snow_companion_peak_1_{idx:02d}"
                else: # 雪山2.png
                    if idx in (0, 1, 2, 5):
                        sub_cat = "GlacierPeak"
                        name = f"snow_glacier_peak_2_{idx:02d}"
                    elif idx == 4:
                        sub_cat = "SnowRidge"
                        name = f"snow_ridge_2_{idx:02d}"
                    else:
                        sub_cat = "SnowCompanionPeak"
                        name = f"snow_companion_peak_2_{idx:02d}"

            elif family == "Exotic1":
                # 奇异山貌1: mesas, cliff pillars, encircling arms
                scale = 0.026
                if idx in (3, 6):
                    cat = "Plateau"
                    sub_cat = "CliffPillar"
                    name = f"plateau_cliff_pillar_1_{idx:02d}"
                elif idx in (4, 8):
                    cat = "Basin"
                    sub_cat = "EncirclingArm"
                    name = f"basin_encircling_arm_1_{idx:02d}"
                else:
                    cat = "Plateau"
                    sub_cat = "TableMesa"
                    name = f"plateau_table_mesa_1_{idx:02d}"

            elif family == "Exotic2":
                # 奇异山貌2: volcanoes, spires, craters, wide arms
                scale = 0.026
                if idx in (0, 4):
                    cat = "Volcano"
                    sub_cat = "VolcanoCone"
                    name = f"volcano_cone_2_{idx:02d}"
                elif idx == 2:
                    cat = "Volcano"
                    sub_cat = "VolcanoCrater"
                    name = f"volcano_crater_2_{idx:02d}"
                elif idx == 3:
                    cat = "Plateau"
                    sub_cat = "CliffPillar"
                    name = f"plateau_cliff_pillar_2_{idx:02d}"
                elif idx == 1:
                    cat = "Basin"
                    sub_cat = "EncirclingArm"
                    name = f"basin_encircling_arm_2_{idx:02d}"
                else:
                    cat = "Plateau"
                    sub_cat = "TableMesa"
                    name = f"plateau_table_mesa_2_{idx:02d}"

            # 1. Original piece (preserving exact physical world_w & world_h)
            orig_item = {
                "category": cat,
                "sub_category": sub_cat,
                "raw_img": scaled_raw,
                "img": feathered,
                "w": target_w,
                "h": target_h,
                "aspect": aspect,
                "area": int(np.count_nonzero(feathered[:, :, 3] > 20)),
                "pivot_x": px,
                "pivot_y": py,
                "world_w": round(tw * scale, 2),
                "world_h": round(th * scale, 2),
                "name": name,
                "is_composite": False
            }
            all_singles.append(orig_item)

            # 2. Horizontally mirrored variant for visual diversity
            flipped_raw = cv2.flip(scaled_raw, 1)
            flipped_img = cv2.flip(feathered, 1)
            flip_item = {
                "category": cat,
                "sub_category": sub_cat,
                "raw_img": flipped_raw,
                "img": flipped_img,
                "w": target_w,
                "h": target_h,
                "aspect": aspect,
                "area": orig_item["area"],
                "pivot_x": round(1.0 - px, 3),
                "pivot_y": py,
                "world_w": orig_item["world_w"],
                "world_h": orig_item["world_h"],
                "name": f"{name}_flip",
                "is_composite": False
            }
            all_singles.append(flip_item)

    return all_singles


def compose_cluster(pieces_spec, cluster_name, cat, sub_cat, world_scale=0.024, pad_bottom=22):
    """
    Synthesize a composite mountain cluster adhering strictly to the universal grounding rule:
    '物体的底部只能覆盖地面，不能覆盖任何其他的贴图，但是可以被其他素材覆盖。'
    
    1. 前景山峰主体（雪顶、岩峰、山脊）按 Painter 算法自然遮挡后方背景；
    2. 前景山峰底部接地部分（草皮裙边、地基泥土、下边缘）只在画布为空（地面）时绘制；
    3. 当重叠区域已有其他山峰素材时，底部接地部分直接丢弃，绝不在山岩上涂抹草皮或泥块；
    4. 仅对最终拼装完成的整个模组最外侧接触地面的底缘进行平滑接地柔化。
    """
    # 1. Compute bounding boxes
    boxes = []
    for p in pieces_spec:
        s = p["item"]
        scale = p["scale"]
        pw = max(10, int(s["w"] * scale))
        ph = max(10, int(s["h"] * scale))
        flip = p["flip"]
        pv_x = (1.0 - s["pivot_x"]) if flip else s["pivot_x"]
        pv_y = s["pivot_y"]

        pos_x = p["x"] * TEX_SCALE
        pos_y = p["y"] * TEX_SCALE
        left = pos_x - pw * pv_x
        top = pos_y - ph * pv_y
        right = left + pw
        bottom = top + ph
        boxes.append((p, pw, ph, pv_x, pv_y, left, top, right, bottom))

    min_left = min(b[5] for b in boxes)
    min_top = min(b[6] for b in boxes)
    max_right = max(b[7] for b in boxes)
    max_bottom = max(b[8] for b in boxes)

    margin = max(10, int(round(50 * TEX_SCALE)))
    offset_x = -min_left + margin
    offset_y = -min_top + margin
    canvas_w = int(max_right - min_left + margin * 2) + 20
    canvas_h = int(max_bottom - min_top + margin * 2) + 20

    # Canvas buffer: RGBA
    canvas = np.zeros((canvas_h, canvas_w, 4), dtype=np.uint8)

    # Sort pieces by base Y (Painter's algorithm: background peaks drawn first, foreground occludes)
    sorted_boxes = sorted(boxes, key=lambda b: b[0]["y"])

    for p, pw, ph, pv_x, pv_y, left, top, right, bottom in sorted_boxes:
        s = p["item"]
        raw_source = s.get("raw_img", s["img"])
        p_img = Image.fromarray(raw_source).resize((pw, ph), Image.Resampling.LANCZOS)
        if p["flip"]:
            p_img = p_img.transpose(Image.Transpose.FLIP_LEFT_RIGHT)

        p_arr = np.array(p_img)
        paste_x = int(left + offset_x)
        paste_y = int(top + offset_y)
        piece_h, piece_w = p_arr.shape[:2]
        cat_name = s.get("category", cat)

        c_slice = canvas[paste_y:paste_y + piece_h, paste_x:paste_x + piece_w]
        c_alpha = c_slice[:, :, 3]

        p_alpha = p_arr[:, :, 3]
        vis_mask = p_alpha > 20

        # 计算该单峰的“接地部分/底部”掩码
        y_indices = np.arange(piece_h).reshape(-1, 1).repeat(piece_w, axis=1)
        y_ratios = y_indices / float(max(piece_h, 1))

        r = p_arr[:, :, 0]
        g = p_arr[:, :, 1]
        b = p_arr[:, :, 2]

        skirt_mask = np.zeros((piece_h, piece_w), dtype=bool)
        if cat_name in ("Mountain", "SnowMountain"):
            green_cond = (y_ratios >= 0.50) & (g.astype(float) > np.maximum(r.astype(float) * 0.95, b.astype(float) * 1.08))
            skirt_mask |= green_cond
        elif cat_name in ("Hills", "Wetland"):
            skirt_mask |= (y_ratios >= 0.92)
        elif cat_name in ("Plateau", "Volcano", "Basin"):
            skirt_mask |= (y_ratios >= 0.90)

        # 最底部边缘（底座羽化带）统一定为接地部分
        skirt_mask |= (y_ratios >= 0.96)

        # 规则 1：画布为空白地面时，主体与接地裙边皆可绘制
        draw_on_empty = vis_mask & (c_alpha < 30)

        # 规则 2：画布已有其他山体素材时，仅主体绘制（主体正常遮挡），接地裙边绝不覆盖已有素材
        draw_on_occupied = vis_mask & (c_alpha >= 30) & (~skirt_mask)

        final_draw_mask = draw_on_empty | draw_on_occupied
        c_slice[final_draw_mask] = p_arr[final_draw_mask]
        canvas[paste_y:paste_y + piece_h, paste_x:paste_x + piece_w] = c_slice

    alpha = canvas[:, :, 3]
    ys, xs = np.where(alpha > 10)
    if len(xs) == 0 or len(ys) == 0:
        return None

    min_x, max_x = int(xs.min()), int(xs.max()) + 1
    min_y, max_y = int(ys.min()), int(ys.max()) + 1
    tight_arr = canvas[min_y:max_y, min_x:max_x]

    # 仅对接触地图土地的最外层底缘执行柔和接地过渡
    pad_lr = max(2, int(round(35 * TEX_SCALE)))
    pad_bot = max(2, int(round(pad_bottom * TEX_SCALE)))
    tight_feathered = feather_perimeter(tight_arr, pad_lr=pad_lr, pad_bottom=pad_bot)
    px, py = compute_terrain_pivot(tight_feathered)

    tw = tight_feathered.shape[1]
    th = tight_feathered.shape[0]

    return {
        "category": cat,
        "sub_category": sub_cat,
        "img": tight_feathered,
        "w": tw,
        "h": th,
        "aspect": round(tw / max(th, 1), 2),
        "area": int(np.count_nonzero(tight_feathered[:, :, 3] > 20)),
        "pivot_x": px,
        "pivot_y": py,
        "world_w": round(tw * (world_scale / TEX_SCALE), 2),
        "world_h": round(th * (world_scale / TEX_SCALE), 2),
        "name": cluster_name,
        "is_composite": True
    }


def synthesize_composite_clusters(singles):
    """
    Synthesize rich composite mountain landforms according to category:
    - Mountain: GrandRange (continuous long ranges), MassifCluster (triad & dominant massifs)
    - Hills: HillChain (undulating knoll ranges), HillCopse (knoll copses)
    - SnowMountain: SnowGrandRange (glacier ranges), SnowMassif (snow massifs)
    - Plateau: PlateauMassif (tableland mesas), KarstPillars (cliff pillars)
    - Basin: EncirclingArm, CalderaRing
    - Volcano: VolcanoCluster
    """
    clusters = []

    # Filter base pools
    knolls = [s for s in singles if s["category"] == "Hills" and not s["name"].endswith("_flip")]
    main_peaks = [s for s in singles if s["category"] == "Mountain" and s["sub_category"] == "MainPeak" and not s["name"].endswith("_flip")]
    comp_peaks = [s for s in singles if s["category"] == "Mountain" and s["sub_category"] == "CompanionPeak" and not s["name"].endswith("_flip")]
    ridges = [s for s in singles if s["category"] == "Mountain" and s["sub_category"] == "Ridge" and not s["name"].endswith("_flip")]

    snow_glaciers = [s for s in singles if s["category"] == "SnowMountain" and s["sub_category"] == "GlacierPeak" and not s["name"].endswith("_flip")]
    snow_comps = [s for s in singles if s["category"] == "SnowMountain" and s["sub_category"] == "SnowCompanionPeak" and not s["name"].endswith("_flip")]
    snow_ridges = [s for s in singles if s["category"] == "SnowMountain" and s["sub_category"] == "SnowRidge" and not s["name"].endswith("_flip")]

    mesas = [s for s in singles if s["category"] == "Plateau" and s["sub_category"] == "TableMesa" and not s["name"].endswith("_flip")]
    pillars = [s for s in singles if s["category"] == "Plateau" and s["sub_category"] == "CliffPillar" and not s["name"].endswith("_flip")]
    arms = [s for s in singles if s["category"] == "Basin" and not s["name"].endswith("_flip")]
    volcanoes = [s for s in singles if s["category"] == "Volcano" and not s["name"].endswith("_flip")]

    # -------------------------------------------------------------
    # 1. 青绿主山脉组合 (Mountain: GrandRange & MassifCluster)
    # -------------------------------------------------------------

    # 1.1 连绵千里长脊 A (5 peaks: knoll -> ridge -> main peak -> ridge -> knoll)
    p_spec_gr1 = [
        {"item": knolls[0], "x": 0, "y": 60, "scale": 0.85, "flip": False},
        {"item": ridges[0], "x": 750, "y": 20, "scale": 0.95, "flip": False},
        {"item": main_peaks[0], "x": 1600, "y": 0, "scale": 1.15, "flip": False},
        {"item": ridges[1 % len(ridges)], "x": 2550, "y": 30, "scale": 0.92, "flip": False},
        {"item": knolls[1 % len(knolls)], "x": 3400, "y": 70, "scale": 0.82, "flip": True},
    ]
    c = compose_cluster(p_spec_gr1, "mountain_grand_range_01", "Mountain", "GrandRange", world_scale=0.020)
    if c: clusters.append(c)

    # 1.2 蜿蜒龙脊连绵长脊 B (6 peaks: S-curve winding crest)
    p_spec_gr2 = [
        {"item": comp_peaks[0], "x": 0, "y": 80, "scale": 0.85, "flip": False},
        {"item": main_peaks[1 % len(main_peaks)], "x": 700, "y": 10, "scale": 1.05, "flip": False},
        {"item": comp_peaks[1 % len(comp_peaks)], "x": 1500, "y": 90, "scale": 0.90, "flip": True},
        {"item": main_peaks[2 % len(main_peaks)], "x": 2250, "y": 0, "scale": 1.18, "flip": False},
        {"item": ridges[0], "x": 3150, "y": 60, "scale": 0.95, "flip": False},
        {"item": knolls[2 % len(knolls)], "x": 3950, "y": 100, "scale": 0.80, "flip": True},
    ]
    c = compose_cluster(p_spec_gr2, "mountain_grand_range_02", "Mountain", "GrandRange", world_scale=0.019)
    if c: clusters.append(c)

    # 1.3 屏障崇山峻岭 C (4 peaks: high double-summit barrier)
    p_spec_gr3 = [
        {"item": comp_peaks[2 % len(comp_peaks)], "x": 0, "y": 50, "scale": 0.88, "flip": False},
        {"item": main_peaks[3 % len(main_peaks)], "x": 780, "y": 0, "scale": 1.10, "flip": False},
        {"item": main_peaks[0], "x": 1650, "y": 20, "scale": 1.08, "flip": True},
        {"item": comp_peaks[0], "x": 2450, "y": 60, "scale": 0.85, "flip": True},
    ]
    c = compose_cluster(p_spec_gr3, "mountain_grand_range_03", "Mountain", "GrandRange", world_scale=0.021)
    if c: clusters.append(c)

    # 1.4 众星拱月主峰群 A (Triad: 1 dominant center peak flanked by 2 companion peaks)
    p_spec_mc1 = [
        {"item": comp_peaks[0], "x": 0, "y": 40, "scale": 0.88, "flip": False},
        {"item": main_peaks[0], "x": 650, "y": 0, "scale": 1.15, "flip": False},
        {"item": comp_peaks[1 % len(comp_peaks)], "x": 1380, "y": 50, "scale": 0.85, "flip": True},
    ]
    c = compose_cluster(p_spec_mc1, "mountain_massif_triad_01", "Mountain", "MassifCluster", world_scale=0.025)
    if c: clusters.append(c)

    # 1.5 众星拱月主峰群 B (4 peaks: dominant summit with foreground foothill knoll)
    p_spec_mc2 = [
        {"item": comp_peaks[2 % len(comp_peaks)], "x": 0, "y": 20, "scale": 0.90, "flip": False},
        {"item": main_peaks[1 % len(main_peaks)], "x": 720, "y": 0, "scale": 1.20, "flip": False},
        {"item": comp_peaks[0], "x": 1450, "y": 30, "scale": 0.88, "flip": True},
        {"item": knolls[0], "x": 700, "y": 140, "scale": 0.70, "flip": False},
    ]
    c = compose_cluster(p_spec_mc2, "mountain_massif_cluster_02", "Mountain", "MassifCluster", world_scale=0.024)
    if c: clusters.append(c)

    # 1.6 横岭侧翼双峰 (Flanked ridge massif)
    p_spec_mc3 = [
        {"item": ridges[0], "x": 0, "y": 40, "scale": 0.92, "flip": False},
        {"item": main_peaks[2 % len(main_peaks)], "x": 950, "y": 0, "scale": 1.12, "flip": False},
        {"item": ridges[1 % len(ridges)], "x": 1900, "y": 45, "scale": 0.90, "flip": True},
    ]
    c = compose_cluster(p_spec_mc3, "mountain_massif_flanked_01", "Mountain", "MassifCluster", world_scale=0.022)
    if c: clusters.append(c)

    # -------------------------------------------------------------
    # 2. 丘陵缓坡组合 (Hills: HillChain & HillCopse)
    # -------------------------------------------------------------

    # 2.1 起伏丘陵带 A (4 knolls chain)
    p_spec_hc1 = [
        {"item": knolls[0], "x": 0, "y": 30, "scale": 0.90, "flip": False},
        {"item": knolls[1 % len(knolls)], "x": 700, "y": 0, "scale": 1.05, "flip": False},
        {"item": knolls[2 % len(knolls)], "x": 1450, "y": 25, "scale": 0.95, "flip": True},
        {"item": knolls[3 % len(knolls)], "x": 2150, "y": 40, "scale": 0.88, "flip": False},
    ]
    c = compose_cluster(p_spec_hc1, "hill_chain_01", "Hills", "HillChain", world_scale=0.020)
    if c: clusters.append(c)

    # 2.2 连绵丘陵带 B (5 knolls long chain)
    p_spec_hc2 = [
        {"item": knolls[4 % len(knolls)], "x": 0, "y": 40, "scale": 0.85, "flip": False},
        {"item": knolls[5 % len(knolls)], "x": 650, "y": 10, "scale": 1.00, "flip": False},
        {"item": knolls[6 % len(knolls)], "x": 1350, "y": 0, "scale": 1.10, "flip": True},
        {"item": knolls[7 % len(knolls)], "x": 2080, "y": 20, "scale": 0.95, "flip": False},
        {"item": knolls[8 % len(knolls)], "x": 2750, "y": 50, "scale": 0.82, "flip": True},
    ]
    c = compose_cluster(p_spec_hc2, "hill_chain_02", "Hills", "HillChain", world_scale=0.019)
    if c: clusters.append(c)

    # 2.3 翠峦小簇 (3 knolls copse)
    p_spec_hcp1 = [
        {"item": knolls[0], "x": 0, "y": 20, "scale": 0.90, "flip": False},
        {"item": knolls[3 % len(knolls)], "x": 580, "y": 0, "scale": 1.05, "flip": False},
        {"item": knolls[1 % len(knolls)], "x": 1150, "y": 30, "scale": 0.88, "flip": True},
    ]
    c = compose_cluster(p_spec_hcp1, "hill_copse_01", "Hills", "HillCopse", world_scale=0.022)
    if c: clusters.append(c)

    # 2.4 茶丘缓坡 (4 knolls copse with foreground knoll)
    p_spec_hcp2 = [
        {"item": knolls[2 % len(knolls)], "x": 0, "y": 10, "scale": 0.95, "flip": False},
        {"item": knolls[5 % len(knolls)], "x": 680, "y": 0, "scale": 1.10, "flip": True},
        {"item": knolls[4 % len(knolls)], "x": 1350, "y": 20, "scale": 0.92, "flip": False},
        {"item": knolls[7 % len(knolls)], "x": 650, "y": 120, "scale": 0.75, "flip": False},
    ]
    c = compose_cluster(p_spec_hcp2, "hill_copse_02", "Hills", "HillCopse", world_scale=0.021)
    if c: clusters.append(c)

    # -------------------------------------------------------------
    # 3. 高山雪脉组合 (SnowMountain: SnowGrandRange & SnowMassif)
    # -------------------------------------------------------------

    # 3.1 万仞冰川长脊 A (5 snow peaks)
    p_spec_sgr1 = [
        {"item": snow_comps[0], "x": 0, "y": 60, "scale": 0.85, "flip": False},
        {"item": snow_glaciers[0], "x": 680, "y": 10, "scale": 1.08, "flip": False},
        {"item": snow_glaciers[1 % len(snow_glaciers)], "x": 1500, "y": 0, "scale": 1.18, "flip": False},
        {"item": snow_ridges[0], "x": 2350, "y": 40, "scale": 0.95, "flip": True},
        {"item": snow_comps[1 % len(snow_comps)], "x": 3150, "y": 70, "scale": 0.80, "flip": True},
    ]
    c = compose_cluster(p_spec_sgr1, "snow_grand_range_01", "SnowMountain", "SnowGrandRange", world_scale=0.020)
    if c: clusters.append(c)

    # 3.2 极地刀脊雪脉 B (5 alpine jagged peaks from 雪山2)
    p_spec_sgr2 = [
        {"item": snow_ridges[1 % len(snow_ridges)], "x": 0, "y": 50, "scale": 0.90, "flip": False},
        {"item": snow_glaciers[2 % len(snow_glaciers)], "x": 800, "y": 0, "scale": 1.15, "flip": False},
        {"item": snow_glaciers[3 % len(snow_glaciers)], "x": 1680, "y": 15, "scale": 1.10, "flip": True},
        {"item": snow_glaciers[4 % len(snow_glaciers)], "x": 2500, "y": 0, "scale": 1.12, "flip": False},
        {"item": snow_comps[2 % len(snow_comps)], "x": 3320, "y": 60, "scale": 0.85, "flip": True},
    ]
    c = compose_cluster(p_spec_sgr2, "snow_grand_range_02", "SnowMountain", "SnowGrandRange", world_scale=0.019)
    if c: clusters.append(c)

    # 3.3 极地双极雪峰 (Twin glacier massif)
    p_spec_sm1 = [
        {"item": snow_comps[0], "x": 0, "y": 30, "scale": 0.85, "flip": False},
        {"item": snow_glaciers[0], "x": 650, "y": 0, "scale": 1.15, "flip": False},
        {"item": snow_glaciers[1 % len(snow_glaciers)], "x": 1420, "y": 10, "scale": 1.10, "flip": True},
        {"item": snow_comps[1 % len(snow_comps)], "x": 2150, "y": 40, "scale": 0.82, "flip": True},
    ]
    c = compose_cluster(p_spec_sm1, "snow_massif_cluster_01", "SnowMountain", "SnowMassif", world_scale=0.022)
    if c: clusters.append(c)

    # 3.4 极地雪峰群 B (3 peaks triad)
    p_spec_sm2 = [
        {"item": snow_comps[2 % len(snow_comps)], "x": 0, "y": 35, "scale": 0.88, "flip": False},
        {"item": snow_glaciers[2 % len(snow_glaciers)], "x": 680, "y": 0, "scale": 1.20, "flip": False},
        {"item": snow_comps[0], "x": 1420, "y": 40, "scale": 0.85, "flip": True},
    ]
    c = compose_cluster(p_spec_sm2, "snow_massif_cluster_02", "SnowMountain", "SnowMassif", world_scale=0.024)
    if c: clusters.append(c)

    # -------------------------------------------------------------
    # 4. 奇异地貌组合 (Plateau / Karst / Basin / Volcano)
    # -------------------------------------------------------------

    # 4.1 高原断崖台地群 A (3 table mesas continuous tableland)
    if len(mesas) >= 3:
        p_spec_pm1 = [
            {"item": mesas[0], "x": 0, "y": 20, "scale": 0.95, "flip": False},
            {"item": mesas[1], "x": 800, "y": 0, "scale": 1.10, "flip": False},
            {"item": mesas[2], "x": 1650, "y": 25, "scale": 0.92, "flip": True},
        ]
        c = compose_cluster(p_spec_pm1, "plateau_massif_01", "Plateau", "PlateauMassif", world_scale=0.022)
        if c: clusters.append(c)

    # 4.2 阶梯状红砂台地 B (2 large mesas)
    if len(mesas) >= 2:
        p_spec_pm2 = [
            {"item": mesas[1 % len(mesas)], "x": 0, "y": 0, "scale": 1.05, "flip": False},
            {"item": mesas[0], "x": 920, "y": 35, "scale": 0.98, "flip": True},
        ]
        c = compose_cluster(p_spec_pm2, "plateau_massif_02", "Plateau", "PlateauMassif", world_scale=0.024)
        if c: clusters.append(c)

    # 4.3 张家界剑阁石林群 A (4 cliff pillars clustered)
    if len(pillars) >= 2:
        p_spec_pf1 = [
            {"item": pillars[0], "x": 0, "y": 30, "scale": 0.88, "flip": False},
            {"item": pillars[1 % len(pillars)], "x": 450, "y": 0, "scale": 1.15, "flip": False},
            {"item": pillars[0], "x": 950, "y": 20, "scale": 0.92, "flip": True},
            {"item": pillars[1 % len(pillars)], "x": 1400, "y": 45, "scale": 0.85, "flip": True},
        ]
        c = compose_cluster(p_spec_pf1, "karst_pillar_forest_01", "Plateau", "CliffPillar", world_scale=0.022)
        if c: clusters.append(c)

    # 4.4 盆地半月形环抱山臂 A (4 peaks arc)
    if len(arms) >= 2:
        p_spec_ba1 = [
            {"item": arms[0], "x": 0, "y": 100, "scale": 0.88, "flip": False},
            {"item": arms[1 % len(arms)], "x": 650, "y": 20, "scale": 1.05, "flip": False},
            {"item": arms[0], "x": 1450, "y": 0, "scale": 1.10, "flip": True},
            {"item": arms[1 % len(arms)], "x": 2250, "y": 80, "scale": 0.90, "flip": True},
        ]
        c = compose_cluster(p_spec_ba1, "basin_encircling_arm_01", "Basin", "EncirclingArm", world_scale=0.021)
        if c: clusters.append(c)

    # 4.5 环形火山口 / 盆地环山 (Caldera Ring)
    if len(arms) >= 1 and len(volcanoes) >= 1:
        p_spec_cr1 = [
            {"item": arms[0], "x": 0, "y": 40, "scale": 0.95, "flip": False},
            {"item": volcanoes[0], "x": 800, "y": 0, "scale": 1.08, "flip": False},
            {"item": arms[0], "x": 1600, "y": 40, "scale": 0.95, "flip": True},
        ]
        c = compose_cluster(p_spec_cr1, "caldera_ring_01", "Basin", "CalderaRing", world_scale=0.022)
        if c: clusters.append(c)

    # 4.6 火山赤峦群 (Volcano Cluster)
    if len(volcanoes) >= 2:
        p_spec_vc1 = [
            {"item": volcanoes[1 % len(volcanoes)], "x": 0, "y": 30, "scale": 0.90, "flip": False},
            {"item": volcanoes[0], "x": 650, "y": 0, "scale": 1.15, "flip": False},
            {"item": volcanoes[1 % len(volcanoes)], "x": 1380, "y": 40, "scale": 0.85, "flip": True},
        ]
        c = compose_cluster(p_spec_vc1, "volcano_cluster_01", "Volcano", "VolcanoCrater", world_scale=0.023)
        if c: clusters.append(c)

    return clusters


def derive_auxiliary_landforms(singles, clusters):
    """
    Derive harmonious auxiliary landforms (Desert, Grassland, Wetland)
    from the new high-resolution hill components to ensure all terrain categories
    have crisp, matching hand-drawn assets.
    """
    aux_singles = []
    aux_clusters = []

    knolls = [s for s in singles if s["category"] == "Hills" and not s["name"].endswith("_flip")]

    # 1. Desert Sand Dunes & Ridges
    # Golden ochre palette (#D6A256, #BF853B)
    for idx, k in enumerate(knolls[:3]):
        img = k["img"].copy()
        alpha = img[:, :, 3]
        r = img[:, :, 0].astype(np.float32)
        g = img[:, :, 1].astype(np.float32)
        b = img[:, :, 2].astype(np.float32)

        r_sand = r * 0.50 + 215.0 * 0.50
        g_sand = g * 0.45 + 165.0 * 0.55
        b_sand = b * 0.30 + 88.0 * 0.70

        img[:, :, 0] = np.clip(r_sand, 0, 255).astype(np.uint8)
        img[:, :, 1] = np.clip(g_sand, 0, 255).astype(np.uint8)
        img[:, :, 2] = np.clip(b_sand, 0, 255).astype(np.uint8)

        sub = "CrescentDune" if idx == 0 else ("SandRipple" if idx == 1 else "Oasis")
        aux_singles.append({
            "category": "Desert",
            "sub_category": sub,
            "img": img,
            "w": k["w"],
            "h": k["h"],
            "aspect": k["aspect"],
            "area": k["area"],
            "pivot_x": k["pivot_x"],
            "pivot_y": k["pivot_y"],
            "world_w": k["world_w"],
            "world_h": k["world_h"],
            "name": f"desert_{sub.lower()}_{idx}",
            "is_composite": False
        })

    # Cluster Sand Ridge
    hill_chains = [c for c in clusters if c["category"] == "Hills"]
    if hill_chains:
        c = hill_chains[0]
        c_img = c["img"].copy()
        r = c_img[:, :, 0].astype(np.float32)
        g = c_img[:, :, 1].astype(np.float32)
        b = c_img[:, :, 2].astype(np.float32)

        c_img[:, :, 0] = np.clip(r * 0.50 + 215.0 * 0.50, 0, 255).astype(np.uint8)
        c_img[:, :, 1] = np.clip(g * 0.45 + 165.0 * 0.55, 0, 255).astype(np.uint8)
        c_img[:, :, 2] = np.clip(b * 0.30 + 88.0 * 0.70, 0, 255).astype(np.uint8)

        aux_clusters.append({
            "category": "Desert",
            "sub_category": "SandRidge",
            "img": c_img,
            "w": c["w"],
            "h": c["h"],
            "aspect": c["aspect"],
            "area": c["area"],
            "pivot_x": c["pivot_x"],
            "pivot_y": c["pivot_y"],
            "world_w": c["world_w"],
            "world_h": c["world_h"],
            "name": "desert_sand_ridge_01",
            "is_composite": True
        })

    # 2. Grassland Tussocks & Shrubs
    for idx, k in enumerate(knolls[3:6]):
        img = k["img"].copy()
        r = img[:, :, 0].astype(np.float32)
        g = img[:, :, 1].astype(np.float32)
        b = img[:, :, 2].astype(np.float32)

        img[:, :, 0] = np.clip(r * 0.70 + 35, 0, 255).astype(np.uint8)
        img[:, :, 1] = np.clip(g * 0.90 + 55, 0, 255).astype(np.uint8)
        img[:, :, 2] = np.clip(b * 0.65 + 30, 0, 255).astype(np.uint8)

        sub = "Tussock" if idx == 0 else ("Shrub" if idx == 1 else "MeadowPlain")
        aux_singles.append({
            "category": "Grassland",
            "sub_category": sub,
            "img": img,
            "w": k["w"],
            "h": k["h"],
            "aspect": k["aspect"],
            "area": k["area"],
            "pivot_x": k["pivot_x"],
            "pivot_y": k["pivot_y"],
            "world_w": round(k["world_w"] * 0.65, 2),
            "world_h": round(k["world_h"] * 0.65, 2),
            "name": f"grassland_{sub.lower()}_{idx}",
            "is_composite": False
        })

    # 3. Wetland Reeds & Islets
    for idx, k in enumerate(knolls[6:8]):
        img = k["img"].copy()
        r = img[:, :, 0].astype(np.float32)
        g = img[:, :, 1].astype(np.float32)
        b = img[:, :, 2].astype(np.float32)

        img[:, :, 0] = np.clip(r * 0.60 + 30, 0, 255).astype(np.uint8)
        img[:, :, 1] = np.clip(g * 0.85 + 75, 0, 255).astype(np.uint8)
        img[:, :, 2] = np.clip(b * 0.80 + 70, 0, 255).astype(np.uint8)

        sub = "Islet" if idx == 0 else "Reeds"
        aux_singles.append({
            "category": "Wetland",
            "sub_category": sub,
            "img": img,
            "w": k["w"],
            "h": k["h"],
            "aspect": k["aspect"],
            "area": k["area"],
            "pivot_x": k["pivot_x"],
            "pivot_y": k["pivot_y"],
            "world_w": round(k["world_w"] * 0.70, 2),
            "world_h": round(k["world_h"] * 0.70, 2),
            "name": f"wetland_{sub.lower()}_{idx}",
            "is_composite": False
        })

    return aux_singles, aux_clusters


def pack_shelf_atlas(items, atlas_base_name, atlas_w=4096, max_page_h=4096, padding=16):
    sorted_items = sorted(items, key=lambda s: s["h"], reverse=True)
    pages = []
    current_placed = []
    cur_x = padding
    cur_y = padding
    shelf_h = 0

    for it in sorted_items:
        w, h = it["w"], it["h"]
        max_item_w = atlas_w - padding * 2
        if w > max_item_w:
            scale_down = max_item_w / float(w)
            w = int(w * scale_down)
            h = int(h * scale_down)
            it["img"] = np.array(Image.fromarray(it["img"]).resize((w, h), Image.Resampling.LANCZOS))
            it["w"] = w
            it["h"] = h

        if cur_x + w + padding > atlas_w:
            cur_x = padding
            cur_y += shelf_h + padding
            shelf_h = 0

        # Check if exceeds max_page_h and we already placed items on this page
        if cur_y + h + padding > max_page_h and len(current_placed) > 0:
            page_h = cur_y + shelf_h + padding
            pages.append((page_h, current_placed))
            current_placed = []
            cur_x = padding
            cur_y = padding
            shelf_h = 0

        current_placed.append((it, cur_x, cur_y, w, h))
        cur_x += w + padding
        if h > shelf_h:
            shelf_h = h

    if current_placed:
        page_h = cur_y + shelf_h + padding
        pages.append((page_h, current_placed))

    prefix, ext = os.path.splitext(atlas_base_name)
    results = []

    for idx, (page_h, placed) in enumerate(pages):
        page_name = atlas_base_name if len(pages) == 1 else f"{prefix}_{idx}{ext}"
        print(f"Packing {page_name}: {atlas_w} x {page_h} ({len(placed)} items)")
        canvas = np.zeros((page_h, atlas_w, 4), dtype=np.uint8)
        meta_list = []
        for it, px, py, pw, ph in placed:
            canvas[py:py + ph, px:px + pw] = it["img"]
            meta_list.append({
                "category": it["category"],
                "sub_category": it["sub_category"],
                "atlas": page_name,
                "rect": [px, py, pw, ph],
                "pivot": [it["pivot_x"], it["pivot_y"]],
                "native_size": [pw, ph],
                "world_size": [it["world_w"], it["world_h"]],
                "aspect": it["aspect"],
                "area": it["area"],
                "name": it.get("name", "")
            })
        results.append((page_name, canvas, meta_list))

    return results


def save_png_safe(canvas_arr, out_path, optimize=True):
    temp_path = out_path + ".tmp.png"
    im = Image.fromarray(canvas_arr, "RGBA")
    im.save(temp_path, optimize=optimize)
    if os.path.exists(out_path):
        try:
            os.replace(temp_path, out_path)
        except Exception:
            try:
                os.remove(out_path)
            except Exception:
                pass
            os.replace(temp_path, out_path)
    else:
        os.replace(temp_path, out_path)
    print(f"Saved {out_path}")


def pack_all_mountain_atlases(all_singles, all_clusters, out_dir):
    all_atlases = []
    all_sprites = []
    global_id = 0

    # 1. Pack mountain_atlas_single (width 4096, max height 4096)
    single_results = pack_shelf_atlas(all_singles, "mountain_atlas_single.png", atlas_w=4096, max_page_h=4096, padding=16)
    for page_name, canvas, meta_list in single_results:
        all_atlases.append(page_name)
        out_single_png = os.path.join(out_dir, page_name)
        save_png_safe(canvas, out_single_png, optimize=True)
        for m in meta_list:
            m_copy = dict(m)
            m_copy["id"] = global_id
            all_sprites.append(m_copy)
            global_id += 1

    # 2. Pack mountain_atlas_cluster (width 4096, max height 4096)
    cluster_results = pack_shelf_atlas(all_clusters, "mountain_atlas_cluster.png", atlas_w=4096, max_page_h=4096, padding=20)
    for page_name, canvas, meta_list in cluster_results:
        all_atlases.append(page_name)
        out_cluster_png = os.path.join(out_dir, page_name)
        save_png_safe(canvas, out_cluster_png, optimize=True)
        for m in meta_list:
            m_copy = dict(m)
            m_copy["id"] = global_id
            all_sprites.append(m_copy)
            global_id += 1

    # 3. Export unified terrain_catalog.json
    catalog_path = os.path.join(out_dir, "terrain_catalog.json")
    with open(catalog_path, "w", encoding="utf-8") as f:
        json.dump({
            "version": 3,
            "atlases": all_atlases,
            "total_sprites": len(all_sprites),
            "sprites": all_sprites
        }, f, indent=2, ensure_ascii=False)
    print(f"Saved terrain catalog to {catalog_path} with {len(all_sprites)} sprites across {len(all_atlases)} atlas page(s).")


def export_fallback_textures(singles, out_dir):
    # 1. mountain_peak.png: best representative standalone peak
    peaks = [s for s in singles if s["category"] == "Mountain" and s["sub_category"] == "MainPeak" and not s["name"].endswith("_flip")]
    best_peak = peaks[0] if peaks else singles[0]
    peak_resized = np.array(Image.fromarray(best_peak["img"]).resize((240, 240), Image.Resampling.LANCZOS))
    save_png_safe(peak_resized, os.path.join(out_dir, "mountain_peak.png"))

    # 2. mountain_hill.png: best knoll
    knolls = [s for s in singles if s["category"] == "Hills" and s["sub_category"] == "Knoll" and not s["name"].endswith("_flip")]
    best_hill = knolls[0] if knolls else singles[-1]
    hill_resized = np.array(Image.fromarray(best_hill["img"]).resize((260, 180), Image.Resampling.LANCZOS))
    save_png_safe(hill_resized, os.path.join(out_dir, "mountain_hill.png"))

    # 3. mountain_snow.png: best snow peak from 雪山1 / 雪山2
    snows = [s for s in singles if s["category"] == "SnowMountain" and s["sub_category"] == "GlacierPeak" and not s["name"].endswith("_flip")]
    best_snow = snows[0] if snows else singles[0]
    snow_resized = np.array(Image.fromarray(best_snow["img"]).resize((240, 240), Image.Resampling.LANCZOS))
    save_png_safe(snow_resized, os.path.join(out_dir, "mountain_snow.png"))
    print("Exported standalone fallback textures: mountain_peak.png, mountain_hill.png, mountain_snow.png")


def clean_legacy_atlases(out_dir):
    targets = [
        "terrain_atlas.png", "terrain_atlas.png.import",
        "terrain_atlas_cluster.png", "terrain_atlas_cluster.png.import",
        "terrain_atlas_single.png", "terrain_atlas_single.png.import"
    ]
    for t in targets:
        p = os.path.join(out_dir, t)
        if os.path.exists(p):
            try:
                os.remove(p)
                print(f"Deleted legacy file: {p}")
            except Exception as ex:
                print(f"Warning: could not delete {p}: {ex}")


if __name__ == "__main__":
    main()
