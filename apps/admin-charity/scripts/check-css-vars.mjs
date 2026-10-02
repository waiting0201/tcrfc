#!/usr/bin/env node
/**
 * CSS 自訂屬性（變數）引用檢查（docs/18-work-errors.md E-122）。
 *
 * 為什麼需要：`var(--不存在的名字)` 在瀏覽器裡不會報錯，屬性只是悄悄失效（背景變透明、文字變預設色）。
 * `vue-tsc`、eslint、`vite build`、禁用詞與對比度檢查都看不到這件事——2026-10-02 的報表趨勢條就是這樣整欄隱形，
 * 靠截圖才發現。這支腳本比對「引用」與「定義」：src 底下任何 `var(--x)` 引用，`--x` 必須在 src 的某處被定義。
 *
 * 範圍與限制：
 *   · 只看 `src/**` 的 .vue／.css／.ts；Element Plus 自己的變數（`--el-` 開頭）由元件庫定義，不檢查。
 *   · 有備用值的寫法 `var(--x, 預設)` 視為作者有意處理缺值，不檢查。
 *   · 只檢查「名字有沒有定義」，不檢查值對不對（色值由 check-contrast.mjs 負責）。
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, extname, relative } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '..')
const SRC = join(ROOT, 'src')

function walk(dir) {
  const out = []
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry)
    if (statSync(full).isDirectory()) out.push(...walk(full))
    else if (['.vue', '.css', '.ts'].includes(extname(entry))) out.push(full)
  }
  return out
}

const files = walk(SRC)
const defined = new Set()
const refs = []

for (const file of files) {
  const text = readFileSync(file, 'utf-8')
  for (const m of text.matchAll(/(--[\w-]+)\s*:/g)) defined.add(m[1])
  const lines = text.split('\n')
  lines.forEach((line, i) => {
    for (const m of line.matchAll(/var\(\s*(--[\w-]+)\s*([,)])/g)) {
      if (m[2] === ',') continue // 有備用值
      refs.push({ name: m[1], file: relative(ROOT, file), line: i + 1 })
    }
  })
}

const missing = refs.filter((r) => !r.name.startsWith('--el-') && !defined.has(r.name))

if (missing.length > 0) {
  console.error('\n✗ 引用了沒有定義的 CSS 變數（瀏覽器不會報錯，樣式會悄悄失效）：')
  for (const r of missing) console.error(`  - ${r.name}  ${r.file}:${r.line}`)
  console.error('\n請對照 src/styles/charity-admin-theme.css 的實際定義改名（docs/22 §5 的速查表是簡稱，不等於實際變數名）。\n')
  process.exit(1)
}

console.log(`✓ CSS 變數引用檢查通過（${files.length} 個檔案、${refs.length} 處引用、${defined.size} 個定義）`)
