"""Angular coastal gun review, informed by AWM Mk XI coastal gun records."""
from pathlib import Path
previous=Path(__file__).resolve().parents[1]/'CoastalBatteryResearch_v26/build.py'
wrapper=previous.read_text(encoding='utf8')
exec(compile(wrapper[:wrapper.rindex('exec(compile')],str(previous),'exec'))
# code now contains v26's resolved build source. Remove broad bevels and smooth caps.
for b in ('bevel=.045','bevel=.08','bevel=.06','bevel=.05','bevel=.04'):
 code=code.replace(b,'bevel=0')
code=code.replace('c.setup()',"""c.COLORS.update({'GunPlate':(.22,.27,.26,1),'SteelEdge':(.34,.38,.36,1)})
c.setup()
original_cylinder=c.cyl
def hard_cylinder(*args,**kwargs):
 kwargs['smooth']=False
 return original_cylinder(*args,**kwargs)
c.cyl=hard_cylinder
def plate(name,outline,depth,parent,col,mat,axis='x'):
 # Closed plate with a clipped angular outline, never a shaded rounded box.
 verts=[]
 for t in (-depth/2,depth/2):
  verts.extend([(t,a,b) if axis=='x' else (a,t,b) for a,b in outline])
 n=len(outline);faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
 faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 return c.mesh(name,verts,faces,parent,col,mat)
""")
# Retain the foundation, with a low front parapet so the artillery is legible.
code=code.replace("(side*2.24,-.13,1.08),(.60,4.80,.72)","(side*2.24,-.13,1.01),(.60,4.80,.58)")
code=code.replace("(side*2.24,-.13,1.46),(.70,4.82,.12)","(side*2.24,-.13,1.34),(.64,4.82,.08)")
code=code.replace("(side*.97,2.18,1.06),(1.22,.60,.65)","(side*.97,2.18,.96),(1.22,.60,.45)")
code=code.replace("(side*.97,2.18,1.41),(1.18,.69,.07)","(side*.97,2.18,1.215),(1.18,.64,.06)")
start=code.index("  c.box('GunShieldSide'")
end=code.index(" c.box('LoadingDeck'",start)
code=code[:start]+"""  shield=plate('ClippedSideShield',[(-.58,.42),(.79,.42),(.88,1.11),(.53,1.46),(-.31,1.46),(-.58,1.15)],.075,turret,col,'GunPlate')
  shield.location.x=side*.76
  c.box('SidePlateStiffener',(side*.804,.08,.72),(.025,.91,.045),turret,col,'SteelEdge')
  for yy in (-.40,.05,.50):
   for zz in (.53,1.14):c.cyl('ShieldFastener',(side*.811,yy,zz),.023,.025,turret,col,'Gunmetal',6,(0,math.pi/2,0))
 # Two clipped front plates form a real vertical barrel/elevation slot.
 for side in (-1,1):
  front=plate('SplitFrontShield',[(side*.23,.43),(side*.76,.43),(side*.76,1.11),(side*.50,1.46),(side*.23,1.46)],.085,turret,col,'GunPlate','y')
  front.location.y=.80
 c.box('FrontShieldLowerTie',(0,.80,.42),(1.52,.085,.07),turret,col,'SteelEdge')
"""+code[end:]
# Solid disc handles and blocked muzzle are replaced with openings and spokes.
code=code.replace("c.cyl('TraversingHandwheel',(.88,-.36,.63),.19,.045,turret,col,'Gunmetal',12,(0,math.pi/2,0))", """c.ring('TraversingHandwheel',(.89,-.36,.63),.17,.018,turret,col,'Gunmetal',16,4,(0,math.pi/2,0))
 for ang in (0,math.pi/2):
  c.beam('HandwheelSpoke',(.89,-.36-math.cos(ang)*.16,.63-math.sin(ang)*.16),(.89,-.36+math.cos(ang)*.16,.63+math.sin(ang)*.16),.018,turret,col,'SteelEdge')""")
code=code.replace("c.cyl('MuzzleRing',(0,2.81,0),.145,.13,pivot,col,'BatteryConcrete',12,(math.pi/2,0,0))", """# Annular muzzle and recessed dark bore give a real opening.
 verts=[(rad*math.cos(i*math.tau/16),yy,rad*math.sin(i*math.tau/16)) for yy,rad in [(2.71,.115),(2.87,.115),(2.87,.074),(2.56,.074)] for i in range(16)]
 faces=[]
 for k in range(3):
  for i in range(16):j=(i+1)%16;faces.append((k*16+i,k*16+j,(k+1)*16+j,(k+1)*16+i))
 c.mesh('OpenMuzzle',verts,faces,pivot,col,'Gunmetal')
 c.cyl('BoreDarkness',(0,2.55,0),.073,.009,pivot,col,'Gunmetal',16,(math.pi/2,0,0))""")
code=code.replace("(0,1.77,0),.115,2.00", "(0,1.61,0),.115,1.70")
code=code.replace(" c.empty('Muzzle',pivot", """ for side in (-1,1):
  c.cyl('RecoilCylinder',(side*.27,.12,.23),.075,1.06,pivot,col,'SteelEdge',12,(math.pi/2,0,0))
  c.cyl('RecoilPiston',(side*.27,.76,.23),.038,.34,pivot,col,'Gunmetal',12,(math.pi/2,0,0))
 c.box('LoadingTray',(0,-.94,-.18),(.35,.69,.06),pivot,col,'SteelEdge')
 for side in (-1,1):c.box('LoadingTrayRim',(side*.18,-.94,-.12),(.025,.69,.10),pivot,col,'GunPlate')
 c.empty('Muzzle',pivot""")
# Detailed concrete joints and a small equipment cabinet, all tied to the emplacement.
code=code.replace(" c.box('CableTrench'", """ for side in (-1,1):
  for yy in (-1.45,0,1.45):c.box('ConcreteJoint',(side*2.545,yy,1.01),(.006,.012,.56),r,col,'BatteryDark')
  c.box('RearWallVent',(side*2.24,-2.539,.99),(.27,.008,.18),r,col,'BatteryDark')
 c.box('CableTrench'""")
# Only rebuild the requested coastal battery.
code=code.replace('models=[build_battery(),build_supply()]','models=[build_battery()]')
cut=code.index('for r in models:\n for o in c.descendants(r):o.hide_render=(r!=models[1])')
code=code[:cut]+"""
render('CoastalBattery_Rear',(0,-.25,1.2),(7,-10,7),7.8)
render('CoastalBattery_Side',(0,.25,1.2),(10,2,5),7.8)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CoastalBatteryAngular.blend'))
(OUT/'manifest.json').write_text(json.dumps(report,indent=2))
"""
code=code.replace("render('CoastalBattery',(0,0,1),(7,11,8),8.2)","render('CoastalBattery',(0,.15,1.1),(7,11,7),8.1)")
exec(compile(code,str(source),'exec'))
