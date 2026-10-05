"""Closed, grounded miniature coastal facilities; no Unity integration.

Manufactured silhouettes are deliberately restrained and functional.  Each
facility can be independently placed on a level footprint.  Rotating equipment
is kept beneath explicit empty pivots, separate from the fixed structure.
"""
import math
import environment_detail_common as c


def _base(root, col, name, width, length, thickness=.24):
    c.box(name + 'Foundation', (0, 0, thickness / 2),
          (width, length, thickness), root, col, 'ENV_Concrete')
    c.empty('GroundOrigin', root, col)


def _door(root, col, name, x, y, floor, height=1.9, width=.82):
    c.box(name + 'DoorFrame', (x, y, floor + height / 2),
          (width + .14, .12, height + .10), root, col, 'ENV_Concrete')
    c.box(name + 'Door', (x, y + .075, floor + height / 2),
          (width, .055, height), root, col, 'Gunmetal')
    c.box(name + 'DoorHandle', (x + width * .32, y + .11, floor + .9),
          (.13, .055, .035), root, col, 'MAT_IslandWhite')


def _window(root, col, name, x, y, z, width=1.05, height=.65):
    c.box(name + 'Frame', (x, y, z), (width + .12, .11, height + .12),
          root, col, 'MAT_IslandWhite')
    c.box(name + 'Glass', (x, y + .063, z), (width, .04, height),
          root, col, 'Radar Glass')
    c.box(name + 'Mullion', (x, y + .09, z), (.045, .025, height),
          root, col, 'MAT_IslandWhite')
    c.box(name + 'Sill', (x, y + .06, z - height / 2 - .07),
          (width + .2, .23, .065), root, col, 'ENV_Concrete')


def _ladder(root, col, name, x, y, bottom, top, width=.6):
    for side in (-1, 1):
        c.beam(name + 'Stile', (x + side * width / 2, y, bottom),
               (x + side * width / 2, y, top), .047, root, col, 'Gunmetal')
    count=max(2, int((top - bottom) / .28))
    for i in range(count + 1):
        z=bottom + (top - bottom) * i / count
        c.beam(name + 'Rung', (x - width / 2, y, z),
               (x + width / 2, y, z), .035, root, col, 'Gunmetal')


def _rail(root, col, name, a, b, z, height=.75):
    count=max(1, int(math.dist(a, b) / .85))
    for i in range(count + 1):
        t=i/count
        x=a[0] + (b[0] - a[0])*t; y=a[1] + (b[1] - a[1])*t
        c.beam(name + 'Post', (x,y,z-.015), (x,y,z+height),
               .038, root, col, 'Gunmetal')
    for h in (height * .48, height):
        c.beam(name + 'Rail', (*a,z+h), (*b,z+h), .035, root, col, 'Gunmetal')


def build_radar_station(root, col):
    """Control hut supporting a fully braced search-array tower."""
    _base(root,col,'RadarStation',6,6)
    c.box('RadarControlHouse', (0, -.3, 1.59), (4.8,4.6,2.70),
          root,col,'MAT_IslandWhite')
    c.box('RadarHousePlinth', (0,-.3,.39), (4.94,4.74,.30),
          root,col,'ENV_Concrete')
    c.box('RadarHouseRoof', (0,-.3,3.01), (5.1,4.9,.18),
          root,col,'ENV_Concrete')
    _door(root,col,'RadarControl',-1.5,2.035,.24)
    _window(root,col,'RadarConsoleWindow',.30,2.035,1.77,1.55,.70)
    c.box('RadarEquipmentVent', (1.83,2.06,1.43), (.60,.10,.90),
          root,col,'Gunmetal')
    for z in (1.10,1.27,1.44,1.61,1.78):
        c.box('RadarVentSlat',(1.83,2.125,z),(.52,.03,.04),root,col,'ENV_Concrete')
    for i in range(3):
        c.box('RadarEntryStep',( -1.5,2.43+i*.21,(.24-i*.065)/2),
              (1.05,.24,.24-i*.065),root,col,'ENV_Concrete')
    # The structure bears directly on the roof; every brace terminates on a leg.
    low,high=3.10,6.40
    for x in (-.83,.83):
        for y in (-1.13,.53):
            c.box('RadarTowerLoadPlate',(x,y,3.13),(.42,.42,.075),root,col,'Gunmetal')
            c.beam('RadarTowerLeg',(x,y,low),(x*.76,y*.76-.072,high),
                   .14,root,col,'Gunmetal')
    lower=[(-.83,-1.13),(.83,-1.13),(.83,.53),(-.83,.53)]
    upper=[(x*.76,y*.76-.072) for x,y in lower]
    for k in range(4):
        j=(k+1)%4
        c.beam('RadarTowerXBrace',(*lower[k],low+.05),(*upper[j],high-.04),
               .073,root,col,'Gunmetal')
        c.beam('RadarTowerXBrace',(*lower[j],low+.05),(*upper[k],high-.04),
               .073,root,col,'Gunmetal')
        c.beam('RadarTowerBelt',(*lower[k],3.16),(*lower[j],3.16),
               .11,root,col,'Gunmetal')
    c.box('RadarTowerBearingPlatform',(0,-.3,6.43),(1.65,1.65,.15),root,col,'ENV_Concrete')
    c.cyl('RadarFixedAzimuthBearing',(0,-.3,6.59),.36,.21,root,col,'Gunmetal',segments=16)
    pivot=c.empty('RadarPivot',root,col,(0,-.3,6.70))
    c.cyl('RadarRotatingSpindle',(0,0,.22),.17,.45,pivot,col,'Gunmetal',segments=12)
    head=c.empty('SearchArrayHead',pivot,col,(0,0,.68))
    c.box('SearchArrayBackplate',(0,0,0),(2.95,.32,1.27),head,col,'MAT_IslandWhite')
    c.box('SearchArrayAntennaFace',(0,.181,0),(2.75,.075,1.08),head,col,'Radar Glass')
    for i in range(7):
        c.box('SearchArrayElementColumn',(-1.18+i*.393,.226,0),(.034,.025,1.02),
              head,col,'ENV_Concrete')
    for z in (-.34,0,.34):
        c.box('SearchArrayElementRow',(0,.227,z),(2.65,.025,.025),head,col,'ENV_Concrete')
    c.beam('SearchArrayRearBrace',(-1.30,-.20,-.42),(0,-.27,-.56),.095,head,col,'Gunmetal')
    c.beam('SearchArrayRearBrace',(1.30,-.20,-.42),(0,-.27,-.56),.095,head,col,'Gunmetal')
    _ladder(root,col,'RadarServiceLadder',-1.68,-2.70,.24,3.85)
    c.beam('RadarServiceTopHook',(-1.98,-2.70,3.85),(-1.98,-2.40,3.85),.047,root,col,'Gunmetal')
    c.beam('RadarServiceTopHook',(-1.38,-2.70,3.85),(-1.38,-2.40,3.85),.047,root,col,'Gunmetal')
    for z in (.7,1.7,2.7):
        for x in (-1.98,-1.38):
            c.beam('RadarLadderWallAnchor',(x,-2.70,z),(x,-2.56,z),.037,root,col,'Gunmetal')
    c.empty('EntryOrigin',root,col,(-1.5,2.14,.24))
    return {'features':['Reinforced concrete footing and control-house entrance',
                        'Four fully supported tower legs with four-sided X bracing',
                        'Separate azimuth RadarPivot and framed phased-array head',
                        'Service ladder, console window and equipment cooling vent'],
            'required_sockets':['GroundOrigin','EntryOrigin','RadarPivot','SearchArrayHead'],
            'footprint_m':(6,6),'dimensions':(6,6,8.02)}


def build_watch_post(root, col):
    """Braced elevated observation shelter with roof-mounted searchlight."""
    _base(root,col,'WatchPost',4.0,5.0)
    for x in (-1.30,1.30):
        for y in (-1.48,1.48):
            c.box('WatchPostColumnFoot',(x,y,.32),(.42,.42,.18),root,col,'ENV_Concrete')
            c.box('WatchPostSupportColumn',(x,y,1.90),(.17,.17,3.35),root,col,'Gunmetal')
    for x in (-1.30,1.30):
        c.beam('WatchPostSideXBrace',(x,-1.48,.42),(x,1.48,3.52),.085,root,col,'Gunmetal')
        c.beam('WatchPostSideXBrace',(x,1.48,.42),(x,-1.48,3.52),.085,root,col,'Gunmetal')
    c.beam('WatchPostRearXBrace',(-1.30,-1.48,.42),(1.30,-1.48,3.52),.085,root,col,'Gunmetal')
    c.beam('WatchPostRearXBrace',(1.30,-1.48,.42),(-1.30,-1.48,3.52),.085,root,col,'Gunmetal')
    c.box('WatchPostObservationDeck',(0,.12,3.59),(3.45,3.95,.18),root,col,'ENV_Concrete')
    c.box('WatchPostCabinLowerWall',(0,-.23,3.95),(2.98,3.14,.58),root,col,'MAT_IslandWhite')
    c.box('WatchPostPanoramicGlass',(0,-.23,4.62),(2.94,3.10,.83),root,col,'Radar Glass')
    for x in (-1.48,1.48):
        for y in (-1.78,1.32):
            c.box('WatchPostCornerMullion',(x,y,4.58),(.07,.07,1.03),root,col,'MAT_IslandWhite')
    for x in (-.50,.50):
        for y in (-1.79,1.33):
            c.box('WatchPostFrontRearMullion',(x,y,4.63),(.045,.06,.87),root,col,'MAT_IslandWhite')
    for x in (-1.49,1.49):
        c.box('WatchPostSideMullion',(x,-.23,4.63),(.06,.045,.87),root,col,'MAT_IslandWhite')
    c.box('WatchPostRainRoof',(0,-.23,5.16),(3.25,3.42,.22),root,col,'ENV_Concrete')
    _door(root,col,'WatchPostCabin',1.00,1.365,3.68,1.36,.66)
    # Front balcony leaves a centre ladder opening rather than blocking access.
    _rail(root,col,'WatchPostPortRail',(-1.67,-1.84),(-1.67,2.04),3.68,.66)
    _rail(root,col,'WatchPostStarboardRail',(1.67,-1.84),(1.67,2.04),3.68,.66)
    _rail(root,col,'WatchPostFrontLeftRail',(-1.67,2.04),(-.47,2.04),3.68,.66)
    _rail(root,col,'WatchPostFrontRightRail',(.47,2.04),(1.67,2.04),3.68,.66)
    _ladder(root,col,'WatchPostAccessLadder',0,2.045,.24,4.13,.75)
    for z in (.80,2.18,3.52):
        c.beam('WatchPostLadderBracket',(-.375,2.045,z),(-.375,1.48,z),.05,root,col,'Gunmetal')
        c.beam('WatchPostLadderBracket',(.375,2.045,z),(.375,1.48,z),.05,root,col,'Gunmetal')
    c.cyl('WatchPostSearchlightPedestal',(0,-.15,5.43),.16,.35,root,col,'Gunmetal',segments=12)
    pivot=c.empty('SearchlightPivot',root,col,(0,-.15,5.60))
    c.cyl('SearchlightBarrel',(0,.10,.12),.28,.54,pivot,col,'MAT_IslandWhite',segments=16,
          rot=(math.pi/2,0,0))
    c.cyl('SearchlightLens',(0,.38,.12),.243,.035,pivot,col,'ENV_Lamp',segments=16,
          rot=(math.pi/2,0,0))
    for side in (-1,1):
        c.box('SearchlightFork',(side*.30,.10,.07),(.055,.14,.36),pivot,col,'Gunmetal')
        c.cyl('SearchlightTrunnion',(side*.282,.10,.12),.095,.10,pivot,col,'Gunmetal',segments=8,
              rot=(0,math.pi/2,0))
    c.empty('LampOrigin',pivot,col,(0,.41,.12))
    c.empty('EntryOrigin',root,col,(0,2.045,.24))
    return {'features':['Four-column elevated observation cabin with load-bearing X braces',
                        'Four-sided opaque panoramic glazing and weather canopy',
                        'Guarded access balcony with deliberate ladder gate',
                        'Separately parented roof searchlight and lamp origin'],
            'required_sockets':['GroundOrigin','EntryOrigin','SearchlightPivot','LampOrigin'],
            'footprint_m':(4,5),'dimensions':(4,5,6.0)}


def build_coastal_bunker(root, col):
    """Low, battered-wall reinforced observation bunker, not an armed turret."""
    _base(root,col,'CoastalBunker',4.8,4.5)
    c.frustum('CoastalBunkerBatteredShell',(0,0,1.23),(4.68,4.38),(4.12,3.82),
              1.98,root,col,'ENV_Concrete')
    c.box('CoastalBunkerRoofSlab',(0,0,2.25),(4.72,4.32,.38),root,col,'MAT_IslandCliff')
    # Inset-looking opaque slits are closed solids; no open/back-facing shell.
    c.box('BunkerSlitConcreteSurround',(.65,2.035,1.50),(2.72,.18,.53),root,col,'ENV_Concrete')
    c.box('BunkerObservationSlit',(.65,2.135,1.50),(2.43,.035,.25),root,col,'Gunmetal')
    for x in (-.30,.51,1.32):
        c.box('BunkerSlitDivider',(x,2.16,1.50),(.075,.035,.28),root,col,'ENV_Concrete')
    _door(root,col,'Bunker',-1.49,2.085,.24,1.43,.65)
    c.box('BunkerEntryRainHood',(-1.49,2.21,1.77),(1.02,.43,.12),root,col,'MAT_IslandCliff')
    for side in (-1,1):
        c.box('BunkerRearVent',(side*1.20,-2.075,1.20),(.48,.085,.53),root,col,'Gunmetal')
        for z in (1.02,1.15,1.28,1.41):
            c.box('BunkerRearVentSlat',(side*1.20,-2.126,z),(.39,.025,.037),root,col,'ENV_Concrete')
    c.box('BunkerRoofVentFoot',(-1.2,-.75,2.48),(.52,.44,.13),root,col,'ENV_Concrete')
    c.box('BunkerArmouredRoofVent',(-1.2,-.75,2.60),(.36,.29,.16),root,col,'Gunmetal')
    c.box('BunkerVentWeatherCap',(-1.2,-.75,2.71),(.55,.47,.08),root,col,'MAT_IslandCliff')
    c.empty('EntryOrigin',root,col,(-1.49,2.18,.24))
    return {'features':['Low reinforced concrete bunker with sloped/battered walls',
                        'Continuous thick roof and protected three-bay observation slit',
                        'Steel access door, rain hood and armoured weather-proof vent'],
            'required_sockets':['GroundOrigin','EntryOrigin'],
            'footprint_m':(4.8,4.9),'dimensions':(4.8,4.9,2.75)}


def build_supply_depot(root, col):
    """Compact closed-roof depot with roll-up door and stacked supply crates."""
    _base(root,col,'SupplyDepot',5.0,6.0)
    c.box('SupplyDepotWallBody',(-.4,0,1.52),(3.90,5.66,2.56),root,col,'MAT_IslandWhite')
    c.box('SupplyDepotWallSkirt',(-.4,0,.45),(4.00,5.77,.36),root,col,'ENV_Concrete')
    # Closed pentagonal prism includes both end gables and underside.
    section=[(-2.47,2.78),(1.73,2.78),(1.73,2.95),(-.37,3.58),(-2.47,2.95)]
    verts=[(x,y,z) for y in (-2.94,2.94) for x,z in section]
    faces=[(4,3,2,1,0),(5,6,7,8,9)]
    faces.extend((i,(i+1)%5,(i+1)%5+5,i+5) for i in range(5))
    c.mesh('SupplyDepotClosedGableRoof',verts,faces,root,col,'MAT_IslandCliff')
    c.box('SupplyDepotShutterFrame',(-.15,2.875,1.37),(2.15,.13,2.31),root,col,'ENV_Concrete')
    c.box('SupplyDepotRollUpShutter',(-.15,2.96,1.34),(1.95,.06,2.18),root,col,'Gunmetal')
    for i in range(12):
        c.box('SupplyDepotShutterSlat',(-.15,3.004,.36+i*.175),(1.88,.024,.044),root,col,'ENV_Concrete')
    c.box('SupplyDepotShutterBottomBar',(-.15,3.012,.28),(2.00,.036,.10),root,col,'MAT_IslandWhite')
    c.box('SupplyDepotShutterHandle',(-.15,3.045,.68),(.30,.055,.042),root,col,'Warning Yellow')
    _door(root,col,'SupplyPersonnel',-1.80,2.886,.24,1.91,.68)
    _window(root,col,'SupplyDepotWindow',1.17,2.88,1.96,.55,.54)
    for x in (-2.45,1.71):
        c.box('SupplyDepotGutter',(x,0,2.91),(.095,5.93,.12),root,col,'Gunmetal')
        c.beam('SupplyDepotDownpipe',(x,-2.75,2.90),
               (x,-2.75,.27),.055,root,col,'Gunmetal')
        wall_x=-2.35 if x < 0 else 1.55
        for z in (.63,1.48,2.33):
            c.beam('SupplyDepotDownpipeWallBracket',(x,-2.75,z),(wall_x,-2.75,z),
                   .038,root,col,'Gunmetal')
    # Boxes are within the slab footprint, on a real load-bearing pallet.
    c.box('DepotSupplyPallet',(1.95,-1.88,.31),(.70,1.24,.14),root,col,'ENV_Wood')
    for y in (-2.18,-1.58):
        c.box('DepotSecuredSupplyCrate',(1.95,y,.64),(.65,.54,.53),root,col,'ENV_Wood')
        for x in (1.73,2.17):
            c.box('DepotCrateStrap',(x,y,.64),(.045,.558,.55),root,col,'Gunmetal')
    c.empty('EntryOrigin',root,col,(-.15,3.06,.24))
    return {'features':['Continuous reinforced base and closed pentagonal gable roof',
                        'Detailed roll-up shutter, personnel door and window',
                        'Roof gutters, grounded downpipes and pallet-secured supply crates'],
            'required_sockets':['GroundOrigin','EntryOrigin'],
            'footprint_m':(5,6.16),'dimensions':(5,6.16,3.58)}
