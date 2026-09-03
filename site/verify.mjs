#!/usr/bin/env node
// 建置後自檢：抓出 token 殘留、h1 數量、缺 alt、寫死色碼、站內斷鏈，以及統計 .pending。
import { readFile, readdir, stat } from 'node:fs/promises';
import { join, dirname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = dirname(fileURLToPath(import.meta.url));
const DIST = join(ROOT, 'dist');
const SRC = join(ROOT, 'src/pages');

async function walk(dir, out = []) {
  for (const e of await readdir(dir, { withFileTypes: true })) {
    const p = join(dir, e.name);
    if (e.isDirectory()) await walk(p, out);
    else if (e.name.endsWith('.html')) out.push(p);
  }
  return out;
}

const problems = [];
let pendingTotal = 0;

const files = await walk(DIST);
for (const f of files) {
  const rel = relative(DIST, f);
  const html = await readFile(f, 'utf8');

  if (html.includes('{{')) problems.push([rel, '殘留未代換的 {{token}}']);

  const isRedirect = /location\.replace/.test(html);
  const h1 = (html.match(/<h1[\s>]/g) || []).length;
  if (h1 === 0 && !isRedirect) problems.push([rel, '缺少 <h1>']);
  if (h1 > 1) problems.push([rel, `有 ${h1} 個 <h1>，應該只有 1 個`]);

  const imgs = html.match(/<img\b[^>]*>/g) || [];
  const noAlt = imgs.filter((t) => !/\salt=/.test(t)).length;
  if (noAlt) problems.push([rel, `${noAlt} 個 <img> 缺 alt`]);
  const noDim = imgs.filter((t) => !/\swidth=/.test(t) || !/\sheight=/.test(t)).length;
  if (noDim) problems.push([rel, `${noDim} 個 <img> 缺 width/height`]);

  // 寫死品牌色（應改用 CSS 變數）
  // theme-color meta 與 <code> 內的色碼（品牌規範說明）屬正當用途，排除
  const body = html
    .replace(/<meta name="theme-color"[^>]*>/gi, '')
    .replace(/<code>[\s\S]*?<\/code>/gi, '');
  const hard = body.match(/#(E0218A|231916|D61E83)/gi) || [];
  if (hard.length) problems.push([rel, `${hard.length} 處寫死品牌色碼，應改用 var(--brand) 等`]);

  const desc = html.match(/<meta name="description" content="([^"]*)"/);
  if (!isRedirect && (!desc || desc[1].trim().length < 20)) problems.push([rel, 'meta description 太短或缺漏']);

  pendingTotal += (html.match(/class="pending"/g) || []).length;
}

// 站內斷鏈：href 指到 dist 裡不存在的檔案（header/footer 的連結會複製到每一頁，
// 一個錯字就是 72 頁同時壞掉，所以這裡用「斷鏈種類」而不是出現次數來回報）
const exists = async (p) => { try { await stat(p); return true; } catch { return false; } };
const brokenLinks = new Map();
for (const f of files) {
  const html = await readFile(f, 'utf8');
  for (const m of html.matchAll(/href="([^"]*)"/g)) {
    const href = m[1];
    if (!href || /^(https?:|mailto:|tel:|#|data:|javascript:)/i.test(href)) continue;
    const path = href.split('#')[0].split('?')[0];
    if (!path) continue;
    const target = join(dirname(f), path);
    if (await exists(target) || await exists(join(target, 'index.html'))) continue;
    if (!brokenLinks.has(href)) brokenLinks.set(href, []);
    brokenLinks.get(href).push(relative(DIST, f));
  }
}
for (const [href, pages] of brokenLinks) {
  const where = pages.length > 3 ? `${pages[0]} 等 ${pages.length} 頁` : pages.join('、');
  problems.push([where.split(' ')[0], `站內斷鏈 ${href}（共 ${pages.length} 頁）`]);
}

// Lorem / 佔位廢話偵測
const srcFiles = await walk(SRC);
const BAD = /lorem ipsum|這裡是介紹文字|範例文字|placeholder text|待填寫內容/i;
for (const f of srcFiles) {
  const html = await readFile(f, 'utf8');
  if (BAD.test(html)) problems.push([relative(SRC, f), '疑似佔位廢話，應改用 .pending 標記']);
}

console.log(`檢查 ${files.length} 個頁面\n`);
if (problems.length) {
  const byFile = new Map();
  for (const [f, msg] of problems) {
    if (!byFile.has(f)) byFile.set(f, []);
    byFile.get(f).push(msg);
  }
  for (const [f, msgs] of [...byFile].sort()) {
    console.log(`  ${f}`);
    for (const m of msgs) console.log(`    ✗ ${m}`);
  }
  console.log(`\n共 ${problems.length} 個問題，分布在 ${byFile.size} 個檔案`);
} else {
  console.log('✓ 沒有發現問題');
}
console.log(`\n待補內容標記（.pending）：${pendingTotal} 處 —— 上線前必須清空`);
process.exit(problems.length ? 1 : 0);
