#!/usr/bin/env node
/**
 * check-bw-units-citation.mjs — 靜態檢查 `shared/utils/units.ts` 的
 * `BLUE_WHALE_DISABLED_UNITS`，確保每一個關閉的單元代號都附有規格依據。
 *
 * ## 為什麼要有這支腳本（BW-C1，2026-09-29）
 *
 * 藍鯨規劃書 §1.3 總則明文「本站與主站是同一套網站，只有配色不同」，**例外只有
 * §2.1 的四項單元取捨**。S1-15／S2-8／S2-10 三輪先後把 9 個單元加進
 * `BLUE_WHALE_DISABLED_UNITS`，理由都是「藍鯨沒有對應真實內容」——這個理由本身
 * 沒有錯，但不構成「用 404 關閉整頁」的正當性，正確處理是重開頁面、內容顯示
 * 既有空狀態。BW-C1 已把這 9 項改回開放，只留四項例外能直接推導的 3 個代號
 * （'06'／'11'／'4.7'）。
 *
 * 為了不讓下一個人再犯同一種錯（改規格前只核對「有沒有明文排除」，沒有回頭核對
 * 「總則的例外只有四項」這句話本身），這支腳本要求：**陣列裡每一個字串常值都必須
 * 在同一行帶 `//` 行內註解，且註解必須包含 `§`**（引用藍鯨規劃書章節）。沒有章節
 * 依據的關閉一律視為不成立，直接讓 lint 失敗——不是靠自覺遵守，是靠這支腳本強制。
 *
 * ## 用法
 *
 *   node scripts/check-bw-units-citation.mjs
 *
 * 離開碼：任何一個陣列項目缺少 `§` 章節依據 → `1`；否則 `0`。
 */

import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const HERE = dirname(fileURLToPath(import.meta.url))
const UNITS_FILE = resolve(HERE, '../shared/utils/units.ts')

const src = readFileSync(UNITS_FILE, 'utf8')

const arrayMatch = /BLUE_WHALE_DISABLED_UNITS\s*:\s*readonly string\[\]\s*=\s*\[([\s\S]*?)\]/.exec(src)
if (!arrayMatch) {
  console.error('✗ 找不到 BLUE_WHALE_DISABLED_UNITS 陣列，腳本可能與檔案結構不同步。')
  process.exit(1)
}

const lines = arrayMatch[1]
  .split('\n')
  .map((line) => line.trim())
  .filter((line) => line.length > 0)

const problems = []
for (const line of lines) {
  const codeMatch = /^'([^']+)'/.exec(line)
  if (!codeMatch) continue // 不是陣列項目行（理論上不會出現，陣列裡只有字串常值）
  const code = codeMatch[1]
  const commentMatch = /\/\/(.*)$/.exec(line)
  if (!commentMatch || !commentMatch[1].includes('§')) {
    problems.push(code)
  }
}

if (problems.length > 0) {
  console.error(`✗ BLUE_WHALE_DISABLED_UNITS 裡有 ${problems.length} 個單元代號缺少「§ 規格章節」依據的行內註解：`)
  for (const code of problems) {
    console.error(`  - '${code}'`)
  }
  console.error('\n  每一項都必須在同一行寫 `// ...§...`，引用藍鯨規劃書章節，理由見 units.ts 檔頭與 docs/14-invariants.md。')
  process.exit(1)
}

console.log(`✓ BLUE_WHALE_DISABLED_UNITS（${lines.length} 項）全部附有 § 規格章節依據。`)
