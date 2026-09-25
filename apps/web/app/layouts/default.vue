<!-- app/layouts/default.vue — 由 site/src/partials/shell.html 的 body 骨架轉來
     （<head> 內容改由 app/app.vue 與各頁面的 useHead／useSeoMeta 負責，
     見 docs/14-invariants.md「<head> 不受本條約束」）。
     skip-link → header → <main id="main"> → footer，DOM 順序與 class 一律不動。
     🔴 S0-9m（2026-09-23）：template 刻意用 Vue 3 的多根節點（fragment）寫法，不包一層
     <div>——mockup 的 <body> 底下這幾個元素是直屬子節點，沒有任何包裹層（site/dist 實測），
     包一層 <div> 會讓 <body> 多一層 compare-dom.mjs 擴大到 body 範圍後比不起來的結構差異。
     這層 <div> 本來就沒有 class／id，tcrfc.css 全文也沒有任何 `body > div` 這類子選擇器
     依賴它存在（已 grep 核對），拿掉不影響任何樣式。 -->
<script setup lang="ts">
// S0-9m（2026-09-23）：比對範圍擴大到整個 <body> 後，發現 mockup 的 <body> 帶 class
// 屬性（77 頁是 `class=""`，zh/index／zh/schedule／zh/charity/ 三頁另外各帶一個
// 頁面專屬類別 page-home／page-schedule／page-charity），Nuxt 端完全沒有輸出這個屬性
// ——之前的比對範圍只到 <main>，看不到 <body> 自己的屬性，所以這個落差一直沒被抓到。
// docs/14-invariants.md 的不變量明文要求「body 的...class 名稱...一律不動」，即使
// 目前 tcrfc.css 全文沒有任何選擇器讀取這幾個 class（已 grep 核對，現狀零視覺影響），
// 仍照不變量逐字補回去，不是因為它有作用才補。
// 3 個需要非空 class 的頁面用既有的 definePageMeta（比照 activeNav 用的 route.meta.nav
// 那一套機制）各自宣告 bodyClass，這裡統一讀出來、預設空字串，跟 mockup 77 頁的
// `class=""` 對齊。
const route = useRoute()
useHead(() => ({
  bodyAttrs: { class: (route.meta.bodyClass as string | undefined) ?? '' },
}))

// ── S1-13 多語系框架：hreflang alternate 連結 ─────────────────────────────
// 放在共用 layout（每一頁都會經過這裡），不是各頁各自加一份——docs/05-i18n-seo.md
// §1「hreflang：zh-Hant、en，加上 x-default」對「每一頁」都成立，是版型層級的規則，
// 不是內容層級的規則。@nuxtjs/seo 沒裝 @nuxtjs/i18n 就不會自動產生這組標籤（同一份
// 「模組需要 i18n 模組才會自動處理多語系」的限制，site-locale.ts 已經在 <html lang>
// 那件事上繞過一次），這裡手刻。
//
// x-default 固定指向繁中版本（docs/05 §1「語系：繁體中文（預設）」），不是「目前這一
// 語系」——這是 hreflang 規格本身的語意（x-default 是「不符合任何列出語系時」的預設
// 導向，不是「目前頁面的語系」）。
const siteConfig = useSiteConfig()
const { locale, otherLocale } = useLocale()
useHead(() => {
  const siteUrl = (siteConfig.url ?? '').replace(/\/$/, '')
  const zhPath = locale.value === 'zh' ? route.fullPath : localizePath(route.fullPath, 'zh')
  return {
    link: [
      { rel: 'alternate', hreflang: HREFLANG_MAP[locale.value], href: `${siteUrl}${route.fullPath}` },
      { rel: 'alternate', hreflang: HREFLANG_MAP[otherLocale.value], href: `${siteUrl}${localizePath(route.fullPath, otherLocale.value)}` },
      { rel: 'alternate', hreflang: 'x-default', href: `${siteUrl}${zhPath}` },
    ],
  }
})

// ── S1-13：「本頁尚無此語系版本」提示 ─────────────────────────────────────
// docs/05-i18n-seo.md §1 Fallback 規則。en 頁面預設一律顯示（S1-13 當下沒有任何一頁
// 真的翻譯完成）；真的有英文內容的頁面用 definePageMeta({ enReady: true }) 關掉，
// 這個旗標與 unit／nav／bodyClass 同一種機制，不另開一套判斷式。
const showLocaleFallbackNotice = computed(() => locale.value === 'en' && !route.meta.enReady)
</script>

<template>
  <a class="skip-link" href="#main">跳至主要內容</a>
  <SiteHeader />
  <LocaleFallbackNotice v-if="showLocaleFallbackNotice" />
  <main id="main">
    <slot />
  </main>
  <SiteFooter />
</template>
