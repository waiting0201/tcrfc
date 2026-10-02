// app/composables/usePolicy.ts — 政策頁（Cookie／隱私權／會員條款）的公開讀取（I3 全域設定，H 批，2026-10-02）。
//
// `GET /api/v1/{club}/policies/{cookie|privacy|member-terms}?lang=` → `{ title, body(純文字), updatedAt, isFallbackLocale }`。
// 沒有內容或代碼不存在回 404 → 這裡回 `policy = null`，頁面沿用既有的靜態文字（過渡策略）。
// 🔴 `body` 是純文字（空行分段），頁面必須用文字節點輸出，不得 v-html（見 shared/utils/plain-text.ts）。
import type { PublicPolicy } from '#shared/utils/site-settings'

export async function usePolicy(code: 'cookie' | 'privacy' | 'member-terms') {
  const config = useRuntimeConfig()
  const club = config.public.club
  const { locale } = useLocale()
  const { data } = await useFetch<PublicPolicy>(`/api/backend/${club}/policies/${code}`, {
    query: { lang: locale.value },
    key: `policy-${code}-${club}-${locale.value}`,
  })
  const policy = computed(() => (data.value && data.value.body?.trim() ? data.value : null))
  const paragraphs = computed(() => splitParagraphs(policy.value?.body))
  return { policy, paragraphs }
}
