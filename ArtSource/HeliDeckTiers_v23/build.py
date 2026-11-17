"""Review-only 1x1 miniature naval aviation decks, no helicopter embedded."""
import bpy,sys,math,json
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent
(OUT/'Models').mkdir(parents=True,exist_ok=True)
sys.path.insert(0,'C:/Users/최병욱/Documents/Codex/2026-09-11/referenced-chatgpt-conversation-this-is-an')
import environment_detail_common as c
import miniature_enemy_ships as e
bpy.ops.wm.read_factory_settings(use_empty=True)
c.COLORS.update({'Hull':(.30,.37,.40,1),'Deck':(.105,.15,.17,1),'Mark':(.78,.82,.77,1),'Blue':(.12,.48,.63,1),'Dark':(.04,.065,.075,1),'Safety':(.79,.53,.13,1)})
c.setup();roots=[];report=[]
for tier,height in enumerate((.22,.43,.66),1):
 name='MOD_HeliDeck'+('' if tier==1 else '_U'+str(tier-1))
 col=c.collection(name);r=c.empty(name,None,col)
 e._chamfer_box('Foundation',(0,0,.06),(1.8,1.8,.12),r,col,'Hull',.10)
 e._chamfer_box('AviationServiceHull',(0,0,(height+.10)/2),(1.65,1.65,height-.10),r,col,'Hull',.09)
 e._chamfer_box('FlightDeck',(0,0,height-.025),(1.8,1.8,.05),r,col,'Deck',.10)
 z=height+.003
 # Painted safety box and landing H, all flush with deck.
 for side in (-1,1):
  c.box('LandingBoundary',(side*.67,0,z),(.023,1.34,.006),r,col,'Mark')
  c.box('LandingBoundary',(0,side*.67,z),(1.34,.023,.006),r,col,'Mark')
  c.box('HLeg',(side*.20,0,z),(.065,.52,.006),r,col,'Mark')
 c.box('HCrossbar',(0,0,z),(.40,.065,.006),r,col,'Mark')
 for side in (-1,1):
  for p in (-.58,0,.58):
   c.box('RecessedGuideLight',(side*.79,p,height),(.055,.10,.014),r,col,'Blue')
   c.cyl('TieDown',(side*.51,p,z),.023,.005,r,col,'Dark',8)
 for x in (-.47,-.31,-.15,.01,.17,.33,.49):
  c.box('ThresholdStripe',(x,-.79,z),(.085,.065,.007),r,col,'Safety',rot=(0,0,-.35))
 if tier>=2:
  # Service access underneath the unobstructed landing surface.
  c.box('MaintenanceShutter',(0,.829,height*.49),(.89,.018,height-.16),r,col,'Dark')
  for k in range(4+tier):
   c.box('ShutterSlat',(0,.846,.14+k*(height-.19)/(3+tier)),(.86,.025,.018),r,col,'Hull')
  for side in (-1,1):
   c.box('DoorJamb',(side*.49,.85,height*.51),(.065,.045,height-.13),r,col,'Hull')
   c.box('SideServicePanel',(side*.837,0,height*.51),(.025,.73,height-.19),r,col,'Deck')
   c.box('ServicePanelHandle',(side*.858,.16,height*.51),(.018,.11,.025),r,col,'Mark')
   c.box('RoleStripe',(side*.855,-.12,height-.10),(.014,.92,.035),r,col,'Blue')
   for yy in (-.55,-.46,-.37):
    c.box('AirIntake',(side*.85,yy,height*.49),(.018,.035,height-.22),r,col,'Dark')
 if tier==3:
  # Larger under-deck maintenance suite and a flush lift outline, not an animated lift.
  for side in (-1,1):
   c.box('LiftSeam',(side*.57,0,z+.001),(.012,1.17,.005),r,col,'Dark')
   c.box('LiftSeam',(0,side*.585,z+.001),(1.15,.012,.005),r,col,'Dark')
   for yy in (-.60,.60):
    c.box('StructuralButtress',(side*.835,yy,.33),(.10,.13,.49),r,col,'Hull')
   c.box('EquipmentCabinet',(side*.55,-.84,.29),(.33,.055,.35),r,col,'Deck')
   c.box('CabinetFace',(side*.55,-.874,.29),(.26,.012,.25),r,col,'Hull')
  c.box('RescueEquipmentLocker',(0,-.85,.28),(.44,.05,.32),r,col,'Hull')
  c.box('RescueStripe',(0,-.883,.28),(.32,.01,.05),r,col,'Safety')
 c.empty('LandingSpot',r,col,(0,0,height+.01))
 c.finish(r)
 objs=[r]+list(c.descendants(r));saved={o:o.name for o in bpy.data.objects}
 for i,o in enumerate(bpy.data.objects):o.name='temp_%d'%i
 for o in objs:o.name=o['export_name']
 bpy.ops.object.select_all(action='DESELECT')
 for o in objs:o.select_set(True)
 bpy.context.view_layer.objects.active=r
 bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/(name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_NONE',bake_anim=False)
 for i,o in enumerate(bpy.data.objects):o.name='restore_%d'%i
 for o,n in saved.items():o.name=n
 report.append({'name':name,'tier':tier,'footprint':[1.8,1.8],'deck_height':height,'tris':c.tris(r),'landing_socket':'LandingSpot','review_only':True})
 roots.append(r)
scene=bpy.context.scene;scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True
scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.14,.19,.22,1)
scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.6
for loc in [(-5,2,9),(6,-3,7)]:
 d=bpy.data.lights.new('Softbox','AREA');d.energy=1100;d.size=7
 ob=bpy.data.objects.new('Softbox',d);scene.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=(-ob.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new('Camera');cam=bpy.data.objects.new('Camera',d);scene.collection.objects.link(cam);scene.camera=cam;d.type='ORTHO'
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1600;scene.render.resolution_y=900;scene.render.resolution_percentage=100;scene.view_settings.look='AgX - Medium High Contrast'
def render(name,scale):
 cam.location=(6,10,9);cam.rotation_euler=(Vector((0,0,.25))-cam.location).to_track_quat('-Z','Y').to_euler();d.ortho_scale=scale
 scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
for i,r in enumerate(roots):r.location.x=(1-i)*2.55
render('HeliDeck_Tiers',7.9)
for i,r in enumerate(roots):
 for rr in roots:
  for o in c.descendants(rr):o.hide_render=rr!=r
 r.location.x=0;render('HeliDeck_T%d'%(i+1),3.0);r.location.x=(1-i)*2.55
for r in roots:
 for o in c.descendants(r):o.hide_render=False
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'HeliDeck_Tiers.blend'))
(OUT/'manifest.json').write_text(json.dumps(report,indent=2))
