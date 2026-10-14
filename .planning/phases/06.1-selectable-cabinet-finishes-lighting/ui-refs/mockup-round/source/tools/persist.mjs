/**
 * Copies the round material into the phase folder with machine paths scrubbed. Run from the repository root.
 */
import { cpSync, mkdirSync, readFileSync, writeFileSync, readdirSync, statSync, existsSync, rmSync } from 'node:fs';
import { join, extname } from 'node:path';

const here = process.env.ROUND_SCRATCH;
const target = join(process.cwd(), '.planning/phases/06.1-selectable-cabinet-finishes-lighting/ui-refs/mockup-round');
const source = join(target, 'source');
const sheets = join(target, 'sheets');
const tools = join(source, 'tools');
for (const d of [source, sheets, tools, join(tools, 'css')]) mkdirSync(d, { recursive: true });

const scrub = (text) => text.replace(/'\/tmp\/claude-[^']*'/g, 'process.env.ROUND_SCRATCH');

for (const f of readdirSync(join(here, 'out'))) {
  if (f === 'dusk-block.css') continue;
  cpSync(join(here, 'out', f), join(source, f), { recursive: true });
}

for (const f of readdirSync(here)) {
  const p = join(here, f);
  if (statSync(p).isFile() && ['.mjs', '.js', '.css', '.svg'].includes(extname(f)) && f !== 'structure.backup.css' && f !== 'switcher.js' && f !== 'switcher.css' && f !== 'bgg-dark-standin.svg') {
    writeFileSync(join(tools, f), scrub(readFileSync(p, 'utf8')));
  }
}
for (const f of readdirSync(join(here, 'css'))) {
  writeFileSync(join(tools, 'css', f), readFileSync(join(here, 'css', f), 'utf8'));
}

const skip = /^(c[0-9]|b1-|tmp-|scratch-|t-|t5|t7|q-|quick|baseline-mask-|baseline-(release|layers)-400-)/;
for (const f of readdirSync(join(here, 'sheets'))) {
  if (['.png', '.jpg'].includes(extname(f)) && !skip.test(f) && !f.startsWith('baseline-mask-')) {
    cpSync(join(here, 'sheets', f), join(sheets, f));
  }
}

mkdirSync(join(target, 'measurements'), { recursive: true });
for (const f of ['measurements.json', 'measurements-vis.json', 'invariants.json']) {
  if (existsSync(join(here, f))) cpSync(join(here, f), join(target, 'measurements', f));
}
cpSync(join(here, 'sheets/quick-looks.png'), join(sheets, 'look-overview.png'));
cpSync(join(here, 'README.md'), join(tools, 'README.md'));
const resultsPath = join(here, 'sheets/baseline-results.json');
if (existsSync(resultsPath)) {
  const results = JSON.parse(readFileSync(resultsPath, 'utf8'));
  const main = results.find((r) => r.sample === '65' && r.viewport === 1440);
  const lines = ['sample: 65', 'width: 1440', `differing pixels: ${main.count}`];
  for (const r of results) lines.push(`checked: sample ${r.sample} at ${r.viewport} px wide, differing pixels ${r.count}, largest channel difference ${r.maxDelta ?? 0}`);
  lines.push('what is compared: the cabinet element of the release page and of the switcher page, both drawn by the same browser under the same font setup with the page header hidden so both start at the same position');
  writeFileSync(join(target, 'baseline.txt'), lines.join('\n') + '\n');
}

const offenders = [];
const walk = (d) => {
  for (const f of readdirSync(d)) {
    const p = join(d, f);
    if (statSync(p).isDirectory()) walk(p);
    else if (['.html', '.js', '.mjs', '.css', '.md', '.txt', '.svg', '.json'].includes(extname(f))) {
      if (/\/home\/|\/mnt\/|\/tmp\//.test(readFileSync(p, 'utf8'))) offenders.push(p);
    }
  }
};
walk(target);
if (offenders.length > 0) {
  console.log('PATH LEAKS:', offenders.join('\n'));
  process.exit(1);
}
console.log('persisted without machine paths');
