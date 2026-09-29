// app/composables/useHomeSections.ts — 首頁九大區塊開關（B3 首頁編排，S1-14）
//
// 對應 apps/api `GET /api/v1/{club}/home-sections`（Features/Home/HomeEndpoints.cs）：
// 後台可以個別開關／排序九個區塊（db/seed 的 HOME_SECTIONS 九個代碼：hero／core_values／
// ecosystem_nav／upcoming_match／recent_fixtures／latest_news／partner_logos／shop_entry／
// bottom_cta，與規劃書 §3.1 首頁九大區塊表格逐列對應）。
//
// 🔴 只做「開關」，不做「動態排序」：兩俱樂部種子資料的 sort_order 目前恰好與
// zh/index.vue 樣板現有的區塊順序一致（db/seed/generate-club-seed-sql.py 的 HOME_SECTIONS
// 陣列順序），所以本輪只依 API 回傳的 isEnabled 決定該區塊要不要渲染，DOM 順序仍是樣板
// 寫死的固定順序。如果後台之後真的把某個區塊排到不同順序，畫面不會跟著動——這是已知的
// 範圍縮減，留給之後如果後台真的開放拖曳排序時再處理（見 apps/web/README.md「S1-14」節）。
//
// 🔴 Fail-open：API 失敗或回傳空陣列時，所有區塊視為啟用（回傳 true）。九個區塊都是
// 「錦上添花」的呈現開關，不是權限或安全機制，API 打不到時讓首頁退回「全部顯示」比
// 「全部隱藏」更不容易造成前台看起來像壞掉。
export interface HomeSectionState {
  sectionCode: string
  isEnabled: boolean
  sortOrder: number
  featuredBannerId: string | null
}

export function useHomeSections(club: string) {
  const { data } = useFetch<HomeSectionState[]>(`/api/backend/${club}/home-sections`)

  const sectionMap = computed(() => new Map((data.value ?? []).map((s) => [s.sectionCode, s])))

  function isSectionEnabled(code: string): boolean {
    const section = sectionMap.value.get(code)
    return section ? section.isEnabled : true
  }

  return { sections: data, isSectionEnabled }
}
