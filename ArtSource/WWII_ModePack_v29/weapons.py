from common import *

def dual_gun(parent,col,loc=(0,0,0),scale=1,prefix='',count=2,mat='WW2 Hull'):
 # Twin 5in/38 silhouette: broad faceted gunhouse, split front plate and high-angle cradle.
 carrier=c.empty(prefix+'GunAssembly',parent,col,loc)
 cyl(prefix+'Barbette',(0,0,.105),.64,.21,carrier,col,'WW2 Gun',16)
 yaw=c.empty(prefix+'TurretPivot',carrier,col,(0,0,.22))
 clipped('LowerGunhouse',(0,-.12,.16),(1.34,1.34,.30),yaw,col,mat,.13)
 # Rear and side enclosure, left open around the moving gun breeches.
 box('RearBulkhead',(0,-.73,.49),(1.25,.075,.64),yaw,col,mat)
 for side in (-1,1):
  sideplate=plate('GunhouseSide',[(side*.66,-.72,.28),(side*.66,.65,.28),(side*.66,.65,.67),(side*.66,.37,.92),(side*.66,-.72,.92)],.035,yaw,col,mat)
  box('ServiceDoor',(side*.688,-.37,.49),(.018,.32,.37),yaw,col,'WW2 Gun')
  box('DoorHandle',(side*.70,-.29,.50),(.024,.07,.027),yaw,col,'WW2 Dark')
  for yy in (-.58,-.43,-.28):box('VentSlot',(side*.70,yy,.79),(.015,.065,.033),yaw,col,'WW2 Dark')
 plate('GunhouseRoof',[(-.66,-.74,.92),(.66,-.74,.92),(.66,.37,.92),(-.66,.37,.92)],.05,yaw,col,mat)
 xs=[-.22,.22] if count==2 else [0]
 # Three narrow vertical strips leave real elevation slots.
 segments=[(-.54,.23),(0,.23),(.54,.23)] if count==2 else [(-.43,.41),(.43,.41)]
 for x,w in segments:box('SplitFrontArmor',(x,.644,.52),(w,.07,.47),yaw,col,mat)
 pitch=c.empty(prefix+'ElevationPivot',yaw,col,(0,.30,.48))
 for i,x in enumerate(xs,1):
  clipped('MovingGunMantlet',(x,.32,.05),(.24,.22,.35),pitch,col,'WW2 Gun',.035)
  tube('RecoilSleeve',(x,.50,0),.084,.48,pitch,col,'WW2 Gun')
  tube('GunBarrel',(x,1.14,0),.043,1.04,pitch,col,'WW2 Dark')
  bore('OpenMuzzle',x,1.71,0,.055,pitch,col)
  c.empty(prefix+'Muzzle_%02d'%i,pitch,col,(x,1.72,0))
 c.empty(prefix+'Muzzle',pitch,col,(0,1.72,0))
 # Mesh geometry scaling, preserving unit object transforms and socket positions.
 if scale!=1:
  for o in c.descendants(carrier):
   if o==carrier:continue
   o.location*=scale
   if o.type=='MESH':o.data.transform(Matrix.Scale(scale,4))
 return yaw,pitch

def quad_aa(parent,col,loc=(0,0,0),prefix=''):
 carrier=c.empty(prefix+'AAAssembly',parent,col,loc)
 cyl('AATubDeck',(0,0,.07),.87,.14,carrier,col,'WW2 Deck',12)
 # Low segmented splinter tub: no obstruction at the barrel centreline.
 for i in range(8):
  a=i*math.tau/8
  box('SplinterTub',(math.sin(a)*.79,math.cos(a)*.79,.22),(.59,.045,.25),carrier,col,'WW2 Hull',(0,0,-a))
 yaw=c.empty(prefix+'TurretPivot',carrier,col,(0,0,.15))
 cyl('PowerTraverseRing',(0,0,.055),.47,.11,yaw,col,'WW2 Gun',16)
 clipped('Carriage',(0,-.05,.20),(1.09,.70,.30),yaw,col,'WW2 Gun',.06)
 for side in (-1,1):
  box('TrunnionSupport',(side*.58,.02,.40),(.12,.45,.41),yaw,col,'WW2 Hull')
  box('GunnerSeat',(side*.56,-.46,.29),(.20,.21,.055),yaw,col,'WW2 Deck')
  beam('SeatSupport',(side*.56,-.46,.13),(side*.56,-.46,.29),.035,yaw,col,'WW2 Dark')
 pitch=c.empty(prefix+'ElevationPivot',yaw,col,(0,.06,.50))
 for i,x in enumerate((-.36,-.12,.12,.36),1):
  box('BoforsBreech',(x,-.16,0),(.20,.46,.20),pitch,col,'WW2 Gun')
  tube('BoforsCoolingJacket',(x,.30,0),.057,.50,pitch,col,'WW2 Hull')
  tube('BoforsBarrel',(x,.77,0),.025,.52,pitch,col,'WW2 Dark')
  bore('OpenMuzzle',x,1.07,0,.035,pitch,col)
  box('VerticalClipGuide',(x,-.14,.20),(.11,.15,.25),pitch,col,'WW2 Hull')
  for j in range(3):tube('ReadyClipRound',(x-.032+j*.032,-.14,.33),.013,.10,pitch,col,'WW2 Brass')
  c.empty(prefix+'Muzzle_%02d'%i,pitch,col,(x,1.08,0))
 c.empty(prefix+'Muzzle',pitch,col,(0,1.08,0))
 box('OpticalSight',(0,-.31,.38),(.15,.08,.12),pitch,col,'WW2 Dark')
 return yaw,pitch

def torpedo_mount(parent,col):
 box('MountingDeck',(0,0,.07),(1.78,1.78,.14),parent,col,'WW2 Deck')
 cyl('TrainingRing',(0,0,.22),.48,.15,parent,col,'WW2 Gun',16)
 mount=c.empty('TubeMount',parent,col,(0,0,.35))
 clipped('Cradle',(0,0,.04),(1.40,1.02,.15),mount,col,'WW2 Gun',.07)
 for i,x in enumerate((-.465,-.155,.155,.465),1):
  tube('TorpedoTube',(x,.05,.22),.133,1.91,mount,col,'WW2 Hull')
  bore('OpenTubeMouth',x,1.08,.22,.143,mount,col)
  tube('RearBreechCap',(x,-.94,.22),.146,.075,mount,col,'WW2 Gun')
  for yy in (-.64,.60):
   tube('TubeClamp',(x,yy,.22),.146,.045,mount,col,'WW2 Gun')
   box('TubeSaddle',(x,yy,.07),(.25,.12,.16),mount,col,'WW2 Gun')
  box('FiringValve',(x,-.56,.38),(.09,.12,.06),mount,col,'WW2 Dark')
  c.empty('LaunchPoint_%02d'%i,mount,col,(x,1.10,.22))
 box('GyroSettingCabinet',(0,-.36,.55),(.44,.47,.34),mount,col,'WW2 Hull')
 box('ControlPanel',(0,-.61,.56),(.31,.02,.20),mount,col,'WW2 Gun')
 for x in (-.09,.09):cyl('ControlDial',(x,-.626,.58),.035,.015,mount,col,'WW2 White',8,(math.pi/2,0,0))
 return mount

def projectile(name,torpedo=False,air=False,bomb=False,aa=False):
 r,col=root(name)
 length=1.60 if air else 1.86 if torpedo else .64 if bomb else .13 if aa else .32
 radius=.10 if torpedo else .11 if bomb else .018 if aa else .043
 mat='WW2 Gun' if torpedo else 'WW2 Olive' if bomb else 'WW2 Brass'
 profile=[(-length*.5,radius*.56),(-length*.34,radius),(length*.25,radius),(length*.41,radius*.72),(length*.5,radius*.07)]
 c.lathe('OrdnanceBody',(0,0,0),profile,r,col,mat,12,False,(math.pi/2,0,0))
 # Rotating z-lathe by -90deg X points the tapered nose to +Y.
 ob=next(o for o in c.descendants(r) if o.type=='MESH')
 ob.data.transform(Matrix.Rotation(math.pi,4,'Z'))
 if torpedo or bomb:
  for side in (-1,1):
   box('TailFin',(side*radius,-length*.37,0),(radius*.92,length*.18,.018),r,col,'WW2 Gun')
   box('TailFin',(0,-length*.37,side*radius),(.018,length*.18,radius*.92),r,col,'WW2 Gun')
  c.empty('Exhaust',r,col,(0,-length*.5,0))
 c.empty('ImpactPoint',r,col,(0,length*.5,0))
 return r
