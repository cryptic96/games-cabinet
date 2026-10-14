import { pathToFileURL } from 'node:url';
import { writeFileSync } from 'node:fs';
import { chromium, open, growViewport, switcherUrl, OUT, SHEETS, S } from './lib.mjs';
import { writeStage } from './stage.mjs';

/**
 * Measurements of the round: box visibility against every back read from rendered pixels (bare, at the lamp peak and in
 * its tail), spine contrast under every shade tint, chrome contrast on every wall by day and evening, and the cost of
 * Deep with lights on (node and gradient counts, and frame time under four times CPU throttling). Writes
 * measurements.json next to the sheets.
 */
const TONES = [['Oxblood', '#8a2432'], ['Forest', '#1f5a3a'], ['Navy', '#23407a'], ['Mustard', '#e0a82e'], ['Teal', '#17766f'], ['Plum', '#6a2f7a'], ['Rust', '#b8481a'], ['Sky', '#8fbfe0'], ['Cream', '#efe3c2'], ['Sage', '#a3b88f'], ['Slate', '#434f5e'], ['Rose', '#e09aa5']];
const LIGHT_TEXT = '#ffffff';
const DARK_TEXT = '#2a1a10';
const TEXT_OF = { Oxblood: LIGHT_TEXT, Forest: LIGHT_TEXT, Navy: LIGHT_TEXT, Mustard: DARK_TEXT, Teal: LIGHT_TEXT, Plum: LIGHT_TEXT, Rust: LIGHT_TEXT, Sky: DARK_TEXT, Cream: DARK_TEXT, Sage: DARK_TEXT, Slate: LIGHT_TEXT, Rose: DARK_TEXT };

const hex = (h) => [1, 3, 5].map((i) => parseInt(h.slice(i, i + 2), 16));
const lin = (c) => { const s = c / 255; return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4; };
const lum = (rgb) => 0.2126 * lin(rgb[0]) + 0.7152 * lin(rgb[1]) + 0.0722 * lin(rgb[2]);
const ratio = (a, b) => { const x = lum(a); const y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
const blend = (base, over, alpha) => base.map((c, i) => c * (1 - alpha) + over[i] * alpha);
const round2 = (n) => Math.round(n * 100) / 100;

const SHADES = { brown: [20, 10, 4], walnut: [20, 10, 4], oak: [20, 10, 4], birch: [20, 10, 4], mahogany: [20, 10, 4] };
const WALLS = {
  brown: { wall: '#3b2416', text: '#efe3c2', falloff: 0, dusk: 0, logo: '#ffffff' },
  walnut: { wall: '#d2ccc0', text: '#241e1a', falloff: 0.30, dusk: 0, logo: '#3F3A60' },
  oak: { wall: '#27323b', text: '#e9eef1', falloff: 0.06, dusk: 0, logo: '#ffffff' },
  birch: { wall: '#ddd6ca', text: '#251f1b', falloff: 0.30, dusk: 0, logo: '#3F3A60' },
  mahogany: { wall: '#1c2a23', text: '#ebe4cd', falloff: 0.06, dusk: 0, logo: '#ffffff' },
};
const results = { generated: 'scratch measurement of the round', spine: {}, chrome: {}, visibility: {}, cost: {} };

for (const [wood, tint] of Object.entries(SHADES)) {
  results.spine[wood] = {};
  for (const [label, alpha] of [['flat 0', 0], ['deep 0.12', 0.12], ['shaded 0.20', 0.2]]) {
    let worst = 99;
    let worstTone = '';
    for (const [name, bg] of TONES) {
      const b = blend(hex(bg), tint, alpha);
      const t = blend(hex(TEXT_OF[name]), tint, alpha);
      const r = ratio(b, t);
      if (r < worst) { worst = r; worstTone = name; }
    }
    results.spine[wood][label] = { worst: round2(worst), tone: worstTone };
  }
}

for (const [wood, w] of Object.entries(WALLS)) {
  const wall = hex(w.wall);
  const text = hex(w.text);
  const muted = blend(wall, text, 0.7);
  const line = blend(wall, text, 0.6);
  const day = blend(wall, [255, 244, 228], w.falloff);
  const evening = day;
  const dayText = ratio(day, text);
  const evText = ratio(evening, text);
  const evMuted = ratio(evening, blend(evening, text, 0.7));
  const evLine = ratio(evening, blend(evening, text, 0.6));
  const wl = lum(wall);
  const logo = hex(w.logo);
  results.chrome[wood] = {
    wallLuminance: round2(wl),
    textDay: round2(dayText), mutedDay: round2(ratio(day, blend(day, text, 0.7))), lineDay: round2(ratio(day, blend(day, text, 0.6))),
    textEvening: round2(evText), mutedEvening: round2(evMuted), lineEvening: round2(evLine),
    logoDay: round2(ratio(day, logo)), logoEvening: round2(ratio(evening, logo)),
    logoRule: wl > 0.27 ? 'dark' : 'white',
    logoUsed: w.logo === '#ffffff' ? 'white' : 'dark',
  };
}

const browser = await chromium.launch();

const BACKS = [['brown', 'wood', 'Warm brown back'], ['walnut', 'wood', 'Walnut back'], ['oak', 'wood', 'Oak back'], ['birch', 'wood', 'Birch back'], ['mahogany', 'wood', 'Mahogany back'], ['brown', 'painted', 'Painted back on brown'], ['birch', 'painted', 'Painted back on birch']];
const lampRgb = [255, 212, 150];
const lampK = { brown: 1, walnut: 1, oak: 1, birch: 0.12, mahogany: 1.1 };
const poolK = (wood, backs) => (backs === 'painted' ? 1 : lampK[wood]);

async function samplePage(lights, scale = 1) {
  const cells = BACKS.map(([wood, backs, label]) => ({ label, sub: lights, attrs: { design: 'modern', wood, backs, lights, depth: 'flat' }, style: scale === 1 ? undefined : '--lamp-k:' + lampK[wood] * scale, mini: { empty: true, mm: 220 } }));
  writeStage('visibility', { cells, cols: 4, mini: 220, cellW: 300 });
  const page = await open(browser, pathToFileURL(`${OUT}/visibility.html`).href, 1400, 900, 1);
  await growViewport(page, 1400);
  const rects = await page.evaluate(() => Array.from(document.querySelectorAll('.cubby')).map((c) => {
    const r = c.getBoundingClientRect();
    return { x: r.x + window.scrollX, y: r.y + window.scrollY, w: r.width, h: r.height };
  }));
  const out = [];
  const grab = async (x, y) => {
    const png = await page.screenshot({ clip: { x: Math.round(x) - 2, y: Math.round(y) - 2, width: 5, height: 5 } });
    const p2 = await browser.newPage();
    const c = await p2.evaluate(async (d) => {
      const img = await new Promise((resolve) => { const i = new Image(); i.onload = () => resolve(i); i.src = 'data:image/png;base64,' + d; });
      const cv = document.createElement('canvas');
      cv.width = 5; cv.height = 5;
      const g = cv.getContext('2d');
      g.drawImage(img, 0, 0);
      const data = g.getImageData(0, 0, 5, 5).data;
      const sum = [0, 0, 0];
      for (let i = 0; i < data.length; i += 4) { sum[0] += data[i]; sum[1] += data[i + 1]; sum[2] += data[i + 2]; }
      return sum.map((s) => s / 25);
    }, png.toString('base64'));
    await p2.close();
    return c;
  };
  for (const r of rects) {
    const cx = r.x + r.w / 2;
    out.push({ bare: await grab(cx, r.y + 0.6 * r.h), peak: await grab(cx, r.y + 0.06 * r.h), cornerPeak: await grab(r.x + 0.06 * r.w, r.y + 0.06 * r.h), tail: await grab(cx, r.y + 0.35 * r.h), profile: await Promise.all([0.03, 0.09, 0.14, 0.2, 0.26, 0.45, 0.8].map((t) => grab(cx, r.y + t * r.h))) });
  }
  await page.context().close();
  return out;
}

const bareSet = await samplePage('off');
const litSet = await samplePage('on');
const weakest = (colour) => {
  let worst = 99;
  let tone = '';
  for (const [name, bg] of TONES) {
    const r = ratio(colour, hex(bg));
    if (r < worst) { worst = r; tone = name; }
  }
  return { value: round2(worst), tone };
};
BACKS.forEach(([wood, backs, label], i) => {
  const bare = weakest(bareSet[i].bare);
  const peak = weakest(litSet[i].peak);
  const tail = weakest(litSet[i].tail);
  const modelBack = bareSet[i].bare;
  results.visibility[label] = {
    bare, peak, tail,
    cornerPeak: weakest(litSet[i].cornerPeak),
    ringMin: litSet[i].profile.map((c) => weakest(c)).reduce((a, b) => (b.value < a.value ? b : a)),
    modelPeak: weakest(blend(modelBack, lampRgb, 0.55 * poolK(wood, backs))),
    modelTail: weakest(blend(modelBack, lampRgb, 0.05 * poolK(wood, backs))),
  };
});

async function cost(file, state, width) {
  const page = await open(browser, switcherUrl(file, state), width, 900, 1);
  await page.waitForSelector('html[data-ready]');
  await growViewport(page, width);
  const data = await page.evaluate(() => {
    const sections = Array.from(document.querySelectorAll('#cabinet .section'));
    let nodes = 0;
    let layers = 0;
    let boxes = 0;
    const count = (el, pseudo) => {
      const bg = getComputedStyle(el, pseudo).backgroundImage;
      if (bg && bg !== 'none') layers += (bg.match(/gradient\(/g) || []).length;
    };
    for (const s of sections) {
      const all = [s, ...s.querySelectorAll('*')];
      nodes += all.length;
      for (const el of all) {
        count(el, null);
        for (const p of ['::before', '::after']) {
          const cs = getComputedStyle(el, p);
          if (cs.content !== 'none' && cs.content !== 'normal' && cs.display !== 'none') { nodes++; count(el, p); }
        }
      }
      boxes += s.querySelectorAll('.placement').length;
    }
    return { sections: sections.length, nodes, gradientLayers: layers, boxes };
  });
  await page.context().close();
  return data;
}

async function frames(file, state) {
  const medians = [];
  for (let run = 0; run < 3; run++) {
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    await page.goto(switcherUrl(file, { ...state, bare: '1' }), { waitUntil: 'load' });
    await page.waitForSelector('html[data-ready]');
    await page.waitForTimeout(300);
    const cdp = await context.newCDPSession(page);
    await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 });
    const m = await page.evaluate(() => new Promise((resolve) => {
      const deltas = [];
      const max = document.documentElement.scrollHeight - innerHeight;
      let last = performance.now();
      const start = last;
      const step = (now) => {
        deltas.push(now - last);
        last = now;
        const t = (now - start) / 2000;
        window.scrollTo(0, Math.min(1, t) * max);
        if (t < 1) requestAnimationFrame(step);
        else {
          deltas.shift();
          deltas.sort((a, b) => a - b);
          resolve({ median: deltas[Math.floor(deltas.length / 2)], p90: deltas[Math.floor(deltas.length * 0.9)], frames: deltas.length });
        }
      };
      requestAnimationFrame(step);
    }));
    medians.push(m);
    await context.close();
  }
  medians.sort((a, b) => a.median - b.median);
  return medians[1];
}

const COST_STATES = [
  ['Classic + warm brown, Shaded, lights Off (today)', { design: 'classic', wood: 'brown' }],
  ['Classic + warm brown, Deep, Always on', { design: 'classic', wood: 'brown', depth: 'deep', lights: 'on' }],
  ['Library + mahogany, Deep, Always on', { design: 'library', wood: 'mahogany', depth: 'deep', lights: 'on' }],
  ['On legs + oak, Deep, Always on', { design: 'legs', wood: 'oak', depth: 'deep', lights: 'on' }],
  ['Plywood on legs + birch, Deep, Always on', { design: 'plywood-legs', wood: 'birch', depth: 'deep', lights: 'on' }],
];
for (const [label, state] of process.env.SKIP_COST ? [] : COST_STATES) {
  results.cost[label] = { desktop400: await cost('switcher-400.html', state, 1440), phone400: await cost('switcher-400-phone.html', state, 390), frames: await frames('switcher-400-phone.html', state) };
  console.log('cost', label, JSON.stringify(results.cost[label]));
}

await browser.close();
writeFileSync(`${S}/round2/measurements2${process.env.SKIP_COST ? '-vis' : ''}.json`, JSON.stringify(results, null, 2));
console.log(JSON.stringify(results.visibility, null, 1));
console.log(JSON.stringify(results.spine, null, 1));
console.log(JSON.stringify(results.chrome, null, 1));
