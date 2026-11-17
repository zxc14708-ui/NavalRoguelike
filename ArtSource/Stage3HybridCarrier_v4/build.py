"""Build the v3 carrier with a stern well deck and two auxiliary mounts."""
from pathlib import Path

previous = Path(__file__).resolve().parent.parent / 'Stage3HybridCarrier_v3' / 'build.py'
builder = previous.read_text(encoding='utf8')
exec(compile(builder[:builder.rindex('exec(compile')], str(previous), 'exec'))

replace("(-5.54,2.17),(-2.31,2.01)", "(-5.54,2.17),(-1.65,2.01)")
replace('# Bake by independently moving node.', '''
stern_source = OUT / 'stern.py'
exec(compile(stern_source.read_text(encoding='utf8'), str(stern_source), 'exec'))

# Bake by independently moving node.''')
replace("lift.animation_data.action.name = 'DroneElevatorCycle'", '''
lift.animation_data.action.name = 'DroneElevatorCycle'
for frame, angle in [(1, 0), (25, 0), (61, gate_open_angle), (109, gate_open_angle), (145, 0)]:
    gate.rotation_euler.x = math.radians(angle)
    gate.keyframe_insert(data_path='rotation_euler', index=0, frame=frame)
gate.animation_data.action.name = 'SternGateCycle'

# Blender's FBX all-actions mode matches by data path. Give each action a
# target-specific path so the exporter can't assign it to the other pivot.
for node, binding in [(lift, 'DroneElevatorBinding'), (gate, 'SternGateBinding')]:
    node[binding] = 1.0
    node.keyframe_insert(data_path='["' + binding + '"]', frame=1)
''')
replace("'drone_socket':mount.name}}",
        """'drone_socket':mount.name},
 'stern_gate': {'pivot':gate.name, 'staging_socket':boat_staging.name,
                'exit_socket':boat_exit.name, 'open_degrees':gate_open_angle,
                'closed_frame':1, 'open_frame':61},
 'auxiliary_mounts':['Port','Starboard']}""")
replace("render('Stage3HybridCarrier',(-13,8,9),(0,0,1.35),17.45)",
        "render('Stage3HybridCarrier',(-12,-9,8),(0,0,1.3),18.2)")
preview_start = code.index('# Save the ship before importing')
code = code[:preview_start] + '''
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Stage3HybridCarrier.blend'))
exec(compile((OUT/'preview.py').read_text(encoding='utf8'), str(OUT/'preview.py'), 'exec'))
'''

exec(compile(code, str(source), 'exec'))
