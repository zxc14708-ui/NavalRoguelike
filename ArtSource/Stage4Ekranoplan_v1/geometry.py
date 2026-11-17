"""Meter-scale low-poly mesh primitives. Blender forward is +Y, up is +Z."""
import math

import bmesh
import bpy
from mathutils import Vector

HULL = 'Enemy hull grey'
STEEL = 'Enemy superstructure grey'
DARK = 'Enemy Dark'
GUN = 'Enemy weapon dark'
METAL = 'Gunmetal'
GLASS = 'Radar Glass'
RED = 'Warning red'
DECK = 'Stealth deck'
YELLOW = 'Warning Yellow'
WHITE = 'MAT_IslandWhite'
CONCRETE = 'ENV_Concrete'

# Blender review colors approximate the game palette, including runtime enemy tint.
PALETTE = {
    HULL: (.217, .226, .239), STEEL: (.33, .365, .39),
    DARK: (.038, .048, .058), GUN: (.092, .107, .117), METAL: (.062, .079, .092),
    GLASS: (.024, .103, .138), RED: (.604, .015, .01),
    'Emergency Red': (.604, .015, .01), DECK: (.365, .068, .040),
    'Deck Grey': (.246, .277, .281), 'Naval Blue Grey': (.275, .336, .380),
    YELLOW: (.94, .53, .037), WHITE: (.84, .86, .83), CONCRETE: (.329, .354, .337),
}


def material(name):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = (*PALETTE[name], 1)
    return mat


def node(name, collection, parent=None, at=(0, 0, 0), direction=None):
    obj = bpy.data.objects.new(name, None)
    collection.objects.link(obj)
    obj.parent = parent
    obj.location = at
    obj['part_name'] = name
    obj.empty_display_size = .28
    if direction is not None:
        obj.rotation_euler = Vector(direction).to_track_quat('Z', 'Y').to_euler()
    return obj


class Mesh:
    """Accumulate closed low-poly pieces under one mesh origin."""
    def __init__(self):
        self.vertices = []
        self.faces = []
        self.colors = []

    def add(self, vertices, faces, color):
        offset = len(self.vertices)
        self.vertices.extend(vertices)
        self.faces.extend(tuple(offset+i for i in face) for face in faces)
        self.colors.extend([color]*len(faces) if isinstance(color, str) else color)

    def loft(self, rings, color, bands=None, caps=True):
        n = len(rings[0])
        faces = []
        colors = []
        for j in range(len(rings)-1):
            for i in range(n):
                q = (i+1) % n
                faces.append((j*n+i, j*n+q, (j+1)*n+q, (j+1)*n+i))
                colors.append(bands[i] if bands else color)
        if caps:
            faces += [tuple(reversed(range(n))), tuple(range((len(rings)-1)*n, len(rings)*n))]
            colors += [color, color]
        self.add([point for ring in rings for point in ring], faces, colors)

    def prism(self, outline, low, high, color, top=None):
        self.loft([[(x,y,z) for x,y in outline] for z in (low,high)], color)
        self.colors[-1] = top or color

    def box(self, at, dims, color, yaw=0, bevel=0):
        x,y,z = at
        a,b,c = (d/2 for d in dims)
        if bevel:
            k = min(bevel, a*.8, b*.8)
            sides = [(-a+k,-b),(a-k,-b),(a,-b+k),(a,b-k),(a-k,b),(-a+k,b),(-a,b-k),(-a,-b+k)]
        else:
            sides = [(-a,-b),(a,-b),(a,b),(-a,b)]
        u,v = math.cos(yaw),math.sin(yaw)
        self.prism([(x+a*u-b*v,y+a*v+b*u) for a,b in sides],z-c,z+c,color)

    def beam(self, a, b, width, color, depth=None):
        a,b = Vector(a),Vector(b)
        w = width/2
        h = (depth or width)/2
        rot = (b-a).to_track_quat('Z','Y')
        corners = [(-w,-h,0),(w,-h,0),(w,h,0),(-w,h,0)]
        self.loft([[tuple(point+rot@Vector(v)) for v in corners] for point in (a,b)],color)

    def tube(self, center, axis, profile, color, n=10, bands=None, hollow=False):
        """Profile pairs are distance along axis and radius. Close hollow loop for a visible rim."""
        rot = Vector(axis).to_track_quat('Z','Y')
        p = Vector(center)
        rings = [[tuple(p+rot@Vector((r*math.cos(i*math.tau/n),r*math.sin(i*math.tau/n),d)))
                  for i in range(n)] for d,r in profile]
        if not hollow:
            self.loft(rings,color)
            if bands:
                for j,mat in enumerate(bands):
                    self.colors[-(len(profile)-1-j)*n-2:-(len(profile)-2-j)*n-2] = [mat]*n
            return
        faces, mats = [], []
        for j in range(len(rings)):
            k = (j+1)%len(rings)
            for i in range(n):
                q = (i+1)%n
                faces.append((j*n+i,j*n+q,k*n+q,k*n+i))
                mats.append(bands[j] if bands else color)
        self.add(sum(rings, []),faces,mats)

    def object(self,name,collection,parent,at=(0,0,0)):
        data = bpy.data.meshes.new(name)
        data.from_pydata(self.vertices, [], self.faces)
        data.update()
        slots = list(dict.fromkeys(self.colors))
        for slot in slots:
            data.materials.append(material(slot))
        for face,color in zip(data.polygons,self.colors):
            face.material_index = slots.index(color)
        bm = bmesh.new()
        bm.from_mesh(data)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(data)
        bm.free()
        obj = bpy.data.objects.new(name,data)
        collection.objects.link(obj)
        obj.location = at
        obj.parent = parent
        obj['part_name'] = name
        return obj


def hull(mesh, stations, deck=HULL, cargo_recess=0):
    """Stations give longitudinal position, beam radius, keel, chine and crown."""
    rings=[]
    for y,w,bottom,chine,top in stations:
        rings.append([
            (-w*.44,y,bottom),(-w*.88,y,bottom*.55+chine*.45),(-w,y,chine),
            (-w*.96,y,top*.72+chine*.28),(-w*.61,y,top*.985),(0,y,top),
            (w*.61,y,top*.985),(w*.96,y,top*.72+chine*.28),(w,y,chine),
            (w*.88,y,bottom*.55+chine*.45),(w*.44,y,bottom),
        ])
    if cargo_recess:
        mouth=[(x*.74,y,1.05+(z-1.05)*.55) for x,y,z in rings[-1]]
        rings.extend([mouth,[(x,y-cargo_recess,z) for x,y,z in mouth]])
    mesh.loft(rings,HULL,[DARK,DARK,HULL,HULL,deck,deck,HULL,HULL,DARK,DARK,DARK])
    if cargo_recess:
        mesh.colors[-12:]=[DARK]*12


def wing(mesh, side, stations, tip_color=RED, body=HULL):
    rings = []
    for x,a,b,z,t in stations:
        length = b-a
        rings.append([(side*x,a,z),(side*x,a+length*.25,z-t*.43),(side*x,b-length*.20,z-t*.36),
                      (side*x,b,z),(side*x,b-length*.23,z+t*.38),(side*x,a+length*.34,z+t*.57)])
    # Each span interval is a separate material zone on a continuous skin.
    mesh.loft(rings,body)
    if tip_color:
        mesh.colors[-8:] = [tip_color]*8


def vertical_tail(mesh, stations):
    rings = [[(-w,y0,z),(w,y0,z),(w,y1,z),(-w,y1,z)] for z,y0,y1,w in stations]
    mesh.loft(rings,HULL)


def float_tip(mesh, x, y, length, radius, color=RED):
    axis=(0,1,0)
    mesh.tube((x,y,.06),axis,[(-length*.5,radius*.32),(-length*.32,radius),
                              (length*.28,radius),(length*.5,radius*.27)],color,8)

