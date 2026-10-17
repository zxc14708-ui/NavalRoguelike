import bpy, math, sys, json
from pathlib import Path
from mathutils import Vector
LIB=Path('C:/Users/최병욱/Documents/Codex/2026-09-11/referenced-chatgpt-conversation-this-is-an')
sys.path.insert(0,str(LIB))
import taskforce_revision_common as c
import taskforce_revision_stk as stk
import taskforce_revision_asw_ew as asw
OUT=Path(__file__).resolve().parent
(OUT/'Models').mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);c.setup()
report=[]

def mg(root,col,x,y,role,heavy=False):
 p=c.empty('SideMG_Pivot',root,col,(x,y,c.D+.38))
 c.cyl('MGDeckFoot',(x,y,c.D+.045),.13,.09,root,col,c.DECK,8)
 c.cyl('MGPedestal',(0,0,-.18),.045,.36,p,col,c.SUPER,8)
 c.box('MGShield',(0,.12,.10),(.34,.05,.31),p,col,c.SUPER)
 c.box('MGReceiver',(0,0,.10),(.09,.27,.10),p,col,c.DARK)
 c.cyl('MGBarrel',(0,.33,.10),.016,.48,p,col,c.DARK,6,(math.pi/2,0,0))
 c.box('MGAmmoBox',(.13,-.04,.08),(.16,.20,.15),p,col,c.DECK)
 c.empty('SideMG_Muzzle',p,col,(0,.58,.10))

def rafts(root,col):
 for side in (-1,1):
  for y in (-1.53,-2.25):
   x=side*.98
   c.box('RaftCradle',(x,y,c.D+.07),(.38,.61,.14),root,col,c.DECK)
   c.sphere('CapsuleLifeRaft',(x,y,c.D+.26),(.17,.30,.16),root,col,c.WHITE,10,6)
   for yy in (-.13,.13):
    c.box('RaftRetainingStrap',(x,y+yy,c.D+.27),(.36,.035,.30),root,col,c.SUPER)
  mg(root,col,side*.98,-1.90,'STK')

def depth(root,col,tier):
 for side in (-1,1):
  p=c.empty('DepthLauncher_Pivot',root,col,(side*1.10,-1.03,c.D+.16))
  c.box('DepthLauncherBase',(0,0,-.09),(.36,.50,.18),p,col,c.DECK)
  for i in range(1 if tier==1 else 2):
   c.cyl('DepthMortarTube',(0,(i-.5)*.24,.16),.075,.45,p,col,c.SUPER,8,(0,side*.9,0))
   c.empty('DepthLaunchPoint',p,col,(side*.18,(i-.5)*.24,.32))

roots=[]
for role,tiers in [('PB',[0]),('STK',[1,2,3]),('ASW',[1,2,3])]:
 for tier in tiers:
  name=f'ESC_{role}_T{tier}';col=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(col);root=c.empty(name,None,col)
  if role=='PB':
   roof,d=c.base(root,col,'STK',0,bridge_y=.52,bridge_length=1.92,bridge_height=.91,width=2.78)
   c.funnel(root,col,y=-.77,height=.68,width=.65,length=.58);stk.lattice_mast(root,col,roof,0)
   c.gun(root,col,2.76,d,scale=.87,twin=True,stealth=True)
   c.gun(root,col,-3.38,d,scale=.63,prefix='AftGun',twin=True,heading=math.pi,stealth=True)
   rafts(root,col)
  elif role=='STK':
   roof,d=c.base(root,col,'STK',tier,bridge_y=.52,bridge_length=1.92,bridge_height=.99,width=2.78)
   c.funnel(root,col,y=-.82,height=.72,width=.62,length=.59);stk.lattice_mast(root,col,roof,tier)
   stk.fire_control(root,col,1.0,roof,tier)
   c.gun(root,col,2.76,d,scale=.9+tier*.10,stealth=True)
   c.gun(root,col,-3.38,d,scale=.65+tier*.10,prefix='AftGun',twin=tier>=2,heading=math.pi,stealth=True)
   for side in (-1,1):stk.missile_bank(root,col,side,y=-1.65,count=1 if tier==1 else 2)
   if tier>=2:
    for side in (-1,1):mg(root,col,side*1.02,-2.65,'STK')
   if tier==3:
    c.ciws(root,col,-.68,deck=d+.70,scale=.55,prefix='MidDeckCIWS',heading=math.pi)
    c.box('CIWSRaisedServicePlatform',(0,-.68,d+.34),(.85,.75,.68),root,col,c.SUPER)
    for side in (-1,1):
     c.box('ReadyAmmoLocker',(side*.78,-3.17,d+.17),(.33,.35,.34),root,col,c.SUPER)
  else:
   roof,d=c.base(root,col,'ASW',tier,bridge_y=.77,bridge_length=1.68,bridge_height=.88)
   c.gun(root,col,2.69,d,scale=.60);c.nav_mast(root,col,1.01,roof,height=.80)
   c.funnel(root,col,y=-.62,height=.64,width=.61,length=.62)
   asw._helipad(root,col,tier)
   if tier>=2:asw._asw_shed(root,col,tier)
   asw._torpedo_banks(root,col,tier);depth(root,col,tier)
   if tier>=2:asw._sonobuoys(root,col,tier);asw._tow_sonar(root,col,tier)
   if tier==3:
    for side in (-1,1):
     c.box('AcousticProcessingCabinet',(side*.94,.02,d+.28),(.35,.46,.56),root,col,c.SUPER)
     c.box('ASWBlueServicePanel',(side*.94,.26,d+.33),(.28,.018,.30),root,col,c.role_mat('ASW'))
    c.dome(root,col,'SonarDatalink',0,-1.70,d+1.06,.23,c.WHITE)
   c.empty('SonarOrigin',root,col,(0,3.3,-.22))
  c.finish(root)
  objs=list(c.a.descendants(root))+[root]
  for ob in objs:ob.name=ob.get('export_name',ob.name)
  bpy.ops.object.select_all(action='DESELECT')
  for ob in objs:ob.select_set(True)
  bpy.context.view_layer.objects.active=root
  bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/(name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_NONE',bake_anim=False)
  report.append({'model':name,'triangles':sum(len(o.data.polygons) for o in objs if o.type=='MESH'),'hangar':role=='ASW' and tier>=2})
  roots.append((role,tier,root))
scene=bpy.context.scene;scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True
scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.14,.19,.22,1)
scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.6
for loc in [(-7,0,15),(9,-3,10)]:
 data=bpy.data.lights.new('Softbox','AREA');data.energy=2200;data.size=10
 ob=bpy.data.objects.new('Softbox',data);scene.collection.objects.link(ob);ob.location=loc
 ob.rotation_euler=(-ob.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Camera');cam=bpy.data.objects.new('Camera',data);scene.collection.objects.link(cam);scene.camera=cam
cam.location=(16,23,24);cam.rotation_euler=(Vector((0,0,1))-cam.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO'
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.view_settings.look='AgX - Medium High Contrast'
for role in ['PB','STK','ASW']:
 chosen=[r for rr,t,r in roots if rr==role]
 for rr,t,r in roots:
  for ob in list(c.a.descendants(r))+[r]:ob.hide_render=rr!=role
 for i,r in enumerate(chosen):r.location.x=(i-(len(chosen)-1)/2)*4.6
 data.ortho_scale=17 if len(chosen)>1 else 11
 scene.render.filepath=str(OUT/(role+'_Preview.png'));bpy.ops.render.render(write_still=True)
for rr,t,r in roots:
 for ob in list(c.a.descendants(r))+[r]:ob.hide_render=False
 r.location=((t-1)*4.6,{'PB':12,'STK':0,'ASW':-12}[rr],0)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'EscortConcepts.blend'))
(OUT/'manifest.json').write_text(json.dumps(report,indent=2))
