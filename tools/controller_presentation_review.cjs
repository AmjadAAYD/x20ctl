/* Headless production review: shared presentation only, mock native bridge. */
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const http = require('node:http');
const { createHash } = require('node:crypto');
const flags = new Map(process.argv.slice(2).reduce((pairs, value, i, args) => i % 2 ? pairs : [...pairs, [value, args[i + 1]]], []));
const { chromium } = require(flags.get('--playwright') || 'playwright');
const root = path.resolve(__dirname, '..');
const dist = path.join(root, 'dist-ui');
const out = path.join(root, 'artifacts/controller-presentation-review');
const report = { passed: false, mode: 'headless production frontend; no host screen or hardware', models: [] };
const mime = { '.html': 'text/html', '.js': 'application/javascript', '.css': 'text/css', '.png': 'image/png', '.svg': 'image/svg+xml', '.ttf': 'font/ttf' };

(async () => {
  await fs.mkdir(out, { recursive: true });
  const server = http.createServer(async (req, res) => {
    try {
      const url = new URL(req.url, 'http://127.0.0.1');
      const file = path.resolve(dist, '.' + decodeURIComponent(url.pathname === '/' ? '/index.html' : url.pathname));
      if (!file.startsWith(dist + path.sep)) { res.writeHead(403).end(); return; }
      res.setHeader('Content-Type', mime[path.extname(file)] || 'application/octet-stream');
      res.end(await fs.readFile(file));
    } catch { res.writeHead(404).end(); }
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  let browser;
  try {
    const catalog = JSON.parse(await fs.readFile(path.join(root, 'x20ctl/controllers/catalog.json'), 'utf8'));
    const lighting = JSON.parse(await fs.readFile(path.join(root, 'src/assets/controllers/lighting.json'), 'utf8'));
    const original = JSON.parse(await fs.readFile(path.join(root, 'artifacts/rear-controller-sources/front-assets-before.json'), 'utf8'));
    for (const asset of original) assert.equal(createHash('sha256').update(await fs.readFile(path.join(root, asset.path))).digest('hex'), asset.sha256);
    report.originalFrontPhotosUnchanged = true;
    browser = await chromium.launch({ headless: true, executablePath: flags.get('--browser') });
    for (const model of catalog) {
      const context = await browser.newContext({ viewport: { width: 1400, height: 940 }, reducedMotion: 'reduce' });
      const page = await context.newPage();
      const errors = [];
      page.on('pageerror', error => errors.push(error.message));
      await page.addInitScript(() => {
        window.presentationCalls = [];
        window.pywebview = { api: { request: async (operation, payload) => {
          window.presentationCalls.push(operation);
          if (!['select_model', 'disconnect', 'bootstrap', 'input'].includes(operation)) throw new Error('Unexpected native operation: ' + operation);
          return { ok: true, data: operation === 'input' ? { connected: false, input: null } : operation === 'bootstrap' ? { version: '4.0.1', profiles: [], warning: null, updatesEnabled: false } : operation === 'select_model' ? { model: payload.model } : {} };
        } } };
      });
      await page.goto(`http://127.0.0.1:${server.address().port}/`);
      await page.getByRole('button', { name: 'Add controller for Player 2', exact: true }).click();
      const picker = page.getByRole('dialog');
      const row = picker.locator(`.controller-switch-row:has(.controller-canvas[data-model="${model.id}"])`);
      const art = row.locator('.controller-canvas');
      assert.equal(await picker.locator('.controller-selection-grid').evaluate(e => getComputedStyle(e).gridTemplateColumns.split(' ').length), 2);
      assert.equal(await picker.getByRole('button', { name: 'Connect', exact: true }).count(), 1);
      assert.equal(await picker.getByRole('button', { name: 'Open preview', exact: true }).count(), 5);
      await page.mouse.move(0, 0);
      const initialAmbient = await art.locator('.controller-photo-ambient').evaluate(e => parseFloat(getComputedStyle(e).opacity));
      assert.ok(initialAmbient <= (model.placeholder ? .04 : .23));
      assert.equal(await art.locator('.controller-photo:not(.controller-photo-ambient)').evaluate(e => getComputedStyle(e).opacity), '1');
      await row.hover();
      const hoverAmbient = await art.locator('.controller-photo-ambient').evaluate(e => parseFloat(getComputedStyle(e).opacity));
      assert.ok(hoverAmbient > initialAmbient);
      assert.ok(hoverAmbient <= (model.placeholder ? .1 : .56));
      await page.setViewportSize({ width: 680, height: 800 });
      assert.equal(await picker.locator('.controller-selection-grid').evaluate(e => getComputedStyle(e).gridTemplateColumns.split(' ').length), 1);
      assert.ok((await picker.boundingBox()).width <= 680);
      await page.setViewportSize({ width: 1400, height: 940 });
      if (model.id === 'x20') {
        await row.getByRole('button', { name: 'Select X20', exact: true }).click();
        await page.getByRole('button', { name: 'Choose controller for Player 2', exact: true }).click();
        assert.equal(await picker.locator('.controller-canvas[data-emphasis="selected"]').evaluate(e => parseFloat(getComputedStyle(e).getPropertyValue('--art-ambient'))), 1);
        await picker.screenshot({ path: path.join(out, 'picker-grid.png') });
        await picker.getByRole('button', { name: 'Select X20', exact: true }).click();
        await page.getByRole('button', { name: 'Enter Studio for Player 2', exact: true }).click();
      } else {
        await row.getByRole('button', { name: 'Open preview', exact: true }).click();
      }
      const studio = model.id === 'x20' ? page.locator('.rebranded-workspace').filter({ has: page.locator('.editor') }) : page.locator(`.model-workspace[data-model="${model.id}"]`);
      await studio.waitFor();
      await page.emulateMedia({ reducedMotion: 'no-preference' });
      await page.evaluate(() => document.documentElement.dataset.motionPaused = 'false');
      const canvas = studio.locator('.mapping-controller-stage .controller-canvas');
      assert.equal(await canvas.locator('.controller-rim-sweep').count(), 1);
      assert.equal(await canvas.locator('clipPath path').first().getAttribute('d'), lighting.models[model.id].front);
      const sweep = canvas.locator('.controller-rim-sweep');
      await canvas.scrollIntoViewIfNeeded();
      await page.evaluate(() => document.documentElement.dataset.motionPaused = 'false');
      const before = await sweep.evaluate(e => e.getAnimations()[0].currentTime);
      await page.waitForTimeout(150);
      const motion = await sweep.evaluate(e => ({ time: e.getAnimations()[0].currentTime, state: e.getAnimations()[0].playState, css: getComputedStyle(e).animationPlayState, paused: document.documentElement.dataset.motionPaused, reduced: matchMedia('(prefers-reduced-motion: reduce)').matches }));
      assert.ok(motion.time > before + 75, JSON.stringify({ before, ...motion }));
      assert.equal(await sweep.evaluate(e => getComputedStyle(e).animationDuration), '12s');
      await page.evaluate(() => document.documentElement.dataset.motionPaused = 'true');
      assert.equal(await sweep.evaluate(e => getComputedStyle(e).animationPlayState), 'paused');
      await page.evaluate(() => document.documentElement.dataset.motionPaused = 'false');
      const glass = studio.locator('.nav-active-light');
      assert.ok((await glass.evaluate(e => getComputedStyle(e).backdropFilter)).startsWith('blur('));
      await studio.locator('.chassis-nav').getByRole('button', { name: 'Buttons', exact: true }).focus();
      await page.keyboard.press('Tab');
      assert.ok(await page.evaluate(() => document.activeElement.closest('.chassis-nav') !== null));
      await studio.screenshot({ path: path.join(out, `${model.id}-buttons-glass.png`) });
      await studio.getByRole('button', { name: 'Back View', exact: true }).click();
      const rear = studio.locator('.mapping-controller-stage .controller-rear');
      assert.equal(await rear.locator('.controller-rim-sweep').count(), 0);
      assert.equal(await rear.locator('clipPath path').getAttribute('d'), lighting.models[model.id].back);
      assert.ok(await rear.locator('.controller-photo-ambient').evaluate(e => parseFloat(getComputedStyle(e).opacity)) <= .04);
      const entry = { model: model.id, initialAmbient, hoverAmbient, macroPreview: false };
      if (model.macroSlots.length) {
        await rear.getByRole('button', { name: `Open ${model.macroSlots[0]} macro`, exact: true }).click();
        const macro = studio.locator('.macro-studio');
        assert.equal(await macro.locator('.controller-canvas').count(), 0, 'Macros remain rear-only');
        assert.ok((await macro.locator('.macro-controller-preview').boundingBox()).width <= 511);
        await macro.getByRole('button', { name: 'Add first step', exact: true }).click();
        await macro.getByRole('spinbutton', { name: 'Step 1 hold', exact: true }).fill('1000');
        await macro.getByRole('spinbutton', { name: 'Step 1 pause', exact: true }).fill('250');
        await macro.getByRole('button', { name: 'Add Step', exact: true }).click();
        await macro.getByRole('spinbutton', { name: 'Step 2 hold', exact: true }).fill('1000');
        await macro.getByRole('spinbutton', { name: 'Step 2 pause', exact: true }).fill('0');
        await macro.getByRole('button', { name: 'A, step 2', exact: true }).click();
        await macro.getByRole('button', { name: 'X, step 2', exact: true }).click();
        await macro.getByRole('button', { name: 'Preview sequence', exact: true }).click();
        assert.equal(await macro.getAttribute('data-playing-index'), '0');
        assert.equal(await macro.locator('.macro-preview-inputs .is-active').innerText(), 'A');
        assert.equal(await macro.locator('.sequencer-step-heading.is-playing').count(), 1);
        await macro.screenshot({ path: path.join(out, `${model.id}-macro-local-preview.png`) });
        await page.waitForFunction(() => document.querySelector('.macro-studio').dataset.previewPhase === 'gap');
        assert.ok((await macro.locator('.macro-preview-inputs').innerText()).includes('Pause'));
        await page.waitForFunction(() => document.querySelector('.macro-studio').dataset.playingIndex === '1');
        assert.equal(await macro.locator('.macro-preview-inputs .is-active').innerText(), 'X');
        await page.waitForFunction(() => document.querySelector('.macro-studio').dataset.playingIndex === '-1');
        assert.equal(await macro.locator('.macro-preview-inputs').count(), 0);
        await macro.getByRole('button', { name: 'Preview sequence', exact: true }).click();
        await macro.getByRole('button', { name: 'Stop preview', exact: true }).click();
        assert.equal(await macro.getAttribute('data-playing-index'), '-1');
        await macro.getByRole('button', { name: 'Preview sequence', exact: true }).click();
        await macro.locator('.macro-paddle').nth(1).click();
        assert.equal(await macro.getAttribute('data-playing-index'), '-1', 'changing owner cancels playback');
        entry.macroPreview = true;
      } else assert.equal(await studio.locator('.chassis-nav').getByRole('button', { name: 'Macros', exact: true }).count(), 0);

      await studio.locator('.chassis-nav').getByRole('button', { name: 'Vibration', exact: true }).click();
      const vibration = studio.locator(model.id === 'x20' ? '.vibration-controller-art .controller-canvas' : '.model-haptics .controller-canvas');
      const values = [];
      for (const [name, period] of [['Gentle', 3.5], ['Standard', 1.6], ['Maximum', 1]]) {
        await studio.locator('.strength-presets button').filter({ hasText: name }).click();
        const actual = await vibration.locator('.motor-wave:not(.motor-wave-echo)').first().evaluate(e => parseFloat(getComputedStyle(e).animationDuration));
        assert.ok(Math.abs(actual - period) < .01);
        const opacity = await vibration.locator('.controller-haptics-overlay').evaluate(e => parseFloat(getComputedStyle(e).opacity));
        assert.ok(opacity >= .82 && opacity <= .97);
        values.push({ name, period: actual, opacity });
      }
      await page.setViewportSize({ width: 1060, height: 760 });
      await vibration.screenshot({ path: path.join(out, `${model.id}-vibration-quiet.png`) });
      await studio.locator('.strength-presets button').filter({ hasText: 'Off', exact: true }).click();
      assert.equal(await vibration.locator('.controller-haptics-overlay').count(), 0);
      assert.equal(await vibration.locator('.controller-static-rim').count(), 1);
      await studio.locator('.chassis-nav').getByRole('button', { name: 'Buttons', exact: true }).click();
      await page.emulateMedia({ reducedMotion: 'reduce' });
      assert.equal(await studio.locator('.controller-rim-sweep').evaluate(e => getComputedStyle(e).animationName), 'none');
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 2));
      assert.deepEqual(errors, []);
      entry.vibration = values;
      entry.operations = await page.evaluate(() => [...new Set(window.presentationCalls)]);
      if (model.placeholder) assert.deepEqual(entry.operations, ['select_model']);
      report.models.push(entry);
      await context.close();
    }
    report.passed = true;
  } catch (error) {
    report.error = error.stack || String(error); process.exitCode = 1;
  } finally {
    await fs.writeFile(path.join(out, 'report.json'), JSON.stringify(report, null, 2));
    await browser?.close(); await new Promise(resolve => server.close(resolve));
  }
  console.log(JSON.stringify({ passed: report.passed, models: report.models.length, error: report.error || null }));
})();
