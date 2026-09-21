// server/routes/healthz.ts — apps/web/Dockerfile 的 HEALTHCHECK 打這支路由。
// 只需要證明 Node process 還活著、能回應 HTTP，不需要碰資料庫或任何外部服務
// （這個 Nuxt 專案本身也不連資料庫）。
export default defineEventHandler((event) => {
  setResponseStatus(event, 200)
  return { status: 'ok' }
})
