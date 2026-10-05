"""Check actual exported coast FBXs before packaging; no Unity import or writes."""
import sys
import json
from pathlib import Path
import bpy
import bmesh

HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import environment_detail_common as c
OUT=HERE/'EnvironmentDetailPack'
if '--output' in sys.argv:OUT=Path(sys.argv[sys.argv.index('--output')+1]).resolve()
manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
results=[]
for entry in manifest['assets']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(OUT/'Models'/(entry['id']+'.fbx')))
    bpy.context.view_layer.update()
    obs=list(bpy.context.scene.objects);roots=[o for o in obs if not o.parent]
    names={o.name for o in obs};meshes=[o for o in obs if o.type=='MESH']
    root_ok=len(roots)==1 and roots[0].location.length<1e-5
    scale_ok=all(all(abs(v-1)<1e-5 for v in o.scale) for o in obs)
    rotation_ok=all(all(abs(v)<1e-5 for v in o.rotation_euler) for o in meshes)
    missing=sorted(set(entry['required_sockets'])-names)
    nonmanifold=0;negative=[];degenerate=0;tri=0;coords=[]
    for ob in meshes:
        ob.data.calc_loop_triangles();tri+=len(ob.data.loop_triangles)
        coords.extend(ob.matrix_world@v.co for v in ob.data.vertices)
        bm=bmesh.new();bm.from_mesh(ob.data)
        nonmanifold+=sum(not e.is_manifold for e in bm.edges)
        degenerate+=sum(f.calc_area()<1e-9 for f in bm.faces)
        if bm.calc_volume(signed=True)<=0:negative.append(ob.name)
        bm.free()
    actual={axis:[min(p[i] for p in coords),max(p[i] for p in coords)] for i,axis in enumerate('xyz')}
    bbox_ok=all(abs(x-y)<.015 for axis in 'xyz' for x,y in zip(actual[axis],entry['bounds'][axis]))
    colors_ok=True;bad_colors=[]
    for mat in bpy.data.materials:
        if mat.name not in c.COLORS:continue
        shader=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
        color=shader.inputs['Base Color'].default_value if shader else mat.diffuse_color
        if any(abs(x-y)>1e-4 for x,y in zip(color,c.COLORS[mat.name])):
            colors_ok=False;bad_colors.append(mat.name)
        mat.diffuse_color=color
    forbidden=[o.name for o in obs if o.type not in ('MESH','EMPTY')]
    terrain_ok=entry['category']!='island' or 'TerrainShell' in names
    budget=manifest['island_budget'] if entry['category']=='island' else manifest['prop_budget']
    # LOD decimation can introduce tiny triangles but must remain closed and bounded.
    passed=root_ok and scale_ok and rotation_ok and not missing and not nonmanifold and not negative and not degenerate and not forbidden and colors_ok and bbox_ok and terrain_ok and tri<=budget
    results.append({'id':entry['id'],'passed':bool(passed),'triangles':tri,'budget':budget,
        'root_centered':root_ok,'scale_1':scale_ok,'mesh_rotation_0':rotation_ok,
        'missing_sockets':missing,'nonmanifold_edges':nonmanifold,'negative_volume_meshes':negative,
        'tiny_triangles':degenerate,'material_colors_match':colors_ok,'bad_colors':bad_colors,
        'bounds_match':bbox_ok,'terrain_shell_separate':terrain_ok,'bounds':actual})
    if passed and not entry['id'].endswith('_LOD1'):
        bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
        for screen in bpy.data.screens:
            for area in screen.areas:
                if area.type=='VIEW_3D':
                    area.spaces.active.shading.type='SOLID';area.spaces.active.shading.color_type='MATERIAL'
                    area.spaces.active.region_3d.view_distance=35 if entry['category']=='island' else 12
                    area.spaces.active.region_3d.view_location=(0,0,2)
        bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(entry['id']+'.blend')))
report={'all_passed':len(results)==len(manifest['assets']) and all(r['passed'] for r in results),
        'game_tested':False,'validated_models':len(results),'results':results}
(OUT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'all_passed':report['all_passed'],'count':len(results),'failed':[r for r in results if not r['passed']]}))
if not report['all_passed']:raise SystemExit(2)
