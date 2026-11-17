import bpy,json
from pathlib import Path
from mathutils import Quaternion
out=Path(__file__).resolve().parent
results=[]
for path in sorted((out/'Models').glob('*.fbx')):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path))
 obs=list(bpy.context.scene.objects);roots=[o for o in obs if o.parent is None]
 assert len(roots)==1,path.name
 assert all(max(abs(v-1) for v in o.scale)<.001 for o in obs),path.name
 if path.stem.startswith('HEL_'):
  for socket in ('Rotor','TailRotor','TorpedoDropPoint_01'):
   assert bpy.data.objects.get(socket) is not None,(path.name,socket)
  for socket,axis in [('Rotor',(0,0,1)),('TailRotor',(1,0,0))]:
   pivot=bpy.data.objects[socket];child=next(o for o in pivot.children if o.type=='MESH')
   before=child.matrix_world.copy();pivot.rotation_mode='QUATERNION';pivot.rotation_quaternion=Quaternion(axis,.6)
   bpy.context.view_layer.update();assert before!=child.matrix_world
 else:
  # The stern still has two barrels; the T3 broadside guns each have one.
  names=[o.name for o in obs]
  assert sum(n.startswith('AftGunBarrel') for n in names)==2
  assert sum(n.startswith('PrimaryGunBarrel') for n in names)==1
  if path.stem.endswith('T3'):assert sum(n.startswith('BroadsideGun_') and 'Barrel' in n for n in names)==2
 results.append({'asset':path.name,'single_root':True,'unit_scale':True,'weapon_or_rotor_checks':'passed'})
assert len(results)==6
(out/'validation.json').write_text(json.dumps(results,indent=2))
print(json.dumps(results))
