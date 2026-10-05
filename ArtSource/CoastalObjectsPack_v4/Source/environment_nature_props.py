"""Curved miniature island props, standalone builders, no Unity integration.

All authoring geometry is baked into closed meshes.  +Y is the face/bow
direction, ground props rest at Z=0 and the wreck uses a Z=0 waterline.
"""
import math
import random
from mathutils import Euler, Vector
import environment_detail_common as c


def _bounds(root):
    points=[]
    for ob in c.descendants(root):
        if ob.type!='MESH':continue
        # Builders use direct root parenting and baked mesh orientations.
        points.extend(Vector(ob.location)+vertex.co for vertex in ob.data.vertices)
    if not points:return [0.,0.,0.]
    return [round(max(p[i] for p in points)-min(p[i] for p in points),4) for i in range(3)]


def _tube(name,path,radii,parent,col,mat='ENV_Bark',sides=8,smooth=True):
    """Closed swept branch/pipe with a continuous changing centre line."""
    verts=[]
    for k,point in enumerate(path):
        point=Vector(point)
        tangent=Vector(path[min(k+1,len(path)-1)])-Vector(path[max(k-1,0)])
        if tangent.length<1e-8:tangent=Vector((0,0,1))
        direction=tangent.normalized()
        ref=Vector((0,0,1)) if abs(direction.z)<.9 else Vector((1,0,0))
        side=direction.cross(ref).normalized();up=direction.cross(side).normalized()
        for i in range(sides):
            angle=i*math.tau/sides
            verts.append(tuple(point+radii[k]*(math.cos(angle)*side+math.sin(angle)*up)))
    faces=[tuple(reversed(range(sides))),tuple(range((len(path)-1)*sides,len(path)*sides))]
    for k in range(len(path)-1):
        for i in range(sides):
            j=(i+1)%sides;a=k*sides;b=a+sides
            faces.append((a+i,a+j,b+j,b+i))
    return c.mesh(name,verts,faces,parent,col,mat,smooth=smooth)


def _surface_variation(ob,names,selector):
    for name in names:ob.data.materials.append(c.M[name])
    for face in ob.data.polygons:
        centre=sum((ob.data.vertices[i].co for i in face.vertices),Vector())/len(face.vertices)
        index=selector(centre,face.index)
        if index:face.material_index=min(index,len(ob.data.materials)-1)


def build_pine(root,col,seed=3):
    rng=random.Random(seed)
    trunk=_tube('PineBentTrunk',[(0,0,.025),(.035,-.025,.68),(.10,.025,1.45),
          (-.025,.10,2.20),(-.04,.12,2.90),(.13,.10,3.62),(.18,.10,4.26)],
          [.16,.142,.115,.088,.070,.040,.012],root,col)
    for vertex in list(trunk.data.vertices)[:8]:vertex.co.z=0
    trunk.data.update()
    branches=[((.09,.01,1.48),(-.27,.04,1.77),(-.93,.10,2.13)),
              ((.02,.08,2.08),(.35,-.21,2.30),(.95,-.32,2.40)),
              ((-.035,.12,2.60),(-.39,.34,2.77),(-.77,.43,2.99)),
              ((-.04,.12,2.84),(.39,.40,3.08),(.86,.41,3.16)),
              ((.03,.11,3.19),(-.22,-.15,3.43),(-.58,-.33,3.55)),
              ((.12,.10,3.54),(.39,.15,3.77),(.62,.20,3.87))]
    for i,path in enumerate(branches):
        _tube('PineExposedBranch_'+str(i),path,[.047,.031,.011],root,col,sides=7)
    for i,path in enumerate([((-.45,.056,1.868),(-.55,.40,2.09),(-.60,.61,2.24)),
                            ((.415,.4005,3.084),(.62,.59,3.20),(.74,.65,3.29))]):
        _tube('PineNeedleSupportingTwig_'+str(i),path,[.028,.018,.007],root,col,sides=6)
    # Broad asymmetrical horizontal needle sprays: no conical silhouettes.
    crowns=[(-.61,.12,2.12,1.56,1.17,.63),(.65,-.23,2.44,1.64,1.21,.64),
            (-.48,.34,2.98,1.64,1.31,.63),(.62,.32,3.18,1.67,1.31,.63),
            (-.43,-.21,3.53,1.52,1.23,.63),(.40,.18,3.85,1.46,1.24,.59),
            (.10,.10,4.20,1.47,1.22,.59),(.18,.08,4.40,.96,.85,.44)]
    for i,(x,y,z,w,l,h) in enumerate(crowns):
        ob=c.sphere('PineNeedleSpray_'+str(i),(x,y,z),(w,l,h),root,col,
                    'MAT_IslandTree' if i%3 else 'ENV_PineHighlight',16,8,
                    deform=.26,seed=seed*7+i)
        phase=.73*i+seed*.19
        # A closed lobed shell, not separate little balls: two or three broad
        # needle fans break the outline while all upper vertices (including
        # the shared pole) follow one continuous flattened crown mapping.
        for vertex in ob.data.vertices:
            vx,vy,vz=vertex.co
            nx,ny,nz=vx/(w/2),vy/(l/2),vz/(h/2)
            radial=math.sqrt(nx*nx+ny*ny)
            angle=math.atan2(ny,nx)
            lobes=1+.19*math.sin(3*angle+phase)*radial**1.4+.075*math.sin(5*angle-.6*phase)*radial**2
            positive=max(0,nz)
            top_z=h*.44*(.34*nz+.66*math.sin(nz*math.pi/2)) if nz>=0 else vz*.81
            vertex.co=(vx*lobes+.055*w*positive**2,
                       vy*lobes+.042*l*math.sin(phase)*positive**2,
                       top_z+.035*h*math.sin(2*angle+phase)*radial)
        rotation=Euler((rng.uniform(-.07,.07),rng.uniform(-.08,.08),rng.uniform(-.28,.28)))
        ob.data.transform(rotation.to_matrix().to_4x4());ob.data.update()
    for i,angle in enumerate((.35,2.40,4.20)):
        _tube('PineExposedRoot_'+str(i),[(0,0,.08),
              (.15*math.cos(angle),.15*math.sin(angle),.06),
              (.31*math.cos(angle),.31*math.sin(angle),.018)],
              [.06,.045,.014],root,col,sides=6)
    c.empty('ContactOrigin',root,col,(0,0,0))
    return {'features':['bent continuous bark trunk','exposed supporting boughs',
                        'connected irregular lobed pine needle sprays','smooth asymmetric flattened crown mapping',
                        'whole-spray needle colour variation without triangular cap patches'],
            'dimensions_m':_bounds(root)}


def build_rock_cluster(root,col,seed=6):
    from environment_rock_geometry import angular_rock
    rocks=[(-.58,.12,(2.30,2.04,1.79)),
           (.82,-.17,(1.70,1.62,1.94)),
           (-.26,-1.08,(1.26,.96,.86)),
           (1.04,.91,(1.09,.91,.73))]
    for i,(x,y,dims) in enumerate(rocks):
        angular_rock('FracturedCoastalBoulder_'+str(i),(x,y,0),dims,
                     root,col,seed=seed+i*13)
    c.empty('ContactOrigin',root,col,(0,0,0))
    return {'features':['four asymmetrical fractured coastal boulders',
                        'broad tilted planar slabs and unequal corner cuts',
                        'flat stable ground contacts','face-coherent rock and wet-fracture colours',
                        'flat normals preserve geological edges without melted smooth shading'],
            'dimensions_m':_bounds(root)}


def build_seal(root,col):
    c.sphere('SealBody',(0,-.27,.265),(.67,1.30,.53),root,col,'MAT_IslandSeal',16,8)
    c.sphere('SealCurvedNeck',(0,.36,.46),(.43,.60,.51),root,col,
             'MAT_IslandSeal',12,6,rot=(-.24,0,0))
    c.sphere('SealRoundedHead',(0,.71,.68),(.47,.43,.43),root,col,
             'MAT_IslandSeal',14,8,rot=(-.08,0,0))
    c.sphere('SealMuzzle',(0,.90,.63),(.29,.24,.19),root,col,'ENV_SealMuzzle',10,6)
    c.sphere('SealNose',(0,1.006,.675),(.075,.033,.048),root,col,'Gunmetal',6,4)
    for side,label in ((-1,'P'),(1,'S')):
        c.sphere('SealFrontFlipper_'+label,(side*.36,-.015,.045),(.46,.58,.090),root,col,
                 'MAT_IslandSeal',10,5,rot=(0,0,side*.40))
        c.sphere('SealRearFlipper_'+label,(side*.10,-.88,.05),(.23,.44,.10),root,col,
                 'MAT_IslandSeal',10,5,rot=(0,0,side*.26))
        c.sphere('SealEye_'+label,(side*.201,.805,.752),(.044,.039,.042),root,col,'Gunmetal',6,4)
        c.sphere('SealNostril_'+label,(side*.018,1.023,.678),(.013,.010,.011),root,col,'Gunmetal',6,3)
        for i in range(3):
            c.beam('SealWhisker_'+label+str(i),(side*.116,.953,.626+i*.021),
                   (side*(.222+i*.015),1.002-.014*i,.601+i*.029),.005,root,col,'ENV_SealMuzzle')
    c.empty('ContactOrigin',root,col,(0,0,0))
    return {'features':['rounded resting seal','raised curved neck and forward-facing muzzle',
                        'paired front and rear flippers','small eyes, nose and whiskers'],
            'dimensions_m':_bounds(root)}


def _closed_strip(name,sections,side,parent,col,mat):
    # Each section is (Y, half-width, bottom-Z, top-Z).  The top boundary is
    # torn but the plate itself has front/back thickness and sealed end caps.
    verts=[]
    for y,w,low,high in sections:
        outer=side*w;inner=outer-side*.055
        bottom_outer=side*w*.73;bottom_inner=bottom_outer-side*.055
        verts.extend([(inner,y,high),(outer,y,high),
                      (bottom_outer,y,low),(bottom_inner,y,low)])
    faces=[(3,2,1,0),tuple(range((len(sections)-1)*4,len(sections)*4))]
    for k in range(len(sections)-1):
        for j in range(4):
            a=k*4;b=a+4;n=(j+1)%4
            faces.append((a+j,a+n,b+n,b+j))
    return c.mesh(name,verts,faces,parent,col,mat,smooth=True)


def _closed_deck(name,sections,top,parent,col,mat='MAT_IslandRust'):
    verts=[]
    for y,width in sections:
        verts.extend([(-width,y,top),(width,y,top),(width,y,top-.065),(-width,y,top-.065)])
    faces=[(3,2,1,0),tuple(range((len(sections)-1)*4,len(sections)*4))]
    for k in range(len(sections)-1):
        a=k*4;b=a+4
        for j in range(4):n=(j+1)%4;faces.append((a+j,a+n,b+n,b+j))
    return c.mesh(name,verts,faces,parent,col,mat)


def _torn_bulkhead(name,y,polygon,parent,col,mat='ENV_RustLight'):
    verts=[(x,yy,z) for yy in (y-.035,y+.035) for x,z in polygon]
    count=len(polygon)
    faces=[tuple(reversed(range(count))),tuple(range(count,2*count))]
    for i in range(count):j=(i+1)%count;faces.append((i,j,count+j,count+i))
    return c.mesh(name,verts,faces,parent,col,mat)


def _wreck_surface(side,y,sections):
    """Interpolate the exact strip deformation for welded-on fittings."""
    for index,(first,last) in enumerate(zip(sections,sections[1:])):
        if first[0]<=y<=last[0]:
            y0,w0,z0,t0=first;y1,w1,z1,t1=last
            dent=(-.08 if index in (2,4) else .025)*side
            middle=((y0+y1)/2,(w0+w1)/2+dent,(z0+z1)/2,
                    (t0+t1)/2-(.23 if index in (2,3,4) else .015))
            a,b=(first,middle) if y<=middle[0] else (middle,last)
            blend=(y-a[0])/(b[0]-a[0])
            return tuple(a[k]+(b[k]-a[k])*blend for k in (1,2,3))
    raise ValueError('fitting outside wreck side strip')


def build_wreck(root,col):
    sections=[(-4.,.60,-.52,.28),(-3.40,.85,-.68,.40),(-2.30,1.0,-.76,.47),
              (-1.0,1.025,-.81,.34),(.30,1.0,-.80,.44),(1.60,.92,-.75,.43),
              (2.80,.65,-.62,.31),(3.55,.32,-.39,.20),(4.,.028,-.11,.07)]
    # Continuous curved lower keel shell; hollow cargo space remains above it.
    verts=[]
    for y,w,low,high in sections:
        top=max(-.36,low+.065)
        verts.extend([(-w*.90,y,top),(w*.90,y,top),
                      (w*.74,y,low),(-w*.74,y,low)])
    faces=[(3,2,1,0),(32,33,34,35)]
    for k in range(len(sections)-1):
        a=k*4;b=a+4
        for j in range(4):n=(j+1)%4;faces.append((a+j,a+n,b+n,b+j))
    c.mesh('WreckCurvedKeel',verts,faces,root,col,'MAT_IslandRust',smooth=True)
    for side,label in ((-1,'P'),(1,'S')):
        for i,(first,last) in enumerate(zip(sections,sections[1:])):
            y0,w0,z0,t0=first;y1,w1,z1,t1=last
            dent=(-.08 if i in (2,4) else .025)*side
            middle=((y0+y1)/2,(w0+w1)/2+dent,(z0+z1)/2,
                    (t0+t1)/2-(.23 if i in (2,3,4) else .015))
            _closed_strip('TornHullPlate_'+label+str(i),[first,middle,last],side,root,col,
                          'ENV_RustLight' if i in (0,3,6) else 'MAT_IslandRust')
    _closed_deck('WreckForedeck',[(.84,.92),(1.60,.84),(2.80,.59),(3.55,.285),(3.93,.036)],.16,root,col)
    _closed_deck('WreckAftDeck',[(-3.93,.54),(-3.40,.79),(-2.93,.86)],.02,root,col)
    _torn_bulkhead('ForwardTornBulkhead',.83,[(-.80,-.37),(.80,-.37),(.80,.34),
        (.47,.31),(.34,.03),(.08,.11),(-.13,.40),(-.41,.25),(-.80,.28)],root,col)
    _torn_bulkhead('AftTornBulkhead',-2.91,[(-.82,-.37),(.82,-.37),(.82,.26),
        (.42,.10),(.30,.20),(-.20,-.08),(-.45,.32),(-.82,.25)],root,col)
    for i,y in enumerate((-2.45,-1.72,-.85,.04)):
        c.beam('CargoHoldFloorRib_'+str(i),(-.76,y,-.3325),(.76,y,-.3325),.055,root,col,'ENV_RustLight')
        for side in (-1,1):
            c.beam('CargoHoldFrameLeg_'+str(i), (side*.75,y,-.32),(side*.93,y,.20),
                   .055,root,col,'ENV_RustLight')
    # A compressed, leaning bridge with baked rotation, not a scaled cube.
    bridge_rot=(.075,.12,-.065)
    c.frustum('WreckCrushedBridge',(0,1.65,.68),(1.27,1.44),(1.01,1.15),1.09,
              root,col,'ENV_RustLight',bridge_rot)
    bridge_matrix=Euler(bridge_rot).to_matrix();bridge_origin=Vector((0,1.65,.68))
    roof_loc=bridge_origin+bridge_matrix@Vector((0,0,.565))
    c.box('WreckBridgeRoof',roof_loc,(1.06,1.20,.075),root,col,
          'MAT_IslandRust',bridge_rot)
    front_z=.21
    fraction=(front_z+.545)/1.09
    front_y=.72-.145*fraction
    front_rotation=(bridge_matrix@Euler((math.atan(.145/1.09),0,0)).to_matrix()).to_euler()
    for x in (-.31,0,.31):
        location=bridge_origin+bridge_matrix@Vector((x,front_y+.008,front_z))
        c.box('WreckBridgeWindow',location,(.25,.032,.20),root,col,
              'Radar Glass',rot=front_rotation)
    for side in (-1,1):
        side_z=.25;fraction=(side_z+.545)/1.09
        location=bridge_origin+bridge_matrix@Vector((side*(.635-.13*fraction+.006),.07,side_z))
        rotation=(bridge_matrix@Euler((0,-side*math.atan(.13/1.09),0)).to_matrix()).to_euler()
        c.box('WreckBridgeSideWindow',location,(.032,.51,.19),root,col,
              'Radar Glass',rot=rotation)
    _tube('WreckBentSupportedMast',[(0,1.62,1.18),(.025,1.61,1.53),
          (.12,1.52,1.93),(.18,1.41,2.25),(.39,1.25,2.43)],
          [.060,.057,.044,.032,.017],root,col,'MAT_IslandRust',10)
    c.beam('WreckMastCrossarm',(-.32,1.51,1.93),(.48,1.51,1.93),.037,root,col,'Gunmetal')
    c.beam('WreckMastBrace',(-.35,1.65,1.22),(.12,1.52,1.93),.024,root,col,'ENV_RustLight')
    # Circular portholes with opaque inner caps on the forward surviving shell.
    for side in (-1,1):
        for y in (2.10,2.43):
            width,low,high=_wreck_surface(side,y,sections)
            z=.13
            x=side*width*(.73+.27*(z-low)/(high-low))
            c.ring('WreckPortholeRim',(x+side*.003,y,z),.090,.015,root,col,
                   'ENV_RustLight',14,5,rot=(0,math.pi/2,0))
            c.cyl('WreckPortholeDark',(x-side*.012,y,z),.073,.036,root,col,
                  'Gunmetal',14,rot=(0,math.pi/2,0))
    # Remaining rail spans terminate at supported posts, with visibly bent tips.
    rail_sets=[(-1,[(-3.78,.31),(-3.30,.41),(-2.96,.38)]),
               (1,[(-3.65,.33),(-3.30,.41)]),
               (-1,[(2.73,.31),(3.02,.26),(3.34,.225)])]
    for k,(side,posts) in enumerate(rail_sets):
        rail_tops=[]
        for i,(y,unused_z) in enumerate(posts):
            width,low,high=_wreck_surface(side,y,sections)
            x=side*(width-.023)
            top=(x+side*(.05 if i==len(posts)-1 else 0),y,high+.26)
            c.beam('WreckRailPost_'+str(k)+'_'+str(i),(x,y,high-.028),top,.023,root,col,'ENV_RustLight')
            rail_tops.append(top)
        for i,(p,q) in enumerate(zip(rail_tops,rail_tops[1:])):
            c.beam('WreckBrokenRailSpan_'+str(k)+'_'+str(i),p,q,.023,root,col,'ENV_RustLight')
    # Bow anchor, connected chain and deck capstan make the surviving fittings readable.
    c.cyl('WreckCapstanBase',(0,3.12,.205),.14,.09,root,col,'MAT_IslandRust',12)
    c.cyl('WreckCapstanDrum',(0,3.12,.31),.085,.18,root,col,'Gunmetal',12)
    c.cyl('WreckCapstanTop',(0,3.12,.41),.14,.045,root,col,'ENV_RustLight',12)
    for i in range(3):
        c.ring('WreckAnchorChain_'+str(i),(.62,2.82,.18-i*.105),.070,.017,
               root,col,'Gunmetal',12,5,rot=(math.pi/2,0,(i%2)*math.pi/2))
    c.beam('WreckAnchorShank',(.62,2.82,-.13),(.62,2.82,-.43),.05,root,col,'MAT_IslandRust')
    c.beam('WreckAnchorStock',(.43,2.82,-.20),(.81,2.82,-.20),.046,root,col,'MAT_IslandRust')
    for side in (-1,1):
        c.beam('WreckAnchorArm',(.62,2.82,-.41),(.62+side*.17,2.82,-.35),.039,root,col,'MAT_IslandRust')
        c.frustum('WreckAnchorFluke',(.62+side*.17,2.82,-.32),(.15,.08),(.04,.08),.16,
                  root,col,'ENV_RustLight',rot=(0,-side*.42,0))
    # Stern propeller remains attached to a keel-supported shaft, partly damaged.
    c.cyl('WreckPropellerShaft',(0,-3.88,-.46),.043,.50,root,col,'Gunmetal',10,rot=(math.pi/2,0,0))
    c.cyl('WreckPropellerHub',(0,-4.035,-.46),.10,.18,root,col,'MAT_IslandRust',10,rot=(math.pi/2,0,0))
    for i in range(3):
        angle=i*math.tau/3
        c.sphere('WreckBentPropellerBlade_'+str(i),(.16*math.sin(angle),-4.025,-.46+.16*math.cos(angle)),
                 (.18,.065,.40 if i!=1 else .24),root,col,'ENV_RustLight',10,5,
                 rot=(.15,angle,.15))
    for side in (-1,1):
        c.cyl('WreckAftBollard',(side*.38,-3.56,.115),.045,.18,root,col,'MAT_IslandRust',10)
    c.empty('WreckAnchor',root,col,(.62,2.82,-.18))
    c.empty('ContactOrigin',root,col,(0,0,0))
    return {'features':['curved rusted hull made from sealed torn plating','open cargo hold and exposed ribs',
                        'crushed leaning bridge with supported bent mast','broken but supported railing spans',
                        'connected bow anchor chain and surviving stern propeller'],
            'dimensions_m':_bounds(root)}
