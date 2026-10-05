// app/composables/useRegistrablePrograms.ts — 05 課程「目前收得到報名」的課程與梯次（P1／P2 公開端點）
//
// 給 ProgramRegistration.vue 與各 05 頁的 CTA 共用同一份資料：頁面用 `hasRegistrable` 決定
// 「線上報名」按鈕該跳到頁內報名區（#register）還是退回詢問表單，元件用 `programs` 渲染梯次。
// 取法：該類型已發布且 `hasOpenSession` 的課程（最多 10 個），逐一取詳情拿全部梯次，再用
// `isSessionRegistrable`（utils/program-session.ts）濾掉已截止／尚未開放的梯次。
// SSR 階段取好、payload 帶到瀏覽器，不閃爍；API 失敗＝空陣列（頁面落回既有 CTA，不出 500）。
import type { PagedResponse } from '#shared/utils/api-types'

export interface RegistrableSession {
  id: string
  startOn: string | null
  endOn: string | null
  weeklySchedule: string | null
  capacity: number | null
  enrolledCount: number
  price: number | null
  earlyBirdPrice: number | null
  earlyBirdUntil: string | null
  signupOpensAt: string | null
  signupClosesAt: string | null
  status: string
  statusCode?: string
  statusLabelZh?: string
  statusLabelEn?: string
  venueName: string | null
}

export interface RegistrableProgram {
  slug: string
  name: string | null
  sessions: RegistrableSession[]
}

export async function useRegistrablePrograms(programType: string, options: { enabled?: boolean } = {}) {
  const config = useRuntimeConfig()
  const club = config.public.club
  const { locale } = useLocale()
  const enabled = options.enabled ?? true
  const { data } = await useAsyncData<RegistrableProgram[]>(
    `registrable-programs-${club}-${programType}-${locale.value}`,
    async () => {
      if (!enabled) return [] // 藍鯨：線上報名待確認，不發請求
      const list = await $fetch<PagedResponse<{ slug: string, hasOpenSession: boolean }>>(`/api/backend/${club}/programs`, {
        query: { type: programType, pageSize: 10, lang: locale.value },
      })
      const open = (list.items ?? []).filter((p) => p.hasOpenSession)
      return await Promise.all(open.map((p) => $fetch<RegistrableProgram>(`/api/backend/${club}/programs/${p.slug}`, { query: { lang: locale.value } })))
    },
    { default: () => [] },
  )
  const programs = computed(() =>
    (data.value ?? [])
      .map((p) => ({ ...p, sessions: p.sessions.filter((s) => isSessionRegistrable(s)) }))
      .filter((p) => p.sessions.length > 0),
  )
  const hasRegistrable = computed(() => programs.value.length > 0)
  return { programs, hasRegistrable }
}
