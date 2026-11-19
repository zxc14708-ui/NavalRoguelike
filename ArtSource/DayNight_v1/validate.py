"""Check the exported FBX files and optionally prepare the Unity import contract."""
from pathlib import Path
import json
import sys

import bpy
from mathutils import Vector

OUT=Path(__file__).resolve().parent
DIRECTIONAL={'Exhaust','BlastPoint','SmokePoint','FirePoint','FlarePoint','LampPoint',
             'TorpedoLaunchPoint_Port','TorpedoLaunchPoint_Starboard'}


def round_trip(name, expected):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(OUT/'Models'/f'{name}.fbx'))
    bpy.context.view_layer.update()
    objects={ob.name:ob for ob in bpy.context.scene.objects}
    assert set(objects)=={p['name'] for p in expected['parts']}, name
    for part in expected['parts']:
        ob=objects[part['name']]
        assert ob.type==part['type'], (name,ob.name)
        assert (ob.parent.name if ob.parent else None)==part['parent'], (name,ob.name)
        assert (ob.matrix_world.translation-Vector(part['position'])).length<.001, (name,ob.name)
        if ob.name in DIRECTIONAL:
            direction=Vector(part['direction']).normalized()
            assert ob.matrix_world.to_3x3().col[2].normalized().dot(direction)>.99995, (name,ob.name)
    meshes=[ob for ob in objects.values() if ob.type=='MESH']
    tris=sum(len(f.vertices)-2 for ob in meshes for f in ob.data.polygons)
    assert tris==expected['triangles'], name
    assert all(f.area>1e-10 for ob in meshes for f in ob.data.polygons), name
    assert sorted({mat.name for ob in meshes for mat in ob.data.materials})==expected['materials'],name
    points=[ob.matrix_world@v.co for ob in meshes for v in ob.data.vertices]
    for fn,bounds in zip((min,max),expected['bounds']):
        assert all(abs(fn(p[i] for p in points)-bounds[i])<.001 for i in range(3)),name
    return f'PASS {name} | {tris} tris | {len(objects)} nodes'


def tabular_contract(manifest,folder):
    rows=[]
    for name,spec in manifest.items():
        lo,hi=spec['bounds']
        rows.append((name,'@bounds',csv(lo),csv(hi),str(spec['triangles']),'|'.join(spec['materials'])))
        for part in spec['parts']:
            rows.append((name,part['name'],part['parent'] or '',part['type'],
                         csv(part['position']),csv(part['direction']),
                         'direction' if part['name'] in DIRECTIONAL else 'position'))
    (folder/'expected.tsv').write_text('\n'.join('\t'.join(row) for row in rows)+'\n',encoding='utf8')


def csv(values):
    return ','.join(f'{number:.7g}' for number in values)


if __name__=='__main__':
    manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf8'))
    report=['Blender 5.2 FBX round trip. Exact part names, types, hierarchy, axes, geometry and material names.']
    for name,spec in manifest.items():
        report.append(round_trip(name,spec))
    (OUT/'BlenderValidation.txt').write_text('\n'.join(report)+'\n',encoding='utf8')
    print('\n'.join(report))
    if '--' in sys.argv:
        tabular_contract(manifest,Path(sys.argv[sys.argv.index('--')+1]))

