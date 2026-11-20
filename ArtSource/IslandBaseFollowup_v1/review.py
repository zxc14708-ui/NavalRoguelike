"""Render comparison sheets from the same mesh collections as the exported FBX."""
import math

import bpy
from mathutils import Matrix, Vector

from geometry import Mesh


def gallery(out, name, rows, overhead=False, shallow=False, waters=None):
    cells = len(rows[0])
    scene = bpy.data.scenes.new(name)
    bpy.context.window.scene = scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.world = bpy.data.worlds.new('Harbor slate')
    scene.world.color = (.031,.064,.087)
    scene.view_settings.view_transform = 'Standard'
    shading = scene.display.shading
    shading.light = 'STUDIO'
    shading.color_type = 'MATERIAL'
    shading.show_shadows = True
    shading.show_cavity = True
    shading.cavity_type = 'BOTH'
    shading.cavity_ridge_factor = 1.20
    shading.cavity_valley_factor = 1.05
    shading.background_type = 'WORLD'
    data = bpy.data.cameras.new(name)
    camera = bpy.data.objects.new(name,data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.location = (0,0,50) if overhead else (0,-38,27) if shallow else (0,-26,39)
    camera.rotation_euler = (-camera.location).to_track_quat('-Z','Y').to_euler()
    data.type = 'ORTHO'
    data.ortho_scale = cells*10
    rotation = camera.rotation_euler.to_matrix()
    inv = rotation.inverted()
    scene.render.resolution_x = 640*cells
    scene.render.resolution_y = 520*len(rows)
    scene.render.resolution_percentage = 100
    for iy,row in enumerate(rows):
        for ix,(asset,title,yaw) in enumerate(row):
            yaw_matrix = Matrix.Rotation(math.radians(yaw),3,'Z')
            points = [inv @ yaw_matrix @ (obj.matrix_world @ v.co)
                      for obj in asset.collection.objects if obj.type=='MESH' for v in obj.data.vertices]
            left,right = min(v.x for v in points),max(v.x for v in points)
            low,high = min(v.y for v in points),max(v.y for v in points)
            scale = min(8.25/(right-left),5.9/(high-low))
            x = (ix-(cells-1)/2)*10
            y = ((len(rows)-1)/2-iy)*8.125+.5
            instance = bpy.data.objects.new(title,None)
            instance.instance_type = 'COLLECTION'
            instance.instance_collection = asset.collection
            scene.collection.objects.link(instance)
            instance.location = rotation @ Vector((x-scale*(left+right)/2,y-scale*(low+high)/2,0))
            instance.rotation_euler.z = math.radians(yaw)
            instance.scale = (scale,)*3
            if waters and asset.root.name in waters:
                surface = Mesh()
                # A small sheet locates the waterline in the renders.
                height = waters[asset.root.name]
                pad = Vector((4.5/scale,3.6/scale,0))
                surface.prism([(-pad.x,-pad.y),(pad.x,-pad.y),(pad.x,pad.y),(-pad.x,pad.y)],
                              height-.01,height,'Review water')
                water = surface.object('Waterline',scene.collection,None)
                water.location = instance.location
                water.rotation_euler = instance.rotation_euler
                water.scale = instance.scale
            font = bpy.data.curves.new(title,'FONT')
            font.body, font.size, font.align_x = title,.32,'CENTER'
            font.materials.append(bpy.data.materials['MAT_IslandWhite'])
            label = bpy.data.objects.new(title,font)
            scene.collection.objects.link(label)
            label.parent = camera
            label.location = (x,y-3.82,-5)
    scene.render.filepath = str(out/'Preview'/(name+'.png'))
    bpy.ops.render.render(write_still=True)
    bpy.context.window.scene=bpy.data.scenes['Island Base Kit']
    bpy.data.scenes.remove(scene)


def render_reviews(assets,out):
    def cell(name,label,yaw=0):
        return assets[name],label,yaw

    from geometry import PALETTE
    PALETTE['Review water'] = (.036,.166,.216)
    gallery(out,'Piers_and_Supply',[[
        cell('ENV_BaseBreakwater','CONCRETE BREAKWATER',-27),
        cell('ENV_BaseMooringPier','MOORING PIER',-24),
        cell('ENV_DriftingSupplyCrate','DRIFTING SUPPLY CRATE',-22),
    ]],shallow=True,waters={'ENV_DriftingSupplyCrate':0})
    gallery(out,'Facility_Ruins',[[
        cell('MIL_Ruins_Battery','MISSILE BATTERY RUINS',-21),
        cell('MIL_Ruins_Radar','RADAR RUINS',-18),
        cell('MIL_Ruins_Outpost','OUTPOST RUINS',-21),
    ]])
    gallery(out,'Landing_and_Signals',[[
        cell('MIL_Helipad','HELIPAD',-22),
        cell('MIL_Searchlight','SEARCHLIGHT',135),
        cell('NAT_SOSMarker','SOS MARKER',0),
    ]])
    gallery(out,'Island_Variants',[
        [cell(f'MIL_Island_{size}_{variant}',f'{size}  /  COAST {variant}')
         for size in ('S','M','L')] for variant in ('B','C')
    ],overhead=True,waters={name:-.89 for name in assets if name.startswith('MIL_Island_')})
    battery = assets['MIL_MissileBattery_2']
    gallery(out,'Battery_Closed',[[
        (battery,'HATCHES CLOSED',-33),(battery,'REAR CANISTER FACES',156),
    ]])
    # Use distinct open and closed proof images. The source file was saved closed.
    scene = bpy.context.window.scene
    scene.view_layers[0].layer_collection.children[battery.collection.name].exclude = False
    for obj in battery.collection.objects:
        if obj['part_name'].startswith('LauncherHatchPivot_'):
            obj.rotation_euler.x = math.radians(110)
    bpy.context.view_layer.update()
    gallery(out,'Battery_Open',[[(battery,'HATCHES OPEN',-33)]],shallow=True)
