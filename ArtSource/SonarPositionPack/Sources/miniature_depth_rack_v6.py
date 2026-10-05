"""Compact, fixed stern depth-charge racks; model-only and no Unity edits.

Morphology reference: USS SLATER's preserved/restored stern racks. The source
describes inclined gravity rails, alternating release detents and local levers.
This is a four-charge game adaptation, not a dimensionally exact MK6 replica.
"""
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import environment_detail_common as c


REFERENCE_URL = 'https://ussslater.org/depth-charge-racks'
REFERENCE_IMAGE = (
    'https://images.squarespace-cdn.com/content/v1/'
    '5f0eedcc6d27416d718eca4f/1596470933862-VJXLF4BPDCLV84RV0HO5/rack_1.jpg'
)


def _rail_z(y):
    # The rack slopes down toward the stern, authoring -Y.
    return .305 + .085 * y


def _gusset(name, x, y, z, parent, col):
    # Closed triangular steel plate, physically touching post and rail.
    t = .014
    verts = [(-t/2, -.09, 0), (-t/2, .09, 0), (-t/2, .09, -.13),
             (t/2, -.09, 0), (t/2, .09, 0), (t/2, .09, -.13)]
    faces = [(2, 1, 0), (3, 4, 5), (0, 1, 4, 3), (1, 2, 5, 4),
             (2, 0, 3, 5)]
    c.mesh(name, verts, faces, parent, col, 'Naval Blue Grey', (x, y, z))


def _charge(name, loc, parent, col):
    # Transverse barrel axis +X, not a longitudinal torpedo tube. End shoulders
    # and metal rims are a single closed shell; no open or duplicated cap faces.
    shell = c.lathe(
        name, loc, [(-.22, .19), (-.20, .21), (.20, .21), (.22, .19)],
        parent, col, 'Deck Grey', segments=8, smooth=False,
        rot=(0, math.pi/2, 0))
    shell.data.materials.append(c.M['Naval Blue Grey'])
    # Caps and short end shoulders are light naval grey; the central wall is
    # a darker matte grey so the stored charges remain legible on grey racks.
    for face in shell.data.polygons:
        face.material_index = 0 if 10 <= face.index < 18 else 1
    c.cyl(name + '_FuzeHub', (loc[0] + .230, loc[1], loc[2]), .055, .020,
          parent, col, 'Warning Yellow', segments=6, smooth=False,
          rot=(0, math.pi/2, 0))


def build_depth_rack(root, col):
    """Build <=1.8 m square / <=.9 m tall compact fixed rack assembly.

    LaunchPoint_01..04 identify the four visible stored charges. They are fixed
    children of the root for compatibility with the existing model contract;
    model production does not implement gravity release or alter ASW gameplay.
    """
    c.box('DepthRackMountPlate', (0, 0, .04), (1.72, 1.70, .08),
          root, col, 'Deck Grey')
    charges = []
    for lane, x in enumerate((-.43, .43), 1):
        prefix = 'Rack_%02d' % lane
        rail_xs = (x-.20, x+.20)
        # The rails, support legs and cross members all meet. A narrow square
        # section is used instead of one-sided L/channel sheet geometry.
        for edge, rail_x in enumerate(rail_xs, 1):
            c.beam(prefix + '_InclinedRail_%02d' % edge,
                   (rail_x, -.78, _rail_z(-.78)),
                   (rail_x, .78, _rail_z(.78)), .065,
                   root, col, 'Naval Blue Grey')
            for station, y in enumerate((-.61, .61), 1):
                top = _rail_z(y)
                c.box(prefix + '_Support_%02d_%02d' % (edge, station),
                      (rail_x, y, (.08+top)/2), (.075, .12, top-.08),
                      root, col, 'Naval Blue Grey')
                # Side posts sit outside each drum's transverse end cap and
                # connect down into the load-bearing rail support.
                guard_x = x + (-.25 if edge == 1 else .25)
                c.beam(prefix + '_GuardPost_%02d_%02d' % (edge, station),
                       (rail_x, y, top-.01), (guard_x, y, .64), .04,
                       root, col, 'Naval Blue Grey')
            c.beam(prefix + '_GuardBeam_%02d' % edge,
                   (x + (-.25 if edge == 1 else .25), -.61, .64),
                   (x + (-.25 if edge == 1 else .25), .61, .64), .035,
                   root, col, 'Naval Blue Grey')
        for station, y in enumerate((-.61, .61), 1):
            c.box(prefix + '_LoadCrossMember_%02d' % station,
                  (x, y, _rail_z(y)-.045), (.48, .07, .065),
                  root, col, 'Naval Blue Grey')
            _gusset(prefix + '_TriangularBrace_%02d' % station,
                    x + .20, y, _rail_z(y)-.04, root, col)
        for position, y in enumerate((-.43, .31), 1):
            # Rails support the cylindrical lower shell; fuze hubs are attached
            # directly to the transverse end caps (never free floating props).
            z = _rail_z(y) + .240
            number = (lane-1)*2 + position
            loc = (x, y, z)
            _charge('DepthCharge_%02d' % number, loc, root, col)
            charges.append(loc)
            c.empty('LaunchPoint_%02d' % number, root, col,
                    (x, y-.10, z-.06))
            # Alternating stops are closed short blocks at the rail surface.
            c.box(prefix + '_ReleaseDetent_%02d' % position,
                  (x, y-.18, _rail_z(y-.18)+.105), (.20, .045, .15),
                  root, col, 'Gunmetal')

    # A connected, starboard local release lever with a simple linkage. The
    # stable pivot is optional for future animation, not required for gameplay.
    c.box('LocalReleasePedestal', (.76, -.57, .30), (.10, .15, .44),
          root, col, 'Naval Blue Grey')
    lever = c.empty('ReleaseLever', root, col, (.76, -.57, .52))
    c.cyl('ReleaseLeverAxle', (0, 0, 0), .035, .10, lever, col,
          'Gunmetal', segments=6, smooth=False, rot=(0, math.pi/2, 0))
    c.beam('ReleaseLeverHandle', (0, 0, 0), (0, -.13, .14), .03,
           lever, col, 'Warning Yellow')
    for lane, x in enumerate((-.43, .43), 1):
        c.beam('ReleaseLinkage_%02d' % lane,
               (.70, -.57, .27), (x, -.67, .27), .023,
               root, col, 'Gunmetal')

    return {
        'description': 'Four visible barrel charges on two connected inclined stern racks; local release lever and retaining detents',
        'design_basis': 'USS SLATER preserved stern inclined gravity depth-charge racks; miniature game adaptation, not exact historical dimensions',
        'anchor': 'floor_z_0',
        'budget': 900,
        'required_sockets': ['LaunchPoint_%02d' % i for i in range(1, 5)],
        'socket_hierarchy': {'LaunchPoint_%02d' % i: root.name for i in range(1, 5)},
        'optional_sockets': ['ReleaseLever'],
        'stored_charge_centres_blender': charges,
        'stern_drop_direction_blender': [0, -1, 0],
        'release_animation_implemented': False,
        'gameplay_modified': False,
        'reference_urls': [REFERENCE_URL, REFERENCE_IMAGE],
        'primary_reference_visual_confirmed': True,
    }


if __name__ == '__main__':
    # Independent smoke check; this only creates a workspace-side .blend when
    # launched deliberately as a script. Imports have no build side effects.
    import json
    from pathlib import Path
    import bpy
    import bmesh
    import miniature_naval_common as naval
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    naval.setup()
    col = c.collection('DepthRackV6Smoke')
    root = c.empty('MOD_AswLauncher_Root', None, col)
    meta = build_depth_rack(root, col)
    c.finish(root)
    verts = [o.matrix_world @ v.co for o in c.descendants(root)
             if o.type == 'MESH' for v in o.data.vertices]
    bounds = [[min(v[i] for v in verts), max(v[i] for v in verts)]
              for i in range(3)]
    errors = []
    for ob in c.descendants(root):
        if ob.type != 'MESH':
            continue
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        if not all(e.is_manifold for e in bm.edges):
            errors.append(ob.name + ': open/nonmanifold mesh')
        if not all(f.calc_area() > 1e-9 for f in bm.faces):
            errors.append(ob.name + ': zero area face')
        if bm.calc_volume(signed=True) <= 1e-9:
            errors.append(ob.name + ': nonpositive signed volume')
        bm.free()
    report = {'bounds': bounds, 'tris': c.tris(root), 'errors': errors,
              'metadata': meta}
    print('DEPTH_RACK_V6_QA ' + json.dumps(report, ensure_ascii=False))
    out = Path(__file__).resolve().parent / 'DepthRackV6_Smoke.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(out))
    assert not errors, errors
    assert c.tris(root) <= meta['budget'], c.tris(root)
    assert bounds[2][0] >= -1e-7 and bounds[2][1] <= .9
    assert max(bounds[0][1]-bounds[0][0], bounds[1][1]-bounds[1][0]) <= 1.8
    if '--render' in sys.argv:
        import build_environment_detail as preview
        preview.PREVIEW = Path(__file__).resolve().parent
        preview.lighting()
        display = c.collection('DepthRackSmokeRender')
        c.box('SmokePreviewFloor', (0, 0, -.035), (20, 20, .07),
              None, display, 'Medical White')
        preview.camera(display, (3, -4, 3), (0, 0, .38), 2.75,
                       (1100, 850), 'DepthRackV6_Smoke')
