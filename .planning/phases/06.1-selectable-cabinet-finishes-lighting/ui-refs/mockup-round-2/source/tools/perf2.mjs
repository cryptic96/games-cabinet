import { writeFileSync } from 'node:fs';
import { chromium, open, growViewport, switcherUrl, S } from './lib.mjs';

/**
 * Cost of the Deep depth with the lights on, on the 400 sample at phone width: node and gradient counts, an estimate of
 * the compositor memory of the lit sections, and paint and raster time summed from a performance trace of two seconds
 * of scrolling under six times CPU throttling (instead of frame time, which hits the display cap).
 */
const browser = await chromium.launch();
const STATES = [
  ['Classic + warm brown, Shaded, lights Off (today)', { design: 'classic', wood: 'brown' }],
  ['Classic + warm brown, Deep merged, Always on', { design: 'classic', wood: 'brown', depth: 'deep', deepvar: 'merged', lights: 'on' }],
  ['Classic + warm brown, Deep six layers, Always on', { design: 'classic', wood: 'brown', depth: 'deep', deepvar: 'mitre', lights: 'on' }],
  ['Library + mahogany, Deep merged, Always on', { design: 'library', wood: 'mahogany', depth: 'deep', deepvar: 'merged', lights: 'on' }],
  ['On legs + oak, Deep merged, Always on', { design: 'legs', wood: 'oak', depth: 'deep', deepvar: 'merged', lights: 'on' }],
  ['On legs + oak, Deep six layers, Always on', { design: 'legs', wood: 'oak', depth: 'deep', deepvar: 'mitre', lights: 'on' }],
];

async function counts(state) {
  const page = await open(browser, switcherUrl('switcher-400-phone.html', state), 390, 900, 1);
  await page.waitForSelector('html[data-ready]');
  await growViewport(page, 390);
  const data = await page.evaluate(() => {
    const sections = Array.from(document.querySelectorAll('#cabinet .section'));
    let nodes = 0;
    let layers = 0;
    let lightPx = 0;
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
      const light = s.querySelector('.section-light');
      if (light && getComputedStyle(light).display !== 'none') {
        const r = s.getBoundingClientRect();
        lightPx += r.width * r.height;
      }
    }
    return { sections: sections.length, nodes, gradientLayers: layers, lightLayerCssPx: Math.round(lightPx) };
  });
  await page.context().close();
  data.lightLayerMbAt3x = Math.round((data.lightLayerCssPx * 9 * 4) / 1048576);
  return data;
}

async function trace(state) {
  const runs = [];
  for (let run = 0; run < 3; run++) {
    const context = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2 });
    const page = await context.newPage();
    await page.goto(switcherUrl('switcher-400-phone.html', { ...state, bare: '1' }), { waitUntil: 'load' });
    await page.waitForSelector('html[data-ready]');
    await page.waitForTimeout(300);
    const cdp = await context.newCDPSession(page);
    await cdp.send('Emulation.setCPUThrottlingRate', { rate: 6 });
    await browser.startTracing(page, { categories: ['devtools.timeline', 'disabled-by-default-devtools.timeline', 'cc', 'gpu'] });
    await page.evaluate(() => new Promise((resolve) => {
      const max = document.documentElement.scrollHeight - innerHeight;
      const start = performance.now();
      const step = (now) => {
        const t = (now - start) / 2000;
        window.scrollTo(0, Math.min(1, t) * max);
        if (t < 1) requestAnimationFrame(step);
        else resolve();
      };
      requestAnimationFrame(step);
    }));
    const buf = await browser.stopTracing();
    const events = JSON.parse(buf.toString()).traceEvents;
    const sum = {};
    for (const e of events) {
      if (e.ph === 'X' && e.dur && ['Paint', 'PrePaint', 'RasterTask', 'Layerize', 'UpdateLayer', 'UpdateLayerTree', 'Layout', 'UpdateLayoutTree', 'ImageDecodeTask', 'CompositeLayers', 'Commit'].includes(e.name)) sum[e.name] = (sum[e.name] || 0) + e.dur / 1000;
    }
    runs.push(sum);
    await context.close();
  }
  const keys = Array.from(new Set(runs.flatMap((r) => Object.keys(r))));
  const out = {};
  for (const k of keys) {
    const vals = runs.map((r) => r[k] || 0).sort((a, b) => a - b);
    out[k] = Math.round(vals[1] * 10) / 10;
  }
  return out;
}

const results = {};
for (const [label, state] of STATES) {
  results[label] = { counts: await counts(state), traceMsMedianOf3: await trace(state) };
  console.log(label, JSON.stringify(results[label]));
}
await browser.close();
writeFileSync(`${S}/round2/perf2.json`, JSON.stringify(results, null, 2));
