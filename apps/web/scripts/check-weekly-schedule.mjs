#!/usr/bin/env node
/**
 * check-weekly-schedule.mjs — `sessions.weekly_schedule`（JSON）格式化函式的固定資料驗證。
 * 純函式單元檢查，不需要 apps/api，可掛進 `npm run lint`。離開碼：失敗 1／通過 0。
 */
import { readFileSync } from 'node:fs'
import { formatWeeklySchedule } from '../shared/utils/weekly-schedule.ts'

// 測資的唯一來源是 shared/weekly-schedule-cases.json（iOS／Android 同步同一份）。
const casesFile = JSON.parse(readFileSync(new URL('../../../shared/weekly-schedule-cases.json', import.meta.url), 'utf8'))
const cases = casesFile.cases.map((c) => [c.input, c.locale, c.expected])

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
