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
export default defineEventHandler((event) => {
  const club = useRuntimeConfig(event).public.club
  return getEnabledSiteUnits(club).map((unit) => ({ loc: unit.path }))
})
