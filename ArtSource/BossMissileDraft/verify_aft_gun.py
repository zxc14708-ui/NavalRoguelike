"""Read-only swept-pose mesh check for the draft aft gun."""

from pathlib import Path
import math
import bpy
from mathutils.bvhtree import BVHTree

source = Path(__file__).resolve().parent / "PatrolBoat_MissileBoss_Draft.blend"
bpy.ops.wm.open_mainfile(filepath=str(source))

pivot = bpy.data.objects["AftGun_TurretPivot"]
muzzle = bpy.data.objects["AftGun_Muzzle"]
moving = [obj for obj in pivot.children if obj.type == "MESH"]
fixed = [obj for obj in bpy.data.objects if obj.type == "MESH" and
         (obj.name in {"Main_mast", "Wheelhouse", "Bridge_roof"}
          or obj.name.startswith(("Mast_", "Ladder_rung", "Rigging")))]


def tree(obj):
    points = [obj.matrix_world @ v.co for v in obj.data.vertices]
    polys = [list(poly.vertices) for poly in obj.data.polygons]
    return BVHTree.FromPolygons(points, polys)


fixed_trees = {obj.name: tree(obj) for obj in fixed}
rest_yaw = pivot.rotation_euler.z
print("AFT_GUN_PIVOT", tuple(round(x, 3) for x in pivot.matrix_world.translation))
print("MOVING_PARTS", [obj.name for obj in moving])
print("REST_YAW_DEGREES", round(math.degrees(rest_yaw), 1))
for angle in (-180, -165, -150, -135, -120, -90, -60, 0,
              60, 90, 120, 135, 150, 165, 180):
    pivot.rotation_euler.z = rest_yaw + math.radians(angle)
    bpy.context.view_layer.update()
    hits = set()
    for obj in moving:
        moving_tree = tree(obj)
        for name, fixed_tree in fixed_trees.items():
            if moving_tree.overlap(fixed_tree):
                hits.add(name)
    print("ANGLE", angle, "MUZZLE",
          tuple(round(x, 2) for x in muzzle.matrix_world.translation),
          "INTERSECTIONS", sorted(hits))
pivot.rotation_euler.z = rest_yaw
