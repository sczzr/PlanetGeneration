"""
Compose the 21 mountain CLUSTER sprites out of existing `mountain_atlas_single`
sprites (no image model needed), then hand them to pack_mountain_cluster.py.

Depth and footing:
  * peaks sit on different baselines — far ones smaller and higher, near ones
    larger and lower — and are drawn far-to-near, so nearer slopes may occlude
    the mountains behind them;
  * every peak is pasted whole, never clipped;
  * before pasting, a peak is shifted sideways until its foot lands only on
    empty ground. A foot never covers another mountain.

Output: GEN_DIR/{cluster_name}.png  (transparent RGBA, bottom-anchored)
"""
import os
import json
import random
import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None  # the single atlas is a legitimate 6144x27389 sheet

ROOT = r"f:\GameDev\Original\PlanetGenerationCore\csharp"
OUT_DIR = os.path.join(ROOT, "resources", "textures", "guohua")
CATALOG = os.path.join(OUT_DIR, "terrain_catalog.json")
SINGLE_PNG = os.path.join(OUT_DIR, "mountain_atlas_single.png")
GEN_DIR = os.path.join(os.environ.get("PI_SCRATCH_DIR", "."), "mountain_cluster_src")
CH = 320   # working canvas height in px (downscaled for 4K atlas)

# (category, sub_category) -> source pools, overlap, profile.
#   profile "peak" = centre tall (ranges / massifs / volcano / caldera)
#   profile "even" = roughly level row with jitter (hills / dunes / pillars / mesas)
RECIPE = {
    ("Volcano", "VolcanoCrater"): (["VolcanoCrater", "VolcanoCone"], 0.30, "peak"),
    ("Mountain", "GrandRange"): (["MainPeak", "CompanionPeak", "Ridge"], 0.34, "peak"),
    ("Mountain", "MassifCluster"): (["MainPeak", "CompanionPeak"], 0.36, "peak"),
    ("SnowMountain", "SnowGrandRange"): (["GlacierPeak", "SnowCompanionPeak", "SnowRidge"], 0.34, "peak"),
    ("SnowMountain", "SnowMassif"): (["GlacierPeak", "SnowCompanionPeak"], 0.36, "peak"),
    ("Basin", "CalderaRing"): (["EncirclingArm"], 0.30, "even"),
    ("Basin", "EncirclingArm"): (["EncirclingArm"], 0.32, "peak"),
    ("Plateau", "CliffPillar"): (["CliffPillar"], 0.28, "even"),
    ("Plateau", "PlateauMassif"): (["TableMesa", "CliffPillar"], 0.30, "even"),
    ("Hills", "HillChain"): (["Knoll"], 0.46, "even"),
    ("Hills", "HillCopse"): (["Knoll"], 0.42, "even"),
    ("Desert", "SandRidge"): (["CrescentDune", "SandRipple", "Oasis"], 0.40, "even"),
}
def build_pools(catalog, out_dir):
    """sub_category -> list of cropped RGBA source sprites from the single atlas sheets."""
    atlases = {}
    pools = {}
    for s in catalog["sprites"]:
        atlas_file = s.get("atlas", "mountain_atlas_single.png")
        if "cluster" in atlas_file:
            continue
        if atlas_file not in atlases:
            atlas_path = os.path.join(out_dir, atlas_file)
            if os.path.exists(atlas_path):
                atlases[atlas_file] = Image.open(atlas_path).convert("RGBA")
        if atlas_file in atlases:
            atlas_img = atlases[atlas_file]
            x, y, w, h = (int(v) for v in s["rect"])
            crop = atlas_img.crop((x, y, x + w, y + h))
            pools.setdefault(s["sub_category"], []).append(crop)
    return pools


def height_factor(profile, pos, rng):
    """pos = horizontal position 0..1 across the target width."""
    if profile == "peak":
        base = 0.70 + 0.30 * (1.0 - abs(pos - 0.5) * 2.0)
    else:
        base = 0.80
    return base * rng.uniform(0.9, 1.0)


FOOT_RATIO = 0.22  # bottom band that must sit on empty ground, never on a peak
MAX_W = 1900       # stay inside the 4096-wide atlas after the gutter (fits side by side)


def _source_over(dst, src):
    """src over dst. Pixels on empty ground are copied unchanged (no blend rounding)."""
    clear = dst[:, :, 3] == 0
    dst[clear] = src[clear]
    overlap = ~clear & (src[:, :, 3] > 0)
    if not overlap.any():
        return
    s = src[overlap].astype(np.float32)
    d = dst[overlap].astype(np.float32)
    sa = s[:, 3:4] / 255.0
    da = d[:, 3:4] / 255.0
    out_a = sa + da * (1.0 - sa)
    out_rgb = (s[:, :3] * sa + d[:, :3] * da * (1.0 - sa)) / np.maximum(out_a, 1e-6)
    blended = np.empty_like(s)
    blended[:, :3] = out_rgb
    blended[:, 3:4] = out_a * 255.0
    dst[overlap] = np.clip(blended, 0, 255).astype(np.uint8)


def _grow(canvas, need_w, need_h):
    h, w = canvas.shape[:2]
    if need_w <= w and need_h <= h:
        return canvas
    grown = np.zeros((max(h, need_h), max(w, need_w), 4), dtype=np.uint8)
    grown[:h, :w] = canvas
    return grown


def _grow_mask(mask, need_w, need_h):
    h, w = mask.shape
    if need_w <= w and need_h <= h:
        return mask
    grown = np.zeros((max(h, need_h), max(w, need_w)), dtype=bool)
    grown[:h, :w] = mask
    return grown


def body_covers_foot(foot_mask, sprite, x, top, foot_h):
    """True when this peak's body would paint over a foot already on the ground."""
    body = sprite[:-foot_h]
    if body.size == 0 or top < 0 or x < 0:
        return True
    if top + body.shape[0] > foot_mask.shape[0] or x + body.shape[1] > foot_mask.shape[1]:
        return True
    region = foot_mask[top:top + body.shape[0], x:x + body.shape[1]]
    return bool(np.any((body[:, :, 3] > 0) & region))

def clear_foot_x(canvas, sprite, x_hint, top):
    """Smallest x >= x_hint whose foot lands only on empty ground, or past the content."""
    sh, sw = sprite.shape[:2]
    foot_h = max(1, int(round(sh * FOOT_RATIO)))
    fy = top + sh - foot_h
    foot_cols = np.any(sprite[-foot_h:, :, 3] > 0, axis=0)
    if fy < 0 or fy + foot_h > canvas.shape[0]:
        raise SystemExit("[foot] peak foot is outside the canvas")
    blocked = np.any(canvas[fy:fy + foot_h, :, 3] > 0, axis=0).astype(np.int32)
    signal = np.concatenate([blocked, np.zeros(sw, dtype=np.int32)])
    hits = np.convolve(signal, foot_cols[::-1].astype(np.int32), mode="valid")
    if x_hint >= len(hits):
        return x_hint
    clear = np.flatnonzero(hits[x_hint:] == 0)
    if len(clear) == 0:
        raise SystemExit("[foot] no clear ground for a full peak")
    return x_hint + int(clear[0])


def foot_covers_something(canvas, sprite, x, top):
    """True when any foot pixel would land on content already drawn."""
    sh, sw = sprite.shape[:2]
    foot_h = max(1, int(round(sh * FOOT_RATIO)))
    fy = top + sh - foot_h
    if x < 0 or fy < 0 or fy + foot_h > canvas.shape[0] or x + sw > canvas.shape[1]:
        return True
    dest = canvas[fy:fy + foot_h, x:x + sw, 3]
    foot = sprite[-foot_h:, :, 3]
    return bool(np.any((foot > 0) & (dest > 0)))


CANVAS_H = 300
# far -> near. Rear bases sit above the tallest foreground foot, so a near
# foot can rest in front of a far mountain without covering it.
DEPTH_LIFT = (0.50, 0.30, 0.0)
DEPTH_SCALE = ((0.28, 0.42), (0.42, 0.58), (0.72, 0.92))


def compose(defn, pools, rng):
    subs, overlap, profile = RECIPE[(defn["category"], defn["sub_category"])]
    sources = [c for sub in subs for c in pools.get(sub, [])]
    if not sources:
        raise SystemExit(f"no single sources for {defn['name']} ({subs})")
    aspect = defn["world_size"][0] / defn["world_size"][1]
    target_w = max(2, round(CANVAS_H * aspect))

    # A few peaks per depth. Far is drawn first; each peak is then shifted only
    # as far as its own foot needs. Anything that would exceed the atlas width
    # is left out — peaks are never scaled down to fit.
    n_near = max(2, min(5, round(aspect * 1.1)))
    plan = ((0, max(1, n_near - 1)), (1, max(1, n_near - 1)), (2, n_near))
    specs = []
    for depth, count in plan:
        lo, hi = DEPTH_SCALE[depth]
        for _ in range(count):
            src = rng.choice(sources)
            if rng.random() < 0.5:
                src = src.transpose(Image.FLIP_LEFT_RIGHT)
            th = max(2, int(CANVAS_H * rng.uniform(lo, hi)))
            if profile == "peak" and depth == 2:
                th = max(th, int(CANVAS_H * 0.82))
            tw = max(2, int(src.width * (th / src.height)))
            lift = int(CANVAS_H * DEPTH_LIFT[depth] + rng.uniform(0, CANVAS_H * 0.03))
            if depth == 2:
                lift = 0
            lift = min(lift, CANVAS_H - th)
            sprite = np.array(src.resize((tw, th), Image.LANCZOS).convert("RGBA"))
            specs.append((depth, lift, sprite))

    canvas = np.zeros((CANVAS_H, 16, 4), dtype=np.uint8)
    cursors = [0, 0, 0]
    placed = []
    shifts = 0
    for depth, lift, sprite in specs:
        th, tw = sprite.shape[:2]
        top = CANVAS_H - th - lift
        if top < 0:
            raise SystemExit(f"[foot] {defn['name']}: peak does not fit the canvas")
        x_hint = cursors[depth]
        canvas = _grow(canvas, x_hint + tw, CANVAS_H)
        x = clear_foot_x(canvas, sprite, x_hint, top)
        if x + tw > MAX_W:
            continue
        shifts += x - x_hint
        canvas = _grow(canvas, x + tw, CANVAS_H)
        _source_over(canvas[top:top + th, x:x + tw], sprite)
        placed.append((x, tw))
        cursors[depth] = x + max(1, int(tw * (1.0 - overlap)))
    if len(placed) < 3:
        raise SystemExit(f"[foot] {defn['name']}: could not place 3 full peaks without a foot collision")

    used = max(x + tw for x, tw in placed)
    opaque_rows = np.where(canvas[:, :used, 3].any(axis=1))[0]
    top_row = int(opaque_rows[0]) if len(opaque_rows) else 0
    image = Image.fromarray(canvas[top_row:, :used], "RGBA")
    return image, shifts


def main():
    os.makedirs(GEN_DIR, exist_ok=True)
    catalog = json.load(open(CATALOG, encoding="utf-8"))
    pools = build_pools(catalog, OUT_DIR)
    cluster = [s for s in catalog["sprites"] if "cluster" in s.get("atlas", "")]
    for defn in sorted(cluster, key=lambda s: s["id"]):
        rng = random.Random(defn["id"] * 7919 + 13)
        img, shifts = compose(defn, pools, rng)
        out = os.path.join(GEN_DIR, defn["name"] + ".png")
        img.save(out)
        print(f"  {defn['name']:<28} {img.width}x{img.height}  foot_shifts={shifts}")
    print(f"Composed {len(cluster)} cluster sprites into {GEN_DIR}")


if __name__ == "__main__":
    main()
