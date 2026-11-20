"""Check exported props, retained launcher sockets, and intact island plateaus."""
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

OUT=Path(__file__).resolve().parent
MIL=OUT.parents[1]/'Assets/_Game/Resources/Environment/Military'
DIRECTED={'ShoreConnection','MooringPoint_Port','MooringPoint_Starboard','PierEnd',
          'LandingSpot','BeamOrigin','BackblastPoint_01','BackblastPoint_02'}


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    bpy.context.view_layer.update()
    return bpy.context.scene.objects


def plateau(objects):
    def points(z_min):
        return {tuple(round(value,3) for value in p)
                for mesh in objects if mesh.type=='MESH'
                for vertex in mesh.data.vertices
                if (p := mesh.matrix_world @ vertex.co).z >= z_min}
    return points(.899)


def shoreline(objects):
    return sorted(round(p.xy.length,3) for mesh in objects if mesh.type=='MESH'
                  for vertex in mesh.data.vertices
                  if abs((p := mesh.matrix_world @ vertex.co).z+.9)<.001)


def check_hatches(objects, original):
    for name, pose in original.items():
        for a,b in zip(objects[name].matrix_world,pose):
            assert (a-b).length<.001,(name,'original transform')
    elevation=objects['ElevationPivot']
    for i in ('01','02'):
        marker=objects['BackblastPoint_'+i]
        assert marker.parent == elevation
        assert marker.matrix_world.to_3x3().col[2].normalized().dot(Vector((0,1,0)))>.999
        hatch=objects['LauncherHatch_'+i]
        pivot=objects['LauncherHatchPivot_'+i]
        assert hatch.parent==pivot and pivot.parent==elevation
        muzzle=objects['LaunchPoint_'+i]

        def occludes(ang):
            pivot.rotation_euler.x=ang
            bpy.context.view_layer.update()
            mesh = BVHTree.FromPolygons([hatch.matrix_world @ v.co for v in hatch.data.vertices],
                                        [tuple(f.vertices) for f in hatch.data.polygons])
            p = muzzle.matrix_world.translation + Vector((0, .60, 0))
            return mesh.ray_cast(p,Vector((0,-1,0)),1.2)[0] is not None

        assert occludes(0),(i,'hatch should cover opening')
        assert not occludes(1.92),(i,'open hatch should clear opening')


def csv(value):
    return ','.join(f'{number:.7g}' for number in value)


def main():
    spec=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
    available_mats={file.stem for file in (OUT.parents[1]/'Assets/_Game').rglob('*.mat')}
    rows,report=[],[]
    assert {file.stem for file in (OUT/'Models').glob('*.fbx')}==set(spec)
    original=load(MIL/'MIL_MissileBattery_2.fbx')
    sockets={name:original[name].matrix_world.copy() for name in
             ['TurretPivot','ElevationPivot','LaunchPoint_01','LaunchPoint_02','Muzzle']}
    for name,model in spec.items():
        base_terrain=None
        if name.startswith('MIL_Island_'):
            base_terrain=load(MIL/(name[:-2]+'.fbx'))
            ground,rim=plateau(base_terrain),shoreline(base_terrain)
        objects=load(OUT/'Models'/(name+'.fbx'))
        assert [ob.name for ob in objects if ob.parent is None]==[name]
        assert len(objects)==len(model['parts'])
        meshes=[ob for ob in objects if ob.type=='MESH']
        triangles=sum(len(f.vertices)-2 for ob in meshes for f in ob.data.polygons)
        assert triangles==model['triangles'],name
        assert all(f.area>1e-10 for ob in meshes for f in ob.data.polygons),name
        mats={mat.name for ob in meshes for mat in ob.data.materials}
        assert mats==set(model['materials']) and mats<=available_mats,(name,mats-available_mats)
        lo,hi=model['bounds_blender_m']
        rows.append((name,'@bounds',csv(lo),csv(hi),str(triangles),'|'.join(sorted(mats))))
        for part in model['parts']:
            ob=objects[part['name']]
            assert ob.type==part['type']
            assert (ob.parent.name if ob.parent else None)==part['parent']
            x,y,z=part['position_blender_m']
            assert (ob.matrix_world.translation-Vector((x,y,z))).length<.003,(name,ob.name)
            if ob.name in DIRECTED:
                x,y,z=part['axis_blender']
                assert ob.matrix_world.to_3x3().col[2].normalized().dot(Vector((x,y,z)))>.999,(name,ob.name)
            rows.append((name,ob.name,part['parent'] or '',ob.type,csv(part['position_blender_m']),
                        csv(part['axis_blender']),'direction' if ob.name in DIRECTED else 'position'))
        if base_terrain is not None:
            assert plateau(objects)==ground,(name,'flat plateau geometry changed')
            changed=shoreline(objects)
            assert len(changed)==len(rim) and changed!=rim,(name,'shoreline')
            assert .86<max(changed)/max(rim)<1.15,(name,'shore radius')
        if name=='MIL_MissileBattery_2':
            check_hatches(objects,sockets)
        report.append(f'PASS {name}: {triangles} tris, {len(objects)} parts')
    report.append('PASS: reimported models, material names, unchanged island plateaus and launcher pivots, hatch clearance.')
    print('\n'.join(report))
    args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    if args:
        (Path(args[0])/'expected.tsv').write_text('\n'.join('\t'.join(row) for row in rows)+'\n',encoding='utf-8')


if __name__=='__main__':
    main()
