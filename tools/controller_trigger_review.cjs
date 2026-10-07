/* Isolated browser and fixture input only. Never polls the user's controllers. */
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const http = require('node:http');
const { createHash } = require('node:crypto');
const flags = new Map(process.argv.slice(2).reduce((p, v, i, a) => i % 2 ? p : [...p, [v, a[i + 1]]], []));
const { chromium } = require(flags.get('--playwright') || 'playwright');
const root = path.resolve(__dirname, '..'), dist = path.join(root, 'dist-ui'), out = path.resolve(root, flags.get('--out') || 'artifacts/controller-trigger-motion');
const report = { passed: false, mode: 'isolated production UI with fixture bridge; no user screen or devices', models: [] };
const geometryReport = { models: [] };
const mime = { '.html':'text/html', '.js':'application/javascript', '.css':'text/css', '.png':'image/png', '.svg':'image/svg+xml', '.ttf':'font/ttf' };
(async () => {
  await fs.mkdir(out, { recursive:true });
  const server = http.createServer(async (req,res) => {
    try {
      const url = new URL(req.url, 'http://127.0.0.1');
      const file = path.resolve(dist, '.'+decodeURIComponent(url.pathname==='/'?'/index.html':url.pathname));
      if (!file.startsWith(dist+path.sep)) return res.writeHead(403).end();
      res.setHeader('Content-Type',mime[path.extname(file)]||'application/octet-stream'); res.end(await fs.readFile(file));
    } catch { res.writeHead(404).end(); }
  });
  await new Promise(resolve => server.listen(0,'127.0.0.1',resolve));
  let browser;
  try {
    const catalog = JSON.parse(await fs.readFile(path.join(root,'x20ctl/controllers/catalog.json'),'utf8'));
    const models=flags.has('--models') ? catalog.filter(m=>flags.get('--models').split(',').includes(m.id)) : catalog;
    assert.ok(models.length,'review must include a known model');
    for (const asset of JSON.parse(await fs.readFile(path.join(root,'artifacts/controller-shell-fit/trace-report.json'),'utf8')))
      assert.equal(createHash('sha256').update(await fs.readFile(path.join(root,asset.source))).digest('hex'),asset.sha256);
    report.originalPhotosUnchanged = true;
    browser = await chromium.launch({ headless:true, executablePath:flags.get('--browser'),args:flags.has('--disable-webgl')?['--disable-webgl']:[] });
    for (const model of models) {
      const context = await browser.newContext({ viewport:{width:1400,height:940}, reducedMotion:'reduce',deviceScaleFactor:Number(flags.get('--dpr')||1) });
      const page = await context.newPage(); const errors=[]; page.on('pageerror',e=>errors.push(e.message));
      await page.addInitScript(() => {
        window.reviewInput = { connected:false, left:0, right:0 }; window.reviewCalls=[];
        window.pywebview={api:{request:async (operation,payload={}) => {
          window.reviewCalls.push(operation);
          if (!['select_model','disconnect','bootstrap','input'].includes(operation)) throw new Error('Unexpected '+operation);
          return {ok:true,data:operation==='input'?{connected:window.reviewInput.connected,input:window.reviewInput.connected?{slot:0,buttons:[],leftStick:{x:0,y:0},rightStick:{x:0,y:0},leftTrigger:window.reviewInput.left,rightTrigger:window.reviewInput.right}:null}:operation==='bootstrap'?{version:'4.0.1',profiles:[],updatesEnabled:false}:operation==='select_model'?{model:payload.model}:{}};
        }}};
      });
      await page.goto(`http://127.0.0.1:${server.address().port}/`);
      await page.getByRole('button',{name:'Add controller for Player 2',exact:true}).click();
      await page.getByRole('dialog').getByRole('button',{name:`Select ${model.name}`,exact:true}).click();
      await page.getByRole('button',{name:'Enter Studio for Player 2',exact:true}).click();
      const studio = model.id==='x20'?page.locator('.rebranded-workspace').filter({has:page.locator('.editor')}):page.locator(`.model-workspace[data-model="${model.id}"]`);
      const stage = studio.locator('.mapping-controller-stage');
      const entry = {model:model.id,poses:[],sizes:[]};
      assert.equal(await stage.locator('.controller-static-rim').count(),0);
      const sweep = stage.locator('.controller-silhouette-overlay');
      assert.match(await sweep.getAttribute('style'),/clip-path: url/);
      // Show the traveling packet at the reported orange shoulder position.
      await stage.locator('.controller-rim-sweep').evaluate(e => {e.style.animation='none';e.style.strokeDashoffset='10';});
      await stage.locator('.controller-canvas').screenshot({path:path.join(out,`${model.id}-front-rim.png`)});
      const canvas = stage.locator('.controller-canvas');
      const mask = await canvas.evaluate(e => {
        const r=e.getBoundingClientRect(),p=e.querySelector('.controller-image-plane').getBoundingClientRect();
        const mask=document.createElement('canvas');mask.width=Math.ceil(r.width*devicePixelRatio);mask.height=Math.ceil(r.height*devicePixelRatio);
        const ctx=mask.getContext('2d');ctx.fillStyle='black';ctx.fillRect(0,0,mask.width,mask.height);
        ctx.scale(devicePixelRatio,devicePixelRatio);
        ctx.translate(p.x-r.x,p.y-r.y);ctx.scale(p.width/1536,p.height/1024);ctx.fillStyle='white';
        ctx.fill(new Path2D(e.querySelector('.controller-rim-sweep').getAttribute('d')));
        return mask.toDataURL('image/png').split(',')[1];
      });
      await fs.writeFile(path.join(out,`${model.id}-shell-mask.png`),Buffer.from(mask,'base64'));
      await stage.locator('.controller-rim-sweep').evaluate(e=>{e.style.opacity='1';e.style.stroke='#ff8800';e.style.strokeWidth='4px';});
      await canvas.screenshot({path:path.join(out,`${model.id}-rim-stress-on.png`)});
      await sweep.evaluate(e=>{e.style.visibility='hidden';});
      await canvas.screenshot({path:path.join(out,`${model.id}-rim-stress-off.png`)});
      await studio.getByRole('button',{name:'Back View',exact:true}).click();
      const rear = stage.locator('.controller-rear');
      assert.equal(await rear.locator('canvas.controller-trigger-volume').count(),1,'a photographic sheet must be replaced by a solid 3D cap renderer');
      if(flags.has('--disable-webgl')) {
        await page.waitForFunction(()=>document.querySelector('.controller-trigger-volume')?.dataset.status==='unavailable');
        assert.equal(await rear.locator('.controller-trigger-cap image').count(),2,'original photo must remain when GPU rendering is unavailable');
        await studio.getByRole('button',{name:'Keyboard preview',exact:true}).click();
        await studio.getByRole('slider',{name:'LT travel preview',exact:true}).evaluate(e=>{
          Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,'100');
          e.dispatchEvent(new Event('input',{bubbles:true}));
        });
        assert.deepEqual(await rear.locator('.controller-trigger-cap').evaluateAll(es=>es.map(e=>e.style.transform)),['none','none'],'fallback must not flatten or distort the photo');
        await rear.screenshot({path:path.join(out,`${model.id}-static-fallback.png`)});
        assert.deepEqual(errors,[]);report.models.push({model:model.id,staticFallback:true});await context.close();continue;
      }
      await page.waitForFunction(()=>{const c=document.querySelector('.mapping-controller-stage .controller-trigger-volume');return c?.dataset.status==='ready' && c.dataset.ltRendered==='0' && c.dataset.rtRendered==='0';});
      assert.equal(await rear.locator('.controller-trigger-cap image').count(),0,'the GPU cap must not sit on a second photographic cap');
      assert.equal(await rear.locator('.controller-trigger-cap').count(),2);
      assert.equal(await rear.locator('.controller-trigger-aperture[clip-path]').count(),2);
      assert.equal(await rear.locator('.controller-trigger-aperture .controller-trigger-cap').count(),0,'perspective cap cannot be flattened inside the socket SVG');
      await rear.locator('.controller-rear-plane > img').first().evaluate(async image=>{await image.decode();});
      await page.waitForFunction(() => [...document.querySelectorAll('.controller-trigger-layers image')].every(e=>e.href.baseVal));
      await rear.screenshot({path:path.join(out,`${model.id}-released.png`)});
      const originalPhotoStyles = await rear.evaluate(e=>({
        primary:e.querySelector('.controller-rear-plane > img').getAttribute('style'),
        ambient:e.querySelector('.controller-photo-ambient').getAttribute('style'),
      }));
      await rear.evaluate(e=>{
        const shellId=e.querySelector('clipPath[id$="-shell"]').id;
        e.querySelector('.controller-rear-plane > img').style.mask='none';
        e.querySelector('.controller-rear-plane > img').style.clipPath=`url(#${shellId})`;
        e.querySelector('.controller-photo-ambient').style.mask='none';
        e.querySelector('.controller-photo-ambient').style.clipPath='none';
        e.querySelectorAll('.controller-trigger-layers,.controller-trigger-volume').forEach(layer=>{layer.style.visibility='hidden';});
      });
      await rear.screenshot({path:path.join(out,`${model.id}-reference.png`)});
      await rear.evaluate((e,styles)=>{
        e.querySelector('.controller-rear-plane > img').setAttribute('style',styles.primary||'');
        e.querySelector('.controller-photo-ambient').setAttribute('style',styles.ambient||'');
        e.querySelectorAll('.controller-trigger-layers,.controller-trigger-volume').forEach(layer=>{layer.style.visibility='visible';});
      },originalPhotoStyles);
      const rest = await rear.locator('.controller-trigger-cap .controller-trigger-feedback').evaluateAll(es=>es.map(e=>e.getAttribute('d')));
      await studio.getByRole('button',{name:'Keyboard preview',exact:true}).click();
      const setTrigger = async (key,percent) => {
        await studio.getByRole('slider',{name:`${key} travel preview`,exact:true}).evaluate((e,value)=>{
          Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,String(value));
          e.dispatchEvent(new Event('input',{bubbles:true}));
        },percent);
        await page.waitForFunction(({key,value})=>document.querySelector(`.mapping-controller-stage [data-trigger="${key}"]`)?.dataset.triggerValue===value,
          {key,value:(percent/100).toFixed(3)});
        await page.waitForFunction(({key,value})=>Number(document.querySelector('.mapping-controller-stage .controller-trigger-volume')?.dataset[`${key.toLowerCase()}Rendered`])===value,{key,value:percent/100});
      };
      // Use the independently positioned physical hit targets, never raw sprite
      // path bounds: a missing LT mirror transform must not move our test ROI.
      const regions = await rear.evaluate(e=>{
        const r=e.getBoundingClientRect();
        return Object.fromEntries(['LT','RT'].map(key=>{
          const target=e.querySelector(`.is-moving-trigger[aria-label="Select ${key}"]`);
          if(!target) throw new Error(`Missing ${key} physical target`);
          const b=target.getBoundingClientRect();
          return [key,{x:(b.x-r.x)*devicePixelRatio,y:(b.y-r.y)*devicePixelRatio,width:b.width*devicePixelRatio,height:b.height*devicePixelRatio}];
        }));
      });
      geometryReport.models.push({model:model.id,regions});
      await rear.locator('.controller-trigger-label').evaluateAll(labels=>labels.forEach(e=>{e.style.visibility='hidden';}));
      await setTrigger('LT',0); await setTrigger('RT',0);
      await rear.screenshot({path:path.join(out,`${model.id}-independent-rest.png`)});
      for(const key of ['LT','RT']) {
        await setTrigger(key,100);
        await rear.screenshot({path:path.join(out,`${model.id}-${key}-only.png`)});
        await setTrigger(key,0);
      }
      await rear.locator('.controller-trigger-label').evaluateAll(labels=>labels.forEach(e=>{e.style.visibility='';}));
      for (const percent of [23,50,100,0]) {
        for (const key of ['LT','RT']) {
          await setTrigger(key,percent);
        }
        await page.waitForFunction(value=>[...document.querySelectorAll('.mapping-controller-stage [data-trigger-value]')].every(e=>Number(e.dataset.triggerValue)===value),percent/100);
        const transforms = await rear.locator('.controller-trigger-cap .controller-trigger-feedback').evaluateAll(es=>es.map(e=>e.getAttribute('d')));
        assert.equal(JSON.stringify(transforms)===JSON.stringify(rest),percent===0);
        for (const transform of transforms) assert.ok(transform && !/NaN|Infinity/.test(transform));
        const labels = await rear.locator('.controller-trigger-label').evaluateAll(es=>es.map(e=>e.getAttribute('opacity')));
        assert.deepEqual(labels,[percent===0?'0':'1',percent===0?'0':'1']);
        if(percent === 100) {
          entry.solid = await rear.locator('.controller-trigger-volume').evaluate(c=>{
            const gl=c.getContext('webgl2');
            return {status:c.dataset.status,glError:gl.getError(),depth:[Number(c.dataset.ltDepth),Number(c.dataset.rtDepth)],
              walls:[Number(c.dataset.ltWalls),Number(c.dataset.rtWalls)],rendered:[Number(c.dataset.ltRendered),Number(c.dataset.rtRendered)]};
          });
          assert.equal(entry.solid.glError,0);assert.ok(entry.solid.depth.every(d=>d>=24));
          assert.ok(entry.solid.walls.every(n=>n>100));assert.deepEqual(entry.solid.rendered,[1,1]);
        }
        entry.poses.push({percent,transforms});
        if([50,100].includes(percent)) await rear.screenshot({path:path.join(out,`${model.id}-${percent}.png`)});
      }
      if (model.id==='x20') {
        // Each trigger gets its own rendered press/release cycle.
        for (const key of ['LT','RT']) {
          for (let i=0;i<9;i++) {
            await setTrigger(key,[0,25,50,75,100,75,50,25,0][i]);
            await rear.screenshot({path:path.join(out,`motion-${key}-${i}.png`)});
          }
        }
      }
      await studio.getByRole('button',{name:'Keyboard preview',exact:true}).click();
      assert.equal(await stage.locator('[data-trigger="RT"]').getAttribute('data-trigger-value'),'0.000');
      if (model.id==='x20') {
        await page.evaluate(()=>{window.reviewInput={connected:true,left:.23,right:.73};});
        await page.waitForFunction(()=>document.querySelector('.mapping-controller-stage [data-trigger="RT"]')?.dataset.triggerValue==='0.730');
        assert.equal(await stage.locator('[data-trigger="LT"]').getAttribute('data-trigger-value'),'0.230');
        await page.evaluate(()=>{window.reviewInput={connected:false,left:0,right:0};});
        await page.waitForFunction(()=>document.querySelector('.mapping-controller-stage [data-trigger="RT"]')?.dataset.triggerValue==='0.000');
        entry.fixtureLiveInputAndDisconnect = true;
      }
      await studio.getByRole('button',{name:'Keyboard preview',exact:true}).click();
      await setTrigger('LT',73); await setTrigger('RT',73);
      for (const width of [1400,800]) {
        await page.setViewportSize({width,height:940});
        const fit = await rear.evaluate(e=>{const r=e.getBoundingClientRect();const p=e.querySelector('.controller-rear-plane').getBoundingClientRect();const s=e.querySelector('.controller-trigger-layers').getBoundingClientRect();return {width:r.width,plane:p.width,sprite:s.width,overflow:document.documentElement.scrollWidth-innerWidth};});
        assert.ok(Math.abs(fit.plane-fit.sprite)<.1); assert.ok(fit.overflow<=2);
        fit.raster=await rear.locator('.controller-trigger-volume').evaluate(c=>({cssWidth:c.clientWidth,width:c.width,height:c.height,scale:Math.min(devicePixelRatio||1,2)}));
        await page.waitForFunction(()=>{const c=document.querySelector('.mapping-controller-stage .controller-trigger-volume');return Math.abs(c.width-c.clientWidth*Math.min(devicePixelRatio||1,2))<=2;});
        assert.ok(Math.abs(fit.raster.cssWidth-fit.plane)<2,'3D canvas must use the controller image plane');
        entry.sizes.push({width,...fit});
        await rear.screenshot({path:path.join(out,`${model.id}-resize-${width}.png`)});
      }
      assert.deepEqual(errors,[]);
      assert.ok(await page.evaluate(()=>window.reviewCalls.every(op=>!['scan','connect','apply'].includes(op))));
      report.models.push(entry); await context.close();
    }
    report.passed=true;
  } catch(error) {report.error=error.stack||String(error);process.exitCode=1;}
  finally {
    await fs.writeFile(path.join(out,'report.json'),JSON.stringify(report,null,2));
    await fs.writeFile(path.join(out,'geometry.json'),JSON.stringify(geometryReport,null,2));
    await browser?.close();await new Promise(resolve=>server.close(resolve));
  }
  console.log(JSON.stringify({passed:report.passed,models:report.models.length,error:report.error||null}));
})();
