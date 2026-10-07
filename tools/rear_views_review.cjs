/* Isolated headless production preview. No screen capture or controller access. */
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const http = require('node:http');
const { createHash } = require('node:crypto');
const flags = new Map(process.argv.slice(2).reduce((pairs, value, index, args) => index % 2 ? pairs : [...pairs, [value, args[index + 1]]], []));
const { chromium } = require(flags.get('--playwright') || 'playwright');
const root = path.resolve(__dirname, '..');
const dist = path.join(root, 'dist-ui');
const out = path.join(root, 'artifacts/rear-views-review');
const report = { passed: false, mode: 'isolated headless Chromium, production UI, mocked bridge', models: [] };
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
    const frontSnapshot = JSON.parse(await fs.readFile(path.join(root, 'artifacts/rear-controller-sources/front-assets-before.json'), 'utf8'));
    for (const asset of frontSnapshot) {
      assert.equal(createHash('sha256').update(await fs.readFile(path.join(root, asset.path))).digest('hex'), asset.sha256, asset.path);
    }
    report.frontAssetsUnchanged = true;
    browser = await chromium.launch({ headless: true, executablePath: flags.get('--browser') });
    for (const [index, model] of catalog.entries()) {
      const context = await browser.newContext({ viewport: { width: 1400, height: 940 }, reducedMotion: 'reduce' });
      const page = await context.newPage();
      const errors = [];
      page.on('pageerror', error => errors.push(error.message));
      await page.addInitScript(() => {
        window.rearReviewCalls = [];
        window.pywebview = { api: { request: async (operation, payload) => {
          window.rearReviewCalls.push({ operation, payload });
          if (!['select_model', 'disconnect', 'bootstrap', 'input'].includes(operation)) throw new Error('Unexpected native operation: ' + operation);
          return { ok: true, data: operation === 'input' ? { connected: false, input: null } : operation === 'bootstrap' ? { version: '4.0.1', profiles: [], warning: null, updatesEnabled: false } : operation === 'select_model' ? { model: payload.model } : {} };
        } } };
      });
      await page.goto(`http://127.0.0.1:${server.address().port}/`);
      const player = index % 4 + 1;
      await page.getByRole('button', { name: `Add controller for Player ${player}`, exact: true }).click();
      await page.getByRole('dialog').getByRole('button', { name: `Select ${model.name}`, exact: true }).click();
      await page.getByRole('button', { name: `Enter Studio for Player ${player}`, exact: true }).click();
      const studio = model.id === 'x20' ? page.locator('.rebranded-workspace').filter({ has: page.locator('.editor') }) : page.locator(`.model-workspace[data-model="${model.id}"]`);
      await studio.waitFor();
      const geometry = JSON.parse(await fs.readFile(path.join(root, 'src/assets/controllers', model.id, 'rear-geometry.json'), 'utf8'));
      const shapes = JSON.parse(await fs.readFile(path.join(root, 'src/assets/controllers/control-shapes.json'), 'utf8')).models[model.id].macros;
      assert.deepEqual(geometry.controls.map(c => c.slot).sort(), [...model.macroSlots].sort(), `${model.id} controls correspond to actual slots`);
      const entry = { model: model.id, player, layouts: [], slotsVerified: [] };
      for (const viewport of [{ width: 1400, height: 940 }, { width: 1060, height: 760 }]) {
        await page.setViewportSize(viewport);
        await studio.getByRole('button', { name: 'Back View', exact: true }).click();
        const rear = studio.locator('.controller-rear');
        await rear.locator('img:not(.controller-photo-ambient)').evaluate(img => img.decode());
        const metrics = await rear.evaluate((element, shapes) => {
          const plane = element.querySelector('.controller-rear-plane');
          const p = plane.getBoundingClientRect();
          return {
            imageLoaded: element.querySelector('img').naturalWidth === 1536,
            overflow: document.documentElement.scrollWidth > innerWidth + 2,
            controls: Object.entries(shapes).map(([slot, shape]) => {
              const button = element.querySelector(`[data-slot="${slot}"]`);
              const r = button.getBoundingClientRect();
              const [x,y] = shape.bounds;
              return { slot, offsetX: r.x - (p.x + x / 1536 * p.width), offsetY: r.y - (p.y + y / 1024 * p.height), path: button.querySelector('path').getAttribute('d'), expectedPath: shape.path, cursor: getComputedStyle(button).cursor };
            })
          };
        }, shapes);
        assert.ok(metrics.imageLoaded);
        assert.equal(metrics.overflow, false, `${model.id} no horizontal overflow`);
        assert.equal(await rear.locator('.controller-macro-hotspot').count(), model.macroSlots.length);
        for (const control of metrics.controls) {
          assert.ok(Math.abs(control.offsetX) < .1 && Math.abs(control.offsetY) < .1, `${model.id} ${control.slot} stays aligned`);
          assert.equal(control.path, control.expectedPath);
          assert.equal(control.cursor, 'pointer');
        }
        if (model.macroSlots.length) {
          await rear.scrollIntoViewIfNeeded();
          const button = rear.getByRole('button', { name: `Open ${model.macroSlots[0]} macro`, exact: true });
          await button.hover();
          await page.waitForTimeout(160);
          assert.equal(await button.locator('path').evaluate(p => getComputedStyle(p).stroke), 'rgb(185, 225, 255)');
        }
        await rear.screenshot({ path: path.join(out, `${model.id}-${viewport.width}-back.png`) });
        entry.layouts.push({ viewport, ...metrics });
        await studio.getByRole('button', { name: 'Front View', exact: true }).click();
        assert.equal(await studio.locator('.mapping-controller-stage .controller-stick-cap').count(), 2);
      }
      if (model.macroSlots.length) {
        for (const slot of model.macroSlots) {
          await studio.locator('.chassis-nav').getByRole('button', { name: 'Buttons', exact: true }).click();
          await studio.getByRole('button', { name: 'Back View', exact: true }).click();
          const hotspot = studio.getByRole('button', { name: `Open ${slot} macro`, exact: true });
          await hotspot.click();
          await studio.locator('.macro-studio').waitFor();
          assert.equal(await studio.getByText(`${slot} sequence`, { exact: true }).textContent(), `${slot} sequence`);
          assert.equal(await studio.locator('.macro-paddle[aria-pressed="true"] strong').innerText(), slot);
          entry.slotsVerified.push(slot);
        }
        // Selecting another shape within the macro page updates the actual editor.
        const last = model.macroSlots.at(-1);
        const first = model.macroSlots[0];
        await studio.getByRole('button', { name: `Open ${first} macro`, exact: true }).click();
        assert.equal(await studio.locator('.macro-paddle[aria-pressed="true"] strong').innerText(), first);
        await studio.getByRole('button', { name: `Open ${last} macro`, exact: true }).click();
        assert.equal(await studio.locator('.macro-paddle[aria-pressed="true"] strong').innerText(), last);
        const macroPreview = studio.locator('.macro-controller-preview');
        assert.equal(await macroPreview.locator('.controller-view-switch').count(), 0);
        assert.equal(await macroPreview.locator('.controller-canvas').count(), 0);
        assert.equal(await macroPreview.locator('.controller-view').getAttribute('data-view'), 'back');
        assert.equal(await macroPreview.locator('.controller-rear').count(), 1);
        entry.macrosBackOnlyVerified = true;
        const keyboardTarget = studio.getByRole('button', { name: `Open ${last} macro`, exact: true });
        await keyboardTarget.focus();
        await keyboardTarget.press('Enter');
        assert.equal(await studio.locator('.macro-paddle[aria-pressed="true"] strong').innerText(), last);
        entry.keyboardSelectionVerified = true;
        await studio.locator('.macro-controller-preview').screenshot({ path: path.join(out, `${model.id}-macro-selected.png`) });
        if (model.id === 'x20_pro') await studio.locator('.macro-studio').screenshot({ path: path.join(out, 'x20_pro-macro-layout.png') });
      }
      assert.deepEqual(errors, [], `${model.id} no runtime errors`);
      entry.nativeCalls = await page.evaluate(() => window.rearReviewCalls);
      assert.equal(entry.nativeCalls.some(call => ['scan', 'connect', 'apply', 'record_start'].includes(call.operation)), false);
      if (model.id !== 'x20') assert.equal(entry.nativeCalls.some(call => ['bootstrap', 'input'].includes(call.operation)), false);
      report.models.push(entry);
      await context.close();
    }
    report.passed = true;
  } catch (error) {
    report.error = error.stack || String(error);
    process.exitCode = 1;
  } finally {
    await fs.writeFile(path.join(out, 'report.json'), JSON.stringify(report, null, 2));
    await browser?.close();
    await new Promise(resolve => server.close(resolve));
  }
  console.log(JSON.stringify({ passed: report.passed, models: report.models.length, error: report.error || null }));
})();
