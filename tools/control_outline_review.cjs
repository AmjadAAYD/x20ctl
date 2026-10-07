/* Isolated production review. Mock bridge; no host screen or controller access. */
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const http = require('node:http');
const { createHash } = require('node:crypto');
const flags = new Map(process.argv.slice(2).reduce((p,v,i,a) => i%2 ? p : [...p,[v,a[i+1]]], []));
const { chromium } = require(flags.get('--playwright') || 'playwright');
const root = path.resolve(__dirname,'..');
const dist = path.join(root,'dist-ui');
const macroOnly = flags.has('--macro-only');
const out = path.join(root,macroOnly ? 'artifacts/macro-preview-fit' : 'artifacts/control-outline-repair');
const report = { passed: false, mode: 'isolated headless production frontend; mock disconnected bridge', models: [] };
const labels = { SELECT:'Select / Back', START:'Start', DPAD_UP:'D-pad Up', DPAD_DOWN:'D-pad Down', DPAD_LEFT:'D-pad Left', DPAD_RIGHT:'D-pad Right' };
const mime = { '.html':'text/html', '.js':'application/javascript', '.css':'text/css', '.png':'image/png', '.svg':'image/svg+xml', '.ttf':'font/ttf' };
(async () => {
 await fs.mkdir(out,{recursive:true});
 const server = http.createServer(async (req,res) => {
  try {
   const url = new URL(req.url,'http://127.0.0.1');
   const file = path.resolve(dist,'.'+decodeURIComponent(url.pathname==='/'?'/index.html':url.pathname));
   if (!file.startsWith(dist+path.sep)) {res.writeHead(403).end(); return;}
   res.setHeader('Content-Type',mime[path.extname(file)] || 'application/octet-stream'); res.end(await fs.readFile(file));
  } catch {res.writeHead(404).end();}
 });
 await new Promise(r=>server.listen(0,'127.0.0.1',r));
 let browser;
 try {
  const catalog=JSON.parse(await fs.readFile(path.join(root,'x20ctl/controllers/catalog.json'),'utf8'));
  const shapes=JSON.parse(await fs.readFile(path.join(root,'src/assets/controllers/control-shapes.json'),'utf8'));
  const framing=JSON.parse(await fs.readFile(path.join(root,'src/assets/controllers/framing.json'),'utf8'));
  for (const asset of JSON.parse(await fs.readFile(path.join(root,'artifacts/rear-controller-sources/front-assets-before.json'),'utf8'))) {
   assert.equal(createHash('sha256').update(await fs.readFile(path.join(root,asset.path))).digest('hex'),asset.sha256);
  }
  report.frontPhotosUnchanged=true;
  browser=await chromium.launch({headless:true,executablePath:flags.get('--browser')});
  async function checkTarget(target,shape) {
   const metrics=await target.evaluate((e,b) => {
    const plane=e.parentElement.getBoundingClientRect(), r=e.getBoundingClientRect(), s=getComputedStyle(e);
    return {offset:Math.max(Math.abs(r.x-plane.x-b[0]/1536*plane.width), Math.abs(r.y-plane.y-b[1]/1024*plane.height),Math.abs(r.width-b[2]/1536*plane.width),Math.abs(r.height-b[3]/1024*plane.height)),transform:s.transform,border:s.borderWidth,viewBox:e.querySelector('svg').getAttribute('viewBox')};
   },shape.bounds);
   assert.ok(metrics.offset<.15,JSON.stringify(metrics));
   assert.equal(metrics.transform,'none'); assert.equal(metrics.border,'0px');
   assert.equal(metrics.viewBox,shape.bounds.join(' '));
   assert.equal(await target.locator('path').getAttribute('d'),shape.path);
   assert.equal(await target.locator('path').getAttribute('transform'),shape.transform || null);
  }
  for (const model of catalog) {
   if(macroOnly && !model.macroSlots.length) continue;
   const context=await browser.newContext({viewport:{width:1400,height:940},reducedMotion:'reduce'});
   const page=await context.newPage(); const errors=[]; page.on('pageerror',e=>errors.push(e.message));
   await page.addInitScript(() => {
    window.outlineCalls=[];
    window.pywebview={api:{request:async(operation,payload) => {
     window.outlineCalls.push(operation);
     if(!['select_model','disconnect','bootstrap','input'].includes(operation)) throw new Error('Unexpected operation: '+operation);
     return {ok:true,data:operation==='input'?{connected:false,input:null}:operation==='bootstrap'?{version:'4.0.1',profiles:[],warning:null,updatesEnabled:false}:operation==='select_model'?{model:payload.model}:{}};
    }}};
   });
   await page.goto(`http://127.0.0.1:${server.address().port}/`);
   await page.getByRole('button',{name:'Add controller for Player 2',exact:true}).click();
   await page.getByRole('dialog').getByRole('button',{name:`Select ${model.name}`,exact:true}).click();
   await page.getByRole('button',{name:'Enter Studio for Player 2',exact:true}).click();
   const studio=model.id==='x20'?page.locator('.rebranded-workspace').filter({has:page.locator('.editor')}):page.locator(`.model-workspace[data-model="${model.id}"]`);
   const stage=studio.locator('.mapping-controller-stage');
   const front=stage.locator('.controller-canvas');
   const rear=stage.locator('.controller-rear');
   const entry={model:model.id,front:[],rear:[],macroSlots:[],layouts:[]};
   if(!macroOnly) {
   for (const [key,shape] of Object.entries(shapes.models[model.id].front)) {
    const target=front.getByRole('button',{name:`Select ${labels[key] || key}`,exact:true});
    await target.click(); assert.equal(await target.getAttribute('aria-pressed'),'true');
    await checkTarget(target,shape); entry.front.push(key);
   }
   for (const key of ['LB','LT','RB','RT']) assert.equal(await front.getByRole('button',{name:`Select ${key}`,exact:true}).count(),0);
   await front.getByRole('button',{name:'Select A',exact:true}).click();
   await stage.screenshot({path:path.join(out,`${model.id}-front-A.png`)});
   await front.locator('.photo-control').evaluateAll(es=>es.forEach(e=>e.classList.add('is-selected')));
   await front.screenshot({path:path.join(out,`${model.id}-front-all-outlines.png`)});
   await front.locator('.photo-control').evaluateAll(es=>es.forEach(e=>e.classList.remove('is-selected')));
   for (const viewport of [{width:1400,height:940},{width:1920,height:1080},{width:1060,height:760}]) {
    await page.setViewportSize(viewport);
    for(const key of ['LB','LT','RB','RT']) {
     const row=studio.locator('.mapping-assignment-row').filter({has:page.getByRole('combobox',{name:`Remap ${key}`,exact:true})});
     await row.getByRole('button').click();
     assert.equal(await stage.locator('.controller-view').getAttribute('data-view'),'back');
     const target=rear.getByRole('button',{name:`Select ${key}`,exact:true});
     assert.equal(await target.getAttribute('aria-pressed'),'true');
     await checkTarget(target,shapes.models[model.id].back[key]);
     await target.click(); assert.equal(await studio.locator('.mapping-key-large').innerText(),key);
     entry.rear.push(`${viewport.width}:${key}`);
    }
    await studio.getByRole('button',{name:'Front View',exact:true}).click();
    assert.equal(await front.locator('.photo-control[aria-pressed=true]').count(),0);
    await studio.getByRole('combobox',{name:'Remap RT',exact:true}).focus();
    assert.equal(await rear.getByRole('button',{name:'Select RT',exact:true}).getAttribute('aria-pressed'),'true');
    await studio.locator('.mapping-assignment-row').filter({has:page.getByRole('combobox',{name:'Remap A',exact:true})}).getByRole('button').click();
    assert.equal(await front.getByRole('button',{name:'Select A',exact:true}).getAttribute('aria-pressed'),'true');
    const size=await front.boundingBox(); assert.ok(size.width<=471); assert.ok(size.height<=315);
    assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+2));
    entry.layouts.push(viewport);
   }
   await page.setViewportSize({width:1400,height:940});
   await studio.getByRole('button',{name:'Back View',exact:true}).click();
   for (const [slot,shape] of Object.entries(shapes.models[model.id].macros)) await checkTarget(rear.getByRole('button',{name:`Open ${slot} macro`,exact:true}),shape);
   await rear.locator('.photo-control').evaluateAll(es=>es.forEach(e=>e.classList.add('is-selected')));
   await rear.screenshot({path:path.join(out,`${model.id}-rear-all-outlines.png`)});
   await rear.locator('.photo-control').evaluateAll(es=>es.forEach(e=>e.classList.remove('is-selected')));
   }
   for(const slot of model.macroSlots) {
    await studio.locator('.chassis-nav').getByRole('button',{name:'Buttons',exact:true}).click();
    await studio.getByRole('button',{name:'Back View',exact:true}).click();
    await rear.getByRole('button',{name:`Open ${slot} macro`,exact:true}).click();
    assert.equal(await studio.locator('.macro-paddle[aria-pressed=true] strong').innerText(),slot);
    assert.equal(await studio.locator('.macro-controller-preview .controller-canvas').count(),0);
    assert.equal(await studio.locator('.macro-controller-preview .controller-rear-control').count(),0);
    entry.macroSlots.push(slot);
   }
   if(model.macroSlots.length) {
    await page.setViewportSize({width:1920,height:1080});
    const preview=await studio.locator('.macro-controller-preview').boundingBox(), sequencer=await studio.locator('.sequencer-panel').boundingBox();
    assert.ok(preview.width>=340 && preview.width<=441); assert.ok(Math.abs(preview.y-sequencer.y)<2); assert.ok(preview.x+preview.width<sequencer.x);
    await studio.locator('.macro-studio').screenshot({path:path.join(out,`${model.id}-compact-macros.png`)});
    await page.setViewportSize({width:800,height:800});
    const small=await studio.locator('.macro-controller-preview').boundingBox(); assert.ok(small.width<=401);
    assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+2));
    for(const viewport of [{width:1920,height:1080},{width:1400,height:940},{width:1060,height:760},{width:800,height:800}]) {
     await page.setViewportSize(viewport);
     const preview=studio.locator('.macro-controller-preview');
     const photo=preview.locator('.controller-rear');
     const metrics=await photo.evaluate((e,b) => {
      const c=e.getBoundingClientRect(),p=e.querySelector('.controller-rear-plane').getBoundingClientRect();
      const [x,y,w,h]=b;
      return {size:[c.width,c.height],aspect:p.width/p.height,margins:[(p.x+x/1536*p.width-c.x)/c.width,(c.right-p.x-(x+w)/1536*p.width)/c.width,(p.y+y/1024*p.height-c.y)/c.height,(c.bottom-p.y-(y+h)/1024*p.height)/c.height],resolution:e.querySelector('img').naturalWidth};
     },framing.models[model.id].back);
     assert.ok(metrics.margins.every(m=>m>=.049),`${model.id}: complete shell at ${viewport.width}`);
     assert.ok(Math.abs(metrics.aspect-1.5)<.001); assert.equal(metrics.resolution,1536);
     for(const slot of model.macroSlots) await checkTarget(preview.getByRole('button',{name:`Open ${slot} macro`,exact:true}),shapes.models[model.id].macros[slot]);
     assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+2));
     if(viewport.width>=1400) assert.ok(metrics.size[0]>=390);
     if(viewport.width===1400 || viewport.width===800) await studio.locator('.macro-studio').screenshot({path:path.join(out,`${model.id}-${viewport.width}-macro-fit.png`)});
     entry.layouts.push({viewport,...metrics});
    }
   }
   assert.deepEqual(errors,[]);
   entry.operations=await page.evaluate(()=>[...new Set(window.outlineCalls)]);
   if(model.placeholder) assert.deepEqual(entry.operations,['select_model']);
   report.models.push(entry); await context.close();
  }
  report.passed=true;
 } catch(error) {report.error=error.stack || String(error);process.exitCode=1;}
 finally {await fs.writeFile(path.join(out,'report.json'),JSON.stringify(report,null,2)); await browser?.close();await new Promise(r=>server.close(r));}
 console.log(JSON.stringify({passed:report.passed,models:report.models.length,error:report.error || null}));
})();
