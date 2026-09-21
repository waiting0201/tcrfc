#!/usr/bin/env node
// S0-9d ①：HTML 良構性掃描。
//
// 為什麼需要這支工具：瀏覽器的 HTML5 解析器對不良巢狀（孤立結束標籤、標籤未閉合、
// 巢狀錯誤）會依規格「默默修好」，畫面完全正常；但 Vue 的 SFC 模板編譯器不會——
// 它要嘛把內容當純文字跳脫送出（SSR），要嘛直接編譯失敗（client，code 64）。
// 2026-09-20 三頁切片實測就踩到 `site/src/pages/zh/index.html` 尾端孤立的 `</main>`，
// 這支工具就是為了在「搬 80 頁之前」自動抓出同一類問題，不再只能靠人眼看。
//
// 用的是 parse5（WHATWG HTML 解析演算法的忠實實作），但注意：
// parse5 的 `onParseError` 只回報規格明文定義為「parse error」的少數情形
// （例如缺 <!DOCTYPE>、<head> 範圍內的結束標籤不成對），**不會**回報 body 範圍內
// 常見的「孤立結束標籤」「標籤未閉合」——這些在 HTML5 規格裡屬於「有定義的錯誤復原
// 行為」，不算 parse error，但一樣會讓 Vue 編譯器失敗。因此本工具自己比對「解析後的
// DOM 樹」與「原始文字」的落差，用兩個獨立訊號抓出這一類問題（見下方「偵測方法」）。
//
// 偵測方法：
//   A. 標籤未閉合／巢狀錯誤
//      解析後的 DOM 樹，每個元素若有 sourceCodeLocation 但沒有 endTag 位置，
//      代表原始文字裡沒有一個明確對應的結束標籤——不管是因為漏寫、被其他標籤
//      隱性關閉，還是巢狀錯誤導致提前關閉，parse5 都是用「補一個隱性的結束」來
//      復原。排除 HTML5 定義的 void 元素（area/base/br/col/embed/hr/img/input/
//      link/meta/param/source/track/wbr，這些本來就沒有結束標籤）。
//   B. 孤立／多餘的結束標籤
//      在原始文字裡用正規表示式找出所有字面上的 `</tagname>`，扣掉：
//        (a) DOM 樹裡每個元素「真的」被解析器採用的 endTag 位置
//        (b) <script>/<style>/<textarea>/<title> 的文字內容範圍，以及所有註解節點
//            範圍（這些範圍內即使出現看起來像結束標籤的文字，也不是真的標籤）
//      剩下沒被涵蓋到的，就是解析器直接丟棄、瀏覽器不會出錯但 Vue 編譯器會受影響
//      的孤立結束標籤——`zh/index.html` 那個孤立 `</main>` 就是這樣被抓到的。
//   C. <template> 內不得出現的 <style>/<script>（紀律 9，docs/13 §6）
//      找出 <main id="main">（build.mjs 固定包法，見 src/partials/shell.html）
//      內的所有 <style>/<script> 子孫節點——這段內容搬過去就是 Vue SFC 的
//      <template>，紀律 9 明文禁止其中出現這兩種標籤。<main> 範圍外的（例如
//      shell.html 自己的 `<script src=".../site.js">`）屬於未來 layout／app.vue
//      的範圍，不算違規，只在報告的「共用區塊」小節提示。
//
// 用法：
//   node tools/check-wellformed.mjs              人看得懂的報告
//   node tools/check-wellformed.mjs --json        機器可讀（給 CI 用）
//   node tools/check-wellformed.mjs <dist 目錄>    預設 site/dist，可指定其他目錄（例如測試用的暫存複本）
//
// 結束碼：A／B／C 三類只要有任何一筆 → 非 0。「共用區塊」提示不算問題、不影響結束碼。

import { readFile, readdir } from 'node:fs/promises';
import { join, dirname, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import * as parse5 from 'parse5';

const ROOT = dirname(fileURLToPath(import.meta.url));
const SITE_ROOT = dirname(ROOT);

const args = process.argv.slice(2);
const jsonMode = args.includes('--json');
const targetArg = args.find((a) => !a.startsWith('--'));
const DIST = targetArg ? resolve(process.cwd(), targetArg) : join(SITE_ROOT, 'dist');

const VOID_ELEMENTS = new Set([
  'area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input',
  'link', 'meta', 'param', 'source', 'track', 'wbr',
]);
const RAW_TEXT_ELEMENTS = new Set(['script', 'style', 'textarea', 'title']);

async function walk(dir, out = []) {
  for (const e of await readdir(dir, { withFileTypes: true })) {
    const p = join(dir, e.name);
    if (e.isDirectory()) await walk(p, out);
    else if (e.name.endsWith('.html')) out.push(p);
  }
  return out;
}

// offset → 1-indexed 行號／欄號，逐檔快取換行位置避免重複掃描
function makeLineIndex(text) {
  const starts = [0];
  for (let i = 0; i < text.length; i++) {
    if (text[i] === '\n') starts.push(i + 1);
  }
  return (offset) => {
    // 二分搜尋最後一個 <= offset 的行首
    let lo = 0, hi = starts.length - 1;
    while (lo < hi) {
      const mid = (lo + hi + 1) >> 1;
      if (starts[mid] <= offset) lo = mid; else hi = mid - 1;
    }
    return { line: lo + 1, col: offset - starts[lo] + 1 };
  };
}

function hashContent(s) {
  return createHash('sha1').update(s.trim()).digest('hex').slice(0, 10);
}

function walkTree(node, cb) {
  cb(node);
  if (node.childNodes) for (const c of node.childNodes) walkTree(c, cb);
  if (node.content) walkTree(node.content, cb); // <template> 的 content document-fragment
}

function findMain(doc) {
  let found = null;
  walkTree(doc, (n) => {
    if (found) return;
    if (n.tagName === 'main' && (n.attrs || []).some((a) => a.name === 'id' && a.value === 'main')) {
      found = n;
    }
  });
  return found;
}

function analyzeFile(file, html) {
  const rel = relative(DIST, file);
  const toLineCol = makeLineIndex(html);
  const parseErrors = [];
  const doc = parse5.parse(html, {
    sourceCodeLocationInfo: true,
    onParseError: (e) => parseErrors.push(e),
    // 關掉 scripting flag：預設 true 時 HTML5 規格會把 <noscript> 的內容當成
    // RAWTEXT（純文字、不解析成子元素）——這是瀏覽器「有開 JS」時的行為，
    // 但 Vue 編譯器沒有這個特例，一律把 <noscript> 子節點當一般元素解析。
    // 開著預設值會把 <noscript><p><a>...</a></p></noscript> 誤判成一堆孤立標籤。
    scriptingEnabled: false,
  });

  const missingEndTag = [];
  const excludeZones = []; // [start, end) 範圍內的字面 </xxx> 不算孤立標籤
  const consumedEndTags = [];
  const styleScriptInMain = [];

  const mainNode = findMain(doc);
  const mainRange = mainNode?.sourceCodeLocation
    ? [mainNode.sourceCodeLocation.startTag.endOffset, mainNode.sourceCodeLocation.endTag?.startOffset ?? html.length]
    : null;

  // SVG／MathML 是「foreign content」，HTML5 解析規則允許自閉合語法（<path ... />）
  // 真的自我關閉，不是漏寫結束標籤——用原始文字尾端是否為 "/>" 判斷
  const isSelfClosing = (loc) => loc?.startTag && html.slice(loc.startTag.endOffset - 2, loc.startTag.endOffset) === '/>';

  walkTree(doc, (n) => {
    if (!n.tagName) return;
    const loc = n.sourceCodeLocation;
    if (loc?.endTag) {
      consumedEndTags.push([loc.endTag.startOffset, loc.endTag.endOffset]);
    } else if (loc?.startTag && !VOID_ELEMENTS.has(n.tagName) && !isSelfClosing(loc)) {
      const { line, col } = toLineCol(loc.startTag.startOffset);
      missingEndTag.push({ tag: n.tagName, line, col });
    }
    if (RAW_TEXT_ELEMENTS.has(n.tagName) && loc?.startTag && loc?.endTag) {
      excludeZones.push([loc.startTag.endOffset, loc.endTag.startOffset]);
    }
  });
  // 註解節點範圍也排除（parse5 對註解一樣給 sourceCodeLocation）
  walkTree(doc, (n) => {
    if (n.nodeName === '#comment' && n.sourceCodeLocation) {
      excludeZones.push([n.sourceCodeLocation.startOffset, n.sourceCodeLocation.endOffset]);
    }
  });

  if (mainNode) {
    walkTree(mainNode, (n) => {
      if (n === mainNode) return;
      if (n.tagName === 'style' || n.tagName === 'script') {
        const loc = n.sourceCodeLocation;
        const { line, col } = loc ? toLineCol(loc.startTag?.startOffset ?? loc.startOffset) : { line: 0, col: 0 };
        const text = (n.childNodes || []).map((c) => c.value ?? '').join('');
        styleScriptInMain.push({ tag: n.tagName, line, col, hash: hashContent(text), lines: text.split('\n').length });
      }
    });
  }

  // 共用區塊（<main> 之外）的 style/script，僅供提示，不算違規
  const sharedStyleScript = [];
  walkTree(doc, (n) => {
    if ((n.tagName === 'style' || n.tagName === 'script') && (!mainNode || !isDescendant(mainNode, n))) {
      const loc = n.sourceCodeLocation;
      const { line } = loc ? toLineCol(loc.startTag?.startOffset ?? loc.startOffset ?? 0) : { line: 0 };
      sharedStyleScript.push({ tag: n.tagName, line });
    }
  });

  // 孤立／多餘的結束標籤：字面上的 </tag> 減去真的被採用的 endTag 與排除區
  const orphanEndTags = [];
  const endTagRe = /<\/([a-zA-Z][a-zA-Z0-9-]*)\s*>/g;
  let m;
  while ((m = endTagRe.exec(html))) {
    const start = m.index;
    const end = m.index + m[0].length;
    const inConsumed = consumedEndTags.some(([s, e]) => start >= s && end <= e);
    if (inConsumed) continue;
    const inExcluded = excludeZones.some(([s, e]) => start >= s && end < e);
    if (inExcluded) continue;
    const { line, col } = toLineCol(start);
    orphanEndTags.push({ tag: m[1], line, col });
  }

  return {
    rel,
    parseErrors: parseErrors.map((e) => {
      const { line, col } = 'startLine' in e ? { line: e.startLine, col: e.startCol } : toLineCol(0);
      return { code: e.code, line, col };
    }),
    missingEndTag,
    orphanEndTags,
    styleScriptInMain,
    sharedStyleScript,
    hasMain: !!mainNode,
  };
}

function isDescendant(ancestor, node) {
  let found = false;
  walkTree(ancestor, (n) => { if (n === node) found = true; });
  return found;
}

const PARSE_ERROR_LABEL = {
  'missing-doctype': '缺少 <!DOCTYPE html>',
  'duplicate-attribute': '同一標籤重複屬性',
  'non-void-html-element-start-tag-with-trailing-solidus': '非 void 元素的開始標籤多了自閉合斜線',
};

async function main() {
  const files = await walk(DIST);
  files.sort();
  const results = files.map((f) => ({ file: f }));

  for (const r of results) {
    r.html = await readFile(r.file, 'utf8');
  }

  const analyses = results.map((r) => analyzeFile(r.file, r.html));

  // 全站去重：<main> 內的 style / script
  const scriptGroups = new Map(); // hash -> { pages: [], lines }
  const styleGroups = new Map();
  for (const a of analyses) {
    for (const s of a.styleScriptInMain) {
      const map = s.tag === 'script' ? scriptGroups : styleGroups;
      if (!map.has(s.hash)) map.set(s.hash, { pages: [], lines: s.lines });
      map.get(s.hash).pages.push(a.rel);
    }
  }

  const problemFiles = analyses.filter(
    (a) => a.parseErrors.length || a.missingEndTag.length || a.orphanEndTags.length || a.styleScriptInMain.length || !a.hasMain
  );

  const totalIssues = problemFiles.reduce(
    (n, a) => n + a.parseErrors.length + a.missingEndTag.length + a.orphanEndTags.length + a.styleScriptInMain.length + (a.hasMain ? 0 : 1),
    0
  );

  if (jsonMode) {
    console.log(JSON.stringify({
      scanned: analyses.length,
      totalIssues,
      files: problemFiles,
      dedup: {
        scriptsInMain: [...scriptGroups.entries()].map(([hash, v]) => ({ hash, ...v })),
        stylesInMain: [...styleGroups.entries()].map(([hash, v]) => ({ hash, ...v })),
      },
    }, null, 2));
    process.exit(totalIssues ? 1 : 0);
  }

  console.log(`掃描 ${analyses.length} 個頁面（${relative(process.cwd(), DIST)}）\n`);

  if (!problemFiles.length) {
    console.log('✓ 沒有發現良構性問題');
  } else {
    for (const a of problemFiles) {
      console.log(`  ${a.rel}`);
      for (const e of a.parseErrors) {
        console.log(`    ✗ [解析錯誤] ${PARSE_ERROR_LABEL[e.code] ?? e.code}（第 ${e.line} 行第 ${e.col} 欄）`);
      }
      for (const t of a.missingEndTag) {
        console.log(`    ✗ [標籤未閉合] <${t.tag}> 沒有對應的結束標籤（第 ${t.line} 行第 ${t.col} 欄）——瀏覽器會隱性補上，Vue 編譯器可能不會`);
      }
      for (const t of a.orphanEndTags) {
        console.log(`    ✗ [孤立結束標籤] </${t.tag}>（第 ${t.line} 行第 ${t.col} 欄）——找不到對應的開始標籤，瀏覽器會默默忽略，Vue 編譯器會編譯失敗或產生錯誤的樹`);
      }
      for (const t of a.styleScriptInMain) {
        console.log(`    ✗ [template 內禁用標籤] <${t.tag}>（第 ${t.line} 行第 ${t.col} 欄）——落在 <main id="main"> 內，未來會進 Vue <template>，違反紀律 9（docs/13 §6）`);
      }
      if (!a.hasMain) {
        console.log(`    ✗ [結構] 找不到 <main id="main">，無法界定 template 範圍`);
      }
    }
    console.log(`\n共 ${totalIssues} 個問題，分布在 ${problemFiles.length} 個檔案`);
  }

  const sharedFiles = analyses.filter((a) => a.sharedStyleScript.length);
  if (sharedFiles.length) {
    console.log(`\n共用區塊（<main> 之外）的 <style>/<script>，屬於未來 layout／app.vue 範圍，非本次違規對象：`);
    for (const a of sharedFiles) {
      for (const s of a.sharedStyleScript) {
        console.log(`  ${a.rel} 第 ${s.line} 行：<${s.tag}>`);
      }
    }
  }

  console.log(`\n<main> 內 <script> 去重後：${scriptGroups.size} 種不同內容（共 ${[...scriptGroups.values()].reduce((n, v) => n + v.pages.length, 0)} 頁次使用）`);
  for (const [hash, v] of scriptGroups) {
    console.log(`  ${hash}（${v.lines} 行）：${v.pages.length} 頁 — ${v.pages.slice(0, 3).join('、')}${v.pages.length > 3 ? ' 等' : ''}`);
  }
  console.log(`<main> 內 <style> 去重後：${styleGroups.size} 種不同內容（共 ${[...styleGroups.values()].reduce((n, v) => n + v.pages.length, 0)} 頁次使用）`);

  process.exit(totalIssues ? 1 : 0);
}

main().catch((e) => {
  console.error('掃描失敗：', e.stack ?? e.message);
  process.exit(1);
});
