"""Historical open coastal emplacement variant; supply ship retained from v25."""
from pathlib import Path
source=Path(__file__).resolve().parents[1]/'CoastalBatterySupply_v25'/'build.py'
code=source.read_text(encoding='utf8')
start=code.index('def build_battery():')
end=code.index('def build_supply():',start)
replacement='''def build_battery():
 r,col=root('ENE_CoastalBattery_Art')
 # A roughly 5.5m compact concrete coastal position, inset into shoreline rock.
 c.frustum('CutStoneFoundation',(0,0,.29),(5.55,5.55),(5.28,5.28),.58,r,col,'BatteryRock')
 c.box('ConcreteGunPlatform',(0,0,.64),(5.12,5.12,.16),r,col,'BatteryConcrete',bevel=.08)
 for side in (-1,1):
  # Rearward retaining walls separate the gun deck from earth and magazine access.
  c.box('LateralParapet',(side*2.24,-.13,1.08),(.60,4.80,.72),r,col,'BatteryConcrete',bevel=.06)
  c.box('ParapetCap',(side*2.24,-.13,1.46),(.70,4.82,.12),r,col,'MAT_IslandCliff')
  c.box('FrontParapetWing',(side*1.55,2.20,1.03),(1.15,.58,.64),r,col,'BatteryConcrete',bevel=.05)
  c.box('EarthCover',(side*2.50,-1.45,1.20),(.42,1.68,.42),r,col,'BatteryRock',bevel=.04)
  c.box('AmmunitionHatch',(side*1.64,-1.69,.76),(.62,.09,.73),r,col,'BatteryDark')
  c.box('HatchFrame',(side*1.64,-1.76,1.11),(.70,.12,.08),r,col,'BatteryConcrete')
 # Forward wall is low and interrupted only where the barrel clears it.
 for side in (-1,1):
  c.box('FrontBlastWall',(side*.97,2.18,1.06),(1.22,.60,.65),r,col,'BatteryConcrete',bevel=.05)
 c.box('FrontWallCap',(0,2.18,1.41),(2.95,.69,.07),r,col,'MAT_IslandCliff')
 c.box('RearWalkway',(0,-2.03,.76),(2.95,.66,.08),r,col,'MAT_IslandCliff')
 # Bolt ring and rotating carriage are exposed, as on a shore-mounted naval gun.
 c.cyl('CircularHoldfast',(0,0,.77),1.18,.10,r,col,'BatteryDark',16)
 for i in range(12):
  a=i*math.tau/12
  c.cyl('HoldfastBolt',(1.03*math.cos(a),1.03*math.sin(a),.831),.045,.025,r,col,'Gunmetal',6)
 turret=c.empty('TurretPivot',r,col,(0,0,.85))
 c.cyl('TraversePlatform',(0,0,.08),.84,.16,turret,col,'Gunmetal',16)
 for side in (-1,1):
  c.frustum('TrunnionPedestal',(side*.52,-.03,.47),(.31,.75),(.24,.55),.76,turret,col,'BatteryConcrete')
  c.cyl('TrunnionHub',(side*.70,.12,.69),.19,.12,turret,col,'Gunmetal',12,(0,math.pi/2,0))
  # Separate shoulder shields retain open loading space behind the breech.
  c.box('GunShieldSide',(side*.87,.36,.88),(.10,1.13,.82),turret,col,'BatteryConcrete')
  c.box('ShieldUpperAngled',(side*.78,.44,1.32),(.26,.99,.11),turret,col,'MAT_IslandCliff',(0,side*.10,0))
  c.cyl('RecoilCylinder',(side*.28,.38,1.12),.105,.95,turret,col,'BatteryDark',10,(math.pi/2,0,0))
  c.cyl('RecoilEndCap',(side*.28,.86,1.12),.13,.06,turret,col,'BatteryConcrete',10,(math.pi/2,0,0))
 c.box('ShieldFront',(0,.78,.86),(1.51,.12,.77),turret,col,'BatteryConcrete')
 c.box('ShieldGunOpening',(0,.851,.91),(.39,.02,.33),turret,col,'BatteryDark')
 c.box('LoadingDeck',(0,-.72,.21),(1.58,.78,.12),turret,col,'MAT_IslandCliff')
 c.cyl('TraversingHandwheel',(.88,-.36,.63),.19,.045,turret,col,'Gunmetal',12,(0,math.pi/2,0))
 pivot=c.empty('ElevationPivot',turret,col,(0,.20,.94))
 c.box('BreechBlock',(0,-.44,-.045),(.55,.56,.42),pivot,col,'Gunmetal',bevel=.045)
 c.cyl('BreechRoundel',(0,-.76,-.045),.15,.08,pivot,col,'BatteryConcrete',12,(math.pi/2,0,0))
 c.cyl('GunJacket',(0,.28,0),.18,.76,pivot,col,'BatteryConcrete',12,(math.pi/2,0,0))
 c.cyl('TaperedGunTube',(0,1.19,0),.115,1.28,pivot,col,'Gunmetal',12,(math.pi/2,0,0))
 c.cyl('MuzzleRing',(0,1.85,0),.145,.13,pivot,col,'BatteryConcrete',12,(math.pi/2,0,0))
 c.empty('Muzzle',pivot,col,(0,1.93,0))
 # Observation and control are separate from the rotating carriage.
 c.box('ObserverShaft',(1.37,-1.74,1.37),(.62,.72,1.24),r,col,'BatteryConcrete',bevel=.04)
 c.box('ObserverSlit',(1.37,-1.362,1.59),(.40,.017,.12),r,col,'Radar Glass')
 c.box('ObserverRoof',(1.37,-1.74,2.04),(.73,.84,.11),r,col,'MAT_IslandCliff')
 c.box('CableTrench',(0,-1.47,.745),(.19,.90,.02),r,col,'BatteryDark')
 c.box('WarningPanel',(-2.28,.22,1.15),(.018,.35,.17),r,col,'EnemyRed')
 return r

'''
replacement=replacement.replace("(0,1.19,0),.115,1.28", "(0,1.77,0),.115,2.00")
replacement=replacement.replace("(0,1.85,0),.145,.13", "(0,2.81,0),.145,.13")
replacement=replacement.replace("(0,1.93,0)", "(0,2.90,0)")
replacement=replacement.replace("c.box('FrontWallCap',(0,2.18,1.41),(2.95,.69,.07),r,col,'MAT_IslandCliff')", "for side in (-1,1):c.box('FrontWallCap',(side*.97,2.18,1.41),(1.18,.69,.07),r,col,'MAT_IslandCliff')")
code=code[:start]+replacement+code[end:]
exec(compile(code,str(source),'exec'))
