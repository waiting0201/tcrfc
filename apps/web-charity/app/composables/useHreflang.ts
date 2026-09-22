// app/composables/useHreflang.ts — hreflang 三組（zh-Hant／en／x-default），規劃書 §2.4 明文要求
// 保留（不做 SEO 優化不等於不做基本可讀性，見 docs/22-charity-ui.md §2.10）。
export function useHreflang(pathWithoutLang: string) {
  const requestUrl = useRequestURL()
  const origin = requestUrl.origin
  const zhHref = `${origin}/zh${pathWithoutLang}`
  const enHref = `${origin}/en${pathWithoutLang}`

  useHead({
    link: [
      { rel: 'alternate', hreflang: 'zh-Hant', href: zhHref },
      { rel: 'alternate', hreflang: 'en', href: enHref },
      { rel: 'alternate', hreflang: 'x-default', href: zhHref },
      // canonical 指向「目前實際請求到的路徑」，不是固定指回中文版——
      // 中文與英文版是兩個獨立可索引的頁面（掃碼落地頁與項目詳情頁本來就該分別索引），
      // hreflang 才是告訴搜尋引擎「這些是彼此的語系對應版本」的機制，不是靠 canonical 收斂成一個。
      { rel: 'canonical', href: `${origin}${requestUrl.pathname}` },
    ],
  })
}
