from pathlib import Path
exec(compile((Path(__file__).resolve().parents[1]/'EscortHelicopters_v19/validate.py').read_text(encoding='utf8'),'heli_validation','exec'))
for path in sorted((out/'Models').glob('MOD_*.fbx')):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path))
 obs=list(bpy.context.scene.objects)
 assert len([o for o in obs if o.parent is None])==1
 assert all(max(abs(s-1) for s in o.scale)<.001 for o in obs)
 assert bpy.data.objects.get('LandingSpot')
 pts=[o.matrix_world@v.co for o in obs if o.type=='MESH' for v in o.data.vertices]
 assert abs(min(v.z for v in pts))<.001
 assert max(v.x for v in pts)-min(v.x for v in pts)<1.801
 assert max(v.y for v in pts)-min(v.y for v in pts)<1.801
 results.append({'model':path.name,'single_root':True,'unit_scales':True,'footprint_pass':True,'landing_spot':True})
assert len(results)==6
(out/'validation.json').write_text(json.dumps(results,indent=2))
print('All 6 assets passed structural checks.')
