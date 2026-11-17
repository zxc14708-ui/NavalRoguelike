"""Compact fleet supply ship, T-AKE equipment cues; review-only miniature."""
from pathlib import Path
src=Path(__file__).resolve().parents[1]/'CoastalBatterySupply_v25/build.py'
old=src.read_text(encoding='utf8')
exec(compile(old[:old.index('def build_battery():')],str(src),'exec'))
c.COLORS.update({'FleetGrey':(.33,.40,.42,1),'LightGrey':(.58,.65,.65,1),'DeckDark':(.13,.19,.21,1)})
c.setup()
r,col=root('ALLY_SupplyShip_Art')
# Matched station outlines for hull, waterline belt and deck avoid box overhangs.
stations=[(-7.75,1.43),(-6.5,1.92),(-3.5,2.10),(2.8,2.10),(5.4,1.85),(6.7,1.16),(7.75,.045)]
hull('UnderwaterHull',[(y,w*.88,-1.03,-.12) for y,w in stations],r,col,'SupplyHullDark')
hull('UpperHull',[(y,w,-.12,1.08) for y,w in stations],r,col,'FleetGrey')
hull('WaterlineBand',[(y,w+.006,-.16,-.035) for y,w in stations],r,col,'Gunmetal')
hull('FittedDeck',[(y,w*.975,1.08,1.15) for y,w in stations],r,col,'DeckDark')
def rail(start,end,z):
 a=Vector((start[0],start[1],z));b=Vector((end[0],end[1],z))
 c.beam('GuardRail',a+Vector((0,0,.30)),b+Vector((0,0,.30)),.025,r,col,'LightGrey')
 c.beam('MidRail',a+Vector((0,0,.15)),b+Vector((0,0,.15)),.018,r,col,'LightGrey')
 count=max(1,int((b-a).length/.65))
 for i in range(count+1):
  p=a.lerp(b,i/count);c.beam('RailPost',p,p+Vector((0,0,.32)),.027,r,col,'LightGrey')
for side in (-1,1):
 for k in range(len(stations)-1):
  y,w=stations[k];yn,wn=stations[k+1]
  rail((side*w*.96,y),(side*wn*.96,yn),1.15)
 # Repeated freeing ports rather than solid decorative strips.
 for y in (-5.8,-3.0,0,2.6):c.box('FreeingPort',(side*2.106,y,.88),(.016,.30,.095),r,col,'Gunmetal')
# Aft bridge is kept compact; stepped decks and segmented windows make it readable.
c.box('AccommodationBlock',(0,-5.56,1.86),(3.30,2.95,1.42),r,col,'LightGrey')
c.box('BridgeDeck',(0,-5.22,2.60),(3.59,2.61,.13),r,col,'FleetGrey')
c.frustum('Wheelhouse',(0,-5.18,2.98),(3.11,2.32),(2.91,2.10),.64,r,col,'LightGrey')
c.box('BridgeRoof',(0,-5.18,3.36),(3.28,2.47,.12),r,col,'FleetGrey')
for x in (-1.12,-.56,0,.56,1.12):
 c.box('ForwardBridgeWindow',(x,-4.007,3.03),(.43,.018,.26),r,col,'Radar Glass')
for side in (-1,1):
 for y in (-4.60,-5.15,-5.70):c.box('SideBridgeWindow',(side*1.552,y,3.03),(.018,.38,.25),r,col,'Radar Glass')
 for y in (-4.8,-5.45,-6.1):c.box('CabinWindow',(side*1.656,y,2.10),(.018,.24,.22),r,col,'Radar Glass')
 c.box('SideDoor',(side*1.66,-6.56,1.67),(.022,.30,.60),r,col,'FleetGrey')
 rail((side*1.75,-6.35),(side*1.75,-4.04),2.68)
 c.cyl('RaftCanister',(side*1.77,-6.15,2.70),.13,.51,r,col,'SupplyWhite',10,(math.pi/2,0,0),False)
 c.box('RaftCradle',(side*1.77,-6.15,2.56),(.29,.55,.055),r,col,'FleetGrey')
c.frustum('RakedFunnel',(0,-6.28,3.80),(.82,.98),(.71,.82),.81,r,col,'FleetGrey')
c.box('ExhaustCap',(0,-6.28,4.23),(.82,.93,.08),r,col,'Gunmetal')
for side in (-1,1):
 c.box('FunnelStripe',(side*.414,-6.28,3.87),(.012,.75,.09),r,col,'SafetyOrange')
 for y in (-6.50,-6.31,-6.12):c.box('FunnelVent',(side*.417,y,3.58),(.015,.08,.17),r,col,'Gunmetal')
c.beam('MastStem',(0,-4.72,3.42),(0,-4.72,4.47),.065,r,col,'FleetGrey')
for side in (-1,1):c.beam('MastBrace',(side*.46,-4.72,3.42),(0,-4.72,4.15),.045,r,col,'FleetGrey')
c.box('RadarAntenna',(0,-4.72,4.52),(.78,.12,.10),r,col,'LightGrey')
c.empty('RadarPivot',r,col,(0,-4.72,4.52))
# Low hold covers, not a full stack of containers: leave visible work aisles.
for y in (-2.30,1.02,4.04):
 c.box('CargoHoldCoaming',(0,y,1.26),(2.68,2.20,.22),r,col,'FleetGrey')
 for side in (-1,1):
  c.box('SteelHatchPanel',(side*.66,y,1.40),(1.30,2.13,.10),r,col,'LightGrey')
  for yy in (-.72,0,.72):c.box('HatchRib',(side*.66,y+yy,1.465),(1.27,.04,.03),r,col,'FleetGrey')
 for x in (-1.37,1.37):
  for yy in (-.7,.7):c.box('HatchDog',(x,y+yy,1.34),(.065,.11,.08),r,col,'Gunmetal')
# Twin-supported replenishment gantry in the clear gap between holds.
for side in (-1,1):
 c.box('ReplenishmentPost',(side*1.48,-.53,2.14),(.18,.22,1.96),r,col,'FleetGrey')
 c.beam('GantryBrace',(side*1.48,-.88,1.17),(side*1.48,-.53,2.10),.10,r,col,'FleetGrey')
 c.box('OutboardTransferHead',(side*1.77,-.53,3.12),(.39,.28,.23),r,col,'LightGrey')
 c.cyl('SupplyWinch',(side*1.51,-.70,1.40),.16,.36,r,col,'Gunmetal',12,(math.pi/2,0,0),False)
 c.empty('ReplenishmentPoint_'+('P' if side<0 else 'S'),r,col,(side*1.93,-.53,3.12))
c.box('GantryCrossbeam',(0,-.53,3.06),(3.15,.19,.19),r,col,'FleetGrey')
# Stowed cargo cranes stand in side aisles and reach above, not through, hatch covers.
for side,y in ((-1,-3.58),(1,2.50)):
 x=side*1.65
 c.cyl('CranePedestal',(x,y,1.51),.19,.72,r,col,'FleetGrey',12,smooth=False)
 c.box('CraneCab',(x,y,1.97),(.40,.48,.32),r,col,'LightGrey')
 c.beam('CraneLowerBoom',(x,y,2.12),(x,y+.62,2.92),.14,r,col,'SafetyOrange')
 c.beam('CraneUpperBoom',(x,y+.62,2.92),(x,y+1.75,2.48),.11,r,col,'SafetyOrange')
 c.beam('CraneHydraulicRam',(x+.09,y+.12,2.10),(x+.09,y+.53,2.72),.055,r,col,'Gunmetal')
 c.beam('CraneHookCable',(x,y+1.75,2.48),(x,y+1.75,2.12),.018,r,col,'Gunmetal')
# Small secured cargo pallet at forward hatch, anchor gear and mooring fittings.
for x in (-.55,.55):
 c.box('SecuredStores',(x,4.15,1.74),(.82,1.25,.50),r,col,'ContainerTan')
 for dx in (-.25,.25):c.box('CargoStrap',(x+dx,4.15,2.00),(.04,1.27,.02),r,col,'Gunmetal')
c.box('AnchorWinchBase',(0,6.12,1.23),(.90,.50,.12),r,col,'FleetGrey')
for side in (-1,1):
 c.cyl('AnchorWindlass',(side*.28,6.12,1.42),.17,.22,r,col,'Gunmetal',12,(0,math.pi/2,0),False)
 c.beam('AnchorChain',(side*.28,6.16,1.25),(side*.65,6.65,1.20),.034,r,col,'Gunmetal')
 for y in (-7.05,5.8):
  for dy in (-.07,.07):c.cyl('Bollard',(side*1.25,y+dy,1.24),.045,.16,r,col,'Gunmetal',8,smooth=False)
c.empty('Bow',r,col,(0,7.75,1.15));c.empty('Stern',r,col,(0,-7.75,1.15));c.empty('Waterline',r,col)
exec(compile(old[old.index('def export(r):'):old.index('models=[build_battery()')],str(src),'exec'))
export(r)
exec(compile(old[old.index('scene=bpy.context.scene'):old.index('for r in models:\n for o in c.descendants(r):')],str(src),'exec'))
render('SupplyShip',(0,0,1.1),(11,17,12),20.8)
render('SupplyShip_Rear',(0,-.3,1.1),(-11,-17,11),20.8)
render('SupplyShip_Side',(0,0,1.1),(19,2,7),19)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'SupplyShipAngular.blend'))
(OUT/'manifest.json').write_text(json.dumps({'asset':r['export_name'],'triangles':c.tris(r),'length':15.5,'beam':4.2,'review_only':True},indent=2))
