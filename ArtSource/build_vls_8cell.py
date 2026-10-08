"""MOD_VLS: 해치 2×2(4셀) → 4×2(8셀). 블록 크기(1칸)·갑판 테두리·하부 캐니스터·재질은 그대로.
정사각형 셀 4×2: 열 4개(X −0.615·−0.205·0.205·0.615) × 행 2개(Y ±0.205), 해치 0.67 → 0.36 정사각형(간격 0.05).
해치·경고 띠·발사점을 8개로 만들고 이름을 01~08로 맞춘다. 발사 순서 = 이름 순서(행마다 좌우 번갈아).
사용: blender -b --python vls8_build.py -- <원본 fbx> <출력 fbx> <미리보기 png>"""
import bpy, sys, math
args = sys.argv[sys.argv.index('--') + 1:]
src, dst, preview = args[0], args[1], args[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)

root = bpy.data.objects['MOD_VLS_Real']
hatch0 = bpy.data.objects['VLSHatch_01']
stripe0 = bpy.data.objects['HatchWarning_01']
lp0 = bpy.data.objects['LaunchPoint_01']

ROWS = [-0.205, 0.205]
COLS = [-0.615, -0.205, 0.205, 0.615]
CELL_SCALE = 0.36 / 0.67

def apply_scale(obj, sx, sy):
    # 메시 데이터를 복사해 줄인다(오브젝트 배율은 1로 둔다 — Unity에서 배율 없는 부품이 다루기 쉽다)
    me = obj.data.copy()
    for v in me.vertices:
        v.co.x *= sx
        v.co.y *= sy
    obj.data = me

new_objs = []
n = 0
for x in COLS:          # 발사 순서: 열마다 양쪽 행을 번갈아
    for y in ROWS:
        n += 1
        h = hatch0.copy(); h.data = hatch0.data; bpy.context.collection.objects.link(h)
        apply_scale(h, CELL_SCALE, CELL_SCALE)
        h.name = f'VLSHatch_{n:02d}_new'; h.parent = root; h.location = (x, y, hatch0.location.z)
        s = stripe0.copy(); s.data = stripe0.data; bpy.context.collection.objects.link(s)
        apply_scale(s, CELL_SCALE, 1.0)
        s.name = f'HatchWarning_{n:02d}_new'; s.parent = root; s.location = (x, y, stripe0.location.z)
        e = lp0.copy(); bpy.context.collection.objects.link(e)
        e.name = f'LaunchPoint_{n:02d}_new'; e.parent = root; e.location = (x, y, lp0.location.z)
        new_objs += [h, s, e]

for name in [o.name for o in bpy.data.objects]:
    if name.startswith(('VLSHatch_', 'HatchWarning_', 'LaunchPoint_')) and not name.endswith('_new'):
        bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)
for o in new_objs:
    o.name = o.name[:-4]

# 내보내기(기존 모듈 FBX와 같은 축·배율 설정)
for o in bpy.data.objects: o.select_set(True)
bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={'MESH', 'EMPTY'},
                         axis_forward='-Z', axis_up='Y', global_scale=1.0, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_NONE', add_leaf_bones=False)   # 예전 MOD_VLS와 같은 단위(유니티에서 루트 배율 100) — 다른 프리팹에 구워진 사본과 크기가 맞게

# 확인용 미리보기(위에서 비스듬히)
scene = bpy.context.scene
cam_data = bpy.data.cameras.new('cam'); cam = bpy.data.objects.new('cam', cam_data)
scene.collection.objects.link(cam); scene.camera = cam
cam.location = (2.2, -2.6, 2.6); cam.rotation_euler = (math.radians(55), 0, math.radians(40))
light = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN')); scene.collection.objects.link(light)
light.rotation_euler = (math.radians(40), 0, math.radians(30))
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.color_type = 'MATERIAL'
scene.render.resolution_x, scene.render.resolution_y = 640, 480
scene.render.filepath = preview
bpy.ops.render.render(write_still=True)
print('VLS8 objects:', sorted(o.name for o in bpy.data.objects if o.parent == root))
