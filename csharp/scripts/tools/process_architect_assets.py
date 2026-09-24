"""
Process new hand-drawn mountains and trees from csharp/architect/
and replace the legacy assets in csharp/resources/textures/guohua/.

Outputs:
  - terrain_atlas.png & terrain_catalog.json (with new mountains + preserved other landforms)
  - tree_atlas_single.png & tree_atlas_cluster.png & tree_catalog.json
  - Standalone fallback textures:
      mountain_peak.png, mountain_hill.png, mountain_snow.png,
      tree_single.png, tree_cluster.png, tree_willow.png,
      forest_pine.png, forest_bamboo.png
"""

import os
import json
import random
import cv2
import numpy as np
from PIL import Image

def main():
    base_dir = r"f:\GameDev\Original\PlanetGenerationCore"
    architect_dir = os.path.join(base_dir, "csharp", "architect")
    guohua_dir = os.path.join(base_dir, "csharp", "resources", "textures", "guohua")
    os.makedirs(guohua_dir, exist_ok=True)

    print("=========================================================")
    print(" Step 1: Processing Trees from architect/ ")
    print("=========================================================")
    process_trees(architect_dir, guohua_dir)

    print("\n=========================================================")
    print(" Step 2: Processing Mountains from architect/ ")
    print("=========================================================")
    process_mountains(architect_dir, guohua_dir)

    print("\n=========================================================")
    print(" All architect assets processed successfully! ")
    print("=========================================================")


def compute_tree_pivot(tight_rgba):
    th, tw = tight_rgba.shape[:2]
    alpha = tight_rgba[:, :, 3]
    opaque_rows = np.where(alpha > 100)[0]
    if len(opaque_rows) > 0:
        bottom_y = opaque_rows.max()
        low_y_start = max(0, bottom_y - int(th * 0.08))
        low_pixels_x = np.where(tight_rgba[low_y_start:bottom_y + 1, :, 3] > 80)[1]
        if len(low_pixels_x) > 0:
            base_x = float(np.mean(low_pixels_x)) / float(tw)
        else:
            base_x = 0.5
        base_y = float(bottom_y) / float(th)
    else:
        base_x = 0.5
        base_y = 0.95
    return round(base_x, 3), round(base_y, 3)


def extract_isolated_components(img_path, min_area=5000):
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

        # Tight trim to alpha > 5
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
    # Sort by reading order: top-to-bottom, left-to-right
    components.sort(key=lambda c: (c["orig_y"] // 250, c["orig_x"]))
    return components


def process_trees(architect_dir, out_dir):
    sources = [
        {"file": "常青阔叶树.png", "family": "Broadleaf", "subtype": "Evergreen"},
        {"file": "针叶树.png", "family": "Pine", "subtype": "Conifer"},
        {"file": "阔叶树.png", "family": "Broadleaf", "subtype": "Deciduous"}
    ]

    single_trees = []
    tree_idx = 0

    for src in sources:
        fpath = os.path.join(architect_dir, src["file"])
        comps = extract_isolated_components(fpath, min_area=8000)
        print(f"Extracted {len(comps)} isolated trees from {src['file']}")
        for c in comps:
            tight = c["img"]
            th, tw = tight.shape[:2]
            aspect = round(tw / max(th, 1), 2)
            px, py = compute_tree_pivot(tight)

            family = src["family"]
            # Classify some tall slender conifers as Cypress
            if src["subtype"] == "Conifer" and aspect < 0.50:
                family = "Cypress"

            # World scale for trees: 0.0045 gives ~3-5 units in world space (subordinate to hills 14 units & mountains 20 units)
            uniform_scale = 0.0045
            ww = round(tw * uniform_scale, 2)
            wh = round(th * uniform_scale, 2)

            single_trees.append({
                "idx": tree_idx,
                "img": tight,
                "category": "Single",
                "family": family,
                "subtype": src["subtype"],
                "w": tw,
                "h": th,
                "aspect": aspect,
                "area": c["area"],
                "pivot_x": px,
                "pivot_y": py,
                "world_w": ww,
                "world_h": wh
            })
            tree_idx += 1

    print(f"Total single trees extracted: {len(single_trees)}")

    # 1. Create Willow variants (select 4 elegant deciduous trees and designate as Willow)
    willow_candidates = [t for t in single_trees if t["subtype"] == "Deciduous"]
    for i in range(min(4, len(willow_candidates))):
        c = willow_candidates[i]
        single_trees.append({
            "idx": tree_idx,
            "img": c["img"].copy(),
            "category": "Willow",
            "family": "Willow",
            "subtype": "Willow",
            "w": c["w"],
            "h": c["h"],
            "aspect": c["aspect"],
            "area": c["area"],
            "pivot_x": c["pivot_x"],
            "pivot_y": c["pivot_y"],
            "world_w": c["world_w"],
            "world_h": c["world_h"]
        })
        tree_idx += 1

    # 2. Create Bush variants (downscaled compact shrub forms of broadleaf trees)
    bush_candidates = [t for t in single_trees if t["category"] == "Single" and t["family"] == "Broadleaf"]
    for i in range(min(8, len(bush_candidates))):
        c = bush_candidates[i]
        # Resize to ~45% for natural low-lying bush
        bw = int(c["w"] * 0.45)
        bh = int(c["h"] * 0.45)
        b_img = np.array(Image.fromarray(c["img"]).resize((bw, bh), Image.Resampling.LANCZOS))
        px, py = compute_tree_pivot(b_img)
        single_trees.append({
            "idx": tree_idx,
            "img": b_img,
            "category": "Bush",
            "family": "Bush",
            "subtype": "Bush",
            "w": bw,
            "h": bh,
            "aspect": round(bw / max(bh, 1), 2),
            "area": int(c["area"] * 0.20),
            "pivot_x": px,
            "pivot_y": py,
            "world_w": round(bw * 0.0045, 2),
            "world_h": round(bh * 0.0045, 2)
        })
        tree_idx += 1

    # 3. Synthesize Dense Forest Clusters (密林群落 / 林带 / 样板图)
    # Using downscaled single trees densely overlapping with Painter's algorithm
    clusters = []
    cluster_idx = 0

    pines = [t for t in single_trees if t["family"] == "Pine" and t["category"] == "Single"]
    cypresses = [t for t in single_trees if t["family"] == "Cypress" and t["category"] == "Single"]
    conifers = pines + cypresses
    broads = [t for t in single_trees if t["family"] == "Broadleaf" and t["category"] == "Single"]
    willows = [t for t in single_trees if t["family"] == "Willow"]
    if not willows:
        willows = broads[:4]

    def build_forest_patch(tree_list, target_shape="oval", target_tree_h=145, target_w=800, target_h=450, seed=42, is_conifer=False):
        rng = random.Random(seed)
        sample_h = np.mean([t["h"] for t in tree_list])
        base_scale = float(target_tree_h) / float(sample_h)

        if is_conifer:
            row_step = 25
            col_step = 34
        else:
            row_step = 28
            col_step = 40

        margin_x = 60
        margin_y = 50
        rows = int((target_h - margin_y * 2) / row_step) + 1
        cols = int((target_w - margin_x * 2) / col_step) + 1

        placed_trees = []
        center_x = target_w / 2.0
        center_y = target_h / 2.0
        radius_x = (target_w - margin_x * 2) / 2.0
        radius_y = (target_h - margin_y * 2) / 2.0

        for r in range(rows):
            cur_y = margin_y + r * row_step
            stagger = (col_step * 0.5) if (r % 2 == 1) else 0.0
            for c in range(cols):
                cur_x = margin_x + c * col_step + stagger
                jx = cur_x + rng.uniform(-col_step * 0.25, col_step * 0.25)
                jy = cur_y + rng.uniform(-row_step * 0.20, row_step * 0.20)

                nx = (jx - center_x) / max(radius_x, 1)
                ny = (jy - center_y) / max(radius_y, 1)

                include = False
                if target_shape == "oval":
                    dist = nx * nx + ny * ny
                    edge_noise = rng.uniform(-0.16, 0.16)
                    if dist + edge_noise <= 0.95:
                        include = True
                elif target_shape == "strip":
                    dist = (nx * 0.75) ** 2 + (ny * 1.55) ** 2
                    edge_noise = rng.uniform(-0.18, 0.18)
                    if dist + edge_noise <= 0.95 and abs(ny) <= 0.85:
                        include = True
                elif target_shape == "crescent":
                    curve_y = ny - 0.40 * (nx ** 2)
                    dist = nx * nx + (curve_y * 1.6) ** 2
                    edge_noise = rng.uniform(-0.15, 0.15)
                    if dist + edge_noise <= 0.95:
                        include = True
                elif target_shape == "copse":
                    dist = (nx * 1.2) ** 2 + (ny * 1.2) ** 2
                    if dist <= 0.80 + rng.uniform(-0.12, 0.12):
                        include = True

                if include:
                    t_proto = rng.choice(tree_list)
                    t_scale = base_scale * rng.uniform(0.88, 1.12)
                    depth_factor = 0.92 + (cur_y / float(target_h)) * 0.14
                    t_scale *= depth_factor
                    flip_h = rng.random() < 0.48

                    placed_trees.append({
                        "proto": t_proto,
                        "x": jx,
                        "y": jy,
                        "scale": t_scale,
                        "flip": flip_h
                    })

        placed_trees.sort(key=lambda item: item["y"])

        # Compute exact bounding box of all trees with safety padding to prevent any top/side clipping
        tree_boxes = []
        for pt in placed_trees:
            t = pt["proto"]
            tw = max(10, int(t["w"] * pt["scale"]))
            th = max(10, int(t["h"] * pt["scale"]))
            pv_x = 1.0 - t["pivot_x"] if pt["flip"] else t["pivot_x"]
            pv_y = t["pivot_y"]
            left = pt["x"] - tw * pv_x
            top = pt["y"] - th * pv_y
            right = left + tw
            bottom = top + th
            tree_boxes.append((pt, tw, th, pv_x, pv_y, left, top, right, bottom))

        min_left = min(b[5] for b in tree_boxes)
        min_top = min(b[6] for b in tree_boxes)
        max_right = max(b[7] for b in tree_boxes)
        max_bottom = max(b[8] for b in tree_boxes)

        pad = 60
        offset_x = -min_left + pad
        offset_y = -min_top + pad
        canvas_w = int(max_right - min_left + pad * 2) + 20
        canvas_h = int(max_bottom - min_top + pad * 2) + 20

        canvas = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
        for pt, tw, th, pv_x, pv_y, left, top, right, bottom in tree_boxes:
            t_im = Image.fromarray(pt["proto"]["img"]).resize((tw, th), Image.Resampling.LANCZOS)
            if pt["flip"]:
                t_im = t_im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)

            paste_x = int(left + offset_x)
            paste_y = int(top + offset_y)
            canvas.paste(t_im, (paste_x, paste_y), t_im)

        arr = np.array(canvas)
        alpha = arr[:, :, 3]
        ys, xs = np.where(alpha > 10)
        if len(xs) == 0 or len(ys) == 0:
            return None
        min_x, max_x = int(xs.min()), int(xs.max()) + 1
        min_y, max_y = int(ys.min()), int(ys.max()) + 1

        tight_arr = arr[min_y:max_y, min_x:max_x]

        bottom_y = int(tight_arr.shape[0] - 1)
        low_y_start = max(0, bottom_y - int(tight_arr.shape[0] * 0.08))
        root_xs = np.where(tight_arr[low_y_start:bottom_y + 1, :, 3] > 80)[1]
        if len(root_xs) > 0:
            pivot_x = float(np.mean(root_xs)) / float(tight_arr.shape[1])
        else:
            pivot_x = 0.5
        pivot_y = float(bottom_y) / float(tight_arr.shape[0])

        return tight_arr, round(pivot_x, 3), round(pivot_y, 3), len(placed_trees)

    # 28 Forest Cluster Configurations
    # Duo represents small copses (~20-50 trees)
    # Trio represents large massifs & ridge strips (~50-200 trees)
    configs = [
        # --- Pine Forest Clusters (6) ---
        {"family": "Pine", "cat": "Trio", "trees": conifers, "shape": "oval", "w": 820, "h": 450, "th": 140, "seed": 1001, "conifer": True, "name": "pine_massif_01"},
        {"family": "Pine", "cat": "Trio", "trees": pines, "shape": "strip", "w": 900, "h": 360, "th": 135, "seed": 1002, "conifer": True, "name": "pine_strip_01"},
        {"family": "Pine", "cat": "Trio", "trees": conifers, "shape": "crescent", "w": 800, "h": 420, "th": 140, "seed": 1003, "conifer": True, "name": "pine_crescent_01"},
        {"family": "Pine", "cat": "Duo", "trees": pines, "shape": "copse", "w": 520, "h": 340, "th": 130, "seed": 1004, "conifer": True, "name": "pine_copse_01"},
        {"family": "Pine", "cat": "Duo", "trees": conifers, "shape": "copse", "w": 460, "h": 300, "th": 125, "seed": 1005, "conifer": True, "name": "pine_copse_02"},
        {"family": "Pine", "cat": "Trio", "trees": pines, "shape": "oval", "w": 750, "h": 420, "th": 145, "seed": 1006, "conifer": True, "name": "pine_massif_02"},

        # --- Cypress Forest Clusters (4) ---
        {"family": "Cypress", "cat": "Trio", "trees": cypresses, "shape": "oval", "w": 780, "h": 440, "th": 145, "seed": 2001, "conifer": True, "name": "cypress_massif_01"},
        {"family": "Cypress", "cat": "Trio", "trees": cypresses, "shape": "strip", "w": 880, "h": 350, "th": 140, "seed": 2002, "conifer": True, "name": "cypress_strip_01"},
        {"family": "Cypress", "cat": "Duo", "trees": cypresses, "shape": "copse", "w": 500, "h": 320, "th": 130, "seed": 2003, "conifer": True, "name": "cypress_copse_01"},
        {"family": "Cypress", "cat": "Duo", "trees": cypresses, "shape": "copse", "w": 440, "h": 280, "th": 125, "seed": 2004, "conifer": True, "name": "cypress_copse_02"},

        # --- Broadleaf Forest Clusters (7) ---
        {"family": "Broadleaf", "cat": "Trio", "trees": broads, "shape": "oval", "w": 820, "h": 440, "th": 145, "seed": 3001, "conifer": False, "name": "broad_massif_01"},
        {"family": "Broadleaf", "cat": "Trio", "trees": broads, "shape": "strip", "w": 900, "h": 370, "th": 140, "seed": 3002, "conifer": False, "name": "broad_strip_01"},
        {"family": "Broadleaf", "cat": "Trio", "trees": broads, "shape": "crescent", "w": 800, "h": 420, "th": 140, "seed": 3003, "conifer": False, "name": "broad_crescent_01"},
        {"family": "Broadleaf", "cat": "Duo", "trees": broads, "shape": "copse", "w": 520, "h": 340, "th": 135, "seed": 3004, "conifer": False, "name": "broad_copse_01"},
        {"family": "Broadleaf", "cat": "Duo", "trees": broads, "shape": "copse", "w": 460, "h": 300, "th": 130, "seed": 3005, "conifer": False, "name": "broad_copse_02"},
        {"family": "Broadleaf", "cat": "Trio", "trees": broads, "shape": "oval", "w": 760, "h": 410, "th": 140, "seed": 3006, "conifer": False, "name": "broad_massif_02"},
        {"family": "Broadleaf", "cat": "Duo", "trees": broads, "shape": "strip", "w": 650, "h": 300, "th": 125, "seed": 3007, "conifer": False, "name": "broad_strip_sm"},

        # --- Mixed Forest Clusters (7) ---
        {"family": "Mixed", "cat": "Trio", "trees": single_trees, "shape": "oval", "w": 840, "h": 450, "th": 145, "seed": 4001, "conifer": False, "name": "mixed_massif_01"},
        {"family": "Mixed", "cat": "Trio", "trees": single_trees, "shape": "strip", "w": 920, "h": 370, "th": 140, "seed": 4002, "conifer": False, "name": "mixed_strip_01"},
        {"family": "Mixed", "cat": "Trio", "trees": single_trees, "shape": "crescent", "w": 810, "h": 420, "th": 140, "seed": 4003, "conifer": False, "name": "mixed_crescent_01"},
        {"family": "Mixed", "cat": "Duo", "trees": single_trees, "shape": "copse", "w": 530, "h": 340, "th": 135, "seed": 4004, "conifer": False, "name": "mixed_copse_01"},
        {"family": "Mixed", "cat": "Duo", "trees": single_trees, "shape": "copse", "w": 470, "h": 310, "th": 130, "seed": 4005, "conifer": False, "name": "mixed_copse_02"},
        {"family": "Mixed", "cat": "Trio", "trees": single_trees, "shape": "oval", "w": 770, "h": 420, "th": 140, "seed": 4006, "conifer": False, "name": "mixed_massif_02"},
        {"family": "Mixed", "cat": "Duo", "trees": single_trees, "shape": "strip", "w": 660, "h": 300, "th": 125, "seed": 4007, "conifer": False, "name": "mixed_strip_sm"},

        # --- Willow & Riverbank Groves (4) ---
        {"family": "Willow", "cat": "Trio", "trees": willows, "shape": "strip", "w": 800, "h": 350, "th": 140, "seed": 5001, "conifer": False, "name": "willow_strip_01"},
        {"family": "Willow", "cat": "Duo", "trees": willows, "shape": "copse", "w": 480, "h": 320, "th": 130, "seed": 5002, "conifer": False, "name": "willow_copse_01"},
        {"family": "Willow", "cat": "Duo", "trees": willows, "shape": "copse", "w": 420, "h": 280, "th": 125, "seed": 5003, "conifer": False, "name": "willow_copse_02"},
        {"family": "Willow", "cat": "Trio", "trees": willows, "shape": "crescent", "w": 720, "h": 360, "th": 135, "seed": 5004, "conifer": False, "name": "willow_crescent_01"},
    ]

    for cfg in configs:
        res = build_forest_patch(
            tree_list=cfg["trees"],
            target_shape=cfg["shape"],
            target_tree_h=cfg["th"],
            target_w=cfg["w"],
            target_h=cfg["h"],
            seed=cfg["seed"],
            is_conifer=cfg["conifer"]
        )
        if res:
            tight_arr, px, py, n_trees = res
            th, tw = tight_arr.shape[:2]
            # World scale for forest clusters: 0.0135 gives ~5-11 units in world space (subordinate to hills 28 units & mountains 48 units)
            world_scale = 0.0135
            clusters.append({
                "idx": cluster_idx,
                "img": tight_arr,
                "category": cfg["cat"],
                "family": cfg["family"],
                "w": tw,
                "h": th,
                "aspect": round(tw / max(th, 1), 2),
                "area": int(np.count_nonzero(tight_arr[:, :, 3] > 20)),
                "pivot_x": px,
                "pivot_y": py,
                "world_w": round(tw * world_scale, 2),
                "world_h": round(th * world_scale, 2),
                "tree_count": n_trees
            })
            cluster_idx += 1

    print(f"Total cluster trees created: {len(clusters)}")

    # 4. Pack into tree_atlas_single.png and tree_atlas_cluster.png
    def pack_shelf_atlas(items, atlas_name, atlas_w=4096, padding=16):
        sorted_items = sorted(items, key=lambda s: s["h"], reverse=True)
        cur_x = padding
        cur_y = padding
        shelf_h = 0
        placed = []
        for it in sorted_items:
            w, h = it["w"], it["h"]
            if cur_x + w + padding > atlas_w:
                cur_x = padding
                cur_y += shelf_h + padding
                shelf_h = 0
            placed.append((it, cur_x, cur_y, w, h))
            cur_x += w + padding
            if h > shelf_h:
                shelf_h = h
        total_h = cur_y + shelf_h + padding
        print(f"Packing {atlas_name}: {atlas_w} x {total_h}")

        canvas = np.zeros((total_h, atlas_w, 4), dtype=np.uint8)
        meta_list = []
        for it, px, py, pw, ph in placed:
            canvas[py:py + ph, px:px + pw] = it["img"]
            meta_list.append({
                "category": it["category"],
                "family": it["family"],
                "atlas": atlas_name,
                "rect": [px, py, pw, ph],
                "pivot": [it["pivot_x"], it["pivot_y"]],
                "native_size": [pw, ph],
                "world_size": [it["world_w"], it["world_h"]],
                "aspect": it["aspect"],
                "area": it["area"]
            })
        out_png = os.path.join(out_dir, atlas_name)
        Image.fromarray(canvas, "RGBA").save(out_png, optimize=True)
        print(f"Saved {out_png}")
        return meta_list

    meta_singles = pack_shelf_atlas(single_trees, "tree_atlas_single.png", atlas_w=4096)
    meta_clusters = pack_shelf_atlas(clusters, "tree_atlas_cluster.png", atlas_w=4096)

    # Re-index all sprites with unique ID
    all_sprites = []
    global_id = 0
    for m in meta_singles + meta_clusters:
        m_copy = dict(m)
        m_copy["id"] = global_id
        all_sprites.append(m_copy)
        global_id += 1

    # Save tree_catalog.json
    tree_json_path = os.path.join(out_dir, "tree_catalog.json")
    with open(tree_json_path, "w", encoding="utf-8") as f:
        json.dump({
            "total_sprites": len(all_sprites),
            "atlases": ["tree_atlas_single.png", "tree_atlas_cluster.png"],
            "sprites": all_sprites
        }, f, indent=2, ensure_ascii=False)
    print(f"Saved tree catalog to {tree_json_path} with {len(all_sprites)} sprites.")

    # 5. Export individual standalone fallback textures
    # tree_single.png: best representative broadleaf single tree
    best_single = [t for t in single_trees if t["category"] == "Single" and t["family"] == "Broadleaf"][0]
    Image.fromarray(best_single["img"]).resize((240, 240), Image.Resampling.LANCZOS).save(os.path.join(out_dir, "tree_single.png"))

    # tree_cluster.png: best broadleaf forest cluster
    best_cluster = [c for c in clusters if c["family"] == "Broadleaf" and c["category"] == "Trio"][0]
    Image.fromarray(best_cluster["img"]).resize((320, 200), Image.Resampling.LANCZOS).save(os.path.join(out_dir, "tree_cluster.png"))

    # tree_willow.png: best willow tree
    best_willow = [t for t in single_trees if t["category"] == "Willow"][0]
    Image.fromarray(best_willow["img"]).resize((230, 240), Image.Resampling.LANCZOS).save(os.path.join(out_dir, "tree_willow.png"))

    # forest_pine.png: best pine forest cluster
    best_pine_forest = [c for c in clusters if c["family"] == "Pine" and c["category"] == "Trio"][0]
    Image.fromarray(best_pine_forest["img"]).resize((320, 200), Image.Resampling.LANCZOS).save(os.path.join(out_dir, "forest_pine.png"))

    # forest_bamboo.png: mixed / bamboo evergreen forest cluster
    best_mixed_forest = [c for c in clusters if c["family"] == "Mixed" and c["category"] == "Trio"][0]
    Image.fromarray(best_mixed_forest["img"]).resize((320, 200), Image.Resampling.LANCZOS).save(os.path.join(out_dir, "forest_bamboo.png"))
    print("Exported individual fallback tree textures.")


def make_snow_version(tight_rgba):
    """Create snow-capped / frosted peak variant for high-latitude / alpine snow mountains."""
    res = tight_rgba.copy()
    alpha = res[:, :, 3]
    h, w = alpha.shape
    # Add frosty jade-white snow tone on the upper half and ridges
    for y in range(h):
        y_ratio = 1.0 - (y / float(h))  # 1.0 at top, 0.0 at bottom
        snow_amt = np.clip((y_ratio - 0.25) * 1.5, 0.0, 1.0)
        if snow_amt > 0:
            # Shift RGB towards cold snow-ivory: (230, 236, 242)
            row_alpha = alpha[y] > 20
            r = res[y, :, 0].astype(np.float32)
            g = res[y, :, 1].astype(np.float32)
            b = res[y, :, 2].astype(np.float32)

            # Snow caps on higher elevations
            r_snow = r * (1.0 - snow_amt * 0.65) + 232.0 * (snow_amt * 0.65)
            g_snow = g * (1.0 - snow_amt * 0.65) + 238.0 * (snow_amt * 0.65)
            b_snow = b * (1.0 - snow_amt * 0.65) + 245.0 * (snow_amt * 0.65)

            res[y, row_alpha, 0] = np.clip(r_snow[row_alpha], 0, 255).astype(np.uint8)
            res[y, row_alpha, 1] = np.clip(g_snow[row_alpha], 0, 255).astype(np.uint8)
            res[y, row_alpha, 2] = np.clip(b_snow[row_alpha], 0, 255).astype(np.uint8)
    return res


def process_mountains(architect_dir, out_dir):
    mountain_path = os.path.join(architect_dir, "mountain.png")
    comps = extract_isolated_components(mountain_path, min_area=3000)
    print(f"Extracted {len(comps)} mountain components from mountain.png")

    # Load existing terrain_catalog.json to preserve non-mountain categories
    old_catalog_path = os.path.join(out_dir, "terrain_catalog.json")
    old_atlas_path = os.path.join(out_dir, "terrain_atlas.png")
    preserved_sprites = []
    if os.path.exists(old_catalog_path) and os.path.exists(old_atlas_path):
        old_atlas = Image.open(old_atlas_path)
        with open(old_catalog_path, "r", encoding="utf-8") as f:
            old_data = json.load(f)
        for s in old_data.get("sprites", []):
            cat = s.get("category")
            # Preserve Plateau, Desert, Grassland, Wetland, Volcano, Basin (non-mountain)
            if cat not in ("Mountain", "Hills", "SnowMountain"):
                rx, ry, rw, rh = s["rect"]
                crop = np.array(old_atlas.crop((rx, ry, rx + rw, ry + rh)))
                preserved_sprites.append({
                    "category": cat,
                    "sub_category": s.get("sub_category", "General"),
                    "img": crop,
                    "w": rw,
                    "h": rh,
                    "aspect": s.get("aspect", round(rw / max(rh, 1), 2)),
                    "area": s.get("area", rw * rh),
                    "pivot_x": s["pivot"][0],
                    "pivot_y": s["pivot"][1],
                    "world_w": s["world_size"][0],
                    "world_h": s["world_size"][1]
                })
        print(f"Preserved {len(preserved_sprites)} non-mountain terrain sprites (Desert, Wetland, Plateau, etc.)")

    # Classify the 21 mountain components
    new_mountains = []
    snow_mountains = []

    for idx, c in enumerate(comps):
        tight = c["img"]
        th, tw = tight.shape[:2]
        aspect = round(tw / max(th, 1), 2)
        area = c["area"]

        # Compute ground contact pivot
        opaque_rows = np.where(tight[:, :, 3] > 80)[0]
        if len(opaque_rows) > 0:
            bottom_y = opaque_rows.max()
            low_y_start = max(0, bottom_y - int(th * 0.10))
            low_pixels_x = np.where(tight[low_y_start:bottom_y + 1, :, 3] > 80)[1]
            px = float(np.mean(low_pixels_x)) / float(tw) if len(low_pixels_x) > 0 else 0.5
            py = float(bottom_y) / float(th)
        else:
            px, py = 0.5, 0.95

        # Sub-category and scale classification
        if aspect > 2.2:
            cat = "Mountain"
            sub_cat = "Ridge"
            # Horizontal continuous ridges
            w_scale = 0.055 if tw > 2400 else 0.075
        elif th > 650:
            if aspect > 1.2:
                # Basin caldera
                cat = "Basin"
                sub_cat = "EncirclingArm"
                w_scale = 0.040
            else:
                # Vertical or curved multi-peak ridges
                cat = "Mountain"
                sub_cat = "Ridge"
                # Calibrate world scale so multi-peak chains match standard ridge height
                w_scale = 0.030
        elif th < 220 and tw < 400:
            cat = "Hills"
            sub_cat = "Knoll"
            w_scale = 0.090
        elif th <= 380 and tw >= 500:
            # Standalone main peak (e.g. comp 14: 615x261, comp 10: 564x356)
            cat = "Mountain"
            sub_cat = "MainPeak"
            w_scale = 0.085
        else:
            cat = "Mountain"
            sub_cat = "CompanionPeak"
            w_scale = 0.085

        ww = round(tw * w_scale, 2)
        wh = round(th * w_scale, 2)

        new_mountains.append({
            "category": cat,
            "sub_category": sub_cat,
            "img": tight,
            "w": tw,
            "h": th,
            "aspect": aspect,
            "area": area,
            "pivot_x": round(px, 3),
            "pivot_y": round(py, 3),
            "world_w": ww,
            "world_h": wh
        })

        # Also create a SnowMountain variant for prominent peaks & ridges
        if cat == "Mountain" and (sub_cat in ("MainPeak", "Ridge", "CompanionPeak")):
            snow_img = make_snow_version(tight)
            snow_sub = "GlacierPeak" if sub_cat in ("MainPeak", "CompanionPeak") else "SnowRidge"
            snow_mountains.append({
                "category": "SnowMountain",
                "sub_category": snow_sub,
                "img": snow_img,
                "w": tw,
                "h": th,
                "aspect": aspect,
                "area": area,
                "pivot_x": round(px, 3),
                "pivot_y": round(py, 3),
                "world_w": ww,
                "world_h": wh
            })

    print(f"Categorized {len(new_mountains)} new mountain/hill sprites and {len(snow_mountains)} snow mountain sprites.")

    # Combine all sprites to pack into terrain_atlas.png
    all_terrain_sprites = new_mountains + snow_mountains + preserved_sprites

    # Bin-packing into unified terrain_atlas.png (Shelf skyline)
    padding = 24
    max_w = max(s["w"] for s in all_terrain_sprites)
    atlas_w = max(6144, ((max_w + padding * 2 + 63) // 64) * 64)
    all_terrain_sprites.sort(key=lambda s: s["h"], reverse=True)

    skyline = [0] * atlas_w
    max_h = 0
    placed = []

    for s in all_terrain_sprites:
        cw = s["w"] + padding
        ch = s["h"] + padding
        best_x = 0
        best_y = 999999
        max_search = max(0, atlas_w - cw)
        for x in range(0, max_search + 1, 16):
            y = max(skyline[x:x + cw])
            if y < best_y:
                best_y = y
                best_x = x
        px = best_x + padding // 2
        py = best_y + padding // 2
        for x in range(best_x, min(atlas_w, best_x + cw)):
            skyline[x] = best_y + ch
        max_h = max(max_h, best_y + ch)
        placed.append((s, px, py, s["w"], s["h"]))

    total_atlas_h = int(max_h + padding)
    print(f"Packed terrain_atlas size: {atlas_w} x {total_atlas_h}")

    atlas_canvas = np.zeros((total_atlas_h, atlas_w, 4), dtype=np.uint8)
    catalog_sprites = []
    sprite_id = 0

    for s, px, py, pw, ph in placed:
        atlas_canvas[py:py + ph, px:px + pw] = s["img"]
        catalog_sprites.append({
            "id": sprite_id,
            "category": s["category"],
            "sub_category": s["sub_category"],
            "rect": [px, py, pw, ph],
            "pivot": [s["pivot_x"], s["pivot_y"]],
            "native_size": [pw, ph],
            "world_size": [s["world_w"], s["world_h"]],
            "aspect": s["aspect"],
            "area": s["area"]
        })
        sprite_id += 1

    # Save terrain_atlas.png
    out_atlas_png = os.path.join(out_dir, "terrain_atlas.png")
    Image.fromarray(atlas_canvas, "RGBA").save(out_atlas_png, optimize=True)
    print(f"Saved {out_atlas_png}")

    # Save terrain_catalog.json
    out_catalog_json = os.path.join(out_dir, "terrain_catalog.json")
    with open(out_catalog_json, "w", encoding="utf-8") as f:
        json.dump({
            "version": 2,
            "texture": "terrain_atlas.png",
            "atlas_size": [atlas_w, total_atlas_h],
            "total_sprites": len(catalog_sprites),
            "sprites": catalog_sprites
        }, f, indent=2, ensure_ascii=False)
    print(f"Saved terrain catalog to {out_catalog_json} with {len(catalog_sprites)} sprites.")

    # 6. Export individual standalone fallback mountain textures
    # mountain_peak.png: best representative standalone peak (comp 14: 615x261)
    peak_candidates = [m for m in new_mountains if 500 <= m["w"] <= 900 and m["h"] <= 500]
    best_peak = peak_candidates[0] if peak_candidates else new_mountains[0]
    Image.fromarray(best_peak["img"]).resize((240, 240), Image.Resampling.LANCZOS).save(os.path.join(out_dir, "mountain_peak.png"))

    # mountain_hill.png: best knoll / hill (comp 15: 353x201)
    hill_candidates = [m for m in new_mountains if m["category"] == "Hills" and m["sub_category"] == "Knoll"]
    best_hill = hill_candidates[0] if hill_candidates else new_mountains[-1]
    Image.fromarray(best_hill["img"]).resize((260, 180), Image.Resampling.LANCZOS).save(os.path.join(out_dir, "mountain_hill.png"))

    # mountain_snow.png: snow variant of peak
    best_snow = make_snow_version(best_peak["img"])
    Image.fromarray(best_snow).resize((240, 240), Image.Resampling.LANCZOS).save(os.path.join(out_dir, "mountain_snow.png"))
    print("Exported individual fallback mountain textures.")


if __name__ == "__main__":
    main()
