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
 * （apps/web/README.md「S1-18」節）。因此本機驗收這支腳本時，**FAQ 相關路由預期全部
 * 「沒有 FAQPage 節點」**——這是 GEO-05「資料不足時不輸出該型別」的正確行為，不是
 * 這支腳本或 `useFaqPageSchema()` 接錯線。這裡仍然值得跑：能驗證「沒有資料時前台
 * 不會 500、也不會硬塞一個空的 FAQPage」，等 `apps/api` 真的啟動、`faqs` 表有真實
 * 種子資料之後，同一支腳本會開始真的驗到 `mainEntity` 的內容，不需要改程式碼。
 *
 * ## 檢查範圍：12 FAQ 首頁、4 個獨立主題頁、3 個 G-12 嵌入頁 ＋ 3 個型別猜測受害頁，各自 zh／en
 *
 * 後 3 個（`/zh/about/`／`/zh/checkout/`／`/zh/join/contact/`）只為了驗「頁面型別不得
 * 由網址猜測」而納入，跟 GEO-06／FAQPage 本身無關（見下方該節說明）。
 *
 * 藍鯨（`bw`）容器：BW-C1（2026-09-29）修正 S1-15／S2-8／S2-10 誤把「藍鯨沒有對應
 * 內容」當成整頁 404 的理由後，`programs-camps`／`childrens-training`／`summer-camp`
 * 等頁面已重開（見 shared/utils/units.ts 檔頭）。目前藍鯨容器只剩 `academy-admission`
 * （12.2）與 `academy/join`（4.7）預期 404——依附同一項總則例外（04 不沿用招生與
 * 課程報名架構），其餘路由一律預期 200，不是這支腳本要抓的缺陷——同
 * `check-heading-structure.mjs` 對 404 路由的既有處理方式，遇到 404 就跳過。
 *
 * ## 用法
 *
 *   1. 依 `apps/web/README.md`「本機測試兩個 club」把前台跑起來。
 *   2. `node scripts/check-faq-schema-live.mjs [--base-url=http://127.0.0.1:3001]`
 *      （預設 `http://127.0.0.1:3001`，即 `tcrfc` 容器；對 `bw` 容器另外帶
 *      `--base-url=http://127.0.0.1:3002` 或本機埠號跑一次）。
 *
 * 離開碼：任一頁的 JSON-LD 解析失敗、找到的 FAQPage 節點缺 `mainEntity`／
 * 任一題缺 `name`／`acceptedAnswer.text`、或任一頁的 Primary WebPage 節點意外帶有
 * 「頁面型別自動猜測表」裡的其他型別（`AboutPage`／`ContactPage`／`CheckoutPage`／
 * `SearchResultsPage`，見下方「頁面型別不得由網址猜測」）→ `1`；其餘情況（含「整站
 * 都沒有 FAQPage，因為還沒有種子資料」）→ `0`。
 */

// ── 頁面型別不得由網址猜測（`docs/18-work-errors.md` E-74，2026-09-29 修正） ──────
//
// 根因：`nuxt-schema-org` 的 `webPageResolver.defaults()`（`node_modules/nuxt-schema-org/
// dist/schema.mjs`）依「這一頁網址最後一段路徑」猜頁面型別（內建對照表：`about`／
// `about-us`→`AboutPage`、`search`→`SearchResultsPage`、`checkout`→`CheckoutPage`、
// `contact`／`get-in-touch`／`contact-us`→`ContactPage`、`faq`→`FAQPage`），而這個
// 「最後一段路徑」算法（`meta.url.substring(meta.url.lastIndexOf("/") + 1)`）吃的是
// `nuxt-site-config` 解析出來的網址——本站 `nuxt.config.ts` 的 `site.trailingSlash`
// 先前沒有設定（預設 falsy），`site-config-stack/dist/urls.mjs` 的 `fixSlashes()`
// 因此把這個網址一律去掉結尾斜線，讓 `/zh/faq/`／`/zh/about/`／`/zh/checkout/`／
// `/zh/join/contact/`（各自的 `/en/` 孿生路由同樣中招，共 8 條路由）全部誤判成猜測表
// 裡的型別，且都沒有對應的 `mainEntity` 等必要欄位——`/zh/faq/` 只是其中一條，不是
// 唯一一條，也不是本檔案能修的問題（真正修法在框架設定層）。
//
// 修法：`nuxt.config.ts` 的 `site` 已加上 `trailingSlash: true`（完整原始碼追查記錄與
// 修法說明見該檔案的行內註解與 `docs/18-work-errors.md` E-74），讓這個網址一律帶結尾
// 斜線，`endPath` 因此變成空字串，猜測表不再命中任何關鍵字，型別退回預設的
// `WebPage`——已用本機真實 SSR 輸出實測確認全部 8 條路由（含 zh／en）都不再輸出這些
// 猜測型別。這裡把原本針對 `/zh/faq/`／`/en/faq/` 的「已知限制」白名單**移除**（改成
// 下方通用的 hard-fail 檢查）：FAQPage 若沒有 `mainEntity` 一律是錯誤，不再有例外；
// 同時新增對其餘猜測表型別的檢查——**這幾個型別目前在本站沒有任何頁面刻意宣告**
// （只有 FAQPage 有 `useFaqPageSchema()` 這個明確宣告機制），所以只要在輸出裡看到
// `AboutPage`／`ContactPage`／`CheckoutPage`／`SearchResultsPage`，就代表網址猜測
// 又回來了（例如 `trailingSlash` 設定被還原）或有人新增了未經宣告機制產出的型別，
// 兩種情況都該讓這支腳本 fail，而不是悄悄放行。
const URL_GUESSED_TYPES_WITHOUT_DECLARATION = ['AboutPage', 'ContactPage', 'CheckoutPage', 'SearchResultsPage']

const ROUTES_ZH = [
  '/zh/faq/',
  '/zh/faq/join-team/',
  '/zh/faq/academy-admission/',
  '/zh/faq/programs-camps/',
  '/zh/faq/fees-refunds/',
  '/zh/academy/join/',
  '/zh/programs/childrens-training/',
  '/zh/programs/summer-camp/',
  // S2-8／S2-10（2026-09-29）新增消費 useFaqPageSchema 的四個頁面：
  // opportunities 用 'trials' 掛載點，其餘三個用 'program_detail'（同
  // childrens-training／summer-camp 既有掛載點）。
  '/zh/club/opportunities/',
  '/zh/programs/winter-camp/',
  '/zh/programs/specialist/',
  '/zh/programs/school-community/',
]
// E-74 修正後追加：`/zh/about/`／`/zh/checkout/`／`/zh/join/contact/` 是全站另外
// 3 條撞上 `nuxt-schema-org` 猜測表關鍵字的路由（`about`／`checkout`／`contact`，
// 見上方「頁面型別不得由網址猜測」），本身跟 FAQPage／GEO-06 無關，但同一套
// hard-fail 邏輯（`URL_GUESSED_TYPES_WITHOUT_DECLARATION`）需要真的 fetch 到這幾頁
// 才驗得到，這裡一併納入路由清單，不另開一支腳本。
const ROUTES_ZH_TYPE_GUESS_ONLY = ['/zh/about/', '/zh/checkout/', '/zh/join/contact/']
const routes = [...new Set([
  ...ROUTES_ZH,
  ...ROUTES_ZH_TYPE_GUESS_ONLY,
  ...ROUTES_ZH.map((r) => `/en${r.slice(3)}`),
  ...ROUTES_ZH_TYPE_GUESS_ONLY.map((r) => `/en${r.slice(3)}`),
])]

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

    // 通用檢查：Primary WebPage 節點（或任何節點）意外帶有猜測表型別、但本站沒有
    // 對應的宣告機制 → 一律 hard-fail（見檔頭「頁面型別不得由網址猜測」）。
    for (const node of graph) {
      const type = node?.['@type']
      const types = Array.isArray(type) ? type : type ? [type] : []
      for (const guessedType of URL_GUESSED_TYPES_WITHOUT_DECLARATION) {
        if (types.includes(guessedType)) {
          shapeErrors.push({
            route,
            reason: `節點意外帶有 "${guessedType}"（本站目前沒有任何頁面宣告這個型別，代表網址猜測又回來了——見 E-74）`,
          })
        }
      }
    }

    const faqPageNode = graph.find((n) => {
      const type = n?.['@type']
      return type === 'FAQPage' || (Array.isArray(type) && type.includes('FAQPage'))
    })
    if (!faqPageNode) continue

    pagesWithFaqPage++
    const mainEntity = Array.isArray(faqPageNode.mainEntity)
      ? faqPageNode.mainEntity
      : faqPageNode.mainEntity ? [faqPageNode.mainEntity] : []

    // E-74 修正後：FAQPage 缺 mainEntity 一律是錯誤，不再有「已知限制」白名單。
    if (mainEntity.length === 0) {
      shapeErrors.push({ route, reason: 'FAQPage 節點存在，但 mainEntity 是空的（GEO-05：不該輸出空的 FAQPage）' })
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
  if (pagesWithFaqPage === 0) {
    console.log('ℹ️ 目前沒有任何一頁輸出 FAQPage——`apps/api` 未啟動或 `faqs` 表沒有種子資料時，')
    console.log('   這是 GEO-05「資料不足時不輸出該型別」的正確行為，不是缺陷。')
  } else {
    console.log('✓ 全數通過（含頁面型別不得由網址猜測的檢查，見檔頭 E-74）')
  }
} else {
  console.error(`✗ ${shapeErrors.length} 處形狀錯誤：`)
  for (const e of shapeErrors) console.error(`  - ${e.route}：${e.reason}`)
}

if (parseErrors.length > 0 || shapeErrors.length > 0) {
  process.exit(1)
}

console.log('\n✓ GEO-06 FAQPage JSON-LD 檢查與頁面型別檢查（E-74）通過。')
