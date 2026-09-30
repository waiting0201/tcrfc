#!/usr/bin/env node
/**
 * check-club-image-leak.mjs — 在**已渲染的藍鯨站 SSR 輸出**裡，找出不屬於藍鯨的圖片來源。
 *
 * ## 為什麼需要這支腳本（`docs/18-work-errors.md` `E-83`）
 *
 * `check-club-brand-leak.mjs` 只比對**文字**詞表。藍鯨站曾多頁無條件輸出磐石照片
 * （`academy/index.vue` hero 用磐石未成年學員照、`about/*` 九頁用 `nav-about.jpg`、
 * `club/player-stories`／`club/opportunities` 用 `trencin-*.jpg`……），而這些 `<img>`
 * 的 `alt=""`、檔名又不含任何詞表用字，文字檢查從頭到尾看不見。
 *
 * ## 掃描範圍（每頁整份 HTML，含 `<head>`）
 *   - `<img src|srcset>`、`<source src|srcset>`、`<video poster|src>`、`<link rel=preload as=image>`
 *   - `<link rel=icon|apple-touch-icon|mask-icon>` 的 href
 *   - inline `style="…url(…)…"` 與 `<style>` 區塊裡的 `url(…)`
 *   - `<meta property="og:image|og:image:secure_url|twitter:image">`
 *   - JSON-LD（`<script type="application/ld+json">`）裡的 `image`／`logo`／`thumbnailUrl`／`contentUrl`
 *   - 頁面實際載入的外部樣式表（`<link rel=stylesheet>`）裡的 `url(…)`（同一份樣式表只抓一次）
 *
 * ## 分類與允許清單
 *   藍鯨素材（`ALLOWED_PREFIXES`，藍鯨專屬目錄）→ 允許；
 *   明列的中性素材（`NEUTRAL_ALLOWED`，每筆附理由）→ 允許；
 *   **其餘一律視為磐石素材（不確定的歸磐石）** → 失敗。
 *   `data:` URI 只允許 `image/svg+xml`／極小的內嵌圖示，且不含磐石詞彙（見 `classify`）。
 *   站外網址（`https://…`）預設**不允許**，除非明列在 `NEUTRAL_ALLOWED`。
 *
 * 允許清單**只能靠人工加**——新增一筆必須同時寫理由；「不確定」不是理由。含未成年人物、
 * 隊徽、贊助商標誌、場館可辨識的圖，不得列為中性素材。
 *
 * ## 用法
 *   1. 依 `apps/web/README.md`「本機測試兩個 club」把藍鯨站（`NUXT_PUBLIC_CLUB=bw` 且帶
 *      `NUXT_PUBLIC_SITE_NAME=台中藍鯨`）跑起來。
 *   2. `node scripts/check-club-image-leak.mjs [--base-url=http://127.0.0.1:3012] [--inventory]`
 *      `--inventory`：另外印出全站不重複圖片來源的分類清單（含各來源出現在哪些頁）。
 *
 * 離開碼：任何一條路由出現不在允許清單內的圖片來源 → `1`；否則 `0`。
 * 不掛進 `npm run lint`，理由同 `check-club-brand-leak.mjs`（需要先把藍鯨站跑起來）。
 */
import { collectAllRoutes } from './lib/collect-routes.mjs'

// ---------------------------------------------------------------------------
// 允許清單
// ---------------------------------------------------------------------------

/** 藍鯨專屬素材目錄（路徑前綴比對）。 */
const ALLOWED_PREFIXES = [
  { prefix: '/assets/brand/bw/', reason: '藍鯨品牌資產（隊徽點陣、favicon、apple-touch-icon），取自 brand/blue-whale/' },
]

/**
 * 明列的中性素材：不含任何俱樂部人物、隊徽、贊助、可辨識場景。
 * 目前為空——見 `docs/14-invariants.md`：不確定的一律歸磐石。
 * 格式：{ path: '/assets/…', reason: '…' }（完整路徑精確比對，不做前綴）。
 */
const NEUTRAL_ALLOWED = [
  // 無隊徽的通用配件商品圖需先逐張人工確認無磐石贊助標誌／logo 後才可列入。
]

// ---------------------------------------------------------------------------
// 抽取
// ---------------------------------------------------------------------------
function decodeEntities(s) {
  return s.replace(/&amp;/g, '&').replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&lt;/g, '<').replace(/&gt;/g, '>')
}

function splitSrcset(v) {
  return v
    .split(',')
    .map((c) => c.trim().split(/\s+/)[0])
    .filter(Boolean)
}

function attrs(tag) {
  const out = {}
  for (const m of tag.matchAll(/([a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+))/g)) {
    out[m[1].toLowerCase()] = decodeEntities(m[2] ?? m[3] ?? m[4] ?? '')
  }
  return out
}

function cssUrls(css) {
  return [...css.matchAll(/url\(\s*(?:"([^"]*)"|'([^']*)'|([^)"'\s][^)]*?))\s*\)/gi)].map((m) => decodeEntities(m[1] ?? m[2] ?? m[3]).trim())
}

function jsonLdImages(node, out) {
  if (Array.isArray(node)) return node.forEach((n) => jsonLdImages(n, out))
  if (node && typeof node === 'object') {
    for (const [k, v] of Object.entries(node)) {
      if (['image', 'logo', 'thumbnailUrl', 'contentUrl'].includes(k)) {
        const vals = Array.isArray(v) ? v : [v]
        for (const x of vals) {
          if (typeof x === 'string') out.push(x)
          else if (x && typeof x === 'object' && typeof x.url === 'string') out.push(x.url)
        }
      }
      jsonLdImages(v, out)
    }
  }
}

/** 回傳 [{ src, via }]。 */
function extractImages(html) {
  const found = []
  const add = (src, via) => {
    if (src) found.push({ src, via })
  }

  for (const m of html.matchAll(/<(img|source|video|link|meta)\b[^>]*>/gi)) {
    const tag = m[0]
    const name = m[1].toLowerCase()
    const a = attrs(tag)
    if (name === 'img' || name === 'source') {
      add(a.src, `<${name} src>`)
      if (a.srcset) splitSrcset(a.srcset).forEach((s) => add(s, `<${name} srcset>`))
      if (a['data-src']) add(a['data-src'], `<${name} data-src>`)
    } else if (name === 'video') {
      add(a.poster, '<video poster>')
      add(a.src, '<video src>')
    } else if (name === 'link') {
      const rel = (a.rel ?? '').toLowerCase()
      if (/\b(icon|apple-touch-icon|mask-icon|shortcut)\b/.test(rel)) add(a.href, `<link rel=${rel}>`)
      if (rel.includes('preload') && a.as === 'image') {
        add(a.href, '<link preload image>')
        if (a.imagesrcset) splitSrcset(a.imagesrcset).forEach((s) => add(s, '<link imagesrcset>'))
      }
    } else if (name === 'meta') {
      const key = (a.property ?? a.name ?? '').toLowerCase()
      if (['og:image', 'og:image:url', 'og:image:secure_url', 'twitter:image', 'twitter:image:src'].includes(key)) {
        add(a.content, `<meta ${key}>`)
      }
    }
  }

  // 任何元素的 inline style
  for (const m of html.matchAll(/\sstyle\s*=\s*(?:"([^"]*)"|'([^']*)')/gi)) {
    cssUrls(decodeEntities(m[1] ?? m[2])).forEach((u) => add(u, 'inline style url()'))
  }
  // <style> 區塊
  for (const m of html.matchAll(/<style\b[^>]*>([\s\S]*?)<\/style>/gi)) {
    cssUrls(m[1]).forEach((u) => add(u, '<style> url()'))
  }
  // JSON-LD
  for (const m of html.matchAll(/<script\b[^>]*type\s*=\s*["']application\/ld\+json["'][^>]*>([\s\S]*?)<\/script>/gi)) {
    try {
      const imgs = []
      jsonLdImages(JSON.parse(m[1]), imgs)
      imgs.forEach((u) => add(u, 'JSON-LD image/logo'))
    } catch {
      add('(unparseable JSON-LD)', 'JSON-LD parse error')
    }
  }
  return found
}

function stylesheetHrefs(html) {
  const out = []
  for (const m of html.matchAll(/<link\b[^>]*>/gi)) {
    const a = attrs(m[0])
    if ((a.rel ?? '').toLowerCase().includes('stylesheet') && a.href) out.push(a.href)
  }
  return out
}

// ---------------------------------------------------------------------------
// 分類
// ---------------------------------------------------------------------------
function normalizePath(src, baseUrl) {
  if (src.startsWith('data:')) return src
  try {
    const u = new URL(src, baseUrl)
    const base = new URL(baseUrl)
    // 同源（含站台自己的 canonical 網域，因為 SITE_URL 可能不等於本機 base-url）的資源只看路徑
    if (u.origin === base.origin || /\/assets\//.test(u.pathname) && /(^|\.)tcrfc\.tw$|127\.0\.0\.1|localhost/.test(u.hostname)) {
      return u.pathname
    }
    return u.href
  } catch {
    return src
  }
}

function classify(norm) {
  if (norm.startsWith('data:')) {
    if (/^data:image\/svg\+xml/.test(norm) && norm.length < 2000 && !/磐石|TCRFC|tcrfc/i.test(decodeURIComponent(norm.slice(0, 2000)))) {
      return { kind: 'neutral', reason: '內嵌 SVG（小型、不含俱樂部字樣）' }
    }
    return { kind: 'tcrfc', reason: 'data: URI 無法確認內容，歸磐石' }
  }
  for (const p of ALLOWED_PREFIXES) if (norm.startsWith(p.prefix)) return { kind: 'bw', reason: p.reason }
  const n = NEUTRAL_ALLOWED.find((x) => x.path === norm)
  if (n) return { kind: 'neutral', reason: n.reason }
  return { kind: 'tcrfc', reason: '不在允許清單，視為磐石素材（不確定一律歸磐石）' }
}

// ---------------------------------------------------------------------------
// 自我測試：抽取器自己漏抓就會讓整個檢查失真（跟 check-heading-structure 一樣，先驗工具）。
// 每次執行先跑一次，樣本涵蓋檔頭列的每一種來源；漏抓任何一種 → 離開碼 2。
// ---------------------------------------------------------------------------
{
  const sample = `<html><head>
<link rel="icon" href="/i/favicon.png"><link rel="apple-touch-icon" href="/i/touch.png">
<link rel="preload" as="image" href="/i/preload.jpg" imagesrcset="/i/p1.jpg 1x, /i/p2.jpg 2x">
<meta property="og:image" content="/i/og.png"><meta name="twitter:image" content="/i/tw.png">
<style>.a{background:url("/i/css1.jpg")}</style>
<script type="application/ld+json">{"@graph":[{"logo":"/i/ld-logo.png","image":["/i/ld-img.jpg",{"url":"/i/ld-obj.jpg"}]}]}</script>
</head><body>
<img src="/i/img.jpg" srcset="/i/s1.jpg 1x, /i/s2.jpg 2x">
<picture><source srcset="/i/src.webp 1x"></picture>
<video poster="/i/poster.jpg" src="/i/v.mp4"></video>
<div style="background-image:url('/i/inline.jpg')"></div>
</body></html>`
  const got = new Set(extractImages(sample).map((x) => x.src))
  const want = ['favicon.png', 'touch.png', 'preload.jpg', 'p1.jpg', 'p2.jpg', 'og.png', 'tw.png', 'css1.jpg', 'ld-logo.png', 'ld-img.jpg', 'ld-obj.jpg', 'img.jpg', 's1.jpg', 's2.jpg', 'src.webp', 'poster.jpg', 'v.mp4', 'inline.jpg'].map((n) => `/i/${n}`)
  const missing = want.filter((w) => !got.has(w))
  if (missing.length > 0) {
    console.error(`✗ 抽取器自我測試失敗，漏抓：${missing.join('、')}`)
    process.exit(2)
  }
}

// ---------------------------------------------------------------------------
// 主流程
// ---------------------------------------------------------------------------
const baseUrlArg = process.argv.find((a) => a.startsWith('--base-url='))
const baseUrl = (baseUrlArg ? baseUrlArg.slice('--base-url='.length) : 'http://127.0.0.1:3012').replace(/\/$/, '')
const inventory = process.argv.includes('--inventory')
const routes = collectAllRoutes()

console.log(`藍鯨圖片來源檢查（全站，zh／en）—— 目標：${baseUrl}（共 ${routes.length} 條路由）`)

const failures = [] // { route, src, via }
const bySource = new Map() // norm -> { cls, pages:Set }
const cssSeen = new Set()
let checked = 0
let fetchFailures = 0

function record(route, src, via) {
  const norm = normalizePath(src, baseUrl)
  const cls = classify(norm)
  let e = bySource.get(norm)
  if (!e) bySource.set(norm, (e = { cls, pages: new Set(), vias: new Set() }))
  e.pages.add(route)
  e.vias.add(via)
  if (cls.kind === 'tcrfc') failures.push({ route, src: norm.length > 120 ? `${norm.slice(0, 117)}...` : norm, via })
}

for (const route of routes) {
  let res
  try {
    res = await fetch(`${baseUrl}${route}`, { redirect: 'manual' })
  } catch (err) {
    fetchFailures++
    console.error(`  ⚠️ 無法連線 ${baseUrl}${route}：${err.message}`)
    continue
  }
  if (res.status === 404 || (res.status >= 300 && res.status < 400)) continue
  if (!res.ok) {
    console.error(`  ⚠️ ${route} 回應 ${res.status}，跳過`)
    continue
  }
  checked++
  const html = await res.text()
  for (const { src, via } of extractImages(html)) record(route, src, via)
  for (const href of stylesheetHrefs(html)) {
    const abs = new URL(href, baseUrl).href
    if (cssSeen.has(abs)) continue
    cssSeen.add(abs)
    try {
      const css = await (await fetch(abs)).text()
      for (const u of cssUrls(css)) record(route, u, `stylesheet ${href} url()`)
    } catch (err) {
      console.error(`  ⚠️ 無法取得樣式表 ${abs}：${err.message}`)
    }
  }
}

if (fetchFailures === routes.length) {
  console.error('\n✗ 全部路由連不上——藍鯨站沒有跑起來。參見 apps/web/README.md「本機測試兩個 club」。')
  process.exit(1)
}

console.log(`\n已檢查 ${checked} 條路由，共 ${bySource.size} 個不重複圖片來源。`)
const counts = { bw: 0, neutral: 0, tcrfc: 0 }
for (const e of bySource.values()) counts[e.cls.kind]++
console.log(`分類：藍鯨 ${counts.bw}、中性 ${counts.neutral}、磐石（違規）${counts.tcrfc}`)

if (inventory) {
  console.log('\n--- 不重複圖片來源清單 ---')
  for (const [src, e] of [...bySource].sort((a, b) => a[1].cls.kind.localeCompare(b[1].cls.kind) || a[0].localeCompare(b[0]))) {
    console.log(`[${e.cls.kind}] ${src}  ×${e.pages.size} 頁  (${[...e.vias].join(', ')})`)
  }
}

if (failures.length > 0) {
  const grouped = new Map()
  for (const f of failures) {
    if (!grouped.has(f.route)) grouped.set(f.route, new Set())
    grouped.get(f.route).add(`${f.src} [${f.via}]`)
  }
  console.error(`\n✗ ${grouped.size} 條路由出現不在允許清單內的圖片來源（視為磐石素材）：\n`)
  for (const [route, srcs] of grouped) {
    console.error(`  - ${route}`)
    for (const s of srcs) console.error(`      ${s}`)
  }
  console.error('')
  process.exit(1)
}

console.log('\n✓ 藍鯨全站圖片來源皆在允許清單內（藍鯨素材或明列的中性素材）。')
