"""Low-profile craft and markers for the night encounter.

All geometry is original, meter-scale and built with Blender +Y forward, +Z up.
Z=0 is the rest waterline, or the canister attachment point for the flare.
"""
import math
from functools import partial
from mathutils import Vector

from geometry import Mesh, node, HULL, DARK, METAL, PALETTE, YELLOW, WHITE

GLASS = 'Blue black glazing'
RED = 'Emergency Red'
GREEN = 'MAT_art_repair_green'
ORANGE = 'Rescue orange red'
# Suspension lines are 1.2 m along the slope from the canister to the canopy.
FLARE_RIM_Z = .30 + math.sqrt(1.2**2 - (.699-.069)**2)
PALETTE.update({
    DARK: (.018, .032, .036), HULL: (.12, .19, .22), GLASS: (.015, .07, .10),
    METAL: (.035, .05, .06), RED: (.72, .035, .018), GREEN: (.22, .36, .17),
    ORANGE: (.65, .065, .02), WHITE: (.86, .86, .82), YELLOW: (.95, .56, .04),
})


def wedge(mesh, stations, color):
    """Lengthwise stations: position, half beam, keel, chine and crown."""
    rings = []
    for y, w, keel, chine, top in stations:
        rings.append([
            (-w*.35, y, keel), (-w*.80, y, (keel+chine)*.5), (-w, y, chine),
            (-w*.86, y, top-.012), (-w*.40, y, top), (w*.40, y, top),
            (w*.86, y, top-.012), (w, y, chine), (w*.80, y, (keel+chine)*.5),
            (w*.35, y, keel),
        ])
    mesh.loft(rings, color)


def hatch(mesh, y, length, width, base, top):
    """Low wedge cabin with a narrow forward observation slit."""
    rings = []
    for z, scale, shift in ((base,1,0), (top-.084,.82,-.06), (top,.68,-.10)):
        corners = [(-.5,-.47),(-.37,-.5),(.37,-.5),(.5,-.47),(.43,.35),
                   (.26,.50),(-.26,.50),(-.43,.35)]
        rings.append([(x*width*scale, y+dy*length*scale+shift, z) for x,dy in corners])
    mesh.loft(rings, HULL)
    for start,end,normal in ((0,7,(-1,0,0)),(3,4,(1,0,0)),(5,6,(0,1,0))):
        lower=[Vector(rings[0][i]) for i in (start,end)]
        upper=[Vector(rings[1][i]) for i in (start,end)]
        lip=Vector(normal)*.009
        panel=[]
        for along,height in ((.17,.58),(.83,.58),(.83,.83),(.17,.83)):
            panel.append(lower[0].lerp(lower[1],along).lerp(upper[0].lerp(upper[1],along),height))
        mesh.loft([[v-lip for v in panel],[v+lip for v in panel]],GLASS)


def drive(mesh, y, radius):
    mesh.tube((0,y,-.095),(0,-1,0),
              [(0,radius),(.13,radius),(.13,radius*.64),(.025,radius*.64)],
              METAL,8,hollow=True)
    mesh.tube((0,y,-.095),(0,-1,0),[(.027,radius*.39),(.072,radius*.27)],DARK,8)


def suicide(collection, root):
    mesh = Mesh()
    wedge(mesh, [(-3.62,.60,-.24,.05,.23),(-3.39,.79,-.38,.055,.26),
                 (-2.45,.97,-.47,.062,.32),(-1.57,1,-.50,.064,.35),
                 (-.25,.91,-.50,.070,.35),(.95,.74,-.43,.076,.33),
                 (2.28,.48,-.288,.082,.255),(2.75,.32,-.21,.09,.222)], DARK)
    drive(mesh,-3.62,.133)
    for side in (-1,1):
        mesh.beam((side*.78,-2.70,.264),(side*.85,-1.63,.318),.052,DARK,.035)
        mesh.beam((side*.83,-1.63,.322),(side*.54,1.34,.282),.055,DARK,.038)
    for y in (-2.87,.14,.84):
        mesh.box((0,y,.29 if y<0 else .34),(.53,.23,.041),DARK,bevel=.052)
    mesh.object('Hull',collection,root)

    mesh = Mesh()
    hatch(mesh,-1.94,1.60,.90,.325,.70)
    # Roof inspection cover and two low fasteners.
    mesh.box((0,-2.15,.684),(.43,.34,.032),HULL,bevel=.07)
    for x in (-.19,.19):
        mesh.box((x,-2.20,.70),(.103,.041,.010),METAL)
    mesh.object('Hump',collection,root,(0,0,-.005))
    mesh = Mesh()
    wedge(mesh, [(2.747,.32,-.21,.09,.222),(2.88,.277,-.187,.095,.204),
                 (3.39,.139,-.073,.119,.170),(3.75,.018,.110,.126,.143)], HULL)
    mesh.object('WarheadNose',collection,root)

    node('Exhaust',collection,root,(0,-3.75,0),(0,-1,0))
    node('BlastPoint',collection,root,(0,3.65,.135),(0,1,0))


def cigar(mesh):
    stations = [(-5.36,.53,-.32,.28),(-5.05,.72,-.53,.36),(-4.20,.97,-.64,.415),
                (-2.9,1.07,-.70,.45),(-.72,1.10,-.70,.445),(1.86,.92,-.61,.435),
                (3.63,.66,-.41,.372),(4.88,.39,-.27,.284),(5.36,.20,-.10,.23),
                (5.50,.062,.035,.17)]
    segments = 12
    rings = []
    for y, half_width, keel, top in stations:
        center = max(-.12,(keel+min(top,keel+.12))/2)
        points = []
        for i in range(segments):
            angle = i*math.tau/segments
            along = math.sin(angle)
            radius = top-center if along >= 0 else center-keel
            points.append((half_width*math.cos(angle),y,center+radius*along))
        rings.append(points)
    mesh.loft(rings,DARK)


def torpedo(collection, root):
    mesh = Mesh()
    cigar(mesh)
    drive(mesh,-5.36,.148)
    for side in (-1,1):
        mesh.beam((side*.66,-4.63,.205),(side*.79,-3.59,.249),.041,DARK,.038)
        mesh.box((side*.667,-3.66,.277),(.10,.57,.057),METAL)
    mesh.box((0,3.21,.338),(.59,.43,.086),DARK,bevel=.07)
    mesh.object('Hull',collection,root)

    mesh = Mesh()
    hatch(mesh,-1.95,1.88,1.12,.345,.705)
    mesh.tube((0,-2.20,0),(0,0,1),[(.492,.103),(.67,.084),(.962,.068)],METAL,8)
    mesh.tube((0,-2.20,0),(0,0,1),[(.950,.078),(.99,.074),(1.0,.061)],HULL,8)
    mesh.box((0,-1.98,.700),(.44,.34,.023),HULL,bevel=.06)
    mesh.object('Hump',collection,root)

    for side,label in ((-1,'Port'),(1,'Starboard')):
        mesh = Mesh()
        mesh.tube((0,0,0),(0,1,0),[
            (-1.75,.154),(-1.49,.225),(1.75,.225),(1.75,.157),(1.52,.153),(-1.75,.036)
        ], METAL, 6, bands=[DARK,DARK,HULL,METAL,METAL,DARK],hollow=True)
        for y in (-1.05,.76):
            mesh.beam((-side*.30,y,.04),(-side*.12,y,.08),.121,DARK,.16)
        mount=mesh.object('TorpedoTube_'+label,collection,root,(side*1.175,-.96,.010))
        node('TorpedoLaunchPoint_'+label,collection,mount,(0,1.765,0),(0,1,0))
    node('Exhaust',collection,root,(0,-5.50,0),(0,-1,0))
    node('SmokePoint',collection,root,(0,-2.20,1.005),(0,0,1))
    node('FirePoint',collection,root,(0,-3.62,.425),(0,0,1))


def channel_buoy(collection, root, color):
    mesh = Mesh()
    mesh.tube((0,0,0),(0,0,1),[(-.25,.33),(-.13,.53),(.075,.60),
                               (.42,.60),(.50,.49),(.535,.43)],color,8,
              bands=[METAL,color,color,color,METAL])
    mesh.object('Float',collection,root)
    mesh = Mesh()
    # Four slightly tapered cage posts and diagonal braces around the open center.
    for i in range(4):
        angle=math.pi*.25+i*math.pi*.5
        next_angle=angle+math.pi*.5
        x,y=math.cos(angle),math.sin(angle)
        mesh.beam((.405*x,.405*y,.51),(.175*x,.175*y,2.29),.075,METAL)
        if i%2==0:
            mesh.beam((.405*x,.405*y,.55),
                      (.186*math.cos(next_angle),.186*math.sin(next_angle),1.94),.047,METAL)
    mesh.tube((0,0,0),(0,0,1),[(1.99,.28),(2.095,.28)],METAL,4)
    mesh.tube((0,0,0),(0,0,1),[(2.25,.25),(2.35,.162 if color==RED else .25)],color,6)
    mesh.object('Tower',collection,root)
    node('LampPoint',collection,root,(0,0,2.46),(0,0,1))


def yellow_buoy(collection, root):
    mesh = Mesh()
    mesh.tube((0,0,0),(0,0,1),[(-.25,.33),(-.125,.47),(.11,.51),
                               (.40,.43),(.52,.32)],YELLOW,8,
              bands=[METAL,YELLOW,YELLOW,YELLOW])
    mesh.tube((0,0,0),(0,0,1),[(.49,.12),(1.38,.092),(1.53,.124)],METAL,8)
    mesh.tube((0,0,0),(0,0,1),[(1.49,.166),(1.54,.166)],YELLOW,6)
    mesh.object('FloatAndMast',collection,root)
    mesh = Mesh()
    for axis in ((1,0),(0,1)):
        u,v=axis
        for s in (-1,1):
            mesh.beam((-s*u*.245,-s*v*.245,1.625),(s*u*.245,s*v*.245,2.098),.087,YELLOW,.061)
    mesh.object('Daymark',collection,root)
    node('LampPoint',collection,root,(0,0,2.197),(0,0,1))


def distress(collection, root):
    mesh = Mesh()
    mesh.tube((0,0,0),(0,0,1),[(-.26,.19),(-.15,.30),(.03,.33),
                               (.30,.30),(.375,.23),(.45,.19)],ORANGE,8,
              bands=[METAL,ORANGE,ORANGE,WHITE,ORANGE])
    mesh.object('Float',collection,root)
    mesh = Mesh()
    mesh.beam((-.087,0,.435),(-.17,0,.926),.024,METAL)
    mesh.beam((0,0,.44),(0,0,.56),.094,METAL)
    mesh.object('Antenna',collection,root)
    node('LampPoint',collection,root,(-.17,0,.939),(0,0,1))


def sonobuoy(collection, root):
    mesh = Mesh()
    mesh.tube((0,0,0),(0,0,1),[(-.30,.099),(-.265,.134),(.21,.134),(.278,.095)],
              YELLOW,6,bands=[METAL,YELLOW,METAL])
    mesh.tube((0,0,0),(0,0,1),[(-.060,.166),(-.018,.225),(.078,.225),(.117,.168)],
              YELLOW,8,bands=[YELLOW,WHITE,YELLOW])
    mesh.beam((0,0,.272),(0,0,.60),.018,METAL)
    mesh.object('Body',collection,root)
    node('LampPoint',collection,root,(0,0,.610),(0,0,1))


def flare(collection, root):
    mesh = Mesh()
    # Suspended visual with the bottom of the canister at the FX attachment origin.
    rings = []
    for radius,z in ((.18,FLARE_RIM_Z+.334),(.52,FLARE_RIM_Z+.214),(.70,FLARE_RIM_Z)):
        rings.append([(radius*math.cos(i*math.tau/8),radius*math.sin(i*math.tau/8),
                       z - (.022 if i%2 else 0)) for i in range(8)])
    mesh.loft(rings,WHITE,caps=False)
    # A second layer gives the fabric consistent culling from either side.
    offset=len(mesh.vertices)
    mesh.add([(x,y,z-.012) for x,y,z in mesh.vertices],
             [tuple(reversed(face)) for face in mesh.faces],WHITE)
    # Weld the thin canopy into a closed shell at the vent and hem.
    for start in (0,16):
        for i in range(8):
            j=(i+1)%8
            mesh.faces.append((start+i,start+j,offset+start+j,offset+start+i))
            mesh.colors.append(WHITE)
    mesh.object('Canopy',collection,root)
    mesh = Mesh()
    for i in (0,2,4,6):
        angle=i*math.tau/8
        u,v=math.cos(angle),math.sin(angle)
        mesh.beam((u*.069,v*.069,.300),(u*.699,v*.699,FLARE_RIM_Z),.013,WHITE,.011)
    mesh.object('SuspensionLines',collection,root)
    mesh = Mesh()
    mesh.tube((0,0,0),(0,0,1),[(0,.077),(.30,.077)],METAL,5)
    mesh.object('Canister',collection,root)
    node('FlarePoint',collection,root,(0,0,0),(0,0,-1))


# Dimensions are width, length, lower Z, and upper Z, all in meters.
# Flare canopy diameter is represented by its XY envelope.
SPECS = [
    ('ENE_SemiSubSuicide','SEMI-SUBMERSIBLE / SUICIDE',suicide,(350,600),(2,7.5,-.5,.7)),
    ('ENE_SemiSubTorpedo','SEMI-SUBMERSIBLE / TORPEDO',torpedo,(500,800),(2.8,11,-.7,1)),
    ('ENV_ChannelBuoy_Red','CHANNEL BUOY / RED',partial(channel_buoy,color=RED),(120,200),(1.2,1.2,-.25,2.35)),
    ('ENV_ChannelBuoy_Green','CHANNEL BUOY / GREEN',partial(channel_buoy,color=GREEN),(120,200),(1.2,1.2,-.25,2.35)),
    ('ENV_LightBuoy_Yellow','SHOAL WARNING BUOY',yellow_buoy,(120,200),(1.02,1.02,-.25,2.15)),
    ('ENV_DistressBeacon','DISTRESS BEACON',distress,(60,120),(.66,.66,-.26,.94)),
    ('PRJ_Sonobuoy','SONOBUOY',sonobuoy,(60,120),(.45,.45,-.3,.6)),
    ('FX_ParachuteFlare','PARACHUTE FLARE',flare,(80,160),(1.4,1.4,0,FLARE_RIM_Z+.334)),
]

