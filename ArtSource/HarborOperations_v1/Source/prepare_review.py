"""Prepare the review in a separate Unity project using the actual current harbor prefab assets."""
from collections import deque
from pathlib import Path
import argparse
import json
import re
import shutil

PACK = Path(__file__).resolve().parent.parent
GUID = re.compile(rb'guid:\s*([0-9a-f]{32})')
REFERENCES = {'.prefab', '.mat', '.asset', '.controller', '.unity', '.shader'}


def main():
    args = argparse.ArgumentParser()
    args.add_argument('source', type=Path, help='Original project root, read only')
    args.add_argument('review', type=Path, help='Destination for a separate review project')
    options = args.parse_args()
    source, review = options.source.resolve(), options.review.resolve()
    if review == source or source in review.parents:
        raise ValueError('Place the review project outside the original game project.')
    for folder in ('Assets', 'Packages', 'ProjectSettings'):
        (review/folder).mkdir(parents=True, exist_ok=True)
    shutil.copytree(source/'ProjectSettings', review/'ProjectSettings', dirs_exist_ok=True)
    shutil.copytree(source/'Assets/Settings', review/'Assets/Settings', dirs_exist_ok=True)

    manifest=json.loads((source/'Packages/manifest.json').read_text(encoding='utf-8-sig'))
    installed={key:value for key,value in manifest['dependencies'].items() if key.startswith('com.unity.modules.')}
    for directory in (source/'Library/PackageCache').iterdir():
        meta=directory/'package.json'
        if meta.is_file():
            data=json.loads(meta.read_text(encoding='utf-8-sig'))
            installed[data['name']]='file:'+directory.as_posix()
    (review/'Packages/manifest.json').write_text(json.dumps({'dependencies':installed},indent=2),encoding='utf-8')

    index={}
    for meta in (source/'Assets').rglob('*.meta'):
        match=GUID.search(meta.read_bytes()[:8192])
        if match:
            index[match.group(1)]=meta.with_suffix('')
    env=source/'Assets/_Game/Resources/Environment'
    queue=deque([env/'NavalBaseMenu.prefab',env/'HarborFacilityDetails.prefab',
                 *(source/'Assets/_Game/Art/NavalBase').glob('MAT_base_*.mat')])
    copied=set()
    while queue:
        origin=queue.popleft()
        if origin in copied or not origin.is_file() or origin.suffix=='.cs':
            continue
        target=review/origin.relative_to(source)
        target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(origin,target)
        meta=origin.with_name(origin.name+'.meta')
        if meta.is_file():
            shutil.copy2(meta,target.with_name(target.name+'.meta'))
        copied.add(origin)
        if origin.suffix.lower() in REFERENCES:
            queue.extend(index[m.group(1)] for m in GUID.finditer(origin.read_bytes()) if m.group(1) in index)

    for folder in ('Runtime','Editor'):
        shutil.copytree(PACK/'Source'/folder,review/'Assets/HarborOperations'/folder,dirs_exist_ok=True)
    print(f'{len(copied)} referenced assets copied to the review project.')


if __name__ == '__main__':
    main()

