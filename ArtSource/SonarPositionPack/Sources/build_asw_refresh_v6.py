"""Build and render three review-only modules. Never writes inside Unity."""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import environment_detail_common as c
import miniature_naval_common as naval
from miniature_sonar_v6 import build_sonar
from miniature_depth_rack_v6 import build_depth_rack
from miniature_rocket_v6 import audit_current_prefab, build_current_rocket, DEFAULT_PREFAB

OUT = HERE/'MiniatureASWRefresh_v6'
if '--output' in sys.argv:
    OUT = Path(sys.argv[sys.argv.index('--output')+1]).resolve()
SOURCE = DEFAULT_PREFAB
if '--source-prefab' in sys.argv:
    SOURCE = Path(sys.argv[sys.argv.index('--source-prefab')+1]).resolve()


def bounds(root):
    bpy.context.view_layer.update()
    coords = [ob.matrix_world @ vertex.co for ob in c.descendants(root)
              if ob.type == 'MESH' for vertex in ob.data.vertices]
    return {axis: [min(p[i] for p in coords), max(p[i] for p in coords)]
            for i, axis in enumerate('xyz')}


def export(root, path):
    for i, ob in enumerate(bpy.data.objects):
        if 'export_name' in ob:
            ob.name = '__Reserved_'+str(i)
    objects = c.descendants(root)
    for ob in objects:
        ob.name = ob['export_name']
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True,
        object_types={'MESH', 'EMPTY'}, axis_forward='-Z', axis_up='Y',
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE',
        use_space_transform=True, bake_space_transform=False, add_leaf_bones=False,
        bake_anim=False, path_mode='COPY', mesh_smooth_type='FACE')
    bpy.ops.object.select_all(action='DESELECT')


def unique_names(root):
    counts = {}
    for ob in c.descendants(root):
        name = ob['export_name']
        counts[name] = counts.get(name, 0)+1
        if counts[name] > 1:
            ob['export_name'] = name+'__%02d' % counts[name]


def lighting():
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.world = bpy.data.worlds.new('MiniatureSoftDaylight')
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get('Background')
    bg.inputs['Color'].default_value = (.43, .55, .65, 1)
    bg.inputs['Strength'].default_value = .65
    scene.view_settings.view_transform = 'AgX'
    for name, loc, energy, size in [('SoftDaylight', (14, -18, 30), 2400, 15),
                                    ('SkyFill', (-16, 7, 20), 1500, 18)]:
        data = bpy.data.lights.new(name, 'AREA')
        data.energy, data.shape, data.size = energy, 'DISK', size
        ob = bpy.data.objects.new(name, data)
        scene.collection.objects.link(ob)
        ob.location = loc
        ob.rotation_euler = (-ob.location).to_track_quat('-Z', 'Y').to_euler()
    data = bpy.data.lights.new('Sunlight', 'SUN')
    data.energy, data.angle = 2., .16
    ob = bpy.data.objects.new('Sunlight', data)
    scene.collection.objects.link(ob)
    ob.rotation_euler = (.45, -.4, -.4)
    if hasattr(scene, 'eevee') and hasattr(scene.eevee, 'taa_render_samples'):
        scene.eevee.taa_render_samples = 32


def fitted_scale(extent, loc, target, res, padding=1.15):
    rot = (Vector(target)-Vector(loc)).to_track_quat('-Z', 'Y')
    right, up = rot @ Vector((1, 0, 0)), rot @ Vector((0, 1, 0))
    points = [Vector((x, y, z))-Vector(target) for x in extent['x']
              for y in extent['y'] for z in extent['z']]
    return padding*max(2*max(abs(p.dot(right)) for p in points),
                       2*max(abs(p.dot(up)) for p in points)*res[0]/res[1])


def render(col, loc, target, scale, res, name):
    scene = bpy.context.scene
    data = bpy.data.cameras.new('ReviewCamera')
    ob = bpy.data.objects.new('ReviewCamera', data)
    col.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = (Vector(target)-Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    data.type, data.ortho_scale = 'ORTHO', scale
    scene.camera = ob
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.filepath = str(OUT/'Preview'/(name+'.png'))
    bpy.ops.render.render(write_still=True)
    col.hide_render = True


def ground(col):
    c.box('ReviewGround_ONLY', (0, 0, -.1), (40, 40, .2), None, col, 'ENV_Concrete')


def preview(entry, tracking=False):
    col = c.collection('REVIEW_'+entry['id']+('_Tracking' if tracking else ''))
    root = c.clone(entry['root'], col)
    root.location = (0, 0, 0)
    if tracking:
        by_name = {ob['export_name']: ob for ob in c.descendants(root)}
        by_name['TurretPivot'].rotation_euler.z = math.radians(32)
        by_name['ElevationPivot'].rotation_euler.x = math.radians(26)
    extent = bounds(root)
    centre = Vector(tuple(sum(extent[a])/2 for a in 'xyz'))
    direction = (1.10, 1.55, 1.07) if entry['id'] == 'MOD_RocketLauncher' else (1.20, -1.60, 1.14)
    loc = centre+Vector(direction)*1.8
    ground(col)
    res = (1600, 1120)
    render(col, loc, centre, fitted_scale(extent, loc, centre, res), res,
           entry['id']+('_Tracking' if tracking else '_Preview'))


def label(body, loc, size, col):
    data = bpy.data.curves.new('ReviewText', 'FONT')
    data.body, data.size, data.align_x = body, size, 'CENTER'
    ob = bpy.data.objects.new('ReviewText', data)
    col.objects.link(ob)
    ob.location = loc
    data.materials.append(c.M['Gunmetal'])


def overview(entries):
    col = c.collection('REVIEW_ThreeModules')
    ground(col)
    captions = ['SONAR / TOWED SENSOR', 'DEPTH CHARGE / STERN RACK', 'ROCKET / EXISTING 8 TUBES']
    for i, (entry, caption) in enumerate(zip(entries, captions)):
        x = (i-1)*3.25
        root = c.clone(entry['root'], col)
        root.location = (x, 0, 0)
        if entry['id'] == 'MOD_RocketLauncher':
            # Preview-only turn exposes the original tube mouths.
            root.rotation_euler.z = math.pi
        label(caption, (x, -1.30, .014), .14, col)
    label('MINIATURE / ASW EQUIPMENT REFRESH', (0, 1.58, .016), .24, col)
    render(col, (2.6, -7.5, 9), (0, 0, .35), 11.35,
           (2300, 1150), 'Three_Modules_Overview')


def rocket_compare(entry, audit):
    col = c.collection('REVIEW_RocketPaintComparison')
    ground(col)
    original = {}
    for name, values in audit['source_materials'].items():
        old = bpy.data.materials.new('REVIEW_ONLY_'+name)
        old.diffuse_color = values['base_color']
        old.use_nodes = True
        bs = old.node_tree.nodes.get('Principled BSDF')
        bs.inputs['Base Color'].default_value = values['base_color']
        bs.inputs['Roughness'].default_value = values['roughness']
        bs.inputs['Metallic'].default_value = values['metallic']
        original[name] = old
    for i in range(2):
        root = c.clone(entry['root'], col)
        root.location = ((i-.5)*3., 0, 0)
        root.rotation_euler.z = math.pi
        if i == 0:
            for ob in c.descendants(root):
                if ob.type == 'MESH':
                    ob.data = ob.data.copy()
                    for slot in ob.material_slots:
                        key = 'MAT_gun' if ob['export_name'].startswith(('Tube_', 'Ring')) else 'MAT_hull'
                        slot.material = original[key]
        label('CURRENT PREFAB / COLOUR STUDY' if i == 0 else 'SAME SHAPE / MINIATURE PAINT',
              (root.location.x, -1.36, .014), .145, col)
    render(col, (2.3, -6.6, 6.2), (0, 0, .43), 8.15, (2100, 1150), 'Rocket_Same_Shape_Paint_Comparison')


def main():
    for directory in ('Models', 'Preview', 'Blender', 'Sources'):
        (OUT/directory).mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1.
    naval.setup()
    audit = audit_current_prefab(SOURCE)
    (OUT/'rocket_prefab_source_audit.json').write_text(
        json.dumps(audit, ensure_ascii=False, indent=2), encoding='utf-8')
    entries = []
    builders = [('MOD_Sonar', build_sonar), ('MOD_AswLauncher', build_depth_rack),
                ('MOD_RocketLauncher', lambda root, col: build_current_rocket(root, col, audit))]
    for asset, builder in builders:
        col = c.collection(asset)
        root = c.empty(asset+'_Root', None, col)
        features = builder(root, col)
        unique_names(root)
        c.finish(root)
        entry = {'id': asset, 'category': 'module', 'model_file': 'Models/'+asset+'.fbx',
                 'root': root, 'col': col, 'features': features,
                 'bounds': bounds(root), 'triangles': c.tris(root), 'budget': features.get('budget', 900)}
        sockets = ['TurretPivot', 'ElevationPivot', 'Muzzle']+['LaunchPoint_%02d' % i for i in range(1, 9)] if asset == 'MOD_RocketLauncher' else (
                   ['LaunchPoint_%02d' % i for i in range(1, 5)] if asset == 'MOD_AswLauncher' else ['CableDrumPivot', 'CableExit', 'StowedTowBody'])
        names = {ob['export_name']: ob for ob in c.descendants(root)}
        entry['required_sockets'] = sockets
        entry['socket_hierarchy'] = {name: names[name].parent['export_name'] for name in sockets}
        if entry['triangles'] > entry['budget']:
            raise ValueError('Triangle budget: '+asset)
        export(root, OUT/entry['model_file'])
        col.hide_render = True
        entries.append(entry)
        print('BUILT', asset, entry['triangles'], entry['bounds'], flush=True)
    # Export palette excludes preview-only paint/water/text.
    palette = {name: list(mat.diffuse_color) for name, mat in c.M.items()}
    data = {'pack': 'MiniatureASWRefresh_v6', 'game_applied': False, 'unity_tested': False,
            'authoring_axes': {'forward': '+Y', 'up': '+Z'},
            'export_axes': {'forward': '-Z', 'up': '+Y', 'scalings': 'All Local'},
            'cell_size_m': 2., 'palette': palette,
            'assets': [{key: value for key, value in entry.items() if key not in ('root', 'col')} for entry in entries]}
    (OUT/'manifest.json').write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
    lighting()
    for entry in entries:
        preview(entry)
    preview(entries[-1], tracking=True)
    overview(entries)
    rocket_compare(entries[-1], audit)
    # Re-open the useful combined review when the master Blender file loads.
    gallery = bpy.data.collections['REVIEW_ThreeModules']
    gallery.hide_render = False
    for col in bpy.data.collections:
        col.hide_viewport = col != gallery
    scene = bpy.context.scene
    scene.camera = next(ob for ob in gallery.objects if ob.type == 'CAMERA')
    scene.render.resolution_x, scene.render.resolution_y = 2300, 1150
    scene.render.filepath = str(OUT/'Preview'/'Three_Modules_Overview.png')
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.region_3d.view_perspective = 'CAMERA'
                area.spaces.active.shading.type = 'MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Blender'/'ThreeModules_Review.blend'))
    print('COMPLETE', OUT, flush=True)


if __name__ == '__main__':
    main()
