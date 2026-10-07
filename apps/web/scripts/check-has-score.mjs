#!/usr/bin/env node
/**
 * check-has-score.mjs — 驗證 `hasScore()`（app/utils/schedule.ts）與「已完賽但沒有比分」的前台防呆。
 * 背景（2026-10-07）：藍鯨 25/26 木蘭聯賽 21 場為 played，但 score_home／score_away 皆 NULL，
 * 畫面不得印出 ` : `、null、undefined 或 0 : 0。
 */
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { hasScore } from '../app/utils/schedule.ts'

assert.equal(hasScore({ scoreHome: 2, scoreAway: 1 }), true)
assert.equal(hasScore({ scoreHome: 0, scoreAway: 0 }), true, '真的 0:0 是有比分')
assert.equal(hasScore({ scoreHome: null, scoreAway: null }), false)
assert.equal(hasScore({ scoreHome: 1, scoreAway: null }), false)
assert.equal(hasScore({ scoreHome: null, scoreAway: 1 }), false)
assert.equal(hasScore({}), false)
assert.equal(hasScore(null), false)
assert.equal(hasScore(undefined), false)

// 靜態檢查：模板裡印比分的行必須同時有 hasScore 把關
const pages = ['app/pages/zh/club/first-team/index.vue', 'app/pages/zh/academy/teams.vue']
for (const p of pages) {
  const lines = readFileSync(new URL(`../${p}`, import.meta.url), 'utf8').split('\n')
  lines.forEach((l, i) => {
    if (/\{\{\s*m\.scoreHome\s*\}\}/.test(l) && !/hasScore\(m\)/.test(l)) {
      throw new Error(`${p}:${i + 1} 印比分但沒有 hasScore(m) 把關`)
    }
  })
}
const home = readFileSync(new URL('../app/pages/zh/index.vue', import.meta.url), 'utf8')
assert.match(home, /status === 'played'[^\n]*hasScore\(m\)/, '首頁 d1Played 必須以 hasScore 篩選')
console.log('check-has-score: OK')
