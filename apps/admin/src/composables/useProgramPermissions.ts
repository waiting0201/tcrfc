import { computed } from 'vue'
import { useIsSuperAdmin, hasAnyRole } from './useRolePermissions'

/**
 * P1–P3「課程與活動」的操作可視性（主站規劃書 §6 權限矩陣「課程／報名」欄，逐字對照
 * `db/seed/generate-club-seed-sql.py` 的 `ROLE_PERMISSIONS` S1-9 區塊、apps/api/README.md
 * 「S1-9」節「權限碼與角色指派」）：
 *
 * - 系統管理員：全部（含匯出）
 * - `academy_program`（學院／課程管理）：全部（含匯出）
 * - `partner_club_manager`（合作球隊管理）：項目／梯次／報名皆可檢視與異動，**不含匯出**
 * - `content_editor`／`team_competition`／`business_sponsorship`／`viewer`：三個子模組皆唯讀
 * - `customer_service_admin`（客服／行政）：**只有報名的檢視與處理**，看不到項目與梯次
 * - `pr_media`（公關／媒體）／`translator`（翻譯人員）：矩陣是「—」，完全不指派，三個子模組都沒有
 *
 * `isSuperAdmin`／`hasAnyRole` 兩個基礎判斷抽到共用的 `useRolePermissions.ts`（S1-10 起），
 * 該檔檔頭有完整的已知限制說明（`/auth/me` 不回傳權限碼清單、這裡只影響「要不要顯示」不是
 * 安全邊界），本檔不重複。
 */
const FULL_ACCESS_ROLES = new Set(['academy_program'])
const OWN_CLUB_FULL_ROLES = new Set(['partner_club_manager'])
const READ_ONLY_ROLES = new Set(['content_editor', 'team_competition', 'business_sponsorship', 'viewer'])
const REGISTRATION_ONLY_ROLES = new Set(['customer_service_admin'])

export function useProgramPermissions() {
  const isSuperAdmin = useIsSuperAdmin()

  const canManageItemsOrSessions = computed(() => isSuperAdmin.value || hasAnyRole(FULL_ACCESS_ROLES, OWN_CLUB_FULL_ROLES))
  const canViewItemsOrSessions = computed(
    () => isSuperAdmin.value || canManageItemsOrSessions.value || hasAnyRole(READ_ONLY_ROLES),
  )

  const canProcessRegistrations = computed(
    () => isSuperAdmin.value || hasAnyRole(FULL_ACCESS_ROLES, OWN_CLUB_FULL_ROLES, REGISTRATION_ONLY_ROLES),
  )
  const canCreateRegistrations = computed(() => isSuperAdmin.value || hasAnyRole(FULL_ACCESS_ROLES, OWN_CLUB_FULL_ROLES))
  const canViewRegistrations = computed(
    () => isSuperAdmin.value || canProcessRegistrations.value || hasAnyRole(READ_ONLY_ROLES),
  )
  const canExportRegistrations = computed(() => isSuperAdmin.value || hasAnyRole(FULL_ACCESS_ROLES))

  return {
    /** P1／P2：能不能看到「項目」「梯次」這兩個選單與列表。 */
    canViewItems: canViewItemsOrSessions,
    /** P1／P2：能不能新增／編輯項目與梯次。 */
    canManageItems: canManageItemsOrSessions,
    /** P3：能不能看到「報名」選單與列表。 */
    canViewRegistrations,
    /** P3：後台代填報名。 */
    canCreateRegistrations,
    /** P3：處理報名（確認／取消／轉梯次／候補／備註／學員資料）。 */
    canProcessRegistrations,
    /** P3：匯出名單 CSV（`is_restricted`）。 */
    canExportRegistrations,
  }
}
