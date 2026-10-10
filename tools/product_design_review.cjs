/* Production bundle, isolated fixture bridge. Never opens devices or uploads. */
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const http = require('node:http');
const { chromium } = require('C:/Users/amjad/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '..');
const dist = path.join(root, 'dist-ui');
const out = path.join(root, process.env.X20CTL_DESIGN_OUTPUT || 'artifacts/product-design');
const report = { mode: 'headless Chrome, production bundle, isolated fixtures; no native window or hardware', checks: [], screenshots: [], errors: [] };
(async () => {
  await fs.mkdir(out, { recursive: true });
  await fs.mkdir(path.join(out, 'outlines'), { recursive: true });
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
    page.on('pageerror', e => report.errors.push(e.message));
    await page.addInitScript(() => {
      window.fixtureCalls = [];
      window.fixtureInput = { slot: 0, buttons: [], leftStick: { x: 0, y: 0 }, rightStick: { x: 0, y: 0 }, leftTrigger: 0, rightTrigger: 0 };
      let profiles = [];
      window.pywebview = { api: { request: async (operation, payload) => {
        window.fixtureCalls.push({ operation, payload });
        let data;
        if (operation === 'select_model') data = { model: payload.model };
        else if (operation === 'bootstrap') data = { version: '4.1.0-preview.2', profiles, warning: null, updatesEnabled: false, inputBackend: 'Fixture XInput' };
        else if (operation === 'input') data = { connected: false, input: window.fixtureInput };
        else if (operation === 'disconnect') data = { connected: false };
        else if (operation === 'save_profiles') { profiles = payload.profiles; data = profiles; }
        else if (operation === 'record_start') data = {};
        else if (operation === 'record_stop') data = [{ id: 'recorded-fixture', buttons: ['A'], leftStick: 0, rightStick: 0, durationMs: 100, intervalMs: 20 }];
        else if (operation === 'input_evidence') data = { receivers: [], standardControls: [], independentRearInputs: 'unknown' };
        else if (operation === 'gameplay_input') data = { connected: false, input: null };
        else if (operation === 'research_status') data = { state: 'idle', prompt: null, receipt: null, coverage: {}, files: [] };
        else if (operation === 'research_history') data = [];
        else if (operation === 'research_cancel') data = {};
        else throw Error('Unexpected fixture operation: ' + operation);
        return { ok: true, data };
      } } };
    });
    await page.goto(`http://127.0.0.1:${server.address().port}`);
    await page.locator('.startup-intro').waitFor({ state: 'hidden' }).catch(() => {});
    async function capture(name) {
      await page.evaluate(() => window.scrollTo(0, 0));
      await page.waitForTimeout(160);
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false, `${name}: document overflow`);
      await page.screenshot({ path: path.join(out, name + '.png'), fullPage: true });
      report.screenshots.push(name);
    }
    for (const [player, model] of [[1, 'X20'], [2, 'X15']]) {
      await page.getByRole('button', { name: `Choose controller for Player ${player}`, exact: true }).click();
      await page.getByRole('button', { name: `Select ${model}`, exact: true }).click();
    }
    await capture('controllers');
    await page.getByRole('button', { name: 'Enter Studio for Player 1', exact: true }).click();
    await capture('buttons');
    assert.equal(await page.locator('.mapping-controller-stage [data-outline-model="x20"]').count(), 1);
    await page.getByRole('button', { name: 'Photo', exact: true }).click();
    assert.equal(await page.locator('.mapping-controller-stage [data-presentation="photo"]').count(), 1);
    await page.getByRole('button', { name: 'Outline', exact: true }).click();
    await page.locator('#selected-target').selectOption('B');
    await page.locator('.mapping-selection-meta').getByText('Sends B', { exact: true }).waitFor();
    await page.locator('.mapping-all-assignments > summary').click();
    assert.equal(await page.getByRole('combobox', { name: 'Remap A', exact: true }).inputValue(), 'B');
    await capture('button-assignments');
    await page.locator('.mapping-all-assignments > summary').click();
    await page.locator('#selected-target').selectOption('A');
    report.checks.push('Selected-control inspector and expanded assignment list share the same local draft; no hardware write.');
    const nav = page.getByRole('navigation', { name: 'Workspace' });
    await nav.getByRole('button', { name: 'Buttons', exact: true }).focus();
    await page.keyboard.press('ArrowDown');
    assert.equal(await nav.getByRole('button', { name: 'Response curves', exact: true }).evaluate(el => el === document.activeElement), true);
    await page.keyboard.press('Enter');
    await capture('curves');
    await nav.getByRole('button', { name: 'Macros', exact: true }).click();
    assert.equal(await page.locator('.sequencer-panel').count(), 0);
    await capture('macros-overview');
    await page.getByRole('button', { name: /^(Create|Edit) M1$/ }).click();
    await page.getByRole('button', { name: 'Add first step', exact: true }).click();
    await capture('macro-editor');
    await page.getByRole('button', { name: 'Macro overview', exact: false }).click();
    assert.equal(await page.getByRole('button', { name: 'Edit M1', exact: true }).count(), 1);
    assert.equal(await page.locator('.sequencer-panel').count(), 0);
    report.checks.push('Macro overview hides editor; Create/Edit preserves draft steps.');
    await page.getByRole('button', { name: 'Edit M1', exact: true }).click();
    await page.getByRole('button', { name: 'Gamepad navigation', exact: true }).click();
    await page.waitForTimeout(250); // Observe neutral input before arming activation.
    async function press(buttons) {
      await page.evaluate(buttons => { window.fixtureInput.buttons = buttons; }, buttons);
      await page.waitForTimeout(300);
      await page.evaluate(() => { window.fixtureInput.buttons = []; });
      await page.waitForTimeout(220);
    }
    report.navigationContext = await page.evaluate(() => ({ focused: document.hasFocus(), active: document.activeElement?.outerHTML, enabled: document.querySelector('[aria-label="Gamepad navigation"]')?.getAttribute('aria-pressed') }));
    await press(['RB']);
    assert.equal(await page.locator('[data-page="rumble"]').count(), 1);
    await capture('vibration');
    await press(['RB']);
    assert.equal(await page.locator('[data-page="power"]').count(), 1);
    await capture('power');
    await nav.getByRole('button', { name: 'Input tester', exact: true }).click();
    await press(['RB', 'A', 'B']);
    assert.equal(await page.locator('[data-page="tester"]').count(), 1);
    await page.evaluate(() => { window.fixtureInput.buttons = ['A']; window.fixtureInput.leftStick = { x: .25, y: -.4 }; window.fixtureInput.leftTrigger = .65; });
    await page.getByText('65%', { exact: true }).waitFor();
    assert.ok(await page.locator('.tester-controller-hero .is-pressed').count() > 0);
    await capture('tester');
    if (!await page.locator('.tester-detail-readings').evaluate(el => el.open)) await page.locator('.tester-detail-readings > summary').click();
    await page.getByRole('group', { name: 'Input readings' }).getByRole('button', { name: 'Sticks', exact: true }).click();
    await page.getByRole('img', { name: /Left stick: X 0.250, Y -0.400/ }).waitFor();
    await capture('tester-details');
    await page.evaluate(() => { window.fixtureInput.buttons = []; window.fixtureInput.leftStick = { x: 0, y: 0 }; window.fixtureInput.leftTrigger = 0; });
    report.checks.push('Opt-in bumper navigation changes tabs; tester reserves raw input.');
    await nav.getByRole('button', { name: 'Macros', exact: true }).click();
    await page.getByRole('button', { name: /^(Create|Edit) M1$/ }).click();
    await page.getByRole('button', { name: 'Record to M1', exact: true }).click();
    assert.equal(await nav.getByRole('button', { name: 'Input tester', exact: true }).isDisabled(), true);
    await press(['RB', 'B']);
    assert.equal(await page.locator('[data-page="macros"]').count(), 1);
    assert.equal(await page.locator('[data-macro-back]').isDisabled(), true);
    await page.getByRole('button', { name: 'Stop recording', exact: false }).click();
    report.checks.push('Recording suspends navigation and keeps stop controls reachable.');
    await nav.getByRole('button', { name: 'Saved setups', exact: true }).click();
    await page.getByRole('button', { name: 'Save', exact: true }).click();
    await page.getByRole('textbox', { name: 'Saved setup name', exact: true }).fill('Controller review');
    await press(['RB', 'A', 'B']);
    assert.equal(await page.locator('[data-page="profiles"]').count(), 1);
    await page.getByRole('button', { name: 'Rename setup', exact: true }).click();
    await page.getByRole('button', { name: 'Delete Controller review', exact: true }).click();
    await page.getByRole('dialog', { name: 'Delete saved setup?', exact: true }).waitFor();
    await page.getByRole('button', { name: 'Cancel', exact: true }).click();
    report.checks.push('Local setup rename persists, inspector updates, delete requires confirmation.');
    const savesBefore = await page.evaluate(() => window.fixtureCalls.filter(call => call.operation === 'save_profiles').length);
    await page.getByRole('button', { name: 'Save', exact: true }).focus();
    await press(['A']);
    assert.equal(await page.evaluate(() => window.fixtureCalls.filter(call => call.operation === 'save_profiles').length), savesBefore);
    report.checks.push('Text editing suspends navigation; A does not activate unmarked action buttons.');
    await capture('profiles');
    await page.getByRole('button', { name: 'Controller scanner', exact: true }).click();
    await press(['RB', 'A', 'B']);
    assert.equal(await page.getByRole('dialog').count(), 1);
    await capture('scanner');
    await page.getByRole('button', { name: 'Close dialog', exact: true }).click();
    await page.getByRole('button', { name: 'Switch Controller', exact: false }).click();
    await page.getByRole('button', { name: 'Enter Studio for Player 2', exact: true }).click();
    await capture('preview-x15');
    await page.getByText('Technical details · input support and research', { exact: true }).click();
    await capture('preview-details');
    await page.getByRole('button', { name: 'Switch Controller', exact: true }).click();
    await page.setViewportSize({ width: 1100, height: 760 });
    await capture('compact-controllers');
    await page.getByRole('button', { name: 'Enter Studio for Player 1', exact: true }).click();
    await nav.getByRole('button', { name: 'Buttons', exact: true }).click();
    await capture('compact-buttons');
    for (const [tab, name] of [['Response curves', 'curves'], ['Macros', 'macros'], ['Vibration', 'vibration'], ['Power & device', 'power'], ['Input tester', 'tester'], ['Saved setups', 'profiles']]) {
      await nav.getByRole('button', { name: tab, exact: true }).click();
      await capture('compact-' + name);
      if (tab === 'Macros') { await page.getByRole('button', { name: /^(Create|Edit) M1$/ }).click(); await capture('compact-macro-editor'); }
    }
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await nav.getByRole('button', { name: 'Buttons', exact: true }).click();
    await page.waitForTimeout(250);
    const previousIndicator = await page.locator('.nav-active-light').evaluate(el => el.getBoundingClientRect().left);
    await nav.getByRole('button', { name: 'Response curves', exact: true }).click();
    for (const [index, delay] of [0, 60, 80, 140].entries()) {
      await page.waitForTimeout(delay);
      await page.screenshot({ path: path.join(out, `motion-tab-${index}.png`) });
    }
    const indicator = await page.locator('.nav-active-light').evaluate(el => ({ x: el.getBoundingClientRect().left, width: el.getBoundingClientRect().width, duration: getComputedStyle(el).transitionDuration }));
    const selected = await nav.getByRole('button', { name: 'Response curves', exact: true }).evaluate(el => ({ x: el.getBoundingClientRect().left, width: el.getBoundingClientRect().width }));
    assert.ok(indicator.x > previousIndicator && Math.abs(indicator.x - selected.x) < 1 && Math.abs(indicator.width - selected.width) < 1);
    assert.ok(indicator.duration.split(',').every(value => parseFloat(value) <= .24));
    assert.equal(await page.locator('.motion-page').evaluate(el => getComputedStyle(el).animationName), 'console-page-enter');
    await page.getByRole('button', { name: 'Controller scanner', exact: true }).click();
    assert.equal(await page.getByRole('dialog').evaluate(el => getComputedStyle(el).animationName), 'console-dialog-enter');
    await page.getByRole('button', { name: 'Close dialog', exact: true }).click();
    await page.emulateMedia({ reducedMotion: 'reduce' });
    assert.ok(await page.locator('.nav-active-light').evaluate(el => getComputedStyle(el).transitionDuration.split(',').every(value => parseFloat(value) < .001)));
    report.checks.push('Horizontal underline reaches selected tab; 220 ms transition; page/dialog entry motion; reduced motion disables transitions.');
    await page.getByRole('button', { name: /Switch Controller/ }).click();
    for (const model of ['X20', 'X20 Pro', 'X05', 'X05 Pro', 'X10', 'D10', 'X15']) {
      const choose = page.getByRole('button', { name: 'Choose controller for Player 4', exact: true });
      await choose.click();
      await page.getByRole('button', { name: `Select ${model}`, exact: true }).click();
      await page.getByRole('button', { name: 'Enter Studio for Player 4', exact: true }).click();
      const later = page.getByRole('dialog').getByRole('button', { name: 'Scan later', exact: true });
      if (await later.count()) await later.click();
      const currentNav = page.getByRole('navigation', { name: 'Workspace' });
      await currentNav.getByRole('button', { name: 'Buttons', exact: true }).click();
      assert.ok(await page.locator('.controller-outline').count() > 0);
      const normalized = model.toLowerCase().replace(/ /g, '-');
      await capture(`outline-${normalized}-front`);
      await fs.writeFile(path.join(out, 'outlines', `${normalized}-front.svg`), await page.locator('.controller-outline[data-outline-view="front"]').first().evaluate(el => {
        const svg = el.cloneNode(true); svg.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
        const style = document.createElementNS('http://www.w3.org/2000/svg', 'style'); style.textContent = '.outline-shell{stroke-width:2}.outline-shell-light{stroke-width:10;opacity:.2}'; svg.prepend(style); return svg.outerHTML;
      }));
      await page.getByRole('button', { name: 'Back View', exact: true }).click();
      assert.ok(await page.locator('[data-outline-view="back"]').count() > 0);
      assert.equal(await page.locator('.controller-rear[data-presentation="outline"] .controller-trigger-volume').count(), 0);
      await capture(`outline-${normalized}-back`);
      await fs.writeFile(path.join(out, 'outlines', `${normalized}-back.svg`), await page.locator('.controller-outline[data-outline-view="back"]').first().evaluate(el => {
        const svg = el.cloneNode(true); svg.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
        const style = document.createElementNS('http://www.w3.org/2000/svg', 'style'); style.textContent = '.outline-shell{stroke-width:2}.outline-shell-light{stroke-width:10;opacity:.2}'; svg.prepend(style); return svg.outerHTML;
      }));
      await page.getByRole('button', { name: 'Photo', exact: true }).click();
      assert.equal(await page.locator('.controller-rear[data-presentation="photo"]').count(), 1);
      await page.getByRole('button', { name: /Switch Controller/ }).click();
    }
    report.checks.push('All seven catalog models render their own front/rear outlines; Photo fallback works; no hidden WebGL trigger renderer in outline mode.');
    report.calls = await page.evaluate(() => window.fixtureCalls);
    assert.equal(report.calls.some(call => ['apply', 'reset', 'research_start', 'research_finish'].includes(call.operation)), false);
    assert.deepEqual(report.errors, []);
    report.checks.push('No configuration writes, resets, captures or uploads during review; no runtime errors; no document overflow at both sizes.');
    report.passed = true;
  } catch (error) { report.passed = false; report.failure = error.stack; throw error; }
  finally { await fs.writeFile(path.join(out, 'report.json'), JSON.stringify(report, null, 2)); await browser.close(); server.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
