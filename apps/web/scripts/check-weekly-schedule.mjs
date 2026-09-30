#!/usr/bin/env node
/**
 * check-weekly-schedule.mjs — `sessions.weekly_schedule`（JSON）格式化函式的固定資料驗證。
 * 純函式單元檢查，不需要 apps/api，可掛進 `npm run lint`。離開碼：失敗 1／通過 0。
 */
import { formatWeeklySchedule } from '../shared/utils/weekly-schedule.ts'

const cases = [
  // [輸入, 語系, 期望輸出]
  ['{"mon":"18:00-19:30","wed":"18:00-19:30"}', 'zh', '週一 18:00–19:30、週三 18:00–19:30'],
  ['{"mon":"18:00-19:30","wed":"18:00-19:30"}', 'en', 'Mon 18:00–19:30, Wed 18:00–19:30'],
  ['{"wed":"18:00-19:30","mon":"9:00-10:30"}', 'zh', '週一 09:00–10:30、週三 18:00–19:30'], // 依星期排序、補零
  ['{"mon-fri":"09:00-16:00"}', 'zh', '週一至週五 09:00–16:00'],
  ['{"mon-fri":"09:00-16:00"}', 'en', 'Mon–Fri 09:00–16:00'],
  ['{"sat":"09:00-11:00"}', 'zh', '週六 09:00–11:00'],
  ['{"mon":"1.5 小時","wed":"1.5 小時"}', 'zh', '週一 1.5 小時、週三 1.5 小時'],
  ['{"Monday":["18:00-19:00","19:30-20:30"]}', 'en', 'Mon 18:00–19:00, Mon 19:30–20:30'],
  ['{"mon,wed":"18:00-19:30"}', 'zh', '週一 18:00–19:30、週三 18:00–19:30'],
  [null, 'zh', null],
  ['', 'zh', null],
  ['null', 'zh', null],
  ['{}', 'zh', null],
  // 無法解析：一律 null，且不得含原始 JSON 括號
  ['not json', 'zh', null],
  ['[1,2]', 'zh', null],
  ['"每週一"', 'zh', null],
  ['{"foo":"18:00-19:30"}', 'zh', null],
  ['{"mon":123}', 'zh', null],
  ['{"mon":"25:00-26:00"}', 'zh', null],
  ['{"fri-mon":"18:00-19:30"}', 'zh', null],
]

let failed = 0
for (const [input, locale, expected] of cases) {
  const warnings = []
  const actual = formatWeeklySchedule(input, locale, (m) => warnings.push(m))
  const leaked = actual !== null && /[{}[\]"]/.test(actual)
  if (actual !== expected || leaked) {
    failed++
    console.error(`FAIL [${locale}] ${JSON.stringify(input)}\n  期望 ${JSON.stringify(expected)}\n  實得 ${JSON.stringify(actual)}`)
  }
}
// 警告只在「有值卻解析不了」時發出
const w1 = []
formatWeeklySchedule('not json', 'zh', (m) => w1.push(m))
const w2 = []
formatWeeklySchedule(null, 'zh', (m) => w2.push(m))
formatWeeklySchedule('{"mon":"18:00-19:30"}', 'zh', (m) => w2.push(m))
if (w1.length !== 1 || w2.length !== 0) { failed++; console.error('FAIL 警告觸發條件不符') }

if (failed) { console.error(`\ncheck-weekly-schedule: ${failed} 項失敗`); process.exit(1) }
console.log(`check-weekly-schedule: ${cases.length} 組輸入＋警告條件全部通過`)
