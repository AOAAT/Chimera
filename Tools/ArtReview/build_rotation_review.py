from pathlib import Path
import base64
import json
import shutil

root = Path(__file__).resolve().parents[2]
source = root / '.utmp/AssemblyRegression/RotationStudyResults'
out = root / 'ArtReview/RotationStudy'
out.mkdir(parents=True, exist_ok=True)
model = json.loads((source / 'model.json').read_text(encoding='utf-8-sig'))
assets = {}
for layer in model['layers']:
    assets[layer['file']] = 'data:image/png;base64,' + base64.b64encode((source / layer['file']).read_bytes()).decode()
for file in source.iterdir():
    if file.is_file():
        shutil.copy2(file, out / file.name)
page = (root / 'Tools/ArtReview/rotation_review.html').read_text(encoding='utf-8')
page = page.replace('__MODEL__', json.dumps(model, ensure_ascii=False)).replace('__ASSETS__', json.dumps(assets))
(out / 'index.html').write_text(page, encoding='utf-8')
print('Rotation study packaged:', out / 'index.html')
