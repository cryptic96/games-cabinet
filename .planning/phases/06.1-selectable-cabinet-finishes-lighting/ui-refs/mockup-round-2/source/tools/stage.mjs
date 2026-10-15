import { writeFileSync } from 'node:fs';
import { OUT } from './lib.mjs';

/**
 * Builds stage pages: grids of small pieces drawn by the real look layers, each cell carrying its own attributes on a
 * wrapper the way the settings preview will. A miniature is one section with a single cubby holding one box. Used for
 * the pair grid, the tile comparison and the wood, back and lamp comparisons of the round.
 */
const BOXES = ['#6a2f7a', '#b8481a', '#17766f', '#23407a', '#e0a82e'];

export function miniature({ empty = false, box = 0, label = '', mm = 230, cubbyW = 360, cubbyH = 200, lamp = true, boxes = null } = {}) {
  const frame = 20;
  const sw = cubbyW + 2 * frame;
  const sh = cubbyH + 2 * frame;
  const bg = BOXES[box % BOXES.length];
  const fg = '#ffffff';
  return `<div class="cabinet" style="--mh:${mm}px"><div class="section" style="--section-w:${sw};--section-h:${sh};--frame:${frame}">
<div class="section-floor" aria-hidden="true"></div>
<div class="section-body"><div class="cubby" data-board="0" data-row-end="true" style="--x:0;--y:0;--w:${cubbyW};--h:${cubbyH}"><div class="cubby-deco" aria-hidden="true"></div>${boxes ? boxes.map((b) => `<button type="button" class="placement" data-kind="cover" data-pattern="plain" style="--x:${b.x};--y:0;--w:${b.w};--h:${b.h};--bg:${b.bg};--fg:${b.fg || '#ffffff'}"><span class="cover-plate"><span class="placement-label">${b.label || ''}</span></span></button>`).join('') : empty ? '' : `<button type="button" class="placement" data-kind="cover" data-pattern="plain" style="--x:30;--y:0;--w:150;--h:150;--bg:${bg};--fg:${fg}"><span class="cover-plate"><span class="placement-label">${label}</span></span></button>`}</div></div>
<div class="section-top" aria-hidden="true"></div><div class="section-trim" aria-hidden="true"></div><div class="section-base" aria-hidden="true"></div>
<div class="section-light" aria-hidden="true"><div class="light-pool" style="--x:0;--y:0;--w:${cubbyW};--h:${cubbyH}"></div></div>
</div></div>`;
}

export function cellHtml(cell, mini) {
  const a = cell.attrs || {};
  const lit = a.lights === 'on' || (a.lights === 'evenings' && a.evening === '1');
  const attrs = Object.entries({ design: 'classic', wood: 'brown', backs: 'wood', lights: 'off', depth: 'shaded', dim: 'off', ...a })
    .filter(([k]) => k !== 'evening')
    .map(([k, v]) => `data-${k}="${v}"`)
    .join(' ');
  const flags = `${a.evening === '1' ? ' data-evening' : ''}${lit ? ' data-lit' : ''}`;
  const style = cell.style ? ` style="${cell.style}"` : '';
  const body = cell.html ?? miniature({ ...(cell.mini || {}), mm: mini });
  return `<figure class="cell"><div class="stage${cell.cls ? ' ' + cell.cls : ''}" ${attrs}${flags}${style}>${body}</div><figcaption><strong>${cell.label ?? ''}</strong><span>${cell.sub ?? ''}</span></figcaption></figure>`;
}

export function writeStage(name, { title = '', cells, cols, mini = 230, cellW = null }) {
  const body = cells.map((c) => cellHtml(c, mini)).join('\n');
  const html = `<!DOCTYPE html>
<html lang="en" data-ready>
<head>
<meta charset="utf-8">
<title>${title}</title>
<link rel="stylesheet" href="layers.css">
<link rel="stylesheet" href="site.css">
<link rel="stylesheet" href="structure.css">
<link rel="stylesheet" href="design.css">
<link rel="stylesheet" href="wood.css">
<link rel="stylesheet" href="modifiers.css">
<link rel="stylesheet" href="stage.css">
</head>
<body class="sheet" style="--cols:${cols};${cellW ? `--cellw:${cellW}px;` : ''}">
${title ? `<h1 class="sheet-title">${title}</h1>` : ''}
<div class="grid">
${body}
</div>
</body>
</html>
`;
  writeFileSync(`${OUT}/${name}.html`, html);
}
