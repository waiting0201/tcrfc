// shared/utils/locale.ts — 多語系框架的單一真實來源（S1-13，docs/05-i18n-seo.md §1）
//
// 🔴 新增語系（例如日後真的要做日文）只改這裡（SUPPORTED_LOCALES／HREFLANG_MAP 兩張表），
// 不得在其他檔案（nuxt.config.ts 的路由複製、SiteHeader／SiteFooter 的語系切換器、
// server/routes/sitemap.xml.ts 的 hreflang、app/layouts/default.vue 的 hreflang）
// 各自寫一份語系清單或判斷式——這是規劃書「擴充：新增語系不需改程式」（docs/05 §1）
// 的落實方式：所有呼叫點都只讀這裡的常數與函式，不重新判斷一次「有哪些語系」。
//
// 目前只有 zh／en 兩個語系上線（繁中預設、英文次要），日文列為後續評估、不實作，
// 但這個陣列的形狀已經是「加一個字串就好」，架構已預留（docs/05 §1「擴充」欄）。

export type LocaleCode = 'zh' | 'en'

export const SUPPORTED_LOCALES: readonly LocaleCode[] = ['zh', 'en'] as const

/** 預設語系＝繁體中文（docs/05-i18n-seo.md §1：「語系：繁體中文（預設）／英文」）。 */
export const DEFAULT_LOCALE: LocaleCode = 'zh'

/**
 * URL 路徑前綴／<html lang>／hreflang 三者共用同一張對照表（docs/05 §1：
 * 「hreflang：zh-Hant、en，加上 x-default」）。x-default 不在這張表裡，
 * 它固定指向 DEFAULT_LOCALE 對應的網址，由呼叫端另外組出來。
 */
export const HREFLANG_MAP: Record<LocaleCode, string> = {
  zh: 'zh-Hant',
  en: 'en',
}

export function isLocaleCode(value: unknown): value is LocaleCode {
  return typeof value === 'string' && (SUPPORTED_LOCALES as readonly string[]).includes(value)
}

/**
 * 從路徑判斷語系：`/zh/...` → 'zh'、`/en/...` → 'en'。
 * 傳入不帶語系前綴的路徑（理論上不該發生，路由層已經一律帶前綴）時回退預設語系，
 * 不丟例外——這支函式在 SSR 每個請求、每個 useHead／useFetch 都會被呼叫，容錯比嚴格更重要。
 */
export function resolveLocaleFromPath(path: string): LocaleCode {
  const seg = path.split('/').filter(Boolean)[0]
  return isLocaleCode(seg) ? seg : DEFAULT_LOCALE
}

/**
 * 把一個帶語系前綴的路徑換成另一個語系版本：
 * localizePath('/zh/about/', 'en') → '/en/about/'
 * localizePath('/zh/faq/#q-123', 'en') → '/en/faq/#q-123'（query／hash 一併保留，
 * 呼叫端可以直接傳 route.fullPath）
 *
 * 傳入不是 /zh/ 或 /en/ 開頭的路徑（外部連結、錨點、根路徑 `/` 這種尚未加語系前綴的
 * 特例頁面）原樣返回，不強加前綴——現有 SiteHeader／SiteFooter／各頁樣板既有的連結
 * 一律已經是 /zh/... 開頭，呼叫端不需要先判斷格式對不對。
 */
export function localizePath(path: string, locale: LocaleCode): string {
  const match = path.match(/^\/(zh|en)(\/.*|$)/)
  if (!match) return path
  return `/${locale}${match[2] || '/'}`
}
