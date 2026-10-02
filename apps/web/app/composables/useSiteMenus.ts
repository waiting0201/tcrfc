// app/composables/useSiteMenus.ts — 頁首／頁尾選單的公開讀取（I2 選單管理，H 批，2026-10-02）。
//
// `GET /api/v1/{club}/menus?lang=` → `{ main, mega, footer }`（後台「選單管理」維護；兩個語系都沒標籤的項目不輸出）。
// 🔴 **過渡策略**：API 打不到、或某個位置在後台還沒有任何項目時，對應位置回傳空陣列，呼叫端
// （SiteHeader／SiteFooter）據此繼續使用寫死的既有選單；後台一建立該位置的項目，前台就改讀 API。
// 不 await（頁首頁尾在每一頁，await 會讓元件變非同步並在 API 慢時拖住整頁）；useFetch 不 await 時 SSR
// 仍會等資料（onServerPrefetch），所以不閃爍。
import type { PublicMenus, PublicMenuItem } from '#shared/utils/site-settings'

export function useSiteMenus() {
  const config = useRuntimeConfig()
  const club = config.public.club
  const { locale } = useLocale()
  const { data } = useFetch<PublicMenus>(() => `/api/backend/${club}/menus`, {
    query: computed(() => ({ lang: locale.value })),
    key: `site-menus-${club}-${locale.value}`,
  })
  const main = computed<PublicMenuItem[]>(() => data.value?.main ?? [])
  const mega = computed<PublicMenuItem[]>(() => data.value?.mega ?? [])
  const footer = computed<PublicMenuItem[]>(() => data.value?.footer ?? [])
  return { main, mega, footer }
}
