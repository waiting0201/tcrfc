<script setup lang="ts">
// app/pages/index.vue — 由 site/src/pages/index.html 轉來
// 原始頁面用一段內嵌 JS（location.replace('zh/')）做純前端轉址
// （這裡故意不寫出完整的 script 標籤字面文字——SFC 的區塊解析是對原始檔案做
// 標籤比對，不理解「這在註解裡」，寫出完整字面會被誤判成第二個區塊的起訖，
// 這正是本檔一開始 build 失敗的原因，見下方與 docs/18-work-errors.md）。
// ⛔ 依紀律 9 不得把 script 標籤留在 template 裡；改在 script setup 頂層
// （非 onMounted）呼叫 navigateTo，Nuxt 在 SSR 階段會轉成真正的伺服器端
// 302（Nitro sendRedirect），瀏覽器與不執行 JS 的 AI 爬蟲都拿得到正確轉址，
// 比原本純 client-side 的 location.replace 更早生效、也對 SEO 更友善。
// 樣板區塊保留可視連結，等同原頁 noscript 區塊裡那個 <a href="zh/"> 的
// 漸進式強化替代——JS 未執行前也有可點擊的入口。
// 轉址邏輯見 app/middleware/redirect-root.ts（具名 middleware，理由見該檔註解）。
definePageMeta({
  nav: 'home',
  middleware: ['redirect-root'],
})

useSeoMeta({
  title: '台中磐石足球俱樂部 TCRFC',
  description: '台中磐石足球俱樂部官方網站。',
})
</script>

<template>
  <p style="padding:3rem;text-align:center">
    <NuxtLink to="/zh/">進入網站 / Enter site</NuxtLink>
  </p>
</template>
