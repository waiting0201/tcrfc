#!/usr/bin/env node
/**
 * 色票對比度檢查（docs/18 E-31 的防呆）
 *
 * E-31 的根因不是「算錯」，是**驗算範圍由產出者自己挑**——沒進待驗清單的組合
 * 不會被發現。所以這支腳本的核心是「不准挑」：每一階文字色 × 每一層背景色跑
 * 笛卡兒積全驗，漏掉的組合會自己浮出來。
 *
 * 深色主題的陷阱：淺色主題容器越疊越暗、文字對比只會變好；深色主題相反，
 * 容器越疊越亮、對比只會變差。所以最亮的 overlay 才是深色系統真正的門檻。
 * E-31 就是漏驗 overlay 才讓 4.35:1 混過去的。
 *
 * 三種檢查：
 *   1. 笛卡兒積：每階文字 × 每層背景，全部要過門檻
 *   2. 成對色票：四態 tag、語意色按鈕等指定的前景／背景配對
 *   3. 規格 vs 實作：docs/21 §7 的色值要與 admin-theme.css 實際的值一致
 *
 * 色票的真實來源是 docs/21-admin-ui.md §7／§4。**要改色值先改那份文件**，
 * 再同步這裡與 admin-theme.css，三者不一致時本腳本會失敗。
 */

import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'

const HERE = dirname(fileURLToPath(import.meta.url))
const THEME_CSS = resolve(HERE, '../src/styles/admin-theme.css')

/** WCAG 2.1 相對亮度 */
function luminance(hex) {
  const ch = [1, 3, 5]
    .map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)
    .map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4))
  return 0.2126 * ch[0] + 0.7152 * ch[1] + 0.0722 * ch[2]
}

/** WCAG 2.1 對比度 */
function contrast(a, b) {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return (hi + 0.05) / (lo + 0.05)
}

const AA_TEXT = 4.5 // 正文與小字
const AA_UI = 3.0 // UI 元件與圖形邊界

// ── 色票定義（來源：docs/21-admin-ui.md §7、§4）─────────────────────────

/** 四層背景色階 ＋ 輸入框底（§7.2） */
const BACKGROUNDS = {
  '--admin-bg-canvas': '#14161A',
  '--admin-bg-surface': '#1C1F24',
  '--admin-bg-surface-2': '#242830',
  '--admin-bg-overlay': '#2A2E36',
  '--admin-bg-input': '#101215',
}

/** 文字階層（§7.3）。disabled 依 WCAG 豁免對比度要求，不參與笛卡兒積 */
const TEXTS = {
  '--admin-text-primary': '#E8EAED',
  '--admin-text-secondary': '#ABB2BF',
  '--admin-text-tertiary': '#9299A8',
}

/** 依 WCAG 豁免的 token：不驗對比度，但仍驗規格與實作是否一致 */
const EXEMPT = {
  '--admin-text-disabled': '#565C66', // disabled 元件豁免
  '--admin-border': '#333840', // 純裝飾性邊界，非唯一辨識手段
}

/** 操作主色（§7.5） */
const PRIMARY = {
  '--admin-primary': '#409EFF',
  '--admin-primary-fill': '#2C6799',
  '--admin-primary-fill-hover': '#3579B3',
  '--admin-primary-fill-active': '#24557F',
}

/** 邊框（§7.4） */
const BORDERS = {
  '--admin-border-input': '#6B7280',
}

/**
 * 成對色票：笛卡兒積涵蓋不到的、前景與背景綁定的組合
 * （§4.2 四態 tag、§4.3 語意色、§7.5 實心按鈕、§7.4 邊框）
 */
const PAIRS = [
  // 四態 tag（§4.2）
  ['草稿 tag', '#ABB2BF', '#242830', AA_TEXT],
  ['排程發布 tag', '#FFB84D', '#3A2E14', AA_TEXT],
  ['已發布 tag', '#5FD68A', '#13301F', AA_TEXT],
  ['已停用 tag', '#C98BA8', '#33202B', AA_TEXT],

  // 操作型語意色文字，置於 surface 上（§4.3）
  ['危險文字', '#FF9494', '#1C1F24', AA_TEXT],
  ['警告文字', '#FFB84D', '#1C1F24', AA_TEXT],
  ['成功文字', '#5FD68A', '#1C1F24', AA_TEXT],

  // 實心語意按鈕（§4.3）
  ['危險按鈕白字', '#FFFFFF', '#B3453F', AA_TEXT],
  ['成功按鈕白字', '#FFFFFF', '#2E7D4F', AA_TEXT],
  // 警告按鈕刻意用深字：白字只有 1.72:1，深字 10.54:1（§4.3）
  ['警告按鈕深字', '#14161A', '#FFB84D', AA_TEXT],

  // 實心主要按鈕三態，白字（§7.5）
  ['主按鈕白字', '#FFFFFF', '#2C6799', AA_TEXT],
  ['主按鈕 hover 白字', '#FFFFFF', '#3579B3', AA_TEXT],
  ['主按鈕 active 白字', '#FFFFFF', '#24557F', AA_TEXT],

  // primary 當文字色（§7.5）
  ['primary 文字 on surface', '#409EFF', '#1C1F24', AA_TEXT],

  // 表單元件邊框，UI 元件門檻 3:1（§7.4）
  ['輸入框邊框 on surface', '#6B7280', '#1C1F24', AA_UI],
  ['輸入框邊框 on input', '#6B7280', '#101215', AA_UI],
  ['focus 外框 on input', '#409EFF', '#101215', AA_UI],
]

/**
 * 反例：文件用來論證「這個做法不行」的數字。
 * 結論方向對的時候沒有東西會反彈，所以 E-31 才會讓 4.06:1（實為 1.72:1）
 * 混過去——這裡把反例也釘住，數字漂移一樣會失敗。
 */
const COUNTER_EXAMPLES = [
  ['警告按鈕若用白字（§4.3 反例）', '#FFFFFF', '#FFB84D', 1.72],
  ['白字套 Element Plus 預設藍（§7.5 反例）', '#FFFFFF', '#409EFF', 2.78],
]

// ── 檢查 ────────────────────────────────────────────────────────────────

const failures = []
const lines = []

// 1. 笛卡兒積：每階文字 × 每層背景
lines.push('【1】文字 × 背景 全組合（不挑，這是 E-31 的重點）')
for (const [tName, tHex] of Object.entries(TEXTS)) {
  for (const [bName, bHex] of Object.entries(BACKGROUNDS)) {
    const ratio = contrast(tHex, bHex)
    const pass = ratio >= AA_TEXT
    if (!pass) {
      failures.push(
        `${tName} (${tHex}) on ${bName} (${bHex}) = ${ratio.toFixed(2)}:1，未達 AA ${AA_TEXT}:1`,
      )
    }
    lines.push(
      `    ${pass ? '✓' : '✗'} ${tName.padEnd(24)} on ${bName.padEnd(22)} ${ratio.toFixed(2).padStart(6)}:1`,
    )
  }
}

// 2. 成對色票
lines.push('')
lines.push('【2】成對色票（四態、語意色、按鈕、邊框）')
for (const [label, fg, bg, threshold] of PAIRS) {
  const ratio = contrast(fg, bg)
  const pass = ratio >= threshold
  if (!pass) {
    failures.push(
      `${label}：${fg} on ${bg} = ${ratio.toFixed(2)}:1，未達門檻 ${threshold}:1`,
    )
  }
  lines.push(
    `    ${pass ? '✓' : '✗'} ${label.padEnd(30)} ${ratio.toFixed(2).padStart(6)}:1  (門檻 ${threshold})`,
  )
}

// 3. 反例數字釘住
lines.push('')
lines.push('【3】反例數字（文件用來論證「不能這樣做」的數字，漂移就失敗）')
for (const [label, fg, bg, expected] of COUNTER_EXAMPLES) {
  const ratio = contrast(fg, bg)
  const drift = Math.abs(ratio - expected)
  const ok = drift < 0.01
  if (!ok) {
    failures.push(
      `${label}：實算 ${ratio.toFixed(2)}:1，但文件記的是 ${expected}:1`,
    )
  }
  lines.push(
    `    ${ok ? '✓' : '✗'} ${label.padEnd(40)} ${ratio.toFixed(2)}:1`,
  )
}

// 4. 規格 vs 實作：admin-theme.css 的值要與上面的定義一致
lines.push('')
lines.push('【4】規格（docs/21 §7）vs 實作（admin-theme.css）')
let css
try {
  css = readFileSync(THEME_CSS, 'utf8')
} catch {
  failures.push(`讀不到 ${THEME_CSS}`)
  css = null
}

if (css) {
  const ALL_TOKENS = { ...BACKGROUNDS, ...TEXTS, ...EXEMPT, ...PRIMARY, ...BORDERS }
  for (const [token, expected] of Object.entries(ALL_TOKENS)) {
    // 比對時大小寫不敏感（CSS 常寫小寫十六進位）
    const m = css.match(new RegExp(`${token}\\s*:\\s*(#[0-9a-fA-F]{6})`))
    if (!m) {
      failures.push(`admin-theme.css 找不到 ${token} 的定義`)
      lines.push(`    ✗ ${token.padEnd(28)} 未定義`)
      continue
    }
    const actual = m[1].toUpperCase()
    const ok = actual === expected.toUpperCase()
    if (!ok) {
      failures.push(
        `${token}：admin-theme.css 是 ${actual}，docs/21 §7 是 ${expected}`,
      )
    }
    lines.push(`    ${ok ? '✓' : '✗'} ${token.padEnd(28)} ${actual}`)
  }
}

// ── 輸出 ────────────────────────────────────────────────────────────────

const verbose = process.argv.includes('--verbose') || process.argv.includes('-v')
if (verbose || failures.length > 0) {
  console.log(lines.join('\n'))
  console.log('')
}

if (failures.length > 0) {
  console.error(`✗ 色票對比度檢查未通過（${failures.length} 項）：\n`)
  for (const f of failures) console.error(`  - ${f}`)
  console.error(
    '\n色票的真實來源是 docs/21-admin-ui.md §7／§4。' +
      '\n要改色值請先改那份文件，再同步本腳本與 admin-theme.css。',
  )
  process.exit(1)
}

const total =
  Object.keys(TEXTS).length * Object.keys(BACKGROUNDS).length +
  PAIRS.length +
  COUNTER_EXAMPLES.length
console.log(`✓ 色票對比度檢查通過（${total} 組組合 ＋ token 值與 docs/21 §7 一致）`)
