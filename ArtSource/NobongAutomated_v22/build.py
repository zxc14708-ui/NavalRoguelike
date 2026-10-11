"""Reference-inspired open twin gun with progressive remote automation. Review only."""
from pathlib import Path
source=Path(__file__).resolve().parents[1]/'NobongAngular_v20'/'build.py'
code=source.read_text(encoding='utf8')
start=code.index(' # Thick chamfered cheeks')
end=code.index(" pitch=c.empty",start)
code=code[:start]+''' # Low mechanical pedestal; no tall rear armored tower.
 for side in (-1,1):
  c.frustum('TrunnionSupport',(side*.40,.02,.35),(.20,.64),(.16,.35),.52,yaw,col,'Naval Blue Grey')
  c.cyl('ElevationGear',(side*.515,.12,.43),.17,.085,yaw,col,'Gunmetal',16,(0,math.pi/2,0))
  c.cyl('GearHub',(side*.565,.12,.43),.095,.035,yaw,col,'Naval Blue Grey',12,(0,math.pi/2,0))
  c.cyl('TraverseMotor',(side*.43,-.34,.14),.105,.30,yaw,col,'Deck Grey',12,(math.pi/2,0,0))
  e._chamfer_box('AutomaticMagazine',(side*.64,-.22,.25),(.27,.46+level*.13,.33+level*.08),yaw,col,'Naval Blue Grey',.035)
  c.box('MagazineLid',(side*.64,-.22,.43+level*.04),(.29,.46+level*.13,.035),yaw,col,'Deck Grey')
  c.box('FeedHousing',(side*.40,-.11,.43),(.23,.22,.18),yaw,col,'Deck Grey')
  for yy in (-.18,-.10,-.02):
   c.box('FeedRib',(side*.40,yy,.53),(.19,.025,.025),yaw,col,'Naval Blue Grey')
  c.box('MagazineLatch',(side*.79,-.15,.27),(.025,.07,.10),yaw,col,'Gunmetal')
 c.frustum('FrontSlopingShield',(0,.30,.24),(.70,.15),(.61,.11),.37,yaw,col,'Naval Blue Grey')
 c.box('RearController',(0,-.39,.20),(.40,.25,.25),yaw,col,'Deck Grey')
 if level>=1:
  for side in (-1,1):
   c.frustum('AngularSideProtection',(side*.58,.01,.40),(.19,.71),(.15,.53),.58,yaw,col,'Naval Blue Grey')
   c.box('ArmourAccessPanel',(side*.685,-.06,.42),(.02,.30,.22),yaw,col,'Deck Grey')
  e._chamfer_box('RemoteControlUnit',(0,-.47,.39),(.58,.28,.28),yaw,col,'Naval Blue Grey',.045)
 if level==2:
  for side in (-1,1):
   e._chamfer_box('ExtendedFeedCassette',(side*.61,-.48,.30),(.37,.43,.48),yaw,col,'Naval Blue Grey',.05)
   c.box('CassetteServiceHatch',(side*.80,-.48,.31),(.025,.30,.25),yaw,col,'Deck Grey')
   for yy in (-.56,-.48,-.40):c.box('CassetteVent',(side*.816,yy,.33),(.012,.027,.12),yaw,col,'Gunmetal')
''' +code[end:]
code=code.replace("(-.14,.14)","(-.17,.17)")
code=code.replace(".083,.48", ".103,.48")
code=code.replace("'GunTube',(x,.91,0),.041,1.10,barrel,col,'Gunmetal'", "'GunTube',(x,.91,0),.046,1.10,barrel,col,'Naval Blue Grey'")
code=code.replace("  if level>0:\n   for yy in (.40,.52,.64):", "  if True:\n   for yy in (.25,.31,.37,.43,.49,.55,.61):")
code=code.replace(".088,.035,barrel,col,'Naval Blue Grey'", ".111,.032,barrel,col,'Gunmetal'")
start=code.index(" e._chamfer_box('OpticalSight'")
end=code.index(' c.finish(r)',start)
code=code[:start]+''' # Sight travels with the elevating gun; increasing sensor capability is visible.
 c.box('SightBracket',(.43,-.08,.20),(.09,.16,.36),barrel,col,'Deck Grey')
 e._chamfer_box('EOHead',(.43,-.04,.39),(.18+level*.07,.25+level*.06,.17+level*.045),barrel,col,'Naval Blue Grey',.025)
 c.box('EOWindow',(.43,.094+level*.03,.40),(.11+level*.045,.015,.09),barrel,col,'Radar Glass')
 if level==2:
  c.box('Rangefinder',(.43,.17,.30),(.13,.04,.075),barrel,col,'Gunmetal')
  c.box('RangefinderGlass',(.43,.196,.30),(.085,.012,.04),barrel,col,'Radar Glass')
  e._chamfer_box('MovingBreechCowl',(0,-.20,.13),(.68,.55,.25),barrel,col,'Naval Blue Grey',.06)
 # Presentation pose, exported with articulated pitch transform.
 pitch.rotation_euler.x=math.radians(18)
''' +code[end:]
code=code.replace("render('Nobong_Tiers',8.3)","render('Nobong_Tiers',8.3,target=(0,.25,.65),pos=(7,11,7))")
exec(compile(code,str(source),'exec'))
