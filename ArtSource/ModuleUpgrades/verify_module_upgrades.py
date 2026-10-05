from pathlib import Path
import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
FILES = sorted(ROOT.glob("MOD_*_U[12].fbx"))


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def descendants(root):
    return [root] + list(root.children_recursive)


def world_bounds(meshes):
    points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    mins = tuple(min(p[i] for p in points) for i in range(3))
    maxs = tuple(max(p[i] for p in points) for i in range(3))
    return mins, maxs


for path in FILES:
    reset()
    bpy.ops.import_scene.fbx(filepath=str(path))
    roots = [obj for obj in bpy.context.scene.objects if obj.parent is None]
    assert len(roots) == 1, f"{path.name}: expected one root, got {len(roots)}"
    root = roots[0]
    objects = descendants(root)
    meshes = [obj for obj in objects if obj.type == "MESH"]
    names = {obj.name for obj in objects}
    assert all(abs(v - 1.0) < 1e-4 for v in root.scale), f"{path.name}: root scale {tuple(root.scale)}"
    assert max(abs(v) for v in root.location) < 1e-4, f"{path.name}: root location {tuple(root.location)}"
    mins, maxs = world_bounds(meshes)
    assert abs(mins[2]) < 0.002, f"{path.name}: floor z={mins[2]:.4f}"
    if any(token in path.stem for token in ("Autocannon", "Gun76", "CIWS")):
        required = {"TurretPivot", "ElevationPivot", "Muzzle"}
        missing = required - names
        assert not missing, f"{path.name}: missing {sorted(missing)}"
        turret = bpy.data.objects["TurretPivot"]
        elevation = bpy.data.objects["ElevationPivot"]
        muzzle = bpy.data.objects["Muzzle"]
        assert elevation.parent == turret, f"{path.name}: ElevationPivot is not under TurretPivot"
        assert muzzle.parent == elevation, f"{path.name}: Muzzle is not under ElevationPivot"
        fixed = [obj for obj in meshes if obj.parent == root and any(
            token in obj.name.lower() for token in ("pedestal", "foundation", "flange", "bearing"))]
        assert fixed, f"{path.name}: fixed pedestal/foundation was not found"
    tris = sum(len(poly.vertices) - 2 for obj in meshes for poly in obj.data.polygons)
    size = tuple(maxs[i] - mins[i] for i in range(3))
    print(f"PASS {path.name}: root={root.name} scale={tuple(round(v, 3) for v in root.scale)} "
          f"bounds={tuple(round(v, 3) for v in size)} floor={mins[2]:.3f} tris={tris}")

print(f"VERIFIED {len(FILES)} UPGRADE MODELS")
