"""Oil facilities and resupply silhouettes for the Caspian level."""
import math

from geometry import Mesh, node, HULL, STEEL, DARK, GUN, METAL, GLASS, YELLOW, WHITE, CONCRETE


def platform(a, large):
    c,r=a.collection,a.root
    width,height = (22,9) if large else (12,6)
    bottom=height-.72
    rleg=1.1 if large else .7
    step=6.6 if large else 3.57
    for i,(x,y) in enumerate(((-step,-step),(step,-step),(step,step),(-step,step)),1):
        m=Mesh()
        m.tube((0,0,0),(0,0,1),[(-3.5,rleg),(bottom,rleg)],CONCRETE,8 if large else 7)
        m.object(f'Leg_{i:02}',c,r,(x,y,0))

    m=Mesh()
    for x in (-step,step):
        for y in (-step,step):
            if large:
                m.box((x,y,bottom),(.58*step,.58*step,.49),METAL)
    for p,q in (((-step,-step),(step,-step)),((step,-step),(step,step)),
                ((step,step),(-step,step)),((-step,step),(-step,-step))):
        m.beam((*p,.65),(*q,bottom-.4),.30 if large else .21,METAL)
        if large:
            m.beam((*q,.65),(*p,bottom-.4),.30,METAL)
    m.object('Substructure',c,r)
    m=Mesh()
    # 2026-11-20 Claude: 금속 상자 윗면을 콘크리트 윗면보다 3cm 낮춤 — 같은 높이라 게임에서 갑판이 깜박였다(z-fighting)
    m.box((0,0,height-.43),(width,width,.80),METAL,bevel=.80)
    m.box((0,0,height-.045),(width-.21,width-.21,.09),CONCRETE,bevel=.73)
    # Thick guide stripes and wheel stops still read in the game's distant view.
    for s in (-1,1):
        rr=width/2-.25
        m.beam((s*rr,-rr+.68,height+.035),(s*rr,rr-.68,height+.035),.12,YELLOW,.055)
        rail_z=height+.65
        m.beam((s*rr,-rr+.65,rail_z),(s*rr,rr-.65,rail_z),.075,YELLOW)
        for q in (-rr+.65,0,rr-.65):
            m.beam((s*rr,q,height),(s*rr,q,rail_z),.094,METAL)
    m.object('Deck',c,r)

    mount_positions=[(width*.30,width*.30)]
    if large:
        mount_positions.append((-width*.30,width*.30))
    for i,(x,y) in enumerate(mount_positions,1):
        m=Mesh()
        m.box((0,0,-.125),(3,3,.25),CONCRETE,bevel=.125)
        for side in (-1,1):
            m.box((0,side*1.33,.012),(2.55,.13,.024),YELLOW)
        m.object(f'BatteryMount_{i:02}',c,r,(x,y,height+.29))
    m=Mesh()
    if large:
        x,y=-2.85,-2.50
        tower=[(0,2.34),(4.40,1.69),(8.85,1.12),(13.75,.42)]
        for (z0,s0),(z1,s1) in zip(tower,tower[1:]):
            for sx,sy in ((-1,-1),(1,-1),(1,1),(-1,1)):
                a0=(x+sx*s0,y+sy*s0,height+z0)
                b0=(x+sx*s1,y+sy*s1,height+z1)
                m.beam(a0,b0,.21,YELLOW)
                m.beam(a0,(x-sx*s1,y+sy*s1,height+z1),.14,METAL)
                if sx==sy:
                    m.beam(b0,(x+sx*s1,y-sy*s1,height+z1),.15,YELLOW)
        m.box((x,y,height+13.84),(1.35,1.35,.30),YELLOW,bevel=.12)
        m.box((x,y,height+1.77),(.58,1.02,3.50),METAL)
        m.object('Derrick',c,r)
        m=Mesh()
        # The helideck and crew quarters share one side, leaving the drill floor visible.
        m.box((5.39,-4.86,height+.94),(6.22,6.16,1.88),STEEL,bevel=.36)
        for x in (3.5,4.78,6.06,7.34):
            m.box((x,-7.97,height+1.24),(.82,.08,.47),GLASS)
        m.box((5.39,-4.86,height+2.06),(8.50,8,.34),METAL,bevel=1.08)
        hx,hy=5.39,-4.86
        for dx in (-.83,.83):
            m.box((hx+dx,hy,height+2.246),(.19,2.42,.024),WHITE)
        m.box((hx,hy,height+2.246),(1.66,.19,.024),WHITE)
        for q in range(8):
            angle=q*math.pi/4
            m.box((hx+3.63*math.cos(angle),hy+3.41*math.sin(angle),height+2.248),
                  (.13,.42,.029),YELLOW)
        m.object('AccommodationsAndHelideck',c,r)
    else:
        for y in (-2.5,-.73):
            m.tube((-2,y,height+.72),(1,0,0),[(-1.23,.31),(-1,.56),(1,.56),(1.23,.31)],STEEL,8)
            m.box((-2,y,height+.21),(2.16,1,.42),METAL)
        m.box((-.46,-3.99,height+.67),(2.32,1.24,1.34),METAL)
        m.object('ProcessingEquipment',c,r)

    x,y=(8.43,1.09) if large else (3.67,-1.1)
    m=Mesh()
    m.beam((0,0,.2),(0,0,1.92),.46 if large else .33,METAL)
    m.beam((0,0,1.92),(1.23,0,2.87),.26,YELLOW)
    m.beam((1.23,0,2.87),(4.56 if large else 3.61,0,3.68),.20,YELLOW)
    end=4.56 if large else 3.61
    m.tube((end,0,0),(0,0,1),[(3.66,.21),(3.95,.21)],METAL,8)
    flare=m.object('FlareBoom',c,r,(x,y,height))
    node('FlareTip',c,flare,(end,0,3.95),(0,0,1))
    x,y=(-7.8,3.72) if large else (-3.93,2.36)
    m=Mesh()
    m.tube((0,0,0),(0,0,1),[(0,.59),(.66,.51)],METAL,8)
    m.object('CraneBase',c,r,(x,y,height))
    m=Mesh()
    m.box((0,0,.4),(1.1,1,.81),YELLOW,bevel=.10)
    reach=6.0 if large else 3.1
    m.beam((0,0,.61),(0,-reach,2.65),.25,YELLOW)
    m.beam((0,-reach,2.65),(0,-reach,.29),.043,METAL)
    jib=m.object('CraneJib',c,r,(x,y,height+.61))
    jib.rotation_euler.z=-.14
    for i,(x,y,z) in enumerate(((0,0,.5),(width*.25,-width*.25,1.2),(-width*.30,width*.22,.37)),1):
        node(f'FirePoint_{i:02}',c,r,(x,y,height+z),(0,0,1))
    node('OilSlickPoint',c,r,(0,0,0),(0,0,1))


def barge(a):
    c,r=a.collection,a.root
    m=Mesh()
    outline=[(-2.31,-8),(2.31,-8),(3,-7.18),(3,7),(2.31,8),(-2.31,8),(-3,7),(-3,-7.18)]
    m.loft([[(x*s,y*s,z) for x,y in outline] for s,z in ((.92,-.38),(1,.26),(1,1.04))],HULL)
    m.object('Hull',c,r)
    m=Mesh()
    m.prism([(x*.987,y*.996) for x,y in outline],.98,1.20,'Deck Grey')
    for side in (-1,1):
        m.beam((side*2.92,-6.68,1.23),(side*2.92,6.60,1.23),.06,YELLOW)
        for y in (-6.65,0,6.55):
            m.tube((side*2.67,y,1.24),(0,0,1),[(0,.122),(.175,.122)],GUN,8)
    m.box((0,-5.98,1.72),(3.2,1.95,1.04),STEEL)
    m.box((0,-4.988,1.90),(2.72,.063,.36),GLASS)
    m.object('Deck',c,r)
    yaw=node('CranePivot',c,r,(-1.55,5.88,1.21))
    m=Mesh()
    m.box((0,0,.41),(.93,1.33,.81),YELLOW,bevel=.15)
    m.beam((0,0,.54),(0,-3.88,2.94),.23,YELLOW)
    m.beam((0,-3.88,2.94),(0,-3.88,1.31),.06,GUN)
    m.object('CraneJib',c,yaw)
    m=Mesh()
    for y in (-.94,.94):
        m.box((0,y,.17),(4.25,.30,.31),GUN)
    for x in (-1.66,-1.0,-.33,.33,1.,1.66):
        m.tube((x,0,.48),(0,1,0),[(-1.30,.15),(.86,.18),(1.30,.046)],HULL,7)
    m.object('MissileRack',c,r,(0,-.63,1.21))
    node('MooringPoint',c,r,(3.02,0,.48),(1,0,0))
    node('FirePoint',c,r,(0,-.64,1.73),(0,0,1))


def moskit(a):
    c,r=a.collection,a.root
    m=Mesh()
    m.tube((0,0,0),(0,1,0),[(-1.30,.108),(-1.09,.18),(.70,.18),(.78,.18),(1.30,.013)],HULL,8,
           [DARK,HULL,'Emergency Red',DARK])
    for x,z in ((1,0),(-1,0),(0,1),(0,-1)):
        m.add([(x*.184-z*.06,.52,z*.184+x*.06),(x*.184+z*.06,.52,z*.184-x*.06),
               (x*.233+z*.061,.77,z*.233-x*.061),(x*.233-z*.061,.77,z*.233+x*.061),
               (x*.132,.70,z*.132)],[(0,1,2,3),(0,4,1),(1,4,2),(2,4,3),(3,4,0)],GUN)
        axis=(z,-0.,-x)
        w=.014
        for y,reach in ((.03,.45),(-.93,.39)):
            verts=[(x*u+axis[0]*d,yy,z*u+axis[2]*d)
                   for d in (-w,w) for u,yy in ((.13,y-.26),(reach,y-.19),(reach,y+.025),(.13,y+.35))]
            m.loft([verts[:4],verts[4:]],HULL)
    m.object('Body',c,r)
    node('Exhaust',c,r,(0,-1.30,0),(0,-1,0))


def sandbar(a):
    c,r=a.collection,a.root
    m=Mesh()
    m.tube((0,0,0),(0,0,1),[(-.42,.56),(.11,.68),(.47,.29)],YELLOW,8)
    m.tube((0,0,0),(0,0,1),[(.47,.085),(.92,.085),(1.29,.085),(1.58,.085),(2.38,.085)],WHITE,6,
           [WHITE,YELLOW,WHITE,YELLOW])
    m.box((0,0,2.38),(.27,.27,.24),YELLOW,yaw=.785)
    m.object('Marker',c,r)

