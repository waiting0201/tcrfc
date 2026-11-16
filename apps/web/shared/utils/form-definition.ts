// shared/utils/form-definition.ts — 公開表單定義（後台「表單設計器」）的純函式解析（docs/23 W-3／W-24）。
//
// 對應 `GET /api/v1/{club}/forms/{formCode}?lang=`（apps/api/Features/Forms/FormDtos.cs 的 PublicFormDto）。
// 後台對系統預先定義的表單只能改「題目文字（中／英）」與「選項英文顯示文字」；欄位鍵、類型、必填、
// 選項送出值（`options`）鎖定。前台因此：
//   - 標籤文字取定義的 `label`；
//   - 選項「顯示文字」取 `optionLabels`、「送出值」取 `options` 同位置的值（兩者同序同筆數）。
// 頁面寫死的文字只在「定義取不到」「定義沒有這個欄位鍵」「英文頁的定義文字是回退的中文」時當退路。
//
// 🔴 英文頁不得印出中文：API 在請求語系沒有翻譯時會回退成中文（label 與 optionLabels 皆然），
// 所以 `lang === 'en'` 時，含中日韓字元的定義文字一律視為「尚未翻譯」，改用頁面的英文退路。
//
// 本檔刻意不 import 任何東西，讓 scripts/check-form-definition.mjs 能用 node 直接載入驗證。

export interface FormDefinitionField {
  fieldKey: string
  fieldType?: string
  label?: string | null
  isRequired?: boolean
  /** 下拉／多選的送出值。 */
  options?: readonly string[] | null
  /** 下拉／多選的顯示文字，與 options 同序同筆數。 */
  optionLabels?: readonly string[] | null
}

export interface FormDefinition {
  captchaEnabled?: boolean
  redirectPath?: string | null
  fields?: readonly FormDefinitionField[] | null
}

export interface FormOption {
  /** 送出值（後端驗證用的字面值，不因語系而變）。 */
  value: string
  /** 顯示文字。 */
  label: string
}

export type FormLang = 'zh' | 'en'

/** 是否含中日韓字元（英文頁用來辨識「API 回退成中文」的文字）。 */
export function hasCjk(text: string | null | undefined): boolean {
  return !!text && /[぀-ヿ㐀-䶿一-鿿＀-￯]/.test(text)
}

function findField(def: FormDefinition | null | undefined, fieldKey: string): FormDefinitionField | undefined {
  return def?.fields?.find((f) => f.fieldKey === fieldKey)
}

/** 定義的文字在這個語系能不能直接顯示：不可為空；英文頁不可含中文。 */
function usable(text: string | null | undefined, lang: FormLang): boolean {
  const t = (text ?? '').trim()
  return t.length > 0 && !(lang === 'en' && hasCjk(t))
}

/** 欄位題目：定義有可用文字就用定義，否則用頁面退路。 */
export function resolveFieldLabel(def: FormDefinition | null | undefined, fieldKey: string, fallback: string, lang: FormLang): string {
  const label = findField(def, fieldKey)?.label
  return usable(label, lang) ? (label ?? '').trim() : fallback
}

/**
 * 欄位選項清單。定義有 `options` 時，清單由定義產生（value＝options[i]、label＝optionLabels[i]）；
 * 英文頁某一項的顯示文字是回退的中文時，改用 `fallback` 中 value 相同那一項的 label（找不到就沿用定義文字，
 * 因為選項結構鎖定，這只會發生在種子與頁面退路不同步時，由 lint:form-definition 擋）。
 * 定義取不到、沒有這個欄位或欄位沒有選項時，回傳 `fallback`。
 */
export function resolveFieldOptions(def: FormDefinition | null | undefined, fieldKey: string, fallback: readonly FormOption[], lang: FormLang): FormOption[] {
  const field = findField(def, fieldKey)
  const values = field?.options
  if (!values || values.length === 0) return fallback.map((o) => ({ ...o }))
  return values.map((value, i) => {
    const shown = field?.optionLabels?.[i]
    if (usable(shown, lang)) return { value, label: (shown ?? '').trim() }
    const fb = fallback.find((o) => o.value === value)
    return { value, label: fb?.label ?? (shown?.trim() || value) }
  })
}

/** 單一選項（以送出值指定）的顯示文字；用在頁面自己分組或只取部分選項的下拉。取不到用 `fallback`。 */
export function resolveOptionLabel(def: FormDefinition | null | undefined, fieldKey: string, value: string, fallback: string, lang: FormLang): string {
  const field = findField(def, fieldKey)
  const i = field?.options?.indexOf(value) ?? -1
  if (i < 0) return fallback
  const shown = field?.optionLabels?.[i]
  return usable(shown, lang) ? (shown ?? '').trim() : fallback
}
