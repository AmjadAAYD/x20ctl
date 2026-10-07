/* Isolated headless production UI. No user's screen, native bridge or hardware. */
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const http = require('node:http');
const { createHash } = require('node:crypto');
const flags = new Map(process.argv.slice(2).reduce((pairs, value, i, args) => i % 2 ? pairs : [...pairs, [value, args[i + 1]]], []));
const { chromium } = require(flags.get('--playwright') || 'playwright');
const root = path.resolve(__dirname, '..');
const dist = path.join(root, 'dist-ui');
const out = path.resolve(root, flags.get('--out') || 'artifacts/vibration-contour-review');
const report = { passed: false, mode: 'headless production UI with disconnected fake native responses', models: [] };
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
    for (const asset of JSON.parse(await fs.readFile(path.join(root, 'artifacts/rear-controller-sources/front-assets-before.json'), 'utf8'))) {
      assert.equal(createHash('sha256').update(await fs.readFile(path.join(root, asset.path))).digest('hex'), asset.sha256, asset.path);
    }
    report.frontAssetsUnchanged = true;
    browser = await chromium.launch({ headless: true, executablePath: flags.get('--browser') });
    for (const model of catalog) {
      const context = await browser.newContext({ viewport: { width: 1400, height: 940 }, reducedMotion: 'no-preference' });
      const page = await context.newPage();
      const errors = [];
      page.on('pageerror', error => errors.push(error.message));
      await page.addInitScript(() => {
        window.vibrationReviewCalls = [];
        window.pywebview = { api: { request: async (operation, payload) => {
          window.vibrationReviewCalls.push(operation);
          if (!['select_model', 'disconnect', 'bootstrap', 'input'].includes(operation)) throw new Error('Unexpected native operation: ' + operation);
          return { ok: true, data: operation === 'input' ? { connected: false, input: null } : operation === 'bootstrap' ? { version: '4.0.1', profiles: [], warning: null, updatesEnabled: false } : operation === 'select_model' ? { model: payload.model } : {} };
        } } };
      });
      await page.goto(`http://127.0.0.1:${server.address().port}/`);
      await page.getByRole('button', { name: 'Add controller for Player 2', exact: true }).click();
      await page.getByRole('dialog').getByRole('button', { name: `Select ${model.name}`, exact: true }).click();
      await page.getByRole('button', { name: 'Enter Studio for Player 2', exact: true }).click();
      const studio = model.id === 'x20' ? page.locator('.rebranded-workspace').filter({ has: page.locator('.editor') }) : page.locator(`.model-workspace[data-model="${model.id}"]`);
      await studio.locator('.chassis-nav').getByRole('button', { name: 'Vibration', exact: true }).click();
      const canvas = studio.locator(model.id === 'x20' ? '.vibration-controller-art .controller-canvas' : '.model-haptics .controller-canvas');
      const geometry = JSON.parse(await fs.readFile(path.join(root, 'src/assets/controllers', model.id, 'haptics-geometry.json'), 'utf8'));
      const entry = { model: model.id, zones: [], layouts: [] };
      await studio.locator('.strength-presets button').filter({ hasText: 'Maximum' }).click();
      for (const viewport of [{ width: 1400, height: 940 }, { width: 1060, height: 760 }]) {
        await page.setViewportSize(viewport);
        await canvas.locator('img:not(.controller-photo-ambient)').evaluate(img => img.decode());
        const metrics = await canvas.evaluate((canvas, geometry) => {
          const plane = canvas.querySelector('.controller-image-plane').getBoundingClientRect();
          const overlay = canvas.querySelector('.controller-haptics-overlay');
          const bounds = overlay.getBoundingClientRect();
          return { offset: Math.max(Math.abs(plane.x - bounds.x), Math.abs(plane.y - bounds.y), Math.abs(plane.width - bounds.width), Math.abs(plane.height - bounds.height)),
            overflow: document.documentElement.scrollWidth > innerWidth + 2,
            zones: [...canvas.querySelectorAll('.controller-motor')].map(zone => ({ id: zone.dataset.motor,
              contour: zone.querySelector('.motor-contour').getAttribute('d'),
              clipped: !!zone.querySelector('[clip-path]'),
              secondLayer: zone.querySelectorAll('.motor-contour-layer').length === 1 && zone.querySelectorAll('.motor-wave-glow').length === 2,
              glow: getComputedStyle(zone.querySelector('.motor-wave-glow')).filter,
              strokeWidth: parseFloat(getComputedStyle(zone.querySelector('.motor-wave:not(.motor-wave-glow)')).strokeWidth),
              contourWidth: parseFloat(getComputedStyle(zone.querySelector('.motor-contour:not(.motor-contour-layer)')).strokeWidth),
              contourOpacity: parseFloat(getComputedStyle(zone.querySelector('.motor-contour:not(.motor-contour-layer)')).opacity),
              contourFilter: getComputedStyle(zone.querySelector('.motor-contour:not(.motor-contour-layer)')).filter,
              overlayOpacity: parseFloat(getComputedStyle(overlay).opacity),
              coreWidth: zone.querySelector('.motor-wave-core') ? parseFloat(getComputedStyle(zone.querySelector('.motor-wave-core')).strokeWidth) : 0,
              shadowWidth: zone.querySelector('.motor-contour-shadow') ? parseFloat(getComputedStyle(zone.querySelector('.motor-contour-shadow')).strokeWidth) : 0,
              animation: getComputedStyle(zone.querySelector('.motor-wave')).animationName,
              shapeTransform: getComputedStyle(zone).transform })),
            geometryMatches: [...canvas.querySelectorAll('.controller-motor')].every(zone => zone.querySelector('.motor-contour').getAttribute('d') === geometry.contours[zone.dataset.motor]),
            photoTransform: getComputedStyle(canvas.querySelector('.controller-photo')).transform };
        }, geometry);
        assert.ok(metrics.offset < .1, `${model.id} shared plane`);
        assert.equal(metrics.overflow, false);
        assert.equal(metrics.geometryMatches, true);
        assert.equal(metrics.photoTransform, 'none');
        assert.deepEqual(metrics.zones.map(zone => zone.id).sort(), model.visual.motors.map(zone => zone.id).sort());
        assert.ok(metrics.zones.every(zone => zone.clipped && zone.animation === 'motor-contour-travel' && zone.shapeTransform === 'none'));
        assert.ok(metrics.zones.every(zone => zone.secondLayer && zone.glow.startsWith('blur(') && zone.strokeWidth >= (zone.id.endsWith('trigger') ? 7 : 13)));
        entry.layouts.push({ viewport, ...metrics });
        await canvas.screenshot({ path: path.join(out, `${model.id}-${viewport.width}-contours.png`) });
        if (flags.get('--visibility-check') === 'true') {
          assert.ok(metrics.zones.every(zone => zone.overlayOpacity >= .9 && zone.contourOpacity >= .8), `${model.id} outline stays bright`);
          assert.ok(metrics.zones.every(zone => zone.contourWidth >= (zone.id.endsWith('trigger') ? 8 : 12)), `${model.id} full outline stays thick`);
          assert.ok(metrics.zones.every(zone => zone.strokeWidth >= (zone.id.endsWith('trigger') ? 14 : 24) && zone.coreWidth > 0 && zone.contourFilter === 'none'), `${model.id} moving light has a sharp core`);
          assert.ok(metrics.zones.every(zone => zone.shadowWidth > zone.contourWidth), `${model.id} light-shell outline has a contrast edge`);
        }
      }
      const wave = canvas.locator('.motor-wave').first();
      await page.evaluate(() => document.documentElement.dataset.motionPaused = 'false');
      const before = await wave.evaluate(path => ({ time: path.getAnimations()[0].currentTime, rect: path.parentElement.parentElement.parentElement.getBoundingClientRect().toJSON() }));
      await page.waitForTimeout(220);
      const after = await wave.evaluate(path => ({ time: path.getAnimations()[0].currentTime, rect: path.parentElement.parentElement.parentElement.getBoundingClientRect().toJSON() }));
      assert.ok(after.time > before.time + 100, `${model.id} animation advances`);
      assert.deepEqual(after.rect, before.rect, `${model.id} controller does not shake`);
      await page.evaluate(() => document.documentElement.dataset.motionPaused = 'true');
      assert.equal(await wave.evaluate(path => getComputedStyle(path).animationPlayState), 'paused');
      await page.evaluate(() => document.documentElement.dataset.motionPaused = 'false');
      const maximumPeriod = await wave.evaluate(path => parseFloat(getComputedStyle(path).animationDuration));
      assert.ok(Math.abs(maximumPeriod - 1) < .01, `${model.id} maximum strength has the restrained one-second cycle`);
      entry.maximumPeriodSeconds = maximumPeriod;
      const maximumOpacity = await canvas.locator('.controller-haptics-overlay').evaluate(svg => parseFloat(getComputedStyle(svg).opacity));
      await studio.locator('.strength-presets button').filter({ hasText: 'Gentle' }).click();
      assert.equal(await canvas.getAttribute('data-motor-power'), '30');
      assert.ok(await wave.evaluate(path => parseFloat(getComputedStyle(path).animationDuration)) > maximumPeriod);
      assert.ok(await canvas.locator('.controller-haptics-overlay').evaluate(svg => parseFloat(getComputedStyle(svg).opacity)) < maximumOpacity);
      if (flags.get('--visibility-check') === 'true') {
        assert.ok(await canvas.locator('.controller-haptics-overlay').evaluate(svg => parseFloat(getComputedStyle(svg).opacity)) >= .8, `${model.id} gentle strength remains visible`);
      }
      await page.emulateMedia({ reducedMotion: 'reduce' });
      assert.equal(await wave.evaluate(path => getComputedStyle(path).animationName), 'none');
      assert.equal(await canvas.locator('.controller-motor').count(), model.visual.motors.length);
      await studio.locator('.strength-presets button').filter({ hasText: 'Off', exact: true }).click();
      assert.equal(await canvas.getAttribute('data-motor-power'), '0');
      assert.equal(await canvas.locator('.controller-haptics-overlay').count(), 0);
      assert.deepEqual(errors, []);
      const operations = await page.evaluate(() => [...new Set(window.vibrationReviewCalls)]);
      if (model.id !== 'x20') assert.deepEqual(operations, ['select_model']);
      entry.operations = operations;
      entry.animationVerified = entry.reducedMotionVerified = entry.offVerified = entry.strengthResponseVerified = true;
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
