// server/routes/healthz.ts — Dockerfile 的 HEALTHCHECK 打這支路由，比照 apps/web 的既有慣例。
export default defineEventHandler((event) => {
  setResponseStatus(event, 200)
  return { status: 'ok' }
})
