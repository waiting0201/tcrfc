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
  /**
   * 16px 隊徽小圖示（docs/21-admin-ui.md §5.3，v3 起改用真實隊徽圖像，不是純色色塊）：
   * 辨識來源是圖形本身，不受 --admin-primary 系 token 支配，操作主色改品牌桃紅後
   * 仍能維持「這是哪一隊」的獨立辨識度。
   */
  crestUrl: string
}

export interface CurrentUser {
  name: string
  /** 目前帳號被授權的俱樂部；只有一個時站台切換器不顯示下拉箭頭（docs/21 §5） */
  authorizedClubs: ClubOption[]
}
