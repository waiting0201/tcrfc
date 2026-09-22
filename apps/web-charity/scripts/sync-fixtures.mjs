#!/usr/bin/env node
// scripts/sync-fixtures.mjs — 把唯一真實來源 db/seed/charity-fixtures.json 複製成本機建置期的
// 暫存複本 .data/charity-fixtures.json，供 server/utils/fixtures.ts 用相對路徑 import。
//
// 為什麼需要這一步（而不是直接 import ../../db/seed/charity-fixtures.json）：
//   - `nuxt dev` 底下 Vite 的 dev server 預設限制只能提供 `server.fs.allow` 白名單內的檔案
//     （預設是專案根目錄），直接從專案外（../../db/seed/...）匯入在 dev 模式下容易撞到這個限制；
//     搬進專案內一個固定路徑可以完全避開這個問題，build 模式（Rollup/Nitro 直接讀檔）雖然通常不受
//     影響，但兩種模式統一用同一個匯入路徑可以減少「dev 過但 build 壞」或反過來的落差。
//   - 這不是「複製一份到 apps/web-charity 底下」的手寫假資料——`.data/` 整個目錄不納版控
//     （見 .gitignore），每次 `npm run dev`／`npm run build` 前都會重新同步，內容永遠等於
//     db/seed/charity-fixtures.json 當下的樣子，不會漂移出兩份不同的資料。
//
// 🔴 2026-09-22 修正：來源從 `../../db/seed/charity-fixtures.json` 改成專案內的
// `fixtures/charity-fixtures.json`。
//
// 原因是 `docker build apps/web-charity` **實測會失敗**：
//   [sync-fixtures] 找不到來源檔案：/db/seed/charity-fixtures.json
// `docs/20-cicd.md` §3 的既有慣例是以 app 目錄為 build context（`docker build -t x apps/admin`
// 已實測過），repo 根目錄的檔案因此落在 build context 之外，容器內讀不到。
//
// 解法沒有改 build context（那要改兩支 Dockerfile、加根目錄 .dockerignore 擋掉 456MB 的
// 收件夾與 reference/，並推翻 docs/20 已記錄的慣例），而是讓
// `db/seed/emit-charity-fixtures.py` 同時寫一份納版控的副本到各 app 目錄內。
//
// ⛔ `fixtures/charity-fixtures.json` 是機器產生的，不要手動編輯。要改資料就改
// `db/seed/generate-charity-seed-sql.py` 再重跑匯出腳本；`npm run lint` 的
// `emit-charity-fixtures.py --check` 會一次驗三份檔案內容是否一致，過期就擋下。

import { copyFileSync, mkdirSync, existsSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const projectRoot = resolve(here, '..')
const source = resolve(projectRoot, 'fixtures/charity-fixtures.json')
const destDir = resolve(projectRoot, '.data')
const dest = resolve(destDir, 'charity-fixtures.json')

if (!existsSync(source)) {
  console.error(
    `[sync-fixtures] 找不到來源檔案：${source}\n`
    + '這份副本由 db/seed/emit-charity-fixtures.py 產生，應該是納版控的。\n'
    + '請跑：python3 ../../db/seed/emit-charity-fixtures.py',
  )
  process.exit(1)
}

mkdirSync(destDir, { recursive: true })
copyFileSync(source, dest)
console.log(`[sync-fixtures] 已同步 ${source} -> ${dest}`)
