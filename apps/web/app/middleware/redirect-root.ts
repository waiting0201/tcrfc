// app/middleware/redirect-root.ts — 首頁的站根轉址，供 app/pages/index.vue 使用。
// 原頁（site/src/pages/index.html）用 <script>location.replace('zh/')</script>，
// 改用具名 route middleware（而不是 definePageMeta 裡塞一個行內函式）——後者會讓
// Nuxt 的 definePageMeta 巨集編譯器對這個頁面多產生一個沒有 lang="ts" 標記的
// <script> 區塊，跟 <script setup lang="ts"> 衝突導致 build 失敗
// （[@vue/compiler-sfc] <script> and <script setup> must have the same language
// type，已記入 docs/18-work-errors.md）。具名 middleware 檔沒有這個限制。
//
// 🔵 S1-13 判斷（規劃書與 docs/05-i18n-seo.md 都沒有明講站根轉址要不要看瀏覽器語言，
// 只定義了「繁中預設」與各頁面內容層級的 fallback 規則）：既然 /en/... 現在是真的
// 路由，站根轉址依 Accept-Language 判斷訪客偏好、只在明確偏好英文時才轉去 /en/，
// 其餘情況（含未表態、偏好其他語言）一律回退預設語系 zh——比照 docs/05 §1「語系：
// 繁體中文（預設）」，不是新增規格，只是站根這個特例頁面沿用同一條預設值規則的
// 自然延伸判斷。
import type { LocaleCode } from '#shared/utils/locale'

export default defineNuxtRouteMiddleware(() => {
  const locale = resolveRootRedirectLocale()
  return navigateTo(`/${locale}/`, { redirectCode: 302 })
})

function resolveRootRedirectLocale(): LocaleCode {
  const acceptLanguage = import.meta.server
    ? useRequestHeaders(['accept-language'])['accept-language']
    : (import.meta.client ? navigator.language : undefined)
  const primary = acceptLanguage?.split(',')[0]?.trim().toLowerCase()
  return primary?.startsWith('en') ? 'en' : DEFAULT_LOCALE
}
