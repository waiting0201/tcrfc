#!/usr/bin/env node
/**
 * 色票對比度檢查（docs/18 E-31 的防呆，v3 隨 docs/21-admin-ui.md §15.2 全面換色＋擴大覆蓋範圍）
 *
 * E-31 的根因不是「算錯」，是**驗算範圍由產出者自己挑**——沒進待驗清單的組合
 * 不會被發現。這支腳本的核心規則是「不准挑」：每一種前景色（文字／邊框／主色）
 * ×每一層背景色都要跑完整笛卡兒積，漏掉的組合會自己浮出來。
 *
 * v3 上線前又踩了一次同一個根因：`--admin-border-input` 的暖化換算值只驗了 surface
 * 一層（3.52:1 過），沒有驗到 overlay（實際只有 2.66:1，過不了 3:1 的 UI 元件門檻）。
 * 這次修正方式是**把邊框類 token 也納入五層背景的笛卡兒積**（見下方 BORDERS_CARTESIAN），
 * 不再只挑兩三組手動比對——這樣「這支腳本到底驗到了什麼」不再是產出者說了算，是清單本身
 * 的結構保證。
 *
 * 深色主題的陷阱：淺色主題容器越疊越暗、文字對比只會變好；深色主題相反，
 * 容器越疊越亮、對比只會變差。所以最亮的 overlay 才是深色系統真正的門檻。
 *
 * 五種檢查：
 *   1. 文字笛卡兒積：每階文字 × 每層背景，全部要過 4.5:1（AA_TEXT）
 *   2. 邊框笛卡兒積：邊框 token × 每層背景，全部要過 3:1（AA_UI）——這是這次新增的部分
 *   3. 豁免 token：WCAG 本來就豁免、或明文「非唯一辨識手段」的 token，逐層算出來記錄理由，
 *      不強制過門檻，但也不是「沒驗」——理由與數字都印出來
 *   4. 成對色票：四態 tag、語意色按鈕等前景／背景綁定的組合，含「已知不過」的規格內限定
 *      （--admin-primary 對 overlay 明文只有 4.02:1，見 §7.5／§6.3）
 *   5. 反例數字釘住：文件用來論證「這樣不行」的數字，漂移就失敗
 *   6. 規格 vs 實作：docs/21 §7 的色值要與 admin-theme.css 實際的值一致
 *
 * 色票的真實來源是 docs/21-admin-ui.md §7／§4／§15。**要改色值先改那份文件**，
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
const AA_UI = 3.0 // UI 元件與圖形邊界（含邊框、focus 外框）

// ── 色票定義（來源：docs/21-admin-ui.md §7、§4、§15）────────────────────

/** 四層背景色階 ＋ 輸入框底（§7.2，v3 由品牌黑推導） */
const BACKGROUNDS = {
  '--admin-bg-canvas': '#150F0D',
  '--admin-bg-surface': '#231916',
  '--admin-bg-surface-2': '#31231F',
  '--admin-bg-overlay': '#3F2D28',
  '--admin-bg-input': '#070504',
}

/** 文字階層（§7.3，v3 暖化）。disabled 依 WCAG 豁免對比度要求，不參與笛卡兒積，見 EXEMPT */
const TEXTS = {
  '--admin-text-primary': '#EBE9E8',
  '--admin-text-secondary': '#B7B0AC',
  '--admin-text-tertiary': '#A39B95',
}

/**
 * 邊框類 token（§7.4，v3 暖化）：與文字一樣跑五層背景笛卡兒積，門檻 3:1。
 * 這是這次補的洞——舊版只挑了「on surface」「on input」兩組手動比對，漏了 overlay。
 * `--admin-border`（純裝飾性邊界）不放進來，因為文件明文它不需要達 3:1（見 EXEMPT）。
 */
const BORDERS_CARTESIAN = {
  '--admin-border-input': '#8A7E75',
}

/**
 * 豁免 token：WCAG 本來就豁免（disabled），或文件明文「非唯一辨識手段」（純裝飾邊框）。
 * 不強制過門檻，但仍對五層背景逐一算出數字並印出理由——E-33 教訓是「不要只寫掃到什麼，
 * 也要寫掃不到什麼／為什麼不驗」，這裡比照辦理：豁免不等於不驗，是驗了但不當作失敗。
 */
const EXEMPT = {
  '--admin-text-disabled': {
    hex: '#625A55',
    reason: 'WCAG 對 disabled 狀態元件豁免對比度要求',
  },
  '--admin-border': {
    hex: '#3C3733',
    reason: '純裝飾性邊界（卡片、分隔線），不是唯一的邊界辨識手段，旁邊還有背景色階差異（§7.4）',
  },
}

/** 操作主色（§7.5，v3 由 Element Plus 預設藍改為品牌桃紅系） */
const PRIMARY = {
  '--admin-primary': '#E85BA9',
  '--admin-primary-fill': '#D61E83',
  '--admin-primary-fill-hover': '#AE186B',
  '--admin-primary-fill-active': '#841852',
}

/** 邊框（規格 vs 實作比對用，含上面笛卡兒積已涵蓋的 token） */
const BORDERS = {
  '--admin-border-input': '#8A7E75',
}

/**
 * 成對色票：笛卡兒積涵蓋不到的、前景與背景綁定的組合
 * （§4.2 四態 tag、§4.3 語意色、§7.5 實心按鈕、§7.4 邊框 UI 門檻）
 */
const PAIRS = [
  // 四態 tag（§4.2）：草稿跟著新暖色階連動，其餘三態獨立配色未改值
  ['草稿 tag', '#B7B0AC', '#31231F', AA_TEXT],
  ['排程發布 tag', '#FFB84D', '#3A2E14', AA_TEXT],
  ['已發布 tag', '#5FD68A', '#13301F', AA_TEXT],
  ['已停用 tag', '#C98BA8', '#33202B', AA_TEXT],

  // 操作型語意色文字，置於 surface 上（§4.3，v3 危險色橘紅化）
  ['危險文字', '#FFAD94', '#231916', AA_TEXT],
  ['警告文字', '#FFB84D', '#231916', AA_TEXT],
  ['成功文字', '#5FD68A', '#231916', AA_TEXT],
  ['中性／資訊文字 on surface-2', '#B7B0AC', '#31231F', AA_TEXT],

  // 實心語意按鈕（§4.3）
  ['危險按鈕白字', '#FFFFFF', '#B35A3F', AA_TEXT],
  ['成功按鈕白字', '#FFFFFF', '#2E7D4F', AA_TEXT],
  // 警告按鈕刻意用深字：白字只有 1.72:1，深字 11.04:1（§4.3）
  ['警告按鈕深字', '#150F0D', '#FFB84D', AA_TEXT],

  // 實心主要按鈕三態，白字（§7.5：--brand-aa／--brand-deep／衍生 active 色）
  ['主按鈕白字（--brand-aa）', '#FFFFFF', '#D61E83', AA_TEXT],
  ['主按鈕 hover 白字（--brand-deep）', '#FFFFFF', '#AE186B', AA_TEXT],
  ['主按鈕 active 白字（衍生色）', '#FFFFFF', '#841852', AA_TEXT],

  // primary 當文字色（§7.5）：v3 起明文「四層過、overlay 不過」，這裡只放過得了的四層，
  // overlay 那組移到 COUNTER_EXAMPLES 釘住，不混進「必須全過」的清單
  ['primary 文字 on canvas', '#E85BA9', '#150F0D', AA_TEXT],
  ['primary 文字 on surface', '#E85BA9', '#231916', AA_TEXT],
  ['primary 文字 on surface-2', '#E85BA9', '#31231F', AA_TEXT],
  ['primary 文字 on input', '#E85BA9', '#070504', AA_TEXT],

  // focus 外框：UI 元件門檻 3:1，非文字用途，對五層背景都要驗（focus 外框可能出現在任何底上）
  ['focus 外框 on canvas', '#E85BA9', '#150F0D', AA_UI],
  ['focus 外框 on surface', '#E85BA9', '#231916', AA_UI],
  ['focus 外框 on surface-2', '#E85BA9', '#31231F', AA_UI],
  ['focus 外框 on overlay', '#E85BA9', '#3F2D28', AA_UI],
  ['focus 外框 on input', '#E85BA9', '#070504', AA_UI],
]

/**
 * 反例／已知限制：文件用來論證「這個做法不行」的數字，或明文承認「這組沒過但接受」的限制。
 * 結論方向對的時候沒有東西會反彈，所以 E-31 才會讓錯的反例數字混過去——這裡把反例也釘住，
 * 數字漂移一樣會失敗。部分項目用範圍（[min, max]）而不是單一期望值，因為文件本身就是說
 * 「落在某個區間內都算沒有回歸」，不是要求小數點後精確吻合。
 */
const COUNTER_EXAMPLES = [
  // 歷史反例，v3 已無此色，純粹防止未來不小心又接回 Element Plus 預設藍
  ['白字套 Element Plus 預設藍（歷史反例，v3 已無此色）', '#FFFFFF', '#409EFF', 2.78],
  ['警告按鈕若用白字（§4.3 反例，過不了 AA 所以按鈕改深字）', '#FFFFFF', '#FFB84D', 1.72],
  // v3 新增：品牌色反例（§7.5：這是 --brand-aa／--brand-deep 存在的理由）
  ['白字套 --brand 正色 #E0218A（§7.5 反例）', '#FFFFFF', '#E0218A', 4.42],
  ['白字套 --brand-bright #E85BA9（§7.5 反例，更不夠）', '#FFFFFF', '#E85BA9', 3.23],
  // v3 新增：primary 當文字色對 overlay 是規格明文承認的「已知且接受的限制」（§7.5／§6.3），
  // 不列入必過清單，但要釘住數字不能悄悄變化——落在 3.9–4.1 之間都算沒有回歸
  ['primary 文字 on overlay（§7.5／§6.3 明文已知限制，非必過項目）', '#E85BA9', '#3F2D28', [3.9, 4.1]],
  // 本次修正的根因記錄：border-input 的暖化等亮度換算值對 overlay 過不了 3:1，
  // 這裡釘住舊值的實際數字，證明換成 #8A7E75 是必要的，不是隨意選的替代值
  [
    '（迴歸記錄）border-input 若用暖化等亮度換算值 #7A6F68 on overlay（過不了 3:1，因此改用 #8A7E75）',
    '#7A6F68',
    '#3F2D28',
    2.66,
  ],
]

// ── 檢查 ────────────────────────────────────────────────────────────────

const failures = []
const lines = []
let checkedCount = 0

// 1. 文字笛卡兒積：每階文字 × 每層背景
lines.push('【1】文字 × 背景 全組合（AA_TEXT 4.5:1，不挑，這是 E-31 的重點）')
for (const [tName, tHex] of Object.entries(TEXTS)) {
  for (const [bName, bHex] of Object.entries(BACKGROUNDS)) {
    const ratio = contrast(tHex, bHex)
    const pass = ratio >= AA_TEXT
    checkedCount++
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

// 2. 邊框笛卡兒積：邊框 token × 每層背景（這次新補的洞，門檻 3:1）
lines.push('')
lines.push('【2】邊框 × 背景 全組合（AA_UI 3:1，這次新補的洞——上一輪只驗了兩組，漏了 overlay）')
for (const [tName, tHex] of Object.entries(BORDERS_CARTESIAN)) {
  for (const [bName, bHex] of Object.entries(BACKGROUNDS)) {
    const ratio = contrast(tHex, bHex)
    const pass = ratio >= AA_UI
    checkedCount++
    if (!pass) {
      failures.push(
        `${tName} (${tHex}) on ${bName} (${bHex}) = ${ratio.toFixed(2)}:1，未達 AA_UI ${AA_UI}:1`,
      )
    }
    lines.push(
      `    ${pass ? '✓' : '✗'} ${tName.padEnd(24)} on ${bName.padEnd(22)} ${ratio.toFixed(2).padStart(6)}:1`,
    )
  }
}

// 3. 豁免 token：算出來但不強制過門檻，理由印出來
lines.push('')
lines.push('【3】豁免 token（不強制過門檻，但逐層算出數字＋理由，不是默默跳過）')
for (const [tName, { hex, reason }] of Object.entries(EXEMPT)) {
  for (const [bName, bHex] of Object.entries(BACKGROUNDS)) {
    const ratio = contrast(hex, bHex)
    checkedCount++
    lines.push(
      `    ~ ${tName.padEnd(24)} on ${bName.padEnd(22)} ${ratio.toFixed(2).padStart(6)}:1  （豁免：${reason}）`,
    )
  }
}

// 4. 成對色票
lines.push('')
lines.push('【4】成對色票（四態、語意色、按鈕、primary 文字四層、focus 外框五層）')
for (const [label, fg, bg, threshold] of PAIRS) {
  const ratio = contrast(fg, bg)
  const pass = ratio >= threshold
  checkedCount++
  if (!pass) {
    failures.push(`${label}：${fg} on ${bg} = ${ratio.toFixed(2)}:1，未達門檻 ${threshold}:1`)
  }
  lines.push(
    `    ${pass ? '✓' : '✗'} ${label.padEnd(46)} ${ratio.toFixed(2).padStart(6)}:1  (門檻 ${threshold})`,
  )
}

// 5. 反例／已知限制數字釘住
lines.push('')
lines.push('【5】反例與已知限制（文件用來論證「不能這樣做」或「這組已知不過」的數字，漂移就失敗）')
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
  if (!ok) {
    failures.push(`${label}：實算 ${ratio.toFixed(2)}:1，但文件記的是 ${expectedLabel}:1`)
  }
  lines.push(`    ${ok ? '✓' : '✗'} ${label.padEnd(60)} ${ratio.toFixed(2)}:1`)
}

// 6. 規格 vs 實作：admin-theme.css 的值要與上面的定義一致
lines.push('')
lines.push('【6】規格（docs/21 §7／§15）vs 實作（admin-theme.css）')
let css
try {
  css = readFileSync(THEME_CSS, 'utf8')
} catch {
  failures.push(`讀不到 ${THEME_CSS}`)
  css = null
}

if (css) {
  const exemptHexOnly = Object.fromEntries(
    Object.entries(EXEMPT).map(([k, v]) => [k, v.hex]),
  )
  const ALL_TOKENS = { ...BACKGROUNDS, ...TEXTS, ...exemptHexOnly, ...PRIMARY, ...BORDERS }
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
      failures.push(`${token}：admin-theme.css 是 ${actual}，docs/21 §7／§15 是 ${expected}`)
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
    '\n色票的真實來源是 docs/21-admin-ui.md §7／§4／§15。' +
      '\n要改色值請先改那份文件，再同步本腳本與 admin-theme.css。',
  )
  process.exit(1)
}

console.log(
  `✓ 色票對比度檢查通過（共 ${checkedCount} 組組合：文字×背景笛卡兒積、邊框×背景笛卡兒積、` +
    `豁免 token 記錄、成對色票、反例釘住 ＋ token 值與 docs/21 §7／§15 一致）`,
)
