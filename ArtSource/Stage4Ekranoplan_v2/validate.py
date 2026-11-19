"""Check FBX round trip and verify that the first release's prefab contract still holds."""
import json
from pathlib import Path
import sys

OUT=Path(__file__).resolve().parent
BASE=OUT.parent/'Stage4Ekranoplan_v1'
sys.dont_write_bytecode=True
sys.path.insert(0,str(BASE))
import validate as original

before=json.loads((BASE/'manifest.json').read_text(encoding='utf-8'))
after=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
assert len(after)==7
for name,asset in after.items():
    assert asset['parts']==before[name]['parts'],f'{name}: node names, hierarchy or pivot changed'
    for lo,original_lo in zip(asset['bounds_blender_m'],before[name]['bounds_blender_m']):
        assert all(abs(a-b)<.10 for a,b in zip(lo,original_lo)),f'{name}: visible envelope changed'

original.ROOT=OUT
original.validate(after)

