// app/plugins/site-locale.ts — 讓 <html lang>／og:locale／canonical 大小寫依路由動態切換（S1-13）
//
// 背景（docs/18-work-errors.md E-17）：E-17 當時的結論是「<html lang> 全站固定不隨 club
// 變動，用 nuxt.config.ts 的靜態 app.head.htmlAttrs.lang 最穩」——那時只有 zh 頁面，
// lang 永遠是 zh-Hant，靜態值沒有問題。S1-13 起 /en/... 路由真的存在，<html lang> 需要
// 隨「語系」逐路由變動，靜態值不再夠用，nuxt.config.ts 的 app.head.htmlAttrs.lang 已移除
// （改用 site.defaultLocale 當「這支 plugin 沒跑到時」的保險回退值，見 nuxt.config.ts）。
//
// `@nuxtjs/seo` 的 nuxt-seo-utils 子模組（node_modules/nuxt-seo-utils/dist/runtime/app/
// logic/applyDefaults.js）本來就設計成讀 `siteConfig.currentLocale` 決定 <html lang>／
// og:locale／canonical 大小寫（`resolveCurrentLocale()`），只是 `currentLocale` 這個值
// 預設沒有人餵——nuxt-site-config 只在偵測到 @nuxtjs/i18n 或 nuxt-i18n-micro 時才自動接上
// （node_modules/nuxt-site-config/dist/module.mjs、其 runtime/app/plugins/i18n.js）。
// 本專案沒有裝這兩個模組（docs/13-blue-whale-site.md §6 已經用同一套 site-config-stack
// 機制手動餵 NUXT_PUBLIC_SITE_URL／NUXT_PUBLIC_SITE_NAME，這裡延伸同一個機制餵
// currentLocale，不是新發明的作法，也不是繞過模組的 hack——`updateSiteConfig()` 是
// nuxt-site-config 自己 addImportsDir 出來的公開 composable
// （node_modules/nuxt-site-config/dist/runtime/app/composables/updateSiteConfig.js），
// 跟 @nuxtjs/i18n 整合時走的是同一支函式）。
//
// 因此這支 plugin 手刻 nuxt-site-config:i18n 那支 plugin 做的事：把「目前路由算出來的
// 語系」餵給 site config stack，applyDefaults() 自己會讀到、自動算出正確的
// <html lang>／og:locale／canonical 大小寫，不需要在這裡或任何頁面元件另外呼叫 useHead
// 去跟 nuxt-seo-utils 搶 <html lang> 這個 key（E-17 已證實那條路打不贏：即使
// tagPriority: 'high' 也蓋不掉模組自己的 useHead 呼叫）。
//
// `currentLocale` 傳的是一個讀取 `useRoute().path` 的函式（不是呼叫後的字串值）——
// site-config-stack 的 get() 用 toValue() 解析，每次讀取都會重新呼叫這個函式，
// 因此 SSR 每個請求、以及 client-side 換頁後 route.path 變動時都能拿到最新值，
// 不需要另外 watch route 變化重新 push 一次。
export default defineNuxtPlugin({
  name: 'tcrfc:site-locale',
  setup() {
    const route = useRoute()
    updateSiteConfig({
      _context: 'tcrfc:site-locale',
      currentLocale: () => HREFLANG_MAP[resolveLocaleFromPath(route.path)],
    })
  },
})
