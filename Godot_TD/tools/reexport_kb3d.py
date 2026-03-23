"""
Re-export KitBash3D Future Warfare models from .blend with textures embedded.
Run: blender --background <blend_file> --python reexport_kb3d.py -- <output_dir>

Exports each top-level collection/object matching our target names as individual
GLB files with textures baked into the binary (not external references).
"""
import bpy
import sys
import os

# Parse args after "--"
argv = sys.argv
args = argv[argv.index("--") + 1:] if "--" in argv else []
output_dir = args[0] if args else os.path.join(os.path.dirname(bpy.data.filepath), "reexported_glb")

os.makedirs(output_dir, exist_ok=True)

# Target model name fragments we need to re-export
TARGETS = [
    "PropTurret_A",
    "PropTurret_B",
    "PropTurret_C",
    "PropWeapon_A",
    "PropWeapon_B",
    "PropRadar_A",
    "PropSatellite_A",
    "PropAntenna_A",
    "PropAntenna_B",
    "PropGenerator_A",
    "PropBarrier_A",
    "PropBarrier_B",
    "PropLampPost_A",
    "PropLampPost_B",
    "PropAntiTankHedgehog_A",
    "PropFencePost_A",
    # Also re-export the ones that work, for consistency
    "HeroPropMultiRocketLauncher_A",
    "HeroPropPlasmaGun_A",
    "PropGenerator_B",
]

def find_matching_objects(target_fragment):
    """Find top-level objects or collection roots matching the target."""
    matches = []
    for obj in bpy.data.objects:
        if target_fragment in obj.name and obj.parent is None:
            matches.append(obj)
    # Also check collections
    for col in bpy.data.collections:
        if target_fragment in col.name:
            for obj in col.objects:
                if obj.parent is None or obj.parent.name == col.name:
                    matches.append(obj)
    return list(set(matches))

def export_object_as_glb(obj, output_path):
    """Select only this object hierarchy and export as GLB."""
    # Deselect all
    bpy.ops.object.select_all(action='DESELECT')

    # Select the object and all children
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
        export_image_format='JPEG',  # Embed as JPEG to keep size reasonable
        export_jpeg_quality=75,
    )

print(f"[KB3D Re-export] Blend file: {bpy.data.filepath}")
print(f"[KB3D Re-export] Output dir: {output_dir}")
print(f"[KB3D Re-export] Objects in scene: {len(bpy.data.objects)}")
print(f"[KB3D Re-export] Materials: {len(bpy.data.materials)}")
print(f"[KB3D Re-export] Images: {len(bpy.data.images)}")

# List all images to understand texture situation
print("\n[KB3D Re-export] All images in blend file:")
for img in bpy.data.images:
    packed = "PACKED" if img.packed_file else "external"
    print(f"  {img.name}: {img.filepath} ({packed}, {img.size[0]}x{img.size[1]})")

exported = 0
failed = 0

for target in TARGETS:
    objects = find_matching_objects(target)
    if not objects:
        print(f"[KB3D Re-export] WARNING: No objects found for '{target}'")
        failed += 1
        continue

    # Use the first match
    obj = objects[0]
    out_name = f"KB3D_FTW_{target}_grp.glb"
    out_path = os.path.join(output_dir, out_name)

    try:
        print(f"[KB3D Re-export] Exporting: {obj.name} -> {out_name}")
        export_object_as_glb(obj, out_path)

        size_mb = os.path.getsize(out_path) / (1024 * 1024)
        print(f"[KB3D Re-export]   OK ({size_mb:.1f} MB)")
        exported += 1
    except Exception as e:
        print(f"[KB3D Re-export]   FAILED: {e}")
        failed += 1

print(f"\n[KB3D Re-export] Done: {exported} exported, {failed} failed")
