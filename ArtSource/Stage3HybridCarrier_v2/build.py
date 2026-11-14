"""Rebuild v1 with smaller guns, leaving its source and outputs intact."""
from pathlib import Path

source = Path(__file__).resolve().parents[1] / 'Stage3HybridCarrier_v1' / 'build.py'
code = source.read_text(encoding='utf8')
original = 'main_gun(1,5.59,1.00,.12)\nmain_gun(2,3.59,.93,.74)'
assert code.count(original) == 1, 'v1 gun build calls changed'
code = code.replace(original, '''def scaled_main_gun(i, y, deck, riser):
    # Fix both static foundations and moving parts around the deck seating point.
    # All mesh vertices and socket locations are baked; no scaled transforms.
    from mathutils import Matrix
    anchor = Vector((0,y,deck))
    factors = Matrix.Diagonal((.76,.75,.68,1))
    prior = set(c.descendants(root))
    main_gun(i,y,deck,riser)
    for o in c.descendants(root):
        if o in prior:
            continue
        o.location = (anchor + factors @ (o.location - anchor)
                      if o.parent in prior else factors @ o.location)
        if o.type == 'MESH':
            o.data.transform(factors)

scaled_main_gun(1,5.59,1.00,.12)
scaled_main_gun(2,3.59,.93,.74)''')
code = code.replace("'aa_per_side':3}", "'aa_per_side':3,'main_gun_factors_xyz':[.76,.75,.68]}")
exec(compile(code, str(source), 'exec'))
