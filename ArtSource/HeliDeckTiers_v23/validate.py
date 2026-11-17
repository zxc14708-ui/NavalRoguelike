import bpy,json
from pathlib import Path
from mathutils import Vector
p=Path(__file__).resolve().parent;results=[]
for f in sorted((p/'Models').glob('*.fbx')):
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=str(f))
 objects=list(bpy.context.scene.objects);roots=[o for o in objects if o.parent is None]
 vertices=[o.matrix_world@Vector(v) for o in objects if o.type=='MESH' for v in o.bound_box]
 lo=[min(v[i] for v in vertices) for i in range(3)];hi=[max(v[i] for v in vertices) for i in range(3)]
 checks={'single_root':len(roots)==1,'unit_scales':all(all(abs(s-1)<.0001 for s in o.scale) for o in objects),'landing_socket':any(o.name=='LandingSpot' for o in objects),'bottom_zero':abs(lo[2])<.001,'footprint':hi[0]-lo[0]<1.801 and hi[1]-lo[1]<1.801}
 results.append({'file':f.name,'checks':checks,'bounds':[lo,hi]})
 assert all(checks.values()),results[-1]
(p/'validation.json').write_text(json.dumps(results,indent=2))
print('All three decks passed FBX structure and size checks.')
