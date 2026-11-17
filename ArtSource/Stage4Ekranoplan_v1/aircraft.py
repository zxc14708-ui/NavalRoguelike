"""Stage 4 ground effect vehicles. Geometry is for the fictional game scale."""
import math

from mathutils import Vector

from geometry import (Mesh, node, hull, wing, vertical_tail, float_tip,
                      HULL, STEEL, DARK, GUN, METAL, GLASS, RED, WHITE)


def motor(mesh, length, radius, detail=10):
    # Recessed intake and exhaust read as separate apertures at combat distance.
    mesh.tube((0,0,0),(0,1,0),[
        (-length/2,radius*.70),(-length*.32,radius*.86),(.35*length,radius),
        (length/2,radius*.94),(length/2,radius*.66),(.29*length,radius*.63),
        (-.3*length,radius*.47),(-length/2,radius*.48)
    ],GUN,detail,[METAL,GUN,GUN,STEEL,METAL,DARK,DARK,STEEL],hollow=True)
    mesh.tube((0,.24*length,0),(0,1,0),[(-.03,radius*.60),(.03,radius*.60)],DARK,detail)
    mesh.tube((0,.24*length,0),(0,1,0),[(.018,radius*.22),(.10,radius*.115)],STEEL,detail)
    for i in range(6):
        angle=i*math.tau/6
        u,v=math.cos(angle),math.sin(angle)
        mesh.beam((u*radius*.17,.27*length,v*radius*.17),
                  (u*radius*.58,.27*length,v*radius*.58),radius*.11,GUN)


def cockpit(mesh, y, z, width, length):
    # Wrapped canopy: broad forward pane, two sloped sides and distinct central framing.
    rings = []
    for yy,w,lo,hi in ((-.5,.79,0,.23),(.08,1,.03,.34),(.50,.69,-.02,.14)):
        rings.append([(x*width,y+yy*length,z+h*width) for x,h in
                      ((-.5*w,lo),(-.37*w,hi),(.37*w,hi),(.5*w,lo))])
    mesh.loft(rings,GLASS)
    for j in (0,1,2):
        mesh.beam(rings[j][1],rings[j][2],width*.042,STEEL)
    for a,b in zip(rings,rings[1:]):
        mesh.beam(tuple((x+y)*.5 for x,y in zip(a[1],a[2])),
                  tuple((x+y)*.5 for x,y in zip(b[1],b[2])),width*.036,STEEL)


def propeller(mesh, radius, count=5, axial=0):
    for i in range(count):
        a=math.tau*i/count
        u,v=math.cos(a),math.sin(a)
        mesh.beam((u*radius*.12,axial,v*radius*.12),
                  (u*radius*.91-v*radius*.18,axial+.03,v*radius*.91+u*radius*.18),
                  radius*.146,GUN,radius*.07)
        mesh.beam((u*radius*.91-v*radius*.18,axial+.03,v*radius*.91+u*radius*.18),
                  (u*radius-v*radius*.18,axial+.034,v*radius+u*radius*.18),radius*.127,WHITE,radius*.06)
    mesh.tube((0,axial,0),(0,1,0),[(-radius*.17,radius*.19),(.12*radius,radius*.18),
                                     (.23*radius,radius*.06)],STEEL,8)


def lun(a):
    c,r=a.collection,a.root
    m=Mesh()
    hull(m,[(-15,.19,1.63,2.03,2.89),(-13.8,.64,.97,1.32,2.86),(-11.5,1.03,.24,.65,2.80),
            (-8,1.36,-.14,.35,2.75),(-5.6,1.51,-.28,.27,2.78),(-4.9,1.52,-.43,.25,2.79),
            (-1,1.60,-.48,.22,2.85),(3.5,1.57,-.48,.25,2.89),(7.5,1.47,-.39,.34,2.89),
            (10.7,1.31,-.27,.42,2.86),(12.5,1.06,-.14,.53,2.51),(13.5,.78,.24,.76,2.04)])
    for side in (-1,1):
        m.beam((side*1.57,-4.8,.31),(side*1.51,7,.40),.055,STEEL)
        m.box((side*1.39,8.65,1.96),(.09,.87,.34),GUN,bevel=.08)
        for y,x,z in ((-10.25,1.06,1.82),(-8.9,1.22,1.76),(3.75,1.47,1.79),(5.3,1.45,1.83)):
            m.box((side*x,y,z),(.075,.45,.24),STEEL,bevel=.035)
        m.box((side*1.52,-2.35,1.18),(.07,1.04,.105),DARK)
    m.object('Hull',c,r)
    m=Mesh()
    hull(m,[(0,.775,.24,.76,2.04),(.65,.60,.43,.79,1.88),(1.24,.29,.71,.99,1.65),(1.5,.04,1.14,1.20,1.34)],STEEL)
    m.object('NoseRadome',c,r,(0,13.5,0))
    m=Mesh()
    cockpit(m,10.13,2.66,2.24,2.15)
    m.object('CockpitGlass',c,r)

    tail=Mesh()
    vertical_tail(tail,[(2.65,-14.72,-10.45,.41),(3.70,-14.6,-10.73,.36),
                        (6.25,-14.47,-11.6,.25),(7.62,-14.5,-12.3,.17),(7.8,-14.5,-12.54,.11)])
    for side in (-1,1):
        wing(tail,side,[(.03,-14.81,-11.46,7.46,.42),(3.38,-14.81,-12.01,7.50,.40),
                        (4.25,-14.55,-12.56,7.52,.26)])
        tail.beam((side*.22,-14.62,5.93),(side*.23,-12.09,5.93),.18,RED)
    tail.object('Tail',c,r)
    for side,label,tag in ((-1,'Port','P'),(1,'Starboard','S')):
        m=Mesh()
        wing(m,side,[(1.28,-7.6,-.72,.98,.68),(3.2,-7.37,-.79,.92,.65),
                     (7.82,-7.30,-1.47,.90,.48),(8.82,-7.03,-2.11,.94,.39)])
        float_tip(m,side*8.64,-4.87,5.18,.36)
        for y in (-5.72,-3.68):
            m.beam((side*8.57,y,.32),(side*8.47,y,.87),.17,STEEL)
        m.beam((side*2,-6.91,1.14),(side*7.78,-6.84,1.11),.048,STEEL)
        m.object('Wing_'+label,c,r)
        node('WakePoint_'+label,c,r,(side*8.64,-5.42,-.31),(0,-1,0))
        m=Mesh()
        wing(m,side,[(1.13,6.4,9.78,2.54,.49),(3.1,6.82,9.18,2.55,.43),
                     (5.2,7.06,8.79,2.50,.37)],None)
        pylon=m.object('EnginePylon_'+label,c,r)
        for i,x in enumerate((2.2,3.1,4.0,4.9),1):
            engine=Mesh()
            motor(engine,3,.40,8)
            engine.box((0,-.04,-.37),(.31,1.76,.24),STEEL,bevel=.08)
            k=tag+str(i)
            part=engine.object('Engine_'+k,c,pylon,(side*x,8,2.9))
            node('EngineIntake_'+k,c,part,(0,1.50,0),(0,1,0))
            node('EngineExhaust_'+k,c,part,(0,-1.50,0),(0,-1,0))
            node('EngineFire_'+k,c,part,(0,-.74,.29),(0,0,1))

    elevation=math.radians(12)
    forward=Vector((0,math.cos(elevation),math.sin(elevation)))
    upward=Vector((0,-math.sin(elevation),math.cos(elevation)))
    for i,(y,z) in enumerate(((3,3.58),(-1,4.10),(-5,4.62)),1):
        m=Mesh()
        for x in (-.75,.75):
            m.tube((x,0,0),forward,[(-2.10,.28),(-1.94,.40),(-1.64,.45),(1.95,.45),
                                    (2.10,.43),(2.10,.35),(1.77,.34)],STEEL,12,
                   [GUN,STEEL,HULL,STEEL,GUN,DARK,DARK],hollow=True)
            for d in (-1.32,1.30):
                m.tube((x,0,0),forward,[(d-.09,.456),(d+.09,.456)],STEEL,12)
            m.beam((x*.89,-.53,2.52-z),(x,-.53,-.19),.27,GUN,depth=.43)
        m.beam((-.76,-.43,2.72-z),(.76,-.43,2.72-z),.23,GUN)
        number=f'{i:02}'
        pair=m.object('LauncherPair_'+number,c,r,(0,y,z))
        for x,side in ((-.75,'L'),(.75,'R')):
            center=Vector((x,0,0))+forward*2.12
            hinge=center+upward*.40
            door=Mesh()
            door.tube(tuple(center-hinge),forward,[(-.043,.405),(.007,.415),(.055,.38)],RED,12)
            door.beam((-.20,0,0),(.20,0,0),.095,STEEL)
            door.object(f'LauncherHatch_{number}_{side}',c,pair,hinge)
            node(f'LaunchPoint_{number}_{side}',c,pair,center+forward*.065,forward)

    turret=node('TailTurretPivot',c,r,(0,-14.2,3.4))
    m=Mesh()
    m.tube((0,0,0),(0,0,1),[(-.27,.30),(-.20,.48),(.20,.49),(.49,.21)],STEEL,12)
    m.object('TailTurretHousing',c,turret)
    elev=node('TailTurretElevation',c,turret,(0,-.37,.10))
    m=Mesh()
    for x in (-.19,.19):
        m.tube((x,0,0),(0,-1,0),[(0,.093),(.15,.093),(.73,.056),(.90,.064)],GUN,8)
    # Keep the barrels within the 30 m nominal hull extent by recessing their seat.
    barrels=m.object('TailTwinBarrels',c,elev,(0,.49,0))
    node('TailTurretMuzzle',c,elev,(0,barrels.location.y-.90,0),(0,-1,0))
    node('WaterContact',c,r,(0,.5,-.48),(0,0,-1))
    node('SmokePoint_Hull',c,r,(0,6,2.92),(0,0,1))
    node('FirePoint_Tail',c,r,(0,-12.4,3.18),(0,0,1))
    node('BargeMooring',c,r,(-1.65,0,.48),(-1,0,0))


def light(a):
    c,r=a.collection,a.root
    m=Mesh()
    hull(m,[(-3.75,.4,-.02,.16,.52),(-2.65,.68,-.34,.21,.74),(-.55,.78,-.38,.24,.89),
            (1.53,.70,-.35,.24,.85),(2.55,.49,-.22,.25,.69),(3.36,.26,-.01,.25,.47),
            (3.75,.027,.17,.22,.31)])
    cockpit(m,1.25,.74,1.28,1.78)
    m.object('Hull',c,r)
    m=Mesh()
    for side in (-1,1):
        wing(m,side,[(.60,-2.30,1.06,.47,.22),(2.50,-2.11,.11,.44,.18),(3,-1.94,-.24,.43,.14)])
    m.object('Wing',c,r)
    m=Mesh()
    vertical_tail(m,[(.48,-3.52,-1.89,.17),(1.10,-3.50,-2.10,.14),(1.84,-3.35,-2.58,.085)])
    for side in (-1,1):
        wing(m,side,[(.0,-3.68,-2.84,1.0,.16),(1.13,-3.48,-2.83,1.10,.12)],None)
    m.tube((0,-2.61,1.59),(0,1,0),[(-.36,.56),(.40,.61),(.40,.46),(-.36,.46)],STEEL,10,
           [HULL,STEEL,DARK,STEEL],hollow=True)
    m.object('Tail',c,r)
    m=Mesh()
    propeller(m,.443,3)
    m.object('Propeller',c,r,(0,-2.70,1.59))
    node('GunMuzzle',c,r,(0,3.68,.24),(0,1,0))
    node('Exhaust',c,r,(0,-3.04,1.59),(0,-1,0))


def assault(a):
    c,r=a.collection,a.root
    m=Mesh()
    hull(m,[(-9.5,.44,.13,.53,1.66),(-8.1,1.03,-.27,.40,1.93),(-5.5,1.26,-.46,.44,2.15),
            (-1,1.33,-.45,.49,2.30),(3.2,1.29,-.43,.49,2.29),(5.8,1.19,-.29,.54,2.11)],cargo_recess=1.65)
    cockpit(m,4.3,2.00,1.90,2.12)
    m.box((0,5.74,.18),(1.55,.42,.46),DARK)
    for side in (-1,1):
        for y in (-1.5,-.92,-.34,.24,.82,1.4):
            m.box((side*1.266,y,1.56),(.042,.26,.21),GLASS,bevel=.011)
        m.box((side*1.29,3.09,.86),(.09,.78,.36),GUN,bevel=.05)
        m.beam((side*1.31,-4.52,.58),(side*1.29,5.43,.65),.045,STEEL)
    m.object('Hull',c,r)
    hinge=(1.07,5.8,.52)
    door=node('NoseDoorPivot',c,r,hinge)
    m=Mesh()
    hull(m,[(0,1.185,-.29,.54,2.11),(1.16,1.07,-.17,.47,1.89),(2.28,.73,.01,.46,1.48),
            (3.22,.37,.21,.47,1.04),(3.70,.05,.47,.57,.70)])
    m.object('NoseDoor',c,door,(-hinge[0],0,-hinge[2]))
    node('BoatExit',c,r,(0,5.85,.35),(0,1,0))
    for i,y in enumerate((4.51,2.38),1):
        node(f'BoatStaging_{i:02}',c,r,(0,y,.34),(0,1,0))
    for side,label in ((-1,'Port'),(1,'Starboard')):
        m=Mesh()
        wing(m,side,[(1.15,-4.05,1.6,.83,.51),(4.32,-3.95,.38,.76,.35),(5.25,-3.62,-.18,.75,.26)])
        float_tip(m,side*4.98,-2.18,3.25,.27)
        for y in (-2.96,-1.47):
            m.beam((side*4.95,y,.26),(side*4.91,y,.72),.15,STEEL)
        m.object('Wing_'+label,c,r)
        node('WakePoint_'+label,c,r,(side*4.98,-2.33,-.24),(0,-1,0))
    m=Mesh()
    vertical_tail(m,[(1.53,-9.25,-6.65,.37),(3.10,-9.00,-6.92,.31),
                    (4.34,-8.83,-7.27,.23),(4.44,-8.71,-7.69,.15)])
    for side in (-1,1):
        wing(m,side,[(.07,-9.21,-7.03,4.0,.27),(2.45,-9.21,-7.39,4.07,.22),(2.84,-8.97,-7.87,4.10,.15)])
    m.tube((0,-8.06,4.22),(0,1,0),[(-.71,.28),(-.55,.44),(.49,.45),(.85,.31)],STEEL,10)
    m.object('Tail',c,r)
    m=Mesh()
    propeller(m,.94,6,-.15)
    propeller(m,.94,6,.18)
    m.object('TailPropeller',c,r,(0,-7.10,4.25))
    node('SmokePoint',c,r,(0,1.0,2.32),(0,0,1))
    node('FirePoint',c,r,(0,-7.98,4.40),(0,0,1))

