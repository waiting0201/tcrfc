// app/composables/useCharityData.ts — SSR 階段讀取公開資料的 composable（結果會隨 payload 帶到瀏覽器，不重複請求）。
import { useLang } from './useLang'
import { useCharityApi } from './useCharityApi'
import type {
  PublicProjectCard,
  PublicProjectDetail,
  PublicSettings,
  PublicStoreLanding,
} from '../types/charity'

export function useCharitySettings() {
  const { lang } = useLang()
  const api = useCharityApi()
  return useAsyncData<PublicSettings | null>(
    () => `charity-settings-${lang.value}`,
    () => api.request<PublicSettings>('/settings', { query: { lang: lang.value } }).catch(() => null),
    { watch: [lang] },
  )
}

export function useCharityProjects() {
  const { lang } = useLang()
  const api = useCharityApi()
  return useAsyncData<PublicProjectCard[] | null>(
    () => `charity-projects-${lang.value}`,
    () => api.request<PublicProjectCard[]>('/projects', { query: { lang: lang.value } }).catch(() => null),
    { watch: [lang] },
  )
}

/** 掃碼落地頁的店家識別：對不到有效店家時 API 回 `{ store: null }`（視同無店家歸屬，不報錯）。 */
export function useStoreLanding(slug: () => string | null) {
  const { lang } = useLang()
  const api = useCharityApi()
  return useAsyncData<PublicStoreLanding | null>(
    () => `charity-store-${slug() ?? 'none'}-${lang.value}`,
    async () => {
      const s = slug()
      if (!s) return { store: null }
      // 查詢失敗（連線或服務問題）一律降級成「無店家歸屬」，不中斷捐款流程（規劃書 §2.2 第 5 點）。
      return await api.request<PublicStoreLanding>(`/stores/${encodeURIComponent(s)}`, { query: { lang: lang.value } })
        .catch(() => ({ store: null }))
    },
    { watch: [lang, slug] },
  )
}

export function useProjectDetail(slug: string) {
  const { lang } = useLang()
  const api = useCharityApi()
  return useAsyncData<PublicProjectDetail | null>(
    () => `charity-project-${slug}-${lang.value}`,
    async () => {
      try {
        return await api.request<PublicProjectDetail>(`/projects/${encodeURIComponent(slug)}`, { query: { lang: lang.value } })
      } catch (error) {
        // 項目不存在或未上架 → 404；其他錯誤（連線、500）讓錯誤頁處理。
        if (toApiProblem(error).status === 404) return null
        throw error
      }
    },
    { watch: [lang] },
  )
}
