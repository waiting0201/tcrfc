/**
 * 舊的「假身分切換」已移除，改接真登入（見 @/auth/session）。本檔只保留 N4–N7 示範畫面（仍用假資料）
 * 沿用的兩個介面：`hasPermission()` 與 `currentUser`，兩者都轉接到真實登入者。
 */
import { computed } from 'vue'
import { authUser, hasPermission } from '@/auth/session'

export const currentUser = computed(() => ({
  username: authUser.value?.username ?? '',
  displayName: authUser.value?.displayName ?? '',
}))

export { hasPermission }
