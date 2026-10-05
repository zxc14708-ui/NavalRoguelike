"""Closed coastal workboat and floating dock assets, in metres.

Each supplied root stays at its waterline, Z = 0.  The boat points toward +Y.
All fittings are sealed solids and all mesh rotations are baked by the common
helpers.  The builders intentionally do not translate anything by c.SEA.
"""
import math

import environment_detail_common as c


def _prism(name, outline, bottom, top, root, col, material):
    """A closed vertical prism from a counterclockwise XY outline."""
    count = len(outline)
    verts = [(x, y, z) for z in (bottom, top) for x, y in outline]
    faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
    faces.extend((i, (i + 1) % count, (i + 1) % count + count, i + count)
                 for i in range(count))
    return c.mesh(name, verts, faces, root, col, material)


def _cleat(name, root, col, x, y, floor):
    c.box(name + 'Foot', (x, y, floor + .020), (.28, .19, .040),
          root, col, 'Gunmetal')
    c.box(name + 'Stem', (x, y, floor + .093), (.095, .08, .15),
          root, col, 'Gunmetal')
    c.box(name + 'Horns', (x, y, floor + .17), (.39, .055, .065),
          root, col, 'Gunmetal')
    return (x, y, floor + .20)


def _boat_hull(root, col):
    # A real pointed stem, full transom and two hard chines.  A single bow
    # corner in each ring avoids duplicated/zero-area pointed-cap vertices.
    outline = [(-1.13, -3.90), (1.13, -3.90), (1.25, -2.80),
               (1.25, 1.20), (1.07, 2.42), (.60, 3.42), (0, 3.90),
               (-.60, 3.42), (-1.07, 2.42), (-1.25, 1.20),
               (-1.25, -2.80)]
    count = len(outline)
    layers = [(.54, .89, -.65), (.94, .96, -.04), (1., 1., .67)]
    verts = [(x * sx, y * sy, z) for sx, sy, z in layers for x, y in outline]
    faces = [tuple(reversed(range(count))),
             tuple(range(count * 2, count * 3))]
    for layer in range(2):
        for i in range(count):
            j = (i + 1) % count
            faces.append((layer * count + i, layer * count + j,
                          (layer + 1) * count + j, (layer + 1) * count + i))
    hull = c.mesh('FishingBoatPointedClosedHull', verts, faces, root, col,
                  'MAT_IslandWhite')
    hull.data.materials.append(c.M['ENV_Concrete'])
    hull.data.materials.append(c.M['Gunmetal'])
    hull.data.polygons[0].material_index = 2
    hull.data.polygons[1].material_index = 1
    for face in hull.data.polygons[2:2 + count]:
        face.material_index = 2
    hull['hull_length_m'] = 7.8
    hull['bow_axis'] = '+Y'

    # The raised bulwark is itself a closed, thick ring, not an open shell.
    verts = []
    for inward, z in ((False, .655), (False, .90),
                      (True, .655), (True, .90)):
        verts.extend((x * (.94 if inward else 1.),
                      y * (.98 if inward else 1.), z) for x, y in outline)
    faces = []
    for i in range(count):
        j = (i + 1) % count
        faces.extend(((i, j, count + j, count + i),
                      (2 * count + j, 2 * count + i,
                       3 * count + i, 3 * count + j),
                      (count + i, count + j, 3 * count + j, 3 * count + i),
                      (j, i, 2 * count + i, 2 * count + j)))
    c.mesh('FishingBoatContinuousBulwark', verts, faces, root, col,
           'MAT_IslandWhite')
    return outline


def _boat_wheelhouse(root, col):
    c.box('FishingBoatWheelhouseLowerBody', (0, 1.02, 1.035),
          (1.88, 2.10, .74), root, col, 'MAT_IslandWhite')
    c.frustum('FishingBoatOpaqueWheelhouseGlass', (0, 1.02, 1.855),
              (1.88, 2.10), (1.68, 1.90), .94, root, col, 'Radar Glass')
    lower = [(-.94, -.03, 1.385), (.94, -.03, 1.385),
             (.94, 2.07, 1.385), (-.94, 2.07, 1.385)]
    upper = [(-.84, .07, 2.325), (.84, .07, 2.325),
             (.84, 1.97, 2.325), (-.84, 1.97, 2.325)]
    for i, (a, b) in enumerate(zip(lower, upper)):
        c.beam('FishingBoatWheelhouseCorner_%02d' % i, a, b, .065,
               root, col, 'MAT_IslandWhite')
        j = (i + 1) % 4
        c.beam('FishingBoatWindowLowerFrame_%02d' % i, a, lower[j], .055,
               root, col, 'MAT_IslandWhite')
    c.beam('FishingBoatWindscreenCentreMullion', (0, 2.083, 1.39),
           (0, 1.983, 2.34), .055, root, col, 'MAT_IslandWhite')
    for side in (-1, 1):
        c.beam('FishingBoatSideWindowMullion', (side * .949, 1.02, 1.39),
               (side * .849, 1.02, 2.33), .045, root, col, 'MAT_IslandWhite')
    c.box('FishingBoatWheelhouseRoof', (0, 1.02, 2.385),
          (2.07, 2.27, .15), root, col, 'MAT_IslandWhite')
    c.box('FishingBoatRoofOverhangFascia', (0, 2.17, 2.365),
          (2.07, .065, .12), root, col, 'ENV_Concrete')
    c.box('FishingBoatCabinRearDoor', (.26, -.076, 1.31),
          (.60, .080, 1.25), root, col, 'Gunmetal')
    c.box('FishingBoatDoorUpperWindow', (.26, -.124, 1.65),
          (.42, .035, .42), root, col, 'Radar Glass')
    c.box('FishingBoatDoorHandle', (.47, -.158, 1.19),
          (.055, .050, .14), root, col, 'MAT_IslandWhite')
    c.beam('FishingBoatCabinGrabRail', (-.68, -.092, 1.03),
           (-.68, -.092, 1.56), .040, root, col, 'Gunmetal')
    for z in (1.06, 1.53):
        c.beam('FishingBoatGrabRailFoot', (-.68, -.092, z),
               (-.68, -.025, z), .040, root, col, 'Gunmetal')

    # A restrained civilian scanner and nav mast on a foot and triangulated
    # roof stays.  Every stay terminates on the mast or roof, with no floaters.
    c.box('FishingBoatMastRoofFoot', (0, 1.13, 2.49), (.37, .40, .08),
          root, col, 'Gunmetal')
    c.cyl('FishingBoatNavMast', (0, 1.13, 3.045), .045, 1.19,
          root, col, 'MAT_IslandWhite', segments=8)
    for side in (-1, 1):
        c.beam('FishingBoatMastStay', (side * .71, .51, 2.46),
               (0, 1.13, 3.22), .040, root, col, 'Gunmetal')
    c.box('FishingBoatNavCrossarm', (0, 1.13, 3.32), (.90, .060, .065),
          root, col, 'MAT_IslandWhite')
    for side, material in ((-1, 'MAT_IslandRed'), (1, 'ENV_Moss')):
        c.box('FishingBoatNavigationLight', (side * .41, 1.13, 3.37),
              (.12, .10, .10), root, col, material)
    c.cyl('FishingBoatRadarMount', (0, 1.13, 3.65), .14, .18,
          root, col, 'Gunmetal', segments=10)
    c.box('FishingBoatCivilianRadarScanner', (0, 1.13, 3.80),
          (.90, .19, .16), root, col, 'MAT_IslandWhite')
    c.cyl('FishingBoatRadioAntenna', (.25, 1.13, 4.09), .019, .61,
          root, col, 'Gunmetal', segments=6)


def _boat_work_deck(root, col):
    c.box('FishingBoatAftWorkDeck', (0, -1.93, .685),
          (2.24, 3.56, .08), root, col, 'ENV_Concrete')
    for side in (-1, 1):
        c.box('FishingBoatWinchFoot', (side * .57, -1.78, .757),
              (.30, .72, .075), root, col, 'Gunmetal')
        c.box('FishingBoatWinchBearingStand', (side * .57, -1.78, .98),
              (.12, .40, .50), root, col, 'MAT_IslandWhite')
        c.beam('FishingBoatWinchStandBrace', (side * .57, -2.06, .79),
               (side * .57, -1.78, 1.15), .06, root, col, 'Gunmetal')
    c.cyl('FishingBoatWinchAxle', (0, -1.78, 1.20), .055, 1.47,
          root, col, 'Gunmetal', segments=8, rot=(0, math.pi / 2, 0))
    c.cyl('FishingBoatWinchDrum', (0, -1.78, 1.20), .25, .89,
          root, col, 'ENV_Bark', segments=12, rot=(0, math.pi / 2, 0))
    for side in (-1, 1):
        c.cyl('FishingBoatWinchFlange', (side * .47, -1.78, 1.20), .34, .075,
              root, col, 'Gunmetal', segments=12, rot=(0, math.pi / 2, 0))
    for i in range(4):
        c.ring('FishingBoatWinchRopeWinding_%02d' % i,
               (-.30 + i * .20, -1.78, 1.20), .256, .029,
               root, col, 'ENV_SealMuzzle', segments=12, cross=4,
               rot=(0, math.pi / 2, 0))
    c.box('FishingBoatWinchMotorMount', (.86, -1.78, .98),
          (.35, .37, .11), root, col, 'Gunmetal')
    c.cyl('FishingBoatWinchMotor', (.85, -1.78, 1.14), .17, .35,
          root, col, 'MAT_IslandWhite', segments=10, rot=(0, math.pi / 2, 0))

    # A net bundle is lashed to a low deck tray rather than suspended in space.
    c.box('FishingBoatNetTray', (0, -2.80, .757), (1.18, .78, .075),
          root, col, 'ENV_Wood')
    c.sphere('FishingBoatBundledNet', (0, -2.80, .865), (1.12, .70, .24),
             root, col, 'ENV_SealMuzzle', segments=10, rings=5)
    for x in (-.35, 0, .35):
        c.beam('FishingBoatNetLashing', (x, -3.14, .79),
               (x, -2.80, .98), .035, root, col, 'Gunmetal')
        c.beam('FishingBoatNetLashing', (x, -2.80, .98),
               (x, -2.46, .79), .035, root, col, 'Gunmetal')

    for side in (-1, 1):
        for y in (-2.86, -.65):
            x = side * 1.31
            c.ring('FishingBoatTyreFender', (x, y, .39), .195, .071,
                   root, col, 'Gunmetal', segments=12, cross=5,
                   rot=(0, math.pi / 2, 0))
            c.beam('FishingBoatFenderTie', (side * 1.20, y, .91),
                   (x, y - .06, .625), .035, root, col, 'ENV_Bark')
            c.beam('FishingBoatFenderTie', (side * 1.20, y, .91),
                   (x, y + .06, .625), .035, root, col, 'ENV_Bark')
    for x in (-.93, 0, .93):
        c.beam('FishingBoatSternSafetyPost', (x, -3.80, .86),
               (x, -3.80, 1.25), .045, root, col, 'MAT_IslandWhite')
    c.beam('FishingBoatSternSafetyRail', (-.97, -3.80, 1.25),
           (.97, -3.80, 1.25), .045, root, col, 'MAT_IslandWhite')


def build_fishing_boat(root, col):
    """7.8 m civilian fishing launch; pointed bow +Y, waterline origin Z0."""
    _boat_hull(root, col)
    _boat_wheelhouse(root, col)
    _boat_work_deck(root, col)
    _prism('FishingBoatRaisedForedeck',
           [(-.97, 2.15), (.97, 2.15), (.51, 3.32), (0, 3.73), (-.51, 3.32)],
           .66, .745, root, col, 'MAT_IslandWhite')
    c.box('FishingBoatForedeckHatch', (0, 2.69, .79), (.69, .74, .095),
          root, col, 'ENV_Concrete')
    c.box('FishingBoatForedeckHatchHandle', (0, 2.68, .847),
          (.23, .055, .040), root, col, 'Gunmetal')
    bow_mooring = _cleat('FishingBoatBowCleat', root, col, 0, 3.28, .745)
    aft_mooring = _cleat('FishingBoatAftCleat', root, col, -.79, -3.39, .725)
    for i, (radius, z) in enumerate(((.22, .754), (.16, .775))):
        c.ring('FishingBoatCoiledMooringRope_%02d' % i,
               (-.70, -3.00, z), radius, .027, root, col,
               'ENV_Bark', segments=12, cross=4)
    c.beam('FishingBoatMooringRopeTail', (-.89, -3.00, .754),
           (-.83, -3.39, .867), .035, root, col, 'ENV_Bark')

    # Shaft support and rudder are attached to the submerged stern structure.
    c.beam('FishingBoatShaftSupport', (0, -2.91, -.40),
           (0, -3.44, -.52), .10, root, col, 'Gunmetal')
    c.cyl('FishingBoatPropellerHub', (0, -3.43, -.52), .13, .20,
          root, col, 'Gunmetal', segments=8, rot=(math.pi / 2, 0, 0))
    for angle in (0, math.tau / 3, math.tau * 2 / 3):
        c.box('FishingBoatPropellerBlade',
              (.15 * math.cos(angle), -3.43, -.52 + .15 * math.sin(angle)),
              (.27, .055, .090), root, col, 'ENV_Concrete', rot=(0, -angle, 0))
    c.cyl('FishingBoatRudderStock', (0, -3.72, -.255), .029, .49,
          root, col, 'Gunmetal', segments=8)
    c.box('FishingBoatRudderBlade', (0, -3.72, -.58), (.06, .33, .50),
          root, col, 'Gunmetal')
    c.empty('WaterlineOrigin', root, col)
    c.empty('MooringPoint_01', root, col, bow_mooring)
    c.empty('MooringPoint_02', root, col, aft_mooring)
    return {'features': ['7.8 m pointed hard-chine white fishing hull with full aft work deck',
                         'Sloping opaque teal wheelhouse, framed glazing and rear access door',
                         'Roof-supported civilian nav mast, scanner and radio antenna',
                         'Braced net winch with drum, motor and wound rope',
                         'Lashed net bundle, four tied tyre fenders, stern rail and mooring rope',
                         'Submerged shaft, three-blade propeller and connected rudder'],
            'required_sockets': ['WaterlineOrigin', 'MooringPoint_01', 'MooringPoint_02'],
            'hull_length_m': 7.8, 'bow_axis': '+Y', 'anchor': 'waterline_z_0',
            'animation_included': False,
            'placement': 'Place root at water surface Z0; pointed bow faces +Y.',
            'dimensions': (2.762, 7.80, 5.225)}


def _pontoon_gangway(root, col):
    # The spindle is fixed to the dock.  The access ramp and its rails are a
    # separate hierarchy beneath the hinge empty, so a future tide pose can
    # articulate them without making any mesh object carry a rotation.
    hinge = c.empty('GangwayHinge', root, col, (0, -3.0, .64))
    c.cyl('FloatingPontoonGangwayHingeSpindle', (0, -3.0, .64), .060, 1.50,
          root, col, 'Gunmetal', segments=10, rot=(0, math.pi / 2, 0))
    for side in (-1, 1):
        c.box('FloatingPontoonHingeDockEar', (side * .70, -2.96, .575),
              (.14, .26, .23), root, col, 'Gunmetal')
        c.cyl('FloatingPontoonHingeKnuckle', (side * .70, -3.0, .64), .086, .145,
              root, col, 'MAT_IslandWhite', segments=10, rot=(0, math.pi / 2, 0))
    rise, run = .20, 3.0
    pitch = -math.atan2(rise, run)
    c.box('FloatingPontoonShoreAccessGangwayDeck', (0, -run / 2, rise / 2 - .05),
          (1.24, math.hypot(run, rise), .095), hinge, col,
          'ENV_Concrete', rot=(pitch, 0, 0))
    for side in (-1, 1):
        x = side * .56
        c.beam('FloatingPontoonGangwayUndersideBearer', (x, 0, -.08),
               (x, -run, rise - .08), .10, hinge, col, 'Gunmetal')
        for i in range(4):
            y = -float(i)
            z = -y * rise / run
            c.beam('FloatingPontoonGangwayRailPost', (x, y, z - .035),
                   (x, y, z + .90), .045, hinge, col, 'Gunmetal')
        for height in (.44, .90):
            c.beam('FloatingPontoonGangwayHandrail', (x, 0, height),
                   (x, -run, rise + height), .045, hinge, col, 'Gunmetal')
    for i in range(6):
        y = -.25 - i * .50
        z = -y * rise / run + .014
        c.box('FloatingPontoonGangwayGripStrip_%02d' % i, (0, y, z),
              (1.11, .07, .027), hinge, col, 'ENV_Wood', rot=(pitch, 0, 0))
    c.box('FloatingPontoonGangwayShoreLanding', (0, -6.12, .765),
          (1.50, .38, .15), root, col, 'Gunmetal')
    c.empty('ShoreConnection', root, col, (0, -6.0, .84))


def build_floating_pontoon(root, col):
    """3 x 6 m timber float dock with two submerged buoyancy pontoons."""
    for side in (-1, 1):
        c.lathe('FloatingPontoonClosedBuoyancyHull', (side * .84, 0, -.07),
                [(-2.85, .16), (-2.62, .36), (-2.40, .40),
                 (2.40, .40), (2.62, .36), (2.85, .16)],
                root, col, 'ENV_Concrete', segments=12,
                rot=(math.pi / 2, 0, 0))
        c.box('FloatingPontoonLongitudinalBearer', (side * 1.12, 0, .44),
              (.18, 5.82, .22), root, col, 'Gunmetal')
        for y in (-2.40, 0, 2.40):
            c.box('FloatingPontoonHullSaddle', (side * .84, y, .32),
                  (.51, .22, .16), root, col, 'Gunmetal')
    for i, y in enumerate((-2.70, -1.62, -.54, .54, 1.62, 2.70)):
        c.box('FloatingPontoonTransverseJoist_%02d' % i, (0, y, .46),
              (2.94, .16, .18), root, col, 'Gunmetal')
    for i in range(20):
        c.box('FloatingPontoonTimberDeckBoard_%02d' % i,
              (0, -2.85 + i * .30, .595), (2.96, .291, .090),
              root, col, 'ENV_Wood')
    for side in (-1, 1):
        c.box('FloatingPontoonDeckEdgeRim', (side * 1.465, 0, .594),
              (.070, 6.0, .10), root, col, 'ENV_Wood')
        for y in (-2.05, 0, 2.05):
            c.cyl('FloatingPontoonRubberFender', (side * 1.60, y, .15),
                  .105, .70, root, col, 'Gunmetal', segments=10)
            c.beam('FloatingPontoonFenderHanger', (side * 1.47, y, .61),
                   (side * 1.60, y, .485), .040, root, col, 'Gunmetal')
    moorings = []
    for side in (-1, 1):
        for y in (-1.94, 1.94):
            point = _cleat('FloatingPontoonMooringCleat', root, col,
                           side * 1.18, y, .640)
            if y > 0:
                moorings.append(point)
    _pontoon_gangway(root, col)
    c.empty('WaterlineOrigin', root, col)
    for i, point in enumerate(moorings, 1):
        c.empty('MooringPoint_%02d' % i, root, col, point)
    return {'features': ['3 x 6 m timber deck with twenty separate boards and a tied steel frame',
                         'Two capped and tapered 5.7 m submerged buoyancy pontoons',
                         'Load-bearing hull saddles, longitudinal bearers and six deck joists',
                         'Six hanging rubber fenders and four deck mooring cleats',
                         'Separate gangway hinge with a braced 3 m shore access ramp and rails'],
            'required_sockets': ['WaterlineOrigin', 'MooringPoint_01', 'MooringPoint_02',
                                 'GangwayHinge', 'ShoreConnection'],
            'deck_dimensions_m': (3.0, 6.0), 'gangway_length_m': 3.0,
            'anchor': 'waterline_z_0', 'animation_included': False,
            'placement': 'Root at water surface Z0; gangway extends -Y to ShoreConnection '
                         '(0,-6,.84), matching a shore deck at Z.84.',
            'dimensions': (3.41, 9.31, 2.233)}
