#!/usr/bin/env node
/**
 * check-linerefs.mjs — 行號對照表校驗
 *
 * 為什麼有這支腳本：`docs/18-work-errors.md` 的 E-03「規劃書行數變了但 docs/ 行號沒重算」
 * 已經犯了三次（2026-09-20 第三次）。依 CLAUDE.md 第 13 條「記了兩次還在犯，
 * 代表要的是機制不是記錄」，改用腳本擋。
 *
 * 檢查三件事：
 *   1. docs/ 宣告的規劃書版本與總行數，與實際檔案是否一致
 *   2. 行號對照表每一列的起始行，是否真的是該章節的標題行
 *   3. 每個行號區間是否合法（起 ≤ 迄 ≤ 檔案總行數）
 *
 * 對應不到章節標題的列會列為「略過」並計數——**略過數變多本身就是警訊**，
 * 代表對照表寫法偏離了可被機器檢查的形式。
 *
 * 用法：node docs/tools/check-linerefs.mjs [--verbose]
 * 離開碼：0 全部通過；1 有不一致
 */
import { readFileSync, readdirSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const DOCS = join(ROOT, 'docs');
const VERBOSE = process.argv.includes('--verbose');

const DASH = /[–—-]/;                       // en dash / em dash / hyphen 都收
const norm = (s) => s
  .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')   // markdown 連結取文字
  .replace(/[*`_~]/g, '')
  .replace(/[（(].*?[）)]/g, '')
  .replace(/[└├│\s]/g, '')
  .replace(/／/g, '/')
  .trim();

// ---------- 讀規劃書 ----------
const specCache = new Map();
function loadSpec(relPath) {
  if (specCache.has(relPath)) return specCache.get(relPath);
  const abs = join(ROOT, relPath);
  const raw = readFileSync(abs, 'utf8');
  const lines = raw.split('\n');
  // 檔案「行數」以 wc -l 的語意為準（結尾換行不算一行）
  const lineCount = raw.endsWith('\n') ? lines.length - 1 : lines.length;
  const version = (raw.match(/\*\*文件版本\*\*：(v[\d.]+)/) || [])[1] || null;

  const headings = [];   // { num, title, line }
  const summaries = [];  // { version, line }
  lines.forEach((l, i) => {
    const h = l.match(/^#{2,4}\s+(\d+(?:\.\d+)*)\.?\s+(.*)$/);
    if (h) headings.push({ num: h[1], title: norm(h[2]), line: i + 1 });
    const s = l.match(/^>\s*\*\*(v[\d.]+)\s*修訂摘要/);
    if (s) summaries.push({ version: s[1], line: i + 1 });
  });
  const spec = { relPath, lineCount, version, headings, summaries };
  specCache.set(relPath, spec);
  return spec;
}

// ---------- 掃 docs/ ----------
let errors = [], warns = [], skipped = 0, checked = 0;

for (const file of readdirSync(DOCS).filter((f) => f.endsWith('.md'))) {
  const docPath = join(DOCS, file);
  const lines = readFileSync(docPath, 'utf8').split('\n');
  let ctx = null;   // 目前這張表對應哪一份規劃書

  lines.forEach((line, idx) => {
    const at = `${file}:${idx + 1}`;

    // (A) 版本與總行數宣告，例：（**v3.10，共 1765 行**）／（..., 共 1814 行）
    const decl = line.match(/\]\((\.\.\/output\/[^)]+\.md)\)[^\n]{0,40}?(?:\*\*)?(v[\d.]+)?[，,]\s*共?\s*(\d+)\s*行/);
    if (decl) {
      const rel = decl[1].replace(/^\.\.\//, '');
      let spec;
      try { spec = loadSpec(rel); } catch { errors.push(`${at} 指向的檔案不存在：${rel}`); return; }
      ctx = spec;
      checked++;
      if (decl[2] && spec.version && decl[2] !== spec.version)
        errors.push(`${at} 版本寫 ${decl[2]}，實際是 ${spec.version}（${rel}）`);
      if (Number(decl[3]) !== spec.lineCount)
        errors.push(`${at} 行數寫 ${decl[3]}，實際是 ${spec.lineCount}（${rel}）`);
      return;
    }

    // (B) 行號對照表的列：| 章節 | 起–迄 | 說明 |
    const row = line.match(/^(?:>\s*)?\|\s*(.+?)\s*\|\s*\*{0,2}(\d+)\s*[–—-]\s*(\d+)\*{0,2}\s*\|/);
    if (!row || !ctx) return;
    const [, labelRaw, aStr, bStr] = row;
    const a = Number(aStr), b = Number(bStr);
    checked++;

    if (a > b) { errors.push(`${at} 區間顛倒：${a}–${b}`); return; }
    if (b > ctx.lineCount) {
      errors.push(`${at} 區間 ${a}–${b} 超出 ${ctx.relPath} 的 ${ctx.lineCount} 行`);
      return;
    }

    // 修訂摘要列
    const sm = labelRaw.match(/(v[\d.]+)\s*修訂摘要/);
    if (sm) {
      const hit = ctx.summaries.find((s) => s.version === sm[1]);
      if (!hit) { errors.push(`${at} 找不到 ${sm[1]} 修訂摘要（${ctx.relPath}）`); return; }
      if (hit.line !== a) errors.push(`${at} ${sm[1]} 修訂摘要寫 ${a}，實際起於 ${hit.line}`);
      return;
    }

    // 章節列：抓開頭的章節編號
    const label = norm(labelRaw);
    const nm = label.match(/^§?(\d+(?:\.\d+)*)\.?(.*)$/);
    if (!nm) { skipped++; if (VERBOSE) warns.push(`${at} 略過（無章節編號）：${labelRaw}`); return; }

    const [, num, restRaw] = nm;
    const rest = restRaw.replace(/^[.．、]/, '');
    const cands = ctx.headings.filter((h) => h.num === num);
    if (cands.length === 0) { skipped++; if (VERBOSE) warns.push(`${at} 略過（規劃書無此章節編號 ${num}）`); return; }

    // 標題要對得上才比對起始行；對不上代表指的是更深一層的小節，無法自動驗證
    const key = rest.slice(0, 3);
    const hit = cands.find((h) => !key || h.title.includes(key) || key.includes(h.title.slice(0, 3)));
    if (!hit) { skipped++; if (VERBOSE) warns.push(`${at} 略過（標題對不上 §${num}）：${labelRaw}`); return; }

    if (hit.line !== a)
      errors.push(`${at} §${num} 寫 ${a}，實際起於 ${hit.line}（${ctx.relPath}）`);
  });
}

// ---------- 輸出 ----------
if (VERBOSE) warns.forEach((w) => console.log(`  · ${w}`));
console.log(`檢查 ${checked} 筆，略過 ${skipped} 筆（對不上章節標題，無法自動驗證）`);
if (errors.length) {
  console.error(`\n❌ ${errors.length} 筆不一致：`);
  errors.forEach((e) => console.error(`  ${e}`));
  console.error(`\n修法：依 E-03 的定案程序**逐節重抓**，不要用全域加法位移。`);
  process.exit(1);
}
console.log('✅ 行號對照表與規劃書一致');
