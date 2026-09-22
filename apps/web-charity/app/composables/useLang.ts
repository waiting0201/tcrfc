// app/composables/useLang.ts — 從路由的 [lang] 區段取得目前語系，並提供組字用的字典存取。
import { normalizeLang, dict, interpolate } from '../utils/i18n'
import type { Lang } from '../utils/i18n'

export function useLang() {
  const route = useRoute()
  const lang = computed<Lang>(() => normalizeLang(route.params.lang))
  const tr = computed(() => dict[lang.value])
  const associationName = computed(() => tr.value.associationName)

  /** 組字時自動帶入協會名稱（依語系決定是「台灣足球策略發展協會」還是「the Association」）。 */
  function tt(template: string, vars: Record<string, string | number> = {}): string {
    return interpolate(template, { association: associationName.value, ...vars })
  }

  /** 把目前路徑換成另一個語系的對應路徑（語系切換須停留在當前頁，規劃書 §2.4）。 */
  function pathForLang(target: Lang): string {
    const segments = route.fullPath.split('/')
    segments[1] = target
    return segments.join('/') || `/${target}/`
  }

  return { lang, tr, associationName, tt, pathForLang }
}
