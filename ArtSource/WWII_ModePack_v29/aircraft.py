from common import *

def fuselage(n,sections,p,col,mat='WW2 Olive'):
 # 8-sided loft with explicit nose and tail caps.
 verts=[]
 for y,w,h,z in sections:
  for i in range(8):
   a=i*math.tau/8;verts.append((w*math.cos(a),y,z+h*math.sin(a)))
 faces=[tuple(reversed(range(8)))]
 for k in range(len(sections)-1):
  for i in range(8):j=(i+1)%8;faces.append((k*8+i,k*8+j,(k+1)*8+j,(k+1)*8+i))
 faces.append(tuple(range((len(sections)-1)*8,len(sections)*8)))
 return c.mesh(n,verts,faces,p,col,mat)

def propeller(parent,col,y,radius):
 spin=c.empty('Propeller',parent,col,(0,y,0))
 cyl('Spinner',(0,0,0),.075,.12,spin,col,'WW2 Air Dark',12,(math.pi/2,0,0))
 for i in range(3):
  a=i*math.tau/3
  pts=[]
  for x,z in [(.025,.06),(.055,radius*.91),(-.015,radius),(-.05,.10)]:
   pts.append((x*math.cos(a)-z*math.sin(a),0,x*math.sin(a)+z*math.cos(a)))
  plate('PropellerBlade',pts,.014,spin,col,'WW2 Dark')
 return spin

def aircraft(torpedo):
 r,col=root('ENE_Ww2TorpedoBomber' if torpedo else 'ENE_Ww2DiveBomber')
 L=2.60 if torpedo else 2.40;span=3.60 if torpedo else 3.20
 # Avenger-like broad radial cowling, Stuka-like narrow liquid-cooled nose.
 sections=[(-L*.50,.018,.04,.02),(-L*.38,.07,.095,.015),(-L*.23,.13,.15,.01),
           (0,.235 if torpedo else .17,.23 if torpedo else .17,0),
           (L*.23,.245 if torpedo else .16,.23 if torpedo else .18,0),
           (L*.46,.22 if torpedo else .115,.205 if torpedo else .15,0)]
 fuselage('Fuselage',sections,r,col)
 if torpedo:
  tube('RadialEngineCowling',(0,L*.43,0),.25,.31,r,col,'WW2 Air Dark')
  tube('CowlingFrontRing',(0,L*.494,0),.245,.025,r,col,'WW2 Olive')
  tube('EngineFace',(0,L*.502,0),.185,.012,r,col,'WW2 Dark')
  for i in range(7):
   a=i*math.tau/7
   box('RadialCylinder',(math.sin(a)*.13,L*.513,math.cos(a)*.13),(.04,.018,.07),r,col,'WW2 Gun')
 else:
  clipped('ChinRadiator',(0,L*.34,-.15),(.20,.32,.17),r,col,'WW2 Air Dark',.025)
  box('RadiatorIntake',(0,L*.41,-.15),(.14,.018,.09),r,col,'WW2 Dark')
 for side in (-1,1):
  # Inverted gull on dive bomber, nearly level broad wings on torpedo bomber.
  rootz=-.075;midz=-.10 if torpedo else -.20;tipz=-.045 if torpedo else -.07
  plate('WingInner',[(side*.16,.35,rootz),(side*.62,.29,midz),(side*.62,-.20,midz),(side*.15,-.28,rootz)],.052,r,col,'WW2 Olive')
  plate('WingOuter',[(side*.62,.29,midz),(side*span*.48,.12,tipz),(side*span*.50,-.04,tipz),(side*span*.42,-.30,tipz),(side*.62,-.20,midz)],.042,r,col,'WW2 Olive')
  plate('Aileron',[(side*.85,-.235,midz+.017),(side*span*.42,-.30,tipz+.017),(side*span*.45,-.24,tipz+.017),(side*.85,-.175,midz+.017)],.008,r,col,'WW2 Air Dark')
  box('WingRecognitionStripe',(side*span*.36,-.045,tipz+.04),(.15,.39,.008),r,col,'WW2 Enemy Red')
  # Tail span kept ahead of fin and away from main wing.
  plate('Tailplane',[(side*.055,-L*.35,.05),(side*.53,-L*.40,.07),(side*.48,-L*.49,.07),(side*.04,-L*.46,.05)],.026,r,col,'WW2 Olive')
  if not torpedo:
   clipped('FixedGearFairing',(side*.58,.15,-.44),(.14,.32,.40),r,col,'WW2 Olive',.04)
   cyl('FixedWheel',(side*.58,.15,-.60),.105,.065,r,col,'WW2 Dark',12,(0,math.pi/2,0))
   # Separate dive brake below the wing, slotted strips rather than featureless sticks.
   for xx in (.72,.82,.92,1.02):box('DiveBrakeSlot',(side*xx,.15,-.22),(.055,.10,.018),r,col,'WW2 Dark')
  for yy in (L*.29,L*.23,L*.17):tube('EngineExhaust',(side*.19,yy,-.035),.023,.06,r,col,'WW2 Dark')
 # Vertical tail, closed planar section.
 plate('TailFin',[(0,-L*.33,.035),(0,-L*.38,.41),(0,-L*.47,.40),(0,-L*.50,.03)],.035,r,col,'WW2 Olive')
 # Raised, long framed greenhouse with separate front and side windows.
 canopy=[(-.42,.11,.04,.27),(.02,.15,.10,.28),(.36,.14,.10,.25),(.44,.08,.035,.23)] if torpedo else [(-.55,.085,.07,.24),(-.10,.115,.105,.25),(.25,.10,.10,.23),(.39,.035,.03,.20)]
 fuselage('GreenhouseCanopy',canopy,r,col,'WW2 Glass')
 for y in (-.33,-.10,.13,.28):
  a,b=next((canopy[j],canopy[j+1]) for j in range(len(canopy)-1) if canopy[j][0]<=y<=canopy[j+1][0])
  t=(y-a[0])/(b[0]-a[0]);w=a[1]+(b[1]-a[1])*t;h=a[2]+(b[2]-a[2])*t;zz=a[3]+(b[3]-a[3])*t
  arch=[(-w,y,zz),(-w*.707,y,zz+h*.707),(0,y,zz+h),(w*.707,y,zz+h*.707),(w,y,zz)]
  for j in range(4):beam('CanopyFrame',arch[j],arch[j+1],.011,r,col,'WW2 Olive')
 for j in range(len(canopy)-1):
  a,b=canopy[j],canopy[j+1];beam('CanopySpine',(0,a[0],a[3]+a[2]),(0,b[0],b[3]+b[2]),.012,r,col,'WW2 Olive')
 if torpedo:
  c.sphere('DorsalTurret',(0,-.46,.25),(.28,.30,.28),r,col,'WW2 Glass',12,6)
  tube('RearDefensiveGun',(0,-.72,.30),.016,.32,r,col,'WW2 Dark')
  # Open belly bay exposes the torpedo without external pylons on an Avenger-like airframe.
  box('BombBayShadow',(0,.03,-.218),(.20,.89,.02),r,col,'WW2 Dark')
  for side in (-1,1):box('OpenBombBayDoor',(side*.16,.03,-.24),(.025,.92,.16),r,col,'WW2 Olive')
  tube('CarriedTorpedo',(0,.06,-.27),.070,1.02,r,col,'WW2 Gun')
  tube('TorpedoWarheadMark',(0,.45,-.27),.073,.09,r,col,'WW2 Orange')
  for side in (-1,1):box('TorpedoTailFin',(side*.07,-.42,-.27),(.08,.13,.012),r,col,'WW2 Dark')
  c.empty('TorpedoDropPoint',r,col,(0,.06,-.35))
 else:
  tube('CarriedBomb',(0,.10,-.34),.085,.47,r,col,'WW2 Gun')
  tube('BombNoseBand',(0,.28,-.34),.086,.06,r,col,'WW2 Enemy Red')
  for side in (-1,1):
   beam('BombReleaseCrutch',(side*.10,.28,-.14),(side*.10,.06,-.31),.025,r,col,'WW2 Dark')
   box('BombTailFin',(side*.07,-.12,-.34),(.06,.11,.014),r,col,'WW2 Dark')
  c.empty('BombDropPoint',r,col,(0,.1,-.44))
 propeller(r,col,L*.52,.44 if torpedo else .40)
 return r
