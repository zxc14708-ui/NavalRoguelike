"""Render the finished asset with an in-game boat to illustrate clearances."""

boat_path = OUT.parents[1] / 'Assets/_Game/Art/Models/EnemySuicideBoat.fbx'
preview_pos = (-4.2,-15.0,2.92)
preview_focus = (0,-7.8,.64)
scene.render.resolution_x, scene.render.resolution_y = 1350, 850
render('SternGate_Closed', preview_pos, preview_focus, 4.30)

bpy.ops.object.select_all(action='DESELECT')
bpy.ops.import_scene.fbx(filepath=str(boat_path))
preview_objects = list(bpy.context.selected_objects)
boat, = [ob for ob in preview_objects if ob.parent is None]
boat.parent = boat_staging
boat.location = (0,0,0)
boat.scale *= .5  # The carrier is imported at 2x in the prefab builder.
scene.frame_set(85)
bpy.context.view_layer.update()
points = [ob.matrix_world @ v.co for ob in preview_objects if ob.type == 'MESH'
          for v in ob.data.vertices]
assert max(abs(v.x) for v in points) < .69
assert min(v.y for v in points) > well_mouth and max(v.y for v in points) < well_back
assert min(v.z for v in points) > well_floor and max(v.z for v in points) < well_ceiling
boat.location.y = 1.46
render('SternGate_Open', preview_pos, preview_focus, 4.30)
for ob in preview_objects:
    ob.hide_render = True
scene.frame_set(1)
render('AuxiliaryGun', (-4.6,-4.25,3.64), (-2.1,-2.42,1.45), 2.05)
