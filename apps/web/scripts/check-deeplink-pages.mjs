#!/usr/bin/env node
/**
 * check-deeplink-pages.mjs — App 深連結的官網回退網址必須真的有頁面（docs/19 §2 缺口 3、6）。
 *
 * `shared/deeplinks.json`（App 規劃書 §2.3 對照表）的每一列 `webPath` 都是「未安裝 App 的裝置點到深連結時的回退網址」，
 * 規劃書要求「不得顯示錯誤頁」。這支腳本逐列把 `webPath` 對到 `app/pages/zh/` 底下的實際頁面檔
 * （靜態目錄、`[param]`、`[[param]]` 皆可），再確認：
 *   1. 每個 webPath 都有頁面；
 *   2. `webLocales` 列出的語系（/en/）由 nuxt.config.ts 的 `pages:extend` 複製 `/zh/` 孿生路由自動成立，這裡只檢查該 hook 仍在；
 *   3. `/zh/schedule/{slug}` 這種「同檔多路由」的第二條路由（nuxt.config.ts `pages:extend`）還在。
 * 頁面存在不等於回退行為正確（未知參數 302），那部分由 docs/19 §2 的實機驗證表負責。
 *
 * 用法：node scripts/check-deeplink-pages.mjs　（掛在 `npm run lint`）
 */
import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const HERE = dirname(fileURLToPath(import.meta.url))
const PAGES = resolve(HERE, '../app/pages')
const deeplinks = JSON.parse(readFileSync(resolve(HERE, '../../../shared/deeplinks.json'), 'utf8'))
const nuxtConfig = readFileSync(resolve(HERE, '../nuxt.config.ts'), 'utf8')

function hasPage(dir, segments) {
  if (segments.length === 0) return existsSync(resolve(dir, 'index.vue'))
  const [head, ...rest] = segments
  if (!existsSync(dir)) return false
  const entries = readdirSync(dir)
  const isParam = /^\{\w+\}$/.test(head)
  const candidates = isParam
    ? entries.filter((e) => /^\[\[?\w+\]?\]/.test(e))
    : entries.filter((e) => e === head || e === `${head}.vue`)
  for (const entry of candidates) {
    if (entry.endsWith('.vue')) {
      if (rest.length === 0) return true
      continue
    }
    if (hasPage(resolve(dir, entry), rest)) return true
  }
  // 動態區段允許吃掉最後一段（例如 `[slug].vue`、`[[slug]]` 與同檔多路由）
  if (isParam && rest.length === 0) return candidates.length > 0
  return false
}

const errors = []
for (const r of deeplinks.routes) {
  if (!r.webPath) continue
  const segments = r.webPath.split('/').filter(Boolean)
  const found = hasPage(PAGES, segments)
    // schedule：賽事行事曆一個檔，`{team}`／`{slug}` 兩種網址靠 nuxt.config.ts 的 pages:extend 第二條路由
    || (segments[1] === 'schedule' && existsSync(resolve(PAGES, 'zh/schedule.vue')) && nuxtConfig.includes("'/zh/schedule/:slug()'"))
  if (!found) errors.push(`${r.id}：webPath ${r.webPath} 在 app/pages 找不到對應頁面（未安裝 App 的回退網址會 404）`)
}
if ((deeplinks.webLocales ?? ['zh']).includes('en') && !nuxtConfig.includes("`/en${page.path.slice('/zh'.length)}`")) {
  errors.push("deeplinks.json 宣告 /en/，但 nuxt.config.ts 的 pages:extend /en/ 孿生路由複製邏輯不見了")
}
if (!existsSync(resolve(PAGES, 'zh/app/index.vue'))) errors.push('缺 /zh/app/ App 下載頁（App 規劃書 §2.3 對官網的依賴第 2 點）')

if (errors.length) {
  console.error('check-deeplink-pages 失敗：\n- ' + errors.join('\n- '))
  process.exit(1)
}
console.log(`check-deeplink-pages 通過（${deeplinks.routes.filter((r) => r.webPath).length} 條回退網址皆有頁面）`)
