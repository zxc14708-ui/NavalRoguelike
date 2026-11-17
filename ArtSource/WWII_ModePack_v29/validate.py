"""Re-import FBX to catch hierarchy, scale, axis and socket regressions."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent
expected={
 'MOD_Ww2Bridge':{'RadarPivot','LaunchPoint_01','LaunchPoint_02','LaunchPoint_03','LaunchPoint_04'},
 'MOD_Ww2DualGun':{'TurretPivot','ElevationPivot','Muzzle','Muzzle_01','Muzzle_02'},
 'MOD_Ww2QuadAa':{'TurretPivot','ElevationPivot','Muzzle','Muzzle_01','Muzzle_02','Muzzle_03','Muzzle_04'},
 'MOD_Ww2TorpedoMount':{'TubeMount','LaunchPoint_01','LaunchPoint_02','LaunchPoint_03','LaunchPoint_04'},
 'ENE_Ww2GunneryCruiser':{'ModelPivot','A_TurretPivot','B_TurretPivot','X_TurretPivot','A_Muzzle_01','B_Muzzle_01','X_Muzzle_01','Secondary_P_Muzzle','Secondary_S_Muzzle'},
 'ENE_Ww2FastGunboat':{'ModelPivot','TurretPivot','ElevationPivot','Muzzle'},
 'ENE_Ww2Gunboat':{'ModelPivot','TurretPivot','ElevationPivot','Muzzle'},
 'ENE_Ww2TorpedoBomber':{'Propeller','TorpedoDropPoint'},
 'ENE_Ww2DiveBomber':{'Propeller','BombDropPoint'},
}
results=[]
for f in sorted((OUT/'Models').glob('*.fbx')):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(f))
 obs=list(bpy.context.scene.objects);names={o.name for o in obs}
 roots=[o for o in obs if o.parent is None]
 assert len(roots)==1,(f.name,'root count',len(roots))
 assert all(max(abs(s-1) for s in o.scale)<.0001 for o in obs),(f.name,'scale')
 assert roots[0].location.length<.0001,(f.name,'root position')
 missing=expected.get(f.stem,{'ImpactPoint'})-names;assert not missing,(f.name,missing)
 points=[o.matrix_world@v.co for o in obs if o.type=='MESH' for v in o.data.vertices]
 lo=[min(v[i] for v in points) for i in range(3)];hi=[max(v[i] for v in points) for i in range(3)]
 if f.stem.startswith('MOD_'):
  assert abs(lo[2])<.001,(f.name,'floor',lo[2])
  assert hi[0]-lo[0]<=1.81,(f.name,'module width',hi[0]-lo[0])
  if 'Bridge' in f.stem:assert hi[1]-lo[1]<5.81 and hi[2]<3.0
 if f.stem in ('MOD_Ww2DualGun','MOD_Ww2QuadAa'):
  elev=bpy.data.objects['ElevationPivot'];yaw=bpy.data.objects['TurretPivot']
  assert elev.parent==yaw
  count=2 if 'Dual' in f.stem else 4
  for i in range(1,count+1):assert bpy.data.objects['Muzzle_%02d'%i].parent==elev
 if f.stem.startswith('TOR_'):
  # Tapered projectile nose and ImpactPoint must be on +Y after FBX round trip.
  assert bpy.data.objects['ImpactPoint'].matrix_world.translation.y>0
  assert bpy.data.objects['Exhaust'].matrix_world.translation.y<0
 result={'file':f.name,'single_root':True,'unit_scales':True,'required_sockets':True,'bounds':[lo,hi]}
 results.append(result)
assert len(results)==14,len(results)
(OUT/'validation.json').write_text(json.dumps(results,indent=2))
print('PASS: 14 FBX roots, scales, footprints, weapon sockets and projectile directions.')
