/**
 * G1「表單設計器」後台端點（`Features/AdminForms`），對照 apps/api/README.md「S1-10」。
 * **沒有建立／刪除表單本身的端點**——9 個 `formCode` 是固定目錄，只能編輯設定與底下的動態欄位。
 */
import { apiRequest } from './http'

export interface AdminFormListItemDto {
  id: string
  formCode: string
  notifyEmails?: string | null
  captchaEnabled: boolean
  redirectPath?: string | null
  fieldCount: number
  updatedAt: string
}

export interface AdminFormFieldDto {
  id: string
  fieldKey: string
  fieldType: string
  /** 題目文字（中文）——`form_fields_i18n` zh-Hant 列，必存（S1-10 修正）。 */
  labelZh: string
  /** 題目文字（英文）——`form_fields_i18n` en 列，可缺（`null`＝尚未翻譯）。 */
  labelEn?: string | null
  isRequired: boolean
  validationRule?: string | null
  /** 只有下拉／多選會有值，其餘型別一律 `null`。canonical 值，不因語系而變。 */
  options?: string[] | null
  /** 選項的英文顯示文字，與 `options` 同順序、同筆數；`null`＝沒有選項或尚未提供英文翻譯。 */
  optionLabelsEn?: string[] | null
  /** G2 收件匣「內容摘要」欄的來源欄位——同一張表單最多一個為 `true`。 */
  isSummary: boolean
  sortOrder: number
}

export interface AdminFormDetailDto {
  id: string
  formCode: string
  notifyEmails?: string | null
  captchaEnabled: boolean
  redirectPath?: string | null
  autoReplyBodyZh?: string | null
  autoReplyBodyEn?: string | null
  fields: AdminFormFieldDto[]
  updatedAt: string
}

/** 更新表單設定——不含 `formCode`（固定目錄，不開放改代碼）。 */
export interface UpdateFormPayload {
  notifyEmails?: string | null
  captchaEnabled: boolean
  redirectPath?: string | null
  autoReplyBodyZh?: string | null
  autoReplyBodyEn?: string | null
}

export interface FormFieldPayload {
  fieldKey: string
  fieldType: string
  /** 題目文字（中文），必填——前台一定要有東西可顯示。 */
  labelZh: string
  /** 題目文字（英文），選填——尚未翻譯時公開端點回退顯示中文。 */
  labelEn?: string | null
  isRequired: boolean
  validationRule?: string | null
  options?: string[] | null
  /** 選項的英文顯示文字，省略或 `null`＝尚未翻譯。提供時筆數必須與 `options` 一致。 */
  optionLabelsEn?: string[] | null
  isSummary: boolean
}

export function listAdminForms(club: string): Promise<AdminFormListItemDto[]> {
  return apiRequest<AdminFormListItemDto[]>(`/api/v1/admin/${club}/forms`)
}

export function getAdminForm(club: string, id: string): Promise<AdminFormDetailDto> {
  return apiRequest<AdminFormDetailDto>(`/api/v1/admin/${club}/forms/${id}`)
}

export function updateAdminForm(club: string, id: string, payload: UpdateFormPayload): Promise<AdminFormDetailDto> {
  return apiRequest<AdminFormDetailDto>(`/api/v1/admin/${club}/forms/${id}`, { method: 'PUT', body: payload })
}

/** 建立動態欄位——`sortOrder` 省略時後端自動接在最後一個欄位之後。 */
export function createAdminFormField(club: string, formId: string, payload: FormFieldPayload): Promise<AdminFormFieldDto> {
  return apiRequest<AdminFormFieldDto>(`/api/v1/admin/${club}/forms/${formId}/fields`, { method: 'POST', body: payload })
}

export function updateAdminFormField(
  club: string,
  formId: string,
  fieldId: string,
  payload: FormFieldPayload & { sortOrder: number },
): Promise<AdminFormFieldDto> {
  return apiRequest<AdminFormFieldDto>(`/api/v1/admin/${club}/forms/${formId}/fields/${fieldId}`, {
    method: 'PUT',
    body: payload,
  })
}

/** 已有詢問資料引用的欄位會被後端擋下（409，`AdminApiError.kind === 'unknown'`），
 * 呼叫端顯示 `error.message` 即可（後端已給中文說明）。 */
export function deleteAdminFormField(club: string, formId: string, fieldId: string): Promise<void> {
  return apiRequest<void>(`/api/v1/admin/${club}/forms/${formId}/fields/${fieldId}`, { method: 'DELETE' })
}
