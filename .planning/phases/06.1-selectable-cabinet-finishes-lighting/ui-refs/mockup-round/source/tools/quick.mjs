import { chromium } from './lib.mjs';
import { shoot } from './shots.mjs';
import { montage } from './montage.mjs';

/**
 * A quick contact sheet of the seven looks, first section only, for checking the designs while they are tuned.
 */
export const LOOKS = [
  ['L1', { design: 'classic', wood: 'brown' }, 'Classic + warm brown'],
  ['L2', { design: 'modern', wood: 'brown' }, 'Modern + warm brown'],
  ['L3', { design: 'display', wood: 'walnut' }, 'Display + walnut'],
  ['L4', { design: 'legs', wood: 'oak' }, 'On legs + oak'],
  ['L5', { design: 'library', wood: 'mahogany' }, 'Library + mahogany'],
  ['L6', { design: 'plywood-legs', wood: 'birch' }, 'Plywood on legs + birch'],
  ['L7', { design: 'plywood', wood: 'birch', backs: 'painted' }, 'Plywood + birch + painted backs'],
];

if (process.argv[1].endsWith('quick.mjs')) {
  const extra = Object.fromEntries(process.argv.slice(2).map((a) => a.split('=')));
  const browser = await chromium.launch();
  const items = [];
  for (const [id, state, label] of LOOKS) {
    const out = `q-${id}.png`;
    await shoot(browser, { ...state, ...extra, sel: '.section', out });
    items.push({ file: out, label: `${id} ${label}` });
  }
  await montage(browser, { items, cols: 4, out: 'quick-looks.png', title: 'Quick look sheet' });
  await browser.close();
}
