"""Angular, densely assembled 40mm twin naval gun; review-only FBX assets."""
import bpy,sys,math,json
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent
(OUT/'Models').mkdir(parents=True,exist_ok=True)
sys.path.insert(0,'C:/Users/최병욱/Documents/Codex/2026-09-11/referenced-chatgpt-conversation-this-is-an')
import environment_detail_common as c
import miniature_enemy_ships as e
bpy.ops.wm.read_factory_settings(use_empty=True)
c.COLORS.update({'Naval Blue Grey':(.30,.37,.40,1),'Deck Grey':(.17,.22,.24,1),'Enemy weapon dark':(.055,.073,.08,1),'Medical White':(.64,.70,.70,1),'Warning red':(.5,.08,.05,1)})
c.setup();roots=[];report=[]
for level in range(3):
 ident='MOD_Nobong'+('' if level==0 else '_U'+str(level))
 col=c.collection(ident);r=c.empty(ident,None,col)
 e._chamfer_box('DeckAdapter',(0,0,.09),(1.7,1.7,.18),r,col,'Deck Grey',.16)
 c.cyl('TraverseRing',(0,0,.25),.54,.14,r,col,'Gunmetal',16)
 c.cyl('RingShoulder',(0,0,.335),.51,.045,r,col,'Naval Blue Grey',16)
 for x in (-.65,.65):
  for y in (-.65,.65):c.cyl('AnchorBolt',(x,y,.195),.045,.035,r,col,'Gunmetal',6)
 yaw=c.empty('TurretPivot',r,col,(0,0,.36))
 e._chamfer_box('TurretDriveHousing',(0,-.13,.15),(1.03,.97,.30),yaw,col,'Naval Blue Grey',.12)
 # Thick chamfered cheeks carry the trunnions and conceal the former empty cavity.
 for side in (-1,1):
  c.frustum('AngularShieldCheek',(side*.49,-.08,.39),(.24,.93),(.20,.76),.58,yaw,col,'Naval Blue Grey')
  c.cyl('TrunnionCover',(side*.62,.12,.43),.15,.055,yaw,col,'Deck Grey',10,(0,math.pi/2,0))
  e._chamfer_box('AmmunitionFeedBox',(side*(.64 if level==0 else .67),-.30,.24),(.23+level*.045,.56+level*.045,.40),yaw,col,'Naval Blue Grey',.045)
  c.box('AmmoBoxLid',(side*.66,-.30,.46),(.26,.55,.035),yaw,col,'Deck Grey')
  c.box('AmmoBoxLatch',(side*.66,.002,.27),(.10,.027,.11),yaw,col,'Gunmetal')
  c.box('FeedChute',(side*.37,-.17,.41),(.21,.25,.18),yaw,col,'Gunmetal')
 c.box('RearMechanismCover',(0,-.57,.31),(.77,.07,.37),yaw,col,'Deck Grey')
 for x in (-.24,-.12,0,.12,.24):c.box('RearVent',(x,-.609,.34),(.055,.012,.12),yaw,col,'Gunmetal')
 pitch=c.empty('ElevationPivot',yaw,col,(0,.12,.43))
 barrel=c.empty('Barrel',pitch,col)
 e._chamfer_box('TwinBreechBlock',(0,-.16,0),(.66,.61,.29),barrel,col,'Gunmetal',.07)
 c.frustum('FacetedMantlet',(0,.15,.025),(.76,.36),(.63,.30),.35,barrel,col,'Naval Blue Grey')
 for index,x in enumerate((-.14,.14)):
  c.cyl('BarrelJacket',(x,.43,0),.083,.48,barrel,col,'Deck Grey',12,(math.pi/2,0,0))
  c.cyl('GunTube',(x,.91,0),.041,1.10,barrel,col,'Gunmetal',12,(math.pi/2,0,0))
  c.cyl('MuzzleCollar',(x,1.46,0),.066,.13,barrel,col,'Deck Grey',10,(math.pi/2,0,0))
  c.cyl('Bore',(x,1.528,0),.039,.008,barrel,col,'Gunmetal',12,(math.pi/2,0,0))
  c.cyl('RecoilHousing',(x,.11,.16),.047,.49,barrel,col,'Deck Grey',10,(math.pi/2,0,0))
  if level>0:
   for yy in (.40,.52,.64):c.cyl('CoolingBand',(x,yy,0),.088,.035,barrel,col,'Naval Blue Grey',12,(math.pi/2,0,0))
  c.empty('Muzzle_%02d'%(index+1),pitch,col,(x,1.536,0))
 c.empty('Muzzle',pitch,col,(0,1.536,0))
 e._chamfer_box('OpticalSight',(.47,.12,.75),(.23,.29,.20),yaw,col,'Naval Blue Grey',.025)
 c.box('SightLens',(.47,.271,.75),(.14,.02,.10),yaw,col,'Radar Glass')
 if level==2:
  c.box('RearControlPack',(0,-.62,.58),(.56,.20,.25),yaw,col,'Naval Blue Grey')
  c.box('ServiceHatch',(0,-.731,.58),(.40,.015,.15),yaw,col,'Deck Grey')
  c.box('HatchHandle',(.12,-.75,.58),(.07,.02,.055),yaw,col,'Gunmetal')
  c.box('EOAuxiliaryLens',(.47,.279,.66),(.10,.024,.04),yaw,col,'Sensor Glass' if 'Sensor Glass' in c.M else 'Radar Glass')
 c.finish(r)
 # Export canonical names before staging the model for the comparison image.
 saved={o:o.name for o in bpy.data.objects}
 for i,o in enumerate(bpy.data.objects):o.name='TMP_%05d'%i
 objs=list(c.descendants(r))+[r]
 for o in objs:o.name=o['export_name']
 bpy.ops.object.select_all(action='DESELECT')
 for o in objs:o.select_set(True)
 bpy.context.view_layer.objects.active=r
 bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/(ident+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_NONE',bake_anim=False)
 for i,o in enumerate(bpy.data.objects):o.name='RESTORE_%05d'%i
 for o,n in saved.items():o.name=n
 report.append({'model':ident,'tris':c.tris(r),'footprint':[1.7,1.7],'sockets':['TurretPivot','ElevationPivot','Barrel','Muzzle','Muzzle_01','Muzzle_02']})
 roots.append(r)
scene=bpy.context.scene;scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True
scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.14,.19,.22,1);scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.6
for loc in [(-5,2,9),(6,-3,7)]:
 d=bpy.data.lights.new('Softbox','AREA');d.energy=1100;d.size=7
 ob=bpy.data.objects.new('Softbox',d);scene.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=(-ob.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new('Camera');cam=bpy.data.objects.new('Camera',d);scene.collection.objects.link(cam);scene.camera=cam;d.type='ORTHO'
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.view_settings.look='AgX - Medium High Contrast'
def render(n,scale,target=(0,.3,.5),pos=(7,10,8)):
 cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();d.ortho_scale=scale
 scene.render.filepath=str(OUT/(n+'.png'));bpy.ops.render.render(write_still=True)
for i,r in enumerate(roots):r.location.x=(1-i)*2.7
render('Nobong_Tiers',8.3)
for i,r in enumerate(roots):
 for rr in roots:
  for o in c.descendants(rr):o.hide_render=rr!=r
 r.location.x=0;render('Nobong_T%d'%i,3.4);r.location.x=(1-i)*2.7
for r in roots:
 for o in c.descendants(r):o.hide_render=False
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'NobongAngular.blend'))
(OUT/'manifest.json').write_text(json.dumps(report,indent=2))
