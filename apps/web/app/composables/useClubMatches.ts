// app/composables/useClubMatches.ts — 某俱樂部（可限定單一隊別）的全部賽事，賽果與賽程分開查再合併（由舊到新）。
//
// 過去各頁用單次 `pageSize=200`，後端單頁上限 100，會被截成最舊的 100 筆而漏掉新賽果。
// 這裡賽果 `status=played&order=desc`、賽程 `status=scheduled,live,postponed,cancelled&order=asc` 各自逐頁取回
// （見 app/utils/schedule-fetch.ts），回傳 `{ items }` 形狀的 ref，可直接餵給 `hasFallbackLocale()`。
export async function useClubMatches(club: string, lang: string, team?: string) {
  const { data } = await useAsyncData(`club-matches-${club}-${lang}-${team ?? 'all'}`, async () => {
    const base = { ...(team ? { team } : {}), lang }
    const [played, upcoming] = await Promise.all([
      fetchMatchPages<ListedMatch>(club, { ...base, status: PLAYED_QUERY_STATUS, order: 'desc' }),
      fetchMatchPages<ListedMatch>(club, { ...base, status: UPCOMING_QUERY_STATUSES, order: 'asc' }),
    ])
    const items = [...played.items, ...upcoming.items].sort((a, b) =>
      a.matchOn === b.matchOn ? (a.kickoff ?? '').localeCompare(b.kickoff ?? '') : a.matchOn.localeCompare(b.matchOn))
    return { items }
  })
  return data
}
