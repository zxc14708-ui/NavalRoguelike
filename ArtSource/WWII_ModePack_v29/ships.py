from common import *
from weapons import dual_gun

def bridge():
 r,col=root('MOD_Ww2Bridge')
 clipped('IntegratedDeckhouse',(0,-.08,.40),(1.64,5.48,.80),r,col,'WW2 Hull',.14)
 clipped('ForwardBridgeLower',(0,1.46,1.02),(1.55,1.49,.47),r,col,'WW2 Hull',.12)
 clipped('Wheelhouse',(0,1.60,1.46),(1.47,1.35,.42),r,col,'WW2 Hull',.12)
 for x in (-.48,-.16,.16,.48):box('BridgeWindow',(x,2.278,1.50),(.23,.012,.16),r,col,'WW2 Glass')
 for side in (-1,1):
  for y in (1.26,1.62):box('SideBridgeGlass',(side*.744,y,1.50),(.012,.25,.16),r,col,'WW2 Glass')
  box('BridgeWing',(side*.70,1.38,1.72),(.30,1.48,.05),r,col)
  rail((side*.84,.74),(side*.84,2.08),1.75,r,col)
  box('CabinAccess',(side*.831,.54,.40),(.016,.35,.58),r,col,'WW2 Gun')
 clipped('OpenCommandPlatform',(0,1.48,1.76),(1.22,1.12,.08),r,col,'WW2 Deck',.08)
 for side in (-1,1):box('CommandSplinterWall',(side*.59,1.48,1.88),(.05,1.05,.23),r,col)
 director((0,1.44,2.05),.92,r,col)
 funnel((0,-1.42,.80),.70,1.29,r,col)
 # Two tripod legs are visibly supported by the deckhouse.
 mast((0,-.22,.80),1.76,r,col)
 for side in (-1,1):
  for y in (-2.10,-.85):
   tube('EngineRoomVent',(side*.62,y,.95),.07,.21,r,col,'WW2 Gun')
   box('VentHead',(side*.62,y,1.08),(.17,.16,.08),r,col,'WW2 Dark')
  for i,yy in enumerate((-2.29,-2.05)):
   c.empty('LaunchPoint_%02d'%((1 if side<0 else 3)+i),r,col,(side*.69,yy,1.03))
 c.empty('BridgeGunMount',r,col,(0,2.01,1.96))
 return r

def single_light(parent,col,loc,idx=''):
 yaw=c.empty(idx+'TurretPivot',parent,col,loc)
 cyl('LightGunPedestal',(0,0,.12),.16,.24,yaw,col,'WW2 Gun',10)
 box('LightGunShield',(0,.10,.37),(.46,.045,.36),yaw,col,'WW2 Hull')
 elev=c.empty(idx+'ElevationPivot',yaw,col,(0,.08,.36))
 box('LightGunReceiver',(0,-.03,0),(.13,.27,.12),elev,col,'WW2 Gun')
 tube('LightGunBarrel',(0,.38,0),.021,.51,elev,col)
 bore('LightGunMuzzle',0,.66,0,.03,elev,col)
 c.empty(idx+'Muzzle',elev,col,(0,.67,0))
 return yaw

def fittings(parent,col,length,width,deck,enemy=True):
 # Short gunboat rail runs, bollards and two diagonal life-raft cradles.
 for side in (-1,1):
  rail((side*width*.46,-length*.40),(side*width*.46,length*.26),deck,parent,col)
  for y in (-length*.40,length*.33):
   cyl('Bollard',(side*width*.30,y,deck+.055),.035,.11,parent,col,'WW2 Dark',8)
  box('RaftCradle',(side*width*.31,-length*.17,deck+.12),(.32,.58,.07),parent,col,'WW2 Gun')
  clipped('CarleyFloat',(side*width*.31,-length*.17,deck+.20),(.30,.54,.12),parent,col,'WW2 White',.08)
  box('FloatCentre',(side*width*.31,-length*.17,deck+.267),(.17,.36,.012),parent,col,'WW2 Deck')
 if enemy:
  for side in (-1,1):box('EnemyRecognitionBand',(side*width*.501,0,.20),(.012,length*.17,.10),parent,col,'WW2 Enemy Red')

def gunboat(fast):
 r,col=root('ENE_Ww2FastGunboat' if fast else 'ENE_Ww2Gunboat')
 p=c.empty('ModelPivot',r,col)
 L,W,D=(5.6,1.60,.45) if fast else (8.4,2.18,.58)
 hull('HardChineHull',L,W,D,p,col,keel=-.38 if fast else -.60)
 fittings(p,col,L,W,D)
 clipped('LowDeckhouse',(0,-.05,D+.28),(W*.66,L*.32,.56),p,col,'WW2 Enemy',.08)
 clipped('NavigationCabin',(0,L*.07,D+.69),(W*.58,L*.16,.34),p,col,'WW2 Enemy',.08)
 front=L*.15+.012
 for x in (-W*.17,W*.17):box('Windshield',(x,front,D+.71),(W*.22,.012,.15),p,col,'WW2 Glass')
 for side in (-1,1):
  box('SideWindow',(side*W*.295,L*.08,D+.71),(.012,L*.07,.14),p,col,'WW2 Glass')
  box('EngineHatch',(side*W*.18,-L*.14,D+.58),(W*.22,L*.11,.04),p,col,'WW2 Gun')
 if fast:
  single_light(p,col,(0,L*.32,D),'')
  aft=single_light(p,col,(0,-L*.36,D),'Aft_');aft.rotation_euler.z=math.pi
  beam('WhipAntenna',(0,-.12,D+.88),(0,-.12,D+1.46),.021,p,col,'WW2 Dark')
 else:
  yaw,pitch=dual_gun(p,col,(0,L*.31,D),.68,'',1,'WW2 Enemy')
  aft=single_light(p,col,(0,-L*.35,D),'Aft_');aft.rotation_euler.z=math.pi
  funnel((0,-L*.13,D+.56),.39,.48,p,col,'WW2 Enemy')
  mast((0,L*.07,D+.87),.73,p,col)
 return r

def cruiser():
 r,col=root('ENE_Ww2GunneryCruiser');p=c.empty('ModelPivot',r,col)
 L,W,D=18.,3.40,.77
 hull('CruiserHull',L,W,D,p,col,keel=-.90)
 # Long weather deck and superfiring B barbette, A/B/X six-gun layout from current code.
 clipped('ForwardShelterDeck',(0,1.66,1.08),(2.30,5.0,.60),p,col,'WW2 Enemy',.17)
 clipped('BridgeLower',(0,2.16,1.61),(1.91,1.93,.50),p,col,'WW2 Enemy',.13)
 clipped('BridgeUpper',(0,2.22,2.07),(1.64,1.55,.42),p,col,'WW2 Enemy',.12)
 for x in (-.58,-.19,.19,.58):box('BridgeWindow',(x,3.003,2.10),(.26,.015,.15),p,col,'WW2 Glass')
 for side in (-1,1):
  box('BridgeSideGlass',(side*.83,2.22,2.10),(.012,.77,.15),p,col,'WW2 Glass')
  box('BridgeWing',(side*1.04,2.08,2.29),(.48,1.35,.06),p,col,'WW2 Enemy')
  rail((side*1.25,1.50),(side*1.25,2.68),2.34,p,col)
 director((0,2.16,2.57),1.19,p,col)
 mast((0,.83,1.40),2.40,p,col)
 funnel((0,-.73,D),.84,1.79,p,col,'WW2 Enemy')
 funnel((0,-2.53,D),.79,1.63,p,col,'WW2 Enemy')
 clipped('AftSuperstructure',(0,-4.24,1.01),(1.78,2.00,.48),p,col,'WW2 Enemy',.10)
 director((0,-4.24,1.47),.79,p,col)
 for prefix,y,z,aft in [('A_',6.48,D,False),('B_',4.32,1.39,False),('X_',-6.72,D,True)]:
  turret,elev=dual_gun(p,col,(0,y,z),1.03,prefix,2,'WW2 Enemy')
  turret.parent.rotation_euler.z=math.pi if aft else 0
 for side,ident in [(-1,'Secondary_P_'),(1,'Secondary_S_')]:
  t=single_light(p,col,(side*1.30,-.20,D+.10),ident);t.rotation_euler.z=-side*math.pi/2
  box('SecondaryPlatform',(side*1.31,-.20,D+.035),(.70,.85,.07),p,col,'WW2 Enemy')
  # Boats lie on cradles with side davits, not floating decor.
  box('BoatCradle',(side*1.12,-3.05,D+.09),(.60,1.48,.12),p,col,'WW2 Gun')
  clipped('MotorLaunch',(side*1.12,-3.05,D+.27),(.54,1.49,.26),p,col,'WW2 Hull',.15)
  clipped('LaunchCockpit',(side*1.12,-3.05,D+.405),(.37,1.10,.02),p,col,'WW2 Deck',.10)
  for yy in (-3.65,-2.45):beam('BoatDavit',(side*1.42,yy,D),(side*1.42,yy,D+.68),.034,p,col,'WW2 Gun')
  rail((side*1.58,-7.13),(side*1.58,5.60),D,p,col)
  box('EnemyRecognitionBand',(side*1.705,-1.20,.23),(.016,2.2,.12),p,col,'WW2 Enemy Red')
 c.empty('DamageSmoke_01',p,col,(0,-.73,2.70));c.empty('DamageFire_01',p,col,(0,-4.24,1.5))
 return r
