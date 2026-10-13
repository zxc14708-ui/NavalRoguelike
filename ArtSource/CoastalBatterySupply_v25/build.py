"""Reusable miniature coastal battery and allied supply ship. Review assets only."""
import bpy,sys,math,json
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent
(OUT/'Models').mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(OUT.parents[0]/'CoastalObjectsPack_v4'/'Source'))
import environment_detail_common as c
bpy.ops.wm.read_factory_settings(use_empty=True)
c.COLORS.update({
 'BatteryConcrete':(.47,.51,.51,1),'BatteryDark':(.16,.21,.23,1),
 'BatteryRock':(.34,.34,.33,1),'EnemyRed':(.54,.13,.10,1),
 'SupplyHull':(.46,.19,.16,1),'SupplyHullDark':(.31,.10,.095,1),
 'SupplyWhite':(.78,.82,.80,1),'SupplyDeck':(.25,.30,.31,1),
 'ContainerBlue':(.16,.37,.51,1),'ContainerTeal':(.21,.43,.42,1),
 'ContainerTan':(.57,.45,.30,1),'ContainerRed':(.51,.25,.20,1),
 'SafetyOrange':(.82,.42,.11,1)})
c.setup()

def root(name):
 col=c.collection(name);r=c.empty(name,None,col);return r,col

def hull(name,sections,parent,col,mat):
 # Stations are y, half beam, keel, side-deck height; pointed bow and squared stern.
 verts=[]
 for y,w,keel,deck in sections:
  verts.extend([(-w, y,keel),(-w,y,deck),(w,y,deck),(w,y,keel)])
 faces=[(0,1,2,3)]
 for i in range(len(sections)-1):
  a=4*i;b=4*(i+1)
  faces += [(a,a+1,b+1,b),(a+1,a+2,b+2,b+1),(a+2,a+3,b+3,b+2),(a+3,a,b,b+3)]
 n=4*(len(sections)-1);faces.append((n+3,n+2,n+1,n))
 return c.mesh(name,verts,faces,parent,col,mat)

def build_battery():
 r,col=root('ENE_CoastalBattery_Art')
 # Five-metre shore foundation matching current collider and shore terrain.
 c.cyl('WeatheredRockFoundation',(0,0,.27),2.72,.54,r,col,'BatteryRock',12)
 for i in range(10):
  a=i*math.tau/10
  c.box('StrataBlock',(2.45*math.cos(a),2.45*math.sin(a),.40),(.62,.43,.20),r,col,'MAT_IslandCliff',(0,0,a))
 c.cyl('ConcreteEmplacement',(0,0,.72),2.22,.36,r,col,'BatteryConcrete',12)
 c.cyl('TurretRingWell',(0,0,.923),1.16,.055,r,col,'BatteryDark',16)
 # Sandbags leave the front 100 degrees clear for gun elevation.
 for i in range(5,14):
  a=i*math.tau/12
  x,y=1.95*math.cos(a),1.95*math.sin(a)
  c.box('SandbagLow',(x,y,.99),(.84,.37,.22),r,col,'MAT_IslandSand',(0,0,a+math.pi/2),.055)
  if i%2==0:c.box('SandbagUpper',(x,y,1.20),(.77,.33,.21),r,col,'MAT_IslandSand',(0,0,a+math.pi/2),.052)
 turret=c.empty('TurretPivot',r,col,(0,0,1.06))
 c.cyl('TraverseRing',(0,0,.02),.96,.18,turret,col,'Gunmetal',16)
 c.frustum('FacetedGunHouse',(0,-.19,.55),(1.96,2.22),(1.54,1.75),.91,turret,col,'BatteryConcrete')
 c.box('FrontArmorPlate',(0,.66,.58),(1.53,.12,.68),turret,col,'BatteryConcrete')
 c.box('RearAccessDoor',(0,-1.081,.47),(.48,.03,.54),turret,col,'BatteryDark')
 for side in (-1,1):
  c.box('SideWarningStripe',(side*.965,-.08,.59),(.02,1.15,.10),turret,col,'EnemyRed')
  c.box('SideVent',(side*.975,-.62,.65),(.025,.27,.22),turret,col,'BatteryDark')
  for zz in (.60,.66,.72):c.box('VentSlat',(side*.993,-.62,zz),(.015,.25,.018),turret,col,'Gunmetal')
 c.box('RangefinderBridge',(0,-.42,1.05),(.92,.24,.17),turret,col,'BatteryDark')
 for side in (-1,1):
  c.box('OpticalHousing',(side*.41,-.41,1.06),(.20,.33,.23),turret,col,'BatteryConcrete')
  c.box('OpticGlass',(side*.41,-.235,1.06),(.12,.014,.10),turret,col,'Radar Glass')
 pivot=c.empty('ElevationPivot',turret,col,(0,.69,.55))
 c.cyl('BreechSleeve',(0,.14,0),.24,.65,pivot,col,'Gunmetal',12,(math.pi/2,0,0))
 c.cyl('BarrelJacket',(0,.69,0),.165,.76,pivot,col,'BatteryConcrete',12,(math.pi/2,0,0))
 c.cyl('MainGunTube',(0,1.56,0),.112,1.22,pivot,col,'Gunmetal',12,(math.pi/2,0,0))
 c.cyl('MuzzleBrake',(0,2.18,0),.17,.32,pivot,col,'BatteryDark',10,(math.pi/2,0,0))
 for y in (2.08,2.21):c.box('BrakeSideSlot',(0,y,.173),(.21,.055,.016),pivot,col,'Gunmetal')
 c.empty('Muzzle',pivot,col,(0,2.36,0))
 # One small berm-side command/service box, separate from turning gun.
 c.box('CableConduit',(1.65,-.62,1.06),(.42,.18,.12),r,col,'Gunmetal')
 c.box('ServiceBox',(1.79,-.78,1.22),(.42,.35,.45),r,col,'BatteryConcrete',bevel=.035)
 c.box('ServiceDoor',(1.79,-.964,1.20),(.28,.02,.32),r,col,'BatteryDark')
 return r

def build_supply():
 r,col=root('ALLY_SupplyShip_Art')
 sections=[(-7.75,1.55,-1.07,.93),(-6.2,2.06,-1.12,1.10),(-3.1,2.10,-1.14,1.13),
           (2.8,2.10,-1.12,1.15),(5.95,1.82,-1.00,1.13),(7.08,1.10,-.80,1.05),(7.78,.08,-.31,.94)]
 hull('RedDisplacementHull',sections,r,col,'SupplyHull')
 c.box('DarkWaterlineBelt',(0,-.14,-.48),(4.12,14.65,.23),r,col,'SupplyHullDark')
 c.box('GreyWorkingDeck',(0,-.03,1.17),(3.92,14.35,.12),r,col,'SupplyDeck')
 for side in (-1,1):
  c.box('RaisedBulwark',(side*2.01,-.70,1.41),(.09,12.45,.45),r,col,'SupplyWhite')
  for y in (-6,-3,0,3,6):
   c.box('Scupper',(side*2.068,y,1.27),(.012,.24,.09),r,col,'Gunmetal')
 # Aft superstructure with angled front and bridge windows.
 c.frustum('AftDeckhouse',(0,-5.53,2.17),(3.43,3.16),(3.24,2.90),1.85,r,col,'SupplyWhite')
 c.frustum('Wheelhouse',(0,-5.13,3.31),(3.18,2.09),(2.90,1.82),.61,r,col,'SupplyWhite')
 c.box('FrontWindowBand',(0,-4.079,3.32),(2.75,.028,.32),r,col,'Radar Glass')
 for side in (-1,1):
  c.box('BridgeSideWindow',(side*1.55,-5.04,3.32),(.02,.88,.29),r,col,'Radar Glass')
  c.box('BridgeWing',(side*1.78,-4.64,3.02),(.48,1.32,.09),r,col,'SupplyWhite')
 c.box('BridgeRoof',(0,-5.2,3.67),(3.31,2.35,.12),r,col,'SupplyWhite')
 c.box('Funnel',(0,-6.38,4.20),(1.00,.89,1.05),r,col,'SupplyDeck',bevel=.08)
 c.box('FunnelCap',(0,-6.38,4.77),(1.11,.99,.14),r,col,'Gunmetal')
 c.box('NavigationMast',(0,-4.66,4.05),(.06,.06,.67),r,col,'Gunmetal')
 c.box('MastCrossbar',(0,-4.66,4.39),(.98,.06,.05),r,col,'Gunmetal')
 for side in (-1,1):
  c.box('NavigationLight',(side*.48,-4.66,4.39),(.13,.10,.09),r,col,'SafetyOrange')
 # Two cargo lanes, four bays, one half-height spare pallet. Clear side passages.
 palette=['ContainerBlue','ContainerTan','ContainerTeal','ContainerRed']
 for row,y in enumerate((-2.68,-.24,2.20,4.64)):
  for j,side in enumerate((-1,1)):
   x=side*.91;mat=palette[(row+j)%4]
   c.box('CargoContainer',(x,y,1.84),(1.68,2.13,1.24),r,col,mat,bevel=.035)
   c.box('ContainerTop',(x,y,2.47),(1.66,2.11,.06),r,col,'SupplyWhite')
   for dx in (-.70,.70):
    c.box('CornerPost',(x+dx,y,1.84),(.055,2.09,1.20),r,col,'SupplyHullDark')
   for yy in (-.73,-.36,0,.36,.73):
    c.box('Rib',(x,y+yy,1.85),(1.66,.025,1.11),r,col,'SupplyHullDark')
 # Distinct cargo handling crane and equipment aft of stack.
 c.cyl('CargoCranePedestal',(.0,6.44,1.58),.19,.73,r,col,'SupplyWhite',12)
 c.beam('CraneBoom',(0,6.44,1.89),(.08,7.15,3.04),.16,r,col,'SafetyOrange')
 c.box('CraneHead',(.08,7.15,3.03),(.19,.15,.15),r,col,'SupplyDeck')
 c.beam('CraneCable',(.08,7.15,2.97),(.08,7.15,2.64),.018,r,col,'Gunmetal')
 for side in (-1,1):
  c.cyl('LiferaftCanister',(side*1.82,-5.59,2.28),.17,.79,r,col,'SupplyWhite',10,(math.pi/2,0,0))
  for y in (-6.48,6.03):c.cyl('MooringBollard',(side*1.45,y,1.32),.065,.18,r,col,'Gunmetal',10)
 c.empty('Bow',r,col,(0,7.75,1.1))
 c.empty('Stern',r,col,(0,-7.75,1.1))
 c.empty('Waterline',r,col,(0,0,0))
 return r

def export(r):
 c.finish(r)
 obs=c.descendants(r)
 saved={o:o.name for o in bpy.data.objects}
 for i,o in enumerate(bpy.data.objects):o.name='TMP_%05d'%i
 for o in obs:o.name=o['export_name']
 bpy.ops.object.select_all(action='DESELECT')
 for o in obs:o.select_set(True)
 bpy.context.view_layer.objects.active=r
 bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/(r['export_name']+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_NONE',bake_anim=False)
 for i,o in enumerate(bpy.data.objects):o.name='RESTORE_%05d'%i
 for o,n in saved.items():o.name=n

models=[build_battery(),build_supply()]
report=[]
for r in models:
 export(r);report.append({'asset':r['export_name'],'triangles':c.tris(r),'sockets':[o['export_name'] for o in c.descendants(r) if o.type=='EMPTY'],'review_only':True})
scene=bpy.context.scene;scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True
scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.11,.16,.19,1)
scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.7
for loc in [(-8,4,13),(8,-7,10)]:
 d=bpy.data.lights.new('Softbox','AREA');d.energy=1900;d.size=8
 ob=bpy.data.objects.new('Softbox',d);scene.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=(-ob.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new('ReviewCamera');cam=bpy.data.objects.new('ReviewCamera',d);scene.collection.objects.link(cam);scene.camera=cam;d.type='ORTHO'
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1600;scene.render.resolution_y=950
scene.render.resolution_percentage=100;scene.view_settings.look='AgX - Medium High Contrast'
def render(name,target,pos,scale):
 cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();d.ortho_scale=scale
 scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
for r in models:
 for o in c.descendants(r):o.hide_render=(r!=models[0])
render('CoastalBattery',(0,0,1),(7,11,8),8.2)
for r in models:
 for o in c.descendants(r):o.hide_render=(r!=models[1])
render('SupplyShip',(0,0,1),(11,17,12),20)
for r in models:
 for o in c.descendants(r):o.hide_render=False
models[0].location.x=-7;models[1].location.x=5.2
render('CoastalBattery_and_SupplyShip',(0,0,1),(13,19,15),27)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CoastalBatterySupply.blend'))
(OUT/'manifest.json').write_text(json.dumps(report,indent=2))
