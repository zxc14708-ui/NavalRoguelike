import bpy, json
from pathlib import Path
from mathutils import Quaternion, Vector
out=Path(__file__).resolve().parent
reports=[]
for filename,required,pivot in [
 ('EnemyMissileBoat.fbx',['MissileLauncherAssembly','MissileLauncherPivot','MissileLaunchPoint','Muzzle'],'MissileLauncherPivot'),
 ('EnemyPccCorvette.fbx',['ForeGun01','ForeGun02','AftGun01','AftGun02','Muzzle_Fore01','Muzzle_Aft01'],'ForeGun01')]:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=str(out/filename))
 objects=list(bpy.context.scene.objects);names={o.name:o for o in objects}
 assert all(n in names for n in required),filename
 assert len([o for o in objects if o.parent is None])==1
 assert all(max(abs(s-1) for s in o.scale)<.0001 for o in objects)
 p=names[pivot];moving=next(o for o in objects if o.type=='MESH' and o.parent==p)
 before=moving.matrix_world@moving.data.vertices[0].co
 p.rotation_euler.rotate_axis('Y',.5);bpy.context.view_layer.update()
 delta=(moving.matrix_world@moving.data.vertices[0].co-before).length
 assert delta>.01
 reports.append({'file':filename,'single_root':True,'unit_scales':True,'required_sockets':True,'pivot_movement_metres':delta})
(out/'validation.json').write_text(json.dumps(reports,indent=2))
print(json.dumps(reports))
