/**
 * Builds the switcher folder: the four look layers, the page chrome, the saved cabinet markup and the pages. Run from the
 * scratch round folder with the repository path as the only argument.
 */
import { readFileSync, writeFileSync, mkdirSync, cpSync, existsSync, readdirSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repo = process.argv[2] ?? process.cwd();
const out = join(here, 'out');
mkdirSync(join(out, 'img'), { recursive: true });
mkdirSync(join(out, 'markup'), { recursive: true });

const read = (p) => readFileSync(p, 'utf8');
const write = (name, text) => writeFileSync(join(out, name), text);
const wrap = (layer, text) => `@layer ${layer} {\n${text}\n}\n`;

write('layers.css', '@layer structure, design, wood, modifiers;\n');

const cabinet = read(join(repo, 'Cabinet.Service/wwwroot/css/cabinet.css'));
const marker = cabinet.lastIndexOf('/**\n * Classic furniture finish.');
let structure = cabinet.slice(0, marker);
const must = (text, from, to) => {
  if (!text.includes(from)) throw new Error('missing: ' + from.slice(0, 60));
  return text.replace(from, to);
};
structure = must(
  structure,
  '.section-base {\n  left: calc((var(--side-allowance) - var(--side-trim)) * var(--u));\n  top: calc((var(--top-height) + var(--section-h)) * var(--u));\n  width: calc((var(--section-w) + 2 * var(--side-trim)) * var(--u));',
  '.section-base {\n  left: calc((var(--side-allowance) - var(--side-trim) - var(--base-overhang)) * var(--u));\n  top: calc((var(--top-height) + var(--section-h)) * var(--u));\n  width: calc((var(--section-w) + 2 * (var(--side-trim) + var(--base-overhang))) * var(--u));',
);
structure = must(structure, '  pointer-events: none;\n  z-index: 1;\n  background: linear-gradient(to bottom, rgb(var(--shade-rgb)', '  pointer-events: none;\n  z-index: 3;\n  background: linear-gradient(to bottom, rgb(var(--shade-rgb)');
structure = must(structure, '  position: absolute;\n  left: calc(var(--x) * var(--u));\n  bottom: calc(var(--y) * var(--u));', '  position: absolute;\n  z-index: 2;\n  left: calc(var(--x) * var(--u));\n  bottom: calc(var(--y) * var(--u));');
structure = must(structure, '.placement:focus-visible {\n  z-index: 2;', '.placement:focus-visible {\n  z-index: 4;');
structure = must(structure, '.placement[data-open] {\n  z-index: 2;', '.placement[data-open] {\n  z-index: 4;');
structure += read(join(here, 'css/structure-add.css'));
write('structure.css', wrap('structure', structure));

let design = read(join(here, 'css/design-classic.css'));
if (existsSync(join(here, 'css/design-variants.css'))) design += read(join(here, 'css/design-variants.css'));
design += read(join(here, 'css/design-room.css'));
write('design.css', wrap('design', design));

execFileSync('node', [join(here, 'build-wood.mjs'), join(out, 'wood.css')]);
write('modifiers.css', wrap('modifiers', read(join(here, 'css/modifiers.css')) + read(join(out, 'dusk-block.css'))));

let site = read(join(repo, 'Cabinet.Service/wwwroot/css/site.css'));
const cut = site.indexOf('* {\n  box-sizing');
let head = site.slice(0, cut);
let tail = site.slice(cut);
tail = tail.replaceAll('a:focus-visible,\nbutton:focus-visible {\n  outline: 2px solid var(--wood-light);', 'a:focus-visible,\nbutton:focus-visible {\n  outline: 2px solid var(--room-ring, var(--wood-light));');
tail = tail.replaceAll('var(--wall-text)', 'var(--room-text, var(--wall-text))');
tail = tail.replaceAll('var(--chrome-line)', 'var(--room-line, var(--chrome-line))');
tail = tail.replaceAll('var(--chrome-muted)', 'var(--room-muted, var(--chrome-muted))');
tail = tail.replaceAll('var(--wood-dark)', 'var(--room, var(--wood-dark))');
tail = tail.replaceAll('background: var(--wood-light);\n  color: var(--ink);', 'background: var(--room-accent, var(--wood-light));\n  color: var(--room-on-accent, var(--ink));');
write('site.css', head + tail);
cpSync(join(repo, 'Cabinet.Service/wwwroot/css/card.css'), join(out, 'card.css'));

cpSync(join(here, 'switcher.css'), join(out, 'switcher.css'));
cpSync(join(here, 'switcher.js'), join(out, 'switcher.js'));
cpSync(join(repo, 'Cabinet.Service/wwwroot/img/powered-by-bgg.svg'), join(out, 'img/powered-by-bgg.svg'));
cpSync(join(here, 'bgg-dark-standin.svg'), join(out, 'img/bgg-dark-standin.svg'));
for (const f of readdirSync(join(here, 'markup'))) cpSync(join(here, 'markup', f), join(out, 'markup', f));

const groups = [
  ['design', 'Design', [['classic', 'Classic'], ['modern', 'Modern'], ['display', 'Display'], ['legs', 'On legs'], ['library', 'Library'], ['plywood', 'Plywood'], ['plywood-legs', 'Plywood on legs']]],
  ['wood', 'Wood', [['brown', 'Warm brown'], ['walnut', 'Walnut'], ['oak', 'Oak'], ['birch', 'Birch'], ['mahogany', 'Mahogany']]],
  ['backs', 'Backs', [['wood', 'Wood backs'], ['painted', 'Painted backs']]],
  ['lights', 'Lights', [['off', 'Off'], ['on', 'Always on'], ['evenings', 'Evenings only']]],
  ['evening', 'Preview time', [['0', 'Day'], ['1', 'Evening']]],
  ['depth', 'Depth', [['flat', 'Flat'], ['shaded', 'Shaded'], ['deep', 'Deep']]],
  ['dim', 'Room dims in the evening (round choice, mock only)', [['off', 'Lights only'], ['on', 'Room dims']]],
  ['join', 'Phone sections (round choice, mock only)', [['off', 'Separate'], ['on', 'Joined']]],
];
const toolbar = groups
  .map(([key, label, items]) => `<fieldset class="group" data-group="${key}"><legend>${label}</legend>${items.map(([v, t]) => `<button type="button" data-key="${key}" data-value="${v}" aria-pressed="false">${t}</button>`).join('')}</fieldset>`)
  .join('\n');

const pages = [
  ['switcher.html', '65', 'desktop', 'Sample 65, desktop layout'],
  ['switcher-phone.html', '65', 'phone', 'Sample 65, phone layout'],
  ['switcher-400.html', '400', 'desktop', 'Sample 400, desktop layout'],
  ['switcher-400-phone.html', '400', 'phone', 'Sample 400, phone layout'],
];
const nav = pages.map(([f, , , label]) => `<a href="${f}">${label}</a>`).join(' ');

for (const [file, sample, profile] of pages) {
  const cab = read(join(here, `markup/cabinet-${sample}-${profile}.html`));
  let header = read(join(here, `markup/header-${profile === 'phone' ? 'phone' : 'desktop'}.html`));
  header = header.replace(/<nav class="sample-switcher"[\s\S]*?<\/nav>/, '');
  let footer = read(join(here, 'markup/footer.html'));
  footer = footer.replace(/Version [^<]*/, 'Version 1.0.0').replace(/src="[^"]*powered-by-bgg[^"]*"/, 'src="img/powered-by-bgg.svg" id="bgg-logo"');
  const html = `<!DOCTYPE html>
<html lang="en" data-design="classic" data-wood="brown" data-backs="wood" data-lights="off" data-depth="shaded">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Cabinet look switcher</title>
<link rel="stylesheet" href="layers.css">
<link rel="stylesheet" href="site.css">
<link rel="stylesheet" href="structure.css">
<link rel="stylesheet" href="design.css">
<link rel="stylesheet" href="wood.css">
<link rel="stylesheet" href="modifiers.css">
<link rel="stylesheet" href="card.css">
<link rel="stylesheet" href="switcher.css">
</head>
<body>
<aside class="switcher" aria-label="Look switcher">
<p class="switcher-title">Look switcher (mock-up, synthetic collection). ${nav}</p>
${toolbar}
</aside>
${header}
<main>
${cab}
</main>
${footer}
<script src="switcher.js"></script>
</body>
</html>
`;
  write(file, html);
}
console.log('built');
