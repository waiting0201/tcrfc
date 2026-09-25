import { computed } from 'vue'
import { useIsSuperAdmin, hasPermission } from './useRolePermissions'

/**
 * `L1` 行事曆總覽／`L2` 自建事件的操作可視性。**S1-10 起改讀 `GET /auth/me` 的權限碼清單**（見
 * `useRolePermissions.ts` 檔頭），取代原本手寫的角色集合常數（`MANAGE_CUSTOM_EVENT_ROLES`／
 * `VIEW_CUSTOM_EVENT_ONLY_ROLES`／`OVERVIEW_ONLY_ROLES` 已刪除）。
 *
 * 對照 apps/api/README.md「S1-11」「權限碼與角色指派」（主站規劃書 §6 矩陣「行事曆」欄，十個
 * 角色都至少能看到 `calendar.view`，是唯一沒有「—」的一欄）：
 * - `calendar.view`：L1 總覽，全部角色皆有。
 * - `calendar.custom_event.view`：L2 自建事件清單（含唯讀）。
 * - `calendar.custom_event.create`：只有能新增／編輯／刪除自建事件的角色持有（內容編輯、公關
 *   媒體、合作球隊管理），競技／球隊管理與學院／課程管理只給 `calendar.view`（矩陣格對應的是
 *   「賽事事件」「梯隊賽事」，已由既有 `team.match.*`／`academy_only` 承接，不是本模組權限碼）。
 */
export function useCalendarPermissions() {
  const isSuperAdmin = useIsSuperAdmin()

  /** L1：能不能看到「總覽」選單與合併行事曆。 */
  const canViewOverview = computed(() => isSuperAdmin.value || hasPermission('calendar.view'))

  /** L2：能不能新增／編輯／刪除自建事件（`calendar.custom_event.create/update/delete`，種子資料
   * 一律三碼一起發，查其中一碼即可代表整組）。 */
  const canManageCustomEvents = computed(() => isSuperAdmin.value || hasPermission('calendar.custom_event.create'))

  /** L2：能不能看到「自建事件」選單與清單（含唯讀）。 */
  const canViewCustomEvents = computed(
    () => isSuperAdmin.value || canManageCustomEvents.value || hasPermission('calendar.custom_event.view'),
  )

  return {
    canViewOverview,
    canViewCustomEvents,
    canManageCustomEvents,
  }
}
