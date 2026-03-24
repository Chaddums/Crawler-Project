"""
Re-export oversized buildings at 512px textures to stay under GitHub 100MB limit.
"""
import bpy, sys, os

argv = sys.argv
args = argv[argv.index("--") + 1:] if "--" in argv else []
output_dir = args[0] if args else "."
os.makedirs(output_dir, exist_ok=True)

TARGETS = ["BldgSmCheckPoint_A", "BldgSmOutpost_A", "BldgSmWaterTowers_A"]

# Resize to 512px
for img in bpy.data.images:
    if img.size[0] > 512 or img.size[1] > 512:
        s = 512 / max(img.size[0], img.size[1])
        img.scale(int(img.size[0]*s), int(img.size[1]*s))

for target in TARGETS:
    matches = [o for o in bpy.data.objects if target in o.name and o.parent is None]
    if not matches:
        for col in bpy.data.collections:
            if target in col.name:
                matches += [o for o in col.objects if o.parent is None or o.parent.name == col.name]
    if not matches:
        print(f"WARNING: {target} not found"); continue
    obj = list(set(matches))[0]
    out = os.path.join(output_dir, f"KB3D_FTW_{target}_grp.glb")
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    for c in obj.children_recursive: c.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', use_selection=True,
        export_apply=True, export_texcoords=True, export_normals=True,
        export_materials='EXPORT', export_image_format='JPEG', export_jpeg_quality=65)
    print(f"OK: {target} -> {os.path.getsize(out)/(1024*1024):.1f} MB")
