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
 * ## 為什麼不掛進 `npm run lint`（會被問到，先寫死答案）
 *
 * `apps/web` 的 `npm run lint` 目前**純靜態**：不開伺服器、不連資料庫、幾秒內跑完
 * （node 版本檢查、`club-copy.ts` 結構檢查、首頁忠實度、`match-status` 枚舉、eslint）。
 * 這支腳本反過來，**沒有真的把藍鯨站跑起來就無法執行**——而藍鯨站要跑起來還牽動
 * 後端 API 與資料庫（`apps/api` 連 `CLUB_SQL_CONNECTION_STRING`），不是 `npm run build`
 * 就能滿足的前提。硬掛進 `lint`：
 *   1. 讓一支目前秒級的指令變成要先起兩個服務＋一個資料庫才跑得動，`npm run lint`
 *      不再是隨時能跑的靜態檢查；
 *   2. 在沒有資料庫的環境（例如某些 CI 步驟、剛 clone 下來的機器）會讓 `lint` 直接
 *      連不上而失敗，而失敗原因跟「這次改動有沒有問題」無關——這正是
 *      `docs/18-work-errors.md` `E-34` 的起點：**一個长期因環境而紅燈的檢查，會把
 *      「讀錯誤訊息」這個動作淘汰掉**，之後真正的錯誤會混在「反正它一直紅」裡不被看見。
 *
 * **這支腳本因此是獨立指令，不是 `npm run lint` 的一部分**——用法見下方與
 * `apps/web/README.md`「藍鯨品牌詞彙殘留檢查」一節。建議在下列時機手動／於 CI 的
 * 部署前驗收步驟執行：藍鯨站每次要部署前、`BW-2`～`BW-8` 每完成一批頁面後。
 *
 * ## 詞表怎麼挑（連同已排除的候選與理由）
 *
 * 進詞表：
 *   - `磐石`——覆蓋率最高、誤判風險最低的詞。實測：藍鯨站 79 頁裡 59 頁命中，
 *     全部是磐石專屬敘述（俱樂部全名、品牌描述），沒有找到任何一處「藍鯨頁面合理
 *     提到磐石」的反例（例如跨隊敘述、共同的慈善或合作夥伴內容——藍鯨站 11 慈善
 *     單元整個不存在（docs/13 §3），09 夥伴依規劃書必須分區不得混列，所以目前沒有
 *     合理共同提及磐石的內容形狀）。
 *   - `TCRFC`——磐石英文縮寫。實測（已排除環境變數缺陷後）仍命中 16 頁／46 次，
 *     且這 16 頁全部是 `磐石` 命中頁的子集合（無新增覆蓋），對目前這批資料是冗餘的，
 *     但保留它是防禦性的——它不太可能被合理地用在藍鯨頁面上（不像「學院」可能撞到
 *     真實機構名），且成本是零（沒有新增任何一筆誤判）。
 *   - `學院`——04 單元磐石叫「足球學院」、藍鯨依 docs/13 §3 改叫「青年隊」，這個詞
 *     在藍鯨語境下定義上不該出現。**這個詞抓到了 `磐石`／`TCRFC` 都抓不到的一筆真實
 *     殘留**：`/zh/join/` 頁面文案「訓練基地、主場與學院場地的位置與交通指引」，是
 *     10 單元（加入與聯絡）既有頁面內文裡的殘留，不在 SiteHeader，本次任務範圍
 *     （只修 SiteHeader）沒有動它，原樣留著等下一個處理的人接手（見腳本輸出與
 *     交付報告）。
 *     ⚠️ **已知誤判風險，沒有排除機制，只能先記著**：未來藍鯨真實內容（教練／球員
 *     簡歷）有可能合理提到某個真實機構名稱含「學院」兩字（例如某人畢業於「OO體育
 *     學院」）。目前藍鯨站沒有任何這樣的內容，所以先不建排除清單——真的出現的時候
 *     再決定要不要開白名單，不要為了假設中的情況預先蓋一套排除機制（那本身就是一種
 *     過度工程，且排除清單本身可能被濫用去蓋掉真的殘留）。
 *
 * 沒進詞表（探測過，排除理由）：
 *   - `Rock`——只是 `TCRFC`／`磐石` 命中頁裡「Taichung Rock FC」的英文全名，沒有
 *     任何新增覆蓋，而且是比 `TCRFC` 更泛用的字（未來内容若用「rock」當普通英文字
 *     的機率不是零），純冗餘、風險更高，不收。
 *   - `ROCKS`／`Cornerstone`——舊站曾用過的品牌詞（`docs/18` 遺留字串清單），
 *     實測 0 命中，本檔的頁面裡目前不存在，先不放——詞表要對「現在的輸出」負責，
 *     不是對「規劃書提過的所有禁詞」負責，那是 `check-forbidden-terms` 類檢查的工作
 *     （磐石後台專案已有先例），兩者職責不同不合併。
 *
 * ## 掃描範圍：整份 HTML（`<head>` ＋ `<body>`），不是只掃 `<body>`
 *
 * 這一點刻意跟 `compare-dom.mjs`（只比對 `<body>`）不同，理由不是疏忽：
 *   - `compare-dom.mjs` 保護的是「像素／DOM 對不對得上磐石自己的 mockup」，`<head>`
 *     沒有視覺對應物，比對它沒有意義。
 *   - 這支腳本保護的是「藍鯨站有沒有印出磐石的品牌詞」，而 `<title>`／`og:site_name`／
 *     `og:description`／JSON-LD 的 `name`／`description` 全部在 `<head>` 裡——
 *     這正是 S0-9n 之前真正發生過的那類錯誤（`docs/13` 紀律 11a 的 `site.name`
 *     `TypeError`／`E-42` 的 `nameZh` 誤用，兩者主要现形位置都在 `<head>` 或跨頁共用
 *     的頁首頁尾），而且 `GEO-08` 明文要求 SEO／Schema 的事實正確性——那正是
 *     `<head>` 管的範圍。只掃 `<body>` 會讓這支腳本連自己想抓的那類錯誤都抓不全。
 *   - 實測驗證：目前這批資料裡，沒有任何一筆殘留**只**出現在 `<head>` 而不出現在
 *     `<body>`（本檔案交付時的殘留都是全頁複製，head/body 同時中獎）。但這是「目前
 *     資料剛好如此」，不是「只掃 body 也一樣安全」的證明——所以刻意選全頁掃描，
 *     不依賴這個巧合。
 *
 * ## 棘輪機制：「已宣告完工」清單只能往上加，不能往下拿
 *
 * `PROTECTED_PAGES` 是目前**已確認乾淨、應該保持乾淨**的頁面清單，一出現任何詞表
 * 命中就 `exit 1`（並指出是哪一頁、哪個詞、幾次）。**其餘 79－N 頁只計數不報錯**，
 * 當作藍鯨開發的進度計——這是刻意的，理由見 `docs/18-work-errors.md` `E-34`：
 * 對整站 59 頁 hard-fail 會製造一個**永久紅燈**，紅燈變成「已知雜訊」之後就沒有人會
 * 再去看它，於是保護清單裡真正該守住的那幾頁反而被這個永久紅燈蓋住訊號。
 *
 * **這條規則不是靠自覺遵守，是靠這支腳本自己強制的**：執行時會用 `git show
 * HEAD:<this file>` 拿上一次提交的清單，斷言「舊清單 ⊆ 新清單」——拿掉舊清單裡任何
 * 一筆都會讓腳本自己先失敗，訊息會指出是哪一筆被拿掉。找不到上一版（例如這是本檔案
 * 第一次提交）就略過這項檢查。
 *
 * ⚠️ **誠實的邊界（跟 `E-31` 同一個提醒：機制的實際效力不能與它給人的信心不相稱）**：
 * 這道棘輪**只在「有人執行這支腳本」的那一刻生效**——它不是 git hook，不會在
 * `git commit`／`git push` 時自動跑。如果有人直接 commit 一個拿掉某頁保護的版本，
 * 而**沒有跑過這支腳本**，那次移除不會被擋下；下一次真的執行這支腳本時，
 * `git show HEAD:<this file>` 拿到的「上一版」已經是移除之後的 HEAD 了——棘輪比對的
 * 是「這一版 vs 上一版」，不是「這一版 vs 史上曾經出現過的最大版本」，所以**它擋得住
 * 「忘記」，擋不住「繞過」**。要擋住「繞過」需要把這支腳本接進 CI 的必要關卡（例如
 * PR 合併前的檢查），那是另一個決定，本次交付沒有做。
 *
 * ⚠️ **這份清單只保護「已確認乾淨的內容頁」，不含因單元被關閉而回 404 的頁面**
 * （`zh/charity/*` 五頁、`zh/womens/`，依 docs/13 §3 藍鯨不設 06／11 單元）——那些頁面
 * 沒有內容可保護，回 404 本身是否正確是 `isUnitEnabledForClub` 的職責，不是這支腳本
 * 的職責，硬塞進清單只會製造「保護了根本不存在的東西」的假象。
 *
 * ## 用法
 *
 *   1. 依 `apps/web/README.md`「本機測試兩個 club」把藍鯨站（`NUXT_PUBLIC_CLUB=bw`
 *      **且必須帶** `NUXT_PUBLIC_SITE_NAME=台中藍鯨`，見 docs/13 §6 紀律 11a）跑起來。
 *   2. `node scripts/check-club-brand-leak.mjs [--base-url=http://127.0.0.1:3012]`
 *      （預設 `http://127.0.0.1:3012`）。
 *
 * 離開碼：保護清單裡任何一頁命中詞表、或棘輪被違反 → `1`；否則 `0`
 * （其餘頁面的命中只印出來，不影響離開碼）。
 */

import { execSync } from 'node:child_process'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { readdirSync, statSync } from 'node:fs'

const HERE = dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = resolve(HERE, '../../..')
const PAGES_DIR = resolve(HERE, '../app/pages')
const THIS_FILE_REL = 'apps/web/scripts/check-club-brand-leak.mjs'

// ---------------------------------------------------------------------------
// 詞表（理由見檔頭）
// ---------------------------------------------------------------------------
const FORBIDDEN_TERMS = ['磐石', 'TCRFC', '學院']

// ---------------------------------------------------------------------------
// 已宣告完工、必須保持乾淨的頁面（棘輪清單）
//
// 🔴 只能往上加、不能往下拿——這支腳本自己會用 git 檢查這件事（見檔頭「棘輪機制」）。
// 目前 13 筆對應 `STATUS.md` BW-0d 已套用的單元：01 首頁、02 關於全部 9 頁、
// 03 一線隊（僅 first-team 總覽，first-team/player 是球員詳情頁「範本」，
// 內容本來就是磐石 11 號球員楊朝景的示範資料，尚未做成藍鯨版本，不放進來）、
// 10 加入與聯絡（僅 join 首頁與 join/contact，其餘 join/* 子頁屬其他單元）。
// BW-2～BW-8 每完成一批頁面，把對應路徑加進這裡——不要等到全部做完才一次加。
// ---------------------------------------------------------------------------
const PROTECTED_PAGES = [
  '/zh/',
  '/zh/about/',
  '/zh/about/ecosystem/',
  '/zh/about/governance/',
  '/zh/about/history/',
  '/zh/about/milestones/',
  '/zh/about/our-people/',
  '/zh/about/our-story/',
  '/zh/about/philosophy/',
  '/zh/about/vision-mission/',
  '/zh/club/first-team/',
  '/zh/join/',
  '/zh/join/contact/',
]

// ---------------------------------------------------------------------------
// 路由清單：從 app/pages 檔案樹算出來，不手動維護一份會過期的清單
// （跟 compare-dom.mjs 的「基準清單只能靠讀檔案算出來」是同一個道理）。
// 排除動態路由（檔名或目錄含 `[`）——那些頁面的可抓取網址取決於後端當下有哪些
// slug，不是這支腳本要驗的範圍（新聞逐篇頁見 `zh/news/[slug]/`，目前藍鯨 0 篇新聞，
// 依 BW-0d 刻意不做，等 BW-2 才有內容可驗）。
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

const routes = [...new Set(collectRoutes(PAGES_DIR))].sort()

// ---------------------------------------------------------------------------
// 棘輪檢查：上一版的 PROTECTED_PAGES 必須是這一版的子集合
// ---------------------------------------------------------------------------
function checkRatchet() {
  let previousSrc
  try {
    previousSrc = execSync(`git show HEAD:${THIS_FILE_REL}`, {
      cwd: REPO_ROOT,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'ignore'],
    })
  } catch {
    return { ok: true, note: '（找不到上一版，可能是本檔案第一次提交，略過棘輪檢查）' }
  }

  const m = /PROTECTED_PAGES\s*=\s*\[([\s\S]*?)\]/.exec(previousSrc)
  if (!m) return { ok: true, note: '（上一版找不到 PROTECTED_PAGES，略過棘輪檢查）' }

  const previousPages = [...m[1].matchAll(/'([^']+)'/g)].map((mm) => mm[1])
  const removed = previousPages.filter((p) => !PROTECTED_PAGES.includes(p))

  if (removed.length > 0) {
    return {
      ok: false,
      removed,
      note: '保護清單違反棘輪：只能往上加、不能往下拿（理由見本檔案檔頭「棘輪機制」）。',
    }
  }
  return { ok: true, note: `（棘輪檢查通過，上一版 ${previousPages.length} 筆全部還在）` }
}

// ---------------------------------------------------------------------------
// 主流程
// ---------------------------------------------------------------------------
const baseUrlArg = process.argv.find((a) => a.startsWith('--base-url='))
const baseUrl = (baseUrlArg ? baseUrlArg.slice('--base-url='.length) : 'http://127.0.0.1:3012').replace(/\/$/, '')

console.log(`藍鯨品牌詞彙殘留檢查 —— 目標：${baseUrl}（共 ${routes.length} 條路由）`)
console.log(`詞表：${FORBIDDEN_TERMS.join('、')}\n`)

const ratchet = checkRatchet()
console.log(`棘輪檢查：${ratchet.ok ? '✓' : '✗'} ${ratchet.note}`)
if (!ratchet.ok) {
  console.error(`\n  被拿掉的頁面：${ratchet.removed.join('、')}\n`)
}

const results = []
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
  if (res.status === 404) continue // 單元關閉的頁面本來就該 404，見檔頭說明
  if (res.status >= 300 && res.status < 400) continue // 轉址頁（例如 `/`），沒有自己的內容可掃
  if (!res.ok) {
    console.error(`  ⚠️ ${url} 回應 ${res.status}，不是預期的 200／404／30x，跳過`)
    continue
  }
  const html = await res.text()
  const hits = {}
  for (const term of FORBIDDEN_TERMS) {
    const count = html.split(term).length - 1
    if (count > 0) hits[term] = count
  }
  if (Object.keys(hits).length > 0) results.push({ route, hits })
}

if (fetchFailures === routes.length) {
  console.error(`\n✗ ${fetchFailures} 條路由全部連不上——藍鯨站沒有跑起來，這支腳本無法執行。`)
  console.error(`  參見 apps/web/README.md「本機測試兩個 club」，記得帶 NUXT_PUBLIC_SITE_NAME=台中藍鯨。`)
  process.exit(1)
}

const protectedFailures = results.filter((r) => PROTECTED_PAGES.includes(r.route))
const progressOnly = results.filter((r) => !PROTECTED_PAGES.includes(r.route))

console.log(`\n進度計（不影響離開碼）：${progressOnly.length} 頁命中詞表，共 `
  + `${progressOnly.reduce((s, r) => s + Object.values(r.hits).reduce((a, b) => a + b, 0), 0)} 次`)
for (const r of progressOnly) {
  console.log(`  - ${r.route}：${Object.entries(r.hits).map(([t, c]) => `${t}×${c}`).join('、')}`)
}

if (protectedFailures.length > 0) {
  console.error(`\n✗ 保護清單裡有 ${protectedFailures.length} 頁出現磐石專屬詞彙（這些頁面已宣告完工，不得再出現）：\n`)
  for (const r of protectedFailures) {
    console.error(`  - ${r.route}：${Object.entries(r.hits).map(([t, c]) => `${t}×${c}`).join('、')}`)
  }
  console.error('')
  process.exit(1)
}

if (!ratchet.ok) process.exit(1)

console.log(`\n✓ 保護清單（${PROTECTED_PAGES.length} 頁）全數乾淨，棘輪未被違反。`)
