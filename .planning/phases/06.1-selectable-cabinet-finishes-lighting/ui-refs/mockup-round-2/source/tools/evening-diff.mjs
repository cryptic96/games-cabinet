import { chromium, open, growViewport, switcherUrl, SHEETS, writeFileSync } from './lib.mjs';
import { LOOKS } from './quick.mjs';

/**
 * How much the page changes between day and evening for each look (the lights fade in with Evenings only): the largest
 * change in any channel and the number of pixels that change by more than 30 and by more than 12 of 255.
 */
const browser = await chromium.launch();
const results = {};
for (const [id, state] of LOOKS) {
  const shots = [];
  for (const evening of ['0', '1']) {
    const page = await open(browser, switcherUrl('switcher.html', { ...state, lights: 'evenings', evening }), 1440, 900, 1);
    await page.waitForSelector('html[data-ready]');
    await growViewport(page, 1440);
    await page.waitForTimeout(150);
    shots.push((await page.screenshot({ fullPage: true })).toString('base64'));
    await page.context().close();
  }
  const page = await browser.newPage();
  results[id] = await page.evaluate(async ([a, b]) => {
    const load = (d) => new Promise((resolve) => { const i = new Image(); i.onload = () => resolve(i); i.src = 'data:image/png;base64,' + d; });
    const [ia, ib] = await Promise.all([load(a), load(b)]);
    const grab = (img) => { const c = document.createElement('canvas'); c.width = img.width; c.height = img.height; const g = c.getContext('2d'); g.drawImage(img, 0, 0); return g.getImageData(0, 0, c.width, c.height).data; };
    const da = grab(ia);
    const db = grab(ib);
    let max = 0;
    let over30 = 0;
    let over12 = 0;
    for (let i = 0; i < da.length; i += 4) {
      const d = Math.max(Math.abs(da[i] - db[i]), Math.abs(da[i + 1] - db[i + 1]), Math.abs(da[i + 2] - db[i + 2]));
      if (d > max) max = d;
      if (d > 30) over30++;
      if (d > 12) over12++;
    }
    return { max, over30, over12, pixels: da.length / 4 };
  }, shots);
  await page.close();
  console.log(id, JSON.stringify(results[id]));
}
writeFileSync(`${SHEETS}/evening-diff.json`, JSON.stringify(results, null, 2));
await browser.close();
