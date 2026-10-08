/* Isolated production GUI with fixture bridge. No user screen, devices or uploads. */
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const http = require('node:http');
const { chromium } = require('C:/Users/amjad/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '..');
const dist = path.join(root, 'dist-ui');
const out = path.join(root, 'artifacts/x15-scanner-review');
const report = { passed: false, mode: 'isolated fixture bridge; no hardware or Internet submissions', checks: [] };
(async () => {
  await fs.mkdir(out, { recursive: true });
  const server = http.createServer(async (req, res) => {
    try {
      const url = new URL(req.url, 'http://127.0.0.1');
      const file = path.resolve(dist, '.' + (url.pathname === '/' ? '/index.html' : decodeURIComponent(url.pathname)));
      if (!file.startsWith(dist + path.sep)) return res.writeHead(403).end();
      const types = { '.html': 'text/html', '.js': 'application/javascript', '.css': 'text/css', '.png': 'image/png', '.ttf': 'font/ttf', '.svg': 'image/svg+xml' };
      res.setHeader('Content-Type', types[path.extname(file)] || 'application/octet-stream');
      res.end(await fs.readFile(file));
    } catch { res.writeHead(404).end(); }
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const browser = await chromium.launch({ headless: true, executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe' });
  try {
    const context = await browser.newContext({ viewport: { width: 1400, height: 940 }, reducedMotion: 'reduce' });
    const page = await context.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => {
      window.fixtureCalls = [];
      let selectedPlayer = 1;
      let scan = { state: 'idle', prompt: null, receipt: null, coverage: {}, files: [] };
      window.pywebview = { api: { request: async (operation, payload) => {
        window.fixtureCalls.push({ operation, payload });
        let data = {};
        if (operation === 'select_model') { selectedPlayer = payload.player; data = { model: payload.model }; }
        else if (operation === 'input_discover') data = payload.phase === 'before' ? [] : [{ token: 'fixture-source', label: 'Fixture XInput source' }];
        else if (operation === 'input_attach') { if (payload.player !== selectedPlayer) throw Error('wrong player'); data = { connected: true }; }
        else if (operation === 'gameplay_input') { if (payload.player !== 2) throw Error('wrong input player'); data = { connected: true, source: 'Fixture XInput input', input: { slot: 0, source: 'Fixture XInput', buttons: ['A'], leftStick: { x: .25, y: -.4 }, rightStick: { x: 0, y: 0 }, leftTrigger: .5, rightTrigger: 0 } }; }
        else if (operation === 'input_evidence') data = { receivers: [], standardControls: [], independentRearInputs: 'unknown' };
        else if (operation === 'research_history') data = [];
        else if (operation === 'research_status') data = scan;
        else if (operation === 'research_start') {
          if (payload.consent !== true) throw Error('no consent');
          scan = { ...scan, state: 'waiting', reportId: 'f'.repeat(32), model: payload.model, prompt: { id: 'fixture-prompt', kind: 'continue', choices: [], text: 'Disconnect ONLY this controller, then continue.' } };
          data = scan;
        } else if (operation === 'research_answer') {
          scan = { ...scan, state: 'review', prompt: null, coverage: { triggers: { status: 'observed', reason: 'Fixture readings only' }, configuration: { status: 'unavailable', reason: 'No verified commands' } }, files: [{ path: 'device.json', size: 200 }, { path: 'input/neutral.jsonl', size: 1000 }] };
        } else if (operation === 'research_finish') {
          if (!payload.reviewed) throw Error('review missing');
          scan = { ...scan, state: 'submitting', sha256: 'a'.repeat(64), size: 1200 };
          setTimeout(() => { scan = { ...scan, state: 'upload_failed', error: 'Fixture receiver unavailable; ZIP retained.' }; }, 120);
        } else if (operation === 'research_cancel') scan = { state: 'cancelled', prompt: null, receipt: null, coverage: {}, files: [] };
        else if (operation === 'research_export') data = { saved: true };
        else if (operation === 'research_contact' || operation === 'research_inspect' || operation === 'research_open_folder') data = { opened: true };
        else if (operation === 'disconnect' || operation === 'input_detach') data = { connected: false };
        else if (operation === 'bootstrap') data = { version: '4.1.0-preview.1', profiles: [], warning: null, updatesEnabled: false };
        else if (operation === 'input') data = { connected: false, input: null };
        else if (operation === 'check_updates') data = null;
        else throw Error('Unexpected fixture operation: ' + operation);
        return { ok: true, data };
      } } };
    });
    await page.goto(`http://127.0.0.1:${server.address().port}/`);
    await page.getByRole('button', { name: 'Add controller for Player 2', exact: true }).click();
    await page.getByRole('button', { name: 'Select X15', exact: true }).click();
    await page.getByRole('button', { name: 'Enter Studio for Player 2', exact: true }).click();
    const studio = page.locator('.input-workspace[data-model="x15"]');
    await studio.waitFor();
    for (const label of ['Buttons', 'Response curves', 'Macros', 'Vibration', 'Power & device', 'Input tester', 'Saved setups']) assert.equal(await studio.getByRole('button', { name: label, exact: true }).count(), 1);
    for (const label of ['Response curves', 'Macros', 'Vibration', 'Saved setups']) {
      await studio.getByRole('button', { name: label, exact: true }).click();
      await page.getByRole('dialog').getByRole('button', { name: 'Scan later', exact: true }).click();
    }
    assert.equal(await studio.getByRole('button', { name: 'Apply to controller', exact: true }).count(), 0);
    await studio.getByRole('button', { name: 'Connect input', exact: true }).click();
    await page.getByRole('button', { name: 'Controller disconnected · continue', exact: true }).click();
    await page.getByRole('button', { name: 'Controller reconnected · find input', exact: true }).click();
    await page.getByRole('button', { name: 'Fixture XInput source · select', exact: true }).click();
    await studio.getByText('Gameplay input connected', { exact: true }).waitFor();
    assert.equal(await studio.locator('.controller-macro-hotspot').count(), 0);
    await studio.getByRole('button', { name: 'Input tester', exact: true }).click();
    await studio.getByText('Player 2 input connected', { exact: true }).waitFor();
    for (const viewport of [{ width: 1400, height: 940 }, { width: 1060, height: 760 }]) {
      await page.setViewportSize(viewport);
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 2));
      await page.screenshot({ path: path.join(out, `x15-input-${viewport.width}.png`) });
    }
    report.checks.push('X15 real fixture input targets Player 2; shared seven-section navigation; unsupported-feature popup; wide/compact layouts');
    await studio.getByRole('button', { name: 'Switch Controller', exact: true }).click();
    for (const name of ['D10', 'X05', 'X10']) {
      await page.getByRole('button', { name: 'Choose controller for Player 2', exact: true }).click();
      await page.getByRole('button', { name: `Select ${name}`, exact: true }).click();
      await page.getByRole('button', { name: 'Enter Studio for Player 2', exact: true }).click();
      const inputStudio = page.locator('.input-workspace:visible');
      assert.equal(await page.getByRole('dialog').count(), 0);
      await inputStudio.locator('summary').filter({hasText: 'Known / Missing'}).click();
      assert.equal(await inputStudio.getByRole('heading', {name: 'Known', exact:true}).count(), 1);
      assert.equal(await inputStudio.getByRole('heading', {name: 'Missing', exact:true}).count(), 1);
      await inputStudio.getByRole('button', { name: 'Connect input', exact: true }).click();
      await page.getByRole('button', { name: 'Controller disconnected · continue', exact: true }).click();
      await page.getByRole('button', { name: 'Controller reconnected · find input', exact: true }).click();
      await page.getByRole('button', { name: 'Fixture XInput source · select', exact: true }).click();
      await inputStudio.getByText('Gameplay input connected', { exact: true }).waitFor();
      await inputStudio.getByRole('button', { name: 'Input tester', exact: true }).click();
      await inputStudio.getByText('Player 2 input connected', { exact: true }).waitFor();
      assert.equal(await inputStudio.locator('.controller-macro-hotspot').count(), 0);
      await inputStudio.getByRole('button', { name: 'Macros', exact: true }).click();
      await page.getByRole('dialog').getByRole('button', { name: 'Scan later', exact: true }).click();
      for (const width of [1400,1060]) {
        await page.setViewportSize({width, height:940});
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 2));
        await page.screenshot({path:path.join(out,`${name.toLowerCase()}-phase1-${width}.png`)});
      }
      await inputStudio.getByRole('button', { name: 'Switch Controller', exact: true }).click();
      report.checks.push(`${name}: read-only Player 2 input; Known/Missing; unsupported macros guarded; wide/compact`);
    }
    for (const name of ['X05 Pro']) {
      await page.getByRole('button', { name: 'Add controller for Player 1', exact: true }).count() ? await page.getByRole('button', { name: 'Add controller for Player 1', exact: true }).click() : await page.getByRole('button', { name: 'Choose controller for Player 1', exact: true }).click();
      await page.getByRole('button', { name: `Select ${name}`, exact: true }).click();
      await page.getByRole('button', { name: 'Enter Studio for Player 1', exact: true }).click();
      const popup = page.getByRole('dialog');
      await popup.getByRole('button', { name: 'Scan later', exact: true }).click();
      assert.equal(await page.getByRole('dialog').count(), 0);
      const calls = await page.evaluate(() => window.fixtureCalls);
      assert.equal(calls.filter(call => call.operation === 'research_start').length, 0);
      await page.locator('.input-workspace:visible').getByRole('button', { name: 'Switch Controller', exact: true }).click();
      report.checks.push(`${name}: popup, Scan later leaves preview, no scan or upload`);
    }
    await page.getByRole('button', { name: 'Enter Studio for Player 1', exact: true }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Scan now', exact: true }).click();
    const scanner = page.getByRole('dialog');
    assert.equal(await scanner.getByRole('button', { name: 'Finish scan', exact: true }).count(), 0);
    await scanner.getByRole('button', { name: 'Start guided scan', exact: true }).click();
    await scanner.getByRole('button', { name: 'Continue', exact: true }).click();
    const finish = scanner.getByRole('button', { name: 'Finish scan', exact: true });
    await finish.waitFor();
    assert.equal(await finish.isDisabled(), true);
    await scanner.getByRole('checkbox', { name: 'I reviewed these files and approve the selected sharing scope.', exact: true }).check();
    await finish.click();
    await scanner.getByText('Website submission failed. Your ZIP is saved locally.', { exact: true }).waitFor();
    await scanner.getByRole('button', { name: 'Save result ZIP', exact: true }).click();
    await scanner.getByRole('button', { name: 'Send manually', exact: true }).click();
    await scanner.getByRole('button', { name: 'Email Amjad', exact: true }).click();
    await scanner.getByText('Email draft opened. Attach the ZIP and press Send.', { exact: true }).waitFor();
    await page.screenshot({ path: path.join(out, 'scanner-manual-fallback.png') });
    report.checks.push('Scan now upfront scope; guided prompt; Finish disabled before review; failed submission retains ZIP and opens manual email');
    await scanner.getByRole('button', { name: 'Close dialog', exact: true }).click();
    await page.locator('.input-workspace:visible').getByRole('button', { name: 'Switch Controller', exact: true }).click();
    for (const name of ['X20', 'X20 Pro']) {
      const add = page.getByRole('button', { name: 'Add controller for Player 3', exact: true });
      if (await add.count()) await add.click(); else await page.getByRole('button', { name: 'Choose controller for Player 3', exact: true }).click();
      await page.getByRole('button', { name: `Select ${name}`, exact: true }).click();
      await page.getByRole('button', { name: 'Enter Studio for Player 3', exact: true }).click();
      for (const viewport of [{ width: 1400, height: 940 }, { width: 1060, height: 760 }]) {
        await page.setViewportSize(viewport);
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 2), `${name} compact header does not overflow`);
      }
      await page.locator('.rebranded-workspace:visible').getByRole('button', { name: /Switch Controller/ }).click();
      report.checks.push(`${name}: existing Studio remains available with scanner entry at wide/compact sizes`);
    }
    assert.deepEqual(errors, []);
    report.calls = await page.evaluate(() => window.fixtureCalls);
    report.passed = true;
    await context.close();
  } catch (error) { report.error = error.stack || String(error); process.exitCode = 1; }
  finally { await browser.close(); await new Promise(resolve => server.close(resolve)); await fs.writeFile(path.join(out, 'report.json'), JSON.stringify(report, null, 2)); }
  console.log(JSON.stringify({ passed: report.passed, checks: report.checks, error: report.error || null }));
})();
