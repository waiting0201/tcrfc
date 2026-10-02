#!/usr/bin/env node
/**
 * check-site-images.mjs — 客戶照片不得被 import 進建置（docs/14 不變量、docs/18 E-113）。
 *
 * 背景：`public/assets/img/` 的客戶照片（含未成年學員肖像）不納版控、不得進映像檔。
 * Vue SFC 編譯器會把模板裡**靜態**的 `src="/assets/img/…"` 轉成 import，乾淨 checkout 就
 * UNRESOLVED_IMPORT。全站一律走 `siteImg('/assets/img/…')`（app/utils/siteImage.ts）。
 *
 * 預設模式（掛在 `npm run lint`，不需要照片檔，乾淨 checkout 可跑）：
 *   ① app／shared／server／nuxt.config.ts 裡每一個 `/assets/img/…` 字串必須直接是
 *      `siteImg(` 的引數；否則失敗（涵蓋 `import … from '/assets/img/…'`、
 *      靜態 `src="/assets/img/…"`、`new URL('/assets/img/…', import.meta.url)`、
 *      CSS／內嵌 style 的 `url(/assets/img/…)`）。
 *   ② `scripts/site-images.txt` 必須與程式實際引用一致：
 *      - 字面字串引用的每一張都在清單裡；
 *      - 清單裡每一行都有字面引用，或落在「動態前綴」下
 *        （`siteImg(\`/assets/img/news/${slug}.jpg\`)` 這種寫法，前綴＝`news/`，
 *         該目錄底下的照片整批算被用到）。
 *   ③ 清單不得有重複、不得含 `.svg`（svg 留在 repo，不走 Blob）。
 *
 * `--write`（需要本機有 `public/assets/img/`）：重新產生 `scripts/site-images.txt`
 *   ＝字面引用 ∪ 動態前綴目錄下的實際檔案，並檢查字面引用的檔案都存在。
 *   新增／移除照片引用後，先跑 `npm run site-images:write` 再提交清單。
 */
import { readFileSync, writeFileSync, readdirSync, statSync, existsSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('..', import.meta.url))
const LIST = join(ROOT, 'scripts/site-images.txt')
const IMG_DIR = join(ROOT, 'public/assets/img')
const HELPER = 'app/utils/siteImage.ts'
const SCAN = ['app', 'shared', 'server', 'nuxt.config.ts']
const EXT = /\.(vue|ts|mjs|js|css)$/

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

const errors = []
const literal = new Set()
const dynamicPrefixes = new Set()
// 字串開頭是引號／反引號，內容以 /assets/img/ 起頭
const STR = /(['"`])(\/assets\/img\/[^'"`\n]*)\1/g

for (const file of SCAN.flatMap((p) => walk(p))) {
  if (file.split(sep).join('/') === HELPER) continue
  const text = readFileSync(join(ROOT, file), 'utf8')
  for (const m of text.matchAll(STR)) {
    const before = text.slice(Math.max(0, m.index - 40), m.index)
    const line = text.slice(0, m.index).split('\n').length
    if (!/siteImg\(\s*$/.test(before)) {
      errors.push(`${file}:${line} 直接出現 ${m[1]}${m[2]}${m[1]}——客戶照片必須寫成 siteImg('/assets/img/…')（docs/14）`)
      continue
    }
    const rel = m[2].slice('/assets/img/'.length)
    if (rel.includes('${')) dynamicPrefixes.add(rel.slice(0, rel.indexOf('${')))
    else literal.add(rel)
  }
  // 沒有引號包住的 url(/assets/img/…)（CSS／內嵌 style）
  for (const m of text.matchAll(/url\(\s*(\/assets\/img\/[^)'"\s]*)/g)) {
    errors.push(`${file}:${text.slice(0, m.index).split('\n').length} url(${m[1]}) 會被 Vite 解析，禁止（docs/14）`)
  }
}

for (const p of dynamicPrefixes) {
  if (!p.endsWith('/')) errors.push(`動態前綴 "${p}" 必須是目錄（以 / 結尾），例如 siteImg(\`/assets/img/news/\${slug}.jpg\`)`)
}

const underDynamic = (rel) => [...dynamicPrefixes].some((p) => rel.startsWith(p))

if (process.argv.includes('--write')) {
  if (!existsSync(IMG_DIR)) { console.error('✗ 找不到 public/assets/img/，無法列舉動態前綴目錄；請先依 README 同步照片'); process.exit(1) }
  const all = new Set(literal)
  const listAll = (dir) => readdirSync(join(IMG_DIR, dir)).flatMap((n) => {
    const rel = join(dir, n)
    return statSync(join(IMG_DIR, rel)).isDirectory() ? listAll(rel) : [rel.split(sep).join('/')]
  })
  for (const p of dynamicPrefixes) {
    if (!existsSync(join(IMG_DIR, p))) { errors.push(`動態前綴目錄不存在：public/assets/img/${p}`); continue }
    for (const f of listAll(p)) all.add(f)
  }
  for (const rel of literal) if (!existsSync(join(IMG_DIR, rel))) errors.push(`被引用但本機沒有檔案：public/assets/img/${rel}`)
  const lines = [...all].filter((r) => !/\.svg$/i.test(r)).sort()
  if (errors.length === 0) {
    writeFileSync(LIST, lines.join('\n') + '\n')
    console.log(`✓ 已寫入 ${relative(ROOT, LIST)}：${lines.length} 筆（字面引用 ${literal.size}、動態前綴 ${[...dynamicPrefixes].join(', ') || '無'}）`)
  }
} else {
  if (!existsSync(LIST)) errors.push('缺 scripts/site-images.txt，請跑 npm run site-images:write')
  else {
    const raw = readFileSync(LIST, 'utf8').split('\n').map((l) => l.trim()).filter(Boolean)
    const set = new Set(raw)
    if (set.size !== raw.length) errors.push('site-images.txt 有重複行')
    for (const r of raw) if (/\.svg$/i.test(r)) errors.push(`site-images.txt 不得含 .svg（留在 repo，不走 Blob）：${r}`)
    for (const r of literal) if (!set.has(r)) errors.push(`程式引用但清單沒有：${r}（跑 npm run site-images:write）`)
    for (const r of raw) if (!literal.has(r) && !underDynamic(r)) errors.push(`清單有但程式沒引用：${r}（跑 npm run site-images:write）`)
  }
  if (errors.length === 0) console.log(`✓ 照片引用檢查通過：字面 ${literal.size} 張、動態前綴 ${[...dynamicPrefixes].join(', ') || '無'}`)
}

if (errors.length) { for (const e of errors) console.error('✗ ' + e); process.exit(1) }
