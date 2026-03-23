"""
Re-export the two oversized Hero Props with textures resized to 1024px.
Run: blender --background <blend_file> --python reexport_hero_props.py -- <output_dir>
"""
import bpy
import sys
import os

argv = sys.argv
args = argv[argv.index("--") + 1:] if "--" in argv else []
output_dir = args[0] if args else os.path.join(os.path.dirname(bpy.data.filepath), "reexported_glb")
os.makedirs(output_dir, exist_ok=True)

MAX_TEX_SIZE = 1024

TARGETS = [
    "HeroPropMultiRocketLauncher_A",
    "HeroPropPlasmaGun_A",
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
    """Resize all images larger than max_size to max_size."""
    resized = 0
    for img in bpy.data.images:
        if img.size[0] > max_size or img.size[1] > max_size:
            old_w, old_h = img.size[0], img.size[1]
            # Scale proportionally
            scale = max_size / max(old_w, old_h)
            new_w = int(old_w * scale)
            new_h = int(old_h * scale)
            img.scale(new_w, new_h)
            resized += 1
            print(f"  Resized {img.name}: {old_w}x{old_h} -> {new_w}x{new_h}")
    print(f"[Hero Re-export] Resized {resized} images to max {max_size}px")

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

print(f"[Hero Re-export] Resizing all textures to max {MAX_TEX_SIZE}px...")
resize_images(MAX_TEX_SIZE)

for target in TARGETS:
    objects = find_matching_objects(target)
    if not objects:
        print(f"[Hero Re-export] WARNING: No objects found for '{target}'")
        continue
    obj = objects[0]
    out_name = f"KB3D_FTW_{target}_grp.glb"
    out_path = os.path.join(output_dir, out_name)
    try:
        print(f"[Hero Re-export] Exporting: {obj.name} -> {out_name}")
        export_object_as_glb(obj, out_path)
        size_mb = os.path.getsize(out_path) / (1024 * 1024)
        print(f"[Hero Re-export]   OK ({size_mb:.1f} MB)")
    except Exception as e:
        print(f"[Hero Re-export]   FAILED: {e}")
