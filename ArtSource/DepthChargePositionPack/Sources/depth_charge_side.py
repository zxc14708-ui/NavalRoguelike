"""Model-only, fixed-angle side depth-charge projectors for a naval miniature.

Inspired by the museum ship USS SLATER's historical K-gun photographs. This
is a compact stylized interpretation, not a modern naval equipment replica.
Authoring +Y is bow; +X is the outboard side; +Z is up. No Unity files touched.
"""
import math
import sys
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import environment_detail_common as c

GREY = 'Naval Blue Grey'
DECK = 'Deck Grey'
WHITE = 'Medical White'
DARK = 'Gunmetal'
YELLOW = 'Warning Yellow'
REFERENCE_URLS = ['https://ussslater.org/kgun']
FIRE_DIR = Vector((1., 0., 1.)).normalized()


def build_side_projector(root, col):
    """Two supported projectors, each carrying a transverse roll-type charge."""
    c.box('FixedSideProjectorFoundation', (0, 0, .045),
          (1.76, 1.72, .09), root, col, DECK)
    c.box('OutboardDeckEdgeStripe', (.833, 0, .093),
          (.045, 1.52, .006), root, col, YELLOW)
    groups = []
    launch_points = []
    for idx, y in enumerate((-.42, .42), 1):
        tag = '%02d' % idx
        base = Vector((-.19, y, .23))
        tube_end = base + FIRE_DIR * .44
        # The round breech/expansion chamber sits on a real deck pedestal.
        c.box('ProjectorDeckFoot_' + tag, (-.19, y, .114),
              (.56, .48, .048), root, col, WHITE)
        c.frustum('FixedProjectorPedestal_' + tag, (-.19, y, .182),
                  (.34, .32), (.24, .22), .092, root, col, GREY)
        c.cyl('ExpansionChamber_' + tag, (-.19, y, .233),
              .135, .255, root, col, GREY, 8,
              rot=(-math.pi/2, 0, 0))
        c.cyl('BreechCover_' + tag, (-.19, y-.135, .233),
              .092, .014, root, col, DARK, 6,
              rot=(-math.pi/2, 0, 0))
        barrel_center = (base+tube_end)/2
        barrel_rot = FIRE_DIR.to_track_quat('Z', 'Y').to_euler()
        c.lathe('ShortFixedProjectionBarrel_' + tag, barrel_center,
                [(-.22, .092), (.185, .092), (.22, .113), (.245, .113)],
                root, col, GREY, 8, rot=barrel_rot)
        # Dual diagonal load paths make the fixed assembly look engineered.
        for side, suffix in ((-1, 'Aft'), (1, 'Fore')):
            c.beam('ProjectionBarrelBrace_%s_%s' % (tag, suffix),
                   (-.33, y+side*.17, .14),
                   (.06, y+side*.15, .455), .045, root, col, DECK)

        # The visible loaded charge is deliberately a short drum, with its
        # long axis parallel to the ship (+Y), not a missile facing outboard.
        charge = c.empty('LoadedCharge_' + tag, root, col, (.28, y, .70))
        charge['projectile_group'] = True
        charge['projectile_long_axis_authoring'] = '+Y'
        groups.append('LoadedCharge_' + tag)
        c.lathe('LoadedDepthChargeBody_' + tag, (0, 0, 0),
                [(-.25, .168), (-.226, .19), (.226, .19), (.25, .168)],
                charge, col, GREY, 10, rot=(-math.pi/2, 0, 0))
        for end, suffix in ((-1, 'Aft'), (1, 'Fore')):
            c.cyl('ChargeEndPlate_%s_%s' % (tag, suffix),
                  (0, end*.254, 0), .125, .012, charge, col, WHITE, 6,
                  rot=(-math.pi/2, 0, 0))
            c.cyl('ChargeDepthFuse_%s_%s' % (tag, suffix),
                  (0, end*.264, 0), .039, .018, charge, col, YELLOW, 6,
                  rot=(-math.pi/2, 0, 0))
        for band, yy in enumerate((-.158, .158), 1):
            c.cyl('ChargeRetentionBand_%s_%02d' % (tag, band),
                  (0, yy, 0), .198, .026, charge, col, DECK, 10,
                  rot=(-math.pi/2, 0, 0), smooth=False)
        # K-gun arbor travels with the cylindrical charge. Its lower segment
        # is inside the short fixed barrel and its upper end meets the drum.
        c.beam('LoadedChargeArbor_' + tag,
               (-.245, 0, -.245), (-.045, 0, -.045),
               .077, charge, col, DARK)
        c.box('LoadedChargeSaddle_' + tag, (-.10, 0, -.126),
              (.115, .32, .064), charge, col, DECK,
              rot=(0, -math.pi/4, 0))

        # There is no active elevation or yaw pivot: K-gun interpretation is
        # fixed at 45 degrees, with socket orientation recording the launch.
        launch = c.empty('LaunchPoint_' + tag, root, col, (.28, y, .70))
        launch.rotation_euler = FIRE_DIR.to_track_quat('Y', 'Z').to_euler()
        launch['local_forward_axis'] = '+Y'
        launch['socket_basis'] = 'loaded_charge_center'
        launch['launch_direction_authoring'] = list(FIRE_DIR)
        launch_points.append('LaunchPoint_' + tag)

    outboard = c.empty('OutboardDirection', root, col, (.82, 0, .10))
    outboard.rotation_euler = Vector((1, 0, 0)).to_track_quat('Y', 'Z').to_euler()
    outboard['local_forward_axis'] = '+Y'
    c.empty('ForwardMarker', root, col, (0, .75, .10))
    c.empty('AttachPoint', root, col, (0, 0, .04))
    sockets = groups + launch_points + ['OutboardDirection', 'ForwardMarker', 'AttachPoint']
    return {
        'description': 'Two fixed-angle side projectors carrying separate drum-shaped depth charges',
        'design': 'Historical K-gun-inspired compact miniature: short diagonal projection barrels, load-bearing pedestals, crosswise cylindrical charges and expendable arbors',
        'features': ['Two visibly supported fixed projectors',
                     'Short barrel and breech chamber instead of missile launch tubes',
                     'Drum-shaped charges with fore/aft end plates and depth fuses',
                     'Separate LoadedCharge groups containing only expendable charge/arbor',
                     '45-degree launch outward (+X) and upward (+Z)'],
        'reference_urls': REFERENCE_URLS,
        'reference_fidelity': 'Stylized game interpretation of historical equipment, not an exact or modern naval-equipment replica',
        'required_sockets': sockets,
        'charge_groups': groups,
        'projectile_groups': groups,
        'launch_points': launch_points,
        'launch_direction_authoring': list(FIRE_DIR),
        'launch_socket_basis': 'center_of_loaded_charge',
        'projectile_long_axis_authoring': [0, 1, 0],
        'fixed_angle_degrees': 45,
        'footprint_m': [1.8, 1.8],
        'height_limit_m': .90,
        'anchor': 'floor_z_0',
        'budget': 900,
        'gameplay_modified': False,
        'game_applied': False,
    }


def _standalone_qa():
    import json
    import bpy
    import bmesh
    import build_naval_grey_v7 as grey
    import build_environment_detail as b
    grey.fresh_scene()
    col = c.collection('MOD_DepthChargeSide_Test')
    root = c.empty('MOD_DepthChargeSide_Root', None, col)
    metadata = build_side_projector(root, col)
    c.finish(root)
    issues = []
    for obj in c.descendants(root):
        if obj.type != 'MESH':
            continue
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        if (any(not e.is_manifold for e in bm.edges)
                or any(f.calc_area() < 1e-9 for f in bm.faces)
                or bm.calc_volume(signed=True) <= 0):
            issues.append({'mesh': obj.name, 'topology': 'failed'})
        bm.free()
        if any(abs(v) > 1e-6 for v in obj.rotation_euler):
            issues.append({'mesh': obj.name, 'rotation': list(obj.rotation_euler)})
        if any(abs(v-1) > 1e-6 for v in obj.scale):
            issues.append({'mesh': obj.name, 'scale': list(obj.scale)})
    bounds, triangles = b.bounds(root), c.tris(root)
    if triangles > 900:
        issues.append({'triangle_budget': triangles})
    if abs(bounds['z'][0]) > 1e-6 or bounds['z'][1] > .900001:
        issues.append({'height': bounds['z']})
    if any(bounds[a][0] < -.900001 or bounds[a][1] > .900001 for a in 'xy'):
        issues.append({'footprint': bounds})
    out = HERE/'tmp'/'depth_charge_side_qa'
    out.mkdir(parents=True, exist_ok=True)
    report = {'triangles': triangles, 'bounds': bounds, 'errors': issues,
              'all_passed': not issues, 'metadata': metadata}
    (out/'qa.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    b.export(root, out/'MOD_DepthChargeSide.fbx')
    bpy.ops.wm.save_as_mainfile(filepath=str(out/'MOD_DepthChargeSide.blend'))
    print(json.dumps(report, indent=2))
    if issues:
        raise RuntimeError('Side depth-charge geometry QA failed')


if __name__ == '__main__':
    _standalone_qa()
