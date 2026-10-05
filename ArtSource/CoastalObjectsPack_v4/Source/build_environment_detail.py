"""Model-only environment pack, four island assemblies and eight reusable props."""
import json
import math
import random
import sys
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE=Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import environment_detail_common as c
import environment_island_terrain as terrain
import environment_architecture_props as arch
import environment_nature_props as nature
import environment_military_props as military
import environment_layout as layout

OUT=HERE/'EnvironmentDetailPack';MODELS=OUT/'Models';PREVIEW=OUT/'Preview'
if '--output' in sys.argv:
    OUT=Path(sys.argv[sys.argv.index('--output')+1]).resolve()
    MODELS=OUT/'Models';PREVIEW=OUT/'Preview'
PROP_BUILDERS={'ENV_Lighthouse':arch.build_lighthouse,'ENV_Jetty':arch.build_jetty,
    'ENV_NavigationBuoy':arch.build_buoy,'ENV_CoastShed':arch.build_coast_shed,
    'ENV_CoastalPine':nature.build_pine,'ENV_RockCluster':nature.build_rock_cluster,
    'ENV_Seal':nature.build_seal,'ENV_BeachedWreck':nature.build_wreck,
    'ENV_RadarStation':military.build_radar_station,'ENV_WatchPost':military.build_watch_post,
    'ENV_CoastalBunker':military.build_coastal_bunker,'ENV_SupplyDepot':military.build_supply_depot}

def decor_instance(name,parent,col,builder,loc=(0,0,0),heading=0):
    ob=c.empty(name,parent,col,loc);ob.rotation_euler.z=heading;builder(ob,col)
    return ob

def surface_z(root,x,y):
    bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get();best=-100
    for ob in c.descendants(root):
        if ob.type!='MESH':continue
        inv=ob.matrix_world.inverted();origin=inv@Vector((x,y,40))
        direction=(inv.to_3x3()@Vector((0,0,-1))).normalized()
        hit=BVHTree.FromObject(ob,deps).ray_cast(origin,direction)
        if hit[0] is not None:best=max(best,(ob.matrix_world@hit[0]).z)
    if best==-100:raise ValueError('No supporting terrain')
    return best

def build_island(root,col,asset,layout_seed=None):
    p=dict(terrain.PRESETS[asset]);placement=None
    if p.get('kind'):
        actual_seed=p['seed'] if layout_seed is None else layout_seed
        placement=layout.generate_layout(p['kind'],actual_seed,p['radius'],
            lambda a:terrain.outline(a,p),lambda x,y:terrain.height(x,y,p),c.SEA)
        errors=layout.validate_facility_layout(placement,lambda a:terrain.outline(a,p),
            lambda x,y:terrain.height(x,y,p),c.SEA)
        if errors:raise ValueError(errors)
        p['pads']=placement['pads']
    terrain.terrain(root,col,p)
    decor=c.empty('Decor',root,col);shelves=terrain.coast_details(decor,col,p)
    rng=random.Random(p['seed'])
    if asset!='ENV_RockIslet' and not p.get('kind'):
        spots=[(-3.9,2.6),(3.7,2.4),(-5.5,-1.4),(.3,5.6),(-.6,-3.3)]
        if asset=='ENV_LighthouseIsland':spots=spots[:3]
        for i,(x,y) in enumerate(spots):
            z=terrain.height(x,y,p)-.04
            tree=decor_instance('CoastalPine_'+str(i),decor,col,
                lambda r,k:nature.build_pine(r,k,seed=7+i), (x,y,z),rng.uniform(0,6.28))
            # Variation is baked into mesh coordinates, never scale on export roots.
            variation=rng.uniform(.78,1.10)
            for obj in c.descendants(tree):
                if obj.type=='MESH':
                    for v in obj.data.vertices:v.co*=variation
                    obj.location*=variation
                elif obj!=tree:obj.location*=variation
    if placement:
        facilities=[]
        for i,item in enumerate(placement['placements']):
            x,y,z=item['position']
            if item['asset']=='ENV_CoastalPine':
                z=terrain.height(x,y,p)-.035;item['position'][2]=z
                builder=lambda r,k,seed=i+actual_seed:nature.build_pine(r,k,seed=seed)
            else:builder=PROP_BUILDERS[item['asset']]
            instance=decor_instance(item['asset']+'_'+str(i),decor,col,builder,
                (x,y,z),item['heading_radians'])
            instance['placement_role']=item['zone']
            if item['asset'] in layout.FOOTPRINTS and item['asset']!='ENV_CoastalPine':
                # The actual entrance may be off-centre (e.g. radar house door).
                bpy.context.view_layer.update()
                entrance=next(obj for obj in c.descendants(instance)
                              if obj.get('export_name')=='EntryOrigin')
                point=entrance.matrix_world.translation;a=item['heading_radians']
                facilities.append((point.x-math.sin(a)*.65,point.y+math.cos(a)*.65))
        for first,last in zip(facilities,facilities[1:]):
            terrain.path(decor,col,p,first,last,width=.65)
    if asset=='ENV_RockIslet':
        # Fitted wildlife shelf: isolated decorative rock, not a collision proxy.
        x,y,_=shelves[0]
        for i,(x,y) in enumerate([(x-.45,y),(x+.55,y+.15)]):
            z=surface_z(root,x,y)-.025
            decor_instance('Seal_'+str(i),decor,col,nature.build_seal,(x,y,z),i*.6-1)
    if asset=='ENV_LighthouseIsland':
        x,y=1.2,.4;z=terrain.height(x,y,p)-.04
        decor_instance('Lighthouse',decor,col,arch.build_lighthouse,(x,y,z))
        x,y=-2.5,-3.6;z=terrain.height(x,y,p)-.08
        decor_instance('CoastShed',decor,col,arch.build_coast_shed,(x,y,z),-.22)
        terrain.path(decor,col,p,(-1.0,-2.2),(.8,-1.6))
        # Jetty begins at the shoreline and extends seaward, with piles below water.
        a=-1.48;r=terrain.outline(a,p)*.88;x,y=r*math.cos(a),r*math.sin(a)
        decor_instance('Jetty',decor,col,arch.build_jetty,(x,y,-.06),a-math.pi/2)
    if asset=='ENV_WreckCove':
        a=-1.1;r=terrain.outline(a,p)+1.6;x,y=r*math.cos(a),r*math.sin(a)
        ob=decor_instance('BeachedWreck',decor,col,nature.build_wreck,(x,y,c.SEA+.60),a+math.pi/2)
        # Banked hull is part of this composition; standalone wreck remains level.
        ob.rotation_euler.y=.20
        terrain.path(decor,col,p,(-1.5,-3.5),(1.0,-4.8),width=.45)
    return {'rocky':p['rocky'],'nominal_radius_m':p['radius'],'seed':p['seed'],
        'sea_level_z':c.SEA,'base_z':-3.,'collision_mesh_hint':'TerrainShell',
        'colliders_included':False,'features':asset,
        'proposed_sockets':['ShorePoint_'+str(i).zfill(2) for i in range(12)]+['HeightMarker'],
        'layout':placement,'base_archetype':asset}

def merge_static_meshes(root):
    # Foliage and structures stay separate from TerrainShell for collision ownership.
    buckets={}
    for ob in c.descendants(root):
        if ob.type!='MESH' or ob.name.startswith('TerrainShell'):continue
        bucket=ob.parent
        buckets.setdefault(bucket,[]).append(ob)
    for parent,meshes in buckets.items():
        if len(meshes)<2:continue
        bpy.ops.object.select_all(action='DESELECT')
        for ob in meshes:ob.select_set(True)
        bpy.context.view_layer.objects.active=meshes[0]
        bpy.ops.object.join();ob=bpy.context.object;ob.name=parent.get('export_name',parent.name)+'_Visual'
        ob['export_name']=ob.name
    bpy.ops.object.select_all(action='DESELECT')

def bounds(root):
    bpy.context.view_layer.update()
    coords=[o.matrix_world@v.co for o in c.descendants(root) if o.type=='MESH' for v in o.data.vertices]
    return {axis:[min(p[i] for p in coords),max(p[i] for p in coords)] for i,axis in enumerate('xyz')}

def export(root,path):
    objects=c.descendants(root)
    for i,ob in enumerate(bpy.data.objects):
        if 'export_name' in ob:ob.name='__Pending_'+str(i)
    for ob in objects:
        ob.name=ob.get('export_name',ob.name)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','EMPTY'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_NONE',
        use_space_transform=True,bake_space_transform=False,add_leaf_bones=False,
        bake_anim=False,path_mode='COPY',mesh_smooth_type='FACE')
    bpy.ops.object.select_all(action='DESELECT')

def make_lod(entry):
    col=c.collection(entry['id']+'_LOD1');root=c.clone(entry['root'],col)
    root['export_name']=entry['id']+'_LOD1_Root'
    for ob in c.descendants(root):
        if ob.type!='MESH':continue
        ob.data=c.simplify_closed(ob.data,col)
    c.finish(root)
    info={'id':entry['id']+'_LOD1','root':root,'col':col,'category':entry['category'],
          'features':entry['features'],'triangles':c.tris(root),'bounds':bounds(root)}
    export(root,MODELS/(info['id']+'.fbx'));col.hide_render=True
    return info

def lighting():
    scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE'
    scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG'
    if scene.world is None:scene.world=bpy.data.worlds.new('CoastalDaylight')
    scene.world.color=(.30,.38,.44);scene.world.use_nodes=True
    background=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND')
    background.inputs['Color'].default_value=(.43,.55,.65,1);background.inputs['Strength'].default_value=.65
    scene.view_settings.view_transform='AgX'
    for name,loc,power,size in [('SoftDaylight',(14,-18,30),2400,15),('SkyFill',(-16,7,20),1500,18)]:
        light=bpy.data.lights.new(name,'AREA');light.energy=power;light.shape='DISK';light.size=size
        ob=bpy.data.objects.new(name,light);scene.collection.objects.link(ob);ob.location=loc
        ob.rotation_euler=(Vector((0,0,0))-ob.location).to_track_quat('-Z','Y').to_euler()
    light=bpy.data.lights.new('Sunlight','SUN');light.energy=2.0;light.angle=.16
    ob=bpy.data.objects.new('Sunlight',light);scene.collection.objects.link(ob);ob.rotation_euler=(.45,-.4,-.4)
    if hasattr(scene,'eevee') and hasattr(scene.eevee,'taa_render_samples'):scene.eevee.taa_render_samples=32
    return scene

def water(col,z=c.SEA,size=85):
    mat=bpy.data.materials.get('PREVIEW_Water')
    if not mat:
        mat=bpy.data.materials.new('PREVIEW_Water');mat.use_nodes=True
        nodes=mat.node_tree.nodes;bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
        bs.inputs['Base Color'].default_value=(.022,.20,.30,1)
        bs.inputs['Roughness'].default_value=.32;bs.inputs['Metallic'].default_value=.15
        tex=nodes.new('ShaderNodeTexNoise');tex.inputs['Scale'].default_value=90
        tex.inputs['Detail'].default_value=3;tex.inputs['Roughness'].default_value=.65
        bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.19;bump.inputs['Distance'].default_value=.07
        mat.node_tree.links.new(tex.outputs['Fac'],bump.inputs['Height']);mat.node_tree.links.new(bump.outputs['Normal'],bs.inputs['Normal'])
    c.box('PreviewOcean',(0,0,z-.07),(size,size,.14),None,col,mat)

def camera(col,loc,target,scale,res,name):
    scene=bpy.context.scene;data=bpy.data.cameras.new('ReviewCamera');ob=bpy.data.objects.new('ReviewCamera',data)
    col.objects.link(ob);ob.location=loc;ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
    data.type='ORTHO';data.ortho_scale=scale;scene.camera=ob
    scene.render.resolution_x,scene.render.resolution_y=res;scene.render.filepath=str(PREVIEW/(name+'.png'))
    bpy.ops.render.render(write_still=True);col.hide_render=True;col.hide_viewport=True

def label(body,loc,size,col):
    data=bpy.data.curves.new('Label','FONT');data.body=body;data.size=size;data.align_x='CENTER'
    ob=bpy.data.objects.new('Label',data);col.objects.link(ob);ob.location=loc;data.materials.append(c.M['MAT_IslandWhite'])

def preview(entry):
    col=c.collection('PREVIEW_'+entry['id']);root=c.clone(entry['root'],col)
    root.location=(0,0,0)
    b=entry['bounds'];span=max(b['x'][1]-b['x'][0],b['y'][1]-b['y'][0],b['z'][1]-b['z'][0]);h=b['z'][1]
    center=Vector(tuple((b[a][0]+b[a][1])/2 for a in 'xyz'))
    if entry['category']=='island':
        water(col);target=(0,0,2.5 if entry['id']=='ENV_RockIslet' else 1.2)
        scale=span*(1.50 if entry['id']=='ENV_RockIslet' else 1.28)
        loc=(25,-32,27)
    elif entry['id'] in ('ENV_NavigationBuoy','ENV_BeachedWreck','ENV_Jetty'):
        water(col,z=0 if entry['id']!='ENV_Jetty' else -.75,size=40)
        target=center;scale=span*1.45;loc=center+Vector((12,-17,11))
    else:
        c.box('PreviewGround',(0,0,-.10),(50,50,.18),None,col,'ENV_Concrete')
        target=center;scale=span*1.52;loc=center+Vector((12,-17,11))
        if entry['id']=='ENV_RadarStation':loc=center+Vector((12,17,11))
    def safe_scale(loc,target,current,resolution):
        rotation=(Vector(target)-Vector(loc)).to_track_quat('-Z','Y')
        right=rotation@Vector((1,0,0));up=rotation@Vector((0,1,0))
        points=[Vector((x,y,z))-Vector(target) for x in b['x'] for y in b['y'] for z in b['z']]
        horizontal=2*max(abs(point.dot(right)) for point in points)
        vertical=2*max(abs(point.dot(up)) for point in points)*resolution[0]/resolution[1]
        return max(current,1.10*max(horizontal,vertical))
    scale=safe_scale(loc,target,scale,(1500,1100))
    camera(col,loc,target,scale,(1500,1100),entry['id'])
    if entry['category']=='island':
        col=c.collection('PREVIEW_'+entry['id']+'_Detail')
        detail=c.clone(entry['root'],col);detail.location=(0,0,0);water(col)
        scale=safe_scale((14,-26,13),(0,-.4,2.5),span*1.22,(1600,1000))
        camera(col,(14,-26,13),(0,-.4,2.5),scale,(1600,1000),entry['id']+'_Detail')

def overview(entries):
    col=c.collection('PREVIEW_IslandsOverview');water(col,size=300)
    islands=[e for e in entries if e['category']=='island' and '__Seed' not in e['id']]
    spots=[((i%4-1.5)*40,(.5-i//4)*39) for i in range(len(islands))]
    for e,spot in zip(islands,spots):
        ob=c.clone(e['root'],col);ob.location=(spot[0],spot[1],0)
        label(e['id'].replace('ENV_','').upper(),(spot[0],spot[1]-16.5,c.SEA+.02),.86,col)
    camera(col,(37,-65,72),(0,0,2.0),177,(2400,1450),'Environment_Islands_Overview')
    col=c.collection('PREVIEW_PropsOverview')
    c.box('PreviewGround',(0,0,-.10),(74,70,.18),None,col,'ENV_Concrete')
    for i,e in enumerate([e for e in entries if e['category']=='prop']):
        x=(i%4-1.5)*13;y=(1-i//4)*16
        ob=c.clone(e['root'],col);ob.location=(x,y,0)
        label(e['id'].replace('ENV_','').upper(),(x,y-5.2,.02),.42,col)
    camera(col,(27,-35,45),(0,0,2.0),78,(2100,1450),'Environment_Props_Overview')
    variants=[e for e in entries if '__Seed' in e['id']]
    if variants:
        col=c.collection('PREVIEW_SeededVariants');water(col,size=210)
        for i,e in enumerate(variants):
            x=(i-(len(variants)-1)/2)*38
            ob=c.clone(e['root'],col);ob.location=(x,0,0)
            label('RADAR / SEED '+str(e['features']['layout']['seed']),
                  (x,-19,c.SEA+.02),.85,col)
        camera(col,(20,-50,61),(0,0,2),len(variants)*43,(2300,1150),'Environment_Seeded_Variants')

def main():
    for folder in (OUT,MODELS,PREVIEW):folder.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True);c.setup();scene=lighting()
    scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
    entries=[];lods=[]
    seed=int(sys.argv[sys.argv.index('--seed')+1]) if '--seed' in sys.argv else 20261002
    variant_count=int(sys.argv[sys.argv.index('--variants')+1]) if '--variants' in sys.argv else 3
    blueprints=[(asset,asset,None) for asset in list(terrain.PRESETS)+list(PROP_BUILDERS)]
    blueprints.extend(('ENV_RadarOutpost__Seed'+str(seed+i*97),'ENV_RadarOutpost',seed+i*97)
                      for i in range(variant_count))
    for asset,base_asset,variant_seed in blueprints:
        col=c.collection(asset);root=c.empty(asset+'_Root',None,col)
        island=base_asset in terrain.PRESETS
        features=build_island(root,col,base_asset,variant_seed) if island else PROP_BUILDERS[asset](root,col)
        c.finish(root);merge_static_meshes(root);c.finish(root)
        entry={'id':asset,'category':'island' if island else 'prop','root':root,'col':col,
               'features':features or {},'triangles':c.tris(root),'bounds':bounds(root)}
        entry['required_sockets']=sorted({o.get('export_name',o.name) for o in c.descendants(root) if o.type=='EMPTY' and o!=root})
        export(root,MODELS/(asset+'.fbx'));col.hide_render=True;entries.append(entry)
        lod=make_lod(entry);lod['required_sockets']=entry['required_sockets'];lods.append(lod)
    metadata={'schema_version':3,'game_applied':False,'unit':'meters','authoring_up':'+Z','fbx_forward':'-Z',
        'fbx_up':'Y','island_root_zero':0,'island_sea_level_z':c.SEA,
        'existing_material_colors':{k:v for k,v in c.COLORS.items() if k.startswith('MAT_Island')},
        'lod_group_connected':False,'island_budget':35000,'prop_budget':4500,
        'layout_seed':seed,'seeded_variants':variant_count,'base_island_types':len(terrain.PRESETS),
        'prop_types':len(PROP_BUILDERS),'layout_rules':layout.RULES,
        'assets':[{k:v for k,v in e.items() if k not in ('root','col')} for e in entries+lods]}
    (OUT/'manifest.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
    if '--skip-preview' not in sys.argv:
        for entry in entries:preview(entry)
        overview(entries)
    for i,e in enumerate(entries):
        e['root'].location=((i%5-2)*38,(2-i//5)*38,0);e['root'].name=e['id']+'_Root'
        e['col'].hide_render=False
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'EnvironmentDetailPack.blend'))
    print(json.dumps({'complete':True,'models':len(entries),'lods':len(lods),
                     'triangles':{e['id']:e['triangles'] for e in entries+lods}}))

if __name__=='__main__':main()
