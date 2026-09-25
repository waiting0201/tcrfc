import { computed } from 'vue'
import { authUser } from '@/auth/session'
import { currentPermissionCodes } from '@/auth/clubAccess'

/**
 * 跨模組共用的權限判斷基礎元件。
 *
 * 🔴 **S1-10 修正（2026-09-25）改讀 `GET /auth/me` 回傳的權限碼清單，取代原本的「角色→操作」
 * 手寫對照表**：`useProgramPermissions.ts`（P1–P3）／`useFormsPermissions.ts`（G1–G2）原本各自
 * 宣告一份 `FULL_ACCESS_ROLES`／`INBOX_ROLES` 之類的角色集合常數，逐字對照
 * `db/seed/generate-club-seed-sql.py` 的 `ROLE_PERMISSIONS`——後端矩陣調整時前端不會自動反映，
 * 是 `docs/18-work-errors.md` **E-39 同類風險**（已在 P1–P3／G1–G2 兩輪各發生一次）。後端已在
 * `/auth/me` 新增 `permissions: {code, scopeTypes}[]`（這個帳號實際持有的全部權限碼），根治這個
 * 落差：各模組的 `useXxxPermissions.ts` 現在直接查「有沒有這個權限碼」，不需要再知道「哪些角色
 * 有這個權限」。
 *
 * 🔴 **這份清單跟「目前選取的俱樂部」無關**（見 `AdminMeResponse.permissions` 檔頭）——角色與
 * 角色的權限指派都沒有 `club_id` 維度，一個人對某個權限碼持有哪些 `scope_type` 不會因為切換站台
 * 而改變；真正決定「這個人能不能碰這個俱樂部」的是既有的 `ClubGrants`（站台切換器只列得出來的
 * 俱樂部）。`hasPermission()` 因此只回答「這個帳號有沒有這個權限碼」，不是「在目前這個俱樂部
 * 有沒有」——這裡的用途全部是「要不要顯示這個按鈕／選單」，真正的俱樂部範圍檢查一律由後端在
 * 每一次請求時即時判斷，就算前端這裡誤判也不會繞過後端的把關。
 *
 * 🔴 這裡的結果一律**不是安全邊界**——真正的授權判斷永遠是後端每一支端點的權限碼檢查
 * （`IAdminClubAuthorizer`），就算這裡誤判成看得到，送出請求一樣會被後端 403 擋下。
 */
export function useIsSuperAdmin() {
  return computed(() => authUser.value?.isSuperAdmin ?? false)
}

/** 目前登入者是不是持有這個權限碼——系統管理員一律視為持有全部權限碼（後端 `/auth/me` 本身也是
 * 這樣回傳的，這裡再判斷一次 `isSuperAdmin` 只是避免依賴「後端一定有照實填好整份清單」這個假設）。 */
export function hasPermission(code: string): boolean {
  return (authUser.value?.isSuperAdmin ?? false) || currentPermissionCodes.value.has(code)
}

/** 持有給定權限碼清單中任一個。 */
export function hasAnyPermission(...codes: string[]): boolean {
  return codes.some((code) => hasPermission(code))
}
