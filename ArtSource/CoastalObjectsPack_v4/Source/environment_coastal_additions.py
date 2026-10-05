"""Extra miniature shore props; authoring metres, Z-up, no Unity changes."""
import math
import random
import environment_detail_common as c
from environment_rock_geometry import angular_rock


def build_breakwater(root, col):
    """Repeatable harbour wall with angular rubble and an end navigation light."""
    c.frustum('ConcreteFoundation', (0, 0, -.58), (3.1, 12), (2.45, 12),
              2.0, root, col, 'ENV_Concrete')
    c.box('WalkwayCoping', (0, 0, .47), (2.55, 12, .18), root, col,
          'MAT_IslandWhite', bevel=.035)
    for side in (-1, 1):
        c.box('CopingEdge_'+str(side), (side*1.18, 0, .61), (.18, 12, .12),
              root, col, 'ENV_Concrete')
    rng = random.Random(218)
    for side in (-1, 1):
        for i in range(8):
            y = -5.4+i*1.5+rng.uniform(-.16, .16)
            angular_rock('WaveArmour_%s_%s' % (side, i),
                         (side*1.65, y, -.78),
                         (rng.uniform(1.3, 1.65), rng.uniform(1.45, 1.85),
                          rng.uniform(.95, 1.25)), root, col, seed=510+i+side*10)
    # Cast joint grooves stop a repeatable wall reading as a featureless rectangle.
    for i in (-4, -2, 0, 2, 4):
        c.box('ExpansionJoint_'+str(i), (0, i, .567), (2.18, .028, .018),
              root, col, 'Gunmetal')
    for i, y in enumerate((-4.3, .1, 3.2)):
        c.cyl('BollardFoot_'+str(i), (-.75, y, .62), .18, .12, root, col,
              'Gunmetal', segments=12)
        c.cyl('BollardPost_'+str(i), (-.75, y, .82), .10, .40, root, col,
              'Gunmetal', segments=12)
        c.cyl('BollardCap_'+str(i), (-.75, y, 1.02), .15, .065, root, col,
              'Gunmetal', segments=12)
        c.empty('MooringPoint_'+str(i+1).zfill(2), root, col, (-.75, y, .9))
    beacon = c.empty('BeaconMount', root, col, (0, 5.1, .56))
    c.frustum('BeaconPlinth', (0, 0, .20), (.95, .95), (.70, .70), .4,
              beacon, col, 'ENV_Concrete')
    c.lathe('RedNavigationPost', (0, 0, .4), [(0, .26), (1.35, .17)],
            beacon, col, 'MAT_IslandRed', segments=12)
    c.cyl('WhiteBand', (0, 0, 1.24), .205, .21, beacon, col,
          'MAT_IslandWhite', segments=12)
    c.cyl('LightPlatform', (0, 0, 1.80), .34, .12, beacon, col,
          'Gunmetal', segments=12)
    c.cyl('LightLens', (0, 0, 2.03), .17, .32, beacon, col,
          'ENV_Lamp', segments=12)
    c.lathe('LightCap', (0, 0, 2.22), [(0, .24), (.14, .09)],
            beacon, col, 'Gunmetal', segments=12)
    for i in range(4):
        a = i*math.tau/4
        c.beam('LensGuard_'+str(i), (.22*math.cos(a), .22*math.sin(a), 1.86),
               (.22*math.cos(a), .22*math.sin(a), 2.22), .035,
               beacon, col, 'Gunmetal')
    c.empty('LampOrigin', beacon, col, (0, 0, 2.03))
    c.empty('WaterlineOrigin', root, col)
    c.empty('Snap_Start', root, col, (0, -6, .56))
    c.empty('Snap_End', root, col, (0, 6, .56))
    return {'description': 'Modular breakwater, angular armour, mooring bollards, end light',
            'placement': 'harbour / coastal water', 'anchor': 'waterline_z_0',
            'repeat_length_m': 12, 'moving_parts': [], 'animation_included': False}


def build_beach_fishing_gear(root, col):
    """One grounded, connected fishing worksite rather than arbitrary clutter."""
    # Four-foot braced drying frame supports a visible sagging fishing net.
    for x in (-1.85, 1.85):
        for y in (.25, .9):
            c.box('DryingRackFoot', (x, y, .075), (.32, .32, .15),
                  root, col, 'ENV_Concrete')
            c.beam('DryingRackPost', (x, y, .10), (x, y, 2.35), .105,
                   root, col, 'ENV_Wood')
        c.beam('RackBrace', (x, .9, .2), (x, .25, 1.35), .075,
               root, col, 'ENV_Wood')
        c.beam('RackTopLink', (x, .25, 2.35), (x, .9, 2.35), .11,
               root, col, 'ENV_Wood')
    c.beam('RackCrossbar', (-1.98, .25, 2.35), (1.98, .25, 2.35), .11,
           root, col, 'ENV_Wood')
    def net_point(i, j):
        x = -1.74+i*.435
        return (x, .25+.03*j, 2.22-j*.31-.18*(1-(x/1.74)**2))
    for j in range(6):
        for i in range(8):
            c.beam('NetHorizontal_%s_%s' % (j, i), net_point(i, j),
                   net_point(i+1, j), .022, root, col, 'MAT_IslandTree')
    for i in range(9):
        for j in range(5):
            c.beam('NetVertical_%s_%s' % (i, j), net_point(i, j),
                   net_point(i, j+1), .022, root, col, 'MAT_IslandTree')
    for i in range(5):
        x = -1.55+i*.78
        net_top = 2.22-.18*(1-(x/1.74)**2)
        c.beam('NetHangingTie_'+str(i), (x, .25, 2.35),
               (x, .25, net_top), .022, root, col, 'ENV_SandLight')
        c.cyl('NetFloat_'+str(i), (x, .25, net_top),
              .072, .14, root, col, 'ENV_Foam', segments=8,
              rot=(math.pi/2, 0, 0))
    for side in (-1, 1):
        c.beam('NetCornerTie_'+str(side), (side*1.85, .25, 2.22),
               net_point(0 if side<0 else 8, 0), .022,
               root, col, 'ENV_SandLight')
    # Stacked crates each have a real base, lid lip and closed carrying grips.
    for idx, (x, y, z) in enumerate(((-1.26, -1.05, .22), (-1.26, -1.05, .62))):
        c.box('FishCrate_'+str(idx), (x, y, z), (.94, .68, .36),
              root, col, 'Radar Glass', bevel=.025)
        for s in (-1, 1):
            c.box('CrateLip_'+str(idx), (x+s*.46, y, z+.20), (.055, .70, .07),
                  root, col, 'MAT_IslandWhite')
            c.box('CrateHandle_'+str(idx), (x, y+s*.347, z+.025), (.35, .018, .055),
                  root, col, 'Gunmetal')
        for k in range(4):
            c.box('CrateLidSlat', (x-.33+k*.22, y, z+.185), (.07, .62, .018),
                  root, col, 'MAT_IslandWhite')
    # Two lobster/fish cages: repeated ribs and mesh panels make their role readable.
    for idx, (x, y) in enumerate(((.33, -1.12), (1.48, -1.00))):
        width, depth, height = .86, .70, .54
        c.box('TrapBase_'+str(idx), (x, y, .07), (width, depth, .08),
              root, col, 'ENV_Wood')
        for corner_x in (-width/2, width/2):
            for corner_y in (-depth/2, depth/2):
                c.beam('TrapCorner', (x+corner_x, y+corner_y, .07),
                       (x+corner_x, y+corner_y, height), .035, root, col, 'Gunmetal')
        for z in (.25, .52):
            for sy in (-1, 1):
                c.beam('TrapSide', (x-width/2, y+sy*depth/2, z),
                       (x+width/2, y+sy*depth/2, z), .028, root, col, 'Gunmetal')
            for sx in (-1, 1):
                c.beam('TrapEnd', (x+sx*width/2, y-depth/2, z),
                       (x+sx*width/2, y+depth/2, z), .028, root, col, 'Gunmetal')
        for k in range(5):
            c.beam('TrapRoofSlat', (x-.34+k*.17, y-depth/2, .52),
                   (x-.34+k*.17, y+depth/2, .52), .026, root, col, 'Gunmetal')
        # Entry funnel is a short hooped opening, not a solid dummy cylinder.
        c.ring('TrapEntry', (x, y-depth/2-.01, .31), .13, .025, root, col,
               'Gunmetal', segments=12, cross=5, rot=(math.pi/2, 0, 0))
    for i in range(3):
        c.ring('CoiledMooringRope_'+str(i), (.05, -.03, .055+i*.047),
               .30-i*.065, .025, root, col, 'ENV_SandLight', segments=18, cross=5)
    c.beam('RopeTail', (.29, -.03, .048), (.58, -.3, .048), .042,
           root, col, 'ENV_SandLight')
    c.empty('GroundOrigin', root, col)
    c.empty('FrontAccess', root, col, (0, -1.75, 0))
    return {'description': 'Braced net-drying frame, fish crates, traps and coiled rope',
            'placement': 'dry beach / fishing harbour', 'anchor': 'ground_z_0',
            'moving_parts': [], 'animation_included': False}
