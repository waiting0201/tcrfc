// apps/web-charity/nuxt.config.ts — nuxt-charity（慈善捐款平台前台，獨立專案）
//
// 與 apps/web（nuxt-club）刻意不同的地方：
//   - 這裡只服務一個網域、一個法人（台灣足球策略發展協會），沒有「一份 build、多個容器換品牌」
//     的需求，所以不需要 apps/web 那一整套「site.url 留白、runtime 才決定網域」的機制。
//   - 沒有裝 @nuxtjs/seo（理由見 README.md「@nuxtjs/seo 裝不裝」一節）：本平台明文不做 SEO／GEO
//     （docs/10-charity-donation-site.md），僅存的「基本可讀性」需求（noindex、hreflang、
//     og: 系列標記）用 routeRules ＋ 各頁面自己的 useHead 手動處理即可，不需要整包 sitemap／
//     schema-org／og-image 的相依與已知的建置陷阱（nuxt-og-image 需要 @takumi-rs/core 才能
//     build 過，見 docs/18-work-errors.md 與 frontend-architect 記憶庫 nuxt-seo-module-gotchas）。
export default defineNuxtConfig({
  compatibilityDate: '2026-09-22',

  future: { compatibilityVersion: 4 },

  modules: ['@nuxt/eslint'],

  devtools: { enabled: false },

  css: ['~/assets/css/charity.css'],

  nitro: {
    // Dockerfile 用 `node .output/server/index.mjs` 直接執行，標準 Node 部署。
    preset: 'node-server',
  },

  eslint: {
    config: { stylistic: false },
  },

  // 🔴 CLAUDE.md 全域規定 5：正式站上線前全站 noindex，這條不得拿掉。
  // 比照 apps/web 的做法：<link rel="canonical"> 之外另加 X-Robots-Tag 標頭雙重保險，
  // 並用 server/routes/robots.txt.ts 擋掉整站（見該檔案）。
  routeRules: {
    '/**': {
      headers: {
        'X-Robots-Tag': 'noindex, nofollow',
        'X-Content-Type-Options': 'nosniff',
        'Referrer-Policy': 'strict-origin-when-cross-origin',
      },
    },
    // 無語系前綴時導向預設語系（繁中），比照規劃書 §2.1「雙語以 /zh/、/en/ 區隔」。
    '/': { redirect: { to: '/zh/', statusCode: 302 } },
  },

  app: {
    head: {
      // 全站預設語系；[lang]/*.vue 頁面各自用 useHead 依 route.params.lang 覆寫成 'en'。
      htmlAttrs: { lang: 'zh-Hant' },
    },
  },
})
