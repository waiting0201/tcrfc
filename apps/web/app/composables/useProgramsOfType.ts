// app/composables/useProgramsOfType.ts — 05 固定頁 5.1–5.5：某一類型「全部已發布課程」的詳情（B-8，2026-10-06）
//
// 取法：列表（最多 10 個，後端已排序）→ 逐一取詳情（內容、教練團、合作夥伴、梯次）。取代原本「只取 items[0]」的寫法。
// SSR 階段取好；任一支失敗或整個失敗＝空陣列（頁面落回既有寫死內容，不出 500）。
// 梯次層級的「收得到報名」判斷仍由 `useRegistrablePrograms` 負責（報名元件用），本函式不重複。
import type { PagedResponse } from '#shared/utils/api-types'

export async function useProgramsOfType(programType: string) {
  const config = useRuntimeConfig()
  const club = config.public.club
  const { locale } = useLocale()
  const { data } = await useAsyncData<ProgramDetailView[]>(
    `programs-of-type-${club}-${programType}-${locale.value}`,
    async () => {
      try {
        const list = await $fetch<PagedResponse<{ slug: string }>>(`/api/backend/${club}/programs`, {
          query: { type: programType, pageSize: 10, lang: locale.value },
        })
        const details = await Promise.all((list.items ?? []).map((p) =>
          $fetch<ProgramDetailView>(`/api/backend/${club}/programs/${encodeURIComponent(p.slug)}`, { query: { lang: locale.value } }).catch(() => null)))
        return details.filter((d): d is ProgramDetailView => d != null)
      }
      catch {
        return []
      }
    },
    { default: () => [] },
  )
  const programs = computed(() => data.value ?? [])
  /** Course Schema 只接單一課程：取第一個（既有介面，見 useCourseSchema）。 */
  const first = computed(() => programs.value[0] ?? null)
  return { programs, first }
}
