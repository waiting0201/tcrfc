#!/usr/bin/env node
/**
 * check-site-units-coverage.mjs — `SITE_UNITS` 涵蓋度防呆（S1-18，`docs/18-work-errors.md` `E-72`）。
 *
 * ## 為什麼需要這支腳本
 *
 * `shared/utils/site-units.ts` 的 `SITE_UNITS` 是 `sitemap.xml`／`llms.txt`（GEO-01）
 * 兩處 SEO／GEO 曝光清單共用的單一真實來源；`app/middleware/unit-gate.global.ts`
 * 則是另一條完全獨立的呼叫鏈，只管「這個單元此俱樂部准不准訪問」。E-72 就是這兩條
 * 呼叫鏈被誤以為是同一件事：13（賽事行事曆，S1-15）與 10（加入與聯絡，S1-17）
 * 建置完成、`unit-gate` 通過、`curl` 回 200，卻從未補進 `SITE_UNITS`，對搜尋引擎與
 * AI 爬蟲來說形同不存在，而且**沒有任何自動化會在建置或 `npm run lint` 時發現**。
 *
 * 這支腳本把「新增一個頂層單元頁面時，順手檢查 `SITE_UNITS` 有沒有收錄」這件事從
 * 「記得做」改成「lint 幫你記得」：掃描 `app/pages/zh/` 底下所有
 * `definePageMeta({ unit: 'XX' })`，取每個單元代號的**頂層代碼**（見下方定義），
 * 確認頂層代碼要嘛出現在 `SITE_UNITS`，要嘛列在本檔的 `EXCLUDED_TOP_LEVEL_UNITS`
 * 並附理由——兩者都沒有就是新的 E-72，直接讓 `npm run lint` 失敗。
 *
 * ## 什麼是「頂層代碼」
 *
 * `SITE_UNITS` 只到「單元」層級（見該檔檔頭），不含子頁——`'3.1'`（一線隊）、
 * `'4.7'`（加入學院）、`'10-contact'`（聯絡表單）這些細粒度代號都只是**某個頂層
 * 單元底下的子頁**，本身不需要、也不應該各自出現在 `SITE_UNITS` 裡。這支腳本因此
 * 只取每個單元代號**開頭的連續數字**當作頂層代碼（`'3.1'` → `'3'` → 補零成 `'03'`、
 * `'10-contact'` → `'10'`、`'12.2'` → `'12'`），跟 `SITE_UNITS[].code` 的兩位數字
 * 格式對齊後比對——不要求子頁代號逐一收錄，只要求「這個子頁所屬的頂層單元」有
 * 被收錄或有明文排除理由。
 *
 * ## 刻意排除的頂層代碼（不是「查得不夠仔細」，是這些頁面本來就不該進曝光清單）
 *
 * 見下方 `EXCLUDED_TOP_LEVEL_UNITS`——目前兩筆：`14`（會員中心，GEO-02 明文排除
 * 於 AI 爬蟲與代表頁清單之外，且是登入後內容）、`G-07`（隱私權政策／Cookie
 * 政策等站務法遵頁面，不屬於主站規劃書 13 個單元架構，非數字開頭代號原樣比對）。
 *
 * ## 用法
 *
 *   node scripts/check-site-units-coverage.mjs
 *
 * 離開碼：有頂層代碼既不在 `SITE_UNITS` 也不在排除清單 → `1`；否則 `0`。掛在
 * `npm run lint`（見 package.json `lint:site-units-coverage`）。
 */

import { readFileSync, readdirSync, statSync } from 'node:fs'
import { dirname, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const HERE = dirname(fileURLToPath(import.meta.url))
const PAGES_DIR = resolve(HERE, '../app/pages/zh')
const SITE_UNITS_FILE = resolve(HERE, '../shared/utils/site-units.ts')

// ---------------------------------------------------------------------------
// 明文排除的頂層代碼：只放「查過規格、確定不該進 SITE_UNITS」的代碼，
// 每一筆都要附理由；不是「還沒查」的暫存區。
// ---------------------------------------------------------------------------
const EXCLUDED_TOP_LEVEL_UNITS = new Map([
  ['14', '會員中心——docs/14-invariants.md GEO-02：AI 爬蟲與代表頁清單明文排除會員中心，且為登入後內容，不適用未登入可讀的 sitemap／llms 前提。'],
  ['G-07', '站務法遵頁面（隱私權政策／Cookie 政策）——不屬於主站規劃書 13 個單元架構，是每站必備的通用頁面，非「單元」概念涵蓋範圍。'],
])

function collectVueFiles(dir) {
  const out = []
  for (const entry of readdirSync(dir)) {
    if (entry === 'node_modules') continue
    const full = resolve(dir, entry)
    const stat = statSync(full)
    if (stat.isDirectory()) {
      out.push(...collectVueFiles(full))
    } else if (entry.endsWith('.vue')) {
      out.push(full)
    }
  }
  return out
}

function topLevelCodeOf(unitCode) {
  const digitMatch = /^(\d+)/.exec(unitCode)
  if (!digitMatch) return unitCode // 非數字開頭（例如 'G-07'）：原樣比對排除清單。
  return digitMatch[1].padStart(2, '0')
}

const siteUnitsSrc = readFileSync(SITE_UNITS_FILE, 'utf8')
const siteUnitCodes = new Set([...siteUnitsSrc.matchAll(/code:\s*'([^']+)'/g)].map((m) => m[1]))

const files = collectVueFiles(PAGES_DIR)

/** @type {Map<string, Set<string>>} 頂層代碼 → 命中它的原始 unit 代號集合（供錯誤訊息用） */
const seenTopLevelCodes = new Map()
const missing = []

for (const file of files) {
  const relPath = relative(resolve(HERE, '..'), file).replaceAll('\\', '/')
  const src = readFileSync(file, 'utf8')
  for (const match of src.matchAll(/unit:\s*['"]([^'"]+)['"]/g)) {
    const unitCode = match[1]
    const topLevel = topLevelCodeOf(unitCode)
    if (!seenTopLevelCodes.has(topLevel)) seenTopLevelCodes.set(topLevel, new Set())
    seenTopLevelCodes.get(topLevel).add(`${relPath}（unit: '${unitCode}'）`)

    const covered = siteUnitCodes.has(topLevel) || EXCLUDED_TOP_LEVEL_UNITS.has(topLevel)
    if (!covered) {
      missing.push({ topLevel, unitCode, file: relPath })
    }
  }
}

if (missing.length > 0) {
  console.error('✗ SITE_UNITS 涵蓋度檢查失敗：以下頂層單元代碼既不在 shared/utils/site-units.ts')
  console.error('  的 SITE_UNITS，也不在本檔 EXCLUDED_TOP_LEVEL_UNITS 排除清單（docs/18-work-errors.md')
  console.error('  E-72 就是這樣漏收的，sitemap.xml／llms.txt 會悄悄漏掉這個單元）：\n')
  for (const m of missing) {
    console.error(`  - 頂層代碼 '${m.topLevel}'（來自 ${m.file} 的 unit: '${m.unitCode}'）`)
  }
  console.error('\n  修法二選一：① 把頂層代碼補進 SITE_UNITS（若這個單元該有代表頁曝光）；')
  console.error('  ② 在本檔 EXCLUDED_TOP_LEVEL_UNITS 加一筆並寫明排除理由（若這個單元刻意不曝光）。')
  process.exit(1)
}

console.log(
  `✓ SITE_UNITS 涵蓋度檢查通過（掃描 ${files.length} 個頁面檔案、`
  + `${seenTopLevelCodes.size} 個頂層代碼，全部收錄於 SITE_UNITS 或明文排除）。`,
)
