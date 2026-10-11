"""Naval helicopter cockpit/tail redesign; review assets only."""
import bpy,math,sys,json
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent
(OUT/'Models').mkdir(parents=True,exist_ok=True)
sys.path.insert(0,'C:/Users/최병욱/Documents/Codex/2026-09-11/referenced-chatgpt-conversation-this-is-an')
import environment_detail_common as c
import miniature_bridge_support as h
bpy.ops.wm.read_factory_settings(use_empty=True)
c.COLORS.update({'Naval Blue Grey':(.24,.32,.35,1),'Deck Grey':(.12,.17,.19,1),'Medical White':(.68,.73,.74,1),'Sensor Glass':(.025,.105,.14,1),'Radar Glass':(.018,.06,.085,1),'Role ASW':(.22,.68,1,1)})
c.setup()
def children(r):return list(c.descendants(r))
def name(o):return o.get('export_name',o.name)
def export(r):
 c.finish(r);obs=children(r)+[r];saved={o:o.name for o in bpy.data.objects}
 for i,o in enumerate(bpy.data.objects):o.name='TMP_%05d'%i
 for o in obs:o.name=name(o)
 bpy.ops.object.select_all(action='DESELECT')
 for o in obs:o.select_set(True)
 bpy.context.view_layer.objects.active=r
 bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/(name(r)+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_NONE',bake_anim=False)
 for i,o in enumerate(bpy.data.objects):o.name='RESTORE_%05d'%i
 for o,n in saved.items():o.name=n
def redesign(r,col):
 h._loft_fuselage('NavalCabin',[(-.78,.15,.36,.72),(-.48,.35,.27,.96),(.56,.35,.27,.96),(.82,.285,.34,.59),(.99,.12,.39,.54)],r,col)
 # Windshields sit on the steep forward face, not on the roof.
 for side in (-1,1):
  h._quad_plate('ForwardWindshield',[(side*.023,.586,.937),(side*.206,.586,.937),(side*.174,.804,.620),(side*.023,.804,.620)],.009,r,col,'Radar Glass')
  h._quad_plate('WraparoundCornerGlass',[(side*.219,.59,.928),(side*.339,.584,.753),(side*.287,.802,.529),(side*.178,.802,.619)],.008,r,col,'Radar Glass')
  c.box('PilotSideWindow',(side*.355,.365,.653),(.012,.33,.233),r,col,'Radar Glass')
  c.box('PilotDoorLower',(side*.355,.365,.41),(.013,.38,.17),r,col,'Deck Grey')
  c.box('CabinDoor',(side*.357,-.23,.50),(.018,.39,.39),r,col,'Naval Blue Grey')
  c.box('CabinWindow',(side*.369,-.23,.675),(.012,.28,.18),r,col,'Radar Glass')
  c.box('CabinDoorHandle',(side*.372,-.38,.49),(.018,.065,.025),r,col,'Medical White')
  c.beam('SkidStrut',(side*.27,-.35,.30),(side*.41,-.35,.045),.04,r,col,'Deck Grey')
  c.beam('SkidStrut',(side*.27,.40,.30),(side*.41,.40,.045),.04,r,col,'Deck Grey')
  c.cyl('SkidRail',(side*.41,.01,.025),.025,1.35,r,col,'Deck Grey',10,(math.pi/2,0,0))
  c.beam('SkidUpsweptNose',(side*.41,.685,.025),(side*.41,.82,.09),.04,r,col,'Deck Grey')
 c.frustum('UpperEngineCowling',(0,-.19,.99),(.46,.66),(.32,.53),.18,r,col,'Naval Blue Grey')
 h._loft_fuselage('TailBoom',[(-.65,.145,.45,.73),(-1.20,.09,.57,.77),(-1.93,.045,.70,.81),(-2.12,.032,.74,.84)],r,col)
 c.box('HorizontalTailplane',(0,-1.40,.748),(.87,.23,.035),r,col,'Naval Blue Grey')
 for side in (-1,1):c.box('TailplaneTip',(side*.405,-1.40,.75),(.06,.23,.04),r,col,'Medical White')
 # Raised fin and outboard rotor. Stabilizer ends well ahead of the swept disk.
 verts=[(x,y,z) for x in (-.028,.028) for y,z in [(-2.17,.75),(-1.92,.75),(-2.01,1.21),(-2.17,1.24)]]
 c.mesh('TailFin',verts,[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],r,col,'Naval Blue Grey')
 c.cyl('TailRotorAxle',(.093,-2.08,1.04),.029,.186,r,col,'Deck Grey',10,(0,math.pi/2,0))
 tail=c.empty('TailRotor',r,col,(.186,-2.08,1.04))
 for i in range(4):
  a=i*math.pi/2
  c.box('TailRotorBlade',(0,-math.sin(a)*.115,math.cos(a)*.115),(.016,.032,.20),tail,col,'Gunmetal',(a,0,0))
 c.cyl('TailRotorHub',(0,0,0),.042,.03,tail,col,'Medical White',10,(0,math.pi/2,0))
 c.cyl('MainRotorMast',(0,-.16,1.14),.037,.18,r,col,'Deck Grey',10)
 rotor=c.empty('Rotor',r,col,(0,-.16,1.24))
 c.cyl('MainRotorHub',(0,0,.015),.09,.045,rotor,col,'Medical White',12)
 for i in range(4):h._rotor_blade('MainRotorBlade_%02d'%(i+1),rotor,col,i*math.pi/2)
h.build_helicopter=redesign
scene=bpy.context.scene;scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True
scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.14,.19,.22,1)
scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.6
for loc in [(-7,0,15),(9,-3,10)]:
 d=bpy.data.lights.new('Softbox','AREA');d.energy=2200;d.size=10
 o=bpy.data.objects.new('Softbox',d);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new('Review');cam=bpy.data.objects.new('Review',d);scene.collection.objects.link(cam);scene.camera=cam;d.type='ORTHO'
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.view_settings.look='AgX - Medium High Contrast'
def visible(roots):
 for o in bpy.data.objects:
  if o.type=='MESH':o.hide_render=True
 for r in roots:
  for o in children(r):o.hide_render=False
def render(filename,target,position,scale):
 cam.location=position;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
 scene.render.filepath=str(OUT/(filename+'.png'));bpy.ops.render.render(write_still=True)
roots=[];report=[]
source=(OUT.parent/'EscortHelicopters_v18'/'build.py').read_text(encoding='utf8')
tail=source[source.index('def torpedo('):]
tail=tail.replace("(side*.17,-.20,.78)","(side*.17,-.20,1.00)").replace("(side*.17,.14,.81)","(side*.17,.14,1.03)").replace("(side*.17,-.57,.79)","(side*.17,-.57,1.01)")
tail=tail.replace("(0,-.63,.80)","(0,-.59,.95)")
tail=tail.replace("(9,12,11)","(9,14,7)").replace("(5,7,4)","(5,8,3)")
exec(compile(tail,str(OUT/'build.py'),'exec'))
visible([roots[2]]);roots[2].location.x=0
render('T3_Side',(0,-.50,.62),(8,-.50,1.6),3.8)
render('T3_Front',(0,.35,.65),(2,8,2.4),2.5)
render('Tail_Clearance',(0,-1.75,.95),(4,-5,2.5),1.7)
