"""Derive terrain and the launcher visual from the actual in-game meshes."""
import math
import re

import bpy

from geometry import Mesh, material, node, DARK, METAL, YELLOW

CLIFF = 'MAT_IslandCliff'


def import_base(asset, filepath):
    bpy.ops.object.select_all(action='DESELECT')
    bpy.ops.import_scene.fbx(filepath=str(filepath))
    objects = list(bpy.context.selected_objects)
    root, = [obj for obj in objects if obj.parent is None]
    # Normalize the legacy kit's Blender re-import facing to source +Y.
    legacy_rotation = root.rotation_euler.copy()
    root.rotation_euler = (0,0,0)
    bpy.context.view_layer.update()
    for child in list(root.children):
        world = child.matrix_world.copy()
        child.parent = asset.root
        child.matrix_world = world
    bpy.data.objects.remove(root,do_unlink=True)
    for obj in asset.root.children_recursive:
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        asset.collection.objects.link(obj)
        obj['part_name'] = re.sub(r'\.\d{3}$', '', obj.name)
        if obj.type == 'MESH':
            for index, mat in enumerate(obj.data.materials):
                obj.data.materials[index] = material(re.sub(r'\.\d{3}$', '', mat.name))
    bpy.context.view_layer.update()
    return legacy_rotation


def terrain(asset, filepath, shape):
    legacy_rotation = import_base(asset, filepath)
    for obj in asset.collection.objects:
        if obj.type != 'MESH':
            continue
        world = obj.matrix_world.copy()
        inverse = world.inverted()
        for vertex in obj.data.vertices:
            point = world @ vertex.co
            azimuth = math.atan2(point.y,point.x)
            # Keep the original flat construction site and all heights. Apply
            # the same map to each material section so there are no open seams.
            t = min(1,max(0,(.81-point.z)/1.71))
            t = t*t*(3-2*t)
            if shape == 'B':
                # Long shoulder at one end of the island, tucked opposite shore.
                ratio = .985 + .085*math.cos(azimuth-.60) + .047*math.cos(2*azimuth+.76)
                shear = .064
            else:
                # Broader rounded headland, with a flatter opposite shoreline.
                ratio = .978 + .055*math.cos(azimuth+1.34) - .084*math.cos(2*azimuth-.81)
                shear = -.076
            point.x, point.y = (point.x*(1+t*(ratio-1))+point.y*t*shear,
                                point.y*(1+t*(ratio-1)))
            vertex.co = inverse @ point
        obj.data.update()
    asset.root.rotation_euler = legacy_rotation


def collar(mesh, x, z, start, end, outer, inner, color):
    def ring(y, dims):
        w,h = dims
        return [(x+dx*w,y,z+dz*h) for dx,dz in ((-1,-1),(1,-1),(1,1),(-1,1))]
    rings = [ring(start,outer),ring(end,outer),ring(end,inner),ring(start,inner)]
    faces = [(j*4+i,j*4+(i+1)%4,((j+1)%4)*4+(i+1)%4,((j+1)%4)*4+i)
             for j in range(4) for i in range(4)]
    mesh.add(sum(rings,[]),faces,[color]*8+[DARK]*4+[METAL]*4)


def launcher(asset, filepath):
    legacy_rotation = import_base(asset, filepath)
    collection = asset.collection
    parts = {obj['part_name']:obj for obj in collection.objects}
    elevation = parts['ElevationPivot']
    for name in ('ElevationPivot_Gunmetal','ElevationPivot_MAT_IslandCliff'):
        bpy.data.objects.remove(parts[name],do_unlink=True)
    tubes = Mesh()
    for i in (1,2):
        muzzle = parts[f'LaunchPoint_{i:02}']
        x, y, z = muzzle.location
        # Two shallow visual recesses. Preserve the existing launch and pivot nodes.
        tubes.box((x,-.05,z),(1.20,4.14,1.15),CLIFF)
        collar(tubes,x,z,2.02,2.635,(.60,.575),(.44,.398),CLIFF)
        collar(tubes,x,z,-2.27,-2.12,(.60,.575),(.41,.35),CLIFF)
        tubes.box((x,2.035,z),(.905,.035,.83),DARK)
        tubes.box((x,-2.165,z),(.84,.08,.72),DARK)
        for dx in (-.43,.43):
            tubes.tube((x+dx,2.67,z+.485),(1,0,0),[(-.065,.074),(.065,.074)],METAL,n=8)

        hatch_pivot = node(f'LauncherHatchPivot_{i:02}',collection,elevation,(x,2.676,z+.48))
        hatch = Mesh()
        hatch.box((0,.024,-.48),(1.05,.082,.95),METAL)
        hatch.box((0,.073,-.48),(.84,.028,.73),CLIFF,bevel=.08)
        hatch.box((0,.094,-.73),(.34,.018,.070),YELLOW)
        hatch.object(f'LauncherHatch_{i:02}',collection,hatch_pivot)

        node(f'BackblastPoint_{i:02}',collection,elevation,(x,-2.31,z),direction=(0,-1,0))
    tubes.object('CanisterHousings',collection,elevation)
    asset.root.rotation_euler = legacy_rotation
