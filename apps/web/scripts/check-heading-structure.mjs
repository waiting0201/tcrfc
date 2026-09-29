#!/usr/bin/env node
/**
 * check-heading-structure.mjs — GEO-07（S1-12e）可擷取的內容結構，對**已渲染的 SSR
 * 輸出**逐頁檢查。
 *
 * ## 為什麼要對 SSR 輸出檢查，不是對原始碼
 *
 * 標題大綱是「元件組合之後」的結果，不是單一 .vue 檔案能看全的——`SiteHeader.vue`／
 * `SiteFooter.vue`／`MembershipBenefits.vue` 這些共用元件的標題會插進每一頁的最終
 * DOM 裡，而它們在原始碼裡看不到「插進去之後前後接的是哪一級標題」。這支腳本因此
 * 比照 `check-club-brand-leak.mjs` 的既有作法：對一個**已經跑起來**的前台（`tcrfc`
 * 或 `bw` 容器）逐頁 `fetch`，解析實際吐出來的 HTML。
 *
 * ## 為什麼不掛進 `npm run lint`
 *
 * 跟 `check-club-brand-leak.mjs` 同一個理由（見該檔案檔頭「為什麼不掛進 npm run
 * lint」）：`npm run lint` 目前純靜態、秒級跑完，這支腳本需要先把前台跑起來才能執行，
 * 硬掛上去會讓 `lint` 依賴外部環境、失去「隨時能跑」的性質（`docs/18-work-errors.md`
 * `E-34`）。用法見 `apps/web/README.md`「S1-12e」節。
 *
 * ## 檢查三件事（對應 GEO-07 逐項）
 *
 * 1. **H1 唯一**：每頁恰好一個 `<h1>`，且文字內容非空（不得整個 H1 只靠圖片撐版面，
 *    圖片沒有文字節點，`h1.textContent` 會是空字串，藉此抓到「H1 被圖片取代」）。
 *    根路徑 `/`（語系偵測轉址頁，回應 3xx）與動態文章頁（`news/[slug]`，本機沒有
 *    `apps/api` 可用時恆 404，見下方路由收集的排除規則）不計入這項。
 * 2. **標題層級不跳階**：整份 HTML 的標題（`<h1>`–`<h6>`）依文件順序检查，任一標題
 *    的「有效層級」不得比目前為止看過的最大層級多 1 以上（可以往回降到任何層級，
 *    只有「往下跳」的方向有限制——這是 axe-core `heading-order` 規則同一套判斷方式）。
 *    「有效層級」：優先讀 `aria-level` 屬性（`SiteFooter.vue` 的 h4 標題用這個技巧
 *    覆寫成 2 級，理由見該檔案檔頭），沒有才用標籤本身的數字。
 * 3. **首段摘要段結構性檢查**：H1 之後最近的一個 `<p>` 是否存在、內容是否為已知的
 *    「占位／流程骨架」標記（`pending-inline`／`mock-flag`／文字含「待補」「待確認」
 *    「示意」）。**這項不影響離開碼**——沒有真實內容可寫的頁面（購物車／結帳流程骨架、
 *    2 個商品頁材質與描述待補）是內容缺口，不是程式錯誤，比照 `check-club-brand-leak.mjs`
 *    的「進度計不影響離開碼」同一個理由（`E-34`：對缺內容的頁面 hard-fail 會製造永久
 *    紅燈）。這裡只負責**列出來**，不臆造文案去填滿它（任務指示明文禁止）。
 *
 * **明文不做的事**（誠實列出邊界，不是漏做）：
 * - 不判斷「首段是否真的獨立成立、不依賴上下文即可理解」這種語意問題——這是編輯
 *   判斷，不是結構檢查能做的事，這裡只驗「有沒有一段看起來像摘要的文字」。
 * - 不掃描「圖片排版承載文字」的一般情形（例如把整段內文做成一張圖）——本站目前
 *   全站是語意化 HTML＋CSS 排版，沒有這種既有樣式可以自動比對特徵，留給人工審查。
 *
 * ## 用法
 *
 *   1. 依 `apps/web/README.md`「本機測試兩個 club」把前台跑起來（tcrfc 預設
 *      `http://127.0.0.1:3001`，bw 預設 `http://127.0.0.1:3002`）。
 *   2. `node scripts/check-heading-structure.mjs [--base-url=http://127.0.0.1:3001]`
 *
 * 離開碼：任一頁 H1 不是恰好一個、H1 文字為空、或出現標題跳階 → `1`；
 * 首段摘要段的占位／缺漏只列出來，不影響離開碼 → 其餘情況 `0`。
 */

import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { readdirSync, statSync } from 'node:fs'

const HERE = dirname(fileURLToPath(import.meta.url))
const PAGES_DIR = resolve(HERE, '../app/pages')

// ---------------------------------------------------------------------------
// 路由清單：從 app/pages 檔案樹算出來（比照 check-club-brand-leak.mjs 的既有作法，
// 不手動維護一份會過期的清單）。同時輸出 /zh/ 與 /en/ 兩份（S1-13 的 pages:extend
// 孿生路由，同一個檔案），雖然標題結構理論上兩者一致，但實際 fetch 兩份能同時當
// GEO-08 canonical／lang 的附帶抽查（見 README 使用說明），成本很低。
//
// 排除動態路由（檔名或目錄含 `[`）——`news/[slug]` 沒有 apps/api／資料庫時打不到
// 真實文章，恆 404（任務指示明文不啟動 apps/api），跳過。
// ---------------------------------------------------------------------------
function collectRoutes(dir, base = '') {
  const routes = []
  for (const entry of readdirSync(dir)) {
    if (entry.includes('[')) continue
    const full = resolve(dir, entry)
    if (statSync(full).isDirectory()) {
      routes.push(...collectRoutes(full, `${base}/${entry}`))
    } else if (entry.endsWith('.vue')) {
      const name = entry.replace(/\.vue$/, '')
      const path = name === 'index' ? `${base}/` : `${base}/${name}/`
      routes.push(path || '/')
    }
  }
  return routes
}

const zhRoutes = [...new Set(collectRoutes(PAGES_DIR))].filter((r) => r.startsWith('/zh/')).sort()
const routes = [...zhRoutes, ...zhRoutes.map((r) => `/en${r.slice(3)}`)]

// ---------------------------------------------------------------------------
// 標題解析：從整份 HTML 依文件順序抓出 <h1>–<h6>，回傳 { level, effectiveLevel, text }。
// ---------------------------------------------------------------------------
function parseHeadings(html) {
  const headings = []
  const re = /<h([1-6])\b([^>]*)>([\s\S]*?)<\/h\1>/gi
  let m
  while ((m = re.exec(html)) !== null) {
    const level = Number(m[1])
    const attrs = m[2]
    const inner = m[3]
    const ariaLevelMatch = attrs.match(/aria-level="(\d)"/)
    const effectiveLevel = ariaLevelMatch ? Number(ariaLevelMatch[1]) : level
    const text = inner.replace(/<[^>]+>/g, '').replace(/&nbsp;/gi, ' ').replace(/\s+/g, ' ').trim()
    headings.push({ level, effectiveLevel, text, raw: m[0].slice(0, 60) })
  }
  return headings
}

// ---------------------------------------------------------------------------
// 首段摘要段：從第一個 <h1> 結束位置往後找最近的 <p ...>...</p>。
// ---------------------------------------------------------------------------
const PLACEHOLDER_MARKERS = ['pending-inline', 'mock-flag', '待補', '待確認', '流程骨架', '示意內容', '示意']

function findLeadParagraph(html) {
  const h1Close = html.search(/<\/h1>/i)
  if (h1Close === -1) return { status: 'NO_H1' }
  const after = html.slice(h1Close, h1Close + 4000)
  const m = /<p\b([^>]*)>([\s\S]*?)<\/p>/i.exec(after)
  if (!m) return { status: 'MISSING' }
  const attrs = m[1]
  const text = m[2].replace(/<[^>]+>/g, '').replace(/\s+/g, ' ').trim()
  const isPlaceholder = PLACEHOLDER_MARKERS.some((marker) => attrs.includes(marker) || text.includes(marker))
  if (text.length === 0) return { status: 'EMPTY' }
  if (isPlaceholder) return { status: 'PLACEHOLDER', text }
  return { status: 'OK', text }
}

// ---------------------------------------------------------------------------
// 主流程
// ---------------------------------------------------------------------------
const baseUrlArg = process.argv.find((a) => a.startsWith('--base-url='))
const baseUrl = (baseUrlArg ? baseUrlArg.slice('--base-url='.length) : 'http://127.0.0.1:3001').replace(/\/$/, '')

console.log(`標題結構檢查（GEO-07） —— 目標：${baseUrl}（共 ${routes.length} 條路由，含 zh／en）\n`)

let fetchFailures = 0
let checkedCount = 0
const h1Violations = []
const skipViolations = []
const leadGaps = []

for (const route of routes) {
  const url = `${baseUrl}${route}`
  let res
  try {
    res = await fetch(url, { redirect: 'manual' })
  } catch (err) {
    fetchFailures++
    console.error(`  ⚠️ 無法連線 ${url}：${err.message}`)
    continue
  }
  if (res.status === 404) continue // 單元關閉或動態路由打不到，本來就該 404，跳過
  if (res.status >= 300 && res.status < 400) continue // 轉址頁，沒有自己的內容可查
  if (!res.ok) {
    console.error(`  ⚠️ ${url} 回應 ${res.status}，不是預期的 200／404／30x，跳過`)
    continue
  }

  const html = await res.text()
  checkedCount++

  const headings = parseHeadings(html)
  const h1s = headings.filter((h) => h.level === 1)
  if (h1s.length !== 1) {
    h1Violations.push({ route, count: h1s.length, reason: h1s.length === 0 ? '沒有 H1' : `有 ${h1s.length} 個 H1` })
  } else if (h1s[0].text.length === 0) {
    h1Violations.push({ route, count: 1, reason: 'H1 文字內容為空（可能被圖片取代，未提供文字節點）' })
  }

  let maxSeen = 0
  for (const h of headings) {
    if (maxSeen > 0 && h.effectiveLevel > maxSeen + 1) {
      skipViolations.push({
        route,
        from: maxSeen,
        to: h.effectiveLevel,
        text: h.text || h.raw,
      })
    }
    maxSeen = Math.max(maxSeen, h.effectiveLevel)
  }

  const lead = findLeadParagraph(html)
  if (lead.status !== 'OK') {
    leadGaps.push({ route, status: lead.status, text: lead.text })
  }
}

if (fetchFailures === routes.length) {
  console.error(`\n✗ ${fetchFailures} 條路由全部連不上——前台是不是還沒啟動？見 apps/web/README.md「本機測試兩個 club」。`)
  process.exit(1)
}

console.log(`已檢查 ${checkedCount} 條路由（其餘為 30x／404，略過）。\n`)

console.log(`── ① H1 唯一 ─────────────────────────────`)
if (h1Violations.length === 0) {
  console.log(`✓ 全數通過`)
} else {
  console.error(`✗ ${h1Violations.length} 條路由違反：`)
  for (const v of h1Violations) console.error(`  - ${v.route}：${v.reason}`)
}

console.log(`\n── ② 標題層級不跳階 ─────────────────────`)
if (skipViolations.length === 0) {
  console.log(`✓ 全數通過`)
} else {
  console.error(`✗ ${skipViolations.length} 處跳階：`)
  for (const v of skipViolations) console.error(`  - ${v.route}：h${v.from} → h${v.to}（「${v.text}」）`)
}

console.log(`\n── ③ 首段摘要段（不影響離開碼，見檔頭說明）───`)
if (leadGaps.length === 0) {
  console.log(`✓ 全數有非占位的首段摘要`)
} else {
  console.log(`ℹ️ ${leadGaps.length} 頁沒有可用的首段摘要（內容缺口，不是程式錯誤）：`)
  for (const g of leadGaps) console.log(`  - ${g.route}：${g.status}${g.text ? `（"${g.text.slice(0, 40)}"）` : ''}`)
}

if (h1Violations.length > 0 || skipViolations.length > 0) {
  process.exit(1)
}

console.log(`\n✓ GEO-07 結構檢查通過（H1 唯一、標題不跳階）。`)
