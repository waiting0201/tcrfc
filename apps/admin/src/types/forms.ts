/**
 * G1「表單設計器」／G2「詢問收件匣」的畫面型別（對照 apps/api/README.md「S1-10」、
 * `apps/api` `Features/Forms/FormCatalog.cs`／`FormFieldTypes.cs`）。
 */

// ── 9 個固定表單目錄 ─────────────────────────────────────────────────────────────

/**
 * 🔴 已知限制：表單顯示名稱不是資料庫欄位（2026-09-22 拍板不建 `forms_i18n.name`，見
 * apps/api/README.md「S1-10」），後端 `FormCatalog.DisplayNameZh` 是純 C# 常數字典，這裡必須維護
 * 同一份對照——**若後端 `Features/Forms/FormCatalog.cs` 新增或改名表單代碼，這裡要手動同步，
 * 不會自動反映**（同 `useProgramPermissions.ts` 角色對照表的既有限制）。
 */
export type FormCode =
  | 'join_player'
  | 'academy_children_training'
  | 'camp_registration'
  | 'international_player_enquiry'
  | 'partnership_sponsorship'
  | 'media_enquiry'
  | 'general_contact'
  | 'proposal_download'
  | 'donation_enquiry'

/** 依主站規劃書 §3.10 表格順序（10.1–10.7），再接提案下載與捐助洽詢——對照 `FormCatalog.AllCodes`。 */
export const FORM_CODE_ORDER: FormCode[] = [
  'join_player',
  'academy_children_training',
  'camp_registration',
  'international_player_enquiry',
  'partnership_sponsorship',
  'media_enquiry',
  'general_contact',
  'proposal_download',
  'donation_enquiry',
]

export const FORM_CODE_LABEL: Record<FormCode, string> = {
  join_player: '加入球隊',
  academy_children_training: '加入學院／兒童訓練',
  camp_registration: '營隊報名',
  international_player_enquiry: '國際球員詢問',
  partnership_sponsorship: '合作夥伴與贊助洽詢',
  media_enquiry: '媒體詢問',
  general_contact: '一般聯絡',
  proposal_download: '提案簡介下載',
  donation_enquiry: '捐助洽詢',
}

export function formCodeLabel(formCode: string): string {
  return FORM_CODE_LABEL[formCode as FormCode] ?? formCode
}

// ── G1 欄位型別 ──────────────────────────────────────────────────────────────────

/** 對應 `form_fields.field_type`（`FormFieldTypes.Allowed`），逐字對照規劃書 G1 六種欄位型別。 */
export const FIELD_TYPE_ORDER = ['text', 'textarea', 'select', 'multiselect', 'date', 'file', 'consent'] as const
export type FormFieldTypeCode = (typeof FIELD_TYPE_ORDER)[number]

export const FIELD_TYPE_LABEL: Record<FormFieldTypeCode, string> = {
  text: '文字（單行）',
  textarea: '文字（多行）',
  select: '下拉選單',
  multiselect: '多選',
  date: '日期',
  file: '檔案上傳',
  consent: '同意條款',
}

/** 下拉／多選才需要設定選項清單（對應後端 `FormFieldTypes.RequiresOptions`）。 */
export const FIELD_TYPES_REQUIRING_OPTIONS = new Set<FormFieldTypeCode>(['select', 'multiselect'])

// ── G2 詢問狀態 ──────────────────────────────────────────────────────────────────

/** `enquiries.status`：規劃書 G2「新進 → 處理中 → 已回覆 → 已結案 / 無效」。 */
export const ENQUIRY_STATUS_ORDER = ['新進', '處理中', '已回覆', '已結案', '無效'] as const
export type EnquiryStatus = (typeof ENQUIRY_STATUS_ORDER)[number]

export function enquiryStatusTagType(status: string): 'success' | 'warning' | 'info' | 'danger' {
  if (status === '已回覆' || status === '已結案') return 'success'
  if (status === '新進') return 'warning'
  if (status === '無效') return 'danger'
  return 'info' // 處理中
}

/**
 * 已知常用欄位代碼的中文提示（**最佳猜測，非完整對照**）：G1 表單設計器只儲存欄位代碼
 * （`field_key`，英文小寫，管理者自訂），系統目前沒有欄位「問題文字」的多語系儲存（規劃書與後端
 * 均未定義，見 G2 詳情畫面檔頭「發現的缺口」）。這裡只覆蓋種子資料實際用到的慣用鍵，供畫面上
 * 顯示得友善一點；沒有對照到的欄位代碼會原樣顯示欄位代碼本身。
 */
const FIELD_KEY_LABEL_HINTS: Record<string, string> = {
  name: '姓名',
  contact: '聯絡方式',
  phone: '電話',
  email: 'Email',
  message: '內容／留言',
  experience: '經歷／簡歷',
  cooperation_direction: '合作方向',
  company: '公司',
  privacy_consent: '個資同意條款',
}

export function fieldKeyLabel(fieldKey: string): string {
  return FIELD_KEY_LABEL_HINTS[fieldKey] ?? fieldKey
}
