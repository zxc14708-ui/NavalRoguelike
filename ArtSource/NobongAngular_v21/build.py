"""Strong silhouette progression, same twin 40 mm gun identity. Review only."""
from pathlib import Path
source=Path(__file__).resolve().parents[1]/'NobongAngular_v20'/'build.py'
code=source.read_text(encoding='utf8')
# Basic remains low and open. Advanced tiers gain substantial connected armor.
code=code.replace(".23+level*.045,.56+level*.045,.40", ".23+level*.065,.56+level*.06,.40+level*.10")
code=code.replace("(side*.66,-.30,.46),(.26,.55,.035)","(side*.66,-.30,.46+level*.05),(.26+level*.04,.55+level*.06,.035)")
code=code.replace("c.finish(r)", """if level>=1:
  # Rear armored enclosure ends behind the elevation pivot's moving breech.
  e._chamfer_box('ArmouredRearEquipmentHouse',(0,-.45,.54 if level==1 else .68),
                (1.08,.42,.63 if level==1 else .91),yaw,col,'Naval Blue Grey',.10)
  for side in (-1,1):
   c.frustum('OuterArmourShoulder',(side*.57,-.11,.51),(.26,.87),(.23,.70),
             .78 if level==1 else .96,yaw,col,'Naval Blue Grey')
   c.box('SideServicePanel',(side*.715,-.27,.42),(.02,.38,.24),yaw,col,'Deck Grey')
   c.box('PanelHandle',(side*.73,-.27,.43),(.026,.12,.035),yaw,col,'Gunmetal')
  c.box('RearRoof',(0,-.45,.87 if level==1 else 1.15),(1.00,.37,.045),yaw,col,'Deck Grey')
 if level==2:
  # Tall integrated optical head, not a CIWS radar dome.
  c.frustum('IntegratedFireControlPedestal',(0,-.45,1.24),(.42,.32),(.31,.27),.18,yaw,col,'Naval Blue Grey')
  e._chamfer_box('IntegratedEOHead',(0,-.42,1.40),(.53,.40,.23),yaw,col,'Naval Blue Grey',.04)
  c.box('WideEOFace',(0,-.211,1.42),(.41,.018,.10),yaw,col,'Radar Glass')
  c.box('SecondaryEOFace',(.13,-.208,1.33),(.11,.023,.055),yaw,col,'Radar Glass')
  for side in (-1,1):
   e._chamfer_box('ReinforcedLowerFeed',(side*.63,.16,.23),(.31,.45,.32),yaw,col,'Deck Grey',.035)
   c.box('FeedInspectionPanel',(side*.63,.393,.25),(.22,.022,.15),yaw,col,'Naval Blue Grey')
  for x in (-.34,-.17,0,.17,.34):
   c.box('RearCoolingGrille',(x,-.672,.88),(.07,.02,.23),yaw,col,'Gunmetal')
 c.finish(r)""")
code=code.replace("render('Nobong_Tiers',8.3)","render('Nobong_Tiers',8.8,target=(0,.25,.8),pos=(7,11,6))")
code=code.replace("render('Nobong_T%d'%i,3.4)","render('Nobong_T%d'%i,3.8,target=(0,.25,.8),pos=(7,11,6))")
exec(compile(code,str(source),'exec'))
