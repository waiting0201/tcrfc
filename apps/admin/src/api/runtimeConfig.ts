/**
 * API 基底網址的解析（執行期注入優先）。
 *
 * 優先順序：
 *  1. `window.__TCRFC_CONFIG__.apiBaseUrl`——正式容器啟動時由 `/config.js` 注入（環境變數
 *     `ADMIN_API_BASE_URL`，見 docker-entrypoint.d/40-runtime-config.sh）。換網域不必重建映像檔。
 *  2. `import.meta.env.VITE_ADMIN_API_BASE_URL`——建置期變數（本機開發 `.env` 用）。
 *  3. 僅開發模式（`import.meta.env.DEV`）退回本機 API `http://127.0.0.1:5299`。
 *
 * 🔴 正式建置找不到設定時**不得**默默退回 127.0.0.1（那會讓瀏覽器對訪客自己的電腦發請求，
 * 症狀是莫名的 CORS 錯誤，docs/18 E-112 升級段第三次）：回傳空字串，由 main.ts 在畫面明確顯示
 * 「未設定 API 位址」。
 */
interface TcrfcRuntimeConfig {
  apiBaseUrl?: string
  /** 主站前台網址（ADMIN_WEB_BASE_URL），見 resolveFrontendBaseUrl */
  webBaseUrl?: string
  /** 藍鯨官網前台網址（ADMIN_BW_WEB_BASE_URL），見 resolveFrontendBaseUrl */
  bwWebBaseUrl?: string
}

declare global {
  interface Window {
    __TCRFC_CONFIG__?: TcrfcRuntimeConfig
  }
}

export function resolveApiBaseUrl(): string {
  const runtime = typeof window !== 'undefined' ? window.__TCRFC_CONFIG__?.apiBaseUrl?.trim() : ''
  const build = (import.meta.env.VITE_ADMIN_API_BASE_URL as string | undefined)?.trim()
  const value = runtime || build || (import.meta.env.DEV ? 'http://127.0.0.1:5299' : '')
  return value.replace(/\/$/, '')
}

/** 解析結果；空字串＝未設定（只可能發生在非開發模式）。 */
export const API_BASE_URL = resolveApiBaseUrl()

if (!API_BASE_URL) {
  console.error('[TCRFC] 未設定 API 位址：容器請設定環境變數 ADMIN_API_BASE_URL（見 README「環境變數」），建置期可設 VITE_ADMIN_API_BASE_URL。')
}

/**
 * 前台網址（「這裡管理的是：… ↗」與「預覽前台」用）。後台與前台是不同網域（docs/17 §10），
 * 前台路徑若直接寫 `/zh/news/` 會落在後台自己的網域——必須接上前台的基底網址。
 *
 * 主站與藍鯨是兩個前台網域，依後台目前選的俱樂部決定。解析順序同 API 位址：
 * `/config.js`（ADMIN_WEB_BASE_URL／ADMIN_BW_WEB_BASE_URL）＞ VITE_ 建置期變數 ＞ 僅開發模式退回
 * `http://localhost:3000`（apps/web 的 `npm run dev`）。找不到時回傳空字串，呼叫端不放連結。
 */
export function resolveFrontendBaseUrl(isBlueWhale: boolean): string {
  const cfg = typeof window !== 'undefined' ? window.__TCRFC_CONFIG__ : undefined
  const runtime = (isBlueWhale ? cfg?.bwWebBaseUrl : cfg?.webBaseUrl)?.trim()
  const build = (
    (isBlueWhale ? import.meta.env.VITE_ADMIN_BW_WEB_BASE_URL : import.meta.env.VITE_ADMIN_WEB_BASE_URL) as
      | string
      | undefined
  )?.trim()
  const value = runtime || build || (import.meta.env.DEV ? 'http://localhost:3000' : '')
  return value.replace(/\/$/, '')
}
