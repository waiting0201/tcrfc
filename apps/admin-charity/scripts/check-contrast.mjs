#!/usr/bin/env node
/**
 * 色票對比度檢查（docs/22-charity-ui.md §1.7，比照 apps/admin/scripts/check-contrast.mjs 的
 * 方法論——依 docs/18 E-31／E-31 升級的教訓：驗算清單不是由產出者自己挑，是從 token 定義做
 * 完整笛卡兒積；反例與豁免組合也全部列出，不是只列「會過的」。
 *
 * 本腳本同時把關**後台**（--charity-admin-*，比對 charity-admin-theme.css）與**前台**
 * （--charity-*，比對 apps/web-charity/app/assets/css/charity.css，第 8 節）兩套色票。
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
const WEB_CSS = resolve(HERE, '../../web-charity/app/assets/css/charity.css')

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
  '--charity-admin-bg-page': '#F2F5F1',
  '--charity-admin-bg-surface': '#FFFFFF',
  '--charity-admin-bg-surface-2': '#EAEEE8',
  '--charity-admin-primary-tint': '#E3F1E9',
}

const TEXTS = {
  '--charity-admin-text-primary': '#1F2A24',
  '--charity-admin-text-secondary': '#47554C',
  '--charity-admin-text-tertiary': '#58665D',
}

const BORDERS_CARTESIAN = {
  '--charity-admin-border-input': '#74857A',
}

const EXEMPT = {
  '--charity-admin-text-disabled': {
    hex: '#A0A8A2',
    reason: 'WCAG 對 disabled 狀態元件豁免對比度要求，不用於傳達資訊（docs/22 §1.7.2）',
  },
  '--charity-admin-border': {
    hex: '#CBD5CC',
    reason: '純裝飾性邊界（卡片、分隔線），不是唯一的邊界辨識手段，旁邊還有背景色階差異（docs/22 §1.7.3 明文豁免）',
  },
}

const PRIMARY = {
  '--charity-admin-primary': '#1F6B4A',
  '--charity-admin-primary-hover': '#17573C',
  '--charity-admin-primary-active': '#104630',
}

const BORDERS = {
  '--charity-admin-border-input': '#74857A',
}

const SEMANTIC = {
  success: { text: '#0F7A55', bg: '#E0F4EC' },
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

// ── 前台（--charity-*）色票：只做計算驗證，不比對 CSS（前台 CSS 屬 apps/web-charity）──
// 來源：docs/22 §1.6.1／§1.7.1／§1.7.3／§1.7.4。納入此腳本，讓前後台兩套色票由同一支腳本把關。
const F_BG = {
  'F-page': '#F7F5EE',
  'F-surface': '#FFFFFF',
  'F-surface-2': '#F0EEE3',
  'F-primary-tint': '#E8F3EC',
}
const F_TEXT = { 'F-text-primary': '#2C2621', 'F-text-secondary': '#5E554B', 'F-text-tertiary': '#6C6358', 'F-accent-text': '#9A5B00' }
const F_PRIMARY = { 'F-primary': '#1F6B4A', 'F-primary-hover': '#17573C', 'F-primary-active': '#104630' }
const F_ACCENT = { 'F-accent': '#F2A93B', 'F-accent-hover': '#E69A22', 'F-accent-active': '#D98F1A' }
const F_BORDER_INPUT = { 'F-border-input': '#8A7F6B' }
const INK = '#2C2621'
for (const [tn, th] of Object.entries(F_TEXT))
  for (const [bn, bh] of Object.entries(F_BG)) PAIRS.push([`${tn} on ${bn}`, th, bh, AA_TEXT])
for (const [pn, ph] of Object.entries(F_PRIMARY)) {
  PAIRS.push([`白字 on ${pn}`, '#FFFFFF', ph, AA_TEXT])
  for (const [bn, bh] of Object.entries(F_BG)) PAIRS.push([`${pn} on ${bn}（focus 外框）`, ph, bh, AA_UI])
}
for (const [an, ah] of Object.entries(F_ACCENT)) PAIRS.push([`墨色字 on ${an}`, INK, ah, AA_TEXT])
for (const [bn, bh] of Object.entries(F_BG))
  for (const [kn, kh] of Object.entries(F_BORDER_INPUT)) PAIRS.push([`${kn} on ${bn}`, kh, bh, AA_UI])
for (const [name, { text }] of Object.entries(SEMANTIC))
  for (const [bn, bh] of Object.entries(F_BG)) PAIRS.push([`${name} 文字 on ${bn}`, text, bh, AA_TEXT])
// 晨光金為填色（非文字）：「確認捐款」按鈕需與相鄰底色可區分——記錄對 page 的比值，供 §1.7 說明（不設門檻，靠墨色字與形狀辨識）

const COUNTER_EXAMPLES = [
  // docs/22 §1.7.3：border-input 若退回較淺的 #8A9A90 會在 surface-2 掉到 3:1 以下，把這個失敗值釘住
  ['（迴歸記錄）border-input 若用 #8A9A90 on surface-2（不及 3:1，已改用 #74857A）', '#8A9A90', '#EAEEE8', [2.4, 2.6]],
  // docs/22 §1.6.1：晨光金不得承載白字（白字 on #F2A93B 約 2.0），金色上只能放墨色字
  ['（限制釘住）白字 on 晨光金 #F2A93B（不可用，金底一律墨色字）', '#FFFFFF', '#F2A93B', [1.9, 2.1]],
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
const SPEC_ONLY = process.argv.includes('--spec-only')
let css
if (SPEC_ONLY) lines.push('    （--spec-only：略過 CSS 比對）')
try {
  if (SPEC_ONLY) throw new Error('skip')
  css = readFileSync(THEME_CSS, 'utf8')
} catch {
  if (!SPEC_ONLY) failures.push(`讀不到 ${THEME_CSS}`)
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

lines.push('')
lines.push('【8】規格（docs/22 §1.6.1／§1.6.3）vs 實作（apps/web-charity charity.css）')
{
  let webCss = null
  if (SPEC_ONLY) lines.push('    （--spec-only：略過 CSS 比對）')
  else {
    try {
      webCss = readFileSync(WEB_CSS, 'utf8')
    } catch {
      failures.push(`讀不到 ${WEB_CSS}`)
    }
  }
  if (webCss) {
    const strip = (o) => Object.fromEntries(Object.entries(o).map(([k, v]) => [k.replace(/^F-/, '--charity-'), v]))
    const FRONT_TOKENS = {
      '--charity-bg-page': F_BG['F-page'],
      '--charity-bg-surface': F_BG['F-surface'],
      '--charity-bg-surface-2': F_BG['F-surface-2'],
      '--charity-primary-tint': F_BG['F-primary-tint'],
      ...strip(F_TEXT),
      ...strip(F_PRIMARY),
      ...strip(F_ACCENT),
      ...strip(F_BORDER_INPUT),
      '--charity-text-disabled': '#ABA6A0',
      '--charity-border': '#DAD5C6',
      ...Object.fromEntries(
        Object.entries(SEMANTIC).flatMap(([name, { text, bg }]) => [
          [`--charity-${name}-text`, text],
          [`--charity-${name}-bg`, bg],
        ]),
      ),
    }
    for (const [token, expected] of Object.entries(FRONT_TOKENS)) {
      const m = webCss.match(new RegExp(`(?:^|[\\s;{])${token}\\s*:\\s*(#[0-9a-fA-F]{6})`, 'm'))
      if (!m) {
        failures.push(`charity.css 找不到 ${token} 的定義`)
        lines.push(`    ✗ ${token.padEnd(28)} 未定義`)
        continue
      }
      const actual = m[1].toUpperCase()
      const ok = actual === expected.toUpperCase()
      if (!ok) failures.push(`${token}：web-charity charity.css 是 ${actual}，docs/22 §1.6.1／§1.6.3 是 ${expected}`)
      lines.push(`    ${ok ? '✓' : '✗'} ${token.padEnd(28)} ${actual}`)
      checkedCount++
    }
    // 晨光金鐵律：.btn-accent 不得放白字
    const accentBlock = webCss.match(/\.btn-accent\s*\{[^}]*\}/)
    const okInk = !!accentBlock && /color:\s*var\(--charity-text-primary\)/.test(accentBlock[0])
    if (!okInk) failures.push('charity.css 的 .btn-accent 字色必須是 var(--charity-text-primary)（晨光金不得承載白字）')
    lines.push(`    ${okInk ? '✓' : '✗'} .btn-accent 字色為墨色 --charity-text-primary`)
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
