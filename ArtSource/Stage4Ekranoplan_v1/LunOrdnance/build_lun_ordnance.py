"""Create D-4 and D-5 game art using the Stage 4 mesh and FBX conventions."""
import argparse
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector

OUT=Path(__file__).resolve().parent
sys.dont_write_bytecode=True
sys.path.insert(0,str(OUT.parent))
import build as stage4
from geometry import DARK,HULL,YELLOW,WHITE,Mesh,material,node

RED='Emergency Red'


def body(asset,profile,band_colors,base):
    # A material band is part of the outer mesh, with no overlapping faces.
    sides=8
    rings=[[(radius*math.cos(i*math.tau/sides),y,radius*math.sin(i*math.tau/sides))
            for i in range(sides)] for y,radius in profile]
    mesh=Mesh()
    mesh.loft(rings,base)
    for index,color in enumerate(band_colors):
        begin=index*sides
        mesh.colors[begin:begin+sides]=[color]*sides
    mesh.colors[-2:]=[band_colors[0],band_colors[-1]]
    mesh.object('Body',asset.collection,asset.root)


def tail_fins(asset,outline,thickness,color):
    mesh=Mesh()
    for i in range(4):
        yaw=i*math.pi/2
        radial=Vector((math.cos(yaw),0,math.sin(yaw)))
        side=Vector((-math.sin(yaw),0,math.cos(yaw)))
        mesh.loft([[radial*r+side*width+Vector((0,y,0)) for r,y in outline]
                   for width in (-thickness/2,thickness/2)],color)
    mesh.object('TailFins',asset.collection,asset.root)


def bomb(asset):
    body(asset,[(-.90,.076),(-.59,.111),(-.25,.19),(.08,.19),
                (.18,.19),(.67,.174),(.84,.122),(.90,.035)],
         [DARK,DARK,DARK,YELLOW,DARK,DARK,DARK],DARK)
    tail_fins(asset,[(.062,-.89),(.30,-.87),(.30,-.71),(.127,-.53)],.025,HULL)
    fuze=node('Fuze',asset.collection,asset.root,(0,.90,0),(0,1,0))
    fuze.empty_display_size=.08


def rocket(asset):
    body(asset,[(-.80,.057),(-.68,.08),(.49,.08),(.63,.062),(.80,.006)],
         [DARK,HULL,RED,RED],HULL)
    tail_fins(asset,[(.048,-.79),(.20,-.775),(.20,-.68),(.071,-.515)],.012,DARK)
    exhaust=node('Exhaust',asset.collection,asset.root,(0,-.80,0),(0,-1,0))
    exhaust.empty_display_size=.08


SPECS=(
    ('PRJ_LunBomb','LUN / FREE-FALL BOMB',bomb,(80,180),(0.60,1.80,0.60),0.38),
    ('PRJ_LunRocket','LUN / UNGUIDED ROCKET',rocket,(60,140),(0.40,1.60,0.40),0.16),
)


def comparison(assets):
    original=bpy.context.window.scene
    scene=bpy.data.scenes.new('Ordnance scale comparison')
    bpy.context.window.scene=scene
    scene.render.engine='BLENDER_WORKBENCH'
    scene.render.resolution_x=1440
    scene.render.resolution_y=900
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.view_settings.view_transform='Standard'
    scene.view_settings.exposure=.54
    scene.world=bpy.data.worlds.new('Deep blue grey')
    scene.world.color=(.028,.072,.094)
    shading=scene.display.shading
    shading.light='STUDIO'
    shading.color_type='MATERIAL'
    shading.background_type='WORLD'
    shading.show_cavity=True
    shading.cavity_type='WORLD'
    shading.cavity_ridge_factor=1.27
    shading.cavity_valley_factor=1.13
    shading.show_specular_highlight=True

    camera=bpy.data.objects.new('Ordnance camera',bpy.data.cameras.new('Ordnance camera'))
    scene.collection.objects.link(camera)
    scene.camera=camera
    camera.location=(0,-2.45,6.0)
    camera.rotation_euler=(-Vector(camera.location)).to_track_quat('-Z','Y').to_euler()
    camera.data.type='ORTHO'
    camera.data.ortho_scale=2.85

    for asset,y in zip(assets,(.33,-.40)):
        instance=bpy.data.objects.new(asset.root.name,None)
        scene.collection.objects.link(instance)
        instance.instance_type='COLLECTION'
        instance.instance_collection=asset.collection
        instance.location=(0,y,0)
        instance.rotation_euler.z=-math.pi/2

    stage4.camera_text(camera,scene,'LUN  /  STAGE 04 ORDNANCE',-1.26,.74,.058)
    stage4.camera_text(camera,scene,'SHARED SCALE   /   GAME ART',-1.26,.662,.030)
    stage4.camera_text(camera,scene,'D-4  /  BOMB   1.8 m',-.90,-.005,.040)
    stage4.camera_text(camera,scene,'D-5  /  ROCKET   1.6 m',-.80,-.662,.040)
    scene.render.filepath=str(OUT/'Preview'/'Lun_Ordnance.png')
    bpy.ops.render.render(write_still=True)
    bpy.context.window.scene=original
    bpy.data.scenes.remove(scene)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--no-render',action='store_true')
    opts=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    stage4.OUT=OUT
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    scene=bpy.context.scene
    scene.name='Stage 4 Models'
    scene.unit_settings.system='METRIC'
    scene.unit_settings.scale_length=1
    material(WHITE)
    for folder in ('Models','Preview'):
        (OUT/folder).mkdir(exist_ok=True)

    manifest={}
    assets=[]
    for name,title,build,budget,envelope,diameter in SPECS:
        asset=stage4.Asset(name,scene)
        asset.title=title
        build(asset)
        bpy.context.view_layer.update()
        spec=asset.summarize()
        assert budget[0]<=asset.triangles<=budget[1],(name,asset.triangles)
        assert all(abs(a-b)<.0001 for a,b in zip(asset.high-asset.low,envelope)),(name,asset.high-asset.low)
        vertices=next(o for o in asset.collection.objects if o.get('part_name')=='Body').data.vertices
        assert abs(max(v.co.x for v in vertices)-min(v.co.x for v in vertices)-diameter)<.0001
        manifest[name]=spec
        stage4.export(asset)
        assets.append(asset)
        print(f'{name}: {asset.triangles} triangles, envelope {tuple(asset.high-asset.low)}')

    (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    bpy.context.view_layer.layer_collection.children[assets[1].collection.name].exclude=True
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            space=area.spaces.active
            space.shading.color_type='MATERIAL'
            space.region_3d.view_distance=2.65
            space.region_3d.view_location=(0,0,0)
            space.region_3d.view_rotation=Vector((.88,1.08,.85)).to_track_quat('Z','Y')
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Stage4LunOrdnance.blend'))
    if not opts.no_render:
        for asset in assets:
            stage4.preview(asset)
        comparison(assets)


if __name__=='__main__':
    main()
