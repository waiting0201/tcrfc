// app/composables/usePublicTrials.ts — 3.3 球員機會「試訓場次」的公開讀取（P4，H 批，2026-10-02）。
//
// `GET /api/v1/{club}/trials?teamCode=&lang=` → 未結束且日期未過的場次，依日期由近到遠，最多 100 筆。
// SSR 階段取好、payload 帶到瀏覽器；API 失敗＝空陣列（頁面落回既有的「目前尚無公告中的試訓場次」）。
// 各俱樂部只讀自己的場次（藍鯨與磐石的場次各自建立，後端依 club 過濾）。
export interface PublicTrial {
  id: string
  trialOn: string
  teamCode: string | null
  teamName: string | null
  audience: string | null
  venueId: string | null
  venueName: string | null
  venueAddress: string | null
  venueLat: number | null
  venueLng: number | null
  capacity: number | null
  enrolledCount: number
  deadlineOn: string | null
  status: string
  statusCode?: string
  statusLabelZh?: string
  statusLabelEn?: string
  /** 狀態「開放」且未過截止日。 */
  isSignupOpen: boolean
  /** 「額滿／候補」且未過截止日：前台顯示「額滿候補」，仍可報名（列入候補）。 */
  acceptsWaitlist: boolean
}

export async function usePublicTrials() {
  const config = useRuntimeConfig()
  const club = config.public.club
  const { locale } = useLocale()
  const { data } = await useFetch<PublicTrial[]>(`/api/backend/${club}/trials`, {
    query: { lang: locale.value },
    key: `trials-${club}-${locale.value}`,
  })
  const trials = computed(() => data.value ?? [])
  return { trials }
}
