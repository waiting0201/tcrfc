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

// E-1：後台填的追蹤碼是「會被拼進腳本與網址」的字串，前台輸出前再以白名單格式驗證一次（後端也驗，這是第二道）。
// 不符格式就整段不輸出；網址參數另以 encodeURIComponent，腳本內嵌值以 JSON.stringify 輸出成字串字面值，
// 而不是用字串模板直接插入單引號內。
const TRACKING_ID_PATTERNS = {
  ga4: /^G-[A-Z0-9]+$/,
  gtm: /^GTM-[A-Z0-9]+$/,
  metaPixel: /^\d+$/,
  lineTag: /^[A-Za-z0-9-]+$/,
} as const
function trackingId(raw: unknown, kind: keyof typeof TRACKING_ID_PATTERNS): string | null {
  if (typeof raw !== 'string') return null
  const v = raw.trim()
  return TRACKING_ID_PATTERNS[kind].test(v) ? v : null
}

useHead(() => {
  const scripts: Array<{ innerHTML?: string, src?: string, async?: boolean }> = []
  const s = seoSettings.value
  const ga4 = trackingId(s?.ga4MeasurementId, 'ga4')
  const gtm = trackingId(s?.gtmContainerId, 'gtm')
  const metaPixel = trackingId(s?.metaPixelId, 'metaPixel')
  const lineTag = trackingId(s?.lineTagId, 'lineTag')

  if (ga4) {
    scripts.push({ src: `https://www.googletagmanager.com/gtag/js?id=${encodeURIComponent(ga4)}`, async: true })
    scripts.push({
      innerHTML: `window.dataLayer=window.dataLayer||[];function gtag(){dataLayer.push(arguments);}gtag('js',new Date());gtag('config',${JSON.stringify(ga4)});`,
    })
  }

  if (gtm) {
    scripts.push({
      innerHTML: `(function(w,d,s,l,i){w[l]=w[l]||[];w[l].push({'gtm.start':new Date().getTime(),event:'gtm.js'});var f=d.getElementsByTagName(s)[0],j=d.createElement(s),dl=l!='dataLayer'?'&l='+l:'';j.async=true;j.src='https://www.googletagmanager.com/gtm.js?id='+i+dl;f.parentNode.insertBefore(j,f);})(window,document,'script','dataLayer',${JSON.stringify(gtm)});`,
    })
  }

  if (metaPixel) {
    scripts.push({
      innerHTML: `!function(f,b,e,v,n,t,s){if(f.fbq)return;n=f.fbq=function(){n.callMethod?n.callMethod.apply(n,arguments):n.queue.push(arguments)};if(!f._fbq)f._fbq=n;n.push=n;n.loaded=!0;n.version='2.0';n.queue=[];t=b.createElement(e);t.async=!0;t.src=v;s=b.getElementsByTagName(e)[0];s.parentNode.insertBefore(t,s)}(window,document,'script','https://connect.facebook.net/en_US/fbevents.js');fbq('init',${JSON.stringify(metaPixel)});fbq('track','PageView');`,
    })
  }

  if (lineTag) {
    scripts.push({
      innerHTML: `(function(g,d,o){g._ltq=g._ltq||[];g._ltq.push(['init',${JSON.stringify(lineTag)}]);g._ltq.push(['track','PageView']);var s=d.createElement(o);s.async=1;s.src='https://d.line-scdn.net/n/line_tag/public/release/v1/lt.js';d.getElementsByTagName(o)[0].parentNode.insertBefore(s,d.getElementsByTagName(o)[0]);})(window,document,'script');`,
    })
  }

  return { script: scripts }
})

// ── B-3：全站 SEO 預設（標題樣板、預設描述、預設 OG 圖）──────────────────────────────
// 後台「全站搜尋與分享設定」填的值。優先序：頁面自己設的 > 後台全站預設 > getClubAssets 寫死值。
// 描述與 OG 圖用 tagPriority 'low'，頁面自己的 useSeoMeta（預設優先序）一定蓋得過。
const { isEn } = useLocale()
const TITLE_PLACEHOLDER = /\{標題\}|\{title\}|%s/
const titleTemplateRaw = computed(() => {
  const s = seoSettings.value
  const tpl = ((isEn.value ? s?.titleTemplateEn : s?.titleTemplateZh) ?? '').trim()
  return tpl && TITLE_PLACEHOLDER.test(tpl) ? tpl : null
})
function applyTitleTemplate(tpl: string, title?: string): string | undefined {
  if (!title) return title
  // 頁面標題已自帶樣板的固定尾巴（例如站名）就不重複附加
  const fixed = tpl.replace(TITLE_PLACEHOLDER, '').replace(/^[\s｜|\-–—:：·]+|[\s｜|\-–—:：·]+$/g, '')
  if (fixed && title.includes(fixed)) return title
  return tpl.replace(TITLE_PLACEHOLDER, () => title)
}
// 後台沒設（或沒有佔位字）時不註冊，沿用 nuxt-seo-utils 的預設樣板
useHead(() => {
  const tpl = titleTemplateRaw.value
  return tpl ? { titleTemplate: (title?: string) => applyTitleTemplate(tpl, title) } : {}
})

const seoDefaultDescription = computed(() => {
  const s = seoSettings.value
  return ((isEn.value ? s?.defaultDescriptionEn : s?.defaultDescriptionZh) ?? '').trim() || undefined
})
const seoOgImage = computed(() => {
  const s = seoSettings.value
  const url = typeof s?.ogImageUrl === 'string' && s.ogImageUrl ? s.ogImageUrl : null
  if (!url) return { url: assets.value.ogImage, width: undefined, height: undefined }
  return { url, width: s?.ogImageWidth ?? undefined, height: s?.ogImageHeight ?? undefined }
})
useSeoMeta({
  description: () => seoDefaultDescription.value,
  ogImage: () => seoOgImage.value.url,
  ogImageWidth: () => seoOgImage.value.width,
  ogImageHeight: () => seoOgImage.value.height,
}, { tagPriority: 'low' })

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
    // S2-11／S3-2：會員中心與 08 文化互動的補充樣式（tcrfc.css 一個位元都不改，新元件樣式獨立成檔；
    // 只用 tcrfc.css 的 design tokens，兩個俱樂部自動換色）。
    { rel: 'stylesheet', href: '/assets/css/member.css' },
    // S3-5／S3-9：站內商店與積分榜、球員數據的補充樣式（同樣只用 design tokens）。
    { rel: 'stylesheet', href: '/assets/css/shop.css' },
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
  ],
}))
</script>

<template>
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
</template>
