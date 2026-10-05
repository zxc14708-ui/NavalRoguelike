"""Blender background script: separate the missile boat's fixed base and trainable rack.

Usage: blender -b --factory-startup --python rig_missile_boat.py -- input.fbx output.fbx
The FBX is intentionally derived from the existing art to retain its hull/materials.
"""
import sys
import bpy
from mathutils import Matrix, Vector


source, output = sys.argv[sys.argv.index("--") + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=source)

root = bpy.data.objects["EnemyMissileBoat_Real"]
assembly = bpy.data.objects["MissileLauncherAssembly"]
pivot = bpy.data.objects["MissileLauncherPivot"]
assert assembly.parent == root and pivot.parent == assembly

# The old pivot sat at the muzzle while the visible tubes were its siblings.
# Place the yaw axis on the centre of the deck mounting and keep the ship fixed.
pivot.location = Vector((0.0, 0.0, 0.0))
bpy.context.view_layer.update()
moving_prefixes = ("MissileCanister_", "CanisterFront_", "CanisterWarning_", "LauncherCradle_")
moving = [o for o in list(assembly.children) + list(pivot.children)
          if o.name.startswith(moving_prefixes)]
assert len(moving) == 16, f"Expected four complete tubes, got {len(moving)} parts"
for part in moving:
    if part.parent == pivot:
        continue
    world = part.matrix_world.copy()
    part.parent = pivot
    part.matrix_parent_inverse = Matrix.Identity(4)
    part.matrix_world = world
    bpy.context.view_layer.update()

# Short fixed pedestal remains outside the yaw pivot, visibly connecting rack and deck.
if "LauncherBasePedestal" not in bpy.data.objects:
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.56, depth=0.18,
                                        location=(0.0, -2.1, 0.61))
    base = bpy.context.object
    base.name = "LauncherBasePedestal"
    base.data.materials.append(bpy.data.materials["Enemy Dark"])
    world = base.matrix_world.copy()
    base.parent = assembly
    base.matrix_parent_inverse = Matrix.Identity(4)
    base.matrix_world = world

for o in bpy.data.objects:
    o.select_set(False)
root.select_set(True)
for o in root.children_recursive:
    o.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.fbx(filepath=output, use_selection=True,
                         object_types={"MESH", "EMPTY"},
                         axis_forward="-Z", axis_up="Y", add_leaf_bones=False)
print("RIGGED_LAUNCHER", len(moving), "moving parts; fixed pedestal exported")
