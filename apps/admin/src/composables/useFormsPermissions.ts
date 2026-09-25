import { computed } from 'vue'
import { useIsSuperAdmin, hasPermission, hasAnyPermission } from './useRolePermissions'
import type { FormCode } from '@/types/forms'

/**
 * G1「表單設計器」／G2「詢問收件匣」的操作可視性。**S1-10 起改讀 `GET /auth/me` 的權限碼清單**
 * （見 `useRolePermissions.ts` 檔頭），取代原本手寫的角色集合常數（`FORM_DESIGNER_ROLES`／
 * `INBOX_ROLES`／`READ_ONLY_ROLES`／`COURSE_ROLES`／`PARTNERSHIP_ROLES`／`MEDIA_ROLES` 已刪除）。
 *
 * 對照 apps/api/README.md「S1-10」「S1-10 修正」「權限碼與角色指派」：
 * - `form.view`／`form.update`：G1 表單設計器。
 * - `enquiry.inbox.*`：G2「全部詢問」（客服／行政、合作球隊管理、檢視者）。
 * - `enquiry.course.*`／`enquiry.partnership.*`／`enquiry.media.*`：G2 依表單類別的局部權限
 *   （學院／課程管理、商務／贊助、公關／媒體），只看得到對應的表單分頁。
 * - `enquiry.inbox.export`：匯出 CSV，矩陣只指派給系統管理員。
 *
 * ✅ **「指派負責人」姓名選單的既有缺口已由後端補上**（S1-10 修正，2026-09-25）：
 * `GET .../enquiries/assignable-users?formCode=...` 權限碼跟 `PUT .../enquiries/{id}` 同一組
 * （`canUpdateInbox` 涵蓋的角色皆可查詢），不再只有系統管理員能用姓名選單，見
 * `EnquiryEditView.vue`——這裡因此不再需要 `canPickAssigneeByName` 這個獨立判斷。
 */
const COURSE_FORM_CODES = new Set<FormCode>(['academy_children_training', 'camp_registration'])
const PARTNERSHIP_FORM_CODES = new Set<FormCode>(['partnership_sponsorship', 'proposal_download'])
const MEDIA_FORM_CODES = new Set<FormCode>(['media_enquiry'])

export function useFormsPermissions() {
  const isSuperAdmin = useIsSuperAdmin()

  /** G1：能不能新增／編輯表單設定與動態欄位。 */
  const canManageForms = computed(() => isSuperAdmin.value || hasPermission('form.update'))
  /** G1：能不能看到「設計器」選單與表單清單（含唯讀）。 */
  const canViewForms = computed(() => isSuperAdmin.value || canManageForms.value || hasPermission('form.view'))

  /** G2：能不能處理詢問（改狀態／指派負責人／備註／標籤）——全部或依類別局部皆算。 */
  const canUpdateInbox = computed(
    () =>
      isSuperAdmin.value ||
      hasAnyPermission('enquiry.inbox.update', 'enquiry.course.update', 'enquiry.partnership.update', 'enquiry.media.update'),
  )
  /** G2：能不能看到「收件匣」選單與詢問清單（含唯讀）。 */
  const canViewInbox = computed(
    () =>
      isSuperAdmin.value ||
      canUpdateInbox.value ||
      hasAnyPermission('enquiry.inbox.view', 'enquiry.course.view', 'enquiry.partnership.view', 'enquiry.media.view'),
  )

  /** G2：匯出 CSV（`enquiry.inbox.export`，`is_restricted`）——矩陣只指派給系統管理員。 */
  const canExportInbox = computed(() => isSuperAdmin.value || hasPermission('enquiry.inbox.export'))

  /** 這個角色看得到哪些表單分頁——`null` 代表不限（系統管理員或持有 `enquiry.inbox.*`）。 */
  const visibleFormCodes = computed<Set<FormCode> | null>(() => {
    if (isSuperAdmin.value || hasAnyPermission('enquiry.inbox.view', 'enquiry.inbox.update')) return null
    const codes = new Set<FormCode>()
    if (hasAnyPermission('enquiry.course.view', 'enquiry.course.update')) COURSE_FORM_CODES.forEach((c) => codes.add(c))
    if (hasAnyPermission('enquiry.partnership.view', 'enquiry.partnership.update')) PARTNERSHIP_FORM_CODES.forEach((c) => codes.add(c))
    if (hasAnyPermission('enquiry.media.view', 'enquiry.media.update')) MEDIA_FORM_CODES.forEach((c) => codes.add(c))
    return codes
  })

  return {
    canManageForms,
    canViewForms,
    canUpdateInbox,
    canViewInbox,
    canExportInbox,
    visibleFormCodes,
  }
}
