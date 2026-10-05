import bpy
import math
import os
import json
from mathutils import Vector


OUT = r"C:\Users\최병욱\UnityProjects\NavalRoguelike\NavalRoguelike\ArtSource\TaskForceTacticalDraft"
MODEL_DIR = os.path.join(OUT, "Models")
FX_DIR = os.path.join(OUT, "Effects")
PREVIEW_DIR = os.path.join(OUT, "Preview")
for folder in (OUT, MODEL_DIR, FX_DIR, PREVIEW_DIR):
    os.makedirs(folder, exist_ok=True)


ROLE_DATA = {
    "CAP": {"name": "Air Defense Escort", "color": (0.35, 1.00, 0.58, 1.0)},
    "ASW": {"name": "Anti Submarine Escort", "color": (0.22, 0.68, 1.00, 1.0)},
    "EW":  {"name": "Electronic Warfare Escort", "color": (0.83, 0.37, 1.00, 1.0)},
    "STK": {"name": "Surface Strike Escort", "color": (1.00, 0.52, 0.16, 1.0)},
}

FX_DATA = {
    "AEW": ((0.20, 0.95, 1.00, 1.0), "Airborne Early Warning"),
    "CAP": ((0.42, 1.00, 0.62, 1.0), "Combat Air Patrol"),
    "ASW": ((0.25, 0.72, 1.00, 1.0), "Anti Submarine Search"),
    "EW":  ((0.87, 0.42, 1.00, 1.0), "Electronic Attack"),
    "STK": ((1.00, 0.58, 0.18, 1.0), "Surface Strike"),
}

TIER_SCALE = [0.60, 0.76, 0.92, 1.08]
ASSETS = []
FX_ASSETS = []


def clean_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        pass


def mat(name, color, metallic=0.0, roughness=0.45, emission=0.0, alpha=1.0):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.diffuse_color = (color[0], color[1], color[2], alpha)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], alpha)
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Alpha"].default_value = alpha
        if emission > 0:
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = (color[0], color[1], color[2], 1.0)
                bsdf.inputs["Emission Strength"].default_value = emission
            elif "Emission" in bsdf.inputs:
                bsdf.inputs["Emission"].default_value = (color[0], color[1], color[2], 1.0)
                bsdf.inputs["Emission Strength"].default_value = emission
    if alpha < 1.0:
        try:
            m.surface_render_method = 'DITHERED'
        except Exception:
            try:
                m.blend_method = 'BLEND'
            except Exception:
                pass
        m.use_transparency_overlap = False if hasattr(m, "use_transparency_overlap") else False
    return m


M_HULL = None
M_DECK = None
M_NAVAL = None
M_DARK = None
M_GLASS = None
M_SENSOR = None
M_WARNING = None
M_WHITE = None


def setup_materials():
    global M_HULL, M_DECK, M_NAVAL, M_DARK, M_GLASS, M_SENSOR, M_WARNING, M_WHITE
    M_HULL = mat("Naval Blue Grey", (0.20, 0.29, 0.31, 1), 0.42, 0.34)
    M_DECK = mat("Deck Grey", (0.11, 0.16, 0.17, 1), 0.35, 0.38)
    M_NAVAL = mat("Naval Superstructure", (0.34, 0.43, 0.44, 1), 0.28, 0.38)
    M_DARK = mat("Gunmetal", (0.035, 0.055, 0.06, 1), 0.72, 0.27)
    M_GLASS = mat("Radar Glass", (0.025, 0.16, 0.19, 1), 0.46, 0.20)
    M_SENSOR = mat("Sensor Glass", (0.09, 0.42, 0.46, 1), 0.35, 0.25)
    M_WARNING = mat("Warning Yellow", (1.0, 0.58, 0.05, 1), 0.15, 0.42)
    M_WHITE = mat("Deck Marking White", (0.82, 0.89, 0.86, 1), 0.05, 0.48)
    for code, data in ROLE_DATA.items():
        mat(f"Role {code}", data["color"], 0.15, 0.28, 1.2)
    for code, data in FX_DATA.items():
        mat(f"FX {code}", data[0], 0.05, 0.22, 4.0, 0.82)
        c = data[0]
        mat(f"FX {code} Faint", c, 0.0, 0.35, 2.0, 0.25)


def link_only(obj, collection):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    collection.objects.link(obj)


def empty(name, parent=None, collection=None, location=(0, 0, 0)):
    o = bpy.data.objects.new(name, None)
    o.empty_display_type = 'PLAIN_AXES'
    o.empty_display_size = 0.25
    o.location = location
    if collection:
        collection.objects.link(o)
    else:
        bpy.context.collection.objects.link(o)
    if parent:
        o.parent = parent
    return o


def apply_xform(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.select_set(False)


def box(name, loc, scale, material, parent, collection, bevel=0.04, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.scale = (scale[0] / 2, scale[1] / 2, scale[2] / 2)
    if material:
        o.data.materials.append(material)
    if bevel > 0:
        mod = o.modifiers.new("Edge softening", 'BEVEL')
        mod.width = bevel
        mod.segments = 1
    apply_xform(o)
    o.parent = parent
    link_only(o, collection)
    return o


def cylinder(name, loc, radius, depth, material, parent, collection, vertices=12, rot=(0, 0, 0), bevel=0.025):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    if material:
        o.data.materials.append(material)
    if bevel > 0:
        mod = o.modifiers.new("Edge softening", 'BEVEL')
        mod.width = bevel
        mod.segments = 1
    apply_xform(o)
    o.parent = parent
    link_only(o, collection)
    return o


def sphere(name, loc, scale, material, parent, collection, segments=16, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=loc)
    o = bpy.context.object
    o.name = name
    o.scale = scale
    if material:
        o.data.materials.append(material)
    apply_xform(o)
    o.parent = parent
    link_only(o, collection)
    return o


def cone(name, loc, r1, r2, depth, material, parent, collection, vertices=12, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r1, radius2=r2, depth=depth, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    if material:
        o.data.materials.append(material)
    apply_xform(o)
    o.parent = parent
    link_only(o, collection)
    return o


def frustum(name, loc, lower, upper, height, material, parent, collection, y_shift=0.0):
    lx, ly = lower[0] / 2, lower[1] / 2
    ux, uy = upper[0] / 2, upper[1] / 2
    z0, z1 = -height / 2, height / 2
    verts = [
        (-lx, -ly, z0), (lx, -ly, z0), (lx, ly, z0), (-lx, ly, z0),
        (-ux, -uy + y_shift, z1), (ux, -uy + y_shift, z1),
        (ux, uy + y_shift, z1), (-ux, uy + y_shift, z1),
    ]
    faces = [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    o = bpy.data.objects.new(name, mesh)
    o.location = loc
    o.data.materials.append(material)
    collection.objects.link(o)
    o.parent = parent
    bevel_mod = o.modifiers.new("Edge softening", 'BEVEL')
    bevel_mod.width = 0.035
    bevel_mod.segments = 1
    return o


def hull_mesh(name, parent, collection):
    # Nominal 3 x 9 m hull. Current runtime tier scale (0.60..1.08) remains authoritative.
    sections = [
        (-4.5, 1.22, 0.44, -0.58),
        (-3.8, 1.48, 0.48, -0.62),
        (-1.8, 1.52, 0.50, -0.72),
        ( 0.8, 1.46, 0.56, -0.80),
        ( 2.7, 1.02, 0.70, -0.66),
        ( 4.5, 0.10, 0.48, -0.30),
    ]
    verts = []
    for y, w, top, bot in sections:
        verts += [(-w, y, top), (w, y, top), (-w * 0.68, y, bot), (w * 0.68, y, bot)]
    faces = []
    n = len(sections)
    for i in range(n - 1):
        a, b = i * 4, (i + 1) * 4
        faces += [
            (a, b, b + 1, a + 1),
            (a + 2, a + 3, b + 3, b + 2),
            (a, a + 2, b + 2, b),
            (a + 1, b + 1, b + 3, a + 3),
        ]
    faces += [(0, 1, 3, 2), ((n - 1) * 4, (n - 1) * 4 + 2, (n - 1) * 4 + 3, (n - 1) * 4 + 1)]
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    o = bpy.data.objects.new(name, mesh)
    collection.objects.link(o)
    o.parent = parent
    o.data.materials.append(M_HULL)
    bevel_mod = o.modifiers.new("Hull edge softening", 'BEVEL')
    bevel_mod.width = 0.055
    bevel_mod.segments = 1
    return o


def barrel(name, start, length, radius, parent, collection, material=None, angle_x=90):
    # Cylinder local Z is rotated into +Y.
    loc = (start[0], start[1] + length / 2, start[2])
    return cylinder(name, loc, radius, length, material or M_DARK, parent, collection,
                    vertices=8, rot=(math.radians(angle_x), 0, 0), bevel=0.008)


def add_common_ship(root, collection, tier, role):
    role_mat = bpy.data.materials[f"Role {role}"]
    hull_mesh("Hull", root, collection)
    # deck follows the hull but stays deliberately clean at gameplay distance
    frustum("MainDeck", (0, -0.1, 0.54), (2.56, 7.25), (1.85, 6.80), 0.14, M_DECK, root, collection, 0.20)
    frustum("BridgeBlock", (0, 0.05, 1.12), (1.72, 2.15), (1.34, 1.72), 1.16, M_NAVAL, root, collection, 0.16)
    frustum("BridgeCrown", (0, 0.45, 1.78), (1.40, 0.94), (1.06, 0.72), 0.34, M_NAVAL, root, collection, 0.08)
    # faceted bridge windows
    box("BridgeWindowFront", (0, 0.93, 1.80), (1.04, 0.045, 0.18), M_GLASS, root, collection, 0.01)
    box("BridgeWindowPort", (-0.69, 0.51, 1.78), (0.045, 0.58, 0.17), M_GLASS, root, collection, 0.01)
    box("BridgeWindowStarboard", (0.69, 0.51, 1.78), (0.045, 0.58, 0.17), M_GLASS, root, collection, 0.01)
    # role identification strips remain the exact runtime colors
    box("RoleStripePort", (-1.31, -0.25, 0.40), (0.045, 5.10, 0.22), role_mat, root, collection, 0.006)
    box("RoleStripeStarboard", (1.31, -0.25, 0.40), (0.045, 5.10, 0.22), role_mat, root, collection, 0.006)
    box("RolePlateBow", (0, 3.20, 0.66), (0.88, 0.08, 0.10), role_mat, root, collection, 0.006)
    box("RoleDeckChevron", (0, 1.42, 0.69), (1.08, 0.18, 0.035), role_mat, root, collection, 0.006)

    # forward gun; fixed base and named train/elevation pivots for future animation
    gun_base = cylinder("ForwardGunBase", (0, 2.72, 0.74), 0.34, 0.20, M_DECK, root, collection, 12)
    gun_train = empty("PrimaryGunPivot", gun_base, collection, (0, 0, 0.11))
    gun_house = frustum("PrimaryGunHouse", (0, 0.02, 0.20), (0.58, 0.66), (0.42, 0.48), 0.38, M_NAVAL, gun_train, collection, 0.06)
    gun_elev = empty("PrimaryGunElevation", gun_train, collection, (0, 0.26, 0.26))
    barrel("PrimaryGunBarrel", (0, 0.0, 0), 0.86, 0.045, gun_elev, collection)
    empty("PrimaryGunMuzzle", gun_elev, collection, (0, 0.86, 0))

    # mast structure
    cylinder("MastLower", (0, -0.08, 2.35), 0.085, 1.15, M_DARK, root, collection, 8)
    for x in (-0.30, 0.30):
        cylinder(f"MastBrace_{x:+.2f}", (x, -0.06, 2.18), 0.028, 0.88, M_DARK, root, collection, 6,
                 rot=(0, math.radians(x * 30), math.radians(-x * 34)), bevel=0)
    box("MastYard", (0, -0.05, 2.56), (1.10, 0.08, 0.07), M_DARK, root, collection, 0.01)
    radar_pivot = empty("RadarPivot", root, collection, (0, -0.05, 2.93))
    box("NavigationRadar", (0, 0, 0), (0.88, 0.08, 0.15), M_SENSOR, radar_pivot, collection, 0.01)
    empty("SupportOrigin", root, collection, (0, 0.0, 3.05))

    # Tactical silhouette progression shared by all escorts.
    if tier >= 1:
        box("Tier1_BowSensor", (0, 2.04, 0.76), (0.56, 0.50, 0.18), role_mat, root, collection, 0.025)
        cylinder("Tier1_SatDome", (-0.46, -0.48, 2.06), 0.19, 0.26, M_SENSOR, root, collection, 12)
    if tier >= 2:
        for side in (-1, 1):
            box(f"Tier2_Rail_{side}", (side * 1.20, -2.34, 0.83), (0.05, 2.1, 0.16), M_NAVAL, root, collection, 0.01)
        box("Tier2_MastPlatform", (0, -0.05, 2.72), (1.28, 0.60, 0.10), M_DECK, root, collection, 0.015)
    if tier >= 3:
        # rear point defence mount makes the top-tier silhouette read as a frigate
        ciws_base = cylinder("Tier3_CIWSBase", (0, -3.05, 0.78), 0.30, 0.18, M_DECK, root, collection, 12)
        ciws_pivot = empty("Tier3_CIWSPivot", ciws_base, collection, (0, 0, 0.10))
        sphere("Tier3_CIWSRadar", (0, -0.05, 0.31), (0.20, 0.20, 0.24), M_SENSOR, ciws_pivot, collection, 12, 6)
        barrel("Tier3_CIWSBarrel", (0, 0.08, 0.22), 0.50, 0.035, ciws_pivot, collection)


def add_vls_cells(root, collection, count, center=(0, 2.0, 0.73), role_mat=None):
    cols = 4
    rows = int(math.ceil(count / cols))
    spacing = 0.22
    for i in range(count):
        r, c = divmod(i, cols)
        x = (c - (cols - 1) / 2) * spacing
        y = (r - (rows - 1) / 2) * spacing
        box(f"VLSCell_{i+1:02d}", (center[0] + x, center[1] + y, center[2]),
            (0.17, 0.17, 0.045), M_DARK, root, collection, 0.008)
    if role_mat:
        box("VLSRoleOutline", (center[0], center[1], center[2] - 0.02),
            (0.98, max(0.44, rows * spacing + 0.12), 0.035), role_mat, root, collection, 0.01)


def add_cap(root, collection, tier):
    role_mat = bpy.data.materials["Role CAP"]
    add_vls_cells(root, collection, 4 + tier * 4, (0, 1.78, 0.75), role_mat)
    # integrated air-search mast grows from rotating 2D set to four fixed AESA faces
    if tier <= 1:
        pivot = empty("AirSearchRadarPivot", root, collection, (0, -0.07, 3.19))
        box("AirSearchRadar", (0, 0, 0), (1.18 + tier * 0.18, 0.10, 0.30), role_mat, pivot, collection, 0.02)
        box("IFFBar", (0, 0.02, 0.27), (0.72, 0.07, 0.07), M_SENSOR, pivot, collection, 0.01)
    else:
        mast = frustum("IntegratedAesaMast", (0, -0.07, 2.98), (0.82, 0.82), (0.46, 0.46), 1.20, M_NAVAL, root, collection)
        for idx, (x, y, rz) in enumerate(((0, 0.42, 0), (0, -0.42, 0), (0.42, 0, 90), (-0.42, 0, 90))):
            box(f"AESAFace_{idx+1}", (x, y, 0.12), (0.46, 0.025, 0.40), role_mat, mast, collection, 0.008,
                rot=(0, 0, math.radians(rz)))
    if tier >= 1:
        # compact trainable SAM rails at stern
        base = cylinder("SAMBase", (0, -2.48, 0.78), 0.30, 0.18, M_DECK, root, collection, 12)
        pivot = empty("SAM_TrainPivot", base, collection, (0, 0, 0.13))
        for i, x in enumerate((-0.18, 0.18)):
            cylinder(f"SAMTube_{i+1}", (x, 0.18, 0.18), 0.075, 0.76, M_DARK, pivot, collection, 8,
                     rot=(math.radians(67), 0, 0), bevel=0.01)
            empty(f"SAMLaunchPoint_{i+1:02d}", pivot, collection, (x, 0.55, 0.34))


def add_helipad_mark(root, collection, z=0.75):
    role_mat = bpy.data.materials["Role ASW"]
    box("FlightDeck", (0, -2.80, z - 0.05), (2.30, 2.55, 0.13), M_DECK, root, collection, 0.03)
    box("LandingStripeA", (0, -2.80, z + 0.035), (1.16, 0.09, 0.025), role_mat, root, collection, 0.006)
    box("LandingStripeB", (0, -2.80, z + 0.035), (0.09, 1.16, 0.025), role_mat, root, collection, 0.006)
    empty("HelicopterLaunch", root, collection, (0, -2.80, z + 0.12))


def add_asw(root, collection, tier):
    role_mat = bpy.data.materials["Role ASW"]
    # sonar bow bulb is deliberately visible below waterline at oblique views
    sphere("BowSonarDome", (0, 3.52, -0.24), (0.56, 0.78, 0.45), role_mat, root, collection, 16, 8)
    add_helipad_mark(root, collection)
    frustum("HelicopterHangar", (0, -1.42, 1.13), (1.82, 1.30), (1.55, 1.08), 0.90, M_NAVAL, root, collection, -0.08)
    box("HangarDoor", (0, -2.09, 1.13), (1.28, 0.045, 0.56), M_DARK, root, collection, 0.01)
    # paired lightweight torpedo tubes
    for side in (-1, 1):
        tube_pivot = empty(f"TorpedoMount_{'P' if side < 0 else 'S'}", root, collection, (side * 0.88, -0.52, 0.91))
        for j in (-0.10, 0.10):
            cylinder(f"TorpedoTube_{side}_{j}", (0, j, 0), 0.085, 0.78, M_DARK, tube_pivot, collection, 10,
                     rot=(0, math.radians(90), math.radians(side * 18)), bevel=0.01)
        empty(f"TorpedoLaunchPoint_{1 if side < 0 else 2:02d}", tube_pivot, collection, (side * 0.34, 0, 0))
    # towed-array drum and stern fairlead
    cylinder("TowedArrayDrum", (0, -3.58, 0.93), 0.28 + tier * 0.03, 0.52, M_DARK, root, collection, 12,
             rot=(0, math.radians(90), 0))
    empty("SonarOrigin", root, collection, (0, -4.12, 0.70))
    if tier >= 1:
        for x in (-0.62, 0.62):
            cylinder(f"SonobuoyRack_{x:+.2f}", (x, -2.65, 0.94), 0.075, 0.58, role_mat, root, collection, 8,
                     rot=(math.radians(90), 0, 0), bevel=0.01)
    if tier >= 2:
        sphere("DippingSonarDome", (0.53, -0.58, 2.26), (0.23, 0.23, 0.23), M_SENSOR, root, collection, 12, 6)
        box("AcousticProcessor", (-0.50, -0.58, 2.21), (0.44, 0.42, 0.30), role_mat, root, collection, 0.03)
    if tier >= 3:
        # clear blue flight operations rail gives the final tier a broad ASW deck silhouette
        for x in (-1.04, 1.04):
            box(f"FlightOpsRail_{x:+.2f}", (x, -2.82, 0.86), (0.035, 2.10, 0.17), role_mat, root, collection, 0.006)


def add_ew(root, collection, tier):
    role_mat = bpy.data.materials["Role EW"]
    # wide side arrays make the role readable from the quarter-view camera
    for side in (-1, 1):
        mast = empty(f"EWArrayPivot_{'P' if side < 0 else 'S'}", root, collection, (side * 0.72, -0.08, 2.35))
        box(f"EWPanel_{'P' if side < 0 else 'S'}", (0, 0, 0), (0.08, 0.72, 0.72), role_mat, mast, collection, 0.02)
        sphere(f"ESMDome_{'P' if side < 0 else 'S'}", (side * 0.58, -0.78, 2.08), (0.25, 0.25, 0.25), M_SENSOR, root, collection, 12, 6)
    antenna = empty("AntennaPivot", root, collection, (0, -0.05, 3.20))
    cylinder("EWTopDome", (0, 0, 0.12), 0.28 + tier * 0.03, 0.40, role_mat, antenna, collection, 16)
    box("EWTopCrossA", (0, 0, 0.34), (1.02, 0.06, 0.06), M_SENSOR, antenna, collection, 0.01)
    box("EWTopCrossB", (0, 0, 0.34), (0.06, 1.02, 0.06), M_SENSOR, antenna, collection, 0.01)
    empty("EWOrigin", antenna, collection, (0, 0, 0.56))
    if tier >= 1:
        for y in (-1.55, 1.55):
            sphere(f"JammerDome_{y:+.2f}", (0, y, 1.04), (0.34, 0.34, 0.30), role_mat, root, collection, 14, 7)
    if tier >= 2:
        for x in (-1.02, 1.02):
            box(f"WidebandArray_{x:+.2f}", (x, -1.05, 1.18), (0.10, 0.88, 0.62), role_mat, root, collection, 0.02)
    if tier >= 3:
        # four directional interferometer horns
        for i in range(4):
            a = math.radians(i * 90)
            x, y = math.cos(a) * 0.46, math.sin(a) * 0.46 - 0.05
            cone(f"DirectionFinder_{i+1}", (x, y, 3.58), 0.16, 0.05, 0.34, role_mat, root, collection, 8,
                 rot=(0, math.radians(90), a))


def missile_canister(name, loc, parent, collection, side=1, role_mat=None):
    # Four-round inclined launcher; fixed pedestal stays outside the named rotation pivot.
    base = box(name + "Base", loc, (0.84, 1.04, 0.18), M_DECK, parent, collection, 0.025)
    pivot = empty(name + "Pivot", base, collection, (0, 0, 0.16))
    for i in range(4):
        x = (i - 1.5) * 0.16
        tube = cylinder(f"{name}_Tube_{i+1}", (x, 0.04, 0.25), 0.075, 1.12, M_DARK, pivot, collection, 10,
                        rot=(math.radians(68), 0, 0), bevel=0.012)
        box(f"{name}_Cap_{i+1}", (x, 0.56, 0.46), (0.135, 0.04, 0.135), role_mat or M_WARNING,
            pivot, collection, 0.008)
        empty(f"LaunchPoint_{name}_{i+1:02d}", pivot, collection, (x, 0.59, 0.47))
    return pivot


def add_stk(root, collection, tier):
    role_mat = bpy.data.materials["Role STK"]
    # port/starboard inclined banks are the primary role silhouette
    missile_canister("MissileBankPort", (-0.72, -1.52, 0.76), root, collection, -1, role_mat)
    missile_canister("MissileBankStarboard", (0.72, -1.52, 0.76), root, collection, 1, role_mat)
    fcr = empty("FireControlRadarPivot", root, collection, (0, -0.42, 2.34))
    box("FireControlDish", (0, 0, 0), (0.70, 0.09, 0.52), role_mat, fcr, collection, 0.03,
        rot=(math.radians(-12), 0, 0))
    if tier >= 1:
        missile_canister("MissileBankAft", (0, -2.85, 0.78), root, collection, 1, role_mat)
    if tier >= 2:
        add_vls_cells(root, collection, 8, (0, 1.70, 0.75), role_mat)
        sphere("OverHorizonLink", (0.48, -0.50, 2.17), (0.20, 0.20, 0.20), role_mat, root, collection, 12, 6)
    if tier >= 3:
        for side in (-1, 1):
            missile_canister(f"MissileBankReserve_{side}", (side * 0.72, -2.72, 0.80), root, collection, side, role_mat)


def create_ship(role, tier):
    asset_name = f"ESC_{role}_T{tier}"
    collection = bpy.data.collections.new(asset_name)
    bpy.context.scene.collection.children.link(collection)
    root = empty(asset_name + "_Root", None, collection, (0, 0, 0))
    add_common_ship(root, collection, tier, role)
    if role == "CAP":
        add_cap(root, collection, tier)
    elif role == "ASW":
        add_asw(root, collection, tier)
    elif role == "EW":
        add_ew(root, collection, tier)
    elif role == "STK":
        add_stk(root, collection, tier)
    root["role"] = role
    root["upgrade_tier"] = tier
    root["runtime_scale"] = TIER_SCALE[tier]
    root["forward_axis"] = "Blender +Y / Unity +Z"
    ASSETS.append((asset_name, role, tier, collection, root))
    return root


def torus(name, loc, major, minor, material, parent, collection, segments=64):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor,
                                    major_segments=segments, minor_segments=4, location=loc)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(material)
    o.parent = parent
    link_only(o, collection)
    return o


def radial_ticks(parent, collection, material, radius, count=36, long_every=3, prefix="BearingTick"):
    for i in range(count):
        a = math.radians(i * 360 / count)
        length = 0.42 if i % long_every == 0 else 0.20
        x, y = math.cos(a) * radius, math.sin(a) * radius
        box(f"{prefix}_{i:02d}", (x, y, 0.035), (length, 0.035, 0.025), material, parent, collection, 0.003,
            rot=(0, 0, a))


def wedge_sector(name, parent, collection, material, radius=4.2, degrees=42, z=0.018):
    steps = 18
    verts = [(0, 0, z)]
    for i in range(steps + 1):
        a = math.radians(-degrees / 2 + degrees * i / steps)
        verts.append((math.sin(a) * radius, math.cos(a) * radius, z))
    faces = [tuple(range(len(verts)))]
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    o = bpy.data.objects.new(name, mesh)
    o.data.materials.append(material)
    collection.objects.link(o)
    o.parent = parent
    return o


def bracket(name, center, size, material, parent, collection, z=0.07):
    cx, cy = center
    arm = size * 0.34
    thick = 0.055
    idx = 0
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(f"{name}_{idx}_H", (cx + sx * size, cy + sy * size, z), (arm, thick, 0.035),
                material, parent, collection, 0.005)
            box(f"{name}_{idx}_V", (cx + sx * size, cy + sy * size, z), (thick, arm, 0.035),
                material, parent, collection, 0.005)
            idx += 1


def create_effect(code):
    color, display = FX_DATA[code]
    asset_name = f"FX_{code}_Tactical"
    collection = bpy.data.collections.new(asset_name)
    bpy.context.scene.collection.children.link(collection)
    root = empty(asset_name + "_Root", None, collection)
    bright = bpy.data.materials[f"FX {code}"]
    faint = bpy.data.materials[f"FX {code} Faint"]
    for idx, radius in enumerate((1.45, 2.85, 4.20)):
        torus(f"RangeRing_{idx+1}", (0, 0, 0.02), radius, 0.025 if idx < 2 else 0.045, faint, root, collection)
    radial_ticks(root, collection, bright, 4.45, 36, 3)
    box("NorthBearing", (0, 4.67, 0.05), (0.10, 0.45, 0.04), bright, root, collection, 0.004)

    if code == "AEW":
        sweep = empty("SweepPivot", root, collection)
        wedge_sector("ScanSector", sweep, collection, faint, 4.2, 48)
        box("SweepLine", (0, 2.08, 0.055), (0.055, 4.15, 0.035), bright, sweep, collection, 0.004)
        for i, p in enumerate(((-1.6, 1.0), (2.1, 0.6), (0.8, -2.1), (-2.4, -1.7))):
            cylinder(f"TrackPip_{i+1}", (p[0], p[1], 0.08), 0.09, 0.05, bright, root, collection, 8)
            bracket(f"TrackBracket_{i+1}", p, 0.23, bright, root, collection)
        box("DataLinkVector", (1.05, -1.05, 0.06), (0.045, 3.0, 0.035), bright, root, collection, 0.004,
            rot=(0, 0, math.radians(-45)))
    elif code == "CAP":
        for i, ang in enumerate((-32, 18, 64)):
            a = math.radians(ang)
            start = Vector((0.0, 0.0))
            end = Vector((math.sin(a) * 3.4, math.cos(a) * 3.4))
            mid = (start + end) / 2
            box(f"InterceptVector_{i+1}", (mid.x, mid.y, 0.06), (0.055, end.length, 0.04), bright,
                root, collection, 0.004, rot=(0, 0, -a))
            bracket(f"ThreatBracket_{i+1}", (end.x, end.y), 0.30, bright, root, collection)
        torus("DefenseUmbrella", (0, 0, 0.04), 3.55, 0.07, bright, root, collection)
    elif code == "ASW":
        for i, radius in enumerate((0.65, 1.20, 1.80, 2.55, 3.35)):
            torus(f"SonarPulse_{i+1}", (0, 0, 0.04 + i * 0.004), radius, 0.035, bright if i % 2 == 0 else faint, root, collection)
        bearing = empty("BearingLinePivot", root, collection)
        box("BearingLine", (0, 2.05, 0.07), (0.045, 4.1, 0.04), bright, bearing, collection, 0.004)
        bracket("SubmarineContact", (1.75, 2.20), 0.38, bright, root, collection)
        for i in range(5):
            box(f"DepthScale_{i+1}", (-3.55, -1.4 + i * 0.7, 0.065), (0.32 + i * 0.05, 0.045, 0.04),
                bright, root, collection, 0.004)
    elif code == "EW":
        for i in range(9):
            ang = math.radians(i * 40)
            length = 3.5
            box(f"JammingRay_{i+1}", (math.sin(ang) * length / 2, math.cos(ang) * length / 2, 0.055),
                (0.045, length, 0.035), faint if i % 2 else bright, root, collection, 0.004,
                rot=(0, 0, -ang))
        for i in range(7):
            box(f"NoiseBar_{i+1}", (-2.7 + i * 0.9, -3.55 + (i % 2) * 0.18, 0.08),
                (0.58, 0.08, 0.05), bright, root, collection, 0.004,
                rot=(0, 0, math.radians((i % 3 - 1) * 10)))
        bracket("BrokenLock", (2.15, 1.85), 0.52, bright, root, collection)
        box("LockBreakSlash", (2.15, 1.85, 0.09), (0.09, 1.60, 0.05), bright, root, collection, 0.004,
            rot=(0, 0, math.radians(45)))
    elif code == "STK":
        torus("TargetRingOuter", (1.25, 1.15, 0.06), 0.95, 0.07, bright, root, collection)
        torus("TargetRingInner", (1.25, 1.15, 0.07), 0.45, 0.035, bright, root, collection)
        bracket("AimBracket", (1.25, 1.15), 0.72, bright, root, collection)
        box("ApproachVector", (-0.55, -0.45, 0.08), (0.08, 4.20, 0.05), bright, root, collection, 0.004,
            rot=(0, 0, math.radians(-42)))
        for i in range(5):
            box(f"TimeToImpactTick_{i+1}", (-2.90 + i * 0.53, -2.42 + i * 0.53, 0.09),
                (0.30, 0.055, 0.045), bright, root, collection, 0.004,
                rot=(0, 0, math.radians(45)))

    root["effect_code"] = code
    root["display_name"] = display
    root["color_rgba"] = list(color)
    FX_ASSETS.append((asset_name, code, collection, root))
    return root


def descendants(root):
    result = [root]
    stack = list(root.children)
    while stack:
        o = stack.pop()
        result.append(o)
        stack.extend(list(o.children))
    return result


def export_root(root, filepath):
    bpy.ops.object.select_all(action='DESELECT')
    for o in descendants(root):
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=filepath,
        use_selection=True,
        object_types={'EMPTY', 'MESH'},
        axis_forward='-Z', axis_up='Y',
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_NONE',
        use_space_transform=True,
        bake_space_transform=False,
        add_leaf_bones=False,
        path_mode='COPY',
        embed_textures=False,
    )
    bpy.ops.object.select_all(action='DESELECT')


def duplicate_tree(source_root, collection, name_suffix="_Preview"):
    mapping = {}
    def rec(src, parent=None):
        dup = src.copy()
        if src.data:
            dup.data = src.data
        dup.name = src.name + name_suffix
        collection.objects.link(dup)
        dup.parent = parent
        mapping[src] = dup
        for ch in src.children:
            rec(ch, dup)
        return dup
    return rec(source_root)


def add_text(body, loc, size, collection, color=(0.65, 0.85, 0.84, 1), align='CENTER'):
    curve = bpy.data.curves.new("PreviewLabel", 'FONT')
    curve.body = body
    curve.align_x = align
    curve.align_y = 'CENTER'
    curve.size = size
    curve.extrude = 0.008
    obj = bpy.data.objects.new("Label_" + body, curve)
    obj.location = loc
    obj.data.materials.append(mat("Preview Label", color, 0.0, 0.5, 1.4))
    collection.objects.link(obj)
    return obj


def look_at(obj, target=(0, 0, 0)):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()


def setup_render():
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_percentage = 75
    scene.render.image_settings.file_format = 'PNG'
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = 'RGBA'
    scene.view_settings.exposure = 0.0
    shading = scene.display.shading
    shading.light = 'STUDIO'
    shading.color_type = 'MATERIAL'
    shading.show_shadows = True
    shading.show_cavity = True
    shading.cavity_type = 'WORLD'
    shading.curvature_ridge_factor = 1.4
    shading.curvature_valley_factor = 1.0
    shading.show_specular_highlight = True
    shading.background_type = 'VIEWPORT'
    shading.background_color = (0.004, 0.012, 0.020)
    return scene


def make_preview_stage(name="PreviewStage"):
    old = bpy.data.collections.get(name)
    if old:
        bpy.data.collections.remove(old)
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    return col


def stage_grid(collection, size=(44, 34), spacing=2.0):
    ground = box("PreviewWater", (0, 0, -0.80), (size[0], size[1], 0.20),
                 mat("Preview Ocean", (0.008, 0.035, 0.052, 1), 0.32, 0.28), None, collection, 0.0)
    grid_mat = mat("Preview Grid", (0.025, 0.23, 0.27, 1), 0.0, 0.45, 0.45)
    for x in range(int(-size[0] / 2), int(size[0] / 2) + 1, int(spacing)):
        box(f"GridX{x}", (x, 0, -0.685), (0.025, size[1], 0.012), grid_mat, None, collection, 0.0)
    for y in range(int(-size[1] / 2), int(size[1] / 2) + 1, int(spacing)):
        box(f"GridY{y}", (0, y, -0.68), (size[0], 0.025, 0.012), grid_mat, None, collection, 0.0)


def add_preview_lights(collection):
    bpy.ops.object.light_add(type='AREA', location=(0, -6, 19))
    key = bpy.context.object
    key.name = "Preview_Key"
    key.data.energy = 1250
    key.data.shape = 'DISK'
    key.data.size = 12
    key.rotation_euler = (0, 0, 0)
    look_at(key)
    link_only(key, collection)
    bpy.ops.object.light_add(type='AREA', location=(-15, 8, 9))
    fill = bpy.context.object
    fill.name = "Preview_Fill"
    fill.data.energy = 520
    fill.data.color = (0.20, 0.65, 0.85)
    fill.data.size = 10
    look_at(fill)
    link_only(fill, collection)


def render_fleet_overview(scene):
    col = make_preview_stage("PreviewFleet")
    stage_grid(col, (48, 38), 2)
    add_preview_lights(col)
    xs = (-15.0, -5.0, 5.0, 15.0)
    ys = (11.5, 3.8, -3.9, -11.6)
    roles = ("CAP", "ASW", "EW", "STK")
    by_key = {(role, tier): root for _, role, tier, _, root in ASSETS}
    for row, role in enumerate(roles):
        add_text(f"{role}  {ROLE_DATA[role]['name'].upper()}", (-22.0, ys[row], 0.2), 0.62, col, ROLE_DATA[role]["color"], 'LEFT')
        for tier in range(4):
            dup = duplicate_tree(by_key[(role, tier)], col)
            dup.location = (xs[tier], ys[row], 0)
            dup.scale = (TIER_SCALE[tier],) * 3
            add_text(f"T{tier}  x{TIER_SCALE[tier]:.2f}", (xs[tier], ys[row] - 3.35, 0.12), 0.48, col)
    add_text("TASK FORCE ESCORT DEVELOPMENT / ROLE COLOR LOCKED", (0, 17.2, 0.1), 0.78, col)
    bpy.ops.object.camera_add(location=(28, -31, 34))
    cam = bpy.context.object
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = 51
    look_at(cam, (0, 0, 0.0))
    link_only(cam, col)
    scene.camera = cam
    scene.render.resolution_x = 2048
    scene.render.resolution_y = 1536
    scene.render.filepath = os.path.join(PREVIEW_DIR, "TaskForce_Escort_AllTiers.png")
    bpy.ops.render.render(write_still=True)
    col.hide_render = True


def render_role_sheets(scene):
    by_key = {(role, tier): root for _, role, tier, _, root in ASSETS}
    for role in ("CAP", "ASW", "EW", "STK"):
        col = make_preview_stage("PreviewRole_" + role)
        stage_grid(col, (36, 18), 2)
        add_preview_lights(col)
        xs = (-12.0, -4.0, 4.0, 12.0)
        for tier in range(4):
            dup = duplicate_tree(by_key[(role, tier)], col)
            dup.location = (xs[tier], 0.0, 0)
            dup.scale = (TIER_SCALE[tier],) * 3
            add_text(f"T{tier} / x{TIER_SCALE[tier]:.2f}", (xs[tier], -4.1, 0.1), 0.48, col)
        add_text(f"{role} / {ROLE_DATA[role]['name'].upper()}", (0, 7.3, 0.1), 0.78, col, ROLE_DATA[role]["color"])
        bpy.ops.object.camera_add(location=(0, -28, 25))
        cam = bpy.context.object
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = 37
        look_at(cam, (0, 0, 0.1))
        link_only(cam, col)
        scene.camera = cam
        scene.render.resolution_x = 1920
        scene.render.resolution_y = 1080
        scene.render.filepath = os.path.join(PREVIEW_DIR, f"TaskForce_{role}_Tiers.png")
        bpy.ops.render.render(write_still=True)
        col.hide_render = True


def render_effect_sheet(scene):
    col = make_preview_stage("PreviewEffects")
    stage_grid(col, (39, 27), 1)
    add_preview_lights(col)
    spots = {
        "AEW": (-12.0, 5.6), "CAP": (0.0, 5.6), "ASW": (12.0, 5.6),
        "EW": (-6.0, -6.6), "STK": (6.0, -6.6),
    }
    fx_map = {code: root for _, code, _, root in FX_ASSETS}
    ship_map = {role: root for _, role, tier, _, root in ASSETS if tier == 3}
    for code, (x, y) in spots.items():
        dup = duplicate_tree(fx_map[code], col)
        dup.location = (x, y, 0)
        dup.scale = (0.86, 0.86, 0.86)
        if code != "AEW":
            s = duplicate_tree(ship_map[code], col, "_FxPreview")
            s.location = (x, y, 0.0)
            s.scale = (0.28, 0.28, 0.28)
        else:
            # simple AEW aircraft planform marker; effect remains the exported asset.
            box("AEW_Fuselage", (x, y, 0.15), (0.18, 1.45, 0.12), M_NAVAL, None, col, 0.02)
            box("AEW_Wing", (x, y - 0.05, 0.15), (1.25, 0.18, 0.08), M_NAVAL, None, col, 0.02)
            cylinder("AEW_Radome", (x, y, 0.28), 0.34, 0.08, bpy.data.materials["FX AEW"], None, col, 16)
        add_text(f"{code} / {FX_DATA[code][1].upper()}", (x, y - 4.75, 0.12), 0.45, col, FX_DATA[code][0])
    add_text("TACTICAL SUPPORT EFFECT RIGS / ANIMATABLE COMPONENTS", (0, 12.0, 0.12), 0.72, col)
    bpy.ops.object.camera_add(location=(0, 0, 34))
    cam = bpy.context.object
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = 39
    look_at(cam, (0, 0, 0))
    link_only(cam, col)
    scene.camera = cam
    scene.render.resolution_x = 2048
    scene.render.resolution_y = 1400
    scene.render.filepath = os.path.join(PREVIEW_DIR, "TaskForce_SkillFX_All.png")
    previous_shadows = scene.display.shading.show_shadows
    scene.display.shading.show_shadows = False
    bpy.ops.render.render(write_still=True)
    scene.display.shading.show_shadows = previous_shadows
    col.hide_render = True


def count_tris(root):
    total = 0
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for obj in descendants(root):
        if obj.type != 'MESH':
            continue
        ev = obj.evaluated_get(depsgraph)
        mesh = ev.to_mesh()
        mesh.calc_loop_triangles()
        total += len(mesh.loop_triangles)
        ev.to_mesh_clear()
    return total


def main():
    clean_scene()
    setup_materials()
    scene = setup_render()
    for role in ("CAP", "ASW", "EW", "STK"):
        for tier in range(4):
            create_ship(role, tier)
    for code in ("AEW", "CAP", "ASW", "EW", "STK"):
        create_effect(code)

    manifest = {
        "format": "NavalRoguelike task-force tactical draft v1",
        "orientation": "Blender +Y forward, +Z up; FBX -Z Forward / Y Up / All Local",
        "application_status": "ArtSource only; not copied to Unity Assets and not referenced by prefabs or scripts",
        "runtime_tier_scale": TIER_SCALE,
        "ships": [],
        "effects": [],
    }
    for asset_name, role, tier, collection, root in ASSETS:
        path = os.path.join(MODEL_DIR, asset_name + ".fbx")
        export_root(root, path)
        manifest["ships"].append({
            "file": os.path.relpath(path, OUT), "role": role, "tier": tier,
            "runtimeScale": TIER_SCALE[tier], "triangles": count_tris(root),
            "roleColor": ROLE_DATA[role]["color"],
            "root": root.name,
        })
    for asset_name, code, collection, root in FX_ASSETS:
        path = os.path.join(FX_DIR, asset_name + ".fbx")
        export_root(root, path)
        manifest["effects"].append({
            "file": os.path.relpath(path, OUT), "code": code,
            "triangles": count_tris(root), "color": FX_DATA[code][0],
            "root": root.name, "animation": "Animate named pivots/mesh groups in Unity; no baked animation",
        })

    # Source collections intentionally share the origin. Hide them while preview copies are rendered,
    # otherwise every source asset would appear as one overlapping pile in the contact sheets.
    for _, _, _, collection, _ in ASSETS:
        collection.hide_render = True
    for _, _, collection, _ in FX_ASSETS:
        collection.hide_render = True

    render_fleet_overview(scene)
    render_role_sheets(scene)
    render_effect_sheet(scene)

    # Store clean source collections at the origin. Preview collections stay in the file but are hidden.
    blend_path = os.path.join(OUT, "TaskForceTacticalAssets.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    with open(os.path.join(OUT, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
    print(json.dumps({
        "blend": blend_path,
        "ships": len(ASSETS),
        "effects": len(FX_ASSETS),
        "previews": 6,
    }, ensure_ascii=False))


if __name__ == "__main__":
    main()
