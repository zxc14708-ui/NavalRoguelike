import bpy, sys, math, json
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent
(OUT/'Models').mkdir(parents=True,exist_ok=True)
sys.path.insert(0,'C:/Users/최병욱/Documents/Codex/2026-09-11/referenced-chatgpt-conversation-this-is-an')
import taskforce_revision_common as c
bpy.ops.wm.open_mainfile(filepath=str(OUT.parent/'EscortConcept_v14'/'EscortConcepts.blend'))
root=bpy.data.objects['ESC_PB_T0'];root.location=(0,0,0)
objects=list(c.a.descendants(root))+[root]
for ob in list(bpy.data.objects):
 if ob not in objects and ob.type not in ('CAMERA','LIGHT'):bpy.data.objects.remove(ob,do_unlink=True)
for ob in objects:ob.hide_render=False
c.setup();col=root.users_collection[0]
for ob in objects:
 name=ob.get('export_name',ob.name)
 if name.startswith(('RaftCradle','CapsuleLifeRaft','RaftRetainingStrap')):
  ob.location.y+=.29 if ob.location.y>-1.90 else -.29
 if name.startswith('SideMG_Pivot'):
  side=1 if ob.location.x>0 else -1
  ob.location.x=side*.61
  ob.rotation_euler.z=-side*math.pi/2
 if name.startswith('MGDeckFoot'):
  side=1 if ob.location.x>0 else -1
  ob.location.x=side*.61
for side in (-1,1):
 # Flush service footing gives the gunner a visibly clear inboard standing area.
 c.box('GunnerStandingPlate',(side*.43,-1.90,c.D+.012),(.58,.62,.024),root,col,c.DECK)
c.finish(root)
bpy.ops.object.select_all(action='DESELECT')
for ob in list(c.a.descendants(root))+[root]:ob.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/'ESC_PB_T0.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_NONE',bake_anim=False)
scene=bpy.context.scene;cam=scene.camera
for name,target,position,scale in [('Patrol_Fixed',(0,0,1),(16,23,24),11),('MG_Clearance',(0,-1.9,1),(8,7,9),4.6)]:
 cam.location=position;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
 scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Patrol_Fixed.blend'))
(OUT/'README.md').write_text('검토용 고속정 수정. 게임 미적용.\n구명뗏목 중심 Y -1.24 / -2.54, 기관총 중심 X ±0.61 / Y -1.90.\n기관총은 바깥쪽을 향하며, 뗏목 길이 0.60m 기준 사이 빈 공간 0.70m.\n',encoding='utf8')
