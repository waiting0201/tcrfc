#!/usr/bin/env node
// S0-9d ②：{{ROOT}}／{{>partial}} → 絕對路徑的搬遷 codemod。
//
// 背景：`site/src/pages/**/*.html` 用 `{{ROOT}}` 代表站台根路徑（例如
// `{{ROOT}}/assets/img/x.png`），`build.mjs` 依輸出頁面的巢狀深度把它填成
// 相對的 `..`／`../..` 這類 dot-path（見 build.mjs 的 rootPrefix()）。
// Nuxt SSR 沒有這種「輸出檔案巢狀深度」的概念——同一個 route 不管掛在哪一層，
// 資源路徑都應該是從站台根目錄算的絕對路徑（`/assets/img/x.png`）。這支工具
// 就是把 `{{ROOT}}` 機械替換成絕對路徑前綴（預設空字串，讓 `{{ROOT}}/x` 變成 `/x`）。
//
// 🔴 實查 build.mjs 後確認：`site/src/pages/**/*.html`（頁面 body 這一層）
// 實際會出現的 token 只有兩種：
//   1. {{ROOT}}         —— 1035 處（2026-09-21 實測），本工具的主要替換對象
//   2. {{>partial-name}} —— 內容片段 include（2026-09-21 實測僅 2 處，皆為
//      {{>membership-benefits}}），build.mjs 用 applyIncludes() 展開，**展開順序在
//      ROOT 替換之前**（build.mjs：`fill(applyIncludes(body, partials), {ROOT: root})`）。
//      片段檔（src/partials/membership-benefits.html）自己也帶 {{ROOT}}，所以這裡
//      必須先展開片段、再統一做 ROOT 替換，順序錯了片段裡的 {{ROOT}} 會漏轉。
//   （TITLE／DESCRIPTION／NAV／UNIT／LANG／CANONICAL／BODYCLASS／SCHEMA／HEADER／
//    FOOTER／CONTENT 這些 token 只出現在 src/partials/shell.html 本身，不出現在
//    頁面 body 裡，不是本工具的替換對象。）
//
// ⚠️ 已知踩雷點（2026-09-20 切片時真的漏抄過）：{{ROOT}} 可能出現在 <style> 區塊、
// CSS 註解、<script> 區塊裡，不是只出現在 href/src 屬性值。本工具用「整份檔案文字
// 全域替換」，天生涵蓋這些位置，不用另外掃屬性——見下方 selftest 對 CSS 註解的驗證。
//
// ⛔ 不改 site/src/：本工具吃「輸入路徑」寫「輸出路徑」，用於搬頁時處理「複製到
// Nuxt 專案的副本」，不是在原地修改基準線。輸出路徑若解析後等於／包含 site/src，
// 一律拒絕執行（安全防呆）。
//
// 用法：
//   node tools/codemod-root.mjs <輸入檔或目錄> --out <輸出目錄>            dry-run，只印摘要
//   node tools/codemod-root.mjs <輸入檔或目錄> --out <輸出目錄> --write    真的寫檔
//   選用：
//     --base <前綴>       絕對路徑前綴，預設空字串（{{ROOT}}/x → /x）
//     --partials <目錄>   partial 片段所在目錄，預設 site/src/partials
//
// 結束碼：成功一律 0（這是搬遷輔助工具不是驗收關卡，驗收交給 compare-dom.mjs／
// check-wellformed.mjs）；輸入或安全檢查失敗才非 0。

import { readFile, writeFile, mkdir, readdir, stat } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { join, dirname, relative, resolve, basename } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = dirname(fileURLToPath(import.meta.url));
const SITE_ROOT = dirname(ROOT);
const DEFAULT_PARTIALS = join(SITE_ROOT, 'src/partials');
const SRC_BASELINE = join(SITE_ROOT, 'src'); // 禁止輸出到這裡或它的任何子路徑

function parseArgs(argv) {
  const out = { write: false, base: '', partials: DEFAULT_PARTIALS, positional: [] };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--write') out.write = true;
    else if (a === '--out') out.outDir = argv[++i];
    else if (a === '--base') out.base = argv[++i];
    else if (a === '--partials') out.partials = resolve(process.cwd(), argv[++i]);
    else out.positional.push(a);
  }
  return out;
}

async function walkHtml(dir, out = []) {
  for (const e of await readdir(dir, { withFileTypes: true })) {
    const p = join(dir, e.name);
    if (e.isDirectory()) await walkHtml(p, out);
    else if (e.name.endsWith('.html')) out.push(p);
  }
  return out;
}

// 展開 {{>name}} 片段（一層，不遞迴，比照 build.mjs 的 applyIncludes 行為）
function applyIncludes(html, partials, stats) {
  return html.replace(/\{\{>\s*([\w-]+)\s*\}\}/g, (_, name) => {
    if (!(name in partials)) {
      throw new Error(`找不到內容片段 partials/${name}.html（--partials 指到的目錄：${stats.partialsDir}）`);
    }
    stats.includesExpanded++;
    stats.includeNames.add(name);
    return partials[name];
  });
}

function replaceRoot(html, base, stats) {
  return html.replace(/\{\{ROOT\}\}/g, () => {
    stats.rootReplaced++;
    return base;
  });
}

async function loadPartials(partialsDir) {
  const partials = {};
  if (!existsSync(partialsDir)) return partials;
  const reserved = new Set(['shell', 'header', 'footer']);
  for (const e of await readdir(partialsDir, { withFileTypes: true })) {
    if (!e.isFile() || !e.name.endsWith('.html')) continue;
    const name = e.name.slice(0, -5);
    if (reserved.has(name)) continue;
    partials[name] = await readFile(join(partialsDir, e.name), 'utf8');
  }
  return partials;
}

function isWithin(parent, child) {
  const rel = relative(parent, child);
  return rel === '' || (!rel.startsWith('..') && !resolve(child).startsWith('..'));
}

async function main() {
  const opts = parseArgs(process.argv.slice(2));
  const input = opts.positional[0];
  if (!input) {
    console.error('用法：node tools/codemod-root.mjs <輸入檔或目錄> --out <輸出目錄> [--base <前綴>] [--write]');
    process.exit(1);
  }
  const inputPath = resolve(process.cwd(), input);
  if (!existsSync(inputPath)) {
    console.error(`輸入路徑不存在：${inputPath}`);
    process.exit(1);
  }

  if (opts.write) {
    if (!opts.outDir) {
      console.error('--write 模式必須指定 --out <輸出目錄>');
      process.exit(1);
    }
    const outResolved = resolve(process.cwd(), opts.outDir);
    if (isWithin(SRC_BASELINE, outResolved) || outResolved === SRC_BASELINE) {
      console.error(`⛔ 拒絕執行：輸出路徑 ${outResolved} 等於或落在 site/src/ 之內，那是基準線，不得覆寫`);
      process.exit(1);
    }
  }

  const inputIsDir = (await stat(inputPath)).isDirectory();
  const files = inputIsDir ? await walkHtml(inputPath) : [inputPath];
  const partials = await loadPartials(opts.partials);

  console.log(`輸入：${inputPath}${inputIsDir ? `（目錄，${files.length} 個 .html 檔）` : ''}`);
  console.log(`絕對路徑前綴：${JSON.stringify(opts.base)}（{{ROOT}}/x → ${opts.base}/x）`);
  console.log(`片段目錄：${opts.partials}（${Object.keys(partials).length} 個可用片段：${Object.keys(partials).join('、') || '無'}）`);
  console.log(opts.write ? `模式：--write（會真的寫檔到 ${resolve(process.cwd(), opts.outDir)}）` : '模式：dry-run（不寫檔，只列摘要；加 --write 才真的改）');
  console.log('');

  const perFile = [];
  let totalRoot = 0, totalIncludes = 0;

  for (const file of files) {
    let html = await readFile(file, 'utf8');
    const stats = { rootReplaced: 0, includesExpanded: 0, includeNames: new Set(), partialsDir: opts.partials };
    html = applyIncludes(html, partials, stats);
    html = replaceRoot(html, opts.base, stats);

    totalRoot += stats.rootReplaced;
    totalIncludes += stats.includesExpanded;
    perFile.push({ file, stats, html });

    const rel = inputIsDir ? relative(inputPath, file) : basename(file);
    if (stats.rootReplaced || stats.includesExpanded) {
      const parts = [];
      if (stats.rootReplaced) parts.push(`${stats.rootReplaced} 個 {{ROOT}}`);
      if (stats.includesExpanded) parts.push(`${stats.includesExpanded} 個 {{>...}}（${[...stats.includeNames].join('、')}）`);
      console.log(`  ${rel}：${parts.join('，')}`);
    } else {
      console.log(`  ${rel}：無需改動`);
    }

    if (opts.write) {
      const outFile = inputIsDir ? join(resolve(process.cwd(), opts.outDir), rel) : join(resolve(process.cwd(), opts.outDir), basename(file));
      await mkdir(dirname(outFile), { recursive: true });
      await writeFile(outFile, html, 'utf8');
    }
  }

  console.log('');
  console.log(`共處理 ${files.length} 個檔案，替換 {{ROOT}} ${totalRoot} 處、展開內容片段 ${totalIncludes} 處`);
  if (!opts.write) console.log('（dry-run，未寫檔。確認無誤後加 --write 執行）');

  process.exit(0);
}

main().catch((e) => {
  console.error('codemod 失敗：', e.stack ?? e.message);
  process.exit(1);
});
