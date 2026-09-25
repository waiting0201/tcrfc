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
  // defaultLocale 設為 zh-Hant：這是「app/plugins/site-locale.ts 沒有跑到時」的保險
  // 回退值（nuxt-seo-utils 的 resolveCurrentLocale() 讀不到 currentLocale 時，回退讀
  // defaultLocale，讀不到才回退 'en'，見 applyDefaults.js），S1-13 起 <html lang>／
  // og:locale／canonical 大小寫的「真正生效」機制改成逐路由動態算（zh 頁面 zh-Hant、
  // en 頁面 en），見 app/plugins/site-locale.ts 檔頭說明與 docs/18-work-errors.md E-17
  // 的後續發展——E-17 當時（只有 zh 頁面）建議的「用 app.head.htmlAttrs.lang 寫死靜態值
  // 最穩」已經不適用，這裡刻意不再設那個鍵。
  //
  // 🔴 site.name 這裡的 'TCRFC' 只是本機開發預設值，不是實際輸出值——
  // 文案依俱樂部切換機制上線時發現：nuxt-site-config 對 name 用的是跟 url
  // 完全同一套 priority-stack，NUXT_PUBLIC_SITE_NAME 這個 runtime 環境變數
  // 會覆寫這裡的值（同一份 build、只改 env 就變更，已實測 og:site_name／
  // <title> 後綴／Schema.org WebSite.name 三處都正確跟著換，見
  // docs/13-blue-whale-site.md §6 紀律 11 與 apps/web/README.md 環境變數表）。
  // 藍鯨容器啟動時必須明確帶 NUXT_PUBLIC_SITE_NAME=台中藍鯨，否則這三處會
  // 悄悄顯示 'TCRFC'——不是規劃書規格，是這裡的預設值外洩。
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

  // 🔴 S1-12 驗收退回後補做（2026-09-25）：完全關閉 @nuxtjs/robots 的內建 /robots.txt 路由，
  // 改由 server/routes/robots.txt.ts 自組——理由跟 docs/18-work-errors.md E-18（sitemap 那次）
  // 完全同一個模式，不是巧合：這個模組同樣是「建置期固定產生一份規則」的設計，沒有辦法依
  // runtime 才知道的 NUXT_PUBLIC_SITE_ENV（prelaunch／production）切換輸出內容——本輪要做的
  // 「robots.txt 線上編輯」必須依正式環境旗標決定要不要真的輸出後台編輯的自訂規則，上線前
  // （或旗標未設定）一律要維持封鎖版，全站 noindex（CLAUDE.md 第 5 條）不得因為這個新功能而鬆動。
  // enabled: false 沿用既有 sitemap: { enabled: false } 同一種乾淨關閉開關，見該檔案的既有註解。
  robots: {
    enabled: false,
  },

  // 🔴 2026-09-22：完全關閉 @nuxtjs/sitemap 的內建 /sitemap.xml 路由（docs/18-work-errors.md
  // E-18 補上真正根因）。追查 node_modules 原始碼確認：它的產生邏輯會把「路徑命中
  // X-Robots-Tag: noindex 的 route rule」的網址整批排除，本站上線前對 `/**` 全站蓋一條
  // noindex 標頭（CLAUDE.md 第 5 條），所以每一筆候選網址都被排除、urlset 恆為空，
  // 且沒有設定能繞過這個檢查。改由 server/routes/sitemap.xml.ts 自組 XML 接手，
  // 資料來源是 server/utils/sitemap-urls.ts（單元開關呼叫點 3，仍是唯一一份
  // isUnitEnabledForClub → getEnabledSiteUnits 的判斷，只是被兩處共用）。
  // server/api/__sitemap__/urls.ts 保留作為可獨立 curl 驗證的資料端點。
  sitemap: {
    enabled: false,
  },

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
    // 上面兩條的 /en/ 對應版本（S1-13）：routeRules 是純路徑比對，不會像
    // pages:extend 那樣自動複製，這裡手動補上、目的地一併換成 /en/，否則 en 訪客
    // 撞到舊網址會被轉去 zh 頁面，跳出目前的語系。
    '/en/join/childrens-training/**': { redirect: { to: '/en/join/academy/', statusCode: 301 } },
    '/en/join/sponsorship/**': { redirect: { to: '/en/join/partnership/', statusCode: 301 } },
  },

  nitro: {
    // Dockerfile 用 `node .output/server/index.mjs` 直接執行，標準 Node 部署，
    // 不特化其他 Nitro preset（見 Dockerfile 檔頭註解）。
    preset: 'node-server',
  },

  eslint: {
    config: { stylistic: false },
  },

  // 🔴 S1-13 多語系框架：/zh/... 每一頁自動複製出一份 /en/... 孿生路由，共用同一個
  // component 檔案（docs/05-i18n-seo.md §1「URL：/zh/…、/en/… 獨立網址」、
  // 「擴充：新增語系不需改程式」）。這是本專案「單一真實來源」的一貫做法（比照
  // docs/13-blue-whale-site.md §6 紀律 3 的 isUnitEnabledForClub／單元開關單一真實
  // 來源）延伸到語系——只在 app/pages/zh/ 底下新增頁面，這裡自動生成對應的 /en/
  // 路由，不必手動複製 80 個檔案，也不會有兩份路由各自維護、彼此漏改的風險。
  //
  // definePageMeta() 宣告的 meta（unit／nav／bodyClass 等）綁在「檔案」上，不是綁在
  // pages:extend 這裡看到的路由項目上——Nuxt 對每個 component 檔案各自靜態分析一次
  // definePageMeta()，克隆出來的 /en/... 路由指向同一個 file，因此會拿到與 /zh/...
  // 完全相同的 meta（已用 unit-gate 對 bw 容器 curl /en/womens/ 實測回 404，證明
  // meta.unit 確實隨檔案而非路由項目生效，見 apps/web/README.md 的驗收紀錄）。
  //
  // 尚無真實英文內容的頁面（S1-13 當下是全部 79 頁，S1-14 起逐步減少）落地後，
  // 頁面本身仍顯示繁中內容並疊加「本頁尚無此語系版本」提示——見
  // app/components/LocaleFallbackNotice.vue，行為依 docs/05-i18n-seo.md §1
  // 「Fallback」規則（未翻譯內容顯示繁中並標示，不是整頁不存在／404）。
  //
  // 根路徑 `/`（app/pages/index.vue，語系偵測轉址頁）不複製——它本身不帶語系前綴，
  // 職責是把訪客導去 /zh/ 或 /en/，見 app/middleware/redirect-root.ts。
  hooks: {
    'pages:extend'(pages) {
      const clones: typeof pages = []
      for (const page of pages) {
        if (page.path === '/zh' || page.path.startsWith('/zh/')) {
          clones.push({
            ...page,
            name: `${page.name}-en`,
            path: page.path === '/zh' ? '/en' : `/en${page.path.slice('/zh'.length)}`,
          })
        }
      }
      pages.push(...clones)
    },
  },

  app: {
    head: {
      // 🔴 lang 刻意不寫在這裡——S1-13 起需要逐路由（zh／en）動態變化，靜態值只能
      // 二選一。真正生效的機制改成 app/plugins/site-locale.ts 動態餵給 nuxt-seo-utils，
      // 詳見該檔案與 docs/18-work-errors.md E-17 的後續說明。
    },
  },
})
