// apps/web/nuxt.config.ts — nuxt-club（主站 ＋ 藍鯨共用一份程式碼）
//
// 🔴 硬性規則（見 docs/13-blue-whale-site.md §6 紀律 7、8，違反會讓多站共用悄悄失效）：
//   - site.url 絕對不要寫在這裡。NUXT_PUBLIC_SITE_URL 只在 `docker run` / 容器啟動時給，
//     docker build 階段絕對不要帶這個環境變數（見 apps/web/Dockerfile 檔頭註解）。
//   - 顏色只能是 CSS custom properties（tcrfc.css 本體），不得引入 Tailwind JIT 或任何
//     把顏色編譯成字面值的工具（紀律 1）。本專案刻意不裝 Tailwind。
export default defineNuxtConfig({
  compatibilityDate: '2026-09-21',

  future: { compatibilityVersion: 4 },

  modules: ['@nuxtjs/seo', '@nuxt/eslint'],

  devtools: { enabled: false },

  // ⛔ 不要在這裡加 css: ['~/assets/css/tcrfc.css']。
  // tcrfc.css 是「一個位元都不准改」的視覺唯一真實來源，走 Vite 的 css pipeline
  // 會被處理（可能被 postcss/建置流程轉寫），所以整份原封放在 public/，
  // 以 <link rel="stylesheet"> 的純靜態資源形式載入（見 app/app.vue）。
  css: [],

  runtimeConfig: {
    public: {
      // NUXT_PUBLIC_CLUB=tcrfc|bw，容器啟動時給。預設 tcrfc 是本機開發的合理預設值，
      // 不是「忘記帶環境變數時的靜默回退」——藍鯨容器一律明確帶 bw（docs/13 §6 紀律 2、5）。
      club: 'tcrfc',
      // NUXT_PUBLIC_SITE_ENV=prelaunch|production，見 docs/17-deployment.md §10.4。
      // 骨架階段先接住這個變數，實際的三層防護（robots／標頭／Basic Auth）留給 S0-9 之後補完。
      siteEnv: 'prelaunch',
    },
  },

  // 🔴 site.url 刻意留空——由 NUXT_PUBLIC_SITE_URL 在 runtime 覆寫（S0-9b 已實測）。
  // defaultLocale 明確設為 zh-Hant，讓 @nuxtjs/seo 衍生的中繼資料（og:locale 等）
  // 跟著正確；⚠️ 但 <html lang> 本身這個值沒有生效（nuxt-seo-utils 有自己一套
  // htmlAttrs.lang 解析與合併順序，實測設這裡並不會連帶修正 lang 屬性）——
  // <html lang> 真正生效的設定在下面的 app.head.htmlAttrs.lang，兩處分工，
  // 詳見 docs/18-work-errors.md E-17。
  site: {
    name: 'TCRFC',
    defaultLocale: 'zh-Hant',
  },

  // @nuxtjs/seo 內建的 nuxt-og-image 子模組缺 renderer 會讓 build 直接失敗
  // （docs/13 §6「實測時踩到的建置坑」：報 takumi renderer missing dependencies）。
  // 本骨架階段選擇明確關閉 og-image 子模組，理由：
  //   1) mockup 的 OG 圖是固定的靜態檔（見 site/src/partials/shell.html 的 og:image），
  //      不需要動態產圖，裝 @takumi-rs/core 只是為了讓 build 通過、沒有對應的功能需求。
  //   2) @takumi-rs/core 是原生二進位相依，會增加 Alpine（node:22.12-alpine）映像檔的
  //      建置複雜度與體積，骨架階段先不引入這個相依面。
  // 之後若要做動態 OG 圖（例如依文章標題產生 OG 卡片），再回頭裝 @takumi-rs/core 並打開這個模組。
  ogImage: false,

  // 上線前全站 noindex（CLAUDE.md 第 5 條，不得拿掉）。
  // nuxt-robots（@nuxtjs/seo 子模組）依此直接產出 /robots.txt，取代 Cloudflare Pages
  // 時期的 site/src/robots.txt（見 apps/web/README.md 的移植對照）。
  robots: {
    disallow: ['/'],
  },

  // 單元開關呼叫點 3／4：sitemap 的網址清單同樣呼叫 isUnitEnabledForClub
  // （經由 getEnabledSiteUnits 間接呼叫）。實際邏輯在 server/api/__sitemap__/urls.ts
  // ——放在 server/api/__sitemap__/urls 是 @nuxtjs/sitemap 的零設定自動探索慣例，
  // 會被視為「需要 runtime 求值」的動態來源。踩過的坑：① 先放在 nuxt.config.ts
  // 的 sitemap.urls（函式），build 時印 "No dynamic sources detected"；
  // ② 改放 server/routes/__sitemap__/urls.ts 並用 sitemap.sources 指過去，
  // 直接呼叫該路由回應正確，但 /sitemap.xml 仍是空的 urlset——這代表 sources
  // 清單在「伺服器還沒真的啟動」的建置階段就被求值過一次、結果（空陣列）被寫進
  // .output 的靜態資產快取，之後每次請求都回放那份快取，不會再重新呼叫。
  // 改用 server/api/__sitemap__/urls.ts 的自動探索慣例才會在每次請求時真的執行。
  // 已記入 docs/18-work-errors.md。

  // 站內 <link rel="canonical"> 之外，同時要有 X-Robots-Tag 標頭雙重保險
  // （比照 site/src/_headers 的既有作法，改用 Nitro routeRules 移植）。
  routeRules: {
    '/**': {
      headers: {
        'X-Robots-Tag': 'noindex, nofollow',
        'X-Content-Type-Options': 'nosniff',
        'Referrer-Policy': 'strict-origin-when-cross-origin',
      },
    },
    // site/src/_redirects 逐條移植（10.2＋10.3、10.6＋10.7 合併後的舊網址，規劃書 v1.9）
    '/zh/join/childrens-training/**': { redirect: { to: '/zh/join/academy/', statusCode: 301 } },
    '/zh/join/sponsorship/**': { redirect: { to: '/zh/join/partnership/', statusCode: 301 } },
  },

  nitro: {
    // Dockerfile 用 `node .output/server/index.mjs` 直接執行，標準 Node 部署，
    // 不特化其他 Nitro preset（見 Dockerfile 檔頭註解）。
    preset: 'node-server',
  },

  eslint: {
    config: { stylistic: false },
  },

  app: {
    head: {
      htmlAttrs: { lang: 'zh-Hant' },
    },
  },
})
