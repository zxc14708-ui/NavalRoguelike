"""Verify the exported D-4 and D-5 mesh bounds and FBX sockets in Blender."""
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector

OUT=Path(__file__).resolve().parent
sys.dont_write_bytecode=True
sys.path.insert(0,str(OUT.parent))
import validate as stage4

manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
stage4.ROOT=OUT
stage4.validate(manifest)

checks={'PRJ_LunBomb':('Fuze',.90,1),'PRJ_LunRocket':('Exhaust',-.80,-1)}
for name,(socket,point,axis) in checks.items():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(OUT/'Models'/f'{name}.fbx'))
    bpy.context.view_layer.update()
    marker=bpy.context.scene.objects[socket]
    assert marker.type=='EMPTY' and marker.parent.name==name
    assert (marker.matrix_world.translation-Vector((0,point,0))).length<.001
    assert marker.matrix_world.to_3x3().col[2].normalized().dot(Vector((0,axis,0)))>.999
with (OUT/'BlenderValidation.txt').open('a',encoding='utf-8') as report:
    report.write('PASS Fuze and Exhaust positions, axes and empty-node hierarchy.\n')
