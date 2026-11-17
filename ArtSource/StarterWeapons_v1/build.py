"""Build two actual level-one art assets. Blender 5.2, no external packages.

1.8m cell envelope, +Y bow, applied meshes, articulated export sockets.
Only writes this source pack and the new StarterWeapons_v1 model folder.
"""
import json
import math
from pathlib import Path
import bpy
import bmesh
from mathutils import Vector

OUT = Path(__file__).resolve().parent
PROJECT = OUT.parents[1]
MODELS = PROJECT / 'Assets/_Game/Art/Models/StarterWeapons_v1'
MODELS.mkdir(parents=True, exist_ok=True)
PALETTE = {'Deck Grey': (.28, .33, .35),
           'Naval Blue Grey': (.12, .20, .27),
           'Gunmetal': (.035, .05, .06)}
MATS = {}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1
    MATS.clear()
    for name, color in PALETTE.items():
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = (*color, 1)
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get('Principled BSDF')
        bsdf.inputs['Base Color'].default_value = (*color, 1)
        bsdf.inputs['Roughness'].default_value = .5
        MATS[name] = mat


def attach(ob, parent):
    bpy.context.view_layer.update()
    world = ob.matrix_world.copy()
    ob.parent = parent
    ob.matrix_world = world
    return ob


def empty(name, loc, parent=None):
    ob = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(ob)
    ob.location = loc
    ob.empty_display_size = .09
    return attach(ob, parent) if parent else ob


def finish(ob, name, parent, mat):
    ob.name = name
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    ob.data.materials.append(MATS[mat])
    return attach(ob, parent)


def box(name, loc, size, parent, mat='Naval Blue Grey'):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    ob = bpy.context.object
    ob.dimensions = size
    return finish(ob, name, parent, mat)


def cyl(name, loc, radius, depth, parent, mat='Deck Grey', n=10, axis='Z', top=None):
    bpy.ops.mesh.primitive_cone_add(vertices=n, radius1=radius,
                                   radius2=radius if top is None else top,
                                   depth=depth, end_fill_type='NGON', location=loc)
    ob = bpy.context.object
    if axis == 'Y': ob.rotation_euler.x = math.pi / 2
    if axis == 'X': ob.rotation_euler.y = math.pi / 2
    return finish(ob, name, parent, mat)


def mesh(name, verts, faces, parent, mat='Naval Blue Grey'):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    ob = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(ob)
    return finish(ob, name, parent, mat)


def extrude(name, polygon, depth, plane, center, parent, mat='Naval Blue Grey'):
    # polygon in XY or XZ, two closed caps; normals are recalculated before export.
    verts = []
    for d in (-depth / 2, depth / 2):
        for a, b in polygon:
            offset = (a, b, d) if plane == 'XY' else (a, d, b)
            verts.append(tuple(center[i] + offset[i] for i in range(3)))
    n = len(polygon)
    faces = [tuple(range(n - 1, -1, -1)), tuple(range(n, n * 2))]
    faces.extend((i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n))
    return mesh(name, verts, faces, parent, mat)


def barrel(name, x, z, stations, parent, n=10, mat='Gunmetal'):
    # Connected rings, closed rear and recessed bore. No fake flat muzzle disk.
    rings = list(stations)
    end_y, end_r = rings[-1]
    rings.extend([(end_y, end_r * .58), (end_y - .11, end_r * .58)])
    verts = [(x + r * math.cos(i * math.tau / n), y,
              z + r * math.sin(i * math.tau / n)) for y, r in rings for i in range(n)]
    faces = [tuple(range(n - 1, -1, -1))]
    for j in range(len(rings) - 1):
        for i in range(n):
            k = (i + 1) % n
            faces.append((j*n+i, j*n+k, (j+1)*n+k, (j+1)*n+i))
    faces.append(tuple((len(rings)-1)*n+i for i in range(n)))
    return mesh(name, verts, faces, parent, mat)


def ring(name, loc, radius, thickness, parent, mat='Gunmetal', axis='X', n=8):
    bpy.ops.mesh.primitive_torus_add(major_segments=n, minor_segments=3,
        location=loc, major_radius=radius, minor_radius=thickness)
    ob = bpy.context.object
    ob.rotation_euler = (0, math.pi/2, 0) if axis == 'X' else (math.pi/2, 0, 0)
    return finish(ob, name, parent, mat)


def base(root):
    poly = [(-.7,-.85),(.7,-.85),(.85,-.7),(.85,.7),(.7,.85),(-.7,.85),(-.85,.7),(-.85,-.7)]
    extrude('DeckAdapter', poly, .12, 'XY', (0,0,.06), root, 'Deck Grey')
    for x in (-.64,.64):
        for y in (-.64,.64):
            cyl('MountBolt', (x,y,.135), .033, .03, root, 'Gunmetal', 6)


def cannon():
    root = empty('MOD_BasicNavalGun_L1', (0,0,0))
    base(root)
    cyl('FootFlange',(0,0,.17),.37,.10,root,'Naval Blue Grey',12)
    cyl('TaperedPedestal',(0,0,.435),.29,.43,root,'Naval Blue Grey',10,top=.18)
    cyl('TraverseBearing',(0,0,.665),.23,.07,root,'Gunmetal',12)
    yaw = empty('TurretPivot',(0,0,.70),root)
    for side in (-1,1):
        box('CradleCheek',(side*.18,.02,.81),(.11,.43,.23),yaw)
    cyl('TrunnionAxle',(0,.08,.91),.10,.55,yaw,'Deck Grey',8,'X')
    # Two front shield wings leave the barrel opening visible. Open rear.
    for side in (-1,1):
        poly=[(side*.16,.65),(side*.54,.60),(side*.61,1.10),
              (side*.48,1.24),(side*.16,1.20)]
        extrude('ShieldWing',poly,.075,'XZ',(0,.32,0),yaw)
        box('ShieldRearBrace',(side*.45,.11,.77),(.06,.40,.06),yaw,'Deck Grey')
    box('ShieldApron',(0,.32,.64),(1.04,.08,.13),yaw)
    elev=empty('ElevationPivot',(0,.08,.91),yaw)
    box('Breech',(0,-.18,.91),(.24,.46,.24),elev,'Gunmetal')
    box('BreechTop',(0,-.18,1.045),(.19,.32,.04),elev,'Deck Grey')
    barrel('GunTube',0,.91,[(.02,.125),(.40,.125),(.48,.095),(1.46,.073),(1.55,.083)],elev,10,'Deck Grey')
    cyl('RecoilBuffer',(0,.15,.755),.055,.60,elev,'Gunmetal',8,'Y')
    cyl('RecoilRod',(0,.57,.755),.03,.26,elev,'Deck Grey',8,'Y')
    # One readable training wheel; unnecessary tiny fasteners omitted.
    ring('ElevationHandwheel',(-.34,-.21,.83),.115,.017,yaw,'Gunmetal')
    box('WheelSpokeV',(-.34,-.21,.83),(.022,.025,.21),yaw,'Deck Grey')
    box('WheelSpokeH',(-.34,-.21,.83),(.022,.21,.025),yaw,'Deck Grey')
    cyl('HandwheelHub',(-.30,-.21,.83),.037,.13,yaw,'Deck Grey',6,'X')
    empty('Muzzle',(0,1.56,.91),elev)
    return root


def machine_gun():
    root=empty('MOD_MultipurposeMG_L1',(0,0,0))
    base(root)
    cyl('FootFlange',(0,0,.16),.25,.08,root,'Naval Blue Grey',10)
    cyl('PintlePedestal',(0,0,.43),.16,.46,root,'Naval Blue Grey',8,top=.10)
    cyl('TraverseBearing',(0,0,.69),.15,.07,root,'Gunmetal',10)
    yaw=empty('TurretPivot',(0,0,.73),root)
    for side in (-1,1):
        box('ElevationFork',(side*.13,0,.84),(.055,.18,.22),yaw,'Deck Grey')
    cyl('Trunnion',(0,0,.91),.075,.35,yaw,'Deck Grey',8,'X')
    elev=empty('ElevationPivot',(0,0,.91),yaw)
    box('Receiver',(0,-.13,.96),(.20,.49,.18),elev,'Gunmetal')
    box('ReceiverTop',(0,-.13,1.065),(.17,.42,.035),elev,'Deck Grey')
    box('FeedBridge',(.18,-.075,.98),(.20,.16,.07),elev,'Gunmetal')
    box('AmmoBox',(.34,-.10,.88),(.27,.36,.27),elev)
    box('AmmoLid',(.34,-.10,1.03),(.29,.38,.035),elev,'Deck Grey')
    box('AmmoLatch',(.485,-.10,.91),(.025,.07,.105),elev,'Gunmetal')
    barrel('CoolingJacket',0,.96,[(.09,.075),(.43,.075),(.47,.058)],elev,8,'Deck Grey')
    barrel('SingleBarrel',0,.96,[(.42,.040),(1.17,.035),(1.20,.048),(1.28,.048)],elev,8,'Gunmetal')
    # Flat dark vent marks instead of expensive boolean perforations.
    for y in (.17,.28,.39):
        for side in (-1,1):
            box('CoolingVent',(side*.072,y,.96),(.006,.055,.035),elev,'Gunmetal')
    box('RearGripBridge',(0,-.40,.93),(.33,.055,.045),elev,'Deck Grey')
    for x in (-.15,.15):
        cyl('RearGrip',(x,-.40,.87),.026,.16,elev,'Gunmetal',6)
    box('SightStem',(-.065,-.08,1.14),(.024,.03,.13),elev,'Deck Grey')
    ring('RingSight',(-.065,-.08,1.245),.075,.012,elev,'Gunmetal','Y',8)
    box('SightPost',(-.065,-.08,1.22),(.012,.018,.085),elev,'Gunmetal')
    empty('Muzzle',(0,1.29,.96),elev)
    return root


def export(root):
    # Merge by articulated parent, preserving sockets and material slots.
    for parent in [root]+[o for o in root.children_recursive if o.type=='EMPTY']:
        obs=[o for o in parent.children if o.type=='MESH']
        if not obs: continue
        bpy.ops.object.select_all(action='DESELECT')
        for ob in obs: ob.select_set(True)
        bpy.context.view_layer.objects.active=obs[0]
        bpy.ops.object.join()
        ob=bpy.context.object
        ob.name=parent.name+'_Visual'
        bm=bmesh.new();bm.from_mesh(ob.data)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bmesh.ops.triangulate(bm,faces=list(bm.faces))
        bm.to_mesh(ob.data);bm.free()
        for p in ob.data.polygons:p.use_smooth=False
    objects=[root]+list(root.children_recursive)
    points=[o.matrix_world@v.co for o in objects if o.type=='MESH' for v in o.data.vertices]
    tris=sum(len(o.data.polygons) for o in objects if o.type=='MESH')
    assert 300<=tris<=900, f'{root.name}: {tris} triangles exceeds cell budget'
    assert all(all(abs(s-1)<1e-5 for s in o.scale) for o in objects)
    bounds=[[round(fn(p[i] for p in points),5) for i in range(3)] for fn in (min,max)]
    assert abs(bounds[0][2])<.001
    name=root.name
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(name+'.blend')))
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=str(MODELS/(name+'.fbx')),use_selection=True,
        object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_NONE',bake_anim=False,
        add_leaf_bones=False,mesh_smooth_type='FACE',use_triangles=True)
    return {'name':name,'triangles':tris,'meshes':sum(o.type=='MESH' for o in objects),
            'bounds_blender_m':bounds,'materials':list(PALETTE),
            'sockets':['TurretPivot','ElevationPivot','Muzzle'],
            'footprint_m':[1.7,1.7], 'forward_blender':'+Y'}


reports=[]
for build in (cannon,machine_gun):
    reset()
    reports.append(export(build()))
(OUT/'manifest.json').write_text(json.dumps(reports,indent=2),encoding='utf-8')
print('STARTER_WEAPONS_EXPORT_OK '+json.dumps(reports))
