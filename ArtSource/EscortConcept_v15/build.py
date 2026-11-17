"""ASW T3 clearance correction and matching review-only rocket projectile."""
import bpy, sys, math, json
from pathlib import Path
from mathutils import Vector, Matrix
OUT=Path(__file__).resolve().parent
(OUT/'Models').mkdir(parents=True,exist_ok=True)
sys.path.insert(0,'C:/Users/최병욱/Documents/Codex/2026-09-11/referenced-chatgpt-conversation-this-is-an')
import taskforce_revision_common as c
bpy.ops.wm.open_mainfile(filepath=str(OUT.parent/'EscortConcept_v14'/'EscortConcepts.blend'))
root=bpy.data.objects['ESC_ASW_T3'];root.location=(0,0,0)
objects=list(c.a.descendants(root))+[root]
for ob in list(bpy.data.objects):
 if ob not in objects and ob.type not in ('CAMERA','LIGHT'):bpy.data.objects.remove(ob,do_unlink=True)
for ob in objects:ob.hide_render=False
c.setup();col=root.users_collection[0]
p=next(o for o in objects if o.get('export_name',o.name)=='ASWRocketPivot')
p.location.y=2.18
for ob in c.a.descendants(p):
 if ob.type=='MESH' and ob.get('export_name',ob.name).startswith(('ASWMortarTube','ASWMortarMouth')):
  # Positive-Y tube mouths must rise with the launch vector.
  ob.data.transform(Matrix.Rotation(.70,4,'X'))
for ob in objects:
 if ob.parent==root and ob.get('export_name',ob.name).startswith('PrimaryGun'):ob.location.y+=.63
direction=Vector((0,math.cos(.35),math.sin(.35)))
for row in range(2):
 for j in range(3):
  muzzle=c.empty('ASWLaunchPoint_%02d'%(row*3+j+1),p,col,((j-1)*.19,.276,.38+row*.18+.100))
  muzzle.rotation_euler=direction.to_track_quat('Y','Z').to_euler()
bpy.context.view_layer.update()
def bounds(items):
 pts=[o.matrix_world@v.co for o in items if o.type=='MESH' for v in o.data.vertices]
 return [[min(v[i] for v in pts),max(v[i] for v in pts)] for i in range(3)]
launcher=bounds(c.a.descendants(p))
bridge=bounds([o for o in objects if 'Bridge' in o.get('export_name',o.name) or 'House' in o.get('export_name',o.name)])
guns=bounds([o for o in c.a.descendants(root) if o.get('export_name',o.name).startswith('PrimaryGun')])
assert launcher[1][0]>1.61+.20
assert guns[1][0]-launcher[1][1]>.30
def export(r,name):
 c.finish(r);obs=list(c.a.descendants(r))+[r]
 bpy.ops.object.select_all(action='DESELECT')
 for o in obs:o.select_set(True)
 bpy.context.view_layer.objects.active=r
 bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/(name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_NONE',bake_anim=False)
export(root,'ESC_ASW_T3')
scene=bpy.context.scene;cam=scene.camera
def render(name,target,position,scale):
 cam.location=position;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
 scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
render('ASW_T3_Fixed',(0,0,1),(16,23,24),11)
render('Bow_Clearance',(0,2.0,1),(8,10,7),4.6)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ESC_ASW_T3.blend'))
for o in list(c.a.descendants(root)):o.hide_render=True
rc=bpy.data.collections.new('ASWRocket');scene.collection.children.link(rc)
r=c.empty('PRJ_EscortAswRocket',None,rc)
# Compact rocket-assisted depth charge: a rounded heavy nose and short motor.
profile=[(-.22,.030),(-.20,.043),(-.08,.049),(.09,.049),(.16,.040),(.20,.023),(.22,.004)]
verts=[(rad*math.cos(i*math.tau/12),y,rad*math.sin(i*math.tau/12)) for y,rad in profile for i in range(12)]
faces=[tuple(reversed(range(12))),tuple(range(72,84))]
for k in range(len(profile)-1):
 for i in range(12):j=(i+1)%12;faces.append((k*12+i,k*12+j,(k+1)*12+j,(k+1)*12+i))
c.mesh_object('RocketBody',verts,faces,(0,0,0),r,rc,c.SUPER,(0,0,0))
c.cyl('ASWIdentificationBand',(0,.055,0),.050,.040,r,rc,c.role_mat('ASW'),12,(math.pi/2,0,0))
c.cyl('MotorBand',(0,-.13,0),.050,.022,r,rc,c.DARK,12,(math.pi/2,0,0))
c.cyl('ExhaustNozzle',(0,-.224,0),.026,.008,r,rc,c.DARK,12,(math.pi/2,0,0))
for i in range(4):
 angle=i*math.pi/2
 c.box('FoldedTailFin',(math.cos(angle)*.048,-.165,math.sin(angle)*.048),(.017,.085,.012),r,rc,c.DECK,(0,-angle,0))
c.empty('Exhaust',r,rc,(0,-.230,0));c.empty('ImpactPoint',r,rc,(0,.22,0))
export(r,'PRJ_EscortAswRocket')
render('ASW_Rocket',(0,0,0),(.8,1.0,.7),.68)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ASW_Rocket.blend'))
report={'launcher_y_bounds':launcher[1],'bridge_front_y':1.61,'bridge_clearance':launcher[1][0]-1.61,
 'gun_clearance':guns[1][0]-launcher[1][1],'projectile_length_m':.45,'projectile_body_diameter_m':.10,
 'launch_sockets':6,'projectile_sockets':['Exhaust','ImpactPoint'],'game_applied':False}
(OUT/'validation.json').write_text(json.dumps(report,indent=2))
