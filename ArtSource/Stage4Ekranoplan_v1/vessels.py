"""Surface vessels, platform turret and maritime props."""
import math

from geometry import (Mesh, node, hull, wing, HULL, STEEL, DARK, GUN, METAL,
                      GLASS, RED, DECK, YELLOW, WHITE, CONCRETE)


def naval_gun(a, parent, at, names, size=1, twin=False):
    c=a.collection
    yaw_name, pitch_name, muzzle_name = names
    yaw=node(yaw_name,c,parent,at)
    m=Mesh()
    m.tube((0,0,0),(0,0,1),[(0,.45*size),(.18*size,.45*size)],DARK,8)
    outline=[(-.37,-.50),(.37,-.50),(.465,-.37),(.465,.28),(.29,.43),(-.29,.43),(-.465,.28),(-.465,-.37)]
    m.loft([[(x*s*size,y*s*size,z*size) for x,y in outline] for s,z in ((1,.16),(1,.41),(.69,.78))],STEEL)
    m.object(yaw_name+'_Housing',c,yaw)
    pitch=node(pitch_name,c,yaw,(0,.29*size,.55*size))
    m=Mesh()
    for x in ((-.135,.135) if twin else (0,)):
        m.tube((x*size,0,0),(0,1,0),[(d*size,r*size) for d,r in
               ((-.10,.098),(.18,.095),(.37,.056),(1.16,.054))],GUN,6)
    m.object(pitch_name+'_Barrels',c,pitch)
    node(muzzle_name,c,pitch,(0,1.16*size,0),(0,1,0))
    return yaw


def bridge(mesh, at, w,l,h):
    x,y,z=at
    k=.21
    square=[(-.5,-.5),(.5,-.5),(.5,.5-k),(.5-k,.5),(-.5+k,.5),(-.5,.5-k)]
    mesh.loft([[(x+p*w*s,y+q*l*s+dy,z+dz) for p,q in square]
               for dz,s,dy in ((0,1,0),(.56*h,1,0),(h,.83,-.14*l))],STEEL)
    for i in range(4):
        mesh.box((x+(i-1.5)*w*.164,y+.38*l,z+.70*h),(w*.145,l*.086,h*.19),GLASS)
    for side in (-1,1):
        for yy in (-.26,0,.20):
            mesh.box((x+side*.473*w,y+yy*l,z+.66*h),(.085,.14*l,.15*h),GLASS)
    mesh.box((x,y-.10*l,z+h),(.86*w,.83*l,.10),HULL)


def radar(a,parent,at,scale):
    c=a.collection
    p=node('RadarPivot',c,parent,at)
    m=Mesh()
    m.tube((0,0,0),(0,0,1),[(0,.15*scale),(.13*scale,.15*scale)],METAL,8)
    m.box((0,0,.22*scale),(1.50*scale,.23*scale,.25*scale),STEEL,bevel=.065*scale)
    m.box((0,.123*scale,.22*scale),(1.35*scale,.028,.15*scale),GLASS)
    m.object('RadarArray',c,p)


def hovercraft(a):
    c,r=a.collection,a.root
    m=Mesh()
    skirt=[(-3.12,-8.99),(3.12,-8.99),(4.05,-8.25),(4.25,-6.3),(4.25,5.5),
           (4.07,7.35),(3.24,8.78),(2.63,9),(-2.63,9),(-3.24,8.78),(-4.07,7.35),
           (-4.25,5.5),(-4.25,-6.3),(-4.05,-8.25)]
    rings=[[(x*s,y*s,z) for x,y in skirt] for z,s in ((-.34,.963),(.03,1),(.79,1),(1.12,.955))]
    m.loft(rings,DARK)
    for s in (-1,1):
        for y in (-6.43,-2.81,2.03,5.42):
            m.beam((s*4.13,y,-.20),(s*4.236,y,.71),.055,GUN)
    m.object('Skirt',c,r)
    m=Mesh()
    m.prism([(x*.933,y*.97) for x,y in skirt],.82,1.36,HULL,DECK)
    for s in (-1,1):
        m.beam((s*3.93,-6.73,1.415),(s*3.95,6.41,1.415),.075,STEEL)
        m.box((s*2.53,5.77,1.61),(.57,3.54,.49),STEEL,bevel=.07)
    m.box((0,8.37,1.37),(3.83,1.05,.26),DECK,bevel=.11)
    for x in (-1.48,0,1.48):
        m.box((x,8.69,1.514),(.14,.33,.024),YELLOW,yaw=-.4)
    m.object('Hull',c,r)
    m=Mesh()
    bridge(m,(0,-.16,1.34),3.5,4.2,2.62)
    for s in (-1,1):
        m.box((s*2.24,-2.79,1.94),(.92,4.25,1.17),HULL)
        m.box((s*2.24,-2.63,2.56),(.68,1.30,.11),GUN)
    m.beam((0,-1.23,3.88),(0,-1.23,5.50),.073,STEEL)
    m.beam((-.51,-1.23,4.87),(.51,-1.23,4.87),.092,STEEL)
    m.object('Superstructure',c,r)
    for i,x in enumerate((-2.57,0,2.57),1):
        m=Mesh()
        m.tube((0,0,0),(0,1,0),[(-.64,1.12),(.64,1.17),(.64,.99),(-.64,.99)],
               STEEL,9,[HULL,STEEL,DARK,GUN],hollow=True)
        m.beam((-.63,0,-1.60),(-.35,0,-.89),.24,STEEL)
        m.beam((.63,0,-1.60),(.35,0,-.89),.24,STEEL)
        f=m.object(f'Fan_{i:02}',c,r,(x,-6.91,3.10))
        m=Mesh()
        for blade in range(4):
            angle=blade*math.tau/4+.22
            x,z=math.cos(angle),math.sin(angle)
            m.beam((x*.07,0,z*.07),(x*.88-z*.17,.05,z*.88+x*.17),.19,GUN,.061)
        m.tube((0,0,0),(0,1,0),[(-.12,.20),(.15,.153)],STEEL,6)
        m.object(f'FanBlade_{i:02}',c,f,(0,.12,0))
    for side,label in ((-1,'Port'),(1,'Starboard')):
        prefix='RocketLauncher_'+label
        g=node(prefix+'_Pivot',c,r,(side*3.11,2.38,1.36))
        m=Mesh()
        m.box((0,0,.31),(.91,.96,.59),STEEL)
        m.object(prefix,c,g)
        el=node(prefix+'_Elevation',c,g,(0,0,.66))
        m=Mesh()
        m.box((0,0,.16),(.95,1.23,.53),STEEL)
        for j in (-1,0,1):
            for z in (0,.29):
                m.tube((j*.25,.615,z),(0,1,0),[(0,.096),(.048,.083)],DARK,5)
        m.object(prefix+'_Tubes',c,el)
        node(prefix+'_Muzzle',c,el,(0,.70,.15),(0,1,0))
    naval_gun(a,r,(0,6.31,1.38),('GunTurretPivot','GunElevation','GunMuzzle'),1.12,True)
    node('SmokePoint',c,r,(0,-1.65,3.97),(0,0,1))
    node('FirePoint',c,r,(0,-5.05,1.95),(0,0,1))


def hydrofoil(a):
    c,r=a.collection,a.root
    p=node('HullPivot',c,r)
    m=Mesh()
    hull(m,[(-6.75,1.16,-.29,.28,.67),(-5.95,1.76,-.35,.29,.72),(-4.52,2.09,-.38,.35,.75),
            (-2.43,2.20,-.38,.37,.80),(.3,2.09,-.38,.41,.89),(2.48,1.65,-.31,.47,1.04),
            (4.39,1.16,-.16,.56,1.17),(5.97,.61,.05,.67,1.27),(6.75,.035,.31,.68,1.31)],DECK)
    for side in (-1,1):
        m.beam((side*1.99,-4.55,.73),(side*1.71,1.79,.93),.058,STEEL)
        m.box((side*1.41,-5.68,.93),(.64,.71,.29),GUN,bevel=.09)
    m.object('Hull',c,p)
    for label,y,span in (('Bow',3.05,2.58),('Stern',-4.17,3.0)):
        m=Mesh()
        for side in (-1,1):
            m.beam((side*1.06,y,.02),(side*1.21,y+.2,-1.81),.29,GUN,.54)
            wing(m,side,[(0,y-.80,y+.62,-2.06,.29),(1.40,y-.66,y+.64,-2.02,.27),
                        (span,y-.27,y+.53,-1.95,.16)],None,GUN)
            m.beam((side*1.21,y+.2,-1.78),(side*1.21,y+.22,-2.08),.24,GUN,.42)
        m.object('Foil_'+label,c,r)
        m=Mesh()
        for side in (-1,1):
            m.beam((side*1.06,y,.01),(side*1.06,y,-1.27),.17,STEEL,.26)
        m.object('SlidingStruts_'+label,c,p)
    m=Mesh()
    bridge(m,(0,.48,.80),2.78,3.22,1.89)
    m.box((0,-2.89,1.32),(1.91,2.47,1.10),HULL,bevel=.31)
    m.beam((0,.14,2.62),(0,-.05,3.91),.13,STEEL)
    for s in (-1,1):
        m.beam((0,.13,3.48),(s*.44,.13,3.48),.051,STEEL)
    m.object('Superstructure',c,p)
    radar(a,p,(0,-.05,3.89),.67)
    naval_gun(a,p,(0,3.90,1.12),('GunTurretPivot','GunElevation','GunMuzzle'),.92)
    angle=math.radians(15)
    direction=(0,math.cos(angle),math.sin(angle))
    for side,label in ((-1,'Port'),(1,'Starboard')):
        m=Mesh()
        m.tube((0,0,0),direction,[(-1.48,.36),(-1.31,.43),(1.31,.43),(1.44,.37)],HULL,10)
        for y in (-.75,.65):
            m.beam((0,y,-.69),(0,y,-.20),.32,GUN)
        part=m.object('MissileLauncher_'+label,c,p,(side*1.46,-2.34,1.41))
        node('MissileLaunchPoint_'+label,c,part,tuple(x*1.45 for x in direction),direction)
    node('SmokePoint',c,p,(0,-2.6,1.88),(0,0,1))
    node('FirePoint',c,p,(0,.6,2.72),(0,0,1))


def corvette(a):
    c,r=a.collection,a.root
    m=Mesh()
    hull(m,[(-7.50,1.19,-.29,.23,.72),(-6.52,1.52,-.36,.26,.78),(-4.82,1.78,-.40,.34,.85),
            (-2.24,1.90,-.40,.35,.94),(.2,1.88,-.40,.39,1.05),(2.96,1.61,-.25,.49,1.18),
            (5.10,1.18,-.12,.62,1.33),(6.72,.64,.11,.75,1.46),(7.5,.045,.55,.83,1.51)],DECK)
    for side in (-1,1):
        m.beam((side*1.45,-6.5,.77),(side*1.73,-3.04,.90),.056,STEEL)
        for y,x,z in ((-6.74,1.18,.82),(6.48,.47,1.45)):
            m.tube((side*x,y,z),(0,0,1),[(0,.082),(.16,.082)],GUN,6)
        m.box((side*1.346,5.0,1.08),(.06,.40,.15),GUN,bevel=.035)
    m.object('Hull',c,r)
    m=Mesh()
    bridge(m,(0,1.62,1.01),2.34,3.49,2.02)
    m.box((0,-2.79,1.38),(2.38,3.84,1.06),STEEL,bevel=.25)
    m.box((0,-3.11,2.135),(1.12,1.27,.50),HULL,bevel=.18)
    m.box((0,-3.13,2.402),(.88,.97,.055),GUN,bevel=.11)
    for side in (-1,1):
        for y in (-1.65,-2.07,-2.48):
            m.box((side*1.196,y,1.57),(.053,.28,.23),GUN)
    m.object('Superstructure',c,r)
    m=Mesh()
    for x in (-.43,.43):
        for y in (-.82,.20):
            m.beam((x,y,2.30),(x*.26,-.30,4.99),.114,STEEL)
            m.beam((x,y,2.42),(-x*.70,-.30,4.12),.059,STEEL)
    m.beam((-.84,-.26,4.03),(.84,-.26,4.03),.085,HULL)
    m.beam((0,-.28,4.55),(0,-.28,5.28),.085,GUN)
    m.tube((0,-.28,0),(0,0,1),[(4.89,.22),(5.02,.31),(5.35,.30),(5.50,.11)],HULL,10)
    m.object('Mast',c,r)
    radar(a,r,(0,-.65,4.59),.69)
    naval_gun(a,r,(0,5.52,1.34),('BowGun_TurretPivot','BowGun_ElevationPivot','BowGun_Muzzle'),1.03)
    for side,label in ((-1,'Port'),(1,'Starboard')):
        name='MissileLauncher_'+label
        m=Mesh()
        for i,z in enumerate((0,.64),1):
            m.tube((0,0,z),(0,1,0),[(-1.21,.17),(-1.08,.28),(1.17,.253)],HULL,8)
            m.tube((0,1.171,z),(0,1,0),[(0,.208),(.033,.208)],GUN,6)
        for y in (-.77,.50):
            m.beam((0,y,-.50),(0,y,.65),.22,STEEL)
        launcher=m.object(name,c,r,(side*1.49,.53,1.59))
        for i,z in enumerate((0,.64),1):
            node(f'MissileLaunchPoint_{label}_{i:02}',c,launcher,(0,1.225,z),(0,1,0))
        yaw=node(f'Ciws_{label}_Pivot',c,r,(side*1.12,-4.70,1.41))
        yaw.rotation_euler.z=-side*math.pi/2
        m=Mesh()
        m.tube((0,0,0),(0,0,1),[(0,.34),(.20,.34),(.36,.23),(.44,.17)],STEEL,10)
        m.tube((0,.06,.29),(0,1,0),[(0,.075),(.54,.050),(.64,.073)],GUN,8)
        m.object(f'Ciws_{label}_Housing',c,yaw)
        node(f'Ciws_{label}_Muzzle',c,yaw,(0,.714,.29),(0,1,0))
    node('SmokePoint_Bridge',c,r,(0,1.62,3.10),(0,0,1))
    node('FirePoint_Aft',c,r,(0,-4.85,1.36),(0,0,1))


def battery(a):
    c,r=a.collection,a.root
    m=Mesh()
    m.box((0,0,.14),(3,3,.28),CONCRETE,bevel=.17)
    m.tube((0,0,0),(0,0,1),[(.28,.82),(.55,.82),(.71,.69)],GUN,10)
    m.object('Base',c,r)
    yaw=naval_gun(a,r,(0,0,.70),('TurretPivot','ElevationPivot','Muzzle'),1.22,True)
    for side,label in ((-1,'Port'),(1,'Starboard')):
        m=Mesh()
        m.box((0,0,.38),(.70,1.24,.69),STEEL)
        m.box((0,.632,.38),(.62,.052,.60),GUN)
        for x in (-.145,.145):
            for z in (.24,.51):
                m.add([(x-.089,.661,z-.08),(x-.089,.661,z+.08),
                       (x+.089,.661,z+.08),(x+.089,.661,z-.08)],[(0,1,2,3)],METAL)
        m.box((side*.351,0,.38),(.037,.91,.15),'Emergency Red')
        p=m.object('MissileBox_'+label,c,yaw,(side*.85,-.10,.43))
        node('LaunchPoint_'+label,c,p,(0,.672,.38),(0,1,0))
    m=Mesh()
    m.beam((0,-.61,.2),(0,-.68,2.07),.13,STEEL)
    m.object('TrackerSupport',c,yaw)
    radar(a,yaw,(0,-.68,2.07),1.15)

