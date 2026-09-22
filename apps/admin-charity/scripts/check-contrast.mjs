#!/usr/bin/env node
/**
 * 色票對比度檢查（docs/22-charity-ui.md §1.7，比照 apps/admin/scripts/check-contrast.mjs 的
 * 方法論——依 docs/18 E-31／E-31 升級的教訓：驗算清單不是由產出者自己挑，是從 token 定義做
 * 完整笛卡兒積；反例與豁免組合也全部列出，不是只列「會過的」。
 *
 * 本腳本只驗**後台**（--charity-admin-*）色票——本專案（apps/admin-charity）只出後台頁面，
 * 前台（--charity-*，無 -admin- 字首）token 屬於 apps/web-charity 的範圍，不在這裡驗。
 *
 * E-31 升級版的要求（docs/18）：腳本要能回答「有沒有任何一組色票組合是它沒驗到的」——所有
 * 前景 token × 所有背景 token 展開成完整矩陣，逐一落在「通過」／「明文豁免」／「反例釘住」
 * 三類之一，否則報錯。本腳本的六種檢查對應這個要求：
 *   1. 文字 × 背景笛卡兒積（AA_TEXT 4.5:1）
 *   2. 邊框 × 背景笛卡兒積（AA_UI 3:1，含明文豁免的裝飾性邊框）
 *   3. 操作主色（primary／hover／active）× 背景笛卡兒積，UI 元件門檻（focus 外框用途）
 *   4. 白字 × 操作主色三態／語意色實心底（按鈕文字，AA_TEXT 4.5:1）
 *   5. 語意色文字 × 背景笛卡兒積（自身標籤底 ＋ 三層背景，AA_TEXT 4.5:1）
 *   6. 反例／已知限制數字釘住 ＋ 規格（docs/22 §1.6／§1.7）vs 實作（charity-admin-theme.css）
 *
 * 色票的真實來源是 docs/22-charity-ui.md §1.6.2／§1.6.3／§1.7。要改色值先改那份文件，
 * 再同步這裡與 charity-admin-theme.css，三者不一致時本腳本會失敗。
 */

import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'

const HERE = dirname(fileURLToPath(import.meta.url))
const THEME_CSS = resolve(HERE, '../src/styles/charity-admin-theme.css')

function luminance(hex) {
  const ch = [1, 3, 5]
    .map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)
    .map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4))
  return 0.2126 * ch[0] + 0.7152 * ch[1] + 0.0722 * ch[2]
}

function contrast(a, b) {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return (hi + 0.05) / (lo + 0.05)
}

const AA_TEXT = 4.5
const AA_UI = 3.0

// ── 色票定義（來源：docs/22-charity-ui.md §1.6.2／§1.6.3／§1.7）───────────────

const BACKGROUNDS = {
  '--charity-admin-bg-page': '#F3F4F7',
  '--charity-admin-bg-surface': '#FFFFFF',
  '--charity-admin-bg-surface-2': '#E8EAEE',
}

const TEXTS = {
  '--charity-admin-text-primary': '#21242C',
  '--charity-admin-text-secondary': '#505662',
  '--charity-admin-text-tertiary': '#636874',
}

const BORDERS_CARTESIAN = {
  '--charity-admin-border-input': '#778090',
}

const EXEMPT = {
  '--charity-admin-text-disabled': {
    hex: '#A0A4AB',
    reason: 'WCAG 對 disabled 狀態元件豁免對比度要求，不用於傳達資訊（docs/22 §1.7.2）',
  },
  '--charity-admin-border': {
    hex: '#D0D4DC',
    reason: '純裝飾性邊界（卡片、分隔線），不是唯一的邊界辨識手段，旁邊還有背景色階差異（docs/22 §1.7.3 明文豁免）',
  },
}

const PRIMARY = {
  '--charity-admin-primary': '#21242C',
  '--charity-admin-primary-hover': '#343A46',
  '--charity-admin-primary-active': '#111317',
}

const BORDERS = {
  '--charity-admin-border-input': '#778090',
}

const SEMANTIC = {
  success: { text: '#206F41', bg: '#E9F7EF' },
  danger: { text: '#9B3027', bg: '#FBEBE9' },
  warning: { text: '#8C5B07', bg: '#FDF4D8' },
  info: { text: '#25527E', bg: '#EBF2FA' },
  refund: { text: '#7556A1', bg: '#F2EFF5' },
}

const PAIRS = []

// 白字承載色：操作主色三態（docs/22 §1.7.4）
for (const [label, hex] of Object.entries(PRIMARY)) {
  PAIRS.push([`白字 on ${label}`, '#FFFFFF', hex, AA_TEXT])
}

// 白字承載色：語意色實心底（按鈕，docs/22 §1.7.7）
for (const [name, { text }] of Object.entries(SEMANTIC)) {
  PAIRS.push([`白字 on ${name} 實心底`, '#FFFFFF', text, AA_TEXT])
}

// 語意色文字 × 自身標籤底（docs/22 §1.7.5）
for (const [name, { text, bg }] of Object.entries(SEMANTIC)) {
  PAIRS.push([`${name} 文字 on 自身標籤底`, text, bg, AA_TEXT])
}

// 語意色文字 × 三層後台背景笛卡兒積（docs/22 §1.7.6 的後台三欄）
for (const [name, { text }] of Object.entries(SEMANTIC)) {
  for (const [bgName, bgHex] of Object.entries(BACKGROUNDS)) {
    PAIRS.push([`${name} 文字 on ${bgName}`, text, bgHex, AA_TEXT])
  }
}

// 危險色兼任表單錯誤邊框（docs/22 §1.7.8：驗證表單錯誤狀態邊框，門檻 3:1）
for (const [bgName, bgHex] of Object.entries(BACKGROUNDS)) {
  PAIRS.push([`danger 文字兼表單邊框 on ${bgName}`, SEMANTIC.danger.text, bgHex, AA_UI])
}

const COUNTER_EXAMPLES = [
  // docs/22 §1.7.3：後台 border-input 初版 #788191 對 surface-2 只過 3:1 一點點（餘裕僅 0.26），
  // 依同一套反推法重新推導到 #778090，把這個「勉強過」的舊值釘住，避免有人不小心改回去
  ['（迴歸記錄）border-input 若用初版 #788191 on surface-2（過但餘裕僅 0.26，已改用 #778090）', '#788191', '#E8EAEE', [3.2, 3.3]],
]

// ── 檢查 ────────────────────────────────────────────────────────────────

const failures = []
const lines = []
let checkedCount = 0

lines.push('【1】文字 × 背景 全組合（AA_TEXT 4.5:1）')
for (const [tName, tHex] of Object.entries(TEXTS)) {
  for (const [bName, bHex] of Object.entries(BACKGROUNDS)) {
    const ratio = contrast(tHex, bHex)
    const pass = ratio >= AA_TEXT
    checkedCount++
    if (!pass) failures.push(`${tName} (${tHex}) on ${bName} (${bHex}) = ${ratio.toFixed(2)}:1，未達 AA ${AA_TEXT}:1`)
    lines.push(`    ${pass ? '✓' : '✗'} ${tName.padEnd(30)} on ${bName.padEnd(28)} ${ratio.toFixed(2).padStart(6)}:1`)
  }
}

lines.push('')
lines.push('【2】邊框 × 背景 全組合（AA_UI 3:1）')
for (const [tName, tHex] of Object.entries(BORDERS_CARTESIAN)) {
  for (const [bName, bHex] of Object.entries(BACKGROUNDS)) {
    const ratio = contrast(tHex, bHex)
    const pass = ratio >= AA_UI
    checkedCount++
    if (!pass) failures.push(`${tName} (${tHex}) on ${bName} (${bHex}) = ${ratio.toFixed(2)}:1，未達 AA_UI ${AA_UI}:1`)
    lines.push(`    ${pass ? '✓' : '✗'} ${tName.padEnd(30)} on ${bName.padEnd(28)} ${ratio.toFixed(2).padStart(6)}:1`)
  }
}

lines.push('')
lines.push('【3】豁免 token（不強制過門檻，逐層算出數字＋理由）')
for (const [tName, { hex, reason }] of Object.entries(EXEMPT)) {
  for (const [bName, bHex] of Object.entries(BACKGROUNDS)) {
    const ratio = contrast(hex, bHex)
    checkedCount++
    lines.push(`    ~ ${tName.padEnd(30)} on ${bName.padEnd(28)} ${ratio.toFixed(2).padStart(6)}:1  （豁免：${reason}）`)
  }
}

lines.push('')
lines.push('【4】操作主色（primary／hover／active）× 背景 全組合（UI 元件門檻 3:1，focus 外框用途）')
for (const [pName, pHex] of Object.entries(PRIMARY)) {
  for (const [bName, bHex] of Object.entries(BACKGROUNDS)) {
    const ratio = contrast(pHex, bHex)
    const pass = ratio >= AA_UI
    checkedCount++
    if (!pass) failures.push(`${pName} (${pHex}) on ${bName} (${bHex}) = ${ratio.toFixed(2)}:1，未達 AA_UI ${AA_UI}:1`)
    lines.push(`    ${pass ? '✓' : '✗'} ${pName.padEnd(30)} on ${bName.padEnd(28)} ${ratio.toFixed(2).padStart(6)}:1`)
  }
}

lines.push('')
lines.push('【5】成對色票（白字承載色、語意色文字×自身底／三層背景、危險色兼表單邊框）')
for (const [label, fg, bg, threshold] of PAIRS) {
  const ratio = contrast(fg, bg)
  const pass = ratio >= threshold
  checkedCount++
  if (!pass) failures.push(`${label}：${fg} on ${bg} = ${ratio.toFixed(2)}:1，未達門檻 ${threshold}:1`)
  lines.push(`    ${pass ? '✓' : '✗'} ${label.padEnd(46)} ${ratio.toFixed(2).padStart(6)}:1  (門檻 ${threshold})`)
}

lines.push('')
lines.push('【6】反例與已知限制（漂移就失敗）')
for (const [label, fg, bg, expected] of COUNTER_EXAMPLES) {
  const ratio = contrast(fg, bg)
  checkedCount++
  let ok
  let expectedLabel
  if (Array.isArray(expected)) {
    const [min, max] = expected
    ok = ratio >= min && ratio <= max
    expectedLabel = `${min}–${max}`
  } else {
    ok = Math.abs(ratio - expected) < 0.01
    expectedLabel = `${expected}`
  }
  if (!ok) failures.push(`${label}：實算 ${ratio.toFixed(2)}:1，但文件記的是 ${expectedLabel}:1`)
  lines.push(`    ${ok ? '✓' : '✗'} ${label.padEnd(70)} ${ratio.toFixed(2)}:1`)
}

lines.push('')
lines.push('【7】規格（docs/22 §1.6／§1.7）vs 實作（charity-admin-theme.css）')
let css
try {
  css = readFileSync(THEME_CSS, 'utf8')
} catch {
  failures.push(`讀不到 ${THEME_CSS}`)
  css = null
}

if (css) {
  const exemptHexOnly = Object.fromEntries(Object.entries(EXEMPT).map(([k, v]) => [k, v.hex]))
  const semanticTokens = Object.fromEntries(
    Object.entries(SEMANTIC).flatMap(([name, { text, bg }]) => [
      [`--charity-${name}-text`, text],
      [`--charity-${name}-bg`, bg],
    ]),
  )
  const ALL_TOKENS = { ...BACKGROUNDS, ...TEXTS, ...exemptHexOnly, ...PRIMARY, ...BORDERS, ...semanticTokens }
  for (const [token, expected] of Object.entries(ALL_TOKENS)) {
    const m = css.match(new RegExp(`${token}\\s*:\\s*(#[0-9a-fA-F]{6})`))
    if (!m) {
      failures.push(`charity-admin-theme.css 找不到 ${token} 的定義`)
      lines.push(`    ✗ ${token.padEnd(28)} 未定義`)
      continue
    }
    const actual = m[1].toUpperCase()
    const ok = actual === expected.toUpperCase()
    if (!ok) failures.push(`${token}：charity-admin-theme.css 是 ${actual}，docs/22 §1.6／§1.7 是 ${expected}`)
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
    '\n色票的真實來源是 docs/22-charity-ui.md §1.6／§1.7。' +
      '\n要改色值請先改那份文件，再同步本腳本與 charity-admin-theme.css。',
  )
  process.exit(1)
}

console.log(
  `✓ 色票對比度檢查通過（共 ${checkedCount} 組組合：文字×背景笛卡兒積、邊框×背景笛卡兒積、` +
    `豁免 token 記錄、主色×背景笛卡兒積、成對色票、反例釘住 ＋ token 值與 docs/22 §1.6／§1.7 一致）`,
)
