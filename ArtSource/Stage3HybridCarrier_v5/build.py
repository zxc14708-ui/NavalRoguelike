"""Add a second pair of the v4 auxiliary mounts beside the bridge."""
from pathlib import Path

previous = Path(__file__).resolve().parent.parent / 'Stage3HybridCarrier_v4' / 'build.py'
builder = previous.read_text(encoding='utf8')
exec(compile(builder[:builder.rindex('exec(compile')], str(previous), 'exec'))

replace("""stern_source = OUT / 'stern.py'
exec(compile(stern_source.read_text(encoding='utf8'), str(stern_source), 'exec'))""", '''
stern_source = OUT.parent / 'Stage3HybridCarrier_v4' / 'stern.py'
stern_code = stern_source.read_text(encoding='utf8')
existing_mount = """for side, name in [(-1,'Port'),(1,'Starboard')]:
    x, y = side*2.10, -2.42
    chamfer('AuxMountPlatform', x,y,.58,.71,1.376,1.21,HULL,flight,.10)
    beam('AuxPlatformBrace', (side*1.71,y,1.025), (x,y,1.253), .104, HULL, flight)
    cyl('AuxSlewBase', (x,y,1.419), .158,.093,GUN,flight,10)
    yaw = c.empty('AuxGunYaw_'+name, flight, col, (x,y,1.465))"""
assert stern_code.count(existing_mount) == 1, 'v4 auxiliary mount geometry changed'
stern_code = stern_code.replace(existing_mount, """mount_positions = [
    # name, side, x, y, platform top, and inboard brace anchor
    ('Port', -1, 2.10, -2.42, 1.376, 1.71, 1.025),
    ('Starboard', 1, 2.10, -2.42, 1.376, 1.71, 1.025),
    ('BridgePort', -1, 1.58, 1.36, 1.215, 1.49, .79),
    ('BridgeStarboard', 1, 1.58, 1.36, 1.215, 1.49, .79),
]
for name, side, offset, y, deck, anchor_x, anchor_z in mount_positions:
    x = side*offset
    if name.startswith('Bridge'):
        chamfer('AuxBridgeFoundation', side*anchor_x,y,.29,.47,deck-.14,anchor_z,
                HULL,flight,.055)
    chamfer('AuxMountPlatform', x,y,.58,.71,deck,deck-.166,HULL,flight,.10)
    beam('AuxPlatformBrace', (side*anchor_x,y,anchor_z), (x,y,deck-.123), .104, HULL, flight)
    cyl('AuxSlewBase', (x,y,deck+.043), .158,.093,GUN,flight,10)
    yaw = c.empty('AuxGunYaw_'+name, flight, col, (x,y,deck+.089))""")
exec(compile(stern_code, str(stern_source), 'exec'))
''')
replace("'auxiliary_mounts':['Port','Starboard']",
        "'auxiliary_mounts':[position[0] for position in mount_positions]")
replace("exec(compile((OUT/'preview.py').read_text(encoding='utf8'), str(OUT/'preview.py'), 'exec'))", '''
scene.render.resolution_x, scene.render.resolution_y = 1600, 980
render('AuxiliaryGunLayout', (-7.5,-4.7,10), (0,-.42,1.39), 9.10)
scene.render.resolution_x, scene.render.resolution_y = 1700, 765
cam.location = (0,0,21)
cam.rotation_euler = (0,0,-math.pi/2)
data.ortho_scale = 17.25
scene.render.filepath = str(OUT/'Preview/AuxiliaryGunLayout_Top.png')
bpy.ops.render.render(write_still=True)
''')

exec(compile(code, str(source), 'exec'))
