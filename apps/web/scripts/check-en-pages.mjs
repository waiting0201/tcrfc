#!/usr/bin/env node
/**
 * check-en-pages.mjs — 英文版實機掃描：宣告 `enReady: true` 的頁面，/en/ 渲染出來的可見文字
 * 不得含中日文字元（C-6／S2-13，2026-10-05）。
 *
 * 為什麼是實機掃描而不是靜態 lint：英文分支寫在樣板的 `v-if="isEn"`／`tx()` 裡，靜態掃描分不出
 * 「這段中文是 zh 分支（正常）」還是「漏翻」。只有真的以 /en/ 渲染、把 HTML 剝成可見文字，才知道使用者看到什麼。
 * 因此這支腳本**需要一個正在跑的 tcrfc 前台**（`NUXT_PUBLIC_CLUB=tcrfc node .output/server/index.mjs`，後端接假 API
 * `scripts/dev-fixture-api.mjs` 或使用者啟動的真 API），不掛進 `npm run lint`（lint 不起伺服器）。
 *
 * 用法：node scripts/check-en-pages.mjs [BASE_URL=http://127.0.0.1:3000] [--all]
 *   預設只掃「頁面檔含 enReady: true」的路由；--all 掃全部靜態路由（找哪些頁還沒翻）。
 *   動態路由（含 [param]）略過，另以實際網址手動抽查。
 *
 * 容許的中文：`lang="zh-Hant"` 元素（備援提示的中文那行、語言切換器的「繁中」）、<script>／<style>／JSON-LD 不看、
 * 以及被 `data-en-allow-zh` 標記的元素（API 備援內容容器，頁面自行標記）。
 * 輸出：每頁殘留的中文片段（前 5 段），exit 1 表示有殘留。
 */
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const HERE = dirname(fileURLToPath(import.meta.url))
const PAGES = resolve(HERE, '../app/pages/zh')
const args = process.argv.slice(2)
const ALL = args.includes('--all')
// 藍鯨站（NUXT_PUBLIC_CLUB=bw）看 `enReadyBw` 旗標：node scripts/check-en-pages.mjs http://127.0.0.1:PORT --bw
const FLAG = args.includes('--bw') ? 'enReadyBw' : 'enReady'
const BASE = (args.find((a) => a.startsWith('http')) || 'http://127.0.0.1:3000').replace(/\/$/, '')

function walk(dir) {
  const out = []
  for (const name of readdirSync(dir)) {
    const p = join(dir, name)
    if (statSync(p).isDirectory()) out.push(...walk(p))
    else if (p.endsWith('.vue')) out.push(p)
  }
  return out
}

function routeOf(file) {
  let rel = relative(PAGES, file).replace(/\.vue$/, '').replace(/\/index$/, '').replace(/^index$/, '')
  if (rel.includes('[')) return null
  return `/en/${rel}${rel ? '/' : ''}`
}

const CJK = /[぀-ヿ㐀-䶿一-鿿＀-￯　-〿]/

function visibleText(html) {
  let s = html
    .replace(/<script[\s\S]*?<\/script>/gi, ' ')
    .replace(/<style[\s\S]*?<\/style>/gi, ' ')
    .replace(/<!--[\s\S]*?-->/g, ' ')
  // 移除容許的元素（簡化：不巢狀的 span／p／div 帶 lang="zh-Hant" 或 data-en-allow-zh）
  s = s.replace(/<(span|p|div|a|li|b|strong|em)\b[^>]*\b(?:lang="zh-Hant"|data-en-allow-zh)[^>]*>[\s\S]*?<\/\1>/gi, ' ')
  // 屬性裡的可見文字（alt、aria-label、title、placeholder、value 按鈕）也要檢查
  const attrs = [...s.matchAll(/\b(?:alt|aria-label|title|placeholder)="([^"]*)"/g)].map((m) => m[1])
  const text = s.replace(/<[^>]+>/g, ' ')
  return [text, ...attrs].join(' \n ')
}

// 已知、刻意保留的中文（回報時逐項說明）：法律同意／健康聲明（待法務，不自寫英文）、中文地址（無英文欄位）。
// 其餘（人名、藍鯨中文名、輪播指示器）不在此豁免，掃到就是待處理或待確認事項。
const KNOWN_ZH = [/隱私權政策/, /健康聲明|健康狀況/, /崇平路/, /^台中市.{2,12}[路街].*號$/]

const routes = []
for (const f of walk(PAGES)) {
  const src = readFileSync(f, 'utf8')
  const ready = new RegExp(`${FLAG}\\s*:\\s*true`).test(src)
  if (!ALL && !ready) continue
  const r = routeOf(f)
  if (r) routes.push({ route: r, ready })
}
routes.sort((a, b) => a.route.localeCompare(b.route))

let bad = 0
for (const { route, ready } of routes) {
  let html
  try {
    const res = await fetch(BASE + route, { redirect: 'manual' })
    if (res.status >= 300) {
      console.log(`?  ${route}  HTTP ${res.status}`)
      continue
    }
    html = await res.text()
  } catch (e) {
    console.error(`無法連線 ${BASE}${route}：${e.message}`)
    process.exit(2)
  }
  const text = visibleText(html)
  // 語系切換器的「繁中」按鈕文字本身就該是繁中（讓英文使用者找得到回去的路），不算殘留。
  const hits = text.split(/\n|\s{2,}/).map((t) => t.trim()).filter((t) => t !== '繁中' && CJK.test(t) && !KNOWN_ZH.some((re) => re.test(t)))
  if (hits.length) {
    bad++
    console.log(`✗  ${route}${ready ? '' : '  (尚未宣告 enReady)'}  殘留 ${hits.length} 段`)
    for (const h of hits.slice(0, 5)) console.log(`     ${h.slice(0, 80)}`)
  } else {
    console.log(`✓  ${route}${ready ? '' : '  (未宣告 enReady，但已無中文)'}`)
  }
}
console.log(`\n${routes.length} 頁，${bad} 頁有中文殘留。`)
process.exit(bad ? 1 : 0)
