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
