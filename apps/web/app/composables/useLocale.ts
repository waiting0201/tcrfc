// app/composables/useLocale.ts — 頁面／元件用的語系存取點（S1-13）
//
// 所有需要「目前是 zh 還是 en」「切換語系連結怎麼組」的地方都呼叫這支 composable，
// 不要各自 useRoute().path.startsWith('/en') 判斷一次——shared/utils/locale.ts 已經是
// 單一真實來源，這裡只是把它包成 Nuxt 元件慣用的 computed／函式介面。
//
// 🔴 型別特別 import：本專案既有慣例是 shared/utils 的「函式」值一律用 auto-import
// （見 getClubAssets／isUnitEnabledForClub 在 app/ 各檔案的既有用法，沒有任何一處手動
// import），但沒有任何既有先例驗證過「型別」也吃得到同一套 auto-import——為了不在一個
// 新機制上疊另一個沒驗證過的假設，型別一律走 Nuxt 4 內建的 #shared 別名明確 import。
import type { LocaleCode } from '#shared/utils/locale'

export function useLocale() {
  const route = useRoute()

  const locale = computed<LocaleCode>(() => resolveLocaleFromPath(route.path))
  const otherLocale = computed<LocaleCode>(() => (locale.value === 'zh' ? 'en' : 'zh'))

  /** 把「一個 /zh/... 或 /en/... 路徑」換成目前語系版本，供樣板裡的連結使用。 */
  function lp(path: string): string {
    return localizePath(path, locale.value)
  }

  /** 切換到另一個語系，停留在目前這一頁（docs/05 §1「切換行為：停留於當前頁的
   * 對應語系版本，不要跳回首頁」），用 route.fullPath 保留 query／hash。 */
  function switchTo(target: LocaleCode) {
    return navigateTo(localizePath(route.fullPath, target))
  }

  /**
   * 是否「顯示英文文案」（樣板用：`v-if="isEn"` 切換整段含標記的英文版面）。
   * 主站（`tcrfc`）：`/en/` 一律 true。
   * 藍鯨（`bw`，B-5 於 2026-10-05 定案後開放英文版）：`/en/` 且該頁宣告 `enReadyBw: true` 才為 true——
   * 藍鯨英文是逐頁翻完才開，**尚未翻的頁面整頁維持繁中（連同共用頁首頁尾）＋整頁提示**，不出現英文介面配中文內容、
   * 更不會把主站英文（Taichung Rock FC）誤植到藍鯨。要判斷「URL 是不是 /en/」請用 `locale.value === 'en'`。
   */
  const club = useRuntimeConfig().public.club
  const isEn = computed(() => locale.value === 'en' && (club !== 'bw' || route.meta.enReadyBw === true))

  /**
   * 行內雙語取值：`tx('首頁', 'Home')`。zh 版回傳第一個參數，en 版回傳第二個。
   * 用在樣板文字、屬性（`:aria-label="tx(..)"`）與 script 內的字串；整段含 `<strong>`／`<a>`
   * 的長文改用 `<template v-if="isEn">…</template><template v-else>…</template>`，
   * 兩種寫法都讓 zh 版 DOM 與翻譯前逐字相同（compare-dom 不受影響）。
   * en 參數只放「自然的英文」；專有名詞照 docs/06 §1.1 英文用詞對照表。
   */
  function tx(zh: string, en: string): string {
    return isEn.value ? en : zh
  }

  return { locale, otherLocale, isEn, lp, switchTo, tx }
}
