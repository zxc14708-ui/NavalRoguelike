"""Colour-only, review-only naval assets. Never writes to the Unity project."""
import copy
import hashlib
import json
import math
import re
import shutil
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import environment_detail_common as c
import miniature_naval_common as naval
import build_asw_refresh_v6 as review

OUT = HERE / 'NavalGreyPack_v7'
review.OUT = OUT
NAVAL_CATEGORIES = {'module', 'enemy_ship', 'escort_ship', 'aviation', 'hull_prop', 'projectile'}
OVERRIDES = {'MOD_Sonar', 'MOD_AswLauncher', 'MOD_RocketLauncher'}

# Art direction, not a claim of a particular navy's official paint standard.
PALETTE = {
    'Naval Blue Grey': (.30, .35, .40, 1),
    'Naval Superstructure': (.30, .35, .40, 1),
    'Enemy hull grey': (.28, .32, .36, 1),
    'Deck Grey': (.14, .18, .21, 1),
    'Stealth deck': (.12, .16, .19, 1),
    'Gunmetal': (.045, .060, .075, 1),
    'Enemy weapon dark': (.055, .070, .085, 1),
    'Sensor Glass': (.035, .14, .17, 1),
    'Radar Glass': (.035, .14, .17, 1),
    'Medical White': (.65, .67, .67, 1),
    'Missile body': (.55, .58, .60, 1),
    'Deck Marking White': (.73, .75, .75, 1),
    'Submarine green': (.09, .13, .16, 1),
    'Submarine deck': (.065, .095, .115, 1),
    'Submarine dark underside': (.035, .05, .065, 1),
    'Warning Yellow': (.72, .47, .085, 1),
    # Role colours are deliberately retained on the existing small accents.
    **{name: colour for name, colour in naval.NAVAL_COLORS.items() if name.startswith('Role ')},
}


def setup_materials():
    c.M.clear()
    naval.setup()
    c.COLORS.update(PALETTE)
    c.setup()
    for name, mat in c.M.items():
        shader = mat.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Roughness'].default_value = .72 if 'Glass' not in name else .28
        shader.inputs['Metallic'].default_value = .20 if name in ('Gunmetal', 'Enemy weapon dark') else .025
        shader.inputs['Alpha'].default_value = 1.


def canonical_material(name):
    return re.sub(r'\.\d{3}$', '', name)


def fresh_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1.
    setup_materials()


def import_asset(path, asset_id, col=None):
    # Avoid Blender appending .001 to repeated pivot names in gallery imports.
    for i, ob in enumerate(bpy.data.objects):
        if 'export_name' in ob:
            ob.name = '__Existing_%d' % i
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path))
    imported = [ob for ob in bpy.data.objects if ob not in before]
    col = col or c.collection(asset_id)
    for ob in imported:
        ob['export_name'] = ob.name
        for old in list(ob.users_collection):
            old.objects.unlink(ob)
        col.objects.link(ob)
        if ob.type == 'MESH':
            for slot in ob.material_slots:
                if slot.material:
                    name = canonical_material(slot.material.name)
                    if name not in c.M:
                        raise ValueError('Unknown source material: ' + name)
                    slot.material = c.M[name]
    roots = [ob for ob in imported if ob.parent is None]
    if len(roots) != 1:
        raise ValueError('Expected one root: ' + asset_id)
    bpy.context.view_layer.update()
    return roots[0], col


def source_assets():
    base_dir = HERE / 'MiniatureFleetPack_v5'
    latest_dir = HERE / 'MiniatureASWRefresh_v6'
    base = json.loads((base_dir / 'manifest.json').read_text(encoding='utf-8'))
    latest = json.loads((latest_dir / 'manifest.json').read_text(encoding='utf-8'))
    overrides = {a['id']: a for a in latest['assets']}
    result = []
    for original in base['assets']:
        if original['category'] not in NAVAL_CATEGORIES:
            continue
        source_dir = latest_dir if original['id'] in OVERRIDES else base_dir
        entry = copy.deepcopy(overrides[original['id']] if original['id'] in OVERRIDES else original)
        entry['source_file'] = str(source_dir / entry['model_file'])
        entry['source_version'] = source_dir.name
        entry['source_sha256'] = hashlib.sha256(Path(entry['source_file']).read_bytes()).hexdigest()
        folder = 'TaskForce' if entry['category'] == 'escort_ship' else 'Naval'
        entry['model_file'] = 'Models/' + folder + '/' + entry['id'] + '.fbx'
        entry['blend_file'] = 'Blender/Assets/' + entry['id'] + '.blend'
        entry['change_scope'] = 'Material colours only; geometry, hierarchy and sockets preserved'
        result.append(entry)
    return result


def save_asset_blend(root, entry):
    extent = review.bounds(root)
    centre = Vector(tuple(sum(extent[a]) / 2 for a in 'xyz'))
    span = max(pair[1] - pair[0] for pair in extent.values())
    direction = Vector((1.2, -1.6, 1.14))
    rotation = (-direction).to_track_quat('-Z', 'Y')
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                space = area.spaces.active
                space.shading.type = 'SOLID'
                space.shading.color_type = 'MATERIAL'
                space.region_3d.view_location = centre
                space.region_3d.view_rotation = rotation
                space.region_3d.view_distance = max(3., span * 1.9)
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / entry['blend_file']))


def configure_render():
    review.lighting()
    # A separate neutral studio floor is not exported with any model.
    mat = c.M['ENV_Concrete']
    mat.diffuse_color = (.43, .46, .48, 1)
    mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value = mat.diffuse_color


def gallery(entries, filename, title, columns=4, step=4.8):
    fresh_scene()
    configure_render()
    col = c.collection('REVIEW_' + filename)
    rows = math.ceil(len(entries) / columns)
    labels, roots = [], []
    for i, entry in enumerate(entries):
        root, _ = import_asset(OUT / entry['model_file'], entry['id'], col)
        extent = review.bounds(root)
        span = max(pair[1] - pair[0] for pair in extent.values())
        thumbnail_scale = step * .68 / span
        root.scale = (thumbnail_scale,) * 3
        root.rotation_euler.z = math.pi
        x = (i % columns - (columns - 1) / 2) * step
        y = ((rows - 1) / 2 - i // columns) * step
        root.location = (x + sum(extent['x']) / 2 * thumbnail_scale,
                         y + sum(extent['y']) / 2 * thumbnail_scale,
                         -extent['z'][0] * thumbnail_scale)
        roots.append(root)
        name = entry['id'].replace('MOD_', '').replace('Enemy', '').replace('PatrolBoat_', '')
        before = set(col.objects)
        review.label(name, (x, y - step * .445, .018), step * .047, col)
        labels.extend(ob for ob in col.objects if ob not in before)
    before = set(col.objects)
    review.label(title + ' / THUMBNAIL SIZES NORMALIZED',
                 (0, rows * step * .53, .018), step * .065, col)
    labels.extend(ob for ob in col.objects if ob not in before)
    bpy.context.view_layer.update()
    points = [ob.matrix_world @ v.co for root in roots for ob in c.descendants(root)
              if ob.type == 'MESH' for v in ob.data.vertices]
    points += [ob.matrix_world @ Vector(v) for ob in labels for v in ob.bound_box]
    extent = {a: [min(p[k] for p in points), max(p[k] for p in points)]
              for k, a in enumerate('xyz')}
    width, depth = columns * step, rows * step
    centre = Vector((0, sum(extent['y']) / 2, .5))
    loc = centre + Vector((0, -depth * .78, max(width, depth) * 1.6))
    res = (2400, 1900 if rows >= 3 else 1350)
    c.box('REVIEW_ONLY_Floor', (0, 0, -.12), (120, 120, .24), None, col, 'ENV_Concrete')
    review.render(col, loc, centre, review.fitted_scale(extent, loc, centre, res, 1.06), res, filename)
    col.hide_render = False
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.region_3d.view_perspective = 'CAMERA'
                area.spaces.active.shading.type = 'MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Blender' / (filename + '_Review.blend')))


def latest_previews(entries):
    fresh_scene()
    configure_render()
    three = []
    for asset in ('MOD_Sonar', 'MOD_AswLauncher', 'MOD_RocketLauncher'):
        entry = next(a for a in entries if a['id'] == asset)
        root, col = import_asset(OUT / entry['model_file'], asset)
        col.hide_render = True
        three.append({**entry, 'root': root, 'col': col})
    for entry in three:
        review.preview(entry)
    review.preview(three[-1], tracking=True)
    review.overview(three)
    # Store gallery camera as the master file's initial view.
    gallery = bpy.data.collections['REVIEW_ThreeModules']
    gallery.hide_render = False
    for col in bpy.data.collections:
        col.hide_viewport = col != gallery
    bpy.context.scene.camera = next(ob for ob in gallery.objects if ob.type == 'CAMERA')
    bpy.context.scene.render.resolution_x = 2300
    bpy.context.scene.render.resolution_y = 1150
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.region_3d.view_perspective = 'CAMERA'
                area.spaces.active.shading.type = 'MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Blender' / 'LatestThreeModules_Review.blend'))


def detail_previews(entries):
    fresh_scene()
    configure_render()
    bridge = next(a for a in entries if a['id'] == 'MOD_Bridge')
    root, col = import_asset(OUT / bridge['model_file'], bridge['id'])
    col.hide_render = True
    review.preview({**bridge, 'root': root, 'col': col})
    fresh_scene()
    configure_render()
    col = c.collection('REVIEW_EscortHero')
    for i, role in enumerate(('CAP', 'ASW', 'EW', 'STK')):
        entry = next(a for a in entries if a['id'] == 'ESC_' + role + '_T3')
        root, _ = import_asset(OUT / entry['model_file'], entry['id'], col)
        extent = review.bounds(root)
        scale = 4.8 / (extent['y'][1] - extent['y'][0])
        root.scale = (scale,) * 3
        root.rotation_euler.z = math.pi
        root.location = ((i - 1.5) * 3.35, sum(extent['y']) / 2 * scale, -extent['z'][0] * scale)
        review.label(role + ' / T3', (root.location.x, -2.9, .018), .22, col)
    review.ground(col)
    bpy.context.view_layer.update()
    review.render(col, (6.5, -10.5, 11.0), (0, 0, .55), 17.2,
                  (2400, 1500), 'Naval_Escort_Hero')
    col.hide_render = False
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Blender' / 'Naval_Escort_Hero_Review.blend'))


def main():
    for directory in ('Models/Naval', 'Models/TaskForce', 'Blender/Assets', 'Preview', 'Sources'):
        (OUT / directory).mkdir(parents=True, exist_ok=True)
    entries = source_assets()
    palette = None
    for entry in entries:
        fresh_scene()
        root, col = import_asset(Path(entry['source_file']), entry['id'])
        review.export(root, OUT / entry['model_file'])
        save_asset_blend(root, entry)
        palette = {name: list(mat.diffuse_color) for name, mat in c.M.items()}
        print('RECOLOURED', entry['id'], flush=True)
    manifest = {'pack': 'NavalGreyPack_v7', 'game_applied': False, 'unity_tested': False,
                'geometry_changed': False, 'asset_count': len(entries),
                'scope': 'Naval assets only; excludes islands and coastal/environment props',
                'authoring_axes': {'forward': '+Y', 'up': '+Z'},
                'export_axes': {'forward': '-Z', 'up': '+Y', 'scalings': 'All Local'},
                'cell_size_m': 2., 'palette': palette, 'assets': entries}
    (OUT / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    latest_previews(entries)
    base = [a for a in entries if a['category'] == 'module' and '_U' not in a['id']]
    gallery(base, 'Naval_Modules', 'NAVAL GREY / EQUIPMENT', columns=4)
    upgrades = [a for a in entries if a['id'].startswith(('MOD_Autocannon', 'MOD_Gun76', 'MOD_CIWS', 'MOD_Magazine'))]
    upgrades.sort(key=lambda a: (a['id'].split('_U')[0], a['id']))
    gallery(upgrades, 'Naval_Upgrades', 'NAVAL GREY / BASE - U1 - U2', columns=3)
    gallery([a for a in entries if a['category'] == 'enemy_ship'],
            'Naval_Enemy_Fleet', 'NAVAL GREY / HOSTILE FLEET', columns=4, step=10.)
    escorts = [a for a in entries if a['category'] == 'escort_ship']
    escorts.sort(key=lambda a: (a['id'].split('_T')[0], a['id']))
    gallery(escorts, 'Naval_Escort_Fleet', 'NAVAL GREY / ESCORTS T0 - T1 - T2 - T3', columns=4, step=9.)
    gallery([a for a in entries if a['category'] in ('aviation', 'projectile', 'hull_prop')],
            'Naval_Aircraft_And_Fittings', 'NAVAL GREY / AIRCRAFT AND FITTINGS', columns=4)
    detail_previews(entries)
    for name in ('build_naval_grey_v7.py', 'build_asw_refresh_v6.py',
                 'environment_detail_common.py', 'miniature_naval_common.py'):
        shutil.copy2(HERE / name, OUT / 'Sources' / name)
    print('COMPLETE', len(entries), OUT, flush=True)


if __name__ == '__main__':
    if '--details-only' in sys.argv:
        detail_previews(json.loads((OUT / 'manifest.json').read_text(encoding='utf-8'))['assets'])
        shutil.copy2(HERE / 'build_naval_grey_v7.py', OUT / 'Sources' / 'build_naval_grey_v7.py')
    else:
        main()
