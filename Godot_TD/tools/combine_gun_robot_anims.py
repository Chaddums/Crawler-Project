"""
Blender script: Combine Robot_1.FBX + separate animation FBX files into one FBX.
Run headless: blender --background --python combine_gun_robot_anims.py

Imports the base mesh, then imports each animation FBX and bakes them all
into the same armature as separate actions. Exports as a single FBX with
all animations embedded (like LilRobot.fbx).
"""

import bpy
import os
import sys

# Paths
BASE_DIR = r"C:\Users\Stu\GitHub\Crawler_Project\Godot\_downloads\robots_\robot-1\source\Robot 1"
MESH_FBX = os.path.join(BASE_DIR, "Robot_1.FBX")
ANIM_DIR = os.path.join(BASE_DIR, "animation")
OUTPUT_FBX = r"C:\Users\Stu\GitHub\Crawler_Project\Godot_TD\Models\Characters\Player\gun_robot.fbx"

# Animations we want (map filename -> action name)
ANIM_MAP = {
    "Idle.FBX": "Idle",
    "Run.FBX": "Run",
    "Aim_Forward_Shoot.FBX": "Attack",
    "Damage_Light.FBX": "Hit",
    "Die.FBX": "Death",
    "Roll.FBX": "Roll",
    "Jump_Up.FBX": "Jump",
    "Fall.FBX": "Fall",
    "Land.FBX": "Land",
}

def clean_scene():
    """Remove everything from the scene."""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for action in bpy.data.actions:
        bpy.data.actions.remove(action)
    for armature in bpy.data.armatures:
        bpy.data.armatures.remove(armature)
    for mesh in bpy.data.meshes:
        bpy.data.meshes.remove(mesh)

def import_fbx(filepath):
    """Import an FBX file and return the imported objects."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=filepath)
    after = set(bpy.data.objects)
    return after - before

def find_armature(objects):
    """Find the armature object in a set of objects."""
    for obj in objects:
        if obj.type == 'ARMATURE':
            return obj
    return None

def main():
    print(f"\n{'='*60}")
    print("Combining Robot_1 mesh + animations into single FBX")
    print(f"{'='*60}\n")

    # Clean scene
    clean_scene()

    # Import base mesh
    print(f"Importing base mesh: {MESH_FBX}")
    if not os.path.exists(MESH_FBX):
        print(f"ERROR: Base mesh not found: {MESH_FBX}")
        sys.exit(1)

    base_objects = import_fbx(MESH_FBX)
    base_armature = find_armature(base_objects)

    if base_armature is None:
        print("ERROR: No armature found in base mesh FBX")
        sys.exit(1)

    print(f"Base armature: {base_armature.name} ({len(base_armature.data.bones)} bones)")

    # Store all actions we'll create
    actions_created = []

    # If the base mesh came with an animation, rename it
    if base_armature.animation_data and base_armature.animation_data.action:
        base_action = base_armature.animation_data.action
        base_action.name = "TPose"
        print(f"Base mesh had animation, renamed to 'TPose'")

    # Import each animation FBX
    for anim_file, action_name in ANIM_MAP.items():
        anim_path = os.path.join(ANIM_DIR, anim_file)
        if not os.path.exists(anim_path):
            print(f"SKIP: {anim_file} not found")
            continue

        print(f"\nImporting animation: {anim_file} -> '{action_name}'")

        # Import the animation FBX
        anim_objects = import_fbx(anim_path)
        anim_armature = find_armature(anim_objects)

        if anim_armature is None:
            print(f"  WARNING: No armature in {anim_file}, skipping")
            # Clean up imported objects
            for obj in anim_objects:
                bpy.data.objects.remove(obj)
            continue

        # Get the action from the imported armature
        if anim_armature.animation_data and anim_armature.animation_data.action:
            action = anim_armature.animation_data.action
            action.name = action_name
            actions_created.append(action)
            print(f"  Action '{action_name}': {action.frame_range[0]:.0f}-{action.frame_range[1]:.0f} frames")
        else:
            print(f"  WARNING: No animation data in {anim_file}")

        # Delete the imported armature (we only needed the action)
        # Unlink action first so it doesn't get deleted with the armature
        if anim_armature.animation_data and anim_armature.animation_data.action:
            anim_armature.animation_data.action = None

        # Delete imported anim objects (mesh, armature, etc.)
        for obj in anim_objects:
            bpy.data.objects.remove(obj, do_unlink=True)

    print(f"\n{'='*60}")
    print(f"Created {len(actions_created)} actions")

    # Now bake all actions into NLA strips on the base armature
    # so they export as a single timeline in FBX
    if not base_armature.animation_data:
        base_armature.animation_data_create()

    # Push all actions as NLA tracks
    nla_tracks = base_armature.animation_data.nla_tracks
    frame_offset = 0

    for action in actions_created:
        track = nla_tracks.new()
        track.name = action.name

        # Calculate frame range
        start = int(action.frame_range[0])
        end = int(action.frame_range[1])
        length = end - start

        # Add strip
        strip = track.strips.new(action.name, int(frame_offset), action)
        strip.action_frame_start = start
        strip.action_frame_end = end

        print(f"  NLA track '{action.name}': frames {frame_offset:.0f}-{frame_offset + length:.0f}")
        frame_offset += length + 10  # 10 frame gap between animations

    # Set the first action as active for preview
    if actions_created:
        base_armature.animation_data.action = actions_created[0]

    # Select only the base armature and its children for export
    bpy.ops.object.select_all(action='DESELECT')
    base_armature.select_set(True)
    for obj in base_objects:
        obj.select_set(True)

    # Export
    print(f"\nExporting to: {OUTPUT_FBX}")
    os.makedirs(os.path.dirname(OUTPUT_FBX), exist_ok=True)

    bpy.ops.export_scene.fbx(
        filepath=OUTPUT_FBX,
        use_selection=True,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=True,
        bake_anim_force_startend_keying=True,
        add_leaf_bones=False,
        apply_scale_options='FBX_SCALE_ALL',
        path_mode='COPY',
        embed_textures=True,
        bake_space_transform=True,
    )

    print(f"\nDone! Exported {len(actions_created)} animations to {OUTPUT_FBX}")
    print(f"{'='*60}\n")

if __name__ == "__main__":
    main()
