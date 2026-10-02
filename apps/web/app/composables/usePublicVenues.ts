// app/composables/usePublicVenues.ts — 場地（I5 場地管理）的公開讀取（H 批，2026-10-02）。
//
// `GET /api/v1/{club}/venues?lang=` → 這個俱樂部用得到的場地（主場＋賽事／梯次／試訓引用者），含經緯度、交通、照片。
// API 失敗或沒有任何場地 → 空陣列，頁面沿用既有的站台事實（site-facts.ts）作為過渡。
import type { PublicVenue } from '#shared/utils/site-settings'

export async function usePublicVenues() {
  const config = useRuntimeConfig()
  const club = config.public.club
  const { locale } = useLocale()
  const { data } = await useFetch<PublicVenue[]>(`/api/backend/${club}/venues`, {
    query: { lang: locale.value },
    key: `venues-${club}-${locale.value}`,
  })
  const venues = computed(() => (data.value ?? []).filter((v) => !!v.name))
  return { venues }
}
