#!/usr/bin/env node
/**
 * check-club-brand-leak.mjs — 在**已渲染的藍鯨站 SSR 輸出**裡找磐石專屬詞彙殘留。
 *
 * ## 為什麼需要這支腳本（`E-42` 升級段點名的缺口）
 *
 * `check-club-copy.mjs` 只驗「`club-copy.ts` 的文案鍵兩家有沒有填值」——它連不上
 * 「填得對不對」也連不上「該引用的地方有沒有去引用」。`compare-dom.mjs` 驗「磐石站
 * 輸出 vs `site/dist` 基準像不像」——它比對的是**同一站跟自己的基準**，不是「兩隊之間
 * 該不該一致」，而**藍鯨根本沒有 mockup 可以當基準**。這兩支腳本合起來，仍然抓不到
 * 「藍鯨站印出磐石專屬詞彙」這一類錯誤（`docs/18-work-errors.md` `E-42` 升級段、
 * `STATUS.md` `S0-9n`）——S0-9n 就是這樣被**人工**抓到的：獨立審查實際帶對
 * `NUXT_PUBLIC_CLUB=bw` 跑一次 SSR 輸出才發現。
 *
 * 這支腳本把那次人工檢查自動化：**拿一份磐石專屬詞彙表，在藍鯨站已渲染的 SSR 輸出
 * 裡整批比對**——因為藍鯨輸出裡按定義不該出現磐石專屬詞。比對的是**最終渲染文字**，
 * 不是猜程式碼意圖，所以不會漏掉「欄位根本沒被引用」這一類（那一類在原始碼層級看
 * 起來什麼都沒錯——只是欄位沒被用到，只有把最終輸出印出來比對才看得見）。
 *
 * ## 🔴🔴 BW-C1 品牌外洩全站盤點（2026-09-29）：改為全站涵蓋、預設 hard-fail 🔴🔴
 *
 * 舊版只對一份手動維護的 `PROTECTED_PAGES`（33 頁、只含 `/zh/`）hard-fail，其餘頁面
 * 只計數。這個設計留下兩個真正的漏洞（本輪盤點才發現的既有缺口，不是新迴歸）：
 *   1. `PROTECTED_PAGES` 從未涵蓋 `/en/`——`check-heading-structure.mjs` 早就在對
 *      zh／en 兩份路由跑，這支腳本卻只挑 zh，等於藍鯨站英文版的品牌外洩從第一天
 *      起就沒有任何自動化檢查覆蓋。
 *   2. 「其餘頁面只計數，不影響離開碼」等於承認「沒被排進清單就不算數」——
 *      `join/international-player/index.vue`（10.4）就是這樣被漏掉的：`units.ts`
 *      從未關閉這個單元，藍鯨訪客一直看得到整頁固定寫死的「Taichung Rock FC」
 *      「TCRFC」「台中磐石足球俱樂部」「International Department」，但因為這頁
 *      不在 33 頁的手動清單裡，舊版腳本從頭到尾不會讓它讓 `lint`／驗收失敗。
 *
 * 新設計：**自動收集藍鯨站所有會回 200 的路由（zh／en 都收），每一頁預設
 * hard-fail**——命中詞表就是錯誤，不再有「其餘頁面只計數」這個灰色地帶。真正需要
 * 例外的頁面（例如描述兩隊關係、或已知且有明文規格依據的既有缺口）放進下方
 * `EXEMPT_PAGES`，每筆都要附**規格依據或既有缺口編號**，不是「看起來還好」就放行。
 *
 * ## 詞表怎麼挑（連同已排除的候選與理由）
 *
 * 進詞表：
 *   - `磐石`——覆蓋率最高、誤判風險最低的詞。
 *   - `TCRFC`——磐石英文縮寫。
 *   - `學院`——04 單元磐石叫「足球學院」、藍鯨依 docs/13 §3 改叫「青年隊」，這個詞
 *     在藍鯨語境下定義上不該出現。
 *   - `Taichung Rock`——磐石英文全名的核心詞組（`Taichung Rock FC`），比單獨的
 *     `Rock`（見下方「沒進詞表」）更精準，本輪盤點在 `join/international-player/`
 *     實測命中且不是 `磐石`／`TCRFC` 的子集合（英文獨立頁面，中文詞表未觸及）。
 *   - `www.tcrfc.tw`——磐石正式網域（含 `www.` 前綴，見
 *     `app/pages/zh/culture/merchandise/index.vue` 既有引用「舊官網
 *     `https://www.tcrfc.tw`」），藍鯨頁面不應該出現磐石自己的網域字面值。
 *     ⚠️ **實測排除過的候選：不含 `www.` 前綴的裸 `tcrfc.tw`**——本輪盤點實測發現
 *     這個裸字串在**藍鯨站每一頁**（含完全乾淨的頁面）都會命中一次，根因是
 *     `nuxt.config.ts` 的 `blueWhaleSiteUrl: 'https://bw-stg.tcrfc.tw'` 這個
 *     runtime config 預設值會被 Nuxt 序列化進**每一頁**的 hydration payload
 *     （不論該頁有沒有用到這個值），而這個網址本身是**藍鯨自己的 staging 網域**
 *     （兩站共用上層網域直到藍鯨正式網域到位，見 docs/17-deployment.md），不是
 *     磐石網域外洩——裸 `tcrfc.tw` 對「藍鯨站有沒有誤植磐石網域」這個問題完全沒有
 *     鑑別力（每頁必中，不分乾淨或有問題）。加上 `www.` 前綴後，只有磐石舊站的
 *     完整寫法才會命中，`bw-stg.tcrfc.tw` 沒有 `www.` 前綴不受影響。
 *
 * 沒進詞表（探測過，排除理由）：
 *   - `Rock`——只是 `Taichung Rock FC` 的一部分，比 `Taichung Rock` 更泛用（未來
 *     内容若用「rock」當普通英文字的機率不是零），純冗餘、風險更高，不收。
 *   - `ROCKS`／`Cornerstone`——舊站曾用過的品牌詞（`docs/18` 遺留字串清單），
 *     實測 0 命中，先不放——詞表要對「現在的輸出」負責，不是對「規劃書提過的所有
 *     禁詞」負責，那是 `check-forbidden-terms` 類檢查的工作，兩者職責不同不合併。
 *
 * ## 掃描範圍：整份 HTML（`<head>` ＋ `<body>`），不是只掃 `<body>`
 *
 * 理由同舊版（見 git 歷史）：`<title>`／`og:site_name`／`og:description`／JSON-LD
 * 的 `name`／`description` 全部在 `<head>` 裡，只掃 `<body>` 會讓這支腳本連自己想抓
 * 的那類錯誤都抓不全。
 *
 * ## 為什麼不掛進 `npm run lint`
 *
 * 理由同舊版：這支腳本要先把藍鯨站真的跑起來（含後端 API／資料庫）才能執行，硬掛進
 * `npm run lint` 會讓一支秒級的靜態指令變成依賴外部環境（`docs/18-work-errors.md`
 * `E-34`：長期因環境紅燈的檢查會把「讀錯誤訊息」淘汰掉）。用法見
 * `apps/web/README.md`「藍鯨品牌詞彙殘留檢查」一節。
 *
 * ## 例外清單棘輪：只能往下減、不能往上加
 *
 * `EXEMPT_PAGES` 是**目前已知、有明文理由的例外**——不在其中的頁面一旦命中詞表就是
 * `exit 1`。跟舊版 `PROTECTED_PAGES`（棘輪方向是「只能往上加」）刻意相反：**例外
 * 清單只能隨著頁面陸續修正而變短，不能因為嫌麻煩而變長**。執行時會用
 * `git show HEAD:<this file>` 拿上一次提交的例外清單，斷言「這一版 ⊆ 上一版」——
 * 新增任何一筆不在上一版裡的例外會讓腳本自己先失敗，訊息會指出是哪一筆。找不到
 * 上一版（例如這是本檔案第一次以新格式提交）就略過這項檢查。
 *
 * ⚠️ 誠實的邊界（跟 `E-31` 同一個提醒）：這道棘輪只在「有人執行這支腳本」的那一刻
 * 生效，不是 git hook，擋得住「忘記」，擋不住「繞過」。要擋住「繞過」需要把這支
 * 腳本接進 CI 的必要關卡，那是另一個決定，本次交付沒有做。
 *
 * ## 用法
 *
 *   1. 依 `apps/web/README.md`「本機測試兩個 club」把藍鯨站（`NUXT_PUBLIC_CLUB=bw`
 *      **且必須帶** `NUXT_PUBLIC_SITE_NAME=台中藍鯨`，見 docs/13 §6 紀律 11a）跑起來。
 *   2. `node scripts/check-club-brand-leak.mjs [--base-url=http://127.0.0.1:3012]`
 *      （預設 `http://127.0.0.1:3012`）。
 *
 * 離開碼：任何非例外頁面命中詞表、或例外清單棘輪被違反 → `1`；否則 `0`。
 */

import { execSync } from 'node:child_process'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { collectAllRoutes } from './lib/collect-routes.mjs'

const HERE = dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = resolve(HERE, '../../..')
const THIS_FILE_REL = 'apps/web/scripts/check-club-brand-leak.mjs'

// ---------------------------------------------------------------------------
// 詞表（理由見檔頭）
// ---------------------------------------------------------------------------
const FORBIDDEN_TERMS = ['磐石', 'TCRFC', '學院', 'Taichung Rock', 'www.tcrfc.tw']

// ---------------------------------------------------------------------------
// 例外清單：只能往下減、不能往上加（見檔頭「棘輪」說明）。
//
// 每一筆都必須附規格依據或既有缺口編號——「看起來還好」不是理由。`terms` 只豁免
// 列出的詞，同一頁若命中詞表裡沒列出的其他詞仍然算失敗。
// ---------------------------------------------------------------------------
const EXEMPT_PAGES = [
  {
    route: '/zh/checkout/',
    terms: ['磐石'],
    reason:
      '「發票抬頭為台中磐石足球俱樂部」「收款方為台中磐石足球俱樂部」是規格要求的真實揭露，' +
      '不是品牌外洩——藍鯨規劃書 §1.3「本站不另設 LINE Pay 商店號、不使用獨立發票字軌，' +
      '一律沿用主站的單一金流設定」，且主站規劃書 §1.3「前台必須明示收款方」明文要求結帳頁' +
      '必須說清楚收款方與發票抬頭是台中磐石足球俱樂部，即使在藍鯨站上也一樣（BW-C1 盤點）。',
  },
  {
    route: '/en/checkout/',
    terms: ['磐石'],
    reason: '同上（同一份頁面檔案的 en 版本）。',
  },
  {
    route: '/zh/shop/',
    terms: ['磐石'],
    reason: '「收款方為台中磐石足球俱樂部」購物須知揭露，理由同 /zh/checkout/（藍鯨規劃書 §1.3／主站規劃書 §1.3）。',
  },
  {
    route: '/en/shop/',
    terms: ['磐石'],
    reason: '同上（同一份頁面檔案的 en 版本）。',
  },
  {
    route: '/zh/shop/cushioned-socks/',
    terms: ['磐石'],
    reason: '「收款方：台中磐石足球俱樂部」付款須知揭露，理由同 /zh/checkout/（藍鯨規劃書 §1.3／主站規劃書 §1.3）。',
  },
  {
    route: '/en/shop/cushioned-socks/',
    terms: ['磐石'],
    reason: '同上（同一份頁面檔案的 en 版本）。',
  },
  {
    route: '/zh/club/first-team/player/',
    terms: ['磐石', 'TCRFC'],
    reason:
      '本頁明文自稱「球員詳情頁範本」（頁面本身的 template-banner 區塊），以磐石一線隊' +
      '11 號球員楊朝景的真實名單資料示範正式站球員詳情頁的版型結構，正式站上線後由 CMS' +
      '依球員名單自動產生——這是設計範本，不是待補的藍鯨頁面內容。藍鯨球員名單與肖像同意' +
      '尚未到位（STATUS.md 阻塞清單），沒有可替換的真實藍鯨球員資料，換成假資料會違反' +
      '「不得臆造」紀律，換成另一位真實磐石球員一樣沒有解決「這是磐石球員」的問題。既有' +
      '舊版棘輪清單本來就不含這一頁，是同一個既有缺口的延續，不是本輪新增。',
  },
  {
    route: '/en/club/first-team/player/',
    terms: ['磐石', 'TCRFC'],
    reason: '同上（同一份頁面檔案的 en 版本）。',
  },
]

function findExemption(route, term) {
  return EXEMPT_PAGES.some((e) => e.route === route && e.terms.includes(term))
}

// 路由清單：與 check-club-image-leak.mjs 共用 scripts/lib/collect-routes.mjs
const routes = collectAllRoutes()

// ---------------------------------------------------------------------------
// 例外清單棘輪：這一版的 EXEMPT_PAGES 必須是上一版的子集合（只能減少）。
// 比對維度是 `route|term` 這個組合，不是整筆物件（reason 文字可以改寫得更清楚，
// 不算「新增例外」）。
// ---------------------------------------------------------------------------
function exemptionKeys(pages) {
  const keys = []
  for (const p of pages) {
    for (const t of p.terms) keys.push(`${p.route}|${t}`)
  }
  return keys
}

function checkRatchet() {
  let previousSrc
  try {
    previousSrc = execSync(`git show HEAD:${THIS_FILE_REL}`, {
      cwd: REPO_ROOT,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'ignore'],
    })
  } catch {
    return { ok: true, note: '（找不到上一版，可能是本檔案第一次以新格式提交，略過棘輪檢查）' }
  }

  const m = /EXEMPT_PAGES\s*=\s*\[([\s\S]*?)\n\]/.exec(previousSrc)
  if (!m) {
    return { ok: true, note: '（上一版找不到 EXEMPT_PAGES，可能是本次由舊版 PROTECTED_PAGES 格式改版，略過棘輪檢查）' }
  }

  // 逐筆解析上一版的 { route: '...', terms: [...] } 物件，只取得比對用的 route／term 組合。
  const prevEntries = [...m[1].matchAll(/route:\s*'([^']+)'[\s\S]*?terms:\s*\[([^\]]*)\]/g)].map((mm) => ({
    route: mm[1],
    terms: [...mm[2].matchAll(/'([^']+)'/g)].map((t) => t[1]),
  }))
  const previousKeys = exemptionKeys(prevEntries)
  const currentKeys = exemptionKeys(EXEMPT_PAGES)
  const added = currentKeys.filter((k) => !previousKeys.includes(k))

  if (added.length > 0) {
    return {
      ok: false,
      added,
      note: '例外清單違反棘輪：只能往下減、不能往上加（理由見本檔案檔頭「例外清單棘輪」）。',
    }
  }
  return { ok: true, note: `（棘輪檢查通過，這一版 ${currentKeys.length} 筆例外全部是上一版 ${previousKeys.length} 筆的子集合）` }
}

// ---------------------------------------------------------------------------
// 主流程
// ---------------------------------------------------------------------------
const baseUrlArg = process.argv.find((a) => a.startsWith('--base-url='))
const baseUrl = (baseUrlArg ? baseUrlArg.slice('--base-url='.length) : 'http://127.0.0.1:3012').replace(/\/$/, '')

console.log(`藍鯨品牌詞彙殘留檢查（全站，zh／en）—— 目標：${baseUrl}（共 ${routes.length} 條路由）`)
console.log(`詞表：${FORBIDDEN_TERMS.join('、')}\n`)

const ratchet = checkRatchet()
console.log(`例外清單棘輪：${ratchet.ok ? '✓' : '✗'} ${ratchet.note}`)
if (!ratchet.ok) {
  console.error(`\n  新增的例外：${ratchet.added.join('、')}\n`)
}

const failures = []
const exempted = []
let checkedCount = 0
let fetchFailures = 0

for (const route of routes) {
  const url = `${baseUrl}${route}`
  let res
  try {
    res = await fetch(url, { redirect: 'manual' })
  } catch (err) {
    fetchFailures++
    console.error(`  ⚠️ 無法連線 ${url}：${err.message}（藍鯨站是不是還沒啟動？見 README「怎麼跑」）`)
    continue
  }
  if (res.status === 404) continue // 單元關閉的頁面本來就該 404，見 units.ts
  if (res.status >= 300 && res.status < 400) continue // 轉址頁，沒有自己的內容可掃
  if (!res.ok) {
    console.error(`  ⚠️ ${url} 回應 ${res.status}，不是預期的 200／404／30x，跳過`)
    continue
  }
  checkedCount++
  const html = await res.text()
  const realHits = {}
  const exemptHits = {}
  for (const term of FORBIDDEN_TERMS) {
    const count = html.split(term).length - 1
    if (count === 0) continue
    if (findExemption(route, term)) {
      exemptHits[term] = count
    } else {
      realHits[term] = count
    }
  }
  if (Object.keys(realHits).length > 0) failures.push({ route, hits: realHits })
  if (Object.keys(exemptHits).length > 0) exempted.push({ route, hits: exemptHits })
}

if (fetchFailures === routes.length) {
  console.error(`\n✗ ${fetchFailures} 條路由全部連不上——藍鯨站沒有跑起來，這支腳本無法執行。`)
  console.error(`  參見 apps/web/README.md「本機測試兩個 club」，記得帶 NUXT_PUBLIC_SITE_NAME=台中藍鯨。`)
  process.exit(1)
}

console.log(`\n已檢查 ${checkedCount} 條路由（其餘為 30x／404，略過）。`)

if (exempted.length > 0) {
  console.log(`\n例外清單命中（已知、有明文理由，不影響離開碼）：${exempted.length} 頁`)
  for (const r of exempted) {
    console.log(`  - ${r.route}：${Object.entries(r.hits).map(([t, c]) => `${t}×${c}`).join('、')}`)
  }
}

if (failures.length > 0) {
  console.error(`\n✗ ${failures.length} 頁出現磐石專屬詞彙，且不在例外清單內：\n`)
  for (const r of failures) {
    console.error(`  - ${r.route}：${Object.entries(r.hits).map(([t, c]) => `${t}×${c}`).join('、')}`)
  }
  console.error('')
  process.exit(1)
}

if (!ratchet.ok) process.exit(1)

console.log(`\n✓ 全站 ${checkedCount} 條路由（不含例外清單命中）皆無磐石專屬詞彙殘留，例外清單棘輪未被違反。`)
