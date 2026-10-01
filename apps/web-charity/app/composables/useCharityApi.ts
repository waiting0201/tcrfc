// app/composables/useCharityApi.ts — 慈善捐款平台公開 API 的唯一呼叫入口。
//
// 為什麼不做 Nuxt 同源代理（apps/web 的 server/api/backend/[...path].ts 那種做法）：
//   api 的公開寫入端點依「訪客真實 IP」限流，而 `nuxt-charity` 容器刻意不在 api 的 TRUSTED_PROXY_IPS 內
//   （docs/14-invariants.md）。經 Nuxt 伺服器代轉，api 看到的來源會是這個容器的 IP，所有捐款人共用同一份額度
//   （掃碼場景又常是店家 Wi-Fi 同出口，本來就吃緊）。所以：
//     - SSR 階段（讀取公開資料）走 Docker 內部網路 `apiInternalBase`，
//     - 瀏覽器階段（建單、付款、確認、取消、輪詢）直接打公開 API 網域 `public.apiBase`（需要 api 的 CORS 允許本站網域）。
import type { FetchError } from 'ofetch'

export interface CharityApiProblem {
  status: number
  /** ProblemDetails 的 `detail`（後端給的日常中文）；沒有就是 null。 */
  detail: string | null
}

export function useCharityApi() {
  const config = useRuntimeConfig()
  const base = (import.meta.server ? config.apiInternalBase : config.public.apiBase).replace(/\/$/, '')
  const root = `${base}/api/v1/donation-platform`

  function request<T>(
    path: string,
    options: {
      method?: 'GET' | 'POST'
      query?: Record<string, string | number | boolean | undefined>
      body?: unknown
      headers?: Record<string, string>
    } = {},
  ): Promise<T> {
    // 位址是外部 API 的完整網址，不屬於 Nitro 的內部路由型別，所以這裡把回傳型別明確收斂成 T。
    return $fetch(`${root}${path}`, {
      method: options.method ?? 'GET',
      query: options.query,
      body: options.body as Record<string, unknown> | undefined,
      headers: options.headers,
    }) as Promise<T>
  }

  return { request }
}

/** 把 `$fetch` 丟出的錯誤整理成 `{ status, detail }`；連線失敗（沒有回應）時 status 為 0。 */
export function toApiProblem(error: unknown): CharityApiProblem {
  const e = error as Partial<FetchError> | undefined
  const status = e?.response?.status ?? e?.statusCode ?? 0
  const data = e?.data as { detail?: unknown } | undefined
  const detail = typeof data?.detail === 'string' && data.detail.trim() ? data.detail : null
  return { status, detail }
}
