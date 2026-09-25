// server/middleware/redirects.ts — 301 轉址（S1-12，主站規劃書 §4.8 H「301 轉址管理」）。
//
// 用 Nitro **伺服器層中介軟體**（不是 app/middleware 的 Vue Router 中介軟體）是必要的：
// 舊網址（例如 Wix 商店的 /product-page/厚底緩震機能襪）在新站完全沒有對應的 Nuxt 頁面元件，
// Vue Router 連比對路由這一步都不會發生，只有在 HTTP 請求真正進來、Nitro 決定怎麼處理它
// 之前攔截，才攔得到「這個路徑在新站根本不存在」的情況。
//
// 資料來源：GET /api/v1/{club}/seo/redirects（Features/Seo/SeoEndpoints.cs，公開、
// 只回傳 is_active=1 的規則）。這裡**不做任何額外的記憶體快取**——apps/api 那一層本身已經接了
// IQueryCache（docs/17 §4），這裡多加一層等於兩層快取，TTL 語意會更難推理；效能與既有的
// llms.txt／sitemap.xml 走同一套「每次請求打一次 apps/api」的既定作法一致。
//
// 排除規則：只檢查「看起來像頁面」的請求——排除 /api/、/_nuxt/、健康檢查、以及帶副檔名的
// 靜態資源請求，避免每一張圖片／JS／CSS 都多打一次後端，見下方 shouldCheckPath()。
export default defineEventHandler(async (event) => {
  const path = event.path.split('?')[0] ?? event.path

  if (!shouldCheckPath(path)) {
    return
  }

  const club = useRuntimeConfig(event).public.club

  try {
    const redirects = await $fetch<{ fromPath: string, toPath: string }[]>(`/api/v1/${club}/seo/redirects`, {
      baseURL: backendApiBase(),
    })

    const match = (redirects ?? []).find((r) => r.fromPath === path)
    if (match) {
      await sendRedirect(event, match.toPath, 301)
    }
  } catch {
    // apps/api 暫時連不上：不轉址，讓請求照原本的路由邏輯繼續走（跟既有 sitemap-urls.ts／
    // llms.txt.ts 同一種防禦性寫法——轉址失效不該讓整個網站連不上）。
  }
})

function shouldCheckPath(path: string): boolean {
  if (path.startsWith('/api/') || path.startsWith('/_nuxt/') || path === '/healthz') {
    return false
  }

  // 帶副檔名的請求（.js／.css／.png／.svg……）一律視為靜態資源，不查轉址表。
  const lastSegment = path.split('/').pop() ?? ''
  if (lastSegment.includes('.')) {
    return false
  }

  return true
}
