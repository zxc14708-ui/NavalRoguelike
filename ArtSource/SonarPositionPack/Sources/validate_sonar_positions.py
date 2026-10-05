"""Independent, model-only QA for the three positional sonar FBX assets.

Run with Blender: blender --factory-startup --background --python
validate_sonar_positions.py -- --output PATH

Expected dimensions, counts, palette and socket contracts come from the pack's
manifest. The checks below additionally enforce the modeling contract without
importing or calling the production builder. Source .blend files are never saved
or modified by this validator.
"""
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix

HERE = Path(__file__).resolve().parent
OUT = HERE / 'SonarPositionPack'
if '--output' in sys.argv:
    OUT = Path(sys.argv[sys.argv.index('--output') + 1]).resolve()

MODEL_IDS = {'MOD_Sonar_Bow', 'MOD_Sonar_TAS', 'MOD_Sonar_Internal'}
POSITION_TOLERANCE = 1e-4


def matrix_close(first, second, tolerance=1e-5):
    return max(abs(a-b) for ra, rb in zip(first, second)
               for a, b in zip(ra, rb)) <= tolerance


def descendants(ob, ancestor):
    parent = ob.parent
    while parent is not None:
        if parent == ancestor:
            return True
        parent = parent.parent
    return False


def world_bounds(meshes):
    points = [ob.matrix_world @ vertex.co
              for ob in meshes for vertex in ob.data.vertices]
    if not points:
        return None
    return {axis: [min(point[i] for point in points),
                   max(point[i] for point in points)]
            for i, axis in enumerate('xyz')}


def component_volumes(bm):
    """Find every edge-connected face component, including disconnected parts."""
    unseen = set(bm.faces)
    volumes = []
    while unseen:
        first = unseen.pop()
        stack, faces = [first], [first]
        while stack:
            face = stack.pop()
            for edge in face.edges:
                for neighbor in edge.link_faces:
                    if neighbor in unseen:
                        unseen.remove(neighbor)
                        stack.append(neighbor)
                        faces.append(neighbor)
        volume = 0.0
        for face in faces:
            a = face.verts[0].co
            for i in range(1, len(face.verts)-1):
                b, c = face.verts[i].co, face.verts[i+1].co
                volume += a.dot(b.cross(c)) / 6.0
        volumes.append(volume)
    return volumes


def exercise_drum(pivot, objects, meshes):
    """Rotate transverse X by 36 degrees and prove only drum visuals move."""
    if pivot is None or pivot.type != 'EMPTY' or pivot.parent is None:
        return {'drum_pivot_present': False}
    poses = {ob: ob.matrix_basis.copy() for ob in objects}
    moving = [ob for ob in meshes if descendants(ob, pivot)]
    fixed = [ob for ob in meshes if not descendants(ob, pivot)]
    before_vertices = {ob: [ob.matrix_world @ v.co for v in ob.data.vertices]
                       for ob in moving}
    before_fixed = {ob: ob.matrix_world.copy() for ob in fixed}
    parent_pose = pivot.parent.matrix_world.copy()
    checks = {}
    try:
        pivot.rotation_euler.x += math.radians(36)
        bpy.context.view_layer.update()
        checks['drum_has_moving_mesh_children'] = bool(moving)
        checks['drum_has_fixed_non_drum_geometry'] = bool(fixed)
        checks['drum_each_child_geometry_really_rotates'] = bool(moving) and all(
            any((ob.matrix_world @ v.co-point).length > .0001
                for v, point in zip(ob.data.vertices, before_vertices[ob]))
            for ob in moving)
        checks['drum_fixed_geometry_stays'] = bool(fixed) and all(
            matrix_close(ob.matrix_world, pose) for ob, pose in before_fixed.items())
        checks['drum_parent_stays'] = matrix_close(pivot.parent.matrix_world, parent_pose)
    finally:
        for ob, pose in poses.items():
            ob.matrix_basis = pose
        bpy.context.view_layer.update()
    checks['drum_neutral_pose_roundtrip_restored'] = all(
        matrix_close(ob.matrix_basis, pose) for ob, pose in poses.items())
    return checks


def validate_asset(entry, manifest):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path = OUT / entry['model_file']
    if not path.is_file():
        return {'id': entry['id'], 'passed': False,
                'errors': ['model_file_missing'], 'path': str(path)}
    bpy.ops.import_scene.fbx(filepath=str(path))
    bpy.context.view_layer.update()
    objects = list(bpy.context.scene.objects)
    roots = [ob for ob in objects if ob.parent is None]
    names = {ob.name: ob for ob in objects}
    meshes = [ob for ob in objects if ob.type == 'MESH']
    checks = {}
    root = roots[0] if len(roots) == 1 else None
    checks['single_centered_identity_empty_root'] = bool(
        root and root.type == 'EMPTY' and root.name == entry['id']+'_Root'
        and matrix_close(root.matrix_world, Matrix.Identity(4)))
    checks['mesh_and_empty_only'] = all(ob.type in ('MESH', 'EMPTY') for ob in objects)
    checks['all_object_scales_one'] = all(
        max(abs(value-1) for value in ob.scale) <= 1e-5 for ob in objects)
    checks['mesh_local_rotations_baked'] = all(
        ob.rotation_euler.to_quaternion().angle <= 1e-5 for ob in meshes)
    checks['no_duplicate_object_names'] = len(names) == len(objects)

    required = list(entry.get('required_sockets', []))
    checks['manifest_required_sockets_unique'] = len(set(required)) == len(required)
    checks['manifest_required_sockets_empty_and_present'] = all(
        name in names and names[name].type == 'EMPTY' for name in required)
    checks['manifest_socket_hierarchy_matches'] = all(
        name in names and names[name].parent is not None
        and names[name].parent.name == parent
        for name, parent in entry.get('socket_hierarchy', {}).items())
    forward = names.get('ForwardMarker')
    origin = names.get('SonarOrigin')
    checks['forward_marker_points_plus_y'] = bool(
        forward and forward.type == 'EMPTY'
        and forward.matrix_world.translation.y > .25
        and abs(forward.matrix_world.translation.x) <= POSITION_TOLERANCE)
    checks['sonar_origin_empty_present'] = bool(origin and origin.type == 'EMPTY')

    triangles = nonmanifold = zero_area = ngons = loose_vertices = component_count = 0
    bad_components, topology_details = [], []
    for ob in meshes:
        ob.data.calc_loop_triangles()
        triangles += len(ob.data.loop_triangles)
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bad_edges = sum(not edge.is_manifold for edge in bm.edges)
        bad_faces = sum(face.calc_area() < 1e-10 for face in bm.faces)
        large_faces = sum(len(face.verts) > 4 for face in bm.faces)
        loose = sum(not vertex.link_faces for vertex in bm.verts)
        volumes = component_volumes(bm)
        nonmanifold += bad_edges
        zero_area += bad_faces
        ngons += large_faces
        loose_vertices += loose
        component_count += len(volumes)
        bad_components.extend({'mesh': ob.name, 'signed_volume': volume}
                              for volume in volumes if volume <= 1e-12)
        if bad_edges or bad_faces or large_faces or loose:
            topology_details.append({'mesh': ob.name, 'nonmanifold_edges': bad_edges,
                                     'zero_area_faces': bad_faces, 'ngons': large_faces,
                                     'loose_vertices': loose})
        bm.free()
    checks['closed_outward_positive_volume_all_components'] = bool(meshes) and not (
        nonmanifold or bad_components)
    checks['no_zero_area_faces'] = zero_area == 0
    checks['no_ngons'] = ngons == 0
    checks['no_loose_vertices'] = loose_vertices == 0
    checks['triangle_count_matches_manifest'] = triangles == entry['triangles']
    checks['triangle_budget_at_most_900'] = triangles <= min(entry.get('budget', 900), 900)
    actual = world_bounds(meshes)
    checks['manifest_bounds_match'] = bool(actual) and all(
        abs(a-b) <= .001 for axis in 'xyz'
        for a, b in zip(actual[axis], entry['bounds'][axis]))
    checks['centered_1_8m_footprint'] = bool(actual) and all(
        actual[axis][0] >= -.90001 and actual[axis][1] <= .90001 for axis in 'xy')
    checks['floor_z_zero'] = bool(actual) and abs(actual['z'][0]) <= 1e-5
    checks['height_at_most_one_metre'] = bool(actual) and actual['z'][1] <= 1.00001

    bad_materials = []
    used_materials = {mat for ob in meshes for mat in ob.data.materials if mat is not None}
    checks['all_mesh_material_slots_valid'] = bool(meshes) and all(
        len(ob.data.materials) > 0 and all(mat is not None for mat in ob.data.materials)
        and all(face.material_index < len(ob.data.materials) for face in ob.data.polygons)
        for ob in meshes)
    palette = manifest['palette']
    actual_palette = {}
    for mat in used_materials:
        expected = palette.get(mat.name)
        shader = next((node for node in mat.node_tree.nodes
                       if node.type == 'BSDF_PRINCIPLED'), None) if mat.use_nodes else None
        rgba = list(shader.inputs['Base Color'].default_value if shader else mat.diffuse_color)
        actual_palette[mat.name] = rgba
        if expected is None or len(expected) != 4 or any(
                abs(a-b) > 1e-4 for a, b in zip(rgba, expected)):
            bad_materials.append({'material': mat.name, 'actual_rgba': rgba,
                                  'expected_rgba': expected})
    checks['shared_naval_palette_matches'] = bool(used_materials) and not bad_materials

    if entry['id'] == 'MOD_Sonar_Bow':
        checks['bow_detection_origin_in_front'] = bool(
            origin and origin.matrix_world.translation.y > .4)
    elif entry['id'] == 'MOD_Sonar_TAS':
        exit_ob, attach_ob, deploy_ob, drum_ob, stowed_ob = (
            names.get(name) for name in ('CableExit', 'TowAttachPoint', 'TowDeployTarget',
                                        'CableDrumPivot', 'StowedTowBody'))
        checks['tas_cable_exit_faces_stern'] = bool(
            exit_ob and exit_ob.type == 'EMPTY' and exit_ob.matrix_world.translation.y < -.6)
        checks['tas_tow_attach_point_faces_stern'] = bool(
            attach_ob and attach_ob.type == 'EMPTY' and attach_ob.matrix_world.translation.y < -.6)
        checks['tas_deploy_target_further_aft'] = bool(
            deploy_ob and deploy_ob.type == 'EMPTY' and exit_ob
            and deploy_ob.matrix_world.translation.y < exit_ob.matrix_world.translation.y)
        checks['tas_drum_neutral_local_rotation'] = bool(
            drum_ob and drum_ob.type == 'EMPTY'
            and drum_ob.rotation_euler.to_quaternion().angle <= 1e-5)
        checks['tas_stowed_body_is_separate_editable_empty'] = bool(
            stowed_ob and stowed_ob.type == 'EMPTY' and stowed_ob.parent == root
            and drum_ob and not descendants(stowed_ob, drum_ob))
        checks['tas_stowed_body_has_mesh_descendants'] = bool(stowed_ob) and any(
            descendants(ob, stowed_ob) for ob in meshes)
        checks['tas_winch_has_separate_fixed_meshes'] = bool(stowed_ob and drum_ob) and any(
            not descendants(ob, stowed_ob) and not descendants(ob, drum_ob) for ob in meshes)
        checks.update(exercise_drum(drum_ob, objects, meshes))

    errors = [name for name, passed in checks.items() if not passed]
    row = {'id': entry['id'], 'passed': not errors, 'errors': errors,
           'checks': checks, 'triangles': triangles, 'budget': min(entry.get('budget', 900), 900),
           'bounds': actual, 'mesh_count': len(meshes), 'components': component_count,
           'nonmanifold_edges': nonmanifold, 'zero_area_faces': zero_area,
           'ngons': ngons, 'loose_vertices': loose_vertices,
           'inward_or_zero_volume_components': bad_components,
           'topology_details': topology_details, 'bad_material_colors': bad_materials,
           'actual_palette': actual_palette,
           'socket_positions': {name: list(names[name].matrix_world.translation)
                                for name in required if name in names}}
    print('VALIDATED', entry['id'], not errors, errors, flush=True)
    return row


def main():
    manifest = json.loads((OUT / 'manifest.json').read_text(encoding='utf-8'))
    entries = manifest.get('assets', [])
    pack_checks = {
        'exactly_three_expected_assets': len(entries) == 3
        and {entry['id'] for entry in entries} == MODEL_IDS,
        'all_model_files_use_unique_paths': len({entry['model_file'] for entry in entries}) == len(entries),
        'manifest_declares_model_only': manifest.get('game_applied', False) is False,
    }
    rows = []
    for entry in entries:
        try:
            rows.append(validate_asset(entry, manifest))
        except Exception as exc:
            rows.append({'id': entry.get('id', '<missing>'), 'passed': False,
                         'errors': ['validation_exception'], 'exception': repr(exc)})
            print('VALIDATION_EXCEPTION', entry.get('id'), repr(exc), flush=True)
    report = {'all_passed': bool(rows) and all(pack_checks.values())
              and all(row['passed'] for row in rows),
              'count': len(rows), 'validated_models': len(rows),
              'game_applied': False, 'unity_tested': False,
              'qa_method': 'Independent fresh FBX import, topology, palette, placement and X-axis drum articulation',
              'pack_checks': pack_checks, 'results': rows}
    (OUT / 'validation.json').write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'all_passed': report['all_passed'], 'count': len(rows),
                      'failed': [row['id'] for row in rows if not row['passed']]}, ensure_ascii=False), flush=True)
    if not report['all_passed']:
        raise SystemExit(2)


if __name__ == '__main__':
    main()
