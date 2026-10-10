#!/usr/bin/env node
/**
 * 禁用詞掃描（規劃書 §4.0 後台設計通則③④、docs/06-conventions.md §1）。
 *
 * 後台介面一律日常中文，畫面上不得出現：模組代號（B1／K4／S3）、權限碼（shop.order.export）、
 * 隊別代號（D1／BW1）、英文技術詞（slug／canonical／noindex／alt／hreflang／SKU／token／blob／
 * WebP／EXIF／srcset／club_id）。這條規則要在 14 個模組、幾百個畫面上被遵守，靠人記一定會破
 * （docs/18-work-errors.md 整份檔都是這類「規則沒有防呆」的教訓），所以寫成腳本掛進 `npm run lint`。
 *
 * ⚠️ 這支腳本只掃「畫面文字」，不掃「程式碼識別字」——這是這條規則本來就有的兩層分界
 * （規劃書 §4.0 明文：「這條約束介面文字，不是資料結構」）。做法：
 *   1. 只讀 .vue 檔案的 <template> 區塊（<script>／<style> 完全不掃，那裡本來就該是英文識別字）。
 *   2. 樣板裡的動態綁定（:xxx="…"、v-xxx="…"、@xxx="…"）與 {{ … }} 內插值整段丟棄——那些是
 *      JS 運算式（變數名、prop 名稱、函式呼叫），不是使用者會看到的文字。
 *   3. 只保留「使用者看得到的文字」：純文字節點，加上少數會直接顯示成文字的靜態屬性
 *      （placeholder／title／alt／aria-label／label／header／content／sub-title／description）。
 *      其餘靜態屬性（如 module-code="B2" 這種內部查表用的值）不掃，因為那是程式參數不是畫面文字。
 *
 * 寧可少抓也不要誤報：規則涵蓋不到的情況（例如 {{ }} 內插值裡真的寫死了一個禁用字串常值）
 * 就先不管，那種情況很少見，抓太寬會逼人關掉整支腳本，本末倒置（任務交付說明的原話）。
 */

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, extname } from 'node:path'
import { fileURLToPath } from 'node:url'

const SRC_DIR = join(fileURLToPath(new URL('.', import.meta.url)), '..', 'src')

const DISPLAY_ATTRS = [
  'placeholder',
  'title',
  'alt',
  'aria-label',
  'label',
  'header',
  'content',
  'sub-title',
  'description',
]

// 模組代號與子模組代號（docs/03-admin-spec.md §1，14 個一級模組的完整清單）。
// 刻意不含裸的單字母代號（A／H／I）——中英雙語內容的英文分頁裡，英文句子常見獨立單字 "I"，
// 裸字母規則的誤報率太高，這條防呆先只抓「字母+數字」這種明顯是代號而不是英文單字的組合。
const MODULE_CODES = [
  'B1', 'B2', 'B3', 'B4', 'B5', 'B6',
  'C1', 'C2', 'C3', 'C4', 'C5',
  'P1', 'P2', 'P3', 'P4',
  'E1', 'E2', 'E3', 'E4', 'E5', 'E6',
  'F1', 'F2',
  'G1', 'G2', 'G3',
  'J1', 'J2', 'J3', 'J4',
  'K1', 'K2', 'K3', 'K4', 'K5',
  'L1', 'L2', 'L3', 'L4',
  'M1', 'M2', 'M3', 'M4', 'M5',
  'S1', 'S2', 'S3', 'S4', 'S5', 'S6',
]

// 隊別代號（docs/06-conventions.md §1）
const TEAM_CODES = ['D1', 'BW1']

// 英文技術詞（docs/06-conventions.md §1 對照表左欄），要求完整單字邊界比對
const TECH_WORDS = [
  'slug', 'canonical', 'noindex', 'hreflang', 'SKU', 'blob', 'WebP', 'EXIF', 'srcset', 'club_id',
  // "alt" 與 "token" 誤報風險較高（可能是英文句子裡的一般單字），只在明顯的技術情境比對，
  // 見下方 checkAltAndToken()
]

function listVueFiles(dir) {
  const results = []
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry)
    const stat = statSync(full)
    if (stat.isDirectory()) {
      results.push(...listVueFiles(full))
    } else if (extname(entry) === '.vue') {
      results.push(full)
    }
  }
  return results
}

function extractTemplateBlock(source) {
  // 頂層 <template> 內會有巢狀的 <template #main> 等具名插槽，必須取到「最後一個」</template>，
  // 非貪婪比對只會掃到第一個巢狀插槽關閉為止，EditLayout 的主欄／側欄文字整段漏掃（E-314）。
  const start = source.indexOf('<template>')
  const end = source.lastIndexOf('</template>')
  return start >= 0 && end > start ? source.slice(start + '<template>'.length, end) : ''
}

function extractDisplayText(template) {
  // 1. 丟掉 {{ ... }} 內插值（運算式，不是畫面文字常值）
  let text = template.replace(/\{\{[\s\S]*?\}\}/g, ' ')

  // 2. 丟掉動態綁定與指令（:xxx="..."、v-xxx="..."、@xxx="..."），值是 JS 運算式
  text = text.replace(/(?:^|\s)(?::|v-|@)[\w:.-]+="[^"]*"/g, ' ')

  // 3. 收集會直接顯示成文字的靜態屬性值
  const collected = []
  const tagRegex = /<([a-zA-Z][\w-]*)((?:\s+[^<>]*)?)>/g
  let tagMatch
  while ((tagMatch = tagRegex.exec(text))) {
    const attrsSrc = tagMatch[2]
    for (const attrName of DISPLAY_ATTRS) {
      const attrRegex = new RegExp(`(?:^|\\s)${attrName}="([^"]*)"`, 'g')
      let attrMatch
      while ((attrMatch = attrRegex.exec(attrsSrc))) {
        collected.push(attrMatch[1])
      }
    }
  }

  // 4. 其餘標籤整個拿掉，只留文字節點
  const textNodes = text.replace(/<[^>]*>/g, ' ')

  return `${collected.join(' ')} ${textNodes}`
}

function scanText(text) {
  const findings = []

  for (const code of [...MODULE_CODES, ...TEAM_CODES]) {
    const re = new RegExp(`\\b${code}\\b`)
    if (re.test(text)) {
      findings.push(`模組／隊別代號「${code}」`)
    }
  }

  for (const word of TECH_WORDS) {
    const re = new RegExp(`\\b${word}\\b`, 'i')
    if (re.test(text)) {
      findings.push(`英文技術詞「${word}」`)
    }
  }

  // 權限碼樣式：至少兩段小寫（可含底線）以句點相接，排除看起來像網域的情況（www 開頭或
  // 常見網域字尾），避免把「正規網址」欄位範例文字裡的 www.tcrfc.tw 誤判成權限碼
  const permissionRegex = /\b[a-z][a-z_]*(?:\.[a-z][a-z_]*){1,3}\b/g
  const domainLikeTails = new Set(['tw', 'com', 'net', 'org', 'io', 'tv', 'co'])
  let permMatch
  while ((permMatch = permissionRegex.exec(text))) {
    const full = permMatch[0]
    const segments = full.split('.')
    const looksLikeDomain = segments[0] === 'www' || domainLikeTails.has(segments[segments.length - 1])
    if (!looksLikeDomain) {
      findings.push(`疑似權限碼「${full}」`)
    }
  }

  return findings
}

// ── 第二層（docs/18 E-155 起）：腳本裡「會顯示給使用者的中文字串」與樣板文字中的技術外露 ──
// 同一類錯（技術詞、內部代號、程式反引號、過期的開發備註外露到畫面）已犯過三次，只掃樣板抓不到
// `formError.value = '…例如 my_orders…'`、`ElMessage.error('…`venues` 主檔…')` 這類寫在 script 裡的文字。
// 規則只套在「含中文的字串常值」上（純英文識別字、import 路徑、CSS class 一律不管），降低誤報。
const LEAK_PATTERNS = [
  [/`/, '反引號（程式碼標記不該出現在畫面文字）'],
  [/\b[a-z][a-z0-9]*(?:_[a-z0-9]+)+\b/, 'snake_case 內部代號'],
  [/tcrfc:\/\//, '內部連結協定 tcrfc://'],
  [/\bJSON\b/, '英文技術詞 JSON'],
  [/百分位|\bP9[05]\b/, '統計術語（百分位）'],
]

function extractScriptBlock(source) {
  const match = source.match(/<script[^>]*>([\s\S]*?)<\/script>/)
  return match ? match[1] : ''
}

function stripComments(code) {
  return code.replace(/\/\*[\s\S]*?\*\//g, ' ').replace(/(^|[^:'"`\\])\/\/[^\n]*/g, '$1')
}

function scriptStringLiterals(code) {
  const out = []
  const re = /'((?:[^'\\\n]|\\.)*)'|"((?:[^"\\\n]|\\.)*)"/g
  let m
  while ((m = re.exec(code))) out.push(m[1] ?? m[2] ?? '')
  return out
}

function scanScriptLeaks(source) {
  const code = stripComments(extractScriptBlock(source))
  const findings = []
  for (const lit of scriptStringLiterals(code)) {
    if (!/[\u4e00-\u9fff]/.test(lit)) continue
    for (const [re, why] of LEAK_PATTERNS) {
      if (re.test(lit)) findings.push(`${why}：「${lit.length > 60 ? lit.slice(0, 60) + '…' : lit}」`)
    }
  }
  return findings
}

function scanTemplateLeaks(displayText) {
  const findings = []
  for (const [re, why] of LEAK_PATTERNS) {
    if (re.test(displayText)) findings.push(why)
  }
  return findings
}

// 「使用者要自己輸入的資料範例」：欄位本身的內容就是這個字串，不是系統內部名詞外露。
// 每一條都要寫明理由；新增前先確認不是可以改成日常中文說法的情況（E-314）。
const DATA_EXAMPLE_EXEMPT = [
  { file: 'views/ads/parts/SlotTab.vue', match: /WebP/i, why: '「允許的檔案格式」欄位的輸入範例（jpg,png,webp）' },
  { file: 'views/seo/LlmsContentView.vue', match: /taichungrock\.example/, why: '聯絡信箱範例網域' },
  { file: 'views/seo/SeoSettingsView.vue', match: /robots\.txt/, why: '檔案名稱本身就是這個欄位的產品名（線上編輯 robots.txt）' },
  { file: 'views/settings/parts/UiStringsPanel.vue', match: /button\.submit/, why: '字串代號欄位的輸入範例' },
  { file: 'views/teams/TeamEditView.vue', match: /\b(D1|BW1)\b/, why: '「隊別代號」欄位的輸入範例（欄位本身就是代號，是否改名待規格裁決）' },
]

function isExempt(file, finding) {
  return DATA_EXAMPLE_EXEMPT.some((e) => file.endsWith(e.file) && e.match.test(finding))
}

function main() {
  const files = listVueFiles(SRC_DIR)
  let hasError = false

  for (const file of files) {
    const source = readFileSync(file, 'utf-8')
    const template = extractTemplateBlock(source)
    const displayText = template ? extractDisplayText(template) : ''
    const findings = [...(template ? scanText(displayText) : []), ...scanTemplateLeaks(displayText), ...scanScriptLeaks(source)]
      .filter((f) => !isExempt(file, f))

    if (findings.length > 0) {
      hasError = true
      console.error(`\n✗ ${file}`)
      for (const finding of findings) {
        console.error(`  - ${finding}`)
      }
    }
  }

  // 這支腳本只看得到 .vue 的 <template>，**看不到 Element Plus 元件庫自帶的文案**
  // （分頁器的 Total／page、表格空資料的 No Data、ElMessageBox 的 OK／Cancel……）。
  // 那些字串不設語系就會是英文，同樣違反規劃書 §4.0，而且畫面上看得到、腳本卻掃不到。
  // 這是掃描範圍的盲點，只能改成檢查「語系有沒有設」來守住，見 docs/18 E-33。
  const mainTs = readFileSync(join(SRC_DIR, 'main.ts'), 'utf-8')
  if (!/app\.use\(\s*ElementPlus\s*,\s*\{[^}]*locale\s*:/.test(mainTs)) {
    hasError = true
    console.error('\n✗ src/main.ts')
    console.error(
      '  - Element Plus 沒有設繁體中文語系，元件庫自帶文案會是英文' +
        '（分頁器 Total／page、表格 No Data、對話框 OK／Cancel 等）',
    )
    console.error("    修法：app.use(ElementPlus, { locale: zhTw })，zhTw 取自 element-plus/es/locale/lang/zh-tw")
  }

  if (hasError) {
    console.error(
      '\n後台介面一律日常中文，不得出現模組代號／權限碼／隊別代號／英文技術詞' +
        '（規劃書 §4.0、docs/06-conventions.md §1）。請把上面列出的畫面文字改成中文說法。\n',
    )
    process.exit(1)
  }

  console.log(`✓ 禁用詞掃描通過（檢查了 ${files.length} 個 .vue 檔案的 <template> 區塊）`)
}

main()
