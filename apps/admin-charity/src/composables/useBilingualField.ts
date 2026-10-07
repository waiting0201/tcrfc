/**
 * 雙語欄位元件共用邏輯：由 `field` 推欄位鍵、向語言分頁範圍登記（未翻譯、錯誤鍵）。
 * 必須放在 LangTabsBar 內，否則丟錯（沒有並排的過渡退路）；沒有 `field`／`field-zh`＋`field-en` 也丟錯。
 */
import { onBeforeUnmount, onMounted } from 'vue'
import { useRequiredLangScope } from './useLangScope'

export interface BilingualFieldProps {
  field?: string
  fieldZh?: string
  fieldEn?: string
  zh: string
  en: string
}

export function useBilingualField(props: BilingualFieldProps, componentName: string) {
  const scope = useRequiredLangScope(componentName)

  const keyZh = () => props.fieldZh ?? (props.field ? `${props.field}Zh` : '')
  const keyEn = () => props.fieldEn ?? (props.field ? `${props.field}En` : '')

  if (!keyZh() || !keyEn()) throw new Error(`[${componentName}] 必須指定 field（或 field-zh 與 field-en），否則驗證錯誤無法標到欄位。`)

  const id = Symbol(componentName)
  let unregister: (() => void) | null = null
  onMounted(() => {
    unregister = scope.register({
      id,
      keys: { zh: keyZh(), en: keyEn() },
      untranslated: () => !!props.zh.trim() && !props.en.trim(),
    })
  })
  onBeforeUnmount(() => unregister?.())

  return { scope, keyZh, keyEn }
}
