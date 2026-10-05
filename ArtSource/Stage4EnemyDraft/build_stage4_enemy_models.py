import bpy
import math
import os
import json
from mathutils import Vector


OUT = r"C:\Users\최병욱\UnityProjects\NavalRoguelike\NavalRoguelike\ArtSource\Stage4EnemyDraft"
MODEL_DIR = os.path.join(OUT, "Models")
PREVIEW_DIR = os.path.join(OUT, "Preview")
for folder in (OUT, MODEL_DIR, PREVIEW_DIR):
    os.makedirs(folder, exist_ok=True)


ASSETS = []


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)


def material(name, color, metallic=0.0, roughness=0.45):
    existing = bpy.data.materials.get(name)
    if existing:
        return existing
    m = bpy.data.materials.new(name)
    m.diffuse_color = color
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    if p:
        p.inputs["Base Color"].default_value = color
        p.inputs["Metallic"].default_value = metallic
        p.inputs["Roughness"].default_value = roughness
    return m


def setup_materials():
    global HULL, DECK, SUPER, DARK, GLASS, SENSOR, RED, YELLOW, MINE, SUB, SUB_DARK, HELI
    HULL = material("Enemy hull grey", (0.24, 0.29, 0.30, 1), 0.48, 0.34)
    DECK = material("Stealth deck", (0.075, 0.105, 0.11, 1), 0.38, 0.38)
    SUPER = material("Enemy superstructure grey", (0.36, 0.42, 0.42, 1), 0.30, 0.38)
    DARK = material("Enemy weapon dark", (0.025, 0.040, 0.045, 1), 0.70, 0.24)
    GLASS = material("Enemy bridge glass", (0.018, 0.12, 0.15, 1), 0.45, 0.18)
    SENSOR = material("Enemy sensor cyan", (0.08, 0.48, 0.55, 1), 0.36, 0.22)
    RED = material("Warning red", (0.85, 0.035, 0.025, 1), 0.18, 0.34)
    YELLOW = material("Warning Yellow", (1.00, 0.56, 0.035, 1), 0.16, 0.38)
    MINE = material("Mine casing", (0.11, 0.14, 0.12, 1), 0.60, 0.45)
    SUB = material("Submarine green", (0.10, 0.23, 0.19, 1), 0.42, 0.38)
    SUB_DARK = material("Submarine dark underside", (0.025, 0.055, 0.050, 1), 0.48, 0.45)
    HELI = material("Deck Marking White", (0.78, 0.84, 0.80, 1), 0.08, 0.50)


def relink(obj, collection):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    collection.objects.link(obj)


def empty(name, parent=None, collection=None, loc=(0, 0, 0), size=0.22):
    o = bpy.data.objects.new(name, None)
    # Blender object names are global, so repeated socket names gain .001
    # suffixes while the full review fleet is built in one scene. Preserve the
    # intended FBX-facing name and normalize it immediately before each export.
    o["export_name"] = name
    o.empty_display_type = 'PLAIN_AXES'
    o.empty_display_size = size
    o.location = loc
    (collection or bpy.context.collection).objects.link(o)
    if parent:
        o.parent = parent
    return o


def apply_transform(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.select_set(False)


def box(name, loc, dims, mat, parent, collection, bevel=0.04, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.scale = (dims[0] / 2, dims[1] / 2, dims[2] / 2)
    o.data.materials.append(mat)
    if bevel:
        mod = o.modifiers.new("Edge bevel", 'BEVEL')
        mod.width = bevel
        mod.segments = 1
    apply_transform(o)
    o.parent = parent
    relink(o, collection)
    return o


def cyl(name, loc, radius, depth, mat, parent, collection, verts=12, rot=(0, 0, 0), bevel=0.018):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    if bevel:
        mod = o.modifiers.new("Edge bevel", 'BEVEL')
        mod.width = bevel
        mod.segments = 1
    apply_transform(o)
    o.parent = parent
    relink(o, collection)
    return o


def sphere(name, loc, dims, mat, parent, collection, seg=16, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, location=loc)
    o = bpy.context.object
    o.name = name
    o.scale = dims
    o.data.materials.append(mat)
    apply_transform(o)
    o.parent = parent
    relink(o, collection)
    return o


def cone(name, loc, r1, r2, depth, mat, parent, collection, verts=12, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    apply_transform(o)
    o.parent = parent
    relink(o, collection)
    return o


def frustum(name, loc, lower, upper, height, mat, parent, collection, shift_y=0):
    lx, ly = lower[0] / 2, lower[1] / 2
    ux, uy = upper[0] / 2, upper[1] / 2
    z0, z1 = -height / 2, height / 2
    verts = [
        (-lx, -ly, z0), (lx, -ly, z0), (lx, ly, z0), (-lx, ly, z0),
        (-ux, -uy + shift_y, z1), (ux, -uy + shift_y, z1),
        (ux, uy + shift_y, z1), (-ux, uy + shift_y, z1),
    ]
    faces = [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    o = bpy.data.objects.new(name, mesh)
    collection.objects.link(o)
    o.parent = parent
    o.location = loc
    o.data.materials.append(mat)
    b = o.modifiers.new("Edge bevel", 'BEVEL')
    b.width = min(0.07, height * 0.08)
    b.segments = 1
    return o


def hull(name, length, width, height, parent, collection, mat=None, bow=0.05, stern_ratio=0.82):
    if mat is None:
        mat = HULL
    half = length / 2
    sections = [
        (-half, width * stern_ratio / 2, height * 0.46, -height * 0.48),
        (-half * 0.82, width / 2, height * 0.50, -height * 0.55),
        (-half * 0.28, width / 2, height * 0.52, -height * 0.62),
        ( half * 0.35, width * 0.46, height * 0.58, -height * 0.58),
        ( half * 0.76, width * 0.30, height * 0.67, -height * 0.42),
        ( half, width * bow / 2, height * 0.45, -height * 0.22),
    ]
    verts = []
    for y, w, top, bottom in sections:
        verts.extend([(-w, y, top), (w, y, top), (-w * 0.70, y, bottom), (w * 0.70, y, bottom)])
    faces = []
    for i in range(len(sections) - 1):
        a, b = i * 4, (i + 1) * 4
        faces += [(a, b, b + 1, a + 1), (a + 2, a + 3, b + 3, b + 2),
                  (a, a + 2, b + 2, b), (a + 1, b + 1, b + 3, a + 3)]
    faces += [(0, 1, 3, 2), ((len(sections)-1)*4, (len(sections)-1)*4+2,
                              (len(sections)-1)*4+3, (len(sections)-1)*4+1)]
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    o = bpy.data.objects.new(name, mesh)
    collection.objects.link(o)
    o.parent = parent
    o.data.materials.append(mat)
    mod = o.modifiers.new("Hull bevel", 'BEVEL')
    mod.width = 0.055
    mod.segments = 1
    return o


def barrel(name, origin, length, radius, parent, collection, mat=None):
    if mat is None:
        mat = DARK
    loc = (origin[0], origin[1] + length / 2, origin[2])
    return cyl(name, loc, radius, length, mat, parent, collection, 8, (math.radians(90), 0, 0), 0.006)


def bridge(root, collection, center_y, width, length, base_z, height=1.15):
    block = frustum("BridgeBlock", (0, center_y, base_z + height / 2),
                    (width, length), (width * 0.72, length * 0.70), height, SUPER, root, collection, length * 0.05)
    box("BridgeWindowFront", (0, center_y + length * 0.38, base_z + height * 0.77),
        (width * 0.68, 0.035, height * 0.15), GLASS, root, collection, 0.008)
    box("BridgeWindowPort", (-width * 0.43, center_y + length * 0.03, base_z + height * 0.75),
        (0.035, length * 0.50, height * 0.14), GLASS, root, collection, 0.008)
    box("BridgeWindowStarboard", (width * 0.43, center_y + length * 0.03, base_z + height * 0.75),
        (0.035, length * 0.50, height * 0.14), GLASS, root, collection, 0.008)
    return block


def gun_mount(root, collection, loc, scale=1.0, prefix="PrimaryGun"):
    base = cyl(prefix + "Base", loc, 0.28 * scale, 0.16 * scale, DECK, root, collection, 12)
    pivot = empty(prefix + "Pivot", base, collection, (0, 0, 0.10 * scale))
    frustum(prefix + "House", (0, 0.04 * scale, 0.18 * scale),
            (0.54 * scale, 0.62 * scale), (0.40 * scale, 0.45 * scale),
            0.36 * scale, SUPER, pivot, collection, 0.05 * scale)
    elev = empty(prefix + "ElevationPivot", pivot, collection, (0, 0.26 * scale, 0.28 * scale))
    barrel(prefix + "Barrel", (0, 0, 0), 0.82 * scale, 0.042 * scale, elev, collection)
    empty(prefix + "Muzzle", elev, collection, (0, 0.82 * scale, 0))
    return pivot


def mast_radar(root, collection, loc, scale=1.0, integrated=False):
    x, y, z = loc
    if integrated:
        mast = frustum("IntegratedMast", (x, y, z + 0.62 * scale),
                       (0.82 * scale, 0.82 * scale), (0.42 * scale, 0.42 * scale),
                       1.24 * scale, SUPER, root, collection)
        for i, (px, py, rz) in enumerate(((0, 0.42, 0), (0, -0.42, 0), (0.42, 0, 90), (-0.42, 0, 90))):
            box(f"AESAFace_{i+1}", (px * scale, py * scale, 0.12 * scale),
                (0.46 * scale, 0.025 * scale, 0.42 * scale), SENSOR, mast, collection,
                0.006, (0, 0, math.radians(rz)))
        pivot = empty("RadarPivot", root, collection, (x, y, z + 1.34 * scale))
        box("IFFBar", (0, 0, 0), (0.94 * scale, 0.06 * scale, 0.08 * scale), SENSOR, pivot, collection, 0.008)
        return pivot
    cyl("Mast", (x, y, z + 0.55 * scale), 0.06 * scale, 1.10 * scale, DARK, root, collection, 8)
    box("MastYard", (x, y, z + 0.92 * scale), (0.92 * scale, 0.06 * scale, 0.06 * scale), DARK, root, collection, 0.006)
    pivot = empty("RadarPivot", root, collection, (x, y, z + 1.16 * scale))
    box("SearchRadar", (0, 0, 0), (0.82 * scale, 0.07 * scale, 0.20 * scale), SENSOR, pivot, collection, 0.01)
    return pivot


def vls_bank(root, collection, center, cols, rows, spacing, prefix="LaunchPoint"):
    cx, cy, cz = center
    box("VLSDeckPlate", (cx, cy, cz - 0.035),
        (cols * spacing + 0.12, rows * spacing + 0.12, 0.05), DARK, root, collection, 0.008)
    idx = 1
    for row in range(rows):
        for col in range(cols):
            x = cx + (col - (cols - 1) / 2) * spacing
            y = cy + (row - (rows - 1) / 2) * spacing
            box(f"VLSHatch_{idx:02d}", (x, y, cz), (spacing * 0.76, spacing * 0.76, 0.035),
                DECK, root, collection, 0.005)
            box(f"VLSMark_{idx:02d}", (x, y + spacing * 0.25, cz + 0.022),
                (spacing * 0.40, 0.012, 0.012), RED, root, collection, 0.002)
            empty(f"{prefix}_{idx:02d}", root, collection, (x, y, cz + 0.12))
            idx += 1


def ciws(root, collection, loc, prefix="CIWS", scale=1.0):
    base = cyl(prefix + "Base", loc, 0.26 * scale, 0.16 * scale, DECK, root, collection, 12)
    pivot = empty(prefix + "Pivot", base, collection, (0, 0, 0.10 * scale))
    house = frustum(prefix + "House", (0, 0, 0.25 * scale),
                    (0.46 * scale, 0.40 * scale), (0.34 * scale, 0.30 * scale),
                    0.44 * scale, SUPER, pivot, collection)
    sphere(prefix + "Radar", (0, -0.02 * scale, 0.51 * scale),
           (0.18 * scale, 0.18 * scale, 0.22 * scale), SENSOR, pivot, collection, 12, 6)
    elev = empty(prefix + "ElevationPivot", pivot, collection, (0, 0.18 * scale, 0.30 * scale))
    barrel(prefix + "BarrelCluster", (0, 0, 0), 0.48 * scale, 0.034 * scale, elev, collection)
    empty(prefix + "Muzzle", elev, collection, (0, 0.48 * scale, 0))
    return pivot


def quad_launcher(root, collection, loc, prefix, yaw=0, scale=1.0):
    base = box(prefix + "Base", loc, (0.86 * scale, 1.10 * scale, 0.16 * scale), DECK, root, collection, 0.025,
               (0, 0, math.radians(yaw)))
    pivot = empty(prefix + "Pivot", base, collection, (0, 0, 0.15 * scale))
    for i in range(4):
        x = (i - 1.5) * 0.17 * scale
        cyl(f"{prefix}Tube_{i+1}", (x, 0.08 * scale, 0.25 * scale), 0.078 * scale,
            1.10 * scale, DARK, pivot, collection, 10, (math.radians(67), 0, 0), 0.01)
        box(f"{prefix}Cap_{i+1}", (x, 0.57 * scale, 0.47 * scale),
            (0.14 * scale, 0.035 * scale, 0.14 * scale), RED, pivot, collection, 0.005)
        empty(f"{prefix}LaunchPoint_{i+1:02d}", pivot, collection,
              (x, 0.59 * scale, 0.48 * scale))
    return pivot


def mine_model(name, loc, parent, collection, scale=1.0):
    body = sphere(name + "Body", loc, (0.18 * scale, 0.18 * scale, 0.18 * scale), MINE, parent, collection, 12, 6)
    for i in range(6):
        a = math.radians(i * 60)
        cyl(f"{name}Horn_{i+1}", (loc[0] + math.cos(a) * 0.18 * scale,
                                  loc[1] + math.sin(a) * 0.18 * scale,
                                  loc[2] + 0.08 * scale), 0.018 * scale, 0.14 * scale,
            YELLOW, parent, collection, 6, (0, math.radians(42), -a), 0)
    return body


def create_asset(name, display, rank, length, width, builder):
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    root = empty(name + "_Root", None, col)
    root["display_name"] = display
    root["rank"] = rank
    root["length_m"] = length
    root["width_m"] = width
    root["forward_axis"] = "Blender +Y / Unity +Z"
    builder(root, col)
    ASSETS.append((name, display, rank, length, width, col, root))
    return root


def build_unmanned(root, col):
    hull("Hull", 5.5, 1.75, 0.80, root, col)
    frustum("Deck", (0, -0.05, 0.48), (1.46, 4.25), (1.10, 3.65), 0.12, DECK, root, col, 0.12)
    frustum("SensorCitadel", (0, 0.10, 0.92), (1.05, 1.32), (0.55, 0.70), 0.82, SUPER, root, col, 0.07)
    # no crew bridge: black sensor slit and camera eyes emphasize unmanned role
    box("SensorSlit", (0, 0.72, 1.05), (0.58, 0.035, 0.17), GLASS, root, col, 0.006)
    for x in (-0.27, 0.27):
        sphere(f"Optic_{x:+.2f}", (x, 0.73, 1.06), (0.07, 0.035, 0.07), SENSOR, root, col, 10, 5)
    gun_mount(root, col, (0, 1.72, 0.61), 0.62)
    mast_radar(root, col, (0, -0.06, 1.22), 0.58)
    for side in (-1, 1):
        pod = box(f"MissilePod_{side}", (side * 0.54, -1.05, 0.78), (0.38, 0.86, 0.32), DARK, root, col, 0.025)
        empty(f"MissileLaunchPoint_{1 if side < 0 else 2:02d}", pod, col, (0, 0.48, 0.08))
    box("AutonomyMark", (0, -1.72, 0.58), (0.62, 0.10, 0.035), RED, root, col, 0.004)
    empty("WakeOrigin", root, col, (0, -2.75, 0))


def build_minelayer(root, col):
    hull("Hull", 7.2, 2.15, 0.92, root, col)
    frustum("Deck", (0, -0.10, 0.55), (1.86, 5.65), (1.48, 5.10), 0.14, DECK, root, col, 0.20)
    bridge(root, col, 1.18, 1.42, 1.40, 0.62, 0.96)
    mast_radar(root, col, (0, 0.82, 1.42), 0.62)
    gun_mount(root, col, (0, 2.55, 0.68), 0.70)
    # broad working deck, twin rails and visible mines
    box("MineWorkingDeck", (0, -1.65, 0.65), (1.82, 2.60, 0.10), DECK, root, col, 0.018)
    for side in (-1, 1):
        box(f"MineRail_{side}", (side * 0.43, -1.70, 0.76), (0.06, 2.42, 0.08), YELLOW, root, col, 0.005)
        for i, y in enumerate((-0.85, -1.48, -2.11)):
            mine_model(f"Mine_{side}_{i}", (side * 0.43, y, 0.98), root, col, 0.88)
    box("SternMineChute", (0, -3.22, 0.55), (1.10, 0.60, 0.24), DARK, root, col, 0.02)
    empty("MineDropPoint_01", root, col, (-0.43, -3.62, 0.48))
    empty("MineDropPoint_02", root, col, (0.43, -3.62, 0.48))
    box("HazardStripe", (0, -2.92, 0.72), (1.44, 0.08, 0.035), RED, root, col, 0.004)


def build_ew_corvette(root, col):
    hull("Hull", 10.5, 2.75, 1.10, root, col)
    frustum("Deck", (0, -0.15, 0.66), (2.38, 8.10), (1.85, 7.42), 0.16, DECK, root, col, 0.30)
    bridge(root, col, 1.15, 1.95, 2.25, 0.75, 1.28)
    mast_radar(root, col, (0, 0.30, 2.00), 0.90, True)
    gun_mount(root, col, (0, 3.30, 0.78), 0.82)
    # large broadside jammer panels and domes dominate the silhouette
    for side in (-1, 1):
        panel_pivot = empty(f"AntennaPivot_{'Port' if side < 0 else 'Starboard'}", root, col,
                            (side * 1.10, -0.45, 1.74))
        box(f"WidebandJammer_{side}", (0, 0, 0), (0.10, 1.40, 0.86), SENSOR, panel_pivot, col, 0.015)
        sphere(f"ESMDome_{side}", (side * 0.77, -1.62, 1.44), (0.36, 0.36, 0.32), SENSOR, root, col, 14, 7)
        empty(f"EwEmitter_{1 if side < 0 else 2:02d}", panel_pivot, col, (0, 0, 0))
    antenna = empty("AntennaPivot", root, col, (0, 0.20, 3.45))
    sphere("TopEWReceiver", (0, 0, 0.12), (0.30, 0.30, 0.30), SENSOR, antenna, col, 14, 7)
    box("InterferometerA", (0, 0, 0.38), (1.30, 0.065, 0.065), RED, antenna, col, 0.006)
    box("InterferometerB", (0, 0, 0.38), (0.065, 1.30, 0.065), RED, antenna, col, 0.006)
    for y in (-2.30, -3.05):
        sphere(f"JammerDome_{y}", (0, y, 1.00), (0.42, 0.42, 0.36), SENSOR, root, col, 14, 7)
    ciws(root, col, (0, -4.00, 0.78), "RearCIWS", 0.78)


def build_aaw_frigate(root, col):
    hull("Hull", 12.5, 3.15, 1.22, root, col)
    frustum("Deck", (0, -0.15, 0.73), (2.72, 9.70), (2.05, 8.80), 0.18, DECK, root, col, 0.38)
    bridge(root, col, 1.25, 2.18, 2.60, 0.82, 1.45)
    mast_radar(root, col, (0, 0.22, 2.23), 1.12, True)
    gun_mount(root, col, (0, 4.10, 0.86), 1.00)
    vls_bank(root, col, (0, 2.95, 0.84), 4, 4, 0.27)
    ciws(root, col, (0, -4.68, 0.87), "RearCIWS", 0.92)
    for side in (-1, 1):
        box(f"DataLinkArray_{side}", (side * 1.12, -0.65, 2.08), (0.10, 0.82, 0.64), SENSOR, root, col, 0.015)
        quad_launcher(root, col, (side * 0.78, -2.05, 0.82), f"SAMBank_{'P' if side < 0 else 'S'}", 0, 0.72)
    sphere("SATCOM", (0.62, -1.08, 2.38), (0.27, 0.27, 0.27), SENSOR, root, col, 14, 7)
    empty("AirDefenseOrigin", root, col, (0, 0.22, 3.80))


def build_attack_sub(root, col):
    # Teardrop body; origin is waterline center, matching existing submarine handling.
    body = sphere("PressureHull", (0, -0.10, -0.32), (1.16, 4.75, 0.90), SUB, root, col, 24, 12)
    sphere("DarkUnderside", (0, -0.10, -0.67), (1.08, 4.48, 0.56), SUB_DARK, root, col, 20, 10)
    frustum("Sail", (0, 0.25, 0.64), (0.72, 1.32), (0.40, 0.82), 1.15, SUB, root, col, 0.12)
    box("SailWindow", (0, 0.76, 0.84), (0.36, 0.035, 0.14), GLASS, root, col, 0.006)
    periscope = empty("PeriscopePivot", root, col, (0, 0.22, 1.18))
    cyl("Periscope", (0, 0, 0.46), 0.035, 0.92, DARK, periscope, col, 8)
    box("PeriscopeHead", (0, 0.08, 0.91), (0.08, 0.20, 0.07), SENSOR, periscope, col, 0.006)
    # dive planes, tail fins and pumpjet ring
    box("BowPlane", (0, 2.48, -0.12), (2.15, 0.32, 0.10), SUB, root, col, 0.02)
    box("SternPlane", (0, -3.55, -0.22), (2.30, 0.40, 0.11), SUB, root, col, 0.02)
    box("TailFin", (0, -3.68, 0.26), (0.12, 0.58, 1.45), SUB, root, col, 0.02)
    cyl("PumpjetRing", (0, -4.72, -0.30), 0.48, 0.28, DARK, root, col, 16, (math.radians(90), 0, 0), 0.025)
    for x in (-0.40, 0.40):
        empty(f"TorpedoLaunchPoint_{1 if x < 0 else 2:02d}", root, col, (x, 4.45, -0.34))
        cyl(f"TorpedoTube_{x}", (x, 4.18, -0.34), 0.10, 0.42, DARK, root, col, 10,
            (math.radians(90), 0, 0), 0.008)
    for side in (-1, 1):
        box(f"FlankSonar_{side}", (side * 1.05, 0.05, -0.18), (0.035, 3.25, 0.25), SENSOR, root, col, 0.006)


def build_boss(root, col):
    hull("Hull", 24.0, 5.15, 1.72, root, col, HULL, 0.025, 0.88)
    frustum("MainDeck", (0, -0.25, 1.02), (4.50, 19.10), (3.20, 17.20), 0.24, DECK, root, col, 0.85)
    bridge(root, col, 2.15, 3.50, 4.25, 1.20, 2.25)
    # broad command deck and integrated mast
    frustum("CommandDeck", (0, 1.70, 3.25), (3.25, 2.70), (2.45, 2.10), 0.72, SUPER, root, col, 0.15)
    mast_radar(root, col, (0, 0.65, 4.20), 1.72, True)
    gun_mount(root, col, (0, 8.18, 1.20), 1.48)

    # forward and aft VLS fields; sixteen visible hatches each
    vls_bank(root, col, (0, 6.20, 1.13), 4, 4, 0.38, "ForwardLaunchPoint")
    vls_bank(root, col, (0, -4.10, 1.10), 4, 4, 0.38, "AftLaunchPoint")

    for side in (-1, 1):
        quad_launcher(root, col, (side * 1.48, -1.90, 1.18),
                      f"AntiShipBank_{'P' if side < 0 else 'S'}", 0, 1.10)
        sphere(f"EwDome_{side}", (side * 1.20, 0.35, 3.50), (0.44, 0.44, 0.40), SENSOR, root, col, 16, 8)
        box(f"AesaWing_{side}", (side * 1.80, 1.05, 3.00), (0.10, 1.20, 0.86), SENSOR, root, col, 0.02)
    ciws(root, col, (0, 5.05, 1.22), "ForwardCIWS", 1.08)
    ciws(root, col, (0, -8.75, 1.18), "RearCIWS", 1.08)

    # stern hangar and helicopter deck
    frustum("HelicopterHangar", (0, -6.30, 1.95), (3.25, 2.70), (2.65, 2.25), 1.55, SUPER, root, col, -0.08)
    box("HangarDoor", (0, -7.68, 1.88), (2.25, 0.045, 0.98), DARK, root, col, 0.008)
    box("FlightDeck", (0, -9.55, 1.12), (4.15, 3.65, 0.16), DECK, root, col, 0.035)
    box("HelipadH1", (0, -9.55, 1.22), (1.65, 0.13, 0.025), HELI, root, col, 0.003)
    box("HelipadH2", (0, -9.55, 1.22), (0.13, 1.65, 0.025), HELI, root, col, 0.003)
    empty("HelicopterLaunch", root, col, (0, -9.55, 1.36))

    # Compatibility sockets for a large salvo boss.
    for i in range(8):
        name = "MissileTube" if i == 0 else f"MissileTube.{i:03d}"
        x = (i % 4 - 1.5) * 0.38
        y = 6.02 + (i // 4 - 0.5) * 0.38
        empty(name, root, col, (x, y, 1.35))
    empty("AAMount", root, col, (0, -8.75, 1.65))
    empty("Weakpoint_Radar", root, col, (0, 0.65, 6.15), 0.36)
    empty("Weakpoint_ForwardVLS", root, col, (0, 6.20, 1.25), 0.36)
    empty("Weakpoint_AftVLS", root, col, (0, -4.10, 1.25), 0.36)
    empty("Weakpoint_C2", root, col, (0, 2.35, 3.55), 0.36)
    empty("DamageFx_75", root, col, (0.75, 1.60, 4.70), 0.35)
    empty("DamageFx_50", root, col, (-0.85, -4.10, 1.70), 0.35)
    empty("DamageFx_25", root, col, (0, 0.65, 5.85), 0.35)
    box("CommandStripePort", (-2.29, 0.15, 0.55), (0.045, 9.50, 0.28), RED, root, col, 0.005)
    box("CommandStripeStarboard", (2.29, 0.15, 0.55), (0.045, 9.50, 0.28), RED, root, col, 0.005)


def descendants(root):
    result = [root]
    stack = list(root.children)
    while stack:
        o = stack.pop()
        result.append(o)
        stack.extend(list(o.children))
    return result


def export_root(root, path):
    # Move every named empty out of the global namespace, then give the current
    # asset's sockets their exact contract names. This keeps each standalone
    # FBX compatible with exact-name Unity lookups such as RadarPivot.
    named_empties = [o for o in bpy.data.objects if o.type == 'EMPTY' and "export_name" in o]
    for i, o in enumerate(named_empties):
        o.name = f"__PendingSocket_{i:04d}"
    for o in descendants(root):
        if o.type == 'EMPTY' and "export_name" in o:
            o.name = o["export_name"]
    bpy.ops.object.select_all(action='DESELECT')
    for o in descendants(root):
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'EMPTY', 'MESH'},
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_NONE', use_space_transform=True,
                             bake_space_transform=False, add_leaf_bones=False, path_mode='COPY')
    bpy.ops.object.select_all(action='DESELECT')


def duplicate_tree(source, collection, suffix="_Preview"):
    def rec(src, parent=None):
        dup = src.copy()
        if src.data:
            dup.data = src.data
        dup.name = src.name + suffix
        collection.objects.link(dup)
        dup.parent = parent
        for ch in src.children:
            rec(ch, dup)
        return dup
    return rec(source)


def text(body, loc, size, collection, color=(0.62, 0.84, 0.80, 1), align='CENTER'):
    c = bpy.data.curves.new("PreviewText", 'FONT')
    c.body = body
    c.align_x = align
    c.align_y = 'CENTER'
    c.size = size
    c.extrude = 0.008
    o = bpy.data.objects.new("Label_" + body, c)
    o.location = loc
    o.data.materials.append(material("Preview Label", color, 0, 0.55))
    collection.objects.link(o)
    return o


def stage(collection, width, length, spacing=2.0):
    box("Stage", (0, 0, -1.08), (width, length, 0.18),
        material("Preview Ocean", (0.008, 0.035, 0.052, 1), 0.25, 0.35), None, collection, 0)
    grid = material("Preview Grid", (0.025, 0.23, 0.27, 1), 0, 0.50)
    for x in range(math.floor(-width / 2), math.ceil(width / 2) + 1, int(spacing)):
        box(f"GridX{x}", (x, 0, -0.98), (0.025, length, 0.012), grid, None, collection, 0)
    for y in range(math.floor(-length / 2), math.ceil(length / 2) + 1, int(spacing)):
        box(f"GridY{y}", (0, y, -0.98), (width, 0.025, 0.012), grid, None, collection, 0)


def look_at(obj, target=(0, 0, 0)):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()


def setup_render():
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_percentage = 80
    scene.render.image_settings.file_format = 'PNG'
    sh = scene.display.shading
    sh.light = 'STUDIO'
    sh.color_type = 'MATERIAL'
    sh.show_shadows = True
    sh.show_cavity = True
    sh.cavity_type = 'WORLD'
    sh.curvature_ridge_factor = 1.4
    sh.curvature_valley_factor = 1.0
    sh.show_specular_highlight = True
    sh.background_type = 'VIEWPORT'
    sh.background_color = (0.004, 0.012, 0.020)
    return scene


def new_preview(name):
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    return c


def render_overview(scene):
    col = new_preview("Preview_Overview")
    stage(col, 48, 39, 2)
    roots = {name: root for name, _, _, _, _, _, root in ASSETS}
    spots = {
        "EnemyUnmannedAttackCraft": (-14, 9, 1.05),
        "EnemyMineLayer": (-4.5, 9, 1.0),
        "EnemyAttackSubmarine": (7.5, 9, 1.0),
        "EnemyEwCorvette": (-12, -6.5, 1.0),
        "EnemyAirDefenseFrigate": (0, -6.5, 1.0),
        "EnemyCommandCruiserBoss": (13, -4.5, 0.70),
    }
    labels = {
        "EnemyUnmannedAttackCraft": "NORMAL / UNMANNED ATTACK CRAFT",
        "EnemyMineLayer": "NORMAL / MINE LAYER",
        "EnemyAttackSubmarine": "ELITE / ATTACK SUBMARINE",
        "EnemyEwCorvette": "ELITE / EW CORVETTE",
        "EnemyAirDefenseFrigate": "ELITE / AIR DEFENSE FRIGATE",
        "EnemyCommandCruiserBoss": "BOSS / COMMAND CRUISER",
    }
    for name, (x, y, scale) in spots.items():
        d = duplicate_tree(roots[name], col)
        d.location = (x, y, 0)
        d.scale = (scale,) * 3
        text(labels[name], (x, y - (4.6 if name != "EnemyCommandCruiserBoss" else 10.5), 0.2),
             0.48, col, (0.85, 0.18, 0.13, 1) if "BOSS" in labels[name] else (0.60, 0.84, 0.80, 1))
    text("STAGE 4 / NETWORKED A2AD FLEET", (0, 17.8, 0.2), 0.82, col)
    bpy.ops.object.camera_add(location=(31, -37, 38))
    cam = bpy.context.object
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = 52
    look_at(cam, (0, 0, 0))
    relink(cam, col)
    scene.camera = cam
    scene.render.resolution_x = 2048
    scene.render.resolution_y = 1450
    scene.render.filepath = os.path.join(PREVIEW_DIR, "Stage4EnemyFleet_Overview.png")
    bpy.ops.render.render(write_still=True)
    col.hide_render = True


def render_asset_sheets(scene):
    for name, display, rank, length, width, _, root in ASSETS:
        col = new_preview("Preview_" + name)
        stage(col, max(24, length * 3.4), max(18, length * 1.9), 2)
        scale = min(1.35, 11.0 / length)
        for i, (x, yaw, label) in enumerate(((-8.0, -32, "PORT 3/4"), (0, 0, "TOP/FRONT"), (8.0, 32, "STARBOARD 3/4"))):
            d = duplicate_tree(root, col, f"_Preview_{i}")
            d.location = (x, 0, 0)
            d.rotation_euler[2] = math.radians(yaw)
            d.scale = (scale,) * 3
            text(label, (x, -length * scale * 0.62, 0.15), 0.42, col)
        color = (0.88, 0.14, 0.10, 1) if rank == "Boss" else ((1.0, 0.56, 0.08, 1) if rank == "Elite" else (0.62, 0.84, 0.80, 1))
        text(f"{rank.upper()} / {display.upper()} / {length:.1f}m", (0, length * scale * 0.72, 0.15), 0.72, col, color)
        bpy.ops.object.camera_add(location=(0, -max(22, length * 2.1), max(18, length * 1.7)))
        cam = bpy.context.object
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = max(27, length * scale * 2.0)
        look_at(cam, (0, 0, 0.3))
        relink(cam, col)
        scene.camera = cam
        scene.render.resolution_x = 1800
        scene.render.resolution_y = 1050
        scene.render.filepath = os.path.join(PREVIEW_DIR, name + ".png")
        bpy.ops.render.render(write_still=True)
        col.hide_render = True


def tris(root):
    total = 0
    deps = bpy.context.evaluated_depsgraph_get()
    for o in descendants(root):
        if o.type != 'MESH':
            continue
        ev = o.evaluated_get(deps)
        me = ev.to_mesh()
        me.calc_loop_triangles()
        total += len(me.loop_triangles)
        ev.to_mesh_clear()
    return total


def main():
    clear_scene()
    setup_materials()
    scene = setup_render()
    create_asset("EnemyUnmannedAttackCraft", "Unmanned Attack Craft", "Normal", 5.5, 1.75, build_unmanned)
    create_asset("EnemyMineLayer", "Mine Layer", "Normal", 7.2, 2.15, build_minelayer)
    create_asset("EnemyEwCorvette", "Electronic Warfare Corvette", "Elite", 10.5, 2.75, build_ew_corvette)
    create_asset("EnemyAirDefenseFrigate", "Air Defense Frigate", "Elite", 12.5, 3.15, build_aaw_frigate)
    create_asset("EnemyAttackSubmarine", "Attack Submarine", "Elite", 9.5, 2.32, build_attack_sub)
    create_asset("EnemyCommandCruiserBoss", "Integrated Air Defense Command Cruiser", "Boss", 24.0, 5.15, build_boss)

    manifest = {
        "status": "ArtSource review draft only; not copied to Assets and not linked to gameplay",
        "orientation": "Blender +Y forward, +Z up; FBX -Z Forward / Y Up / All Local",
        "assets": [],
    }
    for name, display, rank, length, width, col, root in ASSETS:
        path = os.path.join(MODEL_DIR, name + ".fbx")
        export_root(root, path)
        manifest["assets"].append({
            "file": os.path.relpath(path, OUT), "display": display, "rank": rank,
            "lengthM": length, "widthM": width, "triangles": tris(root), "root": root.name,
        })
        col.hide_render = True

    render_overview(scene)
    render_asset_sheets(scene)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "Stage4EnemyFleet.blend"))
    with open(os.path.join(OUT, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
    print(json.dumps({"models": len(ASSETS), "previews": len(ASSETS) + 1}, ensure_ascii=False))


if __name__ == "__main__":
    main()
