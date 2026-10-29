"""Prepare a separate Unity review project with the actual harbor prefab dependencies.

The original Unity project may be open or modified by another collaborator. This
copies referenced assets with their .meta GUIDs; it does not mutate the original.
"""
from pathlib import Path
from collections import deque
import json
import re
import shutil

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
REVIEW = HERE / 'ValidationProject'
ASSETS = PROJECT / 'Assets'
GUID = re.compile(rb'guid:\s*([0-9a-f]{32})')
TEXT_TYPES = {'.prefab', '.mat', '.asset', '.controller', '.overridecontroller', '.unity', '.shader'}

for folder in ('Assets', 'Packages', 'ProjectSettings'):
    (REVIEW / folder).mkdir(parents=True, exist_ok=True)
shutil.copytree(PROJECT / 'ProjectSettings', REVIEW / 'ProjectSettings', dirs_exist_ok=True)
shutil.copytree(PROJECT / 'Assets/Settings', REVIEW / 'Assets/Settings', dirs_exist_ok=True)

# Reuse installed packages by local file path: no dependency download.
manifest = json.loads((PROJECT / 'Packages/manifest.json').read_text(encoding='utf-8-sig'))
deps = {k:v for k,v in manifest['dependencies'].items() if k.startswith('com.unity.modules.')}
for directory in (PROJECT / 'Library/PackageCache').iterdir():
    package = directory / 'package.json'
    if not package.is_file():
        continue
    data = json.loads(package.read_text(encoding='utf-8-sig'))
    deps[data['name']] = 'file:' + directory.as_posix()
(REVIEW / 'Packages/manifest.json').write_text(json.dumps({'dependencies':deps}, indent=2), encoding='utf-8')

index = {}
for meta in ASSETS.rglob('*.meta'):
    try:
        match = GUID.search(meta.read_bytes()[:8192])
        if match:
            index[match.group(1).decode('ascii')] = Path(str(meta)[:-5])
    except OSError:
        pass

start = [PROJECT / 'Assets/_Game/Resources/Environment/NavalBaseMenu.prefab',
         PROJECT / 'Assets/_Game/Resources/Environment/HarborFacilityDetails.prefab']
for key in ('wall_white','wall_beige','roof_slate','glass_blue','steel','steel_dark',
            'safety_yellow','dock_grey','roof_white','box_blue','box_white','crane_red','flag_navy',
            'concrete','concrete_light','road','mark_white','wall_grey','roof_blue','rubber',
            'hull_black','hull_red','box_red','box_green','box_orange','box_teal','rope'):
    start.append(PROJECT / f'Assets/_Game/Art/NavalBase/MAT_base_{key}.mat')

queue = deque(start)
seen = set()
missing = set()
while queue:
    src = queue.popleft()
    if src in seen or not src.is_file() or src.suffix.lower() == '.cs':
        continue
    seen.add(src)
    relative = src.relative_to(PROJECT)
    dst = REVIEW / relative
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)
    meta = Path(str(src)+'.meta')
    if meta.is_file():
        shutil.copy2(meta, Path(str(dst)+'.meta'))
    if src.suffix.lower() in TEXT_TYPES:
        data = src.read_bytes()
        for match in GUID.finditer(data):
            identity = match.group(1).decode('ascii')
            if identity in index:
                queue.append(index[identity])
            else:
                missing.add(identity)  # package and Unity built-in GUIDs are resolved by Unity

for source in ('Assets/_Game/Editor/HarborFacilityDetailsBuilder.cs',
               'Assets/_Game/Scripts/UI/HarborFacilityArt.cs',
               'Assets/_Game/Scripts/UI/HarborCraneMotion.cs',
               'Assets/_Game/Scripts/UI/HarborFacilityMotion.cs'):
    src = PROJECT / source
    dst = REVIEW / source
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)
    meta = Path(str(src)+'.meta')
    if meta.is_file(): shutil.copy2(meta, Path(str(dst)+'.meta'))

print(f'REVIEW_PROJECT={REVIEW}\nCOPIED_ASSETS={len(seen)}\nEXTERNAL_OR_BUILTIN_GUIDS={len(missing)}')
