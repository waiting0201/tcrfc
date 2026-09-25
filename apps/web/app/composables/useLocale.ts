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

  return { locale, otherLocale, lp, switchTo }
}
