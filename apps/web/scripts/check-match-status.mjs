#!/usr/bin/env node
/**
 * check-match-status.mjs — 擋下「同一個 matches.status enum 又被人另開一份對照表」，
 * 並且驗證 `MATCH_STATUS_MAP` 真的涵蓋所有會被寫進資料庫的字面值。
 *
 * ## 這支腳本要防的具體事故（S0-9j，2026-09-23）
 *
 * `apps/web/app/utils/schedule.ts` 的 `mapMatchStatus()` 原本用 switch 比對
 * `'finished'`，但 `db/seed/generate-club-seed-sql.py` 灌進資料庫的藍鯨賽事一律是
 * `'played'`（見該檔第 590–600 行附近的 `INSERT INTO matches`）。兩個字面值長得像
 * 同一件事、卻從來對不上，導致藍鯨 21 場已完成賽事在畫面上顯示成「未開始」。
 *
 * 真正的根因不是「拼錯一個字」，而是**同一組 enum 在同一支功能裡有兩份手寫對照表**：
 * `schedule.ts` 的 `mapMatchStatus()` 一份、`schedule.vue` 當時自己另開的
 * `EVENT_STATUS_MAP`（給 SportsEvent JSON-LD 用）又一份。兩份表都是 `Record<string, …>`，
 * TypeScript 擋不住字面值拼錯或漏改；`npm run build`／`npm run lint:eslint` 也都不會發現
 * ——這正是 `docs/18-work-errors.md` `E-31` 那種「看起來有保證、實際上不成立」的形狀。
 *
 * 🔴 **2026-09-23 補（同一天第二輪）：檢查 1／2 原本擋不住 bug 本身重演。**
 * 交付後被拿「把 `MATCH_STATUS_MAP` 的鍵 `played` 改回 `finished`」（一字不差重現這次修的
 * bug）去測，結果 exit 0。原因是檢查 1／2 只驗「結構還在、沒有第二份表」，從來沒有驗過
 * 「這份唯一的表，鍵值本身對不對」——一支叫 `check-match-status` 又掛在 `lint` 裡的腳本，
 * 擋不住它命名的那個 bug，就是 `E-31`／`E-34` 反覆點名的「局部套用的機制給出全面的信心」。
 * 因此新增**檢查 3**：直接從「目前唯一真的會寫入 `matches.status` 的地方」
 * （`db/seed/generate-club-seed-sql.py`）抽出實際寫入的字面值，要求每一個都在
 * `MATCH_STATUS_MAP` 裡有對應的鍵。
 *
 * ## 這支腳本驗什麼
 *
 * 1. **單一來源還在**：`app/utils/schedule.ts` 必須存在、必須匯出 `mapMatchStatus` 與
 *    `matchStatusSchemaOrg`，且兩者都是從同一個 `MATCH_STATUS_MAP` 物件讀值。
 * 2. **沒有人在別處另開第二份**：`app/utils/schedule.ts` 以外的 `.ts`／`.vue` 檔案，
 *    一律不准出現 schema.org 的 Event 狀態字面值，或同時命中兩個以上
 *    `matches.status` 字面值（疑似另開一份手寫 enum 表）。
 * 3. **`MATCH_STATUS_MAP` 的鍵值本身涵蓋真實寫入值**：從已登記的
 *    `KNOWN_MATCHES_STATUS_WRITE_SOURCES`（目前只有一個：
 *    `db/seed/generate-club-seed-sql.py`）逐一抽出所有 `INSERT INTO matches (...)`
 *    語句裡 `status`欄位對應的字面值，要求每個都在 `MATCH_STATUS_MAP` 有對應的鍵；
 *    另外在 `db/`／`apps/api/` 掃一次有沒有**未登記**的 `INSERT INTO matches`／
 *    `UPDATE matches` 語句（見下方「涵蓋範圍與已知邊界」）。
 *
 * ⚠️ **檢查 1／2 的覆蓋範圍聲明**（E-31 教訓：局部套用的機制不該假裝全面）：
 * 這兩項只掃 `app/` 與 `shared/`（前端執行期程式碼），不掃 `apps/api`——後端目前對
 * `matches.status` 沒有任何 enum 對照表，只是原樣存取／回傳字串（見
 * `apps/api/Features/Schedule/MatchesRepository.cs`），沒有第二份表可以互相對不上。
 * 後端一旦也開始維護中文標籤或 schema.org 對照，這兩項要跟著擴大範圍。
 *
 * ## 檢查 3 的涵蓋範圍與已知邊界（一樣不假裝全面）
 *
 * - **只認字面值，不評字面值拼法對不對**：`matches.status` 沒有 CHECK 約束
 *   （`db/club-schema.sql`），規劃書也只定義中文語意、不定義英文拼法（`postponed`／
 *   `cancelled`／`live` 是延續既有命名風格的推斷，不是查證值，見 `schedule.ts` 檔頭）。
 *   這支腳本只驗「程式碼有沒有漏接種子資料真的會寫的值」，不驗「這個拼法是不是官方拼法」
 *   ——那需要規劃書先定義一份正式清單，是規格層決定，不是這支腳本能替你做的判斷。
 * - **`MATCH_STATUS_MAP` 比種子資料多出來的鍵不算錯**：`postponed`／`cancelled`／`live`
 *   目前沒有任何真實資料可核對，是為未來資料預留的，不因此判定失敗，只在通過訊息裡揭露
 *   「這幾個鍵目前沒有真實資料背書」，避免造成「全部驗過了」的錯覺（同樣是 E-31 教訓）。
 * - **只讀不改 `db/seed/`**：抽字面值只用 `readFileSync`，不執行、不修改該檔案或其產出。
 * - **目前只有一個已知寫入路徑，未來會有第二個**：後台 C4「賽程與賽果」模組還沒開發，
 *   開發後會是另一個寫入 `matches.status` 的地方（很可能在 `apps/api/`，走參數化查詢，
 *   屆時字面值可能根本不會出現在 SQL 文字裡，而是活在某個下拉選單的選項清單中——
 *   那種情況這支腳本抽不出字面值，需要屆時擴充抽取邏輯或改成人工註記涵蓋）。
 *   **為了不要讓這個已知的未來缺口變成「安靜地漏掉」**，本檢查除了讀
 *   `KNOWN_MATCHES_STATUS_WRITE_SOURCES` 裡登記的檔案，另外會在 `db/`／`apps/api/`
 *   底下（排除 `bin`／`obj`／`__pycache__`／`.generated`／`node_modules`）粗略掃一次
 *   有沒有 `INSERT INTO matches`／`UPDATE matches` 字樣的檔案**不在**登記清單裡——
 *   有就直接判定失敗，逼人把新來源加進登記清單並確認它寫的字面值。這個掃描是**單純字串
 *   比對**，不排除註解，寧可對著一個真的只是在講這件事的註解誤判（成本是有人要看一眼、
 *   把它加進登記清單或確認是誤判），也不要對一個真正的新寫入路徑視而不見。
 *   ⚠️ **就算加了這層，也不保證找到所有情況**——上面說的「參數化查詢、字面值活在下拉選單」
 *   那種寫法，這個字串比對一樣抓不到（沒有 `INSERT INTO matches` 字樣可以比對）。
 *   這是誠實的邊界，不是這支腳本能靠字串比對解決的問題。
 */

import { readFileSync, readdirSync, statSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const HERE = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(HERE, '..') // apps/web
const REPO_ROOT = resolve(ROOT, '..', '..') // 專案根目錄
const SOURCE_PATH = resolve(ROOT, 'app/utils/schedule.ts')
const SOURCE_LABEL = 'app/utils/schedule.ts'

const failures = []
const notices = []

// ---------------------------------------------------------------------------
// 共用工具：三種語言各自的「去註解但保留字串」掃描器，與括號配對／頂層逗號切割
// ---------------------------------------------------------------------------

/**
 * 去掉 JS／TS 的單行／多行註解，但**保留字串內容原封不動**——不能用「遇到 `//` 就砍到
 * 行尾」這種寫法，`'https://schema.org/EventScheduled'` 這種網址字串本身就含 `//`，
 * 天真的行註解剝除會把它從 `//` 開始整段吃掉，反而讓真正要抓的字面值消失
 * （這支腳本自己在 2026-09-23 開發時就踩過一次這個坑，用真的注入測資跑過才發現）。
 * 逐字元掃描，遇到引號就跳過整段字串再繼續判斷註解。
 */
function stripJsComments(src) {
  let out = ''
  for (let i = 0; i < src.length; i++) {
    const c = src[i]
    const c2 = src[i + 1]
    if (c === '"' || c === "'" || c === '`') {
      const quote = c
      let j = i + 1
      while (j < src.length) {
        if (src[j] === '\\') { j += 2; continue }
        if (src[j] === quote) { j++; break }
        j++
      }
      out += src.slice(i, j)
      i = j - 1
      continue
    }
    if (c === '/' && c2 === '/') {
      const nl = src.indexOf('\n', i)
      i = nl < 0 ? src.length : nl - 1
      continue
    }
    if (c === '/' && c2 === '*') {
      const end = src.indexOf('*/', i + 2)
      i = end < 0 ? src.length : end + 1
      continue
    }
    out += c
  }
  return out
}

/**
 * 去掉 Python 的 `#` 註解，但**保留字串內容原封不動**（含三引號多行字串）——
 * 跟上面 `stripJsComments` 同一類問題的 Python 版本：`#` 出現在字串裡不是註解
 * （這份腳本本身沒有這種案例，但 hex 色碼 `#RRGGBB` 這種字串內容在別的專案很常見，
 * 天真的「行尾砍註解」一樣會誤傷），所以一樣逐字元掃描、遇引號先跳過整段字串。
 * 三引號（`'''`／`\"\"\"`）視為單一字串邊界，內容（含換行）整段保留。
 */
function stripPythonComments(src) {
  let out = ''
  let i = 0
  const n = src.length
  while (i < n) {
    const c = src[i]
    if (c === '"' || c === "'") {
      const quote = c
      const triple = src[i + 1] === quote && src[i + 2] === quote
      if (triple) {
        const closeSeq = quote + quote + quote
        let j = i + 3
        while (j < n && src.slice(j, j + 3) !== closeSeq) {
          if (src[j] === '\\') { j += 2; continue }
          j++
        }
        j = Math.min(j + 3, n)
        out += src.slice(i, j)
        i = j
        continue
      }
      let j = i + 1
      while (j < n) {
        if (src[j] === '\\') { j += 2; continue }
        if (src[j] === quote) { j++; break }
        if (src[j] === '\n') break // 防禦：未正常結束的單行字串，不要吃掉整份檔案
        j++
      }
      out += src.slice(i, j)
      i = j
      continue
    }
    if (c === '#') {
      let j = src.indexOf('\n', i)
      if (j < 0) j = n
      out += ' '.repeat(j - i) // 用空白填滿被砍掉的註解，讓後續的字元位移不跑掉
      i = j
      continue
    }
    out += c
    i++
  }
  return out
}

/** 從 `openIdx`（指向開括號）找配對的 `)`，跳過 SQL 的 `'...'` 字串（`''` 是跳脫的單引號）。 */
function findMatchingParen(text, openIdx) {
  let depth = 0
  for (let i = openIdx; i < text.length; i++) {
    const c = text[i]
    if (c === "'") {
      let j = i + 1
      while (j < text.length) {
        if (text[j] === "'" && text[j + 1] === "'") { j += 2; continue }
        if (text[j] === "'") { j++; break }
        j++
      }
      i = j - 1
      continue
    }
    if (c === '(') depth++
    else if (c === ')') { depth--; if (depth === 0) return i }
  }
  return -1
}

/**
 * 依頂層逗號切割（不切括號／中括號／大括號內部的逗號，也不切 SQL 字串裡的逗號）。
 * 這裡刻意不分辨 `(`／`{`／`[` 的種類、只算通用深度——f-string 佔位符 `{esc(x["k"])}`
 * 裡同時混著 Python 的 `{}`／`[]` 與函式呼叫的 `()`，只要開合配對、深度歸零，
 * 不需要真的知道哪個開括號對哪個閉括號，就能正確找出「這一層」的逗號在哪裡。
 */
function splitTopLevel(text) {
  const parts = []
  let depth = 0
  let current = ''
  for (let i = 0; i < text.length; i++) {
    const c = text[i]
    if (c === "'") {
      let j = i + 1
      while (j < text.length) {
        if (text[j] === "'" && text[j + 1] === "'") { j += 2; continue }
        if (text[j] === "'") { j++; break }
        j++
      }
      current += text.slice(i, j)
      i = j - 1
      continue
    }
    if (c === '(' || c === '{' || c === '[') { depth++; current += c; continue }
    if (c === ')' || c === '}' || c === ']') { depth--; current += c; continue }
    if (c === ',' && depth === 0) { parts.push(current); current = ''; continue }
    current += c
  }
  if (current.trim() !== '') parts.push(current)
  return parts.map((s) => s.trim())
}

/** 從 `openIdx`（指向開大括號）找配對的 `}`，跳過 JS／TS 字串。用於解析 `MATCH_STATUS_MAP` 物件字面值。 */
function findMatchingBrace(text, openIdx) {
  let depth = 0
  for (let i = openIdx; i < text.length; i++) {
    const c = text[i]
    if (c === '"' || c === "'" || c === '`') {
      const quote = c
      let j = i + 1
      while (j < text.length) {
        if (text[j] === '\\') { j += 2; continue }
        if (text[j] === quote) { j++; break }
        j++
      }
      i = j - 1
      continue
    }
    if (c === '{') depth++
    else if (c === '}') { depth--; if (depth === 0) return i }
  }
  return -1
}

/** 遞迴列出符合副檔名的檔案，略過指定目錄名稱 */
function listAllFiles(dir, skipDirNames, extRe) {
  const out = []
  let entries
  try {
    entries = readdirSync(dir, { withFileTypes: true })
  } catch {
    return out
  }
  for (const entry of entries) {
    if (skipDirNames.has(entry.name)) continue
    const full = join(dir, entry.name)
    if (entry.isDirectory()) out.push(...listAllFiles(full, skipDirNames, extRe))
    else if (extRe.test(entry.name)) out.push(full)
  }
  return out
}

// ---------------------------------------------------------------------------
// 檢查 1：單一來源本身的結構還在
// ---------------------------------------------------------------------------

let sourceSrc
try {
  sourceSrc = readFileSync(SOURCE_PATH, 'utf8')
} catch {
  failures.push(`找不到 ${SOURCE_LABEL}——matches.status 的單一來源不見了，或路徑被搬走了。`)
  sourceSrc = ''
}

/** MATCH_STATUS_MAP 的頂層鍵名，供檢查 1（結構）與檢查 3（涵蓋度）共用 */
let matchStatusMapKeys = []
let cleanedScheduleSrc = ''

if (sourceSrc) {
  cleanedScheduleSrc = stripJsComments(sourceSrc)

  if (!/const MATCH_STATUS_MAP\s*:/.test(cleanedScheduleSrc)) {
    failures.push(`${SOURCE_LABEL} 找不到 \`MATCH_STATUS_MAP\` 常數——單一來源的核心結構被改掉了。`)
  } else {
    const declIdx = cleanedScheduleSrc.indexOf('MATCH_STATUS_MAP')
    const openBrace = cleanedScheduleSrc.indexOf('{', declIdx)
    const closeBrace = openBrace >= 0 ? findMatchingBrace(cleanedScheduleSrc, openBrace) : -1
    if (openBrace < 0 || closeBrace < 0) {
      failures.push(`${SOURCE_LABEL} 的 \`MATCH_STATUS_MAP\` 物件字面值大括號配對不起來，腳本無法解析鍵名。`)
    } else {
      const body = cleanedScheduleSrc.slice(openBrace, closeBrace)
      // 只抓「鍵名後面接大括號」的頂層鍵（scheduled: { code, label, schemaOrg }），
      // 不會誤抓巢狀的 code:／label:／schemaOrg:（它們後面接的是字串，不是大括號）。
      matchStatusMapKeys = [...body.matchAll(/([A-Za-z_][\w]*)\s*:\s*\{/g)].map((mm) => mm[1])
      if (matchStatusMapKeys.length === 0) {
        failures.push(`${SOURCE_LABEL} 的 \`MATCH_STATUS_MAP\` 解析不出任何鍵——物件字面值的寫法可能已經改變，腳本假設每個值都是 { code, label, schemaOrg } 形狀的物件。`)
      }
    }
  }

  if (!/export function mapMatchStatus\(/.test(sourceSrc)) {
    failures.push(`${SOURCE_LABEL} 找不到 \`export function mapMatchStatus(\`。`)
  }
  if (!/export function matchStatusSchemaOrg\(/.test(sourceSrc)) {
    failures.push(`${SOURCE_LABEL} 找不到 \`export function matchStatusSchemaOrg(\`——`
      + `SportsEvent JSON-LD 的 status 對照如果又被搬去別的檔案自己手刻，就是 S0-9j 重演。`)
  }
  // mapMatchStatus／matchStatusSchemaOrg 兩支都必須讀 MATCH_STATUS_MAP，不能各自硬編碼
  const mapFnMatch = /export function mapMatchStatus\([^)]*\)[^{]*\{([\s\S]*?)\n\}/.exec(sourceSrc)
  const schemaFnMatch = /export function matchStatusSchemaOrg\([^)]*\)[^{]*\{([\s\S]*?)\n\}/.exec(sourceSrc)
  if (mapFnMatch && !mapFnMatch[1].includes('MATCH_STATUS_MAP')) {
    failures.push(`\`mapMatchStatus()\` 的函式本體沒有讀 \`MATCH_STATUS_MAP\`——`
      + `它可能被改回獨立的 switch／查表，等於單一來源名存實亡。`)
  }
  if (schemaFnMatch && !schemaFnMatch[1].includes('MATCH_STATUS_MAP')) {
    failures.push(`\`matchStatusSchemaOrg()\` 的函式本體沒有讀 \`MATCH_STATUS_MAP\`——`
      + `它可能被改回獨立的 switch／查表，等於單一來源名存實亡。`)
  }
}

// ---------------------------------------------------------------------------
// 檢查 2：別的檔案有沒有另開一份
// ---------------------------------------------------------------------------

const SCAN_DIRS = ['app', 'shared']
const SKIP_DIR_NAMES = new Set(['node_modules', '.nuxt', '.output', 'dist'])
const STATUS_LITERALS = ['scheduled', 'played', 'postponed', 'cancelled', 'finished', 'live']

/** 遞迴列出 .ts／.vue 檔案，略過建置產物與 node_modules */
function listSourceFiles(dir) {
  return listAllFiles(dir, SKIP_DIR_NAMES, /\.(ts|vue)$/)
}

const webFiles = SCAN_DIRS.flatMap((d) => listSourceFiles(resolve(ROOT, d)))

for (const file of webFiles) {
  if (resolve(file) === resolve(SOURCE_PATH)) continue // 單一來源本身不受檢查 2 約束
  let src
  try {
    src = statSync(file).isFile() ? readFileSync(file, 'utf8') : ''
  } catch {
    continue
  }
  const cleaned = stripJsComments(src)
  const rel = relative(ROOT, file)

  // 2a：schema.org 的 Event status URL 只准從 matchStatusSchemaOrg() 取得
  if (/schema\.org\/Event(Scheduled|Completed|Postponed|Cancelled|Rescheduled|MovedOnline)/.test(cleaned)) {
    failures.push(
      `${rel} 出現 schema.org 的 EventStatusType 字面值——`
      + `這一定要透過 \`matchStatusSchemaOrg()\`（${SOURCE_LABEL}）取得，`
      + `不得在別的檔案手刻第二份 status → schema.org 對照表（S0-9j 就是這樣壞的）。`,
    )
  }

  // 2b：同一個檔案命中兩個以上 matches.status 字面值，視為疑似另開一份 enum 表。
  // 兩種寫法都要抓：加引號的字面值（switch case、比較式）與物件字面值的裸鍵名
  // （EVENT_STATUS_MAP 那種 `scheduled: '...'` 寫法，JS 允許合法識別字當 key 不加引號）。
  const hits = STATUS_LITERALS.filter((lit) =>
    new RegExp(`['"\`]${lit}['"\`]\\s*[:,)\\]]|\\b${lit}\\s*:`).test(cleaned),
  )
  if (hits.length >= 2) {
    failures.push(
      `${rel} 同時出現 ${hits.map((h) => `'${h}'`).join('、')} 這些 matches.status 字面值——`
      + `看起來像是另開了一份手寫的狀態對照表。matches.status 的顯示文字與 schema.org `
      + `對照唯一來源是 ${SOURCE_LABEL} 的 \`MATCH_STATUS_MAP\`，改這裡不改那裡就會重演 S0-9j。`
      + `如果這裡真的需要用到狀態值，請改用 \`mapMatchStatus()\`／\`matchStatusSchemaOrg()\`，`
      + `不要照抄字面值。`,
    )
  }
}

// ---------------------------------------------------------------------------
// 檢查 3a：已登記的寫入來源，抽出實際字面值，要求 MATCH_STATUS_MAP 都涵蓋
// ---------------------------------------------------------------------------

/** 目前唯一已知會寫入 matches.status 的地方（repo-relative path）。
 * 後台 C4「賽程與賽果」模組開發後，這裡要加上第二個來源——見檔頭「已知邊界」說明。 */
const KNOWN_MATCHES_STATUS_WRITE_SOURCES = [
  { repoRelPath: 'db/seed/generate-club-seed-sql.py', kind: 'python-seed' },
]

/** 從 python 種子產生器的原始碼裡，抽出所有 `INSERT INTO matches (...)` 語句
 * 對應 status 欄位的字面值。回傳 `{ literal }`（可解析成字面值）或
 * `{ dynamic, raw }`（值不是單純的 SQL 字串字面值，腳本無法靜態判斷，需要人工確認）。 */
function extractMatchesStatusLiterals(pySrc) {
  const cleaned = stripPythonComments(pySrc)
  const results = []
  const insertRe = /INSERT INTO matches\b\s*(\()/g
  let m
  while ((m = insertRe.exec(cleaned)) !== null) {
    const colOpen = m.index + m[0].length - 1
    const colClose = findMatchingParen(cleaned, colOpen)
    if (colClose < 0) continue
    const cols = splitTopLevel(cleaned.slice(colOpen + 1, colClose))
    const statusIdx = cols.findIndex((c) => c === 'status')
    if (statusIdx < 0) continue // 這個 INSERT 沒有 status 欄位（理論上不會發生，matches.status 是既有欄位）

    const valuesRe = /VALUES\s*(\()/g
    valuesRe.lastIndex = colClose
    const vm = valuesRe.exec(cleaned)
    if (!vm) continue
    const valOpen = vm.index + vm[0].length - 1
    const valClose = findMatchingParen(cleaned, valOpen)
    if (valClose < 0) continue
    const vals = splitTopLevel(cleaned.slice(valOpen + 1, valClose))
    const token = (vals[statusIdx] ?? '').trim()
    const lit = /^N?'([^']*)'$/.exec(token)
    if (lit) results.push({ literal: lit[1] })
    else results.push({ dynamic: true, raw: token })
  }
  return results
}

for (const source of KNOWN_MATCHES_STATUS_WRITE_SOURCES) {
  const absPath = resolve(REPO_ROOT, source.repoRelPath)
  let pySrc
  try {
    pySrc = readFileSync(absPath, 'utf8')
  } catch {
    failures.push(`登記的寫入來源 \`${source.repoRelPath}\` 讀不到——路徑可能被搬走了，`
      + `請更新 check-match-status.mjs 的 KNOWN_MATCHES_STATUS_WRITE_SOURCES。`)
    continue
  }

  const extracted = extractMatchesStatusLiterals(pySrc)
  if (extracted.length === 0) {
    failures.push(`\`${source.repoRelPath}\` 已登記為 matches.status 的寫入來源，`
      + `但抽不到任何 \`INSERT INTO matches\` 語句——來源清單可能已經過期，請人工確認。`)
    continue
  }

  const literalHits = extracted.filter((e) => 'literal' in e)
  const dynamicHits = extracted.filter((e) => e.dynamic)

  for (const { raw } of dynamicHits) {
    failures.push(`\`${source.repoRelPath}\` 有一處 matches.status 寫入值不是單純的 SQL 字面值`
      + `（原始 token：\`${raw}\`），腳本無法靜態判斷實際會寫入什麼字串，需要人工確認`
      + `並在 MATCH_STATUS_MAP 補上對應處理，不能假設它已經被涵蓋。`)
  }

  const uniqueLiterals = [...new Set(literalHits.map((e) => e.literal))]
  const missing = uniqueLiterals.filter((lit) => !matchStatusMapKeys.includes(lit))
  for (const lit of missing) {
    failures.push(`種子資料（\`${source.repoRelPath}\`）會寫入 '${lit}'，`
      + `但 \`MATCH_STATUS_MAP\`（${SOURCE_LABEL}）沒有這個鍵——`
      + `這正是 S0-9j 那個 bug 的形狀：程式的對照表沒有涵蓋資料庫真的會出現的值。`)
  }
  if (missing.length === 0) {
    notices.push(`\`${source.repoRelPath}\` 實際會寫入的 matches.status 值：`
      + `${uniqueLiterals.map((l) => `'${l}'`).join('、')}，皆已在 MATCH_STATUS_MAP 涵蓋。`)
  }
}

// MATCH_STATUS_MAP 比種子資料多出來的鍵不算錯，但要揭露，避免「全部驗過了」的錯覺（E-31 教訓）
if (matchStatusMapKeys.length > 0) {
  const allKnownLiterals = new Set(
    KNOWN_MATCHES_STATUS_WRITE_SOURCES.flatMap((source) => {
      try {
        return extractMatchesStatusLiterals(readFileSync(resolve(REPO_ROOT, source.repoRelPath), 'utf8'))
          .filter((e) => 'literal' in e).map((e) => e.literal)
      } catch {
        return []
      }
    }),
  )
  const unbackedKeys = matchStatusMapKeys.filter((k) => !allKnownLiterals.has(k))
  if (unbackedKeys.length > 0) {
    notices.push(`⚠️ 覆蓋範圍揭露：\`MATCH_STATUS_MAP\` 的 ${unbackedKeys.map((k) => `'${k}'`).join('、')} `
      + `目前沒有任何已知寫入來源會產生對應真實資料，是為未來資料預留的鍵，本檢查**沒有**驗證它們是否正確，`
      + `之後真的出現這些狀態的真實資料時要重新核對顯示文字（見 ${SOURCE_LABEL} 檔頭）。`)
  }
}

// ---------------------------------------------------------------------------
// 檢查 3b：db/、apps/api/ 底下有沒有未登記的 matches 寫入路徑
// ---------------------------------------------------------------------------

const DISCOVERY_SCAN_REPO_DIRS = ['db', 'apps/api']
const DISCOVERY_SKIP_DIR_NAMES = new Set(['node_modules', '.git', 'bin', 'obj', '__pycache__', '.generated'])
const DISCOVERY_EXTENSIONS = /\.(py|sql|cs)$/
const MATCHES_WRITE_PATTERN = /\b(INSERT INTO|UPDATE)\s+matches\b/i

const knownRepoRelPaths = new Set(KNOWN_MATCHES_STATUS_WRITE_SOURCES.map((s) => s.repoRelPath))

for (const dir of DISCOVERY_SCAN_REPO_DIRS) {
  const files = listAllFiles(resolve(REPO_ROOT, dir), DISCOVERY_SKIP_DIR_NAMES, DISCOVERY_EXTENSIONS)
  for (const file of files) {
    const repoRelPath = relative(REPO_ROOT, file)
    if (knownRepoRelPaths.has(repoRelPath)) continue
    let content
    try {
      content = readFileSync(file, 'utf8')
    } catch {
      continue
    }
    // 這裡刻意不去註解：寧可對著一句提到這件事的註解誤判（成本是有人看一眼），
    // 也不要對一個真正的新寫入路徑視而不見（見檔頭「已知邊界」說明）。
    if (MATCHES_WRITE_PATTERN.test(content)) {
      failures.push(`發現未登記的 matches 寫入路徑：\`${repoRelPath}\`（含 INSERT INTO matches／`
        + `UPDATE matches 字樣）。請把它加進 check-match-status.mjs 的 `
        + `KNOWN_MATCHES_STATUS_WRITE_SOURCES，並確認它寫入的 status 字面值都在 `
        + `MATCH_STATUS_MAP 涵蓋範圍內（如果這支腳本還不支援解析這個檔案的語言，`
        + `要擴充 extractMatchesStatusLiterals 或改成人工註記涵蓋，不能放著不管）。`)
    }
  }
}

// ---------------------------------------------------------------------------
// 輸出
// ---------------------------------------------------------------------------

if (failures.length > 0) {
  console.error(`\n✗ matches.status 單一來源檢查未通過（${failures.length} 項）：\n`)
  for (const f of failures) console.error(`  - ${f}`)
  console.error(`\n真實來源是 ${SOURCE_LABEL} 的 MATCH_STATUS_MAP；背景見 docs/18-work-errors.md S0-9j 交付報告。\n`)
  process.exit(1)
}

console.log(`✓ matches.status 單一來源檢查通過（掃了 ${webFiles.length} 個前端檔案找不到第二份對照表；`
  + `已核對 ${KNOWN_MATCHES_STATUS_WRITE_SOURCES.length} 個已知寫入來源的真實字面值都被涵蓋）`)
for (const n of notices) console.log(`  ${n}`)
