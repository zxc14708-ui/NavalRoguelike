"""Independent FBX QA for the review-only depth-charge rack/projector pack.

Run with Blender --factory-startup --background --python this_file.py.
Optional: -- --output PATH. No Unity files or source .blend files are modified.
"""
import json
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
OUT = HERE / 'DepthChargePositionPack'
if '--output' in sys.argv:
    OUT = Path(sys.argv[sys.argv.index('--output') + 1]).resolve()
MODEL_IDS = {'MOD_DepthChargeRack', 'MOD_DepthChargeProjector'}


def matrix_close(a, b, tolerance=1e-5):
    return max(abs(x-y) for ra, rb in zip(a, b) for x, y in zip(ra, rb)) <= tolerance


def descendants(ob, ancestor):
    parent = ob.parent
    while parent is not None:
        if parent == ancestor:
            return True
        parent = parent.parent
    return False


def bounds(meshes):
    points = [ob.matrix_world @ vertex.co for ob in meshes for vertex in ob.data.vertices]
    if not points:
        return None
    return {axis: [min(point[i] for point in points), max(point[i] for point in points)]
            for i, axis in enumerate('xyz')}


def component_volumes(bm):
    unseen, volumes = set(bm.faces), []
    while unseen:
        first = unseen.pop()
        stack, component = [first], [first]
        while stack:
            face = stack.pop()
            for edge in face.edges:
                for neighbor in edge.link_faces:
                    if neighbor in unseen:
                        unseen.remove(neighbor)
                        stack.append(neighbor)
                        component.append(neighbor)
        volume = 0.0
        for face in component:
            a = face.verts[0].co
            for i in range(1, len(face.verts)-1):
                volume += a.dot(face.verts[i].co.cross(face.verts[i+1].co)) / 6.0
        volumes.append(volume)
    return volumes


def charge_group_names(entry):
    features = entry.get('features', {})
    candidates = features.get('charge_groups', []) if isinstance(features, dict) else []
    candidates = candidates or entry.get('charge_groups', [])
    return [item if isinstance(item, str) else item.get('name', item.get('id', ''))
            for item in candidates]


def exercise_charge(group, objects, meshes):
    """Move one detachable charge and prove all surrounding geometry stays put."""
    if group is None or group.type != 'EMPTY':
        return {'group_present': False}
    poses = {ob: ob.matrix_basis.copy() for ob in objects}
    moving = [ob for ob in meshes if descendants(ob, group)]
    fixed = [ob for ob in meshes if not descendants(ob, group)]
    before = {ob: [ob.matrix_world @ v.co for v in ob.data.vertices] for ob in moving}
    fixed_before = {ob: ob.matrix_world.copy() for ob in fixed}
    try:
        group.location += Vector((.13, -.19, .21))
        bpy.context.view_layer.update()
        checks = {
            'moving_mesh_children': bool(moving),
            'fixed_non_charge_meshes': bool(fixed),
            'every_child_mesh_really_moves': bool(moving) and all(
                any((ob.matrix_world @ v.co-point).length > .01
                    for v, point in zip(ob.data.vertices, before[ob])) for ob in moving),
            'fixed_geometry_stays': bool(fixed) and all(
                matrix_close(ob.matrix_world, pose) for ob, pose in fixed_before.items()),
        }
    finally:
        for ob, pose in poses.items():
            ob.matrix_basis = pose
        bpy.context.view_layer.update()
    checks['neutral_pose_restored'] = all(
        matrix_close(ob.matrix_basis, pose) for ob, pose in poses.items())
    return checks


def socket_direction(ob):
    return (ob.matrix_world.to_3x3() @ Vector((0, 1, 0))).normalized()


def validate(entry, manifest):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path = OUT / entry['model_file']
    if not path.is_file():
        return {'id': entry['id'], 'passed': False, 'errors': ['model_file_missing']}
    bpy.ops.import_scene.fbx(filepath=str(path))
    bpy.context.view_layer.update()
    objects = list(bpy.context.scene.objects)
    roots = [ob for ob in objects if ob.parent is None]
    root = roots[0] if len(roots) == 1 else None
    meshes = [ob for ob in objects if ob.type == 'MESH']
    names = {ob.name: ob for ob in objects}
    checks = {
        'single_centered_identity_empty_root': bool(
            root and root.type == 'EMPTY' and root.name == entry['id']+'_Root'
            and matrix_close(root.matrix_world, Matrix.Identity(4))),
        'mesh_empty_only': all(ob.type in ('MESH', 'EMPTY') for ob in objects),
        'object_scales_one': all(max(abs(value-1) for value in ob.scale) <= 1e-5 for ob in objects),
        'mesh_rotations_baked': all(ob.rotation_euler.to_quaternion().angle <= 1e-5 for ob in meshes),
        'unique_object_names': len(names) == len(objects),
    }
    required = entry.get('required_sockets', [])
    checks['unique_required_socket_names'] = len(set(required)) == len(required)
    checks['required_sockets_empty_present'] = all(
        name in names and names[name].type == 'EMPTY' for name in required)
    checks['required_socket_hierarchy_matches'] = all(
        name in names and names[name].parent is not None and names[name].parent.name == parent
        for name, parent in entry.get('socket_hierarchy', {}).items())
    forward, attach = names.get('ForwardMarker'), names.get('AttachPoint')
    checks['forward_marker_plus_y'] = bool(
        forward and forward.type == 'EMPTY' and forward.matrix_world.translation.y > .25
        and abs(forward.matrix_world.translation.x) <= 1e-4)
    checks['attach_point_empty_present'] = bool(attach and attach.type == 'EMPTY')

    triangles = nonmanifold = zero_area = ngons = loose_vertices = components = 0
    bad_components, topology = [], []
    for ob in meshes:
        ob.data.calc_loop_triangles()
        triangles += len(ob.data.loop_triangles)
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        edges = sum(not edge.is_manifold for edge in bm.edges)
        zeros = sum(face.calc_area() < 1e-10 for face in bm.faces)
        large = sum(len(face.verts) > 4 for face in bm.faces)
        loose = sum(not vertex.link_faces for vertex in bm.verts)
        volumes = component_volumes(bm)
        nonmanifold += edges
        zero_area += zeros
        ngons += large
        loose_vertices += loose
        components += len(volumes)
        bad_components.extend({'mesh': ob.name, 'signed_volume': volume}
                              for volume in volumes if volume <= 1e-12)
        if edges or zeros or large or loose:
            topology.append({'mesh': ob.name, 'nonmanifold': edges, 'zero_area': zeros,
                             'ngons': large, 'loose_vertices': loose})
        bm.free()
    actual = bounds(meshes)
    checks.update({
        'all_components_closed_outward_positive': bool(meshes) and not (nonmanifold or bad_components),
        'no_zero_area_faces': zero_area == 0,
        'no_ngons': ngons == 0,
        'no_loose_vertices': loose_vertices == 0,
        'triangle_count_matches_manifest': triangles == entry['triangles'],
        'triangle_budget_900': triangles <= min(entry.get('budget', 900), 900),
        'manifest_bounds_match': bool(actual) and all(
            abs(a-b) <= .001 for axis in 'xyz' for a, b in zip(actual[axis], entry['bounds'][axis])),
        'centered_1_8m_footprint': bool(actual) and all(
            actual[axis][0] >= -.90001 and actual[axis][1] <= .90001 for axis in 'xy'),
        'floor_zero': bool(actual) and abs(actual['z'][0]) <= 1e-5,
        'height_at_most_0_9m': bool(actual) and actual['z'][1] <= .90001,
    })
    bad_materials = []
    used_materials = {mat for ob in meshes for mat in ob.data.materials if mat is not None}
    checks['valid_mesh_material_slots'] = bool(meshes) and all(
        len(ob.data.materials) > 0 and all(mat is not None for mat in ob.data.materials)
        and all(face.material_index < len(ob.data.materials) for face in ob.data.polygons) for ob in meshes)
    for mat in used_materials:
        expected = manifest['palette'].get(mat.name)
        shader = next((node for node in mat.node_tree.nodes if node.type == 'BSDF_PRINCIPLED'), None) \
            if mat.use_nodes else None
        rgba = list(shader.inputs['Base Color'].default_value if shader else mat.diffuse_color)
        if expected is None or len(expected) != 4 or any(abs(a-b) > 1e-4 for a, b in zip(rgba, expected)):
            bad_materials.append({'material': mat.name, 'actual': rgba, 'expected': expected})
    checks['shared_palette_matches'] = bool(used_materials) and not bad_materials

    groups = charge_group_names(entry)
    checks['manifest_charge_groups_present_unique'] = bool(groups) and len(set(groups)) == len(groups)
    checks['independent_charge_group_roots'] = all(
        name in names and names[name].type == 'EMPTY' and names[name].parent == root for name in groups)
    checks['fixed_structure_outside_all_charge_groups'] = bool(groups) and any(
        all(not descendants(ob, names[name]) for name in groups if name in names) for ob in meshes)
    for name in groups:
        for key, value in exercise_charge(names.get(name), objects, meshes).items():
            checks[name+'_'+key] = value

    launches = [ob for name, ob in names.items() if name.startswith('LaunchPoint_')]
    checks['launch_sockets_present_empty_root_children'] = bool(launches) and all(
        ob.type == 'EMPTY' and ob.parent == root for ob in launches)
    directions = {ob.name: list(socket_direction(ob)) for ob in launches}
    features = entry.get('features', {})
    launch_direction = features.get('launch_direction_authoring') if isinstance(features, dict) else None
    checks['declared_launch_direction_matches_sockets'] = bool(launches and launch_direction) and all(
        socket_direction(ob).dot(Vector(launch_direction).normalized()) >= .999 for ob in launches)
    if entry['id'] == 'MOD_DepthChargeRack':
        checks['rack_four_stored_charge_groups'] = set(groups) == {
            'StoredCharge_%02d' % i for i in range(1, 5)}
        checks['rack_four_release_sockets'] = {ob.name for ob in launches} == {
            'LaunchPoint_%02d' % i for i in range(1, 5)}
        checks['rack_release_sockets_at_stern'] = bool(launches) and all(
            ob.matrix_world.translation.y < -.6 for ob in launches)
        checks['rack_release_directions_rearward_down'] = bool(launches) and all(
            socket_direction(ob).y < -.2 and socket_direction(ob).z < -.03 for ob in launches)
        drops = [ob for name, ob in names.items() if name.startswith('DropPoint_')]
        checks['rack_optional_drop_points_stern_empty'] = all(
            ob.type == 'EMPTY' and ob.parent == root and ob.matrix_world.translation.y < -.6 for ob in drops)
        exit_positions = [ob.matrix_world.translation for ob in launches]
        unique_exits = {tuple(round(value, 5) for value in point) for point in exit_positions}
        checks['rack_two_independent_physical_exits'] = len(unique_exits) == 2
        for first, second in (('LaunchPoint_01', 'LaunchPoint_02'), ('LaunchPoint_03', 'LaunchPoint_04')):
            checks['rack_'+first+'_'+second+'_share_lane_exit'] = bool(
                first in names and second in names
                and (names[first].matrix_world.translation-names[second].matrix_world.translation).length < 1e-5)
    elif entry['id'] == 'MOD_DepthChargeProjector':
        checks['projector_loaded_charge_names'] = bool(groups) and all(
            name.startswith('LoadedCharge_') for name in groups)
        checks['projector_release_direction_outboard_up'] = bool(launches) and all(
            socket_direction(ob).x > .1 and socket_direction(ob).z > .1 for ob in launches)
        checks['projector_release_sockets_outboard'] = bool(launches) and all(
            ob.matrix_world.translation.x > .1 for ob in launches)
        outboard = names.get('OutboardDirection')
        checks['projector_outboard_direction_plus_x'] = bool(
            outboard and outboard.type == 'EMPTY'
            and socket_direction(outboard).dot(Vector((1, 0, 0))) >= .999)
        checks['projector_release_sockets_match_charge_count'] = len(launches) == len(groups)
        if features.get('launch_socket_basis') == 'center_of_loaded_charge':
            for name in groups:
                launch_name = 'LaunchPoint_'+name.rsplit('_', 1)[-1]
                checks['projector_'+name+'_release_at_charge_center'] = bool(
                    name in names and launch_name in names
                    and (names[name].matrix_world.translation
                         -names[launch_name].matrix_world.translation).length < 1e-4)

    errors = [name for name, passed in checks.items() if not passed]
    row = {'id': entry['id'], 'passed': not errors, 'errors': errors, 'checks': checks,
           'triangles': triangles, 'budget': min(entry.get('budget', 900), 900), 'bounds': actual,
           'mesh_count': len(meshes), 'components': components, 'nonmanifold_edges': nonmanifold,
           'zero_area_faces': zero_area, 'ngons': ngons, 'loose_vertices': loose_vertices,
           'bad_components': bad_components, 'topology_details': topology,
           'bad_materials': bad_materials, 'charge_groups': groups,
           'release_directions_authoring_axes': directions,
           'socket_positions': {name: list(names[name].matrix_world.translation)
                                for name in required if name in names}}
    print('VALIDATED', entry['id'], not errors, errors, flush=True)
    return row


def main():
    manifest = json.loads((OUT / 'manifest.json').read_text(encoding='utf-8'))
    entries = manifest.get('assets', [])
    pack_checks = {
        'exactly_two_expected_assets': len(entries) == 2 and {entry['id'] for entry in entries} == MODEL_IDS,
        'unique_model_file_paths': len({entry['model_file'] for entry in entries}) == len(entries),
        'declared_model_only': manifest.get('game_applied', False) is False,
    }
    rows = []
    for entry in entries:
        try:
            rows.append(validate(entry, manifest))
        except Exception as exc:
            rows.append({'id': entry.get('id', '<missing>'), 'passed': False,
                         'errors': ['validation_exception'], 'exception': repr(exc)})
            print('VALIDATION_EXCEPTION', entry.get('id'), repr(exc), flush=True)
    report = {'all_passed': bool(rows) and all(pack_checks.values()) and all(row['passed'] for row in rows),
              'count': len(rows), 'validated_models': len(rows), 'game_applied': False, 'unity_tested': False,
              'qa_method': 'Independent fresh FBX import, component topology, palette, placement, launch direction and detachable charge motion',
              'pack_checks': pack_checks, 'results': rows}
    (OUT / 'validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'all_passed': report['all_passed'], 'count': len(rows),
                      'failed': [row['id'] for row in rows if not row['passed']]}, ensure_ascii=False), flush=True)
    if not report['all_passed']:
        raise SystemExit(2)


if __name__ == '__main__':
    main()
