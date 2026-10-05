"""Build eight standalone module upgrade models and comparison renders.

Models remain under ArtSource/ModuleUpgrades and are not applied to Unity.
Blender axes: +Y forward, +Z up. Unit: metre.
"""

import math
from pathlib import Path

import bpy
from mathutils import Vector


PROJECT = Path(__file__).resolve().parents[2]
BASE_DIR = PROJECT / "Assets/_Game/Art/Models"
OUT = PROJECT / "ArtSource/ModuleUpgrades"
OUT.mkdir(parents=True, exist_ok=True)
BLENDER = bpy.app.version_string


def reset():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.cameras, bpy.data.lights):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0


def mat(name, rgb=None, metallic=0.0, roughness=0.65):
    found = bpy.data.materials.get(name)
    if found:
        return found
    rgb = rgb or (0.4, 0.45, 0.47)
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1.0)
    m.use_nodes = True
    bsdf = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
    return m


def palette():
    return {
        "naval": mat("Naval Blue Grey", (0.36, 0.43, 0.45), 0.22),
        "deck": mat("Deck Grey", (0.24, 0.30, 0.32), 0.26),
        "gun": mat("Gunmetal", (0.055, 0.075, 0.085), 0.65, 0.42),
        "sensor": mat("Sensor Glass", (0.015, 0.10, 0.14), 0.30, 0.18),
        "radar": mat("Radar Glass", (0.70, 0.74, 0.71), 0.10, 0.48),
        "yellow": mat("Warning Yellow", (0.95, 0.55, 0.07), 0.0, 0.62),
        "red": mat("Emergency Red", (0.72, 0.04, 0.025), 0.05, 0.58),
        "white": mat("Medical White", (0.84, 0.88, 0.86), 0.04, 0.58),
        "glass": mat("Dark bridge glazing", (0.018, 0.06, 0.075), 0.2, 0.16),
    }


def parent_keep(obj, parent):
    world = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = world


def finish(obj, name, material, parent=None, smooth=False):
    obj.name = name
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if material:
        obj.data.materials.append(material)
    if smooth and obj.type == "MESH":
        for poly in obj.data.polygons:
            poly.use_smooth = True
    if parent:
        bpy.context.view_layer.update()
        parent_keep(obj, parent)
    return obj


def empty(name, loc=(0, 0, 0), parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.08
    if parent:
        bpy.context.view_layer.update()
        parent_keep(obj, parent)
    return obj


def box(name, loc, dims, material, parent=None, bevel=0.0, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.dimensions = dims
    bpy.context.view_layer.update()
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel:
        mod = obj.modifiers.new("Edge bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 1
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(obj, name, material, parent)


def cylinder(name, loc, radius, depth, material, parent=None, vertices=12, axis="Z", rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc)
    obj = bpy.context.object
    if axis == "Y":
        obj.rotation_euler[0] = -math.pi / 2
    elif axis == "X":
        obj.rotation_euler[1] = math.pi / 2
    obj.rotation_euler.rotate_axis("X", rotation[0])
    obj.rotation_euler.rotate_axis("Y", rotation[1])
    obj.rotation_euler.rotate_axis("Z", rotation[2])
    return finish(obj, name, material, parent, smooth=vertices >= 12)


def sphere(name, loc, radius, scale, material, parent=None, segments=14, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=radius, location=loc)
    obj = bpy.context.object
    obj.scale = scale
    return finish(obj, name, material, parent, smooth=True)


def beam(name, a, b, thickness, material, parent=None):
    a, b = Vector(a), Vector(b)
    delta = b - a
    bpy.ops.mesh.primitive_cube_add(size=1, location=(a + b) * 0.5)
    obj = bpy.context.object
    obj.dimensions = (thickness, thickness, delta.length)
    obj.rotation_euler = delta.to_track_quat("Z", "Y").to_euler()
    return finish(obj, name, material, parent)


def import_base(name):
    path = BASE_DIR / f"{name}.fbx"
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(path))
    added = [o for o in bpy.context.scene.objects if o not in before]
    roots = [o for o in added if o.parent is None and o.type in {"EMPTY", "MESH"}]
    if not roots:
        raise RuntimeError(f"No root imported from {path}")
    return roots[0]


def simplify_imported_meshes(root, ratio):
    """Keep inherited silhouettes while reserving triangles for upgrade hardware."""
    for obj in [root] + list(root.children_recursive):
        if obj.type != "MESH" or len(obj.data.polygons) < 16:
            continue
        modifier = obj.modifiers.new("Upgrade budget decimate", "DECIMATE")
        modifier.ratio = ratio
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)


def find(name):
    obj = bpy.data.objects.get(name)
    if obj is None:
        raise RuntimeError(f"Required object missing: {name}")
    return obj


def add_autocannon(level, p):
    root = import_base("MOD_Autocannon")
    root.name = f"MOD_Autocannon_U{level}"
    turret, elev = find("TurretPivot"), find("ElevationPivot")

    # U1: visible ready-use ammunition boxes and a perforated thermal jacket.
    for side, label in [(-1, "Port"), (1, "Starboard")]:
        box(f"U1 {label} ammunition box", (side * 0.48, -0.03, 0.78),
            (0.25, 0.48, 0.36), p["naval"], turret, 0.035)
        box(f"U1 {label} magazine latch", (side * 0.615, 0.00, 0.79),
            (0.025, 0.24, 0.12), p["yellow"], turret, 0.01)
    cylinder("U1 barrel thermal jacket", (0, 0.80, 0.90), 0.095, 0.52,
             p["gun"], elev, 12, "Y")
    for i in range(4):
        box(f"U1 cooling slot {i+1}", (0, 0.61 + i * 0.12, 0.995),
            (0.035, 0.07, 0.022), p["sensor"], elev)

    if level >= 2:
        # U2: armored cheeks, larger dual-channel optic and reinforced muzzle.
        for side, label in [(-1, "Port"), (1, "Starboard")]:
            box(f"U2 {label} armored cheek", (side * 0.47, 0.16, 0.83),
                (0.18, 0.52, 0.52), p["deck"], turret, 0.045)
            beam(f"U2 {label} cradle brace", (side * 0.38, -0.05, 0.64),
                 (side * 0.36, 0.42, 1.02), 0.055, p["gun"], turret)
        box("U2 dual sensor housing", (-0.39, 0.09, 1.12),
            (0.39, 0.32, 0.27), p["naval"], turret, 0.035)
        for x in (-0.48, -0.31):
            cylinder("U2 optic lens", (x, 0.267, 1.13), 0.065, 0.025,
                     p["sensor"], turret, 12, "Y")
        cylinder("U2 reinforced muzzle collar", (0, 1.37, 0.90), 0.118, 0.15,
                 p["deck"], elev, 12, "Y")
        box("U2 recoil housing", (0, 0.21, 0.90), (0.36, 0.34, 0.28),
            p["naval"], elev, 0.025)
    return root


def add_ciws(level, p):
    root = import_base("MOD_CIWS")
    root.name = f"MOD_CIWS_U{level}"
    # The source CIWS already uses most of the allowed budget. Simplify only
    # inherited meshes so the upgraded variants remain below the 2,200-tri cap.
    simplify_imported_meshes(root, 0.70)
    turret, elev = find("TurretPivot"), find("ElevationPivot")

    # U1: bigger ready magazine and thermal-management equipment.
    box("U1 extended ammunition drum", (-0.50, -0.03, 0.59),
        (0.37, 0.56, 0.50), p["gun"], turret, 0.055)
    cylinder("U1 drum hub", (-0.705, -0.03, 0.59), 0.15, 0.035,
             p["yellow"], turret, 12, "X")
    box("U1 cooling pack", (0.52, 0.05, 0.62),
        (0.30, 0.48, 0.42), p["deck"], turret, 0.04)
    for i in range(3):
        box(f"U1 cooling grille {i+1}", (0.682, -0.10 + i * 0.10, 0.62),
            (0.018, 0.065, 0.24), p["sensor"], turret)

    if level >= 2:
        # U2: dual-channel tracker, radar collar and extra armored side panels.
        box("U2 tracker arm", (0.48, -0.26, 1.31),
            (0.30, 0.24, 0.16), p["gun"], turret, 0.025)
        box("U2 dual tracker", (0.60, -0.20, 1.45),
            (0.34, 0.32, 0.27), p["naval"], turret, 0.035)
        for x in (0.52, 0.67):
            cylinder("U2 tracker lens", (x, 0.0, 1.46), 0.058, 0.026,
                     p["sensor"], turret, 12, "Y")
        cylinder("U2 radome lower collar", (0, -0.32, 1.07), 0.40, 0.10,
                 p["deck"], turret, 16)
        for side, label in [(-1, "Port"), (1, "Starboard")]:
            box(f"U2 {label} armor panel", (side * 0.72, 0.10, 0.78),
                (0.13, 0.68, 0.58), p["naval"], turret, 0.035)
        cylinder("U2 forward barrel brace", (0, 0.94, 0.92), 0.19, 0.09,
                 p["deck"], elev, 12, "Y")
    return root


def add_magazine(level, p):
    root = import_base("MOD_Magazine")
    root.name = f"MOD_Magazine_U{level}"

    # U1: fixed steel rack, tie bars and automatic extinguisher bottle.
    for x in (-0.76, 0.76):
        for y in (-0.69, 0.69):
            box("U1 rack post", (x, y, 0.50), (0.055, 0.055, 0.82), p["gun"], root)
    for y in (-0.69, 0.69):
        beam("U1 rack crossbar", (-0.76, y, 0.88), (0.76, y, 0.88), 0.055, p["gun"], root)
    for x in (-0.76, 0.76):
        beam("U1 rack siderail", (x, -0.69, 0.88), (x, 0.69, 0.88), 0.055, p["gun"], root)
    cylinder("U1 extinguisher bottle", (0.70, -0.53, 0.48), 0.105, 0.58,
             p["red"], root, 12)
    cylinder("U1 extinguisher cap", (0.70, -0.53, 0.79), 0.045, 0.07,
             p["gun"], root, 10)
    box("U1 inventory panel", (-0.68, -0.62, 0.49),
        (0.08, 0.34, 0.37), p["white"], root, 0.015)

    if level >= 2:
        # U2: blast-resistant partial enclosure and suppression plumbing.
        box("U2 port blast wall", (-0.82, 0, 0.54),
            (0.10, 1.55, 0.86), p["naval"], root, 0.035)
        box("U2 starboard blast wall", (0.82, 0, 0.54),
            (0.10, 1.55, 0.86), p["naval"], root, 0.035)
        box("U2 aft blast wall", (0, -0.79, 0.54),
            (1.55, 0.10, 0.86), p["naval"], root, 0.035)
        box("U2 armored canopy", (0, 0, 1.00),
            (1.62, 1.58, 0.12), p["deck"], root, 0.035)
        for x in (-0.52, 0.0, 0.52):
            cylinder("U2 suppression nozzle", (x, 0.54, 0.88), 0.055, 0.13,
                     p["yellow"], root, 10, "Y")
        cylinder("U2 suppression manifold", (0, 0.61, 0.92), 0.035, 1.15,
                 p["red"], root, 8, "X")
        box("U2 pressure monitor", (0.66, 0.61, 0.62),
            (0.18, 0.08, 0.26), p["sensor"], root, 0.018)
    return root


def make_gun76(level, p):
    root = empty(f"MOD_Gun76_U{level}")
    # Fixed deck and traverse bearing.
    box("ModuleDeck", (0, 0, 0.07), (1.75, 1.75, 0.14), p["deck"], root, 0.025)
    cylinder("Fixed pedestal", (0, -0.05, 0.25), 0.68, 0.34, p["naval"], root, 16)
    cylinder("Traverse bearing", (0, -0.05, 0.43), 0.71, 0.08, p["gun"], root, 16)
    turret = empty("TurretPivot", (0, -0.05, 0.45), root)
    elev = empty("ElevationPivot", (0, 0.28, 0.93), turret)

    # Angular stealth-like gunhouse, distinct from the autocannon.
    box("Turret lower body", (0, -0.04, 0.73), (1.26, 1.02, 0.55),
        p["naval"], turret, 0.08)
    box("Turret sloped crown", (0, 0.02, 1.04), (1.02, 0.84, 0.24),
        p["naval"], turret, 0.07)
    for side, label in [(-1, "Port"), (1, "Starboard")]:
        box(f"{label} access panel", (side * 0.64, -0.08, 0.75),
            (0.035, 0.44, 0.30), p["deck"], turret, 0.01)
        box(f"{label} recoil cheek", (side * 0.40, 0.35, 0.96),
            (0.19, 0.42, 0.24), p["deck"], turret, 0.025)

    barrel_length = 1.32 if level == 0 else (1.48 if level == 1 else 1.68)
    cylinder("Recoil sleeve", (0, 0.47, 0.94), 0.145, 0.48,
             p["gun"], elev, 14, "Y")
    cylinder("76mm barrel", (0, 0.93 + barrel_length * 0.34, 0.94), 0.060,
             barrel_length, p["gun"], elev, 12, "Y")
    muzzle_y = 0.69 + barrel_length
    cylinder("Muzzle brake", (0, muzzle_y - 0.06, 0.94), 0.105, 0.20,
             p["deck"], elev, 12, "Y")
    box("Muzzle brake port", (-0.095, muzzle_y - 0.06, 0.94),
        (0.035, 0.10, 0.09), p["sensor"], elev)
    box("Muzzle brake starboard", (0.095, muzzle_y - 0.06, 0.94),
        (0.035, 0.10, 0.09), p["sensor"], elev)
    empty("Muzzle", (0, muzzle_y + 0.05, 0.94), elev)

    if level >= 1:
        # Upgrade I includes a compact EO fire-control head.
        box("U1 EO pedestal", (-0.40, -0.08, 1.21), (0.18, 0.22, 0.18),
            p["deck"], turret, 0.02)
        sphere("U1 EO head", (-0.40, -0.03, 1.38), 0.16,
               (1.0, 0.86, 0.9), p["naval"], turret, 12, 7)
        cylinder("U1 EO lens", (-0.40, 0.105, 1.39), 0.060, 0.025,
                 p["sensor"], turret, 12, "Y")
        box("U1 loader fairing", (0.42, -0.15, 1.03), (0.24, 0.40, 0.28),
            p["deck"], turret, 0.035)

    if level >= 2:
        # U2 has visible recoil machinery, rangefinder shoulders and extra armor.
        for side, label in [(-1, "Port"), (1, "Starboard")]:
            box(f"U2 {label} rangefinder", (side * 0.60, 0.07, 1.22),
                (0.25, 0.31, 0.22), p["naval"], turret, 0.035)
            cylinder(f"U2 {label} rangefinder lens", (side * 0.60, 0.245, 1.24),
                     0.065, 0.026, p["sensor"], turret, 12, "Y")
        box("U2 forward mantlet", (0, 0.42, 0.96),
            (0.66, 0.20, 0.43), p["naval"], elev, 0.055)
        for side in (-1, 1):
            cylinder("U2 recoil cylinder", (side * 0.18, 0.53, 0.82),
                     0.048, 0.50, p["yellow"], elev, 10, "Y")
        box("U2 rear armor pack", (0, -0.48, 0.91),
            (0.78, 0.25, 0.48), p["deck"], turret, 0.045)
    return root


BUILDERS = {
    "MOD_Autocannon": add_autocannon,
    "MOD_Gun76": make_gun76,
    "MOD_CIWS": add_ciws,
    "MOD_Magazine": add_magazine,
}


def model_objects(root):
    return [root] + list(root.children_recursive)


def export_model(stem, root):
    blend = OUT / f"{stem}.blend"
    fbx = OUT / f"{stem}.fbx"
    bpy.ops.object.select_all(action="DESELECT")
    objs = [o for o in model_objects(root) if o.type in {"MESH", "EMPTY"}]
    for obj in objs:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    bpy.ops.export_scene.fbx(
        filepath=str(fbx), use_selection=True, object_types={"MESH", "EMPTY"},
        axis_forward="-Z", axis_up="Y", global_scale=1.0, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE", add_leaf_bones=False,
        use_mesh_modifiers=True, mesh_smooth_type="FACE")
    tris = sum(len(poly.vertices) - 2 for o in objs if o.type == "MESH" for poly in o.data.polygons)
    print(f"BUILT {stem}: tris={tris} objects={len(objs)} blender={BLENDER}")


def studio():
    p = palette()
    floor = box("Preview floor", (0, 0, -0.04), (20, 20, 0.06),
                mat("Preview ocean", (0.025, 0.075, 0.105), 0.15, 0.72))
    floor.hide_select = True
    world = bpy.context.scene.world or bpy.data.worlds.new("Preview World")
    bpy.context.scene.world = world
    world.color = (0.025, 0.04, 0.055)
    for name, loc, energy, size in (
        ("Key", (4.5, 5.5, 7), 780, 5.5),
        ("Fill", (-4, 3, 4), 430, 4.0),
        ("Rim", (-3, -4, 5), 560, 4.0)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.shape, data.size = energy, "DISK", size
        obj = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(obj)
        obj.location = loc
        obj.rotation_euler = (Vector((0, 0, 0.8)) - obj.location).to_track_quat("-Z", "Y").to_euler()
    camera_data = bpy.data.cameras.new("Preview Camera")
    camera = bpy.data.objects.new("Preview Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (6.3, 7.5, 5.6)
    camera.rotation_euler = (Vector((0, 0.08, 0.82)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 7.6
    bpy.context.scene.camera = camera
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"


def add_label(text, loc, size=0.28):
    curve = bpy.data.curves.new(f"Label {text}", "FONT")
    curve.body = text
    curve.align_x = "CENTER"
    curve.size = size
    curve.extrude = 0.005
    curve.materials.append(mat("Label white", (0.82, 0.92, 0.96), 0.0, 0.55))
    obj = bpy.data.objects.new(f"Label {text}", curve)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    camera = bpy.context.scene.camera
    obj.rotation_euler = (camera.location - obj.location).to_track_quat("Z", "Y").to_euler()
    return obj


def render_comparison(name):
    reset()
    roots = []
    for idx, stem in enumerate((name, f"{name}_U1", f"{name}_U2")):
        path = BASE_DIR / f"{stem}.fbx" if idx == 0 else OUT / f"{stem}.fbx"
        if idx == 0 and not path.exists():
            path = OUT / f"{name}_BaseReference.fbx"
        before = set(bpy.context.scene.objects)
        bpy.ops.import_scene.fbx(filepath=str(path))
        new = [o for o in bpy.context.scene.objects if o not in before]
        root = next(o for o in new if o.parent is None and o.type in {"EMPTY", "MESH"})
        root.location.x = (-2.3, 0, 2.3)[idx]
        roots.append(root)
    studio()
    # Place the legend on the camera-side foreground so no model can occlude it.
    add_label("BASE", (-2.3, 1.58, 0.18), 0.22)
    add_label("UPGRADE I", (0, 1.58, 0.18), 0.22)
    add_label("UPGRADE II", (2.3, 1.58, 0.18), 0.22)
    bpy.context.scene.render.filepath = str(OUT / f"COMPARE_{name}.png")
    bpy.ops.render.render(write_still=True)
    print(f"RENDERED COMPARE_{name}.png")


def main():
    for name, builder in BUILDERS.items():
        for level in (1, 2):
            reset()
            p = palette()
            root = builder(level, p)
            export_model(f"{name}_U{level}", root)
    # The project has no authored 76 mm base FBX yet. This comparison-only
    # reference represents the current simple gunhouse before visible upgrades.
    reset()
    p = palette()
    root = make_gun76(0, p)
    export_model("MOD_Gun76_BaseReference", root)
    for name in BUILDERS:
        render_comparison(name)


if __name__ == "__main__":
    main()
