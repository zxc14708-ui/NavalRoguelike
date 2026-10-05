"""Continuous irregular coastline meshes, rooted at the current IslandBuilder zero."""
import math
import random
from functools import lru_cache
from mathutils import Vector, noise
import environment_detail_common as c


@lru_cache(maxsize=8)
def _rock_rim(seed,radius):
    """Wide straight fracture faces, with three narrow V-shaped joints."""
    rng=random.Random(seed)
    points=[]
    for i in range(18):
        angle=i*math.tau/18+rng.uniform(-.045,.045)
        r=radius*(1+.11*math.sin(3*angle+seed*.13)+rng.uniform(-.06,.06))
        if i in (3,10,15):
            for offset,factor in ((-.038,1),(.0,.84),(.038,1)):
                a=angle+offset;points.append((r*factor*math.cos(a),r*factor*math.sin(a)))
        else:
            points.append((r*math.cos(angle),r*math.sin(angle)))
    return tuple(points)


@lru_cache(maxsize=8)
def _rock_shell(seed,radius,peak):
    """Closed stepped cliff, not a smooth radial height-field hill."""
    rim=_rock_rim(seed,radius);n=len(rim);verts=[];faces=[];bands=[]
    profiles=[(-3.,.99),(-1.2,1.03),(.12,1.045),(.52,1.015),
              (.73,.965),(2.40,.965),(2.57,.90),(4.90,.885),
              (5.16,.82),(6.12,.35)]
    for k,(z,factor) in enumerate(profiles):
        for i,(x,y) in enumerate(rim):
            # Corresponding vertices keep fracture seams continuous through layers.
            a=math.atan2(y,x)
            offset=0 if k<2 else (.10+.025*k)*math.sin(2*a+.7)
            if k>=7:offset+=.075*x-.045*y
            verts.append((x*factor,y*factor,z+offset))
    faces.append(tuple(reversed(range(n))));bands.append(0)
    for k in range(len(profiles)-1):
        for i in range(n):
            j=(i+1)%n;faces.append((k*n+i,k*n+j,(k+1)*n+j,(k+1)*n+i));bands.append(k)
    top=(len(profiles)-1)*n
    verts.append((0,0,6.12));center=len(verts)-1
    for i in range(n):faces.append((top+i,top+(i+1)%n,center));bands.append(9)
    return tuple(verts),tuple(faces),tuple(bands)


def _rock_surface_height(x,y,p):
    """Highest triangulated shell surface, including the real ledge setbacks."""
    verts,faces,_=_rock_shell(p['seed'],p['radius'],p['height']);best=-3.
    for face in faces[1:]:
        for j in range(1,len(face)-1):
            a,b,d=[verts[index] for index in (face[0],face[j],face[j+1])]
            den=(b[1]-d[1])*(a[0]-d[0])+(d[0]-b[0])*(a[1]-d[1])
            if abs(den)<1e-10:continue
            u=((b[1]-d[1])*(x-d[0])+(d[0]-b[0])*(y-d[1]))/den
            v=((d[1]-a[1])*(x-d[0])+(a[0]-d[0])*(y-d[1]))/den
            w=1-u-v
            if min(u,v,w)>=-1e-7:best=max(best,u*a[2]+v*b[2]+w*d[2])
    return best

PRESETS={
 'ENV_RockIslet':{'radius':7.6,'height':7.6,'seed':43,'rocky':True},
 'ENV_PineIsland':{'radius':12.,'height':4.8,'seed':57,'rocky':False},
 'ENV_LighthouseIsland':{'radius':10.4,'height':3.7,'seed':81,'rocky':False},
 'ENV_WreckCove':{'radius':13.,'height':4.2,'seed':91,'rocky':False},
 'ENV_RadarOutpost':{'radius':14.,'height':3.8,'seed':113,'rocky':False,'kind':'radar'},
 'ENV_CoastalOutpost':{'radius':13.,'height':3.0,'seed':127,'rocky':False,'kind':'outpost'},
 'ENV_HarborIsland':{'radius':15.5,'height':2.6,'seed':139,'rocky':False,'kind':'harbor'},
 'ENV_Atoll':{'radius':15.,'height':2.1,'seed':151,'rocky':False,'kind':'atoll'}}

def outline(angle,p):
    if p['rocky']:
        direction=(math.cos(angle),math.sin(angle));rim=_rock_rim(p['seed'],p['radius'])
        for a,b in zip(rim,rim[1:]+rim[:1]):
            edge=(b[0]-a[0],b[1]-a[1])
            den=direction[0]*edge[1]-direction[1]*edge[0]
            if abs(den)<1e-9:continue
            distance=(a[0]*edge[1]-a[1]*edge[0])/den
            fraction=(a[0]*direction[1]-a[1]*direction[0])/den
            if distance>0 and -1e-7<=fraction<=1+1e-7:return distance
        raise ValueError('rock outline ray misses closed rim')
    phase=p['seed']*.13
    r=p['radius']*(1+.12*math.sin(3*angle+phase)+.07*math.cos(5*angle-phase)
                         +.035*math.sin(9*angle+2*phase))
    if p['seed']==91 or p.get('kind')=='harbor':
        # A visible shallow cove on the near edge, not a circular lathed mound.
        d=math.atan2(math.sin(angle+1.3),math.cos(angle+1.3))
        r*=1-(.32 if p.get('kind')=='harbor' else .24)*math.exp(-(d/.38)**2)
    return r

def height(x,y,p):
    if p['rocky']:return _rock_surface_height(x,y,p)
    a=math.atan2(y,x);r=outline(a,p);t=min(1.5,math.hypot(x,y)/r)
    ripple=noise.noise_vector(Vector((x*.31+11,y*.31+p['seed'],p['seed']*.23)))[0]
    broad=noise.noise_vector(Vector((x*.095,y*.095,p['seed'])))[0]
    if p.get('kind')=='atoll':
        inner=max(0,min(1,(t-.26)/.18))
        outer=min(1,max(0,(1-t)/.19))
        ridge=p['height']*math.exp(-((t-.62)/.18)**2)
        z=c.SEA-1.0+inner*(1.08+outer*.65+ridge+.07*ripple)
    else:
        # Broad sand skirt grading into a pine-covered, asymmetrical low hill.
        z=c.SEA+.12+min(1,max(0,(1-t)/.24))*.82
        ridge=math.exp(-(((x+p['radius']*.13)/(p['radius']*.48))**2+
                        ((y-p['radius']*.10)/(p['radius']*.62))**2))
        blend=min(1,max(0,(.83-t)/.22))
        z+=blend*(p['height']*ridge+.20*broad+.11*ripple)
        if p['seed']==81:
            # Flatten a coherent lighthouse terrace, preserving the surrounding slope.
            weight=max(0,min(1,(3.3-math.hypot(x-1.2,y-.4))/1.3))
            weight=weight*weight*(3-2*weight)
            z=z*(1-weight)+3.30*weight
            dx,dy=x+2.5,y+3.6
            dx,dy=dx*math.cos(.22)-dy*math.sin(.22),dx*math.sin(.22)+dy*math.cos(.22)
            q=max(abs(dx)/2.1,abs(dy)/3.2)
            weight=max(0,min(1,(1.32-q)/.32))
            weight=weight*weight*(3-2*weight)
            z=z*(1-weight)+2.25*weight
    for pad in p.get('pads',[]):
        dx,dy=x-pad['x'],y-pad['y'];a=pad['heading']
        u,v=dx*math.cos(a)+dy*math.sin(a),-dx*math.sin(a)+dy*math.cos(a)
        excess=max(abs(u)-pad['width']/2,abs(v)-pad['length']/2,0)
        weight=max(0,min(1,1-excess/pad['fade_m']))
        weight=weight*weight*(3-2*weight)
        z=z*(1-weight)+pad['z']*weight
    return z

def terrain(root,col,p):
    if p['rocky']:
        verts,faces,bands=_rock_shell(p['seed'],p['radius'],p['height'])
        ob=c.mesh('TerrainShell',verts,faces,root,col,'MAT_IslandRock',smooth=False)
        for name in ('MAT_IslandCliff','ENV_WetRock','ENV_RockStrata'):
            ob.data.materials.append(c.M[name])
        for face,band in zip(ob.data.polygons,bands):
            # One material per wide face/terrace: no random triangular patches.
            if band<2:idx=2
            elif band in (3,5):idx=3
            elif band==8:idx=1
            else:
                idx=1 if face.normal.x<-.25 and face.normal.y>.2 else 0
            face.material_index=idx;face.use_smooth=False
        for i in range(12):
            a=i*math.tau/12;r=outline(a,p)*1.04
            c.empty('ShorePoint_'+str(i).zfill(2),root,col,(r*math.cos(a),r*math.sin(a),c.SEA))
        c.empty('HeightMarker',root,col,(0,0,max(z for x,y,z in verts)))
        return ob
    seg=112;rings=24
    verts=[(0,0,height(0,0,p))]
    for k in range(1,rings+1):
        t=k/rings
        for i in range(seg):
            a=math.tau*i/seg;r=outline(a,p)*t
            x,y=r*math.cos(a),r*math.sin(a)
            verts.append((x,y,height(x,y,p)))
    faces=[(0,1+i,1+(i+1)%seg) for i in range(seg)]
    for k in range(rings-1):
        a=1+k*seg;b=a+seg
        for i in range(seg):j=(i+1)%seg;faces.append((a+i,b+i,b+j,a+j))
    top=1+(rings-1)*seg
    # Submerged skirts follow the same shoreline; the bottom is a closed cap.
    for k,(z,f) in enumerate([(c.SEA-.28,1.035),(-1.95,1.05),(-3.,.98)]):
        start=len(verts)
        for i in range(seg):
            a=i*math.tau/seg;r=outline(a,p)*f
            verts.append((r*math.cos(a),r*math.sin(a),z))
        for i in range(seg):j=(i+1)%seg;faces.append((top+i,start+i,start+j,top+j))
        top=start
    faces.append(tuple(reversed(range(top,top+seg))))
    ob=c.mesh('TerrainShell',verts,faces,root,col,'MAT_IslandRock',smooth=True)
    materials=['MAT_IslandRock','MAT_IslandCliff','ENV_WetRock','ENV_RockStrata',
               'MAT_IslandSand','ENV_SandLight','MAT_IslandGrass','ENV_Moss']
    ob.data.materials.clear()
    for mat in materials:ob.data.materials.append(c.M[mat])
    ob.data.update()
    for poly in ob.data.polygons:
        center=poly.center;x,y,z=center
        q=noise.noise_vector(Vector((x*.32+2,y*.32,p['seed'])))[0]
        if z<c.SEA+.25:idx=2
        elif p['rocky']:
            layer=math.sin(z*3.0+math.sin(x*.65)+y*.1)
            idx=3 if layer>.91 and poly.normal.z<.60 else (1 if q>.48 else 0)
            poly.use_smooth=True
        else:
            t=math.hypot(x,y)/outline(math.atan2(y,x),p)
            if t>.79 or z<.5 or (p.get('kind')=='atoll' and t<.47):idx=4 if q<.15 else 5
            elif poly.normal.z<.58:idx=1
            else:idx=6 if q<.1 else 7
        poly.material_index=idx
    # These metadata sockets are newly proposed, not names the game already searches.
    for i in range(12):
        a=i*math.tau/12;r=outline(a,p)
        c.empty('ShorePoint_'+str(i).zfill(2),root,col,(r*math.cos(a),r*math.sin(a),c.SEA))
    c.empty('HeightMarker',root,col,(0,0,max(z for x,y,z in verts)))
    return ob

def rock_at(root,col,x,y,z,size,seed=3):
    from environment_rock_geometry import angular_rock
    return angular_rock('FracturedCoastalRock',(x,y,z),size,root,col,seed=seed)

def coast_details(root,col,p):
    rng=random.Random(p['seed']);n=11 if not p['rocky'] else 9
    for i in range(n):
        a=rng.uniform(-math.pi,math.pi)
        t=rng.uniform(.97,1.025) if p['rocky'] else rng.uniform(.83,1.01)
        r=outline(a,p)*t;x,y=r*math.cos(a),r*math.sin(a);z=height(x,y,p)
        size=rng.uniform(.75,1.50) if p['rocky'] else rng.uniform(.45,1.25)
        rock_at(root,col,x,y,z-.05,(size,size*.77,size*.68),p['seed']+i)
    if p['rocky']:
        # Wide lower shelves interrupt the single-cone silhouette and hold wildlife.
        shelves=[]
        for a,t,s in [(-1.1,1.01,(3.4,2.8,1.05)),(2.2,.73,(3.0,2.8,2.6)),(-2.0,1.01,(3.0,2.4,.95))]:
            r=outline(a,p)*t;x,y=r*math.cos(a),r*math.sin(a)
            ob=rock_at(root,col,x,y,height(x,y,p)-.28,s,p['seed']+int(a*10))
            shelves.append((x,y,ob))
        return shelves
    return []

def path(root,col,p,start,end,width=.58):
    # Individual fitted stone slabs sit on the sampled terrain, never floating.
    a,b=Vector(start),Vector(end);distance=(b-a).length;steps=max(2,int(distance/.45))
    for i in range(steps+1):
        q=a.lerp(b,i/steps);z=height(q.x,q.y,p)
        c.box('StoneFootpath',(q.x,q.y,z+.024),(width,.39,.055),root,col,
              'ENV_Concrete',(0,0,-math.atan2(b.x-a.x,b.y-a.y)),bevel=.01)
