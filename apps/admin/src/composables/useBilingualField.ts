/**
 * 雙語欄位元件共用邏輯：由 `field` 推欄位鍵、向語言分頁範圍登記（未翻譯、錯誤鍵）。
 * 在 LangTabsBar 之外使用時回傳 `scope: null`，元件走過渡期的舊版並排畫面。
 */
import { onBeforeUnmount, onMounted } from 'vue'
import { useLangScope } from './useLangScope'
import { useFormErrors } from './useFormErrors'

export interface BilingualFieldProps {
  field?: string
  fieldZh?: string
  fieldEn?: string
  zh: string
  en: string
}

export function useBilingualField(props: BilingualFieldProps, componentName: string) {
  const scope = useLangScope()
  const formErrors = useFormErrors()

  const keyZh = () => props.fieldZh ?? (props.field ? `${props.field}Zh` : '')
  const keyEn = () => props.fieldEn ?? (props.field ? `${props.field}En` : '')

  if (scope) {
    if (!keyZh() && !keyEn() && import.meta.env.DEV) {
      console.warn(`[${componentName}] 在 LangTabsBar 內必須指定 field（欄位鍵），否則驗證錯誤無法標到欄位。`)
    }
    const id = Symbol(componentName)
    let unregister: (() => void) | null = null
    onMounted(() => {
      unregister = scope.register({
        id,
        keys: { zh: keyZh() || undefined, en: keyEn() || undefined },
        untranslated: () => !!props.zh.trim() && !props.en.trim(),
      })
    })
    onBeforeUnmount(() => unregister?.())
  } else if (import.meta.env.DEV) {
    console.warn(`[${componentName}] 沒有放在 LangTabsBar 內，使用過渡期的舊版並排畫面；請改放進 LangTabsBar 並指定 field。`)
  }

  return { scope, keyZh, keyEn, formErrors }
}
