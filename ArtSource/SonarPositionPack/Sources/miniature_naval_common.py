"""One matte-painted diorama palette shared by the miniature naval rebuild."""
import math
import bpy
import environment_detail_common as c

NAVAL_COLORS = {
    'Deck Grey': (.50, .52, .48, 1),
    'Naval Blue Grey': (.76, .79, .78, 1),
    'Sensor Glass': (.10, .40, .43, 1),
    'Medical White': (.86, .86, .82, 1),
    'Emergency Red': (.60, .13, .10, 1),
    'Warning red': (.60, .13, .10, 1),
    'Missile body': (.84, .85, .81, 1),
    'Sam Blue': (.10, .29, .46, 1),
    'Enemy hull grey': (.58, .63, .61, 1),
    'Stealth deck': (.37, .42, .41, 1),
    'Enemy weapon dark': (.10, .13, .13, 1),
    'Submarine green': (.22, .38, .32, 1),
    'Submarine deck': (.19, .25, .23, 1),
    'Submarine dark underside': (.065, .10, .10, 1),
    'Role CAP': (.35, 1., .58, 1),
    'Role ASW': (.22, .68, 1., 1),
    'Role EW': (.83, .37, 1., 1),
    'Role STK': (1., .52, .16, 1),
    'Deck Marking White': (.86, .86, .82, 1),
    'Naval Superstructure': (.76, .79, .78, 1),
}


def setup():
    c.COLORS.update(NAVAL_COLORS)
    c.setup()
    for name, mat in c.M.items():
        shader = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        shader.inputs['Roughness'].default_value = .72 if 'Glass' not in name else .28
        shader.inputs['Metallic'].default_value = .20 if name in ('Gunmetal', 'Enemy weapon dark') else .025


def build_missile(root, col, kind):
    length, diameter, bodymat, nose, bands, wing = {
        'MIS_PlayerRocket': (1.0, .16, 'Missile body', 'Warning red', 'Gunmetal', .16),
        'MIS_PlayerVls': (2.1, .36, 'Enemy weapon dark', 'Gunmetal', 'Warning Yellow', .32),
        'MIS_PlayerSam': (1.3, .15, 'Medical White', 'Sam Blue', 'Deck Grey', .20),
        'MIS_EnemyAsm': (1.6, .28, 'Warning red', 'Enemy weapon dark', 'Deck Grey', .22),
    }[kind]
    radius = diameter/2
    # Local Z lathe rotated so the tapered nose points in authoring +Y.
    # A small non-zero cap closes the nose without duplicated pole triangles.
    c.lathe('SealedMissileBody', (0, 0, 0),
            [(-length/2, radius*.85), (-length*.40, radius),
             (length*.25, radius), (length*.47, radius*.18), (length/2, radius*.06)],
            root, col, bodymat, segments=10, rot=(-math.pi/2, 0, 0))
    c.lathe('NoseColour', (0, 0, 0),
            [(length*.25, radius*1.009), (length*.47, radius*.183),
             (length/2+.003, radius*.061)],
            root, col, nose, segments=10, rot=(-math.pi/2, 0, 0))
    c.cyl('IdentificationBand', (0, -length*.07, 0), radius*1.018,
          length*.047, root, col, bands, segments=10, rot=(math.pi/2, 0, 0))
    def fin(name, y, chord, span, angle):
        # Closed tapered trapezoidal fin, not a one-sided polygon.
        t = .012
        verts = [(radius*.8, y-chord/2, -t/2), (radius+span, y-chord*.34, -t/2),
                 (radius+span*.75, y+chord*.29, -t/2), (radius*.8, y+chord/2, -t/2)]
        verts += [(x, u, t/2) for x, u, _ in verts]
        faces = [(3, 2, 1, 0), (4, 5, 6, 7), (0, 1, 5, 4),
                 (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        c.mesh(name, verts, faces, root, col, bodymat, rot=(0, angle, 0))
    for i in range(4):
        fin('TailFin_'+str(i), -length*.34, length*.23, wing, i*math.pi/2)
        if kind == 'MIS_PlayerSam':
            fin('MidBodyGuidanceWing_'+str(i), length*.06, length*.38, wing*.85, i*math.pi/2)
    c.empty('Exhaust', root, col, (0, -length/2, 0))
    return {'description': 'Closed miniature missile body; no fire or smoke included',
            'nominal_length_m': length, 'body_diameter_m': diameter,
            'anchor': 'geometric_center', 'forward_blender': [0, 1, 0], 'budget': 300}


def build_mount_pedestal(root, col):
    c.lathe('LoadBearingWeaponPedestal', (0, 0, 0),
            [(0, .50), (.10, .50), (.15, .38), (1.05, .38), (1.10, .5), (1.2, .5)],
            root, col, 'Deck Grey', segments=14)
    for a in (0, math.pi/2, math.pi, math.pi*1.5):
        c.box('PedestalInspectionPlate', (.382*math.cos(a), .382*math.sin(a), .59),
              (.027, .16, .32), root, col, 'Naval Blue Grey', rot=(0, 0, a))
    return {'anchor': 'floor_z_0', 'description': 'Fixed 1.2 m weapon elevation mount', 'budget': 600}


def build_anchor(root, col):
    c.ring('HawsePipeRim', (0, 0, 0), .088, .026, root, col, 'Deck Grey',
           segments=12, cross=4, rot=(math.pi/2, 0, 0))
    c.beam('AnchorShank', (0, .032, -.015), (0, .032, -.39), .050,
           root, col, 'Gunmetal')
    c.ring('AnchorCrown', (0, .032, -.38), .051, .016, root, col,
           'Gunmetal', segments=10, cross=4, rot=(math.pi/2, 0, 0))
    for side in (-1, 1):
        c.beam('AnchorArm', (0, .032, -.40), (side*.15, .032, -.37), .045,
               root, col, 'Gunmetal')
        c.beam('AnchorFluke', (side*.15, .032, -.37), (side*.13, .032, -.265),
               .054, root, col, 'Gunmetal')
    return {'anchor': 'hawse_center', 'description': 'Hanging anchor; authoring +Y faces outward', 'budget': 300}


def build_bow_fittings(root, col):
    c.box('WindlassBed', (0, -.15, .065), (.98, .57, .13), root, col, 'Deck Grey')
    for side in (-1, 1):
        c.cyl('WindlassDrum', (side*.30, -.15, .23), .15, .29, root, col,
              'Deck Grey', segments=10)
        c.cyl('WindlassDrumCap', (side*.30, -.15, .39), .18, .07, root, col,
              'Naval Blue Grey', segments=10)
        for i in range(3):
            c.ring('AnchorChain', (side*.3, .08+i*.18, .038), .045, .016,
                   root, col, 'Gunmetal', segments=6, cross=3,
                   rot=(0, 0, i*.2))
        c.beam('HawseChainGuide', (side*.3, .45, .035), (side*.3, .67, .035),
               .024, root, col, 'Gunmetal')
        c.box('HawseCover', (side*.3, .67, .035), (.23, .21, .07),
              root, col, 'Deck Grey')
        c.box('MooringBollardBase', (side*.53, -.57, .035), (.24, .30, .07),
              root, col, 'Deck Grey')
        c.cyl('MooringBollard', (side*.53, -.57, .17), .065, .27,
              root, col, 'Gunmetal', segments=8)
        c.cyl('BollardSafetyBand', (side*.53, -.57, .235), .069, .055,
              root, col, 'Warning Yellow', segments=8)
    return {'anchor': 'floor_z_0', 'description': 'Connected windlass, chains, hawse covers and cleats', 'budget': 600}
