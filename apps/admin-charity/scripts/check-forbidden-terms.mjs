#!/usr/bin/env node
/**
 * 禁用詞掃描（規劃書 §4.0 後台設計通則③④、docs/06-conventions.md §1，比照 apps/admin 的做法
 * 搬過來，字典換成慈善後台的：N1–N7 模組代號，不是俱樂部的 B1／K4／S3；沒有隊別代號可掃
 * ——慈善庫沒有 club_id、沒有球隊維度）。
 *
 * ⚠️ 這支腳本只掃「畫面文字」，不掃「程式碼識別字」——只讀 .vue 檔案的 <template> 區塊，
 * 動態綁定與 {{ }} 內插值整段丟棄，只保留文字節點與少數會直接顯示成文字的靜態屬性。
 *
 * ⚠️ E-33（docs/18-work-errors.md）：這支腳本看不到 Element Plus 元件庫自帶的文案
 * （分頁器 Total／page、表格 No Data、對話框 OK／Cancel……），所以額外檢查 main.ts
 * 有沒有設語系——語系沒設就讓 lint 失敗，不是默默漏掉。
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

// 慈善後台的一級模組代號：N1–N7（docs/10-charity-donation-site.md §3），不是俱樂部的 B1／K4 等
const MODULE_CODES = ['N1', 'N2', 'N3', 'N4', 'N5', 'N6', 'N7']

// 英文技術詞（docs/06-conventions.md §1 對照表左欄），要求完整單字邊界比對
const TECH_WORDS = ['slug', 'canonical', 'noindex', 'hreflang', 'SKU', 'blob', 'WebP', 'EXIF', 'srcset', 'club_id']

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
  const match = source.match(/<template>([\s\S]*?)<\/template>/)
  return match ? match[1] : ''
}

function extractDisplayText(template) {
  let text = template.replace(/\{\{[\s\S]*?\}\}/g, ' ')
  text = text.replace(/(?:^|\s)(?::|v-|@)[\w:.-]+="[^"]*"/g, ' ')

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

  const textNodes = text.replace(/<[^>]*>/g, ' ')
  return `${collected.join(' ')} ${textNodes}`
}

function scanText(text) {
  const findings = []

  for (const code of MODULE_CODES) {
    const re = new RegExp(`\\b${code}\\b`)
    if (re.test(text)) {
      findings.push(`模組代號「${code}」`)
    }
  }

  for (const word of TECH_WORDS) {
    const re = new RegExp(`\\b${word}\\b`, 'i')
    if (re.test(text)) {
      findings.push(`英文技術詞「${word}」`)
    }
  }

  // 權限碼樣式：n1.donation_store.view 這種以句點相接的小寫片段，排除看起來像網域的情況
  // （local／test 是本專案測試帳號 Email 慣用的網域，如 sa@charity.local、donor01@example.test，
  // 畫面上的帳號範例文字會出現這些字，不是權限碼）
  const permissionRegex = /\b[a-z][a-z_]*(?:\.[a-z][a-z_]*){1,3}\b/g
  const domainLikeTails = new Set(['tw', 'com', 'net', 'org', 'io', 'tv', 'co', 'local', 'test'])
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

function main() {
  const files = listVueFiles(SRC_DIR)
  let hasError = false

  for (const file of files) {
    const source = readFileSync(file, 'utf-8')
    const template = extractTemplateBlock(source)
    if (!template) continue

    const displayText = extractDisplayText(template)
    const findings = scanText(displayText)

    if (findings.length > 0) {
      hasError = true
      console.error(`\n✗ ${file}`)
      for (const finding of findings) {
        console.error(`  - ${finding}`)
      }
    }
  }

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
      '\n後台介面一律日常中文，不得出現模組代號／權限碼／英文技術詞' +
        '（規劃書 §4.0、docs/06-conventions.md §1）。請把上面列出的畫面文字改成中文說法。\n',
    )
    process.exit(1)
  }

  console.log(`✓ 禁用詞掃描通過（檢查了 ${files.length} 個 .vue 檔案的 <template> 區塊）`)
}

main()
