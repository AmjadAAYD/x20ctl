"""Prepare self-contained concept assets; layouts are independently authored per theme."""
from pathlib import Path
import json,re,shutil
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts'/'illustrated-theme-concepts'
PRODUCT=ROOT/'desktop-dotnet'/'X20Ctl.Product'
SOURCE=ROOT/'tools'/'theme_designs'
def build():
    OUT.mkdir(parents=True,exist_ok=True);(OUT/'assets').mkdir(exist_ok=True)
    for name in ['shared.css','shared.js']:shutil.copy2(SOURCE/name,OUT/name)
    Image.open(ROOT/'src/assets/controllers/x20/native/front-full.png').resize((1024,683),Image.Resampling.LANCZOS).save(OUT/'assets/controller.png')
    spec=(PRODUCT/'IllustratedThemeSpec.cs').read_text(encoding='utf-8')
    themes=re.findall(r'new IllustratedThemeSpec\("([^"]+)","([^"]+)","([^"]+)","([^"]+)"',spec)
    entries=[]
    for key,name,folder,prefix in themes:
        art=OUT/key/'art';art.mkdir(parents=True,exist_ok=True)
        for name in ['theme.css','theme.js']:shutil.copy2(SOURCE/key/name,OUT/key/name)
        for route,num,slug in [('zone',2,'controller-zone'),('buttons',5,'buttons'),('curves',6,'curves'),('tools',17,'tools')]:
            shutil.copy2(PRODUCT/'Brand/illustrated'/key/'wallpapers'/f'{prefix}-{num:02d}-{slug}.jpg',art/f'{route}.jpg')
        shutil.copy2(PRODUCT/'Brand/illustrated'/key/'logo'/f'{prefix}-logo-mark.png',art/'mark.png')
        page=f'''<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>X20CTL · {name} concept</title><link rel="stylesheet" href="../shared.css"><link rel="stylesheet" href="theme.css"></head><body data-theme="{key}"><main id="stage" aria-label="{name} design preview"></main><dialog id="message"><form method="dialog"><h2></h2><p></p><button>Close</button></form></dialog><script src="../geometry.js"></script><script src="../shared.js"></script><script src="theme.js"></script></body></html>'''
        page=page.replace('<main id="stage"','<div id="viewport"><main id="stage"').replace('</main><dialog','</main></div><dialog')
        (OUT/key/'index.html').write_text(page,encoding='utf-8');entries.append({'id':key,'name':name,'routes':['zone','buttons','curves','tools']})
    shapes=json.loads((ROOT/'src/assets/controllers/control-shapes.json').read_text())['models']['x20']['front']
    (OUT/'geometry.js').write_text('window.CONTROL_SHAPES='+json.dumps(shapes)+';',encoding='utf-8')
    (OUT/'manifest.json').write_text(json.dumps(entries,indent=2),encoding='utf-8')
    print('Prepared seven concept folders with real controller geometry and 28 page backdrops.')
if __name__=='__main__':build()
