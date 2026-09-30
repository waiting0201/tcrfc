/**
 * 路由收集（`check-club-brand-leak.mjs` 與 `check-club-image-leak.mjs` 共用，確保兩支
 * 檢查掃的是同一份路由清單）。
 *
 * 從 app/pages/zh 檔案樹算出全部路由，zh／en 都收（en 路由是 pages:extend 在 build 時從 zh
 * 複製出來的孿生路由，原始碼裡沒有實體 en/ 目錄，見 nuxt.config.ts）。排除動態路由
 * （檔名或目錄含 `[`）——那些頁面的可抓取網址取決於後端當下有哪些 slug。
 */
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { readdirSync, statSync } from 'node:fs'

const HERE = dirname(fileURLToPath(import.meta.url))
export const PAGES_DIR = resolve(HERE, '../../app/pages')

function collectRoutes(dir, base = '') {
  const routes = []
  for (const entry of readdirSync(dir)) {
    if (entry.includes('[')) continue
    const full = resolve(dir, entry)
    if (statSync(full).isDirectory()) {
      routes.push(...collectRoutes(full, `${base}/${entry}`))
    } else if (entry.endsWith('.vue')) {
      const name = entry.replace(/\.vue$/, '')
      const path = name === 'index' ? `${base}/` : `${base}/${name}/`
      routes.push(path || '/')
    }
  }
  return routes
}

export function collectAllRoutes() {
  const zhRoutes = [...new Set(collectRoutes(PAGES_DIR))].filter((r) => r.startsWith('/zh/')).sort()
  return [...zhRoutes, ...zhRoutes.map((r) => `/en${r.slice(3)}`)]
}
