"""Reposition the forward auxiliary pair and clear the flight deck."""
from pathlib import Path

previous = Path(__file__).resolve().parent.parent / 'Stage3HybridCarrier_v5' / 'build.py'
builder = previous.read_text(encoding='utf8')
exec(compile(builder[:builder.rindex('exec(compile')], str(previous), 'exec'))

replace("""    ('Port', -1, 2.10, -2.42, 1.376, 1.71, 1.025),
    ('Starboard', 1, 2.10, -2.42, 1.376, 1.71, 1.025),
    ('BridgePort', -1, 1.58, 1.36, 1.215, 1.49, .79),
    ('BridgeStarboard', 1, 1.58, 1.36, 1.215, 1.49, .79),""",
        """    ('BridgePort', -1, 1.33, 2.60, 1.205, 1.30, .84),
    ('BridgeStarboard', 1, 1.33, 2.60, 1.205, 1.30, .84),""")

# Restore the rails where the flight-deck auxiliary platforms were removed.
replace("(-5.54,2.17),(-1.65,2.01)", "(-5.54,2.17),(-2.31,2.01)")
replace("render('AuxiliaryGunLayout', (-7.5,-4.7,10), (0,-.42,1.39), 9.10)",
        "render('AuxiliaryGunLayout', (-5.5,6.9,11.5), (0,2.33,1.56), 7.15)")

exec(compile(code, str(source), 'exec'))
