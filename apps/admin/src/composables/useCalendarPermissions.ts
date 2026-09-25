import { computed } from 'vue'
import { useIsSuperAdmin, hasAnyRole } from './useRolePermissions'

/**
 * `L1` 行事曆總覽／`L2` 自建事件的操作可視性，逐字對照 apps/api/README.md「S1-11」
 * 「權限碼與角色指派」（主站規劃書 §6 矩陣「行事曆」欄，十個角色逐列展開）：
 *
 * - 系統管理員：全部 5 碼
 * - `content_editor`（內容編輯）／`pr_media`（公關媒體）：`calendar.view` ＋ `calendar.custom_event.*` 全給
 * - `partner_club_manager`（合作球隊管理）：`calendar.view` ＋ `calendar.custom_event.*`
 *   （`scope_type=own_clubs`，效果等同 `all`，見 `docs/14-invariants.md`）
 * - `business_sponsorship`（商務／贊助）／`customer_service_admin`（客服／行政）／`viewer`（檢視者）：
 *   `calendar.view` ＋ `calendar.custom_event.view`（唯讀）
 * - `team_competition`（競技／球隊管理）／`academy_program`（學院／課程管理）：只給 `calendar.view`
 *   ——**看得到 L1 總覽，但完全看不到 L2 自建事件**（矩陣格對應的是「賽事事件」「梯隊賽事」，
 *   已由既有 `team.match.*`／`academy_only` 承接，不是本模組權限碼）
 * - `translator`（翻譯人員）：不指派，L1／L2 都看不到——「僅翻譯欄位」全系統目前沒有任何模組真的
 *   做出欄位級強制，延續既有慣例（比照 P1–P3／G1–G2 對 `translator` 的既有判斷）
 *
 * `isSuperAdmin`／`hasAnyRole` 兩個基礎判斷來自共用的 `useRolePermissions.ts`，已知限制見該檔檔頭
 * （`/auth/me` 不回傳權限碼清單，這裡只影響「要不要顯示」，不是安全邊界）。
 */
const MANAGE_CUSTOM_EVENT_ROLES = new Set(['content_editor', 'pr_media', 'partner_club_manager'])
const VIEW_CUSTOM_EVENT_ONLY_ROLES = new Set(['business_sponsorship', 'customer_service_admin', 'viewer'])
/** 只給 `calendar.view`，看得到 L1 但看不到 L2 自建事件清單。 */
const OVERVIEW_ONLY_ROLES = new Set(['team_competition', 'academy_program'])

export function useCalendarPermissions() {
  const isSuperAdmin = useIsSuperAdmin()

  /** L1：能不能看到「總覽」選單與合併行事曆。 */
  const canViewOverview = computed(
    () => isSuperAdmin.value || hasAnyRole(MANAGE_CUSTOM_EVENT_ROLES, VIEW_CUSTOM_EVENT_ONLY_ROLES, OVERVIEW_ONLY_ROLES),
  )

  /** L2：能不能新增／編輯／刪除自建事件（`calendar.custom_event.create/update/delete`）。 */
  const canManageCustomEvents = computed(() => isSuperAdmin.value || hasAnyRole(MANAGE_CUSTOM_EVENT_ROLES))

  /** L2：能不能看到「自建事件」選單與清單（含唯讀）。`team_competition`／`academy_program`
   * 只拿到 `calendar.view`，這裡刻意不包含它們——矩陣上這兩格對應的是賽事事件／梯隊賽事，
   * 不是自建事件。 */
  const canViewCustomEvents = computed(
    () => isSuperAdmin.value || canManageCustomEvents.value || hasAnyRole(VIEW_CUSTOM_EVENT_ONLY_ROLES),
  )

  return {
    canViewOverview,
    canViewCustomEvents,
    canManageCustomEvents,
  }
}
