"""Build standalone FBX, Blender source, and review images for the island kit."""
import json
from pathlib import Path
import sys

import bpy

OUT = Path(__file__).resolve().parent
STAGE4 = OUT.parent / 'Stage4Ekranoplan_v1'
MILITARY = OUT.parents[1] / 'Assets/_Game/Resources/Environment/Military'

sys.dont_write_bytecode = True
sys.path.insert(0,str(STAGE4))
import build as shared
from geometry import PALETTE, material
sys.path.insert(0,str(OUT))
from derived import launcher, terrain
from props import helipad, pier, ruins, searchlight, sos, supply
from review import render_reviews


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    scene=bpy.context.scene
    scene.name='Island Base Kit'
    scene.unit_settings.system='METRIC'
    scene.unit_settings.scale_length=1
    for folder in ('Models','Preview'):
        (OUT/folder).mkdir(exist_ok=True)
    shared.OUT=OUT
    PALETTE.update({
        'MAT_IslandCliff':(.22,.263,.254), 'MAT_IslandRock':(.105,.146,.157),
        'MAT_IslandGrass':(.20,.312,.18), 'MAT_IslandSand':(.53,.47,.33),
        'MAT_IslandRed':(.54,.093,.052), 'MAT_IslandRust':(.29,.131,.064),
    })
    for name in PALETTE:
        material(name)
    make = [
        ('ENV_BaseBreakwater', lambda a:pier(a,False), 1350),
        ('ENV_BaseMooringPier', lambda a:pier(a,True), 1020),
        ('MIL_Ruins_Battery', lambda a:ruins(a,'Battery'), 530),
        ('MIL_Ruins_Radar', lambda a:ruins(a,'Radar'), 530),
        ('MIL_Ruins_Outpost', lambda a:ruins(a,'Outpost'), 530),
        ('MIL_Helipad', helipad, 470),
        ('MIL_Searchlight', searchlight, 460),
        ('ENV_DriftingSupplyCrate', supply, 290),
        ('NAT_SOSMarker', sos, 380),
        ('MIL_MissileBattery_2',lambda a:launcher(a,MILITARY/'MIL_MissileBattery_2.fbx'),650),
    ]
    assets={}
    manifest={}

    def add(name, model, limit):
        asset=shared.Asset(name,scene)
        model(asset)
        bpy.context.view_layer.update()
        manifest[name]=asset.summarize()
        assert 0 < asset.triangles <= limit, (name,asset.triangles,limit)
        shared.export(asset)
        assets[name]=asset

    for name,model,budget in make:
        add(name,model,budget)
    for size in ('S','M','L'):
        stem='MIL_Island_'+size
        for variant in ('B','C'):
            add(stem+'_'+variant,lambda a:terrain(a,MILITARY/(stem+'.fbx'),variant),3200)
    (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')

    for name,asset in assets.items():
        scene.view_layers[0].layer_collection.children[asset.collection.name].exclude = name != 'ENV_BaseMooringPier'
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            space=area.spaces.active
            space.region_3d.view_distance=31
            space.shading.color_type='MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'IslandBaseFollowup.blend'))
    if 'no-render' not in sys.argv:
        render_reviews(assets,OUT)


if __name__=='__main__':
    main()
