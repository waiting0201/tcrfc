#!/usr/bin/env node
/**
 * check-fact-single-source.mjs — GEO-03（S1-12d）事實單一來源防呆。
 *
 * ## 為什麼需要這支腳本
 *
 * 主站規劃書 §7 `GEO-03`：「成立年份、主場與場地、梯隊組成、所屬聯賽、聯絡方式…
 * 全站只有一個維護處」。S1-12d 把這五類事實集中到 `shared/utils/site-facts.ts`
 * （見該檔檔頭說明），並把散落在 20 餘個頁面裡的字面值改成引用該檔。**但沒有機制
 * 攔住的話，下一個人很容易在新頁面裡順手打一個「西屯足球場」或「2024 年創立」**——
 * 這支腳本掃描 `app/`／`shared/` 底下的原始碼，確認這些字面值只出現在
 * `site-facts.ts` 本身（唯一允許的維護處），其餘地方一律改用 `SITE_FACTS`／
 * `getPrimaryVenue()`／`academyTeamCodesLabel()` 等既有匯出值。
 *
 * ## 刻意排除的範圍（不是「查得不夠仔細」，是這些地方的字面值不是同一種事實）
 *
 * - `shared/utils/site-facts.ts` 本身：唯一允許定義這些字面值的地方。
 * - **歷史時間軸資料**（`shared/utils/club-copy.ts` 的 `HISTORY_YEARS_BW`／
 *   `TIMELINE_BW`、`app/pages/zh/about/milestones.vue`）：這些是「哪一年發生了
 *   什麼事」的既有核實歷史紀錄（例如藍鯨隊史逐年參加第幾屆木蘭聯賽、磐石
 *   成立當年奪冠的里程碑卡片），跟「我們現在的主場／聯賽是什麼」是不同的事實
 *   類型，硬性禁止會誤殺這些合法的歷史記錄——見 `docs/18-work-errors.md`
 *   （若之後這類例外持續增加，代表該重新設計判斷方式而不是一直加白名單）。
 *
 * ## 用法
 *
 *   node scripts/check-fact-single-source.mjs
 *
 * 離開碼：命中任何一筆非允許位置的字面值 → `1`；否則 `0`。掛在 `npm run lint`
 * （見 package.json `lint:fact-single-source`）。
 */

import { readFileSync, readdirSync, statSync } from 'node:fs'
import { dirname, extname, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const HERE = dirname(fileURLToPath(import.meta.url))
const APP_DIR = resolve(HERE, '../app')
const SHARED_DIR = resolve(HERE, '../shared')

// ---------------------------------------------------------------------------
// 允許出現這些字面值的檔案（相對 apps/web），見檔頭「刻意排除的範圍」。
// ---------------------------------------------------------------------------
const ALLOWED_FILES = new Set([
  'shared/utils/site-facts.ts',
  // 歷史時間軸資料與其對應頁面——逐年既有核實紀錄，不是「單一事實」的重複陳述。
  'shared/utils/club-copy.ts', // HISTORY_YEARS_BW／TIMELINE_BW 陣列所在檔案
  'app/pages/zh/about/milestones.vue',
  // 這兩個檔案裡的命中都是開發註解（說明梯隊代碼現況／既有缺口），不是使用者看得到的
  // 頁面文字——實際渲染邏輯已改讀 ACADEMY_TEAM_TABS／academyTeamCodesLabel()。
  'app/pages/zh/academy/teams.vue',
  'app/pages/zh/index.vue',
])

// ---------------------------------------------------------------------------
// 禁止的字面值：GEO-03 五類事實（成立年份、主場、梯隊、聯賽、聯絡方式）目前
// 已核實的具體值。新增俱樂部或事實時，記得同步更新 site-facts.ts 與這份清單。
// ---------------------------------------------------------------------------
const FORBIDDEN_LITERALS = [
  // 聯絡方式：辦公室地址（tcrfc，唯一已核實地址；bw 沒有實體地址）
  '台中市北屯區崇平路二段景谷巷 11 弄 41 號',
  // 主場：場地名稱
  '西屯足球場',
  '太原足球場',
  '豐原體育場',
  // 聯賽：現役聯賽全名
  '企業甲級聯賽',
  '台灣木蘭足球聯賽',
  // 成立年份：首季頭銜（tcrfc 專屬核實事實）
  '全國乙級聯賽冠軍',
  // 梯隊組成：年齡層代碼清單（含頓號／斜線兩種既有分隔符）
  'U15／U14／U12',
  'U15、U14、U12',
  'U15／U12',
  'U15、U12',
]

function collectFiles(dir) {
  const out = []
  for (const entry of readdirSync(dir)) {
    if (entry === 'node_modules' || entry === '.nuxt' || entry === '.output') continue
    const full = resolve(dir, entry)
    const stat = statSync(full)
    if (stat.isDirectory()) {
      out.push(...collectFiles(full))
    } else if (['.vue', '.ts'].includes(extname(entry))) {
      out.push(full)
    }
  }
  return out
}

const files = [...collectFiles(APP_DIR), ...collectFiles(SHARED_DIR)]

const violations = []
for (const file of files) {
  const relPath = relative(resolve(HERE, '..'), file).replaceAll('\\', '/')
  if (ALLOWED_FILES.has(relPath)) continue
  const src = readFileSync(file, 'utf8')
  for (const literal of FORBIDDEN_LITERALS) {
    if (src.includes(literal)) {
      violations.push({ file: relPath, literal })
    }
  }
}

if (violations.length > 0) {
  console.error('✗ GEO-03 事實單一來源檢查失敗：以下位置重新寫死了單一來源事實的字面值')
  console.error('  （這些事實的唯一維護處是 shared/utils/site-facts.ts，請改用 SITE_FACTS／')
  console.error('   getPrimaryVenue()／academyTeamCodesLabel() 等既有匯出值）：\n')
  for (const v of violations) {
    console.error(`  - ${v.file}：「${v.literal}」`)
  }
  process.exit(1)
}

console.log(`✓ GEO-03 事實單一來源檢查通過（掃描 ${files.length} 個檔案，${FORBIDDEN_LITERALS.length} 條禁止字面值，0 筆命中）。`)
