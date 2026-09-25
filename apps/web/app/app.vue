<script setup lang="ts">
// app/app.vue — 品牌切換的進入點（docs/13-blue-whale-site.md §6）
//
// SSR 階段就決定 data-club，隨 HTML 一起吐出，沒有 hydration mismatch 風險，
// 也不需要動態組 style 標籤字串（紀律 1：顏色永遠只能是 CSS custom properties）。
//
// ⚠️ lang 刻意不在這裡設（曾經試過在這裡動態設，記錄在下面）：
// @nuxtjs/seo 的 nuxt-seo-utils 子模組會自己對 htmlAttrs.lang 呼叫一次
// useHead（依 site.defaultLocale／currentLocale 解析）；實測 htmlAttrs／
// bodyAttrs 的合併是「最後註冊的呼叫覆蓋同一個 key」，不像一般 <meta> 標籤走
// tagPriority 去重，即使這裡指定 tagPriority: 'high' 也蓋不掉（已記入
// docs/18-work-errors.md E-17）。E-17 當時（只有 zh 頁面）的結論是「改用
// nuxt.config.ts 的靜態 app.head.htmlAttrs.lang」——S1-13 起 /en/... 路由
// 真的存在，lang 需要逐路由變動，靜態值不再適用，改成 app/plugins/
// site-locale.ts 把「目前路由算出來的語系」餵進 nuxt-site-config 的
// currentLocale，讓 nuxt-seo-utils 自己算出正確的 <html lang>（見該檔案的
// 完整說明），這裡只留會隨 club 變動的 data-club，不重複處理 lang。
const config = useRuntimeConfig()
const club = computed<ClubCode>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const assets = computed(() => getClubAssets(club.value))

// ── 追蹤碼（S1-12，主站規劃書 §4.8 H「追蹤碼管理」）───────────────────────────
// 走既有的 /api/backend/{club}/... 同源代理（見 server/api/backend/[...path].ts 檔頭），
// 不直接呼叫 backendApiBase()——那支函式只在伺服器端可用，這裡的請求要同時支援 SSR 與
// client-side（例如切換俱樂部後的 client-only 導覽）。個別 ID 未設定（後台尚未填寫）時
// 對應腳本整段不輸出，不送出空字串當參數——那樣仍會建立分析工作階段，只是收不到有意義的資料。
const { data: seoSettings } = await useFetch(() => `/api/backend/${club.value}/seo/settings`)

useHead(() => {
  const scripts: Array<{ innerHTML?: string, src?: string, async?: boolean }> = []
  const s = seoSettings.value

  if (s?.ga4MeasurementId) {
    scripts.push({ src: `https://www.googletagmanager.com/gtag/js?id=${s.ga4MeasurementId}`, async: true })
    scripts.push({
      innerHTML: `window.dataLayer=window.dataLayer||[];function gtag(){dataLayer.push(arguments);}gtag('js',new Date());gtag('config','${s.ga4MeasurementId}');`,
    })
  }

  if (s?.gtmContainerId) {
    scripts.push({
      innerHTML: `(function(w,d,s,l,i){w[l]=w[l]||[];w[l].push({'gtm.start':new Date().getTime(),event:'gtm.js'});var f=d.getElementsByTagName(s)[0],j=d.createElement(s),dl=l!='dataLayer'?'&l='+l:'';j.async=true;j.src='https://www.googletagmanager.com/gtm.js?id='+i+dl;f.parentNode.insertBefore(j,f);})(window,document,'script','dataLayer','${s.gtmContainerId}');`,
    })
  }

  if (s?.metaPixelId) {
    scripts.push({
      innerHTML: `!function(f,b,e,v,n,t,s){if(f.fbq)return;n=f.fbq=function(){n.callMethod?n.callMethod.apply(n,arguments):n.queue.push(arguments)};if(!f._fbq)f._fbq=n;n.push=n;n.loaded=!0;n.version='2.0';n.queue=[];t=b.createElement(e);t.async=!0;t.src=v;s=b.getElementsByTagName(e)[0];s.parentNode.insertBefore(t,s)}(window,document,'script','https://connect.facebook.net/en_US/fbevents.js');fbq('init','${s.metaPixelId}');fbq('track','PageView');`,
    })
  }

  if (s?.lineTagId) {
    scripts.push({
      innerHTML: `(function(g,d,o){g._ltq=g._ltq||[];g._ltq.push(['init','${s.lineTagId}']);g._ltq.push(['track','PageView']);var s=d.createElement(o);s.async=1;s.src='https://d.line-scdn.net/n/line_tag/public/release/v1/lt.js';d.getElementsByTagName(o)[0].parentNode.insertBefore(s,d.getElementsByTagName(o)[0]);})(window,document,'script');`,
    })
  }

  return { script: scripts }
})

useHead(() => ({
  htmlAttrs: {
    'data-club': club.value,
  },
  link: [
    // tcrfc.css 是視覺的唯一真實來源，整份原封放在 public/、以純靜態資源載入
    // （不透過 Vite css pipeline，避免任何一位元被改動，見 nuxt.config.ts 的 css: [] 註解）。
    { rel: 'stylesheet', href: '/assets/css/tcrfc.css' },
    // 藍鯨色票覆寫檔，載入順序必須在 tcrfc.css 之後。tcrfc 站台下這份檔案沒有任何
    // 選擇器會命中（見 club-bw.css 的 :root[data-club='bw'] 前綴），故兩站共用同一份
    // <link> 清單也安全。
    { rel: 'stylesheet', href: '/assets/css/club-bw.css' },
    ...assets.value.favicon.map((icon) => ({
      rel: 'icon',
      href: icon.href,
      type: icon.type,
      sizes: icon.sizes,
    })),
    { rel: 'apple-touch-icon', href: assets.value.appleTouchIcon },
  ],
  meta: [
    { name: 'theme-color', content: assets.value.themeColor },
    { property: 'og:image', content: assets.value.ogImage },
  ],
}))
</script>

<template>
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
</template>
