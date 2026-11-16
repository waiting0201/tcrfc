#!/usr/bin/env node
/**
 * check-visitor-copy.mjs — 會輸出給訪客或搜尋引擎的字串，不得出現內部口吻（2026-11-13）。
 *
 * 內部口吻＝「待客戶提供」「客戶尚未確認」「規劃書」「由後台提供」「待肖像同意到位」「mockup」這類只有團隊內部看得懂、
 * 對訪客沒有意義甚至會誤導的說法。空狀態要寫成對訪客說的話（例如「…整理中，稍後將於本頁公布。」），
 * SEO description 要描述這一頁是什麼，不得提到資料狀態。
 *
 * 範圍刻意收窄、規則刻意少而準（誤判率高的檢查會變成擺設）：
 *   - 只掃 app／shared／server 的 .vue、.ts，**先剝掉註解再掃**：
 *     .ts 與 .vue 的 <script>／<style> 用小型 tokenizer 剝 // 與 /* *\/（認得字串與樣板字串，不會把 https:// 當註解）；
 *     .vue 的 <template> 剝 <!-- -->。程式註解、HTML 註解一律不算。
 *   - 規則只比對「字面內部詞」，不做語意判斷。
 *   - 例外（檔案層）：app/pages/zh/preview/ 是編輯者用的預覽頁（noindex、憑 token 進入），文案本來就對後台人員說話。
 *   - 例外（行層）：丟給開發者的訊息不算訪客文案——`console.*`、`throw new Error(`、`createError({ ... statusMessage` 之外的 Error 訊息
 *     若含內部詞，請改寫而非加例外；目前只放行 `console.` 與 `throw new Error(`。
 *   - 刻意不擋：同意書／健康聲明旁的「pending legal review（待法務定稿）」——那是法遵揭露，是否改寫由產品決定，不是文案潤飾。
 */
import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs'
import { join, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('..', import.meta.url))
const SCAN = ['app', 'shared', 'server']
const EXT = /\.(vue|ts)$/
const FILE_EXEMPT = ['app/pages/zh/preview/']
const LINE_EXEMPT = /console\.(log|warn|error|info|debug)\(|throw new Error\(/

/**
 * 已知待處理（暫時放行，**不是長期例外**）：這幾句同時是 `db/seed/page_seed_content.json` 的種子來源
 * （`check-page-seed-sync` 逐位元比對），改前台文案就要重跑 extract 並由後端重產 db/prod 種子與 manifest，
 * 不在前台單獨可完成的範圍（2026-11-13）。種子重產後請改文案並**刪掉這個清單**。
 */
const KNOWN_PENDING = [
  '已核實、取得肖像同意的球員故事案例將陸續建立於本頁',
  '目前尚無已核實、取得肖像同意的球員故事案例',
  'Verified player stories, with image consent obtained',
  'There are no verified player stories with image consent yet',
]

/** [pattern, 說明] */
const RULES = [
  [/待客戶|客戶尚未|客戶提供|客戶確認|客戶收件夾|客戶資料|尚待客戶/, '提到「客戶」的內部口吻'],
  [/\b(?:the|by the|from the) client\b|\bclient has not\b|to be provided by the client/i, 'Internal wording about the client'],
  [/規劃書|mockup|Mockup|TODO|FIXME|lorem ipsum/, '規劃書／mockup／TODO 等內部詞'],
  [/佔位|占位/, '「佔位」類內部詞'],
  [/後台/, '提到「後台」（訪客看不到、也不需要知道後台）'],
  [/\bback[- ]?office\b/i, 'Mentions the back office (visitors do not see it)'],
  [/肖像同意|image consents?\b/, '提到肖像同意狀態（內部作業進度）'],
  [/尚未開發|正式上線(?:時|前)|待確認後回頭|目前為空狀態/, '開發進度或空狀態等內部詞'],
  [/\bdocs\/\d|STATUS\.md/, '指向內部文件'],
]

function walk(p, out = []) {
  const full = join(ROOT, p)
  if (!existsSync(full)) return out
  const st = statSync(full)
  if (st.isFile()) { if (EXT.test(p)) out.push(p); return out }
  for (const n of readdirSync(full)) {
    if (n === 'node_modules' || n === '.nuxt' || n === '.output') continue
    walk(join(p, n), out)
  }
  return out
}

/** 剝掉 JS／TS 註解，保留換行（行號不變）。認得 '、"、` 與 ${ } 巢狀。 */
function stripJsComments(src) {
  let out = ''
  let i = 0
  const n = src.length
  // 狀態堆疊：'code' 與 'tpl'；每個 'code' 帶一個 brace 計數（只用於 ${ } 內）
  const stack = [{ k: 'code', depth: 0 }]
  while (i < n) {
    const top = stack[stack.length - 1]
    const c = src[i]
    const d = src[i + 1]
    if (top.k === 'code') {
      if (c === '/' && d === '/') {
        while (i < n && src[i] !== '\n') i++
        continue
      }
      if (c === '/' && d === '*') {
        i += 2
        while (i < n && !(src[i] === '*' && src[i + 1] === '/')) { if (src[i] === '\n') out += '\n'; i++ }
        i += 2
        continue
      }
      if (c === '\'' || c === '"') {
        const q = c
        out += c; i++
        while (i < n && src[i] !== q && src[i] !== '\n') {
          if (src[i] === '\\') { out += src[i++] }
          out += src[i++]
        }
        if (i < n) out += src[i++]
        continue
      }
      if (c === '`') { stack.push({ k: 'tpl' }); out += c; i++; continue }
      if (c === '{' && stack.length > 1) top.depth++
      if (c === '}' && stack.length > 1) {
        if (top.depth === 0) { stack.pop(); out += c; i++; continue }
        top.depth--
      }
      out += c; i++
      continue
    }
    // 樣板字串內
    if (c === '\\') { out += c + (d ?? ''); i += 2; continue }
    if (c === '`') { stack.pop(); out += c; i++; continue }
    if (c === '$' && d === '{') { stack.push({ k: 'code', depth: 0 }); out += '${'; i += 2; continue }
    out += c; i++
  }
  return out
}

function stripCssComments(src) {
  return src.replace(/\/\*[\s\S]*?\*\//g, (m) => m.replace(/[^\n]/g, ''))
}

function stripHtmlComments(src) {
  return src.replace(/<!--[\s\S]*?-->/g, (m) => m.replace(/[^\n]/g, ''))
}

/** 回傳去除註解後的全文（行號與原檔一致）。 */
function stripComments(file, src) {
  if (file.endsWith('.ts')) return stripJsComments(src)
  // .vue：<script>／<style> 各自處理，其餘（template）剝 HTML 註解
  let out = ''
  let last = 0
  const re = /<(script|style)\b[^>]*>([\s\S]*?)<\/\1>/g
  let m
  while ((m = re.exec(src))) {
    const bodyStart = m.index + m[0].indexOf('>') + 1
    out += stripHtmlComments(src.slice(last, bodyStart))
    const body = m[2]
    out += m[1] === 'script' ? stripJsComments(body) : stripCssComments(body)
    last = bodyStart + body.length
  }
  out += stripHtmlComments(src.slice(last))
  return out
}

const files = SCAN.flatMap((p) => walk(p)).filter((f) => !FILE_EXEMPT.some((e) => f.split(sep).join('/').includes(e)))
const problems = []
let pendingSkipped = 0
for (const f of files) {
  const text = stripComments(f, readFileSync(join(ROOT, f), 'utf8'))
  text.split('\n').forEach((line, idx) => {
    if (LINE_EXEMPT.test(line)) return
    if (KNOWN_PENDING.some((t) => line.includes(t))) { pendingSkipped++; return }
    for (const [re, why] of RULES) {
      const m = re.exec(line)
      if (m) problems.push(`${f}:${idx + 1}  [${why}]  「${m[0]}」  ${line.trim().slice(0, 120)}`)
    }
  })
}

if (problems.length > 0) {
  console.error(`check-visitor-copy: 發現 ${problems.length} 處會輸出給訪客或搜尋引擎的內部口吻（程式註解不算）：`)
  for (const p of problems) console.error('  ' + p)
  console.error('\n改成對訪客說的話（例：「…整理中，稍後將於本頁公布。」）；SEO description 描述這一頁是什麼；沒有合適說法的區塊寧可不顯示。')
  process.exit(1)
}
console.log(`check-visitor-copy: OK（掃描 ${files.length} 個檔案，${RULES.length} 條規則；已剝除程式與 HTML 註解；暫時放行種子連動文案 ${pendingSkipped} 處，見 KNOWN_PENDING）`)
