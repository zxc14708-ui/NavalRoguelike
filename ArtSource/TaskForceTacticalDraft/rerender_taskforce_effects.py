import bpy
import os
from mathutils import Vector

OUT = r"C:\Users\최병욱\UnityProjects\NavalRoguelike\NavalRoguelike\ArtSource\TaskForceTacticalDraft"
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.resolution_percentage = 75
scene.display.shading.show_shadows = False

for col in bpy.data.collections:
    if col.name.startswith("Preview"):
        col.hide_render = col.name != "PreviewEffects"

target = bpy.data.collections.get("PreviewEffects")
if target is None:
    raise RuntimeError("PreviewEffects collection is missing")

camera = next((obj for obj in target.objects if obj.type == 'CAMERA'), None)
if camera is None:
    raise RuntimeError("PreviewEffects camera is missing")
scene.camera = camera
scene.render.resolution_x = 2048
scene.render.resolution_y = 1400
scene.render.filepath = os.path.join(OUT, "Preview", "TaskForce_SkillFX_All.png")
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "TaskForceTacticalAssets.blend"))
print(scene.render.filepath)
