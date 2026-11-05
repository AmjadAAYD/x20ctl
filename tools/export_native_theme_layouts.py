"""Compile the independently authored previews into native layout measurements, not screenshots.

Runtime rendering uses WPF text, geometry, images and the application's existing controls.
Chrome is used only by this development exporter; it is not shipped or embedded in X20CTL.
"""
from pathlib import Path
import asyncio,json
from playwright.async_api import async_playwright
ROOT=Path(__file__).resolve().parents[1]
CONCEPTS=ROOT/'artifacts/illustrated-theme-concepts'
OUT=ROOT/'desktop-dotnet/X20Ctl.Product/Brand/illustrated'
SHELL=r'''id=>{
 const stage=document.querySelector('#stage');
 const keep={vaporwave:'.vw-header,.vw-dock,.vw-footer','frutiger-aero':'.fa-header,.fa-river-nav,.fa-footer',gothic:'.go-header,.go-index,.go-footer',manga:'.ma-head,.ma-index,.ma-footer','tokyo-night':'.tn-header,.tn-sidebar,.tn-footer',synthwave:'.sy-header,.sy-cartridge-rail,.sy-footer','crimson-rebellion':'.cr-header,.cr-cut-index,.cr-footer'};
 const designs={
  vaporwave:`<div style="position:absolute;left:76px;top:112px"><small>Controller Studio</small><h1 data-native-text="page" style="font-size:48px;font-weight:400;margin-top:14px">Dashboard</h1></div><div class="vw-inspector native-workspace" style="left:76px;right:58px;top:226px;height:514px;min-height:0;padding:18px"></div>`,
  'frutiger-aero':`<div class="fa-context" style="top:112px"><small>Controller Studio</small><h1 data-native-text="page">Dashboard</h1></div><div class="fa-lake native-workspace" style="left:76px;top:225px;width:1448px;height:490px;border-radius:65px 25px 65px 25px"></div>`,
  gothic:`<div class="go-title" style="position:absolute;left:250px;top:112px"><small>Controller Studio</small><h1 data-native-text="page">Dashboard</h1></div><div class="go-graph-frame native-workspace" style="left:250px;right:42px;top:244px;height:572px;padding:20px"></div>`,
  manga:`<div class="ma-chapter" style="left:180px;top:108px"><small>CONTROLLER STUDIO</small><h1 data-native-text="page">Dashboard</h1></div><div class="ma-hero-panel native-workspace" style="position:absolute;left:178px;right:38px;top:235px;height:556px;clip-path:polygon(0 0,100% 0,98.5% 100%,0 100%)"></div>`,
  'tokyo-night':`<div class="tn-page-heading" style="position:absolute;left:250px;top:112px"><div><small>Controller Studio</small><h1 data-native-text="page">Dashboard</h1></div></div><div class="tn-controller-window native-workspace" style="position:absolute;left:250px;right:38px;top:210px;height:608px"></div>`,
  synthwave:`<div class="sy-title" style="position:absolute;left:58px;top:110px"><small>CONTROLLER STUDIO</small><h1 data-native-text="page">Dashboard</h1></div><div class="sy-monitor native-workspace" style="position:absolute;left:58px;top:240px;width:1310px;height:530px"></div>`,
  'crimson-rebellion':`<div class="cr-studio-title" style="left:250px;top:120px"><small>CONTROLLER STUDIO</small><h1 data-native-text="page">Dashboard</h1></div><div class="cr-controller-poster native-workspace" style="left:250px;right:40px;top:264px;height:545px;transform:none;clip-path:polygon(1% 0,100% 1%,99% 97%,74% 100%,39% 98%,0 100%)"></div>`
 };
 const chrome=Array.from(stage.querySelectorAll(keep[id]));stage.replaceChildren(...chrome);stage.insertAdjacentHTML('beforeend',designs[id]);
 stage.querySelectorAll('footer>span:not(.change-note)').forEach(e=>e.remove());
 stage.querySelectorAll('footer .apply').forEach(e=>e.style.marginLeft='auto');
 stage.querySelectorAll('.vw-breadcrumb span').forEach(e=>e.dataset.nativeText='page-breadcrumb');
 stage.querySelectorAll('.go-header-title').forEach(e=>e.dataset.nativeText='page');
}'''
JS=r'''()=>{
 const root=document.querySelector('#stage'), rr=root.getBoundingClientRect();
 const result={width:1600,height:900,primitives:[],widgets:[],pads:[],graph:null,veil:getComputedStyle(root,':before').backgroundImage};
 const rect=e=>{let r=e.getBoundingClientRect();return {x:r.x-rr.x,y:r.y-rr.y,w:r.width,h:r.height}};
 const clip=(value,r)=>{if(!value.startsWith('polygon('))return value;let at=(t,size)=>{let v=t.replace(/calc\(|\)/g,'').replace(/([\d.]+)%/g,(_,n)=>String(+n*size/100)).replace(/px/g,'');let parts=v.match(/[+-]?\s*[\d.]+/g)||[];return parts.reduce((n,t)=>n+parseFloat(t.replace(/\s/g,'')),0)};return 'polygon('+value.slice(8,-1).split(',').map(pair=>{let tokens=pair.trim().match(/calc\([^)]*\)|[^\s]+/g);return at(tokens[0],r.w)+'px '+at(tokens[1],r.h)+'px'}).join(',')+')'};
 const alpha=e=>{let a=1;for(let p=e;p&&p!==root;p=p.parentElement)a*=parseFloat(getComputedStyle(p).opacity);return a};
 const style=e=>{let s=getComputedStyle(e),r=rect(e);let radii=[s.borderTopLeftRadius,s.borderTopRightRadius,s.borderBottomRightRadius,s.borderBottomLeftRadius].flatMap(x=>{let v=x.split(' ');let at=(t,size)=>parseFloat(t)*(t.includes('%')?size/100:1);return [at(v[0],r.w),at(v[1]||v[0],r.h)]});return {fill:s.backgroundColor,gradient:s.backgroundImage,border:s.borderTopColor,line:parseFloat(s.borderTopWidth),radii,clip:clip(s.clipPath,r),opacity:alpha(e),color:s.color,font:s.fontFamily,size:parseFloat(s.fontSize),weight:parseInt(s.fontWeight)||400,align:s.textAlign}};
 const outputSelectors=['.output-value','.fa-binding-flow>div:last-child b','.go-binding-row:last-of-type strong','.ma-before-after>span:last-child b','.tn-inline-binding>span:last-child b','.sy-binding-display>strong:last-of-type','.cr-binding-paper b:last-of-type'];
 outputSelectors.forEach(s=>document.querySelectorAll(s).forEach(e=>e.dataset.nativeText='output'));
 document.querySelectorAll('.change-note').forEach(e=>e.dataset.nativeText='count');
 document.querySelectorAll('[data-value],[data-readout]').forEach(e=>e.dataset.nativeText=e.dataset.value||'readout:'+e.dataset.readout);
 document.querySelectorAll('button[data-input] strong').forEach(e=>e.dataset.nativeText='assignment:'+e.closest('button').dataset.input);
 document.querySelectorAll('button.selected').forEach(e=>e.classList.remove('selected'));
 document.querySelectorAll('time').forEach(e=>e.dataset.nativeText='clock');
 document.querySelectorAll('.tn-player b').forEach(e=>e.dataset.nativeText='player-name');
 document.querySelectorAll('.tn-player-icon').forEach(e=>e.dataset.nativeText='player-number');
 document.querySelectorAll('.tn-player small').forEach(e=>e.dataset.nativeText='controller-state');
 if(state.route==='zone')document.querySelectorAll('article').forEach((e,i)=>{e.dataset.nativePlayer=i;e.querySelectorAll('h2').forEach(x=>x.dataset.nativeText='player-model:'+i);e.querySelectorAll('.offline').forEach(x=>x.dataset.nativeText='player-status:'+i);e.querySelectorAll('button').forEach(x=>x.dataset.nativeText='player-action:'+i)});
 // Export neutral faces. Native selection/focus follows the actual draft and tab state at runtime.
 document.querySelectorAll('[aria-pressed],[aria-current]').forEach(e=>{e.setAttribute('aria-pressed','false');e.setAttribute('aria-current','false')});
 let channel=0;
 function action(e){
   if(e.closest('[data-native-player]'))return 'player:'+e.closest('[data-native-player]').dataset.nativePlayer;
   if(e.dataset.output)return 'output:'+e.dataset.output;
   if(e.dataset.input)return 'input:'+e.dataset.input;
   if(e.dataset.page)return 'nav:'+e.textContent.trim();
   if(e.dataset.preset)return 'preset:'+e.dataset.preset;
   if(e.dataset.tool)return 'tool:'+e.dataset.tool;
   if(e.hasAttribute('data-reset'))return 'reset';
   if(e.classList.contains('apply'))return 'apply';
   if(e.dataset.field)return 'slider:'+e.dataset.field;
   let text=e.textContent.trim();
   if(['Left stick','Right stick','Left trigger','Right trigger','LT','RT'].includes(text))return 'channel:'+channel++;
   return 'nav:'+text;
 }
 function visit(e,list,origin={x:0,y:0}){
   const s=getComputedStyle(e),r=rect(e);if(s.display==='none'||s.visibility==='hidden')return;
   if(e.matches('.window-buttons')){
      ['minimize','maximize','close'].forEach((name,i)=>{result.widgets.push({x:r.x+i*r.w/3,y:r.y,w:r.w/3,h:r.h,key:'chrome:'+name,primitives:[{kind:'text',x:0,y:0,w:r.w/3,h:r.h,text:['−','□','×'][i],...style(e)}]})});return;
   }
   if(e.matches('.pad')){
      let angle=0;for(let p=e;p&&p!==root;p=p.parentElement){let m=new DOMMatrix(getComputedStyle(p).transform);angle+=Math.atan2(m.b,m.a)*180/Math.PI;}
      let w=parseFloat(s.width),h=parseFloat(s.height);
      result.pads.push({...r,x:r.x+r.w/2-w/2,y:r.y+r.h/2-h/2,w,h,angle,empty:e.classList.contains('empty-pad'),key:e.closest('[data-native-player]')?'player-art:'+e.closest('[data-native-player]').dataset.nativePlayer:'controller'});return;
   }
   if(e.matches('.graph')){result.graph=r;return;}
   if(e.matches('.native-workspace'))result.graph={...r,key:'workspace'};
   if(e.matches('button,input[type=range]')){
      let key=action(e);
      const capture=()=>{let primitives=[],own={kind:'box',...r,...style(e)};own.x=0;own.y=0;primitives.push(own);if(e.tagName==='BUTTON')for(let n of e.childNodes)node(n,primitives,{x:r.x,y:r.y});return primitives};
      let primitives=capture(),active=[];
      if(/^(output|input|nav|preset|channel):/.test(key)){
         e.setAttribute('aria-pressed','true');e.setAttribute('aria-current','page');e.classList.add('selected');active=capture();
         e.setAttribute('aria-pressed','false');e.setAttribute('aria-current','false');e.classList.remove('selected');
      }
      result.widgets.push({...r,key,primitives,active});return;
   }
   if(e.matches('svg'))return;
   if(e.matches('img')){list.push({kind:'logo',...r,x:r.x-origin.x,y:r.y-origin.y,opacity:alpha(e)});return;}
   let st=style(e);
   if(r.w>0&&r.h>0&&(st.fill!=='rgba(0, 0, 0, 0)'||st.gradient!=='none'||st.line>0))list.push({kind:'box',...r,...st,x:r.x-origin.x,y:r.y-origin.y});
   for(let n of e.childNodes)node(n,list,origin);
 }
 function node(n,list,origin){
   if(n.nodeType===1){visit(n,list,origin);return;}
   if(n.nodeType!==3||!n.textContent.trim())return;
   let e=n.parentElement,st=style(e),lines=[],boundKey=e.closest('[data-native-text]')?.dataset.nativeText||'';
   // Browser-measured line breaks avoid guessing about wrapping, gutters or heading hierarchy.
   for(let i=0;i<n.length;i++){
      let range=document.createRange();range.setStart(n,i);range.setEnd(n,i+1);let r=range.getBoundingClientRect();if(!r.width||!r.height)continue;
      let last=lines.at(-1);if(last&&(boundKey.length>0||Math.abs(last.y-(r.y-rr.y))<1)){last.text+=n.textContent[i];last.w=r.right-rr.x-last.x}
      else lines.push({text:n.textContent[i],x:r.x-rr.x,y:r.y-rr.y,w:r.width,h:r.height});
   }
   for(let line of lines){let text=line.text.replace(/\s+/g,' ');if(!text.trim())continue;
      let key=e.closest('[data-native-text]')?.dataset.nativeText||'';
      if(text.trim()==='%')key='';
      if(!key&&text.trim()==='A'&&!e.closest('button'))key='input';
      if(!key&&/^A button$/.test(text.trim()))key='input-name';
      list.push({kind:'text',...line,...st,text:safeText(text,getComputedStyle(e).textTransform),key,x:line.x-origin.x,y:line.y-origin.y});
   }
 }
 function safeText(t,transform){return transform==='uppercase'?t.toUpperCase():t}
 for(let e of root.children)visit(e,result.primitives);
 return result;
}'''
async def main():
    themes=json.loads((CONCEPTS/'manifest.json').read_text())
    async with async_playwright() as p:
        browser=await p.chromium.launch(channel='chrome',headless=True)
        page=await browser.new_page(viewport={'width':1600,'height':900})
        for theme in themes:
            folder=OUT/theme['id']/'layouts';folder.mkdir(exist_ok=True)
            for route in ['buttons','curves','tools','zone','shell']:
                await page.goto((CONCEPTS/theme['id']/'index.html').as_uri()+'?page='+('buttons' if route=='shell' else route))
                await page.emulate_media(reduced_motion='reduce');await page.evaluate('document.fonts.ready');await page.wait_for_timeout(180)
                if route=='shell':await page.evaluate(SHELL,theme['id'])
                data=await page.evaluate(JS)
                (folder/f'{route}.json').write_text(json.dumps(data,separators=(',',':')),encoding='utf-8')
            print(theme['id'],flush=True)
        await browser.close()
if __name__=='__main__':asyncio.run(main())


