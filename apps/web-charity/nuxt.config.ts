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

  // 後端 API 位址（慈善 CH-2 公開端點，apps/api 的 `/api/v1/donation-platform/…`）。
  //   - apiInternalBase：SSR 階段走 Docker 內部網路（NUXT_API_INTERNAL_BASE=http://api:8080），不繞 Caddy／Cloudflare。
  //   - public.apiBase：瀏覽器端直接呼叫公開 API 網域（NUXT_PUBLIC_API_BASE=https://{API_DOMAIN}）。
  //     🔴 寫入端點（建單、付款、確認、取消）與結果頁輪詢一律由瀏覽器直打，不經 Nuxt 伺服器代轉：
  //     api 的公開限流依「訪客真實 IP」分區，`nuxt-charity` 容器刻意不在 TRUSTED_PROXY_IPS 內
  //     （docs/14-invariants.md），代轉會讓所有捐款人在 api 眼中變成同一個 IP，30 次／10 分鐘的額度被全站共用。
  //   - public.turnstileSiteKey：後端設了 TURNSTILE_SECRET_KEY_CHARITY 才需要給（NUXT_PUBLIC_TURNSTILE_SITE_KEY）。
  //   - public.simulatedPayment：只給本機／預備環境用的「模擬 LINE Pay 付款頁」（/{lang}/pay/<單號>）開關，
  //     對應後端假金流 FakePaymentGateway；正式環境一律不給（NUXT_PUBLIC_SIMULATED_PAYMENT）。
  // 預設值與 apps/web 的 backendApiBase() 相同（本機 dotnet run 的監聽位址 http://127.0.0.1:5299）。
  runtimeConfig: {
    apiInternalBase: 'http://127.0.0.1:5299',
    public: {
      apiBase: 'http://127.0.0.1:5299',
      turnstileSiteKey: '',
      simulatedPayment: false,
    },
  },

  // 🔴 暫時排除舊 mockup 的假資料端點與讀取工具（server/api/charity/**、server/utils/fixtures.ts）：
  // 前台已改接真 API（useCharityApi），這幾個檔案是待刪除的殘留（本輪 agent 的刪檔操作被權限分類器擋下，
  // 留給使用者確認後刪除，見 README「待使用者處理」）。刪除後這個 ignore 設定一併移除。
  ignore: ['server/api/charity/**', 'server/utils/fixtures.ts'],

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
