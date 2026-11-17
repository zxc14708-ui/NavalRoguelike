"""Closed, flat-shaded miniature geometry and export helpers."""
import bpy,sys,math,json
from pathlib import Path
from mathutils import Vector,Matrix
OUT=Path(__file__).resolve().parent
sys.path.insert(0,str(OUT.parent/'CoastalObjectsPack_v4/Source'))
import environment_detail_common as c
PALETTE={'WW2 Hull':(.46,.50,.53,1),'WW2 Deck':(.36,.33,.28,1),
 'WW2 Gun':(.33,.36,.38,1),'WW2 Dark':(.10,.125,.14,1),
 'WW2 Enemy':(.40,.41,.44,1),'WW2 Enemy Deck':(.30,.28,.25,1),
 'WW2 Olive':(.34,.38,.26,1),'WW2 Air Dark':(.18,.20,.16,1),
 'WW2 Air Belly':(.51,.56,.53,1),'WW2 Glass':(.035,.13,.17,1),
 'WW2 Brass':(.62,.45,.19,1),'WW2 Orange':(.73,.39,.13,1),
 'WW2 Enemy Red':(.56,.17,.13,1),'WW2 White':(.74,.76,.70,1),
 'WW2 Waterline':(.17,.12,.12,1)}

def setup():
 bpy.ops.wm.read_factory_settings(use_empty=True);c.COLORS.update(PALETTE);c.setup()

def root(name):
 col=c.collection(name);r=c.empty(name,None,col);return r,col

def box(n,loc,dims,p,col,mat='WW2 Hull',rot=(0,0,0)):
 return c.box(n,loc,dims,p,col,mat,rot)

def cyl(n,loc,r,d,p,col,mat='WW2 Dark',segments=12,rot=(0,0,0)):
 return c.cyl(n,loc,r,d,p,col,mat,segments,rot,False)

def tube(n,loc,r,length,p,col,mat='WW2 Dark'):
 return cyl(n,loc,r,length,p,col,mat,12,(math.pi/2,0,0))

def beam(n,a,b,width,p,col,mat='WW2 Hull'):
 return c.beam(n,a,b,width,p,col,mat)

def plate(n,points,thickness,p,col,mat='WW2 Hull'):
 normal=(Vector(points[1])-Vector(points[0])).cross(Vector(points[2])-Vector(points[0])).normalized()
 verts=[tuple(Vector(point)+normal*dz) for dz in (-thickness/2,thickness/2) for point in points]
 k=len(points);faces=[tuple(reversed(range(k))),tuple(range(k,2*k))]
 faces += [(i,(i+1)%k,(i+1)%k+k,i+k) for i in range(k)]
 return c.mesh(n,verts,faces,p,col,mat)

def clipped(n,loc,dims,p,col,mat='WW2 Hull',cut=.09):
 x,y,z=[v/2 for v in dims];t=min(cut,x*.45,y*.45)
 outline=[(-x+t,-y),(x-t,-y),(x,-y+t),(x,y-t),(x-t,y),(-x+t,y),(-x,y-t),(-x,-y+t)]
 verts=[(a,b,zz) for zz in (-z,z) for a,b in outline]
 faces=[tuple(reversed(range(8))),tuple(range(8,16))]
 faces += [(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
 return c.mesh(n,verts,faces,p,col,mat,loc)

def bore(n,x,y,z,r,p,col):
 # Short annular sleeve with a recessed back cap; front is genuinely hollow.
 k=12;verts=[(x+rr*math.cos(i*math.tau/k),yy,z+rr*math.sin(i*math.tau/k)) for yy,rr in [(y-.10,r),(y,r),(y,r*.66),(y-.11,r*.66)] for i in range(k)]
 faces=[]
 for j in range(3):
  for i in range(k):q=(i+1)%k;faces.append((j*k+i,j*k+q,(j+1)*k+q,(j+1)*k+i))
 faces.append(tuple(reversed(range(3*k,4*k))))
 return c.mesh(n,verts,faces,p,col,'WW2 Dark')

def hull(n,length,width,deck,p,col,mat='WW2 Enemy',keel=-.65):
 stations=[(-.50,.70),(-.40,.94),(-.24,1),(.19,1),(.35,.85),(.44,.48),(.50,.018)]
 verts=[]
 for yy,ww in stations:
  y=yy*length;w=ww*width/2
  verts.extend([(-w,y,deck),(-w*.92,y,-.10),(-w*.56,y,keel),(w*.56,y,keel),(w*.92,y,-.10),(w,y,deck)])
 faces=[tuple(reversed(range(6)))]
 for j in range(len(stations)-1):
  for i in range(6):q=(i+1)%6;faces.append((j*6+i,j*6+q,(j+1)*6+q,(j+1)*6+i))
 faces.append(tuple(range((len(stations)-1)*6,len(stations)*6)))
 c.mesh(n,verts,faces,p,col,mat)
 # Fitted polygon deck follows the hull, including the sharply tapered bow.
 outline=[(-ww*width*.48,yy*length,deck+.012) for yy,ww in stations]
 outline += [(ww*width*.48,yy*length,deck+.012) for yy,ww in reversed(stations)]
 plate('FittedDeck',outline,.025,p,col,'WW2 Enemy Deck')

def rail(a,b,z,p,col):
 a=Vector((a[0],a[1],z));b=Vector((b[0],b[1],z))
 beam('TopRail',a+Vector((0,0,.22)),b+Vector((0,0,.22)),.018,p,col)
 count=max(1,int((b-a).length/.52))
 for i in range(count+1):
  v=a.lerp(b,i/count);beam('RailPost',v,v+Vector((0,0,.23)),.019,p,col)

def funnel(loc,width,height,p,col,mat='WW2 Hull'):
 x,y,z=loc
 clipped('FunnelCasing',(x,y,z+height/2),(width,width*1.20,height),p,col,mat,.10)
 clipped('SootBlackCap',(x,y,z+height+.015),(width*1.04,width*1.24,.09),p,col,'WW2 Dark',.10)
 for side in (-1,1):
  box('FunnelVent',(x+side*(width/2+.005),y,z+.23),(.014,width*.60,.13),p,col,'WW2 Dark')
  beam('FunnelStay',(x+side*width*.65,y-.1,z+.05),(x+side*width*.48,y,z+height*.65),.025,p,col,'WW2 Dark')

def director(loc,size,p,col):
 x,y,z=loc
 clipped('OpticalDirector',(x,y,z),(size*.58,size*.49,size*.34),p,col,'WW2 Gun',.04)
 box('OpticalRangefinderBar',(x,y,z+.04),(size,size*.15,size*.12),p,col,'WW2 Hull')
 for side in (-1,1):box('RangefinderLens',(x+side*size*.41,y+size*.09,z+.04),(size*.12,.01,size*.085),p,col,'WW2 Glass')

def mast(loc,height,p,col):
 x,y,z=loc
 beam('MastLeg',(x-.24,y-.13,z),(x,y,z+height),.045,p,col,'WW2 Dark')
 beam('MastLeg',(x+.24,y-.13,z),(x,y,z+height),.045,p,col,'WW2 Dark')
 beam('MastBackstay',(x,y+.20,z),(x,y,z+height),.03,p,col,'WW2 Dark')
 box('Yardarm',(x,y,z+height*.75),(.88,.034,.034),p,col,'WW2 Dark')
 radar=c.empty('RadarPivot',p,col,(x,y,z+height+.04))
 box('PeriodRadarFrame',(0,0,0),(.68,.045,.23),radar,col,'WW2 Gun')
 for xx in (-.26,-.13,0,.13,.26):box('RadarGrid',(xx,.028,0),(.012,.015,.20),radar,col,'WW2 White')
 return radar

def export(r):
 c.finish(r);obs=c.descendants(r);saved={o:o.name for o in bpy.data.objects}
 for i,o in enumerate(bpy.data.objects):o.name='TEMP_%05d'%i
 for o in obs:o.name=o['export_name']
 bpy.ops.object.select_all(action='DESELECT')
 for o in obs:o.select_set(True)
 bpy.context.view_layer.objects.active=r
 bpy.ops.export_scene.fbx(filepath=str(OUT/'Models'/(r['export_name']+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_NONE',bake_anim=False)
 for i,o in enumerate(bpy.data.objects):o.name='RESTORE_%05d'%i
 for o,n in saved.items():o.name=n

def bounds(r):
 bpy.context.view_layer.update()
 points=[o.matrix_world@v.co for o in c.descendants(r) if o.type=='MESH' for v in o.data.vertices]
 lo=[min(v[i] for v in points) for i in range(3)];hi=[max(v[i] for v in points) for i in range(3)]
 return lo,hi
