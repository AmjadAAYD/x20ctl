/* Offscreen production-UI review. Never opens or captures a desktop window. */
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const http = require('node:http');
const { createHash } = require('node:crypto');
const flags = new Map(process.argv.slice(2).reduce((pairs, value, i, args) => i % 2 ? pairs : [...pairs, [value, args[i+1]]], []));
const { chromium } = require(flags.get('--playwright') || 'playwright');
const root = path.resolve(__dirname, '..');
const dist = path.join(root, 'dist-ui');
const out = path.resolve(root, flags.get('--output') || 'artifacts/preview-models-headless');
const models = [
  { id:'x05', name:'X05', player:1, macros:0, motors:2, rgb:true },
  { id:'x05_pro', name:'X05 Pro', player:2, macros:2, motors:4, rgb:true },
  { id:'x10', name:'X10', player:3, macros:2, motors:2, rgb:false },
  { id:'d10', name:'D10', player:4, macros:2, motors:2, rgb:true },
];
const mime = {'.html':'text/html','.js':'application/javascript','.css':'text/css','.png':'image/png','.jpg':'image/jpeg','.svg':'image/svg+xml','.ttf':'font/ttf'};
const report = { passed:false, mode:'headless Chrome; production assets; mocked desktop selection bridge', models:[], screenshots:[] };
const rectCenter = rect => ({x:rect.x+rect.w/2,y:rect.y+rect.h/2});
const assertNear = (actual, expected, label, tolerance=.1) => assert.ok(Math.abs(actual-expected)<tolerance,`${label}: ${actual} vs ${expected}`);

async function canvasMetrics(studio) {
  return studio.locator('.mapping-controller-stage .controller-canvas').evaluate(async canvas => {
    const rect = element => {
      const r=element.getBoundingClientRect();
      return {x:r.x,y:r.y,w:r.width,h:r.height};
    };
    const styles = element => {
      const s=getComputedStyle(element);
      return {opacity:s.opacity,boxShadow:s.boxShadow,filter:s.filter,backgroundImage:s.backgroundImage,transitionDuration:s.transitionDuration};
    };
    const photo=canvas.querySelector('.controller-photo');
    const caps=[...canvas.querySelectorAll('.controller-stick-cap')];
    // Confirm the complete-plate cap textures have decoded, not just the base img.
    for (const cap of caps) {
      const url=getComputedStyle(cap).backgroundImage.match(/^url\(["']?(.*?)["']?\)$/)?.[1];
      if (!url) throw new Error('Missing cap texture');
      const image=new Image(); image.src=url; await image.decode();
    }
    return {
      canvas:rect(canvas),plane:rect(canvas.querySelector('.controller-image-plane')),
      photo:{...rect(photo),loaded:photo.complete&&photo.naturalWidth>0,layer:photo.dataset.artLayer,src:photo.src,naturalWidth:photo.naturalWidth,naturalHeight:photo.naturalHeight},
      sticks:[...canvas.querySelectorAll('.controller-stick')].map(element => ({button:rect(element),cap:rect(element.querySelector('.controller-stick-cap')),styles:styles(element),capStyles:styles(element.querySelector('.controller-stick-cap'))})),
      controls:[...canvas.querySelectorAll('.controller-control')].map(styles),
      caps:caps.length,markers:canvas.querySelectorAll('.controller-stick-marker').length,
      scrollWidth:document.documentElement.scrollWidth,viewport:innerWidth,
    };
  });
}

function checkLayers(metrics, model) {
  assert.ok(metrics.photo.loaded,`${model.id} base image loaded`);
  assert.equal(metrics.photo.layer,'stickless-base',`${model.id} moving canvas uses edited stickless base`);
  assert.equal(metrics.caps,2,`${model.id} has two photographed caps`);
  assert.equal(metrics.markers,0,`${model.id} has no input-marker substitute`);
  assert.equal(metrics.sticks.length,2);
  for (const stick of metrics.sticks) {
    assert.equal(stick.capStyles.opacity,'1',`${model.id} opaque cap`);
    assert.notEqual(stick.capStyles.backgroundImage,'none');
    assert.ok(!stick.capStyles.backgroundImage.includes(metrics.photo.src),`${model.id} caps use complete plate rather than edited base`);
    // The shared reduced-motion rule reports 0.00001s in Chromium.
    assert.ok(stick.capStyles.transitionDuration.split(',').every(value => parseFloat(value)<=.001),`${model.id} cap motion is immediate`);
    for (const styles of [stick.styles,stick.capStyles]) {
      assert.equal(styles.boxShadow,'none',`${model.id} stick layers have no added glow`);
      assert.equal(styles.filter,'none',`${model.id} stick layers have no added filter`);
    }
  }
  for (const control of metrics.controls) {
    assert.equal(control.boxShadow,'none',`${model.id} control overlays have no added glow`);
    assert.equal(control.filter,'none',`${model.id} control overlays have no added filter`);
  }
}

function checkCenters(metrics, profile, label) {
  const offsets=[];
  for (const [index,side] of ['left','right'].entries()) {
    const point=profile.visual.sticks[side];
    assert.ok(point.cap,`${profile.id} ${side} has calibrated cap crop`);
    const expected={x:metrics.plane.x+point.x*metrics.plane.w,y:metrics.plane.y+point.y*metrics.plane.h};
    for (const layer of ['button','cap']) {
      const actual=rectCenter(metrics.sticks[index][layer]);
      assertNear(actual.x,expected.x,`${profile.id} ${side} ${layer} x center at ${label}`);
      assertNear(actual.y,expected.y,`${profile.id} ${side} ${layer} y center at ${label}`);
      offsets.push({side,layer,offsetCssPixels:{x:actual.x-expected.x,y:actual.y-expected.y}});
    }
  }
  return offsets;
}

async function movePreviewStick(studio, side, x) {
  const stick=studio.locator('.mapping-controller-stage .controller-stick').nth(side==='left'?0:1);
  await stick.evaluate((element,value) => {
    if (value===0) {
      element.dispatchEvent(new PointerEvent('pointercancel',{bubbles:true}));
      return;
    }
    const r=element.getBoundingClientRect();
    element.dispatchEvent(new PointerEvent('pointermove',{bubbles:true,buttons:1,clientX:r.x+r.width/2+r.width/2*value,clientY:r.y+r.height/2}));
  },x);
  await studio.locator('.mapping-controller-stage .controller-canvas').evaluate((canvas,{side,x}) => new Promise((resolve,reject) => {
    const deadline=performance.now()+5000;
    const check=() => {
      if (Math.abs(Number(canvas.dataset[side+'X'])-x)<.02) resolve();
      else if (performance.now()>deadline) reject(new Error(side+' stick did not update to '+x));
      else requestAnimationFrame(check);
    };
    check();
  }),{side,x});
}

(async () => {
  await fs.mkdir(out, {recursive:true});
  const server = http.createServer(async (req,res) => {
    try {
      const url = new URL(req.url, 'http://127.0.0.1');
      const file = path.resolve(dist, '.' + decodeURIComponent(url.pathname === '/' ? '/index.html' : url.pathname));
      if (!file.startsWith(dist + path.sep)) { res.writeHead(403); res.end(); return; }
      res.setHeader('Content-Type', mime[path.extname(file)] || 'application/octet-stream');
      res.end(await fs.readFile(file));
    } catch { res.writeHead(404); res.end(); }
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  let browser;
  try {
    const catalog=JSON.parse(await fs.readFile(path.join(root,'x20ctl/controllers/catalog.json'),'utf8'));
    const protectedSnapshot=JSON.parse((await fs.readFile(path.join(root,'artifacts/front-controller-sources/protected-before.json'),'utf8')).replace(/^\uFEFF/,''));
    assert.deepEqual(catalog.slice(0,2),protectedSnapshot.registry,'Protected X20/X20 Pro registry objects must be unchanged');
    for (const asset of protectedSnapshot.assets) {
      const digest=createHash('sha256').update(await fs.readFile(path.join(root,asset.path))).digest('hex');
      assert.equal(digest,asset.sha256,`Protected asset unchanged: ${asset.path}`);
    }
    report.protectedAssetsUnchanged=true;
    browser = await chromium.launch({headless:true, executablePath:flags.get('--browser')});
    const context = await browser.newContext({viewport:{width:1400,height:940},reducedMotion:'reduce'});
    const page = await context.newPage();
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    await page.addInitScript(() => {
      window.previewNativeCalls = [];
      window.pywebview = {api:{request:async (operation,payload) => {
        window.previewNativeCalls.push({operation,payload});
        if (!['select_model','disconnect'].includes(operation)) throw new Error('Unexpected native operation: '+operation);
        return {ok:true,data:operation === 'select_model' ? {model:payload.model} : {}};
      }}};
    });
    await page.goto(`http://127.0.0.1:${server.address().port}/`);
    await page.locator('.controller-card').first().waitFor();
    assert.equal(await page.locator('.controller-card').count(), 4);
    assert.equal(await page.locator('.model-workspace').count(), 0);
    for (const model of models) {
      await page.getByRole('button',{name:`Add controller for Player ${model.player}`,exact:true}).click();
      const picker = page.getByRole('dialog');
      assert.equal(await picker.locator('.controller-switch-row').count(),6);
      assert.equal(await picker.getByRole('button',{name:'Connect',exact:true}).count(),1);
      assert.equal(await picker.getByRole('button',{name:'Open preview',exact:true}).count(),5);
      assert.equal(await picker.locator('.controller-switch-row > button:last-child:disabled').count(),0);
      await picker.getByRole('button',{name:`Select ${model.name}`,exact:true}).click();
      assert.equal(await page.locator(`.controller-card[data-player="${model.player}"]`).getAttribute('data-controller'), model.id);
    }
    await page.screenshot({path:path.join(out,'four-player-preview-assignments.png')});
    report.screenshots.push('four-player-preview-assignments.png');
    for (const model of models) {
      const profile=catalog.find(item=>item.id===model.id);
      assert.ok(profile.visual.baseAsset,`${model.id} edited stickless base is registered`);
      await page.getByRole('button',{name:`Enter Studio for Player ${model.player}`,exact:true}).click();
      const studio = page.locator(`.model-workspace[data-model="${model.id}"]`);
      await studio.waitFor();
      assert.ok((await studio.locator('.header-controller-copy').innerText()).includes(`Player ${model.player}`));
      assert.equal(await studio.getByRole('button',{name:'Connect controller',exact:true}).isDisabled(),true);
      assert.equal(await studio.getByRole('button',{name:'Apply to controller',exact:true}).isDisabled(),true);
      assert.equal(await studio.locator('.mapping-controller-stage .controller-stick-cap').count(),2);
      assert.equal(await studio.locator('.chassis-nav').getByRole('button',{name:'Macros',exact:true}).count(),model.macros ? 1 : 0);
      assert.equal(await studio.locator('.chassis-nav').getByRole('button',{name:'Lighting',exact:true}).count(),model.rgb ? 1 : 0);
      assert.equal(await studio.locator('.chassis-nav').getByRole('button',{name:'Display',exact:true}).count(),0);
      const setupName = `${model.name} local draft`;
      await studio.getByLabel('Active Setup',{exact:true}).fill(setupName);
      const geometry = [];
      for (const size of [{width:1400,height:940},{width:1060,height:760}]) {
        await page.setViewportSize(size);
        await page.evaluate(() => window.scrollTo(0,0));
        const boxes = await canvasMetrics(studio);
        checkLayers(boxes,model);
        const centering=checkCenters(boxes,profile,`${size.width}x${size.height}`);
        // Background padding may extend outside the frame; shell bounds fit
        // within it and are checked by controller_framing_review.cjs.
        assert.ok(boxes.scrollWidth <= boxes.viewport+1);
        geometry.push({size,...boxes,centering});
      }
      assert.ok(await studio.locator('.header-controller-art .controller-photo:not(.controller-photo-ambient)').evaluateAll(images => images.length>0&&images.every(image=>image.dataset.artLayer==='complete'&&image.complete&&image.naturalWidth>0)),`${model.id} static header retains complete controller`);
      await studio.getByRole('button',{name:'Keyboard preview',exact:true}).click();
      const neutral=await canvasMetrics(studio);
      await page.keyboard.down('w');
      await page.waitForFunction(id => document.querySelector(`.model-workspace[data-model="${id}"] .mapping-controller-stage .controller-canvas`).dataset.leftY==='-1.000',model.id);
      const keyboard=await canvasMetrics(studio);
      checkLayers(keyboard,model);
      assertNear(rectCenter(keyboard.sticks[0].cap).y-rectCenter(neutral.sticks[0].cap).y,-neutral.sticks[0].cap.h*.18,`${model.id} keyboard deflects photographed left cap`);
      await page.keyboard.up('w');
      await page.waitForFunction(id => document.querySelector(`.model-workspace[data-model="${id}"] .mapping-controller-stage .controller-canvas`).dataset.leftY==='0.000',model.id);
      checkCenters(await canvasMetrics(studio),profile,'after keyboard release');
      const deflections=[];
      for (const direction of [-1,1]) {
        await movePreviewStick(studio,'left',direction);
        await movePreviewStick(studio,'right',direction);
        const deflected=await canvasMetrics(studio);
        checkLayers(deflected,model);
        for (const [index,side] of ['left','right'].entries()) {
          const actual=rectCenter(deflected.sticks[index].cap),expected=rectCenter(neutral.sticks[index].cap);
          // DOM pointer coordinates round to pixels; the gate vector can be
          // slightly under one at compact image sizes.
          assertNear(actual.x-expected.x,direction*neutral.sticks[index].cap.w*.18,`${model.id} ${side} cap deflects with pointer`,.5);
          assertNear(actual.y,expected.y,`${model.id} ${side} cap stays level during horizontal deflection`,.5);
        }
        deflections.push({direction,sticks:deflected.sticks});
        await page.screenshot({path:path.join(out,`${model.id}-sticks-${direction<0?'left':'right'}-compact.png`)});
        report.screenshots.push(`${model.id}-sticks-${direction<0?'left':'right'}-compact.png`);
      }
      await movePreviewStick(studio,'left',0);
      await movePreviewStick(studio,'right',0);
      checkCenters(await canvasMetrics(studio),profile,'after pointer cancellation');
      await studio.locator('.mapping-controller-stage .controller-stick').first().click();
      checkLayers(await canvasMetrics(studio),model);
      await studio.locator('.mapping-controller-stage .controller-stick').evaluateAll(sticks=>sticks.forEach(stick=>stick.classList.add('is-pressed')));
      checkLayers(await canvasMetrics(studio),model);
      await studio.locator('.mapping-controller-stage .controller-stick').evaluateAll(sticks=>sticks.forEach(stick=>stick.classList.remove('is-pressed')));
      await studio.getByRole('button',{name:'Keyboard preview',exact:true}).click();
      await page.screenshot({path:path.join(out,`${model.id}-buttons-compact.png`)});
      report.screenshots.push(`${model.id}-buttons-compact.png`);
      await studio.getByRole('button',{name:'Support X20ctl',exact:true}).click();
      assert.equal(await page.getByRole('dialog',{name:'Support X20ctl',exact:true}).count(),1);
      await page.getByRole('button',{name:'Close dialog',exact:true}).click();
      if (model.macros) {
        await studio.locator('.chassis-nav').getByRole('button',{name:'Macros',exact:true}).click();
        assert.equal(await studio.locator('.macro-paddle').count(),2);
        if (model.id==='x05_pro') assert.ok((await studio.locator('.macro-eyebrow').innerText()).includes('TOP BUTTON'));
      }
      await studio.locator('.chassis-nav').getByRole('button',{name:'Vibration',exact:true}).click();
      await studio.getByRole('button',{name:'Maximum',exact:true}).click();
      assert.equal(await studio.locator('.controller-motor').count(),model.motors);
      const slider = studio.getByRole('slider',{name:'Vibration strength',exact:true});
      await slider.focus();
      await slider.press('Home');
      assert.equal(await slider.inputValue(),'0');
      assert.equal(await studio.locator('.controller-motor').count(),0);
      await slider.press('End');
      assert.equal(await slider.inputValue(),'100');
      await page.screenshot({path:path.join(out,`${model.id}-vibration-compact.png`)});
      report.screenshots.push(`${model.id}-vibration-compact.png`);
      await studio.getByRole('button',{name:'Switch Controller',exact:true}).click();
      await page.locator('.controller-card').first().waitFor();
      await page.getByRole('button',{name:`Choose controller for Player ${model.player}`,exact:true}).click();
      assert.equal(await page.getByRole('dialog').getByRole('button',{name:'Can’t find your controller?',exact:true}).isDisabled(),true);
      await page.getByRole('button',{name:'Close dialog',exact:true}).click();
      await page.getByRole('button',{name:`Enter Studio for Player ${model.player}`,exact:true}).click();
      assert.equal(await studio.getByLabel('Active Setup',{exact:true}).inputValue(),setupName);
      await studio.getByRole('button',{name:'Switch Controller',exact:true}).click();
      report.models.push({id:model.id,player:model.player,geometry,deflections,layeredCaps:2,opaqueCaps:true,sticklessBase:true,noAddedStickGlow:true,macros:model.macros,motors:model.motors,draftPreserved:true});
    }
    report.nativeCalls=await page.evaluate(() => window.previewNativeCalls);
    assert.ok(report.nativeCalls.every(call => ['select_model','disconnect'].includes(call.operation)));
    for (const model of models) assert.ok(report.nativeCalls.some(call => call.operation==='select_model'&&call.payload.model===model.id&&call.payload.player===model.player));
    assert.deepEqual(errors,[]);
    await page.reload();
    assert.equal(await page.locator('.controller-card[data-controller=""]').count(),4);
    report.passed=true;
  } catch (error) {
    report.error=error.stack;
    process.exitCode=1;
  } finally {
    await browser?.close();
    await new Promise(resolve => server.close(resolve));
    await fs.writeFile(path.join(out,'report.json'),JSON.stringify(report,null,2));
    console.log(JSON.stringify({passed:report.passed,models:report.models.length,screenshots:report.screenshots.length,error:report.error}));
  }
})();
