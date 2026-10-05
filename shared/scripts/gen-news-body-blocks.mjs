#!/usr/bin/env node
// shared/scripts/gen-news-body-blocks.mjs — 新聞內文 `bodyJson` 區塊型別與別名清單（Android 缺口 D2），從程式來源解析，兩端 App 與官網共用同一份。
// 來源（不發明規格）：apps/web/app/utils/news-body.ts 的 `blockOf()`（目前唯一解讀 bodyJson 的實作）。
// 後端（apps/api）對 bodyJson 只做「純文字存成 {"text":"…"}、讀回還原；物件／陣列原樣保存原樣回傳」，**不解讀區塊型別**——
// 所以型別清單的真實來源是官網這支解析器；官網新增或改名區塊型別後必須重跑本產生器（CI 的 shared-contract job 會因漂移而失敗）。
// 用法：node gen-news-body-blocks.mjs

import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, '../..');
const src = readFileSync(join(repoRoot, 'apps/web/app/utils/news-body.ts'), 'utf8');

const fn = src.match(/function blockOf\([\s\S]*?\n}\n/)?.[0];
if (!fn) throw new Error('news-body.ts 找不到 blockOf()（檔案被改動？）');
const sw = fn.match(/switch \(type\) \{([\s\S]*?)\n  }\n}/)?.[1] ?? fn.match(/switch \(type\) \{([\s\S]*)\}/)?.[1];
if (!sw) throw new Error('blockOf() 找不到 switch (type)');

// 一組連續的 case 標籤共用同一段處理；處理段裡出現的 `kind: 'xxx'` 就是它輸出的區塊種類。
const groups = [];
let labels = [];
let body = [];
for (const line of sw.split('\n')) {
  const m = line.match(/^\s*case '([^']+)':\s*(\{)?\s*$/);
  if (m) {
    if (body.length > 0 && labels.length > 0) {
      groups.push({ labels, body: body.join('\n') });
      labels = [];
    }
    if (labels.length === 0) body = [];
    labels.push(m[1]);
  } else if (/^\s*default:/.test(line)) {
    if (labels.length) groups.push({ labels, body: body.join('\n') });
    labels = [];
    body = [];
    break;
  } else {
    body.push(line);
  }
}
const blocks = {};
for (const g of groups) {
  const kind = g.body.match(/kind: '([a-z]+)'/)?.[1] ?? (/plainTextToBlocks/.test(g.body) ? 'p' : undefined); // 段落由 plainTextToBlocks 產生
  if (!kind) throw new Error(`區塊 ${g.labels.join('/')} 解析不到輸出種類`);
  const level = g.body.match(/level: ([23])/)?.[1];
  const entry = (blocks[kind] ??= { type: [], aliases: {} });
  for (const t of g.labels) {
    entry.type.push(t);
    if (level) entry.aliases[t] = { level: Number(level) };
  }
}
for (const e of Object.values(blocks)) e.type.sort();

const FIELDS = {
  p: { text: 'string（空行分段，段內單一換行保留為斷行）' },
  heading: { text: 'string（必填，空白不輸出）' },
  quote: { text: 'string（必填）', 'cite|source': 'string（選填，出處）' },
  list: { items: 'string[]（空項略過）', ordered: 'boolean（選填；type 為 ol 時恆為有序）' },
  image: { 'src|url': 'string（必填，須通過安全網址檢查）', alt: 'string（缺漏退回 caption）', caption: 'string（選填）' },
  gallery: { images: '{src|url, alt?, caption?}[]（沒有任何合法圖片則不輸出）' },
};
for (const [kind, e] of Object.entries(blocks)) {
  if (!FIELDS[kind]) throw new Error(`新增了區塊種類 ${kind}，請在 gen-news-body-blocks.mjs 的 FIELDS 補欄位說明`);
  e.fields = FIELDS[kind];
}
const kinds = Object.keys(blocks);
if (kinds.length < 6) throw new Error(`只解析到 ${kinds.length} 種區塊（預期 6 種以上），解析器可能壞了`);

const urlPolicy = src.match(/function safeUrl[\s\S]*?\n}\n/)?.[0] ?? '';
const doc = {
  $comment:
    '自動產生，請勿手改。產生器：shared/scripts/gen-news-body-blocks.mjs。來源是 apps/web/app/utils/news-body.ts 的 blockOf()（官網目前唯一解讀 bodyJson 的實作），不是規格；後端對 bodyJson 不解讀區塊型別。',
  field: 'ArticleDetailDto.bodyJson（字串；可能是純文字，也可能是 JSON 文字）',
  backendContract:
    '後台目前送純文字：後端存成 {"text":"…"}，公開端點讀回「還原後的原文字」。物件／陣列原樣保存、原樣回傳（為區塊編輯器預留）。後端不驗證也不解讀區塊型別。',
  parseOrder: [
    'null 或空白 → 沒有內文區塊',
    '不是以 { 或 [ 開頭，或 JSON.parse 失敗 → 整段當純文字，空行分段',
    '陣列 → 逐項當區塊解讀',
    '物件：有 blocks（陣列）或 content（陣列）→ 逐項當區塊；否則把這個物件本身當單一區塊',
    '區塊若本身是字串 → 當純文字',
  ],
  blockKinds: blocks,
  type: '區塊 `type` 不分大小寫（比對前轉小寫）。每種區塊的 `type` 清單含別名；`aliases` 標示別名帶來的差異（標題層級）。',
  unknownTypeConvention:
    '未知 type 的區塊一律**忽略**（不渲染、不報錯、不中止整篇）；沒有 type 但有 text 的物件當純文字段落；區塊解讀結果為空的也忽略。用戶端不得猜測或自創格式。',
  safeUrl: {
    rule: '圖片網址只放行 http(s):// 絕對網址與站內相對路徑（以單一 / 開頭，不得 // 或 /\\ 開頭）；其餘（javascript:、data:、vbscript:、protocol-relative）整個圖片區塊丟棄。',
    sourceFound: urlPolicy.length > 0,
  },
  rendering: '一律當文字渲染（自動跳脫），禁止當 HTML／富文本解析（XSS 防線）；網址只在通過上面的檢查後才可用於圖片來源。',
};

writeFileSync(join(repoRoot, 'shared/news-body-blocks.json'), `${JSON.stringify(doc, null, 2)}\n`);
console.log(`news-body-blocks: ${kinds.join('／')} → shared/news-body-blocks.json`);
