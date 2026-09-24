"""
Repack the Guohua mountain CLUSTER atlas from freshly generated per-sprite art.

It rebuilds ONLY the `mountain_atlas_cluster.png` sprites (ids 98..118) and
rewrites their rects in terrain_catalog.json, while leaving every
`mountain_atlas_single.png` entry and the schema (version 3) untouched.

Decal-rule compliance is baked into the geometry, not into runtime guesses:
  * every sprite keeps its bottom-anchored pivot (y ~= 0.99), so the foot sits
    on the ground and the runtime GroundDecalProfile.Contact split (bottom 4%)
    extracts only the ground-contact skirt, never a vertical cliff face;
  * each source is trimmed to its opaque silhouette and padded to the catalog
    aspect (bottom-centre), so world_size / pivot stay valid unchanged;
  * a self-check confirms the bottom 4% band of every packed region is
    non-empty and strictly at the base of the silhouette.

Input : GEN_DIR/{sprite_name}.png  (art on a solid #FF00FF magenta backdrop,
        the painting itself must contain no magenta/pink).
Output: resources/textures/guohua/mountain_atlas_cluster.png  (+ catalog rects)

Usage : python pack_mountain_cluster.py [GEN_DIR]
"""
import os
import sys
import json
import numpy as np
from PIL import Image

ROOT = r"f:\GameDev\Original\PlanetGenerationCore\csharp"
OUT_DIR = os.path.join(ROOT, "resources", "textures", "guohua")
CATALOG = os.path.join(OUT_DIR, "terrain_catalog.json")
ATLAS_NAME = "mountain_atlas_cluster.png"
ATLAS_PNG = os.path.join(OUT_DIR, ATLAS_NAME)

ATLAS_W = 4096          # 4K standard atlas width
GUTTER = 16             # transparent gutter between sprites
KEY_RGB = (255, 0, 255) # magenta chroma backdrop to remove
KEY_TOL = 72            # chroma distance tolerance
DECAL_BAND = 0.04       # runtime GroundDecalProfile.Contact bottom ratio
MAX_PAGE_H = 4096

GEN_DIR = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    os.environ.get("PI_SCRATCH_DIR", "."), "mountain_cluster_src")


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


def load_catalog():
    with open(CATALOG, encoding="utf-8") as f:
        return json.load(f)
def cutout(img):
    """RGBA silhouette: drop the magenta backdrop and any fully transparent px."""
    rgba = np.array(img.convert("RGBA"), dtype=np.uint8)
    rgb = rgba[:, :, :3].astype(np.int32)
    dist = np.abs(rgb - np.array(KEY_RGB)).sum(axis=2)
    is_key = dist <= KEY_TOL
    alpha = rgba[:, :, 3].copy()
    alpha[is_key] = 0
    rgba[:, :, 3] = alpha
    return rgba


def trim(rgba):
    """Crop to the opaque bounding box."""
    ys, xs = np.where(rgba[:, :, 3] > 8)
    if len(ys) == 0:
        raise ValueError("sprite is fully transparent after keying")
    y0, y1 = ys.min(), ys.max() + 1
    x0, x1 = xs.min(), xs.max() + 1
    return rgba[y0:y1, x0:x1]


def pad_to_aspect(rgba, target_aspect, max_w=ATLAS_W - GUTTER):
    """Pad (never scale) to target w/h, anchored bottom-centre so the foot line
    and the bottom-anchored pivot stay exact."""
    h, w = rgba.shape[:2]
    cur = w / h
    if abs(cur - target_aspect) < 1e-3:
        out = rgba
    elif cur < target_aspect:               # too narrow -> pad width, centred
        new_w = int(round(h * target_aspect))
        out = np.zeros((h, new_w, 4), dtype=np.uint8)
        off = (new_w - w) // 2
        out[:, off:off + w] = rgba
    else:                                   # too short -> pad height on TOP only
        new_h = int(round(w / target_aspect))
        out = np.zeros((new_h, w, 4), dtype=np.uint8)
        out[new_h - h:, :] = rgba           # keep silhouette flush to the base

    if out.shape[1] > max_w:
        scale = max_w / float(out.shape[1])
        sw = max_w
        sh = max(4, int(round(out.shape[0] * scale)))
        out = np.array(Image.fromarray(out).resize((sw, sh), Image.Resampling.LANCZOS))
    return out


def skyline_pack(boxes, max_w=ATLAS_W, max_h=MAX_PAGE_H):
    """boxes: list of (key, w, h). Returns list of (placements, page_h)."""
    order = sorted(boxes, key=lambda b: b[2], reverse=True)
    pages = []
    remaining = list(order)
    while remaining:
        skyline = [0] * max_w
        page_placed = {}
        unplaced = []
        page_h = 0
        for key, w, h in remaining:
            cw, ch = w + GUTTER, h + GUTTER
            if cw > max_w:
                raise ValueError(f"{key}: width {w} exceeds atlas width {max_w}")
            best_x, best_y = 0, 1 << 30
            for x in range(0, max_w - cw + 1, 8):
                y = max(skyline[x:x + cw])
                if y < best_y:
                    best_y, best_x = y, x
            if best_y + ch > max_h and len(page_placed) > 0:
                unplaced.append((key, w, h))
                continue
            for x in range(best_x, best_x + cw):
                skyline[x] = best_y + ch
            page_placed[key] = (best_x + GUTTER // 2, best_y + GUTTER // 2)
            page_h = max(page_h, best_y + ch)
        pages.append((page_placed, page_h + GUTTER))
        remaining = unplaced
    return pages


def decal_band_ok(rgba):
    """True iff the bottom DECAL_BAND of the silhouette is non-empty (the runtime
    Contact split would find a ground skirt) and the silhouette touches the base."""
    a = rgba[:, :, 3] > 8
    h = a.shape[0]
    band = max(1, int(round(h * DECAL_BAND)))
    bottom_touch = a[h - 1].any()
    band_filled = a[h - band:].any()
    return bottom_touch and band_filled


def main():
    catalog = load_catalog()
    cluster = [s for s in catalog["sprites"] if "cluster" in s.get("atlas", "")]
    cluster.sort(key=lambda s: s["id"])
    print(f"Rebuilding {len(cluster)} cluster sprites from {GEN_DIR}")

    missing, prepared = [], {}
    for s in cluster:
        src = os.path.join(GEN_DIR, s["name"] + ".png")
        if not os.path.exists(src):
            missing.append(s["name"])
            continue
        target_aspect = s["world_size"][0] / s["world_size"][1]
        rgba = pad_to_aspect(trim(cutout(Image.open(src))), target_aspect)
        if not decal_band_ok(rgba):
            raise SystemExit(f"[decal] {s['name']}: base band empty — sprite is not "
                             f"bottom-anchored; regenerate with the foot on the ground.")
        prepared[s["id"]] = rgba

    if missing:
        raise SystemExit("Missing generated sprites (expected in GEN_DIR):\n  " +
                         "\n  ".join(missing))

    boxes = [(sid, r.shape[1], r.shape[0]) for sid, r in prepared.items()]
    pages = skyline_pack(boxes, max_w=ATLAS_W, max_h=MAX_PAGE_H)
    print(f"Packed {len(boxes)} clusters into {len(pages)} page(s) (width {ATLAS_W})")

    prefix, ext = os.path.splitext(ATLAS_NAME)
    by_id = {s["id"]: s for s in cluster}
    written_atlases = []

    for page_idx, (placements, page_h) in enumerate(pages):
        page_name = ATLAS_NAME if len(pages) == 1 else f"{prefix}_{page_idx}{ext}"
        page_png = os.path.join(OUT_DIR, page_name)
        canvas = np.zeros((page_h, ATLAS_W, 4), dtype=np.uint8)
        for sid, (x, y) in placements.items():
            rgba = prepared[sid]
            h, w = rgba.shape[:2]
            canvas[y:y + h, x:x + w] = rgba
            s = by_id[sid]
            s["atlas"] = page_name
            s["rect"] = [x, y, w, h]
            s["native_size"] = [w, h]
            s["area"] = int(w * h)
            s["aspect"] = round(w / h, 2)
        save_png_safe(canvas, page_png, optimize=True)
        written_atlases.append(page_name)
        print(f"Wrote {page_png} ({ATLAS_W} x {page_h})")

    # Update atlases in catalog
    existing_atlases = [a for a in catalog.get("atlases", []) if "cluster" not in a]
    catalog["atlases"] = existing_atlases + written_atlases

    with open(CATALOG, "w", encoding="utf-8") as f:
        json.dump(catalog, f, indent=2, ensure_ascii=False)
    print(f"Updated {len(cluster)} cluster rects in {os.path.basename(CATALOG)}; "
          f"single-atlas entries and schema left unchanged.")


if __name__ == "__main__":
    main()
