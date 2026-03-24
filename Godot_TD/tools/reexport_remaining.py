"""
Re-export remaining KitBash3D models that still lack textures.
Textures resized to 1024px to keep file sizes under GitHub's 100MB limit.
Run: blender --background <blend_file> --python reexport_remaining.py -- <output_dir>
"""
import bpy
import sys
import os

argv = sys.argv
args = argv[argv.index("--") + 1:] if "--" in argv else []
output_dir = args[0] if args else os.path.join(os.path.dirname(bpy.data.filepath), "reexported_glb")
os.makedirs(output_dir, exist_ok=True)

MAX_TEX_SIZE = 1024

# Models that still have old placeholder textures (small GLB files from Mar 16)
TARGETS = [
    # Props
    "PropBarrel_A",
    "PropBarrels_A",
    "PropCrate_A",
    "PropCrate_B",
    "PropContainer_A",
    "PropContainer_B",
    "PropSandbags_A",
    # AXIS
    "PropPwerMast_A",
    "PropRepeater_A",
    # Buildings
    "BldgSmCheckPoint_A",
    "BldgSmFieldBarracks_A",
    "BldgSmFuelTanks_A",
    "BldgSmOutpost_A",
    "BldgSmTrench_A",
    "BldgSmWaterTowers_A",
]

def find_matching_objects(target_fragment):
    matches = []
    for obj in bpy.data.objects:
        if target_fragment in obj.name and obj.parent is None:
            matches.append(obj)
    for col in bpy.data.collections:
        if target_fragment in col.name:
            for obj in col.objects:
                if obj.parent is None or obj.parent.name == col.name:
                    matches.append(obj)
    return list(set(matches))

def resize_images(max_size):
    resized = 0
    for img in bpy.data.images:
        if img.size[0] > max_size or img.size[1] > max_size:
            scale = max_size / max(img.size[0], img.size[1])
            new_w = int(img.size[0] * scale)
            new_h = int(img.size[1] * scale)
            img.scale(new_w, new_h)
            resized += 1
    print(f"[Re-export] Resized {resized} images to max {max_size}px")

def export_object_as_glb(obj, output_path):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    for child in obj.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(
        filepath=output_path,
        export_format='GLB',
        use_selection=True,
        export_apply=True,
        export_texcoords=True,
        export_normals=True,
        export_materials='EXPORT',
        export_image_format='JPEG',
        export_jpeg_quality=75,
    )

print(f"[Re-export] Resizing textures to max {MAX_TEX_SIZE}px...")
resize_images(MAX_TEX_SIZE)

exported = 0
failed = 0

for target in TARGETS:
    objects = find_matching_objects(target)
    if not objects:
        print(f"[Re-export] WARNING: No objects found for '{target}'")
        failed += 1
        continue
    obj = objects[0]
    out_name = f"KB3D_FTW_{target}_grp.glb"
    out_path = os.path.join(output_dir, out_name)
    try:
        print(f"[Re-export] Exporting: {obj.name} -> {out_name}")
        export_object_as_glb(obj, out_path)
        size_mb = os.path.getsize(out_path) / (1024 * 1024)
        print(f"[Re-export]   OK ({size_mb:.1f} MB)")
        exported += 1
    except Exception as e:
        print(f"[Re-export]   FAILED: {e}")
        failed += 1

print(f"\n[Re-export] Done: {exported} exported, {failed} failed")
