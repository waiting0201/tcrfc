// server/routes/robots.txt.ts — 上線前全站 noindex（CLAUDE.md 全域規定 5）。
// 本平台明文不做 SEO（docs/10-charity-donation-site.md），沒有裝 @nuxtjs/seo 的 nuxt-robots
// 子模組，這裡手動回一份最單純的「全站擋爬蟲」內容，搭配 nuxt.config.ts 的 X-Robots-Tag
// 標頭雙重保險（CLAUDE.md 第 5 條、docs/18-work-errors.md E-29 的教訓：兩者都要有，
// 且都要用 curl -I 實測，不能只看畫面）。
export default defineEventHandler((event) => {
  setHeader(event, 'Content-Type', 'text/plain; charset=utf-8')
  return 'User-agent: *\nDisallow: /\n'
})
