"""Remaining CODEX_MODEL_REQUESTS items. Review-only exports, no Unity changes."""
import sys,json,math
from pathlib import Path
from collections import defaultdict
import bpy
from mathutils import Vector,Matrix
OUT=Path(__file__).resolve().parent
sys.path.insert(0,str(OUT.parent/'WWII_ModePack_v29'))
import common as cm
from common import *
from ships import single_light,gunboat
from aircraft import aircraft
OUT=Path(__file__).resolve().parent;cm.OUT=OUT
setup();(OUT/'Models').mkdir(exist_ok=True);(OUT/'Preview').mkdir(exist_ok=True)
assets=[];report=[]

def consolidate(r):
 """One mesh for each static or independently articulated group; preserve sockets."""
 c.finish(r);bpy.context.view_layer.update();groups=defaultdict(list)
 for o in c.descendants(r):
  if o.type=='MESH':groups[o.parent].append(o)
 for parent,obs in groups.items():
  inv=parent.matrix_world.inverted();verts=[];faces=[];materials=[];indices=[]
  for ob in obs:
   offset=len(verts);mat=inv@ob.matrix_world
   verts.extend(tuple(mat@v.co) for v in ob.data.vertices)
   for f in ob.data.polygons:
    faces.append(tuple(offset+i for i in f.vertices))
    material=ob.data.materials[f.material_index]
    if material not in materials:materials.append(material)
    indices.append(materials.index(material))
  col=parent.users_collection[0];n=parent.get('export_name',parent.name)+'_Visual'
  mesh=bpy.data.meshes.new(n);mesh.from_pydata(verts,[],faces);mesh.update()
  for m in materials:mesh.materials.append(m)
  for f,idx in zip(mesh.polygons,indices):f.material_index=idx;f.use_smooth=False
  ob=bpy.data.objects.new(n,mesh);col.objects.link(ob);ob.parent=parent;ob['export_name']=n
  for old in obs:bpy.data.objects.remove(old,do_unlink=True)

def add(r,kind,notes=None):
 consolidate(r);export(r);lo,hi=bounds(r)
 report.append({'asset':r['export_name'],'kind':kind,'bounds':[lo,hi],'triangles':c.tris(r),
  'meshes':sum(o.type=='MESH' for o in c.descendants(r)),
  'materials':sorted({m.name for o in c.descendants(r) if o.type=='MESH' for m in o.data.materials}),
  'sockets':[o['export_name'] for o in c.descendants(r) if o.type=='EMPTY'],'notes':notes})
 assets.append(r);return r

def torpedo_boat():
 r,col=root('ENE_Ww2TorpedoBoat');p=c.empty('ModelPivot',r,col)
 hull('HardChineTorpedoHull',6.8,1.92,.46,p,col,'WW2 Enemy',-.35)
 clipped('LowCabin',(0,-.63,.71),(.86,1.55,.50),p,col,'WW2 Enemy',.07)
 clipped('Wheelhouse',(0,-.30,1.06),(.83,.86,.25),p,col,'WW2 Enemy',.06)
 for x in (-.24,.24):box('FrontWindshield',(x,.138,1.08),(.30,.015,.135),p,col,'WW2 Glass')
 for side in (-1,1):
  box('SideWindow',(side*.424,-.30,1.08),(.012,.40,.13),p,col,'WW2 Glass')
  # Large fixed tubes can be identified from the battle camera; fixed +Y launch.
  x=side*.665
  tube('FixedTorpedoTube',(x,-.08,.685),.135,2.54,p,col,'WW2 Hull')
  bore('TorpedoTubeMouth',x,1.255,.685,.149,p,col)
  tube('RearTubeBreech',(x,-1.37,.685),.151,.065,p,col,'WW2 Gun')
  for yy in (-.94,.64):
   tube('TubeStrap',(x,yy,.685),.15,.055,p,col,'WW2 Gun')
   box('TubeSaddle',(x,yy,.535),(.25,.15,.15),p,col,'WW2 Gun')
  box('FiringValve',(x,-1.18,.84),(.09,.12,.08),p,col,'WW2 Dark')
  c.empty('LaunchPoint_%02d'%(1 if side<0 else 2),p,col,(x,1.27,.685))
  c.empty('TorpedoLaunchPoint_%02d'%(1 if side<0 else 2),p,col,(x,1.27,.685))
  # Side recognition and practical aft engine access, no extra unpowered weapons.
  box('EnemySideBand',(side*.966,-.33,.23),(.012,1.12,.10),p,col,'WW2 Enemy Red')
  clipped('EngineHatch',(side*.34,-2.09,.51),(.53,.83,.09),p,col,'WW2 Gun',.035)
  for yy in (-2.35,-2.23,-2.11,-1.99):box('EngineVent',(side*.34,yy,.56),(.37,.035,.015),p,col,'WW2 Dark')
  rail((side*.82,-2.78),(side*.87,-1.62),.46,p,col)
  for yy in (-2.94,2.04):cyl('Bollard',(side*.60,yy,.515),.035,.11,p,col,'WW2 Dark',8)
  tube('EngineExhaust',(side*.33,-3.405,-.12),.06,.035,p,col,'WW2 Dark')
  box('CabinRearDoor',(side*.16,-1.413,.68),(.23,.018,.35),p,col,'WW2 Gun')
 single_light(p,col,(0,2.16,.46),'')
 beam('RadioWhip',(0,-.91,1.0),(0,-.96,1.59),.017,p,col,'WW2 Dark')
 box('CanvasRoll',(0,-1.53,.52),(.39,.19,.09),p,col,'WW2 White')
 return r

boat=add(torpedo_boat(),'enemy',{'content_id':'ww2_enemy_torpedo_boat','draft_m':.35,'reference':'NHHC U.S. Navy PT Boats; fictional compact 2-tube configuration'})

def deck(timber=False):
 r,col=root('HULL_Ww2DeckPlate_Timber' if timber else 'HULL_Ww2DeckPlate_Steel')
 box('DeckStructure',(0,0,0),(2,2,.16),r,col,'WW2 Gun')
 if timber:
  # Thin seams and staggered ends; restrained contrast, no thick plank ridges.
  for i in range(10):
   x=-.9+i*.2;split=-.5 if i%2 else .5
   for lo,hi in ((-1,split-.003),(split+.003,1)):
    box('TimberBoard',(x,(lo+hi)/2,.085),(.194,hi-lo,.01),r,col,'WW2 Deck' if i%3 else 'WW2 Enemy Deck')
 else:
  for xx in (-.5,.5):
   for yy in (-.5,.5):box('PaintedSteelPanel',(xx,yy,.085),(.994,.994,.01),r,col,'WW2 Deck')
  for xx in (-.93,.93):
   for yy in (-.84,0,.84):cyl('DeckFastener',(xx,yy,.092),.010,.003,r,col,'WW2 Gun',6)
 return r

steel=add(deck(False),'modular_hull',{'cell_m':2,'surface_z':.09,'use':'painted steel destroyer deck'})
timber=add(deck(True),'modular_hull',{'cell_m':2,'surface_z':.09,'use':'optional timber deck for boats/cargo areas'})
r,col=root('HULL_Ww2SideSkirt')
verts=[(x,y,z) for x in (-1,1) for y,z in [(-.06,.08),(.06,.08),(-.20,-1.52),(-.30,-1.52)]]
c.mesh('SlopedHullSide',verts,[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],r,col,'WW2 Hull')
box('SheerStrake',(0,.069,-.065),(2,.025,.075),r,col,'WW2 Gun')
box('DarkWaterline',(0,-.116,-1.04),(2,.018,.11),r,col,'WW2 Dark')
skirt=add(r,'modular_hull',{'cell_m':2,'outward_axis':'Blender +Y','top_z':.08,'bottom_z':-1.52})

def air_variant(index,band):
 r=aircraft(True);r['export_name']='ENE_Ww2TorpedoBomber_%02d'%index;r.name=r['export_name'];col=r.users_collection[0]
 for ob in c.descendants(r):
  if ob.type=='MESH' and ob.get('export_name')=='WingRecognitionStripe':ob.data.materials[0]=c.M[band]
 # Tail panels and actual mesh tail number, both sides and from overhead.
 for side in (-1,1):
  box('TailIDBand',(side*.024,-1.14,.26),(.008,.15,.25),r,col,band)
  curve=bpy.data.curves.new('TailNumber','FONT');curve.body='%02d'%index;curve.size=.12;curve.align_x='CENTER';curve.align_y='CENTER';curve.resolution_u=1
  ob=bpy.data.objects.new('TailNumber',curve);col.objects.link(ob);ob.parent=r
  ob.location=(side*.032,-1.14,.26);ob.rotation_euler=(math.pi/2,0,side*math.pi/2);curve.materials.append(c.M['WW2 Dark' if band=='WW2 White' else 'WW2 White'])
  bpy.context.view_layer.objects.active=ob;ob.select_set(True);bpy.ops.object.convert(target='MESH');ob=bpy.context.object;ob['export_name']='TailNumber';ob.select_set(False)
 for xx in (-.36,.36):box('TailplaneIDBand',(xx,-1.12,.092),(.13,.20,.008),r,col,band)
 return r

planes=[add(air_variant(i+1,mat),'aircraft_variant',{'number':'%02d'%(i+1),'band_material':mat}) for i,mat in enumerate(('WW2 White','WW2 Orange','WW2 Hull'))]

# Load accepted v27 artwork, repair the measured gap, merge static parts by motion group.
blend=OUT.parent/'CoastalBatteryAngular_v27/CoastalBatteryAngular.blend'
with bpy.data.libraries.load(str(blend),link=False) as (src,dst):dst.collections=['ENE_CoastalBattery_Art']
coast_col=dst.collections[0];bpy.context.scene.collection.children.link(coast_col)
coast=next(o for o in coast_col.objects if o.parent is None);coast.location=(0,0,0)
tube_ob=next(o for o in c.descendants(coast) if o.get('export_name')=='TaperedGunTube')
mouth=next(o for o in c.descendants(coast) if o.get('export_name')=='OpenMuzzle')
bpy.context.view_layer.update()
elev=next(o for o in c.descendants(coast) if o.get('export_name')=='ElevationPivot')
mat=elev.matrix_world.inverted()@tube_ob.matrix_world
old_front=max((mat@v.co).y for v in tube_ob.data.vertices)
target_front=2.74
for v in tube_ob.data.vertices:
 q=mat@v.co
 if q.y>old_front-.001:
  q.y=target_front;v.co=mat.inverted()@q
# Slightly proud sleeve avoids coincident outer surfaces in the 3cm overlap.
for v in mouth.data.vertices:
 radius=math.hypot(v.co.x,v.co.z)
 if radius>.114:
  v.co.x*=.123/.115;v.co.z*=.123/.115
coast['MuzzleGapFixed']=True
# A true below-water footing; preserve original pivot positions above the emplacement.
c.frustum('SubmergedRockFooting',(0,0,-.95),(4.80,4.80),(5.55,5.55),1.90,coast,coast_col,'MAT_IslandRock')
# Preserve two orientation anchors even after consolidating the base's meshes.
for n in ('ObserverRoof','CutStoneFoundation'):
 ob=next(o for o in c.descendants(coast) if o.get('export_name')==n)
 loc=tuple(ob.location);ob['export_name']=n+'_OldMesh';ob.name=n+'_OldMesh';c.empty(n,coast,coast_col,loc)
palette_alias={'BatteryConcrete':'ENV_Concrete','BatteryRock':'ENV_RockStrata','BatteryDark':'WW2 Dark',
 'GunPlate':'WW2 Gun','SteelEdge':'WW2 Hull','EnemyRed':'WW2 Enemy Red','MAT_IslandCliff':'ENV_RockStrata','MAT_IslandRock':'ENV_RockStrata','Radar Glass':'WW2 Glass','Gunmetal':'WW2 Dark'}
for ob in c.descendants(coast):
 if ob.type=='MESH':
  for slot in ob.material_slots:
   original=slot.material.name.split('.')[0]
   if original in palette_alias:slot.material=c.M[palette_alias[original]]
coast=add(coast,'repair',{'source':'CoastalBatteryAngular_v27','old_barrel_end':old_front,'new_barrel_end':target_front,'mouth_outer_start':2.71,'overlap_m':.03,'footing_bottom_z':-1.90})

# Studio and reference objects for relative scale; all remain out of exported FBX.
reference_col=c.collection('ReferenceOnly');reference_roots=[]
reference=gunboat(True);reference_roots.append(reference)
scene=bpy.context.scene;scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True
scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.12,.17,.20,1)
scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.7
for loc in [(-7,5,12),(8,-6,10)]:
 d=bpy.data.lights.new('Softbox','AREA');d.energy=1800;d.size=8
 ob=bpy.data.objects.new('Softbox',d);scene.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=(-ob.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new('Review');cam=bpy.data.objects.new('Review',d);scene.collection.objects.link(cam);scene.camera=cam;d.type='ORTHO'
scene.render.engine='BLENDER_EEVEE';scene.eevee.taa_render_samples=48;scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.view_settings.look='AgX - Medium High Contrast'
def visible(roots):
 for rr in assets+reference_roots:
  for ob in c.descendants(rr):ob.hide_render=(rr not in roots)
def render(name,scale,target=(0,0,.5),pos=(8,12,9)):
 cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();d.ortho_scale=scale
 scene.render.filepath=str(OUT/'Preview'/(name+'.png'));bpy.ops.render.render(write_still=True)
visible([boat]);render('WWII_TorpedoBoat',8.8)
render('WWII_TorpedoBoat_Top',8.8,pos=(0,0,15))
visible([boat,reference]);boat.location.x=-2.5;reference.location.x=2.5
render('TorpedoBoat_SizeComparison',11.2,target=(0,0,.45))
boat.location.x=reference.location.x=0
visible(planes)
for i,rr in enumerate(planes):rr.location.x=(1-i)*3.8
render('TorpedoBomber_Variants',12,target=(0,-.2,.15))
for rr in planes:rr.location=(0,0,0)
visible([steel,timber,skirt]);steel.location.x=2.6;skirt.location.y=-2.0;timber.location.x=-2.6
render('WWII_HullPieces',9.3,target=(0,-.2,-.20))
for rr in (steel,timber,skirt):rr.location=(0,0,0)
visible([coast]);render('CoastalBattery_Fixed',8,target=(0,.15,.65))
render('CoastalBattery_MuzzleDetail',2.8,target=(0,1.95,1.78),pos=(5,8,4))
visible([coast,boat]);coast.location.x=4;boat.location.x=-3.4
render('CoastalBattery_CombatScale',15.2,target=(0,0,.1),pos=(0,3,19))
coast.location.x=boat.location.x=0

# Load v30 modules to demonstrate deck pieces under the actual player layout.
with bpy.data.libraries.load(str(OUT.parent/'WWII_ScaleReview_v30/WWII_ScaleReviewed.blend'),link=False) as (src,dst):
 dst.collections=[n for n in src.collections if n in {'MOD_Ww2Bridge','MOD_Ww2DualGun','MOD_Ww2QuadAa','MOD_Ww2TorpedoMount'}]
demo_col=c.collection('HullStyleDemo');demo=c.empty('PreviewPlayerHull',None,demo_col)
demo_modules=[]
for coll in dst.collections:
 bpy.context.scene.collection.children.link(coll)
 rr=next(o for o in coll.objects if o.parent is None);rr.location=(0,0,0);demo_modules.append(rr)
 name=rr.get('export_name',rr.name)
 if name=='MOD_Ww2DualGun':rr.location.y=4;forwardgun=rr
 elif name=='MOD_Ww2QuadAa':rr.location.y=-6;rr.rotation_euler.z=math.pi
 elif name=='MOD_Ww2TorpedoMount':rr.location.y=-4
for y in range(-8,5,2):
 for rr,loc,rot in [(steel,(0,y,0),0),(skirt,(-1,y,0),math.pi/2),(skirt,(1,y,0),-math.pi/2)]:
  ob=c.clone(rr,demo_col);ob.location=loc;ob.rotation_euler.z=rot;ob.parent=demo
aft=c.clone(forwardgun,demo_col);aft.location=(0,-8,0);aft.rotation_euler.z=math.pi;aft.parent=demo
plate('GeneratedBowDeck',[(-1,5,.09),(0,7.4,.09),(1,5,.09)],.12,demo,demo_col,'WW2 Deck')
verts=[(-1,5,.08),(0,7.4,.08),(1,5,.08),(-.8,5,-1.52),(0,6.68,-1.52),(.8,5,-1.52)]
c.mesh('GeneratedBowSides',verts,[(0,3,4,1),(1,4,5,2),(0,2,5,3),(3,5,4),(0,1,2)],demo,demo_col,'WW2 Hull')
box('SternClosure',(0,-9,-.72),(2,.08,1.60),demo,demo_col,'WW2 Hull')
visible([])
for rr in demo_modules+[demo]:
 for ob in c.descendants(rr):ob.hide_render=False
render('WWII_PlayerHull_Style',19,target=(0,-1,.40),pos=(12,18,14))
for ob in list(demo_col.objects):bpy.data.objects.remove(ob,do_unlink=True)
for rr in demo_modules:
 for ob in list(c.descendants(rr)):bpy.data.objects.remove(ob,do_unlink=True)
for rr in reference_roots:
 for ob in list(c.descendants(rr)):bpy.data.objects.remove(ob,do_unlink=True)
reference_roots=[];visible(assets)
for i,rr in enumerate(assets):rr.location=(i*12,0,0)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'WWII_RequestPack.blend'))
(OUT/'manifest.json').write_text(json.dumps(report,indent=2))
print('REQUEST PACK COMPLETE',len(assets),'assets')
