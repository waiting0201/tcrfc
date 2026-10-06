// app/composables/useHomeSections.ts — 首頁九大區塊開關（B3 首頁編排，S1-14）
//
// 對應 apps/api `GET /api/v1/{club}/home-sections`（Features/Home/HomeEndpoints.cs）：
// 後台可以個別開關／排序九個區塊（db/seed 的 HOME_SECTIONS 九個代碼：hero／core_values／
// ecosystem_nav／upcoming_match／recent_fixtures／latest_news／partner_logos／shop_entry／
// bottom_cta，與規劃書 §3.1 首頁九大區塊表格逐列對應）。
//
// 排序（B-14）：後台排序清單只有九個代碼，頁面上的區塊卻有十個（「一線隊球員橫幅」沒有自己的代碼）。
// `orderBlocks()` 以頁面預設順序為底，有代碼的區塊依 `sortOrder` 排；沒有代碼的區塊（球員橫幅）
// 與不在排序清單內的區塊，維持「緊跟在預設順序前一個區塊後面」的相對位置。API 失敗或空陣列＝預設順序。
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

  /** 明確開啟才算開：API 沒回、或沒有這個代碼一律視為關（不走 fail-open）。藍鯨核心價值用。 */
  function isSectionExplicitlyEnabled(code: string): boolean {
    return sectionMap.value.get(code)?.isEnabled === true
  }

  /** 後台為「精選輪播」指定的橫幅 id（只有 hero 區塊可能有值）。 */
  const featuredBannerId = computed(() => sectionMap.value.get('hero')?.featuredBannerId ?? null)

  /**
   * 依後台排序重排頁面區塊。`defaults` 是頁面原本由上到下的區塊清單，`codes` 為該區塊對應的後台代碼
   * （可多個，取最小排序；空陣列＝沒有自己的代碼，跟著前一個區塊走）。
   */
  function orderBlocks<K extends string>(defaults: ReadonlyArray<{ key: K, codes: readonly string[] }>): K[] {
    let lastKey = -1
    const keyed = defaults.map((b, idx) => {
      const orders = b.codes.map((c) => sectionMap.value.get(c)?.sortOrder).filter((n): n is number => typeof n === 'number')
      const own = orders.length ? Math.min(...orders) : null
      const sortKey = own ?? lastKey + 0.001
      if (own !== null) lastKey = own
      else lastKey = sortKey
      return { key: b.key, sortKey, idx }
    })
    return keyed.sort((a, b) => a.sortKey - b.sortKey || a.idx - b.idx).map((x) => x.key)
  }

  return { sections: data, isSectionEnabled, isSectionExplicitlyEnabled, featuredBannerId, orderBlocks }
}
