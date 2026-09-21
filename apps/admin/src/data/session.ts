import type { CurrentUser } from '@/types/common'

/**
 * 假資料：目前登入帳號與可操作俱樂部（docs/21-admin-ui.md §5 站台切換器）。
 * 改這裡的 `authorizedClubs` 陣列長度可以測試「只授權一個俱樂部時切換器變成純文字標籤」的行為
 * ——只留一筆就會觸發那個分支。
 */
export const CURRENT_USER: CurrentUser = {
  name: '王小明',
  authorizedClubs: [
    { id: 'tcrfc', name: '台中磐石', markColor: '#E0218A' },
    { id: 'bw', name: '台中藍鯨', markColor: '#2196D5' },
  ],
}
