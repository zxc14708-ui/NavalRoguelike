"""Player aviation block and helicopter upgrade sets; review exports only."""
from pathlib import Path
src=Path(__file__).resolve().parents[1]
base=(src/'EscortHelicopters_v19/build.py').read_text(encoding='utf8')
exec(compile(base[:base.index('scene=bpy.context.scene')],str(src/'EscortHelicopters_v19/build.py'),'exec'))
import miniature_enemy_ships as e
roots=[];report=[]
old=(src/'EscortHelicopters_v18/build.py').read_text(encoding='utf8')
build=old[old.index('def torpedo('):old.index('visible(roots)',old.index('def torpedo('))]
build=build.replace('HEL_EscortAsw_T','HEL_PlayerAsw_T')
build=build.replace('(side*.17,-.20,.78)','(side*.17,-.20,1.00)').replace('(side*.17,.14,.81)','(side*.17,.14,1.03)').replace('(side*.17,-.57,.79)','(side*.17,-.57,1.01)').replace('(0,-.63,.80)','(0,-.59,.95)')
exec(compile(build,'helicopters','exec'))
helis=list(roots);decks=[]
for tier in (1,2,3):
 col=c.collection('PlayerDeck_T%d'%tier)
 r=c.empty('MOD_HeliDeck'+('' if tier==1 else '_U%d'%(tier-1)),None,col)
 e._chamfer_box('BlockBase',(0,0,.075),(1.8,1.8,.15),r,col,'Naval Blue Grey',.09)
 e._chamfer_box('LandingSurface',(0,0,.18),(1.76,1.76,.06),r,col,'Deck Grey',.08)
 z=.213
 for side in (-1,1):
  c.box('HLeg',(side*.17,0,z),(.055,.45,.006),r,col,'Medical White')
  c.box('PadBoundary',(side*.55,0,z),(.022,1.12,.006),r,col,'Medical White')
  c.box('PadBoundary',(0,side*.56,z),(1.12,.022,.006),r,col,'Medical White')
  for yy in (-.65,0,.65):
   c.box('FlushGuideLight',(side*.83,yy,.218),(.045,.08,.012),r,col,'Role ASW')
  for yy in (-.43,.43):c.cyl('TieDown',(side*.50,yy,z),.025,.005,r,col,'Gunmetal',8)
 c.box('HBar',(0,0,z),(.34,.055,.006),r,col,'Medical White')
 if tier>=2:
  # Equipment stays outside landing gear and below rotating blade height.
  for side in (-1,1):
   e._chamfer_box('ServicePod',(side*.73,-.13,.335),(.25,.75,.24),r,col,'Naval Blue Grey',.03)
   c.box('ServicePodLid',(side*.73,-.13,.462),(.25,.74,.025),r,col,'Deck Grey')
   c.box('AviationBlueStripe',(side*.865,-.13,.35),(.015,.50,.04),r,col,'Role ASW')
   c.box('ServiceLatch',(side*.73,.253,.34),(.075,.02,.055),r,col,'Medical White')
  c.cyl('FuelHoseReel',(-.73,-.65,.32),.095,.15,r,col,'Gunmetal',12,(0,math.pi/2,0))
  c.cyl('ReelEnd',(-.815,-.65,.32),.11,.02,r,col,'Naval Blue Grey',12,(0,math.pi/2,0))
 if tier==3:
  for side in (-1,1):
   # Forward corner automatic service manipulators, parked outboard.
   c.cyl('ServiceArmBase',(side*.73,.58,.27),.095,.12,r,col,'Deck Grey',10)
   c.beam('ServiceArmLower',(side*.73,.58,.31),(side*.77,.61,.59),.065,r,col,'Naval Blue Grey')
   c.beam('ServiceArmUpper',(side*.77,.61,.59),(side*.75,.34,.64),.06,r,col,'Medical White')
   c.box('ServiceCoupler',(side*.75,.32,.63),(.10,.10,.08),r,col,'Deck Grey')
   c.box('ArmSensor',(side*.75,.264,.65),(.055,.015,.03),r,col,'Role ASW')
   c.box('ServiceBayVent',(side*.873,-.13,.29),(.015,.42,.075),r,col,'Gunmetal')
  for xx in (-.26,.26):
   c.box('DeckCaptureRail',(xx,0,z),(.025,.85,.008),r,col,'Gunmetal')
  c.box('CaptureGrid',(0,-.25,z),(.22,.23,.008),r,col,'Gunmetal')
 c.empty('LandingSpot',r,col,(0,0,.22))
 export(r);decks.append(r)
 report.append({'asset':name(r),'tier':tier,'footprint':[1.8,1.8],'landing_height':.22,'triangles':c.tris(r)})
# Borrow only studio and camera helpers, not escort model assembly.
studio=base[base.index('scene=bpy.context.scene'):base.index('roots=[];report=[]')]
exec(compile(studio,'studio','exec'))
for i,(deck,heli) in enumerate(zip(decks,helis)):
 deck.location.x=heli.location.x=(1-i)*3.5
 heli.location.z=.22
visible(decks+helis)
render('PlayerAviation_Tiers',(0,-.35,.55),(8,13,10),10.7)
for i,(deck,heli) in enumerate(zip(decks,helis)):
 visible([deck,heli]);deck.location.x=heli.location.x=0
 render('PlayerAviation_T%d'%(i+1),(0,-.40,.65),(5,8,5),3.9)
 deck.location.x=heli.location.x=(1-i)*3.5
visible(decks)
render('PlayerDecks_Only',(0,0,.2),(8,13,10),10.3)
visible(decks+helis)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'PlayerAviation.blend'))
(OUT/'manifest.json').write_text(json.dumps(report,indent=2))
