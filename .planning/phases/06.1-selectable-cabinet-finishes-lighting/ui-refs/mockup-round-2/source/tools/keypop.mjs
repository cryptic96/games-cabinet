import { writeFileSync } from 'node:fs';
import { chromium, open, growViewport, switcherUrl, S } from './lib.mjs';

/**
 * How much the page changes at the first frame of the lamp fade: the lights are on but the light layer is hidden
 * hidden, so the only thing that differs from the lights-off page is the box keyline. Reports the largest channel
 * change, the share of pixels that change and how many change by more than 24 of 255, for every wood.
 */
const browser = await chromium.launch();
const results = {};
for (const [wood, backs] of [['brown', 'wood'], ['walnut', 'wood'], ['oak', 'wood'], ['birch', 'wood'], ['mahogany', 'wood'], ['brown', 'painted'], ['birch', 'painted']]) {
  const shots = [];
  for (const lights of ['off', 'on']) {
    const page = await open(browser, switcherUrl('switcher.html', { design: 'modern', wood, backs, lights }), 1440, 900, 1);
    await page.waitForSelector('html[data-ready]');
    if (lights === 'on') await page.addStyleTag({ content: '.section-light{display:none !important}' });
    await growViewport(page, 1440);
    await page.waitForTimeout(150);
    shots.push((await page.locator('#cabinet').screenshot()).toString('base64'));
    await page.context().close();
  }
  const page = await browser.newPage();
  results[wood + '/' + backs] = await page.evaluate(async ([a, b]) => {
    const load = (d) => new Promise((resolve) => { const i = new Image(); i.onload = () => resolve(i); i.src = 'data:image/png;base64,' + d; });
    const [ia, ib] = await Promise.all([load(a), load(b)]);
    const grab = (img) => { const c = document.createElement('canvas'); c.width = img.width; c.height = img.height; const g = c.getContext('2d'); g.drawImage(img, 0, 0); return g.getImageData(0, 0, c.width, c.height).data; };
    const da = grab(ia);
    const db = grab(ib);
    let max = 0; let changed = 0; let over24 = 0;
    for (let i = 0; i < da.length; i += 4) {
      const d = Math.max(Math.abs(da[i] - db[i]), Math.abs(da[i + 1] - db[i + 1]), Math.abs(da[i + 2] - db[i + 2]));
      if (d > max) max = d;
      if (d > 0) changed++;
      if (d > 24) over24++;
    }
    return { max, changedPercent: Math.round((changed / (da.length / 4)) * 10000) / 100, over24 };
  }, shots);
  await page.close();
  console.log(wood, backs, JSON.stringify(results[wood + '/' + backs]));
}
writeFileSync(`${S}/round2/measurements-keyline-pop.json`, JSON.stringify(results, null, 2));
await browser.close();
