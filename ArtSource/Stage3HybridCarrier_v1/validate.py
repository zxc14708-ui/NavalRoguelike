"""Inspect the actual exported FBX, without changing the model."""
from pathlib import Path
import bpy

p=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(p/'Models/EnemyHybridBoss.fbx'))
objects=list(bpy.context.scene.objects)
by_name={o.name:o for o in objects}
assert [o.name for o in objects if o.parent is None]==['EnemyHybridBoss']
assert all(max(abs(s-1) for s in o.scale)<.001 for o in objects)
vertices=[o.matrix_world@v.co for o in objects if o.type=='MESH' for v in o.data.vertices]
assert abs(min(v.y for v in vertices)+8)<.001 and abs(max(v.y for v in vertices)-8)<.001
assert min(v.z for v in vertices)<0<max(v.z for v in vertices)
deck=by_name['FlightDeck_Mesh']
deck_points=[deck.matrix_world@v.co for v in deck.data.vertices if 1.36<v.co.z<1.40]
assert abs(max(v.y for v in deck_points)-min(v.y for v in deck_points)-8.47)<.001
assert by_name['FlightDeckLaunch'].parent==by_name['FlightDeck']
assert by_name['RadarPivot'].type=='EMPTY'
for i in range(1,3):
    yaw=by_name[f'MainGun_{i:02}']; pitch=by_name[f'MainGun_Elevation_{i:02}']
    assert pitch.parent==yaw and yaw.location.y>2
    for j in (2*i-1,2*i):
        muzzle=by_name[f'MainGun_Muzzle_{j:02}']
        assert muzzle.parent==pitch and muzzle.location.y>1.4
assert 'MainGun_03' not in by_name
for side,sign in (('Port',-1),('Starboard',1)):
    for i in range(1,4):
        yaw=by_name[f'AA_{side}_{i:02}']; pitch=by_name[f'AA_{side}_Elevation_{i:02}']
        muzzle=by_name[f'AA_{side}_Muzzle_{i:02}']
        assert yaw.location.x*sign>2 and yaw.location.y<0
        assert pitch.parent==yaw and muzzle.parent==pitch
        assert muzzle.matrix_world.translation.x*sign>yaw.location.x*sign
    assert f'AA_{side}_04' not in by_name
print('PASS: 16m hull, waterline, 8.47m flight deck, unit scale, twin guns 2x2, AA 3+3, pivots and sockets.')
