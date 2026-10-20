// app/utils/schedule-fetch.ts — 賽程 API 的分頁取回（賽事行事曆頁用）
//
// 後端 `GET /{club}/schedule` 單頁上限 100 筆。過去一次 `pageSize=200` 會被截成 100 筆「最舊」的，
// 賽果因此消失。現在賽果（`status=played&order=desc`）與賽程（`status=scheduled,live,postponed,cancelled&order=asc`）
// 各自查詢、各自逐頁取到取完或達到頁數上限為止；被截斷時保留的是「最接近現在」的那一端
// （賽果＝最新、賽程＝最近），不是最舊。

/** 賽果查詢的 `status` 參數（一律搭配 `order=desc`）。字面值集中在這裡，頁面不要自己再寫一份（check-match-status）。 */
export const PLAYED_QUERY_STATUS = 'played'
/** 賽程查詢的 `status` 參數（未完賽的全部狀態，搭配 `order=asc`）。 */
export const UPCOMING_QUERY_STATUSES = 'scheduled,live,postponed,cancelled'
export const MATCH_PAGE_SIZE = 100
/** 每條查詢最多取幾頁（防呆）：100 × 5 = 500 場，遠大於一個俱樂部多季多隊的實際量。 */
export const MATCH_MAX_PAGES = 5

export interface MatchPagesResult<T> {
  items: T[]
  totalCount: number
  /** 達到頁數上限仍有更多資料 */
  truncated: boolean
}

export async function fetchMatchPages<T>(
  club: string,
  query: Record<string, string | number>,
  maxPages: number = MATCH_MAX_PAGES,
): Promise<MatchPagesResult<T>> {
  const items: T[] = []
  let totalCount = 0
  for (let page = 1; page <= maxPages; page++) {
    const res = await $fetch<{ items?: T[]; totalCount?: number; pageSize?: number }>(`/api/backend/${club}/schedule`, {
      query: { ...query, page, pageSize: MATCH_PAGE_SIZE },
    })
    items.push(...(res.items ?? []))
    totalCount = res.totalCount ?? items.length
    if ((res.items ?? []).length === 0 || items.length >= totalCount) break
  }
  return { items, totalCount, truncated: items.length < totalCount }
}
