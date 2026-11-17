from pathlib import Path
source=Path(__file__).resolve().parents[1]/'WWII_ModePack_v29/validate.py'
exec(compile(source.read_text(encoding='utf8'),str(source),'exec'))
