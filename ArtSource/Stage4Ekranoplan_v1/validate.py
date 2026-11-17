"""Blender FBX round trip and generation of the isolated Unity contract."""
import json
from pathlib import Path
import re
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parent


def make_unity_contract(manifest, directory):
    rows=[]
    for model,spec in manifest.items():
        lo,hi=spec['bounds_blender_m']
        rows.append((model,'@bounds',csv(lo),csv(hi),str(spec['triangles']),'|'.join(spec['materials'])))
        for p in spec['parts']:
            directional=re.search('LaunchPoint|Muzzle|Exhaust|Intake|BoatExit|Mooring|FlareTip',p['name'])
            rows.append((model,p['name'],p['parent'] or '',p['type'],csv(p['position_blender_m']),
                         csv(p['axis_blender']),'direction' if directional else 'position'))
    (directory/'expected.tsv').write_text('\n'.join('\t'.join(row) for row in rows)+'\n',encoding='utf-8')


def csv(values):
    return ','.join(f'{number:.7g}' for number in values)


def redness(meshes,bounds):
    low,high=bounds
    points,faces,red=[],[],[]
    for ob in meshes:
        offset=len(points)
        points.extend(ob.matrix_world@v.co for v in ob.data.vertices)
        faces.extend(tuple(v+offset for v in f.vertices) for f in ob.data.polygons)
        red.extend(ob.data.materials[f.material_index].name in {'Warning red','Emergency Red'}
                   for f in ob.data.polygons)
    tree=BVHTree.FromPolygons(points,faces)
    hit=colored=0
    for y in range(300):
        for x in range(180):
            origin=(low[0]+(x+.5)*(high[0]-low[0])/180,
                    low[1]+(y+.5)*(high[1]-low[1])/300,high[2]+1)
            _,_,index,_=tree.ray_cast(Vector(origin),Vector((0,0,-1)))
            if index is not None:
                hit+=1
                colored+=red[index]
    return 100*colored/hit


def validate(manifest):
    report=['Blender 5.2 FBX import. Units are meters, Blender axes.']
    for model,spec in manifest.items():
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(ROOT/'Models'/f'{model}.fbx'))
        scene=bpy.context.scene
        bpy.context.view_layer.update()
        meshes=[o for o in scene.objects if o.type=='MESH']
        tris=sum(len(f.vertices)-2 for o in meshes for f in o.data.polygons)
        assert tris==spec['triangles'], (model,tris,spec['triangles'])
        assert all(f.area>1e-10 for o in meshes for f in o.data.polygons), (model,'zero-area face')
        assert all(o.data.materials for o in meshes), (model,'missing material slots')
        for p in spec['parts']:
            ob=scene.objects.get(p['name'])
            assert ob is not None and ob.type==p['type'], (model,p['name'],p['type'])
            assert (ob.parent.name if ob.parent else None)==p['parent'], (model,p['name'],'parent')
            assert (ob.matrix_world.translation-Vector(p['position_blender_m'])).length < .002, (model,p['name'],'origin')
        line=f'PASS {model} | {tris} tris | {len(spec["parts"])} nodes'
        if model=='ENE_Boss4_Lun':
            percentage=redness(meshes,spec['bounds_blender_m'])
            line+=f' | overhead red coverage {percentage:.2f}%'
            assert 5<=percentage<=10,(model,'overhead red coverage',percentage)
        report.append(line)
    (ROOT/'BlenderValidation.txt').write_text('\n'.join(report)+'\n',encoding='utf-8')
    print('\n'.join(report))


if __name__=='__main__':
    spec=json.loads((ROOT/'manifest.json').read_text(encoding='utf-8'))
    args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    if args:
        make_unity_contract(spec,Path(args[0]))
    validate(spec)

