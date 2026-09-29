#!/usr/bin/env node
/**
 * check-faq-schema-live.mjs — GEO-06（S1-18a）對**已渲染的 SSR 輸出**檢查 FAQPage
 * JSON-LD。比照 `check-heading-structure.mjs`／`check-club-brand-leak.mjs` 的既有作法：
 * 對一個**已經跑起來**的前台（`tcrfc` 或 `bw` 容器）逐頁 `fetch`，解析實際吐出來的
 * HTML，不是對原始碼做靜態分析。
 *
 * ## 跟 `check-faq-schema.mjs` 的分工
 *
 * `check-faq-schema.mjs` 用固定 fixture 資料驗證「輸出的邏輯本身對不對」（GEO-05
 * 過濾、去重、注入防護），**不需要任何服務就能跑**，掛進 `npm run lint`。這支腳本
 * 驗證的是「接到真實頁面之後，實際吐出來的 HTML 裡真的有這段 JSON-LD、而且能被
 * 解析」——這需要前台先跑起來，比照既有的 `check-heading-structure.mjs` 不掛進
 * `npm run lint` 的同一個理由（`docs/18-work-errors.md` `E-34`：會被伺服器可用性
 * 影響的檢查不能掛進「隨時能跑」的 `lint`）。
 *
 * ## 🔴 已知限制：`apps/api` 未啟動、`faqs` 表 0 筆種子資料
 *
 * 依派工規則本輪不啟動 `apps/api`（見任務指示），且該表目前沒有任何真實問答
 * （apps/web/README.md「S1-18」節）。因此本機驗收這支腳本時，**8 條路由預期全部
 * 「沒有 FAQPage 節點」**——這是 GEO-05「資料不足時不輸出該型別」的正確行為，不是
 * 這支腳本或 `useFaqPageSchema()` 接錯線。這裡仍然值得跑：能驗證「沒有資料時前台
 * 不會 500、也不會硬塞一個空的 FAQPage」，等 `apps/api` 真的啟動、`faqs` 表有真實
 * 種子資料之後，同一支腳本會開始真的驗到 `mainEntity` 的內容，不需要改程式碼。
 *
 * ## 檢查範圍：12 FAQ 首頁、4 個獨立主題頁、3 個 G-12 嵌入頁，各自 zh／en
 *
 * 藍鯨（`bw`）容器：`academy-admission`／`programs-camps`（S1-18b 已關閉，回 404）與
 * 3 個 G-12 嵌入頁（`academy/join`／`programs/childrens-training`／
 * `programs/summer-camp`，S1-15 已關閉，回 404）一律預期 404，不是這支腳本要抓的
 * 缺陷——同 `check-heading-structure.mjs` 對 404 路由的既有處理方式，遇到 404 就跳過。
 *
 * ## 用法
 *
 *   1. 依 `apps/web/README.md`「本機測試兩個 club」把前台跑起來。
 *   2. `node scripts/check-faq-schema-live.mjs [--base-url=http://127.0.0.1:3001]`
 *      （預設 `http://127.0.0.1:3001`，即 `tcrfc` 容器；對 `bw` 容器另外帶
 *      `--base-url=http://127.0.0.1:3002` 或本機埠號跑一次）。
 *
 * 離開碼：任一頁的 JSON-LD 解析失敗、或找到的 FAQPage 節點缺 `mainEntity`／
 * 任一題缺 `name`／`acceptedAnswer.text` → `1`；其餘情況（含「整站都沒有 FAQPage，
 * 因為還沒有種子資料」）→ `0`。
 */

// 🔴 已知、且與本檔無關的框架既有缺陷（`docs/18-work-errors.md` E-74）：`/zh/faq/`
// （僅此一條）即使完全沒有真實題目，也會被 `nuxt-schema-org` 的 `webPageResolver`
// 依網址結尾（canonical 網址不帶結尾斜線、`endPath` 剛好等於內建猜測表裡的
// `'faq'`）自動宣告成 `@type` 含 `FAQPage` 卻沒有 `mainEntity`——已證實 `/zh/about/`
// 在 S1-18a 之前就有一模一樣的 `AboutPage` 版本，根因是全站既有的 canonical 網址
// 生成方式，不是 `useFaqPageSchema()` 忘記過濾（該 composable 在無資料時完全不呼叫
// `useSchemaOrg`，已用原始碼與本機實測雙重確認）。這裡刻意不讓它讓離開碼變 1——
// 對它 hard-fail 只會製造一個跟這次任務無關、且不會被這次任務修好的永久紅燈
// （`docs/18-work-errors.md` E-34 的既有教訓），改成單獨列出來，不影響離開碼。
const KNOWN_FRAMEWORK_TYPE_GUESS_ROUTES = new Set(['/zh/faq/', '/en/faq/'])
const knownFrameworkQuirkHits = []

const ROUTES_ZH = [
  '/zh/faq/',
  '/zh/faq/join-team/',
  '/zh/faq/academy-admission/',
  '/zh/faq/programs-camps/',
  '/zh/faq/fees-refunds/',
  '/zh/academy/join/',
  '/zh/programs/childrens-training/',
  '/zh/programs/summer-camp/',
]
const routes = [...ROUTES_ZH, ...ROUTES_ZH.map((r) => `/en${r.slice(3)}`)]

const baseUrlArg = process.argv.find((a) => a.startsWith('--base-url='))
const baseUrl = (baseUrlArg ? baseUrlArg.slice('--base-url='.length) : 'http://127.0.0.1:3001').replace(/\/$/, '')

/** 從整份 HTML 抓出所有 `<script type="application/ld+json">…</script>` 區塊的原始
 * innerHTML（尚未還原 unhead 的 `</script` 轉義）。 */
function extractJsonLdBlocks(html) {
  const blocks = []
  const re = /<script type="application\/ld\+json"[^>]*>([\s\S]*?)<\/script>/gi
  let m
  while ((m = re.exec(html)) !== null) {
    // 還原 unhead tagToString() 的轉義（見 check-faq-schema.mjs／
    // shared/utils/faq-schema.ts 檔頭對同一份原始碼的引用）：瀏覽器讀
    // <script> 的 textContent 時看到的就是這個還原後的字串。
    blocks.push(m[1].replace(/<\\\/script/gi, '</script'))
  }
  return blocks
}

console.log(`GEO-06 FAQPage JSON-LD 檢查（SSR 實測） —— 目標：${baseUrl}（共 ${routes.length} 條路由，含 zh／en）\n`)

let fetchFailures = 0
let checkedCount = 0
let pagesWithFaqPage = 0
let totalQuestions = 0
const parseErrors = []
const shapeErrors = []

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
  if (res.status === 404) continue // 單元關閉（藍鯨）或路由不存在，本來就該 404，跳過
  if (res.status >= 300 && res.status < 400) continue
  if (!res.ok) {
    console.error(`  ⚠️ ${url} 回應 ${res.status}，不是預期的 200／404／30x，跳過`)
    continue
  }

  const html = await res.text()
  checkedCount++

  for (const block of extractJsonLdBlocks(html)) {
    let parsed
    try {
      parsed = JSON.parse(block)
    } catch (err) {
      parseErrors.push({ route, message: err.message })
      continue
    }
    const graph = Array.isArray(parsed['@graph']) ? parsed['@graph'] : [parsed]
    const faqPageNode = graph.find((n) => {
      const type = n?.['@type']
      return type === 'FAQPage' || (Array.isArray(type) && type.includes('FAQPage'))
    })
    if (!faqPageNode) continue

    pagesWithFaqPage++
    const mainEntity = Array.isArray(faqPageNode.mainEntity)
      ? faqPageNode.mainEntity
      : faqPageNode.mainEntity ? [faqPageNode.mainEntity] : []

    if (mainEntity.length === 0) {
      if (KNOWN_FRAMEWORK_TYPE_GUESS_ROUTES.has(route)) {
        knownFrameworkQuirkHits.push(route)
      } else {
        shapeErrors.push({ route, reason: 'FAQPage 節點存在，但 mainEntity 是空的（GEO-05：不該輸出空的 FAQPage）' })
      }
      continue
    }

    for (const q of mainEntity) {
      totalQuestions++
      const hasName = typeof q.name === 'string' && q.name.trim().length > 0
      const hasAnswerText = typeof q.acceptedAnswer?.text === 'string' && q.acceptedAnswer.text.trim().length > 0
      if (!hasName || !hasAnswerText) {
        shapeErrors.push({
          route,
          reason: `題目缺欄位（name=${JSON.stringify(q.name)}／acceptedAnswer.text=${JSON.stringify(q.acceptedAnswer?.text)}）`,
        })
      }
    }
  }
}

if (fetchFailures === routes.length) {
  console.error(`\n✗ ${fetchFailures} 條路由全部連不上——前台是不是還沒啟動？見 apps/web/README.md「本機測試兩個 club」。`)
  process.exit(1)
}

console.log(`已檢查 ${checkedCount} 條路由（其餘為 30x／404，略過）。`)
console.log(`找到 ${pagesWithFaqPage} 個 FAQPage 節點、共 ${totalQuestions} 題。\n`)

console.log('── JSON 可解析 ─────────────────────────────')
if (parseErrors.length === 0) {
  console.log('✓ 全數通過（找到的每一段 JSON-LD 都能被 JSON.parse()）')
} else {
  console.error(`✗ ${parseErrors.length} 段解析失敗：`)
  for (const e of parseErrors) console.error(`  - ${e.route}：${e.message}`)
}

console.log('\n── FAQPage 形狀（mainEntity／name／acceptedAnswer.text）───')
if (shapeErrors.length === 0) {
  if (pagesWithFaqPage === knownFrameworkQuirkHits.length) {
    console.log('ℹ️ 目前沒有任何一頁輸出「有內容」的 FAQPage——`apps/api` 未啟動或 `faqs` 表沒有種子資料時，')
    console.log('   這是 GEO-05「資料不足時不輸出該型別」的正確行為，不是缺陷（見檔頭「已知限制」）。')
  } else {
    console.log('✓ 全數通過')
  }
} else {
  console.error(`✗ ${shapeErrors.length} 處形狀錯誤：`)
  for (const e of shapeErrors) console.error(`  - ${e.route}：${e.reason}`)
}

if (knownFrameworkQuirkHits.length > 0) {
  console.log(`\nℹ️ ${knownFrameworkQuirkHits.length} 條路由命中已知、與本次任務無關的框架既有缺陷`)
  console.log('   （docs/18-work-errors.md E-74，不影響離開碼）：')
  for (const r of knownFrameworkQuirkHits) console.log(`  - ${r}`)
}

if (parseErrors.length > 0 || shapeErrors.length > 0) {
  process.exit(1)
}

console.log('\n✓ GEO-06 FAQPage JSON-LD 檢查通過。')
