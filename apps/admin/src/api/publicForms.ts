/**
 * 10 表單中心的公開讀取端點（`Features/Forms`，對照 apps/api/README.md「S1-10 修正」），
 * **不需要登入**。後台這裡只用到它取得「這張表單目前的欄位題目文字」——G2 詢問詳情頁
 * （`EnquiryEditView.vue`）顯示訪客回答時，不管目前登入角色有沒有 G1（`form.view`）權限，
 * 都能呼叫這支公開端點把 `fieldKey` 換成人看得懂的題目文字，不用再猜測或顯示英文欄位代碼
 * （見 `types/forms.ts` 檔頭：舊版 `fieldKeyLabel()` 猜測對照表已刪除）。
 */
import { apiRequest } from './http'

export interface PublicFormFieldDto {
  fieldKey: string
  fieldType: string
  /** 題目文字，依請求語系回傳（沒有翻譯時後端回退顯示中文）。 */
  label: string
  isRequired: boolean
  validationRule?: string | null
  /** 下拉／多選的送出值（canonical，不因語系而變）。 */
  options?: string[] | null
  /** 下拉／多選的顯示文字，與 `options` 同順序、同筆數；`null`＝這個欄位沒有選項。 */
  optionLabels?: string[] | null
  sortOrder: number
}

export interface PublicFormDto {
  formCode: string
  formNameZh: string
  formNameEn: string
  captchaEnabled: boolean
  fields: PublicFormFieldDto[]
}

/** `lang` 預設 `zh`——後台介面一律中文，這裡只是要拿題目文字，不是要做雙語切換。 */
export function getPublicForm(club: string, formCode: string, lang: 'zh' | 'en' = 'zh'): Promise<PublicFormDto> {
  return apiRequest<PublicFormDto>(`/api/v1/${club}/forms/${encodeURIComponent(formCode)}?lang=${lang}`)
}
