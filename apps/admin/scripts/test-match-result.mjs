#!/usr/bin/env node
/** C4 賽事編輯純函式（`src/utils/matchResult.ts`）的單元測試，做法同 test-news-blocks.mjs。 */
import assert from 'node:assert/strict'
import { fileURLToPath } from 'node:url'
import { createServer } from 'vite'

const ROOT = fileURLToPath(new URL('..', import.meta.url))
const server = await createServer({ root: ROOT, configFile: false, optimizeDeps: { noDiscovery: true, include: [] }, logLevel: 'error', appType: 'custom', server: { middlewareMode: true }, resolve: { alias: { '@': `${ROOT}src` } } })

let failed = 0
function test(name, fn) {
  try {
    fn()
    console.log(`  ok  ${name}`)
  } catch (error) {
    failed++
    console.error(`  FAIL  ${name}\n${error.stack}`)
  }
}

try {
  const M = await server.ssrLoadModule('/src/utils/matchResult.ts')
  const base = { status: 'scheduled', matchOn: '2026-10-19', scoreHome: 2, scoreAway: 1 }
  const today = '2026-10-20'

  test('已填比分、未開始、日期已到 → 詢問', () => assert.equal(M.shouldOfferMarkPlayed(base, today), true))
  test('當天比賽也詢問', () => assert.equal(M.shouldOfferMarkPlayed({ ...base, matchOn: today }, today), true))
  test('未來日期不詢問', () => assert.equal(M.shouldOfferMarkPlayed({ ...base, matchOn: '2026-10-21' }, today), false))
  test('只填一邊比分不詢問', () => assert.equal(M.shouldOfferMarkPlayed({ ...base, scoreAway: null }, today), false))
  test('0:0 算有比分', () => assert.equal(M.shouldOfferMarkPlayed({ ...base, scoreHome: 0, scoreAway: 0 }, today), true))
  test('已結束／延賽等其他狀態不詢問', () => {
    for (const status of ['played', 'live', 'postponed', 'cancelled']) assert.equal(M.shouldOfferMarkPlayed({ ...base, status }, today), false)
  })
  test('沒有日期不詢問', () => assert.equal(M.shouldOfferMarkPlayed({ ...base, matchOn: '' }, today), false))

  const none = new Set()
  const scorer = new Set(['p1'])
  test('先發恆為 false', () => assert.equal(M.effectiveCameOn({ playerId: 'p1', isStarter: true, cameOn: true }, scorer), false))
  test('替補勾選 → true', () => assert.equal(M.effectiveCameOn({ playerId: 'p2', isStarter: false, cameOn: true }, none), true))
  test('替補未勾選 → false', () => assert.equal(M.effectiveCameOn({ playerId: 'p2', isStarter: false, cameOn: false }, none), false))
  test('替補有進球或吃牌 → 強制 true', () => assert.equal(M.effectiveCameOn({ playerId: 'p1', isStarter: false, cameOn: false }, scorer), true))
} finally {
  await server.close()
}

if (failed > 0) {
  console.error(`${failed} 項失敗`)
  process.exit(1)
}
console.log('全部通過')
