/**
 * Writes wood.css: the only layer that may hold a colour literal. The base block holds today's warm brown and every
 * shared token; each other wood overrides only what differs. Tokens that paint the furniture are mixed toward the dusk
 * colour by --dusk-pct, which the modifiers layer raises when the room dims, so the dim follows every furniture shape.
 */
import { writeFileSync } from 'node:fs';

const out = process.argv[2];

const dimmable = [
  ['wood', '--wood-src'],
  ['wood-lit', '--wood-lit-src'],
  ['back-plain', '--back-src'],
  ['back-paint', '--back-paint-src'],
  ['recess', '--recess-src'],
  ['recess-low', '--recess-low-src'],
  ['ply-a', '--ply-a-src'],
  ['ply-b', '--ply-b-src'],
  ['ply-glue', '--ply-glue-src'],
  ['inlay', '--inlay-src'],
  ['cubby-top', '--cubby-top-src'],
  ['cubby-left', '--cubby-left-src'],
  ['cubby-right', '--cubby-right-src'],
  ['cubby-floor-back', '--cubby-floor-back-src'],
  ['cubby-floor-front', '--cubby-floor-front-src'],
];

const mix = (name, src) => `  --${name}: color-mix(in srgb, var(${src}), rgb(var(--dusk-rgb)) var(--dusk-pct));`;
const plain = (name, src) => `  --${name}: var(${src});`;

const woods = {
  brown: {},
  walnut: {
    '--wood-src': '#593a29',
    '--wood-lit-src': 'color-mix(in oklch, #593a29, white 22%)',
    '--back-src': '#261811',
    '--recess-src': 'color-mix(in oklch, #593a29, black 55%)',
    '--recess-low-src': 'color-mix(in oklch, #593a29, black 55%)',
    '--cubby-top-src': 'color-mix(in oklch, #593a29, black 78%)',
    '--cubby-left-src': 'color-mix(in oklch, #593a29, black 31%)',
    '--cubby-right-src': 'color-mix(in oklch, #593a29, black 56%)',
    '--cubby-floor-back-src': '#261811',
    '--cubby-floor-front-src': 'color-mix(in oklch, #593a29, black 20%)',
    '--shade-rgb': '8 3 1',
    '--shade-deep-rgb': '5 2 0',
    '--grain-dark-rgb': '18 8 3',
    '--grain-strength': '1.2',
    '--arris-light-rgb': '255 210 160',
    '--arris-dark-rgb': '8 3 1',
    '--room-k': '1',
    '--room': '#d2ccc0',
    '--room-text': '#241e1a',
    '--room-accent': '#2a1a10',
    '--room-lit-alpha': '0.30',
    '--wall-shadow': '0.15',
    '--room-shade-rgb': '60 52 44',
    '--room-shade-deep-rgb': '40 34 28',
    '--floor': '#aaa393',
    '--skirting': '#c4bdaf',
  },
  oak: {
    '--wood-src': '#c9a46d',
    '--wood-lit-src': '#f2e3bd',
    '--back-src': '#221a12',
    '--recess-src': '#2a2218',
    '--recess-low-src': '#2a2218',
    '--cubby-top-src': '#2c2418',
    '--cubby-left-src': '#8b714b',
    '--cubby-right-src': '#584830',
    '--cubby-floor-back-src': '#221a12',
    '--cubby-floor-front-src': '#8d734c',
    '--shade-rgb': '40 22 6',
    '--shade-deep-rgb': '24 12 3',
    '--grain-dark-rgb': '130 88 38',
    '--grain-strength': '1.5',
    '--arris-light-rgb': '255 250 235',
    '--arris-dark-rgb': '95 62 25',
    '--ring-halo': 'var(--ink)',
    '--inlay-src': '#7d5a1e',
    '--arch-alpha-top': '0.60',
    '--arch-alpha-bottom': '0.45',
    '--room-k': '1',
    '--room': '#27323b',
    '--room-text': '#e9eef1',
    '--room-lit-alpha': '0.06',
    '--floor': '#1f2124',
    '--skirting': '#323c44',
  },
  birch: {
    '--wood-src': '#dfc79a',
    '--wood-lit-src': '#fffcf0',
    '--back-src': '#9c8a6a',
    '--recess-src': '#4a4337',
    '--recess-low-src': '#5f574a',
    '--ply-a-src': '#ead7ad',
    '--ply-b-src': '#cdb07c',
    '--ply-glue-src': '#9a7b4b',
    '--cubby-top-src': '#7b6d55',
    '--cubby-left-src': '#c9b38b',
    '--cubby-right-src': '#a79574',
    '--cubby-floor-back-src': '#9c8a6a',
    '--cubby-floor-front-src': '#c4af88',
    '--shade-rgb': '70 45 20',
    '--shade-deep-rgb': '50 32 14',
    '--grain-dark-rgb': '120 80 40',
    '--grain-strength': '0.8',
    '--arris-light-rgb': '255 252 240',
    '--arris-dark-rgb': '90 65 30',
    '--ring-halo': 'var(--ink)',
    '--inlay-src': '#876528',
    '--lamp-k': '0.18',
    '--pool-k': '0.12',
    '--wall-shadow': '0.22',
    '--arch-alpha-top': '0.60',
    '--arch-alpha-bottom': '0.45',
    '--room-shade-rgb': '60 52 44',
    '--room-shade-deep-rgb': '40 34 28',
    '--room-k': '1',
    '--room': '#ddd6ca',
    '--room-text': '#251f1b',
    '--room-accent': '#2a1a10',
    '--room-lit-alpha': '0.30',
    '--floor': '#aa9e8e',
    '--skirting': '#d4cdc0',
  },
  mahogany: {
    '--wood-src': '#5e2b1c',
    '--wood-lit-src': 'color-mix(in srgb, #ffc4a0 38%, #5e2b1c)',
    '--back-src': '#1f0e09',
    '--recess-src': 'color-mix(in oklch, #5e2b1c, black 70%)',
    '--recess-low-src': 'color-mix(in oklch, #5e2b1c, black 70%)',
    '--cubby-top-src': '#130705',
    '--cubby-left-src': '#45200f',
    '--cubby-right-src': '#2e140b',
    '--cubby-floor-back-src': '#1f0e09',
    '--cubby-floor-front-src': '#4d2416',
    '--shade-rgb': '14 3 0',
    '--shade-deep-rgb': '8 1 0',
    '--grain-dark-rgb': '28 6 2',
    '--grain-strength': '1',
    '--arris-light-rgb': '255 196 160',
    '--arris-dark-rgb': '14 3 0',
    '--lamp-k': '1.1',
    '--room-k': '1',
    '--room': '#1c2a23',
    '--room-text': '#ebe4cd',
    '--room-lit-alpha': '0.06',
    '--floor': '#191d16',
    '--skirting': '#333f39',
  },
};

const lines = [];
lines.push('@layer wood {');
lines.push('/**');
lines.push(' * Every wood is a block of colours and unitless strengths and nothing else: no length, no size, no layout. The base');
lines.push(' * block is the warm brown of today and holds every shared token, so a wood only writes what makes it different.');
lines.push(' * Tokens are declared on the element that carries the wood, never on a pair of choices.');
lines.push(' */');
lines.push(':root,\n[data-wood] {');
lines.push('  --dusk-rgb: 10 14 30;');
lines.push('  --dusk-pct: 0%;');
lines.push('  --falloff-rgb: 255 244 228;');
lines.push('  --lamp-rgb: 255 212 150;');
lines.push('  --lamp-k: 1;');
lines.push('  --pool-k: var(--lamp-k);');
lines.push('  --lamp-hot-rgb: 255 236 205;');
lines.push('  --room-shade-rgb: var(--shade-rgb);');
lines.push('  --room-shade-deep-rgb: var(--shade-deep-rgb);');
lines.push('  --highlight-rgb: 255 236 205;');
lines.push('  --box-shade-rgb: 20 10 4;');
lines.push('  --shade-rgb: 20 10 4;');
lines.push('  --shade-deep-rgb: 10 5 2;');
lines.push('  --sheen-rgb: 255 245 230;');
lines.push('  --plank-lift-rgb: 255 235 210;');
lines.push('  --neutral-dark-rgb: 0 0 0;');
lines.push('  --neutral-light-rgb: 255 255 255;');
lines.push('  --arch-top-rgb: 8 4 2;');
lines.push('  --arch-bottom-rgb: 30 16 8;');
lines.push('  --arris-light-rgb: 255 240 220;');
lines.push('  --arris-dark-rgb: 18 8 2;');
lines.push('  --grain-dark-rgb: 40 20 8;');
lines.push('  --grain-strength: 1;');
lines.push('  --wood-src: #6b4226;');
lines.push('  --wood-lit-src: #8c5d38;');
lines.push('  --back-src: #2a1a10;');
lines.push('  --back-paint-src: #0f2025;');
lines.push('  --recess-src: color-mix(in oklch, #6b4226, black 55%);');
lines.push('  --recess-low-src: color-mix(in oklch, #6b4226, black 55%);');
lines.push('  --ply-a-src: color-mix(in oklch, var(--wood-src), white 16%);');
lines.push('  --ply-b-src: color-mix(in oklch, var(--wood-src), black 14%);');
lines.push('  --ply-glue-src: color-mix(in oklch, var(--wood-src), black 60%);');
lines.push('  --inlay-src: #c4a15a;');
lines.push('  --cubby-top-src: #170d07;');
lines.push('  --cubby-left-src: #4a2e1b;');
lines.push('  --cubby-right-src: #2f1c10;');
lines.push('  --cubby-floor-back-src: #2a1a10;');
lines.push('  --cubby-floor-front-src: color-mix(in oklch, #6b4226, black 20%);');
for (const [name, src] of dimmable) lines.push(plain(name, src));
lines.push('  --back: var(--back-plain);');
lines.push('  --edge-up-6: color-mix(in oklch, var(--wood-lit), white 6%);');
lines.push('  --edge-up-12: color-mix(in oklch, var(--wood-lit), white 12%);');
lines.push('  --wood-up-2: color-mix(in oklch, var(--wood), white 2%);');
lines.push('  --wood-up-4: color-mix(in oklch, var(--wood), white 4%);');
lines.push('  --wood-up-6: color-mix(in oklch, var(--wood), white 6%);');
lines.push('  --wood-dn-4: color-mix(in oklch, var(--wood), black 4%);');
lines.push('  --wood-dn-6: color-mix(in oklch, var(--wood), black 6%);');
lines.push('  --wood-dn-22: color-mix(in oklch, var(--wood), black 22%);');
lines.push('  --room: #3b2416;');
lines.push('  --room-text: #efe3c2;');
lines.push('  --room-accent: #d9b98a;');
lines.push('  --room-ring: var(--room-accent);');
lines.push('  --room-muted: color-mix(in srgb, var(--room-text) 70%, var(--room));');
lines.push('  --room-line: color-mix(in srgb, var(--room-text) 60%, var(--room));');
lines.push('  --room-lit-alpha: 0;');
lines.push('  --room-falloff: none;');
lines.push('  --arch-alpha-top: 0.92;');
lines.push('  --arch-alpha-bottom: 0.72;');
lines.push('  --floor: transparent;');
lines.push('  --skirting: transparent;');
lines.push('  --ring-halo: transparent;');
lines.push('  --wall-shadow: 0;');
lines.push('  --room-k: 0;');

lines.push('}');
lines.push('');

for (const [name, tokens] of Object.entries(woods)) {
  if (name === 'brown') continue;
  lines.push(`[data-wood="${name}"] {`);
  for (const [key, value] of Object.entries(tokens)) lines.push(`  ${key}: ${value};`);
  lines.push('  --room-falloff: radial-gradient(ellipse 120% 70% at 50% 0%, rgb(var(--falloff-rgb) / var(--room-lit-alpha)), transparent);');
  lines.push('}');
  lines.push('');
}

lines.push('.section > * {');
lines.push('  --grain-k: var(--grain-strength);');
lines.push('}');
lines.push('');
lines.push('@container (max-width: 400px) {');
lines.push('  .section > * {');
lines.push('    --grain-k: calc(var(--grain-strength) * 0.5);');
lines.push('  }');
lines.push('}');
lines.push('}');

writeFileSync(out, lines.join('\n') + '\n');
const dusk = ['[data-dim="on"][data-lights="evenings"][data-evening] {', ...dimmable.map(([name, src]) => mix(name, src)), '}', ''];
writeFileSync(out.replace('wood.css', 'dusk-block.css'), dusk.join('\n'));
