/* Production UI review in an isolated headless browser. No host screen or devices. */
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const http = require('node:http');
const flags = new Map(process.argv.slice(2).reduce((out, value, index, args) => index % 2 ? out : [...out, [value, args[index + 1]]], []));
const { chromium } = require(flags.get('--playwright') || 'playwright');
const root = path.resolve(__dirname, '..');
const dist = path.join(root, 'dist-ui');
const out = path.join(root, 'artifacts/macro-library-x15');
const report = { passed: false, mode: 'isolated headless production frontend; fixture native bridge only', models: [] };
const mime = {'.html':'text/html', '.js':'application/javascript', '.css':'text/css', '.png':'image/png', '.svg':'image/svg+xml', '.ttf':'font/ttf'};
(async () => {
  await fs.mkdir(out, {recursive:true});
  const server = http.createServer(async (req,res) => {
    try {
      const url = new URL(req.url, 'http://127.0.0.1');
      const file = path.resolve(dist, '.'+decodeURIComponent(url.pathname==='/'?'/index.html':url.pathname));
      if (!file.startsWith(dist+path.sep)) {res.writeHead(403).end();return;}
      res.setHeader('Content-Type',mime[path.extname(file)] || 'application/octet-stream');res.end(await fs.readFile(file));
    } catch {res.writeHead(404).end();}
  });
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  let browser;
  try {
    browser=await chromium.launch({headless:true, executablePath:flags.get('--browser')});
    const catalog=JSON.parse(await fs.readFile(path.join(root,'x20ctl/controllers/catalog.json'),'utf8'));
    for (const model of catalog.filter(model=>model.macroSlots.length)) {
      const context=await browser.newContext({viewport:{width:1400,height:940},reducedMotion:'reduce'});
      const page=await context.newPage();const errors=[];page.on('pageerror',error=>errors.push(error.message));
      await page.addInitScript(() => {
        const rows=[{id:'fixture-recording',buttons:['B','RT'],leftStick:3,rightStick:1,durationMs:75,intervalMs:0}];
        const imported={format:'x20ctl-macro',version:1,name:'Imported fixture',sourceModel:'x20',loopMs:200,steps:rows};
        window.reviewCalls=[];window.reviewLibrary=[];window.reviewExports=[];
        window.pywebview={api:{request:async(operation,payload={}) => {
          window.reviewCalls.push({operation,payload});
          const success=data=>({ok:true,data});
          if(operation==='select_model')return success({model:payload.model});
          if(operation==='disconnect')return success({});
          if(operation==='bootstrap')return success({version:'4.0.1',profiles:[],updatesEnabled:false});
          if(operation==='input')return success({connected:false,input:{slot:0,buttons:[],leftStick:{x:0,y:0},rightStick:{x:0,y:0},leftTrigger:0,rightTrigger:0}});
          if(operation==='record_start')return success({recording:true});
          if(operation==='record_stop')return success(rows);
          if(operation==='macro_library') {
            if(payload.action==='save')window.reviewLibrary.push({...structuredClone(payload.entry),id:crypto.randomUUID()});
            if(payload.action==='delete')window.reviewLibrary=window.reviewLibrary.filter(entry=>entry.id!==payload.id);
            return success({entries:structuredClone(window.reviewLibrary),path:'Fixture app data/macro-library.json'});
          }
          if(operation==='macro_import')return success(imported);
          if(operation==='macro_export'){window.reviewExports.push(payload.entry);return success({saved:true});}
          throw new Error('Unexpected operation: '+operation);
        }}};
      });
      await page.goto(`http://127.0.0.1:${server.address().port}/`);
      assert.equal(await page.getByRole('button',{name:/Add controller for Player/}).count(),4);
      await page.getByRole('button',{name:'Add controller for Player 3',exact:true}).click();
      await page.getByRole('dialog').getByRole('button',{name:`Select ${model.name}`,exact:true}).click();
      await page.getByRole('button',{name:'Enter Studio for Player 3',exact:true}).click();
      const studio=model.id==='x20'?page.locator('.rebranded-workspace').filter({has:page.locator('.editor')}):page.locator(`.model-workspace[data-model="${model.id}"]`);
      await studio.getByRole('button',{name:'Macros',exact:true}).click();
      const slot=model.macroSlots.at(-1);
      await studio.locator('.macro-paddle').filter({hasText:slot}).click();
      await studio.getByRole('button',{name:'Add first step',exact:true}).click();
      await studio.getByLabel('Step 1 hold',{exact:true}).fill('80');
      await studio.getByLabel('Macro loop interval',{exact:true}).fill('100');
      await studio.getByRole('button',{name:'Library & copy',exact:true}).click();
      const dialog=page.getByRole('dialog',{name:'Macro library',exact:true});
      await dialog.getByLabel('Macro library name',{exact:true}).fill(`${model.name} fixture`);
      await dialog.getByRole('button',{name:'Save copy',exact:true}).click();
      await dialog.getByRole('status').filter({hasText:'Saved'}).waitFor();
      await dialog.getByRole('button',{name:'Export current',exact:true}).click();
      await dialog.getByRole('button',{name:'Import macro',exact:true}).click();
      await dialog.getByText('Imported fixture',{exact:true}).waitFor();
      const imported=dialog.locator('article').filter({hasText:'Imported fixture'});
      await imported.getByRole('button',{name:`Load into ${slot}`,exact:true}).click();
      await dialog.getByRole('status').filter({hasText:'Loaded'}).waitFor();
      await dialog.getByLabel('Copy macro destination',{exact:true}).selectOption(model.macroSlots[0]);
      await dialog.getByRole('button',{name:'Copy to slot',exact:true}).click();
      await dialog.getByRole('status').filter({hasText:'Copied'}).waitFor();
      await page.getByRole('button',{name:'Close macro library',exact:true}).click();
      assert.equal(await studio.getByLabel('Step 1 hold',{exact:true}).inputValue(),'75');
      assert.equal(await studio.getByLabel('Macro loop interval',{exact:true}).inputValue(),'200');
      assert.equal(await studio.getByRole('button',{name:'B, step 1',exact:true}).getAttribute('aria-pressed'),'true');
      await studio.locator('.macro-paddle').filter({hasText:model.macroSlots[0]}).click();
      assert.equal(await studio.getByLabel('Step 1 hold',{exact:true}).inputValue(),'75');
      assert.equal(await studio.getByLabel('Macro loop interval',{exact:true}).inputValue(),'200');
      await studio.getByRole('button',{name:'Library & copy',exact:true}).click();
      await dialog.getByLabel('Fixed macro pause',{exact:true}).fill('55');
      await dialog.getByRole('button',{name:'Set pauses',exact:true}).click();
      await dialog.getByRole('button',{name:'Remove pauses',exact:true}).click();
      await imported.getByRole('button',{name:'Delete Imported fixture',exact:true}).click();
      assert.equal(await dialog.getByText('Imported fixture',{exact:true}).count(),0);
      for (const viewport of [{width:1400,height:940},{width:800,height:800}]) {
        await page.setViewportSize(viewport);
        assert.ok(await dialog.evaluate(e=>e.scrollWidth<=e.clientWidth+2));
        assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+2));
        if(viewport.width===1400)await dialog.screenshot({path:path.join(out,`${model.id}-library.png`)});
      }
      await page.getByRole('button',{name:'Close macro library',exact:true}).click();
      assert.equal(await studio.getByLabel('Step 1 pause',{exact:true}).inputValue(),'0');
      if(model.id==='x20') {
        await studio.locator('.macro-paddle').filter({hasText:'M3'}).click();
        await studio.getByRole('button',{name:'Record to M3',exact:true}).click();
        await studio.locator('.macro-paddle').filter({hasText:'M2'}).click();
        await studio.getByRole('button',{name:'Stop recording → M3',exact:true}).click();
        await studio.locator('.macro-paddle').filter({hasText:'M3'}).click();
        assert.equal(await studio.getByLabel('Step 1 hold',{exact:true}).inputValue(),'75');
        assert.equal(await studio.getByRole('button',{name:'B, step 1',exact:true}).getAttribute('aria-pressed'),'true');
      }
      if(model.id==='x15') {
        await studio.getByRole('button',{name:'Buttons',exact:true}).click();
        const canvas=studio.locator('.mapping-controller-stage .controller-canvas');
        assert.equal(await canvas.locator('.controller-stick-cap').count(),2);
        const caps=await canvas.locator('.controller-stick-cap').evaluateAll(elements=>elements.map(e=>({opacity:getComputedStyle(e).opacity,filter:getComputedStyle(e).filter,shadow:getComputedStyle(e).boxShadow})));
        assert.ok(caps.every(cap=>cap.opacity==='1'&&cap.filter==='none'&&cap.shadow==='none'));
        await studio.getByRole('button',{name:'Vibration',exact:true}).click();
        await studio.getByRole('button',{name:'Maximum',exact:true}).click();
        assert.equal(await studio.locator('.model-haptics-body .controller-canvas').getAttribute('data-motor-power'),'100');
        assert.equal(await studio.locator('.controller-motor').count(),2);
        await studio.locator('.model-haptics-body').screenshot({path:path.join(out,'x15-vibration.png')});
        await studio.getByRole('button',{name:'Off',exact:true}).click();
        assert.equal(await studio.locator('.controller-motor').count(),0);
      }
      assert.deepEqual(errors,[]);
      const operations=await page.evaluate(()=>[...new Set(window.reviewCalls.map(call=>call.operation))]);
      assert.ok(!operations.includes('apply')&&!operations.includes('scan')&&!operations.includes('connect'));
      if(model.placeholder)assert.ok(operations.every(op=>['select_model','macro_library','macro_import','macro_export'].includes(op)));
      assert.equal(await page.evaluate(()=>window.reviewExports[0].steps[0].durationMs),80);
      report.models.push({id:model.id,player:3,slot,operations,library:true,copy:true,pauses:true,importExport:true,viewports:2});
      await context.close();
    }
    report.passed=true;
  } catch(error) {report.error=error.stack || String(error);process.exitCode=1;}
  finally {await fs.writeFile(path.join(out,'report.json'),JSON.stringify(report,null,2));await browser?.close();await new Promise(resolve=>server.close(resolve));}
  console.log(JSON.stringify({passed:report.passed,models:report.models.length,error:report.error||null}));
})();
