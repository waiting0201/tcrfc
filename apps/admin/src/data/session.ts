import type { CurrentUser } from '@/types/common'
import tcrfcCrest from '@/assets/brand/tcrfc-mark-pink.svg'
import bwCrest from '@/assets/brand/bw-crest-48.png'

/**
 * 假資料：目前登入帳號與可操作俱樂部（docs/21-admin-ui.md §5 站台切換器）。
 * 改這裡的 `authorizedClubs` 陣列長度可以測試「只授權一個俱樂部時切換器變成純文字標籤」的行為
 * ——只留一筆就會觸發那個分支。
 *
 * `crestUrl`（v3 起改用真實隊徽圖像，見 docs/21 §5.3）：
 * - 台中磐石：`brand/svg/tcrfc-mark-pink.svg`（`.ai` 主檔萃取，唯一來源見 docs/14-invariants.md）。
 * - 台中藍鯨：`apps/web/public/assets/brand/bw/favicon-48.png`（既有隊徽點陣主檔已裁切縮放好的網頁用
 *   圖示，直接取用；藍鯨目前只有點陣主檔，**不得自行描摹或放大點陣充當向量**，見 docs/13 §4 踩雷點 8）。
 */
export const CURRENT_USER: CurrentUser = {
  name: '王小明',
  authorizedClubs: [
    { id: 'tcrfc', name: '台中磐石', crestUrl: tcrfcCrest },
    { id: 'bw', name: '台中藍鯨', crestUrl: bwCrest },
  ],
}
