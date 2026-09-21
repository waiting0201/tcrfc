/**
 * 側欄導覽的模組定義（docs/21-admin-ui.md §1）：14 個一級模組（依字母代號），
 * 有子模組的（如 B 內容管理轄下 B1–B6）用 `el-sub-menu` 手風琴展開；
 * 沒有子模組的（如 A 儀表板）直接是可點擊的葉節點。
 *
 * ⚠️ `code` 只作程式內部識別（路由、開發溝通），畫面樣板一律只輸出 `label`（已經是中文）。
 */
export interface NavChild {
  /** 子模組代號，如 B2——只作內部識別，不得顯示在畫面上 */
  code: string
  label: string
  path: string
  /** 本階段是否已經真的做出功能（false＝顯示「尚未建置」佔位頁，不是死連結） */
  implemented: boolean
}

export interface NavModule {
  /** 一級模組代號，如 B——只作內部識別，不得顯示在畫面上 */
  code: string
  label: string
  /** 有子模組時渲染為 el-sub-menu；沒有時必須提供 path／implemented，渲染為葉節點 */
  children?: NavChild[]
  path?: string
  implemented?: boolean
}

export interface NavGroup {
  /** 側欄視覺分組標題（docs/21 §1，六組），純視覺輔助不是重新切模組 */
  groupLabel: string
  modules: NavModule[]
}
