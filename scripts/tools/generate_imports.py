import os
import hashlib

guohua_dir = r"f:\GameDev\Original\PlanetGenerationCore\csharp\resources\textures\guohua"
files = [f for f in os.listdir(guohua_dir) if f.endswith(".png")]

for f in files:
    import_path = os.path.join(guohua_dir, f + ".import")
    h = hashlib.md5(f.encode("utf-8")).hexdigest()
    uid = "uid://gh_" + h[:12]
    ctex_name = f"{f}-{h}.ctex"
    content = f"""[remap]

importer="texture"
type="CompressedTexture2D"
uid="{uid}"
path="res://.godot/imported/{ctex_name}"
metadata={{
"vram_texture": false
}}

[deps]

source_file="res://resources/textures/guohua/{f}"
dest_files=["res://.godot/imported/{ctex_name}"]

[params]

compress/mode=0
compress/high_quality=false
compress/lossy_quality=0.7
compress/uastc_level=0
compress/rdo_quality_loss=0.0
compress/hdr_compression=1
compress/normal_map=0
compress/channel_pack=0
mipmaps/generate=false
mipmaps/limit=-1
roughness/mode=0
roughness/src_normal=""
process/channel_remap/red=0
process/channel_remap/green=1
process/channel_remap/blue=2
process/channel_remap/alpha=3
process/fix_alpha_border=true
process/premult_alpha=false
process/normal_map_invert_y=false
process/hdr_as_srgb=false
process/hdr_clamp_exposure=false
process/size_limit=0
detect_3d/compress_to=1
"""
    with open(import_path, "w", encoding="utf-8") as out:
        out.write(content)

print(f"Generated {len(files)} import files successfully.")
