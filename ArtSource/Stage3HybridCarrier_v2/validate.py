"""Round-trip verification plus comparison against the actual original FBX."""
from pathlib import Path
import bpy

out = Path(__file__).resolve().parent
before = out.parent / 'Stage3HybridCarrier_v1' / 'Models' / 'EnemyHybridBoss.fbx'
after = out / 'Models' / before.name

def inspect(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    objects = bpy.context.scene.objects
    assert [o.name for o in objects if not o.parent] == ['EnemyHybridBoss']
    assert all(max(abs(s-1) for s in o.scale)<.001 for o in objects)
    verts = [o.matrix_world@v.co for o in objects if o.type=='MESH' for v in o.data.vertices]
    assert abs(max(v.y for v in verts)-min(v.y for v in verts)-16)<.001
    assert objects['FlightDeckLaunch'].parent==objects['FlightDeck']
    deck = objects['FlightDeck_Mesh']
    deck_pts = [deck.matrix_world@v.co for v in deck.data.vertices if 1.36<v.co.z<1.4]
    assert abs(max(v.y for v in deck_pts)-min(v.y for v in deck_pts)-8.47)<.001
    assert 'RadarPivot' in objects
    aa = [o.name for o in objects if o.type=='EMPTY' and
          o.name.startswith(('AA_Port_', 'AA_Starboard_')) and o.name.count('_')==2]
    assert set(aa)=={f'AA_{side}_{i:02}' for side in ('Port','Starboard') for i in range(1,4)}
    assert len([o for o in objects if o.name.startswith('MainGun_Muzzle_')]) == 4
    gun_sizes = []
    barrel_z = []
    roof_z = []
    for i in (1,2):
        gun = objects[f'MainGun_{i:02}']
        pitch = objects[f'MainGun_Elevation_{i:02}']
        assert pitch.parent==gun
        muzzle = [objects[f'MainGun_Muzzle_{j:02}'] for j in (2*i-1,2*i)]
        assert all(m.parent==pitch for m in muzzle)
        assert muzzle[0].location.x<0<muzzle[1].location.x
        assert all(m.location.y>0 for m in muzzle)
        mesh = next(o for o in gun.children if o.type=='MESH')
        w = max(v.co.x for v in mesh.data.vertices)-min(v.co.x for v in mesh.data.vertices)
        gun_sizes.append((w,muzzle[0].location.y))
        barrel_z.append(pitch.matrix_world.translation.z)
        roof_z.append(max((mesh.matrix_world@v.co).z for v in mesh.data.vertices))
    assert barrel_z[1]-.06>roof_z[0], 'raised turret barrels should clear forward roof at rest'
    return gun_sizes

v1 = inspect(before)
v2 = inspect(after)
for (w1,y1),(w2,y2) in zip(v1,v2):
    assert abs(w2/w1-.76)<.001 and abs(y2/y1-.75)<.001
print(f'PASS: widths {v1[0][0]:.3f}m -> {v2[0][0]:.3f}m; deck, 4 main muzzle sockets, 3+3 AA and unit transforms preserved.')
