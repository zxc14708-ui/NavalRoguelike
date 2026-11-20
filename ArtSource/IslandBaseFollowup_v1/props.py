"""Terrain-compatible props at the scale of the existing island kit."""
import math
import random

from geometry import Mesh, node, METAL, DARK, YELLOW, WHITE, CONCRETE, GLASS

CLIFF = 'MAT_IslandCliff'
ROCK = 'MAT_IslandRock'
RUST = 'MAT_IslandRust'
RED = 'MAT_IslandRed'
DECK = 'Deck Grey'


def pier(asset, quay):
    root, collection = asset.root, asset.collection
    length, width = (19, 9) if quay else (22, 5.8)
    half_length, half_width = length/2, width/2
    mesh = Mesh()
    # The ramp reaches the beach behind the ShoreConnection; the deck is fixed.
    mesh.box((0,0,.78), (width,length,.62), CONCRETE, bevel=.32)
    mesh.box((0,0,1.105), (width-.24,length-.25,.08), DECK, bevel=.23)
    ramp = [(-half_width+.25,-half_length-2.4),(half_width-.25,-half_length-2.4),
            (half_width-.25,-half_length+.24),(-half_width+.25,-half_length+.24)]
    mesh.loft([[(x,y,-.28) for x,y in ramp],
               [(x,y,.20 if y < -half_length else 1.14) for x,y in ramp]], CONCRETE)
    if not quay:
        mesh.box((0,0,-.95), (width-.60,length-1.1,2.85), CONCRETE, bevel=.40)
        for side in (-1,1):
            for row in range(11):
                mesh.box((side*(half_width-.32),-9.8+1.9*row,-.17),
                         (.76,1.7,1.13), ROCK, yaw=side*.12*((row%3)-1), bevel=.21)
    else:
        for x in (-3.4,3.4):
            for y in (-7.9,-3.95,0,3.95,7.9):
                mesh.tube((x,y,0),(0,0,1),[(-3.3,.40),(.67,.40)],CONCRETE,n=8)
    mesh.object('DeckAndSubstructure',collection,root)

    details = Mesh()
    for side in (-1,1):
        for y in (-half_length+2,-half_length/3,half_length/3,half_length-1):
            details.box((side*(half_width+.03),y,.31),(.22,1.03,.48),DARK)
            x = side*(half_width-.52)
            details.box((x,y,1.18),(.46,.34,.10),METAL)
            details.tube((x,y,1.18),(0,0,1),[(0,.13),(.25,.13),(.30,.17)],METAL,n=6)
            details.box((side*(half_width-.17),y,1.157),(.12,.88,.022),YELLOW)
    for y in (-half_length*.5, 0, half_length*.5):
        details.box((0,y,1.158),(width-.7,.032,.016),CONCRETE)
    details.box((0,half_length-.31,1.16),(width-.65,.12,.026),YELLOW)
    details.object('BollardsAndFenders',collection,root)
    node('ShoreConnection',collection,root,(0,-half_length,0),direction=(0,1,0))
    for side, label in [(-1,'Port'),(1,'Starboard')]:
        node('MooringPoint_'+label,collection,root,(side*(half_width+.3),.9,0),direction=(0,1,0))
    node('PierEnd',collection,root,(0,half_length,1.145),direction=(0,1,0))


def rubble(mesh, width, depth, count, seed):
    rng = random.Random(seed)
    for _ in range(count):
        x,y = rng.uniform(-width*.41,width*.41), rng.uniform(-depth*.40,depth*.40)
        s,h = rng.uniform(.23,.57),rng.uniform(.15,.35)
        mesh.box((x,y,.31+h/2),(s,s*rng.uniform(.63,1.25),h),
                 rng.choice((CLIFF,ROCK,CONCRETE)),yaw=rng.uniform(-math.pi,math.pi),bevel=s*.18)


def ruins(asset, kind):
    root, collection = asset.root, asset.collection
    sizes = {'Battery':(4.8,5.6), 'Radar':(4,4), 'Outpost':(4.8,4)}
    w,d = sizes[kind]
    mesh = Mesh()
    mesh.box((0,0,.15),(w,d,.30),CONCRETE,bevel=.20)
    mesh.box((0,.12,.301),(w*.71,d*.71,.025),ROCK,bevel=.51)
    if kind == 'Battery':
        mesh.tube((0,0,0),(0,0,1),[(.30,1.03),(.63,.96),(.69,.75)],DARK,n=12)
        for x, y, yaw in ((-.72,.46,-.33),(.91,-.44,.20)):
            mesh.box((x,y,.79),(1.10,2.46,.89),CLIFF,yaw=yaw,bevel=.14)
            mesh.box((x-.10,y+.55,1.235),(.55,.43,.029),RED,yaw=yaw)
        mesh.beam((-1.48,-1.71,.32),(-.48,.73,.89),.105,METAL)
        mesh.beam((.37,-2.06,.32),(1.54,-.84,.57),.14,RUST)
    elif kind == 'Radar':
        mesh.box((0,-.43,.69),(1.63,1.06,.78),CLIFF,bevel=.12)
        for x in (-.67,.67):
            for y in (-.76,.62):
                mesh.beam((x,y,.30),(.49+x*.46,y*.59,1.42),.095,METAL)
        mesh.beam((.35,-.54,1.39),(.77,1.22,.46),.21,RUST)
        mesh.beam((1.59,.76,.49),(-1.33,1.12,.60),.27,METAL,.39)
        for x in (-1.06,-.53,0,.53,1.06):
            mesh.beam((x,1.08,.52),(x,1.56,.36),.056,CLIFF)
    else:
        # Uneven wall heights break the silhouette without adding scattered debris outside the lot.
        for x,y,length,heights in ((-2.08,0,2.9,(.84,1.83,1.44,.63)),
                                  (2.00,.44,2.33,(.51,.73,1.35,1.72))):
            steps = [y+length*(i/3-.5) for i in range(4)]
            for i in range(3):
                a,b = steps[i:i+2]
                A,B = heights[i:i+2]
                rect = [(x-.13,a),(x+.13,a),(x+.13,b),(x-.13,b)]
                mesh.loft([[(u,v,.30) for u,v in rect],
                           [(u,v,A if v==a else B) for u,v in rect]],CLIFF)
        mesh.beam((-1.82,-1.23,1.05),(.84,.55,.37),.20,METAL,.83)
        mesh.beam((-1.76,.78,1.06),(.30,1.03,.35),.17,RUST,.47)
    rubble(mesh,w,d,10, {'Battery':41,'Radar':17,'Outpost':9}[kind])
    mesh.object('Debris',collection,root)
    node('SmokePoint',collection,root,(0,0,.68))


def helipad(asset):
    mesh = Mesh()
    mesh.box((0,0,.105),(13,13,.21),CONCRETE,bevel=.50)
    mesh.box((0,0,.222),(12.0,12.0,.045),DECK,bevel=.54)
    for side in (-1,1):
        mesh.box((side*.64,0,.253),(.25,2.25,.018),WHITE)
        for x,y,angle in ((side*4.75,0,0),(0,side*4.75,math.pi/2)):
            mesh.box((x,y,.253),(.11,6.8,.018),WHITE,yaw=angle)
        for x in (-5.55,5.55):
            mesh.box((x,side*5.52,.269),(.24,.24,.093),METAL,bevel=.044)
            mesh.box((x,side*5.52,.322),(.14,.14,.035),YELLOW,bevel=.035)
    mesh.box((0,0,.257),(1.31,.25,.018),WHITE)
    mesh.box((0,-5.51,.255),(1.70,.075,.018),YELLOW)
    mesh.object('LandingPad',asset.collection,asset.root)
    node('LandingSpot',asset.collection,asset.root,(0,0,.248),direction=(0,1,0))


def searchlight(asset):
    collection, root = asset.collection, asset.root
    base = Mesh()
    base.box((0,0,.105),(1.27,1.27,.21),CONCRETE,bevel=.11)
    base.tube((0,0,0),(0,0,1),[(.205,.38),(.35,.38),(.80,.22),(1.07,.22)],METAL,n=10)
    base.object('Pedestal',collection,root)
    yaw = node('SearchlightYaw',collection,root,(0,0,1.05))
    bracket = Mesh()
    bracket.box((0,0,.06),(.74,.42,.13),METAL,bevel=.06)
    for side in (-1,1):
        bracket.box((side*.30,0,.29),(.11,.29,.48),METAL,bevel=.03)
    bracket.object('Yoke',collection,yaw)
    pitch = node('SearchlightPitch',collection,yaw,(0,0,.43))
    lamp = Mesh()
    lamp.tube((0,0,0),(0,1,0),[(-.33,.231),(-.22,.281),(.36,.281),(.46,.259),
                               (.46,.225),(.386,.225)],CLIFF,n=12,hollow=True)
    lamp.tube((0,0,0),(0,1,0),[(.387,.223),(.405,.223)],GLASS,n=12)
    lamp.box((0,-.24,.26),(.32,.21,.05),METAL,bevel=.041)
    lamp.object('LampHousing',collection,pitch)
    node('BeamOrigin',collection,pitch,(0,.468,0),direction=(0,1,0))


def supply(asset):
    mesh = Mesh()
    mesh.box((0,0,.30),(1.54,1.09,.83),CLIFF,bevel=.14)
    mesh.box((0,0,.74),(1.61,1.15,.12),CONCRETE,bevel=.13)
    for x in (-.67,.67):
        mesh.box((x,0,-.015),(.25,1.24,.35),YELLOW,bevel=.095)
        mesh.box((x,0,.37),(.11,1.16,.67),METAL)
        mesh.box((x,0,.816),(.11,1.00,.032),METAL)
    for y in (-.565,.565):
        mesh.box((0,y,.56),(.38,.038,.11),YELLOW)
    mesh.box((0,0,.815),(.54,.32,.026),DARK,bevel=.048)
    mesh.box((0,0,.833),(.32,.070,.015),WHITE)
    mesh.box((0,0,.833),(.070,.24,.015),WHITE)
    mesh.object('FloatingCrate',asset.collection,asset.root)
    node('PickupPoint',asset.collection,asset.root,(0,0,.81))


def sos(asset):
    mesh = Mesh()
    S = [(.95,1.0),(-.95,1.0),(-.95,0),(.95,0),(.95,-1.0),(-.95,-1.0)]
    O = [(-.87,.79),(-.64,1),(.64,1),(.87,.79),(.87,-.79),(.64,-1),
         (-.64,-1),(-.87,-.79),(-.87,.79)]
    for center,line in ((-2.38,S),(0,O),(2.38,S)):
        for a,b in zip(line,line[1:]):
            p,q = (a[0]+center,a[1],.094),(b[0]+center,b[1],.094)
            mesh.beam(p,q,.15,WHITE,depth=.13)
    for x,y in [(-3.21,-.97),(-1.47,1),(-.76,-.88),(2.07,.99),(3.29,-.13)]:
        mesh.box((x,y,.067),(.30,.20,.133),ROCK,yaw=.27,bevel=.048)
    mesh.object('SosMarkers',asset.collection,asset.root)
