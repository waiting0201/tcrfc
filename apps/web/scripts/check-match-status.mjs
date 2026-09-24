#!/usr/bin/env node
/**
 * check-match-status.mjs — 擋下「同一個 matches.status enum 又被人另開一份對照表」，
 * 並且驗證 `MATCH_STATUS_MAP` 真的涵蓋資料庫允許寫入的每一個值——不多不少。
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
 * ## 🔴 2026-09-24 重新設計：值域真實來源改成 DDL 的 CHECK 約束
 *
 * `db/club-schema.sql` 在 v3.14（主站規劃書同步）替 `matches.status` 加上
 * `CONSTRAINT CK_matches_status CHECK (status IN ('scheduled','live','played','postponed','cancelled'))`
 * ——**資料庫本身現在保證 `matches.status` 只會是這五個值**，不管寫入路徑是 Python 種子腳本、
 * 後台 C4 模組、還是任何一支測試。這件事在舊版腳本設計時還不成立（當時檔頭寫的是
 * 「無 CHECK 約束，DB 這一層也不提供保證」），舊設計因此得**自己去逐一登記每個會寫入
 * `matches.status` 的地方、抽出字面值、核對涵蓋度**——這在 CHECK 出現後變成了多餘的重複工。
 *
 * **舊設計在 S0-9l 之後從綠燈變紅燈，原因正是這個機制的代價浮現**：
 * `apps/api/Tcrfc.Api.Tests/ScheduleOriginalDateTests.cs` 新增了一支測試，用**參數化查詢**
 * （`INSERT INTO matches (...) VALUES (@Id, ..., @Status, ...)`）插入人造測資、測完刪乾淨。
 * SQL 文字裡確實出現了 `INSERT INTO matches`，於是舊版「掃 `db/`／`apps/api/` 找未登記的
 * `INSERT INTO matches`／`UPDATE matches` 字樣」那道檢查判定它是未登記的新寫入來源，
 * 直接判定失敗——但這支測試的字面值（`"postponed"`、`"scheduled"`）是 C# 字串常值，
 * 從來不會出現在 SQL 文字本身裡（走 `@Status` 參數），舊版的字面值抽取邏輯本來就只認
 * python 種子腳本那種把字面值直接寫進 SQL 文字的形狀，**抽不出來，只能一律判定失敗、
 * 要求人工登記**。這正是「一支叫 check-match-status 的腳本，因為別的功能新增了一支測試
 * 而長期紅燈」——不是它抓到真的 bug，是它的涵蓋機制本身跟不上參數化查詢的寫法。
 *
 * ### 重新設計的決定：移除「逐一登記寫入來源」機制，改以 CHECK 約束為值域真實來源
 *
 * 舊機制想達成的效果——「`MATCH_STATUS_MAP` 有沒有涵蓋資料庫真的會出現的值」——
 * 現在由**兩件事共同、且更可靠地**達成，不需要再追蹤「誰在哪裡寫」：
 *
 * 1. **DB 層**：`CK_matches_status` 保證任何寫入路徑（不管是 Python、C#、後台表單，
 *    現在的還是未來的）只要試圖寫入不在值域內的字面值，一律在 DDL 這一層被拒絕——
 *    不需要這支腳本知道「誰在寫」就能保證這件事。
 * 2. **程式層**（本檔案的檢查 3）：`MATCH_STATUS_MAP` 的鍵集合**恰好等於** `CK_matches_status`
 *    的值域——多一個少一個都判定失敗。任何值只要能通過 CHECK 寫進資料庫，
 *    `MATCH_STATUS_MAP` 就一定有對應的鍵；不會再發生「資料庫真的存在的值，
 *    程式的對照表沒有涵蓋」（S0-9j 的形狀）。
 *
 * 兩者相加，**涵蓋度的保證不再依賴「有沒有人記得把新的寫入來源登記進一份清單」**——
 * 這正是舊機制最脆弱的地方：C# 參數化寫入完全繞過了「字面值出現在 SQL 文字裡」這個
 * 抽取前提，未來任何用參數化查詢寫入 `matches` 的地方（後台 C4 模組幾乎一定會是這樣寫）
 * 都會重演同一種假紅燈。**移除登記機制不是放寬檢查，是把同一個保證換一個不會被
 * 參數化查詢繞過的方式重新做一次，而且做得更嚴（不多不少，不是只驗「沒漏」）。**
 *
 * 保留下來的只有一項**範圍明確縮小、不再是「發現所有寫入來源」的探索機制**的檢查（檢查 4）：
 * 對 `db/seed/generate-club-seed-sql.py` 這一支**具名、已知**的種子產生器，額外做一次
 * 「它寫進 SQL 文字的字面值有沒有在 CHECK 值域內」的靜態核對——這不是為了涵蓋度
 * （涵蓋度已經由檢查 3 保證），純粹是「不用實際跑一次種子腳本、不用起一顆真的資料庫，
 * 就能在 lint 階段先抓到這支腳本自己手誤打錯字」的低成本加分項，範圍只限這一支檔案，
 * 不會因為別處新增任何 SQL 語句而連帶紅燈。
 *
 * ## 這支腳本現在驗什麼
 *
 * 1. **單一來源還在**：`app/utils/schedule.ts` 必須存在、必須匯出 `mapMatchStatus` 與
 *    `matchStatusSchemaOrg`，且兩者都是從同一個 `MATCH_STATUS_MAP` 物件讀值。
 * 2. **沒有人在別處另開第二份**：`app/utils/schedule.ts` 以外的 `.ts`／`.vue` 檔案，
 *    一律不准出現 schema.org 的 Event 狀態字面值，或同時命中兩個以上
 *    `matches.status` 字面值（疑似另開一份手寫 enum 表）。
 * 3. **`MATCH_STATUS_MAP` 的鍵集合＝`db/club-schema.sql` 的 `CK_matches_status` 值域**：
 *    解析不到這條 CHECK 約束就直接判定失敗（不能靜默通過——這正是 E-31 的教訓：
 *    「解析不到」不等於「沒問題」）。解析到之後，兩邊集合逐一比對，**少了**是 S0-9j
 *    那種「資料庫允許但程式沒接住」的 bug；**多了**代表 `MATCH_STATUS_MAP` 裡有一個
 *    資料庫不可能出現的鍵——這支腳本選擇**同樣判定失敗**，理由見下方「涵蓋範圍與決定」。
 * 4. **已知種子產生器的字面值不越界**：`db/seed/generate-club-seed-sql.py` 裡
 *    `INSERT INTO matches (...)` 對應 `status` 欄位的字面值，逐一核對是否落在
 *    `CK_matches_status` 值域內；越界的話 lint 階段就能抓到，不用等實際跑種子腳本
 *    被 DB 的 CHECK 擋下才發現。
 *
 * ⚠️ **檢查 1／2 的覆蓋範圍聲明**（E-31 教訓：局部套用的機制不該假裝全面）：
 * 這兩項只掃 `app/` 與 `shared/`（前端執行期程式碼），不掃 `apps/api`——後端目前對
 * `matches.status` 沒有任何 enum 對照表，只是原樣存取／回傳字串（見
 * `apps/api/Features/Schedule/MatchesRepository.cs`），沒有第二份表可以互相對不上。
 * 後端一旦也開始維護中文標籤或 schema.org 對照，這兩項要跟著擴大範圍。
 *
 * ## 檢查 3／4 的涵蓋範圍與決定（一樣不假裝全面）
 *
 * - **值域真實來源是 `db/club-schema.sql` 的 DDL 文字，不是 `docs/12-database-schema.md`**。
 *   `CLAUDE.md` 明訂資料庫綱要的真實來源是 `docs/12`、DDL 是從文件同步出來的下游——
 *   但**這支腳本檢查的是「程式碼有沒有跟現行 DDL 對齊」，不是「DDL 有沒有跟規劃書對齊」**，
 *   後者是文件同步鏈（`00-harness.md` §2.5）的責任範圍，不是這支前端 lint 腳本能做或該做的判斷。
 *   如果 DDL 本身就抄錯了規劃書，這支腳本會忠實地讓程式碼對齊那個抄錯的值域，抓不到。
 * - **「多出來的鍵也判定失敗」的理由**：CHECK 約束定案後，`matches.status` 的值域不再是
 *   「目前已知的、可能還會擴充的」開放集合，而是**封閉集合**——DDL 明文列出全部五個值，
 *   之後真要新增狀態值一定會改 DDL（同步鏈會先改規劃書、`docs/12`，再改 DDL）。
 *   在這個前提下，`MATCH_STATUS_MAP` 比 CHECK 值域多出來的鍵**保證是資料庫永遠打不中的死鍵**，
 *   留著它不是「預留給未來」，是「跟 DDL 各自表述的第二份定義」——正是 E-39／S0-9j 那個
 *   「同一組 enum 兩份手寫表、沒有機制要求一致」根因的另一種變形，只是這次多出來的那份
 *   是憑空多寫、不是漏寫。**選擇讓它跟「少了」一樣判定失敗**，而不是只揭露不擋，
 *   是因為揭露而不擋（舊版對 `postponed`／`cancelled`／`live` 的處理方式）在 CHECK 約束
 *   出現之前是合理的——那時它們確實是「未來可能出現、現在還沒有真實資料」；
 *   但現在 DDL 已經把值域釘死，「多出來的鍵」不會隨時間變成「有真實資料了」，
 *   它就是錯的，沒有留著的理由。
 * - **只認字面值，不驗字面值拼法本身對不對**：這支腳本驗的是「程式碼／種子腳本的字面值
 *   有沒有跟 DDL 的 CHECK 值域一致」，不驗「這個拼法是不是正確的英文拼法」——
 *   那是規劃書與 DDL 之間的同步問題，不是這支腳本能替你做的判斷。
 * - **只讀不改** `db/club-schema.sql`／`db/seed/`：全程只用 `readFileSync`，
 *   不執行、不修改任何檔案或其產出。
 * - **C# 參數化寫入（`@Status` 這類具名參數）不在檢查 4 的範圍內，也刻意不再嘗試涵蓋**：
 *   字面值是 C# 端的字串常值，不會出現在 SQL 文字裡，靜態掃描 SQL 文字本質上抽不出來；
 *   這類寫入的正確性現在完全交給 DB 的 `CK_matches_status` 在執行期把關（見上方
 *   「重新設計的決定」）。**這不是「這支腳本還沒做到」的缺口，是刻意的範圍邊界**——
 *   對參數化查詢做字面值抽取需要理解 C# 的變數綁定關係，成本遠高於它能多抓到的東西
 *   （DB 本身已經保證了）。
 */

import { readFileSync, readdirSync, statSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const HERE = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(HERE, '..') // apps/web
const REPO_ROOT = resolve(ROOT, '..', '..') // 專案根目錄
const SOURCE_PATH = resolve(ROOT, 'app/utils/schedule.ts')
const SOURCE_LABEL = 'app/utils/schedule.ts'
const SCHEMA_PATH = resolve(REPO_ROOT, 'db/club-schema.sql')
const SCHEMA_LABEL = 'db/club-schema.sql'
const SEED_FILE_REL = 'db/seed/generate-club-seed-sql.py'

const failures = []
const notices = []

// ---------------------------------------------------------------------------
// 共用工具：各語言「去註解但保留字串」掃描器，與括號配對／頂層逗號切割
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

/**
 * 去掉 T-SQL 的 `--` 行註解與 `/* *\/` 區塊註解，但**保留字串內容原封不動**
 * （單引號字串內的 `''` 是跳脫的單引號，不是字串結束）。跟上面兩支同一類問題的 SQL 版本，
 * 用空白填滿被砍掉的部分以維持字元位移，方便後續用位置相關的邏輯核對。
 */
function stripSqlComments(src) {
  let out = ''
  let i = 0
  const n = src.length
  while (i < n) {
    const c = src[i]
    const c2 = src[i + 1]
    if (c === "'") {
      let j = i + 1
      while (j < n) {
        if (src[j] === "'" && src[j + 1] === "'") { j += 2; continue }
        if (src[j] === "'") { j++; break }
        j++
      }
      out += src.slice(i, j)
      i = j
      continue
    }
    if (c === '-' && c2 === '-') {
      let j = src.indexOf('\n', i)
      if (j < 0) j = n
      out += ' '.repeat(j - i)
      i = j
      continue
    }
    if (c === '/' && c2 === '*') {
      let j = src.indexOf('*/', i + 2)
      j = j < 0 ? n : j + 2
      out += ' '.repeat(j - i)
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

/**
 * 從 `db/club-schema.sql` 的原始碼裡解析 `CONSTRAINT CK_matches_status CHECK (status IN (...))`
 * 的值域清單。回傳 `{ values: string[] }`，或 `{ error }`（`'not-found'`／`'unparseable'`／`'empty'`），
 * 呼叫端要對每一種 error 分別給出明確的失敗訊息，不能靜默跳過（E-31 教訓）。
 */
function parseMatchesStatusCheckDomain(sqlSrc) {
  const cleaned = stripSqlComments(sqlSrc)
  const m = /CONSTRAINT\s+CK_matches_status\s+CHECK\s*\(\s*status\s+IN\s*\(([^)]*)\)\s*\)/i.exec(cleaned)
  if (!m) return { error: 'not-found' }
  const parts = splitTopLevel(m[1])
  if (parts.length === 0) return { error: 'empty' }
  const values = []
  for (const part of parts) {
    const lit = /^N?'([^']*)'$/.exec(part.trim())
    if (!lit) return { error: 'unparseable', raw: part }
    values.push(lit[1])
  }
  return { values }
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
// 檢查 3：MATCH_STATUS_MAP 的鍵集合 = db/club-schema.sql 的 CK_matches_status 值域
// ---------------------------------------------------------------------------

let schemaSrc
try {
  schemaSrc = readFileSync(SCHEMA_PATH, 'utf8')
} catch {
  failures.push(`找不到 ${SCHEMA_LABEL}——matches.status 的值域真實來源不見了，或路徑被搬走了。`)
  schemaSrc = ''
}

/** CK_matches_status 的值域（解析成功時才會是陣列，供檢查 4 共用） */
let checkDomain = null

if (schemaSrc) {
  const parsed = parseMatchesStatusCheckDomain(schemaSrc)
  if (parsed.error === 'not-found') {
    failures.push(`${SCHEMA_LABEL} 找不到 \`CONSTRAINT CK_matches_status\` 的 CHECK 子句——`
      + `這支腳本現在把它當成 matches.status 的值域真實來源，找不到就無法驗證涵蓋度，`
      + `不能假裝沒事直接放行（約束可能被移除或改寫成別的形式，需要人工確認並視情況更新這支腳本的解析邏輯）。`)
  } else if (parsed.error === 'unparseable') {
    failures.push(`${SCHEMA_LABEL} 的 \`CK_matches_status\` 值域裡有一項不是單純的字串字面值`
      + `（原始片段：\`${parsed.raw}\`），腳本用「逗號分隔的簡單字串清單」這個假設解析不了，`
      + `需要人工確認並視情況擴充 \`parseMatchesStatusCheckDomain()\`。`)
  } else if (parsed.error === 'empty') {
    failures.push(`${SCHEMA_LABEL} 的 \`CK_matches_status\` 解析出空的值域清單——約束可能被改成別的寫法。`)
  } else {
    checkDomain = parsed.values
  }
}

if (checkDomain && matchStatusMapKeys.length > 0) {
  const domainSet = new Set(checkDomain)
  const keysSet = new Set(matchStatusMapKeys)
  const missingInMap = checkDomain.filter((v) => !keysSet.has(v))
  const extraInMap = matchStatusMapKeys.filter((v) => !domainSet.has(v))

  for (const v of missingInMap) {
    failures.push(`${SCHEMA_LABEL} 的 \`CK_matches_status\` 值域允許 '${v}'，`
      + `但 \`MATCH_STATUS_MAP\`（${SOURCE_LABEL}）沒有這個鍵——`
      + `這正是 S0-9j 那個 bug 的形狀：資料庫允許寫入的值，程式的對照表沒有涵蓋。`)
  }
  for (const v of extraInMap) {
    failures.push(`\`MATCH_STATUS_MAP\`（${SOURCE_LABEL}）有鍵 '${v}'，`
      + `但 ${SCHEMA_LABEL} 的 \`CK_matches_status\` 值域不允許這個值——`
      + `這個鍵在真實資料庫裡永遠不會被打中，是資料庫不可能出現的鍵。`
      + `CHECK 約束定案後 matches.status 是封閉值域，\`MATCH_STATUS_MAP\` 的鍵集合必須`
      + `恰好等於它，不多不少（見本檔頭「涵蓋範圍與決定」）。`)
  }
  if (missingInMap.length === 0 && extraInMap.length === 0) {
    notices.push(`\`MATCH_STATUS_MAP\` 的鍵集合與 ${SCHEMA_LABEL} 的 \`CK_matches_status\` 值域完全一致`
      + `（${checkDomain.map((v) => `'${v}'`).join('、')}），以 DDL 為值域真實來源的驗證通過。`)
  }
}

// ---------------------------------------------------------------------------
// 檢查 4：已知種子產生器的字面值有沒有落在 CHECK 值域內
// （範圍明確限於這一支檔案，不是「發現所有寫入來源」的探索機制——見本檔頭說明）
// ---------------------------------------------------------------------------

if (checkDomain) {
  const domainSet = new Set(checkDomain)
  const seedAbsPath = resolve(REPO_ROOT, SEED_FILE_REL)
  let pySrc
  try {
    pySrc = readFileSync(seedAbsPath, 'utf8')
  } catch {
    failures.push(`讀不到 \`${SEED_FILE_REL}\`——路徑可能被搬走了，`
      + `請更新 check-match-status.mjs 的 \`SEED_FILE_REL\`。`)
    pySrc = null
  }

  if (pySrc) {
    const extracted = extractMatchesStatusLiterals(pySrc)
    if (extracted.length === 0) {
      failures.push(`\`${SEED_FILE_REL}\` 抽不到任何 \`INSERT INTO matches\` 語句——`
        + `這支腳本假設它仍是直接把字面值寫進 SQL 文字的種子產生器，`
        + `如果它已經改寫成別種寫法（例如改走參數化查詢），這條檢查需要跟著調整或移除。`)
    } else {
      const literalHits = extracted.filter((e) => 'literal' in e)
      const dynamicHits = extracted.filter((e) => e.dynamic)

      for (const { raw } of dynamicHits) {
        failures.push(`\`${SEED_FILE_REL}\` 有一處 matches.status 寫入值不是單純的 SQL 字面值`
          + `（原始 token：\`${raw}\`），腳本無法靜態判斷它是否落在 \`CK_matches_status\` 值域內，`
          + `需要人工確認。`)
      }

      const uniqueLiterals = [...new Set(literalHits.map((e) => e.literal))]
      const outOfDomain = uniqueLiterals.filter((lit) => !domainSet.has(lit))
      for (const lit of outOfDomain) {
        failures.push(`\`${SEED_FILE_REL}\` 寫入了 '${lit}'，但這個值不在 ${SCHEMA_LABEL} 的 `
          + `\`CK_matches_status\` 值域內——實際跑這支種子腳本對真實資料庫時會被 CHECK 約束擋下，`
          + `這裡先在 lint 階段抓出來，不用起一顆資料庫才發現。`)
      }

      if (outOfDomain.length === 0 && dynamicHits.length === 0) {
        notices.push(`\`${SEED_FILE_REL}\` 實際會寫入的 matches.status 字面值：`
          + `${uniqueLiterals.map((l) => `'${l}'`).join('、')}，皆在 \`CK_matches_status\` 值域內。`)
      }

      // 揭露「MATCH_STATUS_MAP 裡有哪些鍵目前沒有真實種子資料背書」——
      // 檢查 3 已經保證這些鍵都在 CHECK 值域內（不會是錯的鍵），這裡純粹是可見度揭露，
      // 避免「全部驗過了」的錯覺（E-31 教訓）：拼法／顯示文字仍未被任何真實資料核對過。
      const literalsSet = new Set(uniqueLiterals)
      const unbackedKeys = matchStatusMapKeys.filter((k) => !literalsSet.has(k))
      if (unbackedKeys.length > 0) {
        notices.push(`⚠️ 覆蓋範圍揭露：\`MATCH_STATUS_MAP\` 的 ${unbackedKeys.map((k) => `'${k}'`).join('、')} `
          + `目前沒有 \`${SEED_FILE_REL}\` 的真實種子資料背書（雖然已確認在 \`CK_matches_status\` `
          + `值域內、鍵本身沒錯），顯示文字與 schema.org 對照仍未被任何真實資料核對過，`
          + `之後真的出現這些狀態的真實資料時要重新核對（見 ${SOURCE_LABEL} 檔頭）。`)
      }
    }
  }
}

// ---------------------------------------------------------------------------
// 輸出
// ---------------------------------------------------------------------------

if (failures.length > 0) {
  console.error(`\n✗ matches.status 單一來源檢查未通過（${failures.length} 項）：\n`)
  for (const f of failures) console.error(`  - ${f}`)
  console.error(`\n真實來源是 ${SOURCE_LABEL} 的 MATCH_STATUS_MAP、值域真實來源是 ${SCHEMA_LABEL} 的 `
    + `CK_matches_status；背景見 docs/18-work-errors.md E-39 交付報告。\n`)
  process.exit(1)
}

console.log(`✓ matches.status 單一來源檢查通過（掃了 ${webFiles.length} 個前端檔案找不到第二份對照表；`
  + `MATCH_STATUS_MAP 的鍵集合與 ${SCHEMA_LABEL} 的 CK_matches_status 值域完全一致；`
  + `${SEED_FILE_REL} 的字面值皆在值域內）`)
for (const n of notices) console.log(`  ${n}`)
