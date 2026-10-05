import bpy
import os
import json

OUT = r"C:\Users\최병욱\UnityProjects\NavalRoguelike\NavalRoguelike\ArtSource\TaskForceTacticalDraft"
folders = [os.path.join(OUT, "Models"), os.path.join(OUT, "Effects")]
report = {"passed": True, "files": []}

for folder in folders:
    for filename in sorted(os.listdir(folder)):
        if not filename.lower().endswith(".fbx"):
            continue
        bpy.ops.object.select_all(action='SELECT')
        bpy.ops.object.delete(use_global=False)
        path = os.path.join(folder, filename)
        bpy.ops.import_scene.fbx(filepath=path)
        objects = list(bpy.context.scene.objects)
        roots = [o for o in objects if o.parent is None]
        meshes = [o for o in objects if o.type == 'MESH']
        root_ok = len(roots) == 1
        scale_ok = root_ok and all(abs(v - 1.0) < 1e-4 for v in roots[0].scale)
        finite_size = os.path.getsize(path) > 1024
        passed = root_ok and scale_ok and len(meshes) > 0 and finite_size
        report["passed"] = report["passed"] and passed
        report["files"].append({
            "file": os.path.relpath(path, OUT),
            "passed": passed,
            "rootCount": len(roots),
            "root": roots[0].name if root_ok else None,
            "rootScale": list(roots[0].scale) if root_ok else None,
            "meshCount": len(meshes),
            "bytes": os.path.getsize(path),
        })

with open(os.path.join(OUT, "validation.json"), "w", encoding="utf-8") as f:
    json.dump(report, f, ensure_ascii=False, indent=2)
print(json.dumps({"passed": report["passed"], "count": len(report["files"])}, ensure_ascii=False))
