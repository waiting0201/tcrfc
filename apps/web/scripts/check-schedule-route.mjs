#!/usr/bin/env node
/**
 * check-schedule-route.mjs — `/schedule/{參數}` 的外流網址相容（STATUS CF-6，主站 v3.29，磐石學院梯隊改為 U10／U12／U14／U16／U18）。
 *
 * 要守住的事（docs/14 隊別代號全站唯一、docs/19 §2 深連結回退規則）：
 *   1. 磐石站：`d1`／`u14`／`u12` 的網址維持原樣（解析成隊別），`u10`／`u16`／`u18` 是新隊別；
 *      舊制才有的 `u15` 磐石不存在 → 解析成 unknown，頁面 302 回 `/schedule/`（不是 404、不崩潰）。
 *   2. 藍鯨站：`bw1`／`bw-u15`／`bw-u12` 維持原樣；磐石的代號（`d1`、`u14`…）藍鯨不認得 → unknown。
 *   3. 磐石站不認得 `bw1`、藍鯨站不認得 `d1`。
 * 隊別分頁清單用 `getAcademyTeamTabs()`（和 schedule.vue 的 fallback 同一來源），不另外手寫一份。
 * 純函式檢查，不需要 apps/api，掛進 `npm run lint`。離開碼：失敗 1／通過 0。
 */
import { resolveScheduleSlug } from '../shared/utils/schedule-route.ts'
import { getAcademyTeamTabs } from '../shared/utils/club-copy.ts'
import { getSiteFacts } from '../shared/utils/site-facts.ts'

let failed = 0
const eq = (actual, expected, label) => {
  const ok = JSON.stringify(actual) === JSON.stringify(expected)
  if (!ok) failed++
  console.log(`${ok ? '✓' : '✗'} ${label}${ok ? '' : `\n    期望 ${JSON.stringify(expected)}\n    實際 ${JSON.stringify(actual)}`}`)
}

/** 與 schedule.vue fallback 相同的組法：一線隊 ＋ 學院（青年）隊別分頁。 */
function tabsOf(club) {
  const first = club === 'bw' ? 'BW1' : 'D1'
  const youth = getAcademyTeamTabs(club, getSiteFacts(club))
    .filter((t) => t.teamCode !== null)
    .map((t) => ({ id: t.teamCode.toLowerCase(), filter: t.teamCode }))
  return [{ id: 'all', filter: 'all' }, { id: 'first-team', filter: first }, ...youth, { id: 'club', filter: 'club' }]
}
const kindOf = (club, slug) => resolveScheduleSlug(slug, tabsOf(club), [])

eq(getSiteFacts('tcrfc').squadCodes, ['U10', 'U12', 'U14', 'U16', 'U18'], '磐石梯隊代碼（site-facts 備援）＝U10／U12／U14／U16／U18')
for (const slug of ['d1', 'D1', 'u14', 'u12', 'u10', 'u16', 'u18']) {
  eq(kindOf('tcrfc', slug).kind, 'team', `磐石 /schedule/${slug}/ → 隊別`)
}
eq(kindOf('tcrfc', 'u14'), { kind: 'team', filter: 'U14' }, '磐石 u14 仍指向 U14（舊網址不得改指其他球隊）')
eq(kindOf('tcrfc', 'u12'), { kind: 'team', filter: 'U12' }, '磐石 u12 仍指向 U12')
eq(kindOf('tcrfc', 'u15'), { kind: 'unknown' }, '磐石 u15 已不存在 → unknown（頁面 302 回 /schedule/）')
eq(kindOf('tcrfc', 'bw1'), { kind: 'unknown' }, '磐石站不認得藍鯨的 bw1')
eq(kindOf('tcrfc', ''), { kind: 'none' }, '沒有參數 → 一般行事曆首頁')

for (const slug of ['bw1', 'bw-u15', 'bw-u12']) {
  eq(kindOf('bw', slug).kind, 'team', `藍鯨 /schedule/${slug}/ → 隊別`)
}
eq(kindOf('bw', 'd1'), { kind: 'unknown' }, '藍鯨站不認得磐石的 d1')
eq(kindOf('bw', 'u14'), { kind: 'unknown' }, '藍鯨站不認得磐石的 u14')

const MATCH_ID = '3f2b8c1e-5a6d-4e7f-9a0b-1c2d3e4f5a6b'
eq(resolveScheduleSlug(MATCH_ID, tabsOf('tcrfc'), []), { kind: 'match', matchId: MATCH_ID }, 'UUID → 賽事（不需要列表裡找得到，改由頁面打詳情端點確認）')
eq(resolveScheduleSlug(MATCH_ID.toUpperCase(), tabsOf('bw')).kind, 'match', '大寫 UUID 亦視為賽事')
eq(resolveScheduleSlug('not-a-uuid', tabsOf('tcrfc'), []), { kind: 'unknown' }, '非 UUID 的未知參數 → unknown')

if (failed) {
  console.error(`\ncheck-schedule-route 失敗：${failed} 項`)
  process.exit(1)
}
console.log('\ncheck-schedule-route 通過')
