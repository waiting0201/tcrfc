// app/composables/useShopInfo.ts — 商店入口與政策（`GET /api/shop/info`，S6 維護）
//
// SSR 與瀏覽器端都可呼叫。回應是 `no-store`（BFF 保證），Nuxt 的 payload 只在「同一次 SSR→hydration」內重用，
// 不跨請求快取。API 失敗＝`info` 為 `null`，呼叫端落回空狀態（不丟 500）。
//
// 🔴 `paymentAvailable === false`（LINE Pay 尚未串接，STATUS B-10）：結帳頁明示暫未開放線上付款，
// **不得假裝付款成功**；`info` 取不到（`null`）時**也當作不可付款**，不假設可付。
import type { ShopInfo } from '#shared/utils/shop'

export function useShopInfo(options: { server?: boolean } = {}) {
  const { locale } = useLocale()
  const config = useRuntimeConfig()
  const club = config.public.club === 'bw' ? 'bw' : 'tcrfc'
  const { data, error, status } = useFetch<ShopInfo | null>('/api/shop/info', {
    query: computed(() => ({ lang: locale.value })),
    key: `shop-info-${club}-${locale.value}`,
    default: () => null,
    server: options.server ?? true,
  })
  const info = computed(() => data.value ?? null)
  const paymentAvailable = computed(() => info.value?.paymentAvailable === true)
  return { info, paymentAvailable, error, status }
}
