import { computed } from 'vue'
import { authUser } from '@/auth/session'
import { currentRoleCodes } from '@/auth/clubAccess'

/**
 * 跨模組共用的角色權限判斷基礎元件。
 *
 * 背景：`useProgramPermissions.ts`（P1–P3，S1-9）原本各自宣告一份 `isSuperAdmin` computed 與
 * `hasAnyRole()` 輔助函式；S1-10（G1／G2）需要同一套判斷邏輯時，沒有理由再複製一份——這裡把
 * 「這個帳號是不是系統管理員」「這個帳號的角色代碼裡有沒有落在指定的角色集合」這兩件每個模組都要
 * 用到的基礎判斷抽出來，各模組自己的 `useXxxPermissions.ts`（如 `useProgramPermissions.ts`、
 * `useFormsPermissions.ts`）只需要宣告「這個模組的角色→操作」對照表本身。
 *
 * 🔴 已知限制（沿用 `useProgramPermissions.ts` 原本的檔頭說明，不因為搬到這裡而改變）：
 * `GET /api/v1/admin/auth/me` 目前只回傳角色代碼（`roles[].code`），不回傳這個帳號實際擁有的
 * 權限碼清單（`AdminMeResponse` 沒有 `permissions` 欄位）。呼叫端的角色→操作對照表因此必須在
 * 前端手動維護一份，跟後端 `db/seed/generate-club-seed-sql.py` 的 `ROLE_PERMISSIONS` 保持一致，
 * 不會自動反映；長期應由 `/auth/me` 直接回傳權限碼清單取代這裡的推導（已在多個模組的 README
 * 段落回報，供之後評估）。
 *
 * 🔴 這裡的結果只用來決定「要不要顯示這個按鈕／這個選單項目」，**不是安全邊界**——真正的授權
 * 判斷一律由後端每一支端點的權限碼檢查（`IAdminClubAuthorizer`）執行，就算這裡誤判成看得到，
 * 送出請求一樣會被後端 403 擋下。
 */
export function useIsSuperAdmin() {
  return computed(() => authUser.value?.isSuperAdmin ?? false)
}

/** 目前登入者的角色代碼是否落在任一個給定的角色集合裡。 */
export function hasAnyRole(...sets: Set<string>[]): boolean {
  const codes = currentRoleCodes.value
  return sets.some((set) => codes.some((code) => set.has(code)))
}
