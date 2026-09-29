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
//
// 🔴 S1-15（2026-09-29）新增細粒度頁面代號 '4.7'／'5.1'／'5.2'：
// - '4.7'（加入學院）：藍鯨規劃書 §3.4「04 青年隊沿用主站 04 的梯隊版型，
//   但不沿用招生與課程報名架構」——4.7 整頁就是磐石的招生流程與費用表，
//   不是「配色不同」能涵蓋的差異，屬於規劃書明文排除的內容，故關閉。
// - '5.1'／'5.2'（兒童足球訓練／夏令營）：藍鯨自己的「05 推廣活動」是完全不同的
//   活動集合（社區與學校推廣、足球節、藍鯨盃，見藍鯨規劃書 §3.5），不是磐石
//   課程頁換個配色就能沿用的內容。此頁面在改動前對兩俱樂部皆無任何俱樂部分支
//   （0 筆 isTcrfc／clubKey 判斷），代表藍鯨容器過去會直接顯示磐石課程內容——
//   這是本輪盤點時發現的既有缺口，不是新引入的迴歸，關閉後改回誠實的 404
//   （見 apps/web/scripts/check-club-brand-leak.mjs 對 404 的既有豁免）。
// ⚠️ 同一批課程頁面裡的 '5.3'／'5.4'／'5.5'（冬令營／專項訓練／校園社區）現況
//   相同（0 筆俱樂部分支），但不在本輪任務範圍（STATUS.md S1-15 只列 5.1／5.2，
//   5.3–5.5 排在 S2-10），刻意不在此一併關閉——留給 S2-10 一併評估與修正，
//   避免本輪「順手」擴大範圍卻沒有對應驗收。
const BLUE_WHALE_DISABLED_UNITS: readonly string[] = ['06', '11', '4.7', '5.1', '5.2']

export function isUnitEnabledForClub(unitCode: string, club: string): boolean {
  const normalizedClub: ClubCode = club === 'bw' ? 'bw' : 'tcrfc'
  if (normalizedClub === 'bw' && BLUE_WHALE_DISABLED_UNITS.includes(unitCode)) {
    return false
  }
  return true
}
