"""Review-only enemy models. Uses the established miniature fleet geometry library."""
import sys, math, json
from pathlib import Path
import bpy
from mathutils import Vector
LIB=Path('C:/Users/최병욱/Documents/Codex/2026-09-11/referenced-chatgpt-conversation-this-is-an')
sys.path.insert(0,str(LIB))
import environment_detail_common as c
import miniature_enemy_ships as e
OUT=Path(__file__).resolve().parent
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
c.COLORS.update({'Enemy hull grey':(.23,.29,.32,1),'Stealth deck':(.10,.15,.17,1),
 'Naval Blue Grey':(.36,.43,.46,1),'Deck Grey':(.29,.34,.35,1),
 'Enemy weapon dark':(.075,.095,.105,1),'Missile body':(.34,.38,.38,1),
 'Warning red':(.58,.075,.06,1),'Medical White':(.72,.75,.73,1)})
c.setup()
entries=[]
for name,builder,x in [('EnemyMissileBoat',e.build_missile_boat,-3.3),('EnemyPccCorvette',e.build_corvette,3.3)]:
 col=c.collection(name);root=c.empty(name,None,col);meta=builder(root,col)
 # Functional identification panels, visible from the normal overhead game camera.
 for side in (-1,1):
  c.box('EnemyRecognitionPanel',(side*(.60 if name=='EnemyMissileBoat' else .98),.3,1.58 if name=='EnemyMissileBoat' else 2.20),
        (.022,.65,.15),root,col,'Warning red')
 c.finish(root)
 objs=list(c.descendants(root))+[root]
 for ob in objs: ob.name=ob.get('export_name',ob.name)
 bpy.ops.object.select_all(action='DESELECT')
 for ob in objs: ob.select_set(True)
 bpy.context.view_layer.objects.active=root
 bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},
  axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_NONE',bake_anim=False)
 points=[ob.matrix_world@v.co for ob in objs if ob.type=='MESH' for v in ob.data.vertices]
 entries.append({'model':name,'triangles':c.tris(root),'bounds':[[min(p[i] for p in points),max(p[i] for p in points)] for i in range(3)],'sockets':meta.get('sockets',[])})
 root.location.x=x
scene=bpy.context.scene
scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True
scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.17,.22,.26,1)
scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.5
for pos,power,size in [((-8,3,13),2100,9),((8,-5,10),1800,8)]:
 data=bpy.data.lights.new('StudioSoftbox','AREA');data.energy=power;data.shape='DISK';data.size=size
 ob=bpy.data.objects.new('StudioSoftbox',data);scene.collection.objects.link(ob);ob.location=pos
 ob.rotation_euler=(Vector((0,0,0))-ob.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('ReviewCamera');cam=bpy.data.objects.new('ReviewCamera',data);scene.collection.objects.link(cam)
cam.location=(18,23,22);cam.rotation_euler=(Vector((0,0,1))-cam.location).to_track_quat('-Z','Y').to_euler()
data.type='ORTHO';data.ortho_scale=20;scene.camera=cam
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.view_settings.look='AgX - Medium High Contrast'
scene.render.filepath=str(OUT/'Preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'EnemyRefresh.blend'))
bpy.ops.render.render(write_still=True)
(OUT/'model_report.json').write_text(json.dumps(entries,indent=2),encoding='utf8')
