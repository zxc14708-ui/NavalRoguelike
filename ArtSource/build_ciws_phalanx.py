"""Build the stylized Phalanx-like CIWS for Unity.

Run with Blender 5.x: blender -b --python ArtSource/build_ciws_phalanx.py
Axes: Blender +Y forward, +Z up. FBX: -Z Forward / Y Up / All Local.
"""

import math
from pathlib import Path

import bpy
from mathutils import Vector


PROJECT = Path(__file__).resolve().parents[1]
FBX = PROJECT / "Assets/_Game/Art/Models/MOD_CIWS.fbx"
SOURCE = PROJECT / "ArtSource/MOD_CIWS_Source.blend"
PREVIEW = PROJECT / "ArtSource/MOD_CIWS_preview.png"

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.context.scene.unit_settings.system = "METRIC"
bpy.context.scene.unit_settings.scale_length = 1.0


def material(name, rgb, metallic=0.0, roughness=0.7):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*rgb, 1.0)
    mat.use_nodes = True
    bsdf = next((node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        bsdf = mat.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
        output = next((node for node in mat.node_tree.nodes if node.type == "OUTPUT_MATERIAL"), None)
        if output is None:
            output = mat.node_tree.nodes.new("ShaderNodeOutputMaterial")
        mat.node_tree.links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return mat


naval = material("Naval Blue Grey", (0.39, 0.46, 0.48), 0.22)
deck = material("Deck Grey", (0.29, 0.36, 0.37), 0.24)
gunmetal = material("Gunmetal", (0.075, 0.105, 0.12), 0.60, 0.48)
sensor = material("Sensor Glass", (0.025, 0.095, 0.13), 0.25, 0.19)
radar = material("Radar Glass", (0.67, 0.71, 0.68), 0.12, 0.55)
yellow = material("Warning Yellow", (0.95, 0.54, 0.08), 0.0, 0.66)


def empty(name, location, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.09
    obj.location = location
    if parent is not None:
        bpy.context.view_layer.update()
        world = obj.matrix_world.copy()
        obj.parent = parent
        obj.matrix_world = world
    return obj


# Keep the legacy FBX root name so Unity can retain nested-prefab source IDs.
root = empty("MOD_CIWS_Real", (0, 0, 0))
turret = empty("TurretPivot", (0, 0, 0.27), root)
elevation = empty("ElevationPivot", (0, 0.12, 0.92), turret)
barrels = empty("BarrelCluster", (0, 0.38, 0.92), elevation)
muzzle = empty("Muzzle", (0, 1.39, 0.92), elevation)


def finish(obj, name, mat, parent, smooth=False):
    obj.name = name
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(mat)
    if smooth:
        for face in obj.data.polygons:
            face.use_smooth = True
    bpy.context.view_layer.update()
    world = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = world
    return obj


def box(name, loc, scale, mat, parent, bevel=0.0, rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.dimensions = scale
    if rotation is not None:
        obj.rotation_euler = rotation
    bpy.context.view_layer.update()
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel:
        mod = obj.modifiers.new("Soft armor edge", "BEVEL")
        mod.width = bevel
        mod.segments = 1
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(obj, name, mat, parent)


def cylinder(name, loc, radius, depth, mat, parent, vertices=12, along_y=False):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc)
    obj = bpy.context.object
    if along_y:
        obj.rotation_euler[0] = -math.pi / 2
    return finish(obj, name, mat, parent, smooth=vertices >= 12)


def sphere(name, loc, radius, scale, mat, parent, segments=14, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=radius, location=loc)
    obj = bpy.context.object
    obj.scale = scale
    return finish(obj, name, mat, parent, smooth=True)


def beam(name, a, b, thickness, mat, parent):
    mid = (Vector(a) + Vector(b)) * 0.5
    delta = Vector(b) - Vector(a)
    bpy.ops.mesh.primitive_cube_add(size=1, location=mid)
    obj = bpy.context.object
    obj.dimensions = (thickness, thickness, delta.length)
    obj.rotation_euler = delta.to_track_quat("Z", "Y").to_euler()
    return finish(obj, name, mat, parent)


# Fixed deck foundation: only this stays still while the weapon traverses.
cylinder("Deck flange", (0, 0, 0.055), 0.73, 0.11, deck, root, 16)
cylinder("Pedestal neck", (0, 0, 0.18), 0.51, 0.22, naval, root, 14)
cylinder("Traverse bearing", (0, 0, 0.28), 0.61, 0.07, gunmetal, root, 16)
for i in range(12):
    a = 2 * math.pi * i / 12
    cylinder(f"Base bolt {i+1:02d}", (0.64 * math.cos(a), 0.64 * math.sin(a), 0.118),
             0.025, 0.018, gunmetal, root, 6)

# Armored rotating lower body, two characteristic open-sided cheeks.
box("Central dark recess", (0, 0, 0.58), (0.88, 0.89, 0.63), gunmetal, turret, 0.04)
for side, label in [(-1, "Port"), (1, "Starboard")]:
    x = side * 0.59
    box(f"{label} armored cheek", (x, -0.025, 0.67), (0.34, 0.92, 0.78), naval, turret, 0.045)
    box(f"{label} service hatch", (side * 0.775, -0.07, 0.70),
        (0.016, 0.42, 0.43), deck, turret)
    box(f"{label} hatch hinge", (side * 0.79, -0.30, 0.71),
        (0.034, 0.05, 0.47), gunmetal, turret)
    box(f"{label} upper brace", (side * 0.53, 0.26, 1.00),
        (0.21, 0.55, 0.16), deck, turret)
    for row in range(3):
        box(f"{label} grab handle {row}", (side * 0.79, -0.34 + row * 0.23, 0.75),
            (0.04, 0.12, 0.045), gunmetal, turret)

# Characteristic aft search/track radome. It traverses with the upper mount.
cylinder("Radar mast drum", (0, -0.32, 1.19), 0.355, 0.49, radar, turret, 16)
sphere("Radar round cap", (0, -0.32, 1.43), 0.355, (1, 1, 0.84), radar, turret, 16, 8)
cylinder("Radome seam", (0, -0.32, 1.23), 0.369, 0.018, deck, turret, 16)
box("Radar service panel", (0.10, -0.68, 1.16), (0.19, 0.018, 0.11), naval, turret)
box("Radar panel inset", (0.10, -0.693, 1.16), (0.13, 0.008, 0.05), gunmetal, turret)

# Side electro-optical tracker and its protective visor.
box("EO mount arm", (0.59, -0.30, 1.16), (0.37, 0.25, 0.16), deck, turret)
box("EO housing", (0.67, -0.23, 1.30), (0.35, 0.34, 0.30), naval, turret, 0.025)
cylinder("EO optical barrel", (0.69, 0.015, 1.32), 0.12, 0.21, naval, turret, 12, True)
cylinder("EO dark lens", (0.69, 0.13, 1.32), 0.103, 0.012, sensor, turret, 12, True)
box("EO sun hood", (0.69, 0.15, 1.45), (0.29, 0.18, 0.045), gunmetal, turret)

# Elevating gun cradle and the triangular frame visible in the reference.
box("Gun cradle", (0, 0.24, 0.89), (0.51, 0.68, 0.35), naval, elevation, 0.025)
cylinder("Barrel drive ring", (0, 0.42, 0.92), 0.20, 0.15, gunmetal, elevation, 12, True)
for side, label in [(-1, "L"), (1, "R")]:
    beam(f"{label} gun frame lower", (side * 0.29, 0.27, 0.69),
         (side * 0.27, 0.93, 0.82), 0.060, naval, elevation)
    beam(f"{label} gun frame upper", (side * 0.29, 0.27, 1.12),
         (side * 0.27, 0.93, 1.01), 0.060, naval, elevation)
    beam(f"{label} triangular brace", (side * 0.29, 0.27, 1.12),
         (side * 0.27, 0.93, 0.82), 0.045, deck, elevation)
box("Forward frame crosspiece", (0, 0.91, 0.90), (0.61, 0.06, 0.075), naval, elevation)

# Six separate barrels. BarrelCluster is the spin axis; Muzzle is not its child.
cylinder("Rotating rear collar", (0, 0.41, 0.92), 0.15, 0.18, gunmetal, barrels, 12, True)
for i in range(6):
    a = 2 * math.pi * i / 6
    x, z = 0.083 * math.cos(a), 0.92 + 0.083 * math.sin(a)
    cylinder(f"Gatling barrel {i+1}", (x, 0.83, z), 0.026, 0.84,
             gunmetal, barrels, 8, True)
cylinder("Perforated muzzle shroud", (0, 1.15, 0.92), 0.152, 0.32,
         gunmetal, barrels, 12, True)
cylinder("Shroud front rim", (0, 1.32, 0.92), 0.161, 0.027,
         deck, barrels, 12, True)
cylinder("Dark muzzle inset", (0, 1.341, 0.92), 0.128, 0.005,
         gunmetal, barrels, 12, True)
for i in range(6):
    a = 2 * math.pi * i / 6
    cylinder(f"Barrel bore {i+1}", (0.083 * math.cos(a), 1.347,
             0.92 + 0.083 * math.sin(a)), 0.019, 0.006,
             sensor, barrels, 8, True)
for i in range(8):
    a = 2 * math.pi * i / 8
    box(f"Shroud vent {i+1}", (0.155 * math.cos(a), 1.12, 0.92 + 0.155 * math.sin(a)),
        (0.035, 0.13, 0.035), sensor, barrels)

# In miniature view these few bright magazine-feed links identify the mechanism.
cylinder("Feed drum", (-0.39, -0.04, 0.60), 0.22, 0.30, gunmetal, turret, 12)
for i in range(11):
    t = i / 10
    box(f"Ammunition link {i+1:02d}", (-0.50 + t * 0.24,
        -0.22 + t * 0.50, 0.43 + t * 0.30), (0.060, 0.075, 0.045),
        yellow if i % 2 == 0 else gunmetal, turret)

# Keep pivot boundaries intact while combining meshes that share a material.
# This cuts the number of renderer objects without changing the silhouette.
for pivot in (root, turret, elevation, barrels):
    groups = {}
    for child in pivot.children:
        if child.type == "MESH" and child.data.materials:
            groups.setdefault(child.data.materials[0].name, []).append(child)
    for name, objects in groups.items():
        if len(objects) < 2:
            continue
        bpy.ops.object.select_all(action="DESELECT")
        for obj in objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.object.join()
        objects[0].name = f"{pivot.name} {name} mesh"

# Export only the art hierarchy, with applied mesh transforms and 1 m = 1 Unity unit.
bpy.ops.object.select_all(action="DESELECT")
export_objects = [obj for obj in bpy.data.objects if obj.type in {"MESH", "EMPTY"}]
for obj in export_objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
bpy.ops.export_scene.fbx(filepath=str(FBX), use_selection=True,
                         object_types={"MESH", "EMPTY"}, axis_forward="-Z", axis_up="Y",
                         global_scale=1.0, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False)

triangles = sum(len(poly.vertices) - 2 for obj in export_objects if obj.type == "MESH"
                for poly in obj.data.polygons)
print(f"CIWS_EXPORT={FBX} TRIANGLES={triangles} OBJECTS={len(export_objects)}")

# A separate studio preview, not part of the game model.
bpy.ops.object.select_all(action="DESELECT")
bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.03))
floor = bpy.context.object
floor.data.materials.append(material("Preview ocean", (0.025, 0.085, 0.12), 0.2, 0.7))

def area_light(name, loc, energy, size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    obj.rotation_euler = (Vector((0, 0, 0.8)) - obj.location).to_track_quat("-Z", "Y").to_euler()

area_light("Key", (2.8, 3.5, 5.0), 520, 4.0)
area_light("Rim", (-2.3, -2.2, 3.8), 330, 3.0)
camera_data = bpy.data.cameras.new("Preview camera")
camera = bpy.data.objects.new("Preview camera", camera_data)
bpy.context.collection.objects.link(camera)
camera.location = (3.0, 4.2, 2.7)
camera.rotation_euler = (Vector((0, 0.12, 1.0)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera_data.type = "ORTHO"
camera_data.ortho_scale = 3.6
bpy.context.scene.camera = camera
bpy.context.scene.render.engine = "BLENDER_EEVEE"
bpy.context.scene.render.resolution_x = 1100
bpy.context.scene.render.resolution_y = 1100
bpy.context.scene.render.resolution_percentage = 100
bpy.context.scene.render.image_settings.file_format = "PNG"
bpy.context.scene.render.filepath = str(PREVIEW)
bpy.context.scene.render.film_transparent = False
bpy.context.scene.world.color = (0.045, 0.075, 0.1)
bpy.ops.render.render(write_still=True)
print(f"CIWS_PREVIEW={PREVIEW}")
