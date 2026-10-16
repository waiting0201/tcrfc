#!/usr/bin/env node
/**
 * check-page-seed-sync.mjs — `db/seed/page_seed_content.json` 必須等於 `page_seed_content.extract.ts` 的重產結果（STATUS CF-3b）。
 *
 * 為什麼需要：`page_seed_content.json` 是 B1 固定頁的種子，由 extract.ts 自前台文案（shared/utils）產生。
 * 2026-10-16 後端曾直接手改 json（四大核心價值、4.4 課綱頁），而前台文案還是舊的——這時**任何人重跑 extract 都會把後端的手改蓋掉**，
 * 而且沒有任何東西會響。現在 about/philosophy 與 academy/curriculum 的文案來源已搬到前台
 * （shared/utils/core-values.ts、academy-curriculum.ts），本腳本確保兩者永遠一致：
 *   - 改前台文案忘了重跑 extract → 這裡失敗（請重跑並提醒後端重產 db/prod 種子）；
 *   - 有人直接改 json → 這裡失敗（請改前台來源檔再重跑 extract，不要直接改 json）。
 *
 * 做法：用 vite-node 跑 extract.ts（輸出到 stdout），與 json 檔逐位元比對。只讀不寫，不會改任何檔案。
 * 掛在 `npm run lint`。離開碼：不一致或執行失敗 1／一致 0。
 */
import { execFileSync } from 'node:child_process'
import { readFileSync, existsSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const HERE = dirname(fileURLToPath(import.meta.url))
const WEB = resolve(HERE, '..')
const EXTRACT = resolve(WEB, '../../db/seed/page_seed_content.extract.ts')
const JSON_FILE = resolve(WEB, '../../db/seed/page_seed_content.json')
const BIN = resolve(WEB, 'node_modules/.bin/vite-node')

if (!existsSync(EXTRACT) || !existsSync(JSON_FILE)) {
  console.log('check-page-seed-sync：略過（找不到 db/seed 檔案；apps/web 被單獨取出時不適用）')
  process.exit(0)
}
if (!existsSync(BIN)) {
  console.error('check-page-seed-sync 失敗：找不到 node_modules/.bin/vite-node（請先 npm ci）')
  process.exit(1)
}

let out
try {
  out = execFileSync(BIN, [EXTRACT], { cwd: WEB, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, stdio: ['ignore', 'pipe', 'pipe'] })
}
catch (err) {
  console.error('check-page-seed-sync 失敗：extract.ts 執行錯誤\n' + (err.stderr || err.message))
  process.exit(1)
}
const expected = readFileSync(JSON_FILE, 'utf8')
if (out.trimEnd() !== expected.trimEnd()) {
  const a = out.split('\n'), b = expected.split('\n')
  let i = 0
  while (i < a.length && i < b.length && a[i] === b[i]) i++
  console.error('check-page-seed-sync 失敗：db/seed/page_seed_content.json 與 extract.ts 的重產結果不一致')
  console.error(`  第一個差異在第 ${i + 1} 行\n    extract 產生：${(a[i] ?? '(結尾)').slice(0, 120)}\n    json 現況　：${(b[i] ?? '(結尾)').slice(0, 120)}`)
  console.error('  處理：改前台文案來源（apps/web/shared/utils，見 extract.ts 檔頭）→ 重跑 extract 寫回 json → 請後端重產 db/prod 種子與 manifest。不要直接改 json。')
  process.exit(1)
}
console.log('check-page-seed-sync 通過：page_seed_content.json 與 extract.ts 重產結果逐位元一致')
