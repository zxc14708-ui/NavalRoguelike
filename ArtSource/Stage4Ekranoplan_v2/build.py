"""Refinish the seven Stage 4 enemies while preserving their FBX part IDs."""
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector

OUT = Path(__file__).resolve().parent
BASE = OUT.parent / 'Stage4Ekranoplan_v1'
sys.dont_write_bytecode = True
sys.path.insert(0, str(BASE))

import build as original
from geometry import DARK, DECK, GLASS, HULL, METAL, RED, STEEL, Mesh, PALETTE, material

COOL = 'Naval cool grey'
WHITE = 'Medical White'
SHADE = 'Blue black glazing'
PALETTE.update({
    HULL: (.145,.23,.28), STEEL: (.53,.59,.61), COOL: (.32,.39,.44),
    WHITE: (.82,.86,.84), SHADE: (.016,.095,.121), DARK: (.02,.036,.047),
    DECK: (.365,.068,.040), RED: (.64,.022,.016),
})
original.OUT = OUT


def part(asset,name):
    return next(o for o in asset.collection.objects if o.get('part_name') == name)


def refinish(obj,detail=None,replace=None,face_color=None,omit=()):
    """Bake details into the existing mesh node so prefab references keep resolving."""
    src=obj.data
    points=[tuple(p.co) for p in src.vertices]
    kept=[face for face in src.polygons if src.materials[face.material_index].name not in omit]
    faces=[tuple(face.vertices) for face in kept]
    colors=[src.materials[face.material_index].name for face in kept]
    if replace:
        colors=[replace.get(color,color) for color in colors]
    if face_color:
        colors=[face_color(face,color) for face,color in zip(kept,colors)]
    mesh=Mesh()
    mesh.add(points,faces,colors)
    if detail:
        mesh.add(detail.vertices,detail.faces,detail.colors)
    # Preserve original object and mesh data names, independent of newly added faces.
    data=bpy.data.meshes.new(src.name+' Finished')
    used=sorted({i for face in mesh.faces for i in face})
    new_index={old:new for new,old in enumerate(used)}
    data.from_pydata([mesh.vertices[i] for i in used],[],
                     [tuple(new_index[i] for i in face) for face in mesh.faces])
    slots=list(dict.fromkeys(mesh.colors))
    for color in slots:
        data.materials.append(material(color))
    indices={color:i for i,color in enumerate(slots)}
    for face,color in zip(data.polygons,mesh.colors):
        face.material_index=indices[color]
    data.update()
    old_name=src.name
    obj.data=data
    bpy.data.meshes.remove(src)
    data.name=old_name


def projected(obj, x,y, offset=.024):
    """Deck details follow the base facets underneath them."""
    p,location,normal,_=obj.ray_cast(Vector((x,y,40)),Vector((0,0,-1)))
    if not p:
        return None
    return location+normal*offset


def line(mesh,obj,a,b,width,color):
    x,y=a
    q,r=b
    across=Vector((r-y,x-q)).normalized()*width/2
    outline=[Vector(point)+across*sign for point,sign in ((a,-1),(b,-1),(b,1),(a,1))]
    points=[projected(obj,*p) for p in outline]
    if all(p is not None for p in points):
        mesh.add(points,[(0,3,2,1)],color)

def rail_bar(mesh, a, b, width, color):
    rotation=(b-a).to_track_quat('Z','Y')
    profile=[(-width/2,-width/3,0),(width/2,-width/3,0),(0,width*.62,0)]
    mesh.loft([[p+rotation@Vector(v) for v in profile] for p in (a,b)],color)


def straked_hull(asset, rails, hatch_y, stripe_height=.48):
    hull=part(asset,'Hull')
    mesh=Mesh()
    for side in (-1,1):
        local=[projected(hull,side*x,y,.04) for x,y in rails]
        local=[p for p in local if p is not None]
        for a,b in zip(local,local[1:]):
            rail_bar(mesh,a+Vector((0,0,stripe_height)),b+Vector((0,0,stripe_height)),.054,WHITE)
        for p in local:
            rail_bar(mesh,p,p+Vector((0,0,stripe_height)),.055,COOL)
        for (x,a),(x2,b) in zip(rails,rails[1:]):
            line(mesh,hull,(side*x,a),(side*x2,b),.055,COOL)
    for y in hatch_y:
        for x in (-.26,.26):
            line(mesh,hull,(x,y-.20),(x,y+.21),.054,WHITE)
        line(mesh,hull,(-.25,y-.20),(.25,y-.20),.054,WHITE)
    refinish(hull,mesh)


def superstructure(asset,y,w,length,z,height):
    obj=part(asset,'Superstructure')
    # A pale eave and dark bridge glazing match the existing PCC / modern corvette.
    def roof(face,color):
        if color==HULL and face.normal.z>.75:
            return WHITE
        return color
    refinish(obj,face_color=roof,omit={GLASS})
    mesh=Mesh()

    def pane(points,direction):
        axis=Vector(direction)
        hit=[]
        for start in points:
            found,at,normal,_=obj.ray_cast(Vector(start),-axis)
            if not found or normal.dot(axis)<.52:
                return
            hit.append(at+normal*.022)
        if (hit[1]-hit[0]).cross(hit[2]-hit[0]).dot(axis)<0:
            hit.reverse()
        mesh.add(hit,[(0,1,2,3)],SHADE)

    bottom,top=z+height*.62,z+height*.77
    for x in (w*-.238,w*-.079,w*.079,w*.238):
        a,b=x-w*.069,x+w*.069
        pane([(a,y+length,bottom),(b,y+length,bottom),(b,y+length,top),(a,y+length,top)],(0,1,0))
    for s in (-1,1):
        for offset in (-.22,-.038,.144):
            a,b=y+length*(offset-.062),y+length*(offset+.062)
            pane([(s*w,a,bottom),(s*w,b,bottom),(s*w,b,top),(s*w,a,top)],(s,0,0))
    refinish(obj,mesh)


def gray_trim(asset,names):
    for name in names:
        obj=part(asset,name)
        refinish(obj,replace={GLASS:SHADE},
                 face_color=lambda p,c: COOL if c==STEEL and p.normal.z>.75 else c)


def wig(asset,large=False,boss=False):
    hull=part(asset,'Hull')
    detail=Mesh()
    # Small structural bands and hatches read from the combat camera.
    for y in ((-8.45,-6.55,5.85,7.25) if boss else (-6.2,-4.8,1.0,2.75) if large else (-1.95,-1.42,.24)):
        for s in (-1,1):
            width=.14 if not large and not boss else .27
            line(detail,hull,(s*width,y-.23),(s*width,y+.23),.054,COOL)
        line(detail,hull,(-.20,y-.20),(.20,y-.20),.049,COOL)
    refinish(hull,detail,replace={GLASS:SHADE})
    for side,label in ((-1,'Port'),(1,'Starboard')):
        wing=part(asset,'Wing' if not large and not boss else 'Wing_'+label)
        detail=Mesh()
        if boss:
            a,b,y=(2.0,7.57,-5.85)
        elif large:
            a,b,y=(1.47,4.19,-3.04)
        else:
            a,b,y=(.91,2.32,-1.45)
        line(detail,wing,(side*a,y),(side*b,y),.12 if boss else .074,COOL)
        line(detail,wing,(side*(b-.14),y+.16),(side*(b-.14),y+.63),.115 if boss else .075,WHITE)
        refinish(wing,detail)
    if boss:
        refinish(part(asset,'CockpitGlass'),replace={GLASS:SHADE,STEEL:COOL})
        for side in ('P','S'):
            for i in range(1,5):
                obj=part(asset,f'Engine_{side}{i}')
                refinish(obj,face_color=lambda p,c: COOL if c==STEEL and p.normal.y>.63 else c)
        gray_trim(asset,('LauncherPair_01','LauncherPair_02','LauncherPair_03'))
    if large and not boss:
        door=part(asset,'NoseDoor')
        detail=Mesh()
        for x in (-.30,.30):
            line(detail,door,(x,.23),(x,.98),.07,COOL)
        refinish(door,detail)


def hover(asset):
    straked_hull(asset,[(3.60,-7.06),(3.68,-4.5),(3.69,-1.44),(3.68,4.7),(3.35,7.02)],[-7.65,6.9],.38)
    superstructure(asset,-.16,3.5,4.2,1.34,2.62)
    for number in range(1,4):
        fan=part(asset,f'Fan_{number:02}')
        refinish(fan,face_color=lambda p,c: COOL if p.normal.y<-.65 and c==STEEL else c)


def hydro(asset):
    straked_hull(asset,[(1.22,-6.01),(1.67,-4.6),(1.85,-2.72),(1.66,.59),(1.22,2.8),(.70,4.59)],[-5.5],.31)
    superstructure(asset,.48,2.78,3.22,.80,1.89)
    gray_trim(asset,('MissileLauncher_Port','MissileLauncher_Starboard','GunTurretPivot_Housing','RadarArray'))


def corvette(asset):
    straked_hull(asset,[(1.15,-6.71),(1.54,-5.46),(1.65,-3.46),(1.64,-1.28),(1.56,1.35),
                       (1.23,3.62),(.85,5.11),(.41,6.7)],[-6.49,6.48],.36)
    superstructure(asset,1.62,2.34,3.49,1.01,2.02)
    gray_trim(asset,('BowGun_TurretPivot_Housing','Ciws_Port_Housing','Ciws_Starboard_Housing',
                     'RadarArray','MissileLauncher_Port','MissileLauncher_Starboard'))


def battery(asset):
    m=Mesh()
    for x in (-1.12,1.12):
        for y in (-1.12,1.12):
            m.box((x,y,.293),(.27,.27,.024),COOL,bevel=.042)
    refinish(part(asset,'Base'),m)
    gray_trim(asset,('TurretPivot_Housing','MissileBox_Port','MissileBox_Starboard','RadarArray'))


FINISH={
    'ENE_Boss4_Lun': lambda a:wig(a,large=True,boss=True),
    'ENE_LightWig': wig,
    'ENE_AssaultWig': lambda a:wig(a,large=True),
    'ENE_Hovercraft': hover,
    'ENE_Hydrofoil': hydro,
    'ENE_MissileCorvette': corvette,
    'ENE_PlatformBattery': battery,
}


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    scene=bpy.context.scene
    scene.name='Stage 4 Models'
    scene.unit_settings.system='METRIC'
    for folder in ('Models','Preview'):
        (OUT/folder).mkdir(exist_ok=True)
    manifest={}
    assets=[]
    for name,label,build,_ in original.SPECS:
        if name not in FINISH:
            continue
        asset=original.Asset(name,scene)
        asset.title=label
        build(asset)
        bpy.context.view_layer.update()
        FINISH[name](asset)
        bpy.context.view_layer.update()
        manifest[name]=asset.summarize()
        if manifest[name]['triangles'] > (6000 if name=='ENE_Boss4_Lun' else 1950):
            raise ValueError(f'{name} exceeds revision budget')
        print(name,manifest[name]['triangles'],flush=True)
        original.export(asset)
        assets.append(asset)
    (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    view=bpy.context.view_layer
    for asset in assets[1:]:
        view.layer_collection.children[asset.collection.name].exclude=True
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Stage4EnemyRefresh.blend'))
    if '--no-render' not in sys.argv:
        for asset in assets:
            original.preview(asset)


if __name__=='__main__':
    main()

