"""Deterministic semantic prop placement; no Blender or Unity dependencies.

Ground sampling is supplied by the host.  Positions are authoring XYZ: +Z up.
This module produces layouts; it does not load assets or change the game.
"""
import math
import random
import zlib

FOOTPRINTS={
    'ENV_RadarStation':(6.5,6.5),'ENV_WatchPost':(4.5,5.2),
    'ENV_CoastalBunker':(5.2,5.0),'ENV_SupplyDepot':(5.5,6.8),
    'ENV_CoastShed':(3.5,4.6),'ENV_CoastalPine':(2.8,2.8),
    'ENV_RockCluster':(3.8,3.6)}

RULES={
    'radar':{'primary':'ENV_RadarStation','secondary':('ENV_SupplyDepot','ENV_WatchPost'),
             'secondary_count':1,'trees':(2,4),'buoys':(1,2),'jetty_chance':.55},
    'outpost':{'primary':'ENV_WatchPost','secondary':('ENV_CoastalBunker','ENV_CoastShed'),
               'secondary_count':1,'trees':(3,5),'buoys':(0,1),'jetty_chance':.30},
    'harbor':{'primary':'ENV_SupplyDepot','secondary':('ENV_CoastShed','ENV_WatchPost'),
              'secondary_count':1,'trees':(2,4),'buoys':(2,3),'jetty_chance':1.0},
    'atoll':{'primary':None,'secondary':('ENV_WatchPost','ENV_CoastShed'),
             'secondary_count':1,'trees':(3,6),'buoys':(1,2),'jetty_chance':.0}}


def _rect(x,y,heading,size,margin=0):
    cosine,sine=math.cos(heading),math.sin(heading)
    return [(x+cosine*u-sine*v,y+sine*u+cosine*v)
            for u,v in ((-size[0]/2-margin,-size[1]/2-margin),
                        (size[0]/2+margin,-size[1]/2-margin),
                        (size[0]/2+margin,size[1]/2+margin),
                        (-size[0]/2-margin,size[1]/2+margin))]


def _overlap(first,second):
    for rectangle in (first,second):
        for a,b in zip(rectangle,rectangle[1:]+rectangle[:1]):
            axis=(-(b[1]-a[1]),b[0]-a[0])
            one=[x*axis[0]+y*axis[1] for x,y in first]
            two=[x*axis[0]+y*axis[1] for x,y in second]
            if max(one)<=min(two) or max(two)<=min(one):return False
    return True


def generate_layout(kind,seed,radius,outline,sample_height,sea=-.9):
    """Return semantic layout and flattened pads with stable local RNG.

    Structures require dry corners, bounded local relief and separated OBBs.
    A primary structure is compulsory: inability to fit is an explicit error,
    not a silent omission. Optional props may be skipped on a small island.
    """
    if kind not in RULES:raise ValueError('Unknown island theme: '+kind)
    rng=random.Random(zlib.crc32((kind+':'+str(seed)).encode('utf-8')))
    rules=RULES[kind];placed=[];occupied=[];pads=[]

    def choose(asset,zone,mandatory=False):
        size=FOOTPRINTS[asset];candidates=[]
        for attempt in range(240):
            if kind=='atoll':fraction=rng.uniform(.51,.65)
            elif zone=='summit':fraction=rng.uniform(.02,.36)
            else:fraction=rng.uniform(.25,.68)
            angle=rng.uniform(-math.pi,math.pi)
            x,y=radius*fraction*math.cos(angle),radius*fraction*math.sin(angle)
            heading=angle-math.pi/2 if zone!='summit' else rng.uniform(-math.pi,math.pi)
            rect=_rect(x,y,heading,size,.35)
            if any(math.hypot(u,v)>outline(math.atan2(v,u))*.92 for u,v in rect):continue
            heights=[sample_height(u,v) for u,v in rect]+[sample_height(x,y)]
            if min(heights)<sea+.38:continue
            relief=max(heights)-min(heights)
            if relief>2.25:continue
            if any(_overlap(rect,previous) for previous in occupied):continue
            # Prefer naturally level sites to excessive earthwork terraces.
            score=relief+.25*fraction+rng.uniform(0,.06)
            candidates.append((score,x,y,heading,sorted(heights)[len(heights)//2],rect))
        if not candidates:
            if mandatory:raise ValueError('Required structure does not fit: '+asset)
            return
        candidates.sort(key=lambda candidate:candidate[0])
        # Pick among good sites, rather than always converging to one minimum.
        shortlist=[item for item in candidates[:24] if item[0]<=candidates[0][0]+.65]
        _,x,y,heading,z,rect=rng.choice(shortlist);occupied.append(rect)
        placement={'asset':asset,'position':[x,y,z-.035],'heading_radians':heading,
                   'footprint_m':list(size),'zone':zone,'ground_height':z}
        placed.append(placement)
        pads.append({'x':x,'y':y,'z':z,'heading':heading,
                     'width':size[0]+.65,'length':size[1]+.65,'fade_m':1.15})

    if rules['primary']:choose(rules['primary'],'summit',True)
    for asset in rng.sample(list(rules['secondary']),rules['secondary_count']):
        choose(asset,'coast',kind=='atoll')
    facilities=list(placed)
    trees=[]
    for i in range(rng.randint(*rules['trees'])):
        before=len(placed);choose('ENV_CoastalPine','coast')
        if len(placed)>before:trees.append(placed.pop());pads.pop()
    # Trees are terrain-fitted, not put on manufactured level terraces.
    placed.extend(trees)
    water_reserved=[]
    if rng.random()<rules['jetty_chance']:
        angle=-math.pi/2+rng.uniform(-.30,.30);distance=outline(angle)*.91
        placed.append({'asset':'ENV_Jetty',
                       'position':[distance*math.cos(angle),distance*math.sin(angle),-.06],
                       'heading_radians':angle-math.pi/2,'zone':'shore_connection'})
        item=placed[-1];x,y,_=item['position'];a=item['heading_radians']
        water_reserved.append(_rect(x-math.sin(a)*3,y+math.cos(a)*3,a,(4.0,7.6)))
    for i in range(rng.randint(*rules['buoys'])):
        for attempt in range(60):
            angle=rng.uniform(-2.8,-.30);distance=outline(angle)+rng.uniform(2.8,4.6)
            x,y=distance*math.cos(angle),distance*math.sin(angle)
            rect=_rect(x,y,0,(2.5,2.5))
            if any(_overlap(rect,previous) for previous in water_reserved):continue
            placed.append({'asset':'ENV_NavigationBuoy','position':[x,y,sea],
                           'heading_radians':rng.uniform(-math.pi,math.pi),'zone':'water'})
            water_reserved.append(rect);break
    return {'seed':seed,'kind':kind,'placements':placed,'pads':pads,
            'facility_count':len(facilities),'coordinate_space':'Blender XYZ / +Z up',
            'terrain_fitting':True,'facility_overlap_rejected':True}


def validate_facility_layout(layout,outline,sample_height,sea=-.9):
    errors=[];rectangles=[]
    for item in layout['placements']:
        if item['asset'] not in FOOTPRINTS or item['asset']=='ENV_CoastalPine':continue
        x,y,z=item['position'];rect=_rect(x,y,item['heading_radians'],item['footprint_m'],.35)
        if any(math.hypot(u,v)>outline(math.atan2(v,u))*.92 for u,v in rect):errors.append('off-land:'+item['asset'])
        if any(sample_height(u,v)<sea+.38 for u,v in rect):errors.append('wet-foundation:'+item['asset'])
        if any(_overlap(rect,previous) for previous in rectangles):errors.append('overlap:'+item['asset'])
        rectangles.append(rect)
    return errors


def validate_water_clearance(layout):
    reserved=[];errors=[]
    for item in layout['placements']:
        if item['asset']!='ENV_Jetty':continue
        x,y,_=item['position'];a=item['heading_radians']
        reserved.append(_rect(x-math.sin(a)*3,y+math.cos(a)*3,a,(4.0,7.6)))
    for item in layout['placements']:
        if item['asset']!='ENV_NavigationBuoy':continue
        x,y,_=item['position'];rect=_rect(x,y,0,(2.5,2.5))
        if any(_overlap(rect,other) for other in reserved):errors.append('buoy/pier-overlap')
        reserved.append(rect)
    return errors
