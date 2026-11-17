"""Build current WWII patch B art; leaves Assets and gameplay untouched."""
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from common import *
from weapons import *
from ships import *
from aircraft import *
setup();(OUT/'Models').mkdir(exist_ok=True);(OUT/'Preview').mkdir(exist_ok=True)
assets=[];report=[]
def add(r,kind,content):
 export(r);lo,hi=bounds(r)
 report.append({'file':r['export_name']+'.fbx','kind':kind,'content_id':content,'bounds':[lo,hi],
 'triangles':c.tris(r),'sockets':[o['export_name'] for o in c.descendants(r) if o.type=='EMPTY'],'review_only':True})
 assets.append(r);return r
br=add(bridge(),'module','ww2_mod_bridge_destroyer')
r,col=root('MOD_Ww2DualGun');dual_gun(r,col)
dg=add(r,'module','ww2_mod_dual_purpose_gun')
r,col=root('MOD_Ww2QuadAa');quad_aa(r,col)
aa=add(r,'module','ww2_mod_quad_aa')
r,col=root('MOD_Ww2TorpedoMount');torpedo_mount(r,col)
tt=add(r,'module','ww2_mod_broadside_torpedo')
fast=add(gunboat(True),'enemy','ww2_enemy_fast_gunboat')
gun=add(gunboat(False),'enemy','ww2_enemy_gunboat')
tb=add(aircraft(True),'aircraft','ww2_enemy_torpedo_bomber')
db=add(aircraft(False),'aircraft','ww2_enemy_dive_bomber')
boss=add(cruiser(),'boss','ww2_boss_gunnery_cruiser')
for name,options in [('TOR_Ww2Player',{'torpedo':True}),('TOR_Ww2Air',{'torpedo':True,'air':True}),('BOMB_Ww2',{'bomb':True}),('PRJ_Ww2DualGun',{}),('PRJ_Ww2QuadAa',{'aa':True})]:
 add(projectile(name,**options),'projectile',None)
# Consistent neutral studio. Orthographic views are for art review only.
scene=bpy.context.scene;scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True
scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.12,.17,.20,1)
scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.7
for loc in [(-8,6,15),(10,-8,12)]:
 d=bpy.data.lights.new('Softbox','AREA');d.energy=2100;d.size=9
 ob=bpy.data.objects.new('Softbox',d);scene.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=(-ob.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('ReviewCamera');cam=bpy.data.objects.new('ReviewCamera',data);scene.collection.objects.link(cam);scene.camera=cam;data.type='ORTHO'
scene.render.engine='BLENDER_EEVEE';scene.eevee.taa_render_samples=48
scene.render.resolution_x=1500;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.view_settings.look='AgX - Medium High Contrast'
def visible(roots):
 for r in assets:
  for o in c.descendants(r):o.hide_render=(r not in roots)
def render(name,scale,target=(0,0,.5),position=(7,11,8)):
 cam.location=position;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();data.ortho_scale=scale
 scene.render.filepath=str(OUT/'Preview'/(name+'.png'));bpy.ops.render.render(write_still=True)
for r in assets:
 if r in (br,dg,aa,tt):s={'MOD_Ww2Bridge':7.,'MOD_Ww2DualGun':3.5,'MOD_Ww2QuadAa':3.2,'MOD_Ww2TorpedoMount':3.4}[r['export_name']]
 elif r==boss:s=22
 elif r==gun:s=11
 elif r==fast:s=7.7
 elif r in (tb,db):s=4.3
 else:continue
 visible([r]);render(r['export_name'],s)
# Four module comparison, tier labels are rendered in the contact sheet later.
visible([br,dg,aa,tt])
for r,pos in [(br,(-2.3,0,0)),(dg,(1.8,1.7,0)),(aa,(1.8,-1.7,0)),(tt,(4.4,0,0))]:r.location=pos
render('WWII_Modules',11.2,target=(.5,0,.6),position=(8,12,10))
for r in assets:r.location=(0,0,0)
visible([fast,gun,boss]);fast.location=(-5,4,0);gun.location=(-4,-4.1,0);boss.location=(4,0,0)
render('WWII_EnemyShips',24,target=(0,0,1),position=(14,19,18))
for r in assets:r.location=(0,0,0)
visible([tb,db]);tb.location=(2.1,0,0);db.location=(-2.1,0,0)
render('WWII_Aircraft',8.6,target=(0,0,0),position=(6,10,9))
for r in assets:r.location=(0,0,0)
# Reusable models stay exported separately. This destroyer is only a layout preview.
preview=c.collection('Preview_Destroyer');demo=c.empty('Preview_Destroyer',None,preview)
hull('PreviewPlayerHull',14,1.95,.07,demo,preview,'WW2 Hull',-.56)
demo.location.y=-2
visible([br,dg,aa,tt]);dg.location.y=4;tt.location.y=-4;aa.location.y=-6;aa.rotation_euler.z=math.pi
aft=c.clone(dg,preview);aft.location.y=-8;aft.rotation_euler.z=math.pi
for o in c.descendants(aft):o.hide_render=False
render('WWII_PlayerDestroyer',17.5,target=(0,-2,.5),position=(11,16,13))
for r in assets:r.location=(0,0,0);r.rotation_euler=(0,0,0)
for o in list(preview.objects):bpy.data.objects.remove(o,do_unlink=True)
visible(assets)
# Stage models apart for an editable source file; exports remain at origin.
for i,r in enumerate(assets):r.location=(i*22,0,0)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'WWII_ModePack.blend'))
(OUT/'manifest.json').write_text(json.dumps(report,indent=2))
print('WWII ART PACK COMPLETE:',len(assets),'models')
