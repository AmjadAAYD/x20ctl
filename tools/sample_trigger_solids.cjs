/* Sample saved contour paths in a hidden browser; no screen or hardware. */
const fs = require('node:fs/promises');
const path = require('node:path');
const { chromium } = require(process.argv[2] || 'playwright');
(async () => {
  const root = path.resolve(__dirname, '..');
  const controls = JSON.parse(await fs.readFile(path.join(root,'src/assets/controllers/control-shapes.json'),'utf8'));
  const browser = await chromium.launch({headless:true, executablePath:process.argv[3]});
  try {
    const page = await browser.newPage();
    const models = await page.evaluate(models => Object.fromEntries(Object.entries(models).map(([model,shapes]) =>
      [model,Object.fromEntries(['LT','RT'].map(key => {
        const path = document.createElementNS('http://www.w3.org/2000/svg','path');
        path.setAttribute('d',shapes.back[key].path);
        const length=path.getTotalLength();
        return [key,Array.from({length:96},(_,i)=>{const p=path.getPointAtLength(length*i/96);return [Number(p.x.toFixed(3)),Number(p.y.toFixed(3))];})];
      }))])),controls.models);
    await fs.writeFile(path.join(root,'src/assets/controllers/trigger-solids.json'),JSON.stringify({sourceSize:[1536,1024],models}));
    console.log(JSON.stringify({models:Object.keys(models).length,pointsPerCap:96}));
  } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
