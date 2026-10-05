"""Export two positional depth-charge models and review renders, outside Unity."""
import json
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
from depth_charge_stern import build_stern_rack
from depth_charge_side import build_side_projector

OUT = HERE / 'DepthChargePositionPack'
if '--output' in sys.argv:
    OUT = Path(sys.argv[sys.argv.index('--output')+1]).resolve()
review.OUT = OUT

BUILDERS = [
    ('MOD_DepthChargeRack', build_stern_rack, 'STERN / ROLL-OFF RACK'),
    ('MOD_DepthChargeProjector', build_side_projector, 'SIDE / DEPTH-CHARGE PROJECTOR'),
]


def view_setup(root):
    extent = review.bounds(root)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.shading.type = 'MATERIAL'
                area.spaces.active.region_3d.view_location = (0, 0, extent['z'][1]/2)
                area.spaces.active.region_3d.view_distance = 3.3


def build_models():
    entries = []
    for asset, builder, title in BUILDERS:
        grey.fresh_scene()
        col = c.collection(asset)
        root = c.empty(asset+'_Root', None, col)
        details = builder(root, col)
        review.unique_names(root)
        c.finish(root)
        entry = {'id': asset, 'category': 'depth_charge_module',
                 'model_file': 'Models/'+asset+'.fbx', 'glb_file': 'Models/'+asset+'.glb',
                 'blend_file': 'Blender/'+asset+'.blend', 'triangles': c.tris(root),
                 'bounds': review.bounds(root), 'budget': 900, 'footprint_cells': [1, 1],
                 'features': details, 'required_sockets': details['required_sockets']}
        by_name = {ob['export_name']: ob for ob in c.descendants(root)}
        entry['socket_hierarchy'] = {name: by_name[name].parent['export_name']
                                     for name in entry['required_sockets']}
        extent = entry['bounds']
        if entry['triangles'] > 900 or any(extent[a][0] < -.90001 or extent[a][1] > .90001 for a in 'xy'):
            raise ValueError('Footprint/triangle budget exceeded: '+asset)
        if abs(extent['z'][0]) > .00001 or extent['z'][1] > .90001:
            raise ValueError('Floor/height budget exceeded: '+asset)
        review.export(root, OUT / entry['model_file'])
        bpy.ops.object.select_all(action='DESELECT')
        for ob in c.descendants(root):
            ob.select_set(True)
        bpy.context.view_layer.objects.active = root
        bpy.ops.export_scene.gltf(filepath=str(OUT / entry['glb_file']), export_format='GLB',
                                  use_selection=True, export_animations=False, export_extras=True)
        bpy.ops.object.select_all(action='DESELECT')
        root.select_set(True)
        bpy.context.view_layer.objects.active = root
        view_setup(root)
        bpy.ops.wm.save_as_mainfile(filepath=str(OUT / entry['blend_file']))
        entries.append(entry)
        print('BUILT', asset, entry['triangles'], extent, flush=True)
    manifest = {
        'pack': 'DepthChargePositionPack', 'game_applied': False, 'gameplay_implemented': False,
        'unity_tested': False, 'cell_size_m': 2., 'maximum_module_footprint_m': [1.8, 1.8],
        'maximum_height_m': .9, 'authoring_axes': {'forward': '+Y', 'up': '+Z'},
        'export_axes': {'forward': '-Z', 'up': 'Y', 'apply_scalings': 'All Local'},
        'palette': {name: list(mat.diffuse_color) for name, mat in c.M.items()},
        'placement_rules_not_implemented': {
            'stern_open_boundary': 'MOD_DepthChargeRack',
            'port_or_starboard_open_boundary': 'MOD_DepthChargeProjector',
            'corner_priority': 'Not specified; choose stern-first or player selection during gameplay implementation',
            'inboard_placement': 'Not specified; should not silently fire through neighboring blocks',
        },
        'integration_note': 'Distinct review-only filenames. Existing MOD_AswLauncher.fbx and Unity prefabs are untouched. Positional resolver, projectile trajectories, damage, cooldown and release animation are not implemented.',
        'socket_direction_contract': 'Blender socket local +Y indicates launch direction; convert with the same model axis mapping used by the existing Unity importer. Do not assume default Transform.forward (+Z) when the project contract is -Z forward.',
        'realism_note': 'Historical roll-off rack and K-gun visual references adapted to the current naval-grey miniature style, not exact dimensions or a claim of modern fleet adoption.',
        'assets': entries,
    }
    (OUT / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    return entries


def preview(entry):
    grey.fresh_scene()
    grey.configure_render()
    root, col = grey.import_asset(OUT / entry['model_file'], entry['id'])
    extent = review.bounds(root)
    centre = Vector(tuple(sum(extent[a])/2 for a in 'xyz'))
    review.ground(col)
    loc = centre+Vector((2.55, -3.2, 2.45))
    res = (1450, 1100)
    review.render(col, loc, centre, review.fitted_scale(extent, loc, centre, res, 1.15),
                  res, entry['id']+'_Preview')
    col.hide_render = False
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Blender' / (entry['id']+'_Review.blend')))


def arrow(col, x, y, direction, length=.5):
    p = Vector((x, y, .015))
    d = Vector((direction[0], direction[1], 0)).normalized()
    end = p+d*length
    c.beam('Review_DirectionLine', p, end-d*.12, .025, None, col, 'Warning Yellow')
    side = Vector((-d.y, d.x, 0))*.085
    for sign in (-1, 1):
        c.beam('Review_DirectionHead', end, end-d*.14+side*sign,
               .025, None, col, 'Warning Yellow')


def overview(entries):
    grey.fresh_scene()
    grey.configure_render()
    col = c.collection('REVIEW_Depth_Charge_Two_Types')
    review.ground(col)
    for i, entry in enumerate(entries):
        root, _ = grey.import_asset(OUT / entry['model_file'], entry['id'], col)
        root.location.x = (i-.5)*3.2
        review.label(BUILDERS[i][2], (root.location.x, -1.44, .016), .15, col)
        if i == 0:
            arrow(col, root.location.x, -1.0, (0, -1), .30)
        else:
            arrow(col, root.location.x+.98, 0, (1, 0), .48)
    review.label('NAVAL GREY / POSITIONAL DEPTH CHARGES', (0, 1.55, .016), .23, col)
    bpy.context.view_layer.update()
    review.render(col, (3.0, -7.6, 8.3), (0, 0, .32), 9.15,
                  (2300, 1400), 'Depth_Charge_Two_Types_Overview')
    col.hide_render = False
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.region_3d.view_perspective = 'CAMERA'
                area.spaces.active.shading.type = 'MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Blender' / 'Two_Depth_Charges_Review.blend'))


def main():
    for name in ('Models', 'Blender', 'Preview', 'Sources'):
        (OUT / name).mkdir(parents=True, exist_ok=True)
    entries = build_models()
    for entry in entries:
        preview(entry)
    overview(entries)
    for filename in ('build_depth_charge_positions.py', 'depth_charge_stern.py', 'depth_charge_side.py',
                     'validate_depth_charge_positions.py', 'miniature_depth_rack_v6.py',
                     'environment_detail_common.py', 'miniature_naval_common.py',
                     'build_naval_grey_v7.py', 'build_asw_refresh_v6.py',
                     'miniature_sonar_v6.py', 'miniature_rocket_v6.py'):
        source, destination = HERE / filename, OUT / 'Sources' / filename
        if source.resolve() != destination.resolve():
            shutil.copy2(source, destination)
    print('COMPLETE', OUT, flush=True)


if __name__ == '__main__':
    main()
