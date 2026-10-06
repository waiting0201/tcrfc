/**
 * 編輯頁的欄位錯誤（鍵 → 訊息）與「定位到第一個錯誤」。
 *
 * 用法：頁面 setup 裡 `const formErrors = provideFormErrors()`，其餘元件（FormField、雙語欄位、
 * LangTabsCard、EditActionBar 的狀態列）用 `useFormErrors()` 取同一份。
 * 欄位鍵是程式內部識別（`nameZh`、`slug`），**絕不顯示在畫面上**。
 */
import { inject, nextTick, provide, reactive, computed, type InjectionKey } from 'vue'
import type { Lang, LangScope } from './useLangScope'

export interface FieldAnchor {
  key: string
  el: HTMLElement
  /** 此欄位屬於哪個語言分頁；與 scope 一起用，定位時先切到該語言。 */
  lang?: Lang
  scope?: LangScope | null
  /** 欄位在頁面層分頁（el-tabs）或摺疊區裡時，由頁面提供「把它打開」。 */
  reveal?: () => void | Promise<void>
}

/** 後端 `fieldErrors` 型別的最小需求（避免與 api/http 循環依賴）。 */
interface FieldErrorSource {
  fieldErrors?: Record<string, string>
}

const FOCUSABLE = [
  'input:not([type="hidden"]):not([disabled])',
  'textarea:not([disabled])',
  'select:not([disabled])',
  'button:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',')

export function firstFocusable(root: HTMLElement): HTMLElement | null {
  return root.querySelector<HTMLElement>(FOCUSABLE)
}

export function createFormErrors() {
  const errors = reactive<Record<string, string>>({})
  const anchors = new Set<FieldAnchor>()

  const count = computed(() => Object.keys(errors).length)

  function set(key: string, message: string) {
    errors[key] = message
  }
  function get(key: string): string | undefined {
    return errors[key]
  }
  function has(key: string): boolean {
    return !!errors[key]
  }
  function clear(key: string) {
    if (key in errors) delete errors[key]
  }
  function clearAll() {
    for (const k of Object.keys(errors)) delete errors[k]
  }
  /** 整批取代；有任何錯誤回傳 true。 */
  function replaceAll(record: Record<string, string>): boolean {
    clearAll()
    for (const [k, v] of Object.entries(record)) if (v) errors[k] = v
    return count.value > 0
  }

  function registerAnchor(anchor: FieldAnchor): () => void {
    anchors.add(anchor)
    return () => {
      anchors.delete(anchor)
    }
  }

  function anchorsWithError(): FieldAnchor[] {
    return [...anchors]
      .filter((a) => a.el.isConnected && has(a.key))
      .sort((a, b) => {
        const pos = a.el.compareDocumentPosition(b.el)
        if (pos & Node.DOCUMENT_POSITION_FOLLOWING) return -1
        if (pos & Node.DOCUMENT_POSITION_PRECEDING) return 1
        return 0
      })
  }

  /** 定位到文件順序最前面的錯誤欄位；找不到可定位的欄位回傳 false。 */
  async function focusFirst(): Promise<boolean> {
    const target = anchorsWithError()[0]
    if (!target) return false
    if (target.lang && target.scope) target.scope.setLang(target.lang)
    if (target.reveal) await target.reveal()
    await nextTick()
    const reduce = typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
    target.el.scrollIntoView({ block: 'center', behavior: reduce ? 'auto' : 'smooth' })
    let focusTarget = firstFocusable(target.el)
    if (!focusTarget) {
      focusTarget = target.el
      if (!focusTarget.hasAttribute('tabindex')) focusTarget.setAttribute('tabindex', '-1')
    }
    focusTarget.focus({ preventScroll: true })
    return true
  }

  /**
   * 把後端回來的欄位錯誤標到畫面上。所有鍵都對得到欄位才回傳 true；
   * 有對不到的鍵（或根本沒有欄位錯誤）回傳 false，呼叫端改顯示頁首提示。
   * 對得到的鍵仍會先標上，並定位到第一處。
   */
  function applyApiError(err: FieldErrorSource | null | undefined): boolean {
    const fieldErrors = err?.fieldErrors
    if (!fieldErrors) return false
    const known = new Set([...anchors].map((a) => a.key))
    // 前綴退回：精確鍵沒有 anchor 時逐層去掉尾段（`tags[2].slug` → `tags[2]` → `tags`），標在最近的群組 anchor 上
    const resolve = (key: string): string | null => {
      let k = key
      while (k) {
        if (known.has(k)) return k
        const next = k.replace(/(\.[^.[\]]+|\[\d+\])$/, '')
        if (next === k) return null
        k = next
      }
      return null
    }
    const matched: Record<string, string> = {}
    let allMatched = true
    for (const [k, v] of Object.entries(fieldErrors)) {
      const target = resolve(k)
      if (target === null) allMatched = false
      else if (!(target in matched)) matched[target] = v
    }
    if (Object.keys(matched).length === 0) return false
    for (const [k, v] of Object.entries(matched)) set(k, v)
    void focusFirst()
    return allMatched
  }

  return { errors, count, set, get, has, clear, clearAll, replaceAll, registerAnchor, focusFirst, applyApiError }
}

export type FormErrors = ReturnType<typeof createFormErrors>

const FORM_ERRORS_KEY: InjectionKey<FormErrors> = Symbol('form-errors')

export function provideFormErrors(): FormErrors {
  const instance = createFormErrors()
  provide(FORM_ERRORS_KEY, instance)
  return instance
}

/** 沒有頁面提供時回傳一份獨立的空實例（元件在舊頁面裡仍可運作，只是錯誤不會共享）。 */
export function useFormErrors(): FormErrors {
  return inject(FORM_ERRORS_KEY, null) ?? createFormErrors()
}
