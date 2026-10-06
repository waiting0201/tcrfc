/**
 * 卡片層級的語言分頁範圍（LangTabsCard 提供、雙語欄位與 LangPane 取用）。
 *
 * 每張卡片一個獨立的範圍：目前語言預設中文、各卡互不影響；切換分頁只是畫面狀態，
 * 不算表單的未儲存變更。欄位向範圍「登記」自己，範圍據此算出分頁標籤上的
 * 「N 項尚未翻譯」（中文有值且英文空才算）與「N 處需修正」。
 * 範圍內的兩種語言都用 v-show 留在 DOM（保留輸入狀態與編輯器）。
 */
import { computed, inject, provide, shallowReactive, ref, type ComputedRef, type InjectionKey, type Ref } from 'vue'
import { useFormErrors } from './useFormErrors'

export type Lang = 'zh' | 'en'

export const LANG_LABEL: Record<Lang, string> = { zh: '中文', en: '英文' }

export interface LangScopeEntry {
  id: symbol
  /** 各語言對應的欄位鍵（用來數錯誤）。 */
  keys: Partial<Record<Lang, string>>
  /** 這個欄位是否「中文有值、英文空」。只在非中文分頁計入。 */
  untranslated: () => boolean
}

export interface LangScope {
  langs: Lang[]
  current: Ref<Lang>
  setLang: (lang: Lang) => void
  untranslatedCount: ComputedRef<number>
  errorCount: (lang: Lang) => number
  register: (entry: LangScopeEntry) => () => void
  tabId: (lang: Lang) => string
}

const LANG_SCOPE_KEY: InjectionKey<LangScope> = Symbol('lang-scope')

export function provideLangScope(langs: Lang[], idPrefix: string): LangScope {
  const errors = useFormErrors()
  const current = ref<Lang>(langs[0] ?? 'zh')
  const entries = shallowReactive(new Map<symbol, LangScopeEntry>())

  const untranslatedCount = computed(() => {
    let n = 0
    for (const e of entries.values()) if (e.untranslated()) n++
    return n
  })

  function errorCount(lang: Lang): number {
    let n = 0
    for (const e of entries.values()) {
      const key = e.keys[lang]
      if (key && errors.has(key)) n++
    }
    return n
  }

  const scope: LangScope = {
    langs,
    current,
    setLang(lang) {
      if (langs.includes(lang)) current.value = lang
    },
    untranslatedCount,
    errorCount,
    register(entry) {
      entries.set(entry.id, entry)
      return () => {
        entries.delete(entry.id)
      }
    },
    tabId: (lang) => `${idPrefix}-tab-${lang}`,
  }
  provide(LANG_SCOPE_KEY, scope)
  return scope
}

/** 不在任何 LangTabsCard 內時回傳 null（雙語欄位的過渡退路依此判斷）。 */
export function useLangScope(): LangScope | null {
  return inject(LANG_SCOPE_KEY, null)
}
