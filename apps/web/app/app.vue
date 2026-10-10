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
  if (!url) {
    // 沒有後台上傳的分享圖：沿用前台靜態資產，Alt 取俱樂部名稱（不發明描述）。
    const name = isEn.value ? (club.value === 'bw' ? BW_NAME_EN : CLUB_NAME_EN) : assets.value.nameZh
    return { url: assets.value.ogImage, width: undefined, height: undefined, alt: name }
  }
  // Alt 依語系取後台 SEO 設定；英文空白回退繁中；都空則不輸出（不用錯誤的圖說頂替）。
  const alt = imgAlt(isEn.value ? s?.ogImageAltEn : undefined, s?.ogImageAltZh) || undefined
  return { url, width: s?.ogImageWidth ?? undefined, height: s?.ogImageHeight ?? undefined, alt }
})
useSeoMeta({
  description: () => seoDefaultDescription.value,
  ogImage: () => seoOgImage.value.url,
  ogImageWidth: () => seoOgImage.value.width,
  ogImageHeight: () => seoOgImage.value.height,
  ogImageAlt: () => seoOgImage.value.alt,
}, { tagPriority: 'low' })

const route = useRoute()
// 首頁（含英文版）：/zh、/zh/、/en、/en/
const isHome = computed(() => /^\/(zh|en)\/?$/.test(route.path))
/** 靜態 CSS 網址：走版本化路徑 /assets/css-<內容雜湊>/（nuxt.config.ts nitro.publicAssets，一年 immutable）；
 *  雜湊算不出來時退回原路徑 /assets/css/（等同舊行為）。 */
function cssHref(name: 'tcrfc' | 'club-bw' | 'member' | 'shop'): string {
  const v = config.public.cssVersion as string | undefined
  return v ? `/assets/css-${v}/${name}.css` : `/assets/css/${name}.css`
}

useHead(() => ({
  htmlAttrs: {
    'data-club': club.value,
  },
  link: [
    // tcrfc.css 是視覺的唯一真實來源，整份原封放在 public/、以純靜態資源載入
    // （不透過 Vite css pipeline，避免任何一位元被改動，見 nuxt.config.ts 的 css: [] 註解）。
    // 網址走版本化路徑 /assets/css-<內容雜湊>/（nuxt.config.ts cssVersion／nitro.publicAssets）：檔名沒有雜湊，
    // 靠路徑版本化才能放心一年長快取，改版後 URL 變動、使用者不會拿到舊 CSS。
    { rel: 'stylesheet', href: cssHref('tcrfc') },
    // 藍鯨色票覆寫檔，載入順序必須在 tcrfc.css 之後。檔內所有選擇器都以 :root[data-club='bw'] 開頭，
    // 磐石站不會命中任何一條，因此只在藍鯨站輸出這條 <link>（少一個阻塞渲染的請求）。
    ...(club.value === 'bw' ? [{ rel: 'stylesheet', href: cssHref('club-bw') }] : []),
    // S2-11／S3-2：會員中心與 08 文化互動的補充樣式（tcrfc.css 一個位元都不改，新元件樣式獨立成檔；
    // 只用 tcrfc.css 的 design tokens，兩個俱樂部自動換色）。
    // 首頁（/zh/、/en/）用不到：首頁沒有任何元件使用 mc-*／member-card／reader／fe-card 等類別，
    // 也沒有 form-field 表單，故首頁不阻塞載入、改成 prefetch（從首頁點進會員／文化頁時多半已在快取）。
    // 其他頁面的共用選擇器（.form-field、.table-scroll 等）太多，不逐路由判斷，維持原樣載入。
    isHome.value
      ? { rel: 'prefetch', as: 'style', href: cssHref('member') }
      : { rel: 'stylesheet', href: cssHref('member') },
    // S3-5／S3-9：站內商店與積分榜、球員數據的補充樣式（同樣只用 design tokens）。
    // 🔴 首頁要保留：首頁的商店入口區塊用到 .sh-entry-*，且 .field-error／.form-field 等共用規則散在各表單頁。
    { rel: 'stylesheet', href: cssHref('shop') },
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
