"""Inspect exported review assets; battery muzzle remains outside the forward wall."""
import bpy,json
from pathlib import Path
from mathutils import Vector
out=Path(__file__).resolve().parent;results=[]
for path in sorted((out/'Models').glob('*.fbx')):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path))
 obs=list(bpy.context.scene.objects);roots=[o for o in obs if o.parent is None]
 assert len(roots)==1
 assert all(all(abs(s-1)<.0001 for s in o.scale) for o in obs)
 names={o.name for o in obs};checks={'single_root':True,'unit_scale':True}
 if 'Battery' in path.name:
  assert {'TurretPivot','ElevationPivot','Muzzle'}<=names
  turret=bpy.data.objects['TurretPivot'];elev=bpy.data.objects['ElevationPivot'];muzzle=bpy.data.objects['Muzzle']
  assert elev.parent==turret and muzzle.parent==elev
  front=max((o.matrix_world@Vector((0,.30,0))).y for o in obs if o.name.startswith('FrontBlastWall'))
  assert muzzle.matrix_world.translation.y>front+.25
  checks['pivot_hierarchy']=True;checks['muzzle_clears_front_wall']=True
 else:
  assert {'Bow','Stern','Waterline'}<=names
  points=[o.matrix_world@v.co for o in obs if o.type=='MESH' for v in o.data.vertices]
  assert 15.5<max(v.y for v in points)-min(v.y for v in points)<15.7
  assert min(v.z for v in points)<-1.0
  checks['waterline_and_length']=True
 results.append({'asset':path.name,'checks':checks})
assert len(results)==2
(out/'validation.json').write_text(json.dumps(results,indent=2))
print('Both models passed FBX inspection.')
