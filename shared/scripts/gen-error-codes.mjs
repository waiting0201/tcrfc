#!/usr/bin/env node
// shared/scripts/gen-error-codes.mjs — 從 apps/api 原始碼掃出「現行 API 實際會回的機器可讀錯誤代碼」，寫入 shared/error-codes.json。
//
// 資料來源（不發明規格）：
//   - apps/api/Common/MemberExceptions.cs 的 ICodedApiException 一族（會員／付款／報名等公開端點）。
//     ApiExceptionHandler 把它們轉成 RFC 7807 ProblemDetails，並把 Code 放進擴充欄位 `code`。
//   - 其餘例外（後台一族、AdminValidationException 等）目前**沒有**機器可讀代碼，只有 HTTP 狀態與標題；
//     它們不在 codes 清單，而是在 shared/error-codes.json 的 `uncodedFamilies` 說明。
//
// App 規劃書 §9.5 要求的「統一錯誤結構：錯誤代碼、雙語訊息、是否可重試」已於 2026-10-05 在後端完成（相容擴充）：
// 所有 ProblemDetails 都帶 code／messageZh／messageEn／retryable，見 apps/api/Common/ApiErrorEnvelope.cs。
// 本產生器另解析 apps/api/Common/ApiErrorMessages.cs（代碼 → 英文訊息）與 ApiErrorEnvelope.cs（狀態 → 通用代碼），
// 把每個代碼的英文訊息與「依狀態的通用代碼」寫進 error-codes.json，讓兩端 App 不必自備對照。
//
// 用法：node gen-error-codes.mjs

import { readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, '../..');
const apiDir = join(repoRoot, 'apps/api');

/** 例外類別 -> { status, defaultCode }（對照 Common/MemberExceptions.cs 與 Common/ApiExceptionHandler.cs） */
const CODED = {
  MemberValidationException: { status: 400, defaultCode: 'validation_failed' },
  MemberUnauthenticatedException: { status: 401, defaultCode: 'unauthenticated' },
  MemberForbiddenException: { status: 403, defaultCode: 'forbidden' },
  MemberNotFoundException: { status: 404, defaultCode: 'not_found' },
  MemberConflictException: { status: 409, defaultCode: 'conflict' },
  MemberAccountLockedException: { status: 423, defaultCode: 'account_locked', fixedCode: true },
  FeatureNotConfiguredException: { status: 503, defaultCode: 'not_configured' },
};

const CODE_RE = /^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$/;

function walk(dir, out) {
  for (const name of readdirSync(dir).sort()) {
    if (['bin', 'obj', 'Tcrfc.Api.Tests', 'node_modules'].includes(name)) continue;
    const p = join(dir, name);
    if (statSync(p).isDirectory()) walk(p, out);
    else if (name.endsWith('.cs')) out.push(p);
  }
  return out;
}

/** 讀一個 C# 字串字面值（src[i] 是起始的 `"`）。回傳 { end, text }；插值洞 {…} 裡的內容整段略過。 */
function readString(src, i, interpolated, verbatim) {
  let j = i + 1;
  let text = '';
  while (j < src.length) {
    const ch = src[j];
    if (ch === '\\' && !verbatim) {
      text += src.slice(j, j + 2);
      j += 2;
      continue;
    }
    if (ch === '"') {
      if (verbatim && src[j + 1] === '"') {
        text += '""';
        j += 2;
        continue;
      }
      return { end: j + 1, text };
    }
    if (interpolated && ch === '{') {
      if (src[j + 1] === '{') {
        text += '{{';
        j += 2;
        continue;
      }
      // 插值洞：跳到對應的 }，洞內可能還有字串
      let depth = 1;
      j++;
      while (j < src.length && depth > 0) {
        if (src[j] === '"') {
          j = readString(src, j, false, false).end;
        } else {
          if (src[j] === '{') depth++;
          else if (src[j] === '}') depth--;
          j++;
        }
      }
      text += '{…}';
      continue;
    }
    text += ch;
    j++;
  }
  throw new Error('字串未結束');
}

/** 從 src[start]（'(' 之後）取到對應的 ')'。回傳最上層的字串字面值（插值字串含「{…}」記號，不會被當成代碼）。 */
function readArgs(src, start) {
  let depth = 1;
  let i = start;
  const literals = [];
  while (i < src.length && depth > 0) {
    const ch = src[i];
    if (ch === '"') {
      const prefix = src.slice(Math.max(0, i - 2), i);
      const r = readString(src, i, prefix.includes('$'), prefix.includes('@'));
      literals.push(r.text);
      i = r.end;
      continue;
    }
    if (ch === '(') depth++;
    else if (ch === ')') depth--;
    i++;
  }
  return literals;
}

const found = new Map(); // code -> { status, exceptions:Set, files:Set }
function record(code, status, exc, file) {
  const e = found.get(code) ?? { statuses: new Set(), exceptions: new Set(), files: new Set() };
  e.statuses.add(status);
  e.exceptions.add(exc);
  e.files.add(file);
  found.set(code, e);
}

for (const file of walk(apiDir, [])) {
  const src = readFileSync(file, 'utf8');
  const rel = relative(repoRoot, file);
  if (rel.endsWith('Common/MemberExceptions.cs')) continue; // 類別定義本身（含預設值）不是呼叫點
  for (const [exc, meta] of Object.entries(CODED)) {
    const re = new RegExp(`new\\s+${exc}\\s*\\(`, 'g');
    let m;
    while ((m = re.exec(src)) !== null) {
      const literals = readArgs(src, m.index + m[0].length).filter((x) => CODE_RE.test(x));
      const code = meta.fixedCode ? meta.defaultCode : (literals.at(-1) ?? meta.defaultCode);
      record(code, meta.status, exc, rel);
    }
  }
}

// 預設代碼（呼叫端沒傳 code 時的值）一律列入，因為它們會真的出現在回應裡
for (const meta of Object.values(CODED)) {
  if (!found.has(meta.defaultCode)) {
    const exc = Object.keys(CODED).find((k) => CODED[k] === meta);
    record(meta.defaultCode, meta.status, exc, 'apps/api/Common/MemberExceptions.cs');
  }
}


// ── 英文訊息與狀態通用代碼（apps/api/Common/ApiErrorMessages.cs、ApiErrorEnvelope.cs）──────────────
function unescapeCs(t) {
  return t.replace(/\\(["\\])/g, '$1');
}
const messagesSrc = readFileSync(join(apiDir, 'Common/ApiErrorMessages.cs'), 'utf8');
const envelopeSrc = readFileSync(join(apiDir, 'Common/ApiErrorEnvelope.cs'), 'utf8');
const messageEn = new Map();
for (const m of messagesSrc.matchAll(/\["([a-z][a-z0-9_]*)"\]\s*=\s*"((?:[^"\\]|\\.)*)",/g)) {
  messageEn.set(m[1], unescapeCs(m[2]));
}
const nonRetryable = new Set();
{
  const block = messagesSrc.match(/NonRetryableServerCodes[\s\S]*?\{([\s\S]*?)\};/);
  for (const m of (block?.[1] ?? '').matchAll(/"([a-z][a-z0-9_]*)"/g)) nonRetryable.add(m[1]);
}
const statusDefaults = [];
{
  const block = envelopeSrc.match(/public static string DefaultCode\(int status\)[\s\S]*?\};/)?.[0] ?? '';
  for (const m of block.matchAll(/Status(\d{3})\w+\s*=>\s*"([a-z][a-z0-9_]*)"/g)) {
    statusDefaults.push({ status: Number(m[1]), code: m[2] });
  }
  const ge = block.match(/>=\s*500\s*=>\s*"([a-z][a-z0-9_]*)"/);
  if (ge) statusDefaults.push({ status: '5xx（其餘）', code: ge[1] });
}
if (messageEn.size === 0 || statusDefaults.length === 0) {
  throw new Error('解析 ApiErrorMessages.cs／ApiErrorEnvelope.cs 失敗（格式被改動？）');
}

const codes = [...found.entries()]
  .sort(([a], [b]) => a.localeCompare(b))
  .map(([code, e]) => ({
    code,
    httpStatus: [...e.statuses].sort((a, b) => a - b),
    exceptions: [...e.exceptions].sort(),
    messageEn: messageEn.get(code) ?? null,
    retryable: [...e.statuses].some((st) => st >= 500) && !nonRetryable.has(code),
  }));
const missingEn = codes.filter((c) => c.messageEn === null).map((c) => c.code);
if (missingEn.length) {
  throw new Error(`這些代碼在 apps/api/Common/ApiErrorMessages.cs 沒有英文訊息：${missingEn.join(', ')}`);
}

const doc = {
  $comment:
    '自動產生，請勿手改。產生器：shared/scripts/gen-error-codes.mjs（docs/19 §2）。來源是 apps/api 原始碼，不是規格；結構對應 App 規劃書 §9.5「錯誤代碼、雙語訊息、是否可重試」。',
  envelope: {
    format: 'application/problem+json 或 application/json（RFC 7807 ProblemDetails 形狀；例外處理器沿用 application/json，空本文補出的是 application/problem+json，欄位相同，請以本文解析、不要依 Content-Type 分流）',
    fields: {
      status: 'HTTP 狀態碼（整數）',
      title: '短標題（繁中）',
      detail: '可直接呈現給使用者的訊息（繁中，不洩漏內部實作細節）',
      instance: '請求路徑',
      code: '機器可讀代碼——所有錯誤都有。有專屬代碼的見 codes；其餘依狀態給通用代碼（見 statusDefaults）',
      messageZh: '繁中使用者訊息，恆等於 detail',
      messageEn: '英文使用者訊息：codes 內有專屬英文；沒有登記的代碼退回該狀態的通用英文。動態值（件數、訂單編號、商品名）不放進英文，需要顯示時用 messageZh 或自備字串表',
      retryable: '是否可重試（boolean）：5xx 為 true、4xx（含 423、429）為 false；例外：外部服務尚未設定的 503（codes 內 retryable=false）',
      lockedUntil: '僅 account_locked（423）：解鎖時間，UTC ISO 8601',
    },
    compatibility: '相容擴充：既有欄位一個不刪不改，舊版 App 忽略新增欄位即可。',
  },
  retryRule: {
    source: 'App 規劃書 §9.5「網路錯誤與伺服器 5xx 可自動重試，採指數退避；4xx 不重試」',
    retryStatus: 'status >= 500（回應的 retryable 為 true 時），或沒有收到回應的網路錯誤',
    noRetryStatus: '所有 4xx（含 423、429；429 另帶 Retry-After 標頭，要不要等待後由用戶端決定，規劃書未定義）',
    backoff: '1／2／4 秒加 jitter，最多 3 次（docs/19 §3）',
    exception: '付款訂單建立帶冪等鍵時，5xx 可即時以同一冪等鍵重試；不可暫存後補送（docs/19 §3）',
  },
  statusDefaults: statusDefaults.map((d) => ({ ...d, messageEn: messageEn.get(d.code) ?? null })),
  codes,
  uncodedFamilies: {
    note: '下列例外沒有專屬代碼，回應的 code 是依狀態的通用代碼（見 statusDefaults），messageEn 為該狀態的通用英文。App 目前只會呼叫公開與會員端點，後台一族不會出現在 App；公開端點的 AdminValidationException 等同 400「輸入內容有誤」。',
    byStatus: {
      400: '輸入內容有誤（AdminValidationException 一族，含 App 公開端點的裝置、廣告事件、診斷回報驗證）',
      403: '沒有權限／此功能不適用／共用內容唯讀',
      404: '找不到資料（各模組專屬標題）',
      409: '衝突（重複、已被變更、庫存不足…）',
      500: '伺服器發生未預期的錯誤（一律通用訊息，細節只進日誌）',
    },
  },
  pending: [
    '【待決】沒有專屬 code 的錯誤（400／403／404／409 的後台一族例外）：現在有通用代碼（依狀態），App 若需要依情境分流，後端要再補專屬 code，這屬於後端改動，不在 shared/ 發明。',
    '【待決】英文訊息不含動態值：需要「只剩 3 件」這類數字提示時，用戶端自備字串表或顯示 messageZh。',
  ],
};

writeFileSync(join(repoRoot, 'shared/error-codes.json'), `${JSON.stringify(doc, null, 2)}\n`);
console.log(`error-codes: ${codes.length} 個代碼 → shared/error-codes.json`);
