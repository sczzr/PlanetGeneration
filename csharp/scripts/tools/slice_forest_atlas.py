import os
import json
import cv2
import numpy as np
from PIL import Image

def process_forest_atlas():
    src_path = r"C:\Users\shawn\Downloads\joyai_001.png"
    if not os.path.exists(src_path):
        src_path = r"C:\Users\shawn\Downloads\joyai_001 (1).png"

    out_dir = r"f:\GameDev\Original\PlanetGenerationCore\csharp\resources\textures\guohua"
    os.makedirs(out_dir, exist_ok=True)
    out_png = os.path.join(out_dir, "forest_atlas.png")
    out_json = os.path.join(out_dir, "forest_atlas.json")

    print(f"Loading true original 5.5K image from {src_path}...")
    img = Image.open(src_path).convert("RGB")
    arr = np.array(img, dtype=np.float32)

    # 1. Background color estimation from corners
    corners = np.vstack([arr[:100, :100], arr[:100, -100:], arr[-100:, :100], arr[-100:, -100:]])
    bg = np.mean(corners, axis=(0, 1))
    print(f"Estimated background color: {bg}")

    # 2. Connected components detection
    diff = np.linalg.norm(arr - bg, axis=2)
    bin_mask = (diff > 20.0).astype(np.uint8) * 255
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (15, 15))
    closed = cv2.morphologyEx(bin_mask, cv2.MORPH_CLOSE, kernel)

    num_labels, labels, stats, centroids = cv2.connectedComponentsWithStats(closed)
    print(f"Detected {num_labels - 1} raw components.")

    raw_clusters = []
    for i in range(1, num_labels):
        s = stats[i]
        w, h, area = int(s[cv2.CC_STAT_WIDTH]), int(s[cv2.CC_STAT_HEIGHT]), int(s[cv2.CC_STAT_AREA])
        x, y = int(s[cv2.CC_STAT_LEFT]), int(s[cv2.CC_STAT_TOP])
        if w < 40 or h < 40 or area < 1000:
            continue

        pad = 22
        x0_pad = max(0, x - pad)
        y0_pad = max(0, y - pad)
        x1_pad = min(arr.shape[1], x + w + pad)
        y1_pad = min(arr.shape[0], y + h + pad)

        crop_arr = arr[y0_pad:y1_pad, x0_pad:x1_pad].copy()
        sub_labels = labels[y0_pad:y1_pad, x0_pad:x1_pad]
        comp_mask = (sub_labels == i)
        comp_dilated = cv2.dilate(comp_mask.astype(np.uint8), np.ones((15, 15), np.uint8)) > 0
        crop_arr[~comp_dilated] = bg

        crop_diff = np.linalg.norm(crop_arr - bg, axis=2)
        hsv = cv2.cvtColor(crop_arr.astype(np.uint8), cv2.COLOR_RGB2HSV)
        is_ink = np.min(crop_arr, axis=2) < 175
        is_foliage = (crop_diff > 18) & (hsv[:, :, 1] > 12)
        is_tree_raw = (is_ink | is_foliage).astype(np.uint8)

        # 闭运算连接细密针叶与墨点，并进行空洞填充确保树冠内部 100% 实心无漏洞
        close_kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (15, 15))
        tree_closed = cv2.morphologyEx(is_tree_raw, cv2.MORPH_CLOSE, close_kernel)
        
        ch, cw = tree_closed.shape
        flood = np.zeros((ch + 2, cw + 2), np.uint8)
        inv = (~(tree_closed > 0)).astype(np.uint8)
        cv2.floodFill(inv, flood, (0, 0), 0)
        tree_solid = (tree_closed | inv) > 0

        # 树冠内部 100% 不透明，边缘 1.5px 距离场平滑抗锯齿
        tree_dist = cv2.distanceTransform(tree_solid.astype(np.uint8), cv2.DIST_L2, 3)
        tree_alpha = np.clip(tree_dist / 1.5, 0.0, 1.0)

        # 树身外部的宣纸地晕平滑衰减
        dist_from_tree = cv2.distanceTransform((~tree_solid).astype(np.uint8), cv2.DIST_L2, 5)
        dist_from_comp_edge = cv2.distanceTransform(comp_dilated.astype(np.uint8), cv2.DIST_L2, 5)

        ground_reach = 24.0
        ground_tree_fade = np.clip(1.0 - dist_from_tree / ground_reach, 0.0, 1.0)
        ground_tree_fade = ground_tree_fade * ground_tree_fade * (3.0 - 2.0 * ground_tree_fade)

        edge_fade = np.clip(dist_from_comp_edge / 16.0, 0.0, 1.0)
        edge_fade = edge_fade * edge_fade * (3.0 - 2.0 * edge_fade)

        ground_fade = ground_tree_fade * edge_fade
        ground_alpha = np.clip((crop_diff - 14.0) / 20.0, 0.0, 0.42) * ground_fade

        cluster_alpha = np.where(tree_solid, tree_alpha, ground_alpha)
        cluster_alpha[~comp_dilated] = 0.0

        # 树木主体直接保留原画无损 RGB（杜绝小 alpha 除法噪点），仅对地晕层进行背景解算
        alpha_exp = cluster_alpha[:, :, np.newaxis]
        wash_rgb = np.clip((crop_arr - bg * (1.0 - alpha_exp)) / np.maximum(alpha_exp, 0.15), 0.0, 255.0)
        crop_rgb = np.where(tree_solid[:, :, np.newaxis], crop_arr, wash_rgb)
        rgba_crop = np.dstack([crop_rgb, cluster_alpha * 255.0]).astype(np.uint8)

        # Tight crop by alpha > 5
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

        # Shape classification
        if (orig_x > 4300 and orig_y > 2100 and alpha_area < 12000) or (tw <= 170 and th <= 220 and alpha_area < 10000):
            shape = "SingleScatter"
        elif orig_y < 700 and 2500 <= orig_x <= 4400:
            shape = "ForkY"
        elif (orig_y < 700 and orig_x >= 4650) or (orig_y >= 600 and orig_y <= 1400 and orig_x >= 4400) or (orig_x >= 4700 and 1200 <= orig_y <= 1900):
            shape = "CornerL"
        elif 2150 <= orig_x <= 3100 and orig_y >= 1250 and 1.25 <= aspect <= 2.6:
            shape = "Dumbbell"
        elif aspect >= 2.1:
            shape = "LinearStrip"
        elif (1400 <= orig_x <= 2200 and 380 <= orig_y <= 1300) or (orig_x >= 4000 and 1500 <= orig_y <= 2100):
            shape = "CurvedS"
        elif (800 <= orig_x <= 1550 and 950 <= orig_y <= 1650) or (3000 <= orig_x <= 3750 and 1900 <= orig_y <= 2550):
            shape = "HollowRing"
        elif (2950 <= orig_x <= 3650 and 800 <= orig_y <= 1500) or (850 <= orig_x <= 1550 and 2050 <= orig_y <= 2700):
            shape = "CurvedC"
        elif alpha_area >= 80000:
            shape = "DenseCore"
        elif alpha_area < 28000:
            shape = "SparseEdge"
        else:
            shape = "CircleOval"

        # 严格保持原图手绘比例（统一世界坐标缩放系数 0.085，禁止个别树木群落放大）
        uniform_scale = 0.085
        base_w = round(tw * uniform_scale, 1)
        base_h = round(th * uniform_scale, 1)

        raw_clusters.append({
            "shape": shape,
            "w": tw,
            "h": th,
            "aspect": aspect,
            "area": alpha_area,
            "base_world_w": base_w,
            "base_world_h": base_h,
            "img": tight
        })

    print(f"Total isolated valid clusters: {len(raw_clusters)}")

    # 3. Skyline Shelf Bin Packing into an atlas with 24px padding
    atlas_w = 5120
    padding = 24

    raw_clusters.sort(key=lambda c: c["h"], reverse=True)

    skyline = [0] * atlas_w
    max_atlas_h = 0

    for c in raw_clusters:
        cw = c["w"] + padding
        ch = c["h"] + padding
        best_x = 0
        best_y = 999999
        for x in range(0, atlas_w - cw + 1, 16):
            y = max(skyline[x:x+cw])
            if y < best_y:
                best_y = y
                best_x = x
        px = best_x + padding // 2
        py = best_y + padding // 2
        for x in range(best_x, best_x + cw):
            skyline[x] = best_y + ch
        max_atlas_h = max(max_atlas_h, best_y + ch)
        c["packed_rect"] = [px, py, c["w"], c["h"]]

    atlas_h = int(max_atlas_h + padding)
    print(f"Atlas size: {atlas_w} x {atlas_h}")

    atlas_canvas = np.zeros((atlas_h, atlas_w, 4), dtype=np.uint8)
    clusters_json = []

    for idx, c in enumerate(raw_clusters):
        px, py, w, h = c["packed_rect"]
        atlas_canvas[py:py+h, px:px+w] = c["img"]
        clusters_json.append({
            "id": idx,
            "shape": c["shape"],
            "rect": [px, py, w, h],
            "pivot": [0.5, 0.85],
            "world_size": [c["base_world_w"], c["base_world_h"]],
            "area": c["area"],
            "aspect": c["aspect"]
        })

    Image.fromarray(atlas_canvas, mode="RGBA").save(out_png)
    print(f"Saved full-res 5.5K isolated atlas ({atlas_w}x{atlas_h}) to {out_png}")

    catalog_data = {
        "version": 4,
        "texture": "forest_atlas.png",
        "atlas_size": [atlas_w, atlas_h],
        "clusters": clusters_json
    }

    with open(out_json, "w", encoding="utf-8") as f:
        json.dump(catalog_data, f, indent=2, ensure_ascii=False)
    print(f"Saved catalog JSON to {out_json}")

if __name__ == "__main__":
    process_forest_atlas()
