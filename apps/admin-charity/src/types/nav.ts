export interface NavItem {
  code: string
  label: string
  path: string
  /** 這個模組對應的前台位置，畫面文字比照 docs/22 §3.3「本頁對應前台：○○○」 */
  frontendUnit: string
  /**
   * 顯示這個選單項目所需的權限碼（任一個即可）。權限碼只用來決定顯示或隱藏，不顯示在介面上；
   * 真正的授權一律由後端判斷。
   */
  anyOf: string[]
}
