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
</script>

<template>
  <a class="skip-link" href="#main">跳至主要內容</a>
  <SiteHeader />
  <main id="main">
    <slot />
  </main>
  <SiteFooter />
</template>
