"""Second review iteration: role silhouettes and visibly different equipment tiers.
Reuses the v13 authoring pipeline. Does not modify Unity Assets.
"""
from pathlib import Path
source=Path(__file__).resolve().parents[1]/'EscortConcept_v13'/'build.py'
code=source.read_text(encoding='utf8')
code=code.replace("('PB',[0])", "('PB',[0])")
code=code.replace("scale=.9+tier*.10,stealth=True", "scale={1:.82,2:1.14,3:1.48}[tier],twin=tier==3,stealth=True")
code=code.replace("scale=.65+tier*.10,prefix='AftGun'", "scale={1:.62,2:.95,3:1.23}[tier],prefix='AftGun'")
code=code.replace("bridge_height=.99,width=2.78", "bridge_height={1:.80,2:1.04,3:1.26}[tier],width=2.78")
code=code.replace("if tier>=2:\n    for side in (-1,1):mg(root,col,side*1.02,-2.65,'STK')", """if tier==2:
    for side in (-1,1):mg(root,col,side*1.02,-2.65,'STK')
   if tier==3:
    # Outboard sponsons carry two actual autocannon turrets, away from the funnel.
    for side in (-1,1):
     s=c.empty('BroadsideBattery_'+str(side),root,col,(side*1.03,-2.43,0))
     c.frustum('BatteryDeckSponson',(0,0,d+.10),(.48,.90),(.67,1.03),.20,s,col,c.SUPER)
     c.gun(s,col,0,d+.20,scale=.59,prefix='BroadsideGun_'+str(side),twin=True,heading=-side*math.pi/2)
     c.box('BatteryRolePanel',(side*.32,0,d+.12),(.02,.67,.14),s,col,c.role_mat('STK'))""")
code=code.replace("c.ciws(root,col,-.68,deck=d+.70,scale=.55,prefix='MidDeckCIWS',heading=math.pi)\n    c.box('CIWSRaisedServicePlatform',(0,-.68,d+.34),(.85,.75,.68),root,col,c.SUPER)", """# Heavy stepped turret foundations make the T3 gunnery profile unmistakable.
    c.box('ArmouredForeTurretDeck',(0,2.62,d+.09),(1.37,1.38,.18),root,col,c.SUPER)
    c.box('ArmouredAftTurretDeck',(0,-3.34,d+.08),(1.18,1.23,.16),root,col,c.SUPER)
    for ob in list(c.a.descendants(root)):
     if ob.parent==root and ob.get('export_name','').startswith(('PrimaryGun','AftGun')):ob.location.z+=.18
    c.box('FireDirectionCabin',(0,.12,roof+.20),(.88,.70,.40),root,col,c.SUPER)
    c.box('FireDirectionOptics',(0,.48,roof+.23),(.67,.025,.15),root,col,c.SENSOR)""")
code=code.replace("asw._helipad(root,col,tier)", """asw._helipad(root,col,tier)
   if tier==3:
    # Stern quarter working platforms: recovery gear stays outside the landing circle.
    for side in (-1,1):
     c.frustum('ASWWorkingSponson',(side*1.20,-3.52,d-.025),(.34,1.48),(.54,1.60),.15,root,col,c.SUPER)
     c.box('ASWWorkingDeck',(side*1.20,-3.52,d+.06),(.53,1.59,.025),root,col,c.DECK)
     c.beam('SonarRecoveryAFrame',(side*1.36,-4.05,d+.08),(side*1.36,-3.72,d+1.05),.07,root,col,c.SUPER)
    # Offset port recovery gantry, not across the helicopter touchdown area.
    c.beam('SonarRecoveryBoom',(-1.36,-3.72,d+1.05),(-.82,-3.72,d+1.05),.07,root,col,c.SUPER)
    c.beam('RecoveryCable',(-1.08,-3.72,d+1.05),(-1.08,-3.72,d+.44),.016,root,col,c.DARK)
    c.sphere('TowedSonarBody',(1.18,-3.47,d+.30),(.13,.48,.13),root,col,c.role_mat('ASW'),12,6)
    c.box('SonarStowageCradle',(1.18,-3.47,d+.16),(.36,.80,.20),root,col,c.SUPER)
    c.box('SonarTailFin',(1.18,-3.86,d+.34),(.45,.16,.035),root,col,c.SUPER)""")
code=code.replace("asw._torpedo_banks(root,col,tier);depth(root,col,tier)", """asw._torpedo_banks(root,col,tier);depth(root,col,tier)
   if tier==3:
    # Clearly recognizable forward multi-tube ASW rocket mortar.
    p=c.empty('ASWRocketPivot',root,col,(0,1.72,d+.08))
    c.cyl('ASWRocketBearing',(0,0,.06),.33,.12,p,col,c.DECK,12)
    c.frustum('ASWRocketYoke',(0,0,.24),(.66,.42),(.57,.32),.36,p,col,c.SUPER)
    for row in range(2):
     for j in range(3):
      x=(j-1)*.19;z=.38+row*.18
      c.cyl('ASWMortarTube',(x,0,z),.08,.55,p,col,c.SUPER,8,(math.pi/2-.35,0,0))
      c.cyl('ASWMortarMouth',(x,.267,z+.095),.065,.018,p,col,c.DARK,8,(math.pi/2-.35,0,0))""")
code=code.replace("if tier>=2:asw._sonobuoys(root,col,tier);asw._tow_sonar(root,col,tier)", """if tier>=2:asw._sonobuoys(root,col,tier)
   if tier==2:asw._tow_sonar(root,col,tier)
   if tier==3:
    c.cyl('HeavyTowWinch',(-1.13,-3.23,d+.30),.23,.36,root,col,c.DARK,12,(0,math.pi/2,0))
    for x in (-1.34,-.92):c.box('WinchSupport',(x,-3.23,d+.19),(.06,.54,.38),root,col,c.SUPER)
    c.empty('SonarTowPoint',root,col,(-1.1,-4.25,d+.18))""")
# Left-to-right stage order when seen from the chosen camera.
code=code.replace("(i-(len(chosen)-1)/2)*4.6", "((len(chosen)-1)/2-i)*5.2")
code=code.replace("data.ortho_scale=17", "data.ortho_scale=19")
# Dedicated close views preserve the detail that disappears on comparison sheets.
code=code.replace("for rr,t,r in roots:\n for ob in list(c.a.descendants(r))+[r]:ob.hide_render=False", """for role in ['STK','ASW']:
 for rr,t,r in roots:
  for ob in list(c.a.descendants(r))+[r]:ob.hide_render=not(rr==role and t==3)
  if rr==role and t==3:r.location.x=0
 data.ortho_scale=11
 scene.render.filepath=str(OUT/(role+'_T3_Close.png'));bpy.ops.render.render(write_still=True)
for rr,t,r in roots:
 for ob in list(c.a.descendants(r))+[r]:ob.hide_render=False""")
exec(compile(code,str(source),'exec'))
