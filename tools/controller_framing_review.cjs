/* Isolated headless production UI; no desktop screen or controller access. */
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const http = require('node:http');
const flags = new Map(process.argv.slice(2).reduce((pairs, value, i, args) => i % 2 ? pairs : [...pairs, [value, args[i + 1]]], []));
const { chromium } = require(flags.get('--playwright') || 'playwright');
const root = path.resolve(__dirname, '..');
const dist = path.join(root, 'dist-ui');
const out = path.join(root, 'artifacts/controller-framing-review');
const report = { passed: false, mode: 'headless production UI with mock disconnected bridge', models: [] };
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
    const framing = JSON.parse(await fs.readFile(path.join(root, 'src/assets/controllers/framing.json'), 'utf8'));
    browser = await chromium.launch({ headless: true, executablePath: flags.get('--browser') });
    for (const model of catalog) {
      const context = await browser.newContext({ viewport: { width: 1400, height: 940 }, reducedMotion: 'reduce' });
      const page = await context.newPage();
      const errors = [];
      page.on('pageerror', error => errors.push(error.message));
      await page.addInitScript(() => {
        window.framingReviewCalls = [];
        window.pywebview = { api: { request: async (operation, payload) => {
          window.framingReviewCalls.push(operation);
          if (!['select_model', 'disconnect', 'bootstrap', 'input'].includes(operation)) throw new Error('Unexpected native operation: ' + operation);
          return { ok: true, data: operation === 'input' ? { connected: false, input: null } : operation === 'bootstrap' ? { version: '4.0.1', profiles: [], warning: null, updatesEnabled: false } : operation === 'select_model' ? { model: payload.model } : {} };
        } } };
      });
      await page.goto(`http://127.0.0.1:${server.address().port}/`);
      await page.getByRole('button', { name: 'Add controller for Player 2', exact: true }).click();
      await page.getByRole('dialog').getByRole('button', { name: `Select ${model.name}`, exact: true }).click();
      await page.getByRole('button', { name: 'Enter Studio for Player 2', exact: true }).click();
      const studio = model.id === 'x20' ? page.locator('.rebranded-workspace').filter({ has: page.locator('.editor') }) : page.locator(`.model-workspace[data-model="${model.id}"]`);
      const entry = { model: model.id, layouts: [] };
      for (const viewport of [{ width: 1400, height: 940 }, { width: 1060, height: 760 }, { width: 1920, height: 1080 }]) {
        await page.setViewportSize(viewport);
        for (const view of ['front', 'back', 'vibration']) {
          await studio.locator('.chassis-nav').getByRole('button', { name: view === 'vibration' ? 'Vibration' : 'Buttons', exact: true }).click();
          if (view !== 'vibration') await studio.getByRole('button', { name: view === 'front' ? 'Front View' : 'Back View', exact: true }).click();
          else await studio.locator('.strength-presets button').filter({ hasText: 'Maximum' }).click();
          const canvas = studio.locator(view === 'back' ? '.mapping-controller-stage .controller-rear' : view === 'front' ? '.mapping-controller-stage .controller-canvas' : model.id === 'x20' ? '.vibration-controller-art .controller-canvas' : '.model-haptics .controller-canvas');
          await canvas.locator('img:not(.controller-photo-ambient)').evaluate(img => img.decode());
          const metrics = await canvas.evaluate((element, data) => {
            const stage = element.getBoundingClientRect();
            const plane = element.querySelector('.controller-image-plane, .controller-rear-plane').getBoundingClientRect();
            const img = element.querySelector('img');
            const photo = img.getBoundingClientRect();
            const [x, y, w, h] = data.bounds;
            const [sw, sh] = data.sourceSize;
            const shell = { x: plane.x + x / sw * plane.width, y: plane.y + y / sh * plane.height, width: w / sw * plane.width, height: h / sh * plane.height };
            return {
              framing: element.dataset.framing,
              naturalSize: [img.naturalWidth, img.naturalHeight],
              imageAspect: photo.width / photo.height,
              planeOffset: Math.max(Math.abs(photo.x - plane.x), Math.abs(photo.y - plane.y), Math.abs(photo.width - plane.width), Math.abs(photo.height - plane.height)),
              coverage: Math.max(shell.width / stage.width, shell.height / stage.height),
              margins: [(shell.x - stage.x) / stage.width, (stage.right - shell.x - shell.width) / stage.width, (shell.y - stage.y) / stage.height, (stage.bottom - shell.y - shell.height) / stage.height],
              pixelScale: photo.width / sw,
              overflow: document.documentElement.scrollWidth > innerWidth + 2,
              stageSize: [stage.width, stage.height],
              stickOffsets: data.sticks ? ['left', 'right'].map((side, index) => {
                const r = element.querySelectorAll('.controller-stick')[index].getBoundingClientRect();
                const center = data.sticks[side];
                return Math.max(Math.abs(r.x + r.width / 2 - (plane.x + center.x * plane.width)), Math.abs(r.y + r.height / 2 - (plane.y + center.y * plane.height)));
              }) : [],
            };
          }, { bounds: framing.models[model.id][view === 'back' ? 'back' : 'front'], sourceSize: framing.sourceSize, sticks: view === 'front' ? model.visual.sticks : null });
          assert.equal(metrics.framing, 'detail');
          assert.deepEqual(metrics.naturalSize, framing.sourceSize);
          assert.ok(Math.abs(metrics.imageAspect - 1.5) < .001, `${model.id} ${view} undistorted source`);
          assert.ok(metrics.planeOffset < .1, `${model.id} ${view} photo shares the overlay plane`);
          assert.ok(Math.abs(metrics.coverage - framing.coverage) < .001, `${model.id} ${view} consistent shell scale`);
          assert.ok(metrics.margins.every(margin => margin >= .049), `${model.id} ${view} no cropped shoulders or grips`);
          assert.ok(metrics.pixelScale <= 1, `${model.id} ${view} no enlargement past source resolution`);
          assert.ok(metrics.stickOffsets.every(offset => offset < .1), `${model.id} aligned stick centers`);
          assert.equal(metrics.overflow, false);
          if (view !== 'vibration') {
            const stage = studio.locator('.mapping-controller-stage');
            const caption = stage.locator('.mapping-canvas-caption');
            const [s, c] = await Promise.all([stage.boundingBox(), caption.boundingBox()]);
            assert.ok(c.y + c.height <= s.y + s.height + 1, 'view controls and caption fit the Buttons stage');
          }
          await canvas.screenshot({ path: path.join(out, `${model.id}-${viewport.width}-${view}.png`) });
          if (viewport.width === 1400 && view !== 'back') {
            const panel = studio.locator(view === 'front' ? '.mapping-canvas-panel' : model.id === 'x20' ? '.haptics-console' : '.model-haptics');
            await panel.screenshot({ path: path.join(out, `${model.id}-${view}-panel.png`) });
          }
          entry.layouts.push({ viewport, view, ...metrics });
        }
      }
      assert.deepEqual(errors, []);
      entry.operations = await page.evaluate(() => [...new Set(window.framingReviewCalls)]);
      if (model.id !== 'x20') assert.deepEqual(entry.operations, ['select_model']);
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
