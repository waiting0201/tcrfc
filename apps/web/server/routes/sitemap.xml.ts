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
// ⚠️ 雙語 hreflang（S1-13 更新，取代原本「只有 zh 頁面」的假設）：nuxt.config.ts 的
// pages:extend hook 已經讓每個 /zh/... 頁面都有一個真實存在的 /en/... 孿生路由
// （即使目前顯示的是繁中內容＋LocaleFallbackNotice 提示，見該元件），不再是會 404
// 的虛構網址，因此這裡改成每個候選網址各輸出兩筆 <url>（zh 版＋en 版），每筆都帶完整
// 的三個 hreflang alternate（zh-Hant／en／x-default 自我＋互相參照，符合 Google 對
// hreflang 需要「自我參照」的慣例）。lastmod 兩個版本共用同一個值——en 版目前是 zh
// 內容的鏡像，異動時間跟著來源一起變動，不是兩份獨立內容。
export default defineEventHandler(async (event) => {
  const club = useRuntimeConfig(event).public.club
  const siteUrl = (getSiteConfig(event).url ?? '').replace(/\/$/, '')

  const urls = await getSitemapUrls(club)

  const body = urls
    .flatMap((u) => {
      const zhHref = escapeXml(`${siteUrl}${u.loc}`)
      const enHref = escapeXml(`${siteUrl}${localizePath(u.loc, 'en')}`)
      // lastmod（S1-12 新增）：只有 Article 這類有真實 updated_at 的動態內容才帶，
      // 靜態單元頁（getEnabledSiteUnits）沒有異動時間可回報，刻意不虛構一個假值
      // （GEO-08「更新時間要真的更新，不是發布時間複製一份」同一個精神）。
      const lastmod = u.lastmod ? `\n    <lastmod>${escapeXml(u.lastmod)}</lastmod>` : ''
      const alternates = [
        `    <xhtml:link rel="alternate" hreflang="zh-Hant" href="${zhHref}" />`,
        `    <xhtml:link rel="alternate" hreflang="en" href="${enHref}" />`,
        `    <xhtml:link rel="alternate" hreflang="x-default" href="${zhHref}" />`,
      ].join('\n')
      return [
        ['  <url>', `    <loc>${zhHref}</loc>${lastmod}`, alternates, '  </url>'].join('\n'),
        ['  <url>', `    <loc>${enHref}</loc>${lastmod}`, alternates, '  </url>'].join('\n'),
      ]
    })
    .join('\n')

  setHeader(event, 'Content-Type', 'application/xml; charset=UTF-8')
  return `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">\n${body}\n</urlset>\n`
})

function escapeXml(value: string): string {
  return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')
}
