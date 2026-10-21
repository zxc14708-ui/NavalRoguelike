"""F/A-18F 슈퍼호넷(항공대 호출 전투기) — 저폴리 각진 모형, 게임 크기로 내보낸다.

실행: blender -b --factory-startup --python build.py
  - 실제 치수(m)로 만든 뒤 게임 크기(길이 3.4 = 적 전투기·헬기와 같은 급, 함선 15의 약 1/4)로 줄여 내보낸다.
  - 기수 +Y(블렌더), 위 +Z. Unity에서는 기수가 +Z가 되도록 런타임이 Nose/Tail 소켓으로 맞춘다.
  - 소켓(빈 노드): Nose · Tail · GunPort · WingtipL/R · NozzleL/R
  - 재질 이름은 Unity 재질(Art/Models/Materials/Aviation)과 이름으로 이어진다.
결과: Models/AIR_SuperHornet.fbx · SuperHornet.blend · Preview_*.png · manifest.json
"""
import bpy, bmesh, math, json
from pathlib import Path
from mathutils import Vector, Matrix

OUT = Path(__file__).resolve().parent
(OUT / 'Models').mkdir(exist_ok=True)
REAL_LENGTH = 18.3
GAME_LENGTH = 3.4

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------------------------------------------------------- 재질
PALETTE = {
    'Hornet Light Grey': ((0.60, 0.64, 0.68), 0.35),
    'Hornet Grey':       ((0.44, 0.48, 0.53), 0.35),
    'Hornet Canopy':     ((0.10, 0.13, 0.15), 0.85),
    'Hornet Dark':       ((0.16, 0.18, 0.20), 0.30),
    'Gunmetal':          ((0.035, 0.05, 0.06), 0.45),
    'Medical White':     ((0.82, 0.86, 0.84), 0.30),
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

root = bpy.data.objects.new('AIR_SuperHornet', None)
scene.collection.objects.link(root)


def obj_from_bmesh(name, bm, mat):
    me = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = False
    me.materials.append(MATS[mat])
    ob = bpy.data.objects.new(name, me)
    scene.collection.objects.link(ob)
    ob.parent = root
    return ob


def loft(name, sections, mat, sides=8, cap=True):
    """sections: (y, 반폭, 위 z, 아래 z) — 기수→꼬리 순. 팔각 단면을 이어 동체를 만든다."""
    bm = bmesh.new()
    rings = []
    for y, w, top, bot in sections:
        cz, hz = (top + bot) / 2, (top - bot) / 2
        ring = []
        for i in range(sides):
            a = 2 * math.pi * i / sides + math.pi / sides
            # 위아래가 납작하고 옆이 각진 단면(초퍼 모양)
            x, z = math.cos(a), math.sin(a)
            x = math.copysign(abs(x) ** 0.7, x)
            ring.append(bm.verts.new((x * w, y, cz + z * hz)))
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((r0[i], r0[j], r1[j], r1[i]))
    if cap:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return obj_from_bmesh(name, bm, mat)


def plate(name, outline, z, thick, mat, xform=None):
    """outline: 위에서 본 볼록 다각형 (x, y) — 두께 thick의 판(날개·LEX·꼬리)."""
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
    return obj_from_bmesh(name, bm, mat)


def cyl(name, center, radius, length, mat, segs=10, cone=0.0, axis='Y'):
    """Y축 원통(cone > 0이면 앞끝을 그만큼 뾰족하게)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segs, radius1=radius, radius2=radius * (1 - cone), depth=length)
    rot = Matrix.Rotation(-math.pi / 2, 4, 'X') if axis == 'Y' else Matrix.Identity(4)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(center)) @ rot, verts=bm.verts)
    return obj_from_bmesh(name, bm, mat)


def socket(name, pos):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = 0.3
    scene.collection.objects.link(e)
    e.parent = root
    e.location = pos
    return e


# ---------------------------------------------------------------- 동체 (실제 치수, m)
loft('Fuselage', [
    (9.15, 0.04, 0.10, 0.00),
    (8.40, 0.36, 0.34, -0.22),
    (7.20, 0.60, 0.48, -0.42),
    (5.60, 0.74, 0.56, -0.55),
    (3.60, 0.80, 0.60, -0.60),
    (1.60, 0.98, 0.56, -0.70),
    (-1.00, 1.52, 0.60, -0.78),
    (-4.00, 1.44, 0.56, -0.68),
    (-6.60, 1.24, 0.46, -0.56),
    (-8.40, 1.10, 0.40, -0.46),
], 'Hornet Light Grey')
loft('Radome', [(9.40, 0.02, 0.06, 0.04), (9.15, 0.10, 0.12, -0.02), (8.70, 0.26, 0.26, -0.14)], 'Hornet Grey')
# 조종석(복좌 F형): 긴 캐노피
loft('Canopy', [
    (6.20, 0.08, 0.62, 0.50),
    (5.50, 0.40, 1.02, 0.52),
    (4.00, 0.44, 1.18, 0.56),
    (2.40, 0.40, 1.10, 0.56),
    (1.60, 0.20, 0.80, 0.54),
], 'Hornet Canopy')
plate('AntiGlare', [(-0.30, 7.6), (0.30, 7.6), (0.42, 6.2), (-0.42, 6.2)], 0.52, 0.04, 'Hornet Dark')
# 등 척추(캐노피 뒤 → 꼬리)
loft('Spine', [(1.80, 0.30, 0.86, 0.50), (-1.50, 0.42, 0.80, 0.50), (-5.80, 0.30, 0.66, 0.44)], 'Hornet Light Grey')

# LEX(앞전 연장부): 캐노피 옆부터 날개 뿌리까지 넓게 — 위에서 본 호넷의 특징
plate('LEX', [(-0.70, 6.60), (0.70, 6.60), (1.85, 1.40), (1.90, -0.60), (-1.90, -0.60), (-1.85, 1.40)], 0.30, 0.10, 'Hornet Light Grey')

# 공기 흡입구(LEX 아래 양옆, 앞면은 어둡게)
for s in (-1, 1):
    plate(f'Intake{"L" if s < 0 else "R"}', [(s * 0.95, 1.9), (s * 1.62, 1.9), (s * 1.62, -1.8), (s * 0.95, -1.8)], -0.56, 0.62, 'Hornet Light Grey')
    plate(f'IntakeMouth{"L" if s < 0 else "R"}', [(s * 1.00, 1.95), (s * 1.57, 1.95), (s * 1.57, 1.86), (s * 1.00, 1.86)], -0.56, 0.52, 'Gunmetal')

# ---------------------------------------------------------------- 날개 · 꼬리
for s, tag in ((-1, 'L'), (1, 'R')):
    # 주익: 뿌리 시위 4.6m, 끝 1.5m, 앞전 후퇴 약 29°, 반폭 6.3m
    wing = [(s * 1.55, 1.30), (s * 6.30, -1.35), (s * 6.30, -2.90), (s * 1.55, -3.50)]
    plate(f'Wing{tag}', wing if s > 0 else list(reversed(wing)), 0.02, 0.16, 'Hornet Light Grey')
    # 날개 접힘선(어두운 가는 띠)
    fold = [(s * 4.25, -0.12), (s * 4.40, -0.20), (s * 4.40, -3.08), (s * 4.25, -3.12)]
    plate(f'WingFold{tag}', fold if s > 0 else list(reversed(fold)), 0.11, 0.01, 'Hornet Grey')
    # 수평 꼬리(스태빌레이터)
    stab = [(s * 1.15, -5.50), (s * 3.55, -7.35), (s * 3.55, -8.85), (s * 1.15, -8.55)]
    plate(f'Stabilator{tag}', stab if s > 0 else list(reversed(stab)), -0.06, 0.10, 'Hornet Grey')
    # 수직 꼬리: 바깥으로 20° 기운 쌍꼬리
    fin = [(-4.0, 0.0), (-6.4, 2.45), (-7.85, 2.45), (-7.55, 0.0)]   # (y, 높이)
    xf = Matrix.Translation(Vector((s * 1.10, 0, 0.42))) @ Matrix.Rotation(s * math.radians(20), 4, 'Y') @ Matrix.Rotation(math.pi / 2, 4, 'Y')
    # 판을 XY 평면에 만들고(x = -높이) Y축으로 세운다
    outline = [(-h, y) for y, h in fin]
    plate(f'Fin{tag}', outline if s < 0 else list(reversed(outline)), 0.0, 0.10, 'Hornet Grey', xf)
    tipf = [(-2.30, -6.25), (-2.45, -6.40), (-2.45, -7.85), (-2.30, -7.80)]
    plate(f'FinTip{tag}', tipf if s < 0 else list(reversed(tipf)), 0.0, 0.11, 'Hornet Dark', xf)

    # 엔진 노즐(쌍발)
    cyl(f'Nozzle{tag}', (s * 0.62, -8.75, -0.04), 0.50, 0.90, 'Gunmetal', 12, cone=-0.08)
    cyl(f'NozzleCore{tag}', (s * 0.62, -9.18, -0.04), 0.36, 0.06, 'Hornet Dark', 12)

    # 날개끝 AIM-9 + 레일
    cyl(f'TipRail{tag}', (s * 6.36, -1.95, 0.02), 0.06, 1.9, 'Hornet Grey', 6)
    cyl(f'AIM9{tag}', (s * 6.45, -1.70, 0.02), 0.07, 2.90, 'Medical White', 8, cone=0.6)
    # 날개 아래 증가 탱크 + 파일런
    plate(f'TankPylon{tag}', [(s * 2.95, -0.2), (s * 3.05, -0.2), (s * 3.05, -2.0), (s * 2.95, -2.0)], -0.24, 0.36, 'Hornet Grey')
    cyl(f'DropTank{tag}', (s * 3.0, -1.0, -0.72), 0.34, 4.6, 'Hornet Light Grey', 10, cone=0.55)
    # 바깥 파일런 AIM-120
    plate(f'MissilePylon{tag}', [(s * 4.95, -1.0), (s * 5.05, -1.0), (s * 5.05, -2.4), (s * 4.95, -2.4)], -0.18, 0.24, 'Hornet Grey')
    cyl(f'AIM120{tag}', (s * 5.0, -1.6, -0.42), 0.09, 3.6, 'Medical White', 8, cone=0.6)

    socket(f'Wingtip{tag}', (s * 6.45, -2.4, 0.02))
    socket(f'Nozzle{tag}', (s * 0.62, -9.3, -0.04))

socket('Nose', (0, 9.4, 0.05))
socket('Tail', (0, -9.3, 0.05))
socket('GunPort', (0.25, 7.4, 0.40))

# ---------------------------------------------------------------- 게임 크기로
k = GAME_LENGTH / REAL_LENGTH
for ob in root.children:
    if ob.type == 'MESH':
        ob.data.transform(Matrix.Scale(k, 4))
    else:
        ob.location = ob.location * k
        ob.empty_display_size = 0.08

tris = sum(len(p.vertices) - 2 for ob in root.children if ob.type == 'MESH' for p in ob.data.polygons)

# ---------------------------------------------------------------- 내보내기
bpy.ops.object.select_all(action='DESELECT')
root.select_set(True)
for ob in root.children:
    ob.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.fbx(filepath=str(OUT / 'Models' / 'AIR_SuperHornet.fbx'), use_selection=True,
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


def render(name, eye, target=(0, 0, 0), lens=55):
    cam.location = eye
    d = Vector(target) - Vector(eye)
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    cam_data.lens = lens
    scene.render.filepath = str(OUT / f'{name}.png')
    bpy.ops.render.render(write_still=True)


render('Preview_Top', (0, 0, 9), lens=40)
render('Preview_Quarter', (4.2, 3.6, 3.0), lens=45)
render('Preview_Side', (6.5, 0.2, 0.4), lens=45)

bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'SuperHornet.blend'))
(OUT / 'manifest.json').write_text(json.dumps({
    'asset': 'AIR_SuperHornet', 'real_length_m': REAL_LENGTH, 'game_length': GAME_LENGTH, 'scale': round(k, 4),
    'triangles': tris, 'sockets': ['Nose', 'Tail', 'GunPort', 'WingtipL', 'WingtipR', 'NozzleL', 'NozzleR'],
    'materials': list(PALETTE.keys()),
}, indent=2, ensure_ascii=False), encoding='utf-8')
print('SUPERHORNET_OK tris', tris)
