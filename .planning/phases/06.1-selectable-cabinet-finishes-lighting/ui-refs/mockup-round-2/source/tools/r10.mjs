import { writeFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { chromium, S, OUT } from './lib.mjs';

/**
 * Box visibility on the 65 sample, Modern, 1440 px wide, for every back at flat, shaded and deep depth. The boxes are
 * hidden and the back is read from the rendered pixels at the centre line of every cubby; each sample is compared with
 * the weakest of the 12 spine tones after the shade that lies over the top of every box (alpha by depth, fading out over
 * the top fifth of the cubby), which is what a visitor sees at the top edge of a box that stands at that height. Peak is
 * the sample at 6 percent of the cubby height, tail at 35 percent, ring the weakest sample anywhere along the fall.
 */
const TONES = [['Oxblood', '#8a2432'], ['Forest', '#1f5a3a'], ['Navy', '#23407a'], ['Mustard', '#e0a82e'], ['Teal', '#17766f'], ['Plum', '#6a2f7a'], ['Rust', '#b8481a'], ['Sky', '#8fbfe0'], ['Cream', '#efe3c2'], ['Sage', '#a3b88f'], ['Slate', '#434f5e'], ['Rose', '#e09aa5']];
const hex = (h) => [1, 3, 5].map((i) => parseInt(h.slice(i, i + 2), 16));
const lin = (c) => { const s = c / 255; return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4; };
const lum = (rgb) => 0.2126 * lin(rgb[0]) + 0.7152 * lin(rgb[1]) + 0.0722 * lin(rgb[2]);
const ratio = (a, b) => { const x = lum(a); const y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
const blend = (base, over, alpha) => base.map((c, i) => c * (1 - alpha) + over[i] * alpha);
const T = [0.03, 0.06, 0.09, 0.12, 0.15, 0.18, 0.21, 0.24, 0.28, 0.35];
const BACKS = [['brown', 'wood', 'Warm brown'], ['walnut', 'wood', 'Walnut'], ['oak', 'wood', 'Oak'], ['birch', 'wood', 'Birch'], ['mahogany', 'wood', 'Mahogany'], ['brown', 'painted', 'Painted on brown'], ['birch', 'painted', 'Painted on birch']];
const SHADE = { flat: 0, shaded: 0.2, deep: 0.12 };
const browser = await chromium.launch();
const out = {};

async function read(wood, backs, depth, lights, ts) {
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await ctx.newPage();
  const hash = Object.entries({ settle: '0', bare: '1', design: 'modern', wood, backs, depth, lights }).map(([k, v]) => k + '=' + v).join('&');
  await page.goto(pathToFileURL(`${OUT}/switcher.html`).href + '#' + hash);
  await page.waitForSelector('html[data-ready]');
  const h = await page.evaluate(() => document.documentElement.scrollHeight);
  await page.setViewportSize({ width: 1440, height: h });
  await page.addStyleTag({ content: '.placement{visibility:hidden !important}' });
  await page.waitForTimeout(200);
  const cubbies = await page.evaluate(() => Array.from(document.querySelectorAll('#cabinet .cubby')).map((c) => { const r = c.getBoundingClientRect(); return { x: r.x, y: r.y + scrollY, w: r.width, h: r.height }; }));
  const png = await page.screenshot({ fullPage: true });
  const samples = await page.evaluate(async ([d, cs, tt]) => {
    const img = await new Promise((r) => { const i = new Image(); i.onload = () => r(i); i.src = 'data:image/png;base64,' + d; });
    const cv = document.createElement('canvas'); cv.width = img.width; cv.height = img.height; const g = cv.getContext('2d'); g.drawImage(img, 0, 0);
    const grab = (x, y) => { const p = g.getImageData(Math.round(x) - 2, Math.round(y) - 2, 5, 5).data; const s = [0, 0, 0]; for (let i = 0; i < p.length; i += 4) { s[0] += p[i]; s[1] += p[i + 1]; s[2] += p[i + 2]; } return s.map((v) => v / 25); };
    return cs.map((c) => tt.map((t) => grab(c.x + c.w / 2, c.y + t * c.h)));
  }, [png.toString('base64'), cubbies, ts]);
  await ctx.close();
  return samples;
}

function median(samples, k, t, shadeA) {
  const a = t < 0.2 ? shadeA * (1 - t / 0.2) : 0;
  const vals = samples.map((s) => {
    let worst = 99; let tone = '';
    for (const [n, hx] of TONES) { const r = ratio(blend(hex(hx), [20, 10, 4], a), s[k]); if (r < worst) { worst = r; tone = n; } }
    return { worst, tone };
  }).sort((x, y) => x.worst - y.worst);
  const m = vals[Math.floor(vals.length / 2)];
  return { value: Math.round(m.worst * 100) / 100, tone: m.tone };
}

for (const [wood, backs, label] of BACKS) {
  out[label] = {};
  const bareSamples = await read(wood, backs, 'flat', 'off', [0.6]);
  const bare = median(bareSamples, 0, 0.6, 0);
  for (const depth of ['flat', 'shaded', 'deep']) {
    const samples = await read(wood, backs, depth, 'on', T);
    const profile = T.map((t, k) => ({ t, ...median(samples, k, t, SHADE[depth]) }));
    const ring = profile.reduce((a, b) => (b.value < a.value ? b : a));
    out[label][depth] = { bare, peak: profile[1], tail: profile[profile.length - 1], ring };
  }
  console.log(label, JSON.stringify(out[label]));
}
await browser.close();
writeFileSync(`${S}/round2/measurements-r10.json`, JSON.stringify(out, null, 2));
