/**
 * 雙語欄位元件共用邏輯：由 `field` 推欄位鍵、向語言分頁範圍登記（未翻譯、錯誤鍵）。
 * 必須放在 LangTabsBar 內；在其外使用時回傳 `scope: null`（元件不渲染），開發模式 console.warn。
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
    console.warn(`[${componentName}] 必須放在 LangTabsBar 內（整頁用 page 版、對話框用 variant="bare"）並指定 field，否則不會渲染。`)
  }

  return { scope, keyZh, keyEn, formErrors }
}
