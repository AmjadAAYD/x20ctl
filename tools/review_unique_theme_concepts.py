"""Render and exercise concept previews offscreen; never opens a desktop window."""
import asyncio,json,argparse
from pathlib import Path
from playwright.async_api import async_playwright
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[1]/'artifacts'/'illustrated-theme-concepts'

async def review():
    parser=argparse.ArgumentParser();parser.add_argument('--themes',nargs='*');args=parser.parse_args()
    themes=json.loads((ROOT/'manifest.json').read_text())
    report=json.loads((ROOT/'review.json').read_text()) if args.themes and (ROOT/'review.json').exists() else {}
    shots=ROOT/'screenshots';shots.mkdir(exist_ok=True)
    async with async_playwright() as p:
        browser=await p.chromium.launch(channel='chrome',headless=True)
        page=await browser.new_page(viewport={'width':1600,'height':900},device_scale_factor=1)
        page.set_default_timeout(5000)
        errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
        for t in themes:
            if args.themes and t['id'] not in args.themes:continue
            result={'routes':[],'interaction_checks':[],'outside_controls':[],'obscured_controls':[]}
            report[t['id']]=result
            for route in t['routes']:
                await page.goto((ROOT/t['id']/'index.html').as_uri()+'?page='+route)
                await page.evaluate('document.fonts.ready');await page.wait_for_timeout(400)
                assert await page.locator('#stage').get_attribute('data-page')==route,(t['id'],route,errors)
                assert await page.evaluate('Array.from(document.images).every(x=>x.complete&&x.naturalWidth>0)'),(t['id'],'missing image')
                assert await page.evaluate('''()=>new Promise(resolve=>{let i=new Image();i.onload=()=>resolve(i.naturalWidth>0);i.onerror=()=>resolve(false);i.src=getComputedStyle(document.querySelector('#stage')).backgroundImage.slice(5,-2);})'''),(t['id'],route,'missing backdrop')
                await page.locator('#stage').screenshot(path=str(shots/f"{t['id']}-{route}.png"))
                bounds=await page.evaluate('''() => Array.from(document.querySelectorAll('#stage button:not(:disabled),#stage svg a[data-input]:not([tabindex="-1"]),#stage input')).map(e=>{let r=e.getBoundingClientRect();let x=r.x+r.width/2,y=r.y+r.height/2;let hit=document.elementFromPoint(x,y);return {name:e.getAttribute('aria-label')||e.textContent.trim().slice(0,40),x:r.x,y:r.y,w:r.width,h:r.height,outside:r.x< -1||r.y< -1||r.right>1601||r.bottom>901,obscured:!!hit&&!e.contains(hit)&&!hit.contains(e)}}).filter(x=>x.w>0&&x.h>0)''')
                result['outside_controls'] += [{'route':route,**v} for v in bounds if v['outside']]
                result['obscured_controls'] += [{'route':route,**v} for v in bounds if v['obscured']]
                result['routes'].append(route)
                (ROOT/'review.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
                if route=='buttons':
                    await page.get_by_role('button',name='Map A to B',exact=True).click()
                    assert await page.evaluate('state.mapping.A')=='B'
                    assert await page.locator('.change-note').first.inner_text()=='1 unsent change'
                    assert await page.get_by_role('button',name='Apply Changes',exact=True).is_disabled()
                    key=page.get_by_role('button',name='Map A to X',exact=True);await key.focus();await page.keyboard.press('Enter')
                    assert await page.evaluate('state.mapping.A')=='X'
                    await page.locator('[data-reset]').first.click();assert await page.evaluate('Object.keys(state.mapping).length')==0
                    result['interaction_checks']+=['click stages mapping','keyboard stages mapping','Apply remains offline','reset clears local draft']
                    result['layout']=await page.evaluate('''()=>{let rect=e=>{let r=e.getBoundingClientRect();return [r.x,r.y,r.width,r.height].map(Math.round)};return {controller:rect(document.querySelector('.pad')),navigation:rect(document.querySelector('nav')),outputs:rect(document.querySelector('.outputs'))}}''')
                    await page.locator('[data-page="curves"]').first.click();assert await page.evaluate('state.route')=='curves'
                    result['interaction_checks'].append('navigation changes route')
                elif route=='curves':
                    await page.locator('[data-preset="quick"]').click();assert await page.evaluate('state.preset')=='quick'
                    control=page.get_by_role('slider',name='Inner dead zone',exact=True);await control.fill('12');await control.dispatch_event('input')
                    assert await page.evaluate('state.deadzone')==12
                    assert await page.locator('[data-value="deadzone"]').inner_text()=='12%'
                    result['interaction_checks']+=['preset changes local curve','slider updates local draft']
                elif route=='zone':
                    assert await page.locator('[data-add]').count()==3
                    await page.get_by_role('button',name='Open studio',exact=True).click();assert await page.evaluate('state.route')=='buttons'
                    result['interaction_checks'].append('assigned player opens Studio')
                elif route=='tools':
                    assert await page.locator('[data-tool]').count()==9
                    await page.locator('[data-tool="0"]').click();assert await page.locator('#message').is_visible()
                    await page.locator('#message button').click();assert not await page.locator('#message').is_visible()
                    result['interaction_checks'].append('Tools actions identified as previews')
            await page.emulate_media(reduced_motion='reduce');await page.goto((ROOT/t['id']/'index.html').as_uri()+'?page=buttons');await page.wait_for_timeout(150)
            assert await page.locator('#stage').get_attribute('data-page')=='buttons';await page.set_viewport_size({'width':1040,'height':700});await page.wait_for_timeout(100)
            rect=await page.locator('#viewport').bounding_box();assert rect['width']<=1041 and rect['height']<=701
            result['interaction_checks']+=['reduced-motion render','1040x700 viewport fit'];await page.set_viewport_size({'width':1600,'height':900});await page.emulate_media(reduced_motion='no-preference')
            report[t['id']]=result;print(t['id'],len(result['outside_controls']),'outside,',len(result['obscured_controls']),'obscured',flush=True)
        await browser.close()
    assert not errors,errors
    assert len({json.dumps(v['layout'],sort_keys=True) for v in report.values()})==7
    (ROOT/'review.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    sheet=Image.new('RGB',(1600,7*490),(13,15,20));draw=ImageDraw.Draw(sheet)
    for i,t in enumerate(themes):
        image=Image.open(shots/f"{t['id']}-buttons.png");image.thumbnail((800,450));sheet.paste(image,(0,i*490+30))
        image2=Image.open(shots/f"{t['id']}-zone.png");image2.thumbnail((800,450));sheet.paste(image2,(800,i*490+30));draw.text((20,i*490+8),t['name']+' / Buttons and Controller Zone',fill='white')
    sheet.save(ROOT/'COMPARISON.png')
    print('Concept pack reviewed; interaction checks passed. Review layout findings in review.json.')
if __name__=='__main__':asyncio.run(review())
