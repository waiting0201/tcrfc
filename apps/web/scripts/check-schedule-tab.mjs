#!/usr/bin/env node
/**
 * check-schedule-tab.mjs — 賽事分頁歸屬（matchTabOf）、台灣日期鍵、課程早鳥價有效判斷的純函式驗證（audit W-1／W-8）。
 * 賽程頁原本只收「未開始」／「已結束」，延賽、取消、進行中兩個分頁都看不到；早鳥價原本不看截止日。
 * 離開碼：失敗 1／通過 0。
 */
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { matchTabOf, taipeiDateKey } from '../app/utils/schedule.ts'
import { isEarlyBirdActive, sessionPriceView } from '../app/utils/program-session.ts'

let failed = 0
const t = (name, fn) => {
  try { fn(); console.log('✓', name) }
  catch (e) { failed++; console.error(`✗ ${name}\n    ${e.message}`) }
}
const TODAY = '2026-11-13'

t('未開始、進行中、延賽 → 賽程（延賽原定日已過也留在賽程）', () => {
  assert.equal(matchTabOf('scheduled', '2026-12-01', TODAY), 'fixtures')
  assert.equal(matchTabOf('live', TODAY, TODAY), 'fixtures')
  assert.equal(matchTabOf('postponed', '2026-12-01', TODAY), 'fixtures')
  assert.equal(matchTabOf('postponed', '2026-10-01', TODAY), 'fixtures')
})
t('已結束 → 賽果', () => {
  assert.equal(matchTabOf('played', '2026-10-01', TODAY), 'results')
})
t('取消：今天或未來 → 賽程；已過 → 賽果', () => {
  assert.equal(matchTabOf('cancelled', '2026-12-01', TODAY), 'fixtures')
  assert.equal(matchTabOf('cancelled', TODAY, TODAY), 'fixtures')
  assert.equal(matchTabOf('cancelled', '2026-11-12', TODAY), 'results')
})
t('未知或空狀態沿用預設（賽程）', () => {
  assert.equal(matchTabOf(null, '2026-12-01', TODAY), 'fixtures')
  assert.equal(matchTabOf('weird', '2026-12-01', TODAY), 'fixtures')
})
t('taipeiDateKey 用 UTC+8：UTC 前一天 20:00 已是台灣隔天', () => {
  assert.equal(taipeiDateKey(new Date('2026-11-12T20:00:00Z')), '2026-11-13')
  assert.equal(taipeiDateKey(new Date('2026-11-12T15:59:59Z')), '2026-11-12')
})
t('早鳥：截止日當天仍有效、隔天失效', () => {
  const s = { price: 1000, earlyBirdPrice: 800, earlyBirdUntil: '2026-11-13' }
  assert.equal(isEarlyBirdActive(s, '2026-11-13'), true)
  assert.equal(isEarlyBirdActive(s, '2026-11-14'), false)
})
t('早鳥：沒填截止日視為有效；沒有早鳥價一律無效', () => {
  assert.equal(isEarlyBirdActive({ price: 1000, earlyBirdPrice: 800, earlyBirdUntil: null }, TODAY), true)
  assert.equal(isEarlyBirdActive({ price: 1000, earlyBirdPrice: null, earlyBirdUntil: '2099-01-01' }, TODAY), false)
})
t('價格列：有效時同時給早鳥（含截止日）與正價；失效只給正價；都沒有為 null', () => {
  const live = sessionPriceView({ price: 1000, earlyBirdPrice: 800, earlyBirdUntil: '2026-11-20' }, TODAY)
  assert.deepEqual(live, { early: { price: 800, until: '2026/11/20' }, regular: 1000 })
  const expired = sessionPriceView({ price: 1000, earlyBirdPrice: 800, earlyBirdUntil: '2026-11-01' }, TODAY)
  assert.deepEqual(expired, { early: null, regular: 1000 })
  assert.deepEqual(sessionPriceView({ price: null, earlyBirdPrice: null, earlyBirdUntil: null }, TODAY), { early: null, regular: null })
  assert.deepEqual(sessionPriceView(null, TODAY), { early: null, regular: null })
})
t('頁面不得再用二分法或直接印早鳥價', () => {
  const sched = readFileSync(new URL('../app/pages/zh/schedule.vue', import.meta.url), 'utf8')
  assert.ok(!/cardStatusCode\(m\) === 'finished'\s*[:)]/.test(sched.split('function cardMatches')[1]?.split('\n}')[0] ?? ''), 'cardMatches 不得再用狀態二分法')
  for (const p of ['summer-camp', 'winter-camp']) {
    const src = readFileSync(new URL(`../app/pages/zh/programs/${p}/index.vue`, import.meta.url), 'utf8')
    assert.ok(!/session\?\.earlyBirdPrice/.test(src), `${p} 不得直接印 earlyBirdPrice，改用 sessionPriceView`)
  }
})

if (failed) { console.error(`\ncheck-schedule-tab 失敗：${failed} 項`); process.exit(1) }
console.log('\ncheck-schedule-tab 通過')
