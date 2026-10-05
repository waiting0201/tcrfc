#!/usr/bin/env node
/**
 * check-en-copy.mjs — 主站英文版文案檔的兩條硬規則（C-6／S2-13）。
 *
 * 檔案不存在就略過（英文文案檔由多個群組分批建立）；存在就檢查：
 *
 * (a) `shared/utils/club-copy-en-*.ts` 與 `shared/utils/units-en.ts`：剝除註解後，
 *     字串字面值（單引號、雙引號、範本字面值）不得含中日文字元。英文版不得混入中文——
 *     中文漏進去不會有任何建置錯誤，只會在 /en/ 頁面上看到一句中文（docs/06 §1.1）。
 *
 *     例外：物件鍵（`{ '日本': 'Japan' }`）是中文→英文查表的輸入，允許含中文。
 *
 * (b) 配對檢查（預設為警告，`--strict` 為失敗；原因見 `warn` 的註解）：每個匯出的 `FOO_EN`（常數）／`getFooEn`（函式）／`fooEn`（函式）必須在
 *     `shared/utils/club-copy.ts` 找得到匯出的 `FOO`／`getFoo`／`foo`。英文版是中文版的孿生，
 *     沒有中文孿生代表要嘛命名打錯、要嘛中文版根本沒這項內容（英文版不得引入規劃書沒有的新文案）。
 *     沒有中文孿生又合理的共用項，列在下方 NO_ZH_TWIN 並寫明理由。
 *     `getXxxEn` 與 `xxxEn` 兩種寫法都接受配對到 `getXxx`／`xxx`。
 */
import { readFileSync, readdirSync, existsSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const HERE = dirname(fileURLToPath(import.meta.url))
const UTILS = resolve(HERE, '../shared/utils')
const ZH_SOURCE = resolve(UTILS, 'club-copy.ts')

/** 沒有中文孿生、但屬於英文版自己的合法共用項（名稱 → 理由）。 */
const NO_ZH_TWIN = new Map([
  ['CLUB_NAME_EN', '英文版俱樂部名稱常數；中文版名稱由 `ClubAssets.nameZh` 提供，不在 club-copy.ts'],
  ['getUnitLabelEn', '單元英文標籤；中文孿生是 site-units.ts 的 `getUnitLabelZh`，不在 club-copy.ts'],
])

const CJK = /[　-〿㐀-䶿一-鿿豈-﫿＀-￯぀-ヿ]/

const errors = []
const warnings = []
const fail = (msg) => errors.push(msg)
// 配對檢查預設只警告：scheduling／news／shop／club／academy 群組的中文文案原本內嵌在頁面樣板，
// 沒有抽成 club-copy.ts 的具名匯出，硬性失敗會擋住整個英文版。`--strict` 時警告升級為失敗。
const STRICT = process.argv.includes('--strict')
const warn = (msg) => (STRICT ? errors : warnings).push(msg)

/** 剝除註解，但保留字串內容（字串裡的 `//` 如網址不能被當成註解）。回傳 { code, strings }。 */
function scan(src) {
  let code = ''
  const strings = []
  let i = 0
  const lineOf = (idx) => src.slice(0, idx).split('\n').length
  while (i < src.length) {
    const c = src[i]
    if (c === '/' && src[i + 1] === '/') {
      const end = src.indexOf('\n', i)
      i = end < 0 ? src.length : end
      continue
    }
    if (c === '/' && src[i + 1] === '*') {
      const end = src.indexOf('*/', i + 2)
      i = end < 0 ? src.length : end + 2
      continue
    }
    if (c === '"' || c === "'" || c === '`') {
      const start = i
      let j = i + 1
      while (j < src.length) {
        if (src[j] === '\\') { j += 2; continue }
        if (src[j] === c) break
        j++
      }
      // 物件鍵（`{ '日本': 'Japan' }` 這類中文→英文查表的鍵）允許含中文：前一個非空白字元是 `{` 或 `,`，
      // 且字串結尾後第一個非空白字元是 `:`。值與一般字串仍不得含中文。
      const before = src.slice(0, start).trimEnd().slice(-1)
      const after = src.slice(j + 1).trimStart()[0]
      const isKey = (before === '{' || before === ',') && after === ':'
      strings.push({ text: src.slice(start + 1, j), line: lineOf(start), isKey })
      code += '""'
      i = j + 1
      continue
    }
    code += c
    i++
  }
  return { code, strings }
}

function exportedNames(code) {
  const names = new Set()
  for (const m of code.matchAll(/export\s+(?:async\s+)?(?:const|function|let)\s+([A-Za-z_$][\w$]*)/g)) names.add(m[1])
  return names
}

// ── 目標檔案 ───────────────────────────────────────────────────────────────
const targets = []
for (const f of readdirSync(UTILS)) {
  if (/^club-copy-en-.+\.ts$/.test(f) || f === 'units-en.ts') targets.push(f)
}
targets.sort()

if (targets.length === 0) {
  console.log('check-en-copy: 沒有英文文案檔，略過')
  process.exit(0)
}

// ── (a) 字串字面值不得含中日文字元 ─────────────────────────────────────────
const exportsByFile = new Map()
for (const f of targets) {
  const { code, strings } = scan(readFileSync(resolve(UTILS, f), 'utf8'))
  for (const s of strings) {
    if (!s.isKey && CJK.test(s.text)) fail(`${f}:${s.line} 字串含中日文字元：「${s.text.slice(0, 40)}」`)
  }
  exportsByFile.set(f, exportedNames(code))
}

// ── (b) FOO_EN／getFooEn／fooEn 必須在 club-copy.ts 有 FOO／getFoo／foo ──────
if (!existsSync(ZH_SOURCE)) {
  fail('找不到 shared/utils/club-copy.ts，無法做配對檢查')
}
else {
  const zhNames = exportedNames(scan(readFileSync(ZH_SOURCE, 'utf8')).code)
  for (const [f, names] of exportsByFile) {
    for (const name of names) {
      let twin = null
      if (/^[A-Z][A-Z0-9_]*_EN$/.test(name)) twin = [name.replace(/_EN$/, '')]
      else if (/^[a-z][A-Za-z0-9]*En$/.test(name)) {
        const base = name.replace(/En$/, '')
        twin = [base]
        // `getFooEn` → `getFoo`；`fooEn` → `foo`
      }
      if (!twin) continue // 型別、介面、內部常數等不是 `_EN`／`En` 結尾的匯出不檢查
      if (NO_ZH_TWIN.has(name)) continue
      if (!twin.some((t) => zhNames.has(t))) {
        warn(`${f}：匯出 \`${name}\` 在 club-copy.ts 找不到對應的 \`${twin[0]}\`（命名打錯？或中文版沒有這項內容？共用項請列入 NO_ZH_TWIN 並寫明理由）`)
      }
    }
  }
}

if (errors.length > 0) {
  console.error(`check-en-copy: ${errors.length} 項失敗`)
  for (const e of errors) console.error(`  ✗ ${e}`)
  process.exit(1)
}
if (warnings.length > 0) {
  console.warn(`check-en-copy: ${warnings.length} 項警告（配對檢查，--strict 時為失敗）`)
  for (const w of warnings) console.warn(`  ! ${w}`)
}
console.log(`check-en-copy: OK（${targets.length} 個檔案：${targets.join('、')}）`)
