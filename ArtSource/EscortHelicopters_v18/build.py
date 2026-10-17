"""Review-only ASW helicopter progression and single-barrel escort side guns."""
import bpy, sys, math, json
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent
(OUT/'Models').mkdir(parents=True,exist_ok=True)
sys.path.insert(0,'C:/Users/최병욱/Documents/Codex/2026-09-11/referenced-chatgpt-conversation-this-is-an')
import environment_detail_common as c
import miniature_bridge_support as h
import taskforce_revision_common as ship
def children(r):return list(c.descendants(r))
def name(o):return o.get('export_name',o.name)
def export(r):
 c.finish(r);obs=children(r)+[r]
 # Give each exported asset canonical socket names despite the multi-model scene.
 saved={o:o.name for o in bpy.data.objects}
 for i,o in enumerate(bpy.data.objects):o.name='TMP_%05d'%i
 for o in obs:o.name=name(o)
 bpy.ops.object.select_all(action='DESELECT')
 for o in obs:o.select_set(True)
 bpy.context.view_layer.objects.active=r
 bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/(name(r)+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_NONE',bake_anim=False)
 for i,o in enumerate(bpy.data.objects):o.name='TMP_RESTORE_%05d'%i
 for o,n in saved.items():o.name=n

bpy.ops.wm.open_mainfile(filepath=str(OUT.parent/'EscortConcept_v17'/'EscortWeapons.blend'))
ship_roots=[bpy.data.objects['ESC_STK_T%d'%t] for t in (1,2,3)]
for r in ship_roots:
 r.location=(0,0,0)
 for o in children(r):
  n=name(o)
  if n.startswith('BroadsideGun_'):
   if n.endswith(('Barrel1','MuzzleCollar1','Muzzle_02')):bpy.data.objects.remove(o,do_unlink=True)
   elif n.endswith(('Barrel0','MuzzleCollar0','Muzzle')):o.location.x=0
 export(r)
scene=bpy.context.scene;cam=scene.camera
def visible(roots):
 for o in bpy.data.objects:
  if o.type=='MESH':o.hide_render=True
 for r in roots:
  for o in children(r):o.hide_render=False
def render(filename,target,position,scale):
 cam.location=position;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
 scene.render.filepath=str(OUT/(filename+'.png'));bpy.ops.render.render(write_still=True)
visible([ship_roots[2]]);render('STK_T3_SingleSideGuns',(0,-1.0,1),(12,15,17),10.5)
for o in list(bpy.data.objects):
 if o.type not in ('LIGHT','CAMERA'):bpy.data.objects.remove(o,do_unlink=True)
c.COLORS.update({'Naval Blue Grey':(.24,.32,.35,1),'Deck Grey':(.12,.17,.19,1),'Medical White':(.68,.73,.74,1),'Sensor Glass':(.055,.22,.27,1),'Role ASW':(.22,.68,1,1)})
c.setup();roots=[];report=[]
def torpedo(r,col,x,idx):
 c.box('TorpedoPylon',(x*.78,-.10,.32),(.24,.28,.07),r,col,'Naval Blue Grey')
 c.cyl('AirDroppedTorpedo',(x,-.10,.235),.065,.72,r,col,'Medical White',10,(math.pi/2,0,0))
 c.sphere('TorpedoNose',(x,.28,.235),(.065,.10,.065),r,col,'Gunmetal',10,5)
 c.cyl('TorpedoBand',(x,.14,.235),.068,.045,r,col,'Role ASW',10,(math.pi/2,0,0))
 c.box('TorpedoTail',(x,-.43,.235),(.20,.11,.018),r,col,'Deck Grey')
 c.empty('TorpedoDropPoint_%02d'%idx,r,col,(x,-.1,.16))
for tier in (1,2,3):
 col=c.collection('ASW Helicopter T%d'%tier);r=c.empty('HEL_EscortAsw_T%d'%tier,None,col)
 h.build_helicopter(r,col)
 # Consistent ASW blue identification, visibly different mission equipment.
 for side in (-1,1):
  c.box('ASWIdentificationStripe',(side*.371,-.21,.42),(.013,.32,.055),r,col,'Role ASW')
 c.sphere('ChinEOHousing',(0,.76,.34),(.085,.10,.075),r,col,'Medical White',10,5)
 c.box('ChinEOLens',(0,.848,.33),(.09,.014,.055),r,col,'Radar Glass')
 c.empty('SensorOrigin',r,col,(0,.85,.33))
 torpedo(r,col,-.44,1)
 if tier>=2:
  torpedo(r,col,.44,2)
  for o in children(r):
   if name(o).startswith(('Skid','UpperEngineCowling')):bpy.data.objects.remove(o,do_unlink=True)
  for side in (-1,1):
   c.frustum('TwinEngineCowling',(side*.17,-.20,.78),(.25,.67),(.20,.54),.20,r,col,'Naval Blue Grey')
   c.box('EngineIntake',(side*.17,.14,.81),(.16,.02,.095),r,col,'Gunmetal')
   c.cyl('EngineExhaust',(side*.17,-.57,.79),.061,.14,r,col,'Gunmetal',10,(math.pi/2,0,0))
   c.beam('MainGearStrut',(side*.28,-.32,.33),(side*.40,-.32,.09),.036,r,col,'Deck Grey')
   c.cyl('MainWheel',(side*.40,-.32,.075),.075,.055,r,col,'Gunmetal',10,(0,math.pi/2,0))
  c.beam('NoseGearStrut',(0,.61,.38),(0,.61,.07),.032,r,col,'Deck Grey')
  c.cyl('NoseWheel',(0,.61,.06),.06,.045,r,col,'Gunmetal',10,(0,math.pi/2,0))
  c.cyl('DippingSonarWell',(0,-.29,.255),.10,.10,r,col,'Gunmetal',12)
  c.empty('SonarCableOrigin',r,col,(0,-.29,.20))
 if tier==3:
  # Wide naval sponsons, prominent radar and five-blade rotor mark the dedicated ASW variant.
  for side in (-1,1):
   c.frustum('NavalSponson',(side*.35,-.39,.39),(.24,.66),(.19,.52),.20,r,col,'Naval Blue Grey')
   c.box('SponsonBluePanel',(side*.476,-.39,.41),(.014,.35,.06),r,col,'Role ASW')
   c.box('SonobuoyDispenser',(side*.27,-.61,.32),(.14,.13,.16),r,col,'Deck Grey')
   for i in range(3):c.cyl('SonobuoyExit',(side*.27,-.65+i*.04,.236),.015,.015,r,col,'Gunmetal',6)
  c.sphere('MaritimeSearchRadar',(0,.75,.27),(.20,.22,.105),r,col,'Medical White',12,6)
  c.box('RadarBlueMark',(0,.953,.275),(.12,.014,.04),r,col,'Role ASW')
  rotor=next(o for o in children(r) if name(o)=='Rotor')
  for o in list(rotor.children):
   if name(o).startswith('MainRotorBlade'):bpy.data.objects.remove(o,do_unlink=True)
  for i in range(5):h._rotor_blade('MainRotorBlade_%02d'%(i+1),rotor,col,i*math.tau/5)
  c.box('DorsalDatalink',(0,-.63,.80),(.12,.22,.16),r,col,'Medical White')
  c.empty('SonobuoyDropPoint',r,col,(.27,-.61,.22))
 export(r);roots.append(r)
 report.append({'asset':name(r),'tier':tier,'triangles':c.tris(r),'rotors':['Rotor','TailRotor'],'torpedoes':1 if tier==1 else 2})
visible(roots)
for i,r in enumerate(roots):r.location.x=(1-i)*3.4
render('Helicopter_Tiers',(0,-.4,.4),(9,12,11),10.6)
for i,r in enumerate(roots):
 visible([r]);r.location.x=0
 render('Helicopter_T%d'%(i+1),(0,-.4,.4),(5,7,4),3.8)
 r.location.x=(1-i)*3.4
visible(roots)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ASW_Helicopters.blend'))
(OUT/'manifest.json').write_text(json.dumps(report,indent=2))
