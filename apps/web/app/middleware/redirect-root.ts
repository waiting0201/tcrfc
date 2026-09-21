// app/middleware/redirect-root.ts — 首頁的站根轉址，供 app/pages/index.vue 使用。
// 原頁（site/src/pages/index.html）用 <script>location.replace('zh/')</script>，
// 改用具名 route middleware（而不是 definePageMeta 裡塞一個行內函式）——後者會讓
// Nuxt 的 definePageMeta 巨集編譯器對這個頁面多產生一個沒有 lang="ts" 標記的
// <script> 區塊，跟 <script setup lang="ts"> 衝突導致 build 失敗
// （[@vue/compiler-sfc] <script> and <script setup> must have the same language
// type，已記入 docs/18-work-errors.md）。具名 middleware 檔沒有這個限制。
export default defineNuxtRouteMiddleware(() => {
  return navigateTo('/zh/', { redirectCode: 302 })
})
