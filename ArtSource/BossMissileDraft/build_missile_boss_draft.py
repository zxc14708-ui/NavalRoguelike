"""Produce a separate stage-1 missile-boss art draft; never write into Assets.

Run with Blender 5.x in background mode. The original PatrolBoat.fbx is read only.
Blender: +Y bow, +Z up. Output FBX: -Z Forward, Y Up, 1 m/unit.
"""

import math
from pathlib import Path

import bpy
from mathutils import Vector


HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
ORIGINAL = PROJECT / "Assets/_Game/Art/Models/PatrolBoat.fbx"
OUTPUT = HERE / "PatrolBoat_MissileBoss_Draft.fbx"
SOURCE = HERE / "PatrolBoat_MissileBoss_Draft.blend"

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.context.scene.unit_settings.system = "METRIC"
bpy.context.scene.unit_settings.scale_length = 1.0
bpy.ops.import_scene.fbx(filepath=str(ORIGINAL))
root = bpy.data.objects["PatrolBoat_ReferenceStyle"]

# Remove the decorative aft lifebuoys and two bare black exhaust cylinders
# identified in the reference crop; leave the forward safety gear untouched.
for name in ("Lifebuoy", "Lifebuoy.002", "Exhaust", "Exhaust.001"):
    obj = bpy.data.objects.get(name)
    if obj is not None:
        bpy.data.objects.remove(obj, do_unlink=True)

# The imported mast sits only 0.75 m ahead of the after-gun pivot: its ladder,
# bracing and lower spar intrude into the rotating turret envelope. Re-seat the
# entire mast/antenna assembly on the aft edge of the bridge roof instead.
mast_shift_forward = 1.30
mast_roots = []
for obj in bpy.data.objects:
    if obj.parent != root:
        continue  # RadarPivot children follow their parent automatically.
    if (obj.name == "Main_mast" or obj.name.startswith("Mast_")
            or obj.name.startswith("Ladder_rung") or obj.name.startswith("Rigging")
            or obj.name in {"RadarPivot", "Beacon"}):
        mast_roots.append(obj)
for obj in mast_roots:
    obj.location.y += mast_shift_forward
bpy.context.view_layer.update()


def studio_render(path, view, target=(0, 0, 1.2)):
    # Preview-only lights/camera are created after export, so cannot leak into FBX.
    camera_data = bpy.data.cameras.new("Preview camera")
    camera = bpy.data.objects.new("Preview camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = view
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 24.0
    bpy.context.scene.camera = camera
    for name, pos, power, size in (
        ("Key", (10, 11, 18), 2200, 10),
        ("Fill", (-11, -5, 15), 1600, 12),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = power
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(light)
        light.location = pos
        light.rotation_euler = (Vector(target) - light.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.render.engine = "BLENDER_EEVEE"
    bpy.context.scene.render.resolution_x = 1300
    bpy.context.scene.render.resolution_y = 1100
    bpy.context.scene.render.resolution_percentage = 100
    bpy.context.scene.render.image_settings.file_format = "PNG"
    bpy.context.scene.render.filepath = str(path)
    bpy.context.scene.world.color = (0.06, 0.10, 0.14)
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)
    for name in ("Key", "Fill"):
        bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)


if "--base-only" in __import__("sys").argv:
    studio_render(HERE / "PatrolBoat_before.png", (17, -18, 22))
    raise SystemExit(0)


def material(name, color, metallic=0.0, roughness=0.67):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    shader = next((node for node in mat.node_tree.nodes
                   if node.type == "BSDF_PRINCIPLED"), None)
    if shader is not None:
        shader.inputs["Base Color"].default_value = (*color, 1.0)
        shader.inputs["Metallic"].default_value = metallic
        shader.inputs["Roughness"].default_value = roughness
    return mat


armor = material("Naval cool grey", (0.37, 0.45, 0.47), 0.12)
deck = material("Nonslip deck", (0.22, 0.29, 0.31), 0.10)
weapon = material("Enemy weapon dark", (0.10, 0.16, 0.18), 0.26)
recess = material("Gunmetal", (0.026, 0.044, 0.054), 0.55)
warning = material("Warning red", (0.62, 0.07, 0.045), 0.10)


def box(name, loc, size, mat, parent=root, rotation=None, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    if rotation is not None:
        obj.rotation_mode = "QUATERNION"
        obj.rotation_quaternion = rotation
    bpy.context.view_layer.update()
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel:
        mod = obj.modifiers.new("Box edge", "BEVEL")
        mod.width = bevel
        mod.segments = 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(mat)
    bpy.context.view_layer.update()
    world = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = world
    return obj


def socket(name, pos, direction):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.empty_display_type = "SINGLE_ARROW"
    obj.empty_display_size = 0.17
    obj.location = pos
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("-Y", "Z")
    bpy.context.view_layer.update()
    world = obj.matrix_world.copy()
    obj.parent = root
    obj.matrix_world = world
    return obj


def cylinder(name, loc, radius, depth, mat, axis, vertices=14):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius,
                                        depth=depth, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = axis.to_track_quat("Z", "Y")
    bpy.context.view_layer.update()
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(mat)
    bpy.context.view_layer.update()
    world = obj.matrix_world.copy()
    obj.parent = root
    obj.matrix_world = world
    return obj


# Two four-round inclined launchers: four circular canisters in a 2 x 2 stack
# per side, framed like the supplied naval photo. This is an eight-tube visual
# variant; the unchanged game script still fires its existing six-shot salvo.
canister_length = 1.80
canister_y = -5.90
for side, bank_name in ((-1, "Port"), (1, "Starboard")):
    rack_x = side * 1.53
    box(f"{bank_name}_LauncherFoundation", (rack_x, canister_y, 0.20),
        (2.30, 1.35, 0.20), deck, bevel=0.035)
    box(f"{bank_name}_Cradle", (rack_x, canister_y, 0.49),
        (0.64, 1.15, 0.48), weapon, bevel=0.035)
    for rail_y in (canister_y - 0.54, canister_y + 0.54):
        box(f"{bank_name}_LauncherSideFrame_{rail_y:.2f}",
            (rack_x, rail_y, 1.04), (0.15, 0.085, 1.28), armor)
    box(f"{bank_name}_UpperCrossbar", (rack_x + side * 0.18, canister_y, 1.68),
        (0.13, 1.14, 0.09), weapon)

    # Both banks fire outboard (port/stbd), slightly above the railing.
    direction = Vector((side * 1.0, -0.05, 0.30)).normalized()
    for row in range(2):
        for column in range(2):
            index = (0 if side < 0 else 4) + row * 2 + column + 1
            y = canister_y + (column - 0.5) * 0.50
            center = Vector((rack_x, y, 0.96 + row * 0.48))
            front = center + direction * (canister_length * 0.5)
            rear = center - direction * (canister_length * 0.5)
            tube_name = "MissileTube" if index == 1 else f"MissileTube.{index - 1:03d}"
            cylinder(tube_name, center, 0.215, canister_length, armor, direction)
            cylinder(f"{bank_name}_Tube_{index}_FrontRim", front - direction * 0.035,
                     0.238, 0.085, weapon, direction)
            cylinder(f"{bank_name}_Tube_{index}_Mouth", front + direction * 0.015,
                     0.170, 0.012, recess, direction)
            cylinder(f"{bank_name}_Tube_{index}_RearCap", rear + direction * 0.028,
                     0.228, 0.065, weapon, direction)
            cylinder(f"{bank_name}_Tube_{index}_LockRing", center - direction * 0.59,
                     0.224, 0.045, deck, direction)
            # Narrow red index band so tube count is legible from the game view.
            cylinder(f"{bank_name}_Tube_{index}_Mark", center - direction * 0.69,
                     0.222, 0.020, warning, direction)
            socket(f"MissileLaunchPoint_{index:02d}", front + direction * 0.09,
                   direction)

# New draft assets remain outside Assets. Existing PatrolBoat.fbx and ENE_Boss
# are not changed or linked by this script.
root.name = "PatrolBoat_MissileBoss_Draft"
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
bpy.ops.object.select_all(action="DESELECT")
export_objects = [obj for obj in bpy.data.objects if obj.type in {"MESH", "EMPTY"}]
for obj in export_objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.fbx(filepath=str(OUTPUT), use_selection=True,
                         object_types={"MESH", "EMPTY"}, axis_forward="-Z", axis_up="Y",
                         global_scale=1.0, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False)
tris = sum(len(face.vertices) - 2 for obj in export_objects if obj.type == "MESH"
           for face in obj.data.polygons)
print(f"BOSS_DRAFT={OUTPUT} TRIANGLES={tris} OBJECTS={len(export_objects)}")

studio_render(HERE / "PatrolBoat_MissileBoss_3q.png", (17, -18, 22))
studio_render(HERE / "PatrolBoat_MissileBoss_top.png", (0, -8, 32))

# Proof image only: the FBX and .blend above retain the neutral aft-facing pose.
aft_pivot = bpy.data.objects["AftGun_TurretPivot"]
rest_yaw = aft_pivot.rotation_euler.z
aft_pivot.rotation_euler.z = rest_yaw - math.radians(90)
bpy.context.view_layer.update()
studio_render(HERE / "PatrolBoat_AftGun_turn_90.png", (17, -18, 22))
aft_pivot.rotation_euler.z = rest_yaw
