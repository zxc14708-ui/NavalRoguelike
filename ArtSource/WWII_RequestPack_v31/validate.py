"""FBX round-trip and outstanding request checks, no Unity writes."""
import bpy,json,math
from pathlib import Path
OUT=Path(__file__).resolve().parent
materials={p.stem for p in (OUT.parents[1]/'Assets/_Game/Art/Models/Materials').rglob('*.mat')}
results=[]
for path in sorted((OUT/'Models').glob('*.fbx')):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path))
 obs=list(bpy.context.scene.objects);roots=[o for o in obs if o.parent is None]
 assert len(roots)==1,(path.name,'root count')
 assert roots[0].location.length<.001
 assert all(max(abs(s-1) for s in o.scale)<.001 for o in obs)
 meshes=[o for o in obs if o.type=='MESH'];names={o.name for o in obs}
 used={m.name for o in meshes for m in o.data.materials}
 assert used<=materials,(path.name,'unknown material',used-materials)
 pts=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
 lo=[min(v[i] for v in pts) for i in range(3)];hi=[max(v[i] for v in pts) for i in range(3)]
 checks={'single_root':True,'unit_scales':True,'existing_palette_only':True}
 if path.stem=='ENE_Ww2TorpedoBoat':
  assert {'ModelPivot','TurretPivot','ElevationPivot','Muzzle','LaunchPoint_01','LaunchPoint_02'}<=names
  assert bpy.data.objects['ElevationPivot'].parent==bpy.data.objects['TurretPivot']
  assert abs(lo[2]+.35)<.001 and 6.7<hi[1]-lo[1]<6.9
  assert len(meshes)==3
  for n in ('LaunchPoint_01','LaunchPoint_02'):assert bpy.data.objects[n].matrix_world.translation.y>1.2
  checks.update({'draft_035m':True,'two_forward_launch_sockets':True,'three_motion_groups':True})
 elif path.stem.startswith('ENE_Ww2TorpedoBomber'):
  assert {'Propeller','TorpedoDropPoint'}<=names and len(meshes)==2
  checks['two_motion_groups']=True
 elif path.stem.startswith('HULL_Ww2Deck'):
  assert abs(hi[0]-lo[0]-2)<.001 and abs(hi[1]-lo[1]-2)<.001 and .089<hi[2]<.095
  assert len(meshes)==1
  checks['two_metre_grid']=True
 elif path.stem=='HULL_Ww2SideSkirt':
  assert abs(hi[0]-lo[0]-2)<.001 and abs(lo[2]+1.52)<.001
  checks['side_piece_fit']=True
 elif path.stem=='ENE_CoastalBattery_Art':
  assert {'TurretPivot','ElevationPivot','Muzzle','CutStoneFoundation','ObserverRoof'}<=names
  assert abs(lo[2]+1.9)<.001 and len(meshes)==3
  elev=bpy.data.objects['ElevationPivot'];inv=elev.matrix_world.inverted()
  local=[inv@(o.matrix_world@v.co) for o in meshes if o.parent==elev for v in o.data.vertices]
  # End ring of lengthened tube is 3cm beyond the first outer muzzle ring.
  assert sum(abs(v.y-2.74)<.001 and abs(math.hypot(v.x,v.z)-.115)<.001 for v in local)>=12
  assert sum(abs(v.y-2.71)<.001 and abs(math.hypot(v.x,v.z)-.123)<.001 for v in local)>=12
  checks.update({'barrel_muzzle_overlap_003m':True,'submerged_footing':True,'three_motion_groups':True})
 results.append({'file':path.name,'checks':checks,'meshes':len(meshes),'bounds':[lo,hi]})
assert len(results)==8
(OUT/'validation.json').write_text(json.dumps(results,indent=2))
print('PASS: 8 models; sockets, motion-group meshes, dimensions, draft, muzzle join and palette verified.')
