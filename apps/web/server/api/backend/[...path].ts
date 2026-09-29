// server/api/backend/[...path].ts — apps/api（.NET 唯讀讀取 API／表單公開送出）的同源代理路由。
//
// 為什麼要代理而不是前端頁面直接打 apps/api：
//   1. SSR 階段必須走 Docker 內部網路（NUXT_API_INTERNAL_BASE=http://api:8080），
//      不能繞去 Caddy／Cloudflare 再繞回來；瀏覽器端則需要公開網域。同一支
//      useFetch 呼叫沒辦法依「現在是 server 還是 client」自動換網址，
//      同源代理讓頁面永遠呼叫 /api/backend/...，網址差異全部收斂在這支路由。
//   2. 不需要在 nuxt.config.ts 額外設定 CORS 白名單或 public runtimeConfig
//      （S0-9 資料驅動頁搬遷期間 nuxt.config.ts 是兩個 agent 共用、禁止修改的檔案，
//      見 apps/web/server/utils/backend-api.ts 檔頭說明）。
//
// 🔴 S1-17 新增 POST 轉發（原本「只轉發 GET」，S0-9 當時五組端點全是唯讀查詢）：
// 10 表單中心的公開送出端點（POST /api/v1/{club}/forms/{formCode}/submissions）需要這支代理
// 轉發請求主體。**刻意只開放 GET／POST 兩種方法**——本專案公開端點（news／forms／site-facts／
// schedule……）目前只有這兩種，沒有 PUT／PATCH／DELETE 的公開寫入需求；後台（apps/admin）走
// 另一條直接呼叫 apps/api 的路徑（帶 Authorization header），不經過這支代理，所以這裡不需要
// 也不應該轉發其餘方法或轉發呼叫端的 Authorization header——限制方法白名單是縱深防禦，避免這支
// 「任何 path 都能打」的萬用代理意外變成後台端點的公開跳板（即使沒有轉發認證標頭，讓後台路徑
// 也能從這裡打到仍然是不必要的攻擊面）。
const ALLOWED_METHODS = new Set(['GET', 'POST'])

export default defineEventHandler(async (event) => {
  const path = event.context.params?.path ?? ''
  const method = event.method

  if (!ALLOWED_METHODS.has(method)) {
    throw createError({ statusCode: 405, statusMessage: 'Method Not Allowed' })
  }

  const query = getQuery(event)
  const base = backendApiBase()

  // GET：沿用既有行為（無 body）。POST：讀取 JSON 主體轉發（表單送出用）——
  // 公開表單端點不需要任何自訂標頭轉發（不帶 cookie／Authorization，比照這支代理原本
  // 就不轉發後台認證的既有原則），$fetch 對 POST 預設就會用 JSON 編碼 body，跟
  // apps/api 的 Minimal API 期待的 Content-Type 一致。
  const body = method === 'POST' ? await readBody(event).catch(() => undefined) : undefined

  return await $fetch(`/api/v1/${path}`, {
    baseURL: base,
    method,
    query,
    body,
  })
})
