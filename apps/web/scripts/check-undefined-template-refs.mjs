#!/usr/bin/env node
/**
 * check-undefined-template-refs.mjs — 擋住「樣板呼叫了 script setup 沒解構出來的識別字」。
 *
 * ## 為什麼需要這支腳本（E-63）
 *
 * `docs/18-work-errors.md` E-63：S1-13 把 `SiteHeader.vue`／`SiteFooter.vue` 的 78＋19 處連結
 * 改成呼叫 `lp(...)`，但 `<script setup>` 只解構了 `const { locale, switchTo } = useLocale()`，
 * 漏了 `lp` 本身——`npm run build`（Vite／esbuild 只做語法轉譯）與 `npm run lint`（eslint 不做
 * 型別檢查）兩者都全綠，直到本機真的啟動容器、`curl` 首頁才炸出執行期錯誤
 * `_ctx.lp is not a function`（500）。這支腳本要在那之前攔下來。
 *
 * ## 為什麼不是直接把 `vue-tsc --noEmit` 或 `nuxi typecheck` 整個接進 `npm run lint`
 *
 * 本專案目前有一批與樣板識別字無關的既有型別債（`useFetch` 的 `.items` 在多個新聞／賽程頁
 * 回傳 `{}`、`academy/teams.vue`、`about/our-people.vue` 的隱式 any），直接把 `nuxi typecheck`
 * 的結果當「有錯誤就 fail」會立刻紅燈，反而讓人習慣忽略 lint 的紅字——跟 `check-club-copy.mjs`
 * 檔頭記的 `docs/18` `E-31`「一個只在特定條件下才成立的機制卻給出全面信心」是同一個坑。
 *
 * 這支腳本改用 `nuxi typecheck` 的輸出，只挑出兩種訊息形狀，刻意不是「有錯誤就 fail」：
 *
 * 1. **訊息裡含 `ComponentInternalInstance` 的 `TS2339`**——這是「樣板用了一個 script setup
 *    沒回傳（沒解構／沒宣告）的識別字」在 Vue SFC 型別檢查底下實際的樣子：模板編譯後對
 *    `_ctx.lp` 這種存取，型別檢查是拿完整的元件 proxy 型別（永遠帶 `$: ComponentInternalInstance`
 *    這個內部屬性）去比對，找不到就報「Property 'lp' does not exist on type '{ ...；
 *    $: ComponentInternalInstance；... }'」。**已用故意刪掉 `governance.vue` 的
 *    `const { lp } = useLocale()` 這一行實測**：報的正是這種形狀。2026-09-25 也實測過本專案
 *    既有的型別債（`useFetch().items` 在多個新聞／賽程頁回傳 `{}`、`academy/teams.vue`、
 *    `about/our-people.vue` 隱式 any）**訊息裡完全沒有 `ComponentInternalInstance`**——那些是
 *    對一般資料型別（`{}`、陣列元素）的屬性存取，不是對元件 proxy 型別的存取，兩者在訊息
 *    文字上可以乾淨分開，不需要維護 baseline／allowlist。
 * 2. **`TS2304`（Cannot find name）／`TS2552`（Cannot find name, did you mean）**——涵蓋
 *    「在 `<script setup>` 頂層（不是樣板）用了沒 import／沒宣告的識別字」這種鄰近但不同的錯誤
 *    形狀，例如漏了某個 composable 呼叫本身。2026-09-25 實測本專案既有型別債裡這兩種代碼也是
 *    零筆。
 *
 * 之後既有型別債清完、可以把完整 `nuxi typecheck` 接進 `npm run lint` 時，這支腳本可以退役
 * （比照 `check-club-copy.mjs` 檔頭同一句話）。
 */

import { spawnSync } from 'node:child_process'

const UNDEFINED_NAME_CODES = ['TS2304', 'TS2552']

const result = spawnSync('npx', ['nuxi', 'typecheck'], {
  cwd: new URL('..', import.meta.url).pathname,
  encoding: 'utf-8',
  maxBuffer: 1024 * 1024 * 32,
})

const output = `${result.stdout ?? ''}${result.stderr ?? ''}`

if (result.error) {
  console.error('❌ check-undefined-template-refs：執行 `nuxi typecheck` 失敗。')
  console.error(result.error)
  process.exit(1)
}

const lines = output.split('\n')
const offendingLines = lines.filter((line) => {
  if (line.includes('error TS2339:') && line.includes('ComponentInternalInstance')) return true
  return UNDEFINED_NAME_CODES.some((code) => line.includes(`error ${code}:`))
})

if (offendingLines.length > 0) {
  console.error(
    '❌ check-undefined-template-refs：偵測到樣板或 script 用了沒宣告／沒解構的識別字\n' +
      '   （TS2339 對元件 proxy 型別的存取失敗／TS2304 Cannot find name／TS2552 Cannot find\n' +
      '   name, did you mean），這是 E-63 的錯誤形狀——多半是解構 useXxx() composable 時\n' +
      '   漏了樣板實際用到的欄位。\n',
  )
  for (const line of offendingLines) {
    console.error('  ' + line.trim())
  }
  console.error(
    `\n共 ${offendingLines.length} 筆。修法：找到報錯檔案裡的 \`const { ... } = useXxx()\`，\n` +
      '把樣板實際用到的識別字都加進解構清單。（其餘型別錯誤不在這支腳本的檢查範圍內，\n' +
      '是本專案已知的既有型別債，見 docs/13-blue-whale-site.md §6。）',
  )
  process.exit(1)
}

console.log('✅ check-undefined-template-refs：沒有偵測到未宣告識別字錯誤（TS2339 對元件 proxy 型別／TS2304／TS2552）。')
process.exit(0)
