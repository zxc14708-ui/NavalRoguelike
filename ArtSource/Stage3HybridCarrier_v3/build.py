"""Build the carrier with a working flight-deck elevator from the v2 source."""
from pathlib import Path

version_two = Path(__file__).resolve().parent.parent / 'Stage3HybridCarrier_v2' / 'build.py'
wrapper = version_two.read_text(encoding='utf8')
exec(compile(wrapper[:wrapper.rindex('exec(compile')], str(version_two), 'exec'))


def replace(original, updated):
    global code
    if code.count(original) != 1:
        raise ValueError(f'Carrier source changed around {original[:72]!r}')
    code = code.replace(original, updated)


replace("pad('FlightDeckEdge',outline,1.37,1.21,HULL,flight)", '''
# Clear the same rectangular opening through the deck and the hangar shell.
lift_half_width = .74
lift_front, lift_aft = .14, -1.46
lift_y = (lift_front + lift_aft) / 2
lift_length = lift_front - lift_aft
lift_top, lift_bottom = 1.397, .895
lift_gap = .026


def clip_section(polygon, axis, limit, direction):
    result = []
    for start, end in zip(polygon[-1:] + polygon[:-1], polygon):
        start_inside = (start[axis] - limit) * direction >= 0
        end_inside = (end[axis] - limit) * direction >= 0
        if start_inside != end_inside:
            t = (limit - start[axis]) / (end[axis] - start[axis])
            crossing = tuple(limit if i == axis else start[i] + t * (end[i] - start[i])
                             for i in range(2))
            if crossing != start and crossing != end:
                result.append(crossing)
        if end_inside:
            result.append(end)
    return result


def pad_with_lift_opening(name, polygon, top, bottom, material):
    middle = clip_section(clip_section(polygon, 1, lift_aft, 1), 1, lift_front, -1)
    sections = {
        'Fore': clip_section(polygon, 1, lift_front, 1),
        'Aft': clip_section(polygon, 1, lift_aft, -1),
        'Port': clip_section(middle, 0, -lift_half_width, -1),
        'Starboard': clip_section(middle, 0, lift_half_width, 1),
    }
    for side, section in sections.items():
        pad(name + side, section, top, bottom, material, flight)


pad_with_lift_opening('FlightDeckEdge', outline, 1.37, 1.21, HULL)''')
replace("pad('FlightDeckSurface',[(x*.988,y) for x,y in outline],1.397,1.366,FLIGHT,flight)",
        "pad_with_lift_opening('FlightDeckSurface', [(x*.988,y) for x,y in outline], lift_top, 1.366, FLIGHT)")
replace("pad('HangarEnvelope',hangar,1.215,.65,STEEL,flight)", '''
pad_with_lift_opening('HangarEnvelope', hangar, 1.215, .65, STEEL)

shaft = c.empty('DroneElevatorShaft', flight, col, (0, lift_y, 0))
lift = c.empty('DroneElevatorPivot', flight, col, (0, lift_y, lift_top))
c.empty('DroneElevatorUpper', flight, col, (0, lift_y, lift_top))
c.empty('DroneElevatorLower', flight, col, (0, lift_y, lift_bottom))
mount = c.empty('DroneElevatorMount', lift, col, (0, -.09, .0553))

platform_x = lift_half_width - lift_gap
platform_y = lift_length / 2 - lift_gap
platform_outline = [(-platform_x,-platform_y), (platform_x,-platform_y),
                    (platform_x,platform_y), (-platform_x,platform_y)]
pad('LiftPlatformFrame', platform_outline, -.026, -.066, STEEL, lift)
pad('LiftPlatformTread', platform_outline, 0, -.026, FLIGHT, lift)
box('LiftWellFloor', (0, 0, .814), (lift_half_width*2, lift_length, .032), DARK, shaft)

for side in (-1, 1):
    box('LiftWellSideLiner', (side*(lift_half_width+.006), 0, 1.083),
        (.018, lift_length, .50), DARK, shaft)
    box('LiftWellEndLiner', (0, side*(lift_length/2+.006), 1.083),
        (lift_half_width*2, .018, .50), DARK, shaft)
    # Guides sit outside the moving platform's clearance.
    for y in (-.50, .50):
        box('LiftGuide', (side*(lift_half_width-.005), y, 1.076),
            (.020, .066, .46), STEEL, shaft)
    box('LiftSideRim', (side*(lift_half_width+.025), 0, lift_top+.001),
        (.050, lift_length+.10, .005), STEEL, shaft)
    box('LiftEndRim', (0, side*(lift_length/2+.025), lift_top+.001),
        (lift_half_width*2, .050, .005), STEEL, shaft)
    for y in (-.66, -.46, .46, .66):
        box('LiftEdgeStripe', (side*(lift_half_width+.025), y, lift_top+.005),
            (.033, .13, .004), YELLOW, shaft, (0,0,side*.13))
    for x in (-.52, .52):
        box('LiftThresholdStripe', (x, side*(lift_length/2+.025), lift_top+.005),
            (.14, .033, .004), YELLOW, shaft)
        box('LiftLandingMark', (x, side*.59, .002), (.145, .026, .003), WHITE, lift)
''')
replace("def mark(n,a,b,w,m=WHITE):", "def mark(n,a,b,w,m=WHITE,parent=flight):")
replace("    return c.mesh(n,[(x+dx,y+dy,1.399),(u+dx,v+dy,1.399),(u-dx,v-dy,1.399),(x-dx,y-dy,1.399)],[(0,1,2,3)],flight,col,m)",
        '''    z = .002 if parent == lift else lift_top+.002
    if parent == lift:
        y -= lift_y
        v -= lift_y
    return c.mesh(n, [(x+dx,y+dy,z),(u+dx,v+dy,z),(u-dx,v-dy,z),(x-dx,y-dy,z)],
                  [(0,1,2,3)], parent, col, m)''')
replace("    y=-7.19+i*.57;mark('BrokenCenterline',(0,y),(0,y+.31),.033)", '''
    start = -7.19+i*.57
    end = start+.31
    # Continue the centerline on the lift, with a gap at each platform edge.
    for low, high, parent in [(-8, lift_aft-.055, flight),
                              (lift_aft+lift_gap, lift_front-lift_gap, lift),
                              (lift_front+.055, .49, flight)]:
        a, b = max(start, low), min(end, high)
        if b > a:
            mark('BrokenCenterline', (0,a), (0,b), .033, parent=parent)''')
replace("for x in (-.60,.60):mark('RunwayFront',(0,-.095),(x,-.53),.048)",
        "for x in (-.60,.60):mark('RunwayFront',(0,-.095),(x,-.53),.048,parent=lift)")
replace("objs=c.descendants(root)", '''
scene = bpy.context.scene
scene.render.fps = 30
scene.frame_start, scene.frame_end = 1, 145
for frame, height in [(1, lift_top), (25, lift_top), (61, lift_bottom),
                      (91, lift_bottom), (127, lift_top), (145, lift_top)]:
    lift.location.z = height
    lift.keyframe_insert(data_path='location', index=2, frame=frame)
lift.animation_data.action.name = 'DroneElevatorCycle'
for label, frame in [('Flight deck', 1), ('Lower', 25), ('Hangar', 61), ('Raise', 91), ('Return', 127)]:
    scene.timeline_markers.new(label, frame=frame)
scene.frame_set(1)
objs=c.descendants(root)''')
replace("object_types={'EMPTY','MESH'},bake_anim=False)", """object_types={'EMPTY','MESH'},
    bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_use_all_bones=False, bake_anim_simplify_factor=0)""")
replace("'main_gun_factors_xyz':[.76,.75,.68]}",
        """'main_gun_factors_xyz':[.76,.75,.68],
 'drone_elevator': {'opening_m':[lift_half_width*2,lift_length],
                    'platform_m':[platform_x*2,platform_y*2],
                    'travel_m':round(lift_top-lift_bottom,3), 'fps':30, 'frames':[1,145],
                    'upper_frame':1, 'lower_frame':61, 'moving_node':lift.name,
                    'drone_socket':mount.name}}""")
replace("""render('Stage3HybridCarrier_Aft',(12,-8,10),(0,-1,1.3),17.3)
scene.render.resolution_y=835;cam.location=(0,0,21);cam.rotation_euler=(0,0,-math.pi/2);data.ortho_scale=18
scene.render.filepath=str(OUT/'Preview/Stage3HybridCarrier_Top.png');bpy.ops.render.render(write_still=True)""", '''
# Save the ship before importing the game drone used to illustrate the lift.
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Stage3HybridCarrier.blend'))
drone_path = OUT.parents[1] / 'Assets/_Game/Art/Models/EnemyDrone.fbx'
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.import_scene.fbx(filepath=str(drone_path))
drone_objects = list(bpy.context.selected_objects)
drone_root, = [obj for obj in drone_objects if obj.parent is None]
drone_root.parent = mount
drone_root.location = (0, 0, 0)
# The boss prefab uses twice the source scale, so show the same relative fit here.
drone_root.scale *= .5
bpy.context.view_layer.update()
mesh_points = [mount.matrix_world.inverted() @ obj.matrix_world @ vertex.co
               for obj in drone_objects if obj.type == 'MESH' for vertex in obj.data.vertices]
assert all(abs(v.x + mount.location.x) < platform_x and abs(v.y + mount.location.y) < platform_y
           for v in mesh_points)
assert 0 <= min(v.z for v in mesh_points) + mount.location.z < .01
scene.render.resolution_x, scene.render.resolution_y = 1300, 950
for name, frame in [('Elevator_Up', 1), ('Elevator_Down', 75)]:
    scene.frame_set(frame)
    render(name, (-3.2,-2.8,7.8), (0,lift_y,1.38), 3.30)''')
replace("""scene.render.resolution_y=1040;cam.location=(-13,8,9);cam.rotation_euler=(Vector((0,0,1.35))-cam.location).to_track_quat('-Z','Y').to_euler();data.ortho_scale=17.45
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Stage3HybridCarrier.blend'))""", "")

exec(compile(code, str(source), 'exec'))
