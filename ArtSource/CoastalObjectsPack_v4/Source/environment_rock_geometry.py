"""Closed fractured coastal boulders with broad planar faces, not UV spheres."""
import itertools
import math
import random

import bmesh
from mathutils import Matrix, Vector

import environment_detail_common as c


def _polyhedron_points(planes):
    """Intersect convex half-spaces; retain only actual closed hull corners."""
    points=[]
    for first,second,third in itertools.combinations(planes,3):
        matrix=Matrix((first[0],second[0],third[0]))
        if abs(matrix.determinant())<1e-8:continue
        point=matrix.inverted()@Vector((first[1],second[1],third[1]))
        if any(normal.dot(point)>distance+1e-6 for normal,distance in planes):continue
        if not any((point-other).length<1e-5 for other in points):points.append(point)
    return points


def angular_rock(name,loc,dims,parent,col,seed=6):
    """Make a fractured stone. loc is its flat bottom centre, dims full extents.

    Large tilted side slabs, asymmetrical corner cuts and two intersecting roof
    planes produce a quarried/geological silhouette.  All transforms are baked;
    the object has identity rotation/scale and a perfectly planar ground foot.
    """
    if min(dims)<=0:raise ValueError('Rock dimensions must be positive')
    rng=random.Random(seed)
    planes=[(Vector((1,0,0)),1),(Vector((-1,0,0)),1),
            (Vector((0,1,0)),1),(Vector((0,-1,0)),1),
            (Vector((0,0,1)),1),(Vector((0,0,-1)),0)]
    # Wide side planes lean independently: not a bevelled symmetric cube.
    for sx,sy in ((1,0),(-1,0),(0,1),(0,-1)):
        lean=rng.uniform(-.28,.31)
        planes.append((Vector((sx,sy,lean)),rng.uniform(.85,1.05)))
    # Unequal clipped corners read as discrete fracture surfaces.
    for sx,sy in ((1,1),(-1,1),(-1,-1),(1,-1)):
        angle=math.pi/4+rng.uniform(-.19,.19)
        planes.append((Vector((sx*math.cos(angle),sy*math.sin(angle),
                               rng.uniform(-.20,.25))),rng.uniform(.89,1.15)))
    roof_a=rng.uniform(.14,.34);roof_b=rng.uniform(-.19,.15)
    planes.append((Vector((roof_a,roof_b,1)),rng.uniform(.85,.98)))
    planes.append((Vector((-rng.uniform(.16,.30),rng.uniform(.12,.27),1)),
                   rng.uniform(.88,1.03)))
    # Small lower diagonal contact cuts; these are planes, not rounded bevels.
    for sx,sy in ((1,-1),(-1,1)):
        planes.append((Vector((sx*.63,sy*.63,-.45)),rng.uniform(.94,1.10)))
    points=_polyhedron_points(planes)
    if len(points)<8:raise ValueError('Rock half-space intersection is empty')
    bm=bmesh.new()
    for point in points:bm.verts.new(point)
    bmesh.ops.convex_hull(bm,input=list(bm.verts),use_existing_faces=False)
    # Coplanar hull triangles become one broad fracture polygon.  Triangulation
    # at export preserves the polygon's single normal/material on every face.
    bmesh.ops.dissolve_limit(bm,angle_limit=1e-5,verts=list(bm.verts),
                            edges=list(bm.edges),use_dissolve_boundaries=False)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    yaw=rng.uniform(-math.pi,math.pi);cosine=math.cos(yaw);sine=math.sin(yaw)
    for vertex in bm.verts:
        x,y,z=vertex.co
        vertex.co=(cosine*x-sine*y,sine*x+cosine*y,z)
    lows=[min(v.co[i] for v in bm.verts) for i in range(3)]
    highs=[max(v.co[i] for v in bm.verts) for i in range(3)]
    for vertex in bm.verts:
        vertex.co=tuple(((vertex.co[i]-lows[i])/(highs[i]-lows[i])-
                         (.5 if i<2 else 0))*dims[i] for i in range(3))
    bm.verts.ensure_lookup_table();bm.verts.index_update()
    verts=[tuple(vertex.co) for vertex in bm.verts]
    faces=[tuple(vertex.index for vertex in face.verts) for face in bm.faces]
    bm.free()
    ob=c.mesh(name,verts,faces,parent,col,'MAT_IslandRock',loc,smooth=False)
    for material in ('MAT_IslandCliff','ENV_WetRock','ENV_RockStrata'):
        ob.data.materials.append(c.M[material])
    ob.data.update()
    for face in ob.data.polygons:
        normal=face.normal;centre=face.center
        if normal.z<-.12 or centre.z<dims[2]*.12:face.material_index=2
        elif normal.z>.72 and normal.x<-.10:face.material_index=3
        elif normal.x>.35 and normal.y<.38:face.material_index=1
        else:face.material_index=0
        face.use_smooth=False
    ob['rock_style']='broad planar coastal fractures'
    ob['ground_contact_z']=float(loc[2])
    return ob
