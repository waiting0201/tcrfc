#!/usr/bin/env node
// shared/scripts/gen-error-codes.mjs — 從 apps/api 原始碼掃出「現行 API 實際會回的機器可讀錯誤代碼」，寫入 shared/error-codes.json。
//
// 資料來源（不發明規格）：
//   - apps/api/Common/MemberExceptions.cs 的 ICodedApiException 一族（會員／付款／報名等公開端點）。
//     ApiExceptionHandler 把它們轉成 RFC 7807 ProblemDetails，並把 Code 放進擴充欄位 `code`。
//   - 其餘例外（後台一族、AdminValidationException 等）目前**沒有**機器可讀代碼，只有 HTTP 狀態與標題；
//     它們不在 codes 清單，而是在 shared/error-codes.json 的 `uncodedFamilies` 說明。
//
// App 規劃書 §9.5 要求的「統一錯誤結構：錯誤代碼、雙語訊息、是否可重試」目前後端只做到一部分：
// 有 code（僅會員一族）、只有繁中訊息、沒有 retryable 欄位。這些缺口寫在 `pending`，**不在此補規格**。
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

const codes = [...found.entries()]
  .sort(([a], [b]) => a.localeCompare(b))
  .map(([code, e]) => ({
    code,
    httpStatus: [...e.statuses].sort((a, b) => a - b),
    exceptions: [...e.exceptions].sort(),
  }));

const doc = {
  $comment:
    '自動產生，請勿手改。產生器：shared/scripts/gen-error-codes.mjs（docs/19 §2）。來源是 apps/api 原始碼，不是規格；App 規劃書 §9.5 的目標結構與現況的差距見 pending。',
  envelope: {
    format: 'application/problem+json（RFC 7807 ProblemDetails）',
    fields: {
      status: 'HTTP 狀態碼（整數）',
      title: '短標題（繁中）',
      detail: '可直接呈現給使用者的訊息（目前只有繁中，不洩漏內部實作細節）',
      instance: '請求路徑',
      code: '機器可讀代碼——只有「會員一族」例外才有（見 codes）；缺少時 App 只能依 status 處理',
      lockedUntil: '僅 account_locked（423）：解鎖時間，UTC ISO 8601',
    },
  },
  retryRule: {
    source: 'App 規劃書 §9.5「網路錯誤與伺服器 5xx 可自動重試，採指數退避；4xx 不重試」',
    retryStatus: 'status >= 500，或沒有收到回應的網路錯誤',
    noRetryStatus: '所有 4xx（含 423）',
    backoff: '1／2／4 秒加 jitter，最多 3 次（docs/19 §3）',
    exception: '付款訂單建立帶冪等鍵時，5xx 可即時以同一冪等鍵重試；不可暫存後補送（docs/19 §3）',
  },
  codes,
  uncodedFamilies: {
    note: '下列例外只有 HTTP 狀態與標題、沒有 code。App 目前只會呼叫公開與會員端點，後台一族不會出現在 App；公開端點的 AdminValidationException 等同 400「輸入內容有誤」。',
    byStatus: {
      400: '輸入內容有誤（AdminValidationException 一族，含 App 公開端點的裝置、廣告事件、診斷回報驗證）',
      403: '沒有權限／此功能不適用／共用內容唯讀',
      404: '找不到資料（各模組專屬標題）',
      409: '衝突（重複、已被變更、庫存不足…）',
      500: '伺服器發生未預期的錯誤（一律通用訊息，細節只進日誌）',
    },
  },
  pending: [
    '【待決】雙語訊息：現行 ProblemDetails.detail 只有繁中；App 規劃書 §9.5 要求雙語，後端尚未提供英文訊息，App 端暫時只能顯示繁中或自備代碼對照。',
    '【待決】retryable 欄位：規劃書 §9.5 要求回應帶「是否可重試」；現行回應沒有此欄位，App 端以 retryRule（依狀態碼）判斷。',
    '【待決】沒有 code 的錯誤（400／403／404／409 的後台一族例外）：App 若需要依情境分流，後端要先補 code，這屬於後端改動，不在 shared/ 發明。',
    '【待決】429（速率限制）與 5xx 在 ASP.NET 層由中介軟體直接回應時不一定是 ProblemDetails 格式，App 需容忍非 JSON 本文。',
  ],
};

writeFileSync(join(repoRoot, 'shared/error-codes.json'), `${JSON.stringify(doc, null, 2)}\n`);
console.log(`error-codes: ${codes.length} 個代碼 → shared/error-codes.json`);
