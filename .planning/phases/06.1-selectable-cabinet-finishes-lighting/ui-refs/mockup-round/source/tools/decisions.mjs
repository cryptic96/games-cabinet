import { pathToFileURL } from 'node:url';
import { chromium, open, growViewport, switcherUrl, OUT, SHEETS } from './lib.mjs';
import { writeStage, miniature } from './stage.mjs';
import { montage } from './montage.mjs';

/**
 * The decision sheet of the round: one labelled image per open choice, each showing the options side by side. Every
 * option is drawn by the real layers; token overrides are applied on the page only, so nothing here changes the look.
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

async function pageShot(file, state, width, out, { clip = null, tokens = null, after = null, scale = 1 } = {}) {
  const page = await open(browser, switcherUrl(file, state), width, 900, scale);
  await page.waitForSelector('html[data-ready]');
  if (tokens) await page.evaluate((t) => Object.entries(t).forEach(([k, v]) => document.documentElement.style.setProperty(k, v)), tokens);
  await growViewport(page, width);
  if (after) await page.evaluate(after);
  await page.waitForTimeout(200);
  await page.screenshot({ path: `${SHEETS}/${out}`, clip: clip ?? undefined, fullPage: !clip });
  await page.context().close();
}

if (want('R-1')) {
  const items = [];
  for (const [id, state, label] of [['library', { design: 'library', wood: 'mahogany' }, 'Library + mahogany'], ['legs', { design: 'legs', wood: 'oak' }, 'On legs + oak']]) {
    for (const join of ['off', 'on']) {
      const out = `tmp-r1-${id}-${join}.png`;
      await pageShot('switcher-phone.html', { ...state, join }, 390, out, { clip: { x: 0, y: 200, width: 390, height: 2300 } });
      items.push({ file: out, label: `${label}`, sub: join === 'off' ? 'Separate: every section a full piece (recommended)' : 'Joined: top on the first, base on the last' });
    }
  }
  await montage(browser, { items, cols: 4, out: 'decision-R-1.png', title: 'R-1  Phone: separate sections or one joined tall piece', cell: [390, 2300], labelH: 50 });
}

if (want('R-2')) {
  const items = [];
  for (const [wood, design, label] of [['oak', 'legs', 'Oak wall (dark)'], ['mahogany', 'library', 'Mahogany wall (dark)'], ['birch', 'plywood-legs', 'Birch wall (light)']]) {
    for (const dim of ['off', 'on']) {
      const out = `tmp-r2-${wood}-${dim}.png`;
      await pageShot('switcher.html', { design, wood, lights: 'evenings', evening: '1', dim }, 1440, out, { clip: { x: 0, y: 0, width: 1440, height: 760 } });
      items.push({ file: out, label, sub: dim === 'off' ? 'Lights only (recommended)' : 'Room dims: wall 0.35 and furniture 0.25 (birch wall 0.08)' });
    }
  }
  await montage(browser, { items, cols: 2, out: 'decision-R-2.png', title: 'R-2  Evening: lights only or room dims', cell: [900, 475], labelH: 50 });
}

if (want('R-3')) {
  const items = [];
  const base = { design: 'legs', wood: 'oak' };
  const crop = async (out, tokens, after, sub, label) => {
    const page = await open(browser, switcherUrl('switcher.html', base), 1440, 900, 1);
    await page.waitForSelector('html[data-ready]');
    if (tokens) await page.evaluate((t) => Object.entries(t).forEach(([k, v]) => document.documentElement.style.setProperty(k, v)), tokens);
    await growViewport(page, 1440);
    if (after) await page.evaluate(after);
    await page.waitForTimeout(200);
    const box = await page.evaluate(() => {
      const all = document.querySelectorAll('#cabinet .section');
      const s = all[0].getBoundingClientRect();
      const t = all[1].getBoundingClientRect();
      return { bottom: s.bottom + window.scrollY, second: t.bottom + window.scrollY, left: document.querySelector('#cabinet').getBoundingClientRect().left };
    });
    await page.screenshot({ path: `${SHEETS}/${out}`, clip: { x: 40, y: box.second - 170, width: 1360, height: box.bottom - box.second + 200 } });
    await page.context().close();
    items.push({ file: out, label, sub });
  };
  await crop('tmp-r3-patch.png', null, null, 'Floor and skirting drawn inside each piece (recommended). Free: fits paint containment.', 'Patch per piece');
  await crop('tmp-r3-none.png', { '--room-k': '0', '--floor': 'transparent', '--skirting': 'transparent' }, null, 'For comparison: no floor (what warm brown does today).', 'No floor');
  await crop('tmp-r3-row.png', null, () => {
    document.querySelectorAll('.section-floor').forEach((e) => e.style.setProperty('display', 'none'));
    const first = document.querySelectorAll('#cabinet .section')[0].getBoundingClientRect();
    const cab = document.querySelector('#cabinet').getBoundingClientRect();
    const u = first.width / (parseFloat(getComputedStyle(document.querySelector('.section')).getPropertyValue('--section-w')) + 64);
    const feet = first.bottom + window.scrollY - 24 * u;
    const mk = (top, h, bg) => {
      const d = document.createElement('div');
      d.style.cssText = `position:absolute;left:${cab.left}px;width:${cab.width}px;top:${top}px;height:${h}px;background:${bg};z-index:-1;`;
      document.body.append(d);
    };
    mk(feet - 14 * u - 36 * u, 36 * u, 'var(--skirting)');
    mk(feet - 14 * u, 38 * u, 'var(--floor)');
  }, 'One floor per row, a mock: it must pick one feet line, so the shorter second piece floats above it. Real cost: script on every resize or giving up the paint clipping.', 'One floor per row');
  await montage(browser, { items, cols: 1, out: 'decision-R-3.png', title: 'R-3  Floor: a patch per piece or one floor per row (On legs + oak)', cell: [1360, 760], labelH: 50 });
}

if (want('R-4')) {
  const items = [];
  const variants = [
    ['Deep plum #2f2531', null, 'Dark, white logo. Frame against wall 1.4 to 1, relies on the arris and the cast shadow (recommended).'],
    ['Charcoal moss #2e342b', { '--room': '#2e342b', '--room-text': '#ebeee6', '--floor': '#1f231d', '--skirting': '#3b4237' }, 'Dark, white logo. Close to the mahogany green.'],
    ['Warm stone #d2ccc0', { '--room': '#d2ccc0', '--room-text': '#241e1a', '--room-accent': '#2a1a10', '--floor': '#aaa393', '--skirting': '#e6e1d6', '--wall-shadow': '0.15', '--room-lit-alpha': '0.30' }, 'Light, dark logo. A second light wall next to birch; strongest silhouette for a dark frame, weakest evening.'],
  ];
  let n = 0;
  for (const [label, tokens, sub] of variants) {
    const out = `tmp-r4-${n++}.png`;
    await pageShot('switcher.html', { design: 'display', wood: 'walnut' }, 1440, out, { tokens, clip: { x: 0, y: 0, width: 1440, height: 760 }, after: tokens && tokens['--room'] === '#d2ccc0' ? () => document.getElementById('bgg-logo').setAttribute('src', 'img/bgg-dark-standin.svg') : null });
    items.push({ file: out, label: `Walnut on ${label}`, sub });
  }
  await montage(browser, { items, cols: 3, out: 'decision-R-4.png', title: 'R-4  Walnut wall (Display + walnut)', cell: [800, 422], labelH: 64 });
}

if (want('R-5')) {
  const cells = [];
  const designs = [['classic', 'Classic'], ['modern', 'Modern'], ['display', 'Display'], ['legs', 'On legs'], ['library', 'Library'], ['plywood', 'Plywood'], ['plywood-legs', 'Plywood on legs']];
  const woods = [['brown', 'Warm brown'], ['walnut', 'Walnut'], ['oak', 'Oak'], ['birch', 'Birch'], ['mahogany', 'Mahogany']];
  for (const [d, dl] of designs) cells.push({ label: dl, sub: 'whole miniature', cls: 'tilebox whole', attrs: { design: d, wood: 'oak' }, mini: { box: 1 } });
  for (const [d, dl] of designs) cells.push({ label: dl, sub: 'corner crop', cls: 'tilebox', attrs: { design: d, wood: 'oak' }, html: miniature({ box: 1, mm: 330 }) });
  for (const [w, wl] of woods) cells.push({ label: wl, sub: 'corner crop (recommended)', cls: 'tilebox', attrs: { design: 'classic', wood: w }, html: miniature({ box: 2, mm: 330 }) });
  for (const [w, wl] of woods) cells.push({ label: wl, sub: 'whole miniature', cls: 'tilebox whole', attrs: { design: 'classic', wood: w }, mini: { box: 2 } });
  await stageShot('tiles', { title: 'R-5  Tile pictures: whole piece or corner crop (128 px high pictures)', cells, cols: 7, mini: 112 }, 'decision-R-5.png', 1.5, 1400);
}

if (want('R-6')) {
  const cells = [];
  const floorTokens = '--room-k:1;--floor:#2e1c11;--skirting:#573824;--room-lit-alpha:0.12;';
  for (const [d, dl] of [['classic', 'Classic'], ['library', 'Library'], ['legs', 'On legs']]) {
    cells.push({ label: `${dl} + warm brown`, sub: 'Identity: flat wall, no floor, no skirting (recommended, frozen default)', attrs: { design: d, wood: 'brown' }, mini: { box: 0 } });
    cells.push({ label: `${dl} + warm brown`, sub: 'Alternative: floor, skirting and falloff (an exception to the frozen default)', attrs: { design: d, wood: 'brown' }, style: floorTokens, mini: { box: 0 } });
  }
  await stageShot('brown-room', { title: 'R-6  Warm brown room: identity or a floor', cells, cols: 2, mini: 250, cellW: 480 }, 'decision-R-6.png', 1.2, 1100);
}

if (want('R-7')) {
  const cells = [];
  const perWood = { brown: '#161f15', walnut: '#1c1520', oak: '#0e1d2b', birch: '#0f2025', mahogany: '#0b1f16' };
  const names = { brown: 'Warm brown', walnut: 'Walnut', oak: 'Oak', birch: 'Birch', mahogany: 'Mahogany' };
  for (const w of Object.keys(perWood)) cells.push({ label: `${names[w]}: one petrol #0f2025`, sub: 'recommended', attrs: { design: 'modern', wood: w, backs: 'painted' }, mini: { box: 1 } });
  for (const w of Object.keys(perWood)) cells.push({ label: `${names[w]}: ${perWood[w]}`, sub: 'tuned per wood', attrs: { design: 'modern', wood: w, backs: 'painted' }, style: `--back-paint-src:${perWood[w]}`, mini: { box: 1 } });
  await stageShot('painted', { title: 'R-7  Painted back: one petrol for every wood, or a dark tuned per wood', cells, cols: 5, mini: 190 }, 'decision-R-7.png', 1.3, 1500);
}

if (want('R-8')) {
  const cells = [];
  for (const [wood, design, name] of [['mahogany', 'library', 'Library + mahogany'], ['oak', 'legs', 'On legs + oak']]) {
    for (const [k, rgb, sub] of [['2700 K', '255 200 130', 'candle, warmest'], ['3300 K', '255 212 150', 'recommended'], ['4000 K', '255 228 190', 'neutral warm']]) {
      cells.push({ label: `${name}, ${k}`, sub, attrs: { design, wood, lights: 'on', depth: 'shaded' }, style: `--lamp-rgb:${rgb}`, mini: { box: 3, cubbyH: 240 } });
    }
  }
  await stageShot('lamp', { title: 'R-8  Lamp colour (lights Always on)', cells, cols: 3, mini: 250 }, 'decision-R-8.png', 1.3, 1500);
}

if (want('R-9')) {
  const cells = [];
  cells.push({ label: 'Library + oak, before', sub: 'The brass line is the same gold as the wood and vanishes', attrs: { design: 'library', wood: 'oak' }, style: '--inlay-src:#c4a15a', mini: { box: 1 } });
  cells.push({ label: 'Library + oak, token fix', sub: 'Darker brass on the light woods: --inlay', attrs: { design: 'library', wood: 'oak' }, mini: { box: 1 } });
  cells.push({ label: 'Library + birch, token fix', sub: 'Same darker brass', attrs: { design: 'library', wood: 'birch' }, mini: { box: 1 } });
  cells.push({ label: 'Plywood + walnut, before', sub: 'Ply layers too close in tone on a dark wood', attrs: { design: 'plywood', wood: 'walnut' }, style: '--ply-a-src:color-mix(in oklch,#593a29,white 10%);--ply-b-src:color-mix(in oklch,#593a29,black 8%);--ply-glue-src:color-mix(in oklch,#593a29,black 45%)', mini: { box: 1 } });
  cells.push({ label: 'Plywood + walnut, token fix', sub: 'Wider tone steps and a darker glue line: --ply-a, --ply-b, --ply-glue', attrs: { design: 'plywood', wood: 'walnut' }, mini: { box: 1 } });
  cells.push({ label: 'Plywood + mahogany, token fix', sub: 'Same steps', attrs: { design: 'plywood', wood: 'mahogany' }, mini: { box: 1 } });
  cells.push({ label: 'Display + birch, as specified', sub: 'Near-black kick on plaster reads as a hole (table value, no change proposed)', attrs: { design: 'display', wood: 'birch' }, mini: { box: 1 } });
  cells.push({ label: 'Display + birch, lighter kick (option)', sub: 'Kick #3a3329 to #4a4337: reads as a shaded recess. Needs the owner to say yes (it also lightens Modern and Plywood on birch)', attrs: { design: 'display', wood: 'birch' }, style: '--recess-src:#3a3329;--recess-low-src:#4a4337', mini: { box: 1 } });
  await stageShot('odd-pairs', { title: 'R-9  Odd pairs and their token fixes (no pair rule is used)', cells, cols: 3, mini: 190, cellW: 330 }, 'decision-R-9.png', 1.3, 1300);
}

if (want('R-10')) {
  const backs = [['brown', 'wood', 'Warm brown back'], ['walnut', 'wood', 'Walnut back'], ['oak', 'wood', 'Oak back'], ['birch', 'wood', 'Birch back'], ['mahogany', 'wood', 'Mahogany back'], ['brown', 'painted', 'Painted back on brown'], ['birch', 'painted', 'Painted back on birch']];
  const items = [];
  for (const lights of ['off', 'on']) {
    const cells = backs.map(([wood, bk, label]) => ({ label, sub: lights === 'off' ? 'bare' : 'lamp lit (peak near the top, tail lower down)', attrs: { design: 'modern', wood, backs: bk, lights, depth: 'flat' }, mini: { box: 2, mm: 200 } }));
    const out = 'tmp-r10-' + lights + '.png';
    await stageShot('vis-' + lights, { cells, cols: 7, mini: 200, cellW: 260 }, out, 1, 1900);
    items.push({ file: out, label: lights === 'off' ? 'Bare backs' : 'The same backs with the lamp on', sub: 'Box visibility is read from these pixels, see the table in the round document' });
  }
  await montage(browser, { items, cols: 1, out: 'decision-R-10.png', title: 'R-10  Box against the back: bare and under the lamp (Teal box shown)', cell: [1900, 215], labelH: 50 });
}

await browser.close();
