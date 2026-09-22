// server/routes/__sitemap__/urls.ts — sitemap 的動態網址來源（@nuxtjs/sitemap 的官方模式：
// 一支回傳網址陣列的 server route，設定檔用 sitemap.sources 指到這裡）。
//
// 先前直接把函式放在 nuxt.config.ts 的 sitemap.urls 時，@nuxtjs/sitemap 在
// node-server（SSR、非 prerender）模式下並未把它當成 runtime 動態來源
// （建置時只印出「No dynamic sources detected」，實際 request 時 sitemap.xml
// 一直是空的 urlset），改用這支獨立 server route 才會在每次請求時被呼叫。
// 已記入 docs/18-work-errors.md。
//
// 單元開關呼叫點 3／4：與 llms.txt／SiteHeader 共用同一份 getEnabledSiteUnits。
//
// 🔴 S0-9e 新增：新聞逐篇網址做出來之後，這裡跟著補上 83 篇文章各自的 URL——
// 沒有這一段，/api/__sitemap__/urls 只會列出「單元」層級的網址（/zh/news/ 本身），
// 每一篇文章都不會被列出。呼叫的是既有的公開讀取端點（GET /api/v1/{club}/news，
// server/api/backend/[...path].ts 同一套代理／server/utils/backend-api.ts 同一個
// 基底網址），不是另開一條路。07 單元對藍鯨仍是開放的（isUnitEnabledForClub 兩站
// 皆為 true，見 shared/utils/units.ts），只是藍鯨目前 0 篇文章（STATUS.md C-7）——
// 這裡不用特別分支處理，API 回傳空陣列，map 出來自然是空陣列。
//
// ⚠️ 這裡只把「資料來源」補對（任務要求的範圍）；/sitemap.xml 本身尚未把這份
// 動態來源接進最終 XML 輸出（docs/18-work-errors.md E-18，S0-9a 就已知、尚未解決，
// 不是本次新增的問題，修那個是另一層 @nuxtjs/sitemap 模組接線工作，不在本次範圍）。
// try/catch 只是防禦性寫法：apps/api 若暫時連不上，sitemap 退回只有單元清單，
// 不讓整支路由連 200 都回不了。
export default defineEventHandler(async (event) => {
  const club = useRuntimeConfig(event).public.club
  const unitUrls = getEnabledSiteUnits(club).map((unit) => ({ loc: unit.path }))

  try {
    const result = await $fetch<{ items: { slug: string }[] }>(`/api/v1/${club}/news`, {
      baseURL: backendApiBase(),
      query: { pageSize: 200, lang: 'zh' },
    })
    const articleUrls = (result.items ?? []).map((a) => ({ loc: `/zh/news/${a.slug}/` }))
    return [...unitUrls, ...articleUrls]
  } catch {
    return unitUrls
  }
})
