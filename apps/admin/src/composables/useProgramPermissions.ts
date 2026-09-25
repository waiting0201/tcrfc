import { computed } from 'vue'
import { useIsSuperAdmin, hasPermission } from './useRolePermissions'

/**
 * P1–P3「課程與活動」的操作可視性。**S1-10 起改讀 `GET /auth/me` 的權限碼清單**（見
 * `useRolePermissions.ts` 檔頭），取代原本手寫的「角色→操作」對照表（`FULL_ACCESS_ROLES`／
 * `OWN_CLUB_FULL_ROLES`／`READ_ONLY_ROLES`／`REGISTRATION_ONLY_ROLES` 已刪除）。每一個判斷直接
 * 對應它背後呼叫的後端端點所要求的權限碼，跟 `db/seed/generate-club-seed-sql.py` 的
 * `ROLE_PERMISSIONS` 是否同步已經不是前端要煩惱的事——後端矩陣異動時 `/auth/me` 回傳的清單自動
 * 反映，不需要再手動同步兩邊。
 *
 * 對照 apps/api/README.md「S1-9」「權限碼與角色指派」：
 * - `program.item.view`／`program.session.view`：矩陣「課程／報名」欄可檢視的角色一律同時持有
 *   這兩碼（系統管理員、學院／課程管理、合作球隊管理、內容編輯、競技／球隊管理、商務／贊助、
 *   檢視者）——`customer_service_admin`（客服／行政）沒有這兩碼，矩陣寫的是「只有報名」。
 * - `program.item.create`：只有學院／課程管理與合作球隊管理持有，用來決定能不能新增／編輯
 *   項目與梯次。
 * - `program.registration.*`：客服／行政額外持有 `view`／`update`（能處理報名，但不能後台代填
 *   `create`，也不能看到項目與梯次）。
 *
 * 🔴 這裡的結果只用來決定「要不要顯示」，不是安全邊界——真正的授權判斷一律由後端每一支端點的
 * 權限碼檢查執行，就算這裡誤判成看得到，送出請求一樣會被後端 403 擋下。
 */
export function useProgramPermissions() {
  const isSuperAdmin = useIsSuperAdmin()

  /** P1／P2：能不能看到「項目」「梯次」這兩個選單與列表。 */
  const canViewItems = computed(() => isSuperAdmin.value || hasPermission('program.item.view'))
  /** P1／P2：能不能新增／編輯項目與梯次。 */
  const canManageItems = computed(() => isSuperAdmin.value || hasPermission('program.item.create'))

  /** P3：能不能看到「報名」選單與列表。 */
  const canViewRegistrations = computed(() => isSuperAdmin.value || hasPermission('program.registration.view'))
  /** P3：後台代填報名。 */
  const canCreateRegistrations = computed(() => isSuperAdmin.value || hasPermission('program.registration.create'))
  /** P3：處理報名（確認／取消／轉梯次／候補／備註／學員資料）。 */
  const canProcessRegistrations = computed(() => isSuperAdmin.value || hasPermission('program.registration.update'))
  /** P3：匯出名單 CSV（`is_restricted`）。 */
  const canExportRegistrations = computed(() => isSuperAdmin.value || hasPermission('program.registration.export'))

  return {
    canViewItems,
    canManageItems,
    canViewRegistrations,
    canCreateRegistrations,
    canProcessRegistrations,
    canExportRegistrations,
  }
}
