<script setup lang="ts">
// app/app.vue — 品牌切換的進入點（docs/13-blue-whale-site.md §6）
//
// SSR 階段就決定 data-club，隨 HTML 一起吐出，沒有 hydration mismatch 風險，
// 也不需要動態組 style 標籤字串（紀律 1：顏色永遠只能是 CSS custom properties）。
//
// ⚠️ lang="zh-Hant" 刻意不在這裡設（曾經在這裡設過，記錄在下面）：
// @nuxtjs/seo 的 nuxt-seo-utils 子模組會自己對 htmlAttrs.lang 呼叫一次
// useHead（依 site.defaultLocale／currentLocale 解析，預設回退 'en'）；
// 實測 htmlAttrs／bodyAttrs 的合併是「最後註冊的呼叫覆蓋同一個 key」，
// 不像一般 <meta> 標籤走 tagPriority 去重，即使這裡指定 tagPriority: 'high'
// 也蓋不掉，lang 會悄悄變成 en（已記入 docs/18-work-errors.md）。
// 唯一穩定生效的位置是 nuxt.config.ts 的 app.head.htmlAttrs.lang——那份是
// nuxt-seo-utils 自己註解「give nuxt.config values higher priority」時
// 真正指的來源。lang 全站固定為 zh-Hant（不隨 club 變動），故放靜態設定即可，
// 這裡只留會隨 club 變動的 data-club。
const config = useRuntimeConfig()
const club = computed<ClubCode>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const assets = computed(() => getClubAssets(club.value))

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
