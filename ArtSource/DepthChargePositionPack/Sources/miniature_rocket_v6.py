"""Paint-only reconstruction of the CURRENT eight-tube Unity primitive prefab.

This is not the different twelve-tube v5 design. The source YAML is read only.
Cube dimensions, cylinder envelope, sockets and parent relationships are
preserved; primitive scales are baked into meshes for unit-scale FBX export.
Unity's built-in cylinder topology is recreated at 20 sides, not extracted
bit-for-bit from the editor's internal mesh. No gameplay component is exported.
"""
import hashlib
import re
from pathlib import Path

import environment_detail_common as c

DEFAULT_PREFAB = Path('C:/Users/최병욱/UnityProjects/NavalRoguelike/NavalRoguelike/'
                      'Assets/_Game/Prefabs/Modules/MOD_RocketLauncher.prefab')


def _values(body, key, axes):
    match = re.search(r'^  '+key+r': \{([^}]+)\}', body, re.M)
    if not match:
        raise ValueError('Missing '+key)
    values = dict(re.findall(r'(\w+): ([^,}]+)', match.group(1)))
    return [float(values[axis]) for axis in axes]


def audit_current_prefab(path=DEFAULT_PREFAB):
    raw = path.read_bytes()
    blocks = {int(identifier): (int(kind), body) for kind, identifier, body in
              re.findall(r'^--- !u!(\d+) &(\d+)\r?\n(.*?)(?=^--- !u!|\Z)',
                         raw.decode('utf-8-sig'), re.M | re.S)}
    game_objects, transforms, meshes = {}, {}, {}
    for identifier, (kind, body) in blocks.items():
        if kind == 1:
            name = re.search(r'^  m_Name: (.+)$', body, re.M).group(1).strip()
            game_objects[identifier] = name
        elif kind == 4:
            transforms[identifier] = {
                'game_object': int(re.search(r'm_GameObject: \{fileID: (\d+)', body).group(1)),
                'father': int(re.search(r'm_Father: \{fileID: (\d+)', body).group(1)),
                'unity_local_position': _values(body, 'm_LocalPosition', 'xyz'),
                'unity_local_scale': _values(body, 'm_LocalScale', 'xyz'),
                'unity_local_rotation': _values(body, 'm_LocalRotation', 'xyzw'),
            }
        elif kind == 33:
            owner = int(re.search(r'm_GameObject: \{fileID: (\d+)', body).group(1))
            mesh = re.search(r'm_Mesh: \{fileID: (\d+), guid: (\w+)', body)
            if not mesh or mesh.group(2) != '0000000000000000e000000000000000':
                raise ValueError('Current rocket is no longer a built-in primitive prefab')
            primitive = {10202: 'Cube', 10206: 'Cylinder'}.get(int(mesh.group(1)))
            if not primitive:
                raise ValueError('Unsupported current rocket primitive')
            meshes[owner] = primitive
    objects = []
    for transform in transforms.values():
        if transform['unity_local_rotation'] != [0., 0., 0., 1.]:
            raise ValueError('Source rotation changed: inspect before reconstruction')
        parent = transforms.get(transform['father'])
        objects.append({
            'name': game_objects[transform['game_object']],
            'parent': game_objects[parent['game_object']] if parent else None,
            'primitive': meshes.get(transform['game_object']),
            **{key: value for key, value in transform.items() if key.startswith('unity_')},
        })
    names = [item['name'] for item in objects]
    if len(names) != len(set(names)):
        raise ValueError('Ambiguous current prefab names')
    if sorted(n for n in names if n.startswith('LaunchPoint_')) != [
            'LaunchPoint_%02d' % i for i in range(1, 9)]:
        raise ValueError('Current eight-tube contract changed; do not silently redesign')
    source_materials = {}
    for material_name in ('MAT_hull', 'MAT_gun'):
        material_path = path.parents[2]/'Art'/(material_name+'.mat')
        if material_path.is_file():
            material_body = material_path.read_text(encoding='utf-8-sig')
            color = re.search(r'_BaseColor: \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)', material_body)
            smooth = re.search(r'- _Smoothness: ([^\r\n]+)', material_body)
            metal = re.search(r'- _Metallic: ([^\r\n]+)', material_body)
            if color and smooth and metal:
                source_materials[material_name] = {
                    'base_color': [float(x) for x in color.groups()],
                    'roughness': 1.-float(smooth.group(1)),
                    'metallic': float(metal.group(1)),
                }
    return {'prefab_path': str(path), 'source_sha256': hashlib.sha256(raw).hexdigest(),
            'object_count': len(objects), 'objects': objects,
            'source_materials': source_materials,
            'scope': 'Visual geometry and socket hierarchy only; no Unity components or scripts copied',
            'cylinder_topology_note': '20-sided reconstruction of Unity cylinder envelope, not editor mesh extraction'}


def unity_to_blender(values):
    return values[0], values[2], values[1]


def build_current_rocket(root, col, audit):
    source = {item['name']: item for item in audit['objects']}
    original_root = next(item['name'] for item in audit['objects'] if item['parent'] is None)
    made = {original_root: root}
    pending = [item for item in audit['objects'] if item['parent'] is not None]
    while pending:
        progress = False
        for item in list(pending):
            if item['parent'] not in made:
                continue
            name, primitive = item['name'], item['primitive']
            parent = made[item['parent']]
            loc = unity_to_blender(item['unity_local_position'])
            dims = unity_to_blender(item['unity_local_scale'])
            if primitive == 'Cube':
                mat = 'Gunmetal' if name.startswith('Tube_') else (
                    'Deck Grey' if name == 'Deck' else 'Naval Blue Grey')
                ob = c.box(name, loc, dims, parent, col, mat)
                # Surface paint only: darker pod underside, pale outer/top
                # surfaces. No bevel, decal geometry, extra tubes or supports.
                if name.startswith('Pod_'):
                    ob.data.materials.append(c.M['Deck Grey'])
                    ob.data.polygons[0].material_index = 1
            elif primitive == 'Cylinder':
                sx, sy, sz = item['unity_local_scale']
                if abs(sx-sz) > 1e-7:
                    raise ValueError('Elliptic source ring needs an explicit reconstruction')
                ob = c.cyl(name, loc, sx*.5, sy*2., parent, col,
                           'Gunmetal', segments=20)
            else:
                if dims != (1., 1., 1.):
                    raise ValueError('Unexpected scaled socket: '+name)
                ob = c.empty(name, parent, col, loc)
            ob['source_prefab_name'] = name
            made[name] = ob
            pending.remove(item)
            progress = True
        if not progress:
            raise ValueError('Unresolved/cyclic source prefab hierarchy')
    return {
        'description': 'Current twin 2x2 pod launcher; miniature matte paint only',
        'budget': 900, 'anchor': 'floor_z_0', 'launch_tube_count': 8,
        'features': ['Existing twin rectangular pods, four tube mouths each',
                     'Original deck, ring and side-post dimensions',
                     'Original yaw and elevation pivot positions',
                     'Eight unchanged launch sockets and centre Muzzle',
                     'Warm pale-grey matte paint and dark tube mouths'],
        'geometry_scope': 'Primitive shape envelopes, positions, hierarchy and eight-mouth count preserved',
        'source_prefab_sha256': audit['source_sha256'],
        'source_type': 'Current serialized Unity Cube/Cylinder fallback prefab',
        'not_used': 'v5 twelve-tube redesigned launcher',
        'forward_blender': [0, 1, 0], 'game_applied': False,
    }
