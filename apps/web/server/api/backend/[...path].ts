// server/api/backend/[...path].ts — apps/api（.NET 唯讀讀取 API）的同源代理路由。
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
// 只轉發 GET，本次任務（S0-9 資料驅動頁）用到的五組端點全部是唯讀查詢
// （apps/api/README.md：clubs／players／staff／news／schedule），沒有寫入需求。
export default defineEventHandler(async (event) => {
  const path = event.context.params?.path ?? ''
  const query = getQuery(event)
  const base = backendApiBase()
  return await $fetch(`/api/v1/${path}`, {
    baseURL: base,
    query,
  })
})
