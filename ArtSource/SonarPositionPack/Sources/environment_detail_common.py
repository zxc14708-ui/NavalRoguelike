"""Standalone, texture-free miniature coastline geometry. Does not touch Unity."""
import math
import random
import bpy
import bmesh
from mathutils import Euler, Matrix, Vector
from mathutils import noise

SEA=-.9
M={}
COLORS={
 'MAT_IslandRock':(.33,.32,.30,1), 'MAT_IslandCliff':(.42,.39,.34,1),
 'MAT_IslandSand':(.72,.64,.47,1), 'MAT_IslandGrass':(.22,.36,.17,1),
 'MAT_IslandTree':(.10,.25,.12,1), 'MAT_IslandWhite':(.86,.86,.82,1),
 'MAT_IslandRed':(.60,.13,.10,1), 'MAT_IslandRust':(.38,.21,.12,1),
 'MAT_IslandSeal':(.27,.25,.23,1),
 'ENV_WetRock':(.16,.20,.20,1), 'ENV_RockStrata':(.46,.44,.40,1),
 'ENV_SandLight':(.77,.69,.52,1), 'ENV_Moss':(.25,.38,.18,1),
 'ENV_PineHighlight':(.20,.36,.20,1), 'ENV_Bark':(.24,.18,.12,1),
 'ENV_Concrete':(.50,.52,.48,1), 'ENV_Wood':(.42,.31,.20,1),
 'Gunmetal':(.045,.065,.07,1), 'Radar Glass':(.035,.16,.19,1),
 'Warning Yellow':(.94,.61,.14,1), 'ENV_Lamp':(1.,.73,.27,1),
 'ENV_RustLight':(.52,.26,.11,1), 'ENV_SealMuzzle':(.44,.46,.42,1),
 'ENV_Foam':(.77,.87,.85,1)}

def setup():
    for name,color in COLORS.items():
        mat=bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mat.diffuse_color=color;mat.use_nodes=True
        shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        shader.inputs['Base Color'].default_value=color
        shader.inputs['Roughness'].default_value=.72 if 'Glass' not in name else .22
        shader.inputs['Metallic'].default_value=.5 if name=='Gunmetal' else 0
        M[name]=mat

def collection(name):
    col=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(col);return col
def empty(name,parent,col,loc=(0,0,0)):
    ob=bpy.data.objects.new(name,None);col.objects.link(ob);ob.parent=parent;ob.location=loc
    ob['export_name']=name;return ob
def mesh(name,verts,faces,parent,col,mat='MAT_IslandRock',loc=(0,0,0),rot=(0,0,0),smooth=False):
    data=bpy.data.meshes.new(name+'Mesh');data.from_pydata(verts,[],faces);data.update()
    if any(abs(x)>1e-8 for x in rot):data.transform(Euler(rot).to_matrix().to_4x4())
    ob=bpy.data.objects.new(name,data);col.objects.link(ob);ob.parent=parent;ob.location=loc
    ob['export_name']=name;data.materials.append(M[mat] if isinstance(mat,str) else mat)
    for face in data.polygons:face.use_smooth=smooth
    return ob
def box(name,loc,dims,parent,col,mat='MAT_IslandWhite',rot=(0,0,0),bevel=0):
    x,y,z=[n/2 for n in dims]
    ob=mesh(name,[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),
                 (-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)],
        [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],parent,col,mat,loc,rot)
    if bevel:
        mod=ob.modifiers.new('Soft manufactured edges','BEVEL');mod.width=bevel;mod.segments=2
    return ob
def lathe(name,loc,profile,parent,col,mat='MAT_IslandWhite',segments=24,smooth=True,rot=(0,0,0)):
    # Profile radii are positive; planar caps close each shell for reliable normals.
    verts=[(r*math.cos(i*math.tau/segments),r*math.sin(i*math.tau/segments),z)
           for z,r in profile for i in range(segments)]
    faces=[tuple(reversed(range(segments))),tuple(range((len(profile)-1)*segments,len(profile)*segments))]
    for k in range(len(profile)-1):
        for i in range(segments):
            j=(i+1)%segments;a=k*segments;b=a+segments
            faces.append((a+i,a+j,b+j,b+i))
    return mesh(name,verts,faces,parent,col,mat,loc,rot,smooth)
def cyl(name,loc,radius,depth,parent,col,mat='Gunmetal',segments=16,rot=(0,0,0),smooth=True):
    return lathe(name,loc,[(-depth/2,radius),(depth/2,radius)],parent,col,mat,segments,smooth,rot)
def frustum(name,loc,lower,upper,height,parent,col,mat='MAT_IslandWhite',rot=(0,0,0)):
    a,b=[v/2 for v in lower];c,d=[v/2 for v in upper];z=height/2
    return mesh(name,[(-a,-b,-z),(a,-b,-z),(a,b,-z),(-a,b,-z),
                     (-c,-d,z),(c,-d,z),(c,d,z),(-c,d,z)],
         [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],parent,col,mat,loc,rot)
def beam(name,start,end,width,parent,col,mat='Gunmetal'):
    p,q=Vector(start),Vector(end);v=q-p
    return box(name,(p+q)/2,(width,width,v.length),parent,col,mat,v.to_track_quat('Z','Y').to_euler())
def sphere(name,loc,dims,parent,col,mat='MAT_IslandSeal',segments=20,rings=10,rot=(0,0,0),deform=0,seed=1):
    # dims are full dimensions; no duplicated UV-sphere pole vertices.
    verts=[(0,0,-dims[2]/2)]
    for k in range(1,rings):
        t=k*math.pi/rings
        for i in range(segments):
            a=i*math.tau/segments;s=math.sin(t)
            x,y,z=dims[0]/2*s*math.cos(a),dims[1]/2*s*math.sin(a),-dims[2]/2*math.cos(t)
            f=1+deform*noise.noise_vector(Vector((x*.7+seed,y*.7,z*.7)))[0]
            verts.append((x*f,y*f,z*f))
    top=len(verts);verts.append((0,0,dims[2]/2))
    faces=[(0,1+(i+1)%segments,1+i) for i in range(segments)]
    for k in range(rings-2):
        a=1+k*segments;b=a+segments
        for i in range(segments):j=(i+1)%segments;faces.append((a+i,a+j,b+j,b+i))
    a=1+(rings-2)*segments
    faces.extend((a+i,a+(i+1)%segments,top) for i in range(segments))
    return mesh(name,verts,faces,parent,col,mat,loc,rot,True)
def ring(name,loc,radius,tube,parent,col,mat='Gunmetal',segments=28,cross=6,rot=(0,0,0)):
    verts=[((radius+tube*math.cos(j*math.tau/cross))*math.cos(i*math.tau/segments),
            (radius+tube*math.cos(j*math.tau/cross))*math.sin(i*math.tau/segments),
            tube*math.sin(j*math.tau/cross)) for i in range(segments) for j in range(cross)]
    faces=[]
    for i in range(segments):
        for j in range(cross):
            faces.append((i*cross+j,((i+1)%segments)*cross+j,
              ((i+1)%segments)*cross+(j+1)%cross,i*cross+(j+1)%cross))
    return mesh(name,verts,faces,parent,col,mat,loc,rot,True)
def descendants(root):
    result=[root]
    for child in root.children:result.extend(descendants(child))
    return result
def finish(root):
    for ob in descendants(root):
        if ob.type!='MESH':continue
        if ob.modifiers:
            deps=bpy.context.evaluated_depsgraph_get();data=bpy.data.meshes.new_from_object(ob.evaluated_get(deps))
            ob.modifiers.clear();ob.data=data
        bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free();ob.data.update()
    bpy.context.view_layer.update()
    poses={ob:ob.matrix_world.copy() for ob in descendants(root)}
    for ob,pose in poses.items():
        ob.matrix_parent_inverse=Matrix.Identity(4)
        ob.matrix_basis=poses[ob.parent].inverted()@pose if ob.parent in poses else pose
    bpy.context.view_layer.update()
def tris(root):
    return sum(len(o.data.polygons) for o in descendants(root) if o.type=='MESH')
def clone(root,col):
    bpy.context.view_layer.update();poses={o:o.matrix_world.copy() for o in descendants(root)}
    def rec(src,parent=None):
        ob=src.copy();col.objects.link(ob);ob.parent=parent;ob.matrix_parent_inverse=Matrix.Identity(4)
        ob.matrix_basis=poses[src.parent].inverted()@poses[src] if src.parent in poses else poses[src]
        for ch in src.children:rec(ch,ob)
        return ob
    return rec(root)

def simplify_closed(data,col,ratio=.42):
    """Simplify each closed component independently; keep thin fittings intact."""
    parents=list(range(len(data.vertices)))
    def find(i):
        while parents[i]!=i:parents[i]=parents[parents[i]];i=parents[i]
        return i
    for edge in data.edges:
        a,b=edge.vertices;parents[find(a)]=find(b)
    groups={}
    for face in data.polygons:groups.setdefault(find(face.vertices[0]),[]).append(face)
    output_verts=[];output_faces=[];output_indices=[];output_smooth=[]
    for faces in groups.values():
        indices=sorted({i for face in faces for i in face.vertices});remap={v:i for i,v in enumerate(indices)}
        part=bpy.data.meshes.new('LODComponent')
        part.from_pydata([tuple(data.vertices[i].co) for i in indices],[],
                         [tuple(remap[i] for i in face.vertices) for face in faces]);part.update()
        for material in data.materials:part.materials.append(material)
        for f,original in zip(part.polygons,faces):f.material_index=original.material_index;f.use_smooth=original.use_smooth
        chosen=part;candidate=None;temp=None
        if len(faces)>64:
            temp=bpy.data.objects.new('LODComponentTemp',part);col.objects.link(temp)
            modifier=temp.modifiers.new('Safe component simplification','DECIMATE');modifier.ratio=ratio
            deps=bpy.context.evaluated_depsgraph_get();candidate=bpy.data.meshes.new_from_object(temp.evaluated_get(deps))
            bm=bmesh.new();bm.from_mesh(candidate)
            safe=(all(edge.is_manifold for edge in bm.edges) and all(face.calc_area()>1e-8 for face in bm.faces)
                  and bm.calc_volume(signed=True)>1e-7)
            bm.free()
            if safe:chosen=candidate
        offset=len(output_verts);output_verts.extend(tuple(v.co) for v in chosen.vertices)
        for face in chosen.polygons:
            output_faces.append(tuple(offset+i for i in face.vertices))
            output_indices.append(face.material_index);output_smooth.append(face.use_smooth)
        if temp:bpy.data.objects.remove(temp,do_unlink=True)
        if candidate:bpy.data.meshes.remove(candidate)
        bpy.data.meshes.remove(part)
    result=bpy.data.meshes.new(data.name+'_LOD1');result.from_pydata(output_verts,[],output_faces);result.update()
    for material in data.materials:result.materials.append(material)
    for face,index,smooth in zip(result.polygons,output_indices,output_smooth):
        face.material_index=index;face.use_smooth=smooth
    return result
