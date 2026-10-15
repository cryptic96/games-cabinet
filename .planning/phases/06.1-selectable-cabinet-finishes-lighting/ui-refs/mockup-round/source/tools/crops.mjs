import { chromium, open, growViewport, switcherUrl, SHEETS } from './lib.mjs';

/**
 * Takes detail crops of the first section of a look at device scale 2: the top-left corner, a shelf edge, the base and
 * the left leg or crown. Returns the saved file names. Crop boxes are given in millimetres of the section, converted
 * with the section's own unit, so every design is cropped at the same relative place.
 */
export async function lookCrops(browser, id, state, { file = 'switcher.html', width = 1440, scale = 2, names = null, prefix = '' } = {}) {
  const page = await open(browser, switcherUrl(file, state), width, 900, scale);
  await page.waitForSelector('html[data-ready]');
  await growViewport(page, width);
  await page.waitForTimeout(150);
  const geo = await page.evaluate(() => {
    const s = document.querySelector('#cabinet .section');
    const r = s.getBoundingClientRect();
    const cs = getComputedStyle(s.querySelector('.section-body'));
    const bw = parseFloat(getComputedStyle(s).getPropertyValue('--bw'));
    const u = s.querySelector('.section-body').getBoundingClientRect().width / parseFloat(getComputedStyle(s).getPropertyValue('--section-w'));
    const get = (n) => parseFloat(getComputedStyle(s).getPropertyValue(n));
    return { x: r.x + window.scrollX, y: r.y + window.scrollY, w: r.width, h: r.height, u, top: get('--top-height'), base: get('--base-height'), floor: get('--floor-margin'), sectionH: get('--section-h') };
  });
  const { x, y, u, top, base, floor, sectionH, w, h } = geo;
  const clip = (cx, cy, cw, ch) => ({ x: x + cx * u, y: y + cy * u, width: cw * u, height: ch * u });
  const boxes = {
    corner: clip(0, 0, 220, 170),
    edge: clip(20, top + 150, 240, 120),
    base: clip(0, top + sectionH - 30, 300, base + floor + 30),
    feet: clip(0, top + sectionH, 360, base + floor),
  };
  const out = [];
  for (const [name, box] of Object.entries(boxes)) {
    if (names && !names.includes(name)) continue;
    const file2 = `${id}-${prefix}${name}.png`;
    await page.screenshot({ path: `${SHEETS}/${file2}`, clip: box });
    out.push(file2);
  }
  await page.context().close();
  return out;
}

if (process.argv[1].endsWith('crops.mjs')) {
  const [id, design, wood, backs = 'wood'] = process.argv.slice(2);
  const browser = await chromium.launch();
  console.log(await lookCrops(browser, id, { design, wood, backs }));
  await browser.close();
}
