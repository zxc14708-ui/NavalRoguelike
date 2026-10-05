import bpy
import json
import sys
from pathlib import Path


def parse_output_dir() -> Path:
    argv = sys.argv
    if "--" not in argv:
        raise RuntimeError("Usage: blender --background --python validate_stage4_fbx.py -- <output_dir>")
    args = argv[argv.index("--") + 1 :]
    if not args:
        raise RuntimeError("Missing output directory")
    return Path(args[0])


def empty_names(objects):
    return sorted(obj.name for obj in objects if obj.type == "EMPTY")


output_dir = parse_output_dir()
models_dir = output_dir / "Models"
results = []

for fbx_path in sorted(models_dir.glob("*.fbx")):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx_path), use_custom_normals=True)

    objects = list(bpy.context.scene.objects)
    roots = [obj for obj in objects if obj.parent is None]
    meshes = [obj for obj in objects if obj.type == "MESH"]
    empties = empty_names(objects)
    root_scale_ok = len(roots) == 1 and all(abs(v - 1.0) < 0.0001 for v in roots[0].scale)
    root_rotation_ok = len(roots) == 1 and all(abs(v) < 0.0001 for v in roots[0].rotation_euler)
    mesh_scale_ok = all(all(abs(v - 1.0) < 0.0001 for v in obj.scale) for obj in meshes)
    forbidden_types = sorted({obj.type for obj in objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"}})

    required = []
    if "Submarine" in fbx_path.stem:
        required = ["PeriscopePivot"]
    elif "MineLayer" in fbx_path.stem:
        required = ["MineDropPoint_01", "MineDropPoint_02"]
    else:
        required = ["PrimaryGunPivot", "RadarPivot"]
    missing = [name for name in required if name not in empties]

    ok = bool(
        len(roots) == 1
        and meshes
        and root_scale_ok
        and root_rotation_ok
        and mesh_scale_ok
        and not forbidden_types
        and not missing
        and fbx_path.stat().st_size > 1024
    )

    results.append(
        {
            "file": fbx_path.name,
            "ok": ok,
            "bytes": fbx_path.stat().st_size,
            "root": roots[0].name if len(roots) == 1 else None,
            "root_count": len(roots),
            "mesh_count": len(meshes),
            "empty_count": len(empties),
            "root_scale_1": root_scale_ok,
            "root_rotation_0": root_rotation_ok,
            "mesh_scale_1": mesh_scale_ok,
            "forbidden_types": forbidden_types,
            "required_sockets": required,
            "missing_sockets": missing,
        }
    )

report = {
    "validated_count": len(results),
    "all_passed": bool(results) and all(item["ok"] for item in results),
    "results": results,
}
(output_dir / "validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))

if not report["all_passed"]:
    raise SystemExit(2)
