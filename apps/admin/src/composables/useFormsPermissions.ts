import { computed } from 'vue'
import { useIsSuperAdmin, hasAnyRole } from './useRolePermissions'
import type { FormCode } from '@/types/forms'

/**
 * G1「表單設計器」／G2「詢問收件匣」的操作可視性，逐字對照 apps/api/README.md「S1-10」
 * 「權限碼與角色指派」：
 *
 * - 系統管理員：全部（含匯出）
 * - `content_editor`／`team_competition`／`translator`：「—」，不指派，G1／G2 皆看不到
 * - `academy_program`（學院／課程管理）：只看得到「課程類詢問」（`enquiry.course.*`），沒有 G1
 * - `business_sponsorship`（商務／贊助）：只看得到「合作／贊助類詢問」（`enquiry.partnership.*`），沒有 G1
 * - `pr_media`（公關／媒體）：只看得到「媒體類詢問」（`enquiry.media.*`），沒有 G1
 * - `customer_service_admin`（客服／行政）：G1 全部＋G2 收件匣（`enquiry.inbox.view/update`，**不含匯出**）
 * - `viewer`（檢視者）：G1／G2 收件匣皆唯讀
 * - `partner_club_manager`（合作球隊管理）：G1 全部＋G2 收件匣（不含匯出）——`own_clubs` 範圍已由
 *   既有的俱樂部授權機制限制，這裡不需要另外處理
 *
 * `isSuperAdmin`／`hasAnyRole` 兩個基礎判斷來自共用的 `useRolePermissions.ts`，已知限制見該檔檔頭。
 *
 * 🔴 **「指派負責人」的姓名選單額外受限**：`GET /api/v1/admin/accounts`（可用來把
 * `assigneeAdminUserId` 對照回姓名、或列出可指派對象）是 `system.account.view`，**僅系統管理員**
 * 可呼叫（見 `apps/api` `Features/AdminAccounts/AdminAccountsEndpoints.cs`）。這代表非系統管理員
 * 角色即使持有 `enquiry.*.update`（能改狀態／備註／標籤），也**沒有任何後端端點能把
 * `assigneeAdminUserId` 這個 GUID 解析成姓名，或列出「可以指派給誰」的清單**——這是發現的後端
 * 缺口，不是本輪能解的問題（見 `EnquiryEditView.vue` 檔頭與任務報告）。`canPickAssigneeByName`
 * 因此只對系統管理員開放完整的姓名選單；其餘角色只能「指派給自己」或「取消指派」（見該畫面）。
 */
const FORM_DESIGNER_ROLES = new Set(['customer_service_admin', 'partner_club_manager'])
const INBOX_ROLES = new Set(['customer_service_admin', 'partner_club_manager'])
const READ_ONLY_ROLES = new Set(['viewer'])
const COURSE_ROLES = new Set(['academy_program'])
const PARTNERSHIP_ROLES = new Set(['business_sponsorship'])
const MEDIA_ROLES = new Set(['pr_media'])

/** `FormCode` → 分類，對照 `apps/api` `Features/Forms/FormCatalog.cs` 的三個 `*CategoryCodes`
 * 常數。**只用來決定前端要不要顯示這個表單分頁，不是安全邊界**——就算前端誤顯示了某個分頁，
 * 該分頁的清單查詢一樣會被後端依實際持有的權限碼過濾成空集合，不會洩漏資料。 */
const COURSE_FORM_CODES = new Set<FormCode>(['academy_children_training', 'camp_registration'])
const PARTNERSHIP_FORM_CODES = new Set<FormCode>(['partnership_sponsorship', 'proposal_download'])
const MEDIA_FORM_CODES = new Set<FormCode>(['media_enquiry'])

export function useFormsPermissions() {
  const isSuperAdmin = useIsSuperAdmin()

  /** G1：能不能看到「設計器」選單與表單清單。 */
  const canManageForms = computed(() => isSuperAdmin.value || hasAnyRole(FORM_DESIGNER_ROLES))
  const canViewForms = canManageForms

  /** G2：能不能看到「收件匣」選單與詢問清單（含唯讀）。 */
  const canUpdateInbox = computed(
    () => isSuperAdmin.value || hasAnyRole(INBOX_ROLES, COURSE_ROLES, PARTNERSHIP_ROLES, MEDIA_ROLES),
  )
  const canViewInbox = computed(() => isSuperAdmin.value || canUpdateInbox.value || hasAnyRole(READ_ONLY_ROLES))

  /** G2：匯出 CSV（`enquiry.inbox.export`，`is_restricted`）——矩陣只指派給系統管理員。 */
  const canExportInbox = isSuperAdmin

  /** 指派負責人姓名選單只有系統管理員能用，見本檔檔頭。 */
  const canPickAssigneeByName = isSuperAdmin

  /** 這個角色看得到哪些表單分頁——`null` 代表不限（系統管理員或持有 `enquiry.inbox.*`）。 */
  const visibleFormCodes = computed<Set<FormCode> | null>(() => {
    if (isSuperAdmin.value || hasAnyRole(INBOX_ROLES, READ_ONLY_ROLES)) return null
    const codes = new Set<FormCode>()
    if (hasAnyRole(COURSE_ROLES)) COURSE_FORM_CODES.forEach((c) => codes.add(c))
    if (hasAnyRole(PARTNERSHIP_ROLES)) PARTNERSHIP_FORM_CODES.forEach((c) => codes.add(c))
    if (hasAnyRole(MEDIA_ROLES)) MEDIA_FORM_CODES.forEach((c) => codes.add(c))
    return codes
  })

  return {
    canManageForms,
    canViewForms,
    canUpdateInbox,
    canViewInbox,
    canExportInbox,
    canPickAssigneeByName,
    visibleFormCodes,
  }
}
