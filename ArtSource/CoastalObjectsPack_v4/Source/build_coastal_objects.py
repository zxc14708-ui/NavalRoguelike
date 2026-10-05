"""Build a five-prop add-on; exports and renders stay outside the Unity project."""
import json
import math
import sys
from pathlib import Path
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import environment_detail_common as c
import build_environment_detail as b
from environment_coastal_additions import build_breakwater, build_beach_fishing_gear
from environment_maritime_workboats import build_fishing_boat, build_floating_pontoon
from environment_sea_arch import build_sea_rock_arch

OUT = HERE / 'CoastalObjectsPack_v4'
if '--output' in sys.argv:
    OUT = Path(sys.argv[sys.argv.index('--output')+1]).resolve()
b.OUT = OUT
b.MODELS = OUT / 'Models'
b.PREVIEW = OUT / 'Preview'
BUILDERS = {
    'ENV_Breakwater': build_breakwater,
    'ENV_FishingBoat': build_fishing_boat,
    'ENV_FloatingPontoon': build_floating_pontoon,
    'ENV_SeaRockArch': build_sea_rock_arch,
    'ENV_BeachFishingGear': build_beach_fishing_gear,
}


def fitted_scale(bounds, loc, target, res, minimum=1):
    rotation = (Vector(target)-Vector(loc)).to_track_quat('-Z', 'Y')
    right, up = rotation @ Vector((1, 0, 0)), rotation @ Vector((0, 1, 0))
    points = [Vector((x, y, z))-Vector(target) for x in bounds['x']
              for y in bounds['y'] for z in bounds['z']]
    width = 2*max(abs(point.dot(right)) for point in points)
    height = 2*max(abs(point.dot(up)) for point in points)*res[0]/res[1]
    return max(minimum, max(width, height)*1.15)


def preview(entry):
    col = c.collection('PREVIEW_'+entry['id'])
    obj = c.clone(entry['root'], col)
    obj.location = (0, 0, 0)
    bounds = entry['bounds']
    # Underwater foundations are exported but the main view shows the waterline.
    visible = {axis: list(values) for axis, values in bounds.items()}
    visible['z'][0] = max(0, visible['z'][0])
    center = Vector(tuple(sum(visible[axis])/2 for axis in 'xyz'))
    if entry['id'] == 'ENV_BeachFishingGear':
        c.box('PreviewSand', (0, 0, -.14), (40, 40, .28), None, col, 'MAT_IslandSand')
        loc = center+Vector((7, -12, 8))
    else:
        b.water(col, z=0, size=80)
        loc = center+Vector((12, -18, 13))
        if entry['id'] == 'ENV_SeaRockArch':
            loc = center+Vector((10, -20, 9))
    res = (1600, 1100)
    scale = fitted_scale(visible, loc, center, res)
    b.camera(col, loc, center, scale, res, entry['id'])


def overview(entries):
    col = c.collection('PREVIEW_CoastalObjectsOverview')
    b.water(col, z=0, size=140)
    spots = [(-17, 7), (0, 7), (16, 7), (-10, -10), (11, -10)]
    labels = ['BREAKWATER', 'FISHING BOAT', 'FLOATING PONTOON', 'SEA ROCK ARCH', 'BEACH FISHING GEAR']
    for entry, (x, y), title in zip(entries, spots, labels):
        clone = c.clone(entry['root'], col)
        clone.location = (x, y, .025 if entry['id'] == 'ENV_BeachFishingGear' else 0)
        if entry['id'] == 'ENV_BeachFishingGear':
            c.frustum('PreviewBeachPatch', (x, y, -.18), (8, 6), (7.5, 5.5), .40,
                      None, col, 'MAT_IslandSand')
        b.label(title, (x, y-6.6, .03), .46, col)
    b.camera(col, (25, -43, 56), (0, -1, 1.2), 62,
             (2200, 1450), 'Coastal_Objects_Overview')


def harbour_composition(entries):
    """A review-only diorama proves that the separate pieces can combine."""
    col = c.collection('PREVIEW_SmallHarbour')
    b.water(col, z=0, size=100)
    c.frustum('PreviewShoreBank', (0, 9, -.55), (25, 12), (24, 10.5), 2.6,
              None, col, 'MAT_IslandCliff')
    c.box('PreviewHarbourApron', (0, 9, .77), (23.5, 10.2, .12),
          None, col, 'ENV_Concrete')
    chosen = {entry['id']: entry for entry in entries}
    placements = [('ENV_Breakwater', (-9, -1.1, 0), 0),
                  ('ENV_FloatingPontoon', (-1.4, -.15, 0), math.pi),
                  ('ENV_FishingBoat', (3.1, -.1, 0), 0),
                  ('ENV_BeachFishingGear', (4.6, 5.1, .84), 0),
                  ('ENV_SeaRockArch', (16, 3, 0), .35)]
    for asset, loc, angle in placements:
        obj = c.clone(chosen[asset]['root'], col)
        obj.location = loc
        obj.rotation_euler.z = angle
    b.camera(col, (25, -37, 33), (2, 2, 1), 44,
             (2000, 1300), 'Coastal_Harbour_Example')


def main():
    for folder in (OUT, b.MODELS, b.PREVIEW):
        folder.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    c.setup()
    scene = b.lighting()
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1
    entries, lods = [], []
    for asset, builder in BUILDERS.items():
        col = c.collection(asset)
        root = c.empty(asset+'_Root', None, col)
        features = builder(root, col) or {}
        c.finish(root)
        b.merge_static_meshes(root)
        c.finish(root)
        entry = {'id': asset, 'category': 'prop', 'root': root, 'col': col,
                 'features': features, 'triangles': c.tris(root), 'bounds': b.bounds(root)}
        entry['required_sockets'] = sorted({obj.get('export_name', obj.name)
            for obj in c.descendants(root) if obj.type == 'EMPTY' and obj != root})
        b.export(root, b.MODELS/(asset+'.fbx'))
        col.hide_render = True
        entries.append(entry)
        lod = b.make_lod(entry)
        lod['col'].hide_viewport = True
        lod['required_sockets'] = entry['required_sockets']
        lods.append(lod)
    manifest = {'schema_version': 4, 'game_applied': False, 'unit': 'meters',
                'authoring_up': '+Z', 'fbx_forward': '-Z', 'fbx_up': 'Y',
                'prop_types': 5, 'prop_budget': 4500, 'island_budget': 35000,
                'colliders_included': False, 'lod_group_connected': False,
                'runtime_random_placement_connected': False,
                'assets': [{k: v for k, v in entry.items() if k not in ('root', 'col')}
                           for entry in entries+lods]}
    (OUT/'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    if '--skip-preview' not in sys.argv:
        for entry in entries:
            preview(entry)
        overview(entries)
        harbour_composition(entries)
    for i, entry in enumerate(entries):
        entry['root'].location = ((i%3-1)*20, (1-i//3)*20, 0)
        for obj in c.descendants(entry['root']):
            obj.name = obj.get('export_name', obj.name)
        entry['col'].hide_render = False
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.shading.type = 'SOLID'
                area.spaces.active.shading.color_type = 'MATERIAL'
                area.spaces.active.region_3d.view_distance = 65
                area.spaces.active.region_3d.view_location = (0, 10, 1)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CoastalObjectsPack.blend'))
    print(json.dumps({'complete': True, 'base_models': len(entries), 'lods': len(lods),
                      'triangles': {e['id']: e['triangles'] for e in entries+lods}}))


if __name__ == '__main__':
    main()
