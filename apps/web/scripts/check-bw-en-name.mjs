#!/usr/bin/env node
/**
 * check-bw-en-name.mjs — 藍鯨英文正式全名未定前，程式碼不得寫死任何一種英文寫法（STATUS B-5）。
 *
 * 舊站並存 `Taichung Bluewhale`／`Taichung Blue Whale Women's Football Team`／`Taichung blue whale`，
 * 客戶尚未指定；選錯會汙染 SEO、Schema、`llms.txt` 與 App（docs/13 §6 紀律 11、E-158）。
 * 英文句子裡需要藍鯨名稱時一律用 `shared/utils/club-copy.ts` 的 `BW_NAME_EN_PENDING`，B-5 定案後只改那一處。
 *
 * 掃 app／shared／server 的 `.ts`／`.vue`，**略過註解行**（說明文字需要引用這些寫法），不分大小寫、
 * 容許 `Blue Whale`／`Bluewhale` 之間的空白差異。命中 → exit 1。
 */
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { dirname, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const PATTERN = /taichung\s+blue\s*whale/i
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
    if (PATTERN.test(line)) bad.push(`${relative(ROOT, file)}:${i + 1}`)
  })
}
for (const d of ['app', 'shared', 'server']) walk(resolve(ROOT, d))

if (bad.length) {
  console.error('✗ 程式碼寫死了藍鯨英文名（B-5 客戶未指定，改用 BW_NAME_EN_PENDING）：\n  - ' + bad.join('\n  - '))
  process.exit(1)
}
console.log('✓ 藍鯨英文名檢查通過（沒有寫死任何一種 Taichung Blue Whale 寫法，B-5）')
