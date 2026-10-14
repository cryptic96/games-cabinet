import { pathToFileURL } from 'node:url';
import { chromium, open, growViewport, switcherUrl, OUT, SHEETS } from './lib.mjs';
import { writeStage, miniature } from './stage.mjs';
import { montage } from './montage.mjs';
import { makeGrid } from './grid.mjs';

/**
 * The second-round decision and comparison sheets. Every picture is drawn by the real layers; the "before" lamp is the
 * first round's lamp injected as an unlayered rule on the page only.
 */
const browser = await chromium.launch();
const only = process.argv.slice(2);
const want = (id) => only.length === 0 || only.includes(id);

async function stageShot(name, spec, out, scale = 1.5, width = 1600) {
  writeStage(name, spec);
  const page = await open(browser, pathToFileURL(`${OUT}/${name}.html`).href, width, 900, scale);
  await growViewport(page, width);
  const h = await page.evaluate(() => Math.ceil(document.querySelector('.grid').getBoundingClientRect().bottom + 24));
  await page.screenshot({ path: `${SHEETS}/${out}`, clip: { x: 0, y: 0, width, height: h } });
  await page.context().close();
}

const OLD_LAMP = `.light-pool { background-image:
  linear-gradient(to bottom, rgb(var(--lamp-rgb) / calc(0.22 * var(--lamp-k))) 0, transparent 14%),
  linear-gradient(to bottom, rgb(var(--lamp-rgb) / calc(0.55 * var(--lamp-k))) 0 max(1px, calc(1.5 * var(--u))), transparent max(1px, calc(1.5 * var(--u)))),
  radial-gradient(ellipse 45% 50% at 50% 0%, rgb(var(--lamp-rgb) / calc(0.55 * var(--lamp-k))) 0, rgb(var(--lamp-rgb) / calc(0.55 * var(--lamp-k))) 24%, rgb(var(--lamp-rgb) / calc(0.05 * var(--lamp-k))) 70%, transparent 100%) !important; }`;

async function cubbyRowShot(state, out, { oldLamp = false, hideBoxes = true, rowCubbies = 3 } = {}) {
  const page = await open(browser, switcherUrl('switcher.html', state), 1440, 900, 2);
  await page.waitForSelector('html[data-ready]');
  if (oldLamp) await page.addStyleTag({ content: OLD_LAMP });
  if (hideBoxes) await page.addStyleTag({ content: '.placement { visibility: hidden; }' });
  await growViewport(page, 1440);
  const r = await page.evaluate((n) => {
    const section = document.querySelector('#cabinet .section');
    const cubbies = Array.from(section.querySelectorAll('.cubby')).sort((a, b) => a.getBoundingClientRect().top - b.getBoundingClientRect().top || a.getBoundingClientRect().left - b.getBoundingClientRect().left);
    const top = cubbies[0].getBoundingClientRect();
    const row = cubbies.filter((c) => Math.abs(c.getBoundingClientRect().top - top.top) < 4).slice(0, n);
    const last = row[row.length - 1].getBoundingClientRect();
    return { x: top.x + scrollX, y: top.y + scrollY, w: last.right - top.x, h: top.height };
  }, rowCubbies);
  await page.screenshot({ path: `${SHEETS}/${out}`, clip: { x: r.x - 30, y: r.y - 24, width: r.w + 60, height: Math.min(r.h, 330) + 48 } });
  await page.context().close();
}

if (want('lamp')) {
  const items = [];
  const rows = [
    ['brown', 'wood', 'classic', 'Warm brown'],
    ['mahogany', 'wood', 'library', 'Mahogany'],
    ['oak', 'wood', 'legs', 'Oak'],
    ['birch', 'wood', 'plywood-legs', 'Birch'],
    ['walnut', 'wood', 'display', 'Walnut on stone'],
    ['birch', 'painted', 'plywood', 'Painted back'],
  ];
  for (const [wood, backs, design, label] of rows) {
    await cubbyRowShot({ design, wood, backs, lights: 'on' }, 'tmp-lamp-old.png', { oldLamp: true });
    await cubbyRowShot({ design, wood, backs, lights: 'on' }, 'tmp-lamp-new.png');
    items.push({ file: 'tmp-lamp-old.png', label: `${label}: first round lamp`, sub: 'bell-shaped pool, separate band' });
    items.push({ file: 'tmp-lamp-new.png', label: `${label}: now`, sub: 'wide soft wash, hot source line' });
    const a = `tmp-lamp-old-${wood}-${backs}.png`;
    const b = `tmp-lamp-new-${wood}-${backs}.png`;
    const fs = await import('node:fs');
    fs.copyFileSync(`${SHEETS}/tmp-lamp-old.png`, `${SHEETS}/${a}`);
    fs.copyFileSync(`${SHEETS}/tmp-lamp-new.png`, `${SHEETS}/${b}`);
    items[items.length - 2].file = a;
    items[items.length - 1].file = b;
  }
  await montage(browser, { items, cols: 2, out: 'round2-lamp.png', title: 'The lamp, before and after: three empty cubbies per wood, lights on (boxes hidden)', cell: [900, 340], labelH: 50 });
}

if (want('R-10')) {
  const backs = [['brown', 'wood', 'Warm brown back'], ['walnut', 'wood', 'Walnut back'], ['oak', 'wood', 'Oak back'], ['birch', 'wood', 'Birch back'], ['mahogany', 'wood', 'Mahogany back'], ['brown', 'painted', 'Painted back on brown'], ['birch', 'painted', 'Painted back on birch']];
  const items = [];
  const boxes = [{ x: 24, w: 150, h: 216, bg: '#23407a' }, { x: 196, w: 150, h: 216, bg: '#e09aa5', fg: '#2a1a10' }];
  for (const lights of ['off', 'on']) {
    const cells = backs.map(([wood, bk, label]) => ({ label, sub: lights === 'off' ? 'bare: Navy and Rose box' : 'lamp lit: tall boxes reach the peak', attrs: { design: 'modern', wood, backs: bk, lights, depth: 'flat' }, mini: { boxes: [...boxes], mm: 200, cubbyH: 240 } }));
    const out = `tmp-r10-${lights}.png`;
    await stageShot('vis2-' + lights, { cells, cols: 7, mini: 200, cellW: 260 }, out, 1, 2060);
    items.push({ file: out, label: lights === 'off' ? 'Bare backs: every back, one Navy and one Rose box' : 'The same seven backs under the lamp', sub: 'All seven backs are in view; the boxes are tall enough to reach the top 12 % of the cubby where the lamp peaks' });
  }
  await montage(browser, { items, cols: 1, out: 'round2-R-10.png', title: 'R-10 corrected: all seven backs, Navy and Rose box, bare and under the lamp', cell: [2060, 330], labelH: 50 });
}

if (want('kicks')) {
  const cells = [];
  const woods = [['brown', 'Warm brown'], ['walnut', 'Walnut on stone'], ['oak', 'Oak'], ['birch', 'Birch'], ['mahogany', 'Mahogany']];
  for (const [w, wl] of woods) {
    for (const [d, dl] of [['modern', 'Modern'], ['display', 'Display'], ['plywood', 'Plywood']]) cells.push({ label: `${dl} + ${wl}`, sub: d === 'display' ? 'floating plinth, wide float shadow' : 'grounded toe-kick', attrs: { design: d, wood: w }, mini: { box: 1 } });
  }
  await stageShot('kicks', { title: 'Kicks (R-9, C6, C7): Modern, Display and Plywood on every wood', cells, cols: 3, mini: 200, cellW: 330 }, 'round2-kicks.png', 1.3, 1200);
}

if (want('depth')) {
  const items = [];
  const states = [
    ['Shaded (today)', { design: 'classic', wood: 'brown', depth: 'shaded' }, 'Reference'],
    ['Deep, floor merged into the wall element', { design: 'classic', wood: 'brown', depth: 'deep', deepvar: 'merged' }, 'Four layers on one element, no clip-path'],
    ['Deep, six layers with mitred corners', { design: 'classic', wood: 'brown', depth: 'deep', deepvar: 'mitre' }, 'Floor on the cubby, five on the wall element, clip-path'],
    ['Deep, merged, oak', { design: 'legs', wood: 'oak', depth: 'deep', deepvar: 'merged' }, 'Light wood'],
    ['Deep, mitred, oak', { design: 'legs', wood: 'oak', depth: 'deep', deepvar: 'mitre' }, 'Light wood'],
    ['Deep, merged, lights on', { design: 'classic', wood: 'mahogany', depth: 'deep', deepvar: 'merged', lights: 'on' }, 'Mahogany'],
  ];
  let n = 0;
  for (const [label, state, sub] of states) {
    const page = await open(browser, switcherUrl('switcher.html', state), 1440, 900, 2);
    await page.waitForSelector('html[data-ready]');
    await page.addStyleTag({ content: '.placement { visibility: hidden; }' });
    await growViewport(page, 1440);
    const r = await page.evaluate(() => {
      const c = Array.from(document.querySelectorAll('#cabinet .cubby')).find((x) => x.getBoundingClientRect().width > 200 && x.getBoundingClientRect().height > 150).getBoundingClientRect();
      return { x: c.x + scrollX, y: c.y + scrollY, w: c.width, h: c.height };
    });
    const out = `tmp-depth2-${n++}.png`;
    await page.screenshot({ path: `${SHEETS}/${out}`, clip: { x: r.x - 36, y: r.y - 36, width: Math.min(r.w, 420) + 72, height: Math.min(r.h, 330) + 72 } });
    await page.context().close();
    items.push({ file: out, label, sub });
  }
  await montage(browser, { items, cols: 3, out: 'round2-depth.png', title: 'Depth: Deep with the floor merged into the wall element, or with six layers and mitred corners', cell: [520, 400], labelH: 56 });
}

if (want('walnut')) {
  const items = [];
  let n = 0;
  for (const [label, state, sub] of [['L2 Modern + warm brown', { design: 'modern', wood: 'brown' }, 'Grounded toe-kick, brown wall'], ['L3 Display + walnut on warm stone', { design: 'display', wood: 'walnut' }, 'Darker wood, floating plinth, stone wall']]) {
    const page = await open(browser, switcherUrl('switcher.html', state), 1440, 900, 1);
    await page.waitForSelector('html[data-ready]');
    await growViewport(page, 1440);
    const out = `tmp-walnut-${n++}.png`;
    await page.screenshot({ path: `${SHEETS}/${out}`, clip: { x: 0, y: 0, width: 1440, height: 1000 } });
    await page.context().close();
    items.push({ file: out, label, sub });
  }
  await montage(browser, { items, cols: 2, out: 'round2-walnut.png', title: 'Walnut on warm stone against warm brown: two different pieces', cell: [900, 625], labelH: 56 });
}

if (want('falloff')) {
  const items = [];
  let n = 0;
  for (const [label, wood, design] of [['Walnut on warm stone', 'walnut', 'display'], ['Oak', 'oak', 'legs']]) {
    for (const falloff of ['fixed', 'page']) {
      for (const scroll of [0, 650]) {
        const page = await open(browser, switcherUrl('switcher.html', { design, wood, falloff }), 1440, 900, 1);
        await page.waitForSelector('html[data-ready]');
        await growViewport(page, 1440);
        await page.setViewportSize({ width: 1440, height: 900 });
        await page.evaluate((y) => window.scrollTo(0, y), scroll);
        await page.waitForTimeout(150);
        const out = `tmp-falloff-${n++}.png`;
        await page.screenshot({ path: `${SHEETS}/${out}` });
        await page.context().close();
        items.push({ file: out, label: `${label}: ${falloff === 'page' ? 'painted on the page' : 'fixed layer'}`, sub: scroll === 0 ? 'top of the page' : 'scrolled down 650 px' });
      }
    }
  }
  await montage(browser, { items, cols: 4, out: 'round2-falloff.png', title: 'Light falloff: the first round fixed layer against the same gradient painted on the page', cell: [420, 263], labelH: 50 });
}

if (want('grid')) {
  await makeGrid(browser, { name: 'grid-35', out: 'pair-grid-35.png' });
  await makeGrid(browser, { name: 'grid-35-lit', out: 'pair-grid-35-lit.png', attrs: { lights: 'on' }, title: 'All 35 pairs with the lights on' });
}

if (want('tiles')) {
  const cells = [];
  const designs = [['classic', 'Classic'], ['modern', 'Modern'], ['display', 'Display'], ['legs', 'On legs'], ['library', 'Library'], ['plywood', 'Plywood'], ['plywood-legs', 'Plywood on legs']];
  const woods = [['brown', 'Warm brown'], ['walnut', 'Walnut'], ['oak', 'Oak'], ['birch', 'Birch'], ['mahogany', 'Mahogany']];
  for (const [d, dl] of designs) cells.push({ label: dl, sub: 'whole piece (design tile)', cls: 'tilebox whole', attrs: { design: d, wood: 'oak' }, mini: { box: 1 } });
  for (const [w, wl] of woods) cells.push({ label: wl, sub: 'corner crop (wood tile)', cls: 'tilebox', attrs: { design: 'classic', wood: w }, html: miniature({ box: 2, mm: 330 }) });
  for (const [w, wl] of woods) cells.push({ label: `Plywood + ${wl}`, sub: 'whole piece at tile size', cls: 'tilebox whole', attrs: { design: 'plywood', wood: w }, mini: { box: 2 } });
  await stageShot('tiles2', { title: 'Tile pictures at the 128 px picture height: ply stripes now show at tile size', cells, cols: 7, mini: 112 }, 'round2-tiles.png', 1.5, 1400);
}

await browser.close();
