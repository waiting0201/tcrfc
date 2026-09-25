import { computed, reactive, ref } from 'vue'
import { getMe } from '@/api/adminAuth'
import { setDisplayName } from '@/auth/session'
import type { AdminMePermissionDto, AdminMeRoleDto } from '@/api/adminAuth'
import tcrfcCrest from '@/assets/brand/tcrfc-mark-pink.svg'
import bwCrest from '@/assets/brand/bw-crest-48.png'

/**
 * 站台切換器可切換的俱樂部清單。
 *
 * ✅ **已改接 `GET /api/v1/admin/auth/me`（S1-4 續作補上，apps/api/README.md「前端回報缺口①」）**——
 * 取代 2026-09-24 之前用公開 `GET /api/v1/clubs` 頂著的暫時作法（那個做法只能列出系統裡「有哪些
 * 俱樂部」，不是「這個帳號被授權哪些俱樂部」，見 git 歷史）。現在**只列出這個帳號目前有效
 * （未過期、未撤銷）的俱樂部授權**（規劃書 §4.0「站台切換器只列出該帳號已授權且未到期的俱樂部」），
 * 系統管理員例外——後端固定回傳「全部啟用中的俱樂部」（`MeResponse` 的資料來源不是
 * `AdminUserClub`，見 apps/api/README.md 該節說明），對系統管理員而言效果等同於「全部都算被授權」。
 * 同一次呼叫也把姓名（`displayName`）、角色（`roles`）與**權限碼清單**（`permissions`，S1-10
 * 修正新增）帶回來，姓名寫回 `@/auth/session` 供 `UserMenu.vue` 顯示；角色代碼存進
 * `currentRoleCodes`（僅供顯示用途）；權限碼存進 `currentPermissionCodes`，是
 * `@/composables/useRolePermissions` 的 `hasPermission()` 唯一的資料來源——P1–P3／G1–G2／
 * L1–L2 的操作可視性判斷從此直接查這份清單，**不再需要每個模組各自維護一份「角色→操作」
 * 對照表**（原本的做法要跟 `db/seed/generate-club-seed-sql.py` 的 `ROLE_PERMISSIONS` 手動同步，
 * 是 `docs/18-work-errors.md` E-39 同類風險，已在 apps/api/README.md「S1-10 修正」根治）。同一次
 * 呼叫的 `adminUserId` 存進 `currentAdminUserId`（S1-10 起新增）——G2 詢問收件匣「指派給我自己」
 * 用得到，見 `EnquiryEditView.vue`。
 *
 * ⚠️ **切換器仍然只是介面便利，不是安全邊界**（docs/21-admin-ui.md §5）：真正的範圍檢查一律由
 * 後端 `AdminClubAuthorizer` 在每一次俱樂部範圍請求時即時判斷。這裡列出的清單現在雖然已經是
 * 「這個帳號被授權的俱樂部」，但仍然不能拿它取代後端的即時檢查——例如授權在切換器載入之後、
 * 下一次操作之前被撤銷的情況，一樣要靠後端擋下，不是靠前端清單「本來就是對的」。
 */
export interface ClubOption {
  code: string
  name: string
  crestUrl: string
}

const FALLBACK_CREST: Record<string, string> = {
  tcrfc: tcrfcCrest,
  bw: bwCrest,
}

function resolveCrest(code: string): string {
  return FALLBACK_CREST[code] ?? tcrfcCrest
}

interface ClubAccessState {
  clubs: ClubOption[]
  loaded: boolean
  loading: boolean
}

const state = reactive<ClubAccessState>({ clubs: [], loaded: false, loading: false })

export const availableClubs = computed(() => state.clubs)

/** 目前登入者的角色代碼（`GET /auth/me` 的 `roles[].code`）。載入完成前是空陣列。**S1-10 起
 * 這份清單不再是各模組判斷「能不能做某件事」的依據**（見下方 `currentPermissionCodes`）——角色
 * 代碼本身只用來顯示「這個帳號是什麼角色」一類的資訊，不用來反推權限。 */
const roles = ref<AdminMeRoleDto[]>([])
export const currentRoleCodes = computed(() => roles.value.map((r) => r.code))

/** 目前登入者實際持有的權限碼清單（`GET /auth/me` 的 `permissions[].code`，S1-10 修正新增）——
 * 取代原本每個模組各自手寫「角色→操作」對照表的做法（`useRolePermissions.ts` 的 `hasPermission`
 * 讀這份清單）。載入完成前是空集合，讀取的畫面一律採取「保守預設不顯示」，等
 * `ensureClubsLoaded()` 解析完成後會自動反應更新。 */
const permissions = ref<AdminMePermissionDto[]>([])
export const currentPermissionCodes = computed(() => new Set(permissions.value.map((p) => p.code)))

/** 目前登入者的 `AdminUser.id`（`GET /auth/me` 的 `adminUserId`）。載入完成前是 `null`。 */
const adminUserId = ref<string | null>(null)
export const currentAdminUserId = computed(() => adminUserId.value)

const internalActiveClubId = ref('tcrfc')

/** 目前站台切換器選到的俱樂部代碼，模組層級單例、跨元件共用（沿用改版前 `data/activeClub.ts`
 * 的既有設計理由：後台各種俱樂部範圍端點都要知道「現在選的是哪一隊」）。 */
export const activeClubId = computed({
  get: () => internalActiveClubId.value,
  set: (value: string) => {
    internalActiveClubId.value = value
  },
})

export async function ensureClubsLoaded(force = false): Promise<void> {
  if (state.loading) return
  if (state.loaded && !force) return
  state.loading = true
  try {
    const me = await getMe()
    setDisplayName(me.displayName)
    roles.value = me.roles
    permissions.value = me.permissions
    adminUserId.value = me.adminUserId
    state.clubs = me.clubGrants.map((g) => ({
      code: g.clubCode,
      name: g.clubNameZh ?? g.clubCode,
      crestUrl: resolveCrest(g.clubCode),
    }))
    state.loaded = true

    const primary = me.clubGrants.find((g) => g.isPrimary)?.clubCode
    const currentStillValid = state.clubs.some((c) => c.code === internalActiveClubId.value)
    if (!currentStillValid) {
      internalActiveClubId.value = primary ?? state.clubs[0]?.code ?? internalActiveClubId.value
    }
  } catch {
    // 讀不到個人檔案就先留空——各頁面既有的「連不上後台服務」錯誤畫面會處理接下來的 API 呼叫失敗，
    // 這裡不重複跳錯誤訊息。
  } finally {
    state.loading = false
  }
}

export function resetClubAccess(): void {
  state.clubs = []
  state.loaded = false
  roles.value = []
  permissions.value = []
  adminUserId.value = null
  internalActiveClubId.value = 'tcrfc'
}
