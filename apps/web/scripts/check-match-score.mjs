#!/usr/bin/env node
/**
 * check-match-score.mjs — 比分語意與賽事顯示工具（app/utils/schedule.ts）的純函式驗證（主站 v3.31 C4，E-342）。
 * 🔴 `scoreHome`／`scoreAway` ＝ 我方進球／對方進球，不是主隊／客隊：
 *    主場（HOME）我方在左；客場（AWAY）對手在左，比分對調；大小寫不拘；主客場未填一律我方在前。
 * 另驗：進球者摘要、先發／替補切分、輪次標籤（null 不印「第 null 輪」）、預設賽季（當季，不是字典序最大）、
 * 以及頁面不得把 scoreHome 當主隊比分直接印出。離開碼：失敗 1／通過 0。
 */
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import {
  haCode, isAwayMatch, fixtureId, matchScoreDisplay, matchScoreText, ourScorers, groupScorers, splitLineup,
  roundLabel, pickDefaultSeason, sortSeasonsNewestFirst,
} from '../app/utils/schedule.ts'

let failed = 0
const t = (name, fn) => {
  try { fn(); console.log('✓', name) }
  catch (e) { failed++; console.error(`✗ ${name}\n    ${e.message}`) }
}

t('主場：我方進球在左（3 : 1）', () => {
  assert.equal(matchScoreText({ scoreHome: 3, scoreAway: 1, homeAway: 'HOME' }), '3 : 1')
  assert.deepEqual(matchScoreDisplay({ scoreHome: 3, scoreAway: 1, homeAway: 'HOME' }), { left: 3, right: 1, ours: 3, theirs: 1, ourSide: 'left' })
})
t('客場：對手在左，比分對調（我方 3、對方 1 → 1 : 3）', () => {
  assert.equal(matchScoreText({ scoreHome: 3, scoreAway: 1, homeAway: 'AWAY' }), '1 : 3')
  assert.equal(matchScoreDisplay({ scoreHome: 3, scoreAway: 1, homeAway: 'AWAY' })?.ourSide, 'right')
})
t('主客場大小寫不拘（home／Away）', () => {
  assert.equal(matchScoreText({ scoreHome: 2, scoreAway: 0, homeAway: 'home' }), '2 : 0')
  assert.equal(matchScoreText({ scoreHome: 2, scoreAway: 0, homeAway: 'Away' }), '0 : 2')
  assert.equal(haCode('home'), 'home')
  assert.equal(haCode(' HOME '), 'home')
  assert.equal(haCode('AWAY'), 'away')
  assert.equal(isAwayMatch('away'), true)
})
t('主客場未填：我方在前（不當客場）', () => {
  assert.equal(matchScoreText({ scoreHome: 2, scoreAway: 1, homeAway: null }), '2 : 1')
  assert.equal(isAwayMatch(null), false)
})
t('0 : 0 是真的比分；任一為 null 沒有比分', () => {
  assert.equal(matchScoreText({ scoreHome: 0, scoreAway: 0, homeAway: 'AWAY' }), '0 : 0')
  assert.equal(matchScoreText({ scoreHome: null, scoreAway: null, homeAway: 'HOME' }), null)
  assert.equal(matchScoreText({ scoreHome: 1, scoreAway: null, homeAway: 'HOME' }), null)
  assert.equal(matchScoreText(null), null)
})
t('fixtureId 的主客場字母不分大小寫', () => {
  assert.equal(fixtureId('2026-09-13', 'AWAY', 3), 'fx-2026-09-13-a-3')
  assert.equal(fixtureId('2026-09-13', 'home', 3), 'fx-2026-09-13-h-3')
})
t('進球者摘要：只算我方進球、同球員合併依時間排序、null 球員不列名、烏龍（side=opponent）不算我方', () => {
  const goals = [
    { minute: 70, playerName: '乙', side: 'club', goalType: null },
    { minute: 12, playerName: '甲', side: 'club', goalType: 'penalty' },
    { minute: 55, playerName: '甲', side: 'club', goalType: null },
    { minute: 30, playerName: null, side: 'club', goalType: null },
    { minute: 40, playerName: '丙', side: 'opponent', goalType: 'own_goal' },
    { minute: 80, playerName: null, side: 'opponent', goalType: null },
  ]
  const r = ourScorers(goals)
  assert.deepEqual(r.map((x) => x.name), ['甲', '乙'])
  assert.deepEqual(r[0].minutes, [12, 55])
  assert.deepEqual(ourScorers(null), [])
})
t('列表 scorers 摘要：合併同球員、依時間排序、null 球員不列名、空值安全', () => {
  const r = groupScorers([
    { minute: 60, playerName: '乙', goalType: null }, { minute: 10, playerName: '甲', goalType: 'penalty' },
    { minute: 80, playerName: '乙', goalType: null }, { minute: 20, playerName: null, goalType: null },
  ])
  assert.deepEqual(r.map((x) => x.name), ['甲', '乙'])
  assert.deepEqual(r[1].minutes, [60, 80])
  assert.deepEqual(groupScorers(undefined), [])
})
t('先發／替補切分：替補依球衣號碼排序，上場與未上場混排', () => {
  const l = [
    { isStarter: true, cameOn: false, shirtNumber: 9 },
    { isStarter: false, cameOn: true, shirtNumber: 45 },
    { isStarter: false, cameOn: false, shirtNumber: 4 },
    { isStarter: false, cameOn: false, shirtNumber: null },
    { isStarter: false, cameOn: true, shirtNumber: 5 },
  ]
  const r = splitLineup(l)
  assert.equal(r.starters.length, 1)
  assert.deepEqual(r.subs.map((p) => p.shirtNumber), [4, 5, 45, null])
  assert.deepEqual(splitLineup(undefined), { starters: [], subs: [] })
})
t('輪次：null／0 不輸出（不得印「第 null 輪」）', () => {
  assert.equal(roundLabel(null, false), null)
  assert.equal(roundLabel(undefined, true), null)
  assert.equal(roundLabel(0, false), null)
  assert.equal(roundLabel(5, false), '第 5 輪')
  assert.equal(roundLabel(5, true), 'Round 5')
})
t('預設賽季：今天所在的賽季；不在任何賽季內取賽事日期最晚者（不是代碼字典序最大）', () => {
  const ms = [
    { seasonCode: '2025', matchOn: '2025-03-01' }, { seasonCode: '2025', matchOn: '2025-11-30' },
    { seasonCode: '2026-27', matchOn: '2026-09-05' }, { seasonCode: '2026-27', matchOn: '2027-05-20' },
  ]
  assert.equal(pickDefaultSeason(ms, '2026-10-20'), '2026-27')
  assert.equal(pickDefaultSeason(ms, '2025-06-01'), '2025')
  assert.equal(pickDefaultSeason(ms, '2026-03-01'), '2026-27', '空窗期取日期最晚（最新）的賽季')
  // 代碼字典序 '2026' > '2025-26'，但時間上 2025-26 賽季才是當季
  const tricky = [
    { seasonCode: '2026', matchOn: '2026-01-10' }, { seasonCode: '2026', matchOn: '2026-02-01' },
    { seasonCode: '2025-26', matchOn: '2025-08-01' }, { seasonCode: '2025-26', matchOn: '2026-06-30' },
  ]
  assert.equal(pickDefaultSeason(tricky, '2026-06-15'), '2025-26')
  assert.equal(pickDefaultSeason([], '2026-10-20'), 'all')
  assert.deepEqual(sortSeasonsNewestFirst(ms), ['2026-27', '2025'])
})
t('頁面不得把 scoreHome 當主隊比分直接印出（必須經 matchScoreText／scoreOf）', () => {
  const pages = ['app/pages/zh/schedule.vue', 'app/pages/zh/club/first-team/index.vue', 'app/pages/zh/academy/teams.vue', 'app/pages/zh/news/[slug]/index.vue']
  for (const p of pages) {
    const src = readFileSync(new URL(`../${p}`, import.meta.url), 'utf8')
    assert.ok(!/\{\{[^}]*\.scoreHome[^}]*\}\}/.test(src), `${p} 直接印出 scoreHome`)
    assert.ok(!/\$\{[^}]*\.scoreHome[^}]*\}/.test(src), `${p} 以樣板字串直接印出 scoreHome`)
  }
  const sched = readFileSync(new URL('../app/pages/zh/schedule.vue', import.meta.url), 'utf8')
  assert.ok(!/prefetchScorers/.test(sched), '賽果卡片進球者改讀列表 scorers，不得再背景預取詳情')
  assert.ok(!/pageSize:\s*200/.test(sched), 'schedule.vue 不得再用單次 pageSize=200（會被截成最舊 100 筆）')
})

if (failed) { console.error(`\ncheck-match-score 失敗：${failed} 項`); process.exit(1) }
console.log('\ncheck-match-score 通過')
