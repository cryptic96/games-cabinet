import { readFileSync, writeFileSync } from 'node:fs';
import { SHEETS } from './lib.mjs';

/**
 * Lays labelled screenshots out as one sheet. Each item is {file, label, sub}; rows and columns come from "cols". The
 * sheet is drawn in a page canvas, so no image library is needed. Used for look sheets, the pair grid and decision crops.
 */
export async function montage(browser, { items, cols, out, title = '', cell = null, bg = '#1b1b1f', fg = '#eeeeee', gap = 16, pad = 20, labelH = 44, titleH = 48 }) {
  const datas = items.map((i) => readFileSync(i.file.startsWith('/') ? i.file : `${SHEETS}/${i.file}`).toString('base64'));
  const page = await browser.newPage();
  const url = await page.evaluate(async ([its, ds, c, t, cl, background, foreground, g, p, lh, th]) => {
    const imgs = await Promise.all(ds.map((d) => new Promise((resolve) => {
      const i = new Image();
      i.onload = () => resolve(i);
      i.src = 'data:image/png;base64,' + d;
    })));
    const rows = Math.ceil(its.length / c);
    const cw = cl ? cl[0] : Math.max(...imgs.map((i) => i.width));
    const ch = cl ? cl[1] : Math.max(...imgs.map((i) => i.height));
    const W = p * 2 + c * cw + (c - 1) * g;
    const H = p * 2 + (t ? th : 0) + rows * (ch + lh) + (rows - 1) * g;
    const canvas = document.createElement('canvas');
    canvas.width = W;
    canvas.height = H;
    const ctx = canvas.getContext('2d');
    ctx.fillStyle = background;
    ctx.fillRect(0, 0, W, H);
    ctx.fillStyle = foreground;
    ctx.textBaseline = 'top';
    if (t) {
      ctx.font = '600 24px DejaVu Sans, sans-serif';
      ctx.fillText(t, p, p);
    }
    its.forEach((it, k) => {
      const col = k % c;
      const row = Math.floor(k / c);
      const x = p + col * (cw + g);
      const y = p + (t ? th : 0) + row * (ch + lh + g);
      const img = imgs[k];
      const scale = Math.min(cw / img.width, ch / img.height, 1);
      const w = img.width * scale;
      const h = img.height * scale;
      ctx.drawImage(img, x + (cw - w) / 2, y + (ch - h), w, h);
      ctx.fillStyle = foreground;
      ctx.font = '600 15px DejaVu Sans, sans-serif';
      ctx.fillText(it.label || '', x, y + ch + 6);
      if (it.sub) {
        ctx.font = '13px DejaVu Sans, sans-serif';
        ctx.fillStyle = '#b5b5bd';
        ctx.fillText(it.sub, x, y + ch + 25);
      }
    });
    return canvas.toDataURL('image/png');
  }, [items, datas, cols, title, cell, bg, fg, gap, pad, labelH, titleH]);
  await page.close();
  writeFileSync(out.startsWith('/') ? out : `${SHEETS}/${out}`, Buffer.from(url.split(',')[1], 'base64'));
}
