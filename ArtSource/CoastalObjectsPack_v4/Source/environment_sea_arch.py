"""Angular sea stack arch with one continuous, genuinely open rock passage.

The arch is a closed U-shaped solid, not a ring laid over a filled cliff.
Each end is a separate seabed foot; the space between the feet has no floor
or end cap.  The few large fracture planes keep the deliberately low-poly
coastal style and all geometry uses the existing material palette.
"""
import json

import bmesh
import bpy
from mathutils import Vector

if __name__ == '__main__':
    import os
    import sys
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import environment_detail_common as c
from environment_rock_geometry import angular_rock


# Corresponding outer/inner points run from the left seabed foot, over the
# crown, to the right foot.  Short changes in width at .6 m, 2.5 m and 3.5 m
# are strata ledges.  The rest are broad, deliberately uneven rock facets.
# (outer x, outer z, inner x, inner z)
_ARCH_SECTIONS = (
    (-3.86, -1.65, -1.86, -1.65),
    (-4.04, -.55, -1.81, -.55),
    (-3.89, .46, -1.69, .46),
    (-4.04, .64, -1.78, .64),
    (-3.98, .83, -1.74, .83),
    (-4.12, 1.72, -1.76, 1.72),
    (-3.81, 2.32, -1.65, 2.32),
    (-3.85, 2.49, -1.72, 2.49),
    (-3.75, 2.65, -1.62, 2.65),
    (-3.64, 3.28, -1.60, 3.28),
    (-3.75, 3.46, -1.47, 3.46),
    (-3.46, 3.65, -1.32, 3.65),
    (-3.30, 4.50, -1.05, 3.85),
    (-2.85, 5.12, -.65, 4.20),
    (-1.90, 5.62, -.19, 4.42),
    (-.75, 5.90, .34, 4.47),
    (.48, 6.00, .87, 4.37),
    (1.86, 5.62, 1.31, 4.10),
    (2.60, 5.26, 1.70, 3.66),
    (3.42, 4.43, 1.93, 3.25),
    (3.63, 3.62, 1.96, 3.05),
    (3.51, 3.43, 2.07, 2.87),
    (3.73, 2.65, 2.03, 2.65),
    (3.88, 2.48, 2.12, 2.48),
    (3.79, 2.31, 2.00, 2.31),
    (4.05, 1.42, 2.00, 1.42),
    (3.78, .83, 1.82, .83),
    (3.97, .63, 1.94, .63),
    (3.84, .46, 1.89, .46),
    (4.12, -.60, 1.97, -.60),
    (4.10, -1.65, 2.00, -1.65),
)


def _arch_core(root, col):
    """Sweep a thick U profile with closed rock ends, never a passage cap."""
    # Every exposed front/back band is planar.  The two intermediate sections
    # offset the roof and side fractures through the thickness without any
    # smoothing, bevel modifier, or round tube cross-section.
    depth_sections = ((-1.28, -.04, -.035), (-.72, .10, .00),
                      (.69, -.12, .10), (1.25, .025, -.07))
    section_count = len(_ARCH_SECTIONS)
    vertices = []
    for y, x_offset, z_offset in depth_sections:
        for outer_x, outer_z, inner_x, inner_z in _ARCH_SECTIONS:
            for x, z in ((outer_x, outer_z), (inner_x, inner_z)):
                # The submerged contacts all remain on the same base plane.
                height_weight = min(1.0, max(0.0, (z + 1.65) / 3.0))
                vertices.append((x + x_offset * height_weight, y,
                                 z + z_offset * height_weight))

    def vertex(depth, section, inner):
        return depth * section_count * 2 + section * 2 + inner

    faces = []
    face_kinds = []

    def add(indices, kind):
        faces.append(indices)
        face_kinds.append(kind)

    # Front/back surfaces follow the U's rock ribbon.  Individual strips are
    # essential: a single ngon across this profile would seal the opening.
    for depth in (0, len(depth_sections) - 1):
        for i in range(section_count - 1):
            indices = (vertex(depth, i, 0), vertex(depth, i + 1, 0),
                       vertex(depth, i + 1, 1), vertex(depth, i, 1))
            if depth == 0:
                indices = tuple(reversed(indices))
            add(indices, 'front' if depth == 0 else 'rear')

    for depth in range(len(depth_sections) - 1):
        for i in range(section_count - 1):
            add((vertex(depth, i, 0), vertex(depth + 1, i, 0),
                 vertex(depth + 1, i + 1, 0), vertex(depth, i + 1, 0)),
                'outside')
            add((vertex(depth, i + 1, 1), vertex(depth + 1, i + 1, 1),
                 vertex(depth + 1, i, 1), vertex(depth, i, 1)), 'passage')
        # Only the bottoms of the two pillars are sealed.  There is no face
        # between the left and right bottom sections.
        for i in (0, section_count - 1):
            indices = (vertex(depth, i, 0), vertex(depth, i, 1),
                       vertex(depth + 1, i, 1), vertex(depth + 1, i, 0))
            if i == 0:
                indices = tuple(reversed(indices))
            add(indices, 'foot')

    body = c.mesh('SeaRockArch_ContinuousFracturedCore', vertices, faces,
                  root, col, 'MAT_IslandRock', smooth=False)
    for material in ('MAT_IslandCliff', 'ENV_RockStrata', 'ENV_WetRock'):
        body.data.materials.append(c.M[material])
    body.data.update()
    for face, kind in zip(body.data.polygons, face_kinds):
        z = face.center.z
        if z < .46 or kind == 'foot':
            face.material_index = 3
        elif kind in ('front', 'rear') and (
                .52 < z < .85 or 2.37 < z < 2.65 or 3.30 < z < 3.60):
            face.material_index = 2
        elif kind == 'passage' or (kind == 'outside' and face.normal.z < .25):
            face.material_index = 0
        else:
            face.material_index = 1 if kind == 'front' else 0
        face.use_smooth = False
    body['rock_style'] = 'large planar cliffs with angular erosion and strata ledges'
    body['continuous_arch'] = True
    body['passage_open_to_seabed'] = True
    body['foundation_z'] = -1.65
    return body


def build_sea_rock_arch(root, col):
    """Create an 8 m sea arch; root Z=0 is the actual waterline."""
    _arch_core(root, col)
    # These two low fractured footings overlap their respective main piers.
    # They stop well outside the passage and below the visible waterline.
    angular_rock('SeaRockArch_LeftEmbeddedFoot', (-3.08, .02, -1.80),
                 (1.86, 2.76, .86), root, col, seed=417)
    angular_rock('SeaRockArch_RightEmbeddedFoot', (3.14, .04, -1.80),
                 (1.88, 2.70, .92), root, col, seed=418)
    c.empty('WaterlineOrigin', root, col, (0, 0, 0))
    c.empty('PassageOrigin', root, col, (.10, 0, 0))
    c.empty('PlacementOrigin', root, col, (0, 0, -1.80))
    root['waterline_z'] = 0.0
    root['foundation_bottom_z'] = -1.80
    root['placement_notes'] = 'Set root Z at water; embed the two feet into seabed.'
    root['passage_axis'] = 'local Y'
    return {
        'features': [
            'One continuous angular rock arch with a real through passage',
            'Broad flat cliff fractures and three irregular horizontal strata bands',
            'Asymmetric polygonal opening with about 3.5 m clearance at waterline',
            'Wet submerged pillars and two embedded fractured seabed feet',
        ],
        'required_sockets': ['WaterlineOrigin', 'PassageOrigin', 'PlacementOrigin'],
        'dimensions': (8.395, 2.76, 7.90),
        'waterline_z': 0.0,
        'foundation_bottom_z': -1.80,
        'passage_height_above_waterline': 4.30,
        'placement_notes': 'Root Z=0 is the waterline; feet reach Z=-1.8 m.',
    }


def _standalone_quality_check():
    """Run Blender mesh and passage checks without exporting or editing a scene."""
    c.setup()
    col = c.collection('SeaRockArchStandaloneQA')
    root = c.empty('SeaRockArch_QA', None, col)
    metadata = build_sea_rock_arch(root, col)
    c.finish(root)
    reports = []
    points = []
    for ob in c.descendants(root):
        if ob.type != 'MESH':
            continue
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bad_edges = sum(not edge.is_manifold for edge in bm.edges)
        zero_faces = sum(face.calc_area() <= 1e-8 for face in bm.faces)
        volume = bm.calc_volume(signed=True)
        bm.free()
        assert bad_edges == 0, (ob.name, 'non-manifold', bad_edges)
        assert zero_faces == 0, (ob.name, 'zero-area faces', zero_faces)
        assert volume > 0, (ob.name, 'non-positive volume', volume)
        assert all(abs(value) < 1e-8 for value in ob.rotation_euler)
        assert all(abs(value - 1) < 1e-8 for value in ob.scale)
        points.extend(ob.matrix_world @ vertex.co for vertex in ob.data.vertices)
        reports.append({'name': ob.name, 'triangles': len(ob.data.polygons),
                        'non_manifold_edges': bad_edges, 'zero_area_faces': zero_faces,
                        'volume_m3': round(volume, 4)})
    bpy.context.view_layer.update()
    arch_meshes = [ob for ob in c.descendants(root) if ob.type == 'MESH']
    passage_rays = []
    for x, z in ((-1.10, 0), (.10, 0), (1.10, 0),
                 (-1.10, .60), (.10, .60), (1.10, .60),
                 (-1.10, 2.0), (.10, 2.0), (1.10, 2.0),
                 (.10, 3.0), (.10, 4.15)):
        start = Vector((x, -3.0, z))
        direction = Vector((0, 1, 0))
        hit = any(ob.ray_cast(ob.matrix_world.inverted() @ start,
                             ob.matrix_world.inverted().to_3x3() @ direction,
                             distance=6.0)[0] for ob in arch_meshes)
        assert not hit, ('blocked passage', x, z)
        passage_rays.append({'x': x, 'z': z, 'unobstructed': True})
    bounds = {axis: [round(min(p[i] for p in points), 4),
                     round(max(p[i] for p in points), 4)]
              for i, axis in enumerate('xyz')}
    triangle_count = sum(report['triangles'] for report in reports)
    assert triangle_count <= 1500, triangle_count
    print('SEA_ROCK_ARCH_QA=' + json.dumps({
        'triangles': triangle_count, 'meshes': reports,
        'bounds_metres': bounds, 'passage_rays': passage_rays,
        'sockets': metadata['required_sockets'],
    }))


if __name__ == '__main__':
    _standalone_quality_check()
