import os
import cv2
import numpy as np
from PIL import Image
from scipy import ndimage

def extract_clean_sprite(arr, comp_mask, bbox, pad=6, tol=18.0):
    x, y, w, h = bbox
    H, W, _ = arr.shape
    x0 = max(0, x - pad)
    y0 = max(0, y - pad)
    x1 = min(W, x + w + pad)
    y1 = min(H, y + h + pad)

    crop = arr[y0:y1, x0:x1].copy()
    sub_mask = comp_mask[y0:y1, x0:x1]

    # Dilate component mask slightly to preserve fine outer brush bristles
    dilated_mask = cv2.dilate(sub_mask.astype(np.uint8), np.ones((5, 5), np.uint8)) > 0

    # Key out pure white background
    diff = 255.0 - np.min(crop, axis=2)
    alpha = np.clip((diff - 6.0) / tol, 0.0, 1.0) * 255.0

    # Outside dilated mask, alpha is strictly 0
    alpha[~dilated_mask] = 0.0

    # Softly feather the boundary
    h_c, w_c = alpha.shape
    y_dist = np.minimum(np.arange(h_c), h_c - 1 - np.arange(h_c))
    x_dist = np.minimum(np.arange(w_c), w_c - 1 - np.arange(w_c))
    grid_y, grid_x = np.meshgrid(y_dist, x_dist, indexing='ij')
    edge_dist = np.minimum(grid_y, grid_x)
    edge_feather = np.clip(edge_dist / 2.0, 0.0, 1.0)
    alpha = alpha * edge_feather

    # Absolute perimeter zeroing
    alpha[0, :] = 0
    alpha[-1, :] = 0
    alpha[:, 0] = 0
    alpha[:, -1] = 0

    rgba = np.dstack([crop, alpha.astype(np.uint8)])
    img = Image.fromarray(rgba)
    b = img.getbbox()
    if b:
        img = img.crop(b)
        # re-zero perimeter of tight crop
        arr_tight = np.array(img)
        arr_tight[0, :, 3] = 0
        arr_tight[-1, :, 3] = 0
        arr_tight[:, 0, 3] = 0
        arr_tight[:, -1, 3] = 0
        img = Image.fromarray(arr_tight)
    return img

def main():
    root = r"C:\Users\shawn\.gemini\antigravity\brain\219f0018-e433-4b29-bb0f-6e693de57c8c"
    out_dir = r"f:\GameDev\Original\PlanetGenerationCore\csharp\resources\textures\guohua"
    os.makedirs(out_dir, exist_ok=True)

    # 1. PROCESS TREES
    p_tree = os.path.join(root, "guohua_tree_sprites_1789978844841.jpg")
    img_tree = Image.open(p_tree).convert("RGB")
    arr_tree = np.array(img_tree)
    diff = 255 - np.min(arr_tree, axis=2)
    bin_m = (diff > 16).astype(np.uint8) * 255
    closed = cv2.morphologyEx(bin_m, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7)))
    n_lbl, lbls, stats, centroids = cv2.connectedComponentsWithStats(closed, connectivity=8)

    tree_comps = []
    for i in range(1, n_lbl):
        s = stats[i]
        if s[cv2.CC_STAT_AREA] > 2000 and s[cv2.CC_STAT_WIDTH] > 50 and s[cv2.CC_STAT_HEIGHT] > 50:
            cx, cy = centroids[i]
            tree_comps.append((cy, cx, i, (s[cv2.CC_STAT_LEFT], s[cv2.CC_STAT_TOP], s[cv2.CC_STAT_WIDTH], s[cv2.CC_STAT_HEIGHT])))
    
    # Sort roughly by rows then cols
    tree_comps.sort(key=lambda t: (int(t[0] // 240), t[1]))
    print(f"Detected {len(tree_comps)} tree components.")

    pines, willows, broadleafs = [], [], []
    for idx, (cy, cx, i, bbox) in enumerate(tree_comps):
        sprite = extract_clean_sprite(arr_tree, lbls == i, bbox)
        if cx < 500 and cy < 500:
            pines.append(sprite)
        elif cx >= 500 and cy < 500:
            willows.append(sprite)
        elif cx < 500 and cy >= 500:
            if bbox[2] > bbox[3]:
                broadleafs.append(sprite)
            else:
                willows.append(sprite)
        else:
            broadleafs.append(sprite)

    for i, s in enumerate(pines):
        s.save(os.path.join(out_dir, f"tree_pine_{i+1:02d}.png"))
    for i, s in enumerate(willows):
        s.save(os.path.join(out_dir, f"tree_willow_{i+1:02d}.png"))
    for i, s in enumerate(broadleafs):
        s.save(os.path.join(out_dir, f"tree_broadleaf_{i+1:02d}.png"))

    if pines:
        pines[0].save(os.path.join(out_dir, "forest_pine.png"))
        pines[0].save(os.path.join(out_dir, "tree_single.png"))
    if willows:
        willows[0].save(os.path.join(out_dir, "tree_willow.png"))
    if broadleafs:
        broadleafs[0].save(os.path.join(out_dir, "tree_cluster.png"))
        broadleafs[0].save(os.path.join(out_dir, "forest_bamboo.png"))
    print(f"Saved {len(pines)} pines, {len(willows)} willows, {len(broadleafs)} broadleaf trees.")

    # 2. PROCESS CLOUDS
    p_cloud = os.path.join(root, "guohua_cloud_sprites_1789978865775.jpg")
    arr_c = np.array(Image.open(p_cloud).convert("RGB"))
    diff_c = 255 - np.min(arr_c, axis=2)
    bin_c = (diff_c > 14).astype(np.uint8) * 255
    closed_c = cv2.morphologyEx(bin_c, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5)))
    n_c, lbls_c, stats_c, cents_c = cv2.connectedComponentsWithStats(closed_c, connectivity=8)
    
    cloud_comps = []
    for i in range(1, n_c):
        s = stats_c[i]
        if s[cv2.CC_STAT_AREA] > 1500 and s[cv2.CC_STAT_WIDTH] > 60 and s[cv2.CC_STAT_HEIGHT] > 30:
            cloud_comps.append((s[cv2.CC_STAT_AREA], i, (s[cv2.CC_STAT_LEFT], s[cv2.CC_STAT_TOP], s[cv2.CC_STAT_WIDTH], s[cv2.CC_STAT_HEIGHT])))
    cloud_comps.sort(key=lambda x: x[0], reverse=True)
    print(f"Detected {len(cloud_comps)} cloud components.")

    for idx, (_, i, bbox) in enumerate(cloud_comps[:12]):
        sprite = extract_clean_sprite(arr_c, lbls_c == i, bbox)
        sprite.save(os.path.join(out_dir, f"cloud_ruyi_{idx+1:02d}.png"))
        if idx == 0:
            sprite.save(os.path.join(out_dir, "cloud_ruyi.png"))
            sprite.save(os.path.join(out_dir, "mist_stripe.png"))

    # 3. PROCESS NAUTICAL & SEA ASSETS
    p_sea = os.path.join(root, "guohua_sea_assets_1789978885669.jpg")
    arr_s = np.array(Image.open(p_sea).convert("RGB"))
    diff_s = 255 - np.min(arr_s, axis=2)
    bin_s = (diff_s > 14).astype(np.uint8) * 255
    closed_s = cv2.morphologyEx(bin_s, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7)))
    n_s, lbls_s, stats_s, cents_s = cv2.connectedComponentsWithStats(closed_s, connectivity=8)

    sea_comps = []
    for i in range(1, n_s):
        s = stats_s[i]
        area = s[cv2.CC_STAT_AREA]
        w, h = s[cv2.CC_STAT_WIDTH], s[cv2.CC_STAT_HEIGHT]
        cx, cy = cents_s[i]
        if area > 1800 and w > 40 and h > 40:
            sea_comps.append((area, cx, cy, i, (s[cv2.CC_STAT_LEFT], s[cv2.CC_STAT_TOP], w, h)))

    print(f"Detected {len(sea_comps)} sea components.")
    # Identify compasses (round shape, high area)
    boats = []
    dragons = []
    compasses = []
    for area, cx, cy, i, bbox in sea_comps:
        x, y, w, h = bbox
        ratio = float(w) / max(1, h)
        if 0.85 < ratio < 1.15 and area > 10000:
            compasses.append((i, bbox))
        elif ratio > 1.1:
            # Dragons tend to be wider or swimming horizontally
            if cy > 450 and cx < 700:
                dragons.append((i, bbox))
            else:
                boats.append((i, bbox))
        else:
            boats.append((i, bbox))

    for idx, (i, bbox) in enumerate(compasses):
        sp = extract_clean_sprite(arr_s, lbls_s == i, bbox)
        sp.save(os.path.join(out_dir, f"compass_rose_{idx+1}.png"))
        if idx == 0:
            sp.save(os.path.join(out_dir, "compass_rose.png"))

    for idx, (i, bbox) in enumerate(boats):
        sp = extract_clean_sprite(arr_s, lbls_s == i, bbox)
        sp.save(os.path.join(out_dir, f"boat_junk_{idx+1}.png"))
        if idx == 0:
            sp.save(os.path.join(out_dir, "boat.png"))

    for idx, (i, bbox) in enumerate(dragons):
        sp = extract_clean_sprite(arr_s, lbls_s == i, bbox)
        sp.save(os.path.join(out_dir, f"sea_dragon_{idx+1}.png"))
        if idx == 0:
            sp.save(os.path.join(out_dir, "sea_dragon.png"))

    # 4. PROCESS DESERT ASSETS
    p_des = os.path.join(root, "guohua_desert_assets_1789978904904.jpg")
    arr_d = np.array(Image.open(p_des).convert("RGB"))
    diff_d = 255 - np.min(arr_d, axis=2)
    bin_d = (diff_d > 14).astype(np.uint8) * 255
    closed_d = cv2.morphologyEx(bin_d, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7)))
    n_d, lbls_d, stats_d, cents_d = cv2.connectedComponentsWithStats(closed_d, connectivity=8)

    dunes, cliffs, trees, oasis = [], [], [], []
    for i in range(1, n_d):
        s = stats_d[i]
        area = s[cv2.CC_STAT_AREA]
        w, h = s[cv2.CC_STAT_WIDTH], s[cv2.CC_STAT_HEIGHT]
        cx, cy = cents_d[i]
        if area > 1200 and w > 40 and h > 30:
            bbox = (s[cv2.CC_STAT_LEFT], s[cv2.CC_STAT_TOP], w, h)
            if cy < 300: # Top dunes
                dunes.append((i, bbox))
            elif cy < 600: # Middle cliffs
                cliffs.append((i, bbox))
            elif cy < 750: # Oasis
                oasis.append((i, bbox))
            else: # Bottom withered trees
                trees.append((i, bbox))

    for idx, (i, bbox) in enumerate(dunes):
        sp = extract_clean_sprite(arr_d, lbls_d == i, bbox)
        sp.save(os.path.join(out_dir, f"desert_dune_{idx+1:02d}.png"))
        if idx == 0:
            sp.save(os.path.join(out_dir, "desert_dune.png"))

    for idx, (i, bbox) in enumerate(cliffs):
        sp = extract_clean_sprite(arr_d, lbls_d == i, bbox)
        sp.save(os.path.join(out_dir, f"desert_cliff_{idx+1:02d}.png"))
        if idx == 0:
            sp.save(os.path.join(out_dir, "desert_cliff.png"))

    for idx, (i, bbox) in enumerate(oasis):
        sp = extract_clean_sprite(arr_d, lbls_d == i, bbox)
        sp.save(os.path.join(out_dir, f"desert_oasis_{idx+1:02d}.png"))
        if idx == 0:
            sp.save(os.path.join(out_dir, "desert_oasis.png"))

    for idx, (i, bbox) in enumerate(trees):
        sp = extract_clean_sprite(arr_d, lbls_d == i, bbox)
        sp.save(os.path.join(out_dir, f"dead_tree_{idx+1:02d}.png"))
        if idx == 0:
            sp.save(os.path.join(out_dir, "dead_tree.png"))

    print(f"Saved {len(dunes)} dunes, {len(cliffs)} cliffs, {len(oasis)} oasis, {len(trees)} dead trees.")

    # 5. PROCESS MOUNTAINS
    p_mtn = os.path.join(root, "guohua_mountain_assets_1789978931462.jpg")
    arr_m = np.array(Image.open(p_mtn).convert("RGB"))
    diff_m = 255 - np.min(arr_m, axis=2)
    bin_m = (diff_m > 14).astype(np.uint8) * 255
    # Remove text marks (small components < 500 area)
    closed_m = cv2.morphologyEx(bin_m, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7)))
    n_m, lbls_m, stats_m, cents_m = cv2.connectedComponentsWithStats(closed_m, connectivity=8)

    mtn_comps = []
    for i in range(1, n_m):
        s = stats_m[i]
        area = s[cv2.CC_STAT_AREA]
        w, h = s[cv2.CC_STAT_WIDTH], s[cv2.CC_STAT_HEIGHT]
        cx, cy = cents_m[i]
        # Skip small text annotations
        if area > 3500 and w > 60 and h > 50:
            mtn_comps.append((area, cx, cy, i, (s[cv2.CC_STAT_LEFT], s[cv2.CC_STAT_TOP], w, h)))

    print(f"Detected {len(mtn_comps)} mountain components.")
    snow_peaks = []
    green_peaks = []
    hills = []
    for area, cx, cy, i, bbox in mtn_comps:
        x, y, w, h = bbox
        if cx < 280 and cy < 500: # Snow mountain in col 1
            snow_peaks.append((i, bbox))
        elif h > 160: # Tall peaks
            green_peaks.append((i, bbox))
        else: # Low hills
            hills.append((i, bbox))

    for idx, (i, bbox) in enumerate(snow_peaks):
        sp = extract_clean_sprite(arr_m, lbls_m == i, bbox)
        sp.save(os.path.join(out_dir, f"mountain_snow_{idx+1:02d}.png"))
        if idx == 0:
            sp.save(os.path.join(out_dir, "mountain_snow.png"))

    for idx, (i, bbox) in enumerate(green_peaks):
        sp = extract_clean_sprite(arr_m, lbls_m == i, bbox)
        sp.save(os.path.join(out_dir, f"mountain_peak_{idx+1:02d}.png"))
        if idx == 0:
            sp.save(os.path.join(out_dir, "mountain_peak.png"))
            sp.save(os.path.join(out_dir, "mountain_ridge.png"))

    for idx, (i, bbox) in enumerate(hills):
        sp = extract_clean_sprite(arr_m, lbls_m == i, bbox)
        sp.save(os.path.join(out_dir, f"mountain_hill_{idx+1:02d}.png"))
        if idx == 0:
            sp.save(os.path.join(out_dir, "mountain_hill.png"))
    print(f"Saved {len(snow_peaks)} snow peaks, {len(green_peaks)} green peaks, {len(hills)} hills.")

    # 6. PROCESS BUILDINGS & SETTLEMENTS
    p_bld = os.path.join(root, "guohua_building_assets_1789978951415.jpg")
    arr_b = np.array(Image.open(p_bld).convert("RGB"))
    diff_b = 255 - np.min(arr_b, axis=2)
    bin_b = (diff_b > 14).astype(np.uint8) * 255
    closed_b = cv2.morphologyEx(bin_b, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7)))
    n_b, lbls_b, stats_b, cents_b = cv2.connectedComponentsWithStats(closed_b, connectivity=8)

    bld_comps = []
    for i in range(1, n_b):
        s = stats_b[i]
        area = s[cv2.CC_STAT_AREA]
        w, h = s[cv2.CC_STAT_WIDTH], s[cv2.CC_STAT_HEIGHT]
        cx, cy = cents_b[i]
        # Ignore small text labels
        if area > 4000 and w > 60 and h > 50:
            bld_comps.append((area, cx, cy, i, (s[cv2.CC_STAT_LEFT], s[cv2.CC_STAT_TOP], w, h)))

    print(f"Detected {len(bld_comps)} building components.")
    # Map by location:
    for area, cx, cy, i, bbox in bld_comps:
        sp = extract_clean_sprite(arr_b, lbls_b == i, bbox)
        if cx < 600 and cy < 350: # Top left: Imperial Walled City
            sp.save(os.path.join(out_dir, "city_capital.png"))
            sp.save(os.path.join(out_dir, "city.png"))
            sp.save(os.path.join(out_dir, "city_large.png"))
            sp.save(os.path.join(out_dir, "settlement_city.png"))
            print("Saved city_capital.png")
        elif cx >= 600 and cy < 300: # Top right: Palace Gate Tower
            sp.save(os.path.join(out_dir, "palace_tower.png"))
            print("Saved palace_tower.png")
        elif cx < 500 and 350 <= cy < 650: # Middle left: Mountain Pass Fortress
            sp.save(os.path.join(out_dir, "pass.png"))
            sp.save(os.path.join(out_dir, "pass_garrison.png"))
            print("Saved pass.png")
        elif 500 <= cx < 800 and 300 <= cy < 600: # Middle: Temple Courtyard
            sp.save(os.path.join(out_dir, "temple.png"))
            print("Saved temple.png")
        elif cx >= 800 and 300 <= cy < 600: # Middle right: Pagoda
            sp.save(os.path.join(out_dir, "pagoda.png"))
            print("Saved pagoda.png")
        elif cx < 500 and cy >= 650: # Bottom left: Courtyard Town
            sp.save(os.path.join(out_dir, "town.png"))
            print("Saved town.png")
        elif 500 <= cx < 750 and 600 <= cy < 800: # Cottage village
            sp.save(os.path.join(out_dir, "village.png"))
            print("Saved village.png")
        elif 750 <= cx and 600 <= cy < 800: # Upper bridge
            sp.save(os.path.join(out_dir, "bridge.png"))
            sp.save(os.path.join(out_dir, "bridge_arch.png"))
            print("Saved bridge.png")
        elif cx >= 700 and cy >= 800: # Ferry Port
            sp.save(os.path.join(out_dir, "port.png"))
            print("Saved port.png")

    print("\nAll V9 assets extracted and saved successfully!")

if __name__ == "__main__":
    main()
