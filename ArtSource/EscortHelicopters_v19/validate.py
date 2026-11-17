import bpy,json,math
from pathlib import Path
from mathutils import Vector
out=Path(__file__).resolve().parent
results=[]
for path in sorted((out/'Models').glob('HEL_*.fbx')):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path))
 obs=list(bpy.context.scene.objects)
 assert len([o for o in obs if o.parent is None])==1
 assert all(max(abs(s-1) for s in o.scale)<.001 for o in obs)
 rotor=bpy.data.objects['TailRotor'];wing=bpy.data.objects['HorizontalTailplane'];fin=bpy.data.objects['TailFin']
 # Check swept blade envelope in rotor coordinates; shaft is intentionally excluded.
 inv=rotor.matrix_world.inverted()
 bladepoints=[inv@(o.matrix_world@v.co) for o in rotor.children if o.type=='MESH' and 'Blade' in o.name for v in o.data.vertices]
 radius=max(math.hypot(v.y,v.z) for v in bladepoints)
 disk_min=min(v.x for v in bladepoints);disk_max=max(v.x for v in bladepoints)
 clearances={}
 for ob in (wing,fin):
  pts=[inv@(ob.matrix_world@v.co) for v in ob.data.vertices]
  # If x intervals do not overlap, the entire geometry is clear of the disk.
  lo=min(v.x for v in pts);hi=max(v.x for v in pts)
  axial=max(disk_min-hi,lo-disk_max)
  if axial>0:clearances[ob.name]=axial;continue
  ymin,ymax=min(v.y for v in pts),max(v.y for v in pts)
  zmin,zmax=min(v.z for v in pts),max(v.z for v in pts)
  dy=max(ymin,0,-ymax);dz=max(zmin,0,-zmax)
  gap=math.hypot(dy,dz)-radius
  assert gap>0,(path.name,ob.name,gap)
  clearances[ob.name]=gap
 for n in ('Rotor','TailRotor','TorpedoDropPoint_01'):assert bpy.data.objects.get(n)
 results.append({'model':path.name,'unit_scales':True,'single_root':True,'tail_disk_clearance_metres':clearances,'tail_radius':radius})
assert len(results)==3
(out/'validation.json').write_text(json.dumps(results,indent=2))
print(json.dumps(results))
