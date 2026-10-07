/* Headless startup handoff review. No host screen or physical hardware access. */
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const http = require('node:http');
const flags = new Map(process.argv.slice(2).reduce((pairs, value, i, args) => i % 2 ? pairs : [...pairs, [value, args[i + 1]]], []));
const { chromium } = require(flags.get('--playwright') || 'playwright');
const root = path.resolve(__dirname, '..');
const dist = path.join(root, 'dist-ui');
const out = path.join(root, 'artifacts/startup-skip-review');
const report = { passed: false, mode: 'headless production UI with mock disconnected bridge', checks: [] };
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
    browser = await chromium.launch({ headless: true, executablePath: flags.get('--browser') });
    const context = await browser.newContext({ viewport: { width: 1400, height: 940 }, reducedMotion: 'no-preference' });
    const page = await context.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => {
      window.pywebview = { api: { request: async operation => ({ ok: true, data: operation === 'input' ? { connected: false, input: null } : operation === 'bootstrap' ? { version: '4.0.1', profiles: [], warning: null, updatesEnabled: false } : {} }) } };
    });
    const load = async () => {
      await page.emulateMedia({ reducedMotion: 'no-preference' });
      await page.goto(`http://127.0.0.1:${server.address().port}/`);
      await page.locator('.startup-intro').waitFor();
      assert.equal(await page.locator('.studio-root').evaluate(app => app.inert && app.getAttribute('aria-hidden') === 'true'), true);
    };
    const unlocked = async () => {
      assert.equal(await page.locator('.startup-intro').count(), 0);
      assert.equal(await page.locator('.studio-root').evaluate(app => app.inert), false);
      assert.equal(await page.locator('.studio-root').getAttribute('aria-hidden'), null);
      assert.equal(await page.getByRole('button', { name: /Add controller for Player/ }).count(), 4);
    };
    for (const trigger of ['Skip', 'Escape', 'Late skip', 'Missing animation event']) {
      await page.setViewportSize(trigger === 'Escape' ? { width: 1060, height: 760 } : { width: 1400, height: 940 });
      await load();
      await page.waitForTimeout(700);
      if (trigger === 'Late skip') {
        await page.locator('.startup-intro').evaluate(element => {
          for (const animation of element.getAnimations({ subtree: true })) animation.currentTime = 6300;
        });
        await page.waitForTimeout(35);
      }
      const startingOpacity = await page.locator('.startup-intro').evaluate(element => parseFloat(getComputedStyle(element).opacity));
      // Measure in the browser so automation transport/polling isn't counted as
      // transition duration. Observe both normal end and the missing-event timer.
      await page.locator('.startup-intro').evaluate(element => {
        window.skipClock = {};
        const observer = new MutationObserver(() => {
          if (element.classList.contains('is-exiting') && !window.skipClock.start) window.skipClock.start = performance.now();
          if (!element.isConnected) {
            window.skipClock.end = performance.now();
            observer.disconnect();
          }
        });
        observer.observe(document.body, { subtree: true, attributes: true, attributeFilter: ['class'], childList: true });
      });
      if (trigger === 'Escape') await page.keyboard.press('Escape');
      else await page.getByRole('button', { name: 'Skip intro Esc', exact: true }).click();
      const overlay = page.locator('.startup-intro.is-exiting');
      assert.equal(await overlay.count(), 1, `${trigger} does not remove the intro immediately`);
      const metrics = await overlay.evaluate(element => ({
        opacity: parseFloat(getComputedStyle(element).opacity),
        savedOpacity: parseFloat(element.style.getPropertyValue('--intro-exit-opacity')),
        duration: getComputedStyle(element).animationDuration,
        locked: document.querySelector('.studio-root').inert,
        paused: getComputedStyle(element.querySelector('.startup-lockup')).animationPlayState,
      }));
      assert.ok(metrics.opacity <= startingOpacity + .04, `${trigger} no flash back to full opacity`);
      assert.ok(Math.abs(metrics.savedOpacity - startingOpacity) < .05);
      assert.equal(metrics.duration, '0.45s');
      assert.equal(metrics.locked, true);
      assert.equal(metrics.paused, 'paused');
      if (trigger === 'Missing animation event') {
        await overlay.evaluate(element => element.style.animation = 'none');
      } else {
        await page.waitForTimeout(100);
        const midpoint = await overlay.evaluate(element => parseFloat(getComputedStyle(element).opacity));
        assert.ok(midpoint > 0 && midpoint < metrics.opacity, `${trigger} fades through an intermediate frame`);
        // Repeated requests must not restart or abruptly complete the fade.
        await page.keyboard.press('Escape');
        assert.equal(await overlay.count(), 1);
      }
      await overlay.waitFor({ state: 'detached', timeout: 1500 });
      const elapsed = await page.evaluate(() => window.skipClock.end - window.skipClock.start);
      assert.ok(elapsed >= 350 && elapsed < (trigger === 'Missing animation event' ? 1000 : 800), `${trigger} bounded handoff: ${elapsed}ms`);
      await unlocked();
      report.checks.push({ name: trigger, elapsedMs: elapsed, ...metrics });
    }
    await page.getByRole('button', { name: 'Add controller for Player 2', exact: true }).click();
    await page.getByRole('dialog').waitFor();
    assert.equal(await page.locator('.startup-intro').count(), 0);
    report.checks.push({ name: 'Player navigation restored without replay' });

    await load();
    const started = Date.now();
    await page.locator('.startup-intro').waitFor({ state: 'detached', timeout: 8000 });
    assert.ok(Date.now() - started >= 6500, 'Natural intro keeps its seven-second timeline');
    await unlocked();
    report.checks.push({ name: 'Natural playback', elapsedMs: Date.now() - started });

    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.reload();
    await unlocked();
    report.checks.push({ name: 'Reduced motion on launch' });
    await load();
    await page.getByRole('button', { name: 'Skip intro Esc', exact: true }).click();
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.locator('.startup-intro').waitFor({ state: 'detached', timeout: 500 });
    await unlocked();
    report.checks.push({ name: 'Reduced motion during exit unlocks immediately' });
    await load();
    await page.getByRole('button', { name: 'Skip intro Esc', exact: true }).click();
    await page.evaluate(() => {
      Object.defineProperty(document, 'hidden', { value: true, configurable: true });
      document.dispatchEvent(new Event('visibilitychange'));
    });
    await page.locator('.startup-intro').waitFor({ state: 'detached', timeout: 500 });
    await unlocked();
    report.checks.push({ name: 'Hidden document during exit clears the overlay' });
    assert.deepEqual(errors, []);
    report.passed = true;
  } catch (error) {
    report.error = error.stack || String(error); process.exitCode = 1;
  } finally {
    await fs.writeFile(path.join(out, 'report.json'), JSON.stringify(report, null, 2));
    await browser?.close(); await new Promise(resolve => server.close(resolve));
  }
  console.log(JSON.stringify({ passed: report.passed, checks: report.checks.length, error: report.error || null }));
})();
