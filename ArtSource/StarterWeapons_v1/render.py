"""Render the actual exported-source meshes, not generated concept images."""
from pathlib import Path
import bpy
from mathutils import Vector

OUT=Path(__file__).resolve().parent
(OUT/'Preview').mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
roots=[]
for name in ('MOD_BasicNavalGun_L1','MOD_MultipurposeMG_L1'):
    with bpy.data.libraries.load(str(OUT/(name+'.blend')),link=False) as (src,dst):
        dst.objects=src.objects
    for ob in dst.objects:
        if ob: bpy.context.scene.collection.objects.link(ob)
    roots.append(next(o for o in dst.objects if o and o.parent is None))
bpy.context.view_layer.update()

scene=bpy.context.scene
scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=1600;scene.render.resolution_y=1000
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='AgX'
scene.world=bpy.data.worlds.new('Preview World');scene.world.use_nodes=True
scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.16,.20,.23,1)
scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.7

for name,loc,power,size in [('Key',(-3,2,7),1000,5),('Fill',(5,0,4),700,5),('Rim',(0,-5,4),900,4)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    ob=bpy.data.objects.new(name,data);scene.collection.objects.link(ob);ob.location=loc
    ob.rotation_euler=(Vector((0,0,.6))-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.015))
floor=bpy.context.object
mat=bpy.data.materials.new('PreviewOnlyFloor');mat.diffuse_color=(.09,.13,.16,1)
floor.data.materials.append(mat)
data=bpy.data.cameras.new('Preview Camera');cam=bpy.data.objects.new('Preview Camera',data)
scene.collection.objects.link(cam);scene.camera=cam;data.type='ORTHO'

def render(file,target,pos,scale):
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    data.ortho_scale=scale;scene.render.filepath=str(OUT/'Preview'/file)
    bpy.ops.render.render(write_still=True)

for i,r in enumerate(roots):
    for j,other in enumerate(roots):
        for ob in [other]+list(other.children_recursive): ob.hide_render=(i!=j)
    render(('BasicNavalGun' if i==0 else 'MultipurposeMG')+'_Mesh.png',(0,.2,.65),(3.8,5.5,4.5),3.8)
for i,r in enumerate(roots):
    for ob in [r]+list(r.children_recursive): ob.hide_render=False
    r.location.x=-1.45 if i==0 else 1.45
render('StarterWeapons_L1_Mesh.png',(0,.15,.55),(0,9,7.2),6.5)
render('StarterWeapons_L1_Top.png',(0,.2,0),(0,.2,12),6.5)
