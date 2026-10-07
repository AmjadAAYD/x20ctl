/* Sample existing SVG paths as segmentation guides; never accesses the user's screen. */
const fs = require('node:fs/promises');
const path = require('node:path');
const flags = new Map(process.argv.slice(2).reduce((p, v, i, a) => i % 2 ? p : [...p, [v, a[i + 1]]], []));
const { chromium } = require(flags.get('--playwright') || 'playwright');
(async () => {
  const root = path.resolve(__dirname, '..');
  const lighting = JSON.parse(await fs.readFile(path.join(root, 'src/assets/controllers/lighting.json'), 'utf8'));
  const browser = await chromium.launch({ headless: true, executablePath: flags.get('--browser') });
  try {
    const page = await browser.newPage();
    const samples = await page.evaluate(models => {
      const result = {};
      for (const [model, views] of Object.entries(models)) {
        result[model] = {};
        for (const [view, d] of Object.entries(views)) {
          const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
          const curve = document.createElementNS(svg.namespaceURI, 'path');
          curve.setAttribute('d', d); svg.append(curve); document.body.append(svg);
          const length = curve.getTotalLength();
          result[model][view] = Array.from({ length: Math.ceil(length / 2) }, (_, i) => {
            const point = curve.getPointAtLength(i * 2); return [point.x, point.y];
          });
          svg.remove();
        }
      }
      return result;
    }, lighting.models);
    const out = path.join(root, 'artifacts/controller-shell-fit');
    await fs.mkdir(out, { recursive: true });
    await fs.writeFile(path.join(out, 'guides.json'), JSON.stringify(samples));
    console.log('Sampled ' + Object.keys(samples).length + ' model guides.');
  } finally { await browser.close(); }
})();
