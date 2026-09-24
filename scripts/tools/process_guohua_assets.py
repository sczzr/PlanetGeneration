import os
import numpy as np
from PIL import Image

def process_white_background(img_path, tol=25, edge_feather=4):
    img = Image.open(img_path).convert("RGBA")
    arr = np.array(img, dtype=np.float32)
    r, g, b = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2]
    # Pure or near white background
    diff = np.maximum.reduce([255 - r, 255 - g, 255 - b])
    alpha = np.clip((diff - 8) / float(tol), 0, 1) * 255.0
    arr[:, :, 3] = alpha
    result = Image.fromarray(arr.astype(np.uint8))
    bbox = result.getbbox()
    if bbox:
        result = result.crop(bbox)
        arr = np.array(result, dtype=np.float32)

    h, w, _ = arr.shape
    y_dist = np.minimum(np.arange(h), h - 1 - np.arange(h))
    x_dist = np.minimum(np.arange(w), w - 1 - np.arange(w))
    grid_y, grid_x = np.meshgrid(y_dist, x_dist, indexing='ij')
    edge_dist = np.minimum(grid_y, grid_x)
    edge_factor = np.clip((edge_dist - 0.5) / float(edge_feather), 0, 1)
    arr[:, :, 3] = arr[:, :, 3] * edge_factor

    arr[0, :, 3] = 0
    arr[-1, :, 3] = 0
    arr[:, 0, 3] = 0
    arr[:, -1, 3] = 0
    return Image.fromarray(arr.astype(np.uint8))

def process_parchment_background(crop, tol=28, edge_feather=5):
    rgba = crop.convert("RGBA")
    arr = np.array(rgba, dtype=np.float32)
    
    # Reference painting background parchment color
    bg_color = np.array([238.0, 214.0, 184.0], dtype=np.float32)
    diff = np.sqrt(np.sum((arr[:, :, :3] - bg_color) ** 2, axis=2))
    alpha = np.clip((diff - 22) / float(tol), 0, 1) * 255.0
    arr[:, :, 3] = alpha

    temp = Image.fromarray(arr.astype(np.uint8))
    bbox = temp.getbbox()
    if bbox:
        temp = temp.crop(bbox)
        arr = np.array(temp, dtype=np.float32)

    h, w, _ = arr.shape
    y_dist = np.minimum(np.arange(h), h - 1 - np.arange(h))
    x_dist = np.minimum(np.arange(w), w - 1 - np.arange(w))
    grid_y, grid_x = np.meshgrid(y_dist, x_dist, indexing='ij')
    edge_dist = np.minimum(grid_y, grid_x)
    edge_factor = np.clip((edge_dist - 0.5) / float(edge_feather), 0, 1)
    arr[:, :, 3] = arr[:, :, 3] * edge_factor

    # Absolute guarantee: perimeter is 0
    arr[0, :, 3] = 0
    arr[-1, :, 3] = 0
    arr[:, 0, 3] = 0
    arr[:, -1, 3] = 0
    return Image.fromarray(arr.astype(np.uint8))

def zero_perimeter(img):
    arr = np.array(img)
    if arr.ndim == 3 and arr.shape[2] == 4:
        arr[0, :, 3] = 0
        arr[-1, :, 3] = 0
        arr[:, 0, 3] = 0
        arr[:, -1, 3] = 0
        return Image.fromarray(arr)
    return img

def main():
    root = r"C:\Users\shawn\.gemini\antigravity\brain\0e999a6a-707e-4dc1-a571-718ddb3784b7"
    ref_map_path = os.path.join(root, r".user_uploaded\media_1789798300621.jpg")
    ref_map = Image.open(ref_map_path)
    
    out_dir = r"f:\GameDev\Original\PlanetGenerationCore\csharp\resources\textures\guohua"
    os.makedirs(out_dir, exist_ok=True)
    
    # 1. Mountain Peak (from ultra high-res generated image)
    gen_peak = os.path.join(root, "chinese_mountain_test_1789798759043.jpg")
    if os.path.exists(gen_peak):
        peak_img = process_white_background(gen_peak, tol=30)
        peak_img.thumbnail((512, 512), Image.Resampling.LANCZOS)
        peak_img = zero_perimeter(peak_img)
        peak_img.save(os.path.join(out_dir, "mountain_peak.png"))
        print("Saved mountain_peak.png", peak_img.size)
    
    # 2. Walled City (from ultra high-res generated image)
    gen_city = os.path.join(root, "chinese_city_walled_1789798896057.jpg")
    if os.path.exists(gen_city):
        city_img = process_white_background(gen_city, tol=30)
        city_img.thumbnail((512, 512), Image.Resampling.LANCZOS)
        city_img = zero_perimeter(city_img)
        city_img.save(os.path.join(out_dir, "settlement_city.png"))
        print("Saved settlement_city.png", city_img.size)
        
    # 3. Mountain Ridge (from generated image)
    gen_ridge = os.path.join(root, "chinese_mountain_ridge_1789798962539.jpg")
    if os.path.exists(gen_ridge):
        ridge_img = process_white_background(gen_ridge, tol=30)
        ridge_img.thumbnail((768, 384), Image.Resampling.LANCZOS)
        ridge_img = zero_perimeter(ridge_img)
        ridge_img.save(os.path.join(out_dir, "mountain_ridge.png"))
        print("Saved mountain_ridge.png", ridge_img.size)

    # 4. Elements from reference map
    crops_def = {
        # Mountain hill (青绿小山丘 / 丘陵)
        "mountain_hill": ((262, 175, 365, 255), 28),
        # Hill Yunhai (云海岭)
        "mountain_hill_small": ((532, 52, 600, 108), 28),
        # Peak Tianzhu from ref map as alternative
        "mountain_peak_ref": ((375, 35, 455, 140), 28),
        # Pine Forest (密林)
        "forest_pine": ((520, 130, 595, 195), 24),
        # Bamboo Grove (竹林)
        "forest_bamboo": ((422, 195, 475, 235), 22),
        # Terraced fields (梯田)
        "field_terraced": ((268, 395, 388, 440), 24),
        # Town (城镇)
        "settlement_town": ((355, 288, 442, 332), 24),
        # Town 2 (雁归镇下合院)
        "settlement_town_alt": ((488, 342, 542, 382), 24),
        # Village (村庄)
        "settlement_village": ((610, 285, 655, 315), 24),
        # Village 2 (流坝镇茅舍)
        "settlement_village_alt": ((62, 430, 138, 465), 24),
        # Temple (万佛寺古刹)
        "settlement_temple": ((766, 145, 810, 178), 24),
        # Temple Pagoda (东海驿宝塔)
        "settlement_pagoda": ((796, 298, 838, 338), 24),
        # Bridge (拱桥)
        "bridge_arch": ((672, 260, 712, 292), 24),
        # Boat (乌篷帆船)
        "boat_junk": ((42, 165, 72, 192), 22),
        # Boat 2 (帆船2)
        "boat_junk_alt": ((936, 82, 964, 108), 22),
    }

    for name, (box, tol) in crops_def.items():
        crop = ref_map.crop(box)
        proc = process_parchment_background(crop, tol=tol)
        target_path = os.path.join(out_dir, f"{name}.png")
        proc.save(target_path)
        print(f"Saved {name}.png", proc.size)
        
    # Legend Box: we save the complete legend box crop
    # Box bounds: (886, 420, 982, 538)
    legend_crop = ref_map.crop((886, 420, 982, 538))
    # Add thin antique inner frame or clean up border
    legend_crop.save(os.path.join(out_dir, "legend_box.png"))
    print("Saved legend_box.png", legend_crop.size)

    # Parchment background texture patch: sample pure parchment paper texture
    # Clean patch from flat paper region
    paper_patch = ref_map.crop((700, 340, 780, 390))
    paper_patch = paper_patch.resize((512, 512), Image.Resampling.BICUBIC)
    paper_patch.save(os.path.join(out_dir, "parchment_bg.png"))
    print("Saved parchment_bg.png", paper_patch.size)

if __name__ == "__main__":
    main()
