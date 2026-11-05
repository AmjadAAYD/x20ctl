"""Offscreen intro and X20 Pro feature checks. The app's review sandbox prevents controller IO."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor,as_completed
import subprocess,json
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/illustrated-theme-concepts/matched-native-final'
EXE=ROOT/'desktop-dotnet/X20Ctl.Product/bin/Debug/net10.0-windows10.0.19041.0/X20Ctl.exe'
def run(theme,kind):
    folder=OUT/theme['id']/kind;folder.mkdir(parents=True,exist_ok=True)
    (folder/'failure.txt').unlink(missing_ok=True)
    flag='--review-theme-intro' if kind=='intro' else '--review-feature-tabs'
    result=subprocess.run([str(EXE),flag,str(folder),theme['id']],cwd=ROOT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=120)
    assert result.returncode==0,(theme['id'],kind,(folder/'failure.txt').read_text() if (folder/'failure.txt').exists() else result.returncode)
    if kind=='intro':
        data=json.loads((folder/'intro.json').read_text());assert data['headerAligned'] and data['skipCompletesOnce'] and data['silent'] and data['reducedMotionMilliseconds']==100
    else:assert (folder/'result.txt').read_text().startswith('PASS:')
    print(theme['id'],kind,'passed',flush=True)
    return {'theme':theme['id'],'check':kind,'passed':True,'hardware_io':False}
def main():
    import argparse
    parser=argparse.ArgumentParser();parser.add_argument('--kind',choices=['all','intro','features'],default='all');parser.add_argument('--themes',nargs='*');args=parser.parse_args()
    themes=json.loads((OUT.parent/'manifest.json').read_text())
    chosen=[t for t in themes if not args.themes or t['id'] in args.themes]
    kinds=['intro','features'] if args.kind=='all' else [args.kind]
    with ThreadPoolExecutor(max_workers=2) as pool:results=[job.result() for job in as_completed([pool.submit(run,t,k) for t in chosen for k in kinds])]
    if args.kind=='features':
        for theme in themes:
            data=json.loads((OUT/theme['id']/'intro/intro.json').read_text());assert data['headerAligned'] and data['skipCompletesOnce'] and data['silent']
            results.append({'theme':theme['id'],'check':'intro','passed':True,'hardware_io':False})
    target=OUT/'auxiliary-summary.json';combined={(x['theme'],x['check']):x for x in json.loads(target.read_text())} if target.exists() else {}
    combined.update({(x['theme'],x['check']):x for x in results});target.write_text(json.dumps(list(combined.values()),indent=2),encoding='utf-8')
if __name__=='__main__':main()
