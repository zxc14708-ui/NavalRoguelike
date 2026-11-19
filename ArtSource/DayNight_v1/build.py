"""Build the night model pack with Blender 5.2 from the repository checkout."""
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector

OUT = Path(__file__).resolve().parent
PROJECT = OUT.parent.parent
sys.dont_write_bytecode = True
sys.path.insert(0, str(OUT))
sys.path.insert(0, str(OUT.parent/'Stage4Ekranoplan_v1'))

from geometry import Mesh, PALETTE, material, node
from models import SPECS

PALETTE.update({
    'Review water': (.035,.107,.142),
    'Review outline': (.133,.212,.245),
    'Review outline deck': (.267,.343,.369),
    'Review text': (.77,.91,.93),
    'Review measurement': (.42,.70,.73),
})
BOATS = tuple(spec[0] for spec in SPECS[:2])
DIM_TOLERANCE = .035


def create_asset(scene, name, build):
    collection = bpy.data.collections.new(name)
    scene.collection.children.link(collection)
    root = node(name, collection)
    build(collection,root)
    bpy.context.view_layer.update()
    points = [ob.matrix_world@v.co for ob in collection.objects if ob.type=='MESH'
              for v in ob.data.vertices]
    bounds = [Vector([reduction(v[i] for v in points) for i in range(3)])
              for reduction in (min,max)]
    triangles = sum(len(face.vertices)-2 for ob in collection.objects if ob.type=='MESH'
                    for face in ob.data.polygons)
    materials = sorted({mat.name for ob in collection.objects if ob.type=='MESH'
                        for mat in ob.data.materials})
    parts = [dict(name=ob['part_name'],type=ob.type,
                  parent=ob.parent['part_name'] if ob.parent else None,
                  position=[round(n,6) for n in ob.matrix_world.translation],
                  direction=[round(n,6) for n in ob.matrix_world.to_3x3().col[2].normalized()])
             for ob in collection.objects]
    return collection,root,dict(bounds=[list(v) for v in bounds],triangles=triangles,
                                parts=parts,materials=materials)


def verify_contract(name, info, budget, dimensions):
    low,high = info['bounds']
    measured = (high[0]-low[0],high[1]-low[1],low[2],high[2])
    if any(abs(a-b)>DIM_TOLERANCE for a,b in zip(measured,dimensions)):
        raise ValueError(f'{name} measured {measured} against {dimensions}')
    if not budget[0]<=info['triangles']<=budget[1]:
        raise ValueError(f'{name} uses {info["triangles"]} triangles; expected {budget}')
    parts = {p['name']: p for p in info['parts']}
    empty_names = {n for n,p in parts.items() if p['type']=='EMPTY'}
    if name in BOATS:
        names = {'Exhaust','BlastPoint'} if name==BOATS[0] else {
            'Exhaust','SmokePoint','FirePoint','TorpedoLaunchPoint_Port','TorpedoLaunchPoint_Starboard'}
        assert empty_names == names|{name}, (name,empty_names)
        assert 'Enemy Dark' in info['materials']
        assert not set(info['materials'])&{'Deck Grey','Stealth deck','Nonslip deck'}
    else:
        assert empty_names == {name,'FlarePoint' if name=='FX_ParachuteFlare' else 'LampPoint'}
    info['target_bounds'] = list(dimensions)
    info['triangle_budget'] = list(budget)


def export(collection,root):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in collection.objects:
        ob.select_set(True)
    bpy.context.view_layer.objects.active=root
    named = [ob for ob in bpy.data.objects if 'part_name' in ob]
    saved = {ob:ob.name for ob in named}
    for i,ob in enumerate(named):
        ob.name=f'ExportTemp_{i:03}'
    for ob in collection.objects:
        ob.name=ob['part_name']
    try:
        bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/f'{root.name}.fbx'),
            use_selection=True, object_types={'MESH','EMPTY'}, axis_forward='-Z',axis_up='Y',
            apply_unit_scale=True,apply_scale_options='FBX_SCALE_NONE',global_scale=1,
            mesh_smooth_type='FACE',use_triangles=True,bake_anim=False,add_leaf_bones=False)
    finally:
        for i,ob in enumerate(named):
            ob.name=f'RestoreTemp_{i:03}'
        for ob,name in saved.items():
            ob.name=name


def review_scene(name):
    scene=bpy.data.scenes.new(name)
    bpy.context.window.scene=scene
    scene.render.engine='BLENDER_WORKBENCH'
    scene.render.resolution_x=1500
    scene.render.resolution_y=900
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.view_settings.view_transform='Standard'
    scene.view_settings.exposure=.52
    shading=scene.display.shading
    shading.light='STUDIO'
    shading.color_type='MATERIAL'
    shading.show_shadows=False
    shading.show_cavity=True
    shading.cavity_type='WORLD'
    shading.cavity_ridge_factor=1.10
    shading.cavity_valley_factor=1.40
    shading.background_type='WORLD'
    scene.world=bpy.data.worlds.new(name)
    scene.world.color=(.04,.09,.128)
    data=bpy.data.cameras.new('Review Camera')
    data.type='ORTHO'
    data.clip_end=250
    camera=bpy.data.objects.new('Review Camera',data)
    scene.collection.objects.link(camera)
    scene.camera=camera
    return scene,camera


def aim(camera, target, offset, horizontal_span):
    target=Vector(target)
    camera.location=target+Vector(offset)
    camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale=horizontal_span


def title(scene,camera,body,x,y,size,center=False,subdued=False):
    span=camera.data.ortho_scale
    font=bpy.data.curves.new(body,'FONT')
    font.body=body
    font.align_x='CENTER' if center else 'LEFT'
    font.size=span*size
    font.materials.append(material('Review measurement' if subdued else 'Review text'))
    obj=bpy.data.objects.new(body,font)
    scene.collection.objects.link(obj)
    obj.parent=camera
    obj.location=(span*x,span*y,-3)


def write_image(scene,name):
    scene.render.filepath=str(OUT/'Preview'/f'{name}.png')
    bpy.ops.render.render(write_still=True)


def reference_flagship(scene,x):
    outline=[(-1.22,-8),(1.22,-8),(1.65,-7.33),(1.8,-2.0),(1.54,4.36),(.8,6.9),
             (0,8),(-.8,6.9),(-1.54,4.36),(-1.8,-2.0),(-1.65,-7.33)]
    mesh=Mesh()
    mesh.prism(outline,-.013,.013,'Review outline')
    mesh.box((0,-1.55,.044),(2.45,7.2,.06),'Review outline deck',bevel=.18)
    mesh.box((0,3.95,.041),(1.33,1.7,.050),'Review outline deck',bevel=.17)
    mesh.object('16 m flagship silhouette',scene.collection,None,(x,0,0))


def oblique(name,label,col,info,home):
    scene,camera=review_scene(name+'_Oblique')
    scene.collection.children.link(col)
    low,high=map(Vector,info['bounds'])
    radius=max(high-low)
    center=(low+high)/2
    offset=Vector((.74,.89,.55) if name in BOATS else (.80,1.04,.69))*max(radius,5)
    aim(camera,center,offset,max(radius*1.8,1.4))
    bpy.context.view_layer.update()
    view=camera.rotation_euler.to_matrix().inverted()
    points=[view @ (ob.matrix_world@v.co-center) for ob in col.objects if ob.type=='MESH'
            for v in ob.data.vertices]
    lo,hi=[Vector([fn(p[i] for p in points) for i in range(3)]) for fn in (min,max)]
    focus=center+camera.rotation_euler.to_matrix()@Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,0))
    span=max((hi.x-lo.x)/.78,(hi.y-lo.y)/.405)
    focus+=camera.rotation_euler.to_matrix()@Vector((0,span*.017,0))
    aim(camera,focus,offset,span)
    title(scene,camera,label,-.452,.236,.024)
    title(scene,camera,'DAY / NIGHT ASSETS    •    {:,} TRIANGLES'.format(info["triangles"]),
          -.451,.207,.0104,subdued=True)
    write_image(scene,name+'_Oblique')
    bpy.context.window.scene=home
    bpy.data.scenes.remove(scene)


def overhead(name,label,col,info,home):
    scene,camera=review_scene(name+'_Comparison')
    scene.collection.children.link(col)
    width,length,_,height=info['target_bounds']
    x=width/2+3.40
    reference_flagship(scene,x)
    mid=(x+1.8-width/2)/2
    aim(camera,(mid,0,0),(0,-9,26),36)
    title(scene,camera,label,-.452,.236,.024)
    title(scene,camera,'COMBAT VIEW    •    SHARED SCALE',-.451,.207,.0104,subdued=True)
    object_size = f'{length:g} m LONG' if name in BOATS else f'{height-info["target_bounds"][2]:g} m HIGH'
    if name=='FX_ParachuteFlare':
        object_size='1.4 m CANOPY'
    title(scene,camera,object_size,-mid/36,-.22,.011,True)
    title(scene,camera,'FLAGSHIP    16 m',(x-mid)/36,-.22,.011,True)
    write_image(scene,name+'_Comparison')
    bpy.context.window.scene=home
    bpy.data.scenes.remove(scene)


def current_boat(scene, source, x, scale):
    """Use the game's current dedicated surface FBX at the regular prefab scale."""
    original=set(scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(PROJECT/'Assets/_Game/Art/Models'/source))
    imported=set(scene.objects)-original
    roots=[ob for ob in imported if ob.parent not in imported]
    parent=node(source,scene.collection,at=(x,0,0))
    for root in roots:
        root.parent=parent
    parent.scale=(scale,)*3
    bpy.context.view_layer.update()
    return [ob.matrix_world@v.co for ob in imported if ob.type=='MESH' for v in ob.data.vertices]


def boat_lineup(assets,home):
    scene,camera=review_scene('SemiSub Comparison')
    bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection
    locations=(-7.25,-2.5,2.30,7.25)
    labels=['SURFACE SUICIDE BOAT','SEMI-SUB SUICIDE','SEMI-SUB TORPEDO','LIGHT TORPEDO BOAT']
    old_suicide=current_boat(scene,'EnemySuicideBoat.fbx',locations[0],1)
    old_torpedo=current_boat(scene,'EnemyTorpedoBoat.fbx',locations[3],.85)
    sizes=[max(v.y for v in old_suicide)-min(v.y for v in old_suicide),7.5,11,
           max(v.y for v in old_torpedo)-min(v.y for v in old_torpedo)]
    for x,(col,_,_) in zip(locations[1:3],assets[:2]):
        copy=bpy.data.objects.new(col.name+' review',None)
        copy.instance_type='COLLECTION'
        copy.instance_collection=col
        copy.location.x=x
        scene.collection.objects.link(copy)
    # The static review plane clips the portion under the design waterline.
    nodes=Mesh()
    nodes.prism([(-70,-70),(70,-70),(70,70),(-70,70)],-.08,-.004,'Review water')
    nodes.object('Review surface',scene.collection,None)
    scene.display.shading.show_cavity=False
    aim(camera,(0,0,.1),(0,-17,26),25)
    title(scene,camera,'SEMI-SUBMERSIBLES',-.452,.238,.026)
    title(scene,camera,'WATERLINE COMPARISON    •    CURRENT SURFACE BOATS AT PREFAB SCALE',
          -.451,.205,.0104,subdued=True)
    for x,label,size in zip(locations,labels,sizes):
        title(scene,camera,label,x/25,-.207,.0108,True)
        title(scene,camera,f'{size:.1f} m',x/25,-.235,.0104,True,True)
    write_image(scene,'SemiSub_SurfaceBoat_Comparison')
    bpy.context.window.scene=home
    bpy.data.scenes.remove(scene)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    source=bpy.context.scene
    source.name='Day and Night Models'
    source.unit_settings.system='METRIC'
    source.unit_settings.scale_length=1
    for folder in ('Models','Preview'):
        (OUT/folder).mkdir(exist_ok=True)
    manifest={}
    assets=[]
    for name,label,build,budget,dimensions in SPECS:
        col,root,info=create_asset(source,name,build)
        verify_contract(name,info,budget,dimensions)
        manifest[name]=info
        assets.append((col,root,info))
        print(name,info['triangles'],info['bounds'],flush=True)
        export(col,root)
    (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')

    for col,_,_ in assets[1:]:
        bpy.context.view_layer.layer_collection.children[col.name].exclude=True
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            region=area.spaces.active.region_3d
            region.view_distance=9
            region.view_location=(0,0,.08)
            region.view_rotation=Vector((.74,.89,.55)).to_track_quat('Z','Y')
            area.spaces.active.shading.color_type='MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'DayNight.blend'))

    if '--no-render' not in sys.argv:
        for (name,label,*_),(col,root,info) in zip(SPECS,assets):
            oblique(name,label,col,info,source)
            overhead(name,label,col,info,source)
        boat_lineup(assets,source)


if __name__=='__main__':
    main()

