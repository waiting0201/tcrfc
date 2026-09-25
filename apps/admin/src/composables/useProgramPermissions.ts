import { computed } from 'vue'
import { authUser } from '@/auth/session'
import { currentRoleCodes } from '@/auth/clubAccess'

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
 * ⚠️ **已知限制**：`GET /api/v1/admin/auth/me` 目前只回傳角色代碼（`roles[].code`），不回傳
 * 這個帳號實際擁有的權限碼清單（`AdminMeResponse` 沒有 `permissions` 欄位）。本檔用角色代碼
 * 在前端重建一份對照表，是目前唯一可行的做法——**若後端這份角色與權限的對應關係調整，這裡要
 * 手動跟著同步，不會自動反映**。長期應由 `/auth/me` 直接回傳這個帳號的權限碼清單取代這裡的
 * 推導（已記入 apps/admin/README.md「P1–P3 權限顯示的已知限制」，回報供下一輪評估）。
 *
 * 🔴 這裡的結果只用來決定「要不要顯示這個按鈕／這個選單項目」，**不是安全邊界**——真正的授權
 * 判斷一律由後端每一支端點的權限碼檢查（`IAdminClubAuthorizer`）執行，就算這裡誤判成看得到，
 * 送出請求一樣會被後端 403 擋下。
 */
const FULL_ACCESS_ROLES = new Set(['academy_program'])
const OWN_CLUB_FULL_ROLES = new Set(['partner_club_manager'])
const READ_ONLY_ROLES = new Set(['content_editor', 'team_competition', 'business_sponsorship', 'viewer'])
const REGISTRATION_ONLY_ROLES = new Set(['customer_service_admin'])

export function useProgramPermissions() {
  const isSuperAdmin = computed(() => authUser.value?.isSuperAdmin ?? false)

  function hasAnyRole(...sets: Set<string>[]): boolean {
    const codes = currentRoleCodes.value
    return sets.some((set) => codes.some((code) => set.has(code)))
  }

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
