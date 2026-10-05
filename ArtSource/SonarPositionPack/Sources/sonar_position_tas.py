"""Model-only aft-facing, one-cell TAS handling module.

Coordinates: +Y bow, -Y stern, +Z up; unit transforms and a Z=0 deck
foundation.  The wet-end is intentionally separate from the fixed cradle.
This is a miniature game interpretation of public CAPTAS handling equipment,
not a scale replica, engineering design, or implementation of game balance.
"""
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import environment_detail_common as c

GREY = 'Naval Blue Grey'
DECK = 'Deck Grey'
WHITE = 'Medical White'
TEAL = 'Sensor Glass'
DARK = 'Gunmetal'
YELLOW = 'Warning Yellow'

REFERENCE_URLS = ['https://www.thalesdsi.com/captas/']


def build_tas_sonar(root, col):
    """Create a stern-facing cable drum, recovery frame and two-ring wet-end."""
    c.box('FixedSonarFoundation', (0, 0, .055), (1.76, 1.72, .11),
          root, col, DECK)

    # Foredeck winch: feet, triangular pedestals and coaxial bearings visibly
    # reach the deck. Only the drum, its flanges and axle belong to the pivot.
    for side, suffix in ((-1, 'Port'), (1, 'Starboard')):
        x = side * .63
        c.box('WinchBearingFoot_' + suffix, (x, .34, .143),
              (.30, .56, .066), root, col, WHITE)
        c.frustum('WinchBearingStand_' + suffix, (x, .34, .305),
                  (.27, .46), (.16, .25), .30, root, col, GREY)
        c.cyl('FixedDrumBearing_' + suffix, (x, .34, .466),
              .103, .15, root, col, DECK, 8, rot=(0, math.pi/2, 0))
    c.box('WinchMotorHousing', (-.728, .34, .287), (.22, .37, .29),
          root, col, GREY)
    c.cyl('FixedGearbox', (-.739, .34, .466), .125, .16,
          root, col, WHITE, 8, rot=(0, math.pi/2, 0))
    drum = c.empty('CableDrumPivot', root, col, (0, .34, .466))
    drum['rotation_axis_blender'] = 'X'
    c.lathe('WoundSonarCableDrum', (0, 0, 0),
            [(-.455, .205), (-.31, .228),
             (.31, .228), (.455, .205)],
            drum, col, DARK, 8, rot=(0, math.pi/2, 0))
    for side, suffix in ((-1, 'Port'), (1, 'Starboard')):
        c.cyl('DrumSideFlange_' + suffix, (side*.475, 0, 0),
              .277, .046, drum, col, GREY, 8, rot=(0, math.pi/2, 0))
    c.cyl('DrumAxle', (0, 0, 0), .070, 1.30,
          drum, col, DARK, 8, rot=(0, math.pi/2, 0))

    # Aft handling frame. Its bracing closes the load path from the upper
    # cable-guide pulley to the foundation; no unsupported half-mast.
    for side, suffix in ((-1, 'Port'), (1, 'Starboard')):
        x = side*.64
        c.frustum('RecoveryFrameLeg_' + suffix, (x, -.52, .441),
                  (.15, .20), (.105, .13), .66, root, col, GREY)
        c.beam('FrameDiagonalBrace_' + suffix, (x, -.16, .12),
               (x, -.52, .735), .045, root, col, WHITE)
    c.box('RecoveryFrameCrossmember', (0, -.52, .792),
          (1.38, .135, .094), root, col, GREY)
    c.cyl('PulleyAxle', (0, -.52, .843), .034, .23,
          root, col, DARK, 8, rot=(0, math.pi/2, 0))
    c.ring('CableGuidePulley', (0, -.52, .843), .062, .017,
           root, col, DECK, 8, 4, rot=(0, math.pi/2, 0))
    for i, (start, end) in enumerate([
            ((0, .21, .66), (0, -.48, .902)),
            ((0, -.48, .902), (0, -.59, .852)),
            ((0, -.59, .852), (0, -.82, .773)),
            ((0, -.82, .773), (0, -.80, .727))], 1):
        c.beam('SupportedTowCable_%02d' % i, start, end, .022,
               root, col, DARK)
    # A compact roller at the stern edge makes the outlet unambiguously aft.
    for side, suffix in ((-1, 'Port'), (1, 'Starboard')):
        c.box('AftFairleadFoot_' + suffix, (side*.108, -.795, .44),
              (.055, .095, .66), root, col, GREY)
    c.cyl('AftCableExitRoller', (0, -.80, .745), .035, .19,
          root, col, DECK, 8, rot=(0, math.pi/2, 0))

    # Two transverse acoustic rings share one deployable wet-end transform.
    # The fixed cradle and retaining bars are NOT children of that transform.
    tow = c.empty('StowedTowBody', root, col, (0, -.35, 0))
    tow['deployable_group'] = True
    tow['deployment_direction_blender'] = [0, -1, 0]
    for side, suffix in ((-1, 'Port'), (1, 'Starboard')):
        c.box('FixedWetEndCradle_' + suffix, (side*.35, -.38, .172),
              (.22, .59, .125), root, col, DECK)
        c.beam('FixedCradleRestraint_' + suffix,
               (side*.32, -.18, .23), (side*.32, -.58, .23),
               .034, root, col, DECK)
    c.frustum('WetEndVerticalSpine', (0, -.07, .439),
              (.15, .29), (.13, .22), .455, tow, col, YELLOW)
    for i, z in enumerate((.310, .548), 1):
        c.lathe('TransmitterRing_%02d' % i, (0, -.095, z),
                [(-.435, .109), (-.36, .137), (.36, .137), (.435, .109)],
                tow, col, WHITE, 8, rot=(0, math.pi/2, 0))
        for side, suffix in ((-1, 'Port'), (1, 'Starboard')):
            c.cyl('AcousticFace_%02d_%s' % (i, suffix),
                  (side*.438, -.095, z), .104, .008,
                  tow, col, TEAL, 6, rot=(0, math.pi/2, 0))
    c.lathe('StreamlinedWetEndPod', (0, -.265, .383),
            [(-.13, .065), (-.07, .100), (.10, .094), (.25, .018)],
            tow, col, GREY, 8, rot=(math.pi/2, 0, 0))
    c.box('WinchLocalControlFace', (-.749, .137, .302),
          (.115, .020, .095), root, col, TEAL)
    c.box('WinchSafetyPlaque', (.66, .582, .233),
          (.12, .008, .055), root, col, YELLOW)

    # Sockets are inert authoring data, not deployment gameplay or animations.
    cable_exit = c.empty('CableExit', root, col, (0, -.84, .727))
    cable_exit.rotation_euler.z = math.pi
    cable_exit['local_forward_axis'] = '+Y'
    cable_exit['forward_blender'] = [0, -1, 0]
    c.empty('TowAttachPoint', root, col, (0, -.84, .727))
    c.empty('SonarOrigin', tow, col, (0, -.095, .429))
    deploy_target = c.empty('TowDeployTarget', root, col, (0, -3.0, -.65))
    deploy_target['guide_only_not_mesh_bounds'] = True
    deploy_target['forward_blender'] = [0, -1, 0]
    c.empty('ForwardMarker', root, col, (0, .78, .12))
    c.empty('AttachPoint', root, col, (0, 0, 0))

    sockets = ['CableDrumPivot', 'CableExit', 'TowAttachPoint',
               'StowedTowBody', 'SonarOrigin', 'TowDeployTarget',
               'ForwardMarker', 'AttachPoint']
    for name in sockets:
        obj = next(o for o in c.descendants(root)
                   if o.get('export_name') == name)
        obj['optional_socket'] = True
    return {
        'description': 'Aft-facing miniature TAS winch and braced recovery frame; separable two-ring wet-end on a fixed cradle',
        'features': ['Fixed twin bearing pedestals and coaxial drum axle',
                     'Optional transverse CableDrumPivot',
                     'Load-bearing diagonal braces and supported guide pulley',
                     'Cable outlet explicitly points sternward (-Y)',
                     'Deployable StowedTowBody; cradle remains fixed',
                     'Two stacked acoustic transmitter rings with teal faces'],
        'footprint_m': [1.8, 1.8],
        'budget': 900,
        'height_limit_m': 1.0,
        'anchor': 'floor_z_0',
        'required_sockets': sockets,
        'new_optional_sockets': sockets,
        'forward_blender': [0, 1, 0],
        'deployment_direction_blender': [0, -1, 0],
        'mesh_bounds_exclude_guide_socket': ['TowDeployTarget'],
        'real_equipment_basis': 'Public Thales CAPTAS winch / handling rig and two-ring wet-end imagery',
        'reference_urls': REFERENCE_URLS,
        'reference_fidelity': 'Stylized one-cell interpretation; not an exact CAPTAS replica or functional engineering design',
        'gameplay_balance_note': '360-degree / 1.5x range / high-speed penalty are user game-design examples, not real equipment specifications',
        'existing_runtime_socket_required': False,
        'game_applied': False,
    }


def _standalone_qa():
    import json
    import bpy
    import bmesh
    import miniature_naval_common as naval
    import build_environment_detail as b
    bpy.ops.wm.read_factory_settings(use_empty=True)
    naval.setup()
    col = c.collection('MOD_SonarTAS_Test')
    root = c.empty('MOD_SonarTAS_Root', None, col)
    metadata = build_tas_sonar(root, col)
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
    extent, triangles = b.bounds(root), c.tris(root)
    if triangles > 900:
        issues.append({'triangle_budget': triangles})
    if abs(extent['z'][0]) > 1e-6 or extent['z'][1] > 1.0:
        issues.append({'height': extent['z']})
    if any(extent[a][0] < -.9-1e-6 or extent[a][1] > .9+1e-6 for a in 'xy'):
        issues.append({'footprint': extent})
    out = HERE/'tmp'/'sonar_tas_position_qa'
    out.mkdir(parents=True, exist_ok=True)
    report = {'triangles': triangles, 'bounds': extent, 'errors': issues,
              'all_passed': not issues, 'metadata': metadata}
    (out/'qa.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    b.export(root, out/'MOD_SonarTAS.fbx')
    bpy.ops.wm.save_as_mainfile(filepath=str(out/'MOD_SonarTAS.blend'))
    print(json.dumps(report, indent=2))
    if issues:
        raise RuntimeError('TAS geometry QA failed')


if __name__ == '__main__':
    _standalone_qa()
