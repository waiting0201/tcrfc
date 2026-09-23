#!/usr/bin/env node
/**
 * check-homepage-fidelity.mjs — 把 S0-9k 修回去的那批首頁 mockup 逐字值「釘」在
 * `shared/utils/club-copy.ts` 上，接手 `/zh/` 從 `site/tools/compare-dom.mjs` 退役後
 * 失去的那一小塊自動驗收。
 *
 * ## 為什麼需要這支腳本（S0-9k 退役 `/zh/` 的前提條件）
 *
 * `/zh/` 一度是 compare-dom 的 8 個失敗頁之一，5 筆差異全部是「最新消息」新聞卡片的
 * href（`/zh/news/article/` → `/zh/news/<slug>/`，S0-9f 的正確結果，基準本身過期）。
 * 這個成因跟 `/zh/news/club/` 等 4 頁已退役的理由完全一樣，但使用者裁決「不准沿用那 4
 * 頁的 covered_by 就直接退役」——因為那 4 頁退役時，頁面「唯一的已知差異就是 href」，
 * `link-checker/valid-route` 精準頂住那個差異；`/zh/` 不一樣，它除了新聞卡片 href
 * 之外還有 hero CTA、四大支柱卡片（含圖片 alt／尺寸／錨點 id）——這些內容退役後就
 * 不再跟 `site/dist` 比對，而**這次修的 bug 正好都長在這裡**（4 張圖片 alt 被清空、
 * 尺寸不對、`academy`／`programs`／`womens` 錨點 id 消失、hero 兩個 CTA 連到錯的頁面）。
 * 退役 `/zh/` 之前，必須先有東西接手驗這些值，這正是這支腳本的唯一目的。
 *
 * ## 為什麼來源是 site/src 不是 site/dist
 *
 * `docs/14-invariants.md`「前台改 Nuxt」整節寫明**基準線是 `site/src`（131 檔，納管）
 * ＋ `site/build.mjs`；`site/dist` 只是產物（未納管，`node site/build.mjs` 可重產）**。
 * `site/dist` 有可能是舊的、忘記重新 build 的產物；`site/src/pages/zh/index.html` 才是
 * git 追蹤、真正的唯一來源。本腳本讀 `site/src`，值裡帶 `{{ROOT}}` 這個 build-time token，
 * 解析時原樣剝除（見 `stripRootToken()`），不影響比對。
 *
 * ## 涵蓋範圍：只釘「這次修掉的東西」，不是整頁
 *
 * 涵蓋（對應 S0-9k 交付報告「A 類」清單，逐項可回溯到具體修過的欄位）：
 * - `HOME_PILLARS.tcrfc[i]` 四張卡片各自的 `id`（可能不存在）、`href`、
 *   img 的 `alt`／`width`／`height`。
 * - `HOME_HERO.tcrfc` 的 `ctaPrimaryHref`／`ctaSecondaryHref`／`ctaSecondaryLabelZh`
 *   （hero 「加入球隊」「認識台中磐石」兩個 CTA 的連結目標與第二顆文字）。
 *
 * ⛔ **明確不涵蓋**（老實列出，不要讓這支腳本的存在造成「首頁都驗過了」的錯覺）：
 * - 新聞卡片 href（`/zh/news/<slug>/`）——這正是 `/zh/` 退役的原因本身，`site/src` 的值
 *   （`{{ROOT}}/zh/news/article/`）是已知過期的基準，不能拿來當比對目標，見
 *   `RETIRED_ROUTES` 裡 `/zh/` 那筆的 `why`。
 * - CTA 三卡（10.1／10.2／10.5，`cta-band`）、贊助商牆、官方商店帶、賽事行事曆帶、
 *   一線隊球員橫幅——這些是 API／動態資料驅動或這次沒有改動過的內容，本次沒有查證
 *   mockup 逐字值，硬加只會讓這支腳本對 mockup 的 HTML 結構更脆弱，卻沒有對應到任何
 *   已知修過的 bug。之後如果這些區塊也發生類似的資料層回歸，要另外查證後再擴充，
 *   不要現在就假裝涵蓋。
 * - `pillar-card__en`／`pillar-card__zh`／`pillar-card__link` 文字——這次沒有改過，
 *   本來就是對的，不在「這次修回去的值」範圍內。
 *
 * ## 藍鯨呢？
 *
 * **完全不驗。** `site/src` 只有磐石版的 mockup，藍鯨從來沒有這一頁的基準可以比對
 * （`HOME_PILLARS.bw`／`HOME_HERO.bw` 的欄位值是這次交付時人工決定的合理值，不是
 * 從任何 mockup 逐字搬過來的——href 見 `club-copy.ts` 該欄位的註解）。這支腳本只讀取、
 * 只驗證 `.tcrfc` 那份；藍鯨的部分永遠不會出現在下面的失敗訊息裡，這是刻意的範圍界線，
 * 不是遺漏。
 *
 * ## 這支腳本跟 site/ 綁死，site/ 退場那天它要一起退場
 *
 * 依 `docs/14-invariants.md`「前台改 Nuxt」整節，`site/` 骨架終將被拆除（比對關卡的
 * 使命是「改到跟 mockup 一模一樣為止」，任務完成後 mockup 沒有繼續存在的理由）。
 * 本腳本的整個存在前提是 `site/src/pages/zh/index.html` 讀得到——`site/` 一旦被拆，
 * `MOCKUP_PATH` 讀不到檔案，腳本會直接 **fail-loud**（exit 1，見下方讀檔那段的錯誤訊息），
 * 不會安靜地跳過或誤判成通過。看到這支腳本因為「找不到 site/src」而炸掉，代表的是
 * 「該把這支腳本一起刪掉、從 package.json 的 lint 鏈移除」，不是要去修 site/。
 * 屆時 `HOME_PILLARS.tcrfc`／`HOME_HERO.tcrfc` 的值本身仍然要正確，只是驗證手段要換成
 * 別的（例如視覺回歸測試），不是回頭復原 site/。
 */

import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const HERE = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(HERE, '..') // apps/web
const REPO_ROOT = resolve(ROOT, '..', '..') // 專案根目錄
const MOCKUP_PATH = resolve(REPO_ROOT, 'site/src/pages/zh/index.html')
const MOCKUP_LABEL = 'site/src/pages/zh/index.html'
const CLUB_COPY_PATH = resolve(ROOT, 'shared/utils/club-copy.ts')
const CLUB_COPY_LABEL = 'shared/utils/club-copy.ts'

const failures = []

// ---------------------------------------------------------------------------
// 共用工具
// ---------------------------------------------------------------------------

/** 去掉 JS／TS 的單行／多行註解，保留字串內容原封不動（借用 check-match-status.mjs
 * 已經驗證過的作法：逐字元掃描、遇到引號先跳過整段字串再判斷是不是註解，避免
 * 字串裡的 `//`（例如網址）被天真的行註解剝除誤傷）。 */
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

/** 從 `openIdx`（指向開括號）找配對的結束括號，跳過字串。`open`/`close` 是一對字元，
 * 例如 `{`/`}` 或 `[`/`]`。 */
function findMatchingBracket(text, openIdx, open, close) {
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
    if (c === open) depth++
    else if (c === close) { depth--; if (depth === 0) return i }
  }
  return -1
}

/** 在一段（已知不含巢狀大括號的）物件字面值文字裡，取出 `key: 值` 的值。
 * 支援單引號／雙引號字串（含跳脫字元）與數字，找不到回傳 undefined。 */
function extractField(objText, key) {
  const re = new RegExp(`\\b${key}\\s*:\\s*(?:'((?:[^'\\\\]|\\\\.)*)'|"((?:[^"\\\\]|\\\\.)*)"|(-?\\d+))`)
  const m = re.exec(objText)
  if (!m) return undefined
  if (m[1] !== undefined) return m[1]
  if (m[2] !== undefined) return m[2]
  return Number(m[3])
}

/** 把一段陣列字面值文字（`[ {...}, {...} ]` 的內部）切成逐個頂層物件字面值字串，
 * 跳過字串內容裡的 `{`／`}`。物件本身不能有巢狀大括號（本檔的 PillarCopy／
 * HomeHeroCopy 都是純量欄位，不會巢狀），巢狀的話這裡會切錯，需要另外處理。 */
function extractTopLevelObjects(text) {
  const objs = []
  let depth = 0
  let start = -1
  for (let i = 0; i < text.length; i++) {
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
    if (c === '{') { if (depth === 0) start = i; depth++ }
    else if (c === '}') { depth--; if (depth === 0 && start >= 0) { objs.push(text.slice(start, i + 1)); start = -1 } }
  }
  return objs
}

/** mockup 的 href／src 都帶 `{{ROOT}}` build-time token（見 site/build.mjs），
 * club-copy.ts 存的是 Nuxt 用的絕對路徑，比對前先剝掉這個 token。 */
function stripRootToken(href) {
  return href.replace(/^\{\{ROOT\}\}/, '')
}

// ---------------------------------------------------------------------------
// 讀檔（mockup 讀不到就是「site/ 已經退場」的訊號，見檔頭最後一節，fail-loud 不靜默）
// ---------------------------------------------------------------------------

let mockupSrc
try {
  mockupSrc = readFileSync(MOCKUP_PATH, 'utf8')
} catch {
  console.error(`✗ 找不到 ${MOCKUP_LABEL}。\n\n`
    + `這支腳本的存在前提是 site/ 骨架還在（見檔頭「這支腳本跟 site/ 綁死」一節）。\n`
    + `如果 site/ 已經依 docs/14-invariants.md「前台改 Nuxt」整節的規劃被拆除，\n`
    + `這個失敗是預期中的——代表該把 check-homepage-fidelity.mjs 從 package.json 的\n`
    + `lint 鏈移除、把這個檔案一起刪掉，並確認有別的機制（例如視覺回歸測試）接手\n`
    + `驗證首頁四大支柱與 hero CTA 的正確性，不是回頭把 site/ 修好。\n`)
  process.exit(1)
}

let clubCopySrc
try {
  clubCopySrc = stripJsComments(readFileSync(CLUB_COPY_PATH, 'utf8'))
} catch {
  console.error(`✗ 找不到 ${CLUB_COPY_LABEL}——單一真實來源不見了，或路徑被搬走了。`)
  process.exit(1)
}

// ---------------------------------------------------------------------------
// 從 mockup 解析出期望值
// ---------------------------------------------------------------------------

const heroCtasMatch = /<div class="hero__ctas">([\s\S]*?)<\/div>/.exec(mockupSrc)
if (!heroCtasMatch) {
  failures.push(`${MOCKUP_LABEL} 找不到 \`<div class="hero__ctas">\`——mockup 的 hero CTA 區塊結構被改掉了，腳本無法解析期望值。`)
}
let expectedCtaPrimaryHref, expectedCtaSecondaryHref, expectedCtaSecondaryLabelZh
if (heroCtasMatch) {
  const inner = heroCtasMatch[1]
  const anchorRe = /<a class="([^"]+)" href="([^"]+)">([^<]*)<\/a>/g
  const anchors = [...inner.matchAll(anchorRe)]
  const primary = anchors.find((a) => a[1].includes('btn--primary'))
  const secondary = anchors.find((a) => a[1].includes('btn--light'))
  if (!primary) failures.push(`${MOCKUP_LABEL} 的 hero__ctas 裡找不到 class 含 "btn--primary" 的 <a>。`)
  if (!secondary) failures.push(`${MOCKUP_LABEL} 的 hero__ctas 裡找不到 class 含 "btn--light" 的 <a>。`)
  if (primary) expectedCtaPrimaryHref = stripRootToken(primary[2])
  if (secondary) {
    expectedCtaSecondaryHref = stripRootToken(secondary[2])
    expectedCtaSecondaryLabelZh = secondary[3]
  }
}

const pillarRe = /<a class="pillar-card clip-card clip-card--on-dark"(?:\s+id="([^"]+)")?\s+href="([^"]+)">([\s\S]*?)<\/a>/g
const pillarMatches = [...mockupSrc.matchAll(pillarRe)]
if (pillarMatches.length !== 4) {
  failures.push(`${MOCKUP_LABEL} 找到 ${pillarMatches.length} 張 pillar-card，預期 4 張——`
    + `mockup 的「四大支柱」區塊結構被改掉了，腳本無法逐張核對。`)
}

const expectedPillars = pillarMatches.map((m, i) => {
  const [, id, hrefRaw, inner] = m
  const imgTagMatch = /<img\b[^>]*>/.exec(inner)
  if (!imgTagMatch) {
    failures.push(`${MOCKUP_LABEL} 第 ${i + 1} 張 pillar-card 裡找不到 <img> 標籤。`)
    return null
  }
  const imgTag = imgTagMatch[0]
  const alt = /\balt="([^"]*)"/.exec(imgTag)?.[1]
  const width = /\bwidth="(\d+)"/.exec(imgTag)?.[1]
  const height = /\bheight="(\d+)"/.exec(imgTag)?.[1]
  if (alt === undefined || width === undefined || height === undefined) {
    failures.push(`${MOCKUP_LABEL} 第 ${i + 1} 張 pillar-card 的 <img> 缺 alt／width／height 其中之一，腳本解析不出完整期望值。`)
    return null
  }
  return { index: i, id, href: stripRootToken(hrefRaw), imgAlt: alt, imgWidth: Number(width), imgHeight: Number(height) }
})

// ---------------------------------------------------------------------------
// 從 club-copy.ts 解析出實際值
// ---------------------------------------------------------------------------

function extractTcrfcBlock(constName) {
  const declIdx = clubCopySrc.indexOf(`export const ${constName}`)
  if (declIdx < 0) {
    failures.push(`${CLUB_COPY_LABEL} 找不到 \`export const ${constName}\`。`)
    return null
  }
  const eqIdx = clubCopySrc.indexOf('=', declIdx)
  const outerOpen = clubCopySrc.indexOf('{', eqIdx)
  const outerClose = outerOpen >= 0 ? findMatchingBracket(clubCopySrc, outerOpen, '{', '}') : -1
  if (outerOpen < 0 || outerClose < 0) {
    failures.push(`${CLUB_COPY_LABEL} 的 \`${constName}\` 外層大括號配對不起來。`)
    return null
  }
  const body = clubCopySrc.slice(outerOpen, outerClose + 1)
  const tcrfcKeyIdx = body.search(/\btcrfc\s*:/)
  if (tcrfcKeyIdx < 0) {
    failures.push(`${CLUB_COPY_LABEL} 的 \`${constName}\` 找不到 \`tcrfc:\` 這個鍵。`)
    return null
  }
  return { body, tcrfcKeyIdx }
}

// HOME_HERO.tcrfc
let actualCta = {}
const homeHero = extractTcrfcBlock('HOME_HERO')
if (homeHero) {
  const { body, tcrfcKeyIdx } = homeHero
  const colonIdx = body.indexOf(':', tcrfcKeyIdx)
  const objOpen = body.indexOf('{', colonIdx)
  const objClose = objOpen >= 0 ? findMatchingBracket(body, objOpen, '{', '}') : -1
  if (objOpen < 0 || objClose < 0) {
    failures.push(`${CLUB_COPY_LABEL} 的 \`HOME_HERO.tcrfc\` 物件大括號配對不起來。`)
  } else {
    const objText = body.slice(objOpen, objClose + 1)
    actualCta = {
      ctaPrimaryHref: extractField(objText, 'ctaPrimaryHref'),
      ctaSecondaryHref: extractField(objText, 'ctaSecondaryHref'),
      ctaSecondaryLabelZh: extractField(objText, 'ctaSecondaryLabelZh'),
    }
    for (const key of ['ctaPrimaryHref', 'ctaSecondaryHref', 'ctaSecondaryLabelZh']) {
      if (actualCta[key] === undefined) {
        failures.push(`${CLUB_COPY_LABEL} 的 \`HOME_HERO.tcrfc\` 解析不出 \`${key}\` 欄位——欄位可能被改名或格式不再是簡單字面值。`)
      }
    }
  }
}

// HOME_PILLARS.tcrfc
let actualPillars = []
const homePillars = extractTcrfcBlock('HOME_PILLARS')
if (homePillars) {
  const { body, tcrfcKeyIdx } = homePillars
  const colonIdx = body.indexOf(':', tcrfcKeyIdx)
  const arrOpen = body.indexOf('[', colonIdx)
  const arrClose = arrOpen >= 0 ? findMatchingBracket(body, arrOpen, '[', ']') : -1
  if (arrOpen < 0 || arrClose < 0) {
    failures.push(`${CLUB_COPY_LABEL} 的 \`HOME_PILLARS.tcrfc\` 陣列中括號配對不起來。`)
  } else {
    const arrText = body.slice(arrOpen + 1, arrClose)
    const objTexts = extractTopLevelObjects(arrText)
    if (objTexts.length !== 4) {
      failures.push(`${CLUB_COPY_LABEL} 的 \`HOME_PILLARS.tcrfc\` 有 ${objTexts.length} 個物件，預期 4 個——`
        + `跟 mockup 的四張 pillar-card 數量對不上，陣列筆數本身就已經跟 mockup 不一致。`)
    }
    actualPillars = objTexts.map((objText, i) => ({
      index: i,
      id: extractField(objText, 'id'),
      href: extractField(objText, 'href'),
      imgAlt: extractField(objText, 'imgAlt'),
      imgWidth: extractField(objText, 'imgWidth'),
      imgHeight: extractField(objText, 'imgHeight'),
    }))
  }
}

// ---------------------------------------------------------------------------
// 逐項比對
// ---------------------------------------------------------------------------

if (failures.length === 0) {
  // hero CTA 三個欄位
  const ctaChecks = [
    ['ctaPrimaryHref', expectedCtaPrimaryHref],
    ['ctaSecondaryHref', expectedCtaSecondaryHref],
    ['ctaSecondaryLabelZh', expectedCtaSecondaryLabelZh],
  ]
  for (const [key, expected] of ctaChecks) {
    const actual = actualCta[key]
    if (actual !== expected) {
      failures.push(`HOME_HERO.tcrfc.${key} 跟 mockup 對不上——`
        + `mockup（${MOCKUP_LABEL}）是 ${JSON.stringify(expected)}，`
        + `${CLUB_COPY_LABEL} 目前是 ${JSON.stringify(actual)}。`)
    }
  }

  // 四張 pillar-card 逐張比對
  const PILLAR_LABELS = ['第 1 張（一線隊，無 id）', '第 2 張（academy）', '第 3 張（programs）', '第 4 張（womens）']
  for (let i = 0; i < 4; i++) {
    const expected = expectedPillars[i]
    const actual = actualPillars[i]
    const label = PILLAR_LABELS[i] ?? `第 ${i + 1} 張`
    if (!expected || !actual) continue // 上面數量檢查已經報過錯，這裡不用重複噴
    for (const field of ['id', 'href', 'imgAlt', 'imgWidth', 'imgHeight']) {
      const expVal = expected[field]
      const actVal = actual[field]
      // id 允許 undefined（mockup 有 3/4 張有 id，第 1 張沒有），undefined === undefined 才算一致
      if (expVal !== actVal) {
        failures.push(`HOME_PILLARS.tcrfc[${i}]（${label}）的 \`${field}\` 跟 mockup 對不上——`
          + `mockup（${MOCKUP_LABEL}）是 ${JSON.stringify(expVal)}，`
          + `${CLUB_COPY_LABEL} 目前是 ${JSON.stringify(actVal)}。`)
      }
    }
  }
}

// ---------------------------------------------------------------------------
// 輸出
// ---------------------------------------------------------------------------

if (failures.length > 0) {
  console.error(`\n✗ 首頁 mockup 逐字值核對未通過（${failures.length} 項，S0-9k 曾經修過同一批值回歸過一次）：\n`)
  for (const f of failures) console.error(`  - ${f}`)
  console.error(`\n真實來源是 ${MOCKUP_LABEL}；本檢查只涵蓋 hero 兩個 CTA 與四大支柱卡片的\n`
    + `id／href／img alt／width／height，不涵蓋整頁——見 check-homepage-fidelity.mjs 檔頭\n`
    + `「涵蓋範圍」一節，以及 site/tools/compare-dom.mjs 裡 /zh/ 那筆 RETIRED_ROUTES 的 covered_by 說明。\n`)
  process.exit(1)
}

console.log(`✓ 首頁 mockup 逐字值核對通過（hero 2 個 CTA ＋ 4 張支柱卡片的 id／href／alt／width／height，`
  + `共 ${3 + 4 * 5} 個欄位，只驗 tcrfc，藍鯨無 mockup 基準不在此檢查範圍內）`)
