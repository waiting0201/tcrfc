// server/routes/sitemap.xml.ts — 自組 /sitemap.xml，取代 @nuxtjs/sitemap 的內建路由
// （docs/18-work-errors.md E-18，2026-09-22 補上真正根因與修法，取代原本「建置期
// 求值快取」的猜測）。
//
// 🔴 真正根因（本次重新追查 node_modules 原始碼確認，比 E-18 原記錄更精確）：
// @nuxtjs/sitemap 內建的 /sitemap.xml 產生邏輯（buildSitemapRenderPlan，見
// node_modules/@nuxtjs/sitemap/dist/runtime/server/sitemap/nitro.js）逐筆檢查每個
// 候選網址「路徑命不命中帶 X-Robots-Tag: noindex 的 route rule」，命中就整筆排除
// （`hasRobotsDisabled` → `continue`），且原始碼裡沒有任何設定可以繞過這個檢查
// （`routeRules.sitemap` 只能排除，不能強制留下）。本站上線前 nuxt.config.ts 對
// `/**` 全站蓋一條 `X-Robots-Tag: noindex, nofollow`（CLAUDE.md 全域規定第 5 條、
// docs/14-invariants.md，上線前不得拿掉），因此候選網址無一倖免，urlset 恆為空——
// 不是快取或建置期求值順序的問題，是這個「robots 感知」機制與「全站 noindex 上線前」
// 這個專案前提正面衝突，且衝突無法透過設定調解，只能繞過內建路由。
//
// 修法：nuxt.config.ts 已把 `sitemap.enabled` 設為 false（完全關閉 @nuxtjs/sitemap，
// 不掛任何路由、不掛 robots 鉤子、不掛 prerender），改由這支檔案接手 `/sitemap.xml`
// 自己組 XML。資料來源沿用既有 server/utils/sitemap-urls.ts（單一真實來源，呼叫鏈
// 仍是 isUnitEnabledForClub → getEnabledSiteUnits，不另開一份判斷）。
//
// ⚠️ 這支路由本身仍然吃得到 nuxt.config.ts 對 `/**` 的 noindex 標頭——Nitro 的
// routeRules 標頭是依路徑比對疊加在回應上，不看是哪個 handler 送出回應，驗收時
// 應該實測這支路由自己也送出 X-Robots-Tag: noindex（不需要在這裡手動再設一次）。
//
// ⚠️ 雙語 hreflang：目前只有 zh 頁面（apps/web/README「⬜ 只有繁中頁面，en 語系與
// hreflang 留給 S0-9」），沒有真實存在的 /en/ 對應頁——這裡刻意只放 zh-Hant 與
// x-default 自我參照，不虛構一個會 404 的 /en/ 網址（同 GEO-05「資料不足時不輸出」
// 的精神）。等 en 頁面真的搬遷完成，要在這裡改成查真實的 en 對應路徑再輸出
// hreflang="en" alternate。
export default defineEventHandler(async (event) => {
  const club = useRuntimeConfig(event).public.club
  const siteUrl = (getSiteConfig(event).url ?? '').replace(/\/$/, '')

  const urls = await getSitemapUrls(club)

  const body = urls
    .map((u) => {
      const loc = escapeXml(`${siteUrl}${u.loc}`)
      // lastmod（S1-12 新增）：只有 Article 這類有真實 updated_at 的動態內容才帶，
      // 靜態單元頁（getEnabledSiteUnits）沒有異動時間可回報，刻意不虛構一個假值
      // （GEO-08「更新時間要真的更新，不是發布時間複製一份」同一個精神）。
      const lastmod = u.lastmod ? `\n    <lastmod>${escapeXml(u.lastmod)}</lastmod>` : ''
      return [
        '  <url>',
        `    <loc>${loc}</loc>${lastmod}`,
        `    <xhtml:link rel="alternate" hreflang="zh-Hant" href="${loc}" />`,
        `    <xhtml:link rel="alternate" hreflang="x-default" href="${loc}" />`,
        '  </url>',
      ].join('\n')
    })
    .join('\n')

  setHeader(event, 'Content-Type', 'application/xml; charset=UTF-8')
  return `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">\n${body}\n</urlset>\n`
})

function escapeXml(value: string): string {
  return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')
}
