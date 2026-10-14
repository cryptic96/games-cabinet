import { pathToFileURL } from 'node:url';
import { chromium, open, growViewport, OUT, SHEETS } from './lib.mjs';
import { writeStage } from './stage.mjs';

/**
 * The overview grid of all 35 design and wood pairs as whole-piece miniatures: seven designs across, five woods down.
 */
export const DESIGNS = [['classic', 'Classic'], ['modern', 'Modern'], ['display', 'Display'], ['legs', 'On legs'], ['library', 'Library'], ['plywood', 'Plywood'], ['plywood-legs', 'Plywood on legs']];
export const WOODS = [['brown', 'Warm brown'], ['walnut', 'Walnut'], ['oak', 'Oak'], ['birch', 'Birch'], ['mahogany', 'Mahogany']];

export async function makeGrid(browser, { name = 'grid-35', out = 'pair-grid-35.png', attrs = {}, mini = 170, title = 'All 35 pairs: seven designs across, five woods down', scale = 1.5 } = {}) {
  const cells = [];
  let n = 0;
  for (const [w, wl] of WOODS) {
    for (const [d, dl] of DESIGNS) {
      cells.push({ label: `${dl}`, sub: `${wl}`, attrs: { design: d, wood: w, ...attrs }, mini: { box: n++ } });
    }
  }
  writeStage(name, { title, cells, cols: 7, mini, cellW: 230 });
  const page = await open(browser, pathToFileURL(`${OUT}/${name}.html`).href, 1800, 900, scale);
  await growViewport(page, 1800);
  const h = await page.evaluate(() => Math.ceil(document.querySelector('.grid').getBoundingClientRect().bottom + 24));
  await page.screenshot({ path: `${SHEETS}/${out}`, clip: { x: 0, y: 0, width: 1800, height: h } });
  const err = page.errors.slice();
  await page.context().close();
  return err;
}

if (process.argv[1].endsWith('grid.mjs')) {
  const browser = await chromium.launch();
  console.log(await makeGrid(browser));
  await browser.close();
}
