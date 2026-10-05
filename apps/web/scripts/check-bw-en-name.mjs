#!/usr/bin/env node
/**
 * check-bw-en-name.mjs — 藍鯨英文名只允許客戶定案的兩種寫法（STATUS B-5，2026-10-05 定案）。
 *
 * 定案：簡稱 `Taichung Blue Whale`；全名 `Taichung Blue Whale Women's Football Club`。
 * 舊站並存的變體（`Taichung Bluewhale`、`…Women's Football Team`、`Taichung blue whale` 大小寫不一）
 * 會汙染 SEO、Schema、`llms.txt` 與 App（docs/13 §6 紀律 11、E-158），一律擋。
 * 英文句子需要名稱時優先用 `shared/utils/club-copy.ts` 的 `BW_NAME_EN`／`BW_FULL_NAME_EN`；
 * 直接寫上述兩種正確字面值也允許（樣板裡的整句英文常見）。
 *
 * 掃 app／shared／server 的 `.ts`／`.vue`，略過註解行。命中變體 → exit 1。
 */
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { dirname, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
// 抓「Taichung Blue Whale」家族（含連寫、大小寫變體、後接 Women's Football <詞>）；再判斷抓到的字面值是否為兩種定案寫法之一。
const FAMILY = /taichung\s+blue\s*whale(?:\s+women['’]?s\s+football(?:\s+\w+)?)?/gi
const ALLOWED = new Set(['Taichung Blue Whale', "Taichung Blue Whale Women's Football Club"])
const bad = []

function walk(dir) {
  for (const entry of readdirSync(dir)) {
    if (entry === 'node_modules' || entry.startsWith('.')) continue
    const full = resolve(dir, entry)
    if (statSync(full).isDirectory()) walk(full)
    else if (/\.(ts|vue)$/.test(entry)) scan(full)
  }
}
function scan(file) {
  let inBlock = false
  readFileSync(file, 'utf8').split('\n').forEach((line, i) => {
    const t = line.trim()
    if (inBlock) { if (t.includes('*/')) inBlock = false; return }
    if (t.startsWith('/*')) { if (!t.includes('*/')) inBlock = true; return }
    if (t.startsWith('//') || t.startsWith('*') || t.startsWith('<!--')) return
    for (const m of line.matchAll(FAMILY)) {
      // 「Taichung Blue Whale Women's Football」後面若不是 Club（例如 Team）會被整段抓到，不在允許集合 → 違規。
      // 允許的簡稱後面接其他字（如 "Taichung Blue Whale women's team"）時，FAMILY 只會抓到簡稱本身（women's 後不是 football）。
      if (!ALLOWED.has(m[0])) bad.push(`${relative(ROOT, file)}:${i + 1}  「${m[0]}」`)
    }
  })
}
for (const d of ['app', 'shared', 'server']) walk(resolve(ROOT, d))

if (bad.length) {
  console.error('✗ 藍鯨英文名只允許 `Taichung Blue Whale` 與 `Taichung Blue Whale Women\'s Football Club`（B-5）：\n  - ' + bad.join('\n  - '))
  process.exit(1)
}
console.log('✓ 藍鯨英文名檢查通過（只出現定案的兩種寫法，B-5）')
