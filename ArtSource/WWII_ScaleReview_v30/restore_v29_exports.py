"""Regenerate original v29 FBX for historical before/after review, no renders."""
from pathlib import Path
source=Path(__file__).resolve().parents[1]/'WWII_ModePack_v29/build.py'
code=source.read_text(encoding='utf8')
code=code.replace("sys.path.insert(0,str(Path(__file__).resolve().parent))", "sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'WWII_ModePack_v29'))")
code=code[:code.index('# Consistent neutral studio.')]
exec(compile(code,str(source),'exec'))
