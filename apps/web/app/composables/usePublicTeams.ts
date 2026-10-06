// app/composables/usePublicTeams.ts — 公開球隊清單（C1）：`GET /{club}/teams?lang=`，依後台排序（sort_order, code）（B-10，2026-10-06）
// API 失敗或沒有任何球隊 → 空陣列，頁面回退既有的 club-copy／site-facts 常數，不出 500。
export async function usePublicTeams() {
  const config = useRuntimeConfig()
  const club = config.public.club
  const { locale } = useLocale()
  const { data } = await useFetch<PublicTeamView[]>(`/api/backend/${club}/teams`, {
    query: { lang: locale.value },
    key: `public-teams-${club}-${locale.value}`,
    default: () => [],
  })
  const teams = computed(() => data.value ?? [])
  const academyTeams = computed(() => teams.value.filter((t) => t.type === 'academy'))
  const firstTeam = computed(() => teams.value.find((t) => t.type === 'first_team') ?? null)
  return { teams, academyTeams, firstTeam }
}
