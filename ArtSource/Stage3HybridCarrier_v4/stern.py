"""Hull cutout, stern door, and auxiliary mount meshes for build.py."""

well_half_width = .76
well_back = -5.63
well_floor = -.235
well_ceiling = .754
well_mouth = -8.02
gate_open_angle = 96

# Cut through the hull and the aft end of the hangar wherever they overlap
# the boat bay. Keep the deck and its markings above the opening.
cut_end = -8.40
cut = box('SternWellCut',
          (0, (cut_end + well_back)/2, (well_ceiling + well_floor)/2),
          (well_half_width*2, well_back-cut_end, well_ceiling-well_floor),
          DARK, None)
bpy.context.view_layer.update()
for shell_name in ('ArmoredHull', 'HangarEnvelopeAft'):
    shell = bpy.data.objects[shell_name]
    modifier = shell.modifiers.new('Well deck clearance', 'BOOLEAN')
    modifier.operation, modifier.solver, modifier.object = 'DIFFERENCE', 'EXACT', cut
    with bpy.context.temp_override(object=shell):
        bpy.ops.object.modifier_apply(modifier=modifier.name)
bpy.data.objects.remove(cut, do_unlink=True)

well = c.empty('SternWellDeck', root, col)
bay_mid = (well_mouth + well_back)/2
bay_length = well_back - well_mouth
box('WellDeckFloor', (0,bay_mid,well_floor-.009),
    (1.55,bay_length,.026), DARK, well)
box('WellDeckCeiling', (0,bay_mid,well_ceiling+.027),
    (1.58,bay_length,.057), DARK, well)
box('WellDeckBulkhead', (0,well_back+.02,.249), (1.54,.07,.99), DARK, well)
box('SternLintel', (0,-7.985,.815), (1.80,.15,.154), HULL, well)

for side in (-1, 1):
    x = side*.789
    box('SternDoorJamb', (x,-7.99,.244), (.065,.135,.99), HULL, well)
    box('WellDeckLiner', (side*.774,bay_mid,.254), (.031,bay_length,.98), DARK, well)
    for y in (-7.76,-7.33,-6.90,-6.47,-6.04):
        box('WellDeckWallRib', (side*.746,y,.245), (.065,.054,.945), STEEL, well)
    for y in (-7.78,-6.58):
        box('WellDeckLightHousing', (side*.728,y,.67), (.073,.16,.068), GUN, well)
        box('WellDeckLight', (side*.693,y,.666), (.019,.121,.031), YELLOW, well)
    cyl('SternDoorHinge', (side*.692,well_mouth,well_floor+.015),
        .068,.152,GUN,well,12,(0,math.pi/2,0))
    box('SternDoorWarningLamp', (side*.825,-8.068,.656), (.07,.027,.038), YELLOW, well)

# In the open pose the inner, ribbed face becomes the top of the ramp.
gate = c.empty('SternGatePivot', well, col, (0,well_mouth,well_floor+.015))
box('SternGateOuterPlate', (0,.007,.478), (1.425,.082,.94), HULL, gate)
box('SternGateInnerTread', (0,.053,.478), (1.39,.025,.90), GUN, gate)
for side in (-1,1):
    box('SternGateEdge', (side*.682,.070,.478), (.047,.038,.91), STEEL, gate)
    box('SternGateFaceRecess', (side*.518,-.038,.62), (.204,.013,.075), DARK, gate)
for z in (.16,.32,.48,.64,.80):
    box('SternGateTreadRib', (0,.075,z), (1.29,.021,.024), STEEL, gate)
for x in (-.58,-.42,-.26,.26,.42,.58):
    box('SternGateWarningStripe', (x,.068,.875), (.096,.009,.031),
        YELLOW, gate,(0,.20,0))

boat_staging = c.empty('SternBoatStaging', well, col, (0,-6.89,0))
boat_exit = c.empty('SternBoatExit', well, col, (0,-9.96,0))
for socket in (boat_staging, boat_exit):
    socket.rotation_euler.z = math.pi

# Side platforms and compact shielded single-barrel mounts, inboard of the AA line.
for side, name in [(-1,'Port'),(1,'Starboard')]:
    x, y = side*2.10, -2.42
    chamfer('AuxMountPlatform', x,y,.58,.71,1.376,1.21,HULL,flight,.10)
    beam('AuxPlatformBrace', (side*1.71,y,1.025), (x,y,1.253), .104, HULL, flight)
    cyl('AuxSlewBase', (x,y,1.419), .158,.093,GUN,flight,10)
    yaw = c.empty('AuxGunYaw_'+name, flight, col, (x,y,1.465))
    yaw.rotation_euler.z = -side*math.pi/2
    chamfer('AuxGunBase', 0,-.025,.29,.29,.10,.0,STEEL,yaw,.044)
    for flank in (-1,1):
        box('AuxMountTrunnion', (flank*.105,0,.113), (.052,.173,.14), GUN, yaw)
    pitch = c.empty('AuxGunElevation_'+name, yaw, col, (0,.027,.140))
    c.frustum('AuxGunShield', (0,.041,.018), (.23,.169), (.175,.144), .19, pitch,col,HULL)
    box('AuxOptics', (.062,.116,.071), (.064,.074,.046), DARK, pitch)
    cyl('AuxBarrelShroud', (0,.17,.0), .039,.14,GUN,pitch,10,(math.pi/2,0,0))
    cyl('AuxBarrel', (0,.31,.0), .022,.19,GUN,pitch,10,(math.pi/2,0,0))
    cyl('AuxMuzzleCap', (0,.407,.0), .030,.036,DARK,pitch,10,(math.pi/2,0,0))
    c.empty('AuxGunMuzzle_'+name, pitch, col, (0,.43,0))
