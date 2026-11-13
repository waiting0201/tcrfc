#!/usr/bin/env node
/**
 * check-player-position.mjs — 球員位置正規化與中英標籤（audit W-6）。
 * 值域 GK／DF／MF／FW；後台改下拉前的自由文字舊值（門將、守門員、後衛、中場、前鋒）也要認得。
 * 英文頁認不得的值不得輸出中日文字元。離開碼：失敗 1／通過 0。
 */
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { stripTypeScriptTypes } from 'node:module'
import { normalizePlayerPosition, playerPositionLabelZh } from '../shared/utils/player.ts'

let failed = 0
const t = (name, fn) => {
  try { fn(); console.log('✓', name) }
  catch (e) { failed++; console.error(`✗ ${name}\n    ${e.message}`) }
}

// clubPositionLabelEn 在 club-copy-en-club.ts（相對匯入沒有副檔名，node 直接 import 不了）：抽出函式本體、去型別後執行。
const src = readFileSync(new URL('../shared/utils/club-copy-en-club.ts', import.meta.url), 'utf8')
const m = /export function clubPositionLabelEn[\s\S]*?\n}\n/.exec(src)
assert.ok(m, '找不到 clubPositionLabelEn')
const clubPositionLabelEn = new Function('normalizePlayerPosition', `${stripTypeScriptTypes(m[0]).replace('export function', 'function')}; return clubPositionLabelEn`)(normalizePlayerPosition)

t('代碼大小寫不拘、去空白', () => {
  assert.equal(normalizePlayerPosition('GK'), 'GK')
  assert.equal(normalizePlayerPosition('gk'), 'GK')
  assert.equal(normalizePlayerPosition(' fw '), 'FW')
})
t('中文舊值轉代碼', () => {
  for (const [zh, code] of [['門將', 'GK'], ['守門員', 'GK'], ['後衛', 'DF'], ['中場', 'MF'], ['前鋒', 'FW']]) assert.equal(normalizePlayerPosition(zh), code)
})
t('認不得的值原樣回傳；空值回空字串', () => {
  assert.equal(normalizePlayerPosition('邊鋒'), '邊鋒')
  assert.equal(normalizePlayerPosition(null), '')
  assert.equal(normalizePlayerPosition(undefined), '')
})
t('中文標籤', () => {
  assert.equal(playerPositionLabelZh('GK'), '門將')
  assert.equal(playerPositionLabelZh('df'), '後衛')
  assert.equal(playerPositionLabelZh('守門員'), '門將')
  assert.equal(playerPositionLabelZh('邊鋒'), '邊鋒')
  assert.equal(playerPositionLabelZh(null), '—')
  assert.equal(playerPositionLabelZh('  ', ''), '')
})
t('英文標籤：代碼與中文舊值都轉英文全名', () => {
  assert.equal(clubPositionLabelEn('GK'), 'Goalkeeper')
  assert.equal(clubPositionLabelEn('mf'), 'Midfielder')
  assert.equal(clubPositionLabelEn('前鋒'), 'Forward')
  assert.equal(clubPositionLabelEn('門將'), 'Goalkeeper')
})
t('英文標籤：認不得的中文值不輸出中文；空值破折號；拉丁字原樣', () => {
  assert.equal(clubPositionLabelEn('邊鋒'), '—')
  assert.equal(clubPositionLabelEn(null), '—')
  assert.equal(clubPositionLabelEn('Winger'), 'Winger')
})

if (failed) { console.error(`\ncheck-player-position 失敗：${failed} 項`); process.exit(1) }
console.log('\ncheck-player-position 通過')
