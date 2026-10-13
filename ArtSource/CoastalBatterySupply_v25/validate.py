"""Import checks for two review FBX files."""
import bpy,json
from pathlib import Path
from mathutils import Vector
out=Path(__file__).resolve().parent
results=[]
for path in sorted((out/'Models').glob('*.fbx')):
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=str(path))
 objs=list(bpy.context.scene.objects)
 roots=[o for o in objs if o.parent is None]
 assert len(roots)==1,(path.name,'roots',len(roots))
 assert all(all(abs(s-1)<.0001 for s in o.scale) for o in objs),(path.name,'scale')
 verts=[o.matrix_world@Vector(v) for o in objs if o.type=='MESH' for v in o.bound_box]
 lo=[min(v[i] for v in verts) for i in range(3)]
 hi=[max(v[i] for v in verts) for i in range(3)]
 names={o.name for o in objs}
 if 'CoastalBattery' in path.name:
  assert abs(lo[2])<.001
  assert {'TurretPivot','ElevationPivot','Muzzle'}<=names
  assert bpy.data.objects['ElevationPivot'].parent==bpy.data.objects['TurretPivot']
  assert bpy.data.objects['Muzzle'].parent==bpy.data.objects['ElevationPivot']
  assert hi[0]-lo[0]<5.61 and hi[1]-lo[1]<6.4
 else:
  assert {'Bow','Stern','Waterline'}<=names
  assert 15.5<hi[1]-lo[1]<15.7
  assert 4.1<hi[0]-lo[0]<4.3
  assert lo[2]<-1.0 and hi[2]>4.6
 results.append({'file':path.name,'single_root':True,'unit_scale':True,'bounds':{'min':lo,'max':hi},'sockets':sorted(n for n in names if n in {'TurretPivot','ElevationPivot','Muzzle','Bow','Stern','Waterline'})})
assert len(results)==2
(out/'validation.json').write_text(json.dumps(results,indent=2))
print('Both FBX files passed size and socket checks.')
