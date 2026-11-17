"""Review-only 76mm/twin gun and detailed ASW deck hardware revision."""
import bpy, math, sys, json
from pathlib import Path
from mathutils import Vector, Matrix
OUT=Path(__file__).resolve().parent
(OUT/'Models').mkdir(parents=True,exist_ok=True)
sys.path.insert(0,'C:/Users/최병욱/Documents/Codex/2026-09-11/referenced-chatgpt-conversation-this-is-an')
import taskforce_revision_common as c
bpy.ops.wm.open_mainfile(filepath=str(OUT.parent/'EscortConcept_v14'/'EscortConcepts.blend'))
c.setup()
def nm(o):return o.get('export_name',o.name)
def remove(root,prefixes):
 targets=[o for o in c.a.descendants(root) if nm(o).startswith(prefixes)]
 for o in targets:
  if o.name in bpy.data.objects:bpy.data.objects.remove(o,do_unlink=True)
def tube(parent,col,name,x,z):
 # Closed annular tube mesh: a dark bore is physically recessed behind its rim.
 n=12;verts=[]
 for y,r in [(-.42,.095),(.42,.095),(.42,.075),(-.40,.075)]:
  verts.extend((x+r*math.cos(i*math.tau/n),y,z+r*math.sin(i*math.tau/n)) for i in range(n))
 faces=[]
 for k in range(4):
  for i in range(n):j=(i+1)%n;faces.append((k*n+i,k*n+j,((k+1)%4)*n+j,((k+1)%4)*n+i))
 c.mesh_object(name,verts,faces,(0,0,0),parent,col,c.SUPER,(0,0,0))
 c.cyl(name+'Bore',(x,.32,z),.074,.016,parent,col,c.DARK,12,(math.pi/2,0,0))
 c.cyl(name+'RearClosure',(x,-.435,z),.104,.035,parent,col,c.DECK,12,(math.pi/2,0,0))
 c.box(name+'RearLatch',(x,-.459,z),(.105,.025,.04),parent,col,c.DARK)
 for y in (-.26,.24):
  c.cyl(name+'Clamp',(x,y,z),.105,.042,parent,col,c.DECK,12,(math.pi/2,0,0))
  c.box(name+'Saddle',(x,y,z-.085),(.19,.095,.12),parent,col,c.SUPER)
 c.box(name+'Hinge',(x+.086,.36,z+.045),(.04,.10,.045),parent,col,c.DECK)
 return (x,.435,z)
roots=[];report=[]
for role in ('STK','ASW'):
 for tier in (1,2,3):
  root=bpy.data.objects[f'ESC_{role}_T{tier}'];root.location=(0,0,0);col=root.users_collection[0]
  if role=='STK':
   remove(root,('PrimaryGun','AftGun'))
   d=c.D+(.18 if tier==3 else 0)
   gun=c.gun(root,col,2.76,d,scale=(.97,1.12,1.27)[tier-1],prefix='PrimaryGun',twin=False,stealth=True)
   # 76mm enclosed single-barrel turret: broad faceted casing, breech and service hatch.
   s=(.97,1.12,1.27)[tier-1]
   c.box('Gun76ServiceHatch',(0,-.36*s,.20*s),(.32*s,.025,.21*s),gun,col,c.DECK)
   c.box('Gun76HatchHandle',(.10*s,-.38*s,.20*s),(.05,.025,.08),gun,col,c.DARK)
   c.box('Gun76RoofPanel',(0,-.04*s,.443*s),(.30*s,.30*s,.012),gun,col,c.DECK)
   c.gun(root,col,-3.38,d,scale=(.78,.98,1.19)[tier-1],prefix='AftGun',twin=True,heading=math.pi,stealth=True)
  else:
   remove(root,('Torpedo','LightTorpedo','DepthLauncher','DepthMortar','DepthLaunch','Sonobuoy'))
   for side,label in [(-1,'P'),(1,'S')]:
    x=side*.96;y=-.75
    c.box('TorpedoDeckFoundation_'+label,(x,y,c.D+.045),(.53,.72,.09),root,col,c.DECK)
    c.cyl('TorpedoSlewBearing_'+label,(x,y,c.D+.13),.23,.12,root,col,c.DARK,12)
    p=c.empty('TorpedoMount_'+label,root,col,(x,y,c.D+.20));p.rotation_euler.z=-side*math.pi/2
    c.box('TorpedoRotatingCradle',(0,0,.07),(.59,.58,.14),p,col,c.SUPER)
    count=2 if tier==1 else 3
    for i in range(count):
     pos=tube(p,col,'TorpedoTube_'+label+str(i+1),(i-(count-1)/2)*.205,.22)
     c.empty('TorpedoLaunchPoint_'+label+'_%02d'%(i+1),p,col,pos)
    c.box('LauncherControlBox',(0,-.34,.075),(.35,.13,.13),p,col,c.DECK)
    c.box('LauncherRolePlate',(0,-.415,.075),(.24,.015,.055),p,col,c.role_mat('ASW'))
    # Distinct low depth-charge throwing cradle, with restrained blue end cap.
    rack=c.empty('DepthChargeStation_'+label,root,col,(side*1.05,-1.57,c.D))
    c.box('DepthChargeFoot',(0,0,.05),(.40,.47,.10),rack,col,c.DECK)
    for yy in (-.16,.16):c.frustum('DepthChargeSupport',(0,yy,.17),(.34,.06),(.22,.06),.24,rack,col,c.SUPER)
    c.cyl('DepthChargeDrum',(0,0,.29),.13,.36,rack,col,c.SUPER,12,(math.pi/2,0,0))
    c.cyl('DepthChargeEnd',(0,.187,.29),.115,.025,rack,col,c.role_mat('ASW'),12,(math.pi/2,0,0))
    c.beam('DepthChargeThrowArm',(0,0,.10),(side*.21,0,.32),.045,rack,col,c.DARK)
    c.empty('DepthLaunchPoint_'+label,rack,col,(side*.23,0,.34))
   if tier==3:
    for side in (-1,1):
     for i in range(4):
      c.box('AcousticCabinetVent',(side*1.121,.02,c.D+.16+i*.06),(.012,.26,.018),root,col,c.DARK)
     c.box('AcousticCabinetDoor',(side*1.122,.02,c.D+.43),(.012,.33,.14),root,col,c.DECK)
    p=next(o for o in c.a.descendants(root) if nm(o)=='ASWRocketPivot');p.location.y=2.18
    for ob in c.a.descendants(p):
     if ob.type=='MESH' and nm(ob).startswith(('ASWMortarTube','ASWMortarMouth')):ob.data.transform(Matrix.Rotation(.70,4,'X'))
    for ob in c.a.descendants(root):
     if ob.parent==root and nm(ob).startswith('PrimaryGun'):ob.location.y+=.63
    for row in range(2):
     for j in range(3):
      t=c.empty('ASWLaunchPoint_%02d'%(row*3+j+1),p,col,((j-1)*.19,.276,.48+row*.18))
      t.rotation_euler=Vector((0,math.cos(.35),math.sin(.35))).to_track_quat('Y','Z').to_euler()
  c.finish(root)
  obs=list(c.a.descendants(root))+[root]
  bpy.ops.object.select_all(action='DESELECT')
  for o in obs:o.select_set(True)
  bpy.context.view_layer.objects.active=root
  bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/(root.name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_NONE',bake_anim=False)
  report.append({'name':root.name,'triangles':sum(len(o.data.polygons) for o in obs if o.type=='MESH')})
  roots.append((role,tier,root))
scene=bpy.context.scene;cam=scene.camera
def show(role,tier=None):
 for ob in bpy.data.objects:
  if ob.type=='MESH':ob.hide_render=True
 for rr,t,r in roots:
  if rr==role and (tier is None or tier==t):
   for ob in c.a.descendants(r):ob.hide_render=False
   r.location=((2-t)*5.2 if tier is None else 0,0,0)
def render(name,target,loc,scale):
 cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
 scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
for role in ('STK','ASW'):
 show(role);render(role+'_Tiers',(0,0,1),(16,23,24),19)
 show(role,3);render(role+'_Detail',(0,-.5,1),(10,13,12),10)
show('ASW',3);render('Torpedo_Close',(.9,-.75,1),(9,-8,9),3.6)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'EscortWeapons.blend'))
(OUT/'manifest.json').write_text(json.dumps(report,indent=2))
