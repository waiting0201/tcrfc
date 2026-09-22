/**
 * 登入狀態的 mockup（⛔ 不實作任何真的認證邏輯或密碼雜湊——admin_users.password_hash 是
 * 明顯的測試占位字串，後端認證尚未實作，登入頁只是外觀）。
 *
 * 為了讓「退款需要有退款權限的帳號才會看到按鈕」（docs/22 §3.7.3）這類權限相依畫面能被
 * 實際點著看，這裡做成可切換身分：使用者選單裡可以切換成種子資料裡任一個測試帳號，
 * 全站畫面依所選身分的角色權限即時反應。這是本次任務為了展示畫面而做的互動設計，
 * 不是規劃書規格本身。
 */
import { computed, ref } from 'vue'
import { ADMIN_USERS, ROLES } from './fixtures'

const STORAGE_KEY = 'tcrfc-charity-admin-current-user'

const storedUsername = localStorage.getItem(STORAGE_KEY)
const initialUser = ADMIN_USERS.find((u) => u.username === storedUsername) ?? ADMIN_USERS[0]

export const currentUsername = ref(initialUser.username)

export const currentUser = computed(() => ADMIN_USERS.find((u) => u.username === currentUsername.value) ?? ADMIN_USERS[0])

const ALL_PERMISSION_CODES = ROLES.find((r) => r.code === 'system_admin')?.permissionCodes ?? []

export const currentPermissions = computed<Set<string>>(() => {
  const user = currentUser.value
  if (user.isSuperAdmin) return new Set(ALL_PERMISSION_CODES)
  const role = ROLES.find((r) => r.code === user.roleCode)
  return new Set(role?.permissionCodes ?? [])
})

export function hasPermission(code: string): boolean {
  return currentPermissions.value.has(code)
}

export function switchUser(username: string) {
  currentUsername.value = username
  localStorage.setItem(STORAGE_KEY, username)
}

export { ADMIN_USERS }
