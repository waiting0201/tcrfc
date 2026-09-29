import { isIP } from 'node:net'
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
// 🔴 S1-17 新增 POST 轉發（原本「只轉發 GET」，S0-9 當時五組端點全是唯讀查詢）。
//
// ── S1-17 收尾修正（2026-09-29）：POST 收窄為只允許表單送出路徑 ──────────────────────
// 原本只要方法是 POST，任何 path 都會被轉發到 apps/api（只靠方法白名單擋 PUT／PATCH／
// DELETE，沒有限制 path 本身）。這支代理沒有轉發 Authorization header 沒錯，但 apps/api
// 也可能存在「不需要認證、只是還沒設計成給瀏覽器打」的內部端點——刻意再收一層：POST 只放行
// 「{club}/forms/{formCode}/submissions」這個路徑形狀（表單公開送出，唯一真正需要 POST 的
// 公開端點），其他 path 一律 405。GET 維持既有行為不變（唯讀查詢，既有五組端點都在用）。
const FORM_SUBMISSION_PATH = /^[a-z][a-z0-9-]*\/forms\/[a-z][a-z0-9_]*\/submissions$/

// ── S1-18 新增：12 FAQ「這則說明有幫助嗎？」回饋 ──────────────────────────────────
// 規劃書 3.12 明文要求「回饋數據回寫後台」，對應 apps/api
// `POST /api/v1/{club}/faqs/{slug}/feedback`（Features/Faqs/FaqsEndpoints.cs，body
// `{ helpful: true|false }`）。跟表單送出同一層考量：白名單只放行這一種路徑形狀，
// 不是「POST 且非表單就一律放行」，避免無差別開放 apps/api 其他還沒設計給瀏覽器
// 直打的端點。slug 允許小寫英數與連字號（同 FaqSlugPolicy 的既有慣例）。
const FAQ_FEEDBACK_PATH = /^[a-z][a-z0-9-]*\/faqs\/[a-z0-9-]+\/feedback$/

export default defineEventHandler(async (event) => {
  const path = event.context.params?.path ?? ''
  const method = event.method

  if (method === 'GET') {
    // 既有行為不變：無 body，直接轉發查詢字串。
    const query = getQuery(event)
    return await $fetch(`/api/v1/${path}`, { baseURL: backendApiBase(), method, query })
  }

  if (method !== 'POST' || !(FORM_SUBMISSION_PATH.test(path) || FAQ_FEEDBACK_PATH.test(path))) {
    throw createError({ statusCode: 405, statusMessage: 'Method Not Allowed' })
  }

  // 公開表單端點不需要任何自訂標頭轉發（不帶 cookie／Authorization，比照這支代理原本
  // 就不轉發後台認證的既有原則），$fetch 對 POST 預設就會用 JSON 編碼 body，跟
  // apps/api 的 Minimal API 期待的 Content-Type 一致。
  const body = await readBody(event).catch(() => undefined)

  // ── 轉發訪客真實 IP（S1-17 收尾修正，2026-09-29） ──────────────────────────────────
  // apps/api 依「真實訪客 IP」對表單送出端點做依 IP 分區的固定視窗限流（見
  // apps/api/Program.cs「S1-10（審查回饋修正）」段、Security/TrustedProxyConfiguration.cs）。
  // 但這支代理呼叫 apps/api 走的是 Docker 內部網路（backendApiBase() 在 SSR 階段解析成
  // http://api:8080，見上方檔頭），**完全繞過 Caddy**——也就是說，從 apps/api 的角度看，
  // 這次連線的來源是 web（本容器），不是 Caddy。apps/api 端既有的 TRUSTED_PROXY_IP 信任清單
  // 若只認 Caddy 的固定 IP，不會信任這支代理送來的 X-Forwarded-For；把 web 容器也一併納入
  // 信任清單是後端那一側的工作（見 docs/17-deployment.md 與任務指示），這裡只負責把「正確的
  // 訪客 IP」放進標頭，兩側要一起上線才會生效（單獨改一側沒有作用：只改這裡，標頭會被 apps/api
  // 忽略；只改後端，這裡沒送標頭一樣拿不到真實 IP）。
  //
  // 怎麼決定「正確的訪客 IP」：**只讀 Caddy 設定的 `X-Real-IP`**（deploy/Caddyfile 對
  // nuxt-* 上游 `header_up X-Real-IP {client_ip}`）。不讀 X-Forwarded-For——Caddy 對已信任的
  // 上游（Cloudflare）採「附加」，而 Cloudflare 會保留訪客自己送來的 XFF，所以 XFF 的第一個值
  // 可以被訪客偽造（E-71）。`{client_ip}` 是 Caddy 依 client_ip_headers＋trusted_proxies 解出的值，
  // header_up 會覆蓋訪客自送的同名標頭；本容器沒有對外發布 port，只有 Caddy 連得進來。
  // 轉給 apps/api 時以「單一值」的 X-Forwarded-For 送出，對應 api 端信任 web 容器、ForwardLimit=1。
  // 取不到（本機開發沒有 Caddy）時不送標頭，不刻意造假。
  const realIp = getRequestHeader(event, 'x-real-ip')?.trim()
  const clientIp = realIp && isIP(realIp) ? realIp : undefined

  return await $fetch(`/api/v1/${path}`, {
    baseURL: backendApiBase(),
    method,
    body,
    headers: clientIp ? { 'x-forwarded-for': clientIp } : undefined,
  })
})
