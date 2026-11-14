"""Stage 3 hybrid carrier. Original geometry based on the two supplied images."""
import json, math, sys
from pathlib import Path
import bpy
from mathutils import Vector

OUT = Path(__file__).resolve().parent
sys.dont_write_bytecode = True
sys.path.insert(0, str(OUT.parent / 'CoastalObjectsPack_v4' / 'Source'))
import environment_detail_common as c

bpy.ops.wm.read_factory_settings(use_empty=True)
for folder in ('Models', 'Preview'):
    (OUT / folder).mkdir(exist_ok=True)
HULL, STEEL, DARK, FLIGHT = 'Enemy hull grey', 'Enemy superstructure grey', 'Enemy Dark', 'Nonslip deck'
GUN, GLASS, WHITE, YELLOW, RED = 'Enemy weapon dark', 'Enemy bridge glass', 'Medical White', 'Warning Yellow', 'Warning red'
c.COLORS.update({HULL:(.15,.18,.215,1),STEEL:(.21,.254,.29,1),DARK:(.049,.071,.091,1),FLIGHT:(.054,.071,.087,1),GUN:(.072,.095,.118,1),GLASS:(.028,.075,.096,1),WHITE:(.76,.79,.80,1),YELLOW:(.82,.57,.15,1),RED:(.27,.084,.074,1),'Deck Grey':(.18,.205,.22,1)})
c.setup()
col=c.collection('Stage3HybridCarrier'); root=c.empty('EnemyHybridBoss',None,col)
body=c.empty('Hull',root,col)
def box(n,p,d,m=STEEL,parent=body,rot=(0,0,0)):
    return c.box(n,p,d,parent,col,m,rot)
def beam(n,a,b,w,m=STEEL,parent=body):
    return c.beam(n,a,b,w,parent,col,m)
def cyl(n,p,r,d,m=GUN,parent=body,k=10,rot=(0,0,0)):
    return c.cyl(n,p,r,d,parent,col,m,k,rot,False)
def pad(n,outline,top,bottom,m=STEEL,parent=body):
    k=len(outline); vertices=[(x,y,z) for z in (bottom,top) for x,y in outline]
    faces=[tuple(reversed(range(k))),tuple(range(k,2*k))]+[(i,(i+1)%k,(i+1)%k+k,i+k) for i in range(k)]
    return c.mesh(n,vertices,faces,parent,col,m)
def chamfer(n,x,y,w,l,top,bottom,m=STEEL,parent=body,crop=.14):
    a,b=w/2,l/2; t=min(crop,a*.65,b*.65)
    return pad(n,[(x+u,y+v) for u,v in [(-a+t,-b),(a-t,-b),(a,-b+t),(a,b-t),(a-t,b),(-a+t,b),(-a,b-t),(-a,-b+t)]],top,bottom,m,parent)

# Hull loft: wider under the flight deck, a raised forecastle and a sharp stem.
# stern-to-bow stations: y, half-width and main-deck sheer height.
stations=[(-8,1.31,.63),(-7.60,1.61,.69),(-6.30,1.81,.73),(-4.80,1.87,.76),(-2.15,1.86,.77),(.30,1.74,.81),(2.50,1.58,.89),(4.65,1.30,.96),(6.10,.93,1.00),(7.15,.50,1.10),(8,.045,1.23)]
verts=[]; faces=[]; mats=[]
for y,w,z in stations:
    # Continuous section, with separate narrow waterline material and a keel facet.
    ky=y-.23 if y>6.10 else (y+.14 if y< -6 else y)
    depth=-.53 if abs(y)<7.5 else -.38
    verts.extend([(-w,y,z),(-w*.965,y,z*.62),(-w*.91,y,.052),(-w*.90,y,-.052),(-w*.62,ky,depth),
                  (w*.62,ky,depth),(w*.90,y,-.052),(w*.91,y,.052),(w*.965,y,z*.62),(w,y,z)])
faces.append(tuple(reversed(range(10))));mats.append(0)
for j in range(len(stations)-1):
    for i in range(10):
        q=(i+1)%10;faces.append((j*10+i,j*10+q,(j+1)*10+q,(j+1)*10+i))
        mats.append(1 if i in (2,6) else 2 if i in (3,4,5) else 3 if i==9 else 0)
faces.append(tuple(range((len(stations)-1)*10,len(stations)*10)));mats.append(0)
ob=c.mesh('ArmoredHull',verts,faces,body,col,HULL)
for m in (DARK,RED,'Deck Grey'):ob.data.materials.append(c.M[m])
for f,m in zip(ob.data.polygons,mats):f.material_index=m

# Main flight surface and a tapered hangar side beneath the sponsons.
flight=c.empty('FlightDeck',root,col)
outline=[(-1.65,-7.98),(1.65,-7.98),(2.05,-7.69),(2.05,-6.61),(2.23,-6.32),(2.23,-4.62),
 (2.07,-4.3),(2.07,-1.12),(1.86,-.47),(1.31,.49),(-1.31,.49),(-1.86,-.47),(-2.07,-1.12),
 (-2.07,-4.30),(-2.23,-4.62),(-2.23,-6.32),(-2.05,-6.61),(-2.05,-7.69)]
pad('FlightDeckEdge',outline,1.37,1.21,HULL,flight)
pad('FlightDeckSurface',[(x*.988,y) for x,y in outline],1.397,1.366,FLIGHT,flight)
hangar=[(-1.37,-7.77),(1.37,-7.77),(1.72,-6.83),(1.80,-3.9),(1.72,-1.00),(1.35,.44),(-1.35,.44),(-1.72,-1.00),(-1.80,-3.90),(-1.72,-6.83)]
pad('HangarEnvelope',hangar,1.215,.65,STEEL,flight)
def mark(n,a,b,w,m=WHITE):
    x,y=a;u,v=b
    s=w/(2*math.hypot(u-x,v-y)); dx,dy=(v-y)*s,(x-u)*s
    return c.mesh(n,[(x+dx,y+dy,1.399),(u+dx,v+dy,1.399),(u-dx,v-dy,1.399),(x-dx,y-dy,1.399)],[(0,1,2,3)],flight,col,m)
for side in (-1,1):
    mark('RunwayBound',(side*.87,-7.55),(side*.87,-.32),.041)
    for y in (-6.25,-4.92,-3.59,-2.26):
        mark('ParkingGuide',(side*1.11,y),(side*1.79,y),.023)
        mark('ParkingGuide',(side*1.11,y),(side*1.34,y+.26),.023)
    for y in (-6.8,-5.12,-3.36,-1.64):
        x=side*(1.70 if y< -2 else 1.65)
        beam('DeckSupport',(x*.92,y,.74),(x*1.11,y+.16,1.22),.13,HULL,flight)
        box('HangarVent',(x,y,1.02),(.036,.34,.13),DARK,flight)
    for x,y,u,v in [(1.14,-7.56,1.67,-7.56),(1.67,-7.56,1.67,-6.82),(1.67,-6.82,1.14,-6.82)]:
        mark('DeckEdgeWarning',(x*side,y),(u*side,v),.033,YELLOW)
for i in range(12):
    y=-7.19+i*.57;mark('BrokenCenterline',(0,y),(0,y+.31),.033)
for x in (-.66,-.48,-.30,.30,.48,.66):mark('Threshold',(x,-7.66),(x,-7.26),.052)
for x in (-.60,.60):mark('RunwayFront',(0,-.095),(x,-.53),.048)
c.empty('FlightDeckLaunch',flight,col,(0,-.14,1.43))

# Forward island with layered armor, glazing, raked exhaust and narrow mast.
island=c.empty('BridgeAssembly',root,col)
chamfer('ControlDeckBase',0,1.34,2.40,1.70,1.33,.83,HULL,island,.22)
chamfer('BridgeLower',-.12,1.28,1.67,1.33,2.31,1.28,STEEL,island,.21)
chamfer('BridgeWing',-.10,1.34,2.17,1.36,2.36,2.24,HULL,island,.19)
chamfer('WheelhouseGlazing',-.12,1.34,1.96,1.23,2.58,2.33,GLASS,island,.18)
chamfer('WheelhouseRoof',-.12,1.34,2.08,1.32,2.70,2.57,STEEL,island,.19)
for x in (-.77,-.44,-.12,.2,.53):box('GlazingMullion',(x,1.964,2.46),(.032,.035,.24),STEEL,island)
for side in (-1,1):
    box('ArmorPanel',(-.12+side*.837,1.26,1.87),(.048,.76,.49),HULL,island)
    box('IdentificationStripe',(-.12+side*.865,1.27,1.65),(.018,.55,.063),RED,island)
chamfer('ExhaustHousing',.57,.75,.42,.51,2.96,2.67,HULL,island,.063)
chamfer('ExhaustCap',.57,.75,.43,.52,3.004,2.96,DARK,island,.06)
chamfer('MastSeat',-.33,1.25,.63,.57,2.99,2.68,STEEL,island,.08)
for x,y in [(-.58,1.09),(-.03,1.10),(-.31,1.51)]:beam('MastBrace',(x,y,2.97),(-.32,1.25,3.91),.043,STEEL,island)
beam('MastUpright',(-.32,1.25,3.35),(-.32,1.25,4.37),.055,STEEL,island)
for z,w in [(3.43,.70),(3.76,.59),(4.08,.48)]:
    box('Yardarm',(-.32,1.25,z),(w,.043,.043),STEEL,island)
radar=c.empty('RadarPivot',island,col,(-.32,1.25,3.94))
box('RadarPanel',(0,0,0),(.62,.088,.17),HULL,radar,(.13,0,0))
box('RadarInset',(0,.049,.005),(.51,.014,.111),DARK,radar)
for x,y in [(-.79,1.45),(.21,1.66)]:
    cyl('SensorBase',(x,y,2.75),.123,.095,STEEL,island,8)
    cyl('PaleSensor',(x,y,2.87),.098,.18,WHITE,island,10)
beam('SignalWhip',(-.32,1.25,4.32),(-.32,1.25,4.59),.021,DARK,island)

# Two stepped bow turrets, each with distinct yaw, elevation, and paired muzzle sockets.
def main_gun(i,y,deck,riser):
    chamfer('ForecastleGunPlinth',0,y,1.56,1.52,deck+riser,deck-.16,HULL,crop=.17)
    z=deck+riser
    cyl('SlewRing',(0,y,z+.056),.64,.12,GUN,k=12)
    p=c.empty(f'MainGun_{i:02}',root,col,(0,y,z+.113))
    chamfer('GunhouseLower',0,-.065,1.46,1.23,.33,.025,STEEL,p,.16)
    c.frustum('GunhouseSlopedRoof',(0,-.078,.44),(1.43,1.16),(1.17,.94),.28,p,col,HULL)
    for side in (-1,1):box('TurretRecognitionMark',(side*.728,-.24,.236),(.024,.38,.08),RED,p)
    pitch=c.empty(f'MainGun_Elevation_{i:02}',p,col,(0,.42,.37))
    for j,x in enumerate((-.245,.245),1):
        box('GunMantlet',(x,.09,0),(.25,.23,.20),GUN,pitch)
        c.lathe('MainBarrel',(x,0,0),[(.16,.084),(.46,.084),(.51,.057),(1.43,.041)],pitch,col,GUN,10,False,(-math.pi/2,0,0))
        cyl('MuzzleBand',(x,1.405,0),.052,.061,STEEL,pitch,10,(math.pi/2,0,0))
        cyl('BoreMark',(x,1.438,0),.029,.007,DARK,pitch,8,(math.pi/2,0,0))
        c.empty(f'MainGun_Muzzle_{(i-1)*2+j:02}',pitch,col,(x,1.45,0))
main_gun(1,5.59,1.00,.12)
main_gun(2,3.59,.93,.74)

# Each defense station is on a deck-edge sponson, not in the runway lane.
for side,label in [(-1,'Port'),(1,'Starboard')]:
    for i,y in enumerate((-.99,-3.70,-6.53),1):
        x=side*(2.13 if i<3 else 2.20)
        chamfer('AASponson',x,y,.73,.83,1.32,1.12,HULL,flight,.13)
        beam('SponsonKnee',(x-side*.42,y,.83),(x,y,1.14),.13,HULL,flight)
        cyl('AAPedestal',(x,y,1.369),.227,.104,GUN,flight,10)
        yaw=c.empty(f'AA_{label}_{i:02}',root,col,(x,y,1.42));yaw.rotation_euler.z=-side*math.pi/2
        chamfer('AATurret',0,0,.39,.39,.24,0,STEEL,yaw,.058)
        chamfer('SensorHood',0,-.075,.265,.225,.435,.225,WHITE,yaw,.046)
        box('AADarkVisor',(0,.041,.337),(.176,.028,.088),GLASS,yaw)
        pitch=c.empty(f'AA_{label}_Elevation_{i:02}',yaw,col,(0,.14,.18))
        for m in (-.062,.062):cyl('AABarrel',(m,.14,0),.022,.28,GUN,pitch,8,(math.pi/2,0,0))
        c.empty(f'AA_{label}_Muzzle_{i:02}',pitch,col,(0,.285,0))
        if side==1 and i==2:c.empty('AAMount',pitch,col,(0,.285,0))

# Forecastle fittings and sparse safety rails, rendered at the same scale as the hull.
cyl('AnchorWindlass',(0,7.0,1.165),.106,.45,GUN,k=8,rot=(0,math.pi/2,0))
for side in (-1,1):
    beam('AnchorLead',(side*.17,7.02,1.17),(side*.09,7.64,1.21),.028,GUN)
    for y,x,z in [(6.54,.63,1.11),(-7.77,1.29,1.44)]:cyl('MooringBollard',(side*x,y,z),.046,.13,GUN,k=8)
    for y,w in ((1.17,1.622),(2.24,1.545),(3.38,1.424),(4.58,1.258)):
        box('HullSideArmor',(side*w,y*.999,.59),(.034,.41,.074),STEEL)
    for y,x in [(-7.15,2.0),(-5.54,2.17),(-2.31,2.01)]:
        beam('DeckSafetyRail',(side*x,y-.34,1.49),(side*x,y+.34,1.49),.017,STEEL,flight)
        for yy in (y-.33,y+.33):box('RailPost',(side*x,yy,1.44),(.021,.025,.108),STEEL,flight)

# Bake by independently moving node. Unmerged source parts are unnecessary for the FBX.
c.finish(root)
for p in [o for o in c.descendants(root) if o.type=='EMPTY']:
    children=[o for o in p.children if o.type=='MESH']
    if len(children)<2:continue
    verts=[];faces=[];materials=[];slots=[]; inv=p.matrix_world.inverted()
    for ob in children:
        off=len(verts); m=inv@ob.matrix_world
        verts.extend(tuple(m@v.co) for v in ob.data.vertices)
        for f in ob.data.polygons:
            mat=ob.data.materials[f.material_index]
            if mat not in materials:materials.append(mat)
            faces.append(tuple(off+i for i in f.vertices));slots.append(materials.index(mat))
    name=p.name+'_Mesh'; mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    for m in materials:mesh.materials.append(m)
    for f,i in zip(mesh.polygons,slots):f.material_index=i
    for ob in children:bpy.data.objects.remove(ob,do_unlink=True)
    ob=bpy.data.objects.new(name,mesh);col.objects.link(ob);ob.parent=p
bpy.context.view_layer.update()
objs=c.descendants(root)
bpy.ops.object.select_all(action='DESELECT')
for ob in objs:ob.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.export_scene.fbx(filepath=str(OUT/'Models/EnemyHybridBoss.fbx'),use_selection=True,
    axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_NONE',object_types={'EMPTY','MESH'},bake_anim=False)
pts=[o.matrix_world@v.co for o in objs if o.type=='MESH' for v in o.data.vertices]
lo=[min(v[i] for v in pts) for i in range(3)];hi=[max(v[i] for v in pts) for i in range(3)]
(OUT/'manifest.json').write_text(json.dumps({'original_bounds_m':[lo,hi], 'tris':c.tris(root),
 'flight_deck_length_m':8.47, 'main_turrets':2, 'barrels_per_main_turret':2,'aa_per_side':3},indent=2))

scene=bpy.context.scene
scene.world=bpy.data.worlds.new('Blue Grey Studio');scene.world.use_nodes=True
bg=scene.world.node_tree.nodes.get('Background');bg.inputs[0].default_value=(.105,.143,.18,1);bg.inputs[1].default_value=.78
for name,loc,energy,size in [('Key',(-7,6,13),3600,10),('Fill',(9,3,8),2400,8),('Rim',(4,-10,9),3200,9)]:
    ld=bpy.data.lights.new(name,'AREA');ld.energy=energy;ld.size=size
    lamp=bpy.data.objects.new(name,ld);scene.collection.objects.link(lamp);lamp.location=loc;lamp.rotation_euler=(-lamp.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Review');cam=bpy.data.objects.new('Review',data);scene.collection.objects.link(cam);scene.camera=cam;data.type='ORTHO'
scene.render.engine='BLENDER_EEVEE';scene.eevee.taa_render_samples=48
scene.render.resolution_x=1800;scene.render.resolution_y=1040;scene.render.resolution_percentage=100
scene.view_settings.look='AgX - Medium High Contrast'
def render(name,pos,focus,scale):
    cam.location=pos;cam.rotation_euler=(Vector(focus)-cam.location).to_track_quat('-Z','Y').to_euler();data.ortho_scale=scale
    scene.render.filepath=str(OUT/'Preview'/(name+'.png'));bpy.ops.render.render(write_still=True)
render('Stage3HybridCarrier',(-13,8,9),(0,0,1.35),17.45)
render('Stage3HybridCarrier_Aft',(12,-8,10),(0,-1,1.3),17.3)
scene.render.resolution_y=835;cam.location=(0,0,21);cam.rotation_euler=(0,0,-math.pi/2);data.ortho_scale=18
scene.render.filepath=str(OUT/'Preview/Stage3HybridCarrier_Top.png');bpy.ops.render.render(write_still=True)
scene.render.resolution_y=1040;cam.location=(-13,8,9);cam.rotation_euler=(Vector((0,0,1.35))-cam.location).to_track_quat('-Z','Y').to_euler();data.ortho_scale=17.45
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Stage3HybridCarrier.blend'))
