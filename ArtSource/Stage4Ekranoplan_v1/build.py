"""Run with Blender 5.2 in background. All generated files stay in this folder."""
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector

OUT = Path(__file__).resolve().parent
sys.dont_write_bytecode = True
sys.path.insert(0, str(OUT))

from aircraft import lun, light, assault
from vessels import hovercraft, hydrofoil, corvette, battery
from structures import platform, barge, moskit, sandbar
from geometry import Mesh, node

SPECS = [
    ('ENE_Boss4_Lun', 'LUN / CHAPTER BOSS', lun, (0,6000)),
    ('ENE_LightWig', 'LIGHT WIG', light, (450,650)),
    ('ENV_OilPlatform_L', 'OIL PLATFORM / LARGE', lambda a:platform(a,True), (0,1500)),
    ('ENV_OilPlatform_S', 'OIL PLATFORM / SMALL', lambda a:platform(a,False), (0,800)),
    ('ENE_AssaultWig', 'ASSAULT WIG', assault, (1200,1600)),
    ('ENE_Hovercraft', 'LANDING HOVERCRAFT', hovercraft, (1200,1600)),
    ('ENE_Hydrofoil', 'MISSILE HYDROFOIL', hydrofoil, (900,1200)),
    ('ENE_MissileCorvette', 'MISSILE CORVETTE', corvette, (1200,1600)),
    ('ENE_PlatformBattery', 'PLATFORM BATTERY', battery, (300,450)),
    ('ENV_RearmBarge', 'REARM BARGE', barge, (400,600)),
    ('MIS_Moskit', 'MOSKIT / GAME PROJECTILE', moskit, (120,200)),
    ('ENV_Sandbar_Marker', 'SANDBAR MARKER', sandbar, (0,120)),
]


class Asset:
    def __init__(self,name,scene):
        self.collection=bpy.data.collections.new(name)
        scene.collection.children.link(self.collection)
        self.root=node(name,self.collection)
        self.title=''
        self.triangles=0

    def summarize(self):
        objects=list(self.collection.objects)
        meshes=[o for o in objects if o.type=='MESH']
        points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
        self.low=Vector(tuple(min(p[i] for p in points) for i in range(3)))
        self.high=Vector(tuple(max(p[i] for p in points) for i in range(3)))
        self.triangles=sum(len(f.vertices)-2 for o in meshes for f in o.data.polygons)
        return dict(triangles=self.triangles, bounds_blender_m=[list(self.low),list(self.high)],
                    materials=sorted({m.name for o in meshes for m in o.data.materials}),
                    parts=[dict(name=o['part_name'], type=o.type,
                                parent=o.parent['part_name'] if o.parent else None,
                                position_blender_m=[round(v,5) for v in o.matrix_world.translation],
                                axis_blender=[round(v,5) for v in o.matrix_world.to_3x3().col[2].normalized()])
                           for o in objects])


def export(asset):
    bpy.ops.object.select_all(action='DESELECT')
    asset.root.select_set(True)
    bpy.context.view_layer.objects.active=asset.root
    for ob in asset.collection.objects:
        ob.select_set(True)
    # Reset only names of this collection to the required FBX names.
    # Blender object names are global, FBX names are per file.
    saved={ob:ob.name for ob in bpy.data.objects}
    for i,ob in enumerate(saved):ob.name=f'__temp_{i:04}'
    for ob in asset.collection.objects:ob.name=ob['part_name']
    try:
        bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/f'{asset.root.name}.fbx'),
             use_selection=True, object_types={'MESH','EMPTY'}, axis_forward='-Z',axis_up='Y',
             global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE',
             bake_anim=False, mesh_smooth_type='FACE',use_triangles=True,add_leaf_bones=False)
    finally:
        for i,ob in enumerate(saved):ob.name=f'__restore_{i:04}'
        for ob,name in saved.items():ob.name=name


def make_reference(scene,name,x,length,carrier):
    # Silhouettes are scale bars drawn as recognizable hull footprints.
    outline=[(-1,-.50),(1,-.50),(1.25,-.41),(1.25,.19),(.83,.39),(0,.50),
             (-.83,.39),(-1.25,.19),(-1.25,-.41)]
    width=3.9 if not carrier else 7.2
    mesh=Mesh()
    mesh.prism([(x+p*width/2.5,q*length) for p,q in outline],-.03,.016,'Naval Blue Grey')
    if carrier:
        mesh.box((x, -.12*length,.021),(width*.76,.65*length,.012),'Deck Grey')
        mesh.box((x, -.43*length,.036),(.095,.09*length,.015),'MAT_IslandWhite')
    else:
        mesh.box((x,.14*length,.03),(width*.55,.35*length,.035),'Deck Grey')
    return mesh.object(name,scene.collection,None)


def camera_text(camera,scene,body,x,y,size,center=False):
    font=bpy.data.curves.new(body,'FONT')
    font.body=body
    font.size=size
    font.align_x='CENTER' if center else 'LEFT'
    font.materials.append(bpy.data.materials.get('MAT_IslandWhite'))
    obj=bpy.data.objects.new(body,font)
    scene.collection.objects.link(obj)
    obj.parent=camera
    obj.location=(x,y,-8)
    return obj


def preview(asset):
    scene=bpy.data.scenes.new(asset.root.name+'_Review')
    bpy.context.window.scene=scene
    scene.collection.children.link(asset.collection)
    scene.render.engine='BLENDER_WORKBENCH'
    scene.view_settings.view_transform='Standard'
    shading=scene.display.shading
    shading.light='STUDIO'
    shading.color_type='MATERIAL'
    shading.show_shadows=True
    shading.show_cavity=True
    shading.cavity_type='WORLD'
    shading.cavity_ridge_factor=1.25
    shading.cavity_valley_factor=1.05
    shading.show_specular_highlight=True
    scene.view_settings.exposure=.52
    shading.background_type='WORLD'
    scene.world=bpy.data.worlds.new('Dark Teal')
    scene.world.color=(.028,.072,.094)
    scene.render.resolution_x=1500
    scene.render.resolution_y=900
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    data=bpy.data.cameras.new('Review')
    cam=bpy.data.objects.new('Review',data)
    scene.collection.objects.link(cam)
    scene.camera=cam
    data.type='ORTHO'
    data.clip_end=1000

    dims=asset.high-asset.low
    center=(asset.high+asset.low)/2
    radius=max(dims.x,dims.y,dims.z)
    # Put the nose in the foreground and show the tops of the movable parts.
    cam.location=center+Vector((.91,1.16,.91))*max(radius,12)
    cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
    # Fit the projected bounds so tall platforms never lose their submerged legs.
    inv=cam.rotation_euler.to_matrix().inverted()
    silhouette=[inv @ (o.matrix_world @ v.co-center)
                for o in asset.collection.objects if o.type=='MESH' for v in o.data.vertices]
    span_x=max(v.x for v in silhouette)-min(v.x for v in silhouette)
    span_y=max(v.y for v in silhouette)-min(v.y for v in silhouette)
    data.ortho_scale=max(span_x/.75,span_y/.50,radius*1.41)
    title=camera_text(cam,scene,asset.title,-data.ortho_scale*.451,data.ortho_scale*.239,data.ortho_scale*.023)
    detail=camera_text(cam,scene,f'{asset.triangles:,} TRIS   /   STAGE 04',-data.ortho_scale*.451,
                       data.ortho_scale*.207,data.ortho_scale*.0105)
    scene.render.filepath=str(OUT/'Preview'/f'{asset.root.name}_Oblique.png')
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(title,do_unlink=True)
    bpy.data.objects.remove(detail,do_unlink=True)

    # All three footprints share the same linear scale and heading.
    x1=asset.high.x+6
    x2=x1+8.7
    make_reference(scene,'Flagship outline',x1,16,False)
    make_reference(scene,'Hybrid carrier outline',x2,32,True)
    xy_center=(asset.low.x+x2+4)/2
    top_length=max(dims.y,32)
    focus=Vector((xy_center,0,0))
    cam.location=focus+Vector((0,-35,120))
    cam.rotation_euler=(focus-cam.location).to_track_quat('-Z','Y').to_euler()
    data.ortho_scale=max(top_length*2.29,(x2+4-asset.low.x)*1.20)
    camera_text(cam,scene,asset.title,-data.ortho_scale*.451,data.ortho_scale*.243,data.ortho_scale*.022)
    camera_text(cam,scene,'COMBAT VIEW  /  SHARED SCALE',-data.ortho_scale*.451,
                data.ortho_scale*.211,data.ortho_scale*.010)
    for x,label in ((center.x,f'{max(dims.x,dims.y):.1f} m FOOTPRINT'),(x1,'FLAGSHIP  16 m'),
                    (x2,'CARRIER  32 m')):
        camera_text(cam,scene,label,x-xy_center,-data.ortho_scale*.239,data.ortho_scale*.010,True)
    scene.render.filepath=str(OUT/'Preview'/f'{asset.root.name}_Comparison.png')
    bpy.ops.render.render(write_still=True)
    bpy.context.window.scene=bpy.data.scenes['Stage 4 Models']
    bpy.data.scenes.remove(scene)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    scene=bpy.context.scene
    scene.name='Stage 4 Models'
    scene.unit_settings.system='METRIC'
    scene.unit_settings.scale_length=1
    for folder in ('Models','Preview'):
        (OUT/folder).mkdir(exist_ok=True)
    assets=[]
    manifest={}
    for name,title,build,budget in SPECS:
        asset=Asset(name,scene)
        build(asset)
        bpy.context.view_layer.update()
        manifest[name]=asset.summarize()
        if not budget[0] <= asset.triangles <= budget[1]:
            raise ValueError(f'{name}: {asset.triangles} tris outside budget {budget}')
        asset.title=title
        assets.append(asset)
        print(f'{name}: {asset.triangles} tris ({budget[0]}–{budget[1]}), bounds {asset.low[:]} -> {asset.high[:]}',flush=True)
        if 'no-export' not in sys.argv:
            export(asset)
    (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    if 'no-export' in sys.argv:
        return
    view=bpy.context.view_layer
    for asset in assets[1:]:
        view.layer_collection.children[asset.collection.name].exclude=True
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=36
            area.spaces.active.region_3d.view_location=(0,0,1.5)
            area.spaces.active.region_3d.view_rotation=Vector((.63,.79,.63)).to_track_quat('Z','Y')
            area.spaces.active.shading.color_type='MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Stage4Ekranoplan.blend'))
    if 'no-render' not in sys.argv:
        for asset in assets:
            preview(asset)


if __name__=='__main__':
    main()

