/* Isolated production frontend; no user screen, device scans, or hardware writes. */
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const http = require('node:http');
const flags = new Map(process.argv.slice(2).reduce((out, value, i, args) => i % 2 ? out : [...out, [value, args[i + 1]]], []));
const { chromium } = require(flags.get('--playwright') || 'playwright');
const root = path.resolve(__dirname, '..');
const dist = path.join(root, 'dist-ui');
const out = path.join(root, 'artifacts/macro-matrix-fit');
const report = { passed: false, mode: 'headless production frontend with disconnected fixture bridge', cases: [] };
const mime = { '.html': 'text/html', '.js': 'application/javascript', '.css': 'text/css', '.png': 'image/png', '.svg': 'image/svg+xml', '.ttf': 'font/ttf' };
(async () => {
  await fs.mkdir(out, { recursive: true });
  const server = http.createServer(async (req, res) => {
    try {
      const url = new URL(req.url, 'http://127.0.0.1');
      const file = path.resolve(dist, '.' + decodeURIComponent(url.pathname === '/' ? '/index.html' : url.pathname));
      if (!file.startsWith(dist + path.sep)) return res.writeHead(403).end();
      res.setHeader('Content-Type', mime[path.extname(file)] || 'application/octet-stream');
      res.end(await fs.readFile(file));
    } catch { res.writeHead(404).end(); }
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  let browser;
  try {
    browser = await chromium.launch({ headless: true, executablePath: flags.get('--browser') });
    const catalog = JSON.parse(await fs.readFile(path.join(root, 'x20ctl/controllers/catalog.json'), 'utf8'));
    for (const model of catalog.filter(model => model.macroSlots.length && (!flags.has('--quick') || ['x20', 'x20_pro'].includes(model.id)))) {
      const context = await browser.newContext({ viewport: { width: 1400, height: 940 }, reducedMotion: 'reduce' });
      const page = await context.newPage();
      const errors = []; page.on('pageerror', error => errors.push(error.message));
      await page.addInitScript(() => {
        window.reviewLength = 1; window.reviewCalls = []; window.reviewLibrary = [];
        window.pywebview = { api: { request: async (operation, payload = {}) => {
          window.reviewCalls.push(operation);
          const success = data => ({ ok: true, data });
          if (operation === 'select_model') return success({ model: payload.model });
          if (operation === 'bootstrap') return success({ version: '4.0.1', profiles: [], updatesEnabled: false });
          if (operation === 'input') return success({ connected: false, input: { slot: 0, buttons: [], leftStick: { x: 0, y: 0 }, rightStick: { x: 0, y: 0 }, leftTrigger: 0, rightTrigger: 0 } });
          if (operation === 'macro_library') {
            if (payload.action === 'save') window.reviewLibrary = [{ ...structuredClone(payload.entry), id: 'fixture' }];
            return success({ entries: window.reviewLibrary, path: 'Fixture library' });
          }
          if (operation === 'macro_import') return success({ format: 'x20ctl-macro', version: 1, name: 'Matrix fixture', sourceModel: 'x20', loopMs: 0,
            steps: Array.from({ length: window.reviewLength }, (_, i) => ({ id: `s${i}`, buttons: i % 2 ? ['B', 'RT'] : ['A', 'DPAD_UP'], leftStick: i % 9, rightStick: 8 - i % 9, durationMs: 40 + i * 5, intervalMs: window.reviewLength === 23 ? 20 : 0 })) });
          if (operation === 'disconnect') return success({});
          throw new Error('Unexpected operation: ' + operation);
        } } };
      });
      await page.goto(`http://127.0.0.1:${server.address().port}/`);
      await page.getByRole('button', { name: 'Add controller for Player 3', exact: true }).click();
      await page.getByRole('dialog').getByRole('button', { name: `Select ${model.name}`, exact: true }).click();
      await page.getByRole('button', { name: 'Enter Studio for Player 3', exact: true }).click();
      const studio = model.id === 'x20' ? page.locator('.rebranded-workspace').filter({ has: page.locator('.editor') }) : page.locator(`.model-workspace[data-model="${model.id}"]`);
      await studio.getByRole('button', { name: 'Macros', exact: true }).click();
      for (const length of (flags.has('--quick') ? [1, 2] : [1, 2, 23, 47])) {
        await page.evaluate(length => { window.reviewLength = length; }, length);
        await studio.getByRole('button', { name: 'Library & copy', exact: true }).click();
        const dialog = page.getByRole('dialog', { name: 'Macro library', exact: true });
        await dialog.getByRole('button', { name: 'Import macro', exact: true }).click();
        await dialog.getByRole('button', { name: 'Load into M1', exact: true }).click();
        await page.getByRole('button', { name: 'Close macro library', exact: true }).click();
        for (const width of (flags.has('--quick') ? [1400, 800] : [1920, 1400, 1100, 1000, 800])) {
          await page.setViewportSize({ width, height: 940 });
          const matrix = studio.locator('.sequencer-matrix');
          const metrics = await matrix.evaluate(table => {
            const wrapper = table.parentElement;
            const rect = table.getBoundingClientRect();
            const controls = [...table.querySelectorAll('button, select')];
            return { width: rect.width, height: rect.height, horizontalOverflow: wrapper.scrollWidth - wrapper.clientWidth,
              verticalOverflow: wrapper.scrollHeight - wrapper.clientHeight, rows: table.tBodies[0].rows.length,
              columns: table.tHead.rows[0].cells.length - 1, labelWidth: table.tBodies[0].rows[0].cells[0].getBoundingClientRect().width,
              minimumControlWidth: Math.min(...controls.map(e => e.getBoundingClientRect().width)),
              escapedControls: controls.filter(e => { const r = e.getBoundingClientRect(); return r.right > rect.right + 1 || r.left < rect.left - 1; }).length };
          });
          report.cases.push({ model: model.id, length, viewport: width, ...metrics });
          assert.equal(metrics.columns, length);
          assert.equal(metrics.rows, 18);
          assert.ok(metrics.horizontalOverflow <= 2, JSON.stringify(report.cases.at(-1)));
          assert.ok(metrics.verticalOverflow <= 2, JSON.stringify(report.cases.at(-1)));
          assert.equal(metrics.escapedControls, 0);
          assert.ok(metrics.labelWidth <= 110);
          assert.ok(metrics.minimumControlWidth >= 10);
          if (model.id === 'x20_pro' && [2, 47].includes(length) && [1400, 800].includes(width))
            await studio.locator('.sequencer-panel').screenshot({ path: path.join(out, `${model.id}-${length}-${width}.png`) });
        }
        const last = length;
        const a = studio.getByRole('button', { name: `A, step ${last}`, exact: true });
        const before = await a.getAttribute('aria-pressed'); await a.click();
        assert.notEqual(await a.getAttribute('aria-pressed'), before);
        assert.equal(await studio.getByLabel(`Step ${last} hold`, { exact: true }).inputValue(), String(40 + (last - 1) * 5));
        await studio.getByLabel(`Right stick, step ${last}`, { exact: true }).selectOption('3');
        assert.equal(await studio.getByLabel(`Right stick, step ${last}`, { exact: true }).inputValue(), '3');
        const remove = studio.getByRole('button', { name: `Remove Step ${last}`, exact: true });
        assert.equal(await studio.locator('.macro-inspector').getByRole('button', { name: /^Remove/ }).count(), 0);
        assert.ok(await remove.evaluate(e => e.previousElementSibling?.textContent.includes('Add Step')));
        await remove.click();
        if (length === 1) {
          assert.equal(await studio.locator('.sequencer-panel').getAttribute('data-empty'), 'true');
          assert.ok(await studio.getByRole('button', { name: 'Remove Step', exact: true }).isDisabled());
        } else assert.equal(await studio.locator('.sequencer-matrix thead th').count(), length);
      }
      assert.deepEqual(errors, []);
      assert.ok(await page.evaluate(() => window.reviewCalls.every(op => !['apply', 'connect', 'scan'].includes(op))));
      await context.close();
    }
    report.passed = true;
  } catch (error) { report.error = error.stack || String(error); process.exitCode = 1; }
  finally { await fs.writeFile(path.join(out, 'report.json'), JSON.stringify(report, null, 2)); await browser?.close(); await new Promise(resolve => server.close(resolve)); }
  console.log(JSON.stringify({ passed: report.passed, cases: report.cases.length, error: report.error || null }));
})();
