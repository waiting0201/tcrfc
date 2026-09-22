export interface NavItem {
  code: string
  label: string
  path: string
  /** 這個模組對應的前台位置，畫面文字比照 docs/22 §3.3「本頁對應前台：○○○」 */
  frontendUnit: string
}
