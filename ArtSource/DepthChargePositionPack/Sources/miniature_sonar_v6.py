"""Model-only one-cell towed-sonar kit, inspired by public CAPTAS photographs.

The topology is deliberately compact; this is a game interpretation of a cable
winch, recovery frame and stowed two-ring wet-end, not a scale CAPTAS replica.
Authoring coordinates: +Y bow, +Z up. No Unity files are read or written.
"""
import math
import json
import sys
from pathlib import Path
import bpy
import bmesh
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import environment_detail_common as c

GREY = 'Naval Blue Grey'
DECK = 'Deck Grey'
WHITE = 'Medical White'
TEAL = 'Sensor Glass'
DARK = 'Gunmetal'
YELLOW = 'Warning Yellow'

REFERENCE_URLS = [
    'https://www.thalesdsi.com/captas/',
    'https://www.thalesdsi.com/wp-content/uploads/2026/01/2324_V3_CAPTAS_012026.pdf',
    'https://www.thalesdsi.com/maritime/',
    'https://www.thalesdsi.com/wp-content/uploads/2025/04/AAC_CAPTAS_rig_slide.jpg',
    'https://www.thalesdsi.com/wp-content/uploads/2025/04/Maritime-CAPTAS-slide.jpg',
]


def build_sonar(root, col):
    """Build a clearly readable, physically supported sonar handling module."""
    c.box('FixedSonarFoundation', (0, 0, .055), (1.76, 1.72, .11),
          root, col, DECK)
    # Transverse drum. Its motor and two bearing pedestals stay fixed.
    # The triangular feet reach the deck and the axis passes through both
    # bearings, so the rotating barrel never floats between empty side boxes.
    for side in (-1, 1):
        x = side * .63
        c.box('WinchBearingFoot', (x, -.34, .143), (.30, .56, .066),
              root, col, WHITE)
        c.frustum('WinchBearingStand', (x, -.34, .305),
                  (.27, .46), (.16, .25), .30, root, col, GREY)
        c.cyl('FixedDrumBearing', (x, -.34, .466), .103, .15,
              root, col, DECK, 8, rot=(0, math.pi/2, 0))
    c.box('WinchMotorHousing', (-.728, -.34, .287), (.22, .37, .29),
          root, col, GREY)
    c.cyl('FixedGearbox', (-.739, -.34, .466), .125, .16,
          root, col, WHITE, 8, rot=(0, math.pi/2, 0))
    drum = c.empty('CableDrumPivot', root, col, (0, -.34, .466))
    drum['rotation_axis_blender'] = 'X'
    drum['optional_socket'] = True
    # Profile alternates slightly between wound layers. Closed end caps,
    # not duplicated rings or one-sided tubes, keep the export watertight.
    c.lathe('WoundSonarCableDrum', (0, 0, 0),
            [(-.455, .205), (-.31, .228), (-.15, .215),
             (.15, .215), (.31, .228), (.455, .205)],
            drum, col, DARK, 8, rot=(0, math.pi/2, 0))
    for side in (-1, 1):
        c.cyl('DrumSideFlange', (side*.475, 0, 0), .277, .046,
              drum, col, GREY, 8, rot=(0, math.pi/2, 0))
    c.cyl('DrumAxle', (0, 0, 0), .070, 1.30,
          drum, col, DARK, 8, rot=(0, math.pi/2, 0))

    # A short braced recovery frame directly supports the cable pulley.
    # Kept lower than 1 m so adjacent weapons remain visible on a one-cell deck.
    for side in (-1, 1):
        x = side*.64
        c.frustum('RecoveryFrameLeg', (x, .52, .441),
                  (.15, .20), (.105, .13), .66, root, col, GREY)
        c.beam('FrameDiagonalBrace', (x, .16, .12),
               (x, .52, .735), .045, root, col, WHITE)
    c.box('RecoveryFrameCrossmember', (0, .52, .792),
          (1.38, .135, .094), root, col, GREY)
    c.cyl('PulleyAxle', (0, .52, .843), .034, .23,
          root, col, DARK, 8, rot=(0, math.pi/2, 0))
    c.ring('CableGuidePulley', (0, .52, .843), .062, .017,
           root, col, DECK, 8, 4, rot=(0, math.pi/2, 0))
    for start, end in [
            ((0, -.21, .66), (0, .48, .902)),
            ((0, .48, .902), (0, .59, .852)),
            ((0, .59, .852), (0, .39, .678))]:
        c.beam('SupportedTowCable', start, end, .022,
               root, col, DARK)
    cable_exit = c.empty('CableExit', root, col, (0, .59, .852))
    cable_exit['optional_socket'] = True

    # Compact two-ring wet-end in a deck cradle. The transmitter rings follow
    # the recognizable stacked transverse form in the manufacturer's photos;
    # the neutral paint / teal faces use this project's miniature palette.
    tow = c.empty('StowedTowBody', root, col, (0, .35, 0))
    tow['optional_socket'] = True
    for side in (-1, 1):
        c.box('WetEndCradle', (side*.35, .03, .172),
              (.22, .59, .125), tow, col, DECK)
    c.frustum('WetEndVerticalSpine', (0, .07, .439),
              (.15, .29), (.13, .22), .455, tow, col, YELLOW)
    for i, z in enumerate((.310, .548), 1):
        c.lathe('TransmitterRing_%02d' % i, (0, .095, z),
                [(-.435, .109), (-.36, .137), (.36, .137), (.435, .109)],
                tow, col, WHITE, 8, rot=(0, math.pi/2, 0))
        for side in (-1, 1):
            c.cyl('AcousticFace_%02d' % i,
                  (side*.438, .095, z), .104, .008,
                  tow, col, TEAL, 8, rot=(0, math.pi/2, 0))
    # A small streamlined attachment indicates the submerged sensor package.
    # Its +Y nose is sealed; there is no random sphere or oversized console.
    c.lathe('StreamlinedWetEndPod', (0, .265, .383),
            [(-.13, .065), (-.07, .100), (.10, .094), (.25, .018)],
            tow, col, GREY, 8, rot=(-math.pi/2, 0, 0))
    for side in (-1, 1):
        c.beam('WetEndCradleRestraint', (side*.32, -.17, .23),
               (side*.32, .23, .23), .034, tow, col, DECK)
    c.box('WinchLocalControlFace', (-.749, -.137, .302),
          (.115, .020, .095), root, col, TEAL)
    c.box('WinchSafetyPlaque', (.66, -.582, .233),
          (.12, .008, .055), root, col, YELLOW)
    return {
        'description': 'Miniature towed-sonar winch, recovery frame and stowed two-ring wet-end',
        'features': ['Fixed twin bearing pedestals',
                     'Optional transverse CableDrumPivot',
                     'Low physically braced cable-guide pulley',
                     'Connected tow cable',
                     'Stacked two-ring wet-end secured on a cradle'],
        'footprint_m': [1.8, 1.8],
        'budget': 900,
        'anchor': 'floor_z_0',
        'real_equipment_basis': 'Public CAPTAS winch / handling rig and stacked-ring wet-end photographs',
        'reference_urls': REFERENCE_URLS,
        'reference_fidelity': 'Stylized one-cell interpretation; not an exact CAPTAS replica or functional engineering design',
        'new_optional_sockets': ['CableDrumPivot', 'StowedTowBody', 'CableExit'],
        'existing_runtime_socket_required': False,
        'game_applied': False,
    }


def _standalone_qa():
    """Run with Blender --background --python miniature_sonar_v6.py."""
    import miniature_naval_common as naval
    import build_environment_detail as b
    out = HERE/'tmp'/'sonar_v6_qa'
    out.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    naval.setup()
    col = c.collection('MOD_Sonar_v6')
    root = c.empty('MOD_Sonar_Root', None, col)
    metadata = build_sonar(root, col)
    c.finish(root)
    issues = []
    for ob in c.descendants(root):
        if ob.type != 'MESH':
            continue
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        nonmanifold = sum(not edge.is_manifold for edge in bm.edges)
        zeroarea = sum(face.calc_area() < 1e-9 for face in bm.faces)
        signed_volume = bm.calc_volume(signed=True)
        if nonmanifold or zeroarea or signed_volume <= 0:
            issues.append({'mesh': ob.name, 'nonmanifold': nonmanifold,
                           'zeroarea': zeroarea, 'signed_volume': signed_volume})
        bm.free()
        if any(abs(a) > 1e-6 for a in ob.rotation_euler):
            issues.append({'mesh': ob.name, 'nonzero_rotation': list(ob.rotation_euler)})
        if any(abs(a-1) > 1e-6 for a in ob.scale):
            issues.append({'mesh': ob.name, 'nonunit_scale': list(ob.scale)})
    extent = b.bounds(root)
    triangle_count = c.tris(root)
    if triangle_count > 900:
        issues.append({'triangle_budget_exceeded': triangle_count})
    if abs(extent['z'][0]) > 1e-6:
        issues.append({'floor_z': extent['z'][0]})
    for axis in 'xy':
        if extent[axis][0] < -.9-1e-6 or extent[axis][1] > .9+1e-6:
            issues.append({'footprint': extent})
    report = {'triangles': triangle_count, 'bounds': extent,
              'errors': issues, 'all_passed': not issues, 'metadata': metadata}
    (out/'qa.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    b.export(root, out/'MOD_Sonar.fbx')
    b.PREVIEW = out
    b.lighting()
    preview = c.collection('PREVIEW_Sonar')
    c.box('ReviewGround', (0, 0, -.10), (20, 20, .2), None, preview, DECK)
    b.camera(preview, (3.5, 4.2, 3.1), (0, 0, .43), 3.16,
             (1600, 1250), 'MOD_Sonar')
    bpy.ops.wm.save_as_mainfile(filepath=str(out/'MOD_Sonar.blend'))
    print(json.dumps(report, indent=2))
    if issues:
        raise RuntimeError('Sonar geometry QA failed')


if __name__ == '__main__':
    _standalone_qa()
