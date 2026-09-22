#!/usr/bin/env node
/**
 * check-club-copy.mjs — 驗證 shared/utils/club-copy.ts 的文案鍵兩個俱樂部都有填。
 *
 * ## 為什麼需要這支腳本（不要以為「型別已經寫了就夠」）
 *
 * `club-copy.ts` 的 `ClubText<T>` / `ClubBlock<T>` 都是 `Record<ClubCode, …>`，
 * 漏填 `bw` 在 TypeScript 是 `TS2741` 編譯錯誤。**但這個保證在本專案的管線裡不成立**——
 * 2026-09-22 實測：把 `HOME_SEO.bw` 整個拿掉之後
 *
 *   - `npm run build`（Vite／esbuild 只做語法轉譯，不做型別檢查）→ **照樣成功，零警告**
 *   - `npm run lint`（eslint）→ **照樣是改動前那個既有的 1 error / 534 warnings，抓不到**
 *   - `npx vue-tsc --noEmit` → 抓得到，但**沒有人會記得手動跑**
 *
 * 也就是說，「漏填 bw 會擋下開發」原本只是一個**看起來存在、實際不會執行的保證**。
 * 真正會發生的是：漏填的那一頁在藍鯨容器上 SSR 時丟 `TypeError`，**壞在正式環境而不是建置時**。
 *
 * ⚠️ 這與 `docs/18-work-errors.md` `E-31` 是同一種形狀的問題——
 * **一個只在特定條件下才成立的機制，卻給出了全面的信心**。
 * 那次是「笛卡兒積只涵蓋文字、邊框仍靠人工列舉」，這次是「型別只在手動指令下被檢查」。
 *
 * 之所以不是直接把 `vue-tsc` 接進 `npm run lint`：本專案目前有一批與本檔無關的既有型別錯誤
 * （`useFetch` 的 `.items` 在多個新聞／賽程頁回傳 `{}`、`app.vue` 的 `useHead`、`academy/teams.vue`），
 * 貿然接進 CI 會直接紅燈，反而讓人習慣忽略它。**還完那批型別債之後，這支腳本可以由 `vue-tsc` 取代。**
 * 在那之前，這支腳本用結構檢查頂住同一個保證，不依賴型別檢查跑不跑得起來。
 *
 * ## 驗兩件事
 *
 * 1. **每個 `ClubText<…>` / `ClubBlock<…>` / `Record<ClubCode, …>` 常數，`tcrfc` 與 `bw` 都要有。**
 * 2. **每個帶 `zh:` 的物件都要有同層的 `en:`**（`CLAUDE.md` 全域規定第 4 條：
 *    前台可見的內容型別都要有 zh/en 雙欄位，**英文可以是 null，但欄位必須存在**）。
 *    藍鯨舊站 0 個英文字，英文是全新生產——欄位先開好，值後補，
 *    但**不能連欄位都沒有**，否則之後補英文時沒有地方可以放，只能回頭改型別。
 */

import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const HERE = dirname(fileURLToPath(import.meta.url))
const SOURCE = resolve(HERE, '../shared/utils/club-copy.ts')
const SOURCE_LABEL = 'shared/utils/club-copy.ts'

/** 兩個俱樂部代號，來源：shared/utils/club.ts 的 ClubCode */
const CLUBS = ['tcrfc', 'bw']

const src = readFileSync(SOURCE, 'utf8')

/** 把字元位移換成行號，報錯時才指得出位置 */
function lineOf(index) {
  return src.slice(0, index).split('\n').length
}

/**
 * 從 `{` 的位置往後找配對的 `}`，回傳結束位置。
 * 會跳過字串（單雙引號與範本字面值）與註解，否則文案裡的 `}`／`{` 會把括號配對算歪——
 * 本檔文案是中文，出現大括號的機率低，但 `aboutEyebrow` 那類組字註解裡有 `${}`。
 */
function matchBrace(start) {
  let depth = 0
  for (let i = start; i < src.length; i++) {
    const c = src[i]
    if (c === '"' || c === "'" || c === '`') {
      const quote = c
      i++
      while (i < src.length) {
        if (src[i] === '\\') { i += 2; continue }
        if (src[i] === quote) break
        i++
      }
      continue
    }
    if (c === '/' && src[i + 1] === '/') { i = src.indexOf('\n', i); if (i < 0) break; continue }
    if (c === '/' && src[i + 1] === '*') { i = src.indexOf('*/', i) + 1; continue }
    if (c === '{') depth++
    else if (c === '}') { depth--; if (depth === 0) return i }
  }
  return -1
}

/** 取出一個物件字面值裡「第一層」的鍵名 */
function topLevelKeys(openBrace, closeBrace) {
  const keys = []
  let depth = 0
  for (let i = openBrace; i < closeBrace; i++) {
    const c = src[i]
    if (c === '"' || c === "'" || c === '`') {
      const quote = c
      i++
      while (i < closeBrace) {
        if (src[i] === '\\') { i += 2; continue }
        if (src[i] === quote) break
        i++
      }
      continue
    }
    if (c === '/' && src[i + 1] === '/') { i = src.indexOf('\n', i); if (i < 0) break; continue }
    if (c === '/' && src[i + 1] === '*') { i = src.indexOf('*/', i) + 1; continue }
    if (c === '{' || c === '[') { depth++; continue }
    if (c === '}' || c === ']') { depth--; continue }
    if (depth === 1) {
      const rest = src.slice(i)
      const m = /^([A-Za-z_$][\w$]*)\s*:/.exec(rest)
      if (m) { keys.push({ name: m[1], index: i }); i += m[0].length - 1 }
    }
  }
  return keys
}

const failures = []
const checked = []

// ---------------------------------------------------------------------------
// 檢查 1：每個依俱樂部切換的常數，兩家都要有
// ---------------------------------------------------------------------------

// 型別註記裡出現這三種其中之一，就是「依俱樂部切換」的常數
const CLUB_KEYED = /^export const ([A-Z][A-Z0-9_]*)\s*:\s*([^=]*?)=\s*\{/gm

let m
while ((m = CLUB_KEYED.exec(src)) !== null) {
  const [, name, typeAnnotation] = m
  const isClubKeyed = /ClubText\s*<|ClubBlock\s*<|Record\s*<\s*ClubCode/.test(typeAnnotation)
  if (!isClubKeyed) continue

  const open = src.indexOf('{', m.index + m[0].length - 1)
  const close = matchBrace(open)
  if (close < 0) {
    failures.push(`${name}（第 ${lineOf(m.index)} 行）：物件字面值的大括號配對不起來，腳本無法檢查`)
    continue
  }

  const keys = topLevelKeys(open, close).map((k) => k.name)
  const missing = CLUBS.filter((c) => !keys.includes(c))
  if (missing.length > 0) {
    failures.push(
      `${name}（第 ${lineOf(m.index)} 行）：缺少 ${missing.map((c) => `\`${c}\``).join('、')} ——`
      + ` 依俱樂部切換的文案鍵，兩個俱樂部都必須有值。`
      + `漏填不會被 \`npm run build\` 擋下，會在該站 SSR 時丟 TypeError。`,
    )
  } else {
    checked.push(name)
  }
}

if (checked.length === 0 && failures.length === 0) {
  failures.push(
    `在 ${SOURCE_LABEL} 裡一個「依俱樂部切換的常數」都沒找到。`
    + `這通常代表命名慣例變了而這支腳本沒跟著改——`
    + `**空清單通過**正是 E-31 那類「防護的覆蓋範圍與它給人的信心不相稱」的典型，所以這裡直接判定失敗。`,
  )
}

// ---------------------------------------------------------------------------
// 檢查 2：有 zh 就要有 en（全域規定第 4 條）
// ---------------------------------------------------------------------------

let bilingualChecked = 0
for (let i = 0; i < src.length; i++) {
  if (src[i] !== '{') continue
  const close = matchBrace(i)
  if (close < 0) continue
  const keys = topLevelKeys(i, close).map((k) => k.name)
  if (keys.includes('zh')) {
    bilingualChecked++
    if (!keys.includes('en')) {
      failures.push(
        `第 ${lineOf(i)} 行的物件有 \`zh\` 但沒有 \`en\`——`
        + `全域規定第 4 條：前台可見的內容型別都要有 zh／en 雙欄位，`
        + `**英文值可以是 null，但欄位必須存在**（藍鯨英文是全新生產，欄位先開好值後補）。`,
      )
    }
  }
}

// ---------------------------------------------------------------------------
// 檢查 2 的覆蓋範圍揭露（🔴 不要刪掉這一段）
//
// 上面那個檢查只涵蓋 `{ zh, en }` 這種形狀的物件。但本檔多數文案用的是
// `titleZh` / `descZh` / `headlineZh` 這種**後綴命名**，它們沒有對應的 `*En` 欄位。
//
// 這不是腳本的漏洞，是**現況**：`apps/web` 目前只有 zh 頁面上線，英文版尚未開工
// （藍鯨舊站 0 個英文字，英文是全新生產不是翻譯，見 STATUS.md C-10）。
//
// ⚠️ **這裡刻意把數字印出來而不是默默通過**——否則「雙語檢查通過」這句話會讓人以為
// 全站雙語欄位都驗過了，而實際上只驗到 3 個。`E-31` 的教訓就是
// 「局部套用的機制會給出全面的信心」，所以覆蓋範圍要自己說出來。
//
// 英文版開工時，這裡要改成硬性檢查（`*Zh` 必須有 `*En` 兄弟欄位）並移除本段說明。
// ---------------------------------------------------------------------------

const zhSuffixFields = new Set()
for (const mm of src.matchAll(/\b([a-z][\w$]*)Zh\s*:/g)) zhSuffixFields.add(`${mm[1]}Zh`)
const missingEnSibling = [...zhSuffixFields].filter(
  (f) => !new RegExp(`\\b${f.slice(0, -2)}En\\s*:`).test(src),
)

// ---------------------------------------------------------------------------
// 輸出
// ---------------------------------------------------------------------------

if (failures.length > 0) {
  console.error(`\n✗ 藍鯨／磐石文案完整性檢查未通過（${failures.length} 項）：\n`)
  for (const f of failures) console.error(`  - ${f}`)
  console.error(`\n真實來源是 ${SOURCE_LABEL}；規則見 docs/13-blue-whale-site.md §6 紀律 11。\n`)
  process.exit(1)
}

console.log(
  `✓ 文案完整性檢查通過（${checked.length} 個依俱樂部切換的常數兩家都有值、`
  + `${bilingualChecked} 個 { zh, en } 物件都有 en 欄位）`,
)
if (missingEnSibling.length > 0) {
  console.log(
    `  ⚠️ 覆蓋範圍揭露：另有 ${missingEnSibling.length} 種 \`*Zh\` 後綴欄位沒有對應的 \`*En\`，`
    + `本檢查**沒有**涵蓋它們。`,
  )
  console.log(
    `     這是現況不是缺陷——apps/web 目前只有 zh 頁面上線，英文版尚未開工（STATUS.md C-10）。`
    + `英文版開工時要把這裡改成硬性檢查。`,
  )
}
