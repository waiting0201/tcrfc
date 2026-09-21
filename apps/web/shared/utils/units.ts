// app/utils/units.ts — 單元開關的單一真實來源（docs/13-blue-whale-site.md §6 紀律 3）
//
// 🔴 route middleware、導覽選單、sitemap、llms.txt／robots.txt 四處都只能呼叫這個函式，
// 不准各自寫一份判斷。四個呼叫點：
//   1. app/middleware/unit-gate.global.ts
//   2. app/components/SiteHeader.vue（導覽選單）
//   3. nuxt.config.ts 的 sitemap.urls（server/utils/site-units.ts 共用同一份清單）
//   4. server/routes/llms[-en].txt.ts（GEO-01）；robots.txt 由 nuxt-robots 的
//      disallow: ['/'] 全站擋（上線前 noindex，CLAUDE.md 第 5 條），
//      待正式期改為完整 GEO 版時，同樣要改成呼叫這個函式，不得另開一份邏輯。
//
// 藍鯨的四項單元取捨見 docs/13-blue-whale-site.md §3：
//   移除 06（女子足球，自我指涉）、11（慈善，主辦是協會與藍鯨無關）；
//   04 由「學院 ACADEMY」改為「青年隊 YOUTH」、09 夥伴須與磐石分區——
//   這兩項是「內容調整」不是「開關」，不在這支函式的管轄範圍內。

import type { ClubCode } from './club'

/** 藍鯨站不設的單元代號（docs/13-blue-whale-site.md §3） */
const BLUE_WHALE_DISABLED_UNITS: readonly string[] = ['06', '11']

export function isUnitEnabledForClub(unitCode: string, club: string): boolean {
  const normalizedClub: ClubCode = club === 'bw' ? 'bw' : 'tcrfc'
  if (normalizedClub === 'bw' && BLUE_WHALE_DISABLED_UNITS.includes(unitCode)) {
    return false
  }
  return true
}
