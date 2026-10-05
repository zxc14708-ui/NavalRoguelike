"""Connected miniature coastal structures.  Review assets, not game patches.

All helper-generated meshes are sealed.  Mesh rotations are baked by the
common geometry helpers; no new materials or runtime behaviour are added.
"""
import math
import environment_detail_common as c


def _round_guardrail(root, col, radius, floor_z, height, name):
    for i in range(16):
        angle = i * math.tau / 16
        c.cyl(name + 'Post', (radius * math.cos(angle), radius * math.sin(angle),
                              floor_z + height / 2 - .008),
              .020, height + .016, root, col, 'Gunmetal', segments=6)
    for z in (floor_z + height * .48, floor_z + height):
        c.ring(name + 'Rail', (0, 0, z), radius, .020, root, col,
               'Gunmetal', segments=24, cross=4)


def build_lighthouse(root, col):
    """Tapered painted tower with a supported gallery and framed lantern."""
    c.lathe('LighthouseConcretePlinth', (0, 0, 0),
            [(0, 1.30), (.12, 1.30), (.25, 1.17)], root, col,
            'ENV_Concrete', segments=24)
    tower_profile = [(.24, .96), (.45, .93), (6.35, .75)]
    c.lathe('LighthouseWhiteTower', (0, 0, 0), tower_profile,
            root, col, 'MAT_IslandWhite', segments=24)

    def radius(z):
        return .93 + (z - .45) * (.75 - .93) / (6.35 - .45)

    c.lathe('LighthouseRedIdentificationBand', (0, 0, 0),
            [(3.17, radius(3.17) + .009), (3.67, radius(3.67) + .009)],
            root, col, 'MAT_IslandRed', segments=24)
    c.box('LighthouseEntranceFrame', (0, .925, 1.00),
          (.67, .11, 1.53), root, col, 'ENV_Concrete')
    c.box('LighthouseEntranceDoor', (0, .995, .99),
          (.53, .035, 1.40), root, col, 'Gunmetal')
    c.box('LighthouseDoorVent', (0, 1.016, 1.38),
          (.34, .014, .18), root, col, 'Radar Glass')
    c.cyl('LighthouseDoorHandle', (.19, 1.035, .96), .026, .04,
          root, col, 'MAT_IslandRust', segments=8, rot=(math.pi / 2, 0, 0))
    for i, (y, height) in enumerate(((1.15, .27), (1.425, .18), (1.70, .09))):
        c.box('LighthouseEntryStep_%02d' % i, (0, y, height / 2),
              (1.05, .30, height), root, col, 'ENV_Concrete')
    for i, z in enumerate((2.00, 3.98, 5.69)):
        r = radius(z)
        c.box('LighthouseWindowFrame_%02d' % i, (0, r + .028, z),
              (.33, .10, .48), root, col, 'ENV_Concrete')
        c.box('LighthouseWindowGlass_%02d' % i, (0, r + .080, z),
              (.235, .020, .38), root, col, 'Radar Glass')
        c.box('LighthouseWindowSill_%02d' % i, (0, r + .077, z - .235),
              (.37, .16, .055), root, col, 'MAT_IslandWhite')
    c.lathe('LighthouseGalleryCorbel', (0, 0, 0),
            [(6.08, .76), (6.34, 1.03), (6.40, 1.13)], root, col,
            'ENV_Concrete', segments=24)
    c.cyl('LighthouseGalleryFloor', (0, 0, 6.43), 1.16, .10,
          root, col, 'MAT_IslandWhite', segments=24)
    _round_guardrail(root, col, 1.065, 6.48, .53, 'LighthouseGallery')
    c.cyl('LighthouseLanternFloor', (0, 0, 6.55), .71, .15,
          root, col, 'MAT_IslandRed', segments=16)
    c.cyl('LighthouseOpaqueLanternGlass', (0, 0, 7.14), .63, 1.08,
          root, col, 'Radar Glass', segments=16)
    for i in range(8):
        angle = math.pi / 8 + i * math.tau / 8
        c.cyl('LighthouseLanternMullion', (.648 * math.cos(angle),
                                         .648 * math.sin(angle), 7.14),
              .025, 1.16, root, col, 'MAT_IslandWhite', segments=6)
    c.ring('LighthouseLanternLowerFrame', (0, 0, 6.60), .647, .023,
           root, col, 'MAT_IslandWhite', segments=16, cross=4)
    c.cyl('LighthouseLanternUpperCap', (0, 0, 7.71), .73, .13,
          root, col, 'MAT_IslandWhite', segments=16)
    c.lathe('LighthouseConicalRoof', (0, 0, 0),
            [(7.755, .81), (8.34, .055), (8.39, .024)], root, col,
            'MAT_IslandRed', segments=24)
    c.cyl('LighthouseLightningFinial', (0, 0, 8.47), .012, .18,
          root, col, 'Gunmetal', segments=6)
    beacon = c.empty('BeaconPivot', root, col, (0, 0, 7.14))
    c.box('LighthouseBeaconOpticHousing', (0, .633, .10),
          (.22, .08, .25), beacon, col, 'MAT_IslandWhite')
    c.box('LighthouseBeaconOptic', (0, .68, .10),
          (.16, .04, .18), beacon, col, 'ENV_Lamp')
    c.empty('LampOrigin', beacon, col, (0, .70, .10))
    c.empty('EntryOrigin', root, col, (0, 1.05, .27))
    return {'features': ['Tapered white tower with red band', 'Door and three supported steps',
                         'Corbel-supported circular gallery and two-level guardrail',
                         'Opaque teal lantern with eight structural mullions',
                         'Closed conical red roof and lightning finial'],
            'required_sockets': ['BeaconPivot', 'LampOrigin', 'EntryOrigin'],
            'dimensions': (2.60, 3.15, 8.56)}


def _jetty_rail(root, col, side, ys, name):
    x, top = side * 1.405, 1.06
    for y in ys:
        c.box(name + 'Foot', (x, y, .206), (.12, .12, .04),
              root, col, 'ENV_Concrete')
        c.cyl(name + 'Post', (x, y, .646), .019, .90,
              root, col, 'Gunmetal', segments=6)
    for a, b in zip(ys, ys[1:]):
        c.beam(name + 'TopRail', (x, a, top), (x, b, top), .035,
               root, col, 'Gunmetal')
        c.beam(name + 'MidRail', (x, a, .68), (x, b, .68), .026,
               root, col, 'Gunmetal')


def _bollard(root, col, x, y, index):
    c.box('JettyBollardFoot_%02d' % index, (x, y, .2195), (.32, .22, .06),
          root, col, 'Gunmetal')
    for side in (-1, 1):
        c.cyl('JettyBollardPin', (x + side * .075, y, .352), .041, .205,
              root, col, 'Gunmetal', segments=8)
    c.box('JettyBollardCrossbar_%02d' % index, (x, y, .445),
          (.31, .065, .045), root, col, 'Gunmetal')


def build_jetty(root, col):
    """Six-metre pier; root at its shore connection, deck extends forward +Y."""
    c.box('JettyConcreteSlab', (0, 3.0, -.02), (3.0, 6.0, .32),
          root, col, 'ENV_Concrete')
    for i in range(18):
        c.box('JettyDeckPlank_%02d' % i, (0, (i + .5) / 3, .165),
              (2.96, .315, .055), root, col, 'ENV_Wood')
    for side in (-1, 1):
        c.box('JettyEdgeCoping', (side * 1.48, 3.0, .17),
              (.055, 6.0, .08), root, col, 'ENV_Concrete')
        c.box('JettyLongitudinalBearer', (side * 1.05, 3.0, -.25),
              (.27, 5.92, .23), root, col, 'ENV_Concrete')
    for i, y in enumerate((.90, 3.0, 5.25)):
        c.box('JettyTransverseSupport_%02d' % i, (0, y, -.22),
              (2.57, .34, .21), root, col, 'ENV_Concrete')
        for side in (-1, 1):
            c.box('JettySupportPile', (side * 1.05, y, -1.035),
                  (.25, .30, 1.93), root, col, 'ENV_Concrete')
            c.box('JettyPileCapital', (side * 1.05, y, -.24),
                  (.43, .50, .22), root, col, 'ENV_Concrete')
            c.box('JettySeabedFoot', (side * 1.05, y, -1.92),
                  (.48, .55, .16), root, col, 'ENV_Concrete')
    c.box('JettyShoreStepLower', (0, -.38, .035),
          (2.0, .44, .07), root, col, 'ENV_Concrete')
    c.box('JettyShoreStepUpper', (0, -.11, .085),
          (2.0, .25, .17), root, col, 'ENV_Concrete')
    _jetty_rail(root, col, 1, (1.0, 2.2, 3.4, 4.6, 5.85), 'JettyStarboardRail')
    # A deliberate opening in the port guardrail gives the ladder a usable gate.
    _jetty_rail(root, col, -1, (1.0, 2.25, 3.75), 'JettyPortForwardRail')
    _jetty_rail(root, col, -1, (5.0, 5.85), 'JettyPortAftRail')
    for i, (x, y) in enumerate(((-1.03, 2.15), (1.03, 2.15),
                                 (-1.03, 4.94), (1.03, 4.94), (0, 5.70))):
        _bollard(root, col, x, y, i)
    for side in (-1, 1):
        for y in (1.55, 3.75, 5.48):
            c.cyl('JettyRubberFender', (side * 1.595, y, -.10), .125, .70,
                  root, col, 'Gunmetal', segments=12)
            c.beam('JettyFenderHanger', (side * 1.42, y, .20),
                   (side * 1.595, y, .24), .036, root, col, 'MAT_IslandRust')
    for y in (4.25, 4.64):
        c.beam('JettyLadderStile', (-1.615, y, -1.23), (-1.615, y, .92),
               .035, root, col, 'MAT_IslandRust')
        c.beam('JettyLadderTopHook', (-1.615, y, .92), (-1.35, y, .92),
               .035, root, col, 'MAT_IslandRust')
        c.beam('JettyLadderDeckAnchor', (-1.35, y, .92), (-1.35, y, .195),
               .035, root, col, 'MAT_IslandRust')
    for z in (-1.13, -.87, -.61, -.35, -.09, .17, .43, .69):
        c.beam('JettyLadderRung', (-1.615, 4.25, z), (-1.615, 4.64, z),
               .035, root, col, 'MAT_IslandRust')
    c.empty('ShoreConnection', root, col, (0, 0, 0))
    c.empty('MooringPoint_01', root, col, (-1.03, 4.94, .45))
    c.empty('MooringPoint_02', root, col, (1.03, 4.94, .45))
    c.empty('LadderAccess', root, col, (-1.35, 4.445, .20))
    return {'features': ['3 x 6 m concrete pier with individual timber deck boards',
                         'Six piles and tied cross/longitudinal bearers down to -2 m',
                         'Twin-pin mooring bollards and suspended rubber fenders',
                         'Gated corrosion-coloured sea-access ladder',
                         'Zero-height shore connection with two shallow access steps'],
            'required_sockets': ['ShoreConnection', 'MooringPoint_01',
                                 'MooringPoint_02', 'LadderAccess'],
            'dimensions': (3.44, 6.60, 3.10)}


def build_buoy(root, col):
    """Red floating navigation beacon with triangulated frame and anchor eye."""
    c.lathe('BuoyRedFloat', (0, 0, 0),
            [(-.55, .18), (-.39, .50), (.02, .70), (.30, .61), (.43, .35)],
            root, col, 'MAT_IslandRed', segments=24)
    c.ring('BuoyWaterlineRubberCollar', (0, 0, 0), .675, .025,
           root, col, 'Gunmetal', segments=24, cross=4)
    c.cyl('BuoyFrameDeck', (0, 0, .445), .40, .09,
          root, col, 'MAT_IslandRed', segments=16)
    angles = [math.pi / 2 + i * math.tau / 3 for i in range(3)]
    for angle in angles:
        c.beam('BuoyBeaconLeg', (.33 * math.cos(angle), .33 * math.sin(angle), .47),
               (.22 * math.cos(angle), .22 * math.sin(angle), 1.86),
               .055, root, col, 'MAT_IslandRed')
    for z in (1.00, 1.46):
        radius = .33 - (z - .47) * .11 / 1.39
        points = [(radius * math.cos(a), radius * math.sin(a), z) for a in angles]
        for i in range(3):
            c.beam('BuoyHorizontalBrace', points[i], points[(i + 1) % 3],
                   .035, root, col, 'MAT_IslandRed')
    c.cyl('BuoyLanternSeat', (0, 0, 1.86), .235, .08,
          root, col, 'MAT_IslandRed', segments=16)
    c.cyl('BuoyOpaqueLanternGlass', (0, 0, 2.015), .145, .245,
          root, col, 'Radar Glass', segments=12)
    for angle in angles:
        c.cyl('BuoyLanternMullion', (.158 * math.cos(angle), .158 * math.sin(angle), 2.01),
              .015, .26, root, col, 'MAT_IslandRed', segments=6)
    c.lathe('BuoyLanternRainCap', (0, 0, 0),
            [(2.125, .22), (2.205, .10), (2.23, .021)],
            root, col, 'MAT_IslandRed', segments=16)
    c.cyl('BuoyLightningPin', (0, 0, 2.325), .013, .19,
          root, col, 'Gunmetal', segments=6)
    c.ring('BuoyAnchorEye', (0, 0, -.57), .09, .020,
           root, col, 'Gunmetal', segments=12, cross=4, rot=(math.pi / 2, 0, 0))
    beacon = c.empty('BeaconPivot', root, col, (0, 0, 2.015))
    c.box('BuoyAmberOptic', (0, .155, 0), (.11, .045, .13),
          beacon, col, 'ENV_Lamp')
    c.empty('LampOrigin', beacon, col, (0, .18, 0))
    c.empty('WaterlineOrigin', root, col)
    c.empty('AnchorPoint', root, col, (0, 0, -.63))
    return {'features': ['1.4 m red buoy float with submerged tapered belly',
                         'Triangular three-leg beacon frame and two structural brace levels',
                         'Opaque teal lantern with mullions and closed rain cap',
                         'Anchor eye and clear waterline reference'],
            'required_sockets': ['BeaconPivot', 'LampOrigin', 'WaterlineOrigin', 'AnchorPoint'],
            'dimensions': (1.40, 1.40, 3.10)}


def _shed_front_window(root, col, x):
    c.box('CoastShedFrontWindowFrame', (x, 2.025, 1.43),
          (.54, .075, .66), root, col, 'MAT_IslandWhite')
    c.box('CoastShedFrontWindowGlass', (x, 2.072, 1.43),
          (.43, .028, .54), root, col, 'Radar Glass')
    c.box('CoastShedFrontWindowMullion', (x, 2.092, 1.43),
          (.032, .025, .54), root, col, 'MAT_IslandWhite')
    c.box('CoastShedFrontWindowSill', (x, 2.08, 1.11),
          (.61, .16, .055), root, col, 'ENV_Concrete')


def _weather_mast(root, col):
    x, y = -.65, -.94
    slope = math.atan(.65 / 1.60)
    roof_z = 3.01 - .65 * abs(x) / 1.60
    c.box('CoastWeatherRoofFoot', (x, y, roof_z + .021),
          (.28, .36, .065), root, col, 'Gunmetal', rot=(0, -slope, 0))
    c.cyl('CoastWeatherMast', (x, y, roof_z + .56), .021, 1.11,
          root, col, 'Gunmetal', segments=8)
    c.cyl('CoastWeatherMastCollar', (x, y, roof_z + .095), .048, .16,
          root, col, 'MAT_IslandWhite', segments=8)
    c.box('CoastWeatherCrossarm', (x, y, roof_z + 1.035),
          (.63, .04, .045), root, col, 'Gunmetal')
    for side in (-1, 1):
        c.beam('CoastWeatherCrossarmBrace', (x + side * .27, y, roof_z + 1.035),
               (x, y, roof_z + .72), .020, root, col, 'Gunmetal')
    anem = c.empty('AnemometerPivot', root, col, (x + .235, y, roof_z + 1.075))
    c.cyl('CoastAnemometerSpindle', (0, 0, .018), .016, .085,
          anem, col, 'MAT_IslandWhite', segments=6)
    for i in range(3):
        angle = i * math.tau / 3
        end = (.105 * math.cos(angle), .105 * math.sin(angle), .025)
        c.beam('CoastAnemometerArm', (0, 0, .025), end,
               .013, anem, col, 'Gunmetal')
        c.sphere('CoastAnemometerCup', end, (.080, .080, .055),
                 anem, col, 'MAT_IslandWhite', segments=8, rings=4)
    vane = c.empty('WindVanePivot', root, col, (x - .24, y, roof_z + 1.074))
    c.cyl('CoastWindVaneSpindle', (0, 0, .018), .012, .08,
          vane, col, 'MAT_IslandWhite', segments=6)
    c.beam('CoastWindVaneShaft', (0, -.19, .05), (0, .20, .05),
           .016, vane, col, 'Gunmetal')
    c.box('CoastWindVaneTail', (0, -.125, .09),
          (.018, .14, .11), vane, col, 'MAT_IslandWhite')
    return ['AnemometerPivot', 'WindVanePivot']


def build_coast_shed(root, col):
    """Practical 3 x 4 m coast-management cabin with a connected weather station."""
    c.box('CoastShedFoundation', (0, 0, .125), (3.25, 4.23, .25),
          root, col, 'ENV_Concrete')
    c.box('CoastShedWalls', (0, 0, 1.315), (3.0, 4.0, 2.15),
          root, col, 'ENV_Concrete')
    c.frustum('CoastShedClosedGableRoof', (0, 0, 2.685),
              (3.30, 4.30), (.10, 4.30), .65,
              root, col, 'MAT_IslandRust')
    c.box('CoastShedRoofRidgeCap', (0, 0, 3.028),
          (.15, 4.35, .08), root, col, 'Gunmetal')
    for side in (-1, 1):
        c.box('CoastShedEavesFascia', (side * 1.61, 0, 2.366),
              (.08, 4.30, .105), root, col, 'MAT_IslandWhite')
        c.box('CoastShedGutter', (side * 1.67, 0, 2.365),
              (.075, 4.30, .07), root, col, 'Gunmetal')
        c.cyl('CoastShedDownpipe', (side * 1.66, -1.84, 1.29),
              .032, 2.13, root, col, 'MAT_IslandRust', segments=8)
        for z in (.65, 1.66):
            c.box('CoastShedDownpipeBracket', (side * 1.565, -1.84, z),
                  (.19, .06, .035), root, col, 'Gunmetal')
    for i, (y, height) in enumerate(((2.28, .24), (2.52, .16), (2.76, .08))):
        c.box('CoastShedEntryStep_%02d' % i, (0, y, height / 2),
              (.95, .29, height), root, col, 'ENV_Concrete')
    c.box('CoastShedDoorFrame', (0, 2.025, 1.095),
          (.80, .095, 1.71), root, col, 'MAT_IslandWhite')
    c.box('CoastShedEntranceDoor', (0, 2.087, 1.075),
          (.65, .036, 1.55), root, col, 'Gunmetal')
    c.cyl('CoastShedDoorHandle', (.235, 2.125, 1.06), .024, .04,
          root, col, 'MAT_IslandRust', segments=8, rot=(math.pi / 2, 0, 0))
    c.box('CoastShedDoorRainhood', (0, 2.155, 2.055),
          (1.02, .46, .055), root, col, 'Gunmetal')
    for side in (-1, 1):
        c.beam('CoastShedRainhoodBrace', (side * .44, 2.008, 1.78),
               (side * .44, 2.34, 2.028), .024, root, col, 'Gunmetal')
    for x in (-.99, .99):
        _shed_front_window(root, col, x)
    for side in (-1, 1):
        c.box('CoastShedSideWindowFrame', (side * 1.524, .12, 1.42),
              (.073, .92, .72), root, col, 'MAT_IslandWhite')
        c.box('CoastShedSideWindowGlass', (side * 1.571, .12, 1.42),
              (.028, .81, .61), root, col, 'Radar Glass')
        c.box('CoastShedSideWindowMullion', (side * 1.593, .12, 1.42),
              (.025, .035, .61), root, col, 'MAT_IslandWhite')
    c.box('CoastShedRearVentFrame', (0, -2.025, 1.66),
          (.82, .075, .55), root, col, 'Gunmetal')
    for i in range(4):
        c.box('CoastShedVentLouvre', (0, -2.072, 1.49 + i * .11),
              (.70, .032, .045), root, col, 'MAT_IslandWhite')
    sockets = _weather_mast(root, col)
    c.empty('EntryOrigin', root, col, (0, 2.15, .24))
    c.empty('GroundOrigin', root, col)
    return {'features': ['3 x 4 m grey coast-management cabin on a real foundation',
                         'Closed pitched roof, fascia, gutters and bracketed downpipes',
                         'Framed opaque windows, door, supported rainhood and three steps',
                         'Rear ventilation grille',
                         'Roof-mounted weather mast with cup anemometer and wind vane'],
            'required_sockets': ['GroundOrigin', 'EntryOrigin'] + sockets,
            'dimensions': (3.415, 5.08, 3.965)}
