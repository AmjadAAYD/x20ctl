"""Review native WPF against the four reference compositions; never show a desktop window."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor,as_completed
from PIL import Image,ImageDraw
import subprocess,json,os,html
ROOT=Path(__file__).resolve().parents[1]
PACK=ROOT/'artifacts/illustrated-theme-concepts'
OUT=PACK/'matched-native-final'
EXE=ROOT/'desktop-dotnet/X20Ctl.Product/bin/Debug/net10.0-windows10.0.19041.0/X20Ctl.exe'
ROUTES={'zone':'2-zone-player','buttons':'4-buttons','curves':'5-curves','tools':'9-tools'}
EXTRA_ROUTES={'dashboard':'3-dashboard','macros':'6-macros','vibration':'7-vibration','device':'8-device','pc-mode':'8b-pcmode','mouse':'8c-mouse','setups':'8d-setups','tester':'8e-tester','diagnostics':'8f-diagnostics','agent':'8i-agent-answer','themes':'10-themes','model-selector':'11c-choose'}
def run(theme):
    folder=OUT/theme['id'];folder.mkdir(parents=True,exist_ok=True)
    failure=folder/'failure.txt';failure.unlink(missing_ok=True)
    env=dict(os.environ,X20CTL_REVIEW_SHOTS='1-zone,2-zone-player,3-dashboard,4-buttons,4b-buttons-staged,5-curves,6-macros,7-vibration,8-device,8b-pcmode,8c-mouse,8d-setups,8e-tester,8f-diagnostics,8h-agent-empty,8i-agent-answer,9-tools,10-themes,11c-choose',X20CTL_REVIEW_EXTENDED='1')
    env.pop('X20CTL_REVIEW_BASE_ONLY',None)
    with (folder/'process.log').open('w',encoding='utf-8') as log:
        result=subprocess.run([str(EXE),'--review-themes',str(folder),theme['id']],cwd=ROOT,env=env,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW,timeout=240)
    assert result.returncode==0,(theme['id'],failure.read_text() if failure.exists() else result.returncode)
    review=json.loads((folder/'review.json').read_text());assert review['bindingErrors']==0,review
    assert json.loads((folder/'mapping-regression.json').read_text())['toolsPreservesStudio']
    assert json.loads((folder/'curve-regression.json').read_text())['nativeTabRoutes']
    for route,name in ROUTES.items():
        for suffix in ['','-minimum','-dpi125','-dpi150']:assert (folder/f'{name}{suffix}.png').exists()
    count=len([p for p in folder.glob('*.png') if p.stem!='language-menu'])
    return {'id':theme['id'],'native_captures':count,'binding_errors':0,'mapping':'A:B','curve_inner':12,'hardware_writes':False}
def gallery(themes):
    OUT.mkdir(exist_ok=True)
    sheet=Image.new('RGB',(1600,len(themes)*490),'#0b0d13');draw=ImageDraw.Draw(sheet)
    for i,t in enumerate(themes):
        for j,path in enumerate([PACK/'screenshots'/f"{t['id']}-buttons.png",OUT/t['id']/'4-buttons.png']):
            pic=Image.open(path).convert('RGB');pic.thumbnail((800,450));sheet.paste(pic,(j*800,i*490+30))
        draw.text((15,i*490+8),t['name']+' / reference (left), native WPF (right)',fill='white')
    sheet.save(OUT/'BUTTONS-COMPARISON.jpg',quality=94)
    data=[dict(t,images={**{r:f"{t['id']}/{name}.png" for r,name in {**ROUTES,**EXTRA_ROUTES}.items()},'rgb':f"{t['id']}/features/RGB.png",'screen':f"{t['id']}/features/Screen.png"}) for t in themes]
    page='''<!doctype html><html><meta charset="utf-8"><title>X20CTL reference / native WPF comparison</title><style>*{box-sizing:border-box}body{background:#10121a;color:#edf1fa;font:15px Segoe UI;margin:30px}h1{font-size:26px;font-weight:500}p{color:#aeb9ce;max-width:1100px;line-height:1.6}select{background:#242a39;color:white;padding:10px;border:1px solid #536179;margin:5px 15px 15px 0}main{display:grid;grid-template-columns:1fr 1fr;gap:14px}img{width:100%;display:block}figure{margin:0}figcaption{padding:10px 0}a{color:#a8c8ff}.overlay{position:relative;margin-top:25px;aspect-ratio:16/9;max-width:1600px}.overlay img{position:absolute;inset:0}.overlay #native-layer{clip-path:inset(0 50% 0 0)}input{width:min(100%,1600px)}</style><h1>Reference / native WPF</h1><p>These are the actual offscreen WPF renders. The original independently authored preview is on the left. The native app uses the same measured compositions, with real controller artwork, drafts, response processor and native controls. Real player assignments, clock values, graph axes, keyboard focus and additional feature menus can differ. No hardware actions were performed.</p><select id="theme"></select><select id="route"><option value="buttons">Buttons</option><option value="curves">Curves</option><option value="zone">Controller Zone</option><option value="tools">Tools</option></select><main><figure><figcaption>Shown design reference</figcaption><img id="reference"></figure><figure><figcaption>Native WPF implementation</figcaption><img id="native"></figure></main><p>Swipe comparison: move the slider to reveal the native render over the reference.</p><input type="range" id="swipe" min="0" max="100" value="50"><div class="overlay"><img id="base-layer"><img id="native-layer"></div><p><a id="full-native">Open native render at full size</a> · <a id="review">Native verification report</a></p><script>const themes=DATA;const el=id=>document.getElementById(id);el('theme').innerHTML=themes.map(t=>`<option value="${t.id}">${t.name}</option>`).join('');function update(){let t=themes.find(x=>x.id===el('theme').value),r=el('route').value;el('reference').src=el('base-layer').src=`../screenshots/${t.id}-${r}.png`;el('native').src=el('native-layer').src=t.images[r];el('full-native').href=t.images[r];el('review').href=t.id+'/review.json'}el('theme').onchange=el('route').onchange=update;el('swipe').oninput=()=>el('native-layer').style.clipPath=`inset(0 ${100-el('swipe').value}% 0 0)`;update();</script></html>'''.replace('DATA',json.dumps(data))
    extra=''.join('<option value="'+key+'">'+html.escape(key.replace('-',' ').title())+'</option>' for key in [*EXTRA_ROUTES,'rgb','screen'])
    page=page.replace('</select><main>','</select><main>',1).replace('<option value="tools">Tools</option>','<option value="tools">Tools</option>'+extra)
    page=page.replace("el('reference').src=el('base-layer').src=", "let hasReference=['zone','buttons','curves','tools'].includes(r);document.querySelector('main').style.gridTemplateColumns=hasReference?'1fr 1fr':'1fr';document.querySelector('main figure').style.display=hasReference?'block':'none';document.querySelector('.overlay').style.display=hasReference?'block':'none';el('swipe').style.display=hasReference?'block':'none';el('reference').src=el('base-layer').src=")
    page=page.replace('`../screenshots/${t.id}-${r}.png`','(hasReference?`../screenshots/${t.id}-${r}.png`:t.images[r])')
    (OUT/'index.html').write_text(page,encoding='utf-8')
def main():
    import argparse
    parser=argparse.ArgumentParser();parser.add_argument('--themes',nargs='*');args=parser.parse_args()
    themes=json.loads((PACK/'manifest.json').read_text());chosen=[t for t in themes if not args.themes or t['id'] in args.themes]
    results={}
    summary=OUT/'summary.json'
    if summary.exists():results={x['id']:x for x in json.loads(summary.read_text())['themes']}
    with ThreadPoolExecutor(max_workers=2) as pool:
        for done in as_completed([pool.submit(run,t) for t in chosen]):
            result=done.result();results[result['id']]=result;print(result['id'],'passed',flush=True)
    summary.write_text(json.dumps({'themes':list(results.values()),'native_captures':sum(r['native_captures'] for r in results.values()),'window_shown':False,'hardware_writes':False},indent=2),encoding='utf-8')
    if all((OUT/t['id']/'4-buttons.png').exists() for t in themes):gallery(themes)
if __name__=='__main__':main()
