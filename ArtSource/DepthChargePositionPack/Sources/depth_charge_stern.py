"""Review-only stern rack variant; reuses the approved v6 rack geometry."""
import math

import bpy
from mathutils import Matrix, Vector

import environment_detail_common as c
from miniature_depth_rack_v6 import build_depth_rack, _rail_z


def build_stern_rack(root, col):
    meta = build_depth_rack(root, col)
    bpy.context.view_layer.update()
    objects = {ob['export_name']: ob for ob in c.descendants(root)}
    groups = []
    exit_direction = Vector((0, -1, -.18)).normalized()
    for i, centre in enumerate(meta['stored_charge_centres_blender'], 1):
        name = 'StoredCharge_%02d' % i
        group = c.empty(name, root, col, centre)
        bpy.context.view_layer.update()
        for part_name in ('DepthCharge_%02d' % i, 'DepthCharge_%02d_FuzeHub' % i):
            ob = objects[part_name]
            pose = ob.matrix_world.copy()
            ob.parent = group
            ob.matrix_parent_inverse = Matrix.Identity(4)
            ob.matrix_basis = group.matrix_world.inverted() @ pose
        groups.append(name)
        # Four compatibility sockets map to two physical lane exits. They no
        # longer point from inside the stored barrels as the old v6 sockets did.
        launch = objects['LaunchPoint_%02d' % i]
        x = -.43 if i <= 2 else .43
        launch.location = (x, -.795, _rail_z(-.78)+.240)
        launch.rotation_euler = exit_direction.to_track_quat('Y', 'Z').to_euler()
        launch['stored_charge_group'] = name
        launch['lane'] = 1 if i <= 2 else 2
    for lane, x in enumerate((-.43, .43), 1):
        socket = c.empty('DropPoint_%02d' % lane, root, col,
                         (x, -.795, _rail_z(-.78)+.240))
        socket.rotation_euler = exit_direction.to_track_quat('Y', 'Z').to_euler()
    forward = c.empty('ForwardMarker', root, col, (0, .75, .1))
    c.empty('AttachPoint', root, col, (0, 0, .04))
    meta.update({
        'description': 'Stern gravity rack: two inclined lanes, four separately parented visible barrel charges, release lever and retaining stops',
        'variant': 'stern_drop_rack',
        'placement_rule_not_implemented': 'Rear boundary with an open stern-facing neighbor selects the gravity rack',
        'required_sockets': ['ForwardMarker', 'AttachPoint', 'ReleaseLever']
                            + ['LaunchPoint_%02d' % i for i in range(1, 5)]
                            + ['DropPoint_%02d' % i for i in range(1, 3)] + groups,
        'charge_groups': groups,
        'launch_point_contract': 'Local +Y is the release direction. LaunchPoint_01/02 share lane 1 exit; 03/04 share lane 2 exit. They are root-relative and remain fixed when stored charges animate.',
        'physical_exit_count': 2,
        'stored_charge_count': 4,
        'launch_direction_authoring': list(exit_direction),
        'drop_animation_hint': 'Translate one StoredCharge group aft along its rail, then fall downward; do not move the fixed rack.',
        'release_animation_implemented': False,
        'gameplay_modified': False,
        'maximum_height_m': .9,
    })
    return meta
