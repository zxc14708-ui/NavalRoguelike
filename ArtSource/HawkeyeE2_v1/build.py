"""E-2D 어드밴스드 호크아이(항공대 호출 조기경보기) — 저폴리 각진 모형, 게임 크기로 내보낸다.

실행: blender -b --factory-startup --python build.py
  - 실제 치수(m)로 만든 뒤 슈퍼호넷(ArtSource/SuperHornet_v1)과 같은 배율(3.4 / 18.3)로 줄인다
    → 길이 약 3.3 · 날개폭 약 4.6 · 레이더 돔 지름 약 1.4.
  - 기수 +Y(블렌더), 위 +Z. Unity에서는 Nose/Tail 소켓으로 기수를 +Z에 맞춘다.
  - 도는 부품은 원점이 회전 중심인 따로 된 메시: Rotodome(위 축으로 돈다, 무늬 포함) · PropL/PropR(앞 축으로 돈다, 8엽).
  - 소켓(빈 노드): Nose · Tail · WingtipL/R
  - 재질 이름은 Unity 재질(Art/Models/Materials)과 이름으로 이어진다(Hawkeye Light Grey만 새로, 나머지는 슈퍼호넷·기존 팔레트).
결과: Models/AIR_Hawkeye.fbx · Hawkeye.blend · Preview_*.png · manifest.json
"""
import bpy, bmesh, math, json
from pathlib import Path
from mathutils import Vector, Matrix

OUT = Path(__file__).resolve().parent
(OUT / 'Models').mkdir(exist_ok=True)
SCALE = 3.4 / 18.3          # 슈퍼호넷과 같은 배율
REAL_LENGTH = 17.6

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

PALETTE = {
    'Hawkeye Light Grey': ((0.74, 0.76, 0.78), 0.35),
    'Hornet Grey':        ((0.44, 0.48, 0.53), 0.35),
    'Hornet Canopy':      ((0.10, 0.13, 0.15), 0.85),
    'Hornet Dark':        ((0.16, 0.18, 0.20), 0.30),
    'Gunmetal':           ((0.035, 0.05, 0.06), 0.45),
    'Medical White':      ((0.82, 0.86, 0.84), 0.30),
}
MATS = {}
for name, (rgb, smooth) in PALETTE.items():
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    bsdf.inputs['Roughness'].default_value = 1 - smooth
    m.diffuse_color = (*rgb, 1)
    MATS[name] = m

root = bpy.data.objects.new('AIR_Hawkeye', None)
scene.collection.objects.link(root)


def finish(name, bm, mat, parent=None, origin=(0, 0, 0)):
    """bmesh(세계 좌표) → 물체. origin이 물체 원점(회전 중심)이 된다."""
    o = Vector(origin)
    bmesh.ops.translate(bm, vec=-o, verts=bm.verts)
    me = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = False
    me.materials.append(MATS[mat])
    ob = bpy.data.objects.new(name, me)
    scene.collection.objects.link(ob)
    par = parent or root
    ob.parent = par
    ob.location = o - world_pos(par)
    return ob


def world_pos(ob):
    """회전 없이 위치만 쌓인 부모 사슬의 세계 위치(새로 만든 물체는 matrix_world가 아직 갱신되지 않는다)."""
    p = Vector()
    while ob is not None and ob is not root:
        p += ob.location
        ob = ob.parent
    return p


def loft(name, sections, mat, sides=8, **kw):
    bm = bmesh.new()
    rings = []
    for y, w, top, bot in sections:
        cz, hz = (top + bot) / 2, (top - bot) / 2
        ring = []
        for i in range(sides):
            a = 2 * math.pi * i / sides + math.pi / sides
            x, z = math.cos(a), math.sin(a)
            x = math.copysign(abs(x) ** 0.75, x)
            ring.append(bm.verts.new((x * w, y, cz + z * hz)))
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((r0[i], r0[j], r1[j], r1[i]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish(name, bm, mat, **kw)


def plate(name, outline, z, thick, mat, xform=None, **kw):
    bm = bmesh.new()
    top = [bm.verts.new((x, y, z + thick / 2)) for x, y in outline]
    bot = [bm.verts.new((x, y, z - thick / 2)) for x, y in outline]
    bm.faces.new(top)
    bm.faces.new(list(reversed(bot)))
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((top[i], bot[i], bot[j], top[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if xform is not None:
        bmesh.ops.transform(bm, matrix=xform, verts=bm.verts)
    return finish(name, bm, mat, **kw)


def cyl(name, center, radius, length, mat, segs=12, cone=0.0, axis='Y', **kw):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segs, radius1=radius, radius2=radius * (1 - cone), depth=length)
    rot = Matrix.Rotation(-math.pi / 2, 4, 'X') if axis == 'Y' else Matrix.Identity(4)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ rot, verts=bm.verts)
    return finish(name, bm, mat, **kw)


def beam(name, a, b, size, mat, **kw):
    """두 점을 잇는 각진 기둥(지주)."""
    a, b = Vector(a), Vector(b)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    d = b - a
    rot = d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
    m = Matrix.Translation((a + b) / 2) @ rot @ Matrix.Diagonal((size, size, d.length, 1))
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return finish(name, bm, mat, **kw)


def socket(name, pos):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = 0.3
    scene.collection.objects.link(e)
    e.parent = root
    e.location = pos
    return e


# ---------------------------------------------------------------- 동체 (실제 치수, m)
loft('Fuselage', [
    (8.80, 0.20, 0.25, -0.35),
    (8.20, 0.78, 0.80, -0.80),
    (6.80, 1.12, 1.15, -1.02),
    (4.00, 1.20, 1.25, -1.05),
    (0.00, 1.20, 1.25, -1.05),
    (-3.50, 1.05, 1.18, -0.85),
    (-6.40, 0.70, 1.05, -0.25),
    (-8.10, 0.40, 1.00, 0.30),
], 'Hawkeye Light Grey')
# 조종석 창(앞 위 어두운 띠)
loft('Cockpit', [(8.15, 0.55, 0.78, 0.30), (7.60, 0.95, 1.16, 0.55), (6.40, 1.02, 1.30, 0.70), (5.80, 0.70, 1.28, 0.95)], 'Hornet Canopy')

# 고익(직선 테이퍼): 뿌리 시위 3.5m, 끝 1.6m, 반폭 12.3m
for s, tag in ((-1, 'L'), (1, 'R')):
    wing = [(s * 0.6, 1.90), (s * 12.28, 0.70), (s * 12.28, -0.95), (s * 0.6, -1.70)]
    plate(f'Wing{tag}', wing if s > 0 else list(reversed(wing)), 1.30, 0.34, 'Hawkeye Light Grey')
    walk = [(s * 1.2, 1.2), (s * 3.2, 1.0), (s * 3.2, 0.6), (s * 1.2, 0.8)]
    plate(f'WingWalk{tag}', walk if s > 0 else list(reversed(walk)), 1.48, 0.02, 'Hornet Grey')
    # 엔진 나셀(날개 아래) + 앞 스피너
    loft(f'Nacelle{tag}', [(4.10, 0.30, 1.05, 0.55), (3.50, 0.62, 1.40, 0.10), (0.50, 0.66, 1.45, 0.05), (-2.80, 0.40, 1.35, 0.60)], 'Hawkeye Light Grey')
    cyl(f'Spinner{tag}', (s * 3.9, 4.35, 0.80), 0.30, 0.5, 'Hawkeye Light Grey', 10, cone=0.8)
    # 8엽 프로펠러: 원점 = 허브(앞 축으로 돈다)
    hub = (s * 3.9, 4.15, 0.80)
    prop = cyl(f'Prop{tag}', hub, 0.22, 0.18, 'Hornet Dark', 10, origin=hub)
    for i in range(8):
        a = 2 * math.pi * i / 8
        tip = Vector((math.cos(a), 0, math.sin(a))) * 2.05 + Vector(hub)
        beam(f'Blade{tag}{i}', Vector(hub) + (tip - Vector(hub)) * 0.12, tip, 0.16, 'Hornet Dark', parent=prop, origin=hub)
    socket(f'Wingtip{tag}', (s * 12.30, -0.2, 1.30))

# 나셀은 좌우로 옮긴다(loft는 가운데에 만든다)
for tag, s in (('L', -1), ('R', 1)):
    bpy.data.objects[f'Nacelle{tag}'].location.x += s * 3.9

# ---------------------------------------------------------------- 레이더 돔(로토돔): 원점 = 돔 가운데(위 축으로 돈다)
dome_c = (0, -1.6, 3.55)
bm = bmesh.new()
bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=3.65, radius2=3.4, depth=0.75)
bmesh.ops.translate(bm, vec=Vector(dome_c), verts=bm.verts)
dome = finish('Rotodome', bm, 'Medical White', origin=dome_c)
# 돔 위 무늬(도는 게 보이게): 어두운 띠 두 줄
for k, yy in enumerate((-1.2, 1.2)):
    stripe = [(-3.0, dome_c[1] + yy - 0.35), (3.0, dome_c[1] + yy - 0.35), (3.0, dome_c[1] + yy + 0.35), (-3.0, dome_c[1] + yy + 0.35)]
    plate(f'RotodomeStripe{k}', stripe, dome_c[2] + 0.385, 0.02, 'Hornet Dark', parent=dome, origin=dome_c)
cyl('RotodomeRim', (dome_c[0], dome_c[1], dome_c[2]), 3.68, 0.12, 'Hornet Grey', 24, axis='Z', parent=dome, origin=dome_c)
# 지주(뒤집힌 V 두 쌍)
for yy in (-0.5, -2.7):
    for s in (-1, 1):
        beam(f'Pylon{yy}{s}', (s * 0.75, yy, 1.20), (s * 0.25, yy, 3.20), 0.22, 'Hawkeye Light Grey')

# ---------------------------------------------------------------- 꼬리: 상반각 수평 꼬리 + 수직 꼬리 넷
dih = math.radians(11)
for s, tag in ((-1, 'L'), (1, 'R')):
    st = [(s * 0.3, -6.40), (s * 4.0, -7.20), (s * 4.0, -8.40), (s * 0.3, -8.60)]
    xf = Matrix.Translation(Vector((0, 0, 1.05))) @ Matrix.Rotation(s * dih, 4, 'Y').inverted()
    plate(f'Stabilizer{tag}', st if s > 0 else list(reversed(st)), 0.0, 0.16, 'Hawkeye Light Grey', xf)
    tip_z = 1.05 + math.tan(dih) * 4.0
    for name, x, z0, h, c in ((f'FinOuter{tag}', s * 4.0, tip_z, 2.6, 1.9), (f'FinInner{tag}', s * 1.45, 1.05 + math.tan(dih) * 1.45, 2.2, 1.7)):
        fin = [(-0.0, -6.70), (-h, -7.30), (-h, -7.30 - c * 0.75), (-0.0, -6.70 - c)]
        fx = Matrix.Translation(Vector((x, 0, z0 - 0.4))) @ Matrix.Rotation(math.pi / 2, 4, 'Y')
        plate(name, fin, 0.0, 0.10, 'Hawkeye Light Grey', fx)

socket('Nose', (0, 8.95, 0.0))
socket('Tail', (0, -8.95, 0.0))

# ---------------------------------------------------------------- 게임 크기로(원점이 있는 부품은 위치도 함께)
def scale_tree(ob):
    for ch in ob.children:
        if ch.type == 'MESH':
            ch.data.transform(Matrix.Scale(SCALE, 4))
        ch.location = ch.location * SCALE
        if ch.type == 'EMPTY':
            ch.empty_display_size = 0.08
        scale_tree(ch)
scale_tree(root)

def all_meshes(ob):
    for ch in ob.children:
        if ch.type == 'MESH':
            yield ch
        yield from all_meshes(ch)
tris = sum(len(p.vertices) - 2 for ob in all_meshes(root) for p in ob.data.polygons)

# ---------------------------------------------------------------- 내보내기
bpy.ops.object.select_all(action='DESELECT')
def select_tree(ob):
    ob.select_set(True)
    for ch in ob.children:
        select_tree(ch)
select_tree(root)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.fbx(filepath=str(OUT / 'Models' / 'AIR_Hawkeye.fbx'), use_selection=True,
                         object_types={'MESH', 'EMPTY'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                         bake_space_transform=False, mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)

# ---------------------------------------------------------------- 미리보기
world = bpy.data.worlds.new('W')
world.color = (0.32, 0.42, 0.50)
scene.world = world
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'MATERIAL'
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.render.resolution_x, scene.render.resolution_y = 1200, 800
cam_data = bpy.data.cameras.new('Cam')
cam = bpy.data.objects.new('Cam', cam_data)
scene.collection.objects.link(cam)
scene.camera = cam

def render(name, eye, target=(0, 0, 0.2), lens=55):
    cam.location = eye
    d = Vector(target) - Vector(eye)
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    cam_data.lens = lens
    scene.render.filepath = str(OUT / f'{name}.png')
    bpy.ops.render.render(write_still=True)

render('Preview_Top', (0, 0, 10), lens=40)
render('Preview_Quarter', (4.6, 4.0, 3.4), lens=45)
render('Preview_Side', (7.0, 0.2, 0.6), lens=45)

bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Hawkeye.blend'))
(OUT / 'manifest.json').write_text(json.dumps({
    'asset': 'AIR_Hawkeye', 'real_length_m': REAL_LENGTH, 'scale': round(SCALE, 4), 'triangles': tris,
    'sockets': ['Nose', 'Tail', 'WingtipL', 'WingtipR'], 'rotating': {'Rotodome': 'up', 'PropL': 'forward', 'PropR': 'forward'},
    'materials': list(PALETTE.keys()),
}, indent=2, ensure_ascii=False), encoding='utf-8')
print('HAWKEYE_OK tris', tris)
