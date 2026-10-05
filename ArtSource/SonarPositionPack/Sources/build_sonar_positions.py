"""Build three review-only positional sonar modules. Never writes inside Unity."""
import json
import math
import shutil
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import environment_detail_common as c
import build_naval_grey_v7 as grey
import build_asw_refresh_v6 as review
from sonar_position_bow import build_bow_sonar
from sonar_position_tas import build_tas_sonar
from sonar_position_internal import build_internal_sonar

OUT = HERE / 'SonarPositionPack'
if '--output' in sys.argv:
    OUT = Path(sys.argv[sys.argv.index('--output') + 1]).resolve()
review.OUT = OUT

BUILDERS = [
    ('MOD_Sonar_Bow', build_bow_sonar, 'BOW / HULL SENSOR', math.pi),
    ('MOD_Sonar_TAS', build_tas_sonar, 'STERN / TOWED ARRAY', 0),
    ('MOD_Sonar_Internal', build_internal_sonar, 'INBOARD / SONAR ROOM', math.pi),
]


def setup_view(root):
    extent = review.bounds(root)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.shading.type = 'MATERIAL'
                area.spaces.active.region_3d.view_location = (0, 0, extent['z'][1]/2)
                area.spaces.active.region_3d.view_distance = 3.3


def build_models():
    entries = []
    for asset, builder, title, rotation in BUILDERS:
        grey.fresh_scene()
        col = c.collection(asset)
        root = c.empty(asset + '_Root', None, col)
        details = builder(root, col)
        review.unique_names(root)
        c.finish(root)
        entry = {'id': asset, 'category': 'sonar_module', 'model_file': 'Models/' + asset + '.fbx',
                 'blend_file': 'Blender/' + asset + '.blend', 'glb_file': 'Models/' + asset + '.glb',
                 'triangles': c.tris(root), 'bounds': review.bounds(root), 'budget': 900,
                 'footprint_cells': [1, 1], 'features': details,
                 'required_sockets': details['required_sockets']}
        names = {ob['export_name']: ob for ob in c.descendants(root)}
        entry['socket_hierarchy'] = {name: names[name].parent['export_name'] for name in entry['required_sockets']}
        if entry['triangles'] > 900:
            raise ValueError('Triangle budget exceeded: ' + asset + ' ' + str(entry['triangles']))
        review.export(root, OUT / entry['model_file'])
        bpy.ops.object.select_all(action='DESELECT')
        for ob in c.descendants(root):
            ob.select_set(True)
        bpy.context.view_layer.objects.active = root
        bpy.ops.export_scene.gltf(filepath=str(OUT / entry['glb_file']),
            export_format='GLB', use_selection=True, export_animations=False, export_extras=True)
        bpy.ops.object.select_all(action='DESELECT')
        root.select_set(True)
        bpy.context.view_layer.objects.active = root
        setup_view(root)
        bpy.ops.wm.save_as_mainfile(filepath=str(OUT / entry['blend_file']))
        entries.append(entry)
        print('BUILT', asset, entry['triangles'], entry['bounds'], flush=True)
    data = {'pack': 'SonarPositionPack', 'game_applied': False, 'gameplay_implemented': False,
            'unity_tested': False, 'cell_size_m': 2., 'maximum_module_footprint_m': [1.8, 1.8],
            'maximum_height_m': 1., 'authoring_axes': {'forward': '+Y', 'up': '+Z'},
            'export_axes': {'forward': '-Z', 'up': 'Y', 'apply_scalings': 'All Local'},
            'palette': {name: list(mat.diffuse_color) for name, mat in c.M.items()},
            'proposed_game_rules_not_implemented': {
                'bow': {'half_angle_degrees': 70, 'range_multiplier': 1., 'movement_penalty': False},
                'stern_tas': {'coverage_degrees': 360, 'range_multiplier': 1.5,
                              'high_speed_threshold_fraction': .70, 'high_speed_radius_multiplier': .5,
                              'sharp_turn_penalty': 'Not specified; remains a gameplay decision'},
                'inboard': {'coverage_degrees': 360, 'range_multiplier': 'Lower than current; no numeric value supplied'},
            },
            'realism_note': 'Position/range/angle/speed values are supplied game rules, not real sonar specifications. Bow/inboard above-deck appearances are readable miniature/cutaway abstractions of water-coupled equipment.',
            'assets': entries}
    (OUT / 'manifest.json').write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
    return entries


def preview(entry):
    grey.fresh_scene()
    grey.configure_render()
    root, col = grey.import_asset(OUT / entry['model_file'], entry['id'])
    # Review-only facing presents the functional side of each block.
    turn = next(item[3] for item in BUILDERS if item[0] == entry['id'])
    root.rotation_euler.z = turn
    extent = review.bounds(root)
    centre = Vector(tuple(sum(extent[a])/2 for a in 'xyz'))
    review.ground(col)
    loc = centre + Vector((2.55, -3.2, 2.55))
    res = (1450, 1100)
    review.render(col, loc, centre, review.fitted_scale(extent, loc, centre, res, 1.15),
                  res, entry['id'] + '_Preview')
    col.hide_render = False
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Blender' / (entry['id'] + '_Review.blend')))


def overview(entries):
    grey.fresh_scene()
    grey.configure_render()
    col = c.collection('REVIEW_Three_Positional_Sonars')
    review.ground(col)
    for i, entry in enumerate(entries):
        root, _ = grey.import_asset(OUT / entry['model_file'], entry['id'], col)
        root.location.x = (i-1)*3.0
        root.rotation_euler.z = BUILDERS[i][3]
        review.label(BUILDERS[i][2], (root.location.x, -1.18, .016), .155, col)
    review.label('NAVAL GREY / POSITIONAL SONAR MODULES', (0, 1.50, .016), .215, col)
    bpy.context.view_layer.update()
    review.render(col, (3.2, -7.8, 8.8), (0, 0, .37), 10.6, (2300, 1200), 'Sonar_Three_Types_Overview')
    col.hide_render = False
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.region_3d.view_perspective = 'CAMERA'
                area.spaces.active.shading.type = 'MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Blender' / 'Three_Sonars_Review.blend'))


def main():
    for folder in ('Models', 'Blender', 'Preview', 'Sources'):
        (OUT / folder).mkdir(parents=True, exist_ok=True)
    entries = build_models()
    for entry in entries:
        preview(entry)
    overview(entries)
    for filename in ('build_sonar_positions.py', 'sonar_position_bow.py', 'sonar_position_tas.py',
                     'sonar_position_internal.py', 'validate_sonar_positions.py',
                     'environment_detail_common.py', 'miniature_naval_common.py',
                     'build_naval_grey_v7.py', 'build_asw_refresh_v6.py',
                     'miniature_sonar_v6.py', 'miniature_depth_rack_v6.py', 'miniature_rocket_v6.py'):
        source, destination = HERE / filename, OUT / 'Sources' / filename
        if source.resolve() != destination.resolve():
            shutil.copy2(source, destination)
    print('COMPLETE', OUT, flush=True)


if __name__ == '__main__':
    main()
