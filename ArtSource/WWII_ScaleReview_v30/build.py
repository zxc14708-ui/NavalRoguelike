"""Player-module proportion revision; geometry and sockets baked at unit scale."""
from pathlib import Path
source=Path(__file__).resolve().parents[1]/'WWII_ModePack_v29/build.py'
code=source.read_text(encoding='utf8')
code=code.replace("sys.path.insert(0,str(Path(__file__).resolve().parent))", "sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'WWII_ModePack_v29'))")
code=code.replace("setup();(OUT/'Models')", "OUT=Path(__file__).resolve().parent\nimport common\ncommon.OUT=OUT\nsetup();(OUT/'Models')")
code=code.replace("assets=[];report=[]", """assets=[];report=[]
def resize_group(group,factor,move_pivot=False):
 # Bake all descendant offsets and mesh coordinates. No object scale is introduced.
 for o in c.descendants(group):
  if o==group:
   if move_pivot:o.location*=factor
   continue
  o.location*=factor
  if o.type=='MESH':o.data.transform(Matrix.Scale(factor,4))
""")
code=code.replace("r,col=root('MOD_Ww2DualGun');dual_gun(r,col)","r,col=root('MOD_Ww2DualGun');dual_gun(r,col,scale=.78)")
code=code.replace("r,col=root('MOD_Ww2QuadAa');quad_aa(r,col)","r,col=root('MOD_Ww2QuadAa');yaw,pitch=quad_aa(r,col);resize_group(yaw,.86)")
code=code.replace("r,col=root('MOD_Ww2TorpedoMount');torpedo_mount(r,col)","r,col=root('MOD_Ww2TorpedoMount');mount=torpedo_mount(r,col);resize_group(mount,.88,True)")
# Native 3D before/after comparison, with the exact same orthographic camera.
code=code.replace('# Four module comparison, tier labels are rendered in the contact sheet later.',"""# Old and revised gun shown on the same metre grid and with one camera.
visible([dg]);dg.location.x=-1.9
compare_col=c.collection('ComparisonOnly');oldgun=c.empty('OriginalGun',None,compare_col)
dual_gun(oldgun,compare_col);oldgun.location.x=1.9
render('DualGun_Before_After',7.2,target=(0,.35,.55),position=(7,11,8))
for o in list(compare_col.objects):bpy.data.objects.remove(o,do_unlink=True)
dg.location.x=0
""")
code=code.replace("WWII_ModePack.blend","WWII_ScaleReviewed.blend")
exec(compile(code,str(source),'exec'))
