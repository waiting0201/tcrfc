/** 內容狀態四態（docs/21-admin-ui.md §4）。不是每個模組都用得到全部四態。 */
export type ContentStatus = 'draft' | 'scheduled' | 'published' | 'disabled'

export interface StatusMeta {
  status: ContentStatus
  /** 已發布：發布時間；排程發布：預計發布時間；已停用：下架時間 */
  statusAt?: string
  /** 已停用時的操作者（稽核紀錄若有記錄才顯示，docs/21 §4） */
  statusBy?: string
}

/** 雙語文字欄位，en 可為空但欄位必須存在（CLAUDE.md 全域規定第 4 條） */
export interface Bilingual {
  zh: string
  en: string
}

/** 目前登入帳號可操作的俱樂部（站台切換器用，docs/21 §5） */
export interface ClubOption {
  id: string
  name: string
  /** 16px 隊徽小圖示的顏色代表色，mock 用色塊代替真的隊徽圖檔 */
  markColor: string
}

export interface CurrentUser {
  name: string
  /** 目前帳號被授權的俱樂部；只有一個時站台切換器不顯示下拉箭頭（docs/21 §5） */
  authorizedClubs: ClubOption[]
}
