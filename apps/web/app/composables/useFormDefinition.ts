// app/composables/useFormDefinition.ts — 公開表單定義（後台表單設計器）的前台讀取（docs/23 W-3／W-24）。
//
// 後台可改系統預先定義表單的「題目文字（中／英）」與「選項英文顯示文字」，前台要照後台設定顯示。
// 定義取自 `GET /api/v1/{club}/forms/{formCode}?lang=`（經同源代理 `/api/backend`）。
//
// 🔴 在伺服器端渲染時就取得（`useFetch` 會登記 onServerPrefetch 並把結果放進 payload），SSR 輸出的
// 標籤就是後台設定的文字，掛載後不換字、無 hydration 差異。**不要**改回 onMounted 取（會閃動）。
// 🔴 取不到（API 沒啟動、回錯誤）＝`definition` 為 null，所有取值函式回傳頁面退路，表單照常可用，
// 送出時後端才是最後防線（與 useSiteFacts 同一個 fail-open 慣例）。
// 🔴 英文頁：定義的英文缺漏時 API 會回退中文，此時一律改用頁面原本的英文退路，不印出中文
// （判斷邏輯在 shared/utils/form-definition.ts，由 scripts/check-form-definition.mjs 驗證）。
//
// 用法（七張表單共用，不要每頁各寫一套）：
//   const { fieldLabel, fieldOptions, optionLabel } = useFormSubmit('join_player')   // 內部呼叫本 composable
//   <label>{{ fieldLabel('birth_date', tx('出生日期', 'Date of birth')) }}</label>
//   <option v-for="o in fieldOptions('enquiry_type', [...])" :value="o.value">{{ o.label }}</option>
import type { FormDefinition, FormLang, FormOption } from '#shared/utils/form-definition'

export interface FieldLabelOptions {
  /** 指定取哪個語系的定義（雙語並列的頁面用；需 `bothLangs`）。預設＝目前頁面語系。 */
  lang?: FormLang
  /** 欄位在頁面上是選填：定義的題目文字後面補「（選填）」。頁面退路自己已含提示，不會重複加。 */
  optional?: boolean
}

export function useFormDefinition(formCode: string, options: { bothLangs?: boolean } = {}) {
  const config = useRuntimeConfig()
  const club = config.public.club
  const { isEn } = useLocale()
  const lang = computed<FormLang>(() => (isEn.value ? 'en' : 'zh'))
  const url = `/api/backend/${club}/forms/${formCode}`

  const { data: primary } = useFetch<FormDefinition | null>(url, {
    query: computed(() => ({ lang: lang.value })),
    key: computed(() => `form-def-${club}-${formCode}-${lang.value}`),
    default: () => null,
  })
  // 雙語並列頁面（例如國際球員頁：英文主標籤＋中文副標籤）才多取另一個語系。
  const secondary = options.bothLangs
    ? useFetch<FormDefinition | null>(url, {
      query: computed(() => ({ lang: lang.value === 'en' ? 'zh' : 'en' })),
      key: computed(() => `form-def-${club}-${formCode}-${lang.value === 'en' ? 'zh' : 'en'}`),
      default: () => null,
    }).data
    : null

  /** 目前頁面語系的定義（取不到＝null）。 */
  const definition = computed<FormDefinition | null>(() => primary.value ?? null)

  function defOf(l: FormLang): FormDefinition | null {
    if (l === lang.value) return primary.value ?? null
    return secondary?.value ?? null
  }

  /** 欄位題目：定義有可用文字用定義，否則用 `fallback`（頁面原本寫死的文字）。 */
  function fieldLabel(fieldKey: string, fallback: string, opts: FieldLabelOptions = {}): string {
    const l = opts.lang ?? lang.value
    const sentinel = '\u0000'
    const label = resolveFieldLabel(defOf(l), fieldKey, sentinel, l)
    if (label === sentinel) return fallback
    return opts.optional ? `${label}${l === 'en' ? ' (optional)' : '（選填）'}` : label
  }

  /** 欄位選項清單（value＝送出值、label＝顯示文字）；定義沒有這個欄位或選項時回傳 `fallback`。 */
  function fieldOptions(fieldKey: string, fallback: readonly FormOption[]): FormOption[] {
    return resolveFieldOptions(defOf(lang.value), fieldKey, fallback, lang.value)
  }

  /** 單一選項（以送出值指定）的顯示文字，供頁面自己分組或只取部分選項的下拉使用。 */
  function optionLabel(fieldKey: string, value: string, fallback: string): string {
    return resolveOptionLabel(defOf(lang.value), fieldKey, value, fallback, lang.value)
  }

  return { definition, fieldLabel, fieldOptions, optionLabel }
}
